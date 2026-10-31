# ACADEMIX final engineering remediation

**Decision: REMEDIATION_CODE_COMPLETE_VERIFICATION_PENDING**

Source: `C:\Users\lenovo\Desktop\ACADEMIX\phase24-final`. The solution, API/test projects and Phase 24 completion migration were confirmed before editing. No outdated source tree was used as the implementation baseline. The existing two-project .NET 10/PostgreSQL modular monolith remains intact.

The six requested findings have source repairs and regression coverage. Local verification passed. PostgreSQL migration application, database integration/concurrency tests and real MinIO execution remain unexecuted. This report does not certify production readiness.

## A. Resolved findings

| Finding | Final code status | Actual verification boundary |
|---|---|---|
| F01 graduation-document dependency | Fixed | Release compilation/local suite passed; full PDF/credential PostgreSQL workflow compiled and discovered only |
| F02 assessment/gradebook/transcript consistency | Fixed | Source rules and migration/model checks passed locally; complete academic workflow, rollback and reconciliation scenarios require PostgreSQL |
| F03 scholarship lifecycle event identity | Fixed | Existing finance regression strengthened to cover approval/revocation/replay/events; compiled and discovered, not executed |
| F04 academic changes versus campus allocations | Fixed | Seat, capacity and interval rules passed locally; individual/batch, isolation and concurrent booking scenarios compiled/discovered only |
| R01 quality transaction/retry orchestration | Fixed | Configured Npgsql retry strategy and direct controller/service path confirmed the incompatible transaction pattern from source. The original runtime exception was NOT reproduced. Retry/concurrency scenarios remain pending |
| R02 authority after institution-lock waits | Fixed | Current authority is checked after coordination in affected writes. Deterministic PostgreSQL lock-wait/revocation cases are compiled/discovered, not race-verified |

### F01 — graduation documents and invalidated credentials

Root cause: academic invalidation updated issued credentials and transcript/standing documents, but omitted graduation documents. Public verification only inspected the document marker.

Changes:

- `Academix.Api/Services/Infrastructure/AcademicFreshness.cs`: graduation documents now receive the same tenant/student-scoped invalidation as their supporting credential, inside the owning academic transaction. Existing markers are not rewritten repeatedly.
- `Academix.Api/Services/Institutional/InstitutionalDocumentService.cs`: public graduation-document verification additionally requires a currently issued, non-invalidated credential for that institution/student. This protects older issuances whose marker was not propagated. Source checks do not replace an existing revoked, superseded or expired status. Existing key/artifact integrity checks remain.
- The new migration repairs existing invalidated-credential/document relationships and invalidates dependent documents when historical assessment evidence must be reconciled.

Original issuance metadata, snapshot, proof and PDF bytes remain historical evidence. The repair does not delete or rewrite PDFs. Reassessment and reissuance use the existing authorization and supersession workflows; clearing a credential marker does not silently restore an old document's validity.

Regression: `IssuedGraduationPdfBecomesSourceChangedAndReissuanceRetainsOriginalArtifact` issues a credential/PDF, creates real unaffected documents for another student and another institution, corrects a final grade, checks invalidation and unchanged original bytes, synchronizes/reassesses and issues a replacement. PostgreSQL execution pending.

### F02 — assessment evidence and authorized reconciliation

Root cause: published course results contained no assessment dependency/version evidence. Exam corrections could change primary scores while course results/transcripts still agreed with each other and passed the old freshness check.

Changes:

- `Academix.Api/Entities/Assessment/GradebookEntities.cs`: persisted source evidence, reconciliation marker and official-override marker; audit evidence and durable reconciliation command/result fields.
- `Academix.Api/Entities/Examinations/GradingEntities.cs`: separate score version. Score changes advance it; a rejected appeal or metadata-only report-version change does not falsely alter score provenance.
- `Academix.Api/Services/Infrastructure/AssessmentFreshness.cs` (new): captures deterministic, student/section-scoped component and source evidence, including published exam identities/score versions, assignment grades, manual-entry versions and relevant team submissions. Canonical comparison tolerates equivalent JSON formatting. Source changes mark dependent results without overwriting their official percentages. Only the first transition into a stale state adds a SourceChanged audit event.
- `Academix.Api/Services/Examinations/ExamGradingService.cs`: published score changes and publication coordinate with gradebook publication/reconciliation using the institution lock; invalidation commits with the assessment change. Affected paths use the existing outer fresh-scope retry owner rather than an inner tracked-context retry.
- `Academix.Api/Services/Assessment/GradebookService.cs`: initial publication captures source evidence; normal official corrections preserve an explicitly justified manual override and capture the reviewed sources. Results expose additive reconciliation/override flags. Audit responses expose preserved evidence to existing authorized staff.
- `Academix.Api/Services/Assessment/GradebookReconciliation.cs` (new): instructor/admin-authorized recalculation or justified retention, expected-version checks and institution-scoped command fingerprints/results. Identical command replay returns its original result without repeated grade/audit effects. Different input with the same command conflicts. An existing manual override cannot be discarded by automatic recalculation; retain it or use the existing justified correction workflow.
- `Academix.Api/Services/Records/RecordsService.cs` and `AcademicFreshness.cs`: stale assessment-derived results block transcript synchronization, current transcript/standing reads, enrollment eligibility and existing sensitive academic freshness checks. Degree audit, graduation and source-dependent documents inherit the gate. Transcript synchronization remains an explicit authorized operation after reconciliation.
- `Academix.Api/Services/Quality/QualityService.cs`: live proxy evidence rejects unreconciled results within the actual program cohort. Publication coordinates source reads; already published snapshots remain historical publication-time evidence.
- `Academix.Api/Services/Analytics/AnalyticsService.cs`: current percentage aggregates and student-grade exports reject flagged results. Historical record counts are not reinterpreted as recalculated grades.
- `Academix.Api/Services/Advising/AdvisingService.cs`: affected risk rows show AssessmentReconciliationRequired and no current grade percentage, while unaffected students remain available.
- `Academix.Api/Services/Learning/LearningOperations.cs` and `Services/AdvancedLearning/GroupLearningService.cs`: directly related assignment/project grading uses the same atomic source-change propagation, preventing an alternate assessment path from recreating the defect.
- `Academix.Api/Data/Configurations/GradebookConfiguration.cs`, `GradingConfiguration.cs`, `DTOs/Assessment/GradebookContracts.cs`, `DTOs/Advising/AdvisingContracts.cs` and `Controllers/GradebookController.cs`: additive persistence/contracts/route support.

New route: `POST /api/gradebook/sections/{sectionId}/students/{studentId}/reconciliation`.

Request: `RequestId`, `Decision` (Recalculate or Retain), `Reason`, `ExpectedVersion`. Existing correction routes remain supported. Responses add `RequiresReconciliation` and `IsManualOverride`. An idempotent response describes the original command result; current state is obtained from the result endpoints.

Regression coverage includes version/canonical evidence rules, legacy uncertainty, override protection, full exam publication → course publication → transcript sync → upheld appeal → blocked sensitive reads/quality publication → reconciliation → sync, repeated command replay, altered replay rejection, manual override retention, unauthorized reconciliation and injected rollback of the source change plus dependent markers. PostgreSQL scenarios are unexecuted.

### F03 — scholarship approval, revocation and Outbox identity

Root cause: both transitions used FinanceAwardReviewed plus the award ID as the unique event key.

Changed files: `Academix.Api/Services/Finance/FinanceAssistance.cs` and `FinanceService.cs`.

Award transition events now use the already durable financial RequestId for event identity. The payload still identifies the award, rather than replacing the resource identity with a command ID. Existing institution/request command uniqueness and fingerprint validation prevent duplicate events on replay. Financial corrections, state, audit and Outbox still commit together. No historical movement, uniqueness guard or notification kind was weakened.

Regression: the existing `Phase21FinanceTests.PaidInvoiceScholarshipsCreditNotesRevocationAndReschedulingUseCorrectingEntries` now also replays revocation, rejects a student replay, checks two distinct transition events with the correct award payload, and checks unchanged journal count on replay. Its existing correction/balance/reconciliation assertions remain. PostgreSQL execution pending.

### F04 — maintaining existing campus guarantees

Root cause: reservation/timetable writers validated current participants, but later enrollment/capacity/staff changes did not apply the matching checks.

Changed files:

- `Academix.Api/Services/AcademicOperations/CampusIntegrity.cs` (new).
- `Academix.Api/Services/Academic/AcademicOperations.cs`.
- `Academix.Api/Services/Governance/GovernanceService.cs`.

Individual and batch enrollment now validate existing physical exam seats and the incoming students' overlapping weekly timetables and dated sessions/exams. Seat checks use the resulting unique enrollment count. Weekly comparisons respect overlapping term periods and exclude closed terms. Section capacity changes validate assigned timetable rooms, future session rooms and future exam venue rooms. Instructor assignment also checks existing timetable/appointment commitments. All affected mutations use the existing institution coordination and authorization conventions.

Existing booking/session writers retain their overlap checks, maintenance rules, histories and rescheduling/cancellation alternatives. No new scheduling-exception capability was invented; existing prerequisite/credit/hold exceptions continue through academic eligibility. Room and student queries remain institution-scoped. No scheduler or new campus infrastructure was added.

Local tests cover seats, room limits, no-room behavior and half-open intervals. PostgreSQL scenarios cover post-booking enrollment, conflicts introduced by individual/batch enrollment, capacity changes, valid rescheduling followed by enrollment, independent institutions with identical room/timetable codes, and simultaneous enrollment/reservation. These scenarios were compiled/discovered only.

