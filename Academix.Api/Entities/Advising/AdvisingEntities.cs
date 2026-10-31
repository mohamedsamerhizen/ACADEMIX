namespace Academix.Api.Domain.Advising;

public sealed class AdvisingCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid OpenedBy { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Title { get; set; } = "";
    public string InternalSummary { get; set; } = "";
    public string Status { get; set; } = "Open";
    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AdvisingNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AdvisingTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Description { get; set; } = "";
    public DateTime? DueAtUtc { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | ReportedDone | Completed
    public long Version { get; set; } = 1;
    public DateTime? ReportedAtUtc { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}

public sealed class AdvisingEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
