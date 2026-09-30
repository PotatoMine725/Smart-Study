# Report — Epic 2 / T2.4 fence Slice 0 remainder: P0-b (SQLite lock probe) and P0-c (local-save W-2 leg)

**Date:** 2026-09-30 · **Branch:** `test/epic2-fence-s0-remainder`, cut from `origin/dev` at `f4322fd`
(worktree `.claude/worktrees/fence-s0r`; the owner's checkout was not touched)
**Author:** Claude Sonnet 5.5 via Claude Code, single background session.
**Task card:** plan §24.2 card **A0** (`docs/plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md`
§14, §16.3, §21). **Production diff: none** (tests, one test fixture, docs only).

---

## 1. Scope

Docs said "fence Slices 0–2 merged", but only a partial P0-a had landed
(`Tests/Sync/Fence/CascadePredicateProbeTests.cs`). This PR adds the missing Slice-0 measurements the
task listed, as labelled characterization tests:

| Item | File | Status |
|---|---|---|
| fixture | `Tests/Fixtures/FileBackedSqliteFixture.cs` | new |
| P0-b | `Tests/Data/SqliteTransactionLockProbeTests.cs` | new, 8 tests (4 legs × 2 journal modes) |
| P0-c | `Tests/Infrastructure/Persistence/LocalSaveCharacterizationTests.cs` | new, 2 tests (observation + control) |
| P0-d | suite baseline | recorded below |

**Not in scope, and still open:** P0-a's `LuuHocKyAsync` no-change-save re-stamp leg (plan §16.3 P0-a
names it; the existing probe covers `TaskCascadeHelper` and the sync cascade only). **Slice 3's gate is
"P0-a, P0-d"** (plan §21), so Slice 0 is **not** complete after this PR. The plan §21 row and
`docs/active/README.md` were edited to say so.

**Baseline divergence:** local `dev` = `origin/dev` = `f4322fd` (no divergence). Local `dev` has the
owner's pre-existing dirty/untracked files; not touched, not synced.

## 2. Findings

Labels: **OBSERVED** = produced by a run in this session; **INFERENCE** = code reading only. Every
timing is bounded by the fixture's `Default Timeout` (1 s in the timing legs), see §2.3.

### 2.1 P0-b — `BeginTransactionAsync()` lock mode and two-connection outcomes

