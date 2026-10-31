# ACADEMIX Architecture

ACADEMIX is a cohesive ASP.NET Core modular monolith. It intentionally keeps the API, services, entities, and persistence implementation in one deployable project rather than introducing Application, Domain, or Infrastructure class-library layers.

## Request flow

```text
HTTP request
  -> MVC controller (binding, authorization boundary, status code)
  -> scoped application service (business rules, transactions, resource authorization)
  -> AcademixDbContext / UserManager / private file storage
  -> PostgreSQL / MinIO
```

## API project

```text
Academix.Api/
├── Controllers/                 MVC controllers; no direct EF Core queries
├── Services/
│   ├── Academic/                hierarchy, courses, sections, enrollment
│   ├── Courses/                 public course catalog query
│   ├── Identity/                authentication, authorization helpers, bootstrap
│   └── Learning/                LMS workflows
├── DTOs/
│   ├── Academic/
│   ├── Courses/
│   ├── Identity/
│   └── Learning/
├── Entities/                    persistence/domain entity source files
│   ├── Academic/
│   ├── Courses/
│   ├── Identity/
│   └── Learning/
├── Data/
│   ├── Configurations/          EF Core mappings and deterministic seed IDs
│   ├── Migrations/              existing migration history and snapshot
│   ├── AcademixDbContext.cs
│   └── AcademixDbContextFactory.cs
├── Middleware/                  API faults and global exception handling
├── Extensions/                  service registration and application pipeline
├── Storage/                     private MinIO implementation
└── Program.cs                   composition root
```

Entity CLR namespaces remain `Academix.Api.Domain.*` even though their source files now live under `Entities/`. This is deliberate: it avoids rewriting historical migration designer files and makes the structural refactor schema-neutral.

## Controllers and contracts

All public application routes are attribute-routed MVC actions using `[ApiController]`, HTTP method attributes, dependency injection, and `[Authorize]` boundaries. Login and refresh retain the `authentication` rate-limit policy. The Phase 0 catalog and API information route remain public.

Request/response records live under `DTOs`; persistence entities are not used as authentication request contracts. Some existing service responses intentionally remain shaped projections or entities to preserve the current JSON contract during this refactor.

## Security and concurrency

- JWT validation re-checks active status, security stamp, institution membership, roles, and institution activation.
- Refresh-token rotation locks the token row and revokes active descendants after reuse.
- Institution ownership and section membership are enforced in services before data is returned.
- Enrollment locks the section row inside a database transaction before capacity is evaluated.
- Submission draft/submit operations lock the assignment row to serialize concurrent revisions.
- Private file downloads verify section visibility and student ownership.

These guarantees remain database-backed; no in-memory locking replaces PostgreSQL concurrency controls.

## Tests

```text
Academix.Tests/
├── Unit/                         domain and controller-route contract tests
├── Integration/                  PostgreSQL Testcontainers HTTP/concurrency tests
└── Security/                     controller authorization and rate-limit metadata tests
```

Docker-independent tests can run without PostgreSQL or MinIO. Integration tests compile with the solution but require Docker and a PostgreSQL Testcontainer to execute.

## Database safety

The refactor does not add, delete, regenerate, or apply migrations. Existing table names, keys, constraints, concurrency tokens, and seed identifiers remain defined by the unchanged mappings and migration snapshot. Startup migration behavior remains controlled by `Database:ApplyMigrationsOnStartup`.
