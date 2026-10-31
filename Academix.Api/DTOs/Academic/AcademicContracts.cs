namespace Academix.Api.DTOs.Academic;

public sealed record NamedCreate(Guid InstitutionId, string Code, string Name);
public sealed record NamedUpdate(string Name, bool IsActive, long ExpectedVersion);
public sealed record DepartmentCreate(Guid InstitutionId, Guid? FacultyId, string Code, string Name);
public sealed record ProgramCreate(Guid InstitutionId, Guid DepartmentId, string Code, string Name);
public sealed record TermCreate(Guid InstitutionId, string Code, string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record TermUpdate(string Name, DateOnly StartsOn, DateOnly EndsOn, string Status, long ExpectedVersion);
public sealed record CourseCreate(Guid InstitutionId, string Code, string Title);
public sealed record CourseUpdate(string Title, bool IsActive, long ExpectedVersion);
public sealed record SectionCreate(Guid InstitutionId, Guid CourseId, Guid TermId, string Code, int Capacity);
public sealed record SectionUpdate(int Capacity, string Status, long ExpectedVersion);
public sealed record TeachingCreate(Guid UserId, string Kind);
public sealed record EnrollmentCreate(Guid StudentId);
public sealed record InstitutionUpdate(string Name, bool IsActive, long ExpectedVersion);
