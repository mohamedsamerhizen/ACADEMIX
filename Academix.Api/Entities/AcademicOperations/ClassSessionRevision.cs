namespace Academix.Api.Domain.AcademicOperations;

/// <summary>Append-only audit evidence for an explicit change of a class session appointment.</summary>
public sealed class ClassSessionRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
