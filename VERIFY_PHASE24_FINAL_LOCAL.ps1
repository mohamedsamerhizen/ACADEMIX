$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (!(Test-Path './Academix.slnx')) { throw 'SOLUTION NOT FOUND' }
dotnet restore Academix.slnx
if ($LASTEXITCODE -ne 0) { throw 'RESTORE FAILED' }
dotnet build Academix.slnx -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'RELEASE BUILD FAILED' }
dotnet test Academix.Tests/Academix.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Academix.Tests.Integration'
if ($LASTEXITCODE -ne 0) { throw 'LOCAL TESTS FAILED' }
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'EF MODEL CHECK FAILED' }
New-Item -ItemType Directory -Path verification -Force | Out-Null
dotnet ef migrations script 20261105100000_Phase24ProgramQualityFoundation 20261105110000_Phase24QualityEvidenceCompletion --project Academix.Api --configuration Release --no-build --output verification/phase24-final-migration.sql
if ($LASTEXITCODE -ne 0) { throw 'MIGRATION SQL GENERATION FAILED' }
Write-Host 'PHASE 24 FINAL LOCAL GATE PASSED - DATABASE INTEGRATION STILL PENDING' -ForegroundColor Green
