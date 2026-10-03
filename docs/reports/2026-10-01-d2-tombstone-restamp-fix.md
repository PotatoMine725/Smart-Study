# D-2: a local save no longer re-stamps rows that are already tombstoned

**Date:** 2026-10-01 · **Author:** Claude Code agent (owner-dispatched, ticket
`Prompt/2026-09-30-d2-tombstone-restamp-fix.md`) · **PR:** #105 → `dev` · **Branch:**
`fix-d2-tombstone-restamp` (worktree `.claude/worktrees/d2-restamp`, from `origin/dev` `5d80eee`)

Labels: **OBSERVED** (seen in a run in this session) · **FACT** (read in the tree) · **INFERENCE**
(reasoned, not run) · **NOT RUN**.

## 1. Scope

Defect D-2 (fence plan §4.2, §19 R-9; owner approval 2026-09-30 as a separate PR after Slice 3).
A local semester save re-stamped every tombstone in the semester. Each save bumped `Rev`, `DeletedAtUtc`,
`ModifiedAtUtc` and `ModifiedByDeviceId` on every dead MonHoc, task, note and link.

Authority: Slice-2 owner rulings §1 (M-3/A-1). The effective cascade predicate is
`child.IsDeleted == false`. §1.2 (D9-T1): a tombstoned TaskNote still occupies `UNIQUE(MaTask)`.

Production diff, 2 files:
- `Mutations/LocalSemesterSaveExecutor.cs`: the old-graph load uses a filtered `Include` on
  MonHoc and StudyTask (`!IsDeleted`).
- `TaskCascadeHelper.cs`: the note and link queries add `&& !IsDeleted`.

Not touched: `Sync/**`, `SyncStamper`, `AppDbContext` (no index or schema change), VMs, XAML,
`SemesterGraphWriter`, `SqliteStudyTaskRepository`.

## 2. Findings

### 2.1 Preconditions — OBSERVED

1. Slice 3 is merged. `origin/dev` = `5d80eee` (PR #103).
2. Slice 3's `SemesterSaveRegressionSnapshotTests.NoChangeSave_OverAlreadyTombstonedRows_Observed`
   pinned the re-stamp on a no-change save: `Rev` 2→3, `Deleted@t3`→`t4` on Ly, L1, T2, note(T2)
   and LinkT2. D-2 exists.

### 2.2 Baseline and impact — OBSERVED

- **Baseline.** Fresh worktree at `5d80eee`, before any edit: 1010 passed, 0 failed.
- **GitNexus `impact` (upstream, index `Smart-Study`):**
  - `TaskCascadeHelper.RemoveChildrenAsync`: **HIGH**. 3 direct callers: `SemesterGraphWriter.ApplyAsync`, `SqliteStudyTaskRepository.DeleteAsync` and the P0-a leg-1 probe test. 1 process, the local save.
  - `LocalSemesterSaveExecutor.ExecuteAsync`: GitNexus reported **0** callers.
- **Grep cross-check:** `ExecuteAsync` is called by `SqliteHocKyRepository.LuuHocKyAsync`, so every VM save reaches it. This is another GitNexus miss.
- All callers are inside the ticket's scope. The HIGH warning was raised to the owner before editing.

### 2.3 Design choice: A (live-only load)

Chose **A**: a filtered `Include` in the executor, plus the helper filter.

**B (guard at the write) was rejected.** Under B the unfiltered load puts dead tasks into a live
MonHoc's tracked navigation. Removing that MonHoc makes EF's cascade fixup mark them Deleted, and
`SyncStamper` re-stamps them. A guard on the explicit `Remove()` calls never sees that path. B would
also need to detach dead tasks before every MonHoc removal. Under A they are never tracked.

The three questions the ticket requires:

1. **Does A change what cascade fixup reaches when a *live* MonHoc is removed?** No.
   - The filter drops only `IsDeleted` rows, so every live task of a live MonHoc is still loaded and
     still reached. The dead ones are not.
   - **OBSERVED**, `DeleteLiveMonHoc_TombstonesLiveChildren_LeavesDeadChildrenUntouched`:
     - Toan, T1, note(T1), link(T1) and T3 are tombstoned at the save's tick and device.
     - Previously-dead T2 and its note/link, and T3's dead note/link, are byte-identical.
   - The existing `DeleteMonHoc_WithThreeTasksNotesAndLinks_XoaMon` snapshot is unchanged and green.
2. **Can the incoming graph contain an id that exists only as a tombstone?** Not through any path
   today. Each source of the incoming graph (code paths read; FACT for each cited line, INFERENCE for the runtime conclusion):
   - Load: `LayDanhSachHocKyAsync` filters dead HocKy, MonHoc and StudyTask
     (`SqliteHocKyRepository.cs:29-31`). Load-time dedup only merges live clones.
   - `ThemTask` and new MonHocs mint fresh Guids (`QuanLyTaskViewModel.cs:192`, `new StudyTask(...)`).
   - Instances (INFERENCE from reading the navigation code, including frame back-navigation; not run): every page gets the same `HocKy` instance per semester (`MainWindow` → `DashboardPage` →
     `QuanLyMonHocPage`/`QuanLyTaskPage`). `SetupPage` is created once (`MainWindow.xaml.cs:49`) and
     loads once. The background deadline scan reloads, but only reads.

   **Residual (INFERENCE, NOT RUN).** A row could still be tombstoned outside the UI's graph while
   the UI holds it, for example by a future sync apply or by `SqliteStudyTaskRepository.DeleteAsync`
   (zero production callers, OD-5).
   - With A, the next save would plan it as new, call `Add`, hit a primary-key UNIQUE violation and
     roll back. For a MonHoc, the caller's task collection would be replaced first (the known Slice-3
     half-refill behaviour).
   - Before D-2 the same case was silent: `CopySyncSafeValues` kept the row dead.
   - This is the stale-UI-graph case (R-9 D-4) and belongs to the Slice-4 fence, not this ticket.
