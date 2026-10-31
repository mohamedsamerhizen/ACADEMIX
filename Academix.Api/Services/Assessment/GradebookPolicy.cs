using Academix.Api.DTOs.Assessment;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Assessment;

public static class GradebookPolicy
{
    public static decimal Percent(decimal earned, decimal maximum)
    {
        if (maximum <= 0 || earned < 0 || earned > maximum)
            throw ApiFault.Bad("Invalid grading range.");
        return decimal.Round(100m * earned / maximum, 4, MidpointRounding.AwayFromZero);
    }

    public static void Weight(decimal value)
    {
        if (value <= 0m || value > 100m || value != decimal.Round(value, 2))
            throw ApiFault.Bad("Weights must be between 0 and 100 with at most two decimals.");
    }

    public static string Letter(decimal percentage) => percentage switch
    {
        >= 90m => "A", >= 80m => "B", >= 70m => "C", >= 60m => "D", _ => "F"
    };

    public static decimal Calculate(IReadOnlyList<(decimal CategoryWeight, IReadOnlyList<(decimal Weight, decimal Earned, decimal Maximum)> Items)> categories)
    {
        if (categories.Count == 0 || categories.Sum(c => c.CategoryWeight) != 100m)
            throw ApiFault.Conflict("Category weights must total 100%.");
        decimal total = 0;
        foreach (var cat in categories)
        {
            if (cat.Items.Count == 0 || cat.Items.Sum(x => x.Weight) != 100m)
                throw ApiFault.Conflict("Component weights in each category must total 100%.");
            foreach (var item in cat.Items)
                total += cat.CategoryWeight / 100m * item.Weight / 100m * Percent(item.Earned, item.Maximum);
        }
        return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    public static void ValidateSource(string kind, Guid? sourceId)
    {
        if (kind is not ("Manual" or "Assignment" or "Exam" or "GroupProject"))
            throw ApiFault.Bad("Unsupported component source.");
        if (kind == "Manual" && sourceId is not null || kind != "Manual" && (!sourceId.HasValue || sourceId == Guid.Empty))
            throw ApiFault.Bad("Invalid component source reference.");
    }
}
