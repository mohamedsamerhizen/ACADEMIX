using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;

namespace Academix.Api.Services.AcademicOperations;

/// <summary>UTC concrete allocations use half-open intervals and never change online exam windows.</summary>
public static class CampusReservationPolicy
{
    public static void Validate(CampusReservationCreate input, DateTime nowUtc)
    {
        if (input.ClientRequestId == Guid.Empty || input.RoomId == Guid.Empty ||
            input.StartsAtUtc.Kind != DateTimeKind.Utc || input.EndsAtUtc.Kind != DateTimeKind.Utc ||
            input.StartsAtUtc <= nowUtc || input.EndsAtUtc <= input.StartsAtUtc)
            throw ApiFault.Bad("A future UTC interval and nonempty request/room IDs are required.");
        if (input.Kind is not ("Maintenance" or "Exam"))
            throw ApiFault.Bad("Unsupported reservation kind.");
        var max = input.Kind == "Maintenance" ? TimeSpan.FromDays(31) : TimeSpan.FromHours(12);
        if (input.EndsAtUtc - input.StartsAtUtc > max ||
            input.Kind == "Exam" && input.EndsAtUtc - input.StartsAtUtc < TimeSpan.FromMinutes(15))
            throw ApiFault.Bad("Reservation interval exceeds allowed duration or is too short.");
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 3 or > 500)
            throw ApiFault.Bad("A meaningful reason is required (3-500 characters).");
        if (input.Kind == "Maintenance" && (input.ExamId.HasValue || input.InvigilatorId.HasValue || input.SeatCount.HasValue))
            throw ApiFault.Bad("Maintenance cannot refer to an exam, invigilator or seats.");
        if (input.Kind == "Exam" && (input.ExamId is null || input.ExamId == Guid.Empty ||
                input.InvigilatorId is null || input.InvigilatorId == Guid.Empty || input.SeatCount is null or < 1 or > 10000))
            throw ApiFault.Bad("Physical examinations require exam, invigilator and seat count.");
    }

    public static void ValidateCancel(CampusReservationCancel input)
    {
        if (input.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(input.Reason) ||
            input.Reason.Trim().Length is < 3 or > 500)
            throw ApiFault.Bad("Cancellation requires a valid version and reason.");
    }

    public static bool Matches(CampusReservationCreate input, Academix.Api.Domain.AcademicOperations.CampusRoomReservation saved)
        => input.RoomId == saved.RoomId && input.Kind == saved.Kind &&
           input.StartsAtUtc.Ticks / 10 == saved.StartsAtUtc.Ticks / 10 && input.EndsAtUtc.Ticks / 10 == saved.EndsAtUtc.Ticks / 10 &&
           input.Reason.Trim() == saved.Reason && input.ExamId == saved.ExamId &&
           input.InvigilatorId == saved.InvigilatorId && input.SeatCount == saved.SeatCount;
}
