namespace Academix.Api.DTOs.AcademicOperations;

public sealed record SlotUpsert(int Weekday, int StartsAtMinute, int DurationMinutes, string Room, long? ExpectedVersion = null, Guid? RoomId = null, bool RemoveRoomAssignment = false, string? ChangeReason = null);
public sealed record SlotView(Guid Id, Guid SectionId, int Weekday, int StartsAtMinute, int DurationMinutes, string Room, bool IsActive, long Version, Guid? RoomId = null);
public sealed record TimetableSlotState(int Weekday, int StartsAtMinute, int DurationMinutes,
    string Room, Guid? RoomId, bool IsActive, long Version);
public sealed record TimetableSlotRevisionView(Guid Id, Guid SlotId, string Action, string Reason,
    TimetableSlotState? Before, TimetableSlotState After, Guid ActorId, DateTime AtUtc);
public sealed record SessionCreate(DateTime StartsAtUtc, DateTime EndsAtUtc, string Topic, Guid? TimetableSlotId = null, Guid? RoomId = null);
public sealed record SessionView(Guid Id, Guid SectionId, DateTime StartsAtUtc, DateTime EndsAtUtc, string Topic, string Status, Guid? TimetableSlotId, long Version, Guid? RoomId = null);
public sealed record SessionRescheduleInput(DateTime StartsAtUtc, DateTime EndsAtUtc, long ExpectedVersion,
    string Reason, string? Topic = null, Guid? RoomId = null, bool RemoveRoomAssignment = false);
public sealed record ClassSessionState(DateTime StartsAtUtc, DateTime EndsAtUtc, string Topic,
    string Status, Guid? TimetableSlotId, long Version, Guid? RoomId = null);
public sealed record ClassSessionRevisionView(Guid Id, Guid SessionId, string Reason, ClassSessionState Before,
    ClassSessionState After, Guid ActorId, DateTime AtUtc);
public sealed record CampusRoomSessionView(Guid SessionId, Guid SectionId, DateTime StartsAtUtc,
    DateTime EndsAtUtc, string Status, Guid? TimetableSlotId, Guid? RoomId = null);
public sealed record AttendanceUpsert(Guid StudentId, string Status, string Note);
public sealed record AttendanceBatch(IReadOnlyList<AttendanceUpsert> Records);
public sealed record AttendanceView(Guid StudentId, string Status, string Note, DateTime RecordedAtUtc);
public sealed record AttendanceSummary(Guid StudentId, Guid SectionId, int TotalSessions, int Present, int Late, int Absent, int Excused);
public sealed record CalendarEventCreate(Guid? TermId, Guid? SectionId, string Title, string Description,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Audience, bool IsPublished);
public sealed record CalendarEventView(Guid Id, Guid? TermId, Guid? SectionId, string Title, string Description,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Audience, bool IsPublished);
public sealed record AcademicRequestCreate(Guid? SectionId, string Kind, string Subject, string Details, Guid? ClientRequestId = null, Academix.Api.DTOs.Registrar.RequestExecutionData? Execution = null);
public sealed record AcademicRequestView(Guid Id, Guid? SectionId, string Kind, string Subject, string Details,
    string Status, DateTime SubmittedAtUtc, DateTime? DecidedAtUtc, string DecisionReason, long Version, DateTime? FulfilledAtUtc = null, string? FulfillmentResult = null, string? ExecutionDataJson = null);
public sealed record AcademicRequestDecision(string Decision, string Reason, long ExpectedVersion);
public sealed record AcademicRequestAudit(Guid ActorId, string FromStatus, string ToStatus, string Reason, DateTime AtUtc);
public sealed record StudentScheduleItem(Guid Id, Guid SectionId, string Kind, string Title, DateTime StartsAtUtc, DateTime EndsAtUtc);
