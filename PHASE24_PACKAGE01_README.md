# ACADEMIX — Phase 24 / Package 01 (source candidate)

Built on the previously reported Phase23 local gate (471/471 tests). This is the **first** package of Phase24, not the final feature-freeze package.

## Scope
- Program learning outcome definitions with institution/program ownership and configurable course-grade proxy targets.
- Institution-scoped alignments to courses already belonging to the program.
- Per-program/per-term quality review in Draft/Published state; publishing freezes a JSON evidence snapshot.
- Transparent outcome-level proxy signals from published gradebook result rows for members of the program. This is **not** a direct assessment of PLO mastery or accreditation proof.
- Institution-administrator quality improvement actions, completion notes and dashboard counts.
- CSV export with formula-injection protection.
- Four new tables in one additive EF Core migration; no previous migration changed.
- Rule tests, migration smoke test and updated approved API route inventory.

## Windows validation (Docker stays disabled)
```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
```

## Known pending work
- This package has not been compiled or tested in the authoring environment (no .NET SDK).
- Migration has not been executed on PostgreSQL; EF snapshot equivalence is unverified until the above local command runs.
- Integration tests on PostgreSQL, role revocation and concurrent publish races remain pending until final engineering verification.
- Direct rubric-level learning outcome mapping, formal accreditation evidence packs, systematic action event history and broader quality analytics belong to a later Phase24 package.
- No external accreditation certification is implied.
