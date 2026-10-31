using Academix.Api.DTOs.Workspaces;

namespace Academix.Api.DTOs.Analytics;

public sealed record InstitutionAnalytics(Guid InstitutionId, Guid? TermId, int Sections, int ActiveEnrollments,
    int CompletedSessions, int AttendanceMarks, int PublishedGradebookResults, int PassedResults,
    decimal? PublishedGradeAverage, DateTime GeneratedAtUtc);

public sealed record SectionAnalyticsLine(Guid SectionId, Guid TermId, string SectionCode, string CourseCode,
    string CourseTitle, string Status, int Capacity, int ActiveEnrollments, int PublishedGradebookResults);

public sealed record AttendanceAnalytics(Guid? SectionId, Guid InstitutionId, Guid? TermId,
    int CompletedSessions, int RecordedMarks, int Present, int Absent, int Late, int Excused,
    decimal? PresentOrLatePercentOfRecorded);

public sealed record OutcomeBand(string Label, decimal Minimum, decimal Maximum, int Students);
public sealed record OutcomesAnalytics(Guid? SectionId, Guid InstitutionId, Guid? TermId,
    int PublishedResults, int Passed, int Failed, decimal? MeanPercentage,
    IReadOnlyList<OutcomeBand> Distribution);

public sealed record SectionAnalytics(Guid SectionId, Guid InstitutionId, Guid TermId, string SectionCode,
    string CourseCode, string CourseTitle, int ActiveEnrolled, int PublishedLessons,
    int CompletedSessions, int PublishedAssignments, int PublishedExams, int PublishedGradebookResults);

public sealed record EngagementAnalytics(Guid SectionId, int ActiveEnrolled, int PublishedLessons,
    int CompletedLessonRecords, decimal? LessonCompletionPercent,
    int PublishedAssignments, int SubmittedOrGradedAssignments,
    int PublishedExams, int SubmittedAttempts);

public sealed record StudentAnalyticsLine(Guid StudentId, string? Email, int CompletedLessons,
    int SubmittedAssignments, int RecordedAttendance, int PresentOrLateMarks, decimal? PublishedFinalPercentage);

public sealed record PlatformAnalytics(int Institutions, int ActiveInstitutions, int Users,
    int Sections, int ActiveEnrollments, int PublishedGradebookResults, DateTime GeneratedAtUtc);

public sealed record PlatformInstitutionAnalytics(Guid InstitutionId, string Code, string Name, bool IsActive,
    int ActiveUsers, int Sections, int ActiveEnrollments);

public sealed record MyAnalytics(Guid StudentId, Guid InstitutionId, int ActiveSections,
    int CompletedLessons, int SubmittedAssignments, int RecordedAttendance,
    int PresentOrLateMarks, int PublishedSectionResults, decimal? MeanPublishedPercentage);
