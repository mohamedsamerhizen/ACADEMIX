# ACADEMIX — Final Deep Engineering Audit

> **Remediation status:** This audit records the pre-repair source. See [FINAL_RELEASE_CLOSURE.md](../FINAL_RELEASE_CLOSURE.md) for implemented repairs, completed runtime verification and the current publication decision.

Repository: `C:\Users\lenovo\Desktop\ACADEMIX\phase24-final` exclusively. Date: 2026-10-31, Asia/Baghdad.

## 1. Executive Engineering Verdict

**Publication should wait for focused remediation.** This deeper review identified three HIGH findings: missing tenant/resource authorization on three campus reads, transition-recognized credits surviving failure of their underlying local achievement, and residual authorization-before-lock patterns in consequential legacy mutations. A MEDIUM finding concerns unmarked stale academic read responses.

This report supersedes `verification/FINAL_RELEASE_ACCEPTANCE.md`. The earlier targeted review did not establish these additional paths. Passing tests and a clean build do not negate them. The architecture and much of the implementation demonstrate substantial engineering competence; no framework rewrite or new feature phase is justified.

This was an audit. Application/test source, dependencies, schemas and migrations were not changed. Isolated audit probes/evidence were added here. Brief publication-status notices identify the superseded documents. No publishing, deployment or destructive infrastructure action occurred.

## 2. Audit Scope and Coverage

### Architecture reconstructed

`Academix.slnx` contains `Academix.Api` and `Academix.Tests`. The .NET 10 MVC modular monolith follows controllers → scoped services/policies → shared Identity-derived `AcademixDbContext`/PostgreSQL and private MinIO. Entities use `Academix.Api.Domain.*` namespaces inside `Entities`; no separate Domain project exists. `Extensions/ServiceCollectionExtensions.cs` and `WebApplicationExtensions.cs` compose DI, JWT, MVC, middleware and workers.

PostgreSQL owns identities, academic/financial records, document metadata, immutable evidence and durable delivery. MinIO owns bytes; Data Protection protects document keys and recoverable applicant invitation capabilities. Background workers expire attempts and deliver outbox work. Transcripts, standing, graduation evidence, notifications and quality snapshots have distinct derived/historical contracts.

### Completed coverage matrix

Pass one traced critical operations through endpoint → authentication → object authorization → validation → service/rules → EF/constraints → transaction/side effects → response. Pass two challenged those conclusions against deeper checks, alternate paths, FKs/triggers, lock order, synchronization, tests and intentional historical behavior. References below are repository-relative; service names refer to `Academix.Api/Services`.

