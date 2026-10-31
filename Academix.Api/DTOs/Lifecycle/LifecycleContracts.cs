namespace Academix.Api.DTOs.Lifecycle;

public sealed record SubmitAdmission(Guid TermId, string Statement);
public sealed record AdmissionView(Guid Id, Guid StudentId, Guid ProgramId, Guid TermId, string Statement,
    string Status, long Version, DateTime CreatedAtUtc, DateTime? ReviewedAtUtc, string? DecisionReason);
public sealed record LifecycleDecision(long ExpectedVersion, string Decision, string Reason);
public sealed record SubmitTransferCredit(string OriginInstitution, IReadOnlyList<TransferCourseRequest> Courses);
public sealed record TransferCourseRequest(Guid TargetCourseId, string OriginCourseCode, string OriginCourseTitle,
    decimal ProposedCreditHours);
public sealed record TransferCourseView(Guid TargetCourseId, string OriginCourseCode, string OriginCourseTitle,
    decimal ProposedCreditHours);
public sealed record TransferView(Guid Id, Guid StudentId, Guid ProgramId, string OriginInstitution,
    string Status, long Version, string? DecisionReason, IReadOnlyList<TransferCourseView> Courses);
public sealed record GraduationView(Guid Id, Guid StudentId, Guid ProgramId, Guid PlanId, string Status,
    long Version, DateTime CreatedAtUtc, DateTime? ReviewedAtUtc, string? DecisionReason,
    string? CredentialNumber, DateTime? IssuedAtUtc, DateTime? SourceInvalidatedAtUtc = null);
public sealed record IssueGraduation(long ExpectedVersion);
public sealed record GraduationSubmission(Guid? ClientRequestId);
public sealed record DepartmentReviewerInput(Guid ReviewerId,bool Active);
public sealed record DepartmentGraduationDecision(long ExpectedVersion,bool Approve,string Reason);
public sealed record GraduationEvidenceView(GraduationView Application,bool RequiresDepartmentReview,
    Guid? DepartmentReviewedById,DateTime? DepartmentReviewedAtUtc,string? DepartmentReviewReason,
    string? ApprovalEvidenceJson,string? FinalEvidenceJson);
public sealed record LifecycleEventView(Guid Id, string EntityKind, Guid EntityId, string Action,
    Guid ActorId, string Reason, DateTime AtUtc);
