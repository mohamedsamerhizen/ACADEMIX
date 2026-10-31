namespace Academix.Api.Domain.Examinations;

// One row per permitted sitting. AttemptNumber is allocated while holding the exam row lock.
public sealed class ExamAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid SectionId { get; set; }
    public Guid StudentId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = "InProgress"; // InProgress | Submitted | Expired
    public DateTime StartedAtUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

// Order is persisted per attempt: refreshing the page never reshuffles the questions.
public sealed class ExamAttemptQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid PublishedQuestionId { get; set; }
    public int Position { get; set; }
}

// Student input only. Never persist an answer key on an attempt/answer record.
public sealed class ExamAttemptAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid PublishedQuestionId { get; set; }
    public string AnswerJson { get; set; } = "";
    public long ClientSequence { get; set; }
    public DateTime SavedAtUtc { get; set; }
}
