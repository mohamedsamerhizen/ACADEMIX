# Development and configuration

[Back to README](../README.md#quick-start--docker)

## Compose environment

Run commands at the repository root. Copy `.env.example` to `.env`, then privately replace its placeholders. Compose reads this file for interpolation; a host `dotnet run` process does not.

| Compose variable | Meaning |
|---|---|
| `POSTGRES_PASSWORD` | Local PostgreSQL password; initialized volumes retain their existing credentials |
| `JWT_SIGNING_KEY` | Independent random signing secret, at least 32 UTF-8 bytes |
| `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD` | Local object-store credentials; override placeholder development defaults |
| `DOCUMENTS_PDF_LICENSE` | Valid QuestPDF category appropriate to the user/deployment; required for PDF operations |
| `BOOTSTRAP_ENABLED` | Defaults false; enable deliberately for first local administrator provisioning |
| `BOOTSTRAP_EMAIL`, `BOOTSTRAP_PASSWORD` | Private first administrator credentials when bootstrap is enabled |

Generate separate secret values with `[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`. Do not commit `.env` or paste its contents into issues or screenshots.

Compose starts PostgreSQL 18 on host loopback 5434, the API on 8081 and the MinIO console on 9001. The object API at `minio:9000` is internal. The API depends on PostgreSQL health, applies migrations at startup and stores its protected keyring in `document_keyring`. The `/health/ready` endpoint checks PostgreSQL only; successful readiness is not object-store or end-to-end workflow certification.

```powershell
docker info
docker compose up -d --build
docker compose ps
Invoke-WebRequest http://localhost:8081/health/ready
docker compose stop
```

The final command retains volumes. Preserve PostgreSQL, MinIO and Data Protection keyring data. Do not use destructive reset commands to resolve ordinary startup problems. Occupied host ports or credentials from an existing initialized volume require checking that local environment rather than resetting its data.

## Run the API on the host

Prerequisites: .NET 10 SDK, Docker Linux Engine and a privately completed `.env` as above. This optional documentation-only override exposes MinIO's object API on loopback for host development:

```powershell
docker compose -f compose.yaml -f docs/examples/compose.host-storage.yaml up -d postgres minio
```

Use a fresh PowerShell session at the repository root. Assign values matching your local `.env` **privately**; the following placeholders are instructions, not working credentials. Use a dedicated local database and do not connect to an unknown institutional database.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
$env:ConnectionStrings__Default = 'Host=127.0.0.1;Port=5434;Database=academix;Username=academix;Password=<your-local-postgres-password>'
$env:Jwt__SigningKey = '<independent-random-secret-of-at-least-32-UTF8-bytes>'
$env:Jwt__Issuer = 'ACADEMIX'
$env:Jwt__Audience = 'ACADEMIX.Api'
$env:Storage__Endpoint = '127.0.0.1:9000'
$env:Storage__AccessKey = '<your-local-minio-root-user>'
$env:Storage__SecretKey = '<your-local-minio-root-password>'
$env:Storage__Bucket = 'academix-private'
$env:DataProtection__KeyRingPath = Join-Path $env:LOCALAPPDATA 'Academix.Local/keys'
$env:Documents__PdfLicense = '<valid-QuestPDF-license-category-for-your-usage>'
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:Bootstrap__Enabled = 'false'

dotnet restore Academix.slnx
dotnet build Academix.slnx -c Release --no-restore -warnaserror
dotnet run --project Academix.Api --configuration Release --no-launch-profile
```

This guide deliberately chooses host port 5080; it is not a claim about launch-profile defaults. In another shell, open `http://127.0.0.1:5080/openapi/v1.json`. Stop the host with Ctrl+C. Stop the supporting containers with `docker compose -f compose.yaml -f docs/examples/compose.host-storage.yaml stop` when finished.

To provision the first administrator on a fresh local database, set `Bootstrap__Enabled=true`, `Bootstrap__Email` and `Bootstrap__Password` privately **before** starting. The password needs at least 12 characters including uppercase, lowercase, a digit and a symbol. Startup migrations must be enabled for this provisioning path. After provisioning, restart with bootstrap disabled and remove those two credential variables. Manual EF migration alone does not execute bootstrap. No fictional university data is seeded by this guide.

### EF CLI

Use the EF CLI matching the repository's EF Core 10.0.3 packages. If absent, install it separately:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.3
# Inspect the current model without changing the database:
dotnet ef migrations has-pending-model-changes --project Academix.Api --configuration Release --no-build
# Optional manual migration of your explicitly chosen local database:
dotnet ef database update --project Academix.Api --configuration Release --no-build --connection $env:ConnectionStrings__Default
```

The design-time factory uses a placeholder connection. Pass the intended connection explicitly for database updates. `--configuration Release` selects the build configuration; EF's `-c` option means DbContext, unlike `dotnet build -c`.

## Documents and operational boundaries

Select a valid QuestPDF license category for your circumstances. Tests use Evaluation to exercise rendering; this does not confer a production license. The host will not obtain a license automatically.

Keep the protected Data Protection keyring stable and backed up with database and object storage. Legacy document proofs require their original `Documents__LegacyVerificationKey` during the implemented legacy-key capture process; a fresh database has no such legacy artifacts. Do not replace original secrets or keys as a troubleshooting shortcut.

Production deployment requires its own TLS, secret handling, least-privilege storage/database access, image pinning and coordinated backup/restore review. The accepted portfolio verification did not include institutional load testing or a full disaster-recovery rehearsal. See the unchanged [release closure](../verification/FINAL_RELEASE_CLOSURE.md#6-safe-docker-recovery-and-remaining-limitations).
