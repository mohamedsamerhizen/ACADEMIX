# ACADEMIX — Final Database, Integration & Runtime Verification

**Final decision: INTEGRATION_VERIFIED_WITH_LIMITATIONS**

Repository: `C:\Users\lenovo\Desktop\ACADEMIX\phase24-final`  
Verification date: 2026-10-30, Asia/Baghdad.

The former Docker blocker is resolved. Real PostgreSQL applied the complete migration chain, all original 111 integration scenarios passed, the added populated Phase 24 upgrade scenario passed, and separate real MinIO verification passed. Actual runtime failures were repaired before the final passing runs. The limitations below concern duplicate-request semantics and operational fault coverage; this report does not certify production readiness.

## A. Environment

| Item | Actual execution evidence |
|---|---|
| Source | Required solution, API, tests, final remediation migration and prior remediation report present in the required repository |
| .NET SDK | 10.0.103 |
| Docker | Engine 29.3.1; Linux server connectivity confirmed before and after verification; desktop-linux context |
| PostgreSQL | **18.6**, read from the running isolated upgrade database connection |
| PostgreSQL image | postgres:18-alpine; local image sha256:898c60cb390d1085bc97bdd6a1ae968757421df394b071848b52b1c09c89d6a5 |
| MinIO | **RELEASE.2026-09-22T19-25-18Z**, commit df34868a88cc8c396807e04a7e220810b321bdaa; actual container executable returned version |
| MinIO image | cgr.dev/chainguard/minio:latest; local image sha256:d88920d6a50bd0b088223b688be3b76c63e6205dbe494437f67ba1240ea12880 |
| Testcontainers | PostgreSql 4.16.0; actual container startup, connectivity, migrations and disposal exercised |
| Resource isolation | Fresh Testcontainers-owned PostgreSQL databases; separately created Testcontainers-owned MinIO server, random bucket, generated test credentials and dedicated host port |
| Registry access | Configured MinIO image was already available locally and ran successfully; no registry authentication failure or security bypass |

No Docker troubleshooting was repeated. No existing containers, volumes, databases, images or VHDX files were removed. No production or unknown database was used. Testcontainers disposed only its own disposable verification resources. MinIO stop/start targeted only the server created inside its test. No compose environment, factory reset, prune or WSL unregister was used.

## B. Migration Verification

**39 migrations applied successfully**, through:

- 20261106100000_FinalEngineeringRemediation.
- New forward repair 20261106110000_RuntimeFinanceTriggerCorrection.

| Scenario | Result | Evidence |
|---|---|---|
| Entire chain on a fresh PostgreSQL database | Passed | Phase19MigrationExecutionTests, upgrade=False; postgres-final.trx |
| Upgrade from pre-Phase-19 schema with a preserved institution sentinel | Passed | Same test, upgrade=True; postgres-final.trx |
| Populated Phase 24 upgrade | Passed | FinalMigrationUpgradeTests; postgres-final.trx and infrastructure-final.trx |
| Applied history matches assembly; no pending migrations | Passed | Assertions in fresh/upgrade tests |
| EF model consistency | Passed; no pending changes | model-final.log |
| Complete final SQL generation | Passed | full-migration-final.sql, sql-final.log |

The populated scenario first migrates to 20261105110000_Phase24QualityEvidenceCompletion, then creates only synthetic records before applying the remaining migrations. It preserved:

- Three official grades and their percentages: 80, 75 and 90.
- One historical moderation audit, including its ID, reason and percentage.
- One transcript entry and its original captured gradebook version.
- One issued graduation credential.
- Four document IDs and their original snapshot JSON.

The two external-assessment-derived grades acquired reconciliation flags and advanced from version 7 to 8. The known corrected grade retained manual-override classification. The manual-only grade remained unflagged at version 7. No historical source snapshot was fabricated. The issued credential and two affected documents were source-invalidated; the manual-only student's transcript document and the enrollment document remained unaffected.

Live PostgreSQL rejected an attempted historical audit update, a scalar grade-source evidence value, and an incomplete reconciliation command using SQLSTATE 23514. The full suite separately exercised finance integrity guards, tenant foreign keys, source invalidation rollback, uniqueness/idempotency, concurrent mutations and quality immutability. This is exercised coverage, not a claim that every possible database constraint violation was exhaustively enumerated.

Two existing migration corrections were made **only after separate explicit user authorization**:

1. Phase23ServiceDeskCompletion: supplied the intended principalColumn by name instead of accidentally assigning Id to the schema argument.
2. FinalEngineeringRemediation: changed only the malformed closing dollar delimiter from `$;` to `$$;`.

Exact comparisons against preserved before-files confirmed both changes contain only their authorized replacement. Migration IDs, trigger behavior, constraints and data operations were retained. Other historical migrations were not edited. The finance function repair is a new forward migration; it does not update or delete financial records.

## C. Integration Results

Final executed results, with overlapping focused reruns excluded from the unique total:

