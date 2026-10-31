namespace Academix.Api.Domain.Registrar;

public sealed class ImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string SourceSystem { get; set; } = "";
    public string Digest { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
}
public sealed class ImportRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid JobId { get; set; }
    public int Number { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public string? Issue { get; set; }
    public Guid? StudentId { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
}
public sealed class ExternalIdentityMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string SourceSystem { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public Guid StudentId { get; set; }
    public Guid ImportRowId { get; set; }
}
public sealed class InstitutionalOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid? ImportRowId { get; set; }
    public string DeduplicationKey { get; set; } = Guid.NewGuid().ToString("N");
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string Kind { get; set; } = "ImportRowApplied";
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
