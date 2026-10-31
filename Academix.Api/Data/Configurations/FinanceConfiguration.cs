using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Registrar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Academix.Api.Data;

public static class FinanceConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        EntityTypeBuilder<T> Entity<T>(string table) where T:class
        {
            var e=b.Entity<T>();e.ToTable(table);e.HasKey("Id");e.HasAlternateKey("Id","InstitutionId");
            e.HasOne<Institution>().WithMany().HasForeignKey("InstitutionId").OnDelete(DeleteBehavior.Restrict);
            if(e.Metadata.FindProperty("Version") is not null)e.Property("Version").IsConcurrencyToken();
            if(e.Metadata.FindProperty("Currency") is not null)
            {e.Property("Currency").HasMaxLength(3);e.ToTable(t=>t.HasCheckConstraint("CK_"+table+"_currency","\"Currency\" IN ("+Academix.Api.Services.Finance.FinanceCurrencyCatalog.SqlValues+")"));}
            if(e.Metadata.FindProperty("Status") is not null)e.Property("Status").HasMaxLength(20);
            return e;
        }
        void User<T>(EntityTypeBuilder<T> e,string name)where T:class=>e.HasOne<ApplicationUser>().WithMany().HasForeignKey(name).OnDelete(DeleteBehavior.Restrict);
        void Account<T>(EntityTypeBuilder<T> e)where T:class=>e.HasOne<FinancialAccount>().WithMany().HasForeignKey("AccountId","InstitutionId","Currency").HasPrincipalKey(x=>new{x.Id,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        void Invoice<T>(EntityTypeBuilder<T> e)where T:class=>e.HasOne<FinanceInvoice>().WithMany().HasForeignKey("InvoiceId","InstitutionId").HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        void Journal<T>(EntityTypeBuilder<T> e)where T:class=>e.HasOne<FinanceJournal>().WithMany().HasForeignKey("JournalId","InstitutionId").HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        void Check<T>(EntityTypeBuilder<T> e,string name,string sql)where T:class=>e.ToTable(t=>t.HasCheckConstraint("CK_p21_"+name,sql));
        var authority=Entity<FinanceAuthority>("finance_authorities");User(authority,"UserId");authority.HasIndex(x=>new{x.InstitutionId,x.UserId}).IsUnique();Check(authority,"authority","\"Permissions\" BETWEEN 0 AND 255");
        var policy=Entity<FinancialPolicy>("finance_policies");policy.HasAlternateKey(x=>new{x.Id,x.InstitutionId,x.Currency});policy.HasIndex(x=>new{x.InstitutionId,x.Currency,x.Revision}).IsUnique();policy.Property(x=>x.TimeZoneId).HasMaxLength(100);
        Check(policy,"policy","\"Status\" IN ('Published','Retired') AND \"EffectiveFrom\" < \"EffectiveUntil\" AND \"Revision\" > 0 AND \"MaximumInstallments\" BETWEEN 1 AND 60 AND \"MaximumExtensionDays\" BETWEEN 1 AND 730 AND \"GraceDays\" BETWEEN 0 AND 365 AND \"OverdueThresholdMinor\" BETWEEN 0 AND 9000000000000000");
        var account=Entity<FinancialAccount>("finance_accounts");account.HasAlternateKey(x=>new{x.Id,x.InstitutionId,x.Currency});account.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.Currency}).IsUnique();User(account,"StudentId");Check(account,"account","\"Status\" IN ('Active','Frozen')");
        var schedule=Entity<FeeSchedule>("finance_fee_schedules");schedule.HasAlternateKey(x=>new{x.Id,x.InstitutionId,x.Currency});schedule.Property(x=>x.Code).HasMaxLength(60);schedule.HasIndex(x=>new{x.InstitutionId,x.Code,x.Currency,x.Revision}).IsUnique();
        schedule.HasOne<AcademicProgram>().WithMany().HasForeignKey(x=>new{x.ProgramId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        schedule.HasOne<AcademicTerm>().WithMany().HasForeignKey(x=>new{x.TermId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(schedule,"schedule","\"Status\" IN ('Draft','Published','Retired') AND \"Revision\" > 0 AND \"EffectiveFrom\" < \"EffectiveUntil\" AND ((\"TermId\" IS NOT NULL AND \"AcademicYear\" IS NULL) OR (\"TermId\" IS NULL AND \"AcademicYear\" BETWEEN 2000 AND 2200))");
        var fee=Entity<FeeScheduleItem>("finance_fee_items");fee.HasOne<FeeSchedule>().WithMany().HasForeignKey(x=>new{x.ScheduleId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        fee.HasOne<Course>().WithMany().HasForeignKey(x=>new{x.CourseId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        fee.HasIndex(x=>new{x.ScheduleId,x.Code}).IsUnique();fee.Property(x=>x.Code).HasMaxLength(60);fee.Property(x=>x.Category).HasMaxLength(20);fee.Property(x=>x.Mode).HasMaxLength(15);
        Check(fee,"fee","\"UnitMinor\" BETWEEN 1 AND 9000000000000000 AND \"Mode\" IN ('Fixed','PerCredit') AND \"Category\" IN ('Tuition','Registration','Laboratory','Service','Optional')");
        var invoice=Entity<FinanceInvoice>("finance_invoices");Account(invoice);invoice.HasAlternateKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency});invoice.Property(x=>x.Scope).HasMaxLength(20);
        invoice.HasIndex(x=>new{x.AccountId,x.TermId,x.Scope}).IsUnique();invoice.HasIndex(x=>new{x.InstitutionId,x.Currency,x.DueDate});invoice.Property(x=>x.PricingEvidenceJson).HasColumnType("jsonb");invoice.Property(x=>x.OptionalCodesJson).HasColumnType("jsonb");
        invoice.HasOne<FeeSchedule>().WithMany().HasForeignKey(x=>new{x.ScheduleId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        invoice.HasOne<FinancialPolicy>().WithMany().HasForeignKey(x=>new{x.PolicyId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        invoice.HasOne<AcademicProgram>().WithMany().HasForeignKey(x=>new{x.ProgramId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        invoice.HasOne<AcademicTerm>().WithMany().HasForeignKey(x=>new{x.TermId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(invoice,"invoice","\"Status\" IN ('Draft','Posted','Reversed') AND \"Scope\"='Tuition' AND \"TotalMinor\" BETWEEN 1 AND 9000000000000000 AND (\"Status\"='Draft' OR \"PostedAtUtc\" IS NOT NULL)");
        var line=Entity<FinanceInvoiceLine>("finance_invoice_lines");Invoice(line);line.HasAlternateKey(x=>new{x.Id,x.InvoiceId,x.InstitutionId});line.Property(x=>x.Quantity).HasPrecision(12,2);line.Property(x=>x.Code).HasMaxLength(60);line.Property(x=>x.Category).HasMaxLength(20);
        line.HasOne<FeeScheduleItem>().WithMany().HasForeignKey(x=>new{x.FeeItemId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(line,"invoice_line","\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"UnitMinor\" BETWEEN 1 AND 9000000000000000 AND \"Quantity\" > 0 AND \"Quantity\" <= 10000 AND \"AmountMinor\"=round(\"UnitMinor\"::numeric*\"Quantity\",0)");
        var payment=Entity<FinancePayment>("finance_payments");Account(payment);payment.HasAlternateKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency});payment.Property(x=>x.Reference).HasMaxLength(120);payment.Property(x=>x.Method).HasMaxLength(25);
        payment.HasIndex(x=>new{x.InstitutionId,x.Reference}).IsUnique().HasFilter("\"Status\" <> 'Rejected'");User(payment,"RegisteredBy");User(payment,"VerifiedBy");
        Check(payment,"payment","\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Reference\"=upper(btrim(\"Reference\")) AND \"Status\" IN ('Pending','Verified','Posted','Rejected','Reversed') AND \"Method\" IN ('Cash','BankTransfer','ManualTransfer','ExternalReference') AND (\"Status\" NOT IN ('Posted','Reversed') OR \"PostedAtUtc\" IS NOT NULL) AND (\"Status\" NOT IN ('Verified','Posted','Reversed') OR NOT \"RequiresVerification\" OR (\"VerifiedBy\" IS NOT NULL AND \"VerifiedBy\" <> \"RegisteredBy\"))");
        var journal=Entity<FinanceJournal>("finance_journals");Account(journal);journal.Property(x=>x.Kind).HasMaxLength(35);journal.Property(x=>x.PostingKey).HasMaxLength(160);journal.Property(x=>x.Reason).HasMaxLength(1000);User(journal,"ActorId");
        journal.HasIndex(x=>new{x.InstitutionId,x.PostingKey}).IsUnique();journal.HasIndex(x=>new{x.AccountId,x.PostedAtUtc});journal.HasIndex(x=>x.ReversesJournalId).IsUnique();
        journal.HasIndex(x=>new{x.Kind,x.SourceId}).IsUnique().HasFilter("\"Kind\" IN ('InvoicePosted','PaymentPosted','PaymentReversed','RefundExecuted','RefundReversed')");
        journal.HasIndex(x=>x.SourceId);
        journal.HasOne<FinanceJournal>().WithMany().HasForeignKey(x=>new{x.ReversesJournalId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        journal.HasOne<FinanceInvoice>().WithMany().HasForeignKey(x=>new{x.InvoiceId,x.AccountId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        journal.HasOne<FinancePayment>().WithMany().HasForeignKey(x=>new{x.PaymentId,x.AccountId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        Check(journal,"journal_amount","\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Kind\" IN ('InvoicePosted','DebitAdjustment','CreditNote','PaymentPosted','PaymentReversed','Allocation','AllocationReversal','ScholarshipGranted','ScholarshipReclaimed','RefundExecuted','RefundReversed')");
        var leg=Entity<FinanceJournalLine>("finance_journal_lines");Journal(leg);leg.Property(x=>x.Bucket).HasMaxLength(20);leg.HasIndex(x=>new{x.JournalId,x.Bucket}).IsUnique();
        Check(leg,"leg","\"SignedMinor\" <> 0 AND \"SignedMinor\" BETWEEN -9000000000000000 AND 9000000000000000 AND \"Bucket\" IN ('Receivable','StudentCredit','Funds','Revenue','Aid')");
        var adjustment=Entity<FinanceAdjustment>("finance_adjustments");Invoice(adjustment);Journal(adjustment);adjustment.HasIndex(x=>x.JournalId).IsUnique();
        adjustment.HasOne<FinanceInvoiceLine>().WithMany().HasForeignKey(x=>new{x.InvoiceLineId,x.InvoiceId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InvoiceId,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(adjustment,"adjustment","\"SignedMinor\" <> 0 AND \"SignedMinor\" BETWEEN -9000000000000000 AND 9000000000000000");
        var allocation=Entity<FinanceAllocation>("finance_allocations");Account(allocation);Journal(allocation);allocation.HasIndex(x=>x.JournalId).IsUnique();
        allocation.HasOne<FinanceInvoice>().WithMany().HasForeignKey(x=>new{x.InvoiceId,x.AccountId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<FinancePayment>().WithMany().HasForeignKey(x=>new{x.PaymentId,x.AccountId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<FinanceAllocation>().WithMany().HasForeignKey(x=>new{x.OriginalAllocationId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(allocation,"allocation","(\"SignedMinor\" > 0 AND \"OriginalAllocationId\" IS NULL) OR (\"SignedMinor\" < 0 AND \"OriginalAllocationId\" IS NOT NULL)");
        var receipt=Entity<FinanceReceipt>("finance_receipts");receipt.Property(x=>x.Number).HasMaxLength(80);receipt.Property(x=>x.Proof).HasMaxLength(64);receipt.Property(x=>x.SnapshotJson).HasColumnType("jsonb");receipt.HasIndex(x=>x.Number).IsUnique();receipt.HasIndex(x=>x.PaymentId).IsUnique();
        receipt.HasOne<FinancePayment>().WithMany().HasForeignKey(x=>new{x.PaymentId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        receipt.HasOne<DocumentVerificationKey>().WithMany().HasForeignKey(x=>new{x.VerificationKeyId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var refund=Entity<FinanceRefund>("finance_refunds");Account(refund);refund.Property(x=>x.Reason).HasMaxLength(1000);refund.Property(x=>x.ExecutionReference).HasMaxLength(120);refund.HasIndex(x=>new{x.InstitutionId,x.ExecutionReference}).IsUnique().HasFilter("\"ExecutionReference\" IS NOT NULL");User(refund,"RequestedBy");User(refund,"ReviewedBy");
        refund.HasOne<FinancePayment>().WithMany().HasForeignKey(x=>new{x.PaymentId,x.AccountId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.AccountId,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        refund.HasOne<FinancialPolicy>().WithMany().HasForeignKey(x=>new{x.PolicyId,x.InstitutionId,x.Currency}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId,x.Currency}).OnDelete(DeleteBehavior.Restrict);
        Check(refund,"refund","\"AmountMinor\" BETWEEN 1 AND 9000000000000000 AND \"Status\" IN ('Requested','Approved','Rejected','Executed','Reversed') AND (\"Status\" NOT IN ('Approved','Executed','Reversed') OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedBy\" <> \"RequestedBy\")) AND (\"Status\" NOT IN ('Executed','Reversed') OR \"ExecutionReference\" IS NOT NULL)");
        var plan=Entity<FinancePaymentPlan>("finance_payment_plans");Invoice(plan);plan.HasIndex(x=>new{x.InvoiceId,x.Revision}).IsUnique();plan.HasIndex(x=>x.InvoiceId,"IX_finance_plan_approved").IsUnique().HasFilter("\"Status\"='Approved'");plan.HasIndex(x=>x.InvoiceId,"IX_finance_plan_proposed").IsUnique().HasFilter("\"Status\"='Proposed'");plan.Property(x=>x.Reason).HasMaxLength(1000);User(plan,"RequestedBy");User(plan,"ReviewedBy");
        Check(plan,"plan","\"Status\" IN ('Proposed','Approved','Rejected','Superseded') AND \"Revision\" > 0 AND \"BaseDebtMinor\" BETWEEN 1 AND 9000000000000000 AND (\"Status\" NOT IN ('Approved','Superseded') OR \"ApprovedAtUtc\" IS NOT NULL) AND (\"Status\" <> 'Superseded' OR \"SupersededAtUtc\" IS NOT NULL)");
        var installment=Entity<FinanceInstallment>("finance_installments");installment.HasIndex(x=>new{x.PlanId,x.Position}).IsUnique();installment.HasOne<FinancePaymentPlan>().WithMany().HasForeignKey(x=>new{x.PlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);Check(installment,"installment","\"Position\" > 0 AND \"AmountMinor\" BETWEEN 1 AND 9000000000000000");
        var scholarship=Entity<ScholarshipProgram>("finance_scholarships");scholarship.Property(x=>x.Code).HasMaxLength(60);scholarship.Property(x=>x.Category).HasMaxLength(20);scholarship.Property(x=>x.Mode).HasMaxLength(15);scholarship.Property(x=>x.MinimumGpa).HasPrecision(4,2);scholarship.HasIndex(x=>new{x.InstitutionId,x.Code,x.Currency,x.Revision}).IsUnique();
        scholarship.HasAlternateKey(x=>new{x.Id,x.InstitutionId,x.Code});
        scholarship.HasOne<AcademicProgram>().WithMany().HasForeignKey(x=>new{x.ProgramId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);scholarship.HasOne<AcademicTerm>().WithMany().HasForeignKey(x=>new{x.TermId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(scholarship,"scholarship","\"Revision\" > 0 AND \"EffectiveFrom\" < \"EffectiveUntil\" AND \"Mode\" IN ('Percent','Fixed','Full') AND \"BasisPoints\" BETWEEN 0 AND 10000 AND \"FixedMinor\" BETWEEN 0 AND 9000000000000000 AND (\"Mode\" <> 'Percent' OR \"BasisPoints\" > 0) AND (\"Mode\" <> 'Fixed' OR \"FixedMinor\" > 0) AND (\"FundingLimitMinor\" IS NULL OR \"FundingLimitMinor\" BETWEEN 0 AND 9000000000000000) AND (\"MinimumGpa\" IS NULL OR \"MinimumGpa\" BETWEEN 0 AND 4)");
        var award=Entity<ScholarshipAward>("finance_awards");Invoice(award);award.HasIndex(x=>new{x.InvoiceId,x.ScholarshipId}).IsUnique().HasFilter("\"Status\" IN ('Requested','Approved')");award.HasOne<ScholarshipProgram>().WithMany().HasForeignKey(x=>new{x.ScholarshipId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);User(award,"RequestedBy");User(award,"ReviewedBy");award.Property(x=>x.Reason).HasMaxLength(1000);award.Property(x=>x.ApprovalEvidenceJson).HasColumnType("jsonb");
        award.Property(x=>x.ProgramCode).HasMaxLength(60);award.HasIndex(x=>new{x.InvoiceId,x.ProgramCode}).IsUnique().HasFilter("\"Status\" IN ('Requested','Approved')");
        award.HasOne<ScholarshipProgram>().WithMany().HasForeignKey(x=>new{x.ScholarshipId,x.InstitutionId,x.ProgramCode}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId,x.Code}).OnDelete(DeleteBehavior.Restrict);
        Check(award,"award","\"Status\" IN ('Requested','Approved','Rejected','Revoked') AND (\"Status\" NOT IN ('Approved','Revoked') OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedBy\" <> \"RequestedBy\" AND \"ApprovalEvidenceJson\" IS NOT NULL))");
        var hold=Entity<FinanceHold>("finance_holds");Invoice(hold);hold.HasIndex(x=>new{x.InvoiceId,x.Scope}).IsUnique().HasFilter("\"Status\"='Open'");hold.HasIndex(x=>new{x.AccountId,x.Status,x.EffectiveFrom});hold.Property(x=>x.Scope).HasMaxLength(20);hold.Property(x=>x.Reason).HasMaxLength(1000);
        hold.HasOne<FinancialAccount>().WithMany().HasForeignKey(x=>new{x.AccountId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);hold.HasOne<FinancialPolicy>().WithMany().HasForeignKey(x=>new{x.PolicyId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);hold.HasOne<FinancePaymentPlan>().WithMany().HasForeignKey(x=>new{x.PlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        Check(hold,"hold","\"Status\" IN ('Open','Released') AND \"Scope\" IN ('Registration','Graduation','Documents') AND (\"Status\" <> 'Released' OR \"ReleasedAtUtc\" IS NOT NULL)");
        var command=Entity<FinanceCommand>("finance_commands");command.HasIndex(x=>new{x.InstitutionId,x.RequestId}).IsUnique();command.Property(x=>x.Fingerprint).HasMaxLength(64);command.Property(x=>x.ResultJson).HasColumnType("jsonb");
        var audit=Entity<FinanceAuditEvent>("finance_audit_events");audit.HasIndex(x=>new{x.InstitutionId,x.AccountId,x.AtUtc});User(audit,"ActorId");audit.Property(x=>x.Action).HasMaxLength(80);audit.Property(x=>x.Detail).HasMaxLength(4000);audit.HasOne<FinancialAccount>().WithMany().HasForeignKey(x=>new{x.AccountId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var recon=Entity<FinanceReconciliation>("finance_reconciliations");User(recon,"ActorId");recon.Property(x=>x.SourceName).HasMaxLength(160);recon.Property(x=>x.ProvidedRowsJson).HasColumnType("jsonb");recon.Property(x=>x.ResultsJson).HasColumnType("jsonb");
    }
}