| Group | Executed | Passed | Failed | Skipped | Evidence |
|---|---:|---:|---:|---:|---|
| Docker-independent tests | 536 | 536 | 0 | 0 | local-final.trx, local-final.log |
| Original PostgreSQL scenarios | 111 | 111 | 0 | 0 | postgres-final.trx |
| Added populated Phase 24 upgrade | 1 | 1 | 0 | 0 | postgres-final.trx |
| Separate real MinIO orchestration | 1 | 1 | 0 | 0 | infrastructure-final.trx |
| **Unique final coverage total** | **649** | **649** | **0** | **0** | Combined groups above |

The real MinIO orchestration invokes additional existing PDF/document scenario methods with the real provider. Those subchecks are not counted again as separately discovered xUnit cases. infrastructure-final.trx contains two passing cases: the real MinIO case and a repeated populated-upgrade case; the repeated upgrade is counted once above.

Additional final checks:

- Restore passed; dependencies up to date: restore-final.log.
- Release build with -warnaserror passed: **0 warnings, 0 errors**, build-final.log.
- EF model check passed: model-final.log.
- Full migration script generation passed: sql-final.log.

Initial failures are retained rather than hidden:

| Run | Result | Interpretation |
|---|---|---|
| migrations-initial.trx | 0/2 passed | Invalid Phase 23 foreign-key schema argument |
| integration-initial.trx | 0/111 passed | Late test JWT configuration; migration setup failure |
| migrations-after-phase23.trx | 0/2 passed | Remediation function's unterminated dollar quote |
| migrations-final.trx | 3/3 passed | Both authorized migration corrections verified |
| integration-after-migrations.trx | 46/112 passed | Runtime and invalid fixture-data failures exposed |
| priority-runtime.trx | 49/52 passed | Targeted corrections validated; three remaining failures isolated |
| integration-stabilization.trx | 108/112 passed | Remaining timestamp replay and lock-test setup failures identified |
| remaining-pg-regressions.trx | 23/23 passed | Affected residual regressions passed |
| postgres-final.trx | **112/112 passed** | Final complete PostgreSQL suite |
| minio.trx | 0/1 passed | Restart changed an automatically published test port |
| minio-final.trx | 1/1 passed | Fixed dedicated port across server restart |
| infrastructure-final.trx | **2/2 passed** | Final MinIO coverage with duplicate-upload/version checks; populated upgrade repeated |

Tests added: two integration cases. Existing tests were strengthened or their invalid fixture setup corrected; no tests were deleted, skipped, disabled or given weaker expected outcomes. The existing PostgreSQL fixture continues to substitute TestArtifactStore by default; real MinIO execution explicitly selects PrivateFileStore instead.

## D. Remediation Verification

| Finding | Runtime status | Supporting evidence |
|---|---|---|
| F01 graduation-document invalidation | Runtime verified | IssuedGraduationPdfBecomesSourceChangedAndReissuanceRetainsOriginalArtifact; also executed with real MinIO |
| F02 assessment reconciliation | Runtime verified | ExamAppealRequiresReconciliationBeforeRecordsAndDegreeAuditAndPreservesManualOverride; FailureDuringSourceInvalidationRollsBackTheAssessmentAndAllDependentMarkers |
| F03 scholarship revocation | Runtime verified | PaidInvoiceScholarshipsCreditNotesRevocationAndReschedulingUseCorrectingEntries; full Phase21FinanceTests |
| F04 campus scheduling | Runtime verified | ExistingPhysicalSeatsAndTimetablesConstrainLaterIndividualAndBatchEnrollment; ConcurrentReservationAndEnrollmentCannotCommitInsufficientSeats; cross-institution room/timetable independence |
| R01 quality transaction retries | Runtime verified | ConcurrentEvidenceAttachmentsAndSealingHaveOneDurableOutcome; SerializationFailureRetriesTheWholeAttachmentWithFreshTracking |
| R02 revocation during lock waits | Runtime verified | All five WaitingProtectedMutationRevalidatesAuthorityAfterInstitutionLock cases |
| LMS recovery | Runtime verified within exercised faults | LearningCleanupIntentSurvivesRollbackAndNeverDeletesReferencedUploads; separate real MinIO rollback/outage/retry scenario |
| Finance integrity | Runtime verified within suite | All finance posting, correction, reversal, scholarship, rollback, authorization, duplicate and concurrency tests passed |

## E. Real MinIO

Actual PrivateFileStore/MinIO operations passed, without TestArtifactStore:

- Student upload, owner download and authorized staff download.
- Unrelated enrolled-student denial and anonymous direct-object HTTP 403.
- Metadata visibility from another database scope.
- Preservation of committed referenced objects during cleanup.
- Injected PostgreSQL failure after a successful real object upload: file metadata rolled back while the cleanup intent and object survived.
- Actual stop of the dedicated MinIO server: Outbox delivery failed and persisted Retry with an attempt count.
- Restart of that same server: both committed and orphan objects survived.
- Explicit Outbox retry: orphan removed, intent Delivered, committed artifact still downloadable.
- Repeated upload calls with the same name/content: distinct metadata/object identities, correct bytes, and both referenced objects retained.
- Real stored PDF issuance, retrieval, digest checks, ownership, HMAC/proof verification, key rotation and revocation, supersession, graduation source invalidation and reissuance retaining original bytes.

