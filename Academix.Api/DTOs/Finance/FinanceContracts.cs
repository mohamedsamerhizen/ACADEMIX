using Academix.Api.Domain.Finance;
using Academix.Api.DTOs.Workspaces;
namespace Academix.Api.DTOs.Finance;

public sealed record FinanceResult(Guid Id,string Kind,string Status,Guid? AccountId=null);
public sealed record FinanceAction(Guid RequestId,long ExpectedVersion,string Action,string Reason);
public sealed record AuthorityInput(Guid RequestId,long ExpectedVersion,FinancePermission Permissions);
public sealed record PolicyInput(Guid RequestId,string Currency,string TimeZoneId,DateOnly EffectiveFrom,DateOnly EffectiveUntil,
    bool RequirePaymentVerification,bool AllowRefunds,bool BlockRefundWithDebt,int MaximumInstallments,int MaximumExtensionDays,
    bool RequirePlanApproval,int GraceDays,long OverdueThresholdMinor,bool BlockRegistration,bool BlockGraduation,bool BlockDocuments);
public sealed record FeeInput(string Code,string Category,string Mode,long UnitMinor,bool Optional,Guid? CourseId=null);
public sealed record FeeScheduleInput(Guid RequestId,Guid ProgramId,Guid? TermId,int? AcademicYear,string Currency,string Code,int Revision,
    DateOnly EffectiveFrom,DateOnly EffectiveUntil,IReadOnlyList<FeeInput> Items);
public sealed record OpenAccountInput(Guid RequestId,string Currency);
public sealed record AssessmentInput(Guid RequestId,Guid ScheduleId,Guid TermId,DateOnly DueDate,IReadOnlyList<string> OptionalCodes);
public sealed record AdjustmentInput(Guid RequestId,long ExpectedVersion,Guid InvoiceLineId,string Direction,long AmountMinor,string Currency,string Reason);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record PaymentInput(Guid RequestId,string Currency,string Method,string Reference,long AmountMinor);
public sealed record AllocationInput(Guid RequestId,Guid InvoiceId,string Currency,long AmountMinor);
public sealed record RefundInput(Guid RequestId,string Currency,long AmountMinor,string Reason);
public sealed record RefundAction(Guid RequestId,long ExpectedVersion,string Action,string Reason,string? ExecutionReference=null);
public sealed record PaymentPlanInput(Guid RequestId,IReadOnlyList<DateOnly> Dates,string Reason);
public sealed record ScholarshipInput(Guid RequestId,string Code,int Revision,string Currency,Guid? ProgramId,Guid? TermId,int Priority,
    string Category,string Mode,int BasisPoints,long FixedMinor,long? FundingLimitMinor,decimal? MinimumGpa,bool RequireGoodStanding,
    DateOnly EffectiveFrom,DateOnly EffectiveUntil);
public sealed record AwardInput(Guid RequestId,Guid ScholarshipId,string Reason);
public sealed record ProvidedBankRow(string Reference,string Currency,long AmountMinor,DateOnly ValueDate);
public sealed record BankReconciliationInput(Guid RequestId,string SourceName,IReadOnlyList<ProvidedBankRow> Rows);
public sealed record BankMatch(string Reference,string Currency,long AmountMinor,string Result,Guid? PaymentId);
public sealed record ReceiptVerificationInput(string Number,string Proof);
public sealed record ReceiptVerification(bool Valid,string Status,string Number,string Currency,long AmountMinor,string Digest,long RefundedMinor=0);
public sealed record ReceiptView(Guid PaymentId,string Number,string Currency,long AmountMinor,string Proof,string SnapshotJson,string Status);
public sealed record AccountView(Guid Id,Guid StudentId,string Currency,int MinorDigits,string Status,long Version);
public sealed record AccountBalances(long ReceivableMinor,long UnappliedCashMinor,long NetRecordedFundsMinor,long ReservedRefundMinor,long RefundableMinor);
public sealed record LedgerLeg(string Bucket,long SignedMinor);
public sealed record JournalView(Guid Id,string Kind,Guid SourceId,Guid? InvoiceId,Guid? PaymentId,long AmountMinor,string Reason,DateTime PostedAtUtc,IReadOnlyList<LedgerLeg> Lines);
public sealed record HoldView(Guid Id,Guid InvoiceId,Guid PolicyId,string Scope,string Status,DateOnly EffectiveFrom,string Reason,long PastDueMinor);
public sealed record StatementView(AccountView Account,AccountBalances Balances,PageResult<JournalView> Movements,PageResult<HoldView> Holds);
public sealed record InvoiceLineView(Guid Id,string Code,string Category,decimal Quantity,long UnitMinor,long AmountMinor);
public sealed record InvoiceView(Guid Id,Guid AccountId,string Currency,Guid ProgramId,Guid TermId,Guid ScheduleId,Guid PolicyId,string Status,long Version,
    DateOnly DueDate,long OriginalMinor,long NetChargesMinor,long AllocatedMinor,long OutstandingMinor,string PricingEvidenceJson,IReadOnlyList<InvoiceLineView> Lines);
public sealed record InstallmentView(int Position,DateOnly DueDate,long AmountMinor,long SettledMinor,long OutstandingMinor,string Status);
public sealed record PaymentPlanView(Guid Id,int Revision,string Status,long Version,long BaseDebtMinor,string Reason,IReadOnlyList<InstallmentView> Installments,long UnscheduledDebtMinor);
public sealed record PaymentView(Guid Id,Guid AccountId,string Currency,string Reference,string Method,long AmountMinor,string Status,long Version,
    long AllocatedMinor,long RefundReservedMinor,long UnappliedMinor);
public sealed record RefundView(Guid Id,Guid PaymentId,long AmountMinor,string Currency,string Status,long Version,string Reason,string? ExecutionReference);
public sealed record AwardView(Guid Id,Guid InvoiceId,Guid ScholarshipId,string Status,long Version,long AppliedMinor,string? ApprovalEvidenceJson);
public sealed record AccountReconciliation(Guid AccountId,string Currency,bool Balanced,long ReceivableDifferenceMinor,long FundsDifferenceMinor,long CreditDifferenceMinor);
public sealed record ReportGroup(string Key,decimal AmountMinor);
public sealed record CurrencyFinanceReport(string Currency,decimal AssessedMinor,decimal ScholarshipMinor,decimal CollectedMinor,decimal RefundedMinor,
    decimal ReceivablesMinor,decimal UnappliedMinor,decimal PastDueMinor,IReadOnlyList<ReportGroup> Aging,
    PageResult<ReportGroup> AllocatedByTerm,PageResult<ReportGroup> AllocatedByProgram,PageResult<ReportGroup> CollectedByMethod);
public sealed record FinanceReport(DateOnly From,DateOnly Until,DateTime AsOfUtc,IReadOnlyList<CurrencyFinanceReport> Currencies);
