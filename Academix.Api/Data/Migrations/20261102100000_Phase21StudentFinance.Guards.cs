namespace Academix.Api.Data.Migrations;

public partial class Phase21StudentFinance
{
    private const string IntegritySql="""
CREATE FUNCTION p21_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Posted finance evidence/history is append-only; use correcting entries' USING ERRCODE='23514'; END $$;

CREATE FUNCTION p21_user_scope() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE field_name text; user_id uuid; scope_id uuid;
BEGIN
 FOREACH field_name IN ARRAY TG_ARGV LOOP
  user_id:=(to_jsonb(NEW)->>field_name)::uuid;
  IF user_id IS NOT NULL THEN
   SELECT "InstitutionId" INTO scope_id FROM identity_users WHERE "Id"=user_id;
   IF scope_id IS DISTINCT FROM NEW."InstitutionId" AND (field_name='StudentId' OR NOT EXISTS
     (SELECT 1 FROM "AspNetUserRoles" ur JOIN identity_roles r ON r."Id"=ur."RoleId" WHERE ur."UserId"=user_id AND r."Name"='SuperAdministrator'))
   THEN RAISE EXCEPTION 'Finance identity/institution mismatch' USING ERRCODE='23514'; END IF;
  END IF;
 END LOOP; RETURN NEW;
END $$;

CREATE FUNCTION p21_state_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE old_json jsonb; new_json jsonb; permitted text[]; old_status text; new_status text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Financial records cannot be deleted' USING ERRCODE='23514'; END IF;
 old_json:=to_jsonb(OLD);new_json:=to_jsonb(NEW);old_status:=old_json->>'Status';new_status:=new_json->>'Status';
 CASE TG_TABLE_NAME
 WHEN 'finance_accounts' THEN permitted:=ARRAY['Status','Version'];
 WHEN 'finance_authorities' THEN permitted:=ARRAY['Permissions','Version'];
 WHEN 'finance_policies' THEN
  permitted:=ARRAY['Status'];
  IF NOT(old_status='Published' AND new_status='Retired') THEN RAISE EXCEPTION 'Published policies may only retire, never change their historical rules' USING ERRCODE='23514'; END IF;
 WHEN 'finance_fee_schedules' THEN
  permitted:=ARRAY['Status','Version'];
  IF old_status<>new_status AND NOT(old_status='Draft' AND new_status='Published' OR old_status='Published' AND new_status='Retired') THEN RAISE EXCEPTION 'Invalid fee schedule transition' USING ERRCODE='23514'; END IF;
 WHEN 'finance_invoices' THEN
  IF old_status='Draft' AND new_status='Draft' THEN RETURN NEW; END IF;
  permitted:=ARRAY['Status','Version','PostedAtUtc'];
  IF old_status<>new_status AND NOT(old_status='Draft' AND new_status='Posted' OR old_status='Posted' AND new_status='Reversed' OR old_status='Reversed' AND new_status='Posted') THEN RAISE EXCEPTION 'Invalid invoice transition' USING ERRCODE='23514'; END IF;
  IF OLD."PostedAtUtc" IS NOT NULL AND NEW."PostedAtUtc" IS DISTINCT FROM OLD."PostedAtUtc" THEN RAISE EXCEPTION 'Posting time is immutable' USING ERRCODE='23514'; END IF;
 WHEN 'finance_payments' THEN
  permitted:=ARRAY['Status','Version','VerifiedBy','PostedAtUtc'];
  IF old_status<>new_status AND NOT(old_status='Pending' AND new_status IN ('Verified','Rejected','Posted') OR old_status='Verified' AND new_status IN ('Posted','Rejected') OR old_status='Posted' AND new_status='Reversed') THEN RAISE EXCEPTION 'Invalid payment transition' USING ERRCODE='23514'; END IF;
  IF old_status IN ('Posted','Reversed') THEN permitted:=ARRAY['Status','Version']; END IF;
 WHEN 'finance_refunds' THEN
  permitted:=ARRAY['Status','Version','ReviewedBy','ExecutionReference'];
  IF old_status<>new_status AND NOT(old_status='Requested' AND new_status IN ('Approved','Rejected') OR old_status='Approved' AND new_status IN ('Executed','Rejected') OR old_status='Executed' AND new_status='Reversed') THEN RAISE EXCEPTION 'Invalid refund transition' USING ERRCODE='23514'; END IF;
  IF old_status IN ('Executed','Reversed') THEN permitted:=ARRAY['Status','Version']; END IF;
 WHEN 'finance_payment_plans' THEN
  permitted:=ARRAY['Status','Version','ReviewedBy','ApprovedAtUtc','SupersededAtUtc'];
  IF old_status<>new_status AND NOT(old_status='Proposed' AND new_status IN ('Approved','Rejected') OR old_status='Approved' AND new_status='Superseded') THEN RAISE EXCEPTION 'Invalid payment plan transition' USING ERRCODE='23514'; END IF;
  IF old_status='Approved' THEN permitted:=ARRAY['Status','Version','SupersededAtUtc']; END IF;
 WHEN 'finance_awards' THEN
  permitted:=ARRAY['Status','Version','ReviewedBy','ApprovalEvidenceJson'];
  IF old_status<>new_status AND NOT(old_status='Requested' AND new_status IN ('Approved','Rejected') OR old_status='Approved' AND new_status='Revoked') THEN RAISE EXCEPTION 'Invalid award transition' USING ERRCODE='23514'; END IF;
  IF old_status IN ('Approved','Revoked') THEN permitted:=ARRAY['Status','Version']; END IF;
 WHEN 'finance_holds' THEN
  permitted:=ARRAY['Status','ReleasedAtUtc'];
  IF NOT(old_status='Open' AND new_status='Released') THEN RAISE EXCEPTION 'Holds are released, never silently rewritten' USING ERRCODE='23514'; END IF;
 ELSE RAISE EXCEPTION 'Unsupported finance state table';
 END CASE;
 IF (old_json-permitted) IS DISTINCT FROM (new_json-permitted) THEN RAISE EXCEPTION 'Financial source amounts, ownership, policy and approval evidence are immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;

CREATE FUNCTION p21_child_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE parent_id uuid; parent_status text;
BEGIN
 IF TG_TABLE_NAME='finance_fee_items' THEN
  IF TG_OP='DELETE' THEN parent_id:=OLD."ScheduleId"; ELSE parent_id:=NEW."ScheduleId"; END IF;
  SELECT "Status" INTO parent_status FROM finance_fee_schedules WHERE "Id"=parent_id;
 ELSE
  IF TG_OP='DELETE' THEN parent_id:=OLD."InvoiceId"; ELSE parent_id:=NEW."InvoiceId"; END IF;
  SELECT "Status" INTO parent_status FROM finance_invoices WHERE "Id"=parent_id;
 END IF;
 IF parent_status IS DISTINCT FROM 'Draft' THEN RAISE EXCEPTION 'Published pricing and posted invoice lines are immutable' USING ERRCODE='23514'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;

CREATE FUNCTION p21_validate_journal() RETURNS trigger LANGUAGE plpgsql AS $$
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
 IF j."Kind" IN ('PaymentReversed','RefundReversed') AND NOT EXISTS(SELECT 1 FROM finance_journals old WHERE old."Id"=j."ReversesJournalId" AND old."AccountId"=j."AccountId" AND old."Currency"=j."Currency" AND old."SourceId"=j."SourceId" AND old."AmountMinor"=j."AmountMinor" AND old."Kind"=CASE WHEN j."Kind"='PaymentReversed' THEN 'PaymentPosted' ELSE 'RefundExecuted' END)
 THEN RAISE EXCEPTION 'A reversal must correct its matching original journal once' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;

CREATE FUNCTION p21_validate_account() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE account_id uuid; ar numeric; funds numeric; credit numeric; gross numeric; aid numeric; allocated numeric; cash numeric; refunds numeric; expected_ar numeric;
BEGIN
 IF TG_TABLE_NAME='finance_journal_lines' THEN SELECT "AccountId" INTO account_id FROM finance_journals WHERE "Id"=NEW."JournalId";
 ELSIF TG_TABLE_NAME IN ('finance_invoice_lines','finance_adjustments','finance_awards','finance_payment_plans') THEN SELECT "AccountId" INTO account_id FROM finance_invoices WHERE "Id"=(to_jsonb(NEW)->>'InvoiceId')::uuid;
 ELSIF TG_TABLE_NAME='finance_installments' THEN SELECT i."AccountId" INTO account_id FROM finance_payment_plans p JOIN finance_invoices i ON i."Id"=p."InvoiceId" WHERE p."Id"=NEW."PlanId";
 ELSIF TG_TABLE_NAME='finance_receipts' THEN SELECT "AccountId" INTO account_id FROM finance_payments WHERE "Id"=NEW."PaymentId";
 ELSE account_id:=(to_jsonb(NEW)->>'AccountId')::uuid; END IF;
 IF account_id IS NULL THEN RAISE EXCEPTION 'Financial account linkage required' USING ERRCODE='23514'; END IF;
 PERFORM 1 FROM finance_accounts WHERE "Id"=account_id FOR UPDATE;
 SELECT COALESCE(sum(l."SignedMinor") FILTER(WHERE l."Bucket"='Receivable'),0),COALESCE(sum(l."SignedMinor") FILTER(WHERE l."Bucket"='Funds'),0),COALESCE(sum(l."SignedMinor") FILTER(WHERE l."Bucket"='StudentCredit'),0)
 INTO ar,funds,credit FROM finance_journal_lines l JOIN finance_journals j ON j."Id"=l."JournalId" WHERE j."AccountId"=account_id;
 IF ar<0 OR funds<0 OR credit>0 OR greatest(abs(ar),abs(funds),abs(credit))>9000000000000000 THEN RAISE EXCEPTION 'Invalid receivable, collected funds, or student cash-credit balance' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_journal_lines l JOIN finance_journals j ON j."Id"=l."JournalId" WHERE j."AccountId"=account_id GROUP BY l."Bucket" HAVING abs(sum(l."SignedMinor"))>9000000000000000)
 THEN RAISE EXCEPTION 'Financial account balance exceeds supported bounds' USING ERRCODE='23514'; END IF;
 SELECT COALESCE(sum("TotalMinor"),0) INTO gross FROM finance_invoices WHERE "AccountId"=account_id AND "Status"<>'Draft';
 SELECT gross+COALESCE(sum(a."SignedMinor"),0) INTO gross FROM finance_adjustments a JOIN finance_invoices i ON i."Id"=a."InvoiceId" WHERE i."AccountId"=account_id;
 SELECT COALESCE(sum(CASE WHEN "Kind"='ScholarshipGranted' THEN "AmountMinor" ELSE -"AmountMinor" END),0) INTO aid FROM finance_journals WHERE "AccountId"=account_id AND "Kind" IN ('ScholarshipGranted','ScholarshipReclaimed');
 SELECT COALESCE(sum("SignedMinor"),0) INTO allocated FROM finance_allocations WHERE "AccountId"=account_id;
 SELECT COALESCE(sum("AmountMinor"),0) INTO cash FROM finance_payments WHERE "AccountId"=account_id AND "Status"='Posted';
 SELECT COALESCE(sum("AmountMinor"),0) INTO refunds FROM finance_refunds WHERE "AccountId"=account_id AND "Status"='Executed';
 expected_ar:=gross-aid-allocated;
 IF ar<>expected_ar OR funds<>cash-refunds OR -credit<>cash-allocated-refunds THEN RAISE EXCEPTION 'Student account ledger does not reconcile to invoice, allocation, posted payment and refund records' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_invoices i WHERE i."AccountId"=account_id AND
     (i."TotalMinor"<>(SELECT COALESCE(sum(l."AmountMinor"),0) FROM finance_invoice_lines l WHERE l."InvoiceId"=i."Id") OR
      (i."Status"<>'Draft' AND i."TotalMinor"+COALESCE((SELECT sum(a."SignedMinor") FROM finance_adjustments a WHERE a."InvoiceId"=i."Id"),0)-
       COALESCE((SELECT sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END) FROM finance_journals j WHERE j."InvoiceId"=i."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed')),0)-
       COALESCE((SELECT sum(a."SignedMinor") FROM finance_allocations a WHERE a."InvoiceId"=i."Id"),0)<0)))
 THEN RAISE EXCEPTION 'Invoice lines or outstanding liability do not reconcile' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_invoice_lines l JOIN finance_invoices i ON i."Id"=l."InvoiceId" WHERE i."AccountId"=account_id AND l."AmountMinor"+COALESCE((SELECT sum(a."SignedMinor") FROM finance_adjustments a WHERE a."InvoiceLineId"=l."Id"),0)<0)
 THEN RAISE EXCEPTION 'Correcting credits exceed the eligible charge' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_payments p WHERE p."AccountId"=account_id AND
   COALESCE((SELECT sum(a."SignedMinor") FROM finance_allocations a WHERE a."PaymentId"=p."Id"),0)+
   COALESCE((SELECT sum(r."AmountMinor") FROM finance_refunds r WHERE r."PaymentId"=p."Id" AND r."Status" IN ('Requested','Approved','Executed')),0)>
   CASE WHEN p."Status"='Posted' THEN p."AmountMinor" ELSE 0 END)
 THEN RAISE EXCEPTION 'Cash was allocated or reserved/refunded more than once' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_allocations a LEFT JOIN finance_allocations original ON original."Id"=a."OriginalAllocationId" JOIN finance_journals j ON j."Id"=a."JournalId"
   WHERE a."AccountId"=account_id AND (a."InvoiceId" IS DISTINCT FROM j."InvoiceId" OR a."PaymentId" IS DISTINCT FROM j."PaymentId" OR a."AccountId"<>j."AccountId" OR
     a."SignedMinor"<>CASE WHEN j."Kind"='Allocation' THEN j."AmountMinor" WHEN j."Kind"='AllocationReversal' THEN -j."AmountMinor" ELSE 0 END OR
     a."SignedMinor"<0 AND (original."SignedMinor"<=0 OR original."InvoiceId"<>a."InvoiceId" OR original."PaymentId"<>a."PaymentId")))
 THEN RAISE EXCEPTION 'Allocation source/reversal lineage mismatch' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_allocations a WHERE a."AccountId"=account_id AND a."SignedMinor">0 AND a."SignedMinor"+COALESCE((SELECT sum(r."SignedMinor") FROM finance_allocations r WHERE r."OriginalAllocationId"=a."Id"),0)<0)
 THEN RAISE EXCEPTION 'Allocation reversed more than its original amount' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_payment_plans p JOIN finance_invoices i ON i."Id"=p."InvoiceId" WHERE i."AccountId"=account_id AND
   p."BaseDebtMinor"<>(SELECT COALESCE(sum(s."AmountMinor"),0) FROM finance_installments s WHERE s."PlanId"=p."Id"))
 THEN RAISE EXCEPTION 'Installment obligations do not reconcile to the frozen debt snapshot' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_awards a JOIN finance_invoices i ON i."Id"=a."InvoiceId" JOIN finance_accounts ac ON ac."Id"=i."AccountId" WHERE i."AccountId"=account_id AND a."Status" IN ('Approved','Revoked') AND a."ReviewedBy"=ac."StudentId")
 THEN RAISE EXCEPTION 'Student self-approved financial assistance' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_awards a JOIN finance_invoices i ON i."Id"=a."InvoiceId" WHERE i."AccountId"=account_id AND
    ((SELECT COALESCE(sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END),0) FROM finance_journals j WHERE j."SourceId"=a."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed'))<0 OR
     a."Status"<>'Approved' AND (SELECT COALESCE(sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END),0) FROM finance_journals j WHERE j."SourceId"=a."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed'))<>0))
 THEN RAISE EXCEPTION 'Scholarship correcting entries exceed their approved source or leave revoked assistance applied' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_scholarships s WHERE s."FundingLimitMinor" IS NOT NULL AND EXISTS(SELECT 1 FROM finance_awards a JOIN finance_invoices i ON i."Id"=a."InvoiceId" WHERE a."ScholarshipId"=s."Id" AND i."AccountId"=account_id) AND
   (SELECT COALESCE(sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END),0) FROM finance_journals j JOIN finance_awards a ON a."Id"=j."SourceId" WHERE a."ScholarshipId"=s."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed'))>s."FundingLimitMinor")
 THEN RAISE EXCEPTION 'Scholarship funding limit exceeded' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_holds h JOIN finance_invoices i ON i."Id"=h."InvoiceId" WHERE h."AccountId"=account_id AND (i."AccountId"<>h."AccountId" OR i."PolicyId"<>h."PolicyId"))
 THEN RAISE EXCEPTION 'Financial hold source mismatch' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;

DO $$ DECLARE table_name text; BEGIN
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines','finance_adjustments','finance_allocations','finance_receipts','finance_commands','finance_audit_events','finance_reconciliations','finance_installments','finance_scholarships'] LOOP
  EXECUTE format('CREATE TRIGGER p21_immutable BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION p21_immutable()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_accounts','finance_authorities','finance_policies','finance_fee_schedules','finance_invoices','finance_payments','finance_refunds','finance_payment_plans','finance_awards','finance_holds'] LOOP
  EXECUTE format('CREATE TRIGGER p21_state BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION p21_state_guard()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_fee_items','finance_invoice_lines'] LOOP
  EXECUTE format('CREATE TRIGGER p21_child BEFORE INSERT OR UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION p21_child_guard()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines'] LOOP
  EXECUTE format('CREATE CONSTRAINT TRIGGER p21_balanced AFTER INSERT ON %I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION p21_validate_journal()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines','finance_invoices','finance_invoice_lines','finance_adjustments','finance_allocations','finance_payments','finance_refunds','finance_receipts','finance_awards','finance_payment_plans','finance_installments','finance_holds'] LOOP
  EXECUTE format('CREATE CONSTRAINT TRIGGER p21_reconciled AFTER INSERT OR UPDATE ON %I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION p21_validate_account()',table_name);
 END LOOP;
END $$;
CREATE TRIGGER p21_account_scope BEFORE INSERT OR UPDATE ON finance_accounts FOR EACH ROW EXECUTE FUNCTION p21_user_scope('StudentId');
CREATE TRIGGER p21_authority_scope BEFORE INSERT OR UPDATE ON finance_authorities FOR EACH ROW EXECUTE FUNCTION p21_user_scope('UserId');
CREATE TRIGGER p21_journal_scope BEFORE INSERT ON finance_journals FOR EACH ROW EXECUTE FUNCTION p21_user_scope('ActorId');
CREATE TRIGGER p21_payment_scope BEFORE INSERT OR UPDATE ON finance_payments FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RegisteredBy','VerifiedBy');
CREATE TRIGGER p21_refund_scope BEFORE INSERT OR UPDATE ON finance_refunds FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RequestedBy','ReviewedBy');
CREATE TRIGGER p21_plan_scope BEFORE INSERT OR UPDATE ON finance_payment_plans FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RequestedBy','ReviewedBy');
CREATE TRIGGER p21_award_scope BEFORE INSERT OR UPDATE ON finance_awards FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RequestedBy','ReviewedBy');
CREATE TRIGGER p21_audit_scope BEFORE INSERT ON finance_audit_events FOR EACH ROW EXECUTE FUNCTION p21_user_scope('ActorId');
CREATE TRIGGER p21_reconciliation_scope BEFORE INSERT ON finance_reconciliations FOR EACH ROW EXECUTE FUNCTION p21_user_scope('ActorId');
""";

