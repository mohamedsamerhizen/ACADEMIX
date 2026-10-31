using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Registrar;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

public static class AttendanceRecordMutation
{
    public static void Apply(AttendanceRecord record,string status,string note,Guid actor,DateTime now)
    {record.Status=AcademicOperationsPolicy.AttendanceStatus(status);record.Note=note;record.RecordedBy=actor;record.RecordedAtUtc=now;}
}
public static class AcademicRequestExecutionRules
{
    public static void Validate(string kind,RequestExecutionData? input)
    {
        if(kind=="General"){if(input is not null)throw ApiFault.Bad("General requests have no automatic academic effect.");return;}
        if(input is null)throw ApiFault.Bad("Typed execution data required before review.");
        if(kind=="AttendanceExcuse"&&(input.SessionId is null||input.SessionId==Guid.Empty||input.PolicyId.HasValue||input.ExceptionRule is not null||input.ExpiresAtUtc.HasValue))throw ApiFault.Bad("Attendance excuse requires only a session reference.");
        if(kind=="EnrollmentException"&&(input.SessionId.HasValue||input.PolicyId is null||input.PolicyId==Guid.Empty||input.ExceptionRule is not("Prerequisites"or"CreditLoad"or"EnrollmentApproval")||input.ExpiresAtUtc is not DateTime expires||expires.Kind!=DateTimeKind.Utc||expires<=DateTime.UtcNow))throw ApiFault.Bad("A scoped academic exception and future UTC expiry are required.");
    }
}
public sealed partial class RegistrarOperationsService
{
    public Task<AcademicRequestView> BeginReview(ClaimsPrincipal user,Guid inst,Guid id,RequestExecutionData? data,CancellationToken ct)
        =>retry.ExecuteAsync<RegistrarOperationsService,AcademicRequestView>(s=>s.BeginReviewCore(user,inst,id,data,ct),ct);
    private async Task<AcademicRequestView> BeginReviewCore(ClaimsPrincipal user,Guid inst,Guid id,RequestExecutionData? data,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var actor=await security.RequireAdminAsync(user,inst,ct);
        var row=(await db.StudentAcademicRequests.FromSqlInterpolated($"SELECT * FROM student_academic_requests WHERE \"Id\"={id} AND \"InstitutionId\"={inst} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()??throw ApiFault.NotFound();
        var previous=row.Status;
        var repairLegacy=row.Status=="Approved"&&row.Kind!="General"&&row.ExecutionDataJson is null;
        if(row.Status!="Pending"&&!repairLegacy)throw ApiFault.Conflict("Request is no longer submitted.");
        var execution=data??(row.ExecutionDataJson is null?null:JsonSerializer.Deserialize<RequestExecutionData>(row.ExecutionDataJson));
        AcademicRequestExecutionRules.Validate(row.Kind,execution);
        if(row.SectionId is null&&execution?.TargetSectionId is Guid target)row.SectionId=target;
        if(row.SectionId is null&&execution?.SessionId is Guid session)
            row.SectionId=await db.ClassSessions.Where(x=>x.Id==session&&x.InstitutionId==inst).Select(x=>(Guid?)x.SectionId).SingleOrDefaultAsync(ct);
        await ValidateReferences(row,execution,ct);
        if(repairLegacy){row.DecidedBy=null;row.DecidedAtUtc=null;row.DecisionReason="";}
        row.ExecutionDataJson=execution is null?null:JsonSerializer.Serialize(execution);row.Status="UnderReview";row.Version++;
        db.AcademicRequestHistories.Add(new(){InstitutionId=inst,RequestId=id,ActorId=actor.Id,FromStatus=previous,ToStatus="UnderReview",Reason="Typed references validated; a current review decision is required."});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return StudentAcademicOperationsService.View(row);
    }
    private async Task ValidateReferences(StudentAcademicRequest request,RequestExecutionData? data,CancellationToken ct)
    {
        if(request.Kind=="General")return;
        if(request.SectionId is not Guid section||!await db.CourseSections.AnyAsync(x=>x.Id==section&&x.InstitutionId==request.InstitutionId,ct))throw ApiFault.NotFound();
        if(data?.TargetSectionId is Guid target&&target!=section)throw ApiFault.Bad("Execution target does not match the request section.");
        if(request.Kind=="AttendanceExcuse"&&!await db.ClassSessions.AnyAsync(x=>x.Id==data!.SessionId&&x.SectionId==section&&x.InstitutionId==request.InstitutionId,ct))throw ApiFault.NotFound();
        if(request.Kind=="EnrollmentException")
        {
            var active=await new AcademicPolicyEngine(db).Active(request.InstitutionId,request.StudentId,ct);
            if(active.Id!=data!.PolicyId)throw ApiFault.Conflict("Exception must reference the student's current published policy.");
        }
    }
    public Task<AcademicRequestView> Fulfill(ClaimsPrincipal user,Guid inst,Guid id,FulfillAcademicRequest input,CancellationToken ct)
        =>retry.ExecuteAsync<RegistrarOperationsService,AcademicRequestView>(s=>s.FulfillCore(user,inst,id,input,ct),ct);
    private async Task<AcademicRequestView> FulfillCore(ClaimsPrincipal user,Guid inst,Guid id,FulfillAcademicRequest input,CancellationToken ct)
    {
        if(input is null)throw ApiFault.Bad("Expected version required.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var actor=await security.RequireAdminAsync(user,inst,ct);
        var row=(await db.StudentAcademicRequests.FromSqlInterpolated($"SELECT * FROM student_academic_requests WHERE \"Id\"={id} AND \"InstitutionId\"={inst} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()??throw ApiFault.NotFound();
        if(row.Status=="Completed"){await tx.CommitAsync(ct);return StudentAcademicOperationsService.View(row);}
        if(row.Status!="Approved"||row.Version!=input.ExpectedVersion)throw ApiFault.Conflict("Approved current request required.");
        if(actor.Id==row.StudentId)throw ApiFault.Forbidden();
        var data=row.ExecutionDataJson is null?null:JsonSerializer.Deserialize<RequestExecutionData>(row.ExecutionDataJson);
        AcademicRequestExecutionRules.Validate(row.Kind,data);await ValidateReferences(row,data,ct);
        if(row.Kind=="AttendanceExcuse")
        {
            var session=(await db.ClassSessions.FromSqlInterpolated($"SELECT * FROM academic_class_sessions WHERE \"Id\"={data!.SessionId} AND \"InstitutionId\"={inst} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()??throw ApiFault.NotFound();
            if(session.Status=="Cancelled"||session.StartsAtUtc>DateTime.UtcNow)throw ApiFault.Conflict("Only held sessions can be excused.");
            var record=await db.AttendanceRecords.SingleOrDefaultAsync(x=>x.SessionId==session.Id&&x.StudentId==row.StudentId&&x.InstitutionId==inst&&x.SectionId==row.SectionId,ct)??throw ApiFault.Conflict("Attendance must be recorded before an excuse is fulfilled.");
            if(record.Status is not("Absent"or"Late"or"Excused"))throw ApiFault.Conflict("Present attendance cannot be converted to an absence excuse.");
            AttendanceRecordMutation.Apply(record,"Excused","Approved request "+row.Id,actor.Id,DateTime.UtcNow);row.FulfillmentResult="AttendanceExcused:"+record.Id;
        }
        else if(row.Kind=="EnrollmentException")
        {
            var exception=new ApprovedAcademicException{InstitutionId=inst,StudentId=row.StudentId,SectionId=row.SectionId!.Value,PolicyId=data!.PolicyId!.Value,
                Rule=data.ExceptionRule!,Reason=row.DecisionReason,Status="Approved",RequestedBy=row.StudentId,ReviewedBy=actor.Id,ReviewedAtUtc=DateTime.UtcNow,ExpiresAtUtc=data.ExpiresAtUtc!.Value};
            db.ApprovedAcademicExceptions.Add(exception);row.FulfillmentResult="AcademicExceptionApproved:"+exception.Id;
            // This grants precisely the approved rule; Enrollment still evaluates term,
            // capacity, holds, identity, tenant and all rules not explicitly waived.
        }
        else row.FulfillmentResult="GeneralRequestAcknowledged";
        row.Status="Completed";row.FulfilledAtUtc=DateTime.UtcNow;row.FulfilledBy=actor.Id;row.Version++;
        db.AcademicRequestHistories.Add(new(){InstitutionId=inst,RequestId=id,ActorId=actor.Id,FromStatus="Approved",ToStatus="Completed",Reason=row.FulfillmentResult});
        security.Audit(actor.Id,inst,"academic-request.fulfilled",id.ToString());OutboxService.Record(db,inst,"AcademicRequestCompleted",id,new{RequestId=id,row.Kind});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return StudentAcademicOperationsService.View(row);
    }
}
