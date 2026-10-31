namespace Academix.Api.Domain.Governance;

// Optional two-person approval workflow. Existing direct role-management routes are unchanged.
public sealed class StaffChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid RequestedBy { get; set; }
    public string Action { get; set; } = ""; // AddRole | RemoveRole | Activate | Deactivate
    public string Role { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid? ReviewedBy { get; set; }
    public string ReviewReason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class EnrollmentBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Draft"; // Draft | Committed
    public DateTime? CommittedAtUtc { get; set; }
    public Guid? CommittedBy { get; set; }
}

public sealed class EnrollmentBatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid BatchId { get; set; }
    public Guid StudentId { get; set; }
    public string Result { get; set; } = "Pending"; // Pending | Enrolled | Reactivated
}
