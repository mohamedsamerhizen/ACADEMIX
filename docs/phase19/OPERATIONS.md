# Phase 19 runtime configuration and compatibility

Apply the EF migration chain to the intended PostgreSQL database through the existing migration process. Back up the database first. SQL generation does not replace an isolated PostgreSQL migration test.

## Official PDFs

Configure `Documents:PdfLicense` with the QuestPDF license applicable to the deployment. Issuance fails closed if it is absent. Tests use `Evaluation`; that value is not a deployment license decision. Templates select an installed font: the default is Arial on Windows and DejaVu Sans on Linux. The API container installs DejaVu and its native font dependencies. Publishing a template renders a sample before activation.

Configure `DataProtection:KeyRingPath` to durable storage and share the same protected keyring between API replicas using the ACADEMIX application name. The compose service mounts `/var/academix/keyring`. Back up this keyring with the private artifact store and database; losing it prevents decryption of verification keys and invitation capabilities. Restrict filesystem access and use the deployment's supported encryption-at-rest mechanism for the keyring. Do not commit key material.

For a database containing historical documents without stored proofs, supply the original historical document signing key through `Documents:LegacyVerificationKey` in the deployment secret configuration. Startup captures existing proofs once before serving HTTP; it refuses to guess a missing original key. Remove this setting after successful capture. New issuance and verification use independent versioned keys and never use `Jwt:SigningKey`.

Keep private artifact storage durable across restarts. Before each PDF upload, issuance independently commits a `DocumentArtifactCleanup` Outbox intent. Reconciliation locks the institution row, waits for issuance commit/rollback, and removes only paths unreferenced by any issued document. Failures retain retry/backoff state; repeated cleanup and issuance at the same path are safe. Enable background processing for recovery and monitor retrying cleanup events. Historical orphans created before this mechanism have no tracking intent and require separate inventory. Do not cancel cleanup events unless recovery is being handled explicitly. Issued metadata and content signatures remain immutable.

The issuance transaction deliberately uses `FOR NO KEY UPDATE` on its institution: it must allow the independently committed intent's foreign-key `KEY SHARE` lock. Reverting that lock to `FOR UPDATE` would deadlock scheduling. Reconciliation uses `FOR UPDATE` through reference checking and removal to serialize against issuance/retry. Keep these operations in the same institution coordination scheme.

Key retirement preserves old verification; revocation invalidates it. Public verification checks current source, supersession, revocation and artifact integrity without exposing student snapshots. Missing keys or an unreadable keyring fail closed with an explicit conflict. Restore the original durable keyring rather than rotating away evidence needed to decrypt historical signing secrets. These are server-side HMAC proofs and detached content signatures, not PAdES/certificate signatures or independent non-repudiation.

## Imports and activation

Imports accept mapped JSON rows or quoted UTF-8 CSV. Map the five required identifiers plus optional `FullName`, `PublishedSectionId` and `InviteIfMissing` columns. Academic history references an existing published institutional gradebook for the verified student; importing arbitrary grades is unsupported. Dry-run and independent administrative approval precede application. Resume uses per-row transactional checkpoints.

An explicitly enabled `InviteIfMissing` row requires a full name. It creates an accepted applicant and enters `AwaitingActivation`, without provisioning a student account. An authorized administrator obtains or renews the invitation through the applicant invitation endpoint and delivers it through the institution's trusted channel. Token possession and activation establish the account; resume then applies curriculum/profile mappings. No passwords belong in import files or event payloads.

## Requests and event processing

Existing `Pending` requests remain compatible. Executable requests enter `UnderReview` with typed resource references, receive a decision, and become `Completed` only after their business effect commits. Legacy approved executable requests without typed references must re-enter review; they are not silently marked fulfilled. Attendance excuses require an existing attendance record. Enrollment exceptions remain bound to the active policy and do not override capacity, holds or institutional ownership.

The PostgreSQL outbox delivers scheduled announcements and institutional feed receipts. There is no implied external email or webhook delivery. Expired processing leases recover after restart. Failures retain a safe error type and retry with bounded backoff; administrators can inspect, retry or cancel eligible events. An already dispatched announcement is acknowledged without replaying notifications. Deferred curriculum transitions recheck the approving administrator's current authority.

The background worker isolates each examination attempt, the scan, Outbox scheduling and Outbox delivery. Polling remains every 15 seconds with at most 100 examined attempts per tick; UUID cursor rotation prevents a full page of persistent failures from starving later eligible attempts. Each finalization has a separate DI scope and atomic status/audit transaction. Shutdown cancellation propagates without being logged as an ordinary failure.

## Verification boundary

The 2026-10-30 remediation passes the Release build and 277 local tests. Docker/Testcontainers setup fails in this environment, so PostgreSQL migration application, baseline upgrade, concurrency/recovery test bodies, and actual MinIO operations remain unverified. See `PHASE19_VERIFICATION_REPORT.md` and `evidence/remediation-*.trx`. Regenerated migration SQL includes all eight Phase19 migrations, but SQL generation is not database execution. A partially upgraded Phase19 database with keyed documents lacking artifact signatures needs a reviewed backfill procedure before the final signature constraint can apply.
