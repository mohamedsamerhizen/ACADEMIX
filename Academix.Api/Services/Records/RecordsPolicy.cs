using Academix.Api.Middleware;

namespace Academix.Api.Services.Records;

public static class RecordsPolicy
{
    public static decimal Percentage(decimal value)
    {
        if (value < 0 || value > 100 || decimal.Round(value, 2) != value)
            throw ApiFault.Bad("Minimum percentage must be 0-100 with at most two decimals.");
        return value;
    }

    public static void DistinctCourses(Guid course, Guid required, IReadOnlyDictionary<Guid, Guid[]> existing)
    {
        if (course == Guid.Empty || required == Guid.Empty || course == required)
            throw ApiFault.Bad("A course cannot require itself.");
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>(); pending.Push(required);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == course) throw ApiFault.Conflict("Prerequisite relationship would create a cycle.");
            if (visited.Add(current) && existing.TryGetValue(current, out var parents))
                foreach (var parent in parents) pending.Push(parent);
        }
    }

    public static bool Satisfies(decimal minimum, IEnumerable<(decimal Percentage, bool Passed)> published)
        => published.Any(x => x.Passed && x.Percentage >= minimum);

    public static string Standing(IReadOnlyList<(decimal Percentage, bool Passed)> grades)
    {
        if (grades.Count == 0) return "InsufficientData";
        if (grades.Any(x => !x.Passed)) return "Probation";
        return grades.Average(x => x.Percentage) < 60m ? "AtRisk" : "GoodStanding";
    }

    public static string Transition(DateOnly startsOn, DateOnly endsOn, DateOnly today,
        string action, bool gradebookPublished)
    {
        if (gradebookPublished || today > endsOn) throw ApiFault.Conflict("Registration changes are closed.");
        return action switch
        {
            "Drop" when today < startsOn => "Dropped",
            "Withdraw" when today >= startsOn && today <= endsOn => "Withdrawn",
            _ => throw ApiFault.Bad("Action is not allowed for the current academic term.")
        };
    }

    public static string Reason(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length is < 5 or > 500) throw ApiFault.Bad("Reason must be 5-500 characters.");
        return text;
    }
}
