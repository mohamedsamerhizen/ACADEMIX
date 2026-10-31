namespace Academix.Api.Domain.Records;

public sealed class StudentProgramMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid AdmissionTermId { get; set; }
    public Guid CurrentTermId { get; set; }
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CoursePrerequisite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid RequiredCourseId { get; set; }
    public decimal MinimumPercentage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StudentTranscriptEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SectionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid TermId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseTitle { get; set; } = "";
    public decimal Percentage { get; set; }
    public bool Passed { get; set; }
    public string Letter { get; set; } = "";
    public long GradebookVersion { get; set; }
    public long Version { get; set; } = 1;
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StudentTermStanding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid TermId { get; set; }
    public int CompletedCourses { get; set; }
    public int PassedCourses { get; set; }
    public decimal? AveragePercentage { get; set; }
    public string Status { get; set; } = "InsufficientData";
    public long Version { get; set; } = 1;
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StudentLifecycleEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
