using System.Security.Cryptography;
using System.Text.Json;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Examinations;

public static class ExamAttemptPolicy
{
    public static DateTime Deadline(DateTime startedUtc, int durationMinutes, DateTime closesUtc)
        => startedUtc.AddMinutes(durationMinutes) < closesUtc
            ? startedUtc.AddMinutes(durationMinutes) : closesUtc;

    public static bool CanStart(DateTime nowUtc, DateTime opensUtc, DateTime closesUtc)
        => nowUtc >= opensUtc && nowUtc < closesUtc;

    public static bool IsExpired(string status, DateTime deadlineUtc, DateTime nowUtc)
        => status == "Expired" || status == "InProgress" && nowUtc >= deadlineUtc;

    public static bool CanGrade(string status) => status is "Submitted" or "Expired";

    public static bool SameAnswer(long existingSequence, string existingJson,
        long incomingSequence, string incomingJson)
        => existingSequence == incomingSequence && existingJson == incomingJson;

    public static IReadOnlyList<T> OrderQuestions<T>(IReadOnlyList<T> questions, bool shuffle)
    {
        var result = questions.ToArray();
        if (shuffle)
            for (var i = result.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
        return result;
    }

    public static string NormalizeAnswer(string kind, IReadOnlyList<QuestionOptionDto> options,
        SaveExamAnswerRequest? request)
    {
        if (request is null || request.ClientSequence is < 1 or > 1_000_000_000)
            throw ApiFault.Bad("ClientSequence must be 1-1000000000.");
        var value = request.Answer;
        if (value.ValueKind == JsonValueKind.Undefined)
            throw ApiFault.Bad("Answer is required. Send null to clear an answer.");
        if (value.ValueKind == JsonValueKind.Null) return "null";
        if (value.GetRawText().Length > 16000)
            throw ApiFault.Bad("Answer is too large.");
        var ids = options.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        switch (kind)
        {
            case "SingleChoice":
            {
                if (value.ValueKind != JsonValueKind.String) throw ApiFault.Bad("SingleChoice requires one option id.");
                var id = value.GetString()?.Trim() ?? "";
                if (!ids.Contains(id)) throw ApiFault.Bad("Unknown option id.");
                return JsonSerializer.Serialize(options.First(o => string.Equals(o.Id, id,
                    StringComparison.OrdinalIgnoreCase)).Id);
            }
            case "MultipleChoice":
            {
                if (value.ValueKind != JsonValueKind.Array) throw ApiFault.Bad("MultipleChoice requires option ids.");
                var list = value.EnumerateArray().ToArray();
                if (list.Length > options.Count || list.Any(x => x.ValueKind != JsonValueKind.String))
                    throw ApiFault.Bad("Invalid multiple-choice response.");
                var selected = list.Select(x => x.GetString()?.Trim() ?? "").ToArray();
                if (selected.Any(id => !ids.Contains(id)) ||
                    selected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
                    throw ApiFault.Bad("Unknown or duplicate option id.");
                return JsonSerializer.Serialize(options.Where(o => selected.Contains(o.Id,
                    StringComparer.OrdinalIgnoreCase)).Select(o => o.Id).ToArray());
            }
            case "TrueFalse":
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw ApiFault.Bad("TrueFalse requires a boolean.");
                return value.GetBoolean() ? "true" : "false";
            case "ShortText":
            case "Essay":
            {
                if (value.ValueKind != JsonValueKind.String) throw ApiFault.Bad("Text response is required.");
                var text = value.GetString()?.Trim() ?? "";
                var max = kind == "ShortText" ? 500 : 12000;
                if (text.Length > max) throw ApiFault.Bad($"Answer is limited to {max} characters.");
                return JsonSerializer.Serialize(text);
            }
            default:
                throw ApiFault.Bad("Unsupported question kind.");
        }
    }
}
