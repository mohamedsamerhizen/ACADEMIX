using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Examinations;

public static class ExamValidation
{
    public static (string Title, string Description) ValidateDraft(ExamDraftRequest? value)
    {
        if (value is null) throw ApiFault.Bad("Exam draft is required.");
        var title = value.Title?.Trim() ?? "";
        var description = value.Description?.Trim() ?? "";
        if (title.Length is < 3 or > 200) throw ApiFault.Bad("Exam title must be 3-200 characters.");
        if (description.Length > 2000) throw ApiFault.Bad("Exam description is too long.");
        if (value.DurationMinutes is < 1 or > 480) throw ApiFault.Bad("Exam duration must be 1-480 minutes.");
        if (value.MaxAttempts is < 1 or > 10) throw ApiFault.Bad("Max attempts must be 1-10.");
        if (value.OpensAtUtc.Kind != DateTimeKind.Utc || value.ClosesAtUtc.Kind != DateTimeKind.Utc ||
            value.ClosesAtUtc <= value.OpensAtUtc ||
            value.ClosesAtUtc - value.OpensAtUtc > TimeSpan.FromDays(180))
            throw ApiFault.Bad("Exam window must use UTC and have a positive duration no longer than 180 days.");
        return (title, description);
    }

    public static IReadOnlyList<ExamQuestionSelection> ValidateQuestions(ExamReplaceQuestionsRequest? request, bool allowEmpty = false)
    {
        if (request is null) throw ApiFault.Bad("Question selection is required.");
        var questions = request.Questions ?? [];
        if (questions.Count > 200 || (questions.Count == 0 && !allowEmpty))
            throw ApiFault.Bad("An exam needs 1-200 questions.");
        if (questions.Any(q => q.QuestionId == Guid.Empty || q.RevisionNumber < 1 ||
            q.Points <= 0 || q.Points > 1000 || decimal.Round(q.Points, 2) != q.Points))
            throw ApiFault.Bad("Invalid question identifier, revision or points.");
        if (questions.Select(q => q.QuestionId).Distinct().Count() != questions.Count)
            throw ApiFault.Bad("The same question cannot appear twice in an exam.");
        if (questions.Sum(q => q.Points) > 10000m)
            throw ApiFault.Bad("Total exam score cannot exceed 10000.");
        return questions;
    }
}
