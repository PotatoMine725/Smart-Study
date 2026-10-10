# D-5 (V2): a failed non-fence local save no longer leaves unsaved additions behind

**Date:** 2026-10-10 · **Author:** Claude Code agent (owner-dispatched, ticket
`Prompt/2026-10-03-d5-failed-save-restore.md`) · **PR:** <PR> → `dev` (draft) · **Branch:**
`fix-d5-failed-save-restore` (worktree `.claude/worktrees/d5-restore`, from `origin/dev` `9394110`)

Labels: **OBSERVED** (seen in a run in this session) · **FACT** (read in the tree) · **INFERENCE**
(reasoned, not run) · **NOT RUN**.

## 1. Scope

This implements ruling §2 of `docs/specs/2026-10-03-fence-slice4-followup-owner-rulings.md` (D-5, V2),
together with the owner's Phase 0 rulings of 2026-10-10:
- **A:** the fresh read uses a new context. X11's harness may capture only the first context; its
  assertions are unchanged.
- **B (shape K):** an unsaved MonHoc that holds a persisted task is kept, with its pre-call collection,
  minus its unsaved tasks. This is recorded as a dated amendment line under rulings §2.
- **C:** one logging line in `App.xaml.cs`'s generic branch logs the repair failure, mirroring the existing
  `MutationRejected.RestoreFailure` line. No other change to `App.xaml.cs`.

Production changes (3 commits on top of `origin/dev`):
- `LocalSemesterSaveExecutor`
  - `ExecuteAsync` now wraps the whole transaction, BEGIN included.
  - On a failure whose fence decision was not a rejection, `DropUnsavedAdditionsAsync` re-reads persisted
    state on a fresh context with `CancellationToken.None`, then rethrows the original exception.
  - A repair failure goes into `Exception.Data`.
- `SemesterGraphRestorer.TaskCollectionsOf` / `DropUnsavedAdditions`: a membership-only routine. It copies
  no values, so OD-7's `Restore` is untouched and not reused.
- `LocalSaveRejection.RepairFailureDataKey` / `RepairFailureOf`.
- `App.xaml.cs`: +2 lines (ruling C).

Unchanged: `Sync/**`, `SyncStamper`, XAML, schema, both VMs (no reorder was needed, see §2.1), the
fence-rejection path (OD-7).

## 2. Findings

### 2.1 Phase 0 answers (reported 2026-10-10, before the owner gate)

**Failure-point map (FACT, `LocalSemesterSaveExecutor` / `SemesterGraphWriter` at `9394110`).** "Caller's
graph" means the caller's in-memory HocKy/MonHoc/StudyTask objects; the third column is what the save
had already changed there when it failed.

| # | Failure point | Inside the old try? | Caller's graph already changed |
|---|---|---|---|
| F0 | `_ctxFactory()` | no | nothing |
| F1 | `BeginTransactionAsync`: SQLITE_BUSY after the busy timeout surfaces here (P0-b) | **no** | nothing |
| F2–F4 | Load the old graph, plan (validation errors are collected, not thrown), fence read | yes | nothing |
| F5 | Fence rejection | yes | OD-7 full restore already done. **D-5 does not run.** |
| F7 | Duplicate MaMonHoc (before the FK heal) | yes | nothing |
| F8 | Duplicate MaTask (after the FK heal) | yes | FK heals |
| F9 | `TaskCascadeHelper` fails | yes | heals; each new MonHoc's `DanhSachTask` **swapped for an empty one** |
| F10 | Unknown-MonHoc throw in the upsert loop | yes | as F9, and new MonHocs **half refilled** |
| F11 | `SaveChangesAsync` | yes | full writer effect: new MonHocs refilled, including the **repository's** instance of any persisted task moved into one; new entities stamped in memory |
| F12 | `CommitAsync` | yes | as F11. If the commit really landed, the fresh read sees the rows and keeps them. |
| F13 | Cancellation | yes | All production callers pass `default`. Pre-existing: `RollbackAsync(ct)` would throw first. |

- **No half-refilled collection on a persisted row (FACT).** The writer resolves owners through
  `hocKyCu`. For persisted MonHocs those are the repository's instances. Only new MonHocs (the caller's
  instances, added to `hocKyCu`) get their collections swapped or refilled.
- **Create-mode form state (FACT).** Neither command clears its form before the await:
  - `ThemMon` resets `TenMon`/`SoTinChi` after it (`QuanLyMonHocViewModel.cs:142`).
  - `ThemTask` resets its form at the end (`QuanLyTaskViewModel.cs:251–259`).

  So no reorder was needed, and none was proposed.