### R01 — complete quality transaction retries

Changed files: `Academix.Api/Services/Quality/QualityEvidenceService.cs` and `QualityService.cs`.

Attachment and sealing now run the entire logical transaction through RetryingServiceWork, yielding a new service/DbContext on every attempt. Institution coordination serializes affected writes and current authorization follows that lock. Read-committed transactions plus this coordination avoid retaining a pre-wait authorization snapshot. Existing unique attachment keys, sealed-pack guards and historical evidence remain.

Quality publication and its mutable outcome/course/criterion mapping sources participate in the same coordination, so the publication lock is meaningful across those source writers. Existing frozen review/pack snapshots are not rewritten.

Two added PostgreSQL scenarios cover simultaneous identical attachments/seals and an injected SQLSTATE 40001 failure on the first attachment attempt, using a nontransactional sequence to prove a later attempt occurred and only one item persisted. The original runtime incompatibility and these scenarios were NOT executed in this session.

### R02 — current authority after waiting

Changed files:

- `Academix.Api/Services/AcademicOperations/CampusReservationService.cs`: creation/cancellation authorize after the institution lock, before changes or idempotent returns.
- `CampusFacilitiesService.cs`: building/room writes check current authority after coordination.
- `TeachingOperationsService.cs`: affected session creation/rescheduling revalidate staff authority after the wait; existing slot-write checks remain.
- `Academix.Api/Services/StudentServices/StudentServiceCaseService.cs`: service definitions/staff grants and affected student mutations revalidate after coordination.
- The repaired quality workflows, grading source changes, transcript sync and reconciliation similarly place current authorization inside the coordinated operation.

No new role names, JWT-trusting bypass or inverted institution lock order was introduced. Existing IdentitySecurity reads active users, membership and persisted roles. Security revocation is not made dependent on acquiring a finance/campus institution lock.

Five compiled PostgreSQL cases hold the institution row from a separate connection, verify the application is actually waiting through pg_blocking_pids, revoke role/disable account/change membership, release the lock and assert no protected mutation. They cover reservations, building creation and service grants, followed by a still-authorized administrator control. No sequential unit result is represented as proof of this race.

## B. Additional directly related repairs

### Durable LMS object/database recovery

The old upload path uploaded first and relied on immediate catch/delete cleanup. Process death, failed deletion or ambiguous database completion could leave disagreement.

Changed files: `Academix.Api/Services/Learning/LearningOperations.cs`, new `LearningArtifactCleanup.cs`, `Services/Infrastructure/OutboxService.cs` and `Extensions/ServiceCollectionExtensions.cs`.

Uploads now have a stable logical file identity across retry attempts, a fresh owning transaction, current ownership checks after coordination and an independently committed cleanup intent before storage. FOR NO KEY UPDATE permits the intent's foreign-key KEY SHARE lock while retaining serialization with cleanup. Successful replay recognizes committed metadata. Cleanup checks references under the institution lock; it removes only unreferenced keys, and failures retain durable Outbox retry state. Immediate deletion after an uncertain database failure was removed. The existing IPrivateFileStore abstraction is used consistently, including by the database integration fixture.

The new integration scenario injects a database failure after upload, checks that metadata rolled back while the cleanup intent/object survived, injects cleanup failure, retries it through the actual Outbox, and verifies a committed private upload is preserved and another student cannot download it. It is compiled/discovered only; actual MinIO crash/network semantics remain unverified.

### Focused second-pass outcomes

The second pass corrected directly connected assignment/project freshness paths, analytics/advising presentation, quality-source coordination, post-lock checks, document status precedence, source score-version granularity and legacy stale-state version advancement. It also strengthened non-vacuous student/institution isolation and post-upload database-failure scenarios. No unrelated feature roadmap was implemented.

## C. Migration and API safety

New migration: **20261106100000_FinalEngineeringRemediation**. Its ID follows the latest existing Phase 24 migration; it is an ordering identifier, not a claim about execution date.

Files:

- `Academix.Api/Data/Migrations/20261106100000_FinalEngineeringRemediation.cs`.
- Its matching `.Designer.cs`.
- Updated `AcademixDbContextModelSnapshot.cs`.

Adds assessment evidence/reconciliation/override fields, exam ScoreVersion and audit evidence/command/result fields. Adds an institution-scoped unique reconciliation-command index, score/evidence/command constraints and an append-only gradebook-audit trigger. Up does not recreate/drop tables or delete official evidence. Down explicitly refuses destructive rollback of remediation evidence; recovery is forward-only. Historical migration implementations were not edited.

