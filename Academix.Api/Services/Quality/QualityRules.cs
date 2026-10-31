using Academix.Api.DTOs.Quality;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Quality;

public static class QualityRules
{
    public const string EvidenceKind = "PublishedCourseGradeProxy";

    public static string Code(string? code)
    {
        var value = code?.Trim().ToUpperInvariant() ?? "";
        if (value.Length is < 2 or > 30 || !value.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
            throw ApiFault.Bad("Outcome code requires 2-30 ASCII letters, digits, hyphens or underscores.");
        return value;
    }

    public static string Description(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length is < 10 or > 1500) throw ApiFault.Bad("Description must contain 10-1500 characters.");
        return text;
    }

    public static decimal Target(decimal value)
    {
        if (value is < 0m or > 100m) throw ApiFault.Bad("Target passing rate must be 0-100.");
        return value;
    }

    public static QualityOutcomeEvidence Evidence(Guid outcomeId, string code, decimal target, int mappings,
        int total, int passing)
    {
        if (mappings < 0 || total < 0 || passing < 0 || passing > total)
            throw ApiFault.Bad("Invalid quality evidence counts.");
        var rate = total == 0 ? (decimal?)null : Math.Round(100m * passing / total, 2, MidpointRounding.AwayFromZero);
        var status = mappings == 0 ? "Unmapped" : rate is null ? "NoPublishedEvidence"
            : rate.Value >= target ? "ProxyTargetMet" : "ProxyBelowTarget";
        return new QualityOutcomeEvidence(outcomeId, code, target, mappings, total, passing, rate, status, EvidenceKind);
    }

    public static string NextActionStatus(string current, string requested)
    {
        if (current == "Completed") throw ApiFault.Conflict("Completed improvement actions are immutable.");
        if ((current == "Open" && (requested == "InProgress" || requested == "Completed")) ||
            (current == "InProgress" && (requested == "Open" || requested == "Completed"))) return requested;
        throw ApiFault.Conflict("Invalid quality action transition.");
    }
    public static bool CriterionIsProficient(decimal points, decimal maxPoints, decimal threshold)
    {
        Target(threshold);
        if (maxPoints <= 0m || points < 0m || points > maxPoints)
            throw ApiFault.Bad("Invalid rubric criterion score and maximum points.");
        return 100m * points >= threshold * maxPoints;
    }

    public static QualityDirectEvidence DirectEvidence(Guid outcomeId, string code, decimal target,
        int mappedCriteria, int observations, int proficient)
    {
        Target(target);
        if (mappedCriteria < 0 || observations < 0 || proficient < 0 || proficient > observations)
            throw ApiFault.Bad("Invalid direct evidence counts.");
        decimal? rate = observations == 0 ? null : Math.Round(100m * proficient / observations, 2, MidpointRounding.AwayFromZero);
        var status = mappedCriteria == 0 ? "UnmappedDirect" : rate is null ? "NoGradedRubricEvidence"
            : rate.Value >= target ? "DirectCriterionTargetMet" : "DirectCriterionBelowTarget";
        return new QualityDirectEvidence(outcomeId, code, mappedCriteria, observations, proficient, rate,
            target, status, "GradedTeamProjectRubricCriterion");
    }

    public static string PackLabel(string? value, int maxLength)
    {
        var result = value?.Trim() ?? "";
        if (result.Length < 3 || result.Length > maxLength || result.Any(char.IsControl))
            throw ApiFault.Bad("Evidence label must contain 3-200 visible characters.");
        return result;
    }

}
