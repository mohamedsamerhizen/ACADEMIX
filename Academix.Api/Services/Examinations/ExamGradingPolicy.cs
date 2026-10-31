using System.Text.Json;
using Academix.Api.DTOs.Examinations;

namespace Academix.Api.Services.Examinations;

public static class ExamGradingPolicy
{
    // No partial credit for multi-select: matches must be exact and order-independent.
    // Essay responses require human review, except unanswered essays score zero.
    public static decimal? AutoScore(QuestionBody question, QuestionAnswerKey key,
        string? answerJson, decimal maximum)
    {
        if (string.IsNullOrEmpty(answerJson) || answerJson == "null") return 0m;
        using var parsed = JsonDocument.Parse(answerJson);
        var answer = parsed.RootElement;
        if (question.Kind == "Essay") return answer.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(answer.GetString()) ? null : 0m;
        if (question.Kind == "TrueFalse")
            return answer.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                key.CorrectOptionIds.Count == 1 &&
                string.Equals(answer.GetBoolean() ? "true" : "false", key.CorrectOptionIds[0],
                    StringComparison.Ordinal) ? maximum : 0m;
        if (question.Kind == "SingleChoice")
            return answer.ValueKind == JsonValueKind.String && key.CorrectOptionIds.Count == 1 &&
                string.Equals(answer.GetString(), key.CorrectOptionIds[0], StringComparison.OrdinalIgnoreCase)
                ? maximum : 0m;
        if (question.Kind == "MultipleChoice")
        {
            if (answer.ValueKind != JsonValueKind.Array) return 0m;
            var values = answer.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            return values.Length == key.CorrectOptionIds.Count &&
                values.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(key.CorrectOptionIds)
                ? maximum : 0m;
        }
        if (question.Kind == "ShortText")
            return answer.ValueKind == JsonValueKind.String &&
                key.AcceptedAnswers.Any(x => Normalize(x) == Normalize(answer.GetString()))
                ? maximum : 0m;
        throw new InvalidOperationException("Unsupported question kind in persisted snapshot.");
    }

    private static string Normalize(string? text)
        => (text ?? "").Trim().Normalize(System.Text.NormalizationForm.FormKC).ToUpperInvariant();

    public static decimal ValidateScore(decimal value, decimal maximum)
    {
        if (value < 0 || value > maximum || decimal.Round(value, 2) != value)
            throw Academix.Api.Middleware.ApiFault.Bad("Score must be between zero and question points with at most two decimals.");
        return value;
    }
}
