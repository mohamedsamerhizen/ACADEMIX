using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Institutional;
using Academix.Api.DTOs.Institutional;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Institutional;

public sealed partial class InstitutionalDocumentService(AcademixDbContext db, IdentitySecurity security,
    RetryingServiceWork retry, DocumentKeyService documentKeys, DocumentPdfRenderer pdfRenderer, Academix.Api.Storage.IPrivateFileStore documentFiles,
    DocumentArtifactCleanup artifactCleanup)
{
    private static DocumentRequestView Map(InstitutionalDocumentRequest x) =>
        new(x.Id, x.StudentId, x.Kind, x.Purpose, x.Status, x.Version,
            x.SubmittedAtUtc, x.ReviewedAtUtc, x.DecisionReason);

    private static DocumentPolicyView Map(InstitutionalDocumentPolicy p) =>
        new(p.InstitutionId, p.AllowEnrollment, p.AllowTranscript, p.AllowStanding, p.ValidityDays, p.Version, p.AllowGraduation);

    private static InstitutionalEventView Map(InstitutionalDocumentEvent e) =>
        new(e.Id, e.RequestId, e.DocumentId, e.StudentId, e.ActorId, e.Action, e.Detail, e.AtUtc);

    private static string Proof(IssuedAcademicDocument document) => document.LegacyProof
        ?? throw ApiFault.Conflict("Legacy document verification must be initialized before use.");

    private AcademicDocumentView Map(IssuedAcademicDocument x) =>
        new(x.Id, x.StudentId, x.RequestId, x.Kind, x.ReferenceNumber, x.SnapshotJson,
            Proof(x), x.IssuedAtUtc, x.ExpiresAtUtc, x.RevokedAtUtc, x.RevocationReason, x.Version, x.ArtifactDigest, x.TemplateId, x.VerificationKeyId, x.SupersededById, x.SourceInvalidatedAtUtc);

    private async Task<(Guid Institution, Guid Student)> Student(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid inst || !roles.Contains(AcademixRoles.Student))
            throw ApiFault.Forbidden();
        return (inst, user.Id);
    }

    private static int Page(int page)
    {
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be between 1 and 10000.");
        return (page - 1) * 50;
    }

    private async Task<InstitutionalDocumentPolicy> PolicyFor(Guid inst, CancellationToken ct)
        => await db.InstitutionalDocumentPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst, ct)
            ?? new InstitutionalDocumentPolicy { InstitutionId = inst, Version = 0 };

    private void Event(InstitutionalDocumentRequest request, Guid actor, string action, string detail,
        Guid? documentId = null)
    {
        db.InstitutionalDocumentEvents.Add(new InstitutionalDocumentEvent
        {
            InstitutionId = request.InstitutionId, StudentId = request.StudentId,
            RequestId = request.Id, DocumentId = documentId, ActorId = actor,
            Action = action, Detail = detail
        });
        security.Audit(actor, request.InstitutionId, "institutional-document." + action, request.Id.ToString());
    }

    public async Task<DocumentPolicyView> Policy(ClaimsPrincipal principal, Guid inst, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        return Map(await PolicyFor(inst, ct));
    }

    public Task<DocumentPolicyView> UpdatePolicy(ClaimsPrincipal principal, Guid inst,
        UpdateDocumentPolicy input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalDocumentService, DocumentPolicyView>(service => service.UpdatePolicyCore(principal, inst, input, ct), ct);

    private async Task<DocumentPolicyView> UpdatePolicyCore(ClaimsPrincipal principal, Guid inst,
        UpdateDocumentPolicy input, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        InstitutionalDocumentPolicyRules.ValidityDays(input.ValidityDays);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var policy = (await db.InstitutionalDocumentPolicies.FromSqlInterpolated(
                $"SELECT * FROM institutional_document_policies WHERE \"InstitutionId\" = {inst} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault();
            if (policy is null)
            {
                if (input.ExpectedVersion != 0) throw ApiFault.Conflict("No stored policy exists yet.");
                policy = new InstitutionalDocumentPolicy { InstitutionId = inst };
                db.InstitutionalDocumentPolicies.Add(policy);
            }
            else
            {
                InstitutionalDocumentPolicyRules.EnsureVersion(policy.Version, input.ExpectedVersion);
                policy.Version++;
            }
            policy.AllowEnrollment = input.AllowEnrollment;
            policy.AllowTranscript = input.AllowTranscript;
            policy.AllowStanding = input.AllowStanding;policy.AllowGraduation=input.AllowGraduation;
            policy.ValidityDays = input.ValidityDays;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(policy);
        });
    }

    public Task<DocumentRequestView> SubmitApplication(ClaimsPrincipal principal,
        SubmitDocumentRequest input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalDocumentService, DocumentRequestView>(service => service.SubmitApplicationCore(principal, input, ct), ct);

    private async Task<DocumentRequestView> SubmitApplicationCore(ClaimsPrincipal principal,
        SubmitDocumentRequest input, CancellationToken ct)
    {
        var (inst, student) = await Student(principal, ct);
        if (input.ClientRequestId == Guid.Empty) throw ApiFault.Bad("ClientRequestId is required for idempotency.");
        var kind = InstitutionalDocumentPolicyRules.Kind(input.Kind);
        var purpose = InstitutionalDocumentPolicyRules.Purpose(input.Purpose);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Serialize submissions from the same student so retries return the same result,
            // including concurrent requests with an identical idempotency key.
            var lockedUser = (await db.Users.FromSqlInterpolated(
                $"SELECT * FROM identity_users WHERE \"Id\" = {student} AND \"InstitutionId\" = {inst} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault();
            if (lockedUser is null) throw ApiFault.Unauthorized();
            var previous = await db.InstitutionalDocumentRequests.AsNoTracking().SingleOrDefaultAsync(x =>
                x.InstitutionId == inst && x.StudentId == student && x.ClientRequestId == input.ClientRequestId, ct);
            if (previous is not null)
            {
                if (previous.Kind != kind || previous.Purpose != purpose || previous.SupersedesDocumentId != input.SupersedesDocumentId)
                    throw ApiFault.Conflict("ClientRequestId was already used for a different request.");
                await tx.CommitAsync(ct);
                return Map(previous);
            }
            var policy = await PolicyFor(inst, ct);
            if (!InstitutionalDocumentPolicyRules.Allowed(kind, policy.AllowEnrollment,
                    policy.AllowTranscript, policy.AllowStanding,policy.AllowGraduation)) throw ApiFault.Conflict("This document type is disabled.");
            if (!await db.StudentProgramMemberships.AsNoTracking().AnyAsync(x =>
                x.InstitutionId == inst && x.StudentId == student, ct))
                throw ApiFault.Conflict("A program membership is required before requesting documents.");
            if(input.SupersedesDocumentId is Guid oldDocument&&!await db.IssuedAcademicDocuments.AnyAsync(x=>x.Id==oldDocument&&x.InstitutionId==inst&&x.StudentId==student&&x.Kind==kind&&x.SupersededById==null,ct))throw ApiFault.NotFound();
            var row = new InstitutionalDocumentRequest
            {
                InstitutionId = inst, StudentId = student, Kind = kind,
                Purpose = purpose, ClientRequestId = input.ClientRequestId, SupersedesDocumentId=input.SupersedesDocumentId
            };
            db.InstitutionalDocumentRequests.Add(row);
            Event(row, student, "submitted", purpose);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(row);
        });
    }

    public async Task<IReadOnlyList<DocumentRequestView>> MyRequests(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (inst, student) = await Student(principal, ct);
        var rows = await db.InstitutionalDocumentRequests.AsNoTracking()
            .Where(x => x.InstitutionId == inst && x.StudentId == student)
            .OrderByDescending(x => x.SubmittedAtUtc).Take(100).ToListAsync(ct);
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<DocumentRequestView>> InstitutionRequests(ClaimsPrincipal principal,
        Guid inst, string? status, int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        if (status is not null && status is not ("Submitted" or "Approved" or "Rejected" or "Cancelled" or "Issued"))
            throw ApiFault.Bad("Invalid document request status.");
        var rows = db.InstitutionalDocumentRequests.AsNoTracking().Where(x => x.InstitutionId == inst);
        if (status is not null) rows = rows.Where(x => x.Status == status);
        return (await rows.OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Skip(Page(page)).Take(50).ToListAsync(ct)).Select(Map).ToArray();
    }

    private async Task<InstitutionalDocumentRequest> LockedRequest(Guid id, CancellationToken ct)
        => (await db.InstitutionalDocumentRequests.FromSqlInterpolated(
            $"SELECT * FROM institutional_document_requests WHERE \"Id\" = {id} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();

    public Task<DocumentRequestView> Cancel(ClaimsPrincipal principal, Guid id,
        CancelDocumentRequest input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalDocumentService, DocumentRequestView>(service => service.CancelCore(principal, id, input, ct), ct);

    private async Task<DocumentRequestView> CancelCore(ClaimsPrincipal principal, Guid id,
        CancelDocumentRequest input, CancellationToken ct)
    {
        var (inst, student) = await Student(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var request = await LockedRequest(id, ct);
            if (request.InstitutionId != inst || request.StudentId != student) throw ApiFault.NotFound();
            InstitutionalDocumentPolicyRules.EnsureVersion(request.Version, input.ExpectedVersion);
            InstitutionalDocumentPolicyRules.EnsureTransition(request.Status, "Submitted");
            request.Status = "Cancelled";
            request.Version++;
            Event(request, student, "cancelled", "Cancelled by applicant");
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(request);
        });
    }

    public Task<DocumentRequestView> Review(ClaimsPrincipal principal, Guid id,
        DecideDocumentRequest input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalDocumentService, DocumentRequestView>(service => service.ReviewCore(principal, id, input, ct), ct);

    private async Task<DocumentRequestView> ReviewCore(ClaimsPrincipal principal, Guid id,
        DecideDocumentRequest input, CancellationToken ct)
    {
        var initial = await db.InstitutionalDocumentRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, initial.InstitutionId, ct);
        InstitutionalDocumentPolicyRules.EnsureIndependentReviewer(initial.StudentId, actor.Id);
        var reason = InstitutionalDocumentPolicyRules.Reason(input.Reason);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var request = await LockedRequest(id, ct);
            if (request.InstitutionId != initial.InstitutionId) throw ApiFault.NotFound();
            InstitutionalDocumentPolicyRules.EnsureVersion(request.Version, input.ExpectedVersion);
            InstitutionalDocumentPolicyRules.EnsureTransition(request.Status, "Submitted");
            request.Status = input.Approve ? "Approved" : "Rejected";
            request.ReviewedById = actor.Id;
            request.ReviewedAtUtc = DateTime.UtcNow;
            request.DecisionReason = reason;
            request.Version++;
            Event(request, actor.Id, input.Approve ? "approved" : "rejected", reason);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(request);
        });
    }

    private async Task<string> Snapshot(InstitutionalDocumentRequest request, CancellationToken ct)
    {
        var membership = await db.StudentProgramMemberships.AsNoTracking()
            .Where(x => x.InstitutionId == request.InstitutionId && x.StudentId == request.StudentId)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct)
            ?? throw ApiFault.Conflict("Student does not have an academic program membership.");
        if (request.Kind is "Transcript" or "Standing" or "Graduation")
            await AcademicFreshness.Ensure(db, request.InstitutionId, request.StudentId, ct);
        var institution = await db.Institutions.AsNoTracking().Where(x => x.Id == request.InstitutionId)
            .Select(x => new { x.Name, x.Code }).SingleAsync(ct);
        var program = await db.AcademicPrograms.AsNoTracking().Where(x => x.Id == membership.ProgramId &&
            x.InstitutionId == request.InstitutionId).Select(x => new { x.Name, x.Code }).SingleAsync(ct);
        var identity=await db.Users.AsNoTracking().Where(x=>x.Id==request.StudentId&&x.InstitutionId==request.InstitutionId)
            .Select(x=>new{x.Email}).SingleOrDefaultAsync(ct)??throw ApiFault.NotFound();
        var fullName=await db.Set<Academix.Api.Domain.Registrar.StudentProfile>().AsNoTracking().Where(x=>x.StudentId==request.StudentId&&x.InstitutionId==request.InstitutionId).Select(x=>x.FullName).SingleOrDefaultAsync(ct);
        var studentIdentity=new{FullName=fullName,identity.Email};
        var now = DateTime.UtcNow;
        if (request.Kind == "Enrollment")
        {
            if (membership.Status != "Active")
                throw ApiFault.Conflict("An enrollment letter requires active program membership.");
            return JsonSerializer.Serialize(new { Schema = "ACADEMIX-Enrollment-v1", IssuedAtUtc = now,
                institution, program, studentIdentity, request.StudentId, membership.AdmissionTermId, membership.CurrentTermId,
                membership.Status });
        }
        if (request.Kind == "Transcript")
        {
            var entries = await db.StudentTranscriptEntries.AsNoTracking().Where(x =>
                x.InstitutionId == request.InstitutionId && x.StudentId == request.StudentId)
                .OrderBy(x => x.TermId).ThenBy(x => x.CourseCode)
                .Select(x => new { x.TermId, x.CourseId, x.CourseCode, x.CourseTitle,
                    x.Percentage, x.Passed, x.Letter }).ToArrayAsync(ct);
            if (entries.Length == 0) throw ApiFault.Conflict("No synchronized published transcript entries available.");
            return JsonSerializer.Serialize(new { Schema = "ACADEMIX-Transcript-v1", IssuedAtUtc = now,
                institution, program, studentIdentity, request.StudentId, Entries = entries });
        }
        if (request.Kind == "Standing")
        {
            var standing = await db.StudentTermStandings.AsNoTracking().Where(x =>
                x.InstitutionId == request.InstitutionId && x.StudentId == request.StudentId &&
                x.Status != "InsufficientData").OrderByDescending(x => x.CalculatedAtUtc)
                .Select(x => new { x.TermId, x.Status, x.PassedCourses,
                    x.CompletedCourses, x.AveragePercentage, x.CalculatedAtUtc }).FirstOrDefaultAsync(ct)
                ?? throw ApiFault.Conflict("Academic standing must be calculated before issuance.");
            if (await db.StudentTranscriptEntries.AnyAsync(x => x.InstitutionId == request.InstitutionId &&
                x.StudentId == request.StudentId && x.TermId == standing.TermId && x.SyncedAtUtc > standing.CalculatedAtUtc, ct))
                throw ApiFault.Conflict("Academic standing is stale. Recalculate before issuance.");
            return JsonSerializer.Serialize(new { Schema = "ACADEMIX-Standing-v1", IssuedAtUtc = now,
                institution, program, studentIdentity, request.StudentId, Standing = standing });
        }
        if(request.Kind=="Graduation")
        {
            var credential=await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==request.InstitutionId&&x.StudentId==request.StudentId&&x.Status=="Issued",ct)
                ??throw ApiFault.Conflict("An issued graduation credential is required.");
            if(credential.SourceInvalidatedAtUtc.HasValue)throw ApiFault.Conflict("Credential requires academic reassessment before document issuance.");
            return JsonSerializer.Serialize(new{Schema="ACADEMIX-Graduation-v1",IssuedAtUtc=now,institution,program,studentIdentity,request.StudentId,credential.CredentialNumber,CredentialIssuedAtUtc=credential.IssuedAtUtc,credential.PlanId});
        }
        throw ApiFault.Bad("Unsupported document kind.");
    }

    public Task<AcademicDocumentView> Issue(ClaimsPrincipal principal, Guid id,
        IssueDocumentRequest input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalDocumentService, AcademicDocumentView>(service => service.IssueCore(principal, id, input, ct), ct);

    private async Task<AcademicDocumentView> IssueCore(ClaimsPrincipal principal, Guid id,
        IssueDocumentRequest input, CancellationToken ct)
    {
        var initial = await db.InstitutionalDocumentRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, initial.InstitutionId, ct);
        InstitutionalDocumentPolicyRules.EnsureIndependentReviewer(initial.StudentId, actor.Id);
        // Issue already runs through RetryingServiceWork. A nested execution strategy
        // would replay this transaction with entities tracked from a failed attempt.
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // NO KEY UPDATE still serializes institution operations, but permits the
            // independent cleanup intent's FK KEY SHARE lock on this institution.
            var institution = await db.Institutions.FromSqlInterpolated(
                $"SELECT * FROM institutions WHERE \"Id\" = {initial.InstitutionId} FOR NO KEY UPDATE").SingleAsync(ct);
            if (!institution.IsActive) throw ApiFault.NotFound();
            await security.RequireAdminAsync(principal,initial.InstitutionId,ct);
            var request = await LockedRequest(id, ct);
            if (request.InstitutionId != initial.InstitutionId) throw ApiFault.NotFound();
            if(request.Status=="Issued")
            {
                var previous=await db.IssuedAcademicDocuments.AsNoTracking().SingleAsync(x=>x.RequestId==request.Id,ct);
                await tx.CommitAsync(ct);return Map(previous);
            }
            InstitutionalDocumentPolicyRules.EnsureVersion(request.Version, input.ExpectedVersion);
            InstitutionalDocumentPolicyRules.EnsureTransition(request.Status, "Approved");
            if(request.Kind is "Transcript" or "Standing" or "Graduation")
                await Academix.Api.Services.Finance.FinanceDebt.RequireClearance(db,request.InstitutionId,request.StudentId,"Documents",request.SubmittedAtUtc,ct);
            var policy = await PolicyFor(request.InstitutionId, ct);
            if (!InstitutionalDocumentPolicyRules.Allowed(request.Kind, policy.AllowEnrollment,
                    policy.AllowTranscript, policy.AllowStanding,policy.AllowGraduation)) throw ApiFault.Conflict("Document kind is currently disabled.");
            var content = await Snapshot(request, ct);
            var now = DateTime.UtcNow;
            var document = new IssuedAcademicDocument
            {
                InstitutionId = request.InstitutionId, StudentId = request.StudentId,
                RequestId = request.Id, Kind = request.Kind, SnapshotJson = content,
                IssuedById = actor.Id, IssuedAtUtc = now,
                ExpiresAtUtc = now.AddDays(InstitutionalDocumentPolicyRules.ValidityDays(policy.ValidityDays)),
                ReferenceNumber = "ACA-" + Guid.NewGuid().ToString("N").ToUpperInvariant()
            };
            document.Id=request.Id;
            var template=await Template(document.InstitutionId,document.Kind,actor.Id,ct);
            var key=await documentKeys.Active(document.InstitutionId,ct);
            document.TemplateId=template.Id;document.VerificationKeyId=key.Id;
            document.LegacyProof=documentKeys.Sign(key,document);
            var bytes=pdfRenderer.Render(document,template);
            if(bytes.Length>20*1024*1024)throw ApiFault.Conflict("Generated document exceeds storage limit.");
            document.ArtifactDigest=DocumentIntegrity.Digest(bytes);
            document.ArtifactSignature=documentKeys.SignArtifact(key,document);
            document.ArtifactKey=$"official/{document.InstitutionId:N}/{document.Id:N}.pdf";
            await artifactCleanup.Schedule(document.InstitutionId, document.Id, ct);
            await documentFiles.PutAsync(document.ArtifactKey,new MemoryStream(bytes,false),bytes.Length,"application/pdf",ct);
            db.IssuedAcademicDocuments.Add(document);
            if(request.SupersedesDocumentId is Guid originalId)
            {
                var original=await db.IssuedAcademicDocuments.SingleAsync(x=>x.Id==originalId&&x.InstitutionId==document.InstitutionId&&x.StudentId==document.StudentId,ct);
                if(original.SupersededById.HasValue||original.Kind!=document.Kind)throw ApiFault.Conflict("Original issuance already superseded or mismatched.");
                original.SupersededById=document.Id;original.Version++;
                Event(request,actor.Id,"superseded",original.ReferenceNumber,original.Id);
            }
            OutboxService.Record(db,document.InstitutionId,"DocumentIssued",document.Id,new{DocumentId=document.Id,document.Kind});
            request.Status = "Issued";
            request.Version++;
            Event(request, actor.Id, "issued", document.ReferenceNumber, document.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(document);
        }
    }

    public async Task<IReadOnlyList<AcademicDocumentView>> MyDocuments(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (inst, student) = await Student(principal, ct);
        var docs = await db.IssuedAcademicDocuments.AsNoTracking()
            .Where(x => x.InstitutionId == inst && x.StudentId == student)
            .OrderByDescending(x => x.IssuedAtUtc).Take(100).ToListAsync(ct);
        return docs.Select(Map).ToArray();
    }

    public async Task<AcademicDocumentView> Document(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var doc = await db.IssuedAcademicDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        await security.RequireStudentAsync(principal, doc.StudentId, doc.InstitutionId, ct);
        return Map(doc);
    }

    public async Task<IReadOnlyList<AcademicDocumentView>> InstitutionDocuments(ClaimsPrincipal principal,
        Guid institutionId, int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institutionId, ct);
        var docs = await db.IssuedAcademicDocuments.AsNoTracking()
            .Where(x => x.InstitutionId == institutionId).OrderByDescending(x => x.IssuedAtUtc)
            .ThenBy(x => x.Id).Skip(Page(page)).Take(50).ToListAsync(ct);
        return docs.Select(Map).ToArray();
    }

    public Task<AcademicDocumentView> Revoke(ClaimsPrincipal principal, Guid id,
        RevokeAcademicDocument input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalDocumentService, AcademicDocumentView>(service => service.RevokeCore(principal, id, input, ct), ct);

    private async Task<AcademicDocumentView> RevokeCore(ClaimsPrincipal principal, Guid id,
        RevokeAcademicDocument input, CancellationToken ct)
    {
        var initial = await db.IssuedAcademicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, initial.InstitutionId, ct);
        var reason = InstitutionalDocumentPolicyRules.Reason(input.Reason);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var doc = (await db.IssuedAcademicDocuments.FromSqlInterpolated(
                $"SELECT * FROM issued_academic_documents WHERE \"Id\" = {id} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            if (doc.InstitutionId != initial.InstitutionId) throw ApiFault.NotFound();
            InstitutionalDocumentPolicyRules.EnsureVersion(doc.Version, input.ExpectedVersion);
            if (doc.RevokedAtUtc is not null) throw ApiFault.Conflict("Document was already revoked.");
            doc.RevokedAtUtc = DateTime.UtcNow;
            doc.RevokedById = actor.Id;
            doc.RevocationReason = reason;
            doc.Version++;
            var request = await db.InstitutionalDocumentRequests.AsNoTracking()
                .SingleAsync(x => x.Id == doc.RequestId && x.InstitutionId == doc.InstitutionId, ct);
            Event(request, actor.Id, "revoked", reason, doc.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(doc);
        });
    }

    public async Task<IReadOnlyList<InstitutionalEventView>> Events(ClaimsPrincipal principal,
        Guid inst, int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        var rows = await db.InstitutionalDocumentEvents.AsNoTracking()
            .Where(x => x.InstitutionId == inst).OrderByDescending(x => x.AtUtc).ThenBy(x => x.Id)
            .Skip(Page(page)).Take(50).ToListAsync(ct);
        return rows.Select(Map).ToArray();
    }

    // Public verification discloses only status, document kind and timestamps, never identity or grades.
    public async Task<AcademicDocumentVerification> Verify(VerifyAcademicDocument input, CancellationToken ct)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.ReferenceNumber) ||
            string.IsNullOrWhiteSpace(input.Proof) || input.Proof.Length != 64 ||
            !input.Proof.All(Uri.IsHexDigit)) throw ApiFault.NotFound();
        var doc = await db.IssuedAcademicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ReferenceNumber == input.ReferenceNumber, ct)
            ?? throw ApiFault.NotFound();
        var claimed = Convert.FromHexString(input.Proof);
        var expected = Convert.FromHexString(await documentKeys.Expected(doc,ct));
        if (!CryptographicOperations.FixedTimeEquals(claimed, expected)) throw ApiFault.NotFound();
        var status = InstitutionalDocumentPolicyRules.State(doc.RevokedAtUtc is not null,
            doc.ExpiresAtUtc, DateTime.UtcNow);
        if(doc.SupersededById.HasValue)status="Superseded";
        if(status=="Valid"&&doc.SourceInvalidatedAtUtc.HasValue)status="SourceChanged";
        // Also protects pre-remediation issuances whose document marker was not propagated.
        if(status=="Valid"&&doc.Kind=="Graduation" && !await db.GraduationApplications.AsNoTracking().AnyAsync(x=>
            x.InstitutionId==doc.InstitutionId && x.StudentId==doc.StudentId && x.Status=="Issued" &&
            x.SourceInvalidatedAtUtc==null,ct))status="SourceChanged";
        if(doc.VerificationKeyId is Guid keyId && await db.Set<Academix.Api.Domain.Registrar.DocumentVerificationKey>().AnyAsync(x=>x.Id==keyId&&x.RevokedAtUtc!=null,ct))status="KeyRevoked";
        if(doc.ArtifactKey is not null)
        {
            var bytes=await documentFiles.GetAsync(doc.ArtifactKey,ct);
            if(!DocumentIntegrity.Equal(DocumentIntegrity.Digest(bytes),doc.ArtifactDigest)||!await documentKeys.ArtifactSignatureValid(doc,ct))status="IntegrityFailure";
        }
        return new AcademicDocumentVerification(status == "Valid", status, doc.Kind,
            doc.ReferenceNumber, doc.IssuedAtUtc, doc.ExpiresAtUtc,doc.ArtifactDigest);
    }
}
