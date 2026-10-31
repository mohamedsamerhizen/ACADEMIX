namespace Academix.Api.DTOs.Examinations;

public sealed record ExamDraftRequest(string? Title, string? Description,
    int DurationMinutes, int MaxAttempts, DateTime OpensAtUtc, DateTime ClosesAtUtc,
    bool ShuffleQuestions);
public sealed record ExamEditRequest(long ExpectedVersion, ExamDraftRequest Draft);
public sealed record ExamQuestionSelection(Guid QuestionId, int RevisionNumber, decimal Points);
public sealed record ExamReplaceQuestionsRequest(long ExpectedVersion, List<ExamQuestionSelection>? Questions);
public sealed record ExamPublishRequest(long ExpectedVersion);
// A pool is constrained to one question kind and an optional difficulty.
// null Difficulty means any difficulty in that kind. Allocation is resolved once at publication.
public sealed record ExamPoolRequest(string? Kind, string? Difficulty, int Quantity, decimal Points);
public sealed record ExamReplacePoolsRequest(long ExpectedVersion, List<ExamPoolRequest>? Pools);
public sealed record ExamPoolView(int Position, string Kind, string? Difficulty, int Quantity, decimal Points);
public sealed record ExamAssemblyCheck(bool CanPublish, int ManualQuestions, int PoolQuestions,
    int TotalQuestions, decimal TotalPoints, string? Reason);


public sealed record ExamSummary(Guid Id, Guid SectionId, Guid CourseId, string Title,
    string Status, long Version, int DurationMinutes, int MaxAttempts,
    DateTime OpensAtUtc, DateTime ClosesAtUtc, bool ShuffleQuestions,
    DateTime? PublishedAtUtc);
public sealed record ExamDraftQuestionView(Guid QuestionId, int RevisionNumber, int Position, decimal Points);
public sealed record ExamDetail(ExamSummary Exam, string Description,
    IReadOnlyList<ExamDraftQuestionView> Questions, decimal TotalPoints,
    IReadOnlyList<ExamPoolView>? Pools = null, int PlannedQuestionCount = 0);
// Staff-only preview: deliberately omits correct answers and the explanatory solution.
public sealed record ExamPublishedQuestionView(int Position, Guid QuestionId, int RevisionNumber,
    decimal Points, string Kind, string Stem, IReadOnlyList<QuestionOptionDto> Options);
public sealed record ExamPublishedPreview(ExamSummary Exam,
    IReadOnlyList<ExamPublishedQuestionView> Questions, decimal TotalPoints);
