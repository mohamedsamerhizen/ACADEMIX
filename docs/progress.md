# ACADEMIX Progress

## Phase 0 — Foundation

Implemented: .NET API foundation, PostgreSQL/EF Core, public course catalog, health checks, and automated tests.

## Phase 1 — Identity and academic core

Implemented: ASP.NET Core Identity, JWT authentication, refresh-token rotation and reuse protection, role and institution authorization, academic hierarchy, courses, terms, sections, teaching assignments, enrollment, withdrawal, and concurrency-safe capacity enforcement.

The pre-refactor checkpoint recorded 21/21 passing tests. The current integration suite remains compiled but requires Docker/PostgreSQL to execute.

## Phase 2 — Learning management

Implemented in source: units, lessons, announcements, assignments, drafts, submission, grading, lesson progress, private file storage, and enrollment-aware visibility. The Phase 2 migration remains present and unchanged.

Docker-dependent end-to-end verification of Phase 2 and live MinIO behavior is still pending. This status must not be interpreted as a production-readiness claim.

## Controller architecture refactor — completed

- Replaced Minimal API feature mappings with attribute-routed MVC controllers.
- Moved application logic under `Services`, contracts under `DTOs`, entity files under `Entities`, mappings under `Data/Configurations`, error handling under `Middleware`, and MinIO code under `Storage`.
- Centralized dependency registration and the HTTP pipeline in `Extensions` while preserving `Program.cs` as the composition root.
- Preserved route templates, methods, created-resource locations, authorization boundaries, rate limiting, service business rules, transactions, and database row locks.
- Reorganized Docker-independent tests into `Unit` and `Security`; retained PostgreSQL Testcontainers tests under `Integration`.
- Added an exact route-surface regression test and authorization/rate-limit metadata tests.

## Verification snapshot

- Baseline Release build: succeeded with 0 warnings and 0 errors.
- Refactored API and test projects: compile successfully.
- Docker-independent Unit + Security tests: 18 passed, 0 failed, 0 skipped.
- EF Core pending-model check: no model changes since the last migration.
- Existing migration and snapshot files: byte-for-byte unchanged.
- Docker/PostgreSQL/MinIO integration tests: not executed during this refactor because Docker was deliberately deferred.
