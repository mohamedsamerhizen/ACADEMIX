using System.Security.Claims;
using Academix.Api.DTOs.Lifecycle;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Lifecycle;
public sealed partial class LifecycleService
{
    public Task<GraduationView> ReassessCredential(ClaimsPrincipal user,Guid inst,Guid id,string reason,CancellationToken ct)
        =>retry.ExecuteAsync<LifecycleService,GraduationView>(s=>s.ReassessCredentialCore(user,inst,id,reason,ct),ct);
    private async Task<GraduationView> ReassessCredentialCore(ClaimsPrincipal user,Guid inst,Guid id,string reason,CancellationToken ct)
    {
        reason=LifecyclePolicy.Description(reason,5,500);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,inst,ct);var actor=await security.RequireAdminAsync(user,inst,ct);
        var row=await db.GraduationApplications.SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==inst&&x.Status=="Issued",ct)??throw ApiFault.NotFound();
        LifecyclePolicy.DisallowSelfApproval(actor.Id,row.StudentId);
        var audit=await curriculum.Audit(user,row.StudentId,ct);if(!audit.GraduationReady||audit.PlanId!=row.PlanId)throw ApiFault.Conflict("Current academic records do not support the issued credential.");
        row.SourceInvalidatedAtUtc=null;row.Version++;Log(inst,row.StudentId,actor.Id,"graduation",id,"reassessed",reason);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Graduation(row);
    }
}
