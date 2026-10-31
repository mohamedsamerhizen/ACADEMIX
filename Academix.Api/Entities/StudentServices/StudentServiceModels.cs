namespace Academix.Api.Domain.StudentServices;

/// <summary>General institutional service only. Academic, financial and graduation requests
/// remain authoritative in their existing workflows.</summary>
public sealed class StudentServiceDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int SlaHours { get; set; } = 72;
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class StudentServiceCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid DefinitionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Subject { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public string CurrentQueueCode { get; set; } = "GeneralSupport";
    public Guid? AssignedStaffId { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime? EscalatedAtUtc { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime DueAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class StudentServiceCaseEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string PublicMessage { get; set; } = "";
    public string PrivateStaffNote { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Explicit staff access to one institutional service desk. Revoked roles or inactive accounts cannot use grants.</summary>
public sealed class StudentServiceStaffGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid UserId { get; set; }
    public string QueueCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;
}

/// <summary>Append-only movement history; the current queue is stored on the case.</summary>
public sealed class StudentServiceReferral
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid ActorId { get; set; }
    public string FromQueue { get; set; } = "";
    public string ToQueue { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Small, atomic private case evidence. Bytes are never included in list DTOs.</summary>
public sealed class StudentServiceAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Visibility { get; set; } = "StudentVisible";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
