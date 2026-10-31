namespace Academix.Api.Domain.Examinations;

// This is the catalog identity. Historical question revisions are append-only.
public sealed class BankQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid CreatedBy { get; set; }
    public int CurrentRevision { get; set; } = 1;
    public long Version { get; set; } = 1;
    public string Kind { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BankQuestionRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public int RevisionNumber { get; set; }
    public string BodyJson { get; set; } = "";
    public string AnswerKeyJson { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
