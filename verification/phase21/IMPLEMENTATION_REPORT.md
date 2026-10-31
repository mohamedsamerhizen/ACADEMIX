# Phase 21 implementation and verification

**Status: `PHASE21_CODE_COMPLETE_VERIFICATION_PENDING`**

Implementation location: `phase19-work` (the existing Phase 19/20 solution). The .NET 10 API and test projects remain the two-project architecture. PostgreSQL remains the authoritative store. No frontend, deployment, installer, or infrastructure work was added.

## Functional scope

- Institution/student/currency accounts expose statements, invoices, posted journals, cash credit, receivables, policy-dependent refundable amounts, restrictions, reconciliation, and audit history. Account freeze/unfreeze uses expected versions.
- Versioned, effective-dated policies and draft/published/retired fee schedules support term or explicitly bounded academic-year scope, program-specific fixed/per-credit tuition, registration, laboratory/service, and optional charges. Published pricing is immutable.
- Assessment requires actual active enrollment and authoritative published curriculum credit values. Draft assessment preserves pricing and academic-source evidence; posting checks that evidence again. Clients cannot provide authoritative invoice totals. The account/term/tuition natural key prevents duplicate tuition liabilities.
- Posted liabilities change through debit/credit adjustments and full invoice reversal, retaining the original lines and correcting journals. Reductions reprice aid and release excess allocations to cash credit. Reopening a reversed invoice uses an explicit debit correction requiring billing and reversal authority; it retains the original invoice identity.
- Payment plans split actual outstanding debt, preserve rounding remainders, support independent approval and superseding revisions, derive partial installment settlement in due-date order, and add no fee for rescheduling. Debt added after approval remains separately due under the original invoice date.
- Scholarships support versioned fixed/percentage/full reductions, eligible charge categories, program/term/date restrictions, supported GPA/standing eligibility, optional funding caps, independent decisions, immutable approval evidence, revocation, and correcting repricing. Priority, code, and identity define deterministic order. The same scholarship code cannot be applied twice through different versions. Aid never becomes cash collected.
- Cash, bank transfer, manual transfer, and externally confirmed reference records support pending verification, independent verification where required, posting, explicit invoice allocation, unapplied credit, and authorized reversal. Student transfer claims always require verification. Unknown payment fields, including raw card data, are rejected.
- Posted payments receive unique numbered receipts with immutable snapshots, existing versioned document-key HMAC proofs, authenticated PDF download, and capability-based public verification. Verification exposes reversal, partial/full refund, and revoked-key states. HMAC verification is not PAdES signing. PDFs use the existing renderer and are generated on demand.
- Refund requests reserve eligible unapplied cash; independent review precedes authorized execution recording. Partial refunds, rejection, reversal/correction, unique execution references, and source-payment capacity checks preserve lineage. Execution records an institution's assertion; it does not transfer money or confirm a bank transaction.
- Internal reconciliation compares journals, liabilities, allocations, assistance, cash, and refunds. Provided-bank-data reconciliation compares references/currencies/amounts without posting money or creating bank confirmation.
- Optional, default-off financial holds cover registration, graduation clearance, and official transcript/standing/graduation document issuance. Holds retain reason, policy, status, and effective dates. Live debt/date evaluation avoids a new scheduler. Resolution releases or reschedules holds through finance mutations. Student statements/payment information remain accessible.
- Reports separate every currency and institution, use authoritative correcting entries, expose assessed/assistance/net collected/refunds/receivables/unapplied balances, installment-aware aging, and paginated term/program/payment-method breakdowns. Term/program collection means net cash allocated to those invoices; unapplied deposits have no invented academic attribution.

### Reused systems and compatibility

Reuses ASP.NET Identity and current role checks, institutional membership, `RetryingServiceWork`, the existing institution coordination lock, academic program/term/enrollment/curriculum/standing data, academic policy evaluation, graduation management, document requests/keys/PDF rendering, audit, and transactional Outbox.

