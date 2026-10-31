using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class RuntimeFinanceTriggerCorrection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION p21_validate_journal() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE journal_id uuid; j finance_journals%ROWTYPE; debit_bucket text; credit_bucket text; n integer; total numeric; positive_count integer; negative_count integer;
            BEGIN
             IF TG_TABLE_NAME='finance_journals' THEN journal_id:=NEW."Id"; ELSE journal_id:=NEW."JournalId"; END IF;
             SELECT * INTO j FROM finance_journals WHERE "Id"=journal_id;
             IF NOT FOUND THEN RAISE EXCEPTION 'Journal header required' USING ERRCODE='23514'; END IF;
             SELECT count(*),COALESCE(sum("SignedMinor"),0) INTO n,total FROM finance_journal_lines WHERE "JournalId"=j."Id";
             IF n<>2 OR total<>0 THEN RAISE EXCEPTION 'An incomplete or unbalanced journal cannot commit' USING ERRCODE='23514'; END IF;
             debit_bucket:=CASE j."Kind" WHEN 'InvoicePosted' THEN 'Receivable' WHEN 'DebitAdjustment' THEN 'Receivable' WHEN 'CreditNote' THEN 'Revenue'
              WHEN 'PaymentPosted' THEN 'Funds' WHEN 'PaymentReversed' THEN 'StudentCredit' WHEN 'Allocation' THEN 'StudentCredit' WHEN 'AllocationReversal' THEN 'Receivable'
              WHEN 'ScholarshipGranted' THEN 'Aid' WHEN 'ScholarshipReclaimed' THEN 'Receivable' WHEN 'RefundExecuted' THEN 'StudentCredit' WHEN 'RefundReversed' THEN 'Funds' END;
             credit_bucket:=CASE j."Kind" WHEN 'InvoicePosted' THEN 'Revenue' WHEN 'DebitAdjustment' THEN 'Revenue' WHEN 'CreditNote' THEN 'Receivable'
              WHEN 'PaymentPosted' THEN 'StudentCredit' WHEN 'PaymentReversed' THEN 'Funds' WHEN 'Allocation' THEN 'Receivable' WHEN 'AllocationReversal' THEN 'StudentCredit'
              WHEN 'ScholarshipGranted' THEN 'Receivable' WHEN 'ScholarshipReclaimed' THEN 'Aid' WHEN 'RefundExecuted' THEN 'Funds' WHEN 'RefundReversed' THEN 'StudentCredit' END;
             SELECT count(*) FILTER(WHERE "Bucket"=debit_bucket AND "SignedMinor"=j."AmountMinor"),count(*) FILTER(WHERE "Bucket"=credit_bucket AND "SignedMinor"=-j."AmountMinor")
             INTO positive_count,negative_count FROM finance_journal_lines WHERE "JournalId"=j."Id";
             IF debit_bucket IS NULL OR positive_count<>1 OR negative_count<>1 THEN RAISE EXCEPTION 'Journal accounts/amounts do not match its financial operation' USING ERRCODE='23514'; END IF;
             IF EXISTS(SELECT 1 FROM finance_accounts a WHERE a."Id"=j."AccountId" AND a."StudentId"=j."ActorId") THEN RAISE EXCEPTION 'Students cannot post to their own account' USING ERRCODE='23514'; END IF;
             IF j."Kind"='InvoicePosted' AND NOT EXISTS(SELECT 1 FROM finance_invoices i WHERE i."Id"=j."SourceId" AND i."Id"=j."InvoiceId" AND i."AccountId"=j."AccountId" AND i."TotalMinor"=j."AmountMinor" AND i."Status" IN ('Posted','Reversed'))
             THEN RAISE EXCEPTION 'Invoice posting source mismatch' USING ERRCODE='23514'; END IF;
             IF j."Kind" IN ('PaymentPosted','PaymentReversed') AND NOT EXISTS(SELECT 1 FROM finance_payments p JOIN finance_receipts r ON r."PaymentId"=p."Id" WHERE p."Id"=j."SourceId" AND p."Id"=j."PaymentId" AND p."AccountId"=j."AccountId" AND p."AmountMinor"=j."AmountMinor" AND p."Status" IN ('Posted','Reversed'))
             THEN RAISE EXCEPTION 'Posted payment and receipt source required' USING ERRCODE='23514'; END IF;
             IF j."Kind" IN ('Allocation','AllocationReversal') AND NOT EXISTS(SELECT 1 FROM finance_allocations a WHERE a."Id"=j."SourceId" AND a."JournalId"=j."Id" AND a."InvoiceId"=j."InvoiceId" AND a."PaymentId"=j."PaymentId" AND a."AccountId"=j."AccountId" AND a."SignedMinor"=CASE WHEN j."Kind"='Allocation' THEN j."AmountMinor" ELSE -j."AmountMinor" END)
             THEN RAISE EXCEPTION 'Allocation journal source mismatch' USING ERRCODE='23514'; END IF;
             IF j."Kind" IN ('DebitAdjustment','CreditNote') AND NOT EXISTS(SELECT 1 FROM finance_adjustments a WHERE a."JournalId"=j."Id" AND a."InvoiceId"=j."InvoiceId" AND a."SignedMinor"=CASE WHEN j."Kind"='DebitAdjustment' THEN j."AmountMinor" ELSE -j."AmountMinor" END)
             THEN RAISE EXCEPTION 'Adjustment journal source mismatch' USING ERRCODE='23514'; END IF;
             IF j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed') AND NOT EXISTS(SELECT 1 FROM finance_awards a JOIN finance_scholarships s ON s."Id"=a."ScholarshipId" WHERE a."Id"=j."SourceId" AND a."InvoiceId"=j."InvoiceId" AND a."Status" IN ('Approved','Revoked') AND s."Currency"=j."Currency")
             THEN RAISE EXCEPTION 'Approved scholarship source required' USING ERRCODE='23514'; END IF;
             IF j."Kind" IN ('RefundExecuted','RefundReversed') AND NOT EXISTS(SELECT 1 FROM finance_refunds r WHERE r."Id"=j."SourceId" AND r."AccountId"=j."AccountId" AND r."PaymentId"=j."PaymentId" AND r."AmountMinor"=j."AmountMinor" AND r."Status" IN ('Executed','Reversed') AND r."ExecutionReference" IS NOT NULL)
             THEN RAISE EXCEPTION 'Executed refund source required' USING ERRCODE='23514'; END IF;
             IF j."Kind" IN ('PaymentReversed','RefundReversed') AND NOT EXISTS(SELECT 1 FROM finance_journals original_journal WHERE original_journal."Id"=j."ReversesJournalId" AND original_journal."AccountId"=j."AccountId" AND original_journal."Currency"=j."Currency" AND original_journal."SourceId"=j."SourceId" AND original_journal."AmountMinor"=j."AmountMinor" AND original_journal."Kind"=CASE WHEN j."Kind"='PaymentReversed' THEN 'PaymentPosted' ELSE 'RefundExecuted' END)
             THEN RAISE EXCEPTION 'A reversal must correct its matching original journal once' USING ERRCODE='23514'; END IF;
             RETURN NEW;
            END $$;
            """);

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException("Forward-only repair preserves finance integrity checks.");
}