3. **`hocKyCu` itself tombstoned.** Out of scope and unchanged (INFERENCE, NOT RUN).
   - The HocKy lookup has no `IsDeleted` filter, so a dead semester is still found.
   - The save then takes the update path, and `CopySyncSafeValues` keeps it dead.
   - Its children are now loaded live-only, so its dead children are no longer re-stamped.
   - The UI cannot reach this case: `LayDanhSachHocKyAsync` never returns a dead semester.

### 2.4 Other behaviour that changes — INFERENCE, NOT RUN

- **Live task under a dead MonHoc.** This is an inconsistent state that no current path produces.
  Before D-2 the re-removal of the dead MonHoc cascaded into such a task. Now neither the MonHoc nor
  the task is loaded, so the task is left as it is.
- **No backfill.** Tombstones already re-stamped in existing databases cannot recover their original
  `Rev`, `DeletedAtUtc` or device provenance; the values are overwritten and not stored anywhere else.
  Sync has never run, so no peer holds the originals and nothing diverges.

### 2.5 Residuals recorded, not fixed (out of scope)

- **`SqliteStudyTaskRepository.DeleteAsync` on an already-dead task** still re-stamps the task row
  itself: its lookup (`:56`) has no `IsDeleted` filter, and the file is not in MAY-edit. It has zero
  production callers. Its children are now skipped when dead (OBSERVED, new test).
- **Stale doc comment, `ImpactResolver.cs:35-41`.** It still says the local path "has no `IsDeleted`
  filter and re-stamps an already-tombstoned child". `Sync/**` is on the MUST-NOT-edit list; this
  needs a follow-up doc touch.
- **`detect_changes` — OBSERVED.** The worktree was indexed with `npx gitnexus analyze` (its
  side-effect edits to `AGENTS.md`, `CLAUDE.md` and `.claude/skills/gitnexus/*` were reverted, so the
  tree is clean). Then `detect_changes(scope: compare, base_ref: origin/dev)` on the worktree was run.
  - Production symbols changed: only `LocalSemesterSaveExecutor.ExecuteAsync` and
    `TaskCascadeHelper.RemoveChildrenAsync`, plus their containing class and namespace nodes.
  - Everything else is test methods and doc sections.
  - Affected flows: 8, all `ExecuteAsync → …`, which is the local save.
- **Stale references left untouched** (existing tests outside the two allowed flips, frozen specs, or
  `Sync/**`). Each now describes pre-D-2 behaviour:
  - `SmartStudyPlanner/Sync/Fence/ImpactResolver.cs:35-41`: the local path "has no `IsDeleted` filter and
    re-stamps".
  - `Tests/Sync/Fence/ImpactCascadeLivenessTests.cs:29`: the helper "may re-stamp its `Rev`/provenance".
  - `Tests/…/Mutations/SemesterReconcilePlannerTests.cs:317-321`
    (`TombstonedRowsInOldGraph_ArePlannedAsDeletesAgain_Observed`):
    - Its comment says the executor loads with "no IsDeleted filter" and cites the old snapshot name.
    - The planner itself is unchanged, so the test still passes.
    - The executor no longer hands it dead rows.
  - `docs/specs/2026-09-17-fence-slice2-owner-rulings-m3-h1.md:42`: a frozen table row records the
    local path as "unfiltered / re-stamps". It was correct when ruled.
  - The old test name `NoChangeSave_OverAlreadyTombstonedRows_Observed` (renamed to `…_WritesNothing`
    by this PR) is still cited as P0-a evidence in fence plan §21 row 0 and in
    `docs/reports/2026-09-30-epic2-fence-s3-extract.md:95`. The R-9 amendment line records the rename.

## 3. Tests

New file `Tests/Infrastructure/Persistence/SQLite/Mutations/TombstoneRestampTests.cs`. Every save runs
under its own device id and clock tick, so a re-stamp cannot hide behind equal values.