Finance adds restrictions to existing academic decisions rather than replacing academic holds. New financial policies cannot retroactively invalidate earlier graduation/document requests: policy publication and invoice posting are compared with the original request cutoff. Already issued artifacts retain their existing idempotent paths. Finance does not write grades, earned credits, or transcript academic content. Existing route/authorization tests include the finance controller; the Phase 20 migration test now selects its migration explicitly while retaining ordering/model assertions.

## API

All routes below have prefix `/api/finance`. Controllers delegate business rules to `FinanceService`. Every route requires authentication except the proof-based receipt verification route. Resource institution/ownership and current role/permission checks occur in services; URL institution IDs do not grant authority. Action values are validated, case-sensitive contracts. Mutations require nonempty `RequestId`; versioned transitions also check expected versions.

| Method | Route | Workflow |
|---|---|---|
| PUT | `institutions/{institutionId}/authorities/{userId}` | Grant/revoke granular financial authority |
| POST | `institutions/{institutionId}/policies` | Publish immutable policy revision |
| POST | `policies/{id}/actions` | Retire policy |
| GET | `institutions/{institutionId}/catalog` | Paged authorities/policies/schedules/scholarships |
| POST | `institutions/{institutionId}/fee-schedules` | Create complete draft schedule |
| POST | `fee-schedules/{id}/actions` | Publish/retire schedule |
| POST | `institutions/{institutionId}/students/{studentId}/accounts` | Open currency account |
| GET | `me/accounts` | Own paged accounts |
| GET | `institutions/{institutionId}/accounts` | Authorized account lookup |
| GET | `accounts/{id}/statement` | Statement/date-filtered journals and holds |
| GET | `accounts/{id}/history` | Paged invoices/payments/refunds/awards/plans/audit |
| POST | `accounts/{id}/actions` | Freeze/unfreeze/refresh holds |
| POST | `accounts/{id}/assessments` | Assess/reassess draft invoice |
| GET | `invoices/{id}` | Invoice detail, balance, plan settlement |
| POST | `invoices/{id}/actions` | Post/reverse invoice |
| POST | `invoices/{id}/adjustments` | Debit/credit correction |
| POST | `invoices/{id}/payment-plans` | Propose/reschedule debt |
| POST | `payment-plans/{id}/decision` | Approve/reject plan |
| POST | `institutions/{institutionId}/scholarships` | Create scholarship version |
| POST | `invoices/{id}/awards` | Request assistance |
| POST | `awards/{id}/actions` | Approve/reject/revoke award |
| POST | `accounts/{id}/payments` | Register payment/transfer claim |
| POST | `payments/{id}/actions` | Verify/reject/post/reverse |
| POST | `payments/{id}/allocations` | Allocate posted cash to invoice |
| GET | `payments/{id}/receipt` | Authorized receipt snapshot/status |
| GET | `payments/{id}/receipt/pdf` | Authorized receipt PDF |
| POST | `receipts/verify` | Anonymous proof verification |
| POST | `payments/{id}/refunds` | Request/reserve refund |
| POST | `refunds/{id}/actions` | Approve/reject/execute/reverse |
| GET | `accounts/{id}/reconciliation` | Internal account reconciliation |
| POST | `institutions/{institutionId}/bank-reconciliations` | Compare provided reference data |
| GET | `bank-reconciliations/{id}` | Authorized immutable comparison result |
| GET | `institutions/{institutionId}/reports` | Currency-separated report |

Statements/history/catalogs/account lists and report breakdowns use pages of 50. Reports accept institution-local `from` inclusive and `until` exclusive, a maximum ten-year window, and expose their UTC as-of cutoff. Balance/aging snapshots are as of that cutoff; movement totals cover the requested interval. Reports, detailed statements/history, invoice reads, and reconciliation use repeatable-read snapshots where multiple queries must agree.

## Schema and migration

Migration: **`20261102100000_Phase21StudentFinance`**, ordered after `20261101100000_Phase20AcademicAdvisingAndGraduation`. The new migration, designer, guards partial, and current model snapshot are supplied. Historical migration implementations were preserved. Up creates finance structures without deleting existing tables, columns, or data.

22 new tables:

