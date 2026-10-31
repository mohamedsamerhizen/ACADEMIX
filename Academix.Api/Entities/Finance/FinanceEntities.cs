namespace Academix.Api.Domain.Finance;

[Flags]
public enum FinancePermission { None=0, Read=1, Configure=2, Billing=4, Payments=8, Approve=16, Reverse=32, Refunds=64, Report=128 }
public sealed class FinanceAuthority
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid UserId { get; set; }
    public FinancePermission Permissions { get; set; }
    public long Version { get; set; }=1;
}
public sealed class FinancialPolicy
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public string Currency { get; set; }="";
    public int Revision { get; set; }
    public string TimeZoneId { get; set; }="UTC";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveUntil { get; set; }
    public string Status { get; set; }="Published";
    public bool RequirePaymentVerification { get; set; }=true;
    public bool AllowRefunds { get; set; }
    public bool BlockRefundWithDebt { get; set; }=true;
    public int MaximumInstallments { get; set; }=1;
    public int MaximumExtensionDays { get; set; }=365;
    public bool RequirePlanApproval { get; set; }=true;
    public int GraceDays { get; set; }
    public long OverdueThresholdMinor { get; set; }
    public bool BlockRegistration { get; set; }
    public bool BlockGraduation { get; set; }
    public bool BlockDocuments { get; set; }
    public DateTime PublishedAtUtc { get; set; }=DateTime.UtcNow;
}
public sealed class FinancialAccount
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public string Currency { get; set; }="";
    public string Status { get; set; }="Active";
    public long Version { get; set; }=1;
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
}
public sealed class FeeSchedule
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? TermId { get; set; }
    public int? AcademicYear { get; set; }
    public string Currency { get; set; }="";
    public string Code { get; set; }="";
    public int Revision { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveUntil { get; set; }
    public string Status { get; set; }="Draft";
    public long Version { get; set; }=1;
}
public sealed class FeeScheduleItem
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid? CourseId { get; set; }
    public string Code { get; set; }="";
    public string Category { get; set; }="Tuition";
    public string Mode { get; set; }="Fixed";
    public long UnitMinor { get; set; }
    public bool Optional { get; set; }
}
public sealed class FinanceInvoice
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid AccountId { get; set; }
    public string Currency { get; set; }="";
    public Guid ProgramId { get; set; }
    public Guid TermId { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid PolicyId { get; set; }
    public string Scope { get; set; }="Tuition";
    public string Status { get; set; }="Draft";
    public DateOnly DueDate { get; set; }
    public string PricingEvidenceJson { get; set; }="{}";
    public string OptionalCodesJson { get; set; }="[]";
    public long TotalMinor { get; set; }
    public long Version { get; set; }=1;
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
    public DateTime? PostedAtUtc { get; set; }
}
public sealed class FinanceInvoiceLine
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid FeeItemId { get; set; }
    public string Code { get; set; }="";
    public string Category { get; set; }="";
    public decimal Quantity { get; set; }
    public long UnitMinor { get; set; }
    public long AmountMinor { get; set; }
}
public sealed class FinanceJournal
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid AccountId { get; set; }
    public string Currency { get; set; }="";
    public string Kind { get; set; }="";
    public Guid SourceId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? ReversesJournalId { get; set; }
    public string PostingKey { get; set; }="";
    public long AmountMinor { get; set; }
    public Guid ActorId { get; set; }
    public string Reason { get; set; }="";
    public DateTime PostedAtUtc { get; set; }=DateTime.UtcNow;
}
public sealed class FinanceJournalLine
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid JournalId { get; set; }
    public string Bucket { get; set; }="";
    public long SignedMinor { get; set; }
}
public sealed class FinanceAdjustment
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid InvoiceLineId { get; set; }
    public Guid JournalId { get; set; }
    public long SignedMinor { get; set; }
}
public sealed class FinancePayment
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid AccountId { get; set; }
    public string Currency { get; set; }="";
    public string Reference { get; set; }="";
    public string Method { get; set; }="";
    public long AmountMinor { get; set; }
    public bool RequiresVerification { get; set; }
    public string Status { get; set; }="Pending";
    public Guid RegisteredBy { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
    public DateTime? PostedAtUtc { get; set; }
    public long Version { get; set; }=1;
}
public sealed class FinanceAllocation
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid AccountId { get; set; }
    public string Currency { get; set; }="";
    public Guid InvoiceId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid JournalId { get; set; }
    public Guid? OriginalAllocationId { get; set; }
    public long SignedMinor { get; set; }
}
public sealed class FinanceReceipt
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid VerificationKeyId { get; set; }
    public string Number { get; set; }="";
    public string SnapshotJson { get; set; }="{}";
    public string Proof { get; set; }="";
    public DateTime IssuedAtUtc { get; set; }=DateTime.UtcNow;
}
public sealed class FinanceRefund
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public string Currency { get; set; }="";
    public long AmountMinor { get; set; }
    public string Status { get; set; }="Requested";
    public Guid RequestedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public Guid? PolicyId { get; set; }
    public string Reason { get; set; }="";
    public string? ExecutionReference { get; set; }
    public long Version { get; set; }=1;
}
public sealed class FinancePaymentPlan
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid InvoiceId { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; }="Proposed";
    public long BaseDebtMinor { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string Reason { get; set; }="";
    public long Version { get; set; }=1;
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
}
public sealed class FinanceInstallment
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid PlanId { get; set; }
    public int Position { get; set; }
    public DateOnly DueDate { get; set; }
    public long AmountMinor { get; set; }
}
public sealed class ScholarshipProgram
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? TermId { get; set; }
    public string Currency { get; set; }="";
    public string Code { get; set; }="";
    public int Revision { get; set; }
    public int Priority { get; set; }
    public string Category { get; set; }="All";
    public string Mode { get; set; }="Percent";
    public int BasisPoints { get; set; }
    public long FixedMinor { get; set; }
    public long? FundingLimitMinor { get; set; }
    public decimal? MinimumGpa { get; set; }
    public bool RequireGoodStanding { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveUntil { get; set; }
}
public sealed class ScholarshipAward
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ScholarshipId { get; set; }
    public string ProgramCode { get; set; }="";
    public string Status { get; set; }="Requested";
    public Guid RequestedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string Reason { get; set; }="";
    public string? ApprovalEvidenceJson { get; set; }
    public long Version { get; set; }=1;
}
public sealed class FinanceHold
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PlanId { get; set; }
    public string Scope { get; set; }="";
    public string Status { get; set; }="Open";
    public string Reason { get; set; }="";
    public DateOnly EffectiveFrom { get; set; }
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
}
public sealed class FinanceCommand
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid RequestId { get; set; }
    public string Fingerprint { get; set; }="";
    public string ResultJson { get; set; }="{}";
}
public sealed class FinanceAuditEvent
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid EntityId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; }="";
    public string Detail { get; set; }="";
    public DateTime AtUtc { get; set; }=DateTime.UtcNow;
}
public sealed class FinanceReconciliation
{
    public Guid Id { get; set; }=Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ActorId { get; set; }
    public string SourceName { get; set; }="";
    public string ProvidedRowsJson { get; set; }="[]";
    public string ResultsJson { get; set; }="[]";
    public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
}
