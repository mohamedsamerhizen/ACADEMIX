using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Academix.Api.Domain.Assessment;
using Academix.Api.DTOs.Assessment;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Assessment;

public sealed partial class GradebookService
{
    public static void ReconciliationDecision(string decision,bool manualOverride)
    {
        if(decision is not ("Recalculate" or "Retain"))throw ApiFault.Bad("Reconciliation decision must be Recalculate or Retain.");
        if(decision=="Recalculate"&&manualOverride)throw ApiFault.Conflict("An official manual override must be explicitly retained or changed through the existing justified correction workflow.");
    }
    public Task<GradebookResultView> Reconcile(ClaimsPrincipal principal,Guid sectionId,Guid studentId,ReconcileGradebookResult input,CancellationToken ct)
        =>retry.ExecuteAsync<GradebookService,GradebookResultView>(s=>s.ReconcileCore(principal,sectionId,studentId,input,ct),ct);
    private async Task<GradebookResultView> ReconcileCore(ClaimsPrincipal principal,Guid sectionId,Guid studentId,ReconcileGradebookResult input,CancellationToken ct)
    {
        if(input.RequestId==Guid.Empty)throw ApiFault.Bad("A reconciliation RequestId is required.");
        var reason=Required(input.Reason,1000);var (section,_)=await Staff(principal,sectionId,true,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await RegistrationCoordination.Lock(db,section.InstitutionId,ct);
        var (_,actor)=await Staff(principal,sectionId,true,ct);var policy=await LockPolicy(sectionId,ct);
        if(!policy.PublishedAtUtc.HasValue)throw ApiFault.Conflict("Only published grades can be reconciled.");
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{sectionId,studentId,input}))));
        var prior=await db.GradebookAudits.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==section.InstitutionId&&x.CommandId==input.RequestId,ct);
        if(prior is not null)
        {
            if(prior.CommandFingerprint!=fingerprint)throw ApiFault.Conflict("Reconciliation RequestId was used with different input.");
            await tx.CommitAsync(ct);return JsonSerializer.Deserialize<GradebookResultView>(prior.ResultJson!)!;
        }
        var row=await db.GradebookResults.SingleOrDefaultAsync(x=>x.InstitutionId==section.InstitutionId&&x.SectionId==sectionId&&x.StudentId==studentId,ct)??throw ApiFault.NotFound();
        if(row.Version!=input.ExpectedVersion)throw ApiFault.Conflict("Course result version changed.");
        ReconciliationDecision(input.Decision,row.IsManualOverride);
        var old=row.Percentage;var previousEvidence=row.SourceEvidenceJson;var previousOverride=row.IsManualOverride;
        if(input.Decision=="Recalculate")
        {
            var preview=await Compute(sectionId,ct);var student=preview.Students.SingleOrDefault(x=>x.StudentId==studentId);
            if(student is null||!student.IsComplete||student.Percentage is null)throw ApiFault.Conflict("Current authoritative assessment scores are incomplete; use a justified official correction where appropriate.");
            row.Percentage=student.Percentage.Value;
        }
        else row.IsManualOverride=true;
        row.SourceEvidenceJson=(await AssessmentFreshness.Capture(db,section.InstitutionId,sectionId,[studentId],ct))[studentId];
        row.RequiresReconciliation=false;row.Passed=row.Percentage>=policy.PassingPercentage;row.Letter=GradebookPolicy.Letter(row.Percentage);row.Version++;
        var result=ResultView(row);
        db.GradebookAudits.Add(new(){InstitutionId=section.InstitutionId,SectionId=sectionId,StudentId=studentId,ActorId=actor.Id,
            Action=input.Decision=="Retain"?"ReconciledRetained":"Reconciled",PreviousPercentage=old,NewPercentage=row.Percentage,Reason=reason,
            EvidenceJson=JsonSerializer.Serialize(new{Previous=previousEvidence,Current=row.SourceEvidenceJson,PreviousOverride=previousOverride,ManualOverride=row.IsManualOverride}),
            CommandId=input.RequestId,CommandFingerprint=fingerprint,ResultJson=JsonSerializer.Serialize(result)});
        await AcademicFreshness.Invalidate(db,section.InstitutionId,studentId,false,ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result;
    }
}
