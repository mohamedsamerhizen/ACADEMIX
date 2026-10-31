namespace Academix.Api.DTOs.Learning;

public sealed record UnitCreate(string Title, int Position);
public sealed record UnitPublish(bool IsPublished, long ExpectedVersion);
public sealed record LessonCreate(Guid UnitId, string Title, string Body, int Position);
public sealed record LessonPublish(bool IsPublished, long ExpectedVersion);
public sealed record AnnouncementCreate(string Title, string Body);
public sealed record AssignmentCreate(string Title, string Instructions, DateTime DueAtUtc, decimal MaxScore);
public sealed record AssignmentPublish(bool IsPublished, long ExpectedVersion);
public sealed record SubmissionDraft(string? Content, Guid? AttachmentId = null);
public sealed record GradeSubmission(decimal Score, string Feedback);
