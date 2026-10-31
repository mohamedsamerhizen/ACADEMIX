# Phase 19 remediation verification

2026-10-30, Asia/Baghdad. Verdict: **PHASE19_CODE_COMPLETE_VERIFICATION_PENDING**.

Commands target `C:\Users\lenovo\Documents\ChatGPT\ACADEMIX\phase19-work`. Solution: `Academix.slnx`; tests: `Academix.Tests/Academix.Tests.csproj`. SDK 10.0.103, target .NET10. The earlier root implementation and installer-test copies were not changed.

## Final executed results

| Check | Actual outcome | Exit code | Evidence |
|---|---|---|---|
| Restore | Projects up to date | 0 | `evidence/remediation-restore.log` |
| Release build, warnings as errors | Success; 0 warnings, 0 errors | 0 | `evidence/remediation-build.log` |
| Docker-independent tests | 277 passed, 0 failed, 0 skipped; total 277 | 0 | `evidence/remediation-local.log`, `.trx` |
| Full suite including integration | 277 passed, 74 failed, 0 skipped; total 351 | 1 | `evidence/remediation-full.log`, `.trx` |
| EF model/snapshot check | No pending model changes | 0 | `evidence/remediation-model.log` |
| SQL generation | Full chain through 20261031190700 generated | 0 | `evidence/phase19-migrations.sql`; command emits no console output |
| Docker probe | Engine pipe unavailable; Testcontainers cannot connect | 1 | Environment probe and full-test errors |
| PostgreSQL clean chain / upgrade | Cases added, but database creation did not execute | Unverified | Full TRX |
| Actual MinIO / container persistence | Not executed | Unverified | No MinIO verification claim |

All 74 failed results contain Docker/Testcontainers initialization failures; integration test bodies did not execute. Failures remain failures, not skips or passes. The local pass does not establish PostgreSQL reliability. Earlier 245/275/276 counts and old evidence are historical.

The sandboxed testhost stalled and was interrupted; completed test runs used approved execution outside the sandbox. The initial default parallel solution build returned exit1 without compiler diagnostics; the serial build succeeded. A new migration test initially lacked a namespace import; it was corrected before the final successful build and reruns. No test was disabled or assertion weakened.

## Reproduction commands

```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration' --logger 'trx;LogFileName=remediation-local.trx' --results-directory docs/phase19/evidence
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=remediation-full.trx' --results-directory docs/phase19/evidence
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project Academix.Api --no-build --configuration Release
dotnet ef migrations script 0 20261031190700_Phase19ArtifactContentSignature --project Academix.Api --no-build --configuration Release --output docs/phase19/evidence/phase19-migrations.sql
```

## New tests

| Test | Scenario | Execution |
|---|---|---|
| `CancelledTickPropagatesCancellationBeforeResolvingDatabaseWork` | Cancellation propagates before resolving database work | Passed |
| `LostDataProtectionKeyringFailsClosedWithAnExplicitConflict` | Another keyring cannot decrypt the signing secret; explicit409 | Passed |
| `WorkerIsolatesPoisonAttemptsDeliversOutboxAndRetriesWithoutDuplicateAudits` (2 cases) | 3 attempts/1 persistent failure and 103 attempts/100 failures. A real audit trigger rejects selected finalizations; other attempts and Outbox progress. Failed transactions retain version1; retries produce one audit/version increment per attempt | Docker setup failed; bodies not run |
| `PdfDatabaseRollbackLeavesDurableCleanupThatRetriesAndPreservesCommittedReissuance` | PostgreSQL rejects insertion after upload; cleanup survives rollback; remove failure persists Retry; recovery removes orphan; retry/duplicate issuance retains one valid PDF | Docker setup failed; body not run |
| `PdfTransientDatabaseFailureRetriesWithFreshTrackedStateAndOneIssuance` | Sequence injects exactly one PostgreSQL40001 rollback; replay yields one issuance/audit, two intents and valid PDF after reconciliation | Docker setup failed; body not run |
| `FullHistoryAppliesToPostgreSqlAndPreservesPrePhase19Data` (2 cases) | Clean isolated PostgreSQL and upgrade from Phase17 with an institution sentinel; exact final history and no pending migrations/model changes | Docker setup failed; bodies not run |

Six database-dependent cases and two local cases were added. The upgrade sentinel checks institution preservation, not a comprehensive production-data simulation. Existing completion tests cover applicants/tenant boundaries, PDF rotation/revocation/supersession/corruption, academic request effects, transitions/credits, imports/activation, concurrent Outbox receipts, expired leases and scheduled-notification replay. Their integration bodies remain unverified here. The in-memory object store cannot certify MinIO.

## Migration review

Eight Phase19 migrations follow `20261030100000_Phase17InstitutionalDocuments`, in this order:

1. `20261031190000_Phase19RegistrarPoliciesAndImports`
2. `20261031190100_Phase19RegistrarForeignKeys`
3. `20261031190200_Phase19WorkflowCompletion`
4. `20261031190300_Phase19DocumentReissuance`
5. `20261031190400_Phase19TenantAndArtifactGuards`
6. `20261031190500_Phase19ApplicantAndIssuanceInvariants`
7. `20261031190600_Phase19ImportIdentityActivation`
8. `20261031190700_Phase19ArtifactContentSignature`

Historical migrations and snapshot were preserved. Regenerated SQL starts at migration0 and includes all eight Phase19 history entries. Npgsql generates PostgreSQL JSONB/UUID syntax, restrictive tenant FKs, uniqueness/nullability/state constraints and PL/pgSQL triggers. Passing migration-operation tests inspect historical course principal-column names and absence of Up table/column/data deletion. Up replaces some constraints/FKs and removes a redundant index; those changes do not delete academic rows. Migration190500 backfills source-invalidation timestamps from correction audits.

SQL generation/model equality do not establish PostgreSQL trigger validity or execution. Fresh-chain and pre-Phase19 upgrade tests must pass before verification. An already partially upgraded Phase19 database requires separate validation: migration190700 requires artifact signatures for keyed documents and cannot derive HMAC values from encrypted secrets in SQL. Existing unsigned keyed rows can prevent that constraint from applying. Preserve those rows and integrity checks; any needed application-assisted backfill requires a reviewed dataset-specific procedure. No such deployed database was available for inspection.

## Remaining limits

Implemented/statically reviewed: worker isolation/fair pagination, durable artifact recovery, fresh-scope issuance replay, explicit key failures, existing HMAC lifecycle, tenant checks, Outbox delivery semantics and requested business workflows. Build verified and local tests executed as above.

Pending required execution: all PostgreSQL workflows/failure/concurrency tests, clean and baseline upgrade migrations, real MinIO operations and restart/container persistence. Docker Desktop launch was attempted, but engine probes remained unavailable. No application database was reset or migrated.

Operational limits: recovery requires enabled processing and available database/storage. Poison events retain bounded backoff and administrative retry/cancel controls; no new terminal dead-letter feature was added. Historical orphans without intents are not inventoried. Keyring loss requires restoring the original keyring. Installer packages were not rebuilt for this revision.

These missing execution checks prevent **PHASE19_VERIFIED**. Current source is build-verified with passing local coverage; database-dependent correctness remains pending a working Docker/PostgreSQL/MinIO environment.
