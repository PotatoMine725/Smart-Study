# Task A1 — WAL-safe backup fix (C3a)

**Date:** 2026-07-12 · **Branch:** `gate/a1-walfix` (worktree off `ui_rf` @ `ae161da`) ·
**Commit (fix):** `2d04be5` · **Status:** DONE — awaiting PM `/code-review` + merge.

## What changed and why

`DbBackup.CreateBackup` is Epic 1's named top-risk mitigation: it copies the live DB file before
`SyncSchema.EnsureColumns` runs the first in-place schema upgrade. The bug (verdict **F5**): the dev
DB is a real pre-Epic-1 database (5,402 StudyLogs) running in SQLite **WAL journal mode**, so
committed data can still live in the `-wal` sidecar until a checkpoint folds it into the main `.db`.
The old `CreateBackup` did a naive `File.Copy` of **only the main `.db`**, silently dropping any
committed-but-un-checkpointed pages — a lossy "backup" precisely when the upgrade needs it as a
safety net. The clean-text test fixtures are *why F5 escaped review*: they never carried a live WAL.

**Fix (D-P1/D-P2):** inside `CreateBackup`, after the `File.Exists` guard and before the existing
path construction + `File.Copy`, open a short-lived `Pooling=False` `SqliteConnection` and run
`PRAGMA wal_checkpoint(TRUNCATE)`. This folds committed WAL frames into the main file (and resets
the WAL); it is a no-op on non-WAL databases. `Pooling=False` releases the file handle before
`File.Copy`. The checkpoint is **not** wrapped in try/catch — a checkpoint failure propagates and
fails the launch loudly, rather than letting the app upgrade against an incomplete backup. Signature
unchanged; `AppStartup.cs` has **zero diff**.

**Tests (D-P3):** the 2 existing tests were converted from fake-text fixtures
(`File.WriteAllText` / `File.ReadAllText`) to **real SQLite** fixtures (create a `Marker` table +
row via `SqliteConnection`, assert by reopening the backup as SQLite). The missing-file test is
behavior-identical (returns `null`). A new RED-first discriminating test reproduces the live-WAL
state and asserts committed rows survive into the backup.

Files: `SmartStudyPlanner/Data/DbBackup.cs` (+15), `SmartStudyPlanner.Tests/Data/DbBackupTests.cs`
(+89/−3). No other files touched.

## RED-first evidence (baseline = unmodified `File.Copy`-only `DbBackup.cs`)

The discriminating test was written and run **before** the fix. Run alone against the baseline:

```
> dotnet test --no-build --filter "FullyQualifiedName~CreateBackup_WithPendingWalPages"

Failed SmartStudyPlanner.Tests.Data.DbBackupTests.CreateBackup_WithPendingWalPages_IncludesCommittedRowsInBackup [105 ms]
  Error Message:
   Assert.Equal() Failure: Values differ
  Expected: 2
  Actual:   0
  Stack Trace:
     at SmartStudyPlanner.Tests.Data.DbBackupTests.CreateBackup_WithPendingWalPages_IncludesCommittedRowsInBackup() in D:\Code\C#\SmartStudyPlanner-a1\SmartStudyPlanner.Tests\Data\DbBackupTests.cs:line 114

Failed!  - Failed: 1, Passed: 0, Skipped: 0, Total: 1
```

The failure is the **marker row-count assertion** (`Assert.Equal(2L, count)`, line 114),
`Expected 2 / Actual 0`. This is the discriminating signal, not a setup artifact:

- **Not** an `IOException` / sharing violation — `File.Copy` succeeded with the writer held open, so
  execution reached the verify block.
- **Not** a "no such table" error — the test checkpoints the `CREATE TABLE` into the main `.db`
  first, so the backup *has* the table; `COUNT(*)` runs and returns 0.
- The two committed rows sat only in the `-wal` sidecar (precondition asserts the sidecar exists and
  is non-empty) and were **absent** from the `File.Copy`-only backup. That is the F5 data loss.

## GREEN evidence (with the checkpoint fix)

```
> dotnet build SmartStudyPlanner.slnx
ok dotnet build: 4 projects, 0 errors, 93 warnings

> dotnet test SmartStudyPlanner.slnx --no-build
ok dotnet test: 331 tests passed, 0 warnings in 1 projects (4.6 s)
```

- Build: **0 errors**. `SqliteConnection` resolves transitively via
  `Microsoft.EntityFrameworkCore.Sqlite` 10.0.5 (public `Microsoft.Data.Sqlite` dependency) — **no
  new package added**, per the spec's guardrail.
- Full suite: **331 passed / 0 failed**. Baseline was 330; the 2 existing tests were converted in
  place (not added) and 1 discriminating test was added → net **+1 = 331**. The two file-based
  startup tests (`AppStartupFileBasedTests`) that also call `CreateBackup` remain green (their DBs
  are non-WAL and their connections are closed before backup, so the checkpoint is a harmless no-op).

