# ACADEMIX — Phase 24 Full Functional Completion (candidate)

**Source:** Phase 24 Package 01 (user confirmed locally: Release build, 491/491 Docker-independent tests, EF no pending model changes). This ZIP is a full working tree with additional Phase 24 code; it is **not** independently compiled or runtime-verified in the authoring environment.

## Delivered feature scope (all remaining planned Phase 24)

1. **Direct criterion-level assessment:** Map learning outcomes to existing graded rubric criteria, with a proficiency threshold and same-program/section/term/tenant checks. Data comes from *published group projects* with *graded submissions* and their rubric score rows. Only teams with a member in the target program are counted. Result explicitly reports **team/criterion observations, not unique learners or confirmed individual PLO mastery**.
2. **Review evidence freezing:** At publication, both existing gradebook **proxy** evidence and new graded-rubric **direct criterion** evidence are snapshotted. Previously published reviews retain their old snapshots; newly added direct evidence is not retroactively claimed for them.
3. **Accreditation evidence packs:** Institution admins can create a draft pack from a published quality review, attach verified provenance sources (published proxy review, published direct review, action or mapped rubric criterion), inspect provenance and SHA-256 hashes, and seal an immutable manifest. A DB trigger guards sealed packs and append-only evidence items.
4. **Improvement action history:** Create/change/complete actions now create append-only per-action events with responsible actor, timestamp, status and resolution note; existing actions without past event records are not retroactively invented.
5. **Institutional quality analytics:** Longitudinal program results from *frozen published snapshots*, explicit separation of proxy metrics from direct criterion metrics, action backlog and overdue action counts. JSON + CSV export. No automatic inference of accreditation or grades.
6. **Security:** All new routes require active institutional admin authorization through the existing identity policy; role revocation and tenant ownership checked on each request. Evidence never returns student names, raw submissions or private notes.
7. **Schema:** A single additive migration `20261105110000_Phase24QualityEvidenceCompletion` creates four tables, extends review snapshots and appends tenant-aware action keys; includes PostgreSQL guards for criterion alignment ownership, published evidence snapshots, sealed packs and immutable audit trails. All Phase 24 Package 01 migrations remain untouched.
8. **Tests:** Additional local rule/migration tests and three PostgreSQL integration scenarios (compile/discovery only here) cover evidence pack sealing, history and live permission revocation. Approved route inventory updated.

## New API routes

- `GET/POST /api/quality/institutions/{institutionId}/outcomes/{outcomeId}/criteria`
- `GET /api/quality/institutions/{institutionId}/reviews/{reviewId}/direct-evidence`
- `GET/POST /api/quality/institutions/{institutionId}/reviews/{reviewId}/packs`
- `GET /api/quality/institutions/{institutionId}/packs/{packId}`
- `GET/POST /api/quality/institutions/{institutionId}/packs/{packId}/items`
- `POST /api/quality/institutions/{institutionId}/packs/{packId}/seal`
- `GET /api/quality/institutions/{institutionId}/packs/{packId}/manifest.json`
- `GET /api/quality/institutions/{institutionId}/actions/{actionId}/history`
- `GET /api/quality/institutions/{institutionId}/programs/{programId}/trends`
- `GET /api/quality/institutions/{institutionId}/programs/{programId}/trends.csv`

## Verification status

- **Already user-verified:** Phase 24 Package 01 — 491/491 non-container tests, clean Release build, clean EF model check.
- **Not yet verified:** This full-final candidate's Release build, local tests, EF pending model check, migration SQL generation, PostgreSQL application, trigger behavior, concurrent publication/sealing, and three new integration scenarios.
- **Out of scope:** External accreditation authority certification, per-learner competence from team submissions, external file storage and post-final feature additions.

## Recommended commands without Docker

```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
dotnet ef migrations script 20261105100000_Phase24ProgramQualityFoundation 20261105110000_Phase24QualityEvidenceCompletion --project Academix.Api --configuration Release --no-build --output verification/phase24-final-migration.sql
```

**Status until successful Windows gate:** `PHASE24_FINAL_CANDIDATE_LOCAL_VERIFICATION_PENDING`. Then `PHASE24_CODE_COMPLETE_VERIFICATION_PENDING` (PostgreSQL and all integration tests still pending). Do not claim `PHASE24_VERIFIED` until the final operational gate passes.
