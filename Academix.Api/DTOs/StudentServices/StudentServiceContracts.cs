namespace Academix.Api.DTOs.StudentServices;

public sealed record ServiceDefinitionInput(string Code, string Name, string Description,
    int SlaHours, bool IsActive, long ExpectedVersion = 0);
public sealed record ServiceDefinitionView(Guid Id, Guid InstitutionId, string Code, string Name,
    string Description, int SlaHours, bool IsActive, long Version);
public sealed record ServiceCaseSubmit(Guid DefinitionId, Guid ClientRequestId,
    string Subject, string Details);
public sealed record ServiceCaseAction(string Action, long ExpectedVersion,
    string PublicMessage, string PrivateStaffNote = "");
public sealed record ServiceCaseView(Guid Id, Guid InstitutionId, Guid DefinitionId,
    Guid StudentId, string Subject, string Details, string Status, DateTime SubmittedAtUtc,
    DateTime UpdatedAtUtc, DateTime DueAtUtc, DateTime? ClosedAtUtc, long Version,
    string CurrentQueueCode = "GeneralSupport", Guid? AssignedStaffId = null,
    string Priority = "Normal", DateTime? EscalatedAtUtc = null, bool IsOverdue = false);
public sealed record ServiceCaseEventView(Guid Id, string Action, string FromStatus,
    string ToStatus, string PublicMessage, DateTime OccurredAtUtc);
public sealed record StaffServiceCaseEventView(Guid Id, Guid ActorId, string Action,
    string FromStatus, string ToStatus, string PublicMessage, string PrivateStaffNote,
    DateTime OccurredAtUtc);

public sealed record ServiceStaffGrantInput(bool IsActive, long ExpectedVersion = 0);
public sealed record ServiceStaffGrantView(Guid Id, Guid UserId, string QueueCode, bool IsActive, long Version);
public sealed record ServiceCaseAssign(Guid? StaffId, long ExpectedVersion, string Reason);
public sealed record ServiceCaseRefer(string TargetQueue, long ExpectedVersion, string Reason);
public sealed record ServiceCaseEscalate(string Priority, long ExpectedVersion, string Reason);
public sealed record ServiceReferralView(Guid Id, Guid ActorId, string FromQueue, string ToQueue,
    string Reason, DateTime OccurredAtUtc);
public sealed record ServiceAttachmentView(Guid Id, string FileName, string ContentType,
    string Visibility, long Size, string Sha256, DateTime UploadedAtUtc);
public sealed record ServiceCaseSlaReport(int Total, int Open, int Overdue, int AwaitingStudent,
    int Resolved, int Closed, int Unassigned, int Urgent, IReadOnlyList<ServiceCaseQueueReport> Queues);
public sealed record ServiceCaseQueueReport(string Queue, int Open, int Overdue, int Unassigned);
