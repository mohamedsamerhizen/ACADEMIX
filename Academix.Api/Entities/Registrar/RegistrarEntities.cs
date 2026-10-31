namespace Academix.Api.Domain.Registrar;

public sealed class AcademicPolicyRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid? ProgramId { get; set; }
    public string RulesJson { get; set; } = "{}";
    public string Status { get; set; } = "Draft";
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveUntilUtc { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
}

public sealed class StudentAcademicHold
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public string Reason { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
    public Guid? ReleasedBy { get; set; }
}

public sealed class ApprovedAcademicException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SectionId { get; set; }
    public Guid PolicyId { get; set; }
    public string Rule { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid RequestedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class EligibilityDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SectionId { get; set; }
    public Guid? PolicyId { get; set; }
    public bool Eligible { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
