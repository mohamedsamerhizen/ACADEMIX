namespace Academix.Api.DTOs.Records;

public sealed record AdmitStudent(Guid StudentId, Guid AdmissionTermId);
public sealed record ProgressProgram(Guid NextTermId, long ExpectedVersion, string Reason);
public sealed record MembershipView(Guid Id, Guid StudentId, Guid ProgramId, Guid AdmissionTermId,
    Guid CurrentTermId, string Status, long Version);
public sealed record AddPrerequisiteRequest(Guid RequiredCourseId, decimal MinimumPercentage);
public sealed record PrerequisiteView(Guid RequiredCourseId, decimal MinimumPercentage);
public sealed record PrerequisiteCheck(Guid RequiredCourseId, decimal MinimumPercentage, decimal? BestPublishedPercentage, bool Satisfied);
public sealed record EnrollmentEligibility(Guid SectionId, Guid StudentId, bool Eligible,
    IReadOnlyList<PrerequisiteCheck> Requirements, IReadOnlyList<string>? RejectionReasons = null, Guid? PolicyId = null);
public sealed record RegistrationChange(string Action, string Reason);
public sealed record RegistrationView(Guid SectionId, Guid StudentId, string Status);
public sealed record TranscriptLine(Guid SectionId, Guid CourseId, Guid TermId, string CourseCode,
    string CourseTitle, decimal Percentage, string Letter, bool Passed, long Version);
public sealed record TranscriptView(Guid StudentId, IReadOnlyList<TranscriptLine> Entries, decimal? OverallAverage,
    int CompletedCourses, int PassedCourses);
public sealed record TranscriptSyncResult(Guid SectionId, int Inserted, int Updated, int Unchanged);
public sealed record StandingView(Guid TermId, Guid StudentId, string Status, int CompletedCourses,
    int PassedCourses, decimal? AveragePercentage, long Version);
public sealed record LifecycleEventView(Guid Id, Guid StudentId, Guid ActorId, string Kind,
    string Detail, DateTime AtUtc);
