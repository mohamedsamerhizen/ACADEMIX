namespace Academix.Api.DTOs.Curriculum;

public sealed record CreateCurriculumPlan(int CatalogYear, int Revision, string Name,
    decimal MinimumTotalCredits, decimal MinimumElectiveCredits, decimal MinimumCumulativeGpa);
public sealed record CurriculumCourseInput(Guid CourseId, string RequirementKind, decimal CreditHours, int RecommendedTerm);
public sealed record CurriculumBandInput(decimal MinimumPercentage, decimal GradePoints, string Letter);
public sealed record ReplaceCurriculumCourses(long ExpectedVersion, IReadOnlyList<CurriculumCourseInput> Courses);
public sealed record ReplaceCurriculumScale(long ExpectedVersion, IReadOnlyList<CurriculumBandInput> Bands);
public sealed record PublishCurriculumPlan(long ExpectedVersion);
public sealed record AssignStudentCurriculum(Guid PlanId);
public sealed record CurriculumCourseView(Guid CourseId, string CourseCode, string CourseTitle, string RequirementKind,
    decimal CreditHours, int RecommendedTerm);
public sealed record CurriculumGradeBandView(decimal MinimumPercentage, decimal GradePoints, string Letter);
public sealed record CurriculumPlanView(Guid Id, Guid ProgramId, int CatalogYear, int Revision, string Name,
    string Status, long Version, decimal MinimumTotalCredits, decimal MinimumElectiveCredits,
    decimal MinimumCumulativeGpa, DateTime? PublishedAtUtc,
    IReadOnlyList<CurriculumCourseView> Courses, IReadOnlyList<CurriculumGradeBandView> GradeScale);
public sealed record StudentCurriculumView(Guid StudentId, Guid PlanId, Guid ProgramId, int CatalogYear,
    string PlanName, DateTime AssignedAtUtc);
public sealed record CurriculumAuditCourse(Guid CourseId, string CourseCode, string RequirementKind,
    decimal CreditHours, bool Completed, decimal? BestPercentage, decimal? GradePoints, Guid? TermId);
public sealed record StudentCurriculumAudit(Guid StudentId, Guid PlanId, bool GraduationReady,
    decimal EarnedRequiredCredits, decimal EarnedElectiveCredits, decimal EarnedTotalCredits,
    decimal MinimumTotalCredits, decimal MinimumElectiveCredits, decimal? CumulativeGpa,
    decimal MinimumCumulativeGpa, int MissingRequiredCourses,
    IReadOnlyList<CurriculumAuditCourse> Courses, IReadOnlyList<string> Blockers);
public sealed record GpaCourseLine(Guid CourseId, Guid TermId, string CourseCode,
    decimal CreditHours, decimal Percentage, bool Passed, string Letter, decimal GradePoints);
public sealed record StudentGpaReport(Guid StudentId, decimal? CumulativeGpa,
    decimal AttemptedCredits, decimal EarnedCredits, IReadOnlyList<GpaTermView> Terms);
public sealed record GpaTermView(Guid TermId, decimal? Gpa, decimal AttemptedCredits,
    IReadOnlyList<GpaCourseLine> Courses);