| Test | Red on `5d80eee`? |
|---|---|
| `NoChangeSave_LeavesTombstonesByteIdentical` (Task, MonHoc, TaskNote, TaskReferenceLink: `Rev`, `DeletedAtUtc`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`) | RED |
| `DeleteLiveMonHoc_TombstonesLiveChildren_LeavesDeadChildrenUntouched` (includes a live task with a dead note/link, the save-path leg for the helper filter) | RED |
| `D9T1_DeadNoteStillOccupiesUniqueMaTask_AfterSave` (real `UNIQUE constraint failed: TaskNotes.MaTask`, plus a control insert that succeeds) | green: it guards the invariant the fix must keep, and goes RED under the hard-delete mutant M4 |
| `StudyTaskRepositoryDeleteAsync_SkipsAlreadyDeadNote_TombstonesLiveLink` (OD-5 path) | RED |

**Intentional flip** (its own commit). Before editing any existing test, the full suite was run with
the fix applied. Exactly two existing tests went red; no other existing test changed:
- `SemesterSaveRegressionSnapshotTests.NoChangeSave_OverAlreadyTombstonedRows_Observed` now expects
  an empty diff. It is renamed to `_WritesNothing`, and its comment cites D-2 and this ticket.
- `CascadePredicateProbeTests.P0a_Leg1_LocalTaskCascade_OverAlreadyTombstonedNote_ObservedBehaviour`
  now expects the dead note unchanged. The class doc states that D-2 aligned both paths.

### 3.1 Mutants (each reverted; tree clean afterwards) — OBSERVED

| # | Mutant | Red tests |
|---|---|---|
| M1 | Helper filter removed (note and link) | `DeleteLiveMonHoc_…`, `StudyTaskRepositoryDeleteAsync_…`, probe leg 1 |
| M2 | MonHoc load filter removed | `NoChangeSave_LeavesTombstonesByteIdentical`, snapshot `NoChangeSave_…_WritesNothing` |
| M3 | Task load filter removed | `NoChangeSave_…`, `DeleteLiveMonHoc_…`, snapshot `NoChangeSave_…_WritesNothing` |
| M4 | Helper hard-deletes the note (`ExecuteDeleteAsync`) instead of tombstoning it | `D9T1_…`, `NoChangeSave_…`, `DeleteLiveMonHoc_…` |

## 4. Verification — OBSERVED

- `dotnet test SmartStudyPlanner.Tests`: baseline 1010 passed / 0 failed. With the fix and before
  the flip: 1012 passed / 2 failed / 1 skipped, and the 2 failures are the two expected rows. After
  the flip: **1014 passed / 0 failed / 1 skipped / 1015 total** (TRX counters; = baseline + 4 new). The
  baseline was read from the rtk summary (1010 passed / 0 failed), which does not show skips.
- Build clean (warnings only, pre-existing).
- **NOT RUN:** the app itself (no UI change), CI (runs on the PR), and the inferences in §2.3 Q2/Q3
  and §2.4.

## 5. Follow-ups

- Doc-only touch for the stale references listed in §2.5: `ImpactResolver.cs`,
  `ImpactCascadeLivenessTests.cs` and the `SemesterReconcilePlannerTests` comment. Each is outside this
  ticket's MAY-edit list.
- `SqliteStudyTaskRepository.DeleteAsync`: filter the task lookup on `!IsDeleted` (OD-5 owner).
- Stale-graph zombie id: a save now fails loudly instead of silently. Slice 4 / D-4 owns it.
- D-1 (`UpsertNoteAsync` over a dead note) is still open, as a separate ticket.

## 6. Decisions made

### 6.1 Live-only load (A) instead of a write-side guard (B)
- **Why it had to be made.** The ticket required a choice between A and B, and B cannot see EF's
  cascade fixup, which re-stamps dead tasks loaded under a removed MonHoc.
- **What it's for.** The local path now applies the ruled predicate at the point where it reads, the
  same way `SyncApplySession.CascadeTombstoneAsync` and `LayDanhSachHocKyAsync` already do. Old and
  incoming graphs are now the same view, live-only.
- **Experience.** When EF navigation fixup is involved, filter what gets *tracked*, not what gets
  *removed*. Guards on explicit `Remove()` calls miss the implicit cascade.

### 6.2 Accept "loud failure" for a stale zombie id
- **Why it had to be made.** Under A, a tombstoned id in the incoming graph turns into an `Add` and a
  PK violation. Under the old code it was a silent no-op.
- **What it's for.** Reading the code paths shows that no current path feeds a dead id into a save, because all pages
  share one graph instance. A failed, rolled-back save is safer than silently writing to a tombstone.
  The general fix belongs to the fence (Slice 4 / D-4).
- **Experience.** Before making a load stricter, check where the incoming graph comes from. "Same
  instance shared across pages" is the property that makes A safe; if that ever changes, revisit
  this.

### 6.3 Test that guards an invariant, not the defect
- **Why it had to be made.** D9-T1 already holds today, so a red-first test for it is impossible.
- **What it's for.** It stops a later "cleanup" from hard-deleting dead notes. M4 shows that it can
  go red.
- **Experience.** When a test can't be red first, prove it can go red with a mutant, and say so.
