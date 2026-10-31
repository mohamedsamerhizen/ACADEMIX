namespace Academix.Api.Domain.AcademicOperations;

/// <summary>Append-only record of a committed timetable mutation.</summary>
public sealed class TimetableSlotRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid SlotId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = ""; // Created|Updated|Deactivated
    public string Reason { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string AfterJson { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
