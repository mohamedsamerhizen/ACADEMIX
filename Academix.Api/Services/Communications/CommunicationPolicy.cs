using Academix.Api.Middleware;

namespace Academix.Api.Services.Communications;

public static class CommunicationPolicy
{
    public static (Guid Low, Guid High) OrderedMembers(Guid one, Guid two)
    {
        if (one == Guid.Empty || two == Guid.Empty || one == two) throw ApiFault.Bad("Invalid message participants.");
        return one.CompareTo(two) < 0 ? (one, two) : (two, one);
    }

    public static string Text(string? text, int limit, string field)
    {
        var value = text?.Trim() ?? "";
        if (value.Length < 1 || value.Length > limit) throw ApiFault.Bad($"Invalid {field} (1-{limit} characters).");
        return value;
    }

    public static void Page(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100) throw ApiFault.Bad("Invalid pagination.");
    }

    public static void Schedule(string? audience, DateTime when)
    {
        if (audience is not ("All" or "Students" or "Staff")) throw ApiFault.Bad("Invalid audience.");
        if (when.Kind != DateTimeKind.Utc || when < DateTime.UtcNow.AddMinutes(-2) || when > DateTime.UtcNow.AddDays(366))
            throw ApiFault.Bad("Scheduled time must be UTC, from now through one year ahead.");
    }
}
