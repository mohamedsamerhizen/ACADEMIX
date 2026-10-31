namespace Academix.Api.DTOs.Examinations;

public sealed record QuestionOptionDto(string Id, string Text);

// Kept separate: answer keys must never be returned in the future student-facing exam API.
public sealed record QuestionDraftRequest(
    string? Kind,
    string? Stem,
    string? Difficulty,
    decimal Points,
    List<QuestionOptionDto>? Options,
    List<string>? CorrectOptionIds,
    List<string>? AcceptedAnswers,
    string? Explanation,
    List<string>? Tags);

public sealed record QuestionEditRequest(long ExpectedVersion, QuestionDraftRequest Draft);
public sealed record QuestionArchiveRequest(long ExpectedVersion, bool IsArchived);

public sealed record QuestionBody(
    string Kind, string Stem, string Difficulty, decimal Points,
    IReadOnlyList<QuestionOptionDto> Options, string? Explanation, IReadOnlyList<string> Tags);

public sealed record QuestionAnswerKey(
    IReadOnlyList<string> CorrectOptionIds, IReadOnlyList<string> AcceptedAnswers);

public sealed record BankQuestionSummary(
    Guid Id, Guid CourseId, string Kind, string Difficulty,
    int CurrentRevision, long Version, bool IsArchived,
    DateTime UpdatedAtUtc);

public sealed record QuestionRevisionResponse(
    Guid RevisionId, Guid QuestionId, int RevisionNumber,
    QuestionBody Body, QuestionAnswerKey AnswerKey,
    Guid CreatedBy, DateTime CreatedAtUtc);

public sealed record BankQuestionDetail(BankQuestionSummary Question, QuestionRevisionResponse Revision);
