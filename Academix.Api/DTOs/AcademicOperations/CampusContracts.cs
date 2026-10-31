namespace Academix.Api.DTOs.AcademicOperations;

public sealed record BuildingInput(string Code, string Name, bool IsActive = true, long? ExpectedVersion = null);
public sealed record BuildingView(Guid Id, Guid InstitutionId, string Code, string Name, bool IsActive, long Version);
public sealed record RoomInput(Guid BuildingId, string Code, string Name, string Kind, int Capacity,
    bool IsActive = true, long? ExpectedVersion = null);
public sealed record RoomView(Guid Id, Guid InstitutionId, Guid BuildingId, string Code,
    string Name, string Kind, int Capacity, bool IsActive, long Version);
public sealed record ConflictView(string Kind, Guid? SectionId, Guid? RoomId, string Description);
