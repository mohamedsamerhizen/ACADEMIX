namespace Academix.Api.Domain.AcademicOperations;

public sealed class CampusBuilding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class CampusRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid BuildingId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Classroom";
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}
