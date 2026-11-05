# ACADEMIX — Phase 23 Complete Source Candidate

Unzip next to `phase22-final` (do not overwrite the baseline). Full solution is at the root (`Academix.slnx`). Phase 23 Package 01 and all final features are included; no earlier ZIP needs to be installed first.

See `verification/phase23/PHASE23_COMPLETE_IMPLEMENTATION_REPORT.md` for scope and unverified runtime checks.

## Verify on Windows with installed .NET 10 SDK

```powershell
dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
```

No Docker, PostgreSQL container or MinIO start. If any command fails, report the exact error; do not count this phase as locally accepted.