## `gitnexus_impact` blast radius — `CreateBackup`

`gitnexus_impact({ target: "CreateBackup", direction: "upstream", repo: "Smart-Study" })`:

- **Risk: LOW.** Impacted = 6; direct callers = 3; processes affected = 1; modules affected = 1.
- **d=1 (WILL BREAK / direct):** `DbBackupTests.CreateBackup_CopiesDbFileWithTimestampedName`,
  `DbBackupTests.CreateBackup_WhenSourceMissing_ReturnsNullAndDoesNotThrow` (both in write scope),
  and `AppStartup.EnsureDatabaseReady` (production caller — unchanged signature, still green).
- **d=2 (indirect):** `App.OnStartup` (the launch flow this hardens) and the two
  `AppStartupFileBasedTests`.
- **Affected process:** `OnStartup` only. **Affected module:** `Data` only. Signature and contract
  unchanged, so no caller edits were required.

**`gitnexus_detect_changes` note:** run against `repo: "Smart-Study"` it reported the *main
checkout's* working tree (the parallel A3 agent's `docs/knowledge/` edits + the owner's local
`AGENTS.md`/`CLAUDE.md`), **not** this worktree — it does not see `DbBackup.cs`/`DbBackupTests.cs`.
The index is stale (last indexed `a3a0a3d`) and a reindex was deliberately not run (a parallel agent
shares the index). Authoritative scope check is therefore `git -C …-a1 diff --stat`, which shows
**only** the two intended files (101 insertions, 3 deletions).

## Decisions made (ADR-style)

### D1 — Fix inside `CreateBackup` (checkpoint-then-copy), signature unchanged (adopts D-P1)
- **Why:** the utility's contract is "lossless backup." Prevent-at-source (same philosophy as M1.3)
  means callers must not have to remember a pre-checkpoint step; a checkpoint before the copy makes
  the copy complete by construction.
- **What for:** no EF coupling, `AppStartup.cs` diff = zero, and the checkpoint is a no-op on non-WAL
  DBs so existing callers/tests are unaffected.
- **Experience:** `Pooling=False` was essential — without it Microsoft.Data.Sqlite keeps the file
  handle pooled and `File.Copy` can race the open handle on Windows. One short-lived `using`
  connection cleanly releases before the copy.

### D2 — Checkpoint failure propagates, no try/catch (adopts D-P2)
- **Why:** backup is Epic 1's top-risk mitigation. A swallowed checkpoint error would let the app
  upgrade against a silently-incomplete backup — the exact failure mode F5 is about.
- **What for:** fail loudly at startup, consistent with the pre-existing `File.Copy` behavior (which
  also throws on failure).
- **Experience:** matches the existing contract; no behavioral surprise for `EnsureDatabaseReady`.

### D3 — Real-SQLite fixtures + checkpoint-the-schema-first in the discriminating test (adopts D-P3)
- **Why:** the pragma throws on non-DB files, and clean-text fixtures are literally why F5 escaped.
  Fixtures must be honest DBs. The discriminating test must fail for the *right* reason.
- **What for:** the test flushes the `CREATE TABLE` into the main `.db` (via a first `TRUNCATE`
  checkpoint) so the backup owns the schema, then disables autocheckpoint and commits rows that stay
  only in the `-wal`. The baseline failure is then "rows absent" (`Expected 2 / Actual 0`), not "no
  such table" — a genuine discriminator, not a setup exception.
- **Experience:** keeping the writer connection **open** across the backup call was load-bearing —
  closing it auto-checkpoints on dispose and hides the scenario. Writer disposed in a `finally` +
  `SqliteConnection.ClearAllPools()` in `Dispose` so the temp-dir cleanup doesn't hit an open handle
  (mirrors the existing `AppStartupFileBasedTests` pattern).

## Self-check against acceptance criteria

- [x] Backup lossless with pending WAL pages — discriminating test GREEN, RED-first baseline failure
  pasted above.
- [x] Existing backup behaviors still asserted and green (converted to real-SQLite fixtures).
- [x] Full suite green (331 = 330 + 1 new); build 0 errors.
- [x] `AppStartup.cs` unchanged (zero diff).
- [x] `gitnexus_impact` on `CreateBackup` reported (LOW).
- [x] Commit `2d04be5` contains only `DbBackup.cs` + `DbBackupTests.cs`; on `gate/a1-walfix`; report
  in its own commit; **not merged, not pushed**.

## Sources

- `docs/plans/2026-07-12-epic1-closure-phase1-execution.md` → §Task A1 spec + D-P1/D-P2/D-P3.
- `docs/plans/2026-07-11-epic-1-closure-gate.md`; `docs/review/2026-07-11-epic1-closure-verdict.md`
  (verdict F5, condition C3).
