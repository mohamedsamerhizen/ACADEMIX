using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Records;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Registrar;

public sealed class AcademicPolicyService(AcademixDbContext db, IdentitySecurity security, AcademicPolicyEngine engine,
    RecordsService records, RetryingServiceWork retry)
{
    private static PolicyView View(AcademicPolicyRevision x) => new(x.Id, x.ProgramId, x.Status, x.EffectiveFromUtc, x.EffectiveUntilUtc,
        JsonSerializer.Deserialize<AcademicPolicyRules>(x.RulesJson)!, x.CreatedBy, x.PublishedBy);
    public async Task<PolicyView> Draft(ClaimsPrincipal user, Guid inst, PolicyDraft request, CancellationToken ct)
    {
        var actor = await security.RequireAdminAsync(user, inst, ct);
        AcademicPolicyRulesValidator.Validate(request);
        if (request.ProgramId is Guid program && !await db.AcademicPrograms.AnyAsync(x => x.Id == program && x.InstitutionId == inst && x.IsActive, ct)) throw ApiFault.Bad("Invalid program binding.");
        var row = new AcademicPolicyRevision { InstitutionId = inst, ProgramId = request.ProgramId, CreatedBy = actor.Id,
            RulesJson = JsonSerializer.Serialize(request.Rules), EffectiveFromUtc = request.EffectiveFromUtc, EffectiveUntilUtc = request.EffectiveUntilUtc };
        db.AcademicPolicyRevisions.Add(row);
        security.Audit(actor.Id, inst, "registrar.policy.drafted", row.Id.ToString());
        await db.SaveChangesAsync(ct);
        return View(row);
    }
    public Task<PolicyView> Publish(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<AcademicPolicyService, PolicyView>(s => s.PublishCore(user, inst, id, ct), ct);
    private async Task<PolicyView> PublishCore(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db, inst, ct);
        var actor = await security.RequireAdminAsync(user, inst, ct);
        var row = await db.AcademicPolicyRevisions.SingleOrDefaultAsync(x => x.Id == id && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        if (row.Status == "Published") return View(row);
        if (row.CreatedBy == actor.Id) throw ApiFault.Forbidden();
        if (row.EffectiveUntilUtc <= DateTime.UtcNow) throw ApiFault.Conflict("Policy effective interval already ended.");
        if (await db.AcademicPolicyRevisions.AnyAsync(x => x.InstitutionId == inst && x.ProgramId == row.ProgramId && x.Status == "Published" &&
            x.EffectiveFromUtc < row.EffectiveUntilUtc && row.EffectiveFromUtc < x.EffectiveUntilUtc, ct)) throw ApiFault.Conflict("Conflicting published policy interval.");
        row.Status = "Published"; row.PublishedBy = actor.Id; row.PublishedAtUtc = DateTime.UtcNow;
        security.Audit(actor.Id, inst, "registrar.policy.published", id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return View(row);
    }
    public async Task<IReadOnlyList<PolicyView>> History(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(user, inst, ct);
        return (await db.AcademicPolicyRevisions.AsNoTracking().Where(x => x.InstitutionId == inst).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(Offset(page)).Take(50).ToListAsync(ct)).Select(View).ToArray();
    }
    private static int Offset(int page) => page is > 0 and <= 100000 ? (page - 1) * 50 : throw ApiFault.Bad("Invalid page.");
    public async Task<PolicyDecision> Evaluate(ClaimsPrincipal user, Guid inst, PolicyEvaluation request, CancellationToken ct)
    {
        if (request is null) throw ApiFault.Bad("Evaluation request required.");
        await security.RequireStudentAsync(user, request.StudentId, inst, ct);
        if (!await db.Users.AnyAsync(x => x.Id == request.StudentId && x.InstitutionId == inst, ct)) throw ApiFault.NotFound();
        var baseDecision = await records.EligibilityForSection(request.SectionId, request.StudentId, ct);
        var result = await engine.Evaluate(inst, request.StudentId, request.SectionId, baseDecision.Requirements.All(x => x.Satisfied), ct);
        db.EligibilityDecisions.Add(new EligibilityDecision { InstitutionId = inst, StudentId = request.StudentId, SectionId = request.SectionId,
            PolicyId = result.PolicyId, Eligible = result.Eligible, SnapshotJson = JsonSerializer.Serialize(new { result, Prerequisites = baseDecision.Requirements }) });
        await db.SaveChangesAsync(ct);
        return result;
    }
    public Task<HoldView> AddHold(ClaimsPrincipal user, Guid inst, Guid student, HoldInput input, CancellationToken ct)
        => retry.ExecuteAsync<AcademicPolicyService, HoldView>(s => s.AddHoldCore(user, inst, student, input, ct), ct);
    private async Task<HoldView> AddHoldCore(ClaimsPrincipal user, Guid inst, Guid student, HoldInput input, CancellationToken ct)
    {
        if (input is null) throw ApiFault.Bad("Hold request required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db, inst, ct);
        var actor = await security.RequireAdminAsync(user, inst, ct);
        if (!await db.Users.AnyAsync(x => x.Id == student && x.InstitutionId == inst, ct)) throw ApiFault.NotFound();
        var row = new StudentAcademicHold { InstitutionId = inst, StudentId = student, Reason = RecordsPolicy.Reason(input.Reason), CreatedBy = actor.Id };
        db.StudentAcademicHolds.Add(row); security.Audit(actor.Id, inst, "registrar.hold.created", row.Id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(row.Id, student, row.Reason, row.CreatedAtUtc, null);
    }
    public Task ReleaseHold(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<AcademicPolicyService>(s => s.ReleaseHoldCore(user, inst, id, ct), ct);
    private async Task ReleaseHoldCore(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db, inst, ct);
        var actor = await security.RequireAdminAsync(user, inst, ct);
        var row = await db.StudentAcademicHolds.SingleOrDefaultAsync(x => x.Id == id && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        if (row.ReleasedAtUtc is null)
        {
            row.ReleasedBy = actor.Id; row.ReleasedAtUtc = DateTime.UtcNow;
            security.Audit(actor.Id, inst, "registrar.hold.released", id.ToString());
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
    public async Task<ExceptionView> RequestException(ClaimsPrincipal user, Guid inst, ExceptionInput input, CancellationToken ct)
    {
        if (input is null) throw ApiFault.Bad("Exception request required.");
        await security.RequireStudentAsync(user, input.StudentId, inst, ct);
        var (actor, _) = await security.CurrentAsync(user, ct);
        if (!await db.Users.AnyAsync(x => x.Id == input.StudentId && x.InstitutionId == inst, ct) ||
            !await db.CourseSections.AnyAsync(x => x.Id == input.SectionId && x.InstitutionId == inst, ct) ||
            !await db.AcademicPolicyRevisions.AnyAsync(x => x.Id == input.PolicyId && x.InstitutionId == inst && x.Status == "Published", ct)) throw ApiFault.NotFound();
        if (input.Rule is not ("Prerequisites" or "CreditLoad" or "EnrollmentApproval") || input.ExpiresAtUtc.Kind != DateTimeKind.Utc || input.ExpiresAtUtc <= DateTime.UtcNow)
            throw ApiFault.Bad("Invalid exception rule or expiration.");
        var row = new ApprovedAcademicException { InstitutionId = inst, StudentId = input.StudentId, SectionId = input.SectionId, PolicyId = input.PolicyId,
            Rule = input.Rule, Reason = RecordsPolicy.Reason(input.Reason), ExpiresAtUtc = input.ExpiresAtUtc, RequestedBy = actor.Id };
        db.ApprovedAcademicExceptions.Add(row); security.Audit(actor.Id, inst, "registrar.exception.requested", row.Id.ToString());
        await db.SaveChangesAsync(ct); return ExceptionView(row);
    }
    private static ExceptionView ExceptionView(ApprovedAcademicException x) => new(x.Id, x.StudentId, x.SectionId, x.PolicyId, x.Rule, x.Status, x.Reason);
    public Task<ExceptionView> ReviewException(ClaimsPrincipal user, Guid inst, Guid id, ExceptionReview input, CancellationToken ct)
        => retry.ExecuteAsync<AcademicPolicyService, ExceptionView>(s => s.ReviewExceptionCore(user, inst, id, input, ct), ct);
    private async Task<ExceptionView> ReviewExceptionCore(ClaimsPrincipal user, Guid inst, Guid id, ExceptionReview input, CancellationToken ct)
    {
        if (input is null) throw ApiFault.Bad("Review request required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db, inst, ct);
        var actor = await security.RequireAdminAsync(user, inst, ct);
        var row = await db.ApprovedAcademicExceptions.SingleOrDefaultAsync(x => x.Id == id && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        if (row.RequestedBy == actor.Id || row.StudentId == actor.Id) throw ApiFault.Forbidden();
        if (row.Status != "Pending" || row.ExpiresAtUtc <= DateTime.UtcNow) throw ApiFault.Conflict("Exception is finalized or expired.");
        row.Status = input.Approve ? "Approved" : "Rejected"; row.ReviewedBy = actor.Id; row.ReviewedAtUtc = DateTime.UtcNow;
        security.Audit(actor.Id, inst, "registrar.exception.reviewed", row.Id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ExceptionView(row);
    }
    public async Task<IReadOnlyList<DecisionHistory>> Decisions(ClaimsPrincipal user, Guid inst, Guid student, int page, CancellationToken ct)
    {
        await security.RequireStudentAsync(user, student, inst, ct);
        return await db.EligibilityDecisions.AsNoTracking().Where(x => x.InstitutionId == inst && x.StudentId == student)
            .OrderByDescending(x => x.AtUtc).ThenBy(x => x.Id).Skip(Offset(page)).Take(50)
            .Select(x => new DecisionHistory(x.Id, x.StudentId, x.SectionId, x.PolicyId, x.Eligible, x.SnapshotJson, x.AtUtc)).ToArrayAsync(ct);
    }
}
