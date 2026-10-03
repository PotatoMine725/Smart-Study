# Epic 2 / T2.4 fence Slice 4: the fence is wired into the local semester save, with OD-7 restoration

**Date:** 2026-10-03 · **Author:** Claude Code agent (owner-dispatched; card
`Prompt/2026-10-03-epic2-fence-slice4-wire-local-save.md`) · **PR:** #106 → `dev` (draft) · **Branch:**
`feat-epic2-fence-s4-local-save` (worktree `.claude/worktrees/fence-s4`, from `origin/dev` `6283231`)

Labels: **OBSERVED** (seen in a run in this session) · **FACT** (read in the tree) · **INFERENCE**
(reasoned, not run) · **RULING** (owner decision, 2026-10-03 unless dated otherwise) · **NOT RUN**.

## 0. Verdict

Slice 4 is done and every acceptance item is checkable. The fence sits between the planner and the
writer in `LocalSemesterSaveExecutor`. It runs on the same context and inside the same transaction
as the write, and a decision that is not `RouteKnown ∧ FencePassed` throws before the writer runs.
On rejection nothing is committed, the caller's in-memory graph is restored in place to persisted
state (OD-7, mechanism R2 per your ruling), the rejection reaches the global handler with its rule
ids, and nothing is retried.

| Acceptance item (card) | Result |
|---|---|
| Build clean | **OBSERVED** — 0 errors, 0 warnings |
| Suite ≥ baseline + new, 0 failed | **OBSERVED** — 1090 total / 1089 passed / 0 failed / 1 skipped (baseline 1015 / 1014 / 0 / 1; +75 new) |
| Rulings §5 items 1–7 checkable | **OBSERVED** — §5 below, each with its test and mutant |
| Mutants RED then reverted (remove fence call; writer before fence; drop restoration; swallow) | **OBSERVED** — M1–M4, plus 11 more (§6) |
| `gitnexus_detect_changes()` ⊆ expected | **Branch diff used instead** (worktree not indexed) — ⊆ expected (§8) |
| Draft PR to `dev`, body split OBSERVED / INFERENCE / NOT RUN | #106 |

**One owner question is open (E-2, §7.1).** A save that changes only Derived fields on a held row
drifts that row away from the record's Base fingerprint. The fence cannot see it, because the
planner (correctly, per plan §6) emits no intent for it. Opening `QuanLyTaskViewModel` and then
saving anything reaches this in production as soon as records can exist. As you instructed, I
measured and recorded it without fixing it.

## 1. Scope

Plan §21 row 4 and §24.2 card A4. Production diff (FACT, branch diff vs `origin/dev`):

| File | Change |
|---|---|
| `Mutations/SemesterReconcilePlanner.cs` | `SemesterReconcilePlan.Request`, a `MutationRequest{LocalApplication}` derived from the same reconcile data the writer walks (plan §6) |
| `Mutations/LocalSemesterSaveExecutor.cs` | Fence gate: `LoadLiveGraphAsync` (extracted, unchanged query), `FenceRouter.EvaluateAsync` on the same ctx/tx, `ApplyIfPassedAsync` (gate, then writer), `RejectAndRestore` |
| `Mutations/SemesterGraphRestorer.cs` (new) | OD-7 R2: in-place restore of the caller's graph from the old graph |
| `Mutations/LocalSaveRejection.cs` (new) | Surfacing pieces that are not WPF: find a rejection, the user message, the attached restore failure |
| `App.xaml.cs` (code-behind) | One `if (LocalSaveRejection.TryFind(...))` branch in `DispatcherUnhandledException` |

Not touched: `Sync/**` (only read; mutants edited it temporarily and were reverted), `SqliteHocKyRepository.cs`,
`SemesterGraphWriter.cs`, `AppDbContext`, `SyncStamper`, any XAML, any VM, frozen specs, schema, packages,
port signatures. One existing test changed: P0-c (its own commit, `efe258a`).

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
- The models have no `INotifyPropertyChanged`, so a reverted scalar does not repaint a bound cell until the VM refreshes, and derived displays refresh on the next navigation. This is the status quo after any failed save.
- The restore mutates UI-bound collections. That relies on nothing under `Infrastructure/`, `Sync/` or `Data/` using `ConfigureAwait(false)` (FACT, grep: 0 hits), which the writer already relies on when a save succeeds.

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

