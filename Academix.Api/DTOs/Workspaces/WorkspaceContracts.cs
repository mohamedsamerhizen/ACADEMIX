namespace Academix.Api.DTOs.Workspaces;

// Explicit public contracts. No persistence entities or answer keys are serialized.
public sealed record PageResult<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);
public sealed record StudentOverview(Guid StudentId, string Email, Guid InstitutionId,
    int ActiveSections, int PublishedLessons, int CompletedLessons,
    int OpenAssignments, int SubmittedAssignments, int PublishedExamResults);
public sealed record StudentCourse(Guid SectionId, Guid CourseId, string CourseCode, string CourseTitle,
    string SectionCode, Guid TermId, string TermName, string SectionStatus, DateTime EnrolledAtUtc);
public sealed record StudentAssignment(Guid Id, string Title, DateTime DueAtUtc, decimal MaxScore,
    string? SubmissionStatus, decimal? Score);
public sealed record StudentExamResult(Guid ExamId, string ExamTitle, Guid AttemptId,
    decimal EarnedPoints, decimal MaximumPoints, DateTime? PublishedAtUtc);
public sealed record StudentCourseProgress(Guid SectionId, int PublishedLessons, int CompletedLessons,
    IReadOnlyList<StudentAssignment> Assignments, IReadOnlyList<StudentExamResult> PublishedResults);
public sealed record StudentCalendarItem(string Kind, Guid Id, Guid SectionId, string Title, DateTime AtUtc);

public sealed record TeachingSection(Guid SectionId, string SectionCode, Guid CourseId, string CourseCode,
    string CourseTitle, string AssignmentKind, string Status, int Capacity, int ActiveStudents);
public sealed record SectionStudent(Guid StudentId, string? Email, DateTime EnrolledAtUtc,
    int CompletedLessons, int SubmittedAssignments, int PublishedExamResults);
public sealed record SectionPerformance(Guid SectionId, int EnrolledStudents,
    int PublishedLessons, int LessonCompletions, int PublishedAssignments,
    int SubmittedAssignments, int PublishedExams, int SubmittedExamAttempts,
    int PublishedExamReports, decimal? AveragePublishedExamPercentage);

public sealed record InstitutionOverview(Guid InstitutionId, string Name, bool IsActive,
    int Faculties, int Departments, int Programs, int Courses, int Sections,
    int ActiveStudents, int ActiveTeachingStaff, int ActiveEnrollments, int PublishedExams);
public sealed record DirectoryUser(Guid UserId, string? Email, bool IsActive, DateTime CreatedAtUtc,
    IReadOnlyList<string> Roles);
public sealed record InstitutionCoursePerformance(Guid CourseId, string CourseCode, string CourseTitle,
    int Sections, int ActiveEnrollments, int SubmittedExamAttempts, int PublishedExamReports);

public sealed record InstitutionDirectoryItem(Guid Id, string Code, string Name, bool IsActive, long Version);
public sealed record InstitutionCreateRequest(string Code, string Name);
public sealed record PlatformOverview(int Institutions, int ActiveInstitutions,
    int RegisteredUsers, int Courses, int Sections, int ActiveEnrollments, int PublishedExams);
