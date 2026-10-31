namespace Academix.Api.DTOs.Examinations;

public sealed record ExamGradeSummary(Guid ReportId, Guid AttemptId, Guid ExamId,
    string Status, decimal EarnedPoints, decimal MaximumPoints, int PendingManual,
    long Version, DateTime? PublishedAtUtc);

public sealed record ExamGradeItemView(Guid PublishedQuestionId, int Position, string Kind,
    decimal MaximumPoints, decimal? EarnedPoints, string State, string Feedback,
    string? StudentAnswerJson);

// Staff-only detail includes submitted student answers; never includes answer keys.
public sealed record ExamStaffGradeView(ExamGradeSummary Report,
    IReadOnlyList<ExamGradeItemView> Questions);

// Student-facing result has no answers, answer keys, or unpublished grades.
public sealed record ExamStudentGradeItemView(Guid PublishedQuestionId, int Position,
    string Kind, decimal MaximumPoints, decimal EarnedPoints, string State, string Feedback);
public sealed record ExamStudentResult(ExamGradeSummary Report,
    IReadOnlyList<ExamStudentGradeItemView> Questions, ExamAppealView? Appeal);

public sealed record ExamManualGradeRequest(long ExpectedVersion, decimal EarnedPoints,
    string? Feedback, string Reason);
public sealed record ExamPublishGradesRequest(long ExpectedVersion);
public sealed record ExamAppealRequest(string Message);
public sealed record ExamResolveAppealRequest(long ExpectedVersion, bool Accept, string Resolution,
    Guid? PublishedQuestionId = null, decimal? CorrectedPoints = null);
public sealed record ExamAppealView(Guid Id, Guid ReportId, string Status, string Message,
    string Resolution, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);
public sealed record ExamReviewQueueItem(Guid AttemptId, Guid StudentId, string AttemptStatus,
    bool HasReport, string? ReportStatus, int? PendingManual);

public sealed record ExamGradeAuditView(Guid Id, Guid? PublishedQuestionId, Guid ActorId,
    string Action, decimal? PreviousPoints, decimal? NewPoints, string Reason, DateTime AtUtc);
