namespace Academix.Api.Domain.AdvancedLearning;

public sealed class DiscussionTopic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsLocked { get; set; }
    public bool IsPinned { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class DiscussionPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid TopicId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
    public bool IsHidden { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class ProjectTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class ProjectTeamMember
{
    public Guid TeamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class GradingRubric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class RubricCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid RubricId { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal MaxPoints { get; set; }
}
public sealed class GroupProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid? RubricId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateTime DueAtUtc { get; set; }
    public decimal MaxScore { get; set; }
    public bool IsPublished { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class GroupProjectSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TeamId { get; set; }
    public Guid LastEditedBy { get; set; }
    public string Content { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public DateTime? SubmittedAtUtc { get; set; }
    public decimal? Score { get; set; }
    public string Feedback { get; set; } = "";
    public Guid? GradedBy { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class RubricCriterionScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid CriterionId { get; set; }
    public decimal Points { get; set; }
    public string Comment { get; set; } = "";
}
public sealed class ProjectGradeAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid ActorId { get; set; }
    public decimal? PreviousScore { get; set; }
    public decimal NewScore { get; set; }
    public string Reason { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
