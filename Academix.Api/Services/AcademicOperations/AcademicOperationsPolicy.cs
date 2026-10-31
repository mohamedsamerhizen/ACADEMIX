using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;

namespace Academix.Api.Services.AcademicOperations;

public static class AcademicOperationsPolicy
{
    public static void Slot(SlotUpsert data)
    {
        if (data.Weekday is < 1 or > 7 || data.StartsAtMinute is < 0 or > 1439 ||
            data.DurationMinutes is < 15 or > 600 || data.StartsAtMinute + data.DurationMinutes > 1440)
            throw ApiFault.Bad("Invalid weekday, start or duration.");
        if (data.Room?.Trim().Length > 120) throw ApiFault.Bad("Room must be at most 120 characters.");
    }

    public static void Interval(DateTime start, DateTime end, int maxDays = 120)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc ||
            end <= start || end - start > TimeSpan.FromDays(maxDays))
            throw ApiFault.Bad("Times must be UTC, ordered and within the permitted range.");
    }

    public static void TimeRange(DateTime from, DateTime to, int maxDays = 60)
        => Interval(from, to, maxDays);

    public static string AttendanceStatus(string? status)
    {
        if (status is not ("Present" or "Absent" or "Late" or "Excused"))
            throw ApiFault.Bad("Invalid attendance status.");
        return status;
    }

    public static void AttendanceBatch(AttendanceBatch batch)
    {
        if (batch.Records is null || batch.Records.Count is < 1 or > 200 ||
            batch.Records.Any(x => x.StudentId == Guid.Empty || x.Note is null || x.Note.Length > 500) ||
            batch.Records.GroupBy(x => x.StudentId).Any(x => x.Count() != 1))
            throw ApiFault.Bad("Invalid or repeated attendance records (max 200).");
        foreach (var item in batch.Records) AttendanceStatus(item.Status);
    }

    public static void Calendar(CalendarEventCreate data)
    {
        Interval(data.StartsAtUtc, data.EndsAtUtc, 30);
        if (data.Title is null || data.Title.Trim().Length is < 2 or > 160 ||
            data.Description is null || data.Description.Length > 2000 ||
            data.Audience is not ("All" or "Students" or "Staff"))
            throw ApiFault.Bad("Invalid calendar event.");
    }

    public static void Request(AcademicRequestCreate data)
    {
        if (data.Kind is not ("General" or "AttendanceExcuse" or "EnrollmentException") ||
            data.Subject is null || data.Subject.Trim().Length is < 3 or > 160 ||
            data.Details is null || data.Details.Trim().Length is < 10 or > 3000)
            throw ApiFault.Bad("Invalid request type, subject or details.");
    }

    public static void Decision(AcademicRequestDecision data)
    {
        if (data.Decision is not ("Approved" or "Rejected") ||
            data.Reason is null || data.Reason.Trim().Length is < 3 or > 2000 || data.ExpectedVersion < 1)
            throw ApiFault.Bad("Invalid administrative decision.");
    }
}