| Module / authoritative data | Pass-one critical implementation | Pass-two result and coverage boundary |
|---|---|---|
| Identity: users/roles/refresh families | Auth/Users controllers; `Identity/AuthService`, `IdentitySecurity`, input policy, JWT pipeline, Identity mappings | Rotation/replay, account state and stamp/role changes inspected. Entry JWT validation does not authorize a URL tenant or cancel an executing service: DA-01/03. |
| Hierarchy: institutions/terms/programs/courses | Structure/Platform controllers; `Academic/StructureService`, `PlatformGovernanceService`, academic alternate keys/version tokens | Global-only platform authority and term closure/activation coordination checked against enrollment; no new confirmed hierarchy defect. |
| Enrollment: sections/registrations/staff | Enrollments/Sections/Records/Governance controllers; individual admission, policy eligibility, batch commit, campus integrity | Capacity, active term, published-grade checks and shared locks inspected. Recognition inputs expose DA-02. |
| Admissions/imports: applications/memberships/maps | Applicant/Imports/Lifecycle controllers; `ApplicantService`, `InstitutionalImportService.ApplyRow`, legacy admission decision | Invitation capability is intentional. Import checkpoint/writes/outbox share a transaction. Legacy admission authorizes before its wait: DA-03. |
| Curriculum/credits/exceptions | Curriculum/Registrar controllers; assign, transition validate/review/apply, independent exceptions, `DegreeAuditService/Engine` | Frozen plans/review/deduplication do not revalidate recognition after source correction: DA-02. Failed-source substitution control rejects. |
| LMS/files | Learning controllers; `LearningOperations`, content policy, assignment locks, cleanup | Ownership/deadline/byte checks traced. Durable cleanup inspected; real storage execution is preserved evidence. |
| Group learning/discussions | Advanced controllers; `AdvancedLearningAccess`, team/rubric/grading services and source invalidation | Membership freezes after first draft, defeating a suspected post-grade membership drift; score changes propagate invalidation. |
| Examinations | Builder/Taking/Grading/Results controllers; question publication, attempt ownership/sequence/deadlines, grade/appeal changes | Student projections omit keys; answer writes reauthorize after locks. Legacy builder publication retains a DA-03-type ordering pattern. |
| Gradebook/transcripts/standing | Gradebook/Records controllers; publish/correct/reconcile, freshness helpers, sync and standing | Repaired source flags/overrides/rollback are meaningful. Recognition and ordinary read projections add DA-02/04. |
| Advising/progression/graduation | Assigned-advisor policy, study-plan review, degree audit, department/registrar stages, issuance | Advisory plans do not create credits/enrollments. Graduation reevaluates degree evidence but inherits DA-02. |
| Official documents/keys/PDFs | Document/Registrar controllers; snapshot/issue/verify/download, key lifecycle, cleanup | Proof/source/credential/digest/ownership defenses traced; original artifacts remain historical. |
| Finance | Finance controller; access, catalog, billing, payments, assistance, queries, reports/debt, monetary FKs/unique keys/deferred guards | Duplicate/currency/refund/allocation/aid/reversal/clearance paths inspected. No new confirmed monetary defect; captured contracts/cutoffs are intentional. |
| Campus | Campus/Teaching controllers; facilities/reservations, scheduling policies and shared integrity | Write-side locks do not authorize reads: DA-01. A service-wide ignored-principal scan found the same three candidates. |
| Student services | StudentServices controller; case/queue/assignment/attachment visibility helpers | Public timelines exclude private notes; student ownership and active queue grants checked. Support-case closure creates no academic exception. |
| Academic requests | Registrar/Academic controllers; typed review, decision, `RequestFulfillment`, policy engine | Fulfillment rechecks references/state/independent actor, waives only the approved rule. Legacy decision also has pre-lock authority ordering. |
| Quality | Quality controllers; live grade/rubric evidence, publish/attach/seal, history/trends, immutable guards | New publication checks current evidence; old frozen evidence intentionally remains historical. Retry and role-revocation assertions inspected. |
| Communications/analytics/workspaces | Thread membership, recipient/tenant filters, dispatch authority, export/workspace scope helpers | Scheduled delivery checks current author; private inboxes/exports scoped in inspected paths. No new confirmed leak there. |
| Database/background/storage reliability | Retry scopes, outbox claim/deliver/receipt/backoff, bounded worker, cleanup locks, final migrations | Durable mechanisms inspected; no universal exactly-once or exhaustive lock-order claim. No full migration line-by-line reaudit. |

This is a whole-system functional inventory with substantive selected high-risk traces, not proof of every endpoint/line. No coverage percentage is invented. Both investigation passes are complete; unavailable fresh Docker execution is recorded explicitly. The subsequent findings distinguish source conclusions from the exact scope of executed probes.

## 3. Confirmed Critical and High Findings

No CRITICAL finding was established. Three HIGH findings remain unresolved.

### DA-01 — Campus resource reads omit institution/object authorization

- **Severity:** HIGH. **Evidence confidence:** CONFIRMED FROM SOURCE, with executed SQL-interception evidence of the missing authorization path; live database disclosure was not executed.
- **Exact locations:** `Academix.Api/Controllers/CampusFacilitiesController.cs:37`, `:43`, `:48`; `Academix.Api/Services/AcademicOperations/CampusFacilitiesService.cs:109` (`RoomOccupancy`), `:129` (`InstitutionCalendar`), `:147` (`AvailableAt`). Existing neighboring read guard: `:15`.
- **Execution path:** valid JWT → general controller `[Authorize]` → one of these methods → query using caller-supplied institution/room → DTO response. None invokes `ReadAccess`, `WriteAccess`, `IdentitySecurity` or another resource check. Its principal parameter is unused.
- **Root cause:** query tenant predicates select the requested tenant without establishing that the caller belongs to or administers it. JWT checks the account's own institution, not the URL tenant. No global EF tenant filter, PostgreSQL RLS policy or MVC authorization filter supplies the missing defense.
- **Realistic trigger:** an authenticated Institution A student/staff/admin supplies a known B institution ID to the calendar/availability route, or a known B room ID to occupancy. Calendar results can reveal additional room/section IDs. Same-institution students also reach the administrator-described calendar.
- **Impact:** unauthorized operational metadata: sessions/reservations, sections, rooms, times, reservation kinds/statuses, building identifiers and capacity. No claim of disclosed grades/names or unauthorized writes.
- **Existing mitigations:** authentication, UUIDs, bounded intervals/rows and limited DTOs; neighboring catalog/reservation routes have separate guards. These do not authorize the affected routes.
- **Minimal correction:** invoke existing appropriate authority before querying: administrator authority for administrative occupancy/calendar, and the established institution-read policy for availability, consistent with the intended access contract.
- **Regression recommendation:** populated A/B institutions; HTTP student/staff/admin A→B requests to all three routes reject without returning rows. Same-tenant nonadmin cases follow intended policy; B admin/global cases succeed.
- **Status:** unmodified; remediation required. [authorization-probe.log](authorization-probe.log) and [NoDatabaseAuthorizationProbes.cs](probes/NoDatabaseAuthorizationProbes.cs) show all three reach resource SQL with an empty principal and absent authorization collaborator. **Database I/O was suppressed.** The controller still blocks anonymous HTTP requests; the exploit requires an authenticated user.

