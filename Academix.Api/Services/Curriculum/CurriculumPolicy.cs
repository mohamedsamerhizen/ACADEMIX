using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Curriculum;

public static class CurriculumPolicy
{
    public static decimal ValidateCredits(decimal value)
    {
        if (value <= 0 || value > 30 || decimal.Round(value, 2) != value)
            throw ApiFault.Bad("Credit hours must be 0.01-30.00 with at most two decimal places.");
        return value;
    }

    public static void ValidatePlan(CreateCurriculumPlan request)
    {
        if (request.CatalogYear < 2000 || request.CatalogYear > 2200 || request.Revision is < 1 or > 100 ||
            string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            throw ApiFault.Bad("Invalid catalog year, revision, or plan name.");
        if (request.MinimumTotalCredits <= 0 || request.MinimumTotalCredits > 500 ||
            request.MinimumElectiveCredits < 0 || request.MinimumElectiveCredits > request.MinimumTotalCredits ||
            decimal.Round(request.MinimumTotalCredits, 2) != request.MinimumTotalCredits ||
            decimal.Round(request.MinimumElectiveCredits, 2) != request.MinimumElectiveCredits ||
            request.MinimumCumulativeGpa < 0 || request.MinimumCumulativeGpa > 4 ||
            decimal.Round(request.MinimumCumulativeGpa, 2) != request.MinimumCumulativeGpa)
            throw ApiFault.Bad("Invalid minimum credits or graduation GPA.");
    }

    public static void ValidateCourses(IReadOnlyList<CurriculumCourseInput> courses)
    {
        if (courses is null || courses.Count is < 1 or > 250) throw ApiFault.Bad("A curriculum requires 1-250 courses.");
        if (courses.Select(x => x.CourseId).Distinct().Count() != courses.Count)
            throw ApiFault.Bad("Each course may occur only once per curriculum.");
        foreach (var c in courses)
        {
            if (c.CourseId == Guid.Empty || c.RequirementKind is not ("Required" or "Elective") ||
                c.RecommendedTerm is < 1 or > 24) throw ApiFault.Bad("Invalid course requirement.");
            ValidateCredits(c.CreditHours);
        }
    }

    public static void ValidateScale(IReadOnlyList<CurriculumBandInput> bands)
    {
        if (bands is null || bands.Count is < 2 or > 30) throw ApiFault.Bad("A GPA scale requires 2-30 bands.");
        var ordered = bands.OrderBy(x => x.MinimumPercentage).ToArray();
        if (ordered[0].MinimumPercentage != 0) throw ApiFault.Bad("The first GPA band must start at zero.");
        for (var i = 0; i < ordered.Length; i++)
        {
            var b = ordered[i];
            if (b.MinimumPercentage < 0 || b.MinimumPercentage > 100 ||
                decimal.Round(b.MinimumPercentage, 2) != b.MinimumPercentage ||
                b.GradePoints < 0 || b.GradePoints > 4 ||
                decimal.Round(b.GradePoints, 2) != b.GradePoints ||
                string.IsNullOrWhiteSpace(b.Letter) || b.Letter.Trim().Length > 10)
                throw ApiFault.Bad("Invalid GPA band.");
            if (i > 0 && (b.MinimumPercentage <= ordered[i - 1].MinimumPercentage ||
                b.GradePoints < ordered[i - 1].GradePoints))
                throw ApiFault.Bad("GPA bands must have unique increasing thresholds and nondecreasing points.");
        }
    }

    public static CurriculumBandInput Band(decimal percentage, IReadOnlyList<CurriculumBandInput> bands)
    {
        if (percentage is < 0 or > 100) throw ApiFault.Bad("Invalid transcript percentage.");
        return bands.Where(x => x.MinimumPercentage <= percentage)
            .OrderByDescending(x => x.MinimumPercentage).First();
    }

    public static decimal? WeightedGpa(IEnumerable<(decimal Credits, decimal Points)> lines)
    {
        var values = lines.ToArray();
        var hours = values.Sum(x => x.Credits);
        return hours == 0 ? null : decimal.Round(values.Sum(x => x.Credits * x.Points) / hours, 2,
            MidpointRounding.AwayFromZero);
    }

    // A repeated course is counted once using the highest percentage; a failed course
    // earns no graduation credits, but its best attempt contributes to the GPA.
    public static IReadOnlyList<TranscriptAttempt> BestAttempts(IEnumerable<TranscriptAttempt> attempts)
        => attempts.GroupBy(x => x.CourseId).Select(g => g.OrderByDescending(x => x.Percentage)
            .ThenByDescending(x => x.Passed).ThenBy(x => x.TermId).First())
            .OrderBy(x => x.CourseCode).ToArray();

    public static CurriculumAuditResult Audit(IReadOnlyList<CurriculumCourseInput> courses,
        IReadOnlyList<TranscriptAttempt> attempts, IReadOnlyList<CurriculumBandInput> bands,
        decimal minCredits, decimal minElectives, decimal minGpa, IReadOnlyCollection<Guid>? approvedEquivalencies = null)
    {
        var best = BestAttempts(attempts).ToDictionary(x => x.CourseId);
        var equivalentCourses = approvedEquivalencies?.ToHashSet() ?? new HashSet<Guid>();
        var completed = courses.Where(c => equivalentCourses.Contains(c.CourseId) ||
            best.TryGetValue(c.CourseId, out var a) && a.Passed).ToArray();
        var required = completed.Where(x => x.RequirementKind == "Required").Sum(x => x.CreditHours);
        var electives = completed.Where(x => x.RequirementKind == "Elective").Sum(x => x.CreditHours);
        var missing = courses.Count(c => c.RequirementKind == "Required" &&
            !equivalentCourses.Contains(c.CourseId) &&
            (!best.TryGetValue(c.CourseId, out var a) || !a.Passed));
        var graded = courses.Where(c => best.ContainsKey(c.CourseId)).Select(c =>
            (c.CreditHours, Band(best[c.CourseId].Percentage, bands).GradePoints));
        var gpa = WeightedGpa(graded);
        var blockers = new List<string>();
        if (missing > 0) blockers.Add("Missing required courses.");
        if (electives < minElectives) blockers.Add("Insufficient elective credits.");
        if (required + electives < minCredits) blockers.Add("Insufficient total credits.");
        if (gpa is null || gpa.Value < minGpa) blockers.Add("Graduation GPA requirement not met.");
        return new CurriculumAuditResult(required, electives, gpa, missing, blockers);
    }
}

public sealed record TranscriptAttempt(Guid CourseId, Guid TermId, string CourseCode,
    decimal Percentage, bool Passed);
public sealed record CurriculumAuditResult(decimal RequiredCredits, decimal ElectiveCredits,
    decimal? Gpa, int MissingRequired, IReadOnlyList<string> Blockers);
