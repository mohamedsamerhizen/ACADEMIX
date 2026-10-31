using System.Security.Claims;
using Academix.Api.Domain.Advising;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Advising;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Curriculum;

public sealed partial class CurriculumService
{
    public Task<IReadOnlyList<ElectiveGroupInput>> ReplaceGroups(ClaimsPrincipal user,Guid planId,ReplaceElectiveGroups input,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,IReadOnlyList<ElectiveGroupInput>>(s=>s.ReplaceGroupsCore(user,planId,input,ct),ct);
    private async Task<IReadOnlyList<ElectiveGroupInput>> ReplaceGroupsCore(ClaimsPrincipal user,Guid planId,ReplaceElectiveGroups input,CancellationToken ct)
    {
        var original=await Plan(planId,ct);await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,original.InstitutionId,ct);
        var actor=await security.RequireAdminAsync(user,original.InstitutionId,ct);
        var plan=(await db.CurriculumPlans.FromSqlInterpolated($"SELECT * FROM curriculum_plans WHERE \"Id\"={planId} FOR UPDATE").ToListAsync(ct)).Single();
        AcademicProgressRules.Version(plan.Version,input.ExpectedVersion);if(plan.Status!="Draft")throw ApiFault.Conflict("Published curriculum groups are immutable; create a new revision.");
        var courses=await db.CurriculumCourses.Where(x=>x.PlanId==planId).Select(x=>new CurriculumCourseView(x.CourseId,"","",x.RequirementKind,x.CreditHours,x.RecommendedTerm)).ToListAsync(ct);
        DegreeAuditEngine.ValidateGroups(input.Groups,courses);
        await db.Set<CurriculumElectiveGroupCourse>().Where(x=>x.PlanId==planId).ExecuteDeleteAsync(ct);
        await db.Set<CurriculumElectiveGroup>().Where(x=>x.PlanId==planId).ExecuteDeleteAsync(ct);
        foreach(var group in input.Groups)
        {
            var row=new CurriculumElectiveGroup{InstitutionId=plan.InstitutionId,PlanId=planId,Name=group.Name.Trim(),MinimumCredits=group.MinimumCredits};db.Add(row);
            db.AddRange(group.CourseIds.Select(x=>new CurriculumElectiveGroupCourse{InstitutionId=plan.InstitutionId,PlanId=planId,GroupId=row.Id,CourseId=x}));
        }
        plan.Version++;security.Audit(actor.Id,plan.InstitutionId,"curriculum.elective-groups.changed",planId.ToString());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return input.Groups;
    }
    public async Task<IReadOnlyList<ElectiveGroupInput>> Groups(ClaimsPrincipal user,Guid student,CancellationToken ct)
    {
        var (assignment,_)=await Assigned(user,student,ct);
        var groups=await db.Set<CurriculumElectiveGroup>().AsNoTracking().Where(x=>x.PlanId==assignment.PlanId&&x.InstitutionId==assignment.InstitutionId).OrderBy(x=>x.Name).ToListAsync(ct);
        var members=await db.Set<CurriculumElectiveGroupCourse>().AsNoTracking().Where(x=>x.PlanId==assignment.PlanId&&x.InstitutionId==assignment.InstitutionId).ToListAsync(ct);
        return groups.Select(g=>new ElectiveGroupInput(g.Name,g.MinimumCredits,members.Where(x=>x.GroupId==g.Id).Select(x=>x.CourseId).Order().ToArray())).ToArray();
    }
    public Task<RequirementExceptionView> RequestException(ClaimsPrincipal user,Guid student,RequirementExceptionInput input,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,RequirementExceptionView>(s=>s.RequestExceptionCore(user,student,input,ct),ct);
    private static RequirementExceptionView ExceptionView(AcademicRequirementException e)=>new(e.Id,e.StudentId,e.PlanId,e.TargetCourseId,e.SourceCourseId,e.Kind,e.Status,e.Version,e.Reason,e.DecisionReason);
    private async Task<RequirementExceptionView> RequestExceptionCore(ClaimsPrincipal user,Guid student,RequirementExceptionInput input,CancellationToken ct)
    {
        var access=new AcademicProgressAccess(db,security);var (inst,_)=await access.Require(user,student,false,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var (_,actor)=await access.Require(user,student,false,ct);
        if(input.Kind is not ("Waiver" or "Substitution")||input.Kind=="Waiver"&&input.SourceCourseId.HasValue||input.Kind=="Substitution"&&(!input.SourceCourseId.HasValue||input.SourceCourseId==input.TargetCourseId))throw ApiFault.Bad("Waivers have no source; substitutions require a different source course.");
        if(!await db.StudentCurriculumAssignments.AnyAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.PlanId==input.PlanId,ct)||
            !await db.CurriculumCourses.AnyAsync(x=>x.InstitutionId==inst&&x.PlanId==input.PlanId&&x.CourseId==input.TargetCourseId,ct)||
            input.SourceCourseId is Guid source&&!await db.Courses.AnyAsync(x=>x.Id==source&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
        if(await db.Set<AcademicRequirementException>().AnyAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.PlanId==input.PlanId&&x.TargetCourseId==input.TargetCourseId&&x.Status!="Rejected",ct))throw ApiFault.Conflict("An active exception already exists for this requirement.");
        var row=new AcademicRequirementException{InstitutionId=inst,StudentId=student,PlanId=input.PlanId,TargetCourseId=input.TargetCourseId,SourceCourseId=input.SourceCourseId,Kind=input.Kind,Reason=AdvisingPolicy.Required(input.Reason,1000,"exception rationale"),RequestedBy=actor};db.Add(row);
        security.Audit(actor,inst,"curriculum.exception.requested",row.Id.ToString());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return ExceptionView(row);
    }
    public Task<RequirementExceptionView> DecideException(ClaimsPrincipal user,Guid id,ReviewRequirementException input,CancellationToken ct)
        =>retry.ExecuteAsync<CurriculumService,RequirementExceptionView>(s=>s.DecideExceptionCore(user,id,input,ct),ct);
    private async Task<RequirementExceptionView> DecideExceptionCore(ClaimsPrincipal user,Guid id,ReviewRequirementException input,CancellationToken ct)
    {
        var initial=await db.Set<AcademicRequirementException>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var actor=await security.RequireAdminAsync(user,initial.InstitutionId,ct);
        if(actor.Id==initial.StudentId||actor.Id==initial.RequestedBy)throw ApiFault.Forbidden();
        var row=await db.Set<AcademicRequirementException>().SingleAsync(x=>x.Id==id,ct);AcademicProgressRules.Version(row.Version,input.ExpectedVersion);
        if(row.Status!="Submitted")throw ApiFault.Conflict("Exception already decided.");
        if(input.Approve)
        {
            if(!await db.StudentCurriculumAssignments.AnyAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==row.InstitutionId&&x.PlanId==row.PlanId,ct))throw ApiFault.Conflict("Assigned curriculum changed.");
            if(row.SourceCourseId is Guid source&&(!await db.StudentTranscriptEntries.AnyAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==row.InstitutionId&&x.CourseId==source&&x.Passed,ct)||
                await db.CurriculumCourses.AnyAsync(x=>x.PlanId==row.PlanId&&x.CourseId==source,ct)||
                await db.Set<AcademicRequirementException>().AnyAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==row.InstitutionId&&x.PlanId==row.PlanId&&x.SourceCourseId==source&&x.Status=="Approved",ct)))
                throw ApiFault.Conflict("Substitution requires an unallocated passed source outside this curriculum; one achievement cannot satisfy two requirements.");
        }
        row.Status=input.Approve?"Approved":"Rejected";row.ReviewedBy=actor.Id;row.DecisionReason=AdvisingPolicy.Required(input.Reason,1000,"decision reason");row.Version++;
        security.Audit(actor.Id,row.InstitutionId,"curriculum.exception."+row.Status.ToLowerInvariant(),id.ToString());
        OutboxService.Record(db,row.InstitutionId,"RequirementExceptionReviewed",id,new{StudentId=row.StudentId,ExceptionId=id,Status=row.Status});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return ExceptionView(row);
    }
    public async Task<PageResult<RequirementExceptionView>> Exceptions(ClaimsPrincipal user,Guid student,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (inst,_)=await new AcademicProgressAccess(db,security).Require(user,student,false,ct);
        var q=db.Set<AcademicRequirementException>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student);
        return new(page,AdvisingPolicy.PageSize,await q.CountAsync(ct),(await q.OrderBy(x=>x.Id).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct)).Select(ExceptionView).ToArray());
    }
}