### DA-02 — Transition recognition keeps credit after its local achievement fails

- **Severity:** HIGH. **Evidence confidence:** CONFIRMED FROM SOURCE for the full consumer path; CONFIRMED BY EXECUTION for the pure engine discrepancy/controls. Full PostgreSQL workflow remains unexecuted.
- **Exact locations:** `Academix.Api/Services/Curriculum/DegreeAuditService.cs:42`, `DegreeAuditEngine.cs:54`; `Academix.Api/Services/Records/RecordsService.cs:223`; transition validation/creation in `CurriculumTransitions.cs:60`, `:73`, `:112`. Correction/sync: `Assessment/GradebookService.cs:435`, `Records/RecordsService.cs:294`. Guards: `Data/Configurations/CompletionConfiguration.cs:61`, `Data/Migrations/20261031190400_Phase19TenantAndArtifactGuards.cs:144`.
- **Execution path:** passed local A → independently reviewed/applied transition maps A→required B → authorized official correction makes A fail → transcript sync matches current gradebook → academic freshness accepts matched records → degree loader still emits B recognition; prerequisite loader still unions B into equivalencies → engine grants B credit without qualifying its local source.
- **Root cause:** original source course is retained for deduplication but not revalidated when recognition is consumed. Freshness compares assessment/gradebook/transcript, not recognition dependencies. Transition validation runs before application, not after later corrections.
- **Realistic trigger:** retrospective grade correction after a valid transition. An otherwise complete curriculum with a permitted zero minimum GPA can remain eligible; other qualifying GPA-bearing courses can also leave a nonzero threshold satisfied. Not every source failure necessarily yields graduation eligibility.
- **Impact:** unsupported earned credits/prerequisites and potentially false degree/graduation decisions. Graduation review/issuance reevaluates this engine and inherits the input defect. Full credential issuance was not executed here.
- **Existing mitigations:** independent review, source deduplication, active membership, no active registrations at transition, freshness checks and immutable recognition rows. None disqualifies the corrected local source after sync. Immutability preserves history, not current qualification; legitimate external transfer provenance must be preserved.
- **Minimal correction:** shared current recognition qualification/provenance checks for degree and prerequisite consumers; revalidate local source achievement or require explicit reassessment after correction. Preserve immutable history and external-transfer recognition. Do not simply require a local grade for all external credits or delete rows.
- **Regression recommendation:** real passed A→B transition, official failure correction, sync; B must cease qualifying prerequisites/degree/reassessment/new issuance until resolved. Include passed-source, legitimate external transfer, chained transition and unaffected-tenant controls.
- **Status:** unmodified; remediation required. [engine-probe.log](engine-probe.log) reports **Eligible / GraduationReady=true / 3 earned credits** with the failed local source and recognition. Without recognition, or using an approved substitution from the same failed source, it reports **NotEligible**. This executes the actual engine, not an end-to-end database reproduction.

### DA-03 — Legacy privileged mutations retain pre-wait authority