**Populated-database upgrade behavior:** older external-assessment-derived grades have no reliable captured source versions. The migration retains their values but marks them for reconciliation, advances their result versions, preserves known manual corrections as overrides and invalidates affected documents/issued credentials. Manual-only historical grades are not treated as unknown external assessment evidence. No fabricated historical snapshot is backfilled. Institutions must review the affected results, synchronize transcripts and reassess/reissue credentials/documents through existing workflows where appropriate.

Public APIs are preserved apart from additive result/audit flags/evidence and the new reconciliation route. The explicit route-snapshot test includes the added route; its assertion was not weakened. Current-record requests may now return conflict when sources require reconciliation, which is the intended integrity behavior.

EF pending-model check passed. The new Phase24-to-remediation PostgreSQL script was generated as `migration.sql`. **SQL generation does not prove successful PostgreSQL application or trigger execution.**

## D. Actual verification

| Check | Actual result | Evidence |
|---|---|---|
| Restore | Passed; dependencies up to date | restore.log |
| Release build with -warnaserror | Passed; 0 warnings, 0 errors; API and tests compile | build.log |
| Existing Docker-independent cases | 512 passed | local.trx, excluding FinalRemediationRulesTests |
| New Docker-independent cases | 24 passed | FinalRemediationRulesTests in local.trx |
| Total Docker-independent cases | **536 passed, 0 failed, 0 skipped** | tests.log, local.trx |
| EF model consistency | No pending changes | model.log |
| New migration SQL | Generated successfully, exit 0 | migration.sql, sql-generation.log (empty success output) |
| Integration discovery | **111 cases discovered** | integration-discovery.log |
| New integration cases | **14**: 12 in FinalRemediationIntegrationTests, 2 added quality cases | Discovery output and source declarations |
| Existing integration regression extended | 1 finance lifecycle scenario | Phase21FinanceTests |
| Integration cases actually executed | **0** | Container restriction respected |
| PostgreSQL migration actually applied | **Not executed** | Separate authorized gate required |
| Real MinIO verification | **Not executed** | The prepared integration fixture substitutes object storage |

The first local run exposed the intentionally added route missing from the explicit approved-route snapshot. That list was extended with the actual new route; the final complete run passed. No existing test was removed, disabled, skipped or relaxed to suppress a defect.

Commands:

~~~powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration' --logger 'trx;LogFileName=local.trx' --results-directory verification/final-remediation
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
dotnet ef migrations script 20261105110000_Phase24QualityEvidenceCompletion 20261106100000_FinalEngineeringRemediation --project Academix.Api --configuration Release --no-build --output verification/final-remediation/migration.sql
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --list-tests --filter 'FullyQualifiedName~Academix.Tests.Integration'
~~~

Test files changed/added:

- `Academix.Tests/Unit/FinalRemediationRulesTests.cs` (new).
- `Academix.Tests/Unit/ControllerRouteTests.cs` (one explicit route added).
- `Academix.Tests/Integration/FinalRemediationIntegrationTests.cs` (new).
- `Academix.Tests/Integration/Phase21FinanceTests.cs` (stronger lifecycle/replay assertions).
- `Academix.Tests/Integration/Phase24QualityCompletionIntegrationTests.cs` (two new retry/concurrency scenarios).

## E. Remaining risks and verification gate

No known failing local test remains. The requested source repairs are implemented; their PostgreSQL-dependent guarantees remain pending, not certified.

Required next gate in a separately authorized environment:

1. Apply the complete migration chain to a fresh database and to representative populated Phase 24 data. Inspect the legacy reconciliation workload and retained historical evidence.
2. Execute all integration tests, prioritizing the new academic/document/rollback, financial lifecycle, campus race, quality serialization and revocation-wait cases.
3. Verify actual SQL constraints/triggers, query translation, lock ordering, retry behavior and ambiguous commit outcomes. Discovery and compilation prove none of these runtime properties.
4. Exercise real MinIO interruption/cleanup behavior and durable keyring restore. Storage-provider substitution is deliberately not described as MinIO verification.
5. Review performance on institutional datasets: provenance checks use batched source reads per section; no scale/load benchmark was performed. Institution coordination deliberately favors correctness over write parallelism.

Already published quality snapshots and original official documents remain historical artifacts. Current grade-dependent decisions require reconciliation, but the repair does not automatically replace official grades, manufacture credits, disburse funds, perform bank operations or rewrite historical evidence.

No Docker, PostgreSQL container, MinIO, deployment, installer or database-deletion work was performed. No production-readiness or integration-pass claim is made.

## F. Final decision

**REMEDIATION_CODE_COMPLETE_VERIFICATION_PENDING**

All six requested finding paths have source corrections and regression scenarios. Restore, warning-as-error Release compilation, all 536 local tests, model consistency, migration SQL generation and integration discovery passed. PostgreSQL/application and real-storage verification remain the explicit outstanding gate.
