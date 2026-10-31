using Academix.Api.DTOs.Curriculum;
namespace Academix.Api.DTOs.Advising;

public sealed record AdvisingPolicyInput(long ExpectedVersion,int? MaximumAdvisees,int RepeatedFailureThreshold,
    decimal MinimumProgressCredits,bool RequireGoodStanding,bool RequireDepartmentReview);
public sealed record AdvisingPolicyView(long Version,int? MaximumAdvisees,int RepeatedFailureThreshold,
    decimal MinimumProgressCredits,bool RequireGoodStanding,bool RequireDepartmentReview);
public sealed record AssignAdvisor(Guid AdvisorId,string Reason);
public sealed record AdvisorAssignmentView(Guid Id,Guid StudentId,Guid AdvisorId,string Reason,DateTime AssignedAtUtc,DateTime? EndedAtUtc);
public sealed record ScheduleAdvisingSession(Guid ClientRequestId,DateTime ScheduledAtUtc);
public sealed record CompleteAdvisingSession(long ExpectedVersion,string Status,string InternalNotes,string Recommendation,DateTime? FollowUpAtUtc);
public sealed record StudentSessionView(Guid Id,Guid AdvisorId,DateTime ScheduledAtUtc,string Status,string Recommendation,
    DateTime? FollowUpAtUtc,DateTime? AcknowledgedAtUtc,long Version);
public sealed record StaffSessionView(StudentSessionView Session,Guid StudentId,string InternalNotes);
public sealed record PlannedCourseInput(Guid CourseId,Guid TermId);
public sealed record ReviseStudyPlan(long ExpectedVersion,string Rationale,IReadOnlyList<PlannedCourseInput> Courses);
public sealed record ReviewStudyPlan(long ExpectedVersion,bool Approve,string Recommendation);
public sealed record StudyPlanRevisionView(Guid Id,Guid CurriculumPlanId,int Revision,string Status,string Rationale,string? Recommendation,
    Guid CreatedBy,Guid? ReviewedBy,DateTime CreatedAtUtc,DateTime? AcknowledgedAtUtc,IReadOnlyList<PlannedCourseInput> Courses);
public sealed record StudyPlanView(Guid Id,Guid StudentId,Guid CurriculumPlanId,long Version,int CurrentRevision,
    int? ApprovedRevision,IReadOnlyList<StudyPlanRevisionView> Revisions);
public sealed record ElectiveGroupInput(string Name,decimal MinimumCredits,IReadOnlyList<Guid> CourseIds);
public sealed record ReplaceElectiveGroups(long ExpectedVersion,IReadOnlyList<ElectiveGroupInput> Groups);
public sealed record RequirementExceptionInput(Guid PlanId,Guid TargetCourseId,Guid? SourceCourseId,string Kind,string Reason);
public sealed record ReviewRequirementException(long ExpectedVersion,bool Approve,string Reason);
public sealed record RequirementExceptionView(Guid Id,Guid StudentId,Guid PlanId,Guid TargetCourseId,Guid? SourceCourseId,
    string Kind,string Status,long Version,string Reason,string? DecisionReason);
public sealed record AuditRequirement(string Code,string Status,decimal Required,decimal Completed,decimal Pending,string Explanation);
public sealed record CourseProgress(Guid CourseId,string Code,string Kind,decimal Credits,string Status,string Evidence,
    int FailedAttempts,IReadOnlyList<Guid> MissingPrerequisites);
public sealed record ProgressIndicator(string Code,Guid? CourseId,string Explanation);
public sealed record DegreeAuditView(StudentCurriculumAudit Audit,int CatalogYear,int CurriculumRevision,
    decimal InProgressCredits,decimal CompletionPercentage,string Eligibility,IReadOnlyList<AuditRequirement> Requirements,
    IReadOnlyList<CourseProgress> Courses,IReadOnlyList<ProgressIndicator> Indicators,DateTime EvaluatedAtUtc,
    long AdvisingPolicyVersion=0,Guid? AcademicPolicyId=null);
