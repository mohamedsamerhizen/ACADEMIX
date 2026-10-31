namespace Academix.Api.Domain.Advising;

public sealed class AcademicAdvisingPolicy
{
    public Guid InstitutionId { get; set; }
    public int? MaximumAdvisees { get; set; }
    public int RepeatedFailureThreshold { get; set; } = 2;
    public decimal MinimumProgressCredits { get; set; }
    public bool RequireGoodStanding { get; set; }
    public bool RequireDepartmentReview { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class AdvisorAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AdvisorId { get; set; }
    public Guid AssignedBy { get; set; }
    public string Reason { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
}
public sealed class AcademicAdvisingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AdvisorId { get; set; }
    public Guid ClientRequestId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string InternalNotes { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public DateTime? FollowUpAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class StudentStudyPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CurriculumPlanId { get; set; }
    public int CurrentRevision { get; set; }
    public int? ApprovedRevision { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class StudyPlanRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudyPlanId { get; set; }
    public Guid CurriculumPlanId { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; } = "Proposed";
    public string Rationale { get; set; } = "";
    public string? Recommendation { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
}
public sealed class StudyPlanCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid TermId { get; set; }
}
public sealed class CurriculumElectiveGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PlanId { get; set; }
    public string Name { get; set; } = "";
    public decimal MinimumCredits { get; set; }
}
public sealed class CurriculumElectiveGroupCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PlanId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CourseId { get; set; }
}
public sealed class AcademicRequirementException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid PlanId { get; set; }
    public Guid TargetCourseId { get; set; }
    public Guid? SourceCourseId { get; set; }
    public string Kind { get; set; } = "Waiver";
    public string Status { get; set; } = "Submitted";
    public string Reason { get; set; } = "";
    public Guid RequestedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? DecisionReason { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class DepartmentGraduationReviewer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid ReviewerId { get; set; }
    public bool IsActive { get; set; } = true;
}
