using System.Security.Claims;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Advising;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Advising;

public static class AcademicProgressRules
{
    public static void Version(long actual,long expected)
    {if(actual!=expected)throw ApiFault.Conflict("Academic progress record version changed.");}
    public static void ReviewPlan(string status)
    {if(status!="Proposed")throw ApiFault.Conflict("Only a proposed revision can be reviewed.");}
    public static void AcknowledgePlan(string status)
    {if(status is not ("Approved" or "Acknowledged"))throw ApiFault.Conflict("Only an approved revision can be acknowledged.");}
    public static void GraduationReview(string status,bool required,bool reviewed)
    {if(status is not ("Submitted" or "UnderReview")||required&&!reviewed)throw ApiFault.Conflict("Graduation requires a submitted application and the configured department review.");}
}

public sealed partial class AdvisingService
{
    public Task<StudyPlanView> RevisePlan(ClaimsPrincipal user,Guid student,ReviseStudyPlan input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,StudyPlanView>(s=>s.RevisePlanCore(user,student,input,ct),ct);
    private async Task<StudyPlanView> RevisePlanCore(ClaimsPrincipal user,Guid student,ReviseStudyPlan input,CancellationToken ct)
    {
        var (inst,_)=await progressAccess.Require(user,student,false,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var (_,actor)=await progressAccess.Require(user,student,false,ct);
        var curriculum=await db.StudentCurriculumAssignments.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct)??throw ApiFault.Conflict("An assigned curriculum is required.");
        if(!await db.StudentProgramMemberships.AnyAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.Status=="Active",ct))throw ApiFault.Conflict("An active program membership is required to propose future studies.");
        if(input.Courses is null||input.Courses.Count is <1 or >100||input.Courses.Select(x=>x.CourseId).Distinct().Count()!=input.Courses.Count)
            throw ApiFault.Bad("An advisory plan requires 1-100 distinct courses.");
        var courseIds=input.Courses.Select(x=>x.CourseId).ToArray();var termIds=input.Courses.Select(x=>x.TermId).Distinct().ToArray();
        if(await db.CurriculumCourses.CountAsync(x=>x.InstitutionId==inst&&x.PlanId==curriculum.PlanId&&courseIds.Contains(x.CourseId),ct)!=courseIds.Length||
            await db.AcademicTerms.CountAsync(x=>x.InstitutionId==inst&&termIds.Contains(x.Id)&&x.Status!="Closed"&&x.StartsOn>DateOnly.FromDateTime(DateTime.UtcNow),ct)!=termIds.Length)
            throw ApiFault.Bad("Plan courses must belong to the assigned curriculum and future institutional terms.");
        var rationale=AdvisingPolicy.Required(input.Rationale,1000,"revision rationale");
        var plan=await db.Set<StudentStudyPlan>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct);
        AcademicProgressRules.Version(plan?.Version??0,input.ExpectedVersion);
        if(plan is null){plan=new(){InstitutionId=inst,StudentId=student,CurriculumPlanId=curriculum.PlanId,Version=0};db.Add(plan);}
        if(plan.CurriculumPlanId!=curriculum.PlanId)
        {
            // Keep old revision history but invalidate its approval after a curriculum transition.
            plan.CurriculumPlanId=curriculum.PlanId;plan.ApprovedRevision=null;
        }
        plan.CurrentRevision++;plan.Version++;
        var revision=new StudyPlanRevision{InstitutionId=inst,StudyPlanId=plan.Id,CurriculumPlanId=curriculum.PlanId,Revision=plan.CurrentRevision,Rationale=rationale,CreatedBy=actor};db.Add(revision);
        db.AddRange(input.Courses.Select(x=>new StudyPlanCourse{InstitutionId=inst,RevisionId=revision.Id,CourseId=x.CourseId,TermId=x.TermId}));
        security.Audit(actor,inst,"advising.plan.revised",revision.Id.ToString());
        OutboxService.Record(db,inst,"StudyPlanProposed",revision.Id,new{StudentId=student,RevisionId=revision.Id});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await StudyPlan(user,student,1,ct);
    }
    public Task<StudyPlanView> ReviewPlan(ClaimsPrincipal user,Guid student,ReviewStudyPlan input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,StudyPlanView>(s=>s.ReviewPlanCore(user,student,input,ct),ct);
    private async Task<StudyPlanView> ReviewPlanCore(ClaimsPrincipal user,Guid student,ReviewStudyPlan input,CancellationToken ct)
    {
        var (inst,_)=await progressAccess.Require(user,student,true,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var (_,actor)=await progressAccess.Require(user,student,true,ct);
        if(actor==student)throw ApiFault.Forbidden();
        var plan=await db.Set<StudentStudyPlan>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct)??throw ApiFault.NotFound();
        AcademicProgressRules.Version(plan.Version,input.ExpectedVersion);
        var revision=await db.Set<StudyPlanRevision>().SingleAsync(x=>x.StudyPlanId==plan.Id&&x.Revision==plan.CurrentRevision,ct);AcademicProgressRules.ReviewPlan(revision.Status);
        if(!await db.StudentCurriculumAssignments.AnyAsync(x=>x.StudentId==student&&x.InstitutionId==inst&&x.PlanId==plan.CurriculumPlanId,ct))throw ApiFault.Conflict("Curriculum changed; submit a new study plan revision.");
        revision.Status=input.Approve?"Approved":"Rejected";revision.ReviewedBy=actor;revision.Recommendation=AdvisingPolicy.Required(input.Recommendation,2000,"advisor recommendation");
        if(input.Approve)plan.ApprovedRevision=revision.Revision;plan.Version++;
        security.Audit(actor,inst,"advising.plan."+revision.Status.ToLowerInvariant(),revision.Id.ToString());
        OutboxService.Record(db,inst,"StudyPlanReviewed",revision.Id,new{StudentId=student,RevisionId=revision.Id,Status=revision.Status});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await StudyPlan(user,student,1,ct);
    }
    public Task<StudyPlanView> AcknowledgePlan(ClaimsPrincipal user,Guid student,TaskTransition input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,StudyPlanView>(s=>s.AcknowledgePlanCore(user,student,input,ct),ct);
    private async Task<StudyPlanView> AcknowledgePlanCore(ClaimsPrincipal user,Guid student,TaskTransition input,CancellationToken ct)
    {
        var (inst,_)=await progressAccess.Require(user,student,false,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var (actor,roles)=await security.CurrentAsync(user,ct);
        if(actor.Id!=student||actor.InstitutionId!=inst||!roles.Contains(AcademixRoles.Student))throw ApiFault.Forbidden();
        var plan=await db.Set<StudentStudyPlan>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct)??throw ApiFault.NotFound();
        if(!await db.StudentCurriculumAssignments.AnyAsync(x=>x.StudentId==student&&x.InstitutionId==inst&&x.PlanId==plan.CurriculumPlanId,ct))throw ApiFault.Conflict("Curriculum changed; submit a new revision.");
        var revision=await db.Set<StudyPlanRevision>().SingleAsync(x=>x.StudyPlanId==plan.Id&&x.Revision==plan.CurrentRevision,ct);AcademicProgressRules.AcknowledgePlan(revision.Status);
        if(revision.Status!="Acknowledged")
        {AcademicProgressRules.Version(plan.Version,input.ExpectedVersion);revision.Status="Acknowledged";revision.AcknowledgedAtUtc=DateTime.UtcNow;plan.Version++;
            security.Audit(actor.Id,inst,"advising.plan.acknowledged",revision.Id.ToString());await db.SaveChangesAsync(ct);}
        await tx.CommitAsync(ct);return await StudyPlan(user,student,1,ct);
    }
    public async Task<StudyPlanView> StudyPlan(ClaimsPrincipal user,Guid student,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (inst,_)=await progressAccess.Require(user,student,false,ct);
        var p=await db.Set<StudentStudyPlan>().AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student,ct)??throw ApiFault.NotFound();
        var revisions=await db.Set<StudyPlanRevision>().AsNoTracking().Where(x=>x.StudyPlanId==p.Id&&x.InstitutionId==inst).OrderByDescending(x=>x.Revision).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct);
        var ids=revisions.Select(x=>x.Id).ToArray();var courses=await db.Set<StudyPlanCourse>().AsNoTracking().Where(x=>x.InstitutionId==inst&&ids.Contains(x.RevisionId)).OrderBy(x=>x.TermId).ThenBy(x=>x.CourseId).ToListAsync(ct);
        return new(p.Id,student,p.CurriculumPlanId,p.Version,p.CurrentRevision,p.ApprovedRevision,revisions.Select(r=>new StudyPlanRevisionView(r.Id,r.CurriculumPlanId,r.Revision,r.Status,r.Rationale,r.Recommendation,r.CreatedBy,r.ReviewedBy,r.CreatedAtUtc,r.AcknowledgedAtUtc,
            courses.Where(x=>x.RevisionId==r.Id).Select(x=>new PlannedCourseInput(x.CourseId,x.TermId)).ToArray())).ToArray());
    }
}
