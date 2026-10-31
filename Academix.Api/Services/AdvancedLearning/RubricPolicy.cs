using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.Middleware;

namespace Academix.Api.Services.AdvancedLearning;

public static class RubricPolicy
{
    public static void Validate(IReadOnlyList<RubricCriterionInput>? criteria)
    {
        if (criteria is not { Count: >= 1 and <= 30 } ||
            criteria.Select(x => x.Position).Distinct().Count() != criteria.Count ||
            criteria.Any(x => x.Position < 1 || x.Position > 30 ||
                string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 160 ||
                (x.Description?.Length ?? 0) > 2000 || x.MaxPoints <= 0 || x.MaxPoints > 10000 ||
                x.MaxPoints != decimal.Round(x.MaxPoints, 2)) ||
            criteria.Sum(x => x.MaxPoints) > 10000)
            throw ApiFault.Bad("Invalid rubric criteria (1-30, unique positions, total maximum 10000).");
    }

    public static decimal Total(IReadOnlyList<(Guid Id, decimal Max)> criteria, IReadOnlyList<CriterionScoreInput>? scores)
    {
        if (scores is null || scores.Count != criteria.Count ||
            scores.Select(x => x.CriterionId).Distinct().Count() != scores.Count)
            throw ApiFault.Bad("Every rubric criterion must be graded exactly once.");
        var allowed = criteria.ToDictionary(x => x.Id, x => x.Max);
        foreach (var score in scores)
        {
            if (!allowed.TryGetValue(score.CriterionId, out var max) || score.Points < 0 ||
                score.Points > max || score.Points != decimal.Round(score.Points, 2) ||
                (score.Comment?.Length ?? 0) > 2000)
                throw ApiFault.Bad("Criterion grade outside its rubric limits.");
        }
        return scores.Sum(x => x.Points);
    }
}
