# Phase 19 remediation implementation report

2026-10-30 (Asia/Baghdad). Verdict: **PHASE19_CODE_COMPLETE_VERIFICATION_PENDING**.

This remediation changes the existing implementation under `phase19-work/Academix.slnx`. The workspace root contains an earlier version without Phase19 completion workflows. No Git metadata is available. The two-project layout, public HTTP contracts and historical migrations are preserved. No Phase20 work, database reset, deployment or delivery-package rebuild was performed.

## Confirmed defects and repairs

| Severity | File/method | Root cause and repair |
|---|---|---|
| Critical | `AcademicBackgroundWorker.Tick` | One exception boundary covered all attempts and Outbox work. Isolate the scan, each finalization, scheduling and delivery in independent scopes; log operation identifiers and propagate shutdown cancellation. Rotate bounded UUID pages so 100 persistent failures cannot starve later attempts. Keep the 15-second polling interval. |
| High | `InstitutionalDocumentService.IssueCore` | Upload preceded commit without recovery tracking. Commit an independent Outbox cleanup intent before every upload. Reconcile under the institution lock, preserving any referenced object and retrying storage failures durably. |
| High | `InstitutionalDocumentService.IssueCore` | An inner execution strategy could reuse tracked entities after rollback despite the outer fresh-scope retry service. Remove the nested retry boundary for issuance; retain `RetryingServiceWork` as the transaction retry owner. |
| Medium | `DocumentKeyService.Expected`, `ArtifactSignatureValid`, secret decryption | Missing keys used `SingleAsync`; unavailable keyrings exposed raw cryptographic failures. Return explicit HTTP 409 conflicts and fail closed without exposing secrets. |
| Evidence | `evidence/phase19-migrations.sql` | Evidence ended after the second Phase19 migration. Regenerate the complete chain from migration 0 through `20261031190700_Phase19ArtifactContentSignature`. |

The existing exam finalizer already uses a conditional status/deadline update and a timeout audit in one transaction. It safely preserves answers, rolls back failures and prevents repeated/concurrent successful finalization. This behavior is retained.

## PDF recovery design

New `DocumentArtifactCleanup.Schedule` commits its intent through an independent scope before upload. Issuance holds the institution row with `FOR NO KEY UPDATE`: this still serializes issuance and academic operations while allowing the independent intent's foreign-key `KEY SHARE` lock. Retaining `FOR UPDATE` here would deadlock that insert.

Cleanup takes `FOR UPDATE` on the same institution until the reference check and deletion finish. It waits for issuance to commit or roll back and prevents a retry from uploading while deletion is in flight. The private path is reconstructed from institution/document UUIDs, never accepted as arbitrary event input. Any issuance referencing that path prevents deletion. Each upload attempt gets a new intent, including retries at the same stable path. A pre-upload crash is harmless; a post-upload crash remains recoverable; an ambiguous successful commit retains the referenced artifact.

`OutboxService.Deliver` handles cleanup using existing leasing, expired-lease recovery, conditional acknowledgment, bounded backoff and receipt uniqueness. Cleanup also runs for inactive institutions. Repeated removal is safe under the store's idempotent contract. No distributed transaction or schema change is needed. Recovery requires enabled processing and eventual database/storage availability. Objects orphaned before this fix have no intent and are not automatically inventoried; manually cancelling/deleting intents also prevents recovery.

## Existing capabilities reviewed in source

Applicant registration/review and accepted-applicant activation/conversion; typed academic request fulfillment; curriculum transition approval/execution, equivalencies and recognized-credit history; mapped JSON and quoted CSV imports with preview, approval, resumable application, profiles, published-history references, activation checkpoints, reconciliation and idempotency; immutable PDF templates, issuance, proof/artifact verification, rotation, revocation and supersession; durable Outbox scheduling, internal notifications and receipts are present.

This corrects the previous report's obsolete claims that applicants, CSV, PDF artifacts, transitions and a dispatcher were absent. Arbitrary imported grades remain unsupported by design. Source inspection is not integration-test proof. External email/webhook delivery was not added.

## Security and persistence

The existing versioned HMAC-SHA256 model is preserved. Separate proof and artifact signatures bind document identity, kind/template context, canonical JSON snapshot and, for artifacts, the PDF digest. Comparisons are constant-time; key lookups include document institution. Retirement preserves old verification; revocation returns `KeyRevoked` for a correctly proven document. Public verification discloses status/timestamps rather than snapshots. Tenant/resource checks remain in issuance, download, key administration and the reviewed workflows.

This is server-side HMAC authenticity/integrity verification, **not PAdES, a PDF certificate signature or independently verifiable non-repudiation**. A holder of server signing secrets can generate signatures. Missing/unreadable key material fails closed; artifact verification also depends on available storage.

Compose already persists MinIO `/data` and the ACADEMIX Data Protection keyring in named volumes; no persistence defect was found there. Preserve/back up the database, artifacts and keyring together. Replicas need the same durable keyring/application name. Restrict keyring access and configure encryption at rest. Historical proof initialization requires the explicitly supplied original legacy key once. No secret was introduced into source/evidence.

## Changed code

Inside `Academix.Api`: `Services/Infrastructure/AcademicBackgroundWorker.cs`, `Services/Infrastructure/OutboxService.cs`, `Services/Institutional/InstitutionalDocumentService.cs`, `Services/Institutional/DocumentKeyService.cs`, new `Services/Institutional/DocumentArtifactCleanup.cs`, and `Extensions/ServiceCollectionExtensions.cs`.

Inside `Academix.Tests`: new `Integration/Phase19RemediationTests.cs`, new `Integration/Phase19MigrationExecutionTests.cs`, new `Unit/BackgroundWorkerCancellationTests.cs`, plus `Integration/Phase19CompletionWorkflowTests.cs` (partial class for setup reuse), `Integration/TestArtifactStore.cs` (observable objects and remove-failure injection), and `Unit/Phase19CompletionTests.cs` (keyring-loss regression).

The test store substitutes only external storage; integration tests still require actual PostgreSQL, Identity, migrations, transactions and PDF rendering. See the verification report for exact counts and execution limits. Existing ZIPs/manifests have not been rebuilt or certified for this revision.