`finance_accounts`, `finance_authorities`, `finance_commands`, `finance_fee_schedules`, `finance_fee_items`, `finance_policies`, `finance_reconciliations`, `finance_scholarships`, `finance_audit_events`, `finance_payments`, `finance_invoices`, `finance_invoice_lines`, `finance_receipts`, `finance_refunds`, `finance_awards`, `finance_journals`, `finance_journal_lines`, `finance_payment_plans`, `finance_installments`, `finance_adjustments`, `finance_allocations`, `finance_holds`.

Critical keys/guards include account institution+student+currency uniqueness; invoice account+term+scope uniqueness; institution+request idempotency; institution+normalized non-rejected payment reference uniqueness; institution+posting key uniqueness; unique receipt per payment; unique refund execution reference; unique active plan; active award invoice+scholarship-code uniqueness across versions; and composite tenant/account/currency relationships on monetary allocations. Status, amount, currency, source ownership, and immutable-evidence checks accompany foreign keys/indexes.

PostgreSQL trigger functions protect posted history, published pricing, receipt/command/audit/reconciliation evidence, valid state transitions, and institutional users. Deferred constraint triggers check complete balanced posting, source matching, account reconciliation, refund reservations, invoice line/credit limits, allocation reversal lineage, aid/funding limits, and installment totals at transaction end. These SQL functions were generated and inspected, **not executed against PostgreSQL**.

## Financial integrity and authorization

- Amounts use signed integer minor units; calculation quantities/rates and cross-account aggregates use decimal. There is no binary floating-point money or automatic FX. Each monetary amount/account bucket is bounded by `9,000,000,000,000,000` minor units. Calculations round once, away from zero, and reject invalid/zero-rounded charges or overflow.
- The frozen currency catalog contains 165 current ISO 4217 codes with numeric minor-unit exponents 0 through 4. It was generated from the [SIX ISO 4217 Maintenance Agency List One](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml), publication `2026-09-17`; the source snapshot is included as `iso4217-list-one.xml`. Codes without numeric minor units are excluded. Institutions explicitly publish currency policies; this catalog needs maintenance when ISO changes.
- A single posting method requires an owning transaction and creates exactly two opposite journal legs. Charges increase receivables against assessed revenue; cash collection increases recorded funds against student credit; allocation consumes credit against receivables. Assistance reduces debt through separate aid journals. Reversals use correcting entries and original-source references. Receivables/recorded funds cannot become negative; student credit cannot become a debit balance.
- Posted totals are derived from journals and immutable evidence, rather than independently editable balance fields. Cash credit and scholarship reductions are distinct. A paid invoice reduction returns excess allocation to unapplied cash; scholarship revocation may restore debt rather than creating fictitious cash.
- Each write acquires the existing institution coordination lock, then rechecks authoritative membership/roles/permissions inside one transaction. Fresh service/DbContext scopes are used for retries. Journals, source state, receipts, command result, holds, audit, and Outbox commit together. Database uniqueness and deferred guards are additional protection; no independent nested financial commit exists.
- Idempotency is scoped by institution/request and fingerprints operation, account, and full input. Identical authorized retries return the recorded result; changed input returns conflict. Authorization is checked before replay, so role/authority revocation still applies. Version checks reject conflicting lifecycle edits.
- Financial flags are `Read`, `Configure`, `Billing`, `Payments`, `Approve`, `Reverse`, `Refunds`, and `Report`. Financial operations require an explicit persisted grant plus a current existing administrative role. Institution authority administration uses the existing institution-admin/superadmin control plane. No new global role was invented. An institution administrator cannot operate in another institution. Revoked roles invalidate retained grants.
- Students read only their own accounts and can submit supported requests. Money writes cannot target the acting staff member as student. Independent review cannot be performed by the requester or beneficiary. Notifications carry generic financial messages/entity identities, without payment references, proofs, or amounts.
- Pending/unverified payments create no cash or receipt. Refund requests/approvals reserve source cash; executed, unreversed refunds reduce it. Allocations and reservations cannot exceed posted source cash. Payment reversal first releases allocations and refuses unresolved/executed refunds. Refund reversal restores accounting cash through a correcting journal; it does not assert a bank recall.

