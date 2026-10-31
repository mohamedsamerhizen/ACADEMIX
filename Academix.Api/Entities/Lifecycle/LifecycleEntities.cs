namespace Academix.Api.Domain.Lifecycle;

public sealed class AdmissionApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid TermId { get; set; }
    public string Statement { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public long Version { get; set; } = 1;
    public Guid? ReviewedById { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
}

public sealed class TransferCreditPetition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }
    public string OriginInstitution { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public long Version { get; set; } = 1;
    public Guid? ReviewedById { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
}

public sealed class TransferCreditLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PetitionId { get; set; }
    public Guid TargetCourseId { get; set; }
    public string OriginCourseCode { get; set; } = "";
    public string OriginCourseTitle { get; set; } = "";
    public decimal ProposedCreditHours { get; set; }
}

public sealed class GraduationApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "Submitted";
    public long Version { get; set; } = 1;
    public Guid? ReviewedById { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? IssuedById { get; set; }
    public DateTime? SourceInvalidatedAtUtc { get; set; }
    public string? CredentialNumber { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public Guid? ClientRequestId { get; set; }
    public bool RequiresDepartmentReview { get; set; }
    public Guid? DepartmentReviewedById { get; set; }
    public DateTime? DepartmentReviewedAtUtc { get; set; }
    public string? DepartmentReviewReason { get; set; }
    public string? ApprovalEvidenceJson { get; set; }
    public string? FinalEvidenceJson { get; set; }
}

public sealed class LifecycleDecisionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActorId { get; set; }
    public string EntityKind { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
