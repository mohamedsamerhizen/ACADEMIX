namespace Academix.Api.Domain.Quality;

/// <summary>An institution-scoped program learning outcome. The target is for a course-grade proxy, not a direct assessment rubric.</summary>
public sealed class ProgramQualityOutcome
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal TargetPassingRate { get; set; } = 70m;
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class ProgramOutcomeCourseMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid OutcomeId { get; set; }
    public Guid CourseId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A published cycle freezes grade-derived proxy evidence as JSON, distinct from future live records.</summary>
public sealed class ProgramQualityReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid TermId { get; set; }
    public string Status { get; set; } = "Draft";
    public string SnapshotJson { get; set; } = "[]";
    public string DirectSnapshotJson { get; set; } = "[]";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class QualityImprovementAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ReviewId { get; set; }
    public Guid? OutcomeId { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Open";
    public DateOnly? DueOn { get; set; }
    public string ResolutionNote { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public Guid? CompletedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}


/// <summary>One rubric criterion mapped to one program outcome, with a proficiency threshold.
/// Direct evidence counts graded TEAM/criterion observations, not unique learners.</summary>
public sealed class QualityCriterionAlignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid OutcomeId { get; set; }
    public Guid SectionId { get; set; }
    public Guid CriterionId { get; set; }
    public decimal ProficiencyThresholdPercent { get; set; } = 70m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>An append-only event capturing every improvement action state change.</summary>
public sealed class QualityActionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ActionId { get; set; }
    public Guid ActorId { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>University-owned review evidence bundle. Sealing freezes evidence; not an accreditation certificate.</summary>
public sealed class QualityAccreditationPack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ReviewId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string ManifestJson { get; set; } = "{}";
    public string ManifestSha256 { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public Guid? SealedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SealedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

/// <summary>Verified source evidence, snapped at attachment time. Only server-derived metadata is saved.</summary>
public sealed class QualityPackItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PackId { get; set; }
    public string EvidenceKind { get; set; } = "";
    public Guid SourceId { get; set; }
    public string Label { get; set; } = "";
    public string EvidenceJson { get; set; } = "";
    public string EvidenceSha256 { get; set; } = "";
    public Guid AddedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
