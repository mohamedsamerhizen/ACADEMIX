namespace Academix.Api.Domain.Curriculum;

public sealed class CurriculumPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public int CatalogYear { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public decimal MinimumTotalCredits { get; set; }
    public decimal MinimumElectiveCredits { get; set; }
    public decimal MinimumCumulativeGpa { get; set; }
    public string Status { get; set; } = "Draft";
    public long Version { get; set; } = 1;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CurriculumCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PlanId { get; set; }
    public Guid CourseId { get; set; }
    public string RequirementKind { get; set; } = "Required"; // Required | Elective
    public decimal CreditHours { get; set; }
    public int RecommendedTerm { get; set; }
}

public sealed class CurriculumGradeBand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PlanId { get; set; }
    public decimal MinimumPercentage { get; set; }
    public decimal GradePoints { get; set; }
    public string Letter { get; set; } = "";
}

public sealed class StudentCurriculumAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid PlanId { get; set; }
    public Guid AssignedById { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}
