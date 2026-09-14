# Policy-driven Mutation Routing + Structural Conflict Fence — Implementation Plan

> **For agentic workers:** execute slice-by-slice with `superpowers:subagent-driven-development` or
> `superpowers:executing-plans`. Each slice has its own worktree from the fetched `origin/dev`, its own
> PR, and its own stop conditions (§24). Do not start a slice whose gate (§20) is still open.

| | |
|---|---|
| **Date** | 2026-09-13 |
| **Status** | `draft`. Planning only. No production code, test code, or PR produced by this document |
| **Goal** | Every mutation that can reach an unresolved StructuralConflict/ConstraintConflict gets an explicit impact set. That impact is routed to one case-specific, read-only policy per protected contract. Persistence happens only after the fence passes **and** the remaining business/persistence/transaction gates pass. The first delivery covers the only live mutation origin today, local UI saves. Sync-apply follows later behind an owner gate |
| **Spec** | `docs/specs/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-spec-complete.md`: owner-authored. It was untracked when this plan was written and is committed as-is alongside this plan (2026-09-14). SB-1 is closed (§20) |
| **Frozen authority** | `docs/specs/T2.3-T2.4-D1-D9-Decision-Record-updated.md` §23A (D9-T1..T6), D4, D8-G, D8-H · `docs/specs/T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md` (B-1..B-4, E-3) · `docs/specs/T2.3-T2.4-D4-D9-T4-Amendment-2026-09-10.md` |
| **Engineering inputs** | `docs/review/2026-09-11-t2.4-pr6-conflict-resolver-dor.md` · `docs/review/2026-09-13-w2-f2-semantic-analysis.md` · `docs/review/2026-09-12-t2.5-recon.md` |
| **Baseline observed** | `git fetch` then local `dev` = `origin/dev` = `4c7c840` (ahead/behind `0 0`). The working tree is **dirty** (owner files: `.claude/*`, `AGENTS.md`, `CLAUDE.md`, untracked spec + stale Decision-Record copy). Not touched, not synced |
| **Tech** | .NET 10 WPF, EF Core 10.0.5 SQLite, xunit 2.9.3. No new packages |

Labels used throughout: **FACT** (read in the tree at `4c7c840`) · **MEASURED** (observed by a run; none were
run in this planning session. Where cited, the measurement is from W-2/F-2 or the T2.5 recon) · **INFERENCE**
(code reading, not run) · **[R]** owner-ratified · **[D]** derived engineering design · **OD-n / SB-n** owner
decision / semantic blocker (§20).

---

## 1. Executive summary

