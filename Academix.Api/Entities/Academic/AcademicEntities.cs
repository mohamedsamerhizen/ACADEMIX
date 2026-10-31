namespace Academix.Api.Domain.Academic;

public static class AcademicIds
{
    public static readonly Guid InitialInstitution = Guid.Parse("d63857d2-dbb5-4659-a879-a8a8d0f53101");
}

public sealed class Institution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class Faculty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid? FacultyId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class AcademicProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid DepartmentId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class AcademicTerm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Status { get; set; } = "Planned";
    public long Version { get; set; } = 1;
}

public sealed class ProgramCourse
{
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid CourseId { get; set; }
}

public sealed class CourseSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid TermId { get; set; }
    public string Code { get; set; } = "";
    public int Capacity { get; set; }
    public string Status { get; set; } = "Draft";
    public long Version { get; set; } = 1;
}

public sealed class TeachingAssignment
{
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = ""; // Instructor | TeachingAssistant
}

public sealed class StudentEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
}
