# Phase 22 — Package 03 (session rescheduling and room occupancy)

**Baseline:** Package 02 passed on the user's Windows computer: Release build and 392 / 392 Docker-independent tests.

## Implemented

- New staff-authorized `PUT /api/academic-operations/sessions/{sessionId}/reschedule` uses `ExpectedVersion`, required reason, future UTC interval, and optional topic.
- Rescheduling is restricted to future, planned sessions with no recorded attendance; does not silently reassign a timetable slot or managed room.
- Persistent, append-only JSON before/after audit with actor, reason and timestamp, created atomically with the change. Staff-only `GET /api/academic-operations/sessions/{sessionId}/revisions`.
- Idempotent no-op changes create no audit/notification. Real changes send privacy-safe Outbox events to enrolled students.
- Both session creation and rescheduling use institution-wide transaction locks; overlap checks include sections, instructors, enrolled students, and managed rooms derived from the referenced slot.
- Prevent timetable slot room reassignment or deactivation while future planned sessions still reference that slot.
- Admin-only actual session occupancy `GET /api/campus-facilities/institutions/{institutionId}/rooms/{roomId}/occupancy?fromUtc=...&toUtc=...` with UTC 31-day range bound and 500-record cap.
- Additive migration `20261103120000_Phase22ClassSessionRescheduling` creates `academic_class_session_revisions` and an append-only trigger. EF model snapshot and designer updated.
- Added focused local contract/domain/migration tests, plus route registry updates.

## Boundaries and known limitations

- Session room assignment is still inherited from the selected **weekly timetable slot**; standalone sessions without linked slots do not reserve managed rooms. Explicit per-session room allocation and closings/maintenance are left for subsequent development.
- The existing weekly room search is wall-clock based; actual session occupancy is queried separately in UTC. It does not convert institution-local weekly slots into UTC.
- Existing historical sessions are not backfilled into the new reschedule audit (only future reschedules are recorded).
- The authoring runtime does not have the .NET SDK, Docker or PostgreSQL. Build, local tests, migrations and integration execution for this package are **not verified here**. Do not apply migrations to a production database before integration verification.

## Local verification on Windows (no Docker)

```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
```
