namespace Academix.Api.DTOs.AdvancedLearning;

public sealed record DiscussionTopicCreate(string Title, string Body);
public sealed record DiscussionReplyCreate(string Body, Guid? ParentId);
public sealed record DiscussionStateUpdate(bool IsLocked, bool IsPinned, long ExpectedVersion);
public sealed record DiscussionTopicView(Guid Id, Guid SectionId, Guid CreatedBy, string Title, string Body,
    bool IsLocked, bool IsPinned, long Version, DateTime CreatedAtUtc);
public sealed record DiscussionPostView(Guid Id, Guid TopicId, Guid? ParentId, Guid AuthorId, string Body,
    bool IsHidden, DateTime CreatedAtUtc);
public sealed record DiscussionPage(DiscussionTopicView Topic, IReadOnlyList<DiscussionPostView> Posts);
public sealed record CreateTeam(string Name);
public sealed record TeamView(Guid Id, Guid SectionId, string Name, IReadOnlyList<Guid> StudentIds);
public sealed record RubricCriterionInput(int Position, string Name, string Description, decimal MaxPoints);
public sealed record CreateRubric(string Name, IReadOnlyList<RubricCriterionInput> Criteria);
public sealed record RubricCriterionView(Guid Id, int Position, string Name, string Description, decimal MaxPoints);
public sealed record RubricView(Guid Id, Guid SectionId, string Name, decimal TotalPoints, IReadOnlyList<RubricCriterionView> Criteria);
public sealed record CreateGroupProject(string Title, string Instructions, DateTime DueAtUtc, decimal MaxScore, Guid? RubricId);
public sealed record PublishGroupProject(bool IsPublished, long ExpectedVersion);
public sealed record GroupProjectView(Guid Id, Guid SectionId, string Title, string Instructions,
    DateTime DueAtUtc, decimal MaxScore, bool IsPublished, Guid? RubricId, long Version);
public sealed record UpdateGroupDraft(string Content, long? ExpectedVersion);
public sealed record GroupSubmissionView(Guid Id, Guid ProjectId, Guid TeamId, string Content, string Status,
    decimal? Score, string Feedback, long Version, DateTime? SubmittedAtUtc);
public sealed record CriterionScoreInput(Guid CriterionId, decimal Points, string Comment);
public sealed record GradeGroupSubmission(decimal? ManualScore, string Feedback, string Reason,
    long ExpectedVersion, IReadOnlyList<CriterionScoreInput>? CriterionScores);
public sealed record GradeAuditView(decimal? PreviousScore, decimal NewScore, Guid ActorId, string Reason, DateTime AtUtc);

public sealed record CriterionScoreView(Guid CriterionId, decimal Points, string Comment);
public sealed record GroupAssessmentView(GroupSubmissionView Submission, IReadOnlyList<CriterionScoreView> RubricScores);
