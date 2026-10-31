namespace Academix.Api.DTOs.AcademicOperations;

/// <summary>ClientRequestId is mandatory and unique per institution, including after cancellation.</summary>
public sealed record CampusReservationCreate(Guid ClientRequestId, Guid RoomId, string Kind,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Reason,
    Guid? ExamId = null, Guid? InvigilatorId = null, int? SeatCount = null);
public sealed record CampusReservationCancel(long ExpectedVersion, string Reason);
public sealed record CampusReservationView(Guid Id, Guid InstitutionId, Guid RoomId, string Kind,
    Guid? SectionId, Guid? ExamId, Guid? InvigilatorId, int? SeatCount,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Status, long Version,
    Guid CreatedBy, DateTime CreatedAtUtc, Guid? CancelledBy, DateTime? CancelledAtUtc);
public sealed record CampusScheduleEntry(Guid Id, string Kind, Guid? SectionId, Guid? RoomId,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Status);
public sealed record StudentExamVenueView(Guid ExamId, Guid SectionId, Guid RoomId, string RoomName,
    string BuildingName, DateTime StartsAtUtc, DateTime EndsAtUtc);
