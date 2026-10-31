$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (!(Test-Path '.\Academix.slnx')) { throw 'ACADEMIX solution was not extracted into this folder.' }
function Run-Dotnet { param([string[]]$Args) & dotnet @Args; if ($LASTEXITCODE -ne 0) { throw ('dotnet failed: ' + ($Args -join ' ')) } }
Run-Dotnet @('restore','Academix.slnx')
Run-Dotnet @('build','Academix.slnx','-c','Release','--no-restore','-warnaserror')
Run-Dotnet @('test','Academix.Tests/Academix.Tests.csproj','-c','Release','--no-build','--no-restore','--filter','FullyQualifiedName!~Academix.Tests.Integration')
Run-Dotnet @('ef','migrations','has-pending-model-changes','--project','Academix.Api','--configuration','Release','--no-build')
Write-Host 'PHASE23 BUILD / LOCAL TESTS / EF MODEL CHECK PASSED' -ForegroundColor Green
