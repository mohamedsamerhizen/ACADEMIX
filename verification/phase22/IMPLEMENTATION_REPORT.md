# ACADEMIX Phase 22 — Implementation report / Final feature package

**Status:** `PHASE22_CODE_COMPLETE_VERIFICATION_PENDING` (conditional on Windows build and local test results).  
**Source base:** Package 04, for which the user-provided PowerShell log recorded 412 passed, 0 failed, 0 skipped and a successful Release build.

## Completed workflows across Packages 01–Final

- Institution buildings and managed rooms/labs with capacity and activation checks.
- Managed-room weekly section timetables, section/instructor/student conflict checks and legacy display labels.
- Timetable revision history and student Outbox update messages.
- Actual class-session allocation, rescheduling, audit history and occupancy queries, including standalone sessions.
- Concrete **maintenance room closures** with start/end UTC, reason, idempotency key, cancellation record and tenant-scoped audit.
- Concrete **physical examination room reservations**, requiring an existing published exam, a section-assigned active invigilator, and capacity within the room and section. These bookings **do not alter online exam windows or submissions**.
- Room conflicts checked against existing sessions, closures and venue reservations. New/rescheduled class sessions check dated room holds and exam/student conflicts.
- Read-only active exam venue lookup for the enrolled student, plus admin room availability at concrete UTC intervals, reservations, and a unified UTC campus calendar.
- Event payloads for physical exam venue creation/cancellation are privacy safe and consumed through the existing internal Outbox.

## Consistency model

- Institution-level `FOR UPDATE` serializes supported room reservation and class-session writes.
- Unique institution/request key prevents repeated reservation creation; the replay must have identical payload.
- Partial unique active exam index prevents multiple active venue allocations for one examination (single-venue design).
- Revocation/cancellation leaves the row and its actor, timestamp, reason and incremented version available.
- Postgres composite tenant FKs enforce room and section affiliation; exam/invigilator ownership is verified in application service against live institution/assignment records.
- Weekly institution-local template slots are *not* mapped to concrete dates; temporary maintenance holds block concrete class-session creation, not the recurring template itself.

## New schema / API

- Migration `20261103140000_Phase22PhysicalVenueAllocations`: additive table `campus_room_reservations`, tenant-bound foreign keys, unique replay keys, time indexes and check constraints. Prior migrations were not edited.
- New controller actions: `/institutions/{id}/reservations` (GET/POST), `/institutions/{id}/reservations/{id}/cancel` (POST), `/institutions/{id}/rooms/available-at` (GET), `/institutions/{id}/calendar` (GET), and `/me/exam-venues` (GET) under `api/campus-facilities`.
- These endpoints use existing Identity role verification, Section/Student enrollment data and scoped admin authorization.

## Verification

- Package 04 Release build and 412/412 local tests: **passed on the user's Windows machine** (user-provided output).
- Final package: **not built or tested in the authoring environment** because .NET SDK is unavailable.
- New local xUnit source includes pure validation, time-boundary, idempotency, cancellation, migration/model and strict controller route tests.
- Two newly added PostgreSQL integration scenarios compile only after Windows build; actual execution remains intentionally pending with Docker disabled.
- PostgreSQL migration application, migration upgrade path, concurrency, triggers, notifications and actual database-backed workflows are **unverified**.
- No Docker, database container, MinIO, deployment or installer commands were run.

## Scope limitations and future review gates

- Single physical venue per published exam. Multi-room cohorts/candidate seat assignments remain unsupported; do not advertise them as implemented.
- The physical exam venue is informational and scheduling-only; it never changes the electronic exam's allowed window or attempt rules.
- No automatic recurrence expansion or time-zone database conversion of weekly timetable templates.
- Room availability for dated requests is based on actual sessions and concrete room reservations. Recurring templates without generated sessions do not block a concrete dated hold.
- Institution-wide fixed-row locks protect application workflows; database trigger/exclusion-level overlapping reservation defense is not independently verified.
- Final gate: build with warnings as errors, run local tests, check EF pending model changes, generate SQL, discover integration tests; execute real PostgreSQL/Testcontainers later.