- **Severity:** HIGH. **Evidence confidence:** CONFIRMED FROM SOURCE. The PostgreSQL race probe compiled but did not execute; no runtime race success is claimed.
- **Exact locations:** `Academix.Api/Services/Lifecycle/LifecycleService.cs:93` authorizes before admission locks at `:103`/`:106`; transfer decision repeats at `:196`/`:205`. `Curriculum/CurriculumService.cs:200` authorizes before student lock `:204`. Other inspected instances: `Examinations/ExamBuilderService.cs:266` before `LockExam`; `AcademicOperations/RegistrarOperationsService.cs`, `DecideCore`, before request lock. This is not a claim that every mutation is affected.
- **Execution path:** currently authorized request → service captures actor/authority → waits on resource row → another committed operation disables/revokes actor → resource unlocks → original service persists using captured actor without a fresh check.
- **Root cause:** current-role authorization occurs before a potentially long synchronization wait. Resource locks/version checks protect resource state, not current actor authority. Entry JWT/stamp validation cannot cancel an executing service; reviewer FKs establish existence, not active authority.
- **Realistic trigger:** admission approval waits on the student's user row while a different authorized administrator disables the approving actor via `AuthService.ChangeActiveAsync`. The locked student and disabled actor are different rows. After release, admission can add membership and record the captured reviewer. Transfer-credit approval has comparable academic impact.
- **Impact:** consequential pending actions can complete after explicit revocation during the wait. This is an in-flight defect, not a route for a never-authorized user to submit a fresh privileged request.
- **Existing mitigations:** current role at entry, independent approval, versions and row locks. Repaired campus/quality/graduation/grade-change paths recheck after coordination. The alternate `RecordsService.AdmitWithinTransaction` checks after its user lock; legacy admission does not call it.
- **Minimal correction:** revalidate account/membership/required authority after acquiring blocking coordination/resource locks, using fresh-read helpers and consistent lock order. Address listed analogous paths; do not weaken existing R02 tests or claim one helper call proves universal safety.
- **Regression recommendation:** hold the exact resource lock, observe pending blocking through `pg_stat_activity`/`pg_blocking_pids`, revoke/disable in a committed transaction, release, require rejection and no durable membership/decision/audit/outbox effects. Test authorized controls and supported role/account/membership revocations.
- **Status:** unmodified; remediation required. [probes/Program.cs](probes/Program.cs) preserves a proposed observed-wait admission probe. [probe-runtime.log](probe-runtime.log) failed during fixture construction before database creation, not after executing the scenario.

## 4. Medium and Low Findings

### DA-04 — Stale academic read projections have no freshness indication

- **Severity:** MEDIUM. **Evidence confidence:** CONFIRMED FROM SOURCE; no fresh PostgreSQL reproduction claimed.
- **Exact locations:** `Academix.Api/Services/Records/RecordsService.cs:344` (`Transcript`, guard `:347`), `:397` (`Standing`); `Academix.Api/DTOs/Records/RecordsContracts.cs:14`. Compare `Infrastructure/AcademicFreshness.cs:20` and standing source-time checks in `Curriculum/DegreeAuditService.cs`.
- **Path/realistic trigger:** official correction/reconciliation changes 80/pass→40/fail; before transcript sync, `Transcript` checks only assessment freshness and returns old 80/pass data. After sync, `Standing` can return previously calculated GoodStanding until recalculation, because matching transcript/gradebook does not make standing current.
- **Root cause:** transcript reads omit transcript-versus-gradebook freshness; standing reads omit calculation/source timestamp validation. DTOs expose neither stale status nor a historical-as-of designation. Transcript `Version` is the projection version, not the current source version.
- **Impact:** authorized users/frontends receive conflicting academic responses presented as ordinary current results. This is not demonstrated persisted grade corruption or bypass of the better-guarded degree/GPA decisions.
- **Mitigations:** explicit sync/recalculation; stronger freshness in degree/GPA, scholarship standing checks and official issuance. Historical artifacts intentionally remain immutable.
- **Minimal correction:** use the existing current-record guard for transcript reads and existing standing source-time checks, or explicitly contract/mark historical status. Do not rewrite historical documents automatically.
- **Regression recommendation:** correction before sync rejects/marks transcript; sync restores transcript but leaves old standing rejected/marked; recalculation restores standing; unaffected student/tenant controls.
- **Status:** unmodified. No additional meaningful LOW defect established; operational limitations appear below.

## 5. Security Assessment

JWT checks issuer/audience/signature/lifetime, zero clock skew, active account/security stamp and institution claim/status. Keys are required and length-checked. Bounded login inputs and Identity lockout are present. Refresh rotation stores hashes, locks user/token rows, keeps family expiry and revokes descendants on replay. Role/activation changes transact stamp and refresh invalidation. Logout revokes the supplied refresh family, not every extant access token; access-token lifetime remains relevant.

Resource helpers generally reload persisted roles and bind teachers to current role-compatible assignments. Finance additionally requires institution-specific permission flags and prevents own-account posting. Applicant invitations are high-entropy hashed/protected expiring capabilities with independent review/provisioning controls. Their anonymous capability endpoints are intentional; this does not justify DA-01's unrestricted tenant selection.