    private const string DropIntegritySql="""
DO $$ DECLARE table_name text; BEGIN
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines','finance_adjustments','finance_allocations','finance_receipts','finance_commands','finance_audit_events','finance_reconciliations','finance_installments','finance_scholarships','finance_policies','finance_accounts','finance_authorities','finance_fee_schedules','finance_invoices','finance_payments','finance_refunds','finance_payment_plans','finance_awards','finance_holds','finance_fee_items','finance_invoice_lines'] LOOP
  EXECUTE format('DROP TRIGGER IF EXISTS p21_immutable ON %I',table_name);EXECUTE format('DROP TRIGGER IF EXISTS p21_state ON %I',table_name);
  EXECUTE format('DROP TRIGGER IF EXISTS p21_child ON %I',table_name);EXECUTE format('DROP TRIGGER IF EXISTS p21_balanced ON %I',table_name);EXECUTE format('DROP TRIGGER IF EXISTS p21_reconciled ON %I',table_name);
 END LOOP;
END $$;
DROP TRIGGER p21_account_scope ON finance_accounts;DROP TRIGGER p21_authority_scope ON finance_authorities;DROP TRIGGER p21_journal_scope ON finance_journals;
DROP TRIGGER p21_payment_scope ON finance_payments;DROP TRIGGER p21_refund_scope ON finance_refunds;DROP TRIGGER p21_plan_scope ON finance_payment_plans;
DROP TRIGGER p21_award_scope ON finance_awards;DROP TRIGGER p21_audit_scope ON finance_audit_events;DROP TRIGGER p21_reconciliation_scope ON finance_reconciliations;
DROP FUNCTION p21_immutable();DROP FUNCTION p21_user_scope();DROP FUNCTION p21_state_guard();DROP FUNCTION p21_child_guard();DROP FUNCTION p21_validate_journal();DROP FUNCTION p21_validate_account();
""";
}
