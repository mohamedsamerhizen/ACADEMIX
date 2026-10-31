# ACADEMIX — Phase 23 Complete Feature Candidate

**Status:** `PHASE23_CODE_COMPLETE_VERIFICATION_PENDING` — based on static source review only. This does not imply a successful build or database execution.

## Baseline

- Full source from Phase 22 Final (which the user built and verified with **427/427** local tests) plus Phase 23 Package 01; no historic migration rewrites.
- Package 01 was previously prepared, but its compilation/model consistency was not yet confirmed on the user's .NET environment. The baseline test count **does not apply to this final package**.
- No Docker, PostgreSQL server, MinIO, or Codex involved in this delivery.

## Phase 23 end-to-end functionality

**Original Package 01:** Existing native academic/financial/registrar workflows remain authoritative; limited service catalog, student submission, idempotency, staff queue and action transitions, public/private event timeline, Outbox notifications, institution isolation and optimistic versioning.

**Full completion added:**

1. Queue-specific delegation for currently active institutional Instructors or TeachingAssistants, granted and revoked by the institution admin. Authorization verifies current persisted roles and active grants; ordinary student role does not authorize staff routes.
2. Case-to-staff assignment and unassignment with authorization, original evidence, expected version and institution coordination lock. Revoking a grant unassigns affected open cases.
3. Audited case transfers between five supported functional desks (GeneralSupport / StudentAffairs / ITSupport / Library / CampusServices), clearing prior assignment while retaining the original service-definition authority.
4. Escalation to High or Urgent priority, with reason and timestamp. The original due date is preserved.
5. Institution-scoped SLA/aging counts, overdue / unassigned / status aggregation by current queue, CSV export with no student-identifying data. This is an on-demand report, **not** a scheduler or automatic reminder service.
6. Atomic small evidence attachment storage in PostgreSQL (up to 2 MiB each, max 10 per case), with PDF/JPEG/PNG signature+extension validation, SHA-256, ownership, staff-only/private visibility and restricted download. Files are saved with the same DB transaction as metadata; no MinIO dependency or orphan handling needed in this phase. No malware scanner is claimed.
7. Strict separate public student timeline / staff timeline and attachment views; student DTOs and Outbox payloads never include private notes or staff-only attachments.
8. Additive migration `20261104110000_Phase23ServiceDeskCompletion`: 3 tables, 4 case columns, relational references, unique staff grant, checks/indexes and backfill of existing cases' desk codes. Existing `20261104100000_Phase23StudentServiceCases` remains unchanged.

### New APIs (14 routes)

- `PUT /api/student-services/institutions/{institutionId}/staff-grants/{userId}/{queue}`
- `GET /api/student-services/institutions/{institutionId}/staff-grants`
- `POST /api/student-services/institutions/{institutionId}/cases/{id}/assign`
- `POST /api/student-services/institutions/{institutionId}/cases/{id}/refer`
- `GET /api/student-services/institutions/{institutionId}/cases/{id}/referrals`
- `POST /api/student-services/institutions/{institutionId}/cases/{id}/escalate`
- `GET /api/student-services/institutions/{institutionId}/reports`
- `GET /api/student-services/institutions/{institutionId}/reports/csv`
- Student/staff `GET` attachment list, `POST` upload and `GET` download (6 routes)

### Quality coverage

- Existing route regression whitelist extended to include new API routes.
- Local unit tests: service desk list, unsupported category rejection, referral versions, escalation priorities, assignment validation, MIME/signature/extension and size rejection, staff-only DTO contracts, additive migration/model checks.
- PostgreSQL integration scenarios compile intent only and remain to be run: grant/revocation/assignment, referral/report, tenant isolation, and private document/note visibility.

## Verification truth table

| Check | Status |
|---|---|
| Phase 22 Final Build / 427 tests on user's machine | PASSED for previous baseline only |
| Phase 23 full source and ZIP structural validation | PASSED static checking |
| Final Phase 23 Release build | NOT EXECUTED here; .NET SDK unavailable |
| Final Phase 23 unit tests | NOT EXECUTED here |
| EF Core model-consistency command | NOT EXECUTED here |
| Phase 23 migrations applied to PostgreSQL | NOT EXECUTED (Docker deliberately disabled) |
| PostgreSQL integration/concurrency tests | NOT EXECUTED (Docker deliberately disabled) |
| MinIO | NOT USED for this feature; existing modules unverified |

## Deliberate scope decisions / limitations

- Functional desks are the supported general-service categories, not a replacement for the university's academic department hierarchy or its finance/graduation workflows.
- Staff are delegated using existing roles; no unnecessary new global user role was introduced.
- SLA time is based on UTC hours from submission and can be queried anytime; does not model business calendars or send timed reminders.
- Attachments are deliberately small and stored in PostgreSQL; future large-scale deployments may introduce private object storage, virus scanning, retention policy and a storage migration.
- Concurrent edits and SQL constraints are designed around an institution row lock; database execution has not been verified and MUST be tested before production.
- Before Phase 24, user should run restore + strict Release build + all local tests + EF pending model check and share the complete output. Correct any real compiler/model problems by a small correction package; do not start Docker yet.

## Acceptance / status

Use `PHASE23_CODE_COMPLETE_VERIFICATION_PENDING` until local build, tests and model check pass. PostgreSQL and integration checks remain deferred until final engineering review. Do NOT label `PHASE23_VERIFIED` without actual runtime evidence.