Learning/document downloads check authority before storage; uploads validate size/content/name. Case timelines omit private notes; attachments enforce visibility/ownership/queue grants. Public document/receipt proofs expose limited status rather than private snapshots. New document keys are separate from JWT and Data-Protection-protected; key-list DTOs omit secrets.

Targeted SQL searches found parameterized runtime identifiers and no runtime external-input `FromSqlRaw`/`ExecuteSqlRaw` path. Migration dynamic SQL formats internal table names. No unsafe binary/polymorphic deserializer was identified. CSV export neutralizes formulas; imports bound records/mappings. Unexpected HTTP errors are redacted; deployment logs still require operational access control.

**Conclusion:** DA-01 is a confirmed tenant/object authorization vulnerability and DA-03 a source-confirmed authorization-freshness gap. No critical unauthenticated takeover, live-secret leak or confirmed monetary privilege bypass was established. JWT correctness alone does not justify a clean security conclusion.

## 6. Academic and Financial Integrity

Strengths include active-term/prerequisite/capacity checks, venue compatibility, attempt ownership/deadlines/monotonic answer sequences, frozen published questions and reasoned official corrections. Assessment changes preserve official results pending reconciliation, atomically invalidate dependents and protect manual overrides. Curriculum calculations deduplicate best attempts and source consumption; waivers do not manufacture GPA/credits. Graduation has meaningful independent stages and final reevaluation.

**DA-02 affects achievement truth after the otherwise correct correction/sync workflow.** Native grades and substitutions react to failure; transition recognition does not. DA-04 concerns intervening read projections; DA-03 concerns decisions during revocation waits.

Finance was followed through fee/policy scope and source credit mapping → draft evidence → post-time revalidation → balanced journal → correcting entries/reversal → net allocation/refund/aid/debt → clearance/notifications. `FinanceService.Write` coordinates before authority/idempotency checks and commits monetary effects, commands and audit together. Payments/refunds validate verification, available cash, reservations and unresolved refund constraints. Scholarship repricing reclaims aid before deterministic replacement grants; charge/funding limits and active program-code uniqueness prevent duplicate benefits across revisions. Captured award eligibility is approval evidence, not a promise of automatic ongoing revocation.

Deferred PostgreSQL guards reconcile source documents, journal legs, account balances, reservations and matching reversals. The latest repair replaces ambiguous `old` with `original_journal`, preserving checks. No new confirmed monetary defect was found. Fresh database concurrency/fault execution was unavailable; preserved finance runs remain the infrastructure evidence.

## 7. Cross-Module Consistency

| Relationship | Authority → derivation; source modifiers | Transaction/failure/adversarial assessment |
|---|---|---|
| Enrollment ↔ Finance | Active registrations/published curriculum → pricing evidence; enrollment/withdrawal/transition versus assess/post/adjust | Shared institution coordination and posting-time quote comparison reject changed drafts. Posted invoices are historical liabilities changed by correcting entries, not silently repriced on withdrawal. Live debt governs holds. Policy/invoice cutoffs intentionally grandfather earlier document/graduation requests. |
| Enrollment ↔ Campus | Registrations, capacity, teachers → room/slot/session/exam compatibility | Individual/batch enrollment, capacity and staff changes invoke shared checks under coordination with reservations. Tests check seats and tenant-independent codes. DA-01 is distinct read disclosure, not a failure of repaired write-side capacity enforcement. |
| Exams/LMS ↔ Gradebooks | Published reports/graded submissions → captured source evidence | Grading/appeal changes share coordination and `AssessmentFreshness.Changed`; flags/audit/invalidation commit or roll back with source. Frozen team membership defeats a suspected ownership-change route. |
| Gradebooks ↔ Transcripts | Versioned official results → synchronized entries | Correction/reconcile/sync coordinate and validate sources. Explicit synchronization does not make unmarked old reads current: DA-04. Sync does not cure recognition: DA-02. |
| Transcripts ↔ Graduation | Fresh transcript/plan/standing/exceptions/recognition → eligibility and credential evidence | Repeatable-read degree evaluation and transaction-aware final checks protect native evidence. Source invalidation exists. Recognition qualification remains uncovered; graduation inherits DA-02. |
| Graduation ↔ Documents | Eligible/current issued credential → immutable snapshot/PDF/proof | F01 includes graduation-document invalidation and supporting credential verification. Retained bytes/history are intentional. Credential reassessment still consumes degree evidence and can inherit DA-02. |
| Student services ↔ Academic requests | Support cases remain support records; typed requests independently authorize effects | Case closure grants no academic power. Fulfillment validates tenant/section/session/policy, independent actor/state/version. Excuses require held-session attendance; exceptions waive only their specific rule. Legacy decision ordering has DA-03. |
| Quality ↔ Published grades | Current published grades/rubrics → frozen review → sealed manifest/trends | New publication checks freshness under coordination. Historical snapshots intentionally remain historical; they do not imply individual mastery/accreditation. Automatic rewriting after correction is not required. |
| Authorization ↔ Background | Durable event → kind-specific delivery/current source checks | Scheduled dispatch checks current author; transitions check current approver and source before applying; recipient scope is filtered. System expiry is a narrow state transition. These controls do not repair legacy HTTP authority order. |
| MinIO ↔ Metadata | Committed metadata references object keys; cleanup intent commits before upload | Compatible institution locks and reference checks prevent deleting committed objects; independent intent survives rollback. Leases/retry make recovery durable within exercised bounds. Controlled storage restart is not exhaustive crash recovery. |

