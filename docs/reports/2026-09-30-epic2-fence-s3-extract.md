# Epic 2 / T2.4 fence — Slice 3: behaviour-preserving extraction of `LuuHocKyAsync`

**Date:** 2026-09-30 · **Author:** Claude Code agent (owner-dispatched, card A3) · **PR:** #103 →
`dev` · **Branch:** `refactor-epic2-fence-s3-reconcile-extract` (worktree `.claude/worktrees/fence-s3`,
from `origin/dev` `f4322fd`)

Labels: **OBSERVED** (seen in a run in this session) · **FACT** (read in the tree) · **INFERENCE**
(reasoned, not run) · **NOT RUN**.

## 1. Scope

Plan: [`../plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md`](../plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md)
§12.2, §13, §15, §19 R-1, §21 row 3, §24.2 card A3. Owner ruling OD-2 = L1.

`SqliteHocKyRepository.LuuHocKyAsync` is split into `SemesterReconcilePlanner` (pure diff),
`SemesterGraphWriter` (the same tracker writes, same statement order, no `SaveChanges`) and
`LocalSemesterSaveExecutor` (context, transaction, load, plan, write, save, commit/rollback), all under
`Infrastructure/Persistence/SQLite/Mutations/`. `LuuHocKyAsync` is a one-line delegate. The
`IHocKyRepository` port is unchanged.

Not in this slice: the fence, any VM change, OD-7 restoration, any fix to the behaviours §4 labels
"not desired".

## 2. Findings

### 2.1 Baseline (P0-d) — OBSERVED

Fresh worktree at `f4322fd`, before any edit: build 0 errors / 96 warnings; suite **971 total, 970
passed, 0 failed, 1 skipped** (TRX counters; the TRX's one `NotExecuted` result is
`SoeBaselineCaptureTests.CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing`). Local `dev` = `origin/dev` = `f4322fd` (0/0); the owner's
checkout was dirty and was not touched.

### 2.2 Impact analysis (GitNexus, index `Smart-Study`) — OBSERVED

| Symbol | Risk | Direct callers | Processes |
|---|---|---|---|
| `LuuHocKyAsync` | HIGH (priced, R-1) | 19: 8 methods in 4 VMs + 11 tests | 0 |
| `CopySyncSafeValues` | HIGH | 1 (`LuuHocKyAsync`) | 0 |
| `TaskCascadeHelper.RemoveChildrenAsync` | HIGH | 2 (`LuuHocKyAsync`, `SqliteStudyTaskRepository.DeleteAsync`) | 0 |

Production callers: `SetupViewModel.TaoHocKy`; `QuanLyMonHocViewModel.ThemMon`/`XoaMon`;
`QuanLyTaskViewModel.ThemTask`/`XoaTask`/`HoanThanhTask`; `DashboardViewModel.LuuDuLieu`/`MoFocusMode`.
No caller or process outside those 4 VMs and tests.

**GitNexus missed three callers**: `SoftDeleteReadPathTests.cs` lines 40, 77, 104 call
`LuuHocKyAsync` and are not in the graph's caller list. Grep found them. They are tests only, and they
pass unchanged, but the graph's "19" is an undercount (plan R-11).

### 2.3 Statement-order map of the pre-extraction `LuuHocKyAsync` — FACT

Line numbers are `SqliteHocKyRepository.cs` at `f4322fd`. The last column says where each step lives now.

