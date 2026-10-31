namespace Academix.Api.Domain.Examinations;

// One grading record per submitted attempt; distinct from the student answer stream.
public sealed class ExamGradeReport
{
    public long ScoreVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public Guid ExamId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid SectionId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Published
    public decimal EarnedPoints { get; set; }
    public decimal MaximumPoints { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
}

public sealed class ExamGradeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid PublishedQuestionId { get; set; }
    public decimal MaximumPoints { get; set; }
    public decimal? EarnedPoints { get; set; } // null only if pending manual grading
    public string State { get; set; } = "PendingManual"; // PendingManual | Auto | Manual
    public string Feedback { get; set; } = "";
    public Guid? GradedBy { get; set; }
    public DateTime? GradedAtUtc { get; set; }
}

// Append-only evidence of grading changes and publication, never returned to students.
public sealed class ExamGradeAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public Guid? PublishedQuestionId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public decimal? PreviousPoints { get; set; }
    public decimal? NewPoints { get; set; }
    public string Reason { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ExamGradeAppeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public Guid StudentId { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "Open"; // Open | Accepted | Rejected
    public string Resolution { get; set; } = "";
    public Guid? ResolvedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}
