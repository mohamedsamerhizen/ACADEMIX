# Installer verification evidence

## Current source remediation — 2026-10-30

This file's installer scenarios below are historical evidence for an earlier payload. The current remediation changes source in `phase19-work`; it does not rebuild delivery ZIPs, update installer manifests/hashes, reapply the installer, or certify those packages against these fixes. Current source restore and Release build pass; local tests pass 277/277. The full run has 277 passed, 74 Docker setup failures, zero skipped. See `PHASE19_VERIFICATION_REPORT.md` and `evidence/remediation-*` for this revision. The historical 245-test installer result is not a current source test count.

Do not use the old delivery package as evidence that the worker/PDF cleanup fixes are installed elsewhere. Repackaging and fresh installer verification would be separate work. No installer script behavior was changed, and no deployment or application database was touched.

## Historical installer runs (not rerun during remediation)

The installer was exercised with Windows PowerShell 5.1 against disposable source copies under `phase19-work/installer-tests`. The Desktop project and all academic databases remained untouched. The same scripts were parsed without syntax errors.

| Scenario | Outcome |
|---|---|
| Changed baseline hash | Rejected before source writes; exit 1 |
| Unknown added C# source | Rejected before source writes; exit 1 |
| Corrupted payload | Integrity rejection before source writes; exit 1 |
| Simulated staged build failure | Exit 1; original source hashes unchanged; no added payload files |
| Simulated target build failure | Exit 1; originals restored and newly added files removed |
| Simulated edit during target verification failure | Unknown contents preserved under `unexpected-files`; originals restored; exit 1 |
| Actual full installation in copied baseline | Staged and target Release builds succeeded with 0 warnings/errors; both local test runs passed 245/245; exit 0 |
| Reapply to installed candidate | Rejected, all installed payload hashes unchanged |
| Manual rollback after a source edit | Refused before rollback; edit preserved |
| Manual rollback with matching installed hashes | Succeeded; all 260 baseline input hashes restored and new payload files removed |

The three failure simulations replace only the test harness's `dotnet` command with controlled return codes; they test installer control flow, not compilation or application correctness. Actual build/test claims above come from separate real .NET invocations captured in `evidence/installer-real.log`. Baseline comparison after each simulated rollback reported zero source/hash mismatches.

The installer deliberately does not run migrations or integration tests. The success message identifies this package as INCOMPLETE. No database rollback scenario has been tested. Generated build outputs, logs, backup folders and staged directories are retained for diagnosis; source rollback does not attempt to restore caches or delete academic data.
