namespace Academix.Api.Domain.Institutional;

public sealed class InstitutionalDocumentPolicy
{
    public Guid InstitutionId { get; set; }
    public bool AllowEnrollment { get; set; } = true;
    public bool AllowTranscript { get; set; } = true;
    public bool AllowGraduation { get; set; } = true;
    public bool AllowStanding { get; set; } = true;
    public int ValidityDays { get; set; } = 180;
    public long Version { get; set; } = 1;
}

public sealed class InstitutionalDocumentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Kind { get; set; } = "";
    public Guid? SupersedesDocumentId { get; set; }
    public string Purpose { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public long Version { get; set; } = 1;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
}

public sealed class IssuedAcademicDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid RequestId { get; set; }
    public string Kind { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public Guid IssuedById { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public Guid? RevokedById { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }
    public long Version { get; set; } = 1;
    public Guid? TemplateId { get; set; }
    public Guid? VerificationKeyId { get; set; }
    public string? LegacyProof { get; set; }
    public string? ArtifactKey { get; set; }
    public string? ArtifactDigest { get; set; }
    public string? ArtifactSignature { get; set; }
    public Guid? SupersededById { get; set; }
    public DateTime? SourceInvalidatedAtUtc { get; set; }
}

public sealed class InstitutionalDocumentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
