namespace Academix.Api.DTOs.Registrar;

public sealed record AcademicPolicyRules(bool EnforcePrerequisites, bool AllowEquivalency,
    decimal? MaximumTermCredits, bool RequireEnrollmentApproval);
public sealed record PolicyDraft(Guid? ProgramId, DateTime EffectiveFromUtc, DateTime EffectiveUntilUtc, AcademicPolicyRules Rules);
public sealed record PolicyView(Guid Id, Guid? ProgramId, string Status, DateTime EffectiveFromUtc,
    DateTime EffectiveUntilUtc, AcademicPolicyRules Rules, Guid CreatedBy, Guid? PublishedBy);
public sealed record HoldInput(string Reason);
public sealed record HoldView(Guid Id, Guid StudentId, string Reason, DateTime CreatedAtUtc, DateTime? ReleasedAtUtc);
public sealed record ExceptionInput(Guid StudentId, Guid SectionId, Guid PolicyId, string Rule, string Reason, DateTime ExpiresAtUtc);
public sealed record ExceptionReview(bool Approve);
public sealed record ExceptionView(Guid Id, Guid StudentId, Guid SectionId, Guid PolicyId, string Rule, string Status, string Reason);
public sealed record PolicyEvaluation(Guid StudentId, Guid SectionId);
public sealed record PolicyDecision(Guid? PolicyId, bool Eligible, decimal CurrentCredits, decimal RequestedCredits, IReadOnlyList<string> Reasons);
public sealed record DecisionHistory(Guid Id, Guid StudentId, Guid SectionId, Guid? PolicyId, bool Eligible, string SnapshotJson, DateTime AtUtc);