## 8. Database, Transactions and Reliability

The 39-migration chain/current snapshot remains intact through `20261106110000_RuntimeFinanceTriggerCorrection`. Preserved fresh/pre-Phase-19/populated upgrades passed. Earlier explicitly authorized historical corrections remain unchanged; this audit did not edit/reapply them.

Composite tenant/account/currency FKs, alternate keys, uniqueness, version tokens and targeted tenant/immutability triggers provide material integrity. They do not authorize a requester. Recognition guards preserve ownership/positive credit/history without checking current grades; reviewer FKs do not enforce active authority. Those distinctions were checked in the second pass.

`RetryingServiceWork` creates fresh scopes per execution-strategy attempt. Most coordinated writes take institution before narrower resources and commit derived invalidation with source. Legacy variations create practical review obligations, including DA-03. No new demonstrated deadlock was found; existing successful cases do not exhaust every lock combination.

Outbox uses tenant/stable event identity, unique keys, `FOR UPDATE SKIP LOCKED`, leases/tokens, durable retry/backoff and receipts. Database notification effects and delivery acknowledgment share a transaction. Kind-specific dispatch/transition/cleanup replay handling covers some effects before acknowledgment. This is deliberate recovery, not universal exactly-once assurance under ambiguous commits. Workers bound/rotate work, isolate faults and honor cancellation.

## 9. Architecture Assessment

The two-project modular monolith is professionally defensible. Controllers largely delegate; authorization/policies are named; transactions are explicit; PostgreSQL enforces real invariants. Coupling between records, finance, campus and graduation reflects implemented business dependencies. A framework/microservice rewrite would not solve these findings.

Practical weaknesses are inconsistent authority placement and duplicated freshness definitions. DA-01 shows that a principal parameter is not enforcement; DA-02/04 show individually reasonable guards missing dependency edges. Dense partial services increase audit effort. Reuse existing policies and focused regressions rather than expand the architecture.

## 10. Previous Remediation Assessment

| Repair | Independent source/test assessment |
|---|---|
| F01 graduation-document invalidation | `AcademicFreshness.Invalidate` includes graduation documents/credentials; verification checks supporting credential. Preserved reissuance/bytes/unrelated-tenant assertions support the repair. No new F01-specific regression established. |
| F02 assessment reconciliation | Source evidence, flags, versions, retain/recalculate, manual overrides and atomic invalidation exist. Tests check downstream rejection and rollback. Original scope supported; DA-02 adds a recognition consumer edge and DA-04 a projection gap. |
| F03 scholarship event identity | Notification identity includes command transition, permitting distinct approval/revocation events. Tests assert distinct persisted keys and no replayed journals. Supported. |
| F04 campus/enrollment | Individual/batch enrollment, capacity and instructor changes invoke shared integrity under coordination; tests cover seats and tenant independence. Supported; DA-01 concerns separate reads. |
| R01 quality retry compatibility | Attach/seal/publish use fresh-scope retries, coordination and internal checks. Forced serialization-retry and single durable outcome assertions support repair. |
| R02 authority after waits | The five exercised protected mutations revalidate after coordination; fresh user reads avoid tracked pre-wait authority. Supported for those paths. DA-03 identifies other legacy paths; no claim that original five tests failed. |
| LMS cleanup | Durable independent intent, canonical keys, compatible locks and referenced-object checks exist. Preserved PostgreSQL rollback/real MinIO recovery support exercised behavior. Client retries/crash boundaries remain limited. |
| Finance trigger correction | Forward migration removes alias ambiguity while retaining source/journal/reversal checks. Preserved finance/upgrade execution supports it. No historical migration modified here. |

## 11. Test Quality Assessment