- **Creating a brand-new semester fails (FACT).** `SetupViewModel.TaoHocKy` keeps `hocKyMoi` in a local,
  not in VM state as the ruling assumed. On failure `OnSetupCompleted` is not called, and the next click
  mints a new id. V2 needs no special case: the fresh read finds no row, so every MonHoc is an addition.
  A fresh semester has none, so in production this is a no-op. A test pins it as OBSERVED.
- **`MoFocusMode` (FACT).** `TaskGoc = task` (`DashboardViewModel.cs:192`) is the graph's own instance,
  and `FocusViewModel.cs:137` adds the minutes to it. That is a scalar edit on a persisted row, so V2
  keeps it.
- **Reparent reachability (FACT).** The only production reparent is the clone fold in
  `LayDanhSachHocKyAsync` (`SqliteHocKyRepository.cs:58`), and its winner is persisted. So shape K is
  not reachable from the UI today.

### 2.2 Throwaway probe (OBSERVED; reverted before any commit)

I applied an unconditional V2 repair to the executor and ran the full suite: **2 tests went red.**
- The Slice 3 half-refilled snapshot: expected, and an allowed change.
- **X11:** the fresh read's second factory call reassigned the captured `failing` variable, so
  `SaveAttempts` read 0 instead of 1. That is test instrumentation, not behaviour. It led to ruling A.

Two further observations from the same run:
- All OD-7 tests stayed green.
- **The two Q-2 characterizations stayed green.** Their `FailNextSaveRepository` throws *before* the
  executor, so no executor-level repair can reach them. Both were re-staged so the failure happens inside
  the executor (via `FailingSaveDbContext`).

DI registers only `SqliteHocKyRepository` (`ServiceLocator.cs:59`), so in production the failure does
reach the executor.

### 2.3 The rule as implemented

On a failed save whose fence decision is absent (the failure came before the fence) or passed:
1. Put back each caller MonHoc's pre-save task collection, undoing the writer's swap.
2. Re-read persisted state with `LoadLiveGraphAsync` on a new context, the same live-only predicate the
   next save's planner uses.
3. Remove each caller MonHoc that has no live row and holds no live task (K). Then remove every task with
   no live row.

No value is copied. FK heals are left as they are: they are what the next save would do anyway.

## 3. Verification

| Run | Result |
|---|---|
| Baseline, `origin/dev` `9394110` | **1109 passed / 1 skipped / 1110 total**, 0 failed (OBSERVED) |
| After `test(d5)` commits, before the fix | 9 red, all assertion failures on behaviour, no setup errors (OBSERVED) |
| HEAD `4305bd7` (code at `f158081` + test tightening) | **1117 passed / 1 skipped / 1118 total**, 0 failed; build 0 errors, no warning in a changed file (OBSERVED) |

There are 8 new tests in `LocalSemesterSaveFailedSaveRepairTests`, plus these existing tests:
- 2 flipped Q-2 tests (`EditModeSaveFailureTests`, renamed to the ruled behaviour).
- 1 snapshot moved to the ruled state (own commit).
- 1 harness-only change (X11).

No other existing test was edited.

### 3.1 Mutants (each applied alone, full suite run, then reverted with `git checkout`; tree clean afterwards)

| Mutant | RED (OBSERVED) |
|---|---|
| M1: skip the repair | 9: generic, BEGIN, create edge, K, repair failure, fresh-read, snapshot, both VM flips |
| M2: V1, full `Restore` instead of removal | 6, among them **`FocusMinutes_Survive…`** and the edit/deletion test (the "V2 keeps edits" pins) |
| M3: repair on the fence path too | 1: `FenceRejection_DoesNotRunTheRepair` (see note) |
| M4: no shape K | 1: `PersistedTaskMovedIntoUnsavedMonHoc_…` |
| M5: repair only once BEGIN succeeded | 1: `BusyAtBegin_TheRepairStillRuns` |
| M6: no try/catch around the repair | 1: `RepairFailure_TheOriginalExceptionSurfaces_…` |
| M7: no collection-swap undo | 3: K, fresh-read, snapshot |
| M8: `RepairFailureOf` finds nothing | 1: `RepairFailure_…` |
| M9: decide persistence from the failed transaction's old graph (ids copied right after the load, before the writer touches it; the fresh-read context is still opened) | 2: `RowsCommittedElsewhereBeforeTheRepair_…` (the target) and `BusyAtBegin_…` (no old graph exists at BEGIN, so the mutant drops the persisted MonHoc wholesale) |