## Actual verification

| Check | Executed result | Evidence |
|---|---|---|
| Restore | Succeeded; projects up to date | `restore.log` |
| Release build, warnings as errors | Succeeded; 0 warnings, 0 errors | `build.log` |
| All non-container tests | **368 passed, 0 failed, 0 skipped** | `tests.log`, `phase21-local.trx` |
| New Phase 21 local test cases | **53 executed and passed**, included in 368 | Phase21-named TRX results |
| EF pending-model check | No changes since last migration | `model.log` |
| PostgreSQL migration SQL generation | Succeeded; Phase20-to-Phase21 script generated | `migrations.sql`, `sql-generation.log` (empty success output) |
| Integration discovery only | **88 discovered**, including **8 Phase 21 scenarios**; exit 0 | `integration-discovery.log` |
| PostgreSQL migration/application/integration execution | **Not executed** | Prohibited container startup respected |

Commands used the existing .NET solution and Release configuration. Local tests used `FullyQualifiedName!~Academix.Tests.Integration`; discovery used `--list-tests --filter FullyQualifiedName~Academix.Tests.Integration` with `--no-build --no-restore`. EF commands used `--configuration Release --no-build`. No Docker, PostgreSQL container, or MinIO was started for this phase.

Local tests cover money/currency/rounding, fixed and credit tuition, academic-year scope, fee/version rules, balanced postings, capacity/refund/installment rules, deterministic assistance/funding, net correction reporting, local dates/holds, authority/role revocation/tenant ownership/independent approval, strict payment input, route protection, model consistency, natural uniqueness, and currency-aware relationships. Natural keys and transaction semantics have local structural coverage; definitive race/rollback proof requires PostgreSQL.

The eight new PostgreSQL scenarios compile and were discovered only:

1. Billing, cash, allocation, partial refunds/reversal, reconciliation, receipt verification.
2. Repeated concurrent posting and duplicate references cannot collect twice.
3. Simultaneous refund requests reserve only available cash.
4. Paid-invoice scholarships, credit notes, revocation, and debt-only rescheduling.
5. Failure after journal write rolls back liability/Outbox/idempotency; database rejection of immutable edits and incomplete journals.
6. Financial holds respect opt-in policy, due dates, student access, and approved plans.
7. Currency-separated reporting, safe notifications, and supplied-data reconciliation without cash/bank confirmation.
8. Revocation and institutional ownership apply even to idempotent retries.

## Remaining verification and limitations

- PostgreSQL must still apply the migration and execute all integration scenarios in an authorized environment. SQL generation/model consistency do not establish trigger syntax/runtime correctness, query translation, migration application, deadlock behavior, or concurrency safety. Those are the principal outstanding risks. No PostgreSQL passing result or Docker health claim is made.
- The institution-wide coordination lock intentionally favors correctness and shared academic/financial lock ordering over maximum finance-write throughput. Load/performance and large institutional datasets have not been benchmarked.
- Financial authority currently requires the existing administrative roles plus explicit flags; a standalone cashier employment/role model is not introduced. Institution timezone remains stable across policy revisions for consistent historical reporting. Academic-year scopes use explicit date spans rather than inferring one university's calendar.
- This is a student receivables/cash/assistance subledger, not a complete institutional general ledger, bank-account integration, tax engine, FX engine, or financial-statement certification. No interest/penalty engine or automatic scholarship eligibility beyond supported academic records is supplied.
- No live gateway, raw card processing, actual disbursement, bank recall, live bank confirmation, or externally trusted/PAdES receipt signature is implemented. Manual execution/reference records require operational evidence outside this API.
- Examination financial restrictions are unsupported because the existing institution policy does not provide that integration. No universal financial block, optional-reminder scheduler, or frontend was introduced.
- No known failing local test remains. The deferred SQL/accounting invariants and multi-request financial workflows remain **unverified in PostgreSQL**, not proven complete by the passing local suite.

Final status remains **`PHASE21_CODE_COMPLETE_VERIFICATION_PENDING`** until the PostgreSQL migration and integration suite execute successfully.