| # | Line | Statement | Now in |
|---|---|---|---|
| 0 | 83 | return if `hocKy` is null | executor |
| 1 | 94–95 | create context, begin transaction | executor |
| 2 | 98–101 | load `hocKyCu` + MonHocs + Tasks (no `IsDeleted` filter) | executor |
| 3a | 105 | `hocKyCu == null`: `db.HocKys.Add(hocKy)`, go to 14 | writer (`plan.IsCreate`) |
| 3b | 109 | `CopySyncSafeValues(hocKyCu, hocKy)` | writer |
| 4 | 111–112 | `newMonById` (`ToDictionary`, throws on duplicate id); `oldMonList` | planner detects, writer throws here |
| 5 | 118–121 | FK heal on the incoming graph | planner computes, writer applies here |
| 6 | 133–134 | old/new task dictionaries (`ToDictionary`, throws on duplicate id) | planner detects, writer throws here |
| 7 | 142–150 | reparent loop: `oldOwner.DanhSachTask.Remove`, `oldTask.MaMonHoc =` | writer, over `plan.TaskReparents` |
| 8 | 151 | `DetectChanges()` | writer |
| 9 | 153–164 | removed MonHocs: `hocKyCu.DanhSachMonHoc.Remove`, `db.MonHocs.Remove` | writer, over `plan.MonHocDeletes` |
| 10 | 166–181 | incoming MonHocs: new ⇒ reset `DanhSachTask`, add, `db.MonHocs.Add`; else `CopySyncSafeValues` | writer, keyed by `plan.MonHocAdds` |
| 11 | 183–189 | removed tasks: `RemoveChildrenAsync`, `db.StudyTasks.Remove` | writer, over `plan.TaskDeletes` |
| 12 | 191–195 | incoming tasks: live owner lookup on `hocKyCu.DanhSachMonHoc`, or throw | writer (live), over `plan.TaskUpserts` |
| 13 | 197–207 | existing ⇒ `CopySyncSafeValues` + `Contains` guard + add; new ⇒ add + `db.StudyTasks.Add` | writer (guard live) |
| 14 | 211–212 | `SaveChangesAsync`, `CommitAsync` | executor |
| 15 | 214–218 | any exception ⇒ `RollbackAsync`, rethrow | executor |

One ordering difference exists and is not observable: the planner now runs between step 2 and step 3b,
where before its computations were interleaved with 3b–6. The planner reads only the two graphs,
mutates neither, and never throws for a planned error, so the writer still reaches every mutation in
the original order (INFERENCE, supported by §2.5).

### 2.4 Caller-visible side effects on the incoming graph — OBSERVED, pinned in `SemesterSaveRegressionSnapshotTests`

All of these are pinned **as observed, not as desired**. None is changed by this slice.

| # | Effect | Pinned by |
|---|---|---|
| 1 | FK heal: a task with `Guid.Empty` gets its navigation owner's id written onto the caller's object (existing-semester branch only) | `TaskWithEmptyFk_IsHealedToItsNavigationOwner` |
| 2 | Create branch: the caller's whole graph is tracked and stamped (`Rev == 1`); the empty FK is filled by EF fixup, not by the explicit heal | `CreateSemester_AttachesWholeIncomingGraph` |
| 3 | New MonHoc: the caller's `DanhSachTask` is replaced by a new collection instance and refilled in incoming order | `AddMonHocWithTasks_ToExistingSemester` |
| 4 | Reparent into a new MonHoc: the caller's new MonHoc ends up holding the **repository's** tracked task instance (`Rev == 2`), not the caller's object (`Rev == 1`) | `ReparentTask_IntoNewMonHoc_CallerGraphHoldsRepositoryInstance_Observed` |
| 5 | New task under an existing MonHoc: the task object is tracked and stamped; the caller's collection is untouched | `AddTask_ToExistingMonHoc` |
| 6 | Existing rows: the caller's objects keep their stale `Rev` | `EditTaskFields_BumpsOnlyThatRow` |
| 7 | Unknown-MonHoc failure: DB unchanged, but the caller's graph keeps the heal, both new MonHocs have a replaced collection, the one iterated before the bad task is refilled and the one after is left **empty** | `TaskPointingToUnknownMonHoc_Throws_DbUnchanged_CallerGraphHalfRefilled_Observed` |
| 8 | Duplicate `MaMonHoc`: `ArgumentException`, DB unchanged, heal **not** applied. Duplicate `MaTask`: `ArgumentException`, DB unchanged, heal **applied** | `DuplicateMaMonHoc_…_BeforeHeal_…`, `DuplicateMaTask_…_AfterHeal_…` |

