namespace Academix.Api.DTOs.Registrar;

public sealed record ImportFieldMap(string ExternalId, string Email, string ProgramId, string AdmissionTermId, string PlanId, string? FullName = null, string? PublishedSectionId = null, string? InviteIfMissing = null);
public sealed record ImportUpload(Guid ClientRequestId, string SourceSystem, ImportFieldMap Fields, IReadOnlyList<Dictionary<string, string?>> Rows);
public sealed record ImportStudentRecord(string ExternalId, string Email, Guid ProgramId, Guid AdmissionTermId, Guid PlanId, string? FullName = null, Guid? PublishedSectionId = null, bool InviteIfMissing = false);
public sealed record ImportJobView(Guid Id, string SourceSystem, string Digest, string Status, Guid CreatedBy, Guid? ApprovedBy,
    int TotalRows, int AppliedRows, int FailedRows);
public sealed record ImportRowResult(int Number, string Status, Guid? StudentId, string? Issue);
public sealed record ImportReport(ImportJobView Job, IReadOnlyList<ImportRowResult> Rows);

public sealed record ImportProposal(int Number, Guid? StudentId, Guid ProgramId, Guid PlanId, string? FullName, Guid? PublishedSectionId, string Status, string? Issue, bool InviteIfMissing = false);