Integration fixtures start real PostgreSQL and apply migrations. Most substitute `TestArtifactStore`; separate real MinIO orchestration uses `PrivateFileStore`, isolated credentials and disposable infrastructure. Representative assertions verify persisted monetary totals/unique journals/receipts, multi-record rollback, retained PDF bytes, cross-tenant rejection and current-role revocation. Historical failed artifacts remain preserved.

Concurrency quality varies: the revocation-wait test proves blocking using `pg_blocking_pids`; quality retry testing injects a real serialization failure. Some `Task.WhenAll` cases assert valid final outcomes without proving every intended overlap/interleaving. They add confidence, not exhaustive race coverage. Route/attribute/source-shape tests are structural: none located exercised object authorization on the three DA-01 reads. Recognition tests cover deduplication/application/native changes, not failure of the original achievement after transition. The reconciliation scenario syncs promptly and does not prove every intervening read current.

### Execution/evidence ledger

| Evidence | Actual supported result |
|---|---|
| Earlier same-day unchanged-source baseline | `verification/release-acceptance/restore.log`, `build.log`, `local.trx`, `model.log`: restore passed; Release `-warnaserror` 0 warnings/errors; 536 independent tests passed; no pending EF model changes. Reused, not rerun for this audit. |
| Source correspondence | [audit-checks.json](audit-checks.json): 425 API/test source/project fingerprints match the earlier acceptance snapshot; no changes to those files. |
| Preserved 2026-10-30 PostgreSQL | `verification/final-integration/postgres-final.trx`: 112 passed, 0 failed, comprising 111 original cases plus populated upgrade. |
| Preserved real MinIO/infrastructure | `infrastructure-final.trx`: 2 passed, 0 failed; one distinct MinIO orchestration plus repeated upgrade. Unique preserved total remains 649; no double counting. |
| Fresh isolated probe build | [probe-build.log](probe-build.log): probe and referenced API/tests compiled Release with warnings as errors, 0 warnings/errors. Probes are outside the solution/existing projects. |
| Fresh degree-engine probe | [engine-probe.log](engine-probe.log): failed source with recognition yields Eligible/3 credits; controls without recognition and with failed-source substitution yield NotEligible. No database execution. |
| Fresh campus-path probe | [authorization-probe.log](authorization-probe.log): all three methods reach resource SQL without authorization. Connection/command I/O was intercepted; no live database disclosure claimed. |
| Attempted PostgreSQL probes | [probe-runtime.log](probe-runtime.log): Docker endpoint timeout during fixture construction before a database was created. Full transition/correction and admission-wait probes did not execute. Docker Desktop startup was attempted; no existing infrastructure was removed/reset. |

Current discovery from the preceding unchanged snapshot contains 649 cases; all final preserved TRX case names match. The historical infrastructure runs had no source hash manifest, so their exact bit identity cannot be proven retroactively. Current fingerprints bind this acceptance/audit snapshot. No fresh full-suite, PostgreSQL, MinIO, migration-application or Docker-health success is claimed.

Reproduce the bounded fresh checks without Docker:

```powershell
dotnet run --project verification/final-audit/probes/AuditProbes.csproj -c Release -- --engine-only
dotnet run --project verification/final-audit/probes/AuditProbes.csproj -c Release -- --authorization-only
```

Default probe mode requires working Docker and attempts isolated PostgreSQL workflows. It compiled but is unverified at runtime. These probes do not replace permanent HTTP/PostgreSQL regressions recommended above.

## 12. Publication Readiness

README/configuration instructions describe the real stack/modules, explicit design-time connection override, migrations/bootstrap, persistent document keyring, PDF license selection and local Compose Development behavior. OpenAPI is Development-only. Earlier Compose syntax validation passed; full Compose startup was not newly verified. Ignore files cover secrets/keyrings, generated outputs and caches; a raw folder ZIP still includes local outputs and is not reviewed source publication.

A repeated scoped scan found zero high-confidence private-key/provider-token/JWT-pattern hits; [secret-pattern-check.json](secret-pattern-check.json) records the actual file count/pattern results. Earlier credential-like matches were test-only or explicit configuration/design-time placeholders. No real institutional dataset was identified. This is bounded pattern/manual review, not proof against arbitrary secret formats. No `.git` history/remote exists here to attest previously committed secrets or remote contents.

Minimum publication work: remediate DA-01–03, resolve or explicitly contract DA-04, add/run meaningful regressions, execute required gates and update acceptance claims to that repaired snapshot. Earlier positive acceptance is historical and does not override this report. Brief superseded-status notices were added to README and prior acceptance for that reason; no cosmetic rewrite occurred.