The bounded test adapter forwards operations to the actual PrivateFileStore and uses cancellation deadlines for outages; it does not synthesize successful storage operations. Authorization in this real-storage scenario is exercised through services with database-backed Identity and ownership checks. The PostgreSQL suite separately exercises HTTP authentication/authorization.

**Duplicate-request limitation:** Upload has no client idempotency token. Two independent calls create two distinct valid artifacts; they are not deduplicated. The new check proves the actual behavior and preservation, not exactly-once semantics across a client HTTP retry. No new idempotency feature was introduced.

## F. Confirmed Runtime Defects and Fixes

| Defect | Correction and affected files | Regression evidence |
|---|---|---|
| JWT key supplied after minimal-host startup validation | ApiFixture.cs supplies its existing test-only key through early host settings; production validation unchanged | HTTP fixture starts; complete suite passes |
| Phase 23 migration treats Id as schema | Exact user-approved principalColumn named argument; Phase23CompletionMigrationTests asserts intended FK semantics | Fresh/upgrade migration execution |
| Malformed remediation function dollar quote | Exact user-approved one-character delimiter correction | Fresh/populated upgrades and live append-only rejection |
| Finance journal alias old conflicts with trigger OLD record (42702) | New RuntimeFinanceTriggerCorrection migration replaces only that alias in the function; retains all journal guards | Entire finance suite passes |
| Ordering over positional DTO constructors cannot translate to SQL | CurriculumService.cs, AnalyticsService.cs, StudentServiceCaseService.cs order source/grouped values before DTO projection | Curriculum, academic progress, analytics and service-report integration tests pass |
| PostgreSQL microsecond precision breaks replay comparisons/receipts | CampusReservationPolicy.cs, AcademicAdvisingWorkflows.cs, CurriculumTransitions.cs compare at persisted precision; ExamTakingService.cs and ApplicantService.cs return persisted-precision timestamps | Reservation/session/transition replay, examination receipt and applicant invitation equality tests pass |
| Missing curriculum yields NotFound during a graduation command | GraduationManagement.cs rejects the absent prerequisite as Conflict after authorization, preserving degree-audit requirements | Existing lifecycle regression passes |
| Invalid test course codes abort setup | Valid synthetic codes in FinalRemediationIntegrationTests.cs, Phase19CompletionWorkflowTests.cs, Phase19RegistrarTests.cs, AssignmentDeadlineConcurrencyTests.cs and Phase8AdvancedLearningTests.cs; production validation unchanged | Previously blocked scenarios execute and pass |
| Invalid fault/attachment setup | Correct dollar quotes in the LMS fault trigger; initialize FormFile Headers before ContentType in Phase23ServiceDeskTests.cs | Actual database rollback and attachment ownership scenarios pass |
| Lock-wait test uses retry-strategy-incompatible ExecuteUpdate inside its externally held transaction | AssignmentDeadlineConcurrencyTests.cs uses parameterized SQL in that same controlled transaction | Both lock-wait variants pass with original rejection/state assertions |
| MinIO outage fixture loses random port after restart | Dedicated disposable host port retained and asserted across stop/start | Real outage/recovery test passes |

New verification cases: FinalMigrationUpgradeTests.cs and RealMinioIntegrationTests.cs. ApiFixture.cs accepts optional real-storage configuration; default PostgreSQL tests retain the substituted provider. FinalRemediationRulesTests.cs recognizes the new last migration while continuing to inspect the original remediation migration. No endpoints gained business logic, no authorization checks were removed, and no historical evidence was rewritten to make tests pass.

## G. Remaining Risks and Limits

- No known failing scenario remains in the final runs; Docker and registry access are not current blockers.
- Upload retries from a client lack an idempotency token, as described above.
- MinIO outage testing used a controlled server stop/start, not host power loss, abrupt application termination at every commit boundary, multi-node storage failure or disk corruption.
- The same-container restart proved object retention; independent VHDX/volume disaster recovery and production Data Protection keyring backup/restore were not tested.
- No institutional-scale load, throughput, sustained contention or long-duration soak benchmark was performed. Mutable image tags were used as configured; exact local image identities and server versions are recorded above.
- No real database was upgraded. Production deployment requires its own data review, backup/recovery rehearsal and migration procedure; synthetic-data success does not remove those operational responsibilities.

## H. Final Engineering Decision

**INTEGRATION_VERIFIED_WITH_LIMITATIONS**

Actual infrastructure execution now supports the repaired academic, financial, campus, quality, authorization and document workflows. All original PostgreSQL scenarios and the added populated upgrade passed; separate real MinIO verification passed; restore, warnings-as-errors Release build, 536 independent tests and EF model consistency passed. The remaining limits are stated explicitly rather than hidden behind a substituted storage provider or a production-readiness claim.
