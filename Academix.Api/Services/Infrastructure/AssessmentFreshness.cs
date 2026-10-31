using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Assessment;
using Academix.Api.Middleware;
using Academix.Api.Services.Institutional;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Infrastructure;

/// <summary>Publication evidence and invalidation for assessment-derived official results.
/// The institution lock serializes source changes, publication and reconciliation.</summary>
public static class AssessmentFreshness
{
    public static bool Same(string first,string second)=>DocumentIntegrity.Sign(new byte[32],Guid.Empty,"GRADEBOOK-SOURCE",first)==DocumentIntegrity.Sign(new byte[32],Guid.Empty,"GRADEBOOK-SOURCE",second);
    public static bool Stale(bool marked,string published,string current,bool external)
        =>marked||!(published=="[]"&&!external)&&!Same(published,current);
    public static async Task<Dictionary<Guid,string>> Capture(AcademixDbContext db,Guid inst,Guid section,Guid[] students,CancellationToken ct)
    {
        var components=await db.GradeComponents.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section).OrderBy(x=>x.Id)
            .Select(x=>new{x.Id,x.CategoryId,x.SourceKind,x.SourceId,x.WeightPercentage,x.MaximumPoints}).ToListAsync(ct);
        var manual=await db.GradeManualEntries.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&students.Contains(x.StudentId)).OrderBy(x=>x.Id).ToListAsync(ct);
        var assignments=await db.LearningSubmissions.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&students.Contains(x.StudentId)&&x.Status=="Graded").OrderBy(x=>x.Id).ToListAsync(ct);
        var exams=await db.ExamGradeReports.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&students.Contains(x.StudentId)&&x.Status=="Published").OrderBy(x=>x.Id).ToListAsync(ct);
        var members=await db.ProjectTeamMembers.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&students.Contains(x.StudentId)).OrderBy(x=>x.TeamId).ThenBy(x=>x.StudentId).ToListAsync(ct);
        var teamIds=members.Select(x=>x.TeamId).Distinct().ToArray();
        var groups=await db.GroupProjectSubmissions.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&teamIds.Contains(x.TeamId)&&x.Status=="Graded").OrderBy(x=>x.Id).ToListAsync(ct);
        return students.Distinct().ToDictionary(student=>student,student=>JsonSerializer.Serialize(new{
            Schema="ACADEMIX-GRADEBOOK-SOURCES-V1",Components=components,
            Manual=manual.Where(x=>x.StudentId==student&&components.Any(c=>c.SourceKind=="Manual"&&c.Id==x.ComponentId)).Select(x=>new{x.Id,x.ComponentId,x.Version,x.Points}),
            Assignments=assignments.Where(x=>x.StudentId==student&&components.Any(c=>c.SourceKind=="Assignment"&&c.SourceId==x.AssignmentId)).Select(x=>new{x.Id,x.AssignmentId,x.Revision,x.Score,x.GradedAtUtc}),
            Exams=exams.Where(x=>x.StudentId==student&&components.Any(c=>c.SourceKind=="Exam"&&c.SourceId==x.ExamId)).Select(x=>new{x.Id,x.ExamId,Version=x.ScoreVersion,x.EarnedPoints,x.MaximumPoints}),
            Projects=groups.Where(x=>members.Any(m=>m.StudentId==student&&m.TeamId==x.TeamId)&&components.Any(c=>c.SourceKind=="GroupProject"&&c.SourceId==x.ProjectId)).Select(x=>new{x.Id,x.ProjectId,x.TeamId,x.Version,x.Score,x.GradedAtUtc})}));
    }
    public static async Task Ensure(AcademixDbContext db,Guid inst,Guid? student,CancellationToken ct,Guid? section=null,Guid[]? cohort=null)
    {
        var query=db.GradebookResults.AsNoTracking().Where(x=>x.InstitutionId==inst&&(!student.HasValue||x.StudentId==student)&&(!section.HasValue||x.SectionId==section));
        if(cohort is not null)query=query.Where(x=>cohort.Contains(x.StudentId));
        var rows=await query.ToListAsync(ct);
        foreach(var group in rows.GroupBy(x=>x.SectionId))
        {
            var external=await db.GradeComponents.AnyAsync(x=>x.InstitutionId==inst&&x.SectionId==group.Key&&x.SourceKind!="Manual",ct);
            var capture=await Capture(db,inst,group.Key,group.Select(x=>x.StudentId).ToArray(),ct);
            if(group.Any(x=>Stale(x.RequiresReconciliation,x.SourceEvidenceJson,capture[x.StudentId],external)))
                throw ApiFault.Conflict("Assessment evidence changed. An authorized instructor must reconcile the affected published course result before using current academic records.");
        }
    }
    public static async Task Changed(AcademixDbContext db,Guid inst,Guid section,string kind,Guid source,Guid[] students,Guid actor,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Source invalidation must commit with its assessment change.");
        if(!await db.GradeComponents.AnyAsync(x=>x.InstitutionId==inst&&x.SectionId==section&&x.SourceKind==kind&&x.SourceId==source,ct))return;
        var rows=await db.GradebookResults.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.SectionId==section&&students.Contains(x.StudentId)).ToListAsync(ct);
        foreach(var row in rows)
        {
            // A second correction while already stale does not duplicate invalidation evidence.
            var changed=await db.GradebookResults.Where(x=>x.Id==row.Id&&x.InstitutionId==inst&&!x.RequiresReconciliation)
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RequiresReconciliation,true).SetProperty(x=>x.Version,x=>x.Version+1),ct);
            if(changed>0)db.GradebookAudits.Add(new(){InstitutionId=inst,SectionId=section,StudentId=row.StudentId,
                ActorId=actor,Action="SourceChanged",PreviousPercentage=row.Percentage,NewPercentage=row.Percentage,
                Reason="Published assessment evidence changed; official grade retained pending reconciliation.",EvidenceJson=JsonSerializer.Serialize(new{kind,source,Previous=row.SourceEvidenceJson})});
            await AcademicFreshness.Invalidate(db,inst,row.StudentId,false,ct);
        }
    }
}
