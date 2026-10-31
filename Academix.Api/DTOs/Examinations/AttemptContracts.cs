using System.Text.Json;

namespace Academix.Api.DTOs.Examinations;

public sealed record AvailableExamView(Guid ExamId, Guid SectionId, string Title,
    DateTime OpensAtUtc, DateTime ClosesAtUtc, int DurationMinutes,
    int MaxAttempts, int AttemptsUsed, bool CanStart, bool CanResume);

public sealed record ExamAttemptSummary(Guid Id, Guid ExamId, int AttemptNumber,
    string Status, DateTime StartedAtUtc, DateTime DeadlineUtc,
    DateTime? SubmittedAtUtc, DateTime ServerNowUtc);

// Student-safe projection: never include QuestionBody.Explanation or AnswerKeyJson.
public sealed record StudentExamQuestion(Guid PublishedQuestionId, int Position, decimal Points,
    string Kind, string Stem, IReadOnlyList<QuestionOptionDto> Options);

public sealed record SavedExamAnswer(Guid PublishedQuestionId, JsonElement Answer,
    long ClientSequence, DateTime SavedAtUtc);

public sealed record ExamAttemptDetail(ExamAttemptSummary Attempt, string ExamTitle,
    IReadOnlyList<StudentExamQuestion> Questions,
    IReadOnlyList<SavedExamAnswer> SavedAnswers);

// The client must increase ClientSequence for each new value PER question.
// Retrying the same sequence and same answer is idempotent.
public sealed record SaveExamAnswerRequest(long ClientSequence, JsonElement Answer);
public sealed record ExamSubmissionReceipt(Guid AttemptId, Guid ExamId,
    int AttemptNumber, string Status, DateTime SubmittedAtUtc, int AnsweredQuestionCount);