**What the code actually looks like (this reshapes the spec's picture):**

1. **The only live mutation origin today is local UI.** FACT: `ServiceLocator.cs:54-63` registers no
   `SyncApplySession` and no `ConflictResolver`, and no production code constructs either. Both are
   test-harness-only in M2.1. Unresolved `ConflictRecord`s can therefore exist only in test databases
   today (INFERENCE: records are written only by `SyncApplySession` staging).
2. **There is no local delete entry point.** FACT: `XoaTask` (`QuanLyTaskViewModel.cs:124-135`) and
   `XoaMon` (`QuanLyMonHocViewModel.cs:89-99`) remove the item from the in-memory `HocKy` graph and call
   `IHocKyRepository.LuuHocKyAsync(HocKyHienTai)`. That call runs a Guid-diff reconcile of the **whole
   semester** in one transaction (`SqliteHocKyRepository.cs:81-219`). It infers deletes from *absence*,
   infers reparents from FK change, and mixes both with creates and scalar edits.
   `IStudyTaskRepository.DeleteAsync` has **zero production callers** (FACT, `data-model.md` §5 and grep).
   No UI path deletes a `HocKy` (FACT, `data-model.md` §4).
3. **Nothing outside `Sync/` consults conflict records** (FACT, PR-6 DoR §10.3; confirmed by grep). Every
   local mutation bypasses the D9-T6 lock today.
4. **Two independent cascade implementations exist.** Local: EF in-memory cascade fixup over *loaded*
   navigations (`AppDbContext.cs:102-137`) + `TaskCascadeHelper` for FK-only children + `SyncStamper`
   Deleted→tombstone (`SyncStamper.cs:60-68`). Sync: `SyncApplySession.CascadeTombstoneAsync`
   (`:589-645`), live children only.

**What this plan delivers:**

- A read-only fence in a new `Sync/Fence/` namespace:
  - a shape classifier keyed on record columns;
  - four pure case policies with **no `AppDbContext` parameter and no origin parameter**;
  - an impact resolver and a conflict-dependency selector (the only DB readers, `AsNoTracking`);
  - a deterministic router/aggregator.
- A behaviour-preserving extraction of `LuuHocKyAsync` into a pure reconcile **planner** (derives the
  `MutationIntent` set *before* any `Remove()`) and a **writer** (today's persistence code). A local
  **executor** runs `plan → fence → gates → write → save → commit` inside **one** context and **one**
  transaction.
- The same executor pattern for TaskNote/TaskReferenceLink mutations (`SqliteTaskEditorRepository`).
- Source-scan fences, in the style of `SyncApplyAuditFenceTests`, that go red on a write path that bypasses
  the executors and on any write inside the fence namespace.
- **Gated, not delivered until the owner rules:** wiring the fence into `SyncApplySession` (SB-2/OD-1). This
  is option C1 of the W-2/F-2 analysis. It flips a MEASURED shipped behaviour and is fenced by PR-6 DoR §14
  and T2.5 non-goal #8.

**Compatibility headline (FACT, §18).** No production code path can write a ConflictRecord (the only
`StageAsync` callers are in the unwired `SyncApplySession`).
Slices 1–5 therefore change **no user-visible behaviour** until sync-apply is wired. The fence is dormant
infrastructure proven by real-SQLite tests with staged records.

**Reviewer index:** the spec → policy → entry point → executor → repository → test trace is **§23**.

---

## 2. Scope / non-goals

### 2.1 In scope

| # | Item | Slice |
|---|---|---|
| S-1 | `MutationRequest`/`MutationIntent` model, `ImpactSet`, `StructuralDependencyRegistry` | 1–2 |
| S-2 | `ConflictShapeClassifier`, `ProtectedContract`, four case policies, `FencePolicyRegistry` | 1 |
| S-3 | `ImpactResolver`, `ConflictDependencySelector`, `FenceRouter`, `FenceDecision` | 2 |
| S-4 | Behaviour-preserving extraction of `LuuHocKyAsync` → `SemesterReconcilePlanner` + `SemesterGraphWriter` | 3 |
| S-5 | `LocalSemesterSaveExecutor` wired behind `IHocKyRepository.LuuHocKyAsync` | 4 |
| S-6 | `LocalTaskEditorExecutor` wired behind the four `ITaskEditorRepository` mutation methods | 5 |
| S-7 | Source-scan fences: fence namespace is read-only; synced-entity writes only via allowlisted writers | 2, 5 |
| S-8 | Measurement/characterization probes that later slices depend on | 0 |
| S-9 | `SyncApplySession` integration: **plan only, gated on OD-1** | 6 (gated) |

### 2.2 Non-goals (verbatim scope control plus what the code investigation adds)

- No generic resurrection, no Restore, no `LifecycleFacade`/`LifecycleRouter` (spec §10; direction L).
- No UI/XAML change. VM changes are limited to what OD-7 authorises, and none are planned by default.
- No schema change. FACT: record columns `Kind`, `EntityType`, `EntityId`, `FieldName`, `ConstraintKey`,
  `ConstraintValue`, `StructuralReason`, `LocalEntityId`, `BaseEntityId` plus the filtered unique index on
  `ScopeKey WHERE Status = 0` already support classification and indexed selection
  (`SyncConflictRecordSchema.cs:86-137`).
- No change to D1–D9/D9-T1..T6, B-1..B-4, E-3, D8-H drift gate, D9-T6 lock, PR-5 cascade, merge core,
  `SyncStamper`, `AppDbContext`, `SyncConflictRecordStore`, `SyncBaseSnapshotStore`, `ConflictStaging`,
  `ConflictResolver`.
- No automatic deferred/retry/re-cascade after rejection. No supersede/reopen/rebase. No cross-device
  resolution propagation (OPEN-2).
- No fix for adjacent defects found during investigation (D-1..D-4, §19). They are reported only.
- No property-based testing and no new test framework. The deterministic `[Theory]` matrix is sufficient,
  following T2.5 recon §4.3.
- No routing of `ConflictResolver` result writes (OD-6: recommended *not* wired; argument in §10.6).
- No routing of `StudyLog` writes. FACT: `StudyLog.MaTask` is `CopyOnCreate` with no FK and no cascade
  (`MergeSurfaceRegistry.cs:86-95`, `SyncApplySession.cs:584-586`), so it reaches no protected contract.

---

## 3. Source authority and semantic invariants

### 3.1 Precedence used by this plan

1. Frozen record D1–D9 / D9-T1..T6 (spec §2.2: "where this specification and a frozen rule appear to
   conflict, the frozen record wins").
2. The fence spec (owner-approved direction A–L restated in the task brief).
3. PR-6 rulings B-1..B-4, E-3 (resolution only).
4. PR-6 DoR, W-2/F-2 analysis, T2.5 recon: facts and measurements, never rulings.

### 3.2 Invariants every slice must preserve (each has a test in §16)

| ID | Invariant | Source |
|---|---|---|
| INV-1 | Protection is per concrete protected contract; no entity/tree lock | [R] A; spec §4.1 |
| INV-2 | Impact = rows ∪ edges (before **and** after) ∪ constraint scopes ∪ lifecycle effects, expanded only through the operation's **actual** cascade | [R] B; spec §3.1, §5.3 |
| INV-3 | Dependency selection and impact are separate computations | [R] C; spec §3.3 |
| INV-4 | Every applicable policy is evaluated; routing order is for determinism only; any `Blocked` blocks | [R] D; spec §7 |
| INV-5 | One policy per materially different shape; unknown shape ⇒ `Unsupported` ⇒ fail closed | [R] E; spec §7.2 |
| INV-6 | Origin is never a policy input (no local bypass) | [R] F; spec §9.1 |
| INV-7 | `FencePassed` is necessary, never sufficient. `MayPersist = RouteKnown ∧ FencePassed ∧ business ∧ persistence ∧ tx/concurrency` | [R] G; spec §3.4 |
| INV-8 | Router/policies never mutate persistent state (no tracker writes, no `SaveChanges`, no record/baseline/tombstone/retry state) | [R] H; spec §8.3 |
| INV-9 | Decision is separate from execution; repository persists an approved plan only | [R] I, J; spec §8 |
| INV-10 | Resolution ≠ mutation; a resolution may write no domain row (AL-PT `KeepBase`) | [R] K; spec §3.5; D7-G; B-2 (i) |
| INV-11 | No implicit resurrection; ordinary create cannot materialise an AL-PT identity | [R] L; spec §4.2 |
| INV-12 | Blocked ⇒ nothing persisted, no deferred work, no retry after resolution, no substitute reparent/resurrect | spec §9.2 |
| INV-13 | D9-T6 lock, D8-H drift gate, D8-G atomicity, M5 single hard-delete site remain unchanged | frozen; `SyncApplyAuditFenceTests` |

---

## 4. Current architecture / code-path findings

### 4.1 Mutation entry points (FACT unless labelled)

| Entry point | Callers (production) | What it can mutate | Transaction owner | Consults conflicts? |
|---|---|---|---|---|
| `IHocKyRepository.LuuHocKyAsync` → `SqliteHocKyRepository.cs:81-219` | `DashboardViewModel.LuuDuLieu` (:309), `.MoFocusMode` (:329); `QuanLyTaskViewModel.XoaTask` (:131), `.HoanThanhTask` (:145), `.ThemTask` (:215); `QuanLyMonHocViewModel.XoaMon` (:96), `.ThemMon` (:135); `SetupViewModel.TaoHocKy` (:88) | Create `HocKy`; create/update/**delete-by-absence** `MonHoc`; create/update/**reparent**/**delete-by-absence** `StudyTask`; cascade tombstones to TaskNote/TaskReferenceLink | Method itself: own context + `BeginTransactionAsync` (:94-95) | **No** |
| `IHocKyRepository.LayDanhSachHocKyAsync` (`:23-79`) | VMs (load) | **In memory only:** dedup merges MonHoc clones and sets `task.MaMonHoc = daiDien.MaMonHoc` (:57). The next `LuuHocKyAsync` persists that as an implicit **reparent** plus a clone **delete** | n/a | No |
| `ITaskEditorRepository.UpsertNoteAsync` (`SqliteTaskEditorRepository.cs:35-46`) | `QuanLyTaskViewModel.ThemTask` (:226) | Create TaskNote in scope `MaTask` or edit `Content` | Own context, no explicit tx | No |
| `.AddLinkAsync` / `.UpdateLinkAsync` / `.DeleteLinkAsync` (`:57-101`) | `ThemTask` (:235-242) | Link create/edit/tombstone | Own context | No |
| `IStudyTaskRepository.AddAsync/UpdateAsync/DeleteAsync` (`SqliteStudyTaskRepository.cs:39-65`) | **none** (only `GetAllAsync` is used: `WeightOptimizerViewModel.cs:141`, `OutcomeMaturationService.cs:30`) | `UpdateAsync` uses `DbSet.Update(detached)` and **can reparent**; `DeleteAsync` cascades via helper | Own context | No |
| `SyncApplySession.ApplyAsync` | **none** (harness) | Any synced entity; staging; cascade | Per-operation context + tx (`:103-148`) | Own scope only: D9-T6 lock `:205-212` |
| `ConflictResolver.ResolveAsync` | **none** (harness) | One row (result), record terminal transition | Per-call context + tx (`:47-89`) | Its own record |

### 4.2 Actual cascade envelopes

| Origin | Mechanism | Selection | Reaches |
|---|---|---|---|
| Local (`LuuHocKyAsync`) | `db.MonHocs.Remove(oldMon)` ⇒ EF cascade fixup over the **loaded** graph (`.Include(DanhSachMonHoc).ThenInclude(DanhSachTask)`, **no `IsDeleted` filter**, :98-101). `TaskCascadeHelper.RemoveChildrenAsync` for every task absent from the new graph (:183-189). `SyncStamper` converts `Deleted` → tombstone | Old graph unfiltered; new graph comes from `LayDanhSachHocKyAsync`, which filters `!IsDeleted` | MonHoc → StudyTask → {TaskNote, TaskReferenceLink} |
| Local (`DeleteAsync`, unused) | helper + `Remove` | helper has no `IsDeleted` filter (`TaskCascadeHelper.cs:19-23`) | StudyTask → {TaskNote, Links} |
| Sync | `CascadeTombstoneAsync` on live→dead only (`SyncApplySession.cs:279-280`) | live children only (`:595, :603, :611, :614`) | HocKy → MonHoc → StudyTask → {TaskNote, Links}; **not** StudyLog |

**INFERENCE, to be measured in Slice 0 (P0-a).** Every `LuuHocKyAsync` call may re-`Remove()` rows that
are *already* tombstoned: `oldTasksByMaTask` includes them, while `newTasksByMaTask` (from a filtered
load) never does. Each save would then re-stamp them (`Rev++`, new `DeletedAtUtc`). If measured, this is a
pre-existing defect (D-2). It is **not fixed** here. It still matters because the impact oracle (§7.4) must
know what "rows actually written" means.

### 4.3 Conflict records and their shapes (FACT)

- Classification columns: `SyncConflictRecordRow.cs:47-53`. `StructuralReason ∈ {ConcurrentReparent,
  ParentTombstoned}`, `ConflictKind ∈ {Field, Structural, Constraint, Tombstone}` (`MergeOutcomes.cs:11,17`).
- Shapes produced by staging (PR-6 DoR §2.1):

  | Shape | Record columns | Live at staging |
  |---|---|---|
  | S1-CR | Structural, `ConcurrentReparent`, local present | row rewritten to whole Base row |
  | S1-PT | Structural, `ParentTombstoned`, local present | row rewritten to whole Base row |
  | AL-PT | Structural, `ParentTombstoned`, **local absent**, Base null in every reachable case | no row |
  | S2 | Constraint TaskNote, Base present | `N0'` rewritten to `N0`; **unreachable in v1 by construction** |
  | S3 | Constraint TaskNote, Base null | no row; `N1` hard-deleted (M5) |

- `ScopeKey` formats: `ConflictKeys.cs:14-33`. Structural `"{EntityType}|{Guid:D}|{Field}"`, with the
  field being `MaHocKy` for MonHoc and `MaMonHoc` for StudyTask (`SyncApplySession.cs:786-801`). Constraint
  `"TaskNote|MaTask={Guid:D}"`.
- Access: `SyncConflictRecordStore` (static, db-first, never saves). `FindUnresolvedByScopeKeyAsync`
  (`:65-70`) is the indexed lookup. No delete API; triggers enforce immutable evidence and terminal
  Resolved.

### 4.4 Boundaries

- **SyncApplySession:** one op = one context + one tx. Rejection ⇒ rollback (`:122`). The lock precedes
  parent handling and writes (`:205-212`). The cascade skips the lock (W-2 F-2, **MEASURED**).
- **ConflictResolver:** B-1 context per call. Drift gate at step 6 (`:142-145`), B-2 at step 8
  (`:157-174`). It writes at most one row and never cascades (B-2/B-3). Unmarked write through
  `SyncStamper` (A2-c).
- **Validation today:** `LuuHocKyAsync` fails loud with `InvalidOperationException` when a task references
  a MonHoc not in the HocKy (`:193-195`). That is the one existing business-validation gate on the local
  save path. `UpdateLinkAsync` rejects tombstoned targets (`:80-82`).
- **Error surfacing today:** `App.xaml.cs:23-30` logs any dispatcher-unhandled exception and shows a generic
  "may not have been saved" MessageBox. INFERENCE: an exception escaping a `[RelayCommand]` async
  command reaches that handler, because the save commands do not set `FlowExceptionsToTaskScheduler`.
  Whether it sets `args.Handled` is **not verified** (pre-check in Slice 4).

### 4.5 Direct-repository-call surface (the bypass inventory)

All four VMs above call repositories directly (FACT). Repositories are DI singletons built from
`Func<AppDbContext>` (`ServiceLocator.cs:54-60`). Any code with an `AppDbContext` can also write
`DbSet.Remove/Update` directly. The only guard in the tree of that kind is `SyncApplyAuditFenceTests`, which
scans for physical deletes.

### 4.6 Test assets to reuse (FACT)

| Asset | Use |
|---|---|
| `Fixtures/SyncApplyFixture.cs` | Real in-memory SQLite, one shared connection, `EnsureTable` triggers, `SeedTreeAsync`, `SetBaselineAsync`, `From`, `Stamp`, `ReadConflictsAsync`, distinct device ids. **Limitation:** one shared connection means concurrency is unobservable |
| `Fixtures/TestDb.cs`, `TestDoubles/FailingSaveDbContext.cs` | Fail the N-th save inside a transaction |
| `Sync/Apply/SyncApplyParentHandlingTests.cs` (L, M, N, O) and `ConflictResolverTests`/`...ParentAndManualMergeTests` (ZP2, ZP5 AL-PT KeepBase no-write, ZN4, ZPT1..3) | Staging recipes for S1-CR, S1-PT (test-M), AL-PT, S3; resolution-without-mutation proof already exists |
| W-2/F-2 Appendix A probe | S1-CR + ancestor-tombstone recipe with control/direct/crossing legs |
| `Sync/Apply/SyncApplyAuditFenceTests.cs` | Source-scan pattern with scanner self-check (non-vacuous) |
| `Infrastructure/Persistence/RepositoriesTests.cs` | `LuuHocKyAsync` reconcile regression suite (delete-by-absence, cascade, clones, Rev stability) |

### 4.7 GitNexus blast radius (run in this session, index `Smart-Study`)

| Symbol | Risk | Direct | Notes |
|---|---|---|---|
| `SqliteHocKyRepository.LuuHocKyAsync` | **HIGH** | 19 | 8 VM commands + 10 repository tests + `TaoHocKy` |
| `TaskCascadeHelper.RemoveChildrenAsync` | **HIGH** | 2 (23 impacted) | via `LuuHocKyAsync` and `DeleteAsync` |
| `SqliteTaskEditorRepository.UpsertNoteAsync` | LOW | 2 | `ThemTask`, 1 repo test |
| `SyncApplySession.ApplyEntityAsync` | LOW | 2 (2 processes) | PR-6 DoR §5 rated `CascadeTombstoneAsync` HIGH; re-run before Slice 6 |

GitNexus counts are not deterministic across re-analysis (project memory). Re-run `impact` at each slice's
own baseline and do not compare symbol counts across runs.

---

## 5. Target architecture

```text
VM command (unchanged, OD-2 L1)
   │  IHocKyRepository.LuuHocKyAsync(graph) / ITaskEditorRepository.<mutation>
   ▼
Local*Executor  ── owns: one AppDbContext, one transaction (INV-7, §14)
   │ 1 load current state (tracked, NOT modified)
   │ 2 SemesterReconcilePlanner.Plan(old, new)  → MutationRequest{Origin=LocalApplication, Intents}
   │                                              + ValidationErrors   (pure; no Remove/DetectChanges)
   │ 3 FenceRouter.EvaluateAsync(db, request)   → FenceDecision           (read-only)
   │      ├ ImpactResolver.ResolveAsync          (AsNoTracking reads, StructuralDependencyRegistry)
   │      ├ ConflictDependencySelector.SelectAsync (AsNoTracking, ScopeKey ∪ identity predicates)
   │      ├ ConflictShapeClassifier.Classify     (pure)
   │      ├ FencePolicyRegistry → IConflictFencePolicy.Derive / Evaluate  (pure: no db, no origin)
   │      └ deterministic aggregation
   │ 4 gates: RouteKnown ∧ FencePassed → else rollback + throw MutationRejectedException(decision)
   │ 5 business validation (existing reconcile rules) → else rollback + throw (existing exception type)
   │ 6 SemesterGraphWriter.ApplyAsync(db, old, new, plan)   ← persistence only (today's code, moved)
   │ 7 SaveChangesAsync (SyncStamper), commit; any failure → rollback, dispose
   ▼
SQLite
```

- `Sync/Fence/*` owns semantics and reads. `Infrastructure/Persistence/SQLite/Mutations/*` owns execution
  and persistence. The fence never references `Infrastructure` (INV-9). The repository class keeps its port
  but no longer contains reconcile logic.
- **Why a new component is needed at all** (spec §8 vs current code): today semantics, planning, and
  persistence are fused inside `LuuHocKyAsync`, and the delete set is never represented as data. No
  existing seam can inspect "what will this save change" before EF's cascade fixup has already mutated the
  tracker (`Remove()` resolves dependents immediately, `SqliteHocKyRepository.cs:136-141`). Without
  extracting a pure planner, INV-2 and INV-12 cannot be proven.

---

## 6. Mutation request / context model

[D] Engineering names (spec O-1). `Sync/Fence/MutationRequest.cs`:

```csharp
public enum MutationOrigin { LocalApplication = 0, SyncApply = 1 }      // audit/diagnostics only (INV-6)

public enum MutationOperation { Create, UpdateFields, Reparent, Tombstone }

/// Structural (D4) or ConstraintScope (D5) field per MergeSurfaceRegistry.FieldClass.
public sealed record RelationChange(string Field, Guid? Before, Guid? After);

public sealed record MutationIntent(
    MutationOperation Operation,
    string EntityType,                       // SyncEntityTypes.*
    Guid EntityId,
    IReadOnlyList<RelationChange> Relations, // before AND after (spec §3.1)
    IReadOnlyList<string> ChangedFields);    // Merge-class fields only; Derived/NotMapped ignored (D9-T3)

/// One logical operation (D8-G): all intents persist atomically or none do.
public sealed record MutationRequest(MutationOrigin Origin, IReadOnlyList<MutationIntent> Intents);
```

Rules:

- An intent is declarative and has no side effects when routed (spec §3.1).
- `ChangedFields` compares *registry* Merge fields only (`MergeSurfaceRegistry.cs:59-109`). A change to
  `DiemUuTien`/`MucDoCanhBao` (Derived) produces no intent. No false "edit" intents on unchanged rows
  (guarded by the existing `HocKyRepository_ResaveWithNoChanges_DoesNotBumpRevOfUnrelatedRows` plus a new
  planner test).
- **RouteKnown** [D] = every `EntityType` is in `StructuralDependencyRegistry` and every `RelationChange.Field`
  is a Structural/ConstraintScope field of that type. Otherwise `RouteKnown = false` ⇒ fail closed.
- `Origin` is carried on `MutationRequest` only. `IConflictFencePolicy` does not receive it (INV-6 by
  construction, and a source-scan asserts it).

Local-save intent derivation (`SemesterReconcilePlanner`, pure). It reuses today's reconcile rules as data:

| Reconcile branch today | Intent(s) |
|---|---|
| `hocKyCu == null` (:103-106) | `Create(HocKy)` + `Create` for each MonHoc/StudyTask in graph |
| old MonHoc absent in new (:153-164) | `Tombstone(MonHoc m)` |
| new MonHoc not in old (:166-177) | `Create(MonHoc)` with `RelationChange(MaHocKy, null, h)` |
| MonHoc present in both, Merge fields differ | `UpdateFields(MonHoc)` |
| task FK differs (:142-150) | `Reparent(StudyTask t)` with `RelationChange(MaMonHoc, old, new)` |
| old task absent in new (:183-189) | `Tombstone(StudyTask t)` (cascade expansion is the ImpactResolver's job, not the planner's) |
| new task not in old (:203-207) | `Create(StudyTask)` with `RelationChange(MaMonHoc, null, m)` |
| task present in both, Merge fields differ (:197-202) | `UpdateFields(StudyTask)` |
| task references MonHoc not present (:193-195) | **ValidationError** (business gate, step 5), not an intent |

Task-editor intents: `UpsertNoteAsync` ⇒ `Create(TaskNote, MaTask: null→t)` when no live note, else
`UpdateFields(TaskNote,[Content])`. `AddLinkAsync` ⇒ `Create(TaskReferenceLink, MaTask: null→t)`.
`UpdateLinkAsync` ⇒ `UpdateFields`. `DeleteLinkAsync` ⇒ `Tombstone(TaskReferenceLink)`.

---

## 7. Impact-set resolution design

### 7.1 Types (`Sync/Fence/ImpactSet.cs`)

```csharp
public enum RowEffect { Created, FieldsChanged, Reparented, Tombstoned, CascadeTombstoned }
public sealed record ImpactRow(string EntityType, Guid EntityId, RowEffect Effect, bool WasLive, Guid CausedByEntityId);

public enum EdgeChange { Removed, Added }
public sealed record ImpactEdge(string ChildType, Guid ChildId, string Field, Guid ParentId, EdgeChange Change);

public enum ScopeChange { Acquired, Released, OccupantContentChanged }
public sealed record ImpactScope(string ScopeKey, Guid ScopeValue, ScopeChange Change, Guid OccupantId);

public enum LifecycleEffect { Create, Tombstone }
public sealed record ImpactLifecycle(string EntityType, Guid EntityId, LifecycleEffect Effect);

public sealed record ImpactSet(IReadOnlyList<ImpactRow> Rows, IReadOnlyList<ImpactEdge> Edges,
                               IReadOnlyList<ImpactScope> Scopes, IReadOnlyList<ImpactLifecycle> Lifecycle);
```

### 7.2 `StructuralDependencyRegistry` (`Sync/Fence/StructuralDependencyRegistry.cs`)

Why a new registry: spec A-1 requires every relation to have an explicit impact entry. `MergeSurfaceRegistry`
classifies *fields* but has no parent-type or cascade information, and it is deliberately free of
`Models` types. The cascade edges exist today only as code, in three places (`AppDbContext.OnModelCreating`,
`TaskCascadeHelper`, `CascadeTombstoneAsync`). The new registry reuses `FieldClass` rather than inventing a
second field taxonomy.

| Parent | Child | Child field | `FieldClass` (from `MergeSurfaceRegistry`) | Cascade |
|---|---|---|---|---|
| HocKy | MonHoc | MaHocKy | Structural | tombstone live children |
| MonHoc | StudyTask | MaMonHoc | Structural | tombstone live children |
| StudyTask | TaskNote | MaTask | ConstraintScope | tombstone live children |
| StudyTask | TaskReferenceLink | MaTask | CopyOnCreate | tombstone live children |
| StudyTask | StudyLog | MaTask | CopyOnCreate | **none** (no FK) |

A guard test (`StructuralDependencyRegistryGuardTests`) reconciles this table against three sources:
1. EF's model: every FK with `OnDelete(Cascade)` appears, and no other.
2. `MergeSurfaceRegistry`: field classes agree.
3. `SyncApplySession.StructuralFieldOf`: via behaviour, the staged ScopeKeys agree.

This follows the `MergeSurfaceRegistryGuardTests` precedent.

### 7.3 `ImpactResolver.ResolveAsync(AppDbContext db, MutationRequest req, CancellationToken ct)`

Read-only (`AsNoTracking` only). Per intent:

- **Create(E):** row `Created`; lifecycle `Create(E)`; edge `Added` for each `RelationChange.After`. For
  TaskNote: scope `Acquired(TaskNote|MaTask=t)`.
- **UpdateFields(E):** row `FieldsChanged`. For TaskNote: scope `OccupantContentChanged(K)`.
- **Reparent(E):** row `Reparented`; edge `Removed(before)` + `Added(after)`. If the field is
  ConstraintScope: `Released(old K)` + `Acquired(new K)`. Operation-defined dependents: none today (D4
  reparent moves only the row, FACT `SqliteHocKyRepository.cs:142-150`).
- **Tombstone(E):** row `Tombstoned` (WasLive from DB); lifecycle `Tombstone(E)`; edge `Removed(E→parent)`.
  If E is a TaskNote: `Released(K)`. Then **expand the actual cascade** recursively through registry edges
  with `Cascade = tombstone live children`, querying children by FK with the same predicate the executing
  path uses. Each reached child adds a `CascadeTombstoned` row, a `Removed` edge, and `Released(K)` for
  reached TaskNotes.
- Deduplicate rows by `(EntityType, EntityId)`, keeping the strongest effect. Order all lists
  deterministically (EntityType ordinal, EntityId).

**Actual, not invented closure (INV-2).** The resolver expands only through registry cascade edges on a
`Tombstone` intent. A `Reparent`/`UpdateFields`/`Create` never adds descendants.

### 7.4 The oracle that makes "actual cascade" provable

`ImpactPredictionMatchesObservedWritesTests` (Slice 4; it consumes `ImpactResolver` from Slice 2, so it
cannot be Slice 3's oracle). For each topology in the §16 matrix, with **no**
unresolved records:
1. Snapshot every domain row `(type, id, IsDeleted, parent FK, Rev)`.
2. Compute `ImpactResolver` output for the planner's request.
3. Run the real save.
4. Diff the snapshots.

Assert that the set of rows whose live→dead state or FK changed equals the predicted `Rows` with
`WasLive = true`.

Slice 0 measurement P0-a decides whether re-stamped already-dead rows are excluded from the oracle (and
reported as D-2) or modelled. Mutation proof: delete the TaskNote branch of the cascade expansion ⇒ the
`MonHoc`-delete topology goes RED.

---

## 8. Conflict dependency / routing design

`ConflictDependencySelector.SelectAsync(AppDbContext db, ImpactSet impact, CancellationToken ct)`
(read-only). It selects `Status == Unresolved` records matching **either** predicate, deduplicated by
`ConflictId`:

1. **ScopeKey predicate (indexed).** `ScopeKey ∈ K(impact)`, where `K` =
   - `ConflictKeys.ScopeKey(Structural, type, id, StructuralFieldOf(type))` for every MonHoc/StudyTask
     appearing in `Rows`, in `Lifecycle`, or as an edge child;
   - every `ImpactScope.ScopeKey`.

   This uses the `IX_SyncConflictRecords_OneUnresolvedPerScope` path. No new index is needed.
2. **Identity predicate (fail-closed net).** `EntityId ∈ ids(impact)` **or** `ConstraintValue ∈
   {task ids in impact}`. This catches an unresolved record whose ScopeKey format is unknown to this code
   (future field, corrupted row). That record then classifies as `Unsupported` and fails closed (INV-5)
   instead of being silently missed.

Conflict dependency ≠ impact (INV-3). Selection says *which contracts must inspect*; the policy decides
*whether the impact violates*. A selected record can return `Passed` (§10, R12 test).

**Routing stage** (for ordering only, spec §7.1 step 4), assigned per `(record, impact)`:
`DirectSubject` when the record's subject is the target of an intent; `CascadeReached` when reached only
via cascade rows; `ConstraintScope` when reached only via an `ImpactScope`.

---

## 9. Protected-subject / contract representation

`Sync/Fence/ProtectedContract.cs`:

```csharp
public enum ConflictShape { Unsupported = 0, ConcurrentReparent, ParentTombstoned, AbsentLocalParentTombstoned, ConstraintOccupancy }
public enum ConstraintForm { BasePresent /*S2*/, BaseNull /*S3*/ }

public abstract record ProtectedSubject;
public sealed record EntitySubject(string EntityType, Guid EntityId) : ProtectedSubject;          // S1-CR, S1-PT
public sealed record AbsentIdentitySubject(string EntityType, Guid EntityId) : ProtectedSubject;  // AL-PT
public sealed record ConstraintScopeSubject(string ScopeKey, Guid ScopeValue) : ProtectedSubject; // Constraint

/// The structural reference frame. HeldParentId = Base parent currently held live (S1), or the
/// tombstoned Remote parent the absent candidate points at (AL-PT).
public sealed record ProtectedEdge(string ChildType, Guid ChildId, string Field, Guid? HeldParentId);

public sealed record ProtectedContract(
    Guid ConflictId, string ScopeKey, ConflictShape Shape,
    ProtectedSubject Subject, ProtectedEdge? Edge,
    IReadOnlyList<string> Predicates,        // named, e.g. "S1CR.SubjectPresent", "S1CR.EdgeUnchanged"
    ConstraintForm? Form, IReadOnlyList<Guid> CandidateIds);
```

`ConflictShapeClassifier.Classify(SyncConflictRecordRow)` is pure. It keys on columns, not names, following
the E-11 precedent:

| `Kind` | `EntityType` / field | `StructuralReason` | `LocalEntityId` | `BaseEntityId` | Shape |
|---|---|---|---|---|---|
| Structural | MonHoc/`MaHocKy` or StudyTask/`MaMonHoc` | ConcurrentReparent | present | present | `ConcurrentReparent` |
| Structural | same | ParentTombstoned | present | present | `ParentTombstoned` |
| Structural | same | ParentTombstoned | **absent** | absent | `AbsentLocalParentTombstoned` |
| Constraint | TaskNote, `ConstraintKey = "MaTask"`, `ConstraintValue` parses as Guid | null | present | present ⇒ S2 / null ⇒ S3 | `ConstraintOccupancy` |
| anything else (incl. `Status ≠ Unresolved`, unknown enum ints, CR with absent local, PT-absent-local with Base present) | | | | | `Unsupported` |

The distinction the spec requires (§2.4) is carried by the types:
- `ConflictRecord` = `SyncConflictRecordRow` (evidence plus lifecycle; the fence never writes it);
- protected subject = `ProtectedSubject`;
- reference frame = `ProtectedEdge`;
- state predicates = `Predicates`;
- resolution stays exclusively inside `ConflictResolver` (unchanged).

`Derive` reads only the row's columns and its immutable evidence JSON (`CanonicalJson.Read`, as
`ConflictResolver.TryDeriveResult` already does). It never reads live state.

---

## 10. Case-specific policy design

Common interface (`Sync/Fence/IConflictFencePolicy.cs`):

```csharp
internal interface IConflictFencePolicy
{
    ConflictShape Shape { get; }
    ProtectedContract Derive(SyncConflictRecordRow unresolved);          // pure
    PolicyResult Evaluate(ProtectedContract contract, ImpactSet impact); // pure: NO AppDbContext, NO MutationOrigin
}

public enum FenceOutcome { NotApplicable, Passed, Blocked, Unsupported }
public enum RoutingStage { DirectSubject = 0, CascadeReached = 1, ConstraintScope = 2 }
public sealed record PolicyResult(Guid ConflictId, string ScopeKey, ConflictShape Shape, ProtectedSubject Subject,
                                  FenceOutcome Outcome, RoutingStage Stage, string RuleId, string Evidence);
```

`FencePolicyRegistry`: exactly one policy per non-`Unsupported` shape. Its static constructor asserts
completeness. `Unsupported` never reaches a policy; the router emits `FenceOutcome.Unsupported` itself.

Per the task brief §3. The rules below are the implementation contract. Cells marked **SB-3** or **OD-4**
return their outcome through `FencePendingDecisions` (§10.0), with tests labelled as characterization, **not** as a ruling.

### 10.0 Pending owner decisions live in one place

```csharp
// Sync/Fence/FencePendingDecisions.cs
internal static class FencePendingDecisions
{
    // SB-3 / OD-3: spec §6 row 1 reading. Spec §2.2 ("frozen wins") + shipped whole-row D9-T1 point to Blocked.
    public const FenceOutcome NonStructuralEditOnHeldRow = FenceOutcome.Passed;

    // OD-4: literal spec §6 reading (S3 empty scope, owning task tombstoned; occupancy not reached).
    public const FenceOutcome EmptyScopeParentTombstone = FenceOutcome.Passed;
}
```

- Rules `S1CR.NonStructuralFields`, `S1PT.NonStructuralFields` and `CONS.OccupantContent` return
  `NonStructuralEditOnHeldRow`.
- Rule `CONS.EmptyScopeParentTombstoned` returns `EmptyScopeParentTombstone`.

**Behavioural proof that every cell uses the switch.** Mutate `NonStructuralEditOnHeldRow` to `Blocked` and
P-CR-6, P-PT-6 and P-K-4 must all turn RED. Mutate `EmptyScopeParentTombstone` and P-K-5 must turn RED. A
policy that hard-codes `Passed` survives the mutant and is caught.

**This is authored behaviour, not status quo.** No fence exists today, so no prior rule is being pinned.
The permissive reading is shipped because it adds no rejection path and no user-visible change. For the
record, spec §2.2 with the whole-row D9-T1 reading argues for `Blocked`. Cells the plan resolves toward
`Blocked` (e.g. §10.2 "deleting E always crosses") need no owner decision, because fail-closed is §17's
default. Resolving toward `Passed` does, hence SB-3/OD-4.

### 10.1 `ConcurrentReparentFencePolicy` (S1-CR)

| Aspect | Content |
|---|---|
| Protected subject/scope | `EntitySubject(E)` (MonHoc or StudyTask); ScopeKey `E|id|field` |
| Protected relation/state | `ProtectedEdge(E, field, HeldParentId = Base parent)`; predicates `SubjectPresent` (E live), `EdgeUnchanged` |
| Relevant impact | any row/edge/lifecycle naming E |
| **Blocked** (RuleId) | `Reparent(E)` ⇒ `S1CR.EdgeReplaced`; `Tombstone(E)` or E in `CascadeTombstoned` rows ⇒ `S1CR.SubjectRemoved` (the latter at stage `CascadeReached`); `Create` of E's identity while E exists ⇒ `S1CR.SubjectReplaced` (defensive) |
| **Passed** | impact names E only as a parent endpoint of a child edge (child create/delete under E) ⇒ `S1CR.ChildEdgeOnly`; `UpdateFields(E)` with no edge change ⇒ `S1CR.NonStructuralFields` (**SB-3**) |
| **NotApplicable** | impact never names E |
| Evidence | intent/impact element string, e.g. `Reparent StudyTask {id} MaMonHoc {A}->{B}` |
| Tests | §16 rows P-CR-1..6 |

### 10.2 `ParentTombstoneFencePolicy` (S1-PT)

| Aspect | Content |
|---|---|
| Protected subject/scope | `EntitySubject(E)` in the frame of its required structural parent |
| Protected relation/state | `ProtectedEdge(E, field, HeldParentId = Base parent)`; predicates `SubjectPresent`, `EdgeUnchanged` (the staged candidate remains subject to E live; no generic path may bypass the tombstoned-parent contract). [D] Deleting E always crosses the contract: spec §3.2 names "E live" as part of the frame. The spec's "when it crosses" therefore evaluates true for any removal of E |
| Blocked | `Reparent(E)` ⇒ `S1PT.ParentFrameChanged`; `Tombstone(E)` / cascade-reached E ⇒ `S1PT.SubjectRemoved`; `Create` of E's identity ⇒ `S1PT.SubjectReplaced` |
| Passed | child-edge-only ⇒ `S1PT.ChildEdgeOnly`; `UpdateFields(E)` ⇒ `S1PT.NonStructuralFields` (**SB-3**) |
| NotApplicable | impact never names E. Explicitly **not** protected: E's siblings, E's parent's other children, E's descendants (spec §4) |
| Tests | P-PT-1..6 |

### 10.3 `AbsentLocalParentTombstonePolicy` (AL-PT)

| Aspect | Content |
|---|---|
| Protected subject/scope | `AbsentIdentitySubject(E)`; `ProtectedEdge(E, field, HeldParentId = Remote parent (tombstoned))` |
| Protected state | predicate `RemainsAbsent`: no ordinary path materialises E |
| Blocked | `Create(E)` (any origin, any parent) ⇒ `ALPT.SameIdentityMaterialization`; any intent whose target is E (`UpdateFields`/`Reparent`/`Tombstone` of an absent id) ⇒ `ALPT.AbsentIdentityTargeted` |
| Passed | impact names E's tombstoned parent P only (e.g. re-stamp of dead P) ⇒ `ALPT.FrameUnchanged` |
| NotApplicable | impact never names E. An ancestor delete never reaches absent E (INFERENCE: nothing to cascade, W-2 §2.5) |
| Never | "absent, so create is harmless" (spec §4.2); inferring a new identity |
| Tests | P-AL-1..4 |

### 10.4 `ConstraintOccupancyFencePolicy` (S2, S3, null-Base)

| Aspect | Content |
|---|---|
| Protected subject/scope | `ConstraintScopeSubject(TaskNote|MaTask=t)`; `CandidateIds` = {Base, Local, Remote} ids; `Form` S2/S3 |
| Protected state | S2: occupant `N0` stays the occupant; S3: scope has no row (D9-T1 null Base) |
| Blocked | `ImpactScope(K, Acquired)` ⇒ `CONS.ScopeAcquired` (S3 note create, the local `UpsertNoteAsync` path); `ImpactScope(K, Released)` ⇒ `CONS.ScopeReleased` (S2 note tombstone, or cascade from its task); a TaskNote reassignment touching K as old **or** new scope ⇒ `CONS.RelationChanged` |
| Passed | `OccupantContentChanged(K)` on S2 ⇒ `CONS.OccupantContent` (**SB-3**); owning task `Tombstone` with S3 empty scope ⇒ `CONS.EmptyScopeParentTombstoned` (**OD-4**: literal spec reading, since occupancy is not reached) |
| NotApplicable | impact names neither K nor any candidate id; any other scope |
| Tests | P-K-1..7 |

### 10.5 What makes each policy materially different (spec E)

S1-CR protects a *contested edge*. S1-PT protects a *parent frame*: same mechanics today, but the
rule ids, evidence, and future divergence differ, and `Derive` reads a different reason. AL-PT protects
*absence*. Constraint protects *occupancy* independent of hierarchy. The classifier routes by column tuple.
A new shape without a registered policy is `Unsupported` (O-2).

### 10.6 Why `ConflictResolver` is not routed (OD-6)

INFERENCE, from B-2/B-3 and the code:
- the resolver writes at most one row, the record's own subject (`ConflictResolver.cs:190-199`);
- it never cascades;
- E-3 pins entity-scoped identity;
- D9-T6 guarantees no second Unresolved record on the same scope.

A resolution write can therefore change only the resolving record's own edge or scope, and routing would
first have to exclude that record [D]. No reachable cross-record violation remains. Routing would add a new
rejection path to frozen PR-6 behaviour for no reachable benefit. Recommendation: do not route. The owner
confirms under OD-6.

---

## 11. Overlap / routing matrix

### 11.1 Concrete mapping of the spec's topology

FACT: only MonHoc and StudyTask have D4 fields (`SyncApplySession.cs:786-791`), so an S1 record can sit only
on those. For `A → T[S1-PT] → N[S1-CR]`: **A = HocKy, T = MonHoc, N = StudyTask**. An N-owned TaskNote scope
`K(N)` can additionally carry a Constraint record. Local reachability: HocKy has no local delete
(FACT), so "delete A" is exercised at **router level** with a constructed `MutationRequest`. The router is
origin-agnostic, so this is a valid proof. Executor level covers it only after Slice 6.

### 11.2 Mutation × shape outcomes (router-level; every applicable policy evaluated)

| Mutation | S1-PT(T) | S1-CR(N) | Constraint K(N) (S2 form) | Aggregate |
|---|---|---|---|---|
| delete A (HocKy) — cascade T, N, N's note | Blocked `S1PT.SubjectRemoved` @CascadeReached | Blocked `S1CR.SubjectRemoved` @CascadeReached | Blocked `CONS.ScopeReleased` @ConstraintScope | **Blocked**, 3 results, all reported |
| delete T (MonHoc) — cascade N, note | Blocked `SubjectRemoved` @Direct | Blocked @CascadeReached | Blocked @ConstraintScope | **Blocked**, 3 results |
| delete N (StudyTask) — cascade note, links | NotApplicable (T appears only as a removed edge's parent endpoint ⇒ `Passed ChildEdgeOnly`; see note) | Blocked `SubjectRemoved` @Direct | Blocked `ScopeReleased` | **Blocked** |
| edit N non-structural fields | NA | Passed `NonStructuralFields` (**SB-3**) | NA | FencePassed → remaining gates |
| reparent N (T → T2) | Passed `ChildEdgeOnly` (T is the old endpoint) | Blocked `EdgeReplaced` | NA | **Blocked** |
| create note in K(N) (S3 form) | NA | Passed `ChildEdgeOnly` | Blocked `ScopeAcquired` | **Blocked** |
| edit N's note content (S2 form) | NA | NA | Passed `OccupantContent` (**SB-3**) | FencePassed |
| delete sibling N2 under T (no records on N2) | Passed `ChildEdgeOnly` | NA | NA | FencePassed (INV-1) |
| reassign note K1 → K2 (router-level; no local writer assigns `TaskNote.MaTask`, FACT) | — | — | Blocked for K1 and for K2, independently | **Blocked**, 2 results |

Note on "delete N" vs S1-PT(T): an edge removal whose *parent* is T is relevant (T is named) but does not
cross T's contract ⇒ `Passed` with evidence, not `NotApplicable`. This is the spec §3.3 distinction
"relevant ≠ violates". Spec §7.3 says S1-PT(T) "is not automatically relevant solely because N is under T".
The plan honours that: T's policy is invoked because T is *named by an edge*, not because of ancestry, and
it passes.

### 11.3 Aggregation algorithm (`FenceRouter`, spec §7.1)

1. Impact (§7).
2. Select (§8).
3. Classify each record.
4. Assign routing stage.
5. Deduplicate identical `(ConflictId, ScopeKey)` pairs only.
6. Route each pair to its one policy, or emit `Unsupported`.
7. Evaluate **all** pairs: no early exit.
8. Sort results by `(Stage, ScopeKey ordinal, ConflictId)`.
9. Build `FenceDecision(Results, RouteKnown)`.

`FencePassed ⇔ RouteKnown ∧ ∀r: r ∈ {NotApplicable, Passed}`. No global conflict-kind priority exists
anywhere in the code. A source-scan fails on any comparer or ordering keyed on `ConflictShape`/`ConflictKind`
inside `Sync/Fence`.

---

## 12. Local delete integration

### 12.1 Traced local delete paths (FACT)

```text
XoaTask(t)  → MonHocHienTai.DanhSachTask.Remove(t) → LuuHocKyAsync(HocKyHienTai)
XoaMon(m)   → HocKyHienTai.DanhSachMonHoc.Remove(m) → LuuHocKyAsync(HocKyHienTai)
(load-time) LayDanhSachHocKyAsync dedup → clone MonHoc dropped + tasks reparented in memory → next LuuHocKyAsync
ThemTask    → DeleteLinkAsync(dead.Id) for links removed in the editor
(unused)    IStudyTaskRepository.DeleteAsync
```

### 12.2 Integration (recommended option **L1**, OD-2)

`SqliteHocKyRepository.LuuHocKyAsync(hocKy, ct)` becomes
`new LocalSemesterSaveExecutor(_ctxFactory).ExecuteAsync(hocKy, ct)`. The VM-facing port is unchanged.
Reconcile logic moves out of the repository class. Each requirement below maps to a mechanism:

| Requirement | Mechanism | Proof |
|---|---|---|
| No local bypass | every delete/reparent in a local save is an intent in the same `MutationRequest`, evaluated by the same router as any origin | `FenceRouterTests.SameImpact_LocalAndSyncOrigin_IdenticalDecision`; source-scan: no `Origin` reference under `Sync/Fence/Policies` |
| No partial cascade before approval | planner is pure; `Remove()`/`DetectChanges()` live only in `SemesterGraphWriter`, which runs after the decision; blocked ⇒ rollback + dispose | `LocalSemesterSaveFenceTests.BlockedDelete_LeavesEveryTableByteIdentical`; mutant "writer before fence" goes RED on the tracker-state assertion inside the executor (see §17 N-6) |
| No implicit deferred delete | no new table, no queue, nothing persisted on reject | table-count/row-checksum assertion; source-scan: no new `DbSet` |
| No automatic retry after resolution | executor is stateless; nothing records the rejected request | `BlockedDelete_ThenResolveKeepBase_ParentStillLive` |
| No hidden fallback to resurrection/reparenting | on reject, nothing is applied at all | `BlockedMonHocDelete_TaskStillUnderSameParent_NoRowChanged` |

**Price of L1** (stated, not hidden):
- HIGH blast radius on `LuuHocKyAsync` (19 direct callers). Mitigated by Slice 3's behaviour-preserving
  extraction under the existing `RepositoriesTests`.
- The port keeps the misleading name "repository" although it now fronts an executor. Recorded as a
  follow-up.
- The **compound-save** consequence: one blocked intent rejects the whole semester save, including
  unrelated edits made in the same save. The VM's in-memory graph still lacks the deleted row, so the
  **next** save re-derives the same blocked intent, and a later sibling-only edit is refused too (sticky
  rejection). Saying "the same request contains a violating intent" is the weaker reading. Seen from the
  entry point, a sibling edit is refused because of T's conflict. **OD-7's reload-on-reject is therefore
  not UX polish. It is the mechanism that makes spec §13.5 (unrelated sibling not rejected) true on the
  local path** (X-20, §22 item 5).

**Alternative L2**: new application service with explicit `DeleteTask`/`DeleteSubject`/`Reparent` use cases;
VMs call it instead of `LuuHocKyAsync`. It matches the spec's literal shape and gives per-intent
granularity. Price: 7 call sites in 4 VMs, VM constructors, VM test doubles (`FakeHocKyRepository` in
`TestDoubles/FakeRepositories.cs` plus local fakes), and `LuuHocKyAsync` would still need the fence for its
residual add/update path. Not recommended for this scope; the owner decides.

---

## 13. Decision vs executor vs repository boundary

| Component | File | May | Must not |
|---|---|---|---|
| `FenceRouter`, `ImpactResolver`, `ConflictDependencySelector` | `Sync/Fence/` | `AsNoTracking` queries on the caller's `db` | `Add/Update/Remove/Attach`, `SaveChanges`, `MarkSyncApplied`, `SyncConflictRecordStore.StageAsync/MarkResolvedAsync`, `SyncBaseSnapshotStore.UpsertAsync`, `ExecuteSql*`, begin/commit tx |
| Policies, classifier, registry | `Sync/Fence/`, `Sync/Fence/Policies/` | pure functions over row + `ImpactSet` | take `AppDbContext` or `MutationOrigin`; any I/O |
| `LocalSemesterSaveExecutor`, `LocalTaskEditorExecutor` | `Infrastructure/Persistence/SQLite/Mutations/` | own context + tx, call router, enforce gates, call writer, save, commit/rollback, dispose | invent/override a fence result; write conflict records; retry |
| `SemesterGraphWriter`, `TaskEditorWriter` | same folder | tracker writes for an already-approved plan (today's code) | call the router; decide; `SaveChanges` (executor saves) |
| `SqliteHocKyRepository` / `SqliteTaskEditorRepository` | unchanged paths | reads; delegate mutations to executors | contain reconcile or conflict logic |

Enforcement: `FenceSourceFenceTests` (namespace read-only) and `SyncedEntityWritePathFenceTests` (allowlist of
files that may call `Remove(`/`RemoveRange(`/`.Update(`/`.Add(` on the six synced DbSets, or assign
`.MaMonHoc =`/`.MaHocKy =`). The allowlist is `SemesterGraphWriter.cs`, `TaskEditorWriter.cs`,
`TaskCascadeHelper.cs`, `SqliteHocKyRepository.cs` (read-time dedup only, :57), `SyncApplySession.cs`,
`ConflictResolver.cs`, `EntitySnapshotMapper.cs`, `SqliteStudyTaskRepository.cs` (OD-5), and the telemetry
repos for non-synced tables. Both source scans carry a non-vacuity self-check, copied from
`SyncApplyAuditFenceTests.TheScanner_ActuallyMatchesTheKnownSite`.

---

## 14. Concurrency / TOCTOU / final revalidation

**Decision [D]: evaluate the fence inside the executor's own write transaction, on the same context, once.**
No separate pre-check whose result is trusted later. This closes TOCTOU by construction instead of by a
revalidation step that could itself be skipped.

- In-process threat: `SyncApplySession`/`ConflictResolver` use separate contexts and connections. Neither
  is wired in production today (FACT), so the threat is future-facing.
- SQLite semantics (A-3, **to be MEASURED in Slice 0, P0-b**): a transaction spanning read-then-write
  either holds the write lock from `BEGIN` (immediate mode) or fails its later write upgrade with
  `SQLITE_BUSY` if another writer committed in between. It never commits a write based on a stale read. The
  probe determines which mode `Database.BeginTransactionAsync()` uses on EF Core 10.0.5 / Microsoft.Data.Sqlite.
- **The shared-connection fixture cannot observe this** (`SyncApplyFixture` shares one in-memory
  connection). Slice 0 adds a file-backed two-connection fixture (`Fixtures/FileBackedSqliteFixture.cs`,
  temp file, deleted on dispose).
- Final revalidation beyond the fence stays owned by the existing mechanisms: `SyncConflictRecordStore`'s
  unique indexes (D9-T6), triggers (D7-D/F), and the resolver's D8-H drift gate. The executor must **not**
  catch and swallow `SqliteException`/`DbUpdateException`; failure ⇒ rollback ⇒ rethrow.
- Human-scale staleness (a VM graph loaded before staging) is handled by construction: the fence reads the
  DB at save time. Stale structural changes are blocked. Stale content edits pass under SB-3 status quo.
  A resolver-then-stale-save revert is D-4 (reported, not addressed).

---

## 15. Detailed file / module change list

### 15.1 Production: new files

| File | Slice | Responsibility |
|---|---|---|
| `SmartStudyPlanner/Sync/Fence/MutationRequest.cs` | 1 | §6 types |
| `SmartStudyPlanner/Sync/Fence/ImpactSet.cs` | 1 | §7.1 types |
| `SmartStudyPlanner/Sync/Fence/ProtectedContract.cs` | 1 | §9 types |
| `SmartStudyPlanner/Sync/Fence/FenceResults.cs` | 1 | `FenceOutcome`, `RoutingStage`, `PolicyResult`, `FenceDecision` |
| `SmartStudyPlanner/Sync/Fence/ConflictShapeClassifier.cs` | 1 | §9 table |
| `SmartStudyPlanner/Sync/Fence/IConflictFencePolicy.cs` | 1 | interface |
| `SmartStudyPlanner/Sync/Fence/FencePolicyRegistry.cs` | 1 | one policy per shape, completeness assert |
| `SmartStudyPlanner/Sync/Fence/FencePendingDecisions.cs` | 1 | the single home of outcomes awaiting owner decisions (SB-3, OD-4); see §10.0 |
| `SmartStudyPlanner/Sync/Fence/Policies/ConcurrentReparentFencePolicy.cs` | 1 | §10.1 |
| `SmartStudyPlanner/Sync/Fence/Policies/ParentTombstoneFencePolicy.cs` | 1 | §10.2 |
| `SmartStudyPlanner/Sync/Fence/Policies/AbsentLocalParentTombstonePolicy.cs` | 1 | §10.3 |
| `SmartStudyPlanner/Sync/Fence/Policies/ConstraintOccupancyFencePolicy.cs` | 1 | §10.4 |
| `SmartStudyPlanner/Sync/Fence/StructuralDependencyRegistry.cs` | 2 | §7.2 |
| `SmartStudyPlanner/Sync/Fence/ImpactResolver.cs` | 2 | §7.3 |
| `SmartStudyPlanner/Sync/Fence/ConflictDependencySelector.cs` | 2 | §8 |
| `SmartStudyPlanner/Sync/Fence/FenceRouter.cs` | 2 | §11.3 |
| `SmartStudyPlanner/Sync/Fence/MutationRejectedException.cs` | 2 | carries `FenceDecision`; message lists rule ids |
| `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Mutations/SemesterReconcilePlanner.cs` | 3 | pure diff → intents + validation errors + writer plan |
| `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Mutations/SemesterGraphWriter.cs` | 3 | today's `LuuHocKyAsync` :105-208 persistence, no save |
| `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Mutations/LocalSemesterSaveExecutor.cs` | 3 (no fence) → 4 (fence) | §5 pipeline |
| `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Mutations/TaskEditorWriter.cs` | 5 | today's four mutation bodies |
| `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Mutations/LocalTaskEditorExecutor.cs` | 5 | pipeline for note/link intents |

### 15.2 Production: modified files

| File | Slice | Change | Why the current code cannot satisfy the contract without it |
|---|---|---|---|
| `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs` | 3, 4 | `LuuHocKyAsync` body → delegate to executor; `CopySyncSafeValues` moves with the writer | Reconcile fuses planning with EF mutation; the delete set is never data, so no impact set can exist before cascade fixup mutates the tracker |
| `Infrastructure/Persistence/SQLite/Repositories/SqliteTaskEditorRepository.cs` | 5 | four mutation methods → delegate | Note create in an S3 scope violates D9-T1 null-Base with no check anywhere (`:39-40`) |
| `Sync/Apply/SyncApplySession.cs` | **6, gated** | fence step in `ApplyEntityAsync` after D9-T6 lock and parent handling, before `StageAutoEvidenceAsync`/`WriteAsync`/`CascadeTombstoneAsync`, for non-staging results only | The cascade writes into Unresolved scopes (W-2, MEASURED) |
| `Sync/Apply/SyncApplyModels.cs` | **6, gated** | `SyncApplyReason.StructuralFenceBlocked`, `.FenceUnsupported` | typed rejection |

`SqliteStudyTaskRepository.cs` changes only if OD-5 ≠ "guard test". `ServiceLocator.cs` is unchanged under L1:
executors are constructed by the repositories from the same factory.

### 15.3 Files explicitly NOT to modify

`Sync/Merge/*` (ThreeWayMerge, ConstraintMerge, ConflictKeys, CanonicalJson, MergeSurfaceRegistry, Lww) ·
`Sync/Apply/ConflictResolver.cs` · `Sync/Apply/ConflictStaging.cs` · `Sync/Apply/EntitySnapshotMapper.cs` ·
`Sync/SyncConflictRecordStore.cs` · `Sync/SyncBaseSnapshotStore.cs` · `Sync/SyncConflictRecordRow.cs` ·
`Data/AppDbContext.cs` · `Data/SyncStamper.cs` · `Data/SyncConflictRecordSchema.cs` · `Services/ServiceLocator.cs`
(under L1) · all `Views/*`, XAML, and VMs (unless OD-7 authorises the reload) · `Sync/Apply/SyncApplySession.cs`
and `SyncApplyModels.cs` until OD-1 · frozen docs: Decision Record (both copies), D4/D9-T4 amendment, PR-5
amendment, PR-6 rulings, engineering DoR, PR-6 DoR, W-2/F-2 analysis, T2.5 recon · the owner's dirty
working-tree files · T2.5 test files (`Sync/Convergence/*`), if they have landed.

### 15.4 Test files (namespaces mirror production 1:1; fixtures in `Fixtures/`, doubles in `TestDoubles/`)

| File | Slice |
|---|---|
| `SmartStudyPlanner.Tests/Fixtures/FileBackedSqliteFixture.cs` | 0 |
| `SmartStudyPlanner.Tests/Data/SqliteTransactionLockProbeTests.cs` | 0 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/LocalSaveCharacterizationTests.cs` | 0 (P0-a re-stamp; P0-c local W-2 crossing leg) |
| `SmartStudyPlanner.Tests/Fixtures/FenceScenarioFixture.cs` | 2 (wraps `SyncApplyFixture`; `StageS1CrAsync`, `StageS1PtAsync`, `StageAlPtAsync`, `StageS3Async`, `SeedS2RecordAsync` *(constructed, unreachable via sync in v1)*, `SeedUnsupportedRecordAsync`, `SnapshotAllTablesAsync`). Recipes are copied from existing tests; existing test files are not edited |
| `SmartStudyPlanner.Tests/TestDoubles/SaveCountingDbContext.cs` | 2 |
| `SmartStudyPlanner.Tests/Sync/Fence/ConflictShapeClassifierTests.cs` | 1 |
| `SmartStudyPlanner.Tests/Sync/Fence/FencePolicyRegistryTests.cs` | 1 |
| `SmartStudyPlanner.Tests/Sync/Fence/Policies/{ConcurrentReparent,ParentTombstone,AbsentLocalParentTombstone,ConstraintOccupancy}FencePolicyTests.cs` | 1 |
| `SmartStudyPlanner.Tests/Sync/Fence/StructuralDependencyRegistryGuardTests.cs` | 2 |
| `SmartStudyPlanner.Tests/Sync/Fence/ImpactResolverTests.cs` | 2 |
| `SmartStudyPlanner.Tests/Sync/Fence/ConflictDependencySelectorTests.cs` | 2 |
| `SmartStudyPlanner.Tests/Sync/Fence/FenceRouterTests.cs` | 2 |
| `SmartStudyPlanner.Tests/Sync/Fence/FenceReadOnlyTests.cs` | 2 |
| `SmartStudyPlanner.Tests/Sync/Fence/FenceSourceFenceTests.cs` | 2 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SQLite/Mutations/SemesterReconcilePlannerTests.cs` | 3 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SQLite/Mutations/SemesterSaveRegressionSnapshotTests.cs` | 3 (bare before/after table-snapshot diff over the X-16 topologies; uses no `Sync/Fence` type; pinned before the refactor, unchanged after) |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SQLite/Mutations/ImpactPredictionMatchesObservedWritesTests.cs` | 4 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SQLite/Mutations/LocalSemesterSaveFenceTests.cs` | 4 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SQLite/Mutations/LocalSemesterSaveGateOrderTests.cs` | 4 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SQLite/Mutations/LocalTaskEditorFenceTests.cs` | 5 |
| `SmartStudyPlanner.Tests/Infrastructure/Persistence/SyncedEntityWritePathFenceTests.cs` | 5 |
| `SmartStudyPlanner.Tests/Sync/Apply/SyncApplyFenceTests.cs` | 6, gated |

---

## 16. Test strategy and test matrix

**Conventions:**
- real SQLite everywhere persistence is involved (in-memory shared connection; file-backed only for P0-b
  and the concurrency test);
- every new assertion is paired with a stated mutant that must turn it RED, using the recon §9.5 loop
  (backup, mutate, grep for the marker, test, restore, `git status --porcelain` empty);
- characterization tests carry `// CHARACTERIZATION — pins status quo pending OD-n; not evidence of a ruling`
  and a `Trait("Kind","Characterization")`.

### 16.1 Shape × mutation matrix (executor level unless marked R = router level)

| ID | Shape | Topology | Mutation (entry) | Expected | Key assertions | Mutant that must go RED |
|---|---|---|---|---|---|---|
| P-CR-1 | S1-CR on T | H→M→T | reparent T (local save) | Blocked `S1CR.EdgeReplaced` | exception carries 1 Blocked result; tables byte-identical | policy returns Passed for Reparent |
| P-CR-2 | S1-CR on T | H→M→T | delete T (`XoaTask`-equivalent: remove from graph, save) | Blocked `SubjectRemoved` | T live, `MaMonHoc` unchanged, note/links live | selector drops ScopeKey predicate **and** identity predicate |
| P-CR-3 | S1-CR on T | H→M→{T,T2} | delete M | Blocked @CascadeReached | T2 also still live (atomic) | ImpactResolver skips cascade recursion |
| P-CR-4 | S1-CR on T | H→M→{T,T2} | delete T2 | FencePassed, persisted | T2 tombstoned; record Unresolved, evidence unchanged | selector "any unresolved under same MonHoc" |
| P-CR-5 | S1-CR on T | T + link | add link under T (editor) | Passed `ChildEdgeOnly` (relevant, not violated) | result list contains the Passed entry | policy returns NotApplicable (fails the R12 assertion) |
| P-CR-6 | S1-CR on T | H→M→T | edit `T.TenTask` | Passed `NonStructuralFields` via `FencePendingDecisions` — **CHARACTERIZATION SB-3** | persisted; `Matches(BaseFp, live)` now false (documents W-1) | set `NonStructuralEditOnHeldRow = Blocked` |
| P-PT-1 | S1-PT on T (live Base parent M; remote parent tombstoned) | H→M→T | reparent T to M2 | Blocked `S1PT.ParentFrameChanged` | 1 Blocked result; tables byte-identical | policy returns Passed for Reparent; classifier maps PT→CR (rule-id assertion RED) |
| P-PT-2 | S1-PT on T (live Base parent) | H→M→T | delete T | Blocked `S1PT.SubjectRemoved` @DirectSubject | T live, `MaMonHoc` unchanged, note/links live | policy ignores Tombstone |
| P-PT-3 | S1-PT on T (live Base parent) | H→M→{T,T2} | delete M | Blocked `S1PT.SubjectRemoved` @CascadeReached | T2 also still live (atomic) | ImpactResolver skips cascade recursion |
| P-PT-4 | S1-PT on T (live Base parent) | H→M→{T,T2} | delete T2 | FencePassed, persisted | T2 tombstoned; record Unresolved, evidence unchanged | selector "any unresolved under same MonHoc" |
| P-PT-5 | S1-PT on T (live Base parent) | T + link | add link under T (editor) | Passed `S1PT.ChildEdgeOnly` | Passed entry present in result list | policy returns NotApplicable |
| P-PT-6 | S1-PT on T (test-M recipe: Base parent already tombstoned, `SyncApplyParentHandlingTests.cs:112-141`) | H→M(dead)→T | edit `T.TenTask` | Passed `S1PT.NonStructuralFields` via `FencePendingDecisions` — **CHARACTERIZATION SB-3** | persisted | set `NonStructuralEditOnHeldRow = Blocked` |
| P-AL-1 | AL-PT on E under dead P | H→M(dead) | local save graph containing new StudyTask with `MaTask = E` under live M2 | Blocked `ALPT.SameIdentityMaterialization` | no StudyTask row E | policy only blocks when parent is P |
| P-AL-2 | AL-PT on E | | same create under dead P | Blocked | no row | — |
| P-AL-3 | AL-PT on E | | delete sibling under live M2 | FencePassed | persisted | selector identity predicate over-broad |
| P-AL-4 (R) | AL-PT on E | | constructed `UpdateFields(E)` | Blocked `AbsentIdentityTargeted` | — | — |
| P-K-1 | S3 on K(T) | T, no note | `UpsertNoteAsync(T, "x")` | Blocked `CONS.ScopeAcquired` | `CountNotesInScopeAsync(T) == 0` | ImpactResolver omits `Acquired` |
| P-K-2 | S2 (constructed) on K(T) | T, N0 | delete T | Blocked `ScopeReleased` @ConstraintScope | N0 live | cascade expansion skips TaskNote edge |
| P-K-3 | S2 | T, N0 | delete M (grandchild note) | Blocked | N0, T live | recursion depth limited to 1 |
| P-K-4 | S2 | T, N0 | edit N0 content | Passed — **CHARACTERIZATION SB-3** | persisted | — |
| P-K-5 | S3 | T, no note | delete T | Passed `EmptyScopeParentTombstoned` — **CHARACTERIZATION OD-4** | T tombstoned | — |
| P-K-6 (R) | S3 at K1 and K2 | | constructed reassign K1→K2 | Blocked ×2 | both results present | router dedups by shape |
| P-K-7 | S2 on K(T), unrelated S3 on K(T2) | | delete T2's link | FencePassed | persisted | selector matches all TaskNote-scope records |

### 16.2 Cross-cutting tests

| ID | Test | Assertion | Mutant |
|---|---|---|---|
| X-1 overlap (R) | `A(HocKy)→T(MonHoc)[S1-PT]→N(StudyTask)[S1-CR]→note[K S2]`; delete A | 3 Blocked results, sorted `(Stage, ScopeKey, ConflictId)` | router `break` on first Blocked |
| X-2 overlap (exec) | same minus HocKy; delete T via local save | 3 results in exception | same |
| X-3 determinism (R) | seed the X-1 records in two different insertion orders in two fixtures | identical ordered `Results` | sort omitted / sort by ConflictKind |
| X-4 no priority (source-scan) | no `OrderBy`/comparer over `ConflictShape`/`ConflictKind` in `Sync/Fence` | scanner self-check matches a planted string | — |
| X-5 origin equivalence (R) | same DB, same intents, `Origin` Local vs SyncApply | `Results` sequence-equal | policy `if (origin==Local) Passed` cannot compile (no param); source-scan catches `MutationOrigin` in `Policies/` |
| X-6 unknown shape | `SeedUnsupportedRecordAsync` (e.g. Structural on HocKy, or `StructuralReason = 99`) on T; delete T | `MutationRejectedException`, Outcome `Unsupported`, rule id names the tuple | classifier default → ConcurrentReparent |
| X-7 route unknown (R) | intent with `EntityType = "Foo"` or a Merge field in `Relations` | `RouteKnown == false`, rejected | RouteKnown check removed |
| X-8 read-only (DB) | run `FenceRouter.EvaluateAsync` over every §16.1 scenario on a `SaveCountingDbContext` | 0 saves; `ChangeTracker.Entries()` empty; `SnapshotAllTablesAsync` before == after (domain, records, baselines) | add `db.Add(...)` in selector |
| X-9 read-only (source) | `Sync/Fence/**` contains no `SaveChanges`, `.Add(`, `.Update(`, `.Remove(`, `Attach(`, `MarkSyncApplied`, `StageAsync`, `MarkResolvedAsync`, `UpsertAsync`, `ExecuteSql`, `BeginTransaction`; `Policies/**` contains no `AppDbContext`, `MutationOrigin` | self-check on planted strings | — |
| X-10 pass ≠ authorize: business validation | no records; graph where a task references a MonHoc not in the HocKy | `InvalidOperationException` (existing message), decision was FencePassed, nothing persisted | executor persists before validation |
| X-11 pass ≠ authorize: persistence failure | no records; `FailingSaveDbContext(failOnSaveNumber: 1)` via factory; delete T2 | exception, rollback, tables identical, context disposed | executor commits before save |
| X-12 pass ≠ authorize: concurrency (file-backed) | executor tx open after fence pass; second connection stages an S1-CR record on T and commits (or is blocked); executor then deletes T | never both a committed Unresolved record on T **and** a committed tombstone of T | executor evaluates the fence on a separate earlier context |
| X-13 no retry after resolution | P-CR-3 blocked; `ConflictResolver.ResolveAsync(KeepBase)` Applied | M still live, T under M, no pending state | — |
| X-14 resolution without mutation | reuse `ZP5_KeepBaseNull_OnAlPt_IsApplied_WithNoWrite`, `ZP2_ConcurrentReparent_KeepBase_WritesNothing` (existing) + new: afterwards the selector returns 0 records for E | record Resolved, no domain row, no fence contribution | selector ignores Status |
| X-15 no resurrection by save | local save of a graph containing a StudyTask whose id is already tombstoned | row stays `IsDeleted = true` (characterizes `CopySyncSafeValues`) | writer copies `IsDeleted` from the POCO |
| X-16 impact oracle | §7.4 over: task delete, MonHoc delete with 3 tasks (notes + links), reparent, clone-merge save, create | predicted rows == observed live-state changes | drop TaskNote/link edges in registry |
| X-17 registry guard | EF model FKs + `OnDelete(Cascade)` == registry cascade edges; field classes == `MergeSurfaceRegistry` | — | remove a registry edge |
| X-18 no-conflict regression | the whole existing `RepositoriesTests`, `TaskNotesTests`, VM tests unchanged and green | count equals baseline + new | — |
| X-19 write-path fence | synced-DbSet mutation calls and FK assignments only in allowlisted files | self-check | add `db.StudyTasks.Remove(x)` in a VM |
| X-20 sticky sibling (same VM session) | S1-CR on T. VM-equivalent flow on **one** in-memory graph: remove T ⇒ save ⇒ rejected; then edit sibling T2 in the same graph ⇒ save | **OD-7 decision point.** With reload-on-reject: the second save persists T2's edit and T stays live. Without reload: the second save is rejected again (planner re-derives `Tombstone(T)`); the test pins that as characterization, and §22 item 5 is recorded as **not met** on the local path | remove the reload step from the VM handler |

### 16.3 Slice-0 measurements (test-only, labelled)

| ID | Measures | Why a later slice needs it |
|---|---|---|
| P0-a | Does `LuuHocKyAsync` re-stamp already-tombstoned tasks/MonHocs/notes/links on a no-change save? (seed, tombstone T via save, save again, compare `Rev`/`DeletedAtUtc`) | §7.4 oracle definition; D-2 report |
| P0-b | `BeginTransactionAsync()` lock mode and the two-connection read-then-write outcome on file SQLite | §14 design (A-3) |
| P0-c | Local-origin W-2 leg: stage S1-CR on T (W-2 Appendix A recipe), then `XoaMon`-equivalent save deleting M | expected today: T tombstoned, record Unresolved, `Matches == false` (INFERENCE per W-2 §2.5). Becomes P-CR-3's RED baseline; Slice 4 flips it |
| P0-d | Full suite count at the slice baseline (`origin/dev`) | acceptance gate baseline; **NOT RUN** in this planning session (last observed: 821 passed / 1 skipped at `fbc72ea`, T2.5 recon) |

---

## 17. Negative / fail-closed cases

| # | Case | Required behaviour |
|---|---|---|
| N-1 | Unresolved record with an unclassifiable tuple reached by impact | `Unsupported` ⇒ reject (X-6) |
| N-2 | Intent on an unregistered type/field | `RouteKnown = false` ⇒ reject (X-7) |
| N-3 | Unreadable evidence JSON in `Derive` (`SnapshotContractException`/`JsonException`) | policy returns `Unsupported` with `RuleId = "*.EvidenceUnreadable"`; never throws past the router |
| N-4 | Selector/impact DB exception | propagates; executor rolls back; no decision fabricated |
| N-5 | Registry incomplete (shape without policy) | `FencePolicyRegistry` static init throws; `FencePolicyRegistryTests` red |
| N-6 | Writer invoked on a Blocked/Unsupported decision | executor guard `if (!decision.FencePassed || !decision.RouteKnown) throw` placed before the writer; test calls the internal executor step with a Blocked decision and asserts `ChangeTracker` has no Deleted/Modified synced entries |
| N-7 | Record resolved between selection and write (concurrent) | cannot occur inside the single immediate/serialised tx (P0-b); if the mode is deferred, the write upgrade fails ⇒ rollback |
| N-8 | Duplicate selection of the same record via both predicates | deduplicated by `ConflictId` before routing; X-1 counts results exactly |
| N-9 | Empty request (no-change save) | no intents ⇒ empty impact ⇒ FencePassed ⇒ writer no-op; Rev stability test still green |
| N-10 | `MutationRejectedException` swallowed anywhere in production | source-scan: no `catch (MutationRejectedException` outside the OD-7-authorised VM handler |

---

## 18. Migration / compatibility considerations

- **Schema:** none (FACT §2.2). No `EnsureTable` change, no migration, no backup.
- **Data:** FACT (grep at `4c7c840`): `SyncConflictRecordStore.StageAsync` is called only from
  `SyncApplySession.cs:362, 459, 486, 533`; `SyncConflictRecordRow` is constructed only in
  `ConflictStaging.ToRow` (`:42`); `MarkResolvedAsync` is called only from `ConflictResolver.cs:194`. Neither
  `SyncApplySession` nor `ConflictResolver` has a production construction site (`ServiceLocator.cs:36-123`).
  **No production code path can write a ConflictRecord.** Slices 1–5 are therefore observationally
  identical for users running the shipped app. The caveat: a DB file used by a test harness outside the
  test project is not covered by this claim. X-18 is the regression proof.
- **API:** `IHocKyRepository` and `ITaskEditorRepository` signatures unchanged (L1). A new failure mode
  (`MutationRejectedException`) exists but is unreachable in production until records exist.
- **Tests in flight:** T2.5 (`docs/review/2026-09-12-t2.5-recon.md` §9.1) pins B1 as P10 characterization.
  Slices 1–5 do not touch sync paths. Slice 6 must flip P10 *with* the owner ruling, never before.
- **Performance:** one extra indexed query per save (ScopeKey `IN` list plus identity predicate over the
  small `SyncConflictRecords` table) plus FK child queries for tombstone intents. The existing reconcile
  already loads the whole semester graph. No measurement is claimed.
- **Rollback:** each slice is a separate PR. Slices 1–2 are additive. Slice 3 is a pure refactor. Slice 4's
  wiring is one delegating line per method and can be reverted without touching the fence.

---

## 19. Risks

| # | Risk | Severity | Mitigation / owner |
|---|---|---|---|
| R-1 | `LuuHocKyAsync` extraction regresses reconcile (clones, reparent-before-remove ordering `:136-151`, FK heal) | High | Slice 3 is behaviour-only and gated on the full existing suite + X-16 oracle; writer keeps statement order verbatim |
| R-2 | Impact model diverges from one of the two cascade implementations | High | registry guard X-17 + oracle X-16 (local); Slice 6 adds the sync oracle |
| R-3 | Compound save ⇒ sticky rejection of later unrelated edits (VM graph keeps the deleted row absent) | Medium (dormant until records exist in production) | OD-7 |
| R-4 | SB-3 status quo keeps W-1: content edits on held-at-Base rows make records permanently unresolvable | High (M2.2 liveness; already known) | owner OD-3; tests are labelled characterization |
| R-5 | TOCTOU guarantee rests on SQLite lock semantics not yet measured in this repo | Medium | P0-b before Slice 4; X-12 |
| R-6 | Load-time dedup's implicit reparent of a conflicted task ⇒ every save of that semester blocked until resolved | Low likelihood / high impact | OD-8 |
| R-7 | Identity-predicate fail-closed net over-selects a record ⇒ spurious `Unsupported` | Low | only fires for unclassifiable records, which are unreachable by construction today |
| R-8 | Implementer "fixes" W-2 on the sync path before OD-1 | Medium | Slice 6 gated; stop condition in §24 cards; `SyncApplySession.cs` on the not-modify list |
| R-9 | Adjacent defects folded into fence PRs: **D-1** `UpsertNoteAsync` adds a second note when a *tombstoned* note occupies unfiltered `UNIQUE(MaTask)` ⇒ raw UNIQUE violation (INFERENCE, `SqliteTaskEditorRepository.cs:38-40` + `AppDbContext.cs:122`); **D-2** re-stamp churn (P0-a); **D-3** W-1; **D-4** resolver + stale UI graph revert (PR-6 DoR §14) | Medium | report in PR descriptions; separate owner tickets; do not fix |
| R-10 | Primary spec untracked; implementation cites a file absent from `origin/dev` | High (governance) | **Closed 2026-09-14:** spec committed with this plan (SB-1) |
| R-11 | GitNexus index stale/nondeterministic | Low | re-run `impact` per slice; do not compare counts |

---

## 20. Open questions / owner decisions still required

| ID | Question | Why the owner | Blocks | Engineering recommendation (not a ruling) |
|---|---|---|---|---|
| **SB-1** ☑ **CLOSED 2026-09-14** | *Original question:* commit the spec to `origin/dev` under a stable name. The task brief cites `…-spec.md`; the tree has an untracked `…-spec-complete.md`. Also confirm that its authority line ("referenced conversation") suffices, or add a dated rulings record. *Owner answer (verbatim):* "it is added by myself, not commited yet, commit new docs". *Closure:* the spec is committed byte-for-byte as-is under `…-spec-complete.md`, which is now the stable name (no separate `…-spec.md` exists). Its authority is the owner's authorship statement recorded here. No separate ratification note was written, because authoring one is not an agent's call; the owner may still add one | Governance precedent: every ratified direction so far lives in a dated tracked `docs/specs/` file | nothing (was: merge of Slice 1) | n/a |
| **SB-2 / OD-1** | Direction F and spec §9.1 say the fence applies to *all* origins. For `SyncApplySession` that means rejecting a remote ancestor tombstone whose actual cascade reaches a protected contract. That is **W-2 option C1**, and it flips MEASURED shipped behaviour (crossing leg `Applied` ⇒ `Rejected`). Spec O-4 and W-2 §8 say a change to the crossing behaviour needs a dated amendment. **Does direction F constitute the W-2 Q-1 ruling for sync-originated ancestor tombstones?** Sub-question: is the peer enumerator's natural re-offer of a rejected tombstone (W-2 §3.3 C1, INFERENCE) compatible with spec §9.2 "no automatic retry"? | Two ratified sources disagree on whether this is already decided; code must not pick | Slice 6 only | none; both readings are coherent. Sync is unwired, so waiting costs nothing user-visible |
| **SB-3 / OD-3** | Spec §6 row 1 and §4.1 make an ordinary non-structural edit on a row held at Base (S1-CR/S1-PT `E`, S2 `N0`) fence-**Pass**. Frozen D9-T1 says "live domain state must equal Base while Unresolved", read *whole-row* by shipped/tested PR-5 (test L; W-2 §3.4 D-a). Passing makes the record permanently unresolvable (W-1). **Pass (spec), Block (D9-T1 whole-row reading, a contract-specific edit lock on E only), or a field-scoped D9-T1 (W-2 Q-2)?** | Spec-vs-frozen tension. Spec §2.2 says frozen wins, yet the spec explicitly anticipated drift (§4.1) | nothing, but see the note. The plan **authors** a rule here: no fence exists today, so there is no prior behaviour to pin. It ships the spec's **Pass** via `FencePendingDecisions.NonStructuralEditOnHeldRow` (§10.0), because Pass adds no rejection path and no user-visible change. **Spec §2.2 together with the shipped whole-row D9-T1 reading points the other way (Block).** A Block ruling is a one-constant change plus flipping P-CR-6/P-PT-6/P-K-4 | none. The plan's default is a surfaced choice, not a recommendation |
| **OD-2** | Integration shape: **L1** executor behind the unchanged port, vs **L2** new application service with VM call-site changes (§12.2) | Architecture/UX scope trade-off | Slice 4 | L1 |
| **OD-4** | S3 (empty scope) owning task tombstoned: literal spec (occupancy not reached ⇒ Pass) vs treat "candidate relation made unmaterialisable" as crossing (Block). After tombstone only `KeepBase(null)` can resolve (B-2 / ZPT2) | Contract interpretation | nothing (characterization) | literal spec reading |
| **OD-5** | `IStudyTaskRepository.AddAsync/UpdateAsync/DeleteAsync` have zero production callers but are unfenced public mutation paths (`UpdateAsync` can reparent) | API surface policy | Slice 5 | guard test forbidding production callers; no route, no removal |
| **OD-6** | Route `ConflictResolver` result writes through the fence (excluding the record being resolved)? | Touches frozen PR-6 behaviour | nothing | do not route (§10.6) |
| **OD-7** | How a rejected local save surfaces, and whether VMs reload their `HocKy` graph after rejection (prevents sticky rejection R-3; **required for spec §13.5 to hold on the local path**, X-20). Today the exception reaches `App.DispatcherUnhandledException` ⇒ generic "may not have been saved" box | UX; touches VMs | Slice 4 merge | minimal: catch `MutationRejectedException` in the four VM save commands, reload the graph from the repository, show the rule ids; no XAML |
| **OD-8** | Accept R-6 (dedup implicit reparent of a conflicted task blocks every save of that semester) or schedule a mitigation | Product liveness | nothing | accept for now; revisit with M2.2 |
| — | Pre-existing D-1..D-4 (§19 R-9) | Separate tickets | nothing | report only |

---

## 21. Explicit implementation sequence (slices)

Each slice = one branch from the fetched `origin/dev` = one PR. After each merge: flag the owner to compare
local `dev` vs `origin/dev` (CLAUDE.md).

| Slice | Deliverable | Depends on | Gate | Exit criteria |
|---|---|---|---|---|
| **0** Measure | `FileBackedSqliteFixture`, `SqliteTransactionLockProbeTests` (P0-b), `LocalSaveCharacterizationTests` (P0-a, P0-c), suite baseline (P0-d). Test-only | — | — | probes green with recorded observations in the PR body; labelled characterization; zero production diff |
| **1** Pure fence | §15.1 Slice-1 files + classifier/registry/policy tests over constructed `ImpactSet`s | — | SB-1 before merge | all P-* rows at policy-unit level; each listed mutant RED then reverted |
| **2** Router | registry, impact resolver, selector, router, exception, `FenceScenarioFixture`, `SaveCountingDbContext`, X-1..X-9, X-14 selector leg, X-17 | 1 | — | router-level matrix green on real SQLite with staged records; read-only proofs green |
| **3** Extract | planner + writer + executor (no fence), `LuuHocKyAsync` delegates; `SemesterReconcilePlannerTests`; `SemesterSaveRegressionSnapshotTests` (committed first against the unrefactored code, then kept unchanged) | P0-a, P0-d | — | zero test count loss; all `RepositoriesTests` green unchanged; `gitnexus_detect_changes` ⊆ {LuuHocKyAsync, new symbols} |
| **4** Wire local save | fence in `LocalSemesterSaveExecutor`; P-CR/P-PT/P-AL/P-K rows reachable via `LuuHocKyAsync`; X-10..X-13, X-15, X-16, X-20; P0-c flipped to Blocked | 2, 3, P0-b | OD-2, OD-7 | executor-level matrix green; mutants RED |
| **5** Wire task editor + write-path fence | `TaskEditorWriter`, `LocalTaskEditorExecutor`, P-K-1, P-CR-5, X-19, OD-5 guard | 4 | OD-5 | as above |
| **6** Wire sync (GATED) | fence step in `ApplyEntityAsync`; `SyncApplyFenceTests` incl. the W-2 crossing leg flipping to `Rejected/StructuralFenceBlocked` with control and direct legs unchanged; sync-origin impact oracle | 2 | **OD-1 ruling recorded in `docs/specs/`** | W-2 probe legs as specified by the ruling; T2.5 P10 updated in the same PR |

**Pre-edit checklist (every code slice, per `docs/plans/README.md`):**
1. `rtk git fetch`; create the worktree from `origin/<base>`; report local/remote divergence, never sync it.
2. `gitnexus_impact({target, direction:"upstream"})` for every modified symbol; report risk; stop on
   HIGH/CRITICAL unless this plan already prices it (R-1: `LuuHocKyAsync`, `RemoveChildrenAsync`).
3. Run the baseline suite before the first edit.

**Acceptance gates (every slice):** `rtk dotnet build` · `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj -v q --nologo`
(count ≥ baseline + new) · every listed mutant observed RED then restored (`git status --porcelain` empty) ·
`gitnexus_detect_changes()` limited to expected symbols · no schema diff · PR body lists
observations/inferences separately and names what was not run.

---

## 22. Definition of Done / acceptance criteria

Maps spec §13 items 1–13 to proofs:

| Spec §13 | Done when |
|---|---|
| 1 per-shape policy | `FencePolicyRegistryTests` + classifier table tests green; 4 policy classes |
| 2 impact set with before/after edges, lifecycle, scopes | `ImpactResolverTests` + X-16 |
| 3 parent deletes include descendants, grandchildren, TaskNote constraints | P-CR-3, P-K-3, X-1, X-16 |
| 4 overlapping: every relevant record evaluated | X-1, X-2, X-3 |
| 5 unrelated sibling not rejected | Different-request case: P-CR-4, P-AL-3, P-K-7. Same-VM-session case (block T, then edit sibling T2 and save): X-20. **Proven on the local path only once OD-7's reload is implemented.** Without it, X-20 pins sticky rejection and item 5 is recorded as **not met** locally |
| 6 local ancestor delete ≡ non-local for the same envelope | X-5 (router) now; executor-level sync equivalence after Slice 6 |
| 7 pass followed by authorization/validation/persistence/concurrency | X-10, X-11, X-12 |
| 8 no side effects; blocked ⇒ no partial/deferred | X-8, X-9, P-CR-2/3 table identity, N-6 |
| 9 AL-PT materialization not via normal create | P-AL-1, P-AL-2 |
| 10 unregistered shape fails closed with evidence | X-6 |
| 11 record ≠ subject ≠ contract in every decision | `PolicyResult` fields asserted in P-CR-5 (relevant-but-Passed) |
| 12 deterministic traversal, no kind winner | X-3, X-4 |
| 13 resolution without domain mutation | X-14 (existing ZP5/ZP2 + selector leg) |

Plus:
- existing suite green with no deleted or rewritten tests (X-18);
- characterization tests labelled for SB-3/OD-4;
- SB-1 closed (☑ 2026-09-14);
- Slice 6 not merged without OD-1;
- `docs/CHANGELOG.md` entry per shipped slice;
- `docs/active/README.md` pointer while `in-progress`.

---

## 23. Spec rule → policy/abstraction → entry point → executor → repository boundary → test (reviewer index)

| # | Spec rule | Policy / abstraction | Entry point | Executor | Repository / persistence boundary | Proving test(s) |
|---|---|---|---|---|---|---|
| T-1 | Protection per shape/contract, no entity lock (§4, A) | `ConflictShapeClassifier` → 4 `IConflictFencePolicy` + `ProtectedContract` | `LuuHocKyAsync`, `ITaskEditorRepository.*` | `LocalSemesterSaveExecutor`, `LocalTaskEditorExecutor` | `SemesterGraphWriter`, `TaskEditorWriter` | P-CR-4, P-AL-3, P-K-7, `FencePolicyRegistryTests` |
| T-2 | Explicit impact set, before+after (§3.1, B) | `ImpactResolver`, `ImpactSet`, `RelationChange` | same | planner builds `MutationRequest` before the writer | writer applies the approved plan | `ImpactResolverTests`, X-16 |
| T-3 | Actual cascade incl. grandchildren/constraints (§5.3) | `StructuralDependencyRegistry` + cascade expansion | `XoaMon`/`XoaTask` → `LuuHocKyAsync` | executor | writer + `TaskCascadeHelper` | P-CR-3, P-K-2, P-K-3, X-16, X-17 |
| T-4 | Dependency ≠ impact (§3.3, C) | `ConflictDependencySelector` (separate from resolver) | same | executor | — | P-CR-5 (selected, Passed), `ConflictDependencySelectorTests` |
| T-5 | All applicable policies evaluated, no priority (§7, D) | `FenceRouter` aggregation | any | executor | — | X-1, X-2, X-3, X-4 |
| T-6 | Distinct policy per case (§4, E) | four policy classes + completeness | — | — | — | classifier/registry tests; rule-id assertions in P-PT vs P-CR |
| T-7 | No local bypass (§9.1, F) | policies have no origin input | local save; router-level sync origin | executor | writer | X-5, X-9 (origin scan), X-19 |
| T-8 | Pass necessary not sufficient (§3.4, G) | `FenceDecision.FencePassed` + `RouteKnown` | local save | executor gate order (§5 steps 4–7) | save/commit only after gates | X-10, X-11, X-12, X-7 |
| T-9 | Router/policies read-only (§8.3, H) | pure policies; `AsNoTracking` readers | — | executor owns all writes | — | X-8, X-9 |
| T-10 | Decision separate from execution (§8, I) | `FenceDecision` value | local save | executor | writer never calls router | N-6, X-19 allowlist |
| T-11 | Repository is not semantic owner (§8.2, J, A-4) | — | `Sqlite*Repository` delegate only | executor | writers | X-19; review checklist: no conflict types referenced from `Repositories/` |
| T-12 | Resolution ≠ mutation (§3.5, K) | `ConflictResolver` unchanged; selector filters `Unresolved` | `ResolveAsync` (harness) | resolver (B-1) | resolver's own write | X-14 (ZP5, ZP2 + selector leg) |
| T-13 | No implicit resurrection; AL-PT materialization blocked (§4.2, §10, L) | `AbsentLocalParentTombstonePolicy` | local save with same-id create | executor | writer (never runs) | P-AL-1, P-AL-2, X-15 |
| T-14 | Unknown shape fails closed (§7.2) | classifier `Unsupported`; identity predicate | any | executor rejects | — | X-6, N-3 |
| T-15 | Blocked ⇒ no partial, deferred, retry, fallback (§9.2) | executor ordering | local save | executor rollback | writer never invoked | P-CR-2, P-CR-3, X-13, N-6 |
| T-16 | TOCTOU / final revalidation (§8.2, A-3) | single-tx evaluation | local save | executor | SQLite lock + unique indexes/triggers | P0-b, X-12 |
| T-17 | Sync-origin fence (§9.1 applied to sync) | same router | `SyncApplySession.ApplyEntityAsync` | session per-op tx | `WriteAsync`/`CascadeTombstoneAsync` | `SyncApplyFenceTests`, **gated OD-1** |
| T-18 | Record vs subject vs contract in every decision (§2.4, §13.11) | `PolicyResult(ConflictId, ScopeKey, Shape, Subject, RuleId, Evidence)` | any | exception payload | — | P-CR-5, X-6 payload assertions |

---

## 24. Agent dispatch

### 24.1 Parallelization decision

- **Wave A (parallel):** Slice 0, Slice 1, Slice 3. Their write scopes are disjoint:
  - Slice 0: test-only new files.
  - Slice 1: new `Sync/Fence/` files and their tests.
  - Slice 3: `SqliteHocKyRepository.cs` plus new `Mutations/` files.

  Slice 3 must wait for P0-a/P0-d results *before merging*. It may start in parallel. It uses no
  `Sync/Fence` type: its regression oracle is a bare snapshot diff, and the impact oracle X-16 belongs to
  Slice 4.
- **Wave B:** Slice 2, after Slice 1 merges (it consumes Slice 1 types).
- **Wave C:** Slice 4, after 2 and 3 merge, P0-b is known, and OD-2/OD-7 are answered.
- **Wave D:** Slice 5, after 4 (reuses the executor pattern and the exception).
- **Wave E:** Slice 6 only after OD-1 is recorded.
- No two agents share a worktree. No agent edits `docs/CHANGELOG.md` except the slice's own PR, merged
  sequentially.

### 24.2 Task cards

| Card | Mission | Venue | Write scope | Skills | Tools | Deliverable | Stop conditions |
|---|---|---|---|---|---|---|---|
| **A0 Measure** | Run P0-a..d and pin observations as labelled tests | worktree `.claude/worktrees/fence-s0`, branch `test/epic2-fence-s0-measurements` from `origin/dev` | `SmartStudyPlanner.Tests/Fixtures/FileBackedSqliteFixture.cs`, `Tests/Data/SqliteTransactionLockProbeTests.cs`, `Tests/Infrastructure/Persistence/LocalSaveCharacterizationTests.cs` | `superpowers:using-git-worktrees`, `superpowers:verification-before-completion` | `rtk dotnet test`, context-mode for test output | PR with observations table (observed vs inferred) | any production edit needed; P0-c contradicts W-2 §2.5 (report, do not "fix") |
| **A1 Pure fence** | §15.1 Slice-1 files + unit tests | `.claude/worktrees/fence-s1`, `feat/epic2-fence-s1-policies` | `SmartStudyPlanner/Sync/Fence/**` (Slice-1 files only), `Tests/Sync/Fence/{ConflictShapeClassifier,FencePolicyRegistry}Tests.cs`, `Tests/Sync/Fence/Policies/**` | `superpowers:test-driven-development`, `superpowers:verification-before-completion` | `rtk dotnet build/test`, `gitnexus_detect_changes` | PR; mutant log | a policy needs DB or origin; a cell not in §10 appears; a frozen-semantics question arises (report as new OD) |
| **A3 Extract** | Behaviour-preserving split of `LuuHocKyAsync` | `.claude/worktrees/fence-s3`, `refactor/epic2-fence-s3-reconcile-extract` | `SqliteHocKyRepository.cs`, `Infrastructure/Persistence/SQLite/Mutations/{SemesterReconcilePlanner,SemesterGraphWriter,LocalSemesterSaveExecutor}.cs`, `Tests/Infrastructure/Persistence/SQLite/Mutations/{SemesterReconcilePlanner,SemesterSaveRegressionSnapshot}Tests.cs` | `gitnexus-refactoring`, `gitnexus-impact-analysis`, `superpowers:verification-before-completion` | `gitnexus_impact` (HIGH, priced R-1), `rtk dotnet test` | PR, zero behaviour change | any existing test must change to pass; statement order in the writer must change |
| **A2 Router** | Registry, impact, selector, router, fixture, X-tests | `.claude/worktrees/fence-s2`, `feat/epic2-fence-s2-router` (from `origin/dev` after A1 merge) | `Sync/Fence/**` (Slice-2 files), `Tests/Fixtures/FenceScenarioFixture.cs`, `Tests/TestDoubles/SaveCountingDbContext.cs`, `Tests/Sync/Fence/**` | TDD, verification | `rtk`, gitnexus | PR | selector needs a schema/index change; staging a shape requires editing existing tests |
| **A4 Wire local** | Fence in executor; executor-level matrix | `.claude/worktrees/fence-s4`, `feat/epic2-fence-s4-local-save` | `Mutations/LocalSemesterSaveExecutor.cs`, `Tests/.../LocalSemesterSave{Fence,GateOrder}Tests.cs`, flip P0-c; VM handler **only if OD-7 authorises** | TDD, `gitnexus-impact-analysis`, verification, `superpowers:requesting-code-review` | `gitnexus_impact` on `LuuHocKyAsync` | PR | OD-2/OD-7 unanswered; X-12 cannot be made red |
| **A5 Wire editor** | Task-editor executor + write-path fence | `.claude/worktrees/fence-s5`, `feat/epic2-fence-s5-task-editor` | `SqliteTaskEditorRepository.cs`, `Mutations/{TaskEditorWriter,LocalTaskEditorExecutor}.cs`, `Tests/.../LocalTaskEditorFenceTests.cs`, `Tests/Infrastructure/Persistence/SyncedEntityWritePathFenceTests.cs` | TDD, verification | `rtk`, gitnexus | PR | D-1 UNIQUE defect blocks a test (use a live-note scenario; report D-1) |
| **A6 Wire sync** | **Do not dispatch before OD-1** | — | `SyncApplySession.cs`, `SyncApplyModels.cs`, `Tests/Sync/Apply/SyncApplyFenceTests.cs`, T2.5 P10 | — | — | — | no dated OD-1 record in `docs/specs/` |

---

## 25. Decisions made (ADR-style, engineering only; none is an owner ruling)

| Decision | Why it had to be made | What it is for | Experience it draws on |
|---|---|---|---|
| Center the delivery on the local save path; gate the sync path | The only live origin is local UI (no DI registration of sync or resolver). The sync-path fence equals W-2 C1, which is owner-unruled | Ship real protection without silently answering W-2 Q-1 | "report, don't resolve" on ratified-rule collisions (W-2 §8, T2.5 non-goal 8) |
| Derive intents from a pure reconcile planner rather than wiring a "delete method" | No local delete entry point exists; deletes are absence in a whole-graph diff, and EF cascade fixup mutates the tracker at `Remove()` | Make INV-2 and INV-12 provable before any write | EF cascade-fixup timing bug (project memory: snapshot-based fixup) |
| Policies take neither `AppDbContext` nor origin | Read-only and no-bypass invariants are cheaper to guarantee by signature than by review | INV-6, INV-8 by construction, plus a source-scan backstop | `SyncStamper` per-entry intent over batch flags; `SyncApplyAuditFenceTests` |
| Classify by column tuple; select by ScopeKey ∪ identity | Names drift; unknown future scopes must fail closed, not be missed | INV-5 with the existing filtered unique index | E-11 precedent ("key on the predicate") |
| Evaluate the fence inside the executor's write transaction once | A separate pre-check plus revalidation is two code paths that can diverge | TOCTOU closed structurally; A-3 verified by P0-b, not assumed | "signal must be able to go red": shared-connection fixture cannot observe concurrency |
| Ship the spec's permissive reading for SB-3/OD-4 through one `FencePendingDecisions` switch | Either outcome is a semantic choice. Pass adds no rejection path, but spec §2.2 plus whole-row D9-T1 argue for Block, so the choice is surfaced rather than hidden | A ruling becomes a one-constant flip; the undecided status is visible in code and proven by mutant | W-2 §7.2: characterization must not be cited as a ruling |
| Move the impact oracle X-16 out of Slice 3 | X-16 consumes `ImpactResolver` (Slice 2) while Slice 3 runs in Wave A | Slice 3 keeps "zero behaviour change" with a fence-free snapshot oracle | dependency order must match wave order |
| Behaviour-preserving extraction as its own slice | `LuuHocKyAsync` is HIGH blast radius with subtle ordering (`:136-151`) | Separate "moved code" review from "new semantics" review | E-7 precedent; separate commits per concern (project memory) |
| Do not route `ConflictResolver` | No reachable cross-record violation, and routing changes frozen PR-6 behaviour | Scope discipline | PR-6 DoR §14 fence |

---

## 26. Closing summary (task brief §14)

**Implementation scope.** A read-only structural-conflict fence in `Sync/Fence/`, plus local-save and
task-editor executors, delivered as Slices 0–5. Sync-apply wiring (Slice 6) is gated on OD-1. The resolver
is not routed (OD-6). No schema, UI/XAML, Restore, or frozen-semantics change.

**Proposed production files** (§15.1–15.2):
- New, `SmartStudyPlanner/Sync/Fence/`:
  - `MutationRequest.cs`, `ImpactSet.cs`, `ProtectedContract.cs`, `FenceResults.cs`
  - `ConflictShapeClassifier.cs`, `IConflictFencePolicy.cs`, `FencePolicyRegistry.cs`, `FencePendingDecisions.cs`
  - `StructuralDependencyRegistry.cs`, `ImpactResolver.cs`, `ConflictDependencySelector.cs`, `FenceRouter.cs`, `MutationRejectedException.cs`
  - `Policies/{ConcurrentReparentFencePolicy, ParentTombstoneFencePolicy, AbsentLocalParentTombstonePolicy, ConstraintOccupancyFencePolicy}.cs`
- New, `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Mutations/`:
  - `SemesterReconcilePlanner.cs`, `SemesterGraphWriter.cs`, `LocalSemesterSaveExecutor.cs`, `TaskEditorWriter.cs`, `LocalTaskEditorExecutor.cs`
- Modified:
  - `SqliteHocKyRepository.cs` (Slices 3–4)
  - `SqliteTaskEditorRepository.cs` (Slice 5)
  - **gated:** `Sync/Apply/SyncApplySession.cs`, `Sync/Apply/SyncApplyModels.cs` (Slice 6)
  - VM save commands only if OD-7 authorises the reload

**Proposed test files** (§15.4):
- `Fixtures/{FileBackedSqliteFixture, FenceScenarioFixture}.cs`
- `TestDoubles/SaveCountingDbContext.cs`
- `Data/SqliteTransactionLockProbeTests.cs`
- `Infrastructure/Persistence/{LocalSaveCharacterizationTests, SyncedEntityWritePathFenceTests}.cs`
- `Sync/Fence/{ConflictShapeClassifier, FencePolicyRegistry, StructuralDependencyRegistryGuard, ImpactResolver, ConflictDependencySelector, FenceRouter, FenceReadOnly, FenceSourceFence}Tests.cs`
- `Sync/Fence/Policies/{4 policy}Tests.cs`
- `Infrastructure/Persistence/SQLite/Mutations/{SemesterReconcilePlanner, SemesterSaveRegressionSnapshot, ImpactPredictionMatchesObservedWrites, LocalSemesterSaveFence, LocalSemesterSaveGateOrder, LocalTaskEditorFence}Tests.cs`
- **gated:** `Sync/Apply/SyncApplyFenceTests.cs`

**Files explicitly not to modify:** §15.3.

**Blockers:**
- ~~**SB-1:** spec untracked; blocks Slice 1 merge.~~ Closed 2026-09-14: owner-authored spec committed with this plan.
- **SB-2/OD-1:** sync-origin crossing = W-2 C1; blocks Slice 6.
- **SB-3/OD-3:** non-structural edit on a row held at Base vs D9-T1; ships the spec's reading behind one
  switch, with the asymmetry stated.

**Open owner decisions:** OD-2 (L1/L2), OD-4, OD-5, OD-6, OD-7 (required for spec §13.5 on the local path),
OD-8. Details in §20.

**Recommended PR / agent decomposition:** seven PRs, one per slice.
- Wave A: S0, S1, S3 in parallel.
- Wave B: S2.
- Wave C: S4.
- Wave D: S5.
- Wave E: S6, only after OD-1 is recorded.

Task cards are in §24.2. After each merge, flag the owner to compare local `dev` with `origin/dev`.
