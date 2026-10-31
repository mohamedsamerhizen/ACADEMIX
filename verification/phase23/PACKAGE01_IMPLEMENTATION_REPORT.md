# ACADEMIX Phase 23 — Package 01 (student services intake)

**Status:** `PHASE23_PACKAGE01_LOCAL_VERIFICATION_PENDING`. This is not the final Phase 23 package.

## Source
Built from the Phase 22 Final all-remaining source package corresponding to the user's 427/427 passing local-test run.

## Included

- General student services catalog with institution-admin configuration and SLA hours, limited to categories that do not duplicate finance, curriculum, graduation or existing academic requests.
- Student intake with student/current-role checks, institutional isolation, stable per-student idempotency key, and server-computed response deadline.
- Staff queue and a controlled status machine (Submitted → InReview → AwaitingStudent → InReview → Resolved/Rejected → Closed); student reply and cancel actions.
- Public student-visible timeline and a separate staff timeline containing internal notes; internal notes never appear in student DTOs or Outbox payloads.
- Atomic case status/event/Outbox writes with institution coordination lock and optimistic versioning.
- 3 additive tables, one EF Core migration `20261104100000_Phase23StudentServiceCases`, model snapshot/designer update, and route regression test updates.
- Local policy and schema tests. New runtime tests still need execution on the user's .NET 10 machine.

## Explicitly deferred to Package 02+

Staff assignment/delegation beyond institution administrators, cross-office routing, escalation, SLA reporting, secure case attachments, integration links to existing native workflows and integration scenarios.

## Verification

- **Last verified baseline:** Phase 22 final release build passed and 427/427 local tests passed on user's Windows machine.
- **Package 01 source:** static checks only in generation environment. No .NET SDK, PostgreSQL, Docker or MinIO execution available.
- EF model consistency, compilation, local tests, PostgreSQL constraints, concurrency and Outbox delivery are **unverified** in this package.
- Do not confuse migration SQL generation or compilation with actual PostgreSQL execution.

## Delivery instructions

Extract alongside the previous phase22-final folder. Do not overwrite. Run restore, release build with warnings as errors, then local tests excluding integration, and EF pending-model check. Send full errors for an immediate corrective package.
