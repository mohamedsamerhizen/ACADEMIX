$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..\.."))
dotnet restore Academix.slnx
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
dotnet build Academix.slnx -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
if ($LASTEXITCODE -ne 0) { throw "Local tests failed" }
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw "EF model mismatch" }
dotnet ef migrations script 20261103130000_Phase22StandaloneSessionRooms 20261103140000_Phase22PhysicalVenueAllocations --project Academix.Api --configuration Release --no-build --output verification/phase22/phase22-final-migration.sql
if ($LASTEXITCODE -ne 0) { throw "SQL generation failed" }
Write-Host "LOCAL VALIDATION PASSED. POSTGRESQL/Docker REMAIN UNVERIFIED." -ForegroundColor Green
