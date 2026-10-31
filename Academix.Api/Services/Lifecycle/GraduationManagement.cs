using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Lifecycle;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Lifecycle;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Advising;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Lifecycle;

public sealed partial class LifecycleService
{
    public Task<GraduationView> ApplyGraduation(ClaimsPrincipal user,CancellationToken ct)=>SubmitGraduation(user,new(null),ct);
    public Task<GraduationView> SubmitGraduation(ClaimsPrincipal user,GraduationSubmission input,CancellationToken ct)
        =>retry.ExecuteAsync<LifecycleService,GraduationView>(s=>s.SubmitGraduationCore(user,input,ct),ct);
    private async Task<GraduationView> SubmitGraduationCore(ClaimsPrincipal principal,GraduationSubmission input,CancellationToken ct)
    {
        var (user,roles)=await security.CurrentAsync(principal,ct);
        if(user.InstitutionId is not Guid inst||!roles.Contains(AcademixRoles.Student))throw ApiFault.Forbidden();
        if(input.ClientRequestId==Guid.Empty)throw ApiFault.Bad("ClientRequestId cannot be empty.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        await Student(principal,inst,ct);
        if(input.ClientRequestId is Guid request)
        {
            var prior=await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==user.Id&&x.ClientRequestId==request,ct);
            if(prior is not null){await tx.CommitAsync(ct);return Graduation(prior);}
        }
        await Academix.Api.Services.Finance.FinanceDebt.RequireClearance(db,inst,user.Id,"Graduation",DateTime.UtcNow,ct);
        if(!await db.StudentCurriculumAssignments.AnyAsync(x=>x.InstitutionId==inst&&x.StudentId==user.Id,ct))
            throw ApiFault.Conflict("An assigned curriculum is required for graduation eligibility.");
        var eligibility=await degreeAudit.Evaluate(principal,user.Id,ct);
        if(eligibility.Eligibility!="Eligible")throw ApiFault.Conflict("Graduation submission requires an eligible authoritative degree audit.");
        var member=await db.StudentProgramMemberships.AsNoTracking().SingleOrDefaultAsync(x=>x.StudentId==user.Id&&x.InstitutionId==inst&&x.Status=="Active",ct)??throw ApiFault.Conflict("Active program membership required.");
        if(await db.GraduationApplications.AnyAsync(x=>x.InstitutionId==inst&&x.StudentId==user.Id&&x.Status!="Rejected",ct))throw ApiFault.Conflict("An active or issued graduation application already exists.");
        var required=await db.Set<AcademicAdvisingPolicy>().Where(x=>x.InstitutionId==inst).Select(x=>x.RequireDepartmentReview).SingleOrDefaultAsync(ct);
        var row=new GraduationApplication{InstitutionId=inst,StudentId=user.Id,ProgramId=member.ProgramId,PlanId=eligibility.Audit.PlanId,
            ClientRequestId=input.ClientRequestId??Guid.NewGuid(),RequiresDepartmentReview=required};db.Add(row);
        Log(inst,user.Id,user.Id,"graduation",row.Id,"submitted","Eligible degree audit validated; official approval remains pending.");
        OutboxService.Record(db,inst,"GraduationSubmitted",row.Id,new{StudentId=user.Id,ApplicationId=row.Id});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Graduation(row);
    }
    private async Task<Guid> DepartmentAuthority(ClaimsPrincipal user,GraduationApplication application,CancellationToken ct)
    {
        var (actor,roles)=await security.CurrentAsync(user,ct);
        if(actor.Id==application.StudentId)throw ApiFault.Forbidden();
        if(roles.Contains(AcademixRoles.SuperAdministrator)||actor.InstitutionId==application.InstitutionId&&roles.Contains(AcademixRoles.InstitutionAdministrator))return actor.Id;
        if(actor.InstitutionId!=application.InstitutionId||!roles.Contains(AcademixRoles.Instructor))throw ApiFault.Forbidden();
        var department=await db.AcademicPrograms.Where(x=>x.Id==application.ProgramId&&x.InstitutionId==application.InstitutionId).Select(x=>x.DepartmentId).SingleAsync(ct);
        if(!await db.Set<DepartmentGraduationReviewer>().AnyAsync(x=>x.InstitutionId==application.InstitutionId&&x.DepartmentId==department&&x.ReviewerId==actor.Id&&x.IsActive,ct))throw ApiFault.Forbidden();
        return actor.Id;
    }
    public Task SetDepartmentReviewer(ClaimsPrincipal user,Guid inst,Guid department,DepartmentReviewerInput input,CancellationToken ct)
        =>retry.ExecuteAsync<LifecycleService>(s=>s.SetDepartmentReviewerCore(user,inst,department,input,ct),ct);
    private async Task SetDepartmentReviewerCore(ClaimsPrincipal user,Guid inst,Guid department,DepartmentReviewerInput input,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);
        if(!await db.Departments.AnyAsync(x=>x.Id==department&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
        var row=await db.Set<DepartmentGraduationReviewer>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.DepartmentId==department&&x.ReviewerId==input.ReviewerId,ct);
        if(input.Active)await new AcademicProgressAccess(db,security).RequireInstructor(inst,input.ReviewerId,ct);
        else if(row is null)throw ApiFault.NotFound();
        if(row is null){row=new(){InstitutionId=inst,DepartmentId=department,ReviewerId=input.ReviewerId};db.Add(row);}row.IsActive=input.Active;
        security.Audit(actor.Id,inst,"graduation.department-authority.changed",row.Id.ToString());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public Task<GraduationView> BeginGraduationReview(ClaimsPrincipal user,Guid id,IssueGraduation input,CancellationToken ct)
        =>retry.ExecuteAsync<LifecycleService,GraduationView>(s=>s.BeginGraduationReviewCore(user,id,input,ct),ct);
    private async Task<GraduationView> BeginGraduationReviewCore(ClaimsPrincipal user,Guid id,IssueGraduation input,CancellationToken ct)
    {
        var initial=await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,initial.InstitutionId,ct);var actor=await DepartmentAuthority(user,initial,ct);
        var row=await db.GraduationApplications.SingleAsync(x=>x.Id==id,ct);
        if(row.Status=="UnderReview"){await tx.CommitAsync(ct);return Graduation(row);}
        AcademicProgressRules.Version(row.Version,input.ExpectedVersion);if(row.Status!="Submitted")throw ApiFault.Conflict("Only a submitted graduation can enter review.");
        row.Status="UnderReview";row.Version++;Log(row.InstitutionId,row.StudentId,actor,"graduation",id,"UnderReview","Department review opened.");
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Graduation(row);
    }
    public Task<GraduationView> DepartmentDecision(ClaimsPrincipal user,Guid id,DepartmentGraduationDecision input,CancellationToken ct)
        =>retry.ExecuteAsync<LifecycleService,GraduationView>(s=>s.DepartmentDecisionCore(user,id,input,ct),ct);
    private async Task<GraduationView> DepartmentDecisionCore(ClaimsPrincipal user,Guid id,DepartmentGraduationDecision input,CancellationToken ct)
    {
        var initial=await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,initial.InstitutionId,ct);var actor=await DepartmentAuthority(user,initial,ct);
        var row=await db.GraduationApplications.SingleAsync(x=>x.Id==id,ct);AcademicProgressRules.Version(row.Version,input.ExpectedVersion);
        if(row.Status!="UnderReview"||row.DepartmentReviewedAtUtc.HasValue)throw ApiFault.Conflict("A department decision requires an undecided application under review.");
        row.DepartmentReviewedById=actor;row.DepartmentReviewedAtUtc=DateTime.UtcNow;row.DepartmentReviewReason=AdvisingPolicy.Required(input.Reason,1000,"department decision reason");
        if(!input.Approve)row.Status="Rejected";row.Version++;
        Log(row.InstitutionId,row.StudentId,actor,"graduation",id,input.Approve?"DepartmentApproved":"DepartmentRejected",row.DepartmentReviewReason);
        OutboxService.Record(db,row.InstitutionId,"GraduationDepartmentReviewed",id,new{StudentId=row.StudentId,ApplicationId=id,Approved=input.Approve});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Graduation(row);
    }
    public async Task<GraduationEvidenceView> GraduationEvidence(ClaimsPrincipal user,Guid id,CancellationToken ct)
    {
        var row=await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        var (actor,roles)=await security.CurrentAsync(user,ct);
        if(actor.Id==row.StudentId&&actor.InstitutionId==row.InstitutionId&&roles.Contains(AcademixRoles.Student))
        {
            // Formal decision evidence is student-readable; it contains no session notes.
        }
        else await DepartmentAuthority(user,row,ct);
        return new(Graduation(row),row.RequiresDepartmentReview,row.DepartmentReviewedById,row.DepartmentReviewedAtUtc,row.DepartmentReviewReason,row.ApprovalEvidenceJson,row.FinalEvidenceJson);
    }
    public async Task<PageResult<GraduationView>> GraduationQueue(ClaimsPrincipal user,Guid inst,string? status,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (actor,roles)=await security.CurrentAsync(user,ct);
        var admin=roles.Contains(AcademixRoles.SuperAdministrator)||actor.InstitutionId==inst&&roles.Contains(AcademixRoles.InstitutionAdministrator);
        if(!admin&&(actor.InstitutionId!=inst||!roles.Contains(AcademixRoles.Instructor)))throw ApiFault.Forbidden();
        if(status is not null&&status is not ("Submitted" or "UnderReview" or "Approved" or "Rejected" or "Issued"))throw ApiFault.Bad("Invalid graduation status.");
        var departments=db.Set<DepartmentGraduationReviewer>().Where(x=>x.InstitutionId==inst&&x.ReviewerId==actor.Id&&x.IsActive).Select(x=>x.DepartmentId);
        var programs=db.AcademicPrograms.Where(x=>x.InstitutionId==inst&&departments.Contains(x.DepartmentId)).Select(x=>x.Id);
        var q=db.GraduationApplications.AsNoTracking().Where(x=>x.InstitutionId==inst&&(admin||programs.Contains(x.ProgramId))&&(status==null||x.Status==status));
        return new(page,AdvisingPolicy.PageSize,await q.CountAsync(ct),(await q.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct)).Select(Graduation).ToArray());
    }
}