## 13. Remaining Limitations

**Publication-blocking:** the three unresolved HIGH findings. They are source defects with concrete paths, not demands for optional enterprise infrastructure. DA-04 is a meaningful projection issue to address alongside focused remediation; it is not independently a critical degree-decision bypass.

**Reasonably postponable:** institutional-scale performance/soak tests; client-upload deduplication; exhaustive crash/ambiguous-commit exploration; richer API examples; dense-service formatting/refactoring. Independent client upload calls intentionally create distinct artifacts. No new feature phase is proposed.

**Before real institutional production, not portfolio prerequisites by themselves:** deployment-specific TLS/proxy/secrets, least-privileged storage credentials, protected/restorable Data Protection keys, coordinated database/object/keyring backup/recovery, real-data upgrade/reconciliation review and operating-scale validation. Mutable image tags and Development defaults remain documented. HMAC is not certificate signing; finance records are not bank execution; quality packs are not accreditation.

### Second-pass disposition

| Suspicion challenged | Adversarial outcome |
|---|---|
| Database/tenant predicates replace request authorization | Rejected: they select the requested resource without binding its caller; DA-01. |
| Synchronizing transcripts cures every academic dependency | Rejected: recognition consumers remain unconditional; engine controls confirm discrepancy, DA-02. |
| Strong JWT/stamp checks eliminate in-flight revocation waits | Rejected from service ordering in DA-03; database race execution remains outstanding. |
| Frozen quality snapshots should automatically rewrite after correction | Rejected as a finding: historical evidence is intentional; new publication checks current sources. |
| Withdrawal must silently reprice posted invoices / new policies block old requests | Rejected as findings: correcting liabilities and cutoff grandfathering are documented contracts. |
| Team membership can change graded credit recipients | Rejected: team locks and post-first-draft freeze defend the inspected path. |
| Financial replay bypasses revoked authority | Rejected in the inspected path: current authorization precedes replay under coordination, with direct regression assertions. |
| Cleanup necessarily deletes committed objects after a prior rollback | Rejected within exercised scope: durable intent, reference checks and coordination exist; preserved real storage evidence supports them. Abrupt crash/disaster limits remain. |
| Original R02 tests prove every legacy mutation safe | Rejected: the five repaired paths are supported, while DA-03 traces other operations. |

## 14. Final Engineering Verdict

1. **Professionally engineered?** Substantially yes in architecture, persistence, domain rules and recovery work, with consequential enforcement inconsistencies remaining.
2. **Confirmed serious security vulnerabilities?** Yes: DA-01 tenant/object authorization omission and DA-03 source-confirmed in-flight authority gaps. No critical unauthenticated takeover established.
3. **Confirmed critical business defects?** No CRITICAL-severity finding claimed. DA-02 is HIGH academic integrity; DA-04 is MEDIUM stale presentation.
4. **Database coherent?** Yes. Explicit mappings, tenant keys, immutable history and financial guards are coherent but cannot replace authorization/dependency freshness.
5. **Previous repairs real within tested scope?** Yes for original exercised F01–04/R01–02/storage/finance cases. Additional paths limit universal claims.
6. **Major structural deficiencies?** No rewrite-level deficiency established. Policy-placement/freshness inconsistencies need focused correction.
7. **Strong portfolio suitability?** Potentially yes after bounded repairs and honest validation; current publication approval is withheld.
8. **Exactly what before publication?** Correct DA-01–03; resolve/contract DA-04; add negative/transition/wait regressions; run fresh restore, warnings-as-errors Release build, local tests and affected PostgreSQL gates (full integration if shared behavior changes), model check and persistence verification if applicable; align acceptance with that snapshot.
9. **What can wait?** Nonblocking performance, exhaustive fault exploration, convenience/refactoring and separately required production operations described above.
10. **Realistic maturity?** Advanced, broadly implemented portfolio backend with meaningful infrastructure evidence, but not a closed release candidate or certified live institutional deployment.

Both substantive passes and the functional/high-risk coverage matrix are complete. Environment-dependent reproduction limits are explicit. Findings await focused remediation authorization; no consequential repair was silently applied.

## 15. FINAL DECISION

**PUBLICATION_BLOCKED**

Unresolved material authorization and academic-integrity defects prevent a defensible current approval. This rests on traced missing defenses and bounded executed probes, not on the lack of a fresh full infrastructure run or a demand for enterprise features. Remediation can remain within the existing .NET/PostgreSQL modular monolith; no new development phase is recommended.
