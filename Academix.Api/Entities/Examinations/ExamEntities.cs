namespace Academix.Api.Domain.Examinations;

// An exam is scoped to one section; the course id is persisted to prevent
// accidental selection of questions from a different course.
public sealed class ExamDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid SectionId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Draft"; // Draft | Published
    public long Version { get; set; } = 1;
    public int DurationMinutes { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public bool ShuffleQuestions { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
}

// Mutable references while the exam is in Draft state.
public sealed class ExamDraftQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid QuestionId { get; set; }
    public int RevisionNumber { get; set; }
    public int Position { get; set; }
    public decimal Points { get; set; }
}

// Immutable published snapshot, including the grading key for server-side use ONLY.
// Student-facing APIs in Phase 4 MUST NOT serialize AnswerKeyJson or Explanation.
public sealed class ExamPublishedQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid QuestionId { get; set; }
    public int RevisionNumber { get; set; }
    public int Position { get; set; }
    public decimal Points { get; set; }
    public string BodyJson { get; set; } = "";
    public string AnswerKeyJson { get; set; } = "";
}

// Draft-only pool blueprint. Selected questions are frozen into ExamPublishedQuestion on publication.
public sealed class ExamQuestionPool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public int Position { get; set; }
    public string Kind { get; set; } = "";
    public string? Difficulty { get; set; } // null = any difficulty
    public int Quantity { get; set; }
    public decimal Points { get; set; }
}