M9's second red showed that `BusyAtBegin_…` did not assert that persisted rows **survive**. The test was tightened in
this PR (the persisted MonHoc stays, its task stays) before the final run.

**Note on M3: an acceptance item changed, needs owner ack (OBSERVED).** The ticket expected "repair on
fence path too ⇒ OD-7 tests RED". **No existing OD-7 test goes red.** After a successful OD-7 restore,
the caller's graph already equals persisted state, so a V2 pass removes nothing, and graph-shape
assertions cannot tell the two apart. The new test counts contexts instead: one per rejected save, plus no
`RepairFailure` entry. It is the only signal for that mutant.

## 4. NOT RUN

- The `App.xaml.cs` line (ruling C). The WPF handler has no test harness, and there is **no practical manual
  check**: the shipped app has no way to make the save fail *and* the repair fail on demand. The residual risk is
  two lines. Its helper `RepairFailureOf` is covered (M8); the call site was only read, not run.
- Real SQLITE_BUSY at BEGIN with a second process holding the write lock. It was simulated with a
  `DbTransactionInterceptor` throwing `SqliteException(…, 5)` at `TransactionStartingAsync`.
- What the UI shows after the repair. `ThemTask` ran `TinhDiemVaSapXep`/`OnRefreshGrid` before the
  await; the removal goes through the bound `ObservableCollection`. `HasData` is not recomputed on
  failure (status quo for any failed save).
- The commit-landed-but-`CommitAsync`-threw case (F12) was not staged. The INFERENCE that the fresh read
  keeps those rows follows from the fresh-read test.

## 5. Follow-ups (non-blocking)

- **Pre-existing:** `RollbackAsync(ct)` in the transaction catch. A cancelled token makes the rollback throw,
  and that exception replaces the original. The D-5 repair still runs (it sits outside), but attaches to
  the replacement. No production caller passes a token.
- **Shape K and in-memory stamps:** after an F11 failure, the kept unsaved MonHoc carries the stamps
  SaveChanges put on it in memory. The next save adds it again; not observed to matter, not tested.

## 6. Decisions made

### 6.1 Wrap the whole transaction, not just its try (agent decision, reported at Phase 0)
- **Why:** the ruling's motivating transient failure, SQLITE_BUSY, surfaces at BEGIN, which was outside
  the try. A repair inside the rollback catch would miss exactly that case.
- **What for:** every non-fence failure gets the same repair.
- **Experience:** M5 shows the BEGIN test is the only signal for this placement.

### 6.2 Tell rejections apart by their decision, not their type (agent decision)
- **Why:** `catch (Exception e) when (e is not MutationRejectedException)` matches N-10's scanner.
- **What for:** the executor records the fence decision, and the repair runs unless that decision was a
  rejection. N-10 stays true with its empty allowlist. The rejection predicate (`Passed`) is now shared
  with `ApplyIfPassedAsync`.

### 6.3 A fresh read on a new context, through the same live-only predicate (ruling §2 + owner A)
- **Why:**
  - The failed context's connection may be the problem.
  - The failed transaction's old graph is stale if another save committed in between.
  - Reusing `LoadLiveGraphAsync` keeps "persisted" equal to what the next save's planner treats as
    existing.
- **What for:** the next save never re-inserts or duplicates.
- **Experience:** the second factory call broke X11's instrumentation, which the probe found before any
  code was written. Owner ruling A authorised the harness change.

### 6.4 Undo the writer's collection swap from a pre-save snapshot (agent decision, needed by K)
- **Why:** the writer swaps each new MonHoc's collection, refilling it with the repository's instances of
  persisted tasks. A kept MonHoc must hold the caller's own instances and the collection the UI is bound
  to.
- **What for:** the writer never mutates the collection it swapped out, so restoring the reference undoes
  the swap exactly. That also covers a MonHoc another save committed in the meantime, which the fresh
  read keeps (M7 makes 3 tests red).

### 6.5 Shape K: keep, don't re-home (owner B, 2026-10-10)
- **Why:** removing the MonHoc would leave the persisted task in no collection, and the next save would
  tombstone it. Re-homing would discard the move (an FK edit), and the ticket's STOP rule forbids
  discarding a scalar edit.
- **What for:** no persisted row is ever lost because of the repair.

### 6.6 Repair failures are logged by the global handler (owner C, 2026-10-10)
- **Why:** the generic handler logs `ex.ToString()`, which omits `Exception.Data`. Attach-only would have
  been invisible.
- **What for:** the original exception still wins; the repair failure reaches `crash.log` next to it.
