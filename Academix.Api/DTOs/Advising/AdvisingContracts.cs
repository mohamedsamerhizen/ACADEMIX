namespace Academix.Api.DTOs.Advising;

public sealed record StudentRisk(Guid StudentId, string? Email, int RecordedAttendance,
    decimal? AttendancePercentage, decimal? PublishedGradePercentage, decimal? PassingPercentage,
    string Indicator, int ActiveCases, bool RequiresReconciliation = false);

public sealed record CreateAdvisingCase(Guid ClientRequestId, Guid StudentId, string Title, string InternalSummary);
public sealed record AdvisingCaseView(Guid Id, Guid InstitutionId, Guid SectionId, Guid StudentId,
    string Title, string InternalSummary, string Status, long Version, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record StudentCaseView(Guid Id, Guid SectionId, string Title, string Status,
    DateTime CreatedAtUtc, IReadOnlyList<AdvisingTaskView> Tasks);
public sealed record CreateAdvisingNote(string Body);
public sealed record AdvisingNoteView(Guid Id, Guid AuthorId, string Body, DateTime AtUtc);
public sealed record CreateAdvisingTask(string Description, DateTime? DueAtUtc);
public sealed record AdvisingTaskView(Guid Id, string Description, DateTime? DueAtUtc,
    string Status, long Version, DateTime? ReportedAtUtc, DateTime? ReviewedAtUtc);
public sealed record TaskTransition(long ExpectedVersion);
public sealed record ReviewAdvisingTask(long ExpectedVersion, bool Accept, string Reason);
public sealed record ChangeAdvisingStatus(long ExpectedVersion, string Status, string Reason);
public sealed record AdvisingEventView(Guid Id, Guid ActorId, string Kind, string Description, DateTime AtUtc);
public sealed record AdvisingCaseDetail(AdvisingCaseView Case,
    IReadOnlyList<AdvisingNoteView> InternalNotes, IReadOnlyList<AdvisingTaskView> Tasks,
    IReadOnlyList<AdvisingEventView> Audit);
public sealed record AdvisingOverview(Guid InstitutionId, int Open, int InProgress, int Resolved,
    int PendingTasks, int ReportedTasks, DateTime GeneratedAtUtc);
