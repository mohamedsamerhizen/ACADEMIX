namespace Academix.Api.DTOs.Governance;

public sealed record CreateStaffChange(Guid ClientRequestId, Guid TargetUserId, string Action, string? Role, string Reason);
public sealed record DecideStaffChange(long ExpectedVersion, string Reason);
public sealed record StaffChangeView(Guid Id, Guid InstitutionId, Guid TargetUserId, Guid RequestedBy,
    string Action, string Role, string Reason, string Status, Guid? ReviewedBy, string ReviewReason,
    DateTime CreatedAtUtc, DateTime? ReviewedAtUtc, long Version);
public sealed record CreateEnrollmentBatch(Guid ClientRequestId, Guid SectionId, IReadOnlyList<Guid> StudentIds);
public sealed record EnrollmentBatchItemView(Guid StudentId, string Result);
public sealed record EnrollmentBatchView(Guid Id, Guid InstitutionId, Guid SectionId, Guid ClientRequestId,
    Guid CreatedBy, string Status, DateTime CreatedAtUtc, DateTime? CommittedAtUtc,
    IReadOnlyList<EnrollmentBatchItemView> Students);
public sealed record BatchPreview(Guid BatchId, bool Ready, int Capacity, int CurrentlyEnrolled,
    IReadOnlyList<string> Issues, IReadOnlyList<EnrollmentBatchItemView> Students);
public sealed record GovernanceEventView(Guid Id, Guid? ActorId, string Action, string Target, DateTime AtUtc);
public sealed record GovernanceOverview(int PendingStaffChanges, int DraftEnrollmentBatches,
    int CommittedEnrollmentBatches, int RecentAuditEvents);
