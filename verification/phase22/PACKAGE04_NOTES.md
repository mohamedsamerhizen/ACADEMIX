# ACADEMIX Phase 22 — Package 04 (standalone session physical room allocation)

**Baseline:** Package 03 passed on Windows: Release build and **404/404** local tests.

## Changes

- Standalone sessions may now reserve an institution-managed room via optional `RoomId` in the existing session creation endpoint; only institution administrators may allocate a standalone room.
- Session rescheduling can assign or release rooms with optimistic version, reason, immutable revision snapshot, and Outbox notification; standalone room changes require institution administrator authorization.
- Linked sessions continue to inherit their room from the weekly timetable slot and reject conflicting explicit overrides. Existing clients may omit room parameters.
- Session creation/reschedule checks institution-owned active room/building, section capacity, and cross-section room overlap under the existing institution transaction lock.
- Actual room occupancy now includes standalone bookings and legacy timetable-linked sessions, excluding cancelled sessions.
- Room deactivation, building reassignment and capacity reductions consider future standalone reservations.
- Additive `20261103130000_Phase22StandaloneSessionRooms` migration backfills `RoomId` from a linked slot, adds tenant-scoped FK and room/time index, retains all historical migrations.
- Added eight local regression tests for assignment, removal, slot inheritance, API backward compatibility, revision comparison, and model/migration metadata.

## Known limits / verification

- Linked sessions **cannot override** the timetable slot room. To move such a session to a different physical room, an authorized administrative workflow is still needed (not silently supported).
- Room occupancy only represents explicit actual sessions, not uninstantiated recurring timetable placeholders. Weekly room availability searches use local weekday/minutes, not UTC calendar reservations.
- The authoring environment has **no .NET SDK or PostgreSQL**. Build, tests and migrations are unverified here; the user's machine must run them. No Docker was started.
- The PostgreSQL migration/backfill and concurrent-session behavior remain operationally unverified until final integration review.

## Run on Windows

```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
```