| # | Question | Result | Label |
|---|---|---|---|
| 1 | Journal mode of a file created by `EnsureCreated` | **`wal`**. No production code sets it; EF Core switches a new file to WAL itself. The owner's WAL dev DB (a1-wal-safe-backup report) is therefore the normal case, not an anomaly | OBSERVED |
| 2 | Lock mode taken by `Database.BeginTransactionAsync()` | **Write lock at BEGIN (immediate).** An independent `BEGIN IMMEDIATE` is refused (`SQLITE_BUSY`, code 5) while an EF transaction is open with **no statement**, and after one SELECT, and with `IsolationLevel.Serializable`. Same in WAL and rollback-journal | OBSERVED |
| 2c | Controls for #2 | raw `BEGIN DEFERRED` ⇒ probe admitted (0–3 ms); raw `BEGIN IMMEDIATE` ⇒ probe refused. The probe answers both ways | OBSERVED |
| 3 | Two EF transactions, second one starts while first is open (held ~400 ms) | The **second waits inside `BeginTransactionAsync()`** (returned after ~465–486 ms), then **sees the first's committed row** (count 1). Both rows commit. WAL = delete | OBSERVED |
| 4 | Two EF transactions, first never ends, second timeout 1 s | Second's `BeginTransactionAsync()` fails `SQLITE_BUSY` (code 5, ext 5) after ~1.1 s. The failure is at BEGIN, before any read | OBSERVED |
| 5 | Read-then-write, stale read (raw **deferred** reader A, EF writer B) — WAL | B commits at once (11 ms). A's later write fails **`SQLITE_BUSY_SNAPSHOT`** (ext 517) after ~1.1 s. Final rows = 1 (B only); A never wrote | OBSERVED |
| 5b | same — rollback journal (supplementary) | B's commit BUSY (A's read lock blocks it) **and** A's write BUSY (B's pending lock blocks it): both fail after ~1.1 s. After B rolls back: rows = 0. B holds its lock until rolled back (a counting reader would block otherwise) | OBSERVED |
| 6 | "A write is never committed on top of a stale read" (plan §14 A-3) | Held in every leg run, **for the deferred-reader style**. EF cannot create a stale read at all (#3) | OBSERVED (scope: these legs) |

**Consequence for Slice 4 (§14):** the design decision "evaluate the fence inside the executor's own
write transaction, once" is supported by the measurement, and is *stronger* than §14 assumed: because
EF's BEGIN already takes the write lock, the fence's reads are taken after the lock is won, so two EF
writers cannot interleave read and write. The §14 hedge ("either holds the write lock from `BEGIN` or
fails its later upgrade with `SQLITE_BUSY`") resolves to the first branch for EF transactions.

### 2.2 P0-c — local-origin W-2 leg

Staging: `FenceScenarioFixture.StageS1CrAsync()` **unmodified** (H → A → T; B local candidate, C remote
candidate; S1-CR staged, T held live at Base parent A). Then the UI's `XoaMon` shape through public
entry points only: `LayDanhSachHocKyAsync` → remove a MonHoc from the loaded graph → `LuuHocKyAsync`.

| State | Delete A (Base parent) | Control: delete B (local candidate parent) | Label |
|---|---|---|---|
| before | T live, `MaMonHoc = A`, `Matches == true`, record `Unresolved`, 1 record, T `Rev` 3 | identical | OBSERVED |
| `A`/`B` after | tombstoned (delete landed) | tombstoned (delete landed) | OBSERVED |
| T after | **`IsDeleted = true`**, `MaMonHoc = A`, `Rev` 4 | `IsDeleted = false`, `Rev` 3 | OBSERVED |
| record after | still `Unresolved`; 1 record (nothing staged or resolved) | same | OBSERVED |
| `Matches(BaseFingerprint, snapshot(T))` | **`false`** — D9-T1 no longer holds | `true` | OBSERVED |

This **matches** W-2 §2.5's INFERENCE ("parent tombstoned by a local UI delete: same end state"); it is
now MEASURED for the local path. **No contradiction, no STOP condition hit.** It is the P-CR-3 RED
baseline that Slice 4 flips to `Blocked`. `T.ModifiedByDeviceId` is `LOCAL-DEVICE` both before and after,
so it does not discriminate; the `Rev` bump does.

Difference from W-2 Appendix A (recorded, not corrected): the appendix baselines the Base parent `A` for
the peer as well; `StageS1CrAsync` baselines only T. The local path consults no baselines, so this
cannot change the observation, but it is a difference.

### 2.3 Instrument caveats (read before quoting a number)

- **`Default Timeout=0` in Microsoft.Data.Sqlite means "retry forever", not "fail immediately".** It
  hung the test host in this session (hang dump collected and deleted). The fixture documents "never
  pass 0"; the smallest usable window is 1 s.
- **"After how long" is the fixture's timeout, not SQLite's.** Microsoft.Data.Sqlite retries a busy
  statement until the timeout — including `SQLITE_BUSY_SNAPSHOT`, which SQLite itself reports at once
  (observed: ext 517 surfaced only after ~1.1 s). Production's connection string sets no timeout, so it
  runs on the library default (30 s) — **that value was not measured**.
- Legs run on Windows, EF Core 10.0.5, Microsoft.Data.Sqlite as resolved by the test project. Not run:
  non-Windows, non-default `synchronous`, multi-process (all connections here are in one process).
- The stale-read leg's writer A is a raw deferred connection, chosen because EF cannot produce the
  situation. It measures SQLite's stale-read protection, not any production code path.

## 3. Verification

| Check | Result |
|---|---|
| Baseline suite at `f4322fd` before any edit (P0-d) | **970 passed, 0 failed, 1 skipped, 971 total** |
| Suite after this PR | **980 passed, 0 failed, 1 skipped, 981 total** (+8 P0-b, +2 P0-c; no loss) |
| P0-b legs | 8/8 green in the final targeted run and again inside the full suite (WAL and rollback-journal); numbers in §2.1 are from the targeted run, ±~30 ms across the two |
| P0-c legs | 2/2 green on first run |
| Discriminating mutation, P0-b | probe channel `BEGIN IMMEDIATE` → `BEGIN DEFERRED`: both `P0b_LockMode` theories **RED**, restored (`git status` clean of the mutation) |
| Discriminating check, P0-c | built-in: the control (delete B) gets the opposite result on every assertion; `Matches` observed both `true` (before) and `false` (after) |
| Production diff | none (`git diff origin/dev -- SmartStudyPlanner/` empty) |

**Not run:** P0-a's `LuuHocKyAsync` leg; no mutation of production code (test-only PR); non-Windows.

## 4. Follow-ups

1. **P0-a `LuuHocKyAsync` no-change-save leg is still unmeasured** and is named by Slice 3's gate.
   Recommend a small follow-up PR (or fold into Slice 3's baseline commit) before Slice 3 merges.
2. Slice 4 should assert `SQLITE_BUSY` from `BeginTransactionAsync()` propagates (plan §14: rollback ⇒
   rethrow, no swallow) — the failure point is BEGIN, not the first write.
3. Optional: measure production's default 30 s timeout behaviour if the UI-thread blocking (sync
   `Thread.Sleep`-style retry inside Microsoft.Data.Sqlite) matters to Slice 4's UX design. Not
   measured here.
4. The `P0b_ReadThenWrite_*` leg uses timing lower/upper bounds only; if CI proves flaky the 6 s upper
   bound is the first thing to widen.

## 5. Decisions made

### 5.1 Measure the production-relevant journal mode first, and both modes anyway
- **Why it had to be made:** the first draft assumed the default file journal was rollback-mode. The
  first run printed `requested=delete actual=wal`: EF creates WAL files. A measurement in the wrong mode
  would have looked authoritative and been wrong for production.
- **What it's for:** every P0-b leg asserts the journal mode it actually ran in (`ReadJournalMode`) and
  runs in WAL (production) and forced rollback-journal (supplementary).
- **Experience:** read back the property you think you configured; "the default" is a claim to verify.

### 5.2 Re-shape the read-then-write leg around what EF actually does
- **Why:** the planned scenario (two EF transactions each reading, then each writing) cannot run: EF's
  BEGIN is immediate, so the second one blocks at `BeginTransactionAsync()`. That *is* the finding, so
  the legs were rebuilt around it (#3, #4), and the stale-read leg uses a raw deferred reader to reach the
  situation §14 reasons about.
- **Experience:** when a scenario cannot be staged, find out why before working around it — the reason
  can be the measurement.

### 5.3 Do not measure P0-a's `LuuHocKyAsync` leg in this PR
- **Why:** the task's work table names P0-b and P0-c only; adding a third measurement is scope the
  owner did not authorise, and Slice 3 runs in parallel against `LuuHocKyAsync`'s internals.
- **What it's for:** the plan §21 row and README were edited to say Slice 0 is partial, so the false
  "complete" reading is removed without this PR pretending to close it.
- **Experience:** an honest "partial" in the plan is worth more than a stretched "done".

### 5.4 P0-c goes through public repository methods only
- **Why:** Slice 3 extracts `LuuHocKyAsync`'s internals in a parallel PR that merges after this one.
  Calling `TaskCascadeHelper` or a new executor directly would tie the RED baseline to symbols about to
  move.
- **Experience:** characterization tests that must outlive a refactor should sit at the seam the
  refactor promises to preserve.
