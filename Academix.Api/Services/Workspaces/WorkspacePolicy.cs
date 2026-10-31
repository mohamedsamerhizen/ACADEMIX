using Academix.Api.Middleware;

namespace Academix.Api.Services.Workspaces;

public static class WorkspacePolicy
{
    public const int PageSize = 20;
    public static int Offset(int page)
    {
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be between 1 and 10000.");
        return (page - 1) * PageSize;
    }

    public static DateTime CalendarEnd(DateTime nowUtc, int days)
    {
        if (days is < 1 or > 60) throw ApiFault.Bad("Days must be between 1 and 60.");
        return nowUtc.AddDays(days);
    }

    public static (string Code, string Name) InstitutionFields(string? code, string? name)
    {
        var cleanCode = code?.Trim().ToUpperInvariant() ?? "";
        var cleanName = name?.Trim() ?? "";
        if (cleanCode.Length is < 2 or > 30 ||
            !cleanCode.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '-'))
            throw ApiFault.Bad("Institution code must contain 2-30 uppercase ASCII letters, digits or hyphens.");
        if (cleanName.Length is < 1 or > 200)
            throw ApiFault.Bad("Institution name must contain 1-200 characters.");
        return (cleanCode, cleanName);
    }
}
