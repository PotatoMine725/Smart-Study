# Epic 2 / T2.4 fence Slice 4: the fence is wired into the local semester save, with OD-7 restoration

**Date:** 2026-10-03 · **Author:** Claude Code agent (owner-dispatched; card
`Prompt/2026-10-03-epic2-fence-slice4-wire-local-save.md`) · **PR:** #106 → `dev` · **Branch:**
`feat-epic2-fence-s4-local-save` (worktree `.claude/worktrees/fence-s4`, from `origin/dev` `6283231`)

Labels: **OBSERVED** (seen in a run in this session) · **FACT** (read in the tree) · **INFERENCE**
(reasoned, not run) · **RULING** (owner decision, 2026-10-03 unless dated otherwise) · **NOT RUN**.

**Post-review amendments (2026-10-03).** The independent review (docs PR #107, `ship-with-followups`)
was answered with new commits on #106, as the owner directed:
- **M-1 / Q-1:** fixed under a new owner ruling. `ThemTask` and `ThemMon` leave edit mode only after the
  save succeeds (§2.3, §7.5, §11.4).
- **L-1:** the "0 warnings" claim is corrected to the clean-build count (§0, §8).
- **L-2:** the "draft" wording is removed.
- **L-3:** recorded as a known limitation, with no code change (§3).

Counts in §0, §4, §6 and §8 are for the new head.

## 0. Verdict

Slice 4 is done and every acceptance item is checkable. The fence sits between the planner and the
writer in `LocalSemesterSaveExecutor`. It runs on the same context and inside the same transaction
as the write, and a decision that is not `RouteKnown ∧ FencePassed` throws before the writer runs.
On rejection nothing is committed, the caller's in-memory graph is restored in place to persisted
state (OD-7, mechanism R2 per your ruling), the rejection reaches the global handler with its rule
ids, and nothing is retried. Under the Q-1 ruling, the two edit-mode save commands also keep their
edit mode after a failed save, so the next click edits the same row instead of creating a duplicate.

| Acceptance item (card) | Result |
|---|---|
| Build clean | **OBSERVED** — 0 errors. A clean build (`dotnet build SmartStudyPlanner.slnx --no-incremental`) gives 96 warning lines (30 distinct). The set is identical to `origin/dev` `6283231`, so this PR introduces none. Two are in files this PR touches: the pre-existing CS8618 at the constructors of the two Q-1 view models (`QuanLyTaskViewModel.cs:85`, `QuanLyMonHocViewModel.cs:51`), present at the same lines on `origin/dev`. *Corrected after review L-1: the earlier "0 warnings" came from an incremental build.* |
| Suite ≥ baseline + new, 0 failed | **OBSERVED** — 1096 total / 1095 passed / 0 failed / 1 skipped (baseline 1015 / 1014 / 0 / 1; +81 new: 75 for the fence, 6 for Q-1) |
| Rulings §5 items 1–7 checkable | **OBSERVED** — §5 below, each with its test and mutant |
| Mutants RED then reverted (remove fence call; writer before fence; drop restoration; swallow) | **OBSERVED** — M1–M4, plus 13 more, including the two Q-1 mutants M16/M17 (§6) |
| `gitnexus_detect_changes()` ⊆ expected | **Branch diff used instead** (worktree not indexed) — ⊆ expected (§8) |
| PR to `dev`, body split OBSERVED / INFERENCE / NOT RUN | #106 |

**Two owner questions are open.**
- **E-2 (§7.1).** A save that changes only Derived fields on a held row drifts that row away from the
  record's Base fingerprint. The fence cannot see it, because the planner (correctly, per plan §6)
  emits no intent for it. Opening `QuanLyTaskViewModel` and then saving anything reaches this in
  production as soon as records can exist. As you instructed, I measured and recorded it without
  fixing it.
- **Q-2 (§7.5), new.** A failed save in **create** mode is outside the Q-1 ruling. `ThemTask` still writes
  two rows on the next click. `ThemMon`'s next click is stopped by the duplicate-name guard, and the
  unsaved MonHoc is written with whatever save comes next. Both behaviours already exist on `origin/dev`.
  They are pinned as characterizations, not fixed.

## 1. Scope

Plan §21 row 4 and §24.2 card A4. Production diff (FACT, branch diff vs `origin/dev`):

| File | Change |
|---|---|
| `Mutations/SemesterReconcilePlanner.cs` | `SemesterReconcilePlan.Request`, a `MutationRequest{LocalApplication}` derived from the same reconcile data the writer walks (plan §6) |
| `Mutations/LocalSemesterSaveExecutor.cs` | Fence gate: `LoadLiveGraphAsync` (extracted, unchanged query), `FenceRouter.EvaluateAsync` on the same ctx/tx, `ApplyIfPassedAsync` (gate, then writer), `RejectAndRestore` |
| `Mutations/SemesterGraphRestorer.cs` (new) | OD-7 R2: in-place restore of the caller's graph from the old graph |
| `Mutations/LocalSaveRejection.cs` (new) | Surfacing pieces that are not WPF: find a rejection, the user message, the attached restore failure |
| `App.xaml.cs` (code-behind) | One `if (LocalSaveRejection.TryFind(...))` branch in `DispatcherUnhandledException` |
| `ViewModels/QuanLyTaskViewModel.cs`, `ViewModels/QuanLyMonHocViewModel.cs` (Q-1, post-review) | `ThemTask` / `ThemMon`: the edit-mode reset (`_taskDangSua`/`_monDangSua = null` and the button text and colour) moves from before `await LuuHocKyAsync` to after it. Nothing else changes |

Not touched: `Sync/**` (only read; mutants edited it temporarily and were reverted), `SqliteHocKyRepository.cs`,
`SemesterGraphWriter.cs`, `AppDbContext`, `SyncStamper`, any XAML, VM architecture (only the Q-1 reorder above),
frozen specs, schema, packages, port signatures. One existing test changed: P0-c (its own commit, `efe258a`).

## 2. Findings

### 2.1 Gate order (FACT, `LocalSemesterSaveExecutor.cs`)

1. BEGIN, which takes the write lock (P0-b).
2. Load the live-only old graph.
3. `Plan`, which is pure and now also produces the request.
4. `FenceRouter.EvaluateAsync(db, plan.Request)` on the same context and transaction.
5. `ApplyIfPassedAsync`. If not `RouteKnown ∧ FencePassed`: restore, then throw `MutationRejectedException`, inside the `try`. The existing catch is the single rollback site.
6. `SemesterGraphWriter.ApplyAsync`. FK heal and tracker writes happen here, and the unknown-MonHoc validation still throws at its Slice-3 stage, so caller-visible effect 7 is unchanged.
7. SaveChanges, then commit.

If a save is both rejected and invalid, the rejection wins, and the caller graph is restored rather than half-refilled.

### 2.2 Intents (FACT, planner; tests `SemesterReconcilePlannerIntentTests`, 18)

| Reconcile data | Intent |
|---|---|
| no old graph | `Create(HocKy)`, plus `Create` for every MonHoc (`MaHocKy: null→h`) and task (`MaMonHoc: null→navigation owner`) |
| old MonHoc absent | `Tombstone(MonHoc)`; **its tasks get no intent** (engineering decision 1) |
| new MonHoc | `Create(MonHoc, MaHocKy: null→h)` |
| MonHoc `MaHocKy` differs | `Reparent(MonHoc, MaHocKy)` (engineering decision 3) |
| task FK differs | `Reparent(StudyTask, MaMonHoc: from→to)` |
| old task absent, owner not deleted | `Tombstone(StudyTask)` |
| new task | `Create(StudyTask, MaMonHoc: null→effective owner)` |
| Merge-field diff on HocKy / MonHoc / StudyTask | `UpdateFields` with the changed Merge fields, ordinal-sorted (engineering decision 2 for HocKy) |
| Derived / NotMapped diff | nothing |
| early-return validation error | empty request; the writer throws at its original stage |

### 2.3 What the user sees (FACT for the code, NOT RUN for the WPF part)

The 8 save commands are unchanged. A rejection travels the same path as any save exception, to
`App.DispatcherUnhandledException` (INFERENCE from CommunityToolkit.Mvvm 8.4.0 docs, Phase 0 §3a). The
new branch recognises it with a type test, which is not a catch. It logs the rejection (and a restore
failure, if any) to crash.log and shows a warning box. The box text lists each blocking `RuleId (Outcome) —
conflict id`, says nothing was saved and nothing will be retried, and says whether the screen was
restored. `Handled = true`, as before. Code after the `await` in a command does not run, which is the same as
for any save exception today (Phase 0 §3a lists what each command skips).

**VM state that a command changes before the `await` (added after review M-1; Q-1 ruling, §11.4).** Phase 0
listed only what a command skips **after** the await. What each of the 8 commands changes **before** it
(the review's audit at `2842b98`, by code reading, INFERENCE. I re-read only the `ThemTask` and `ThemMon` rows, and
§7.5 runs those two):

| Command | Changed before the save | Brought back on rejection by |
|---|---|---|
| `QuanLyTaskViewModel.HoanThanhTask` | model only (`TrangThai`, Derived recompute, re-sort) | the R2 restore |
| `QuanLyTaskViewModel.XoaTask` | model only (collection remove) | the R2 restore (X-20) |
| `QuanLyTaskViewModel.ThemTask` | model; **edit mode, until Q-1** | the R2 restore; edit mode is now cleared only after a successful save |
| `QuanLyMonHocViewModel.XoaMon` | model only | the R2 restore (X-13) |
| `QuanLyMonHocViewModel.ThemMon` | model; **edit mode, until Q-1** | the R2 restore; edit mode is now cleared only after a successful save |
| `SetupViewModel.TaoHocKy` | none (new `HocKy` per click) | n/a (OQ-3) |
| `DashboardViewModel.LuuDuLieu` | none | n/a |
| `DashboardViewModel.MoFocusMode` | whatever `FocusWindow` wrote into the model | the R2 restore, which discards those edits (OD-7: bundled edits are not kept) |

After a rejected edit, the form still holds the user's edited values and the button still reads "Cập Nhật".
The next click re-submits the same edit to the same row. While the record is unresolved it is rejected again;
it never takes the create branch. The note/link follow-up in `ThemTask` runs only after a successful save, so
it targets the edited task (OBSERVED, §7.5).

## 3. OD-7: the mechanism (RULING) and how it works

**RULING:** R2. The executor restores in place, preserving identity, from the old graph loaded in the same
transaction, before the throw. Surfacing is one `MutationRejectedException` branch in
`App.DispatcherUnhandledException`. You chose it over the Phase 0 table:

| | R1 VM-level reload + replace | **R2 in-place restore in the executor (chosen)** | R3 per-VM in-place restore |
|---|---|---|---|
| Sites | 8 catch sites in 4 VMs | 1 (executor, before the throw) | 8 |
| Shared instance | replaced in one VM only; MainWindow and journaled VMs keep the stale graph, so the next save re-derives the rejected Tombstone and **X-20 fails** unless a shared-state service is added (VM architecture change = stop) | the caller's own instances are kept; every holder sees the restore | ok, but duplicated |
| Child `MonHocHienTai` | orphaned | kept (same instance) | kept |
| `catch (MutationRejectedException` in production (N-10) | 8 | **0** | 8 |
| Source of truth | a second query through the de-duplicated view | the old graph already loaded live-only in the same tx; the writer never ran | needs reload |
| Tests | VM tests with WPF MessageBoxes (hard) | executor-level X-20 on real SQLite | VM tests |

**Mechanics** (FACT, `SemesterGraphRestorer.cs`):
- Every EF-mapped scalar is copied from the old graph onto the caller's existing instance with the same id. The list comes from the EF model, so `[NotMapped]` UI state such as `HocKy.NgayKetThuc` survives, and the Derived columns equal the database again.
- Rows the caller no longer has come back as new, detached instances at their persisted position.
- Unsaved additions are dropped and moved tasks are put back. Kept items keep the caller's order.
- The old graph's tracked instances are never handed to the caller, and the change tracker is never touched.
- **OQ-1 (RULING (i)):** the source is the raw live rows, not the de-duplicated view.
- **OQ-3 (engineering decision):** a never-persisted HocKy is restored to an empty MonHoc list.
- **Restore failure (owner requirement 5):** the restore failure is attached to the rejection under `LocalSaveRejection.RestoreFailureDataKey` in `Exception.Data` and never replaces it. "Inner exception" was not possible, because `MutationRejectedException` (Sync/Fence, frozen for this slice) has only a `(FenceDecision)` constructor, and changing it is a stop condition. `Exception.Data` accepting an Exception value on .NET 10 is OBSERVED in `LocalSaveRejectionTests`.

**Limits, stated:**
- Instance identity is preserved for rows the caller still holds: the HocKy, every MonHoc it kept (so `QuanLyTaskViewModel.MonHocHienTai` stays attached), and every task it kept. A row the caller **removed** comes back as a new detached instance. The executor never had the removed object, so anything else still pointing at it (a selected-item field, a journaled task page of a deleted MonHoc) is not re-attached. X-20's `Same` assertions are scoped accordingly: same MonHoc and sibling instances, T equal by value.
- The models have no `INotifyPropertyChanged`, so a reverted scalar does not repaint a bound cell until the VM refreshes, and derived displays refresh on the next navigation. This is the status quo after any failed save.
- The restore mutates UI-bound collections. That relies on nothing under `Infrastructure/`, `Sync/` or `Data/` using `ConfigureAwait(false)` (FACT, grep: 0 hits), which the writer already relies on when a save succeeds.
- **Known limitation, review L-3 (INFERENCE, not measured; owner: note it, no code change).** `SemesterGraphRestorer.Restore` rebuilds `DanhSachTask` only for MonHocs that exist in persisted state, then drops unsaved MonHocs from the HocKy. If the caller moved task X into an unsaved MonHoc N, X goes back under its persisted owner, but N's own `DanhSachTask` still lists X. This only matters if something keeps a reference to N. No VM can today: `ThemMon` saves immediately, and `QuanLyTaskViewModel` is opened on persisted MonHocs.
- The restore covers the model graph. VM fields are outside the executor's reach. The two VM fields a save command changes before the await (edit mode in `ThemTask` and `ThemMon`) are handled in the commands themselves (Q-1, §2.3).

**OD-2 argument (why this is not repository-side hidden policy):** the restore never reads the decision, runs
identically for every rejection, and only keeps a caller-owned cache coherent with the database. That is the same class of
side effect as the writer's FK heal and its collection rewrites on success. The fence stays the only decider.

## 4. Tests

New files only (plus the P0-c flip). All executor-level tests run on real SQLite, staged through `FenceScenarioFixture`, and
save through the public `LayDanhSachHocKyAsync`/`LuuHocKyAsync` path (`Fixtures/LocalSaveDriver.cs`).

| File | Covers |
|---|---|
| `SemesterReconcilePlannerIntentTests` (18) | §2.2 table; Derived/NotMapped ⇒ no intent; N-9 empty request |
| `LocalSemesterSaveFenceTests` (19) | P-CR-1/2/3/4/6, P-PT-1/2/3/4/6, P-AL-1/2/3, P-K-2/3/5, X-6, owner requirement 4 (router check + plain rename persists) |
| `LocalSemesterSaveGateOrderTests` (6) | X-10, X-11, N-6 (Blocked and RouteUnknown), N-9 (plain and over a held row) |
| `LocalSemesterSaveConcurrencyTests` (1) | X-12 |
| `LocalSemesterSaveRejectionLifecycleTests` (8) | X-20, X-13, X-15, restore failure, OQ-1 clone, OQ-3 create, scalar revert + NotMapped kept, unsaved additions dropped |
| `ImpactPredictionMatchesObservedWritesTests` (7) | X-16 over task delete, MonHoc delete (3 tasks + notes + links), reparent, reparent out of a deleted MonHoc, clone-merge, two creates |
| `DerivedOnlySaveDriftObservationTests` (4) | E-2 (control + 3 OBSERVED) |
| `MutationRejectedCatchSourceScanTests` (7) | N-10 + scanner self-checks |
| `LocalSaveRejectionTests` (4) | message builder, `Exception.Data` round trip, `TryFind` |
| `LocalSaveLatencyMeasurementTests` (1) | measurement only, no assertion on numbers |
| `ViewModels/EditModeSaveRejectionTests` (2, Q-1) | (a) fence rejection: `ThemTask` editing an S1-CR-held task; `ThemMon` renaming an S1-CR-held MonHoc (local recipe: both sides reparent the MonHoc to different HocKys). After the rejection: edit mode is kept and the graph matches persisted state. The second click is rejected again with `S1CR.NonStructuralFields` (an edit, not a Create). Live row counts are unchanged, and no note or link write happens |
| `ViewModels/EditModeSaveFailureTests` (4, Q-1) | (b) a forced non-fence save failure (the first `LuuHocKyAsync` throws, nothing is written; real SQLite repository). The second click updates the same row: one row, edited values, and note/link writes target its id, with no "đã tồn tại" message. Plus 2 CHARACTERIZATIONS of create mode (Q-2). Self-contained, so it also runs on `origin/dev` |

Rows not here, by construction:
- Editor rows P-CR-5, P-PT-5, P-K-1, P-K-4, P-K-7 belong to Slice 5.
- Router-only rows P-AL-4 and P-K-6 belong to Slice 2.
- X-2 (executor overlap of three shapes) is NOT RUN.

**P-PT recipe deviation:** the fixture's S1-PT recipe tombstones the Base parent, and a task under a dead
MonHoc is never in the graph `LayDanhSachHocKyAsync` returns, so no VM can address it. P-PT rows use a local
recipe instead: the remote moves T to a locally tombstoned MonHoc, and the session holds T at its live Base
parent. For P-PT-6 this deviates from the plan's recipe; the ruling under test (SB-3 on S1-PT) is the same.

**X-16 interpretation:** §7.4's "`WasLive = true`" does not literally cover creates. The mapping compared is:
Tombstoned/CascadeTombstoned ↔ live→dead; Reparented ↔ FK change; Created ↔ new row; FieldsChanged
excluded on both sides.

**Tests-first evidence (OBSERVED):** at the stub point (`Request` = empty, gate not wired), 45 of the 76
selected tests were RED, all of them new behaviour. After wiring (commit `4b0df27`), only the 7 restoration
tests were RED; commit `4b0df27` on its own gives 1078 / 1077 passed / 1 skipped. After `cd60611`
everything is green. The 31 green at the stub point were guards, characterizations, and the "passes"
rows (P-CR-4, P-PT-4, P-AL-3, P-K-5), which pass with or without a fence; their discriminating mutants
are in §6.

## 5. Rulings §5 acceptance items 1–7

| # | Item | Evidence |
|---|---|---|
| 1 | `*.NonStructuralFields` Blocked | P-CR-6, P-PT-6 (executor level); M13/M14 (policy returns Passed) turn them RED. `CONS.OccupantContent` (P-K-4) is an editor row, Slice 5 |
| 2 | `CONS.EmptyScopeParentTombstoned` Passed; occupied scope Blocked | P-K-5 Passed; P-K-2/P-K-3 Blocked; M15 (returns Blocked) turns P-K-5 RED |
| 3 | No pending switch in `Sync/Fence/` | `Sync/**` not modified (branch diff) |
| 4 | No diff to `ConflictResolver.cs`, `ConflictStaging.cs`, fingerprint/D8-H | branch diff: none |
| 5 | Port signatures unchanged; no new service layer; no reconcile logic in `Repositories/` | `IHocKyRepository`, `SqliteHocKyRepository.cs` untouched |
| 6 | X-20 passes with restoration; removing it turns it RED | X-20 green; M3 turns it RED (and 6 more) |
| 7 | No XAML diff; no retry/deferred record; rejection surfaced with rule ids | no XAML in diff; X-13 + N-10 (zero catch sites, M4 RED); the message lists rule ids (`LocalSaveRejectionTests`; every Blocked executor test asserts the rule id and conflict id in `ex.Message`) |

## 6. Mutants (OBSERVED; each applied by exact string replacement, built, run, reverted with `git checkout`, `git status --porcelain` empty)

| # | Mutant | Target tests run | RED |
|---|---|---|---|
| M1 | remove the fence call (always-passed decision) | 22 | 16: every Blocked row, P0-c, X-6, X-12, X-20 |
| M2 | writer before fence (inside the gate step) | 29 | 6: both N-6, OQ-1, scalar revert, dropped additions, X-13. **X-20 stayed green**: the writer's `Remove` leaves T in the tracked old graph's collection, so the restore source still contained it |
| M3 | drop the restoration | 8 | 7: X-20, X-13, OQ-1, OQ-3, scalar revert, dropped additions, restore-failure |
| M4 | swallow: executor catches `MutationRejectedException`, rolls back, returns | 28 | 16: N-10 scan, every Blocked row, P0-c, X-6, X-20 |
| M5 | fence on a separate, earlier context (no tx) | 1 | X-12 |
| M6 | resolver cascade skips TaskNote (§7.4 proof) | 7 | 3: task delete, MonHoc delete, reparent-out-of-deleted-MonHoc |
| M7 | restore failure thrown instead of attached | 1 | restore-failure test |
| M8 | planner emits direct Tombstone for tasks under a deleted MonHoc | 4 | P-CR-3, P-PT-3 (stage), P0-c, planner test |
| M9 | planner diffs Derived fields too | 6 | planner Derived test + the 3 E-2 observations (they then see a rejection) |
| M10 | restorer replaces caller MonHoc instances | 2 | X-20 (`Same` instance) |
| M11 | `HocKy→MonHoc` edge not fence-routable | 2 | both requirement-4 tests |
| M12 | X-15 two-point: load tombstoned tasks **and** writer copies `IsDeleted` | 1 | X-15 |
| M13 | `S1CR.NonStructuralFields` → Passed | 1 | P-CR-6 |
| M14 | `S1PT.NonStructuralFields` → Passed | 1 | P-PT-6 |
| M15 | `CONS.EmptyScopeParentTombstoned` → Blocked | 3 | P-K-5 |
| M16 | `ThemTask`: edit-mode reset moved back before the await (Q-1) | 6 | 2: `ThemTask` rejection (a) and failure (b) |
| M17 | `ThemMon`: edit-mode reset moved back before the await (Q-1) | 6 | 2: `ThemMon` rejection (a) and failure (b) |

X-15 stays labelled CHARACTERIZATION (it pins D-2's "the stale id becomes a Create and collides on the
PK"). After D-2 no single-point mutant reaches it; the two-point M12 does.

## 7. Observations

### 7.1 E-2: Derived-only saves drift a held row. OBSERVED; owner question

On an S1-CR-held task, each of these saves passes the fence (no intent), writes the column, re-stamps
the row (`Rev + 1`, `ModifiedAtUtc` = save time) and makes
`SyncBaseFingerprint.Matches(record.BaseFingerprint, live)` false. The record stays Unresolved:
- `DiemUuTien`-only;
- `MucDoCanhBao`-only;
- the production path: construct `QuanLyTaskViewModel` on the shared graph, whose `TinhDiemVaSapXep` recomputes `DiemUuTien`/`MucDoCanhBao` for every task of the MonHoc (stub engine fixed at 82 ⇒ "Khẩn cấp"), then save anything.

The control (a no-change save) writes nothing.

Chain (FACT from Phase 0 §3c):
1. `CopySyncSafeValues` → `SetValues` copies the mapped Derived columns.
2. The entry becomes Modified.
3. `SyncStamper` stamps every Modified `ISyncMetadata` entity.
4. The snapshot carries provenance (`CanonicalJson.cs:49`).
5. The fingerprint changes.

INFERENCE: with the real engine, `DiemUuTien` depends on time to deadline, so on most days opening the task
page and saving any sibling drifts every held task of that MonHoc. It is unreachable today, because no record
can exist while sync is unwired.

**Owner question E-2:** which of these should hold for a row held at Base while its record is Unresolved?
- (a) Derived-only writes are allowed and the drift is accepted. D8-H then rejects the later resolution.
- (b) The writer must not write Derived columns of a held row.
- (c) Derived columns stop bumping provenance at all.
- (d) Something else.

Per your instruction there is no Derived intent, no block and no fix in this PR. Mutant M9 shows that
"route Derived fields as intents" would turn these saves into rejections.

> **Ruled 2026-10-03: (c)** — Derived-only changes do not stamp (`Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId` unchanged; any non-Derived property in the entry ⇒ normal stamping). Record: [`../specs/2026-10-03-fence-slice4-followup-owner-rulings.md`](../specs/2026-10-03-fence-slice4-followup-owner-rulings.md) §1. Implemented by ticket `2026-10-03-e2-derived-no-stamp`, not by this report's PR.

### 7.2 OQ-1 consequence (RULING (i), OBSERVED as requested)

After a rejection, a MonHoc clone that the read-side dedup had folded into its representative reappears in
the caller's graph, with its own task moved back under it (`MaMonHoc` = the clone). The graph equals persisted state. The next
`LayDanhSachHocKyAsync` folds it again. The test puts the blocking record on an unrelated task, so the clone pair
is not itself what was rejected.

### 7.3 X-12 (OBSERVED)

File-backed database, WAL (read back). The interceptor fired once, right after the selector's
`SyncConflictRecords` read. The second connection's record insert failed with `DbUpdateException`
wrapping `SQLITE_BUSY` (5) after its 1 s timeout. The save committed the tombstone, and no record was
committed. Under M5 both committed.

### 7.4 Latency (OBSERVED; measurement only)

Setup: reference semester of 1 HocKy × 8 MonHoc × 10 tasks, file-backed WAL, no records, 5 warm-up + 30
samples per shape, 3 runs each. Before = baseline `6283231` + the measurement file only; after = `cd60611`.

| Shape | Before: median (p95) per run, ms | After: median (p95) per run, ms |
|---|---|---|
| no-change save (`MoFocusMode`) | 10.18 (19.65) · 11.23 (21.44) · 11.54 (23.50) | 12.96 (25.36) · 13.53 (27.78) · 12.50 (22.03) |
| one-field edit (`HoanThanhTask`) | 22.24 (47.75) · 24.65 (61.91) · 23.19 (36.27) | 52.50 (67.70) · 44.79 (54.87) · 45.40 (110.79) |
| task delete (`XoaTask`) | 47.80 (67.26) · 33.77 (44.37) · 33.99 (94.83) | 28.82 (32.40) · 33.17 (50.25) · 53.69 (124.95) |

The no-change save gained about 2 ms. The one-field edit roughly doubled its median (+20–28 ms). The delete
numbers overlap within run-to-run noise. Not analysed and not optimised (card: numbers only). INFERENCE, unverified: the
edit path is the only one whose request is non-empty without being a delete, so it pays the resolver and
selector reads.

### 7.5 M-1 / Q-1: edit mode across a failed save (OBSERVED, post-review)

**Before the fix.** The same tests were run with only the "still in edit mode" assertions removed, so that
each could run on to its duplicate check:
- **(a) Fence rejection, this branch with the VM commit reversed.** For both `ThemTask` and `ThemMon`, the second
  click was **not** rejected. A throwaway probe (not committed) then made the second click a plain
  `ExecuteAsync` and read the result:
  - **`ThemTask`:** live tasks went 1 → 2. The in-memory MonHoc held T plus a new task carrying "edited while
    held", and `UpsertNoteAsync` was called with **T's** id. This is the review's M-1, reproduced.
  - **`ThemMon`:** live MonHocs went up by 1, and the new MonHoc in the graph carries the rejected name
    "renamed while held". This is new evidence; the review had not probed it.
- **(b) Forced non-fence failure, on `origin/dev` `6283231`** (temporary worktree, only
  `EditModeSaveFailureTests.cs` added):
  - **`ThemTask`, edit mode: the duplicate reproduces, so it is a pre-existing bug.** Two live rows exist after
    the second click.
  - **`ThemMon`, edit mode: no duplicate, but the retry is lost.** The edited MonHoc still carries the old name
    in the database. The edited name is already in memory, so the second click reaches the duplicate-name
    guard. Reading the code, it returns with "Môn '…' đã tồn tại" (INFERENCE: the run stopped at the earlier
    name assertion, before the message assertion). This is also pre-existing.
  - **Both create-mode characterizations** pass on `origin/dev` and on head, unchanged by this PR (see Q-2).

**After the fix (`b7bc48b`).** All 6 tests pass, and M16/M17 each turn their two tests RED.

**Q-2, owner question (create mode, outside the Q-1 ruling).** `ThemTask` and `ThemMon` add the new row to
the shared collection before the save. Under a fence rejection, the R2 restore drops it. Under any other
save failure nothing does:
- `ThemTask`'s next click adds a second task, and both are written (pinned:
  `ThemTask_CreateMode_SaveFails_NextClick_PersistsTwoRows_Characterization`).
- `ThemMon`'s next click is stopped by the name guard. The unsaved MonHoc then stays in memory and is written
  by the next save of anything (pinned: `ThemMon_CreateMode_SaveFails_NextClick_IsStoppedByNameGuard_Characterization`).

Fixing this needs a rollback-on-failure in the command, which is broader than the reorder Q-1 authorised.
The options are:
- (a) accept it;
- (b) roll the addition back when the save throws;
- (c) defer to Slice 5, where `ThemTask` is reworked anyway.

> **Ruled 2026-10-03: filed as defect D-5, closed V2** — on a non-fence save failure the caller's graph loses every unsaved addition (a `MonHoc`/`StudyTask` with no persisted live row); scalar edits and pending deletions on persisted rows are kept. Record: [`../specs/2026-10-03-fence-slice4-followup-owner-rulings.md`](../specs/2026-10-03-fence-slice4-followup-owner-rulings.md) §2. Implemented by ticket `2026-10-03-d5-failed-save-restore`, not by this report's PR.

## 8. Verification

- Baseline (TRX counters, before any edit, `6283231`): 1015 total / 1014 passed / 0 failed / 1 skipped
  (`SoeBaselineCaptureTests.CaptureBaseline_…`, skipped by design).
- Head `cd60611` (TRX counters): 1090 / 1089 / 0 / 1.
- Head `b7bc48b`, after the Q-1 fix (TRX counters): **1096 / 1095 / 0 / 1**. The docs commits that follow change no code.
- Clean build (`dotnet build SmartStudyPlanner.slnx --no-incremental`, warnings logged to a file) at `b7bc48b` and
  at `origin/dev` `6283231`: 0 errors and 96 warning lines on each, the same 30 distinct warnings (CS8618 ×15,
  CS8625 ×4, NU1903 ×3, CS8622 ×2, xUnit1031 ×2, xUnit2031 ×2, NU1904, CS8602). None is introduced. The two in
  files this PR touches are the pre-existing CS8618 at `QuanLyTaskViewModel.cs:85` and `QuanLyMonHocViewModel.cs:51`.
  The original "0 warnings" came from an incremental build (review L-1).
- Commit `4b0df27` alone: 1078 / 1077 / 0 / 1.
- **Scope: branch diff vs `origin/dev`, not `gitnexus_detect_changes`.** The GitNexus index points at the owner's
  checkout, not this worktree, and is stale (last indexed `6283231`). Changed production symbols:
  - `App.OnStartup` (handler lambda);
  - `LocalSemesterSaveExecutor.ExecuteAsync`, with new `LoadLiveGraphAsync`, `ApplyIfPassedAsync` and `RejectAndRestore`;
  - `SemesterReconcilePlanner.Plan`, with new private intent helpers;
  - new `SemesterReconcilePlan.Request`;
  - new types `SemesterGraphRestorer` and `LocalSaveRejection`.

  All are within the card's MAY-edit list. Post-review, Q-1 adds `QuanLyTaskViewModel.ThemTask` and
  `QuanLyMonHocViewModel.ThemMon`, under the owner's 2026-10-03 authorisation for that reorder. I checked the
  branch diff after committing and before pushing.
- GitNexus `impact` before the Q-1 edit: `ThemTask` LOW, `ThemMon` LOW, 0 indexed callers each. The source-generated
  `ThemTaskCommand`/`ThemMonCommand` and their XAML bindings are the real callers, and the index does not see them.
- GitNexus `impact` before editing: `LocalSemesterSaveExecutor` **HIGH** (1 direct caller,
  `LuuHocKyAsync`, then 8 VM commands and the regression suites; priced as R-1 by the plan),
  `SemesterReconcilePlan` LOW, `App.OnStartup` LOW (0 callers). The Phase 0 miss is recorded below.

## 9. NOT RUN

- The WPF branch in `App.DispatcherUnhandledException` (MessageBox); only `LocalSaveRejection` is unit-tested.
- Any VM save command end to end (they show MessageBoxes); the port they call is tested instead. Exception:
  `ThemTaskCommand` and `ThemMonCommand` run through `ExecuteAsync` in the Q-1 tests, with no Dispatcher and no
  handler.
- Repaint of reverted POCO scalars in the UI.
- X-2 (executor-level three-shape overlap). The router-level X-1 exists from Slice 2.
- E-2 with the real priority engine (stub engine used; see §7.1).

## 10. Follow-ups

- **E-2 owner ruling** (§7.1). Until then, D8-H rejects a resolution whose Base drifted, so the failure mode is loud, not silent.
- **Q-2 owner ruling** (§7.5): a failed save in create mode. This is pre-existing and not caused by the fence.
- After merge: compare local `dev` vs `origin/dev`, and re-run `npx gitnexus analyze`. In Phase 0 the index
  reported 0 callers for `LocalSemesterSaveExecutor.ExecuteAsync`, while grep shows `SqliteHocKyRepository.cs:85`.
  That is the third caller this index has missed.
- The port is still named "repository" though it fronts an executor (plan §12.2 price of L1, unchanged).
- Slice 5 (task editor + write-path fence) is next by plan order. Slice 6 is still gated on OD-1.

## 11. Decisions made

### 11.1 RULING: OD-7 mechanism R2 (restore in place, in the executor, before the throw)
- **Why it had to be made:** OD-7 (2026-09-14) ruled what must happen on a rejection but left the mechanism to Slice 4,
  and the VMs share one `HocKy` instance across pages, which rules out per-VM replacement.
- **What it's for:** one site restores the graph for all 8 callers, no production code catches the rejection
  (N-10 with an empty allowlist), and X-20 (spec §13.5 on the local path) holds.
- **Alternatives, as put to the owner (Phase 0 table, repeated here per the ruling's instruction):**

  | | R1 VM-level reload + replace | **R2 in-place restore in the executor (chosen)** | R3 per-VM in-place restore |
  |---|---|---|---|
  | Sites | 8 catch sites in 4 VMs | 1 (executor, before the throw) | 8 |
  | Shared instance | replaced in one VM only; MainWindow and journaled VMs keep the stale graph, so the next save re-derives the rejected Tombstone and X-20 fails unless a shared-state service is added (VM architecture change = stop) | caller's own instances kept; every holder sees the restore | ok, but duplicated |
  | Child `MonHocHienTai` | orphaned | kept (same instance) | kept |
  | `catch (MutationRejectedException` in production | 8 | 0 | 8 |
  | Source of truth | second query through the de-duplicated view | old graph loaded live-only in the same tx | needs reload |
  | Tests | VM tests with WPF MessageBoxes | executor-level X-20 on real SQLite | VM tests |

- **Experience:** check who else holds an object graph before choosing between "replace" and "restore". R1
  would have failed X-20 through MainWindow's instance alone, which a per-VM test would never have shown.

### 11.2 RULING: OQ-1 (i), restore to the raw live rows
- **Why:** the old graph is raw, and the caller's graph is the de-duplicated view.
- **What for:** "persisted state" in OD-7's wording, and a rejection that cannot become sticky through a
  re-derived clone tombstone.
- **Experience:** the cost (a clone reappears until the next load) is pinned as an OBSERVED test, so a later
  change to the dedup or the restorer shows up.

### 11.3 RULING: E-2 measured, recorded, not fixed (overrides the card's stop condition)
- **Why:** the stop condition would have halted the slice on an issue that sits outside the fence.
- **What for:** Slice 4 ships, with the evidence on record for a ruling.
- **Experience:** the measurement includes the real VM recompute path, not only a synthetic edit. That is
  what makes it a production concern rather than a theoretical one.

### 11.4 RULING (2026-10-03, post-review): Q-1, OD-7's "restore the in-memory graph" covers VM state the save command itself changes before the await
- **The ruling, as given:** OD-7's "restore the in-memory graph" also covers VM state that the save command
  itself changes before awaiting the save. It is narrow: only that state, nothing broader. `ThemTask`
  (`QuanLyTaskViewModel`) and `ThemMon` (`QuanLyMonHocViewModel`) leave edit mode and reset the form only after
  the save succeeds. The change is a minimal reorder, with no VM architecture change and no XAML.
- **Why it had to be made:** the independent review (PR #107, M-1, REPRODUCED) showed that the R2 restore brings
  the model back but cannot reach VM fields. After a rejected edit, the next click took the create branch,
  wrote a duplicate carrying the rejected edit, and sent the note to the held task. The review's Q-1 asked
  whether OD-7 extends to that state. The options were (a) fix, (b) accept as UX, (c) defer to Slice 5;
  the owner chose (a), narrowed.
- **What it's for:** a rejection or failure leaves the user exactly where they were. The same row is still being
  edited and the form still holds their input. A retry is then the user's explicit re-submission of the
  same edit, never a different operation. It also fixes the pre-existing duplicate after any failed `ThemTask`
  edit (§7.5 (b)), because the reorder does not depend on why the save failed.
- **What it deliberately does not cover:** create mode (Q-2, §7.5), any other VM field, and the
  remaining 6 commands, which change only the model before the await.
- **Experience:** a restore at the persistence layer is complete only for state the persistence layer owns.
  Auditing "what does each caller change **before** the await" belongs next to "what does it skip **after**
  it"; Phase 0 did only the second. Running the VM command once, which the review's probe did, found what eight
  executor-level tests could not.

### 11.5 Engineering decisions accepted by the owner (3b(1)–(3), not rulings)
1. **No direct Tombstone for tasks under a deleted MonHoc.** A direct intent outranks the cascade
   (RowEffect precedence) and would turn P-CR-3/P-PT-3 from `@CascadeReached` into `@DirectSubject`. M8 proves the
   tests see it.
2. **`UpdateFields(HocKy)`** although it is not in plan §6's table: the writer writes that row, so it belongs in the
   request. Owner requirement 4 verified that it is RouteKnown (M11).
3. **`Reparent(MonHoc, MaHocKy)` when the FK differs:** the writer copies `MaHocKy` verbatim, so every structural
   write is in the request even though no VM does this today.

### 11.6 Engineering decisions made in this slice (not ruled)
#### The restore failure travels in `Exception.Data`, not as an inner exception
- **Why it had to be made:** owner requirement 5 says a failing restore must not hide the rejection, and suggested
  "attached as inner". `MutationRejectedException` lives in `Sync/Fence` and has only a `(FenceDecision)`
  constructor, and changing a fence type is a stop condition.
- **What it's for:** the user always gets the rejection with its rule ids. The restore failure is logged by the
  handler and changes the message to "could not restore the screen". `LocalSaveRejection.RestoreFailureOf` is the
  one reader, and M7 (throw instead of attach) turns the forced-failure test RED.
- **Experience:** `Exception.Data` takes an Exception value on .NET 10 (OBSERVED; .NET Framework demanded
  `[Serializable]`). If the fence types are ever reopened, an inner-exception constructor would be the more
  conventional channel. Swapping it in later is local to `RejectAndRestore` and `LocalSaveRejection`.

#### OQ-3: a never-persisted HocKy is restored to an empty MonHoc list
- **Why it had to be made:** OQ-3 was raised in Phase 0 and not ruled. A create-branch rejection has no old graph
  to restore from, so the restorer needed some defined behaviour.
- **What it's for:** the literal reading of OD-7's "persisted state" for a row that has none: no MonHoc. The
  HocKy's own scalars have no row to copy from and are left as typed. This is reachable only through an AL-PT
  identity collision, and is pinned by `OQ3_RejectedCreateOfNewHocKy_EmptiesItsMonHocList_NoRowWritten`.
- **Experience:** if a creating flow ever lets the user retry the same form, this choice discards their typed
  subjects. Revisit it if `SetupViewModel` grows a retry path. Today `TaoHocKy` builds a fresh HocKy on every
  attempt.

- **The rejection wins over validation** when a save is both: the writer never runs on a rejected
  decision (N-6), so the unknown-MonHoc error is not reached.
- **Surfacing uses a type test in the existing handler, not a catch.** The rejection is already unhandled when
  it arrives; `Handled = true` is the handler's existing behaviour, now with a specific message.
- **P-PT local recipe and X-16 interpretation** (§4): both stated in the test headers as well.
- **A shared `LocalSaveDriver` test fixture** drives saves through the public port only, so every executor
  test observes what a VM observes.
- **Q-1 test recipes (post-review).**
  - The `ThemMon` rejection uses a local S1-CR-on-MonHoc recipe. Before it is used, the test asserts that the
    record is a `StructuralConflict/ConcurrentReparent` on that MonHoc, and that the MonHoc is held live at
    Base. This keeps the rejection from passing for the wrong reason.
  - The edit is a **rename**: a `SoTinChi`-only edit would be stopped by the name guard and would not reach the
    duplicate.
  - The generic failure is thrown by a wrapper before the real repository is called. Nothing is written and
    no restore runs, which isolates the VM ordering from the R2 restore.

  The failure file is self-contained, so it could be run on `origin/dev` to answer "pre-existing or not". The
  only change for that run was removing the edit-mode assertions (§7.5).

---

## Appendix A — Phase 0 report as delivered to the owner (copied from the session scratchpad, unedited)

# Slice 4 — Phase 0 report (recon + OD-7 proposal). Owner gate.

## 1. Venue / baseline (OBSERVED)
- `rtk git fetch`; local `dev` == `origin/dev` == `6283231` (no divergence; local dev has uncommitted owner files, untouched).
- Worktree `.claude/worktrees/fence-s4`, branch `feat-epic2-fence-s4-local-save` from `origin/dev`.
  Hyphenated: local branch `refactor` exists (blocks `refactor/...`); `feat` does not, but kept the card's hyphen form.
- Baseline (TRX counters, not rtk): total 1015 · passed 1014 · failed 0 · skipped 1 (`SoeBaselineCaptureTests.CaptureBaseline_...`).

## 2. Impact (GitNexus + grep)
- `SemesterReconcilePlanner.Plan`: HIGH — 14 planner tests + executor. Plan's signature/lists stay unchanged; intents are an additive property.
- `SqliteHocKyRepository.LuuHocKyAsync`: HIGH (priced R-1) — 25 direct (8 VM callers in 4 VMs + tests).
- `LocalSemesterSaveExecutor.ExecuteAsync`: GitNexus says 0 callers — **wrong** (grep: `SqliteHocKyRepository.cs:85`). Third index miss; re-analyze after merge.
- VM save commands: `SetupViewModel.TaoHocKy`, `QuanLyMonHocViewModel.XoaMon/ThemMon`, `QuanLyTaskViewModel.XoaTask/HoanThanhTask/ThemTask`, `DashboardViewModel.LuuDuLieu/MoFocusMode`.
- `App.OnStartup` handlers: no callers beyond WPF lifecycle.
- Schema: `SyncConflictRecords` is in the EF model (`AppDbContext.cs:83,159`), so `TestDb.Create`/`EnsureCreated` builds it; all real `LuuHocKyAsync` test callers use `TestDb` or `SyncApplyFixture`. Wiring the selector into every save will not break existing fixtures on schema.

## 3a. How a save exception reaches the user today
- All 8 are `[RelayCommand]` on `async Task` → generated `AsyncRelayCommand`, none sets `FlowExceptionsToTaskScheduler` (only `FocusViewModel`'s two do; they don't save). CommunityToolkit.Mvvm 8.4.0 XML doc: without that option, `Execute` rethrows "on the calling context" → WPF Dispatcher → `App.DispatcherUnhandledException`.
- Handler (`App.xaml.cs:23-30`, VERIFIED, closes plan §4.4 pre-check): `CrashLogger.Log` + generic MessageBox "Thao tác vừa rồi có thể chưa được lưu" + **`args.Handled = true`**.
- Post-save code skipped on a throw: `XoaTask` HasData; `HoanThanhTask` none (already refreshed pre-save); `ThemTask` HasData, DifficultyLabelLog, **note/link saves**, form reset; `XoaMon` none; `ThemMon` form reset; `LuuDuLieu` success box; `MoFocusMode` `LoadDuLieuDashboard`; `TaoHocKy` `OnSetupCompleted`.
- Label: INFERENCE (doc + code); the WPF path is not run in tests.

## 3b. Intent derivation (planner additions)
Merge fields (MergeSurfaceRegistry, `FieldClass.Merge`):
- HocKy: `Ten`, `NgayBatDau`; MonHoc: `TenMonHoc`, `SoTinChi`; StudyTask: `TenTask`, `HanChot`, `TrangThai`, `LoaiTask`, `DoKho`, `ThoiGianDaHoc`, `NgayHoanThanh`.
- Derived (no intent): `HocKy.IsSeeded`, `StudyTask.DiemUuTien`, `StudyTask.MucDoCanhBao`. NotMapped: `HocKy.NgayKetThuc/IsNgayKetThucAuto`.
Proposed `SemesterReconcilePlan.Intents` (additive; existing lists untouched for the writer):
| Plan data | Intent |
|---|---|
| IsCreate | `Create(HocKy)` + `Create(MonHoc, MaHocKy: null→h)` + `Create(StudyTask, MaMonHoc: null→nav owner)` |
| MonHocDeletes | `Tombstone(MonHoc)` |
| MonHocAdds | `Create(MonHoc, MaHocKy: null→hocKy.MaHocKy)` (incoming `MaHocKy` is Guid.Empty on `new MonHoc`) |
| MonHocUpdates with Merge diff | `UpdateFields(MonHoc, [changed])` |
| MonHocUpdates with `MaHocKy` diff (writer copies it verbatim; no VM does this) | `Reparent(MonHoc, MaHocKy)` — fidelity: every structural write is in the request |
| TaskReparents | `Reparent(StudyTask, MaMonHoc: from→to)` |
| TaskDeletes whose old owner is **not** in MonHocDeletes | `Tombstone(StudyTask)` |
| TaskDeletes under a deleted MonHoc | **no intent** — the resolver's cascade from `Tombstone(M)` reaches them; a direct intent would outrank it (precedence) and turn P-CR-3/P-PT-3 `@CascadeReached` into `@DirectSubject` |
| TaskUpserts IsNew | `Create(StudyTask, MaMonHoc: null→effective owner)` |
| TaskUpserts existing, Merge diff | `UpdateFields(StudyTask, [changed])` (alongside Reparent if both) |
| HocKy (existing) Merge diff | `UpdateFields(HocKy, [changed])` — not in plan §6 table; the writer writes the row, so it belongs in the request |
| ValidationErrors at BeforeFkHeal/AfterFkHeal (early return) | empty request → vacuous pass → writer throws at the original stage (effects unchanged) |
- Diff mechanics: by MergeSurfaceRegistry field names (reflection), old = tracked `hocKyCu` POCO, new = incoming; default equality = what EF's own change detection uses. `ChangedFields` empty for Create/Reparent/Tombstone.
- Derived-only change ⇒ no intent: by construction (registry filter); proved by a planner test.

## 3c. E-2 (measure, don't fix) — INFERENCE, to be MEASURED in Phase 1
Chain from code: writer `CopySyncSafeValues` → `SetValues` copies `DiemUuTien`/`MucDoCanhBao` (mapped columns) → entry `Modified` → `SyncStamper` stamps every Added/Modified ISyncMetadata entity regardless of column (`SyncStamper.cs:54-58`: Rev, ModifiedAtUtc, ModifiedByDeviceId) → `EntitySnapshotMapper.ProvenanceOf` puts `ModifiedAtUtc`/`ModifiedByDeviceId` in the snapshot → `CanonicalJson.Write` serialises provenance (`CanonicalJson.cs:49`) → `SyncBaseFingerprint.Matches` false.
Production relevance: `QuanLyTaskViewModel`'s constructor (`TinhDiemVaSapXep`) recomputes `DiemUuTien`/`MucDoCanhBao` for every task of the MonHoc, and `DiemUuTien` depends on time-to-deadline. So an ordinary sibling save from that page will probably rewrite Derived columns on a held row with **no intent to route**.
Planned test (OBSERVED label): stage S1-CR on T, load as UI, change only `T.DiemUuTien` (and separately `MucDoCanhBao`), save → record `Rev`, `ModifiedAtUtc`, `Matches(BaseFp, live)` before/after; control = no-change save.
**Pre-flagged stop:** if measured as drift, this hits the stop "E-2 shows Derived-only saves drift a held row". X-20 will keep Derived values equal to the DB so it isolates OD-7; E-2 stays its own test.

## 4. OD-7 restoration mechanism
Evidence on the shared instance: pages are navigated **by instance** (`NavigationService.Navigate(new QuanLyTaskPage(hk, mh))`), back = `NavigationService.GoBack()` (journal keeps object-navigated pages → same VM, same `HocKy`; WPF doc behaviour, INFERENCE). `MainWindow._currentHocKy` builds every sidebar page (`MainWindow.xaml.cs:240-282`). So the same `HocKy` object is held by MainWindow + every journaled page VM + `QuanLyTaskViewModel.MonHocHienTai` (a child `MonHoc`).

| | R1 VM-level reload+replace | **R2 in-place, identity-preserving restore in the executor (recommended)** | R3 per-VM in-place restore |
|---|---|---|---|
| Sites | 8 catch sites in 4 VMs | 1 (executor, before the throw) | 8 |
| Shared instance | replaced only in one VM; MainWindow + journaled VMs keep the stale graph → next save re-derives the rejected Tombstone → **X-20 fails** unless a shared-state service is added (VM architecture change = stop) | caller's own instances kept; every holder sees the restore | ok, but duplicated |
| Child `MonHocHienTai` | orphaned | kept (same instance) | kept |
| Catch sites (N-10) | 8 `catch (MutationRejectedException)` | **0** — restore happens before the executor throws | 8 |
| Source of truth | a 2nd query (`LayDanhSachHocKyAsync`, dedup view) | `hocKyCu`, already loaded live-only in the same tx the fence read; the writer never ran | needs reload |
| Tests | VM tests with WPF MessageBox in commands (hard) | executor-level X-20 on real SQLite | VM tests |

R2 design:
- Gate failure → `Restore(hocKyCu, hocKy)` (pure, in memory) → `throw new MutationRejectedException(decision)` inside the existing `try` → the existing catch rolls back **once** and rethrows. Single rollback site; nothing was written, so restore/rollback order is irrelevant.
- Restore = for every persisted MonHoc/task in `hocKyCu`: reuse the caller's instance by id if it has one, else hand over the `hocKyCu` instance; copy **EF-mapped scalars only** (Merge, Structural, Derived, sync metadata); `[NotMapped]` `NgayKetThuc`/`IsNgayKetThucAuto` survive. Rebuild collection membership: drop unsaved additions, re-add removed rows, move reparented tasks back; kept items keep the caller's order and re-added ones go at their persisted position.
- Threading: it mutates UI-bound `ObservableCollection`s, which the writer already does on success (effect 3/4). There is no `ConfigureAwait(false)` in `Infrastructure/`, `Sync/` or `Data/` (grep), so when a VM awaits, continuations resume on the Dispatcher. That is an assumption stated in code.
- Not restored: scalar reverts on POCOs (no INPC) don't repaint bound cells until the VM refreshes; derived displays (Dashboard projections, `HasData`, sort) refresh on the next navigation. Status quo for any save exception; changing it means editing VMs.
- Only on fence rejection. Validation/persistence failures keep today's behaviour (effect 7 etc.).
- OD-2 "no repository-side hidden policy": restoration decides nothing, because it never reads the decision's contents and the same transformation runs on every rejection. The fence stays the only decider (`Sync/Fence`). The decision is explicit and surfaced. Restoration is coherence of a caller-owned cache, the same class as the existing FK heal and the effect-3/4 collection rewrites. No repository file changes (`SqliteHocKyRepository.cs` untouched).

Surfacing: a type branch in the existing handler: `if (args.Exception is MutationRejectedException r)` shows a specific MessageBox using `r.Message` (it already lists `RuleId (Outcome, conflict id)` per blocking result) + `CrashLogger.Log`, keeps `Handled = true`. Code only, no XAML. It is surfacing, not swallowing: the rejection stays visible and logged, nothing is retried. Test: a pure message builder unit-tested; the WPF handler branch itself is NOT RUN.
N-10 scan: zero `catch (MutationRejectedException` in production + planted self-check.

## 5. Gate order in the executor
BEGIN (write lock, P0-b) → load live-only `hocKyCu` → `Plan` (pure, intents) → `FenceRouter.EvaluateAsync(db, request)` same ctx+tx → if `!RouteKnown || !FencePassed`: restore + throw (writer never runs, N-6) → writer (FK heal, tracker writes; validation errors still throw at their Slice-3 stage = plan §5 step 5 folded into step 6 to keep effect 7) → SaveChanges → commit; any throw → single rollback.
Consequence: if a request is both rejected and invalid (unknown MonHoc), the rejection wins and the caller graph is restored instead of half-refilled. Existing effect-7 test has no records → unchanged.

## 6. Open questions for the owner
- OQ-1 (OD-7 target vs read-side dedup): `hocKyCu` is raw live-only; the caller's graph is the dedup view. (i) Restore to raw: clones reappear in the UI until next load, rejection not sticky (recommended). (ii) Restore to the dedup view: the clone-tombstone/reparent intents are re-derived on every save, so a rejection caused by a held clone stays sticky until resolution. Moot in production today (no records).
- OQ-2: E-2 likely to show drift (see 3c). Ruling needed later; Phase 1 will report it and stop per card unless you pre-authorise "record as finding and continue".
- OQ-3 (degenerate): create-branch rejection (brand-new HocKy) → restore = empty MonHoc list. Practically unreachable (a new random Guid can't be a record subject).

## 7. Test plan notes
- P0-c control (`P0c_Control_...`): predicted to stay green unchanged (Tombstone(B) reaches no T row or edge) — verify.
- X-12: deterministic interleave via a test-side `DbCommandInterceptor` injected through `ctxFactory` (`AppDbContext(DbContextOptions)` exists). After the selector read, a 2nd connection tries to stage S1-CR on T. Correct code: BUSY (lock from BEGIN). Mutant "fence on separate earlier context": succeeds → both committed → RED.
- X-15: after D-2 the planned mutant ("writer copies IsDeleted") can't fire, because a tombstoned id becomes `IsNew` → PK collision. New mutant candidate: drop the executor load's `!IsDeleted` filter + drop the IsDeleted restore in `CopySyncSafeValues` → resurrects. If no single-point mutant discriminates, X-15 is labelled characterization.
- Latency: Stopwatch over N saves on the snapshot fixture, before/after; numbers only.

*(Appendix note, not part of the Phase 0 text: two details in the proposal changed during implementation, both within the
approved design. (1) Rows the caller no longer holds are re-added as fresh detached copies, not as the old graph's tracked
instances. The tracked instances would put two instances of one key within reach of a later DetectChanges. (2) The surfacing branch
uses `LocalSaveRejection.TryFind` plus a Vietnamese message builder instead of raw `r.Message`, so the text is unit-testable.
Raw `r.Message` is still logged.)*
