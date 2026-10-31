namespace Academix.Api.DTOs.Assessment;

public sealed record ConfigureGradePolicy(decimal PassingPercentage, long ExpectedVersion);
public sealed record GradeCategoryInput(string Name, decimal WeightPercentage);
public sealed record GradeComponentInput(Guid CategoryId, string Name, string SourceKind, Guid? SourceId,
    decimal WeightPercentage, decimal MaximumPoints);
public sealed record GradePolicyView(Guid SectionId, decimal PassingPercentage, long Version,
    DateTime? PublishedAtUtc, IReadOnlyList<GradeCategoryView> Categories);
public sealed record GradeCategoryView(Guid Id, string Name, decimal WeightPercentage,
    IReadOnlyList<GradeComponentView> Components);
public sealed record GradeComponentView(Guid Id, Guid CategoryId, string Name, string SourceKind,
    Guid? SourceId, decimal WeightPercentage, decimal MaximumPoints);
public sealed record ManualGradeRequest(decimal Points, long? ExpectedVersion);
public sealed record ManualGradeView(Guid ComponentId, Guid StudentId, decimal Points, long Version);
public sealed record GradeLineView(Guid ComponentId, string Name, string SourceKind,
    decimal? EarnedPoints, decimal MaximumPoints, decimal WeightPercentage, bool IsMissing);
public sealed record GradeStudentPreview(Guid StudentId, decimal? Percentage, bool IsComplete,
    IReadOnlyList<GradeLineView> Lines);
public sealed record GradebookPreview(Guid SectionId, int ActiveStudents, int IncompleteStudents,
    IReadOnlyList<GradeStudentPreview> Students);
public sealed record PublishGradebookRequest(long ExpectedVersion);
public sealed record GradebookResultView(Guid SectionId, Guid StudentId, decimal Percentage,
    string Letter, bool Passed, long Version, DateTime PublishedAtUtc, bool RequiresReconciliation = false, bool IsManualOverride = false);
public sealed record CorrectGradebookResult(decimal Percentage, string Reason, long ExpectedVersion);
public sealed record GradebookAuditView(Guid StudentId, Guid ActorId, string Action,
    decimal? PreviousPercentage, decimal NewPercentage, string Reason, DateTime AtUtc, string EvidenceJson = "{}");
public sealed record TermGradeSummary(Guid TermId, Guid StudentId, int PublishedSections,
    decimal UnweightedAveragePercentage, IReadOnlyList<GradebookResultView> Results);

public sealed record ReconcileGradebookResult(Guid RequestId, string Decision, string Reason, long ExpectedVersion);
