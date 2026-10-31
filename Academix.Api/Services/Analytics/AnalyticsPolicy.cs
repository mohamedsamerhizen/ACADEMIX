using System.Globalization;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Analytics;

public static class AnalyticsPolicy
{
    public const int PageSize = 20;
    public const int MaximumCsvRows = 2000;

    public static int Offset(int page)
    {
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be between 1 and 10000.");
        return checked((page - 1) * PageSize);
    }

    public static decimal? Rate(int numerator, long denominator)
        => denominator <= 0 ? null : Math.Round(100m * numerator / denominator, 2, MidpointRounding.AwayFromZero);

    // Always quote, escape embedded quotes, and neutralize spreadsheet formula injection.
    public static string CsvCell(string? value)
    {
        var data = value ?? "";
        var leadingTrimmed = data.TrimStart(' ', '\t', '\r', '\n');
        if ((data.Length > 0 && "\t\r\n".Contains(data[0])) ||
            (leadingTrimmed.Length > 0 && "=+-@".Contains(leadingTrimmed[0])))
            data = "'" + data;
        return "\"" + data.Replace("\"", "\"\"") + "\"";
    }

    public static string CsvRow(params object?[] values)
        => string.Join(",", values.Select(x => CsvCell(x is IFormattable f
            ? f.ToString(null, CultureInfo.InvariantCulture) : x?.ToString())));
}
