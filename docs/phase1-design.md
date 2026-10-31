# ACADEMIX Phase 1 — Architecture and Business Rules

Scope: Identity, institution governance, faculties, optional-faculty departments, academic programs, academic terms, course CRUD, many-to-many program curriculum, section offerings, staff assignment, and enrollment.

## Security
- No public signup. Password hashing and account management use ASP.NET Core Identity.
- Stateless access JWT is ten minutes; refresh-session family maximum is seven days, stored by SHA-256 hashes. A replayed rotated token revokes the active family.
- Institution admin and SuperAdministrator roles are read from the current database on each protected request. Institution administrators cannot manage other institutions. SuperAdministrator is global.
- Bootstrap is **off by default** and must be supplied by operator-only environment configuration. Disable immediately after first use.
- Current user status, security stamp and institution activity are verified for JWT requests. Rate limiting is local-process only.

## Academic rules
- Institutional codes are unique within their owning institution. All resources have an InstitutionId.
- Departments may omit FacultyId. Programs must have an active Department.
- Dates use DateOnly for Academic Terms. Planned→Active→Closed; overlapping Active terms rejected by the application. A database exclusion constraint is a future hardening task for the creation race.
- Courses can be attached to multiple programs. Legacy CS101/MATH101 retain their IDs.
- Sections start Draft. Opening requires an Active Term, Active Course, and a primary Instructor; Open→Closed/Cancelled is terminal.
- Teaching staff must belong to the section institution, be active, and possess their assignment role. Primary instructor is unique per section.
- Enrollment requires admin action, an active Student role, and an Open section. A PostgreSQL FOR UPDATE lock on the section row serializes capacity checks and inserts. Unique (SectionId, StudentId) prevents duplicate records. Withdrawal retains history; re-enrollment reactivates the same record.
- Institution deactivation blocks account-based operational access immediately, without deleting historical academic records.

## Limitations to address before production
- Infrastructure authorization and application services live within Academix.Api to match the user's preferred two-project layout; explicit feature folders provide modularity.
- Database tests, migrations, Docker runtime, and live access checks must run on the developer's machine; this ZIP was prepared without .NET/Docker.
- JWT key rotation, durable ASP.NET Data Protection keys, MFA, password reset, distributed rate limiting, high-volume concurrency tests, tenant-wide deletion governance and detailed audit queries are not yet implemented.
- Do not expose the bootstrap password in command logs, commits, or issue reports.
