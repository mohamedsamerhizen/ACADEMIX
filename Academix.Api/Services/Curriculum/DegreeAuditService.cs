using System.Data;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;
using Academix.Api.Services.Advising;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Registrar;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Curriculum;

public sealed class DegreeAuditService(AcademixDbContext db,AcademicProgressAccess access,RetryingServiceWork retry)
{
    public Task<DegreeAuditView> Evaluate(ClaimsPrincipal user,Guid student,CancellationToken ct)
        =>db.Database.CurrentTransaction is not null?EvaluateCore(user,student,ct):
            retry.ExecuteAsync<DegreeAuditService,DegreeAuditView>(s=>s.EvaluateCore(user,student,ct),ct);

    private async Task<DegreeAuditView> EvaluateCore(ClaimsPrincipal user,Guid student,CancellationToken ct)
    {
        await using var tx=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct):null;
        var (inst,_)=await access.Require(user,student,false,ct);
        var assignment=await db.StudentCurriculumAssignments.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct)??throw ApiFault.NotFound();
        var plan=await db.CurriculumPlans.AsNoTracking().SingleAsync(x=>x.Id==assignment.PlanId&&x.InstitutionId==inst,ct);
        await AcademicFreshness.Ensure(db,inst,student,ct);
        var courses=await db.CurriculumCourses.AsNoTracking().Where(x=>x.PlanId==plan.Id&&x.InstitutionId==inst)
            .Join(db.Courses,x=>x.CourseId,x=>x.Id,(c,x)=>new CurriculumCourseView(c.CourseId,x.Code,x.Title,c.RequirementKind,c.CreditHours,c.RecommendedTerm)).ToListAsync(ct);
        var bands=await db.CurriculumGradeBands.AsNoTracking().Where(x=>x.PlanId==plan.Id&&x.InstitutionId==inst).Select(x=>new CurriculumBandInput(x.MinimumPercentage,x.GradePoints,x.Letter)).ToListAsync(ct);
        var attempts=await db.StudentTranscriptEntries.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student)
            .Select(x=>new TranscriptAttempt(x.CourseId,x.TermId,x.CourseCode,x.Percentage,x.Passed)).ToListAsync(ct);
        var recognition=new List<CreditRecognition>();
        var active=await new AcademicPolicyEngine(db).Active(inst,student,ct);
        if(active.Rules.AllowEquivalency)
        {
            var transfers=await (from line in db.TransferCreditLines.AsNoTracking() join petition in db.TransferCreditPetitions.AsNoTracking() on line.PetitionId equals petition.Id
                where petition.InstitutionId==inst&&line.InstitutionId==inst&&petition.StudentId==student&&petition.ProgramId==plan.ProgramId&&petition.Status=="Approved"
                select new{line.TargetCourseId,petition.OriginInstitution,line.OriginCourseCode}).ToListAsync(ct);
            recognition.AddRange(transfers.Select(x=>new CreditRecognition(x.TargetCourseId,"transfer:"+x.OriginInstitution.Trim().ToUpperInvariant()+":"+x.OriginCourseCode.Trim().ToUpperInvariant())));
            var transitions=await RecognitionQualification.Load(db,inst,student,plan.Id,ct);
            recognition.AddRange(transitions.Select(x=>new CreditRecognition(x.Credit.TargetCourseId,x.Source)));
        }
        var exceptions=await db.Set<AcademicRequirementException>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student&&x.PlanId==plan.Id&&x.Status!="Rejected")
            .Select(x=>new DegreeException(x.TargetCourseId,x.SourceCourseId,x.Kind,x.Status)).ToListAsync(ct);
        var enrollment=await (from e in db.StudentEnrollments.AsNoTracking() join s in db.CourseSections.AsNoTracking() on e.SectionId equals s.Id
            join t in db.AcademicTerms.AsNoTracking() on s.TermId equals t.Id
            where e.InstitutionId==inst&&s.InstitutionId==inst&&t.InstitutionId==inst&&e.StudentId==student&&e.Status=="Active"&&t.Status=="Active"
            select new{s.CourseId,s.TermId}).Distinct().ToListAsync(ct);
        var groups=await db.Set<CurriculumElectiveGroup>().AsNoTracking().Where(x=>x.PlanId==plan.Id&&x.InstitutionId==inst).ToListAsync(ct);
        var members=await db.Set<CurriculumElectiveGroupCourse>().AsNoTracking().Where(x=>x.PlanId==plan.Id&&x.InstitutionId==inst).ToListAsync(ct);
        var ids=courses.Select(x=>x.CourseId).ToArray();
        var prerequisites=await db.CoursePrerequisites.AsNoTracking().Where(x=>x.InstitutionId==inst&&ids.Contains(x.CourseId))
            .Select(x=>new DegreePrerequisite(x.CourseId,x.RequiredCourseId,x.MinimumPercentage)).ToListAsync(ct);
        var policy=await db.Set<AcademicAdvisingPolicy>().AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst,ct)??new(){InstitutionId=inst,Version=0};
        var conditions=new List<AuditRequirement>();
        if(await db.StudentAcademicHolds.AnyAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.ReleasedAtUtc==null,ct))
            conditions.Add(new("ActiveAcademicHold","Unsatisfied",0,0,0,"An active academic hold must be released by an authorized administrator."));
        var membership=await db.StudentProgramMemberships.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct);
        if(plan.Status!="Published"||membership?.ProgramId!=plan.ProgramId||membership.Status is not ("Active" or "Completed"))
            conditions.Add(new("CurriculumMembership","Unsatisfied",1,0,0,"Published assigned curriculum must match the student's active or completed program."));
        if(policy.RequireGoodStanding&&membership is not null)
        {
            var standing=await db.StudentTermStandings.AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student&&x.TermId==membership!.CurrentTermId).SingleOrDefaultAsync(ct);
            var stale=standing is not null&&await AcademicFreshness.StandingStale(db,inst,student,standing.TermId,standing.CalculatedAtUtc,ct);
            conditions.Add(new("AcademicStanding",standing is null||standing.Status=="InsufficientData"||stale?"Pending":standing.Status=="GoodStanding"?"Satisfied":"Unsatisfied",1,standing?.Status=="GoodStanding"&&!stale?1:0,0,
                standing is null||stale?"Current-term standing needs calculation from the current academic record.":"Current-term institutional standing: "+standing.Status));
        }
        var result=DegreeAuditEngine.Evaluate(new(student,plan.Id,plan.CatalogYear,plan.Revision,plan.MinimumTotalCredits,plan.MinimumElectiveCredits,plan.MinimumCumulativeGpa,
            courses,bands,attempts,recognition,exceptions,enrollment.Select(x=>x.CourseId).ToHashSet(),
            groups.Select(g=>new ElectiveGroupInput(g.Name,g.MinimumCredits,members.Where(x=>x.GroupId==g.Id).Select(x=>x.CourseId).ToArray())).ToArray(),prerequisites,conditions,policy.RepeatedFailureThreshold,policy.MinimumProgressCredits));
        var study=await db.Set<StudentStudyPlan>().AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.CurriculumPlanId==plan.Id,ct);
        if(study?.ApprovedRevision is int revision)
        {
            var planned=await (from c in db.Set<StudyPlanCourse>().AsNoTracking() join r in db.Set<StudyPlanRevision>() on c.RevisionId equals r.Id
                join t in db.AcademicTerms on c.TermId equals t.Id
                where r.StudyPlanId==study.Id&&r.InstitutionId==inst&&r.Revision==revision&&t.StartsOn<=DateOnly.FromDateTime(DateTime.UtcNow)
                select new{c.CourseId,c.TermId}).ToListAsync(ct);
            var deviations=planned.Where(p=>!result.Courses.Any(c=>c.CourseId==p.CourseId&&c.Status is "Satisfied" or "Waived")&&!enrollment.Any(e=>e.CourseId==p.CourseId&&e.TermId==p.TermId))
                .Select(p=>new ProgressIndicator("PlanDeviation",p.CourseId,"Approved advisory plan scheduled this course in an arrived term, but no completion or active enrollment is recorded."));
            result=result with{Indicators=result.Indicators.Concat(deviations).ToArray()};
        }
        result=result with{AdvisingPolicyVersion=policy.Version,AcademicPolicyId=active.Id};
        if(tx is not null)await tx.CommitAsync(ct);
        return result;
    }
}
