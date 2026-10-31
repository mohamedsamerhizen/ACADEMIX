using Academix.Api.Middleware;
using Academix.Api.DTOs.AcademicOperations;

namespace Academix.Api.Services.AcademicOperations;

/// <summary>Pure shared weekly scheduling rules. Intervals are [start, end) in institution-local minutes.</summary>
public static class CampusSchedulingRules
{
    public static bool Overlaps(int firstStart, int firstDuration, int secondStart, int secondDuration)
        => firstStart < secondStart + secondDuration && secondStart < firstStart + firstDuration;

    public static bool Overlaps(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
        => firstStart < secondEnd && secondStart < firstEnd;

    public static string RevisionReason(string? reason, string defaultReason)
    {
        if (reason is null) return defaultReason;
        var trimmed = reason.Trim();
        if (trimmed.Length is < 3 or > 500)
            throw ApiFault.Bad("Timetable change reason must be 3-500 characters.");
        return trimmed;
    }

    public static bool SameSlot(TimetableSlotState a, TimetableSlotState b)
        => a.Weekday == b.Weekday && a.StartsAtMinute == b.StartsAtMinute &&
            a.DurationMinutes == b.DurationMinutes && a.Room == b.Room &&
            a.RoomId == b.RoomId && a.IsActive == b.IsActive;

    /// <summary>Session changes are only permitted before a session begins and before attendance is recorded.</summary>
    public static void Reschedule(DateTime nowUtc, DateTime startsUtc, DateTime endsUtc,
        string? topic, string? reason)
    {
        if (startsUtc.Kind != DateTimeKind.Utc || endsUtc.Kind != DateTimeKind.Utc ||
            startsUtc <= nowUtc || endsUtc <= startsUtc || endsUtc - startsUtc > TimeSpan.FromHours(12) ||
            string.IsNullOrWhiteSpace(topic) || topic.Trim().Length is < 2 or > 200)
            throw ApiFault.Bad("Invalid future UTC session appointment or topic.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 500)
            throw ApiFault.Bad("A session reschedule requires a reason (3-500 characters).");
    }

    public static bool SameSession(ClassSessionState a, ClassSessionState b)
        => a.StartsAtUtc == b.StartsAtUtc && a.EndsAtUtc == b.EndsAtUtc &&
            a.Topic == b.Topic && a.Status == b.Status && a.TimetableSlotId == b.TimetableSlotId &&
            a.RoomId == b.RoomId;

    /// <summary>A linked slot owns its room; standalone appointments may explicitly allocate or release one.</summary>
    public static Guid? ResolveSessionRoom(Guid? slotId, Guid? slotRoomId, Guid? currentRoomId,
        Guid? requestedRoomId, bool removeAssignment)
    {
        if (removeAssignment && requestedRoomId.HasValue)
            throw ApiFault.Bad("Cannot allocate and remove a room in the same request.");
        if (slotId.HasValue)
        {
            if (removeAssignment || (requestedRoomId.HasValue && requestedRoomId != slotRoomId))
                throw ApiFault.Bad("A linked session must use its timetable slot room.");
            return slotRoomId;
        }
        return removeAssignment ? null : requestedRoomId ?? currentRoomId;
    }

    public static void OccupancyInterval(DateTime startUtc, DateTime endUtc)
    {
        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc ||
            endUtc <= startUtc || endUtc - startUtc > TimeSpan.FromDays(31))
            throw ApiFault.Bad("Occupancy interval must be UTC and no longer than 31 days.");
    }

    public static void Building(string? code, string? name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is > 30 or < 2 ||
            string.IsNullOrWhiteSpace(name) || name.Trim().Length is > 160 or < 2)
            throw ApiFault.Bad("Invalid campus building code or name.");
    }

    public static void Room(string? code, string? name, string? kind, int capacity)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is > 30 or < 1 ||
            string.IsNullOrWhiteSpace(name) || name.Trim().Length is > 160 or < 2 ||
            kind is not ("Classroom" or "Laboratory" or "LectureHall" or "SeminarRoom") ||
            capacity is < 1 or > 10000)
            throw ApiFault.Bad("Invalid campus room attributes or capacity.");
    }
}
