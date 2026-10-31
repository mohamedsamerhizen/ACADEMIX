# Phase 20 implementation and verification

Status: **PHASE20_CODE_COMPLETE_VERIFICATION_PENDING**. Date: 2026-10-30 (Asia/Baghdad).

Implementation is in `phase19-work/Academix.slnx`, extending the same two .NET10 projects used for Phase19. No Docker, PostgreSQL container or MinIO activation was performed. No deployment, installer packaging or historical migration edits were performed.

## Implemented business workflows

| Existing module | Phase20 extension |
|---|---|
| Curriculum and registrar records | One authoritative degree audit used by the existing graduation-audit service, new detailed audit endpoint, graduation approval and finalization. It reads assigned curriculum/revision, published transcript evidence, active enrollments, policy-controlled transfers/recognized credits, holds and standing. |
| Degree requirements | Disjoint elective groups; course/total/elective/group requirements; best-attempt GPA; distinct earned and in-progress credits; failed/repeated course history; prerequisite thresholds; approved waivers/substitutions; remaining requirements, completion percentage and Eligible/NotEligible/PendingReview results. |
| Progress risks | Explainable repeated failures, missing prerequisites, mandatory requirements, prerequisite bottlenecks, configured credit shortfall, deviation from the last approved study plan and potential delays. No predictive model or invented graduation date. |
| Advising | One active advisor per student, bounded workload where configured, reassignment/termination history, stale-safe idempotent termination by assignment UUID, assigned-advisee lists, scheduled/completed/cancelled sessions, confidential staff notes, public recommendations, follow-up dates and student acknowledgement. Existing section cases/tasks remain available. |
| Study plans | Immutable course/rationale revisions tied to their curriculum version, future institutional terms, staff recommendations/approval/rejection, student acknowledgement and history. New revisions do not modify actual enrollments. Curriculum transitions invalidate old approval context while preserving each revision's original curriculum reference. |
| Graduation | Existing Submitted/Approved/Rejected/Issued application extended with UnderReview, optional configured department review, independent institutional approval, structured evidence snapshots, idempotent submission UUIDs, rejection/resubmission, concurrent/idempotent finalization and existing lifecycle history. Issued remains the official finalized state. |
| Communications | Transactional Outbox events feed the existing internal inbox. Privacy-safe notifications, receipt and acknowledgment commit together under lease ownership; unique notification source prevents duplicates. No private notes enter payloads or notifications. |

The new degree engine is used by `CurriculumService.Audit`, so existing academic-document and graduation callers consume the same authoritative decision. Existing GPA helpers, transcript freshness checks, academic policy selection, transfer/transition recognition, institution coordination locks, Identity authorization, lifecycle events, Outbox and document issuance remain in use.

## Integrity and permissions

- Current persisted roles and active accounts are checked, including role revocation. Advisors are existing Instructors with an active institutional student assignment. TeachingAssistant alone grants no advisor privilege.
- InstitutionAdministrator and SuperAdministrator administer assignments/policy and perform academic approval. Department reviewers are explicitly assigned Instructors scoped to a program's department; advisor assignment alone grants no department authority. Administrators can also perform departmental review, but a configured department reviewer cannot be the same actor as final academic approver.
- Students can read their own public progress/plans and acknowledge recommendations. Student session DTOs contain no internal-note property; staff session routes require staff authorization. Other students and foreign institutions are denied.
- New writes use fresh-scope retries and institutional transaction locks. Unique/filter indexes enforce active assignment and request uniqueness. Existing graduation uniqueness, optimistic versions, conditional finalization, membership completion and audit events prevent duplicate academic effects. Finalization also ends active advisor assignments and releases workload slots.
- Academic eligibility is re-evaluated at approval and finalization. Approval and final snapshots preserve curriculum IDs/revisions, evaluated requirements, best grades, credit evidence, GPA, risks and policy context. They are protected against later modification by PostgreSQL triggers. Eligibility evaluation alone cannot confer a degree.
- A waived course satisfies its requirement but grants no invented credits or grade. Substitutions require a passed, unallocated source outside the current curriculum. Recognized sources and repeated achievements cannot be consumed twice. Zero minimum GPA means no GPA requirement; recognized credit never fabricates grades.

## Schema and API

One new migration: `20261101100000_Phase20AcademicAdvisingAndGraduation`, ordered after `20261031190700`. Ten new tables cover advising policy, advisor assignments, sessions, study-plan headers/revisions/courses, elective groups/memberships, requirement exceptions and department reviewer assignments. The existing graduation table gains seven workflow/evidence fields. The model snapshot and new migration target model match.

The migration adds tenant-aware relational keys/FKs, filtered uniqueness, status/credit constraints and PostgreSQL guards for user institution ownership, immutable history/evidence, published group configuration and graduation transitions. No Up table/column/data deletion is introduced. The unexecuted Phase20 migration was consolidated before delivery; historical migrations remain unchanged.