## 8. Verification

- Baseline (TRX counters, before any edit, `6283231`): 1015 total / 1014 passed / 0 failed / 1 skipped
  (`SoeBaselineCaptureTests.CaptureBaseline_…`, skipped by design).
- Head `cd60611` (TRX counters): **1090 / 1089 / 0 / 1**. `dotnet build SmartStudyPlanner.slnx`: 0 errors, 0 warnings.
- Commit `4b0df27` alone: 1078 / 1077 / 0 / 1.
- **Scope: branch diff vs `origin/dev`, not `gitnexus_detect_changes`.** The GitNexus index points at the owner's
  checkout, not this worktree, and is stale (last indexed `6283231`). Changed production symbols:
  - `App.OnStartup` (handler lambda);
  - `LocalSemesterSaveExecutor.ExecuteAsync`, with new `LoadLiveGraphAsync`, `ApplyIfPassedAsync` and `RejectAndRestore`;
  - `SemesterReconcilePlanner.Plan`, with new private intent helpers;
  - new `SemesterReconcilePlan.Request`;
  - new types `SemesterGraphRestorer` and `LocalSaveRejection`.

  All are within the card's MAY-edit list.
- GitNexus `impact` before editing: `LocalSemesterSaveExecutor` **HIGH** (1 direct caller,
  `LuuHocKyAsync`, then 8 VM commands and the regression suites; priced as R-1 by the plan),
  `SemesterReconcilePlan` LOW, `App.OnStartup` LOW (0 callers). The Phase 0 miss is recorded below.

## 9. NOT RUN

- The WPF branch in `App.DispatcherUnhandledException` (MessageBox); only `LocalSaveRejection` is unit-tested.
- Any VM save command end to end (they show MessageBoxes); the port they call is tested instead.
- Repaint of reverted POCO scalars in the UI.
- X-2 (executor-level three-shape overlap). The router-level X-1 exists from Slice 2.
- E-2 with the real priority engine (stub engine used; see §7.1).

## 10. Follow-ups

- **E-2 owner ruling** (§7.1). Until then, D8-H rejects a resolution whose Base drifted, so the failure mode is loud, not silent.
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
- **Experience:** check who else holds an object graph before choosing between "replace" and "restore". The
  table in §3 shows that R1 would have failed X-20 through MainWindow's instance alone, which a per-VM test
  would never have shown.

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

### 11.4 Engineering decisions accepted by the owner (3b(1)–(3), not rulings)
1. **No direct Tombstone for tasks under a deleted MonHoc.** A direct intent outranks the cascade
   (RowEffect precedence) and would turn P-CR-3/P-PT-3 from `@CascadeReached` into `@DirectSubject`. M8 proves the
   tests see it.
2. **`UpdateFields(HocKy)`** although it is not in plan §6's table: the writer writes that row, so it belongs in the
   request. Owner requirement 4 verified that it is RouteKnown (M11).
3. **`Reparent(MonHoc, MaHocKy)` when the FK differs:** the writer copies `MaHocKy` verbatim, so every structural
   write is in the request even though no VM does this today.

### 11.5 Engineering decisions made in this slice (not ruled)
- **The restore failure travels in `Exception.Data`, not as an inner exception.** The frozen type has no such
  constructor. Owner requirement 5 is met (the rejection and its rule ids always reach the user; M7).
- **OQ-3:** a never-persisted HocKy is restored to an empty MonHoc list, with its scalars kept. This is the literal reading of
  "persisted state" for a row that has none, and it is only reachable through an AL-PT identity collision (pinned
  by a test).
- **The rejection wins over validation** when a save is both: the writer never runs on a rejected
  decision (N-6), so the unknown-MonHoc error is not reached.
- **Surfacing uses a type test in the existing handler, not a catch.** The rejection is already unhandled when
  it arrives; `Handled = true` is the handler's existing behaviour, now with a specific message.
- **P-PT local recipe and X-16 interpretation** (§4): both stated in the test headers as well.
- **A shared `LocalSaveDriver` test fixture** drives saves through the public port only, so every executor
  test observes what a VM observes.

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
