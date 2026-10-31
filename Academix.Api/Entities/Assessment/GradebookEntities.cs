namespace Academix.Api.Domain.Assessment;

public sealed class SectionGradePolicy
{
    public Guid SectionId { get; set; }
    public Guid InstitutionId { get; set; }
    public decimal PassingPercentage { get; set; } = 50m;
    public long Version { get; set; } = 1;
    public DateTime? PublishedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; }
}
public sealed class GradeCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Guid InstitutionId { get; set; }
    public string Name { get; set; } = "";
    public decimal WeightPercentage { get; set; }
}
public sealed class GradeComponent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string SourceKind { get; set; } = "Manual"; // Manual | Assignment | Exam | GroupProject
    public Guid? SourceId { get; set; }
    public decimal WeightPercentage { get; set; }
    public decimal MaximumPoints { get; set; }
}
public sealed class GradeManualEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ComponentId { get; set; }
    public Guid StudentId { get; set; }
    public decimal Points { get; set; }
    public long Version { get; set; } = 1;
    public Guid GradedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class GradebookResult
{
    public string SourceEvidenceJson { get; set; } = "[]";
    public bool RequiresReconciliation { get; set; }
    public bool IsManualOverride { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public decimal Percentage { get; set; }
    public bool Passed { get; set; }
    public string Letter { get; set; } = "";
    public long Version { get; set; } = 1;
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class GradebookAudit
{
    public string EvidenceJson { get; set; } = "{}";
    public Guid? CommandId { get; set; }
    public string? CommandFingerprint { get; set; }
    public string? ResultJson { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public decimal? PreviousPercentage { get; set; }
    public decimal NewPercentage { get; set; }
    public string Reason { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
