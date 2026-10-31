namespace Academix.Api.Domain.Registrar;

public sealed class Applicant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string FullName { get; set; } = "";
    public Guid ProgramId { get; set; }
    public Guid TermId { get; set; }
    public string Statement { get; set; } = "";
    public string Status { get; set; } = "Invited";
    public string ProtectedInvitation { get; set; } = "";
    public string InvitationHash { get; set; } = "";
    public DateTime InvitationExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? AdmissionId { get; set; }
    public string? DecisionReason { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class ApplicantHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ApplicantId { get; set; }
    public string Status { get; set; } = "";
    public Guid? ActorId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class StudentProfile
{
    public Guid StudentId { get; set; }
    public Guid InstitutionId { get; set; }
    public string FullName { get; set; } = "";
    public string? ExternalReference { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class DocumentTemplateRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Kind { get; set; } = "";
    public int Revision { get; set; }
    public string Heading { get; set; } = "";
    public string Footer { get; set; } = "";
    public string AccentColor { get; set; } = "#173B57";
    public string FontFamily { get; set; } = "Lato";
    public bool RightToLeft { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}
public sealed class DocumentVerificationKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string ProtectedSecret { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RetiredAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
public sealed class CurriculumTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid FromPlanId { get; set; }
    public Guid ToPlanId { get; set; }
    public string MappingJson { get; set; } = "[]";
    public DateTime EffectiveAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public string? PriorAssignmentJson { get; set; }
}
public sealed class RecognizedCourseCredit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid TransitionId { get; set; }
    public Guid SourceCourseId { get; set; }
    public Guid TargetCourseId { get; set; }
    public Guid PlanId { get; set; }
    public decimal CreditHours { get; set; }
}
public sealed class InstitutionalEventReceipt
{
    public Guid EventId { get; set; }
    public Guid InstitutionId { get; set; }
    public string Kind { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
