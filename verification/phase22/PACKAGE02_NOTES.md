# Phase 22 — Package 02 (source-level, not operationally verified)

This package extends the verified-by-user Package 01 FIX01 baseline (Release build and 382 local tests on Windows).

## Changes

- Timetable create/update/deactivation now stores **a revision row in the same transaction** with actor, reason, before/after snapshot and UTC timestamp.
- Same-value updates do not create artificial history or student notifications.
- Deactivation uses fresh-scoped retries and institution-wide lock, matching timetable updates and room writes.
- Only authorized section staff can view revision history via a new API. Students receive internal notifications but **not staff-provided reasons or history**.
- Outbox events are emitted per currently enrolled student and carry only student/section/slot/revision identifiers.
- PostgreSQL migration `20261103110000_Phase22TimetableRevisionHistory` adds the append-only history table and a database trigger that rejects UPDATE/DELETE.
- Existing timetable request shape and response shape are backward-compatible; `ChangeReason` (slot JSON) and `reason` (deactivation query) are optional.
- Source-level unit and migration-model regression tests added.

## Deferred to later Phase 22 packages

- Actual ad hoc session rescheduling and room allocation for sessions.
- Room closures/maintenance windows, examination room allocation, comprehensive occupancy dashboard.
- Comprehensive integration testing, PostgreSQL migration application, concurrent execution, Docker/MinIO.

## Verification

- ZIP content integrity and static checks performed in the authoring environment.
- **No .NET SDK in the authoring environment. Build and tests have not been executed for this package.**
- Run Release build and local tests with the PowerShell commands in the accompanying instructions. Do not run migrations on a production database.
