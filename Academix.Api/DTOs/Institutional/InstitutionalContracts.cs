namespace Academix.Api.DTOs.Institutional;

public sealed record SubmitDocumentRequest(Guid ClientRequestId, string Kind, string Purpose, Guid? SupersedesDocumentId = null);
public sealed record DecideDocumentRequest(long ExpectedVersion, bool Approve, string Reason);
public sealed record IssueDocumentRequest(long ExpectedVersion);
public sealed record CancelDocumentRequest(long ExpectedVersion);
public sealed record RevokeAcademicDocument(long ExpectedVersion, string Reason);
public sealed record UpdateDocumentPolicy(long ExpectedVersion, bool AllowEnrollment, bool AllowTranscript,
    bool AllowStanding, int ValidityDays, bool AllowGraduation = true);
public sealed record VerifyAcademicDocument(string ReferenceNumber, string Proof);
public sealed record DocumentPolicyView(Guid InstitutionId, bool AllowEnrollment, bool AllowTranscript,
    bool AllowStanding, int ValidityDays, long Version, bool AllowGraduation = true);
public sealed record DocumentRequestView(Guid Id, Guid StudentId, string Kind, string Purpose, string Status,
    long Version, DateTime SubmittedAtUtc, DateTime? ReviewedAtUtc, string? DecisionReason);
public sealed record AcademicDocumentView(Guid Id, Guid StudentId, Guid RequestId, string Kind, string ReferenceNumber,
    string SnapshotJson, string VerificationProof, DateTime IssuedAtUtc, DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc, string? RevocationReason, long Version, string? ArtifactDigest = null, Guid? TemplateId = null, Guid? VerificationKeyId = null, Guid? SupersededById = null, DateTime? SourceInvalidatedAtUtc = null);
public sealed record AcademicDocumentVerification(bool Valid, string Status, string Kind, string ReferenceNumber,
    DateTime IssuedAtUtc, DateTime ExpiresAtUtc, string? ArtifactDigest = null);
public sealed record InstitutionalEventView(Guid Id, Guid RequestId, Guid? DocumentId, Guid StudentId, Guid ActorId,
    string Action, string Detail, DateTime AtUtc);
