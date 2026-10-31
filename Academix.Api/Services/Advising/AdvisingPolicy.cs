using Academix.Api.Middleware;

namespace Academix.Api.Services.Advising;

public static class AdvisingPolicy
{
    public const int PageSize = 25;
    public static int Offset(int page)
    {
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be between 1 and 10000.");
        return checked((page - 1) * PageSize);
    }

    public static string Required(string? value, int max, string field)
    {
        var s = value?.Trim() ?? "";
        if (s.Length is 0 || s.Length > max) throw ApiFault.Bad($"Invalid {field}; expected 1..{max} characters.");
        return s;
    }

    public static string RiskIndicator(int attendanceMarks, int attended,
        decimal? publishedGrade, decimal passingPercentage)
    {
        if (publishedGrade.HasValue && publishedGrade.Value < passingPercentage) return "Attention";
        if (attendanceMarks >= 3 && 100m * attended / attendanceMarks < 75m) return "Attention";
        if (publishedGrade.HasValue || attendanceMarks >= 3) return "OnTrack";
        return "InsufficientData";
    }

    public static decimal? AttendanceRate(int marks, int attended)
        => marks <= 0 ? null : Math.Round(100m * attended / marks, 2, MidpointRounding.AwayFromZero);

    public static void CanChangeStatus(string current, string next, int remainingTasks)
    {
        if (next is not ("Open" or "InProgress" or "Resolved") || current == next)
            throw ApiFault.Bad("Invalid case status transition.");
        if (next == "Resolved" && remainingTasks > 0)
            throw ApiFault.Conflict("Complete all advising tasks before resolving the case.");
        if (current == "Resolved" && next != "InProgress")
            throw ApiFault.Conflict("Resolved cases can only reopen as InProgress.");
    }
}
