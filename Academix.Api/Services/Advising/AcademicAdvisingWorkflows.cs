using System.Security.Claims;
using Academix.Api.Domain.Advising;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Advising;

public sealed partial class AdvisingService
{
    private static AdvisingPolicyView Map(AcademicAdvisingPolicy p)=>new(p.Version,p.MaximumAdvisees,p.RepeatedFailureThreshold,p.MinimumProgressCredits,p.RequireGoodStanding,p.RequireDepartmentReview);
    private static AdvisorAssignmentView Map(AdvisorAssignment a)=>new(a.Id,a.StudentId,a.AdvisorId,a.Reason,a.AssignedAtUtc,a.EndedAtUtc);
    private static StudentSessionView Map(AcademicAdvisingSession s)=>new(s.Id,s.AdvisorId,s.ScheduledAtUtc,s.Status,s.Recommendation,s.FollowUpAtUtc,s.AcknowledgedAtUtc,s.Version);
    public async Task<AdvisingPolicyView> ProgressPolicy(ClaimsPrincipal user,Guid inst,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);
        return Map(await db.Set<AcademicAdvisingPolicy>().AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst,ct)??new(){InstitutionId=inst,Version=0});
    }
    public Task<AdvisingPolicyView> ConfigureProgress(ClaimsPrincipal user,Guid inst,AdvisingPolicyInput input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,AdvisingPolicyView>(s=>s.ConfigureProgressCore(user,inst,input,ct),ct);
    private async Task<AdvisingPolicyView> ConfigureProgressCore(ClaimsPrincipal user,Guid inst,AdvisingPolicyInput input,CancellationToken ct)
    {
        if(input.MaximumAdvisees is <1 or >10000||input.RepeatedFailureThreshold is <2 or >20||input.MinimumProgressCredits is <0 or >500||decimal.Round(input.MinimumProgressCredits,2)!=input.MinimumProgressCredits)
            throw ApiFault.Bad("Invalid advising policy limits.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);
        var p=await db.Set<AcademicAdvisingPolicy>().SingleOrDefaultAsync(x=>x.InstitutionId==inst,ct);
        if((p?.Version??0)!=input.ExpectedVersion)throw ApiFault.Conflict("Advising policy version changed.");
        if(input.MaximumAdvisees is int max&&await db.Set<AdvisorAssignment>().Where(x=>x.InstitutionId==inst&&x.EndedAtUtc==null).GroupBy(x=>x.AdvisorId).AnyAsync(g=>g.Count()>max,ct))
            throw ApiFault.Conflict("Reassign advisees before reducing the configured workload below existing assignments.");
        if(p is null){p=new(){InstitutionId=inst,Version=0};db.Add(p);}
        p.MaximumAdvisees=input.MaximumAdvisees;p.RepeatedFailureThreshold=input.RepeatedFailureThreshold;p.MinimumProgressCredits=input.MinimumProgressCredits;
        p.RequireGoodStanding=input.RequireGoodStanding;p.RequireDepartmentReview=input.RequireDepartmentReview;p.Version++;
        security.Audit(actor.Id,inst,"advising.policy.changed",inst.ToString());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Map(p);
    }
    public Task<AdvisorAssignmentView> AssignAcademicAdvisor(ClaimsPrincipal user,Guid student,AssignAdvisor input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,AdvisorAssignmentView>(s=>s.AssignAcademicAdvisorCore(user,student,input,ct),ct);
    private async Task<AdvisorAssignmentView> AssignAcademicAdvisorCore(ClaimsPrincipal user,Guid student,AssignAdvisor input,CancellationToken ct)
    {
        var (inst,_)=await progressAccess.Require(user,student,true,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);await progressAccess.RequireInstructor(inst,input.AdvisorId,ct);
        if(input.AdvisorId==student)throw ApiFault.Bad("A student cannot advise themselves.");
        var reason=AdvisingPolicy.Required(input.Reason,500,"assignment reason");
        var current=await db.Set<AdvisorAssignment>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.EndedAtUtc==null,ct);
        if(current?.AdvisorId==input.AdvisorId){await tx.CommitAsync(ct);return Map(current);}
        var maximum=await db.Set<AcademicAdvisingPolicy>().Where(x=>x.InstitutionId==inst).Select(x=>x.MaximumAdvisees).SingleOrDefaultAsync(ct);
        if(maximum.HasValue&&await db.Set<AdvisorAssignment>().CountAsync(x=>x.InstitutionId==inst&&x.AdvisorId==input.AdvisorId&&x.EndedAtUtc==null,ct)>=maximum)
            throw ApiFault.Conflict("Advisor workload limit reached.");
        if(current is not null){current.EndedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
        var row=new AdvisorAssignment{InstitutionId=inst,StudentId=student,AdvisorId=input.AdvisorId,AssignedBy=actor.Id,Reason=reason};db.Add(row);
        security.Audit(actor.Id,inst,"advising.advisor.assigned",student.ToString());
        OutboxService.Record(db,inst,"AdvisorAssigned",row.Id,new{StudentId=student,AdvisorId=input.AdvisorId});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Map(row);
    }
    public async Task<PageResult<AdvisorAssignmentView>> AdvisorHistory(ClaimsPrincipal user,Guid student,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (inst,_)=await progressAccess.Require(user,student,false,ct);
        var q=db.Set<AdvisorAssignment>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student);
        return new(page,AdvisingPolicy.PageSize,await q.CountAsync(ct),(await q.OrderByDescending(x=>x.AssignedAtUtc).ThenBy(x=>x.Id).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct)).Select(Map).ToArray());
    }
    public Task EndAdvisorAssignment(ClaimsPrincipal user,Guid student,Guid assignmentId,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService>(s=>s.EndAdvisorAssignmentCore(user,student,assignmentId,ct),ct);
    private async Task EndAdvisorAssignmentCore(ClaimsPrincipal user,Guid student,Guid assignmentId,CancellationToken ct)
    {
        // An administrator can release a workload slot even after the student was
        // deactivated. Address the assignment UUID so a stale request cannot end a replacement.
        var initial=await db.Set<AdvisorAssignment>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==assignmentId&&x.StudentId==student,ct)??throw ApiFault.NotFound();
        await security.RequireAdminAsync(user,initial.InstitutionId,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var actor=await security.RequireAdminAsync(user,initial.InstitutionId,ct);
        var row=await db.Set<AdvisorAssignment>().SingleAsync(x=>x.Id==assignmentId,ct);
        if(row.EndedAtUtc is null)
        {
            row.EndedAtUtc=DateTime.UtcNow;security.Audit(actor.Id,row.InstitutionId,"advising.advisor.ended",row.Id.ToString());
            OutboxService.Record(db,row.InstitutionId,"AdvisorAssignmentEnded",row.Id,new{StudentId=student,AdvisorId=row.AdvisorId,AssignmentId=row.Id});
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
    public async Task<PageResult<AdvisorAssignmentView>> MyAdvisees(ClaimsPrincipal user,Guid inst,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (actor,roles)=await security.CurrentAsync(user,ct);
        var admin=roles.Contains(Academix.Api.Domain.Identity.AcademixRoles.SuperAdministrator)||actor.InstitutionId==inst&&roles.Contains(Academix.Api.Domain.Identity.AcademixRoles.InstitutionAdministrator);
        if(!admin)await progressAccess.RequireInstructor(inst,actor.Id,ct);
        var q=db.Set<AdvisorAssignment>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.EndedAtUtc==null&&(admin||x.AdvisorId==actor.Id));
        return new(page,AdvisingPolicy.PageSize,await q.CountAsync(ct),(await q.OrderBy(x=>x.StudentId).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct)).Select(Map).ToArray());
    }
    public Task<StaffSessionView> ScheduleSession(ClaimsPrincipal user,Guid student,ScheduleAdvisingSession input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,StaffSessionView>(s=>s.ScheduleSessionCore(user,student,input,ct),ct);
    private async Task<StaffSessionView> ScheduleSessionCore(ClaimsPrincipal user,Guid student,ScheduleAdvisingSession input,CancellationToken ct)
    {
        if(input.ClientRequestId==Guid.Empty||input.ScheduledAtUtc.Kind!=DateTimeKind.Utc)throw ApiFault.Bad("Session request ID and UTC schedule are required.");
        var (inst,_)=await progressAccess.Require(user,student,true,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);
        var (_,actor)=await progressAccess.Require(user,student,true,ct);
        var assignment=await db.Set<AdvisorAssignment>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.EndedAtUtc==null,ct)??throw ApiFault.Conflict("Assign an advisor first.");
        await progressAccess.RequireInstructor(inst,assignment.AdvisorId,ct);
        var row=await db.Set<AcademicAdvisingSession>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.ClientRequestId==input.ClientRequestId,ct);
        if(row is not null)
        {
            if(row.ScheduledAtUtc.Ticks / 10 != input.ScheduledAtUtc.Ticks / 10)throw ApiFault.Conflict("Session request ID reused with different schedule.");
            await tx.CommitAsync(ct);return new(Map(row),student,row.InternalNotes);
        }
        row=new(){InstitutionId=inst,StudentId=student,AdvisorId=assignment.AdvisorId,ClientRequestId=input.ClientRequestId,ScheduledAtUtc=input.ScheduledAtUtc};db.Add(row);
        security.Audit(actor,inst,"advising.session.scheduled",row.Id.ToString());
        OutboxService.Record(db,inst,"AdvisingSessionScheduled",row.Id,new{StudentId=student,SessionId=row.Id});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(Map(row),student,row.InternalNotes);
    }
    public Task<StaffSessionView> CompleteSession(ClaimsPrincipal user,Guid id,CompleteAdvisingSession input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,StaffSessionView>(s=>s.CompleteSessionCore(user,id,input,ct),ct);
    private async Task<StaffSessionView> CompleteSessionCore(ClaimsPrincipal user,Guid id,CompleteAdvisingSession input,CancellationToken ct)
    {
        var initial=await db.Set<AcademicAdvisingSession>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var (_,actor)=await progressAccess.Require(user,initial.StudentId,true,ct);
        var row=await db.Set<AcademicAdvisingSession>().SingleAsync(x=>x.Id==id,ct);
        if(row.Status!="Scheduled"||row.Version!=input.ExpectedVersion||input.Status is not ("Completed" or "Cancelled"))throw ApiFault.Conflict("Session changed or transition is invalid.");
        if(input.InternalNotes is null||input.InternalNotes.Length>4000||input.FollowUpAtUtc is DateTime follow&&(follow.Kind!=DateTimeKind.Utc||follow<=DateTime.UtcNow))throw ApiFault.Bad("Valid notes and a future UTC follow-up are required.");
        row.Recommendation=AdvisingPolicy.Required(input.Recommendation,2000,"recommendation or cancellation reason");row.InternalNotes=input.InternalNotes.Trim();
        row.Status=input.Status;row.FollowUpAtUtc=input.FollowUpAtUtc;row.Version++;
        security.Audit(actor,row.InstitutionId,"advising.session."+row.Status.ToLowerInvariant(),id.ToString());
        OutboxService.Record(db,row.InstitutionId,"AdvisingSessionCompleted",id,new{StudentId=row.StudentId,SessionId=id,Status=row.Status});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(Map(row),row.StudentId,row.InternalNotes);
    }
    public Task<StudentSessionView> AcknowledgeSession(ClaimsPrincipal user,Guid id,TaskTransition input,CancellationToken ct)
        =>retry.ExecuteAsync<AdvisingService,StudentSessionView>(s=>s.AcknowledgeSessionCore(user,id,input,ct),ct);
    private async Task<StudentSessionView> AcknowledgeSessionCore(ClaimsPrincipal user,Guid id,TaskTransition input,CancellationToken ct)
    {
        var initial=await db.Set<AcademicAdvisingSession>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var (actor,roles)=await security.CurrentAsync(user,ct);
        if(actor.Id!=initial.StudentId||actor.InstitutionId!=initial.InstitutionId||!roles.Contains(Academix.Api.Domain.Identity.AcademixRoles.Student))throw ApiFault.Forbidden();
        var row=await db.Set<AcademicAdvisingSession>().SingleAsync(x=>x.Id==id,ct);
        if(row.Status!="Completed")throw ApiFault.Conflict("Only a completed session can be acknowledged.");
        if(row.AcknowledgedAtUtc is null)
        {
            if(row.Version!=input.ExpectedVersion)throw ApiFault.Conflict("Session version changed.");
            row.AcknowledgedAtUtc=DateTime.UtcNow;row.Version++;security.Audit(actor.Id,row.InstitutionId,"advising.session.acknowledged",id.ToString());await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);return Map(row);
    }
    public async Task<PageResult<StaffSessionView>> StaffSessions(ClaimsPrincipal user,Guid student,string? status,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (inst,_)=await progressAccess.Require(user,student,true,ct);
        if(status is not null&&status is not ("Scheduled" or "Completed" or "Cancelled"))throw ApiFault.Bad("Invalid session status.");
        var q=db.Set<AcademicAdvisingSession>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==student&&(status==null||x.Status==status));
        return new(page,AdvisingPolicy.PageSize,await q.CountAsync(ct),(await q.OrderByDescending(x=>x.ScheduledAtUtc).ThenBy(x=>x.Id).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct)).Select(x=>new StaffSessionView(Map(x),student,x.InternalNotes)).ToArray());
    }
    public async Task<PageResult<StudentSessionView>> MyAcademicSessions(ClaimsPrincipal user,int page,CancellationToken ct)
    {
        var offset=AdvisingPolicy.Offset(page);var (actor,roles)=await security.CurrentAsync(user,ct);
        if(!roles.Contains(Academix.Api.Domain.Identity.AcademixRoles.Student)||actor.InstitutionId is not Guid inst)throw ApiFault.Forbidden();
        var q=db.Set<AcademicAdvisingSession>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.StudentId==actor.Id);
        return new(page,AdvisingPolicy.PageSize,await q.CountAsync(ct),(await q.OrderByDescending(x=>x.ScheduledAtUtc).ThenBy(x=>x.Id).Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct)).Select(Map).ToArray());
    }
}