P0-a (the plan's original question): **a no-change save re-stamps rows that are already tombstoned.**
Each such MonHoc, task, note and link gets `Rev + 1` and a new `DeletedAtUtc`/`ModifiedAtUtc` on every
save of that semester (`NoChangeSave_OverAlreadyTombstonedRows_Observed`). Cause (FACT): the old graph is
loaded without an `IsDeleted` filter, so a tombstoned row is "in the old graph, absent from the new"
again. This is the plan's R-9 D-2 "re-stamp churn"; it is pinned, not fixed. **This observation
confirms defect D-2.** Its fix is a separate owner ticket after this PR and is not part of Slice 3
(owner direction, 2026-09-30). The same test closes P0-a's `LuuHocKyAsync` leg in plan §21 Slice 0.

One thing the first run of the oracle taught: which clone `LayDanhSachHocKyAsync` keeps follows the
DB's row order over random Guid keys, so the surviving clone differs between runs. The clone-merge
snapshot reads the survivor off the loaded graph instead of assuming one.

### 2.5 Mutant table — OBSERVED

Each mutant was applied by script, the snapshot tests run, and the file restored; `git status
--porcelain` showed no unstaged change after each campaign. Phase 1 target: `SqliteHocKyRepository.cs`
(unrefactored). Phase 2 target: `SemesterGraphWriter.cs`.

| Mutant | Phase 1 (unrefactored) | Phase 2 (writer) | Tests that went red (identical in both phases) |
|---|---|---|---|
| M1 drop FK heal | RED | RED | `TaskWithEmptyFk_IsHealedToItsNavigationOwner`, `DuplicateMaTask_…_AfterHeal_…`, `TaskPointingToUnknownMonHoc_…` |
| M2 reparent loop moved after MonHoc removal | RED | RED | `ReparentTask_OutOfMonHocDeletedInTheSameSave_SurvivorIsNotCascaded`, `CloneMergeSave_AfterLayDanhSachDedup` |
| M3 drop `RemoveChildrenAsync` | RED | RED | `DeleteTask_WithNoteAndLinks_XoaTask`, `DeleteMonHoc_WithThreeTasksNotesAndLinks_XoaMon`, `NoChangeSave_OverAlreadyTombstonedRows_Observed` |
| M4 drop the `DanhSachTask` reset | RED | RED | `AddMonHocWithTasks_ToExistingSemester`, `ReparentTask_IntoNewMonHoc_…`, `TaskPointingToUnknownMonHoc_…` |
| M5 `DetectChanges()` moved after the removal loop | **green — survives** the 16 snapshot tests and the full suite (986 passed / 1 skipped at that point) | **green — survives** the snapshot tests and the full suite | none |

M5 is the E6 report's surviving mutant
([`2026-08-20-e6-cascade-coverage-test.md`](2026-08-20-e6-cascade-coverage-test.md) §3.3) and it still
survives. The `DetectChanges()` call is kept, in the same position. A surviving mutant says the suite
does not cover the line, not that the line is dead.

### 2.6 Design as built

Planner output (`SemesterReconcilePlan`): `IsCreate`, `FkHeals`, `TaskReparents`, `MonHocDeletes`,
`MonHocAdds`, `MonHocUpdates`, `TaskDeletes`, `TaskUpserts`, `ValidationErrors` (each with `Kind`,
`Stage`, ids, `Message`). Owner-approved on 2026-09-30 before Phase 1.

- **FK heal**: the planner computes it and diffs on the effective FK without touching the incoming
  graph; the writer writes it at step 5.
- **Validation errors are data; the writer throws at the original position.** Duplicate `MaMonHoc` ⇒
  stage `BeforeFkHeal`; duplicate `MaTask` ⇒ `AfterFkHeal`; unknown MonHoc ⇒ `TaskUpsert`. For the first
  two the writer throws `ArgumentException` from the plan's error. For the third the writer keeps the
  original live owner lookup and throws `InvalidOperationException` with the original message when it
  reaches the task; the planner's `UnknownMonHoc` error is the same fact as data for Slice 4.
- **Live checks kept in the writer** because they depend on EF fixup state: the owner lookup on
  `hocKyCu.DanhSachMonHoc`, and `owner.DanhSachTask.Contains(oldTask)`.

## 3. Verification

| Check | Result |
|---|---|
| `rtk dotnet build` | 0 errors, 96 warnings (same as baseline) — OBSERVED |
| Full suite after all three commits | **1001 total, 1000 passed, 0 failed, 1 skipped** (TRX) = baseline 970 + 16 snapshot + 14 planner — OBSERVED |
| Full suite after rebasing onto `origin/dev` `62c8042` (PR #102 merged) | **1011 total, 1010 passed, 0 failed, 1 skipped** (TRX) = 1001 + #102's 10 tests — OBSERVED. Commit SHAs cited in this report are pre-rebase; `67505fb`/`033b89b`/`2920804`/`cd99f72`/`01ca7b4` became `7170e41`/`8850986`/`fe9afad`/`f89e453`/`7a34354` |
| Existing test files changed | none. `git diff --stat origin/dev...HEAD` lists 10 files: the 2 new test files, the 3 new `Mutations/` files, `SqliteHocKyRepository.cs`, and 4 docs files — OBSERVED |
| `SemesterSaveRegressionSnapshotTests` between commit 1 and commit 2 | unchanged, green on both — OBSERVED |
| `SemesterSaveRegressionSnapshotTests` after the §3.1 amendment | green on HEAD **and** green with `SqliteHocKyRepository.cs` restored to its unrefactored content from `67505fb` (file then checked out back) — OBSERVED |
| Mutants | §2.5 |
| Schema diff / new packages | none (no `AppDbContext`, migration or csproj change) — FACT |
| `gitnexus_detect_changes()` | **NOT RUN meaningfully** — see below; acceptance item met by a substitute, not by the tool |

### 3.1 Amendment to the oracle after the extraction (disclosed)

The plan says the snapshot file is "pinned before the refactor, unchanged after". It was changed once
after commit 2, in its own test-only commit: the first version snapshotted five tables and missed
`StudyLog`, the sixth `ISyncMetadata` entity. The amendment adds `StudyLogs` to the snapshot and seeds
one log under a task deleted by `XoaTask` and one under a task deleted by `XoaMon` (which shifts the
clock ticks in those two tests by one). It is additive, and it was run against the unrefactored
repository code as well as HEAD (table above); M1–M4 were re-run against the writer and are still RED.
What it pins (OBSERVED, not desired): **a deleted task's `StudyLog` rows are not tombstoned** — they
stay live under a tombstoned task, both before and after the extraction.

### What was not run

- `gitnexus_detect_changes` could not see this worktree: the index is bound to the owner's checkout and
  returned "No changes detected" for staged changes here. The acceptance condition is met by the diff
  instead (FACT): the only modified production file is `SqliteHocKyRepository.cs`
  (`LuuHocKyAsync` → delegate, `CopySyncSafeValues` removed), plus three new files. The index was not
  re-analyzed from the worktree, to avoid registering a second copy of the repo in the owner's GitNexus.
  code-review-graph's `detect_changes_tool` was also tried with `repo_root` = this worktree and
  `base` = `origin/dev`: it saw the 10 changed files but reported 0 changed functions, because it has no
  graph for the worktree either. Uninformative, not a pass.
- No manual run of the WPF app. The four VMs are exercised only through the existing test suite.
- P0-b and P0-c are not part of this slice; they merged with PR #102 before this branch was rebased.
- No snapshot topology for a `MonHoc` arriving with a wrong `MaHocKy`, or for duplicate ids in the
  create branch.
- **Known difference, not pinned, ACCEPTED by owner ruling (§5.5) — not run: a `null` entry inside a
  `DanhSachTask`.** The
  old code healed the tasks before the null and then threw `NullReferenceException` inside the heal
  loop (step 5), leaving those heals on the caller's graph. The planner now hits the null first and
  throws before the writer runs, so no heal is applied. Same exception type, DB unchanged in both; the
  caller-graph state on that failure path differs. No VM puts a null into these collections (FACT for
  the 8 call sites' own code paths; not proven for every binding). Raised as **Q-S3-1**; closed by the
  owner ruling in §5.5.

## 4. Follow-ups

1. **Slice 4 — `RemoveChildrenAsync` reads the DB inside the writer.** Plan §13 describes the writer as
   "tracker writes", but note/link tombstones are discovered by two queries at write time. The pure
   planner therefore cannot list them: `plan.TaskDeletes` is the only handle on them. Slice 4's impact
   resolution has to derive the note/link impact from the task-delete intents before the writer runs.
2. **Slice 4 — validation position.** The unknown-MonHoc error is available in the plan before any
   write, but the throw is still late (step 12) to preserve effect 7. Moving it ahead of the fence is a
   caller-visible behaviour change and needs to be decided there, together with OD-7 restoration.
3. **Effects 4 and 7, and the P0-a re-stamp** are pinned as OBSERVED. Slice 4 / OD-7 owns effects 4 and
   7; the re-stamp confirms plan R-9 defect D-2, whose fix is a separate ticket after this PR, not part
   of Slice 3. Any fix must update the `_Observed` tests deliberately.
4. **M5** remains an uncovered line (E6 follow-up 3, still open).
5. **GitNexus**: the three missed callers (§2.2), and the index needs `npx gitnexus analyze` after merge
   to pick up the three new types.
6. **Branch name**: plan §24.2 names `refactor/epic2-fence-s3-reconcile-extract`; git cannot create it
   while the owner's local branch `refactor` exists, so this slice used
   `refactor-epic2-fence-s3-reconcile-extract`.
7. **Merge order with PR #102** — resolved. #102 merged first (`62c8042`); this branch was rebased onto
   it and the `docs/active/README.md` Epic 2 row conflict resolved keeping #102's facts and adding
   Slice 3's. With P0-a's last leg measured here, plan §21 marks Slice 0 complete (PRs #95, #102, #103).
8. **`StudyLog` orphaning** (§3.1): logs of a deleted task stay live. Pre-existing; pinned, not fixed.
9. Plan §12.2 says "7 call sites in 4 VMs"; the tree has 8 call lines in 8 methods in those 4 VMs.

## 5. Decisions made

### 5.1 Planner-computed, writer-applied FK heal

*Why it had to be made:* the diff is keyed on `MaMonHoc`, so the planner needs healed FKs, but the heal
is a mutation of the caller's graph and the planner must stay pure. *What it's for:* healing in the
executor before planning would move the mutation ahead of steps 3b and 4 and change what the caller
sees on the duplicate-MonHoc failure path. Computing an effective FK in the planner and writing it in
the writer keeps the mutation at step 5. *Experience:* "pure" and "behaviour-preserving" pull apart
wherever the old code mutated its input; splitting *decide* from *write* per mutation resolves it
without moving anything.

### 5.2 Validation errors carry the stage where they surface

*Why:* a planner that rejects bad input up front skips steps the old code had already run by the time
it threw, which changes the caller's graph on failure. *What it's for:* the plan records the error and
its stage; the writer throws at that stage. Duplicate ids were brought into scope by the owner, which
is what made a stage field necessary rather than a single late throw. *Experience:* failure paths are
behaviour too. Pin the caller's state after a throw before refactoring, or an "earlier, cleaner"
validation silently becomes a behaviour change.

### 5.3 The owner lookup stays live in the writer

*Why:* the planner can predict the owner (`incoming MonHoc ids`), but the old code resolved it from
`hocKyCu.DanhSachMonHoc` after EF fixup had run. *What it's for:* keeping the live lookup means the
writer cannot diverge from the old code even on inputs where fixup rearranges the tracked graph; the
planner's prediction is additional data, not the authority. *Experience:* where a prediction and a
live check could disagree, a behaviour-preserving slice keeps the live check and lets the next slice
decide whether the prediction is good enough to act on.

### 5.4 Snapshot oracle uses a stepped clock and labelled rows

*Why:* `Rev` alone cannot show a re-stamp's timestamps, and raw Guids make a diff unreadable. *What
it's for:* one clock tick per save turns "was this row re-stamped" into a visible `t3 → t4`, which is
what pinned P0-a. *Experience:* every expected diff was written before the first run and 15 of 16
matched; the one that did not exposed the non-deterministic clone survivor (§2.4) rather than a wrong
expectation about the save.

### 5.5 Q-S3-1 — null entry in `DanhSachTask`: difference accepted (owner ruling, 2026-09-30)

*Ruling:* the owner accepted the one known behaviour difference (§3, "What was not run"): on a `null`
entry the old code applied the earlier heals and then threw `NullReferenceException`; the planner now
throws first and no heal is applied. It is not pinned and not preserved. *Evidence (FACT, by reading;
the failure path itself is NOT RUN):* the only VM writes into `DanhSachTask` are
`QuanLyTaskViewModel.cs:116` and `:196`, and both add non-null objects. `:196` adds a freshly
constructed `StudyTask`. `:116` re-adds the items of a list built from the same collection, and the
loop just before it (`:103`) dereferences every entry, so a `null` would already throw there.
*What it's for:* it closes the only behaviour difference this slice knowingly introduces, so Slice 3
stays behaviour-preserving for every input the app can produce. *Experience:* an open question about
an unreachable input is closed fastest by showing the write sites, not by building a test for a
state the app cannot reach.
