using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Examinations;

public static class QuestionValidation
{
    public static (QuestionBody Body, QuestionAnswerKey Answers) Validate(QuestionDraftRequest? request)
    {
        if (request is null) throw ApiFault.Bad("Question draft is required.");
        var kind = request.Kind?.Trim() ?? "";
        if (kind is not ("SingleChoice" or "MultipleChoice" or "TrueFalse" or "ShortText" or "Essay"))
            throw ApiFault.Bad("Unsupported question type.");
        var stem = request.Stem?.Trim() ?? "";
        if (stem.Length is < 3 or > 8000) throw ApiFault.Bad("Question stem must be 3-8000 characters.");
        var difficulty = request.Difficulty?.Trim() ?? "";
        if (difficulty is not ("Easy" or "Medium" or "Hard")) throw ApiFault.Bad("Difficulty must be Easy, Medium, or Hard.");
        if (request.Points <= 0 || request.Points > 1000 || decimal.Round(request.Points, 2) != request.Points)
            throw ApiFault.Bad("Points must be 0.01-1000 with no more than 2 decimals.");
        var explanation = request.Explanation?.Trim();
        if (explanation?.Length > 4000) throw ApiFault.Bad("Explanation exceeds 4000 characters.");
        if (string.IsNullOrEmpty(explanation)) explanation = null;
        var tags = (request.Tags ?? []).Select(s => s?.Trim() ?? "").ToArray();
        if (tags.Length > 10 || tags.Any(s => s.Length is < 1 or > 50) ||
            tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tags.Length)
            throw ApiFault.Bad("Tags must be 1-10 distinct nonempty values, maximum 50 characters each.");

        var options = (request.Options ?? []).Select(o => new QuestionOptionDto(o?.Id?.Trim() ?? "", o?.Text?.Trim() ?? "")).ToArray();
        var keys = (request.CorrectOptionIds ?? []).Select(s => s?.Trim() ?? "").ToArray();
        var answers = (request.AcceptedAnswers ?? []).Select(s => s?.Trim() ?? "").ToArray();
        if (keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length ||
            answers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != answers.Length)
            throw ApiFault.Bad("Duplicate correct options or accepted answers are not allowed.");

        if (kind is "SingleChoice" or "MultipleChoice")
        {
            if (options.Length is < 2 or > 8 || options.Any(o => o.Id.Length is < 1 or > 20 ||
                !o.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') || o.Text.Length is < 1 or > 1000) ||
                options.Select(o => o.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length)
                throw ApiFault.Bad("Choice questions require 2-8 unique choices with valid ids/text.");
            var validIds = options.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (keys.Length == 0 || keys.Any(k => !validIds.Contains(k)) ||
                (kind == "SingleChoice" && keys.Length != 1) ||
                (kind == "MultipleChoice" && keys.Length < 2) || answers.Length != 0)
                throw ApiFault.Bad("Incorrect answer key for choice question.");
        }
        else if (kind == "TrueFalse")
        {
            if (options.Length != 0 || keys.Length != 1 ||
                keys[0] is not ("true" or "false") || answers.Length != 0)
                throw ApiFault.Bad("TrueFalse requires exactly one key: true or false, and no options.");
        }
        else if (kind == "ShortText")
        {
            if (options.Length != 0 || keys.Length != 0 || answers.Length is < 1 or > 10 ||
                answers.Any(a => a.Length is < 1 or > 500))
                throw ApiFault.Bad("ShortText requires 1-10 accepted answers, each 1-500 characters.");
        }
        else if (options.Length != 0 || keys.Length != 0 || answers.Length != 0)
        {
            throw ApiFault.Bad("Essay questions cannot contain options or an automatic answer key.");
        }

        return (new QuestionBody(kind, stem, difficulty, request.Points, options, explanation, tags),
            new QuestionAnswerKey(keys, answers));
    }
}