26 new actions extend the existing controllers, without endpoint business logic:

- `/api/advising`: institutional policy, assigned advisees, student advisor/history/termination, sessions, student session inbox/acknowledgement, and study-plan revision/review/acknowledgement/history.
- `/api/curriculum`: detailed degree audit, elective-group configuration/student view, and requirement-exception request/decision/history.
- `/api/lifecycle`: department reviewer delegation, paginated/filterable graduation queue, review opening, department decision and decision evidence.

Existing `POST /api/lifecycle/graduations` still accepts an empty body; an optional `clientRequestId` adds submission idempotency. Existing response records and final Issued state are preserved. All 26 routes are included in the strict approved-route regression test. Collections validate pagination and relevant status filters.

## Actual verification

| Check | Result | Exit code |
|---|---|---|
| Restore | Projects up to date | 0 |
| Release build, warnings as errors | Success; 0 warnings, 0 errors | 0 |
| All Docker-independent tests | 315 passed, 0 failed, 0 skipped | 0 |
| New Phase20 local cases (included above) | 38 passed | Included above |
| EF pending-model check | No changes since last migration | 0 |
| PostgreSQL migration SQL generation | Phase19 boundary to Phase20 SQL generated | 0 |
| Integration discovery only | 80 cases discovered, including 6 new Phase20 scenarios | 0 |
| Integration execution / actual migration execution | Deliberately not run under the container restriction | Not executed |

Evidence: `restore.log`, `build.log`, `tests.log`, `phase20-local.trx`, `model.log`, `migrations.sql` and `integration-discovery.log` in this directory. SQL generation does not prove PostgreSQL executed it. The initial added integration source used an unsupported hold property; it was corrected, then the successful final build and complete local test selection were rerun. No tests were weakened, disabled or reclassified as passing.

Local tests are in `Unit/Phase20DegreeAuditTests.cs` (19 cases), `Security/Phase20AccessTests.cs` (18 cases), and `Unit/Phase20MigrationTests.cs` (1 case). Coverage includes complete/missing degrees, elective groups, overlaps, repeats, recognition/source reuse, GPA/credit boundaries, version differences, waivers/substitutions, in-progress credits, standing review, grade changes, prerequisites/bottlenecks, role/tenant/student isolation, note contracts, revision transitions, review gates and migration/model integrity.

New PostgreSQL scenarios in `Integration/Phase20AcademicProgressTests.cs` compile but were not executed:

1. `AdvisorWorkloadReassignmentHistoryAndCurrentRoleAuthorization`
2. `SessionsKeepNotesPrivateAndRequireStudentAcknowledgement`
3. `StudyPlanRevisionsRetainHistoryAndNeverCreateEnrollments`
4. `GraduationRequiresDepartmentReviewIndependentApprovalAndConcurrentFinalizationIsIdempotent`
5. `RejectedGraduationsCanResubmitAndChangedRecordsPreventFinalization`
6. `ExemptionsRequireIndependentApprovalAndNeverGrantInventedCredit`

These exercise real PostgreSQL/Identity through the existing fixture, including database scope guards, immutable final evidence, concurrent finalization, durable notifications and grade-update re-evaluation. They are pending scenarios, not runtime proof. The previous 74 database-dependent cases are also intentionally unexecuted in this phase, not reported as skipped tests.

## Decisions and remaining verification

Elective groups are deliberately disjoint; overlap is rejected rather than allowing the same course to satisfy multiple group quotas. This is the supported policy model, not a general overlapping-basket optimizer. Waivers do not award credit; no new arbitrary credit-award mechanism was introduced. Future plans are advisory, and session follow-up dates do not create automatic enrollments or a new reminder infrastructure.

The department-review requirement is captured at submission, preserving existing/approved requests when administrative policy later changes. Current academic eligibility still re-evaluates authoritative records and current supported academic conditions before approval/finalization. The existing Submitted-to-Issued lifecycle was extended; no parallel graduation system or arbitrary global role was added.

Pending: application of the migration and trigger validation on PostgreSQL; execution of all 80 container-dependent regressions, especially concurrency/rollback/notification scenarios; upgraded-database validation and existing academic-document regressions against the new audit. These are verification blockers. No independently confirmed build/local-test failures remain. No PHASE20_VERIFIED claim is made.

## Commands executed

```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration' --logger 'trx;LogFileName=phase20-local.trx' --results-directory verification/phase20
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
dotnet ef migrations script 20261031190700_Phase19ArtifactContentSignature 20261101100000_Phase20AcademicAdvisingAndGraduation --project Academix.Api --configuration Release --no-build --output verification/phase20/migrations.sql
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --list-tests --filter 'FullyQualifiedName~Academix.Tests.Integration'
```
