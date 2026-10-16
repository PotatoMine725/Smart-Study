# Epic 2 / T2.4 — Fence Slice 5 (task editor): report

| Field | Content |
|---|---|
| **Status** | **Done 2026-10-16, draft PR #113.** Phase 0 (§1–§5) delivered and ruled the same day ([rulings record](../specs/2026-10-16-fence-slice5-owner-rulings.md)); Phase 1 RED, Phase 2, Phase 3 below (§6–§9). Owner answers to the two PR questions (Q1, Q2) applied the same day (§11). |
| **Ticket** | `Prompt/2026-10-10-epic2-fence-slice5-task-editor.md` (plan §21 row 5, §24.2 card A5) |
| **Worktree / branch** | `.claude/worktrees/fence-s5`, `feat-epic2-fence-s5-task-editor` (hyphenated), from `origin/dev` `2ec5d31` |
| **Baseline** | build OK (96 pre-existing warnings, 0 errors) · **1118 passed / 0 failed / 1 skipped (1119)** · `dotnet test`, no rtk filter |

Labels: **OBSERVED** = measured in this session (probe or command output). **INFERENCE** = read from code, not run.

## 1. Git state

- **OBSERVED:** local `dev` = `origin/dev` = `2ec5d31` (#112 merge). D-5 (#111) merged as `89cb897`, so the
  precondition holds. The local `dev` working tree is dirty with owner files (`AGENTS.md`, `CLAUDE.md`,
  `docs/s2-reader-package/*`, untracked `.agents/`, skills, …). Not touched.

## 2. Impact / callers (input to X-19 and the scope fence)

| Symbol | GitNexus upstream | grep cross-check |
|---|---|---|
| `SqliteTaskEditorRepository.UpsertNoteAsync` | LOW — `ThemTask` + 1 test | same |
| `.AddLinkAsync` | MEDIUM — `ThemTask` + 4 tests (MEDIUM comes from test fan-out only) | same |
| `.UpdateLinkAsync` | MEDIUM — `ThemTask` + 6 tests | same |
| `.DeleteLinkAsync` | LOW — `ThemTask` + 2 tests | same |
| `QuanLyTaskViewModel.ThemTask` | 0 (it is a `[RelayCommand]`, bound from XAML) | `ThemTaskCommand` used by 3 VM test files |
| `IStudyTaskRepository.Add/Update/DeleteAsync` | — | **0 production callers** (re-verified; production uses `GetAllAsync` only, in `OutcomeMaturationService`, `WeightOptimizerViewModel`). Other `.AddAsync(` hits are telemetry/log repos, so the OD-5 guard must match the *receiver type*, not the method name. |

Only production caller of the four editor mutations: `ThemTask` (`QuanLyTaskViewModel.cs:232-248`). Test-side
fakes implementing `ITaskEditorRepository`: `FakeRepositories.cs`, `EditModeSaveRejectionTests`,
`EditModeSaveFailureTests` (unaffected by L1).

## 3. Phase 0 answers

### 3a. Intent mapping — no `Sync/Fence` change needed (INFERENCE, code read)

Plan §6 fixes the mapping; `ImpactResolver` already covers every branch:

| Editor call | Intent | Impact produced | Note |
|---|---|---|---|
| `UpsertNoteAsync`, no live note | `Create(TaskNote n, [MaTask: null→T])` | Row Created, Lifecycle Create, Edge Added (n→T), **Scope Acquired K(T)** | Also fires for a **links-only** edit: `ThemTask` calls Upsert whenever note *or* links are non-empty, creating a `Content = null` note |
| `UpsertNoteAsync`, live note, content differs | `UpdateFields(TaskNote n, [Content])` | Row FieldsChanged, **Scope OccupantContentChanged K(T)** | Content is `Merge`-class |
| `UpsertNoteAsync`, content equal | none (N-9) | empty ⇒ vacuous pass | writer's assignment is an EF no-op |
| `AddLinkAsync` | `Create(TaskReferenceLink l, [MaTask: null→T])` | Row Created, Edge Added (l→T) | `MaTask` is CopyOnCreate for merge, but `IsRegisteredStructuralRoute` (H-1/A-2) ⇒ RouteKnown |
| `UpdateLinkAsync`, fields differ | `UpdateFields(TaskReferenceLink l, [changed ⊆ Category, SortOrder, Title, Url])` | Row FieldsChanged **only — no edge** | The ticket's "link edit ⇒ child-edge intent" is loose: plan §6 says `UpdateFields`, and it names only `l` |
| `UpdateLinkAsync`, fields equal | none | — | `ThemTask` calls Update for *every* surviving link, so this is the common case |
| `UpdateLinkAsync`, target missing/dead | none + business-validation error | empty ⇒ pass ⇒ existing `InvalidOperationException` thrown at step 5 | preserves today's message and the gate order (§5) |
| `DeleteLinkAsync`, live link | `Tombstone(TaskReferenceLink l)` | Row Tombstoned, Lifecycle Tombstone, Edge Removed (l→T) | |
| `DeleteLinkAsync`, missing id | none | — | today a no-op (`FindAsync` → null). Emitting an intent would be wrong: `ReadCurrentAsync` → null ⇒ `WasLive` defaults to `true` ⇒ a fabricated Tombstoned row |
| `DeleteLinkAsync`, already-dead link | see §3f | | |

### 3b. Partial writes in `ThemTask` — what the user ends up with

Fixed call order: **(0)** `LuuHocKyAsync` — committed, already fenced (Slice 4) → **(1)** `UpsertNoteAsync` →
**(2)** `GetLinksAsync` (read) → **(3)** `DeleteLinkAsync` × removed links → **(4/5)** per `StudyLinks` item in
list order, `UpdateLinkAsync` (existing) or `AddLinkAsync` (new). Each is its own context and save today.

Which conflict can reject which call (INFERENCE from the four policies + `ConflictDependencySelector`):

| Unresolved record | (1) note create | (1) note edit | (3) link delete | (4) link update | (5) link add |
|---|---|---|---|---|---|
| S2/S3 on K(T) | **Blocked** `CONS.ScopeAcquired` | **Blocked** `CONS.OccupantContent` | not selected | not selected | not selected |
| S1-CR / S1-PT on T | Passed `ChildEdgeOnly` | not selected | Passed `ChildEdgeOnly` | not selected | Passed `ChildEdgeOnly` |
| Unsupported / unreadable record on T (identity-reached) | **Blocked** (edge parent T selected) | not selected (T only enters `constraintValues`) | **Blocked** | not selected | **Blocked** |
| any record on a link id `l` | — | — | Blocked (Unsupported) | Blocked | n/a (new id) |

So:
- **S2/S3 on K(T)** stops everything at call (1). Committed: the task save only. Every link change is lost, and that includes a links-only edit, because of the null-note create.
- **Identity-reached record on T:** committed are the task save, the note *edit* and the link *updates*; **not** committed are the deletes and adds. This is the real "3rd of 5" case.
- **In production none of these can happen yet.** Sync is unwired, so no record exists (fixture-staged only). The same partial shape *does* happen today on any **non-fence** failure: D-1, `SQLITE_BUSY`, or `UpdateLinkAsync` on a link deleted meanwhile.

**OBSERVED (throwaway VM probe, real repositories, editor wrapper failing the first `AddLinkAsync`; removed
after the run).** Edit T (rename, note `"ghi chu"`, edit L1's title, add L2):

- click 1 throws. DB: task renamed, note saved, L1 edited, L2 absent. VM: `TextNutThem = "Thêm Deadline"`
  (edit mode left), form **not** cleared, both link items now carry `MaTask = T`.
- click 2 (the user retries): **a second task "Da sua" is created** (live tasks 1 → 2), gets its own note, then
  `AddLinkAsync(L1)` fails with `SQLite Error 19: UNIQUE constraint failed: TaskReferenceLinks.Id`.

This is pre-existing for non-fence failures. Wiring the fence adds new triggers for the same shape.

**Options. The owner picks; I make no pick.**

| | What | Atomicity | Port | Cost / risk |
|---|---|---|---|---|
| **E1** | Each of the four calls fenced alone, own context + tx | per call. The task save and calls before the rejected one stay committed (table above) | unchanged (L1 holds) | smallest diff. Partial state documented. Does not by itself fix the duplicate-on-retry. Needs OD-7 handling (§3c) |
| **E2** | New port method (e.g. `SaveEditorAsync(taskId, content, links)`): the whole note + links diff is **one** `MutationRequest` in one executor transaction. `ThemTask` calls it once. The four existing methods stay (fenced, for tests and compatibility) or become internal | editor all-or-nothing. The task save (0) is still separate, so "task saved, editor rejected" remains | **adds** a method to `ITaskEditorRepository` (needs a ruling against L1). Existing signatures unchanged | medium: diff planner over note + links. `ThemTask`'s diff loop moves behind the port. Also needs OD-7 handling, but only one shape: editor untouched |
| **E3** | Fold note + links into the semester save (task + editor in one tx) | full command atomic | `LuuHocKyAsync`/planner/writer change | **out of scope** by the ticket's fence (`LocalSemesterSaveExecutor` behaviour, frozen planner). Listed for completeness |

### 3c. OD-7 on the editor path

State `ThemTask` holds (INFERENCE + the probe above):

| State | Changed before an editor await? | After an editor failure today |
|---|---|---|
| `_taskDangSua`, `TextNutThem`, `MauNutThem` (edit mode) | yes. Cleared right after (0) succeeds (Q-1, PR #106) | edit mode **left** ⇒ next click = create ⇒ duplicate (OBSERVED) |
| `_editingTaskId` | yes, `= null` before call (1) | lost |
| `StudyLinks[i].SortOrder`, `.MaTask` | yes, rewritten before (2) | kept (harmless on their own) |
| `NoteContent`, `StudyLinks` contents, `TenTask`, … | no. Form clear runs only after all calls | kept on screen, including the rejected edit |

Constraints:
1. **R2 does not transfer.** The editor executor receives `(taskId, content)` or a detached link POCO. It never
   sees `NoteContent`/`StudyLinks`/edit mode, so under L1 any restoration must live in `ThemTask` (or, under
   E2, behind the new port's inputs).
2. **N-10 scan** (`MutationRejectedCatchSourceScanTests`): the allowed catch sites list is empty. A
   `catch (Exception) { …; throw; }` passes the regex. Telling a rejection apart *inside* the body
   (`LocalSaveRejection.TryFind`) would pass too, but it is the same discrimination the scan exists to keep out
   of production. Using it would be exploiting a regex gap, so I would only do it with an explicit ruling.
3. A success-flag `try { …; ok = true; } finally { if (!ok) … }` has no catch. It restores on **every** failure.
   D-5 V2 (keep edits on transient failures) was ruled for the semester graph only. Whether it extends to editor
   form state is open.
4. `LocalSaveRejection.UserMessage` says the in-app data "đã được đưa về trạng thái đã lưu". That is true on
   the editor path only if the chosen option reloads note/links.

Restoration options (each combines with E1 or E2):

- **R-a** On an editor failure, stay in edit mode on T (`_taskDangSua = savedTask`, `_editingTaskId = taskId`)
  and keep the form as is. Fixes the duplicate. But the rejected edit stays in memory, and the next click
  re-submits it (user-initiated, but sticky). This conflicts with OD-7 (2): "a rejected request must not
  survive in memory".
- **R-b** On an editor failure, stay in edit mode on T **and** reload `NoteContent`/`StudyLinks` from
  persisted state (`GetBundleAsync(T)`), then rethrow. RAM = DB, the rejected edit is gone, and there is no
  duplicate: the X-20 analogue. Needs a `catch (Exception) … throw;` (or `finally`) in `ThemTask`.
- **R-c** = R-b, but only for fence rejections. Non-fence failures get R-a (keep edits, D-5 V2 spirit). This
  requires discriminating the rejection in the VM (constraint 2).

Sub-question **Q-1′**: does Q-1's "leave edit mode only once **the save** succeeded" mean the task save (today's
placement) or the whole command (task + editor)? R-a/R-b/R-c all read it as the whole command.

### 3d. X-19 allowlist — inventory vs plan §13

Scan (production `.cs`, excluding `obj/`): calls on the six synced DbSets (`HocKys, MonHocs, StudyTasks,
StudyLogs, TaskNotes, TaskReferenceLinks`) to `Add/AddRange/Update/Remove/RemoveRange/Attach`, plus
`.MaMonHoc =` / `.MaHocKy =`.

| File | Sites | On plan allowlist? |
|---|---|---|
| `Mutations/SemesterGraphWriter.cs` | `HocKys.Add`, `MonHocs.Remove/Add`, `StudyTasks.Remove/Add`, `.MaMonHoc =` ×2 | ✔ |
| `SQLite/TaskCascadeHelper.cs` | `TaskNotes.Remove`, `TaskReferenceLinks.RemoveRange` | ✔ |
| `Repositories/SqliteHocKyRepository.cs:58` | `task.MaMonHoc =` (read-time dedup) | ✔ |
| `Sync/Apply/EntitySnapshotMapper.cs:114,121` | `.MaHocKy =`, `.MaMonHoc =` | ✔ |
| `Repositories/SqliteStudyTaskRepository.cs:42,49,63` | `StudyTasks.Add/Update/Remove` | ✔ (OD-5) |
| `Repositories/SqliteTaskEditorRepository.cs:40,60,98` | `TaskNotes.Add`, `TaskReferenceLinks.Add/Remove` | moves to `TaskEditorWriter.cs` (✔) in Phase 2 |
| **`Repositories/SqliteStudyLogRepository.cs:25`** | **`StudyLogs.Add(log)`** | **✘ EXTRA.** StudyLog *is* a synced DbSet, so the "telemetry repos for non-synced tables" clause does not cover it. Explanation: append-only focus-session log, `StudyLog.MaTask` deliberately not fence-routable (registry edge `FenceRoutable: false`, H-1/A-2 CASE B). Not moved. **Allowlisting needs the owner** |
| `Sync/Apply/ConflictResolver.cs:329`, `Sync/Apply/SyncApplySession.cs:553` | generic `db.Add(tracked)` / `st.Db.Add(tracked)` | ✔ listed, but **the literal pattern ("on the six DbSets") does not match them** |

Write paths the literal pattern does not see (reported, not proposed for X-19 unless the owner wants it):
- `Sync/Apply/ConflictStaging.cs:158`: raw `DELETE FROM TaskNotes` (M5). Already guarded by `SyncApplyAuditFenceTests`.
- `Data/AppStartup.cs:30`: raw `UPDATE HocKys SET IsSeeded = 1 WHERE Ten = 'Học Kỳ Dev Seed'`. A startup write on a synced table (`IsSeeded` column).
- `Data/SyncStamper.cs:71`, `Sync/SyncBaseSnapshotStore.cs:105`: `entry.State = EntityState.Modified` (soft-delete conversion).

Scanner pitfalls:
- `MonHoc.MaMonHoc`/`HocKy.MaHocKy` are **PKs** set in model constructors (`Models/HocKy.cs:35,41`, `MonHoc.cs:29,35`). Only member-access `x.MaMonHoc =` should match, never a bare `MaMonHoc =`.
- `QuanLyTaskViewModel.cs:194` sets `MaMonHoc = …` in an **object initializer** for a new task. It is persisted only through the fenced save; the member-access pattern does not match it.

Proposed scanner (engineering, unless the owner objects):
- DbSet-typed calls **plus** generic `<ctx>.Add/Remove/Update/Attach(` on `AppDbContext` receivers (to cover the two Sync sites honestly);
- member-access FK assignments;
- raw SQL out of scope (pointer to `SyncApplyAuditFenceTests`);
- the non-vacuity self-check runs against `SemesterGraphWriter.cs`.

### 3e. D-1 — CONFIRMED (OBSERVED)

Throwaway probe (`SyncApplyFixture`, real `SqliteTaskEditorRepository`, removed after the run): Upsert `"a"` →
tombstone the note via `Remove` (stamper ⇒ `IsDeleted = true, Rev = 2`) → Upsert `"b"` ⇒ `DbUpdateException`,
inner `SQLite Error 19: 'UNIQUE constraint failed: TaskNotes.MaTask'`. Still 1 note row for T.

Reachability (INFERENCE): the only local note tombstone is `TaskCascadeHelper.cs:24`, which runs only when the
owning task is tombstoned. `ClearNote` sets content to null and never deletes. So no local UI path produces
"live task + dead note" today; only sync can. Not fixed (R-9). Phase 1 tests use live-note scenarios.
Under the executor, D-1 stays a **non-fence** persistence failure: the fence passes (or blocks on K(T)), then
SaveChanges throws, rollback, rethrow. Behaviour unchanged.

### 3f. Adjacent observation — `DeleteLinkAsync` re-stamps an already-dead link (OBSERVED, D-2 class)

`FindAsync` is unfiltered: deleting an already-tombstoned link bumps `Rev` (2 → 4 over two re-deletes in the
probe; `DeletedAtUtc` unchanged). UI-unreachable: `ThemTask` deletes only ids from the live-only
`GetLinksAsync`. Under M-3/A-1 a re-stamp is not a semantic effect, so the planner emits **no intent** for a dead
target. Plan: keep the writer's behaviour byte-identical (re-stamp preserved, no fix folded in, R-9) and file it as
**D-7 candidate**. The owner can instead rule "dead target ⇒ no write".

## 4. Owner decisions needed before Phase 1 (all ruled 2026-10-16 — see §6)

1. **E-option**: E1 / E2 / (E3 out of scope) — §3b.
2. **OD-7 on the editor path**: R-a / R-b / R-c — §3c. Plus **Q-1′** (task save vs whole command), and whether a
   `catch (Exception) { restore; throw; }` in `ThemTask` is acceptable under N-10.
3. **X-19**: allowlist `SqliteStudyLogRepository.cs`? Accept the proposed scanner scope (§3d)?
4. **D-7 candidate** (§3f): preserve the re-stamp (default) or rule "dead ⇒ no write"?

## 5. Not run (Phase 0)

- No implementation, no Phase-1 tests, no mutants. Probes were throwaway files, deleted; the worktree is clean.
- The GitNexus index was not refreshed (`npx gitnexus analyze` not run). Every impact result above was cross-checked by grep.

## 6. Owner rulings applied (2026-10-16)

| # | Ruling | Where it landed |
|---|---|---|
| 1 | **E2** + dated L1 amendment (additive port method, no signature change) | `ITaskEditorRepository.SaveEditorAsync`; `SqliteTaskEditorRepository` overrides it with `LocalTaskEditorExecutor.SaveEditorAsync` (one request, one decision, one transaction); `ThemTask` calls it once |
| 2 | **R-a + Q-1′ = whole command** | `ThemTask` leaves edit mode and clears the form only after the task save and the editor save; no catch; no reload |
| 2′ | **Create mode** (asked after Phase 1, option 1) | after a successful create-mode task save, `ThemTask` sets `_taskDangSua`/`_editingTaskId` to the saved task (button "Cập Nhật") before the editor call; reset on success |
| 3 | **X-19** allowlist + scanner scope | `SyncedEntityWritePathFenceTests`; `SqliteStudyLogRepository.cs` allowlisted with its reason |
| 4 | **D-7** filed | see §9 (which of the two outcomes) |

Recorded in [`docs/specs/2026-10-16-fence-slice5-owner-rulings.md`](../specs/2026-10-16-fence-slice5-owner-rulings.md), with
2026-10-16 pointers in the 2026-09-14 record (OD-2/L1, OD-7) and the 2026-10-03 record (Q-1, OD-5 "Implemented by").

## 7. What changed

**Production** (all inside the scope fence):

| File | Change |
|---|---|
| `Repositories/ITaskEditorRepository.cs` | `+ SaveEditorAsync` as a **default interface method** whose body is the pre-Slice-5 four-call sequence. *Why a DIM:* three existing test doubles implement the port, and a plain member would not compile without editing them (a STOP condition). The doubles keep recording the four calls, so `EditModeSaveFailureTests` stays unchanged and green |
| `SQLite/Mutations/TaskEditorPlanner.cs` (new) | pure planner: live rows + input → writer plan + `MutationRequest` (plan §6). Note: none → `Create` (+`MaTask` relation, K(T) Acquired); other content → `UpdateFields[Content]`; same content → nothing. Link: add → `Create` (+`MaTask`); edit → `UpdateFields[changed ⊆ Category, SortOrder, Title, Url]`; live delete → `Tombstone`; dead or missing → no intent. `UpdateLink` on a missing/dead target → validation error |
| `SQLite/Mutations/TaskEditorWriter.cs` (new) | the four former mutation bodies; no decisions, no `SaveChanges` |
| `SQLite/Mutations/LocalTaskEditorExecutor.cs` (new) | own context + `BeginTransactionAsync` → load → plan → `FenceRouter.EvaluateAsync` (same context/tx) → `ApplyIfPassed` (gate, then validation, then writer) → one save → commit; catch-all rollback + rethrow. Returns the decision (tests read P-CR-5's Passed entry); the repository discards it |
| `SQLite/Repositories/SqliteTaskEditorRepository.cs` | reads unchanged; the four mutations and `SaveEditorAsync` delegate to the executor |
| `ViewModels/QuanLyTaskViewModel.cs` (`ThemTask` only) | rulings 2 and 2′; the editor writes go through one `SaveEditorAsync` call |

**Tests:** 31 new tests in 5 new files. No existing test was edited.

| File | Covers |
|---|---|
| `Mutations/LocalTaskEditorFenceTests.cs` (11) | P-K-1, P-K-4, P-K-7, P-CR-5 (record ≠ subject ≠ contract), P-PT-5; each port method Blocked under a record that reaches it; E2 all-or-nothing ("3rd of 5"); the links-only null-note pin; S1-CR passed child edges |
| `Mutations/LocalTaskEditorGateOrderTests.cs` (12) | N-6; RouteKnown = false; gate before validation; today's validation message; rollback on a mid-save PK collision; D-1 under the executor; end-to-end "writer never ran" on rejection; N-9 no-ops; D-7 ×2; intents ⇔ writes |
| `Mutations/SyncedEntityWritePathFenceTests.cs` (3) | X-19, stale-allowlist check, non-vacuity self-check |
| `Repositories/StudyTaskRepositoryMutationGuardTests.cs` (2) | OD-5 guard (receiver-type resolution) + self-check |
| `ViewModels/EditorSaveFailureRetryTests.cs` (3) | ruling 2 failure retry, ruling 2 rejection retry, ruling 2′ create-mode retry |

**Phase 1 RED (OBSERVED, commit `b012772`, skeletons throwing):**
- 23 of the 29 tests that existed then failed, each for the intended reason: the old unfenced path, or a skeleton.
- 6 were green by design: the two self-checks, the OD-5 guard (no caller exists), and three characterizations that hold both before and after (`NoOp_SameNoteSameLinks…`, `D1_…RolledBack`, `D7_SaveEditorAsync_NeverTouchesADeadLink`).
- The 1118 baseline tests were unchanged.
- The create-mode test and the end-to-end writer test came after the create-mode ruling, before Phase 2 (commit `c9fcbcf`).

**Phase 2 (OBSERVED):** build 0 errors, 96 warnings (same as the baseline); **1149 passed / 0 failed / 1 skipped (1150)**.

## 8. Mutants (OBSERVED; each one applied, built and run against the full suite, then reverted with `git checkout`)

| # | Mutant | Result | Caught by (examples) |
|---|---|---|---|
| M1 | remove the fence call (vacuous pass) | **RED** (12) | PK1, PK4, PCR5, PPT5, the four per-method rejections, E2 all-or-nothing, the VM rejection retry |
| M2a | writer before the gate inside `ApplyIfPassed` | **RED** (3) | `N6_BlockedDecision…`, `RouteUnknown…`, `RejectedRequest_WriterNeverRan…` |
| M2b | writer before the fence call in `RunAsync` | **RED** (4) | `RejectedRequest_WriterNeverRan…` (the one test aimed at it); also today's `UpdateLinkAsync_*_Throws` |
| M3a | drop the TaskNote `MaTask` edge from the note `Create` intent | **RED** (4) | PK1, the links-only null-note pin, intents ⇔ writes |
| M3b | drop the link `MaTask` edge from the link `Create` intent | **RED** (4) | PCR5, PPT5, `AddLink_UnderUnsupportedRecord…`, intents ⇔ writes |
| M3c | registry: `StudyTask→TaskReferenceLink` edge not fence-routable (`Sync/**`, reverted) | **RED** (29) | every link path (RouteKnown false), incl. existing H-1 router tests |
| M4 | X-19 self-check: `db.StudyTasks.Remove(x)` added to a VM | **RED** (1) | `X19_SyncedWrites_OnlyInAllowlistedFiles` |
| M5 | OD-5 guard self-check: `_studyTaskRepo?.DeleteAsync(..)` in `WeightOptimizerViewModel` | **RED** (1) | `OD5_NoProductionCodeCallsStudyTaskRepositoryMutations` |
| M6 | `ThemTask` leaves edit mode before the editor save (pre-ruling order) | **RED** (3) | the three `EditorSaveFailureRetryTests` |
| M7b | E2 override replaced by the non-atomic four-call sequence | **RED** (4) | E2 all-or-nothing, mid-save PK-collision rollback, both VM failure retries |
| M8 | create mode not switched to editing the saved task | **RED** (1) | `ThemTask_CreateMode_EditorSaveFails…` |
| M9 | business validation before the gate | **RED** (1) | `GateRunsBeforeBusinessValidation…` |

M7 as first written (delete the override) did not compile — a test calls `SaveEditorAsync` on the concrete type — so
it was replaced by M7b. After every mutant the source tree was restored (`git status` showed only docs).

## 9. Findings: OBSERVED / INFERENCE / NOT RUN

**OBSERVED**
- E2 closes the Phase 0 duplicate-on-retry. With an editor failure injected inside SaveChanges, no note or link is written and the VM stays on T. The second click then writes exactly one task, its note and both links (`EditorSaveFailureRetryTests`).
- D-7 (ruling 4 asked which outcome): **both**.
  - `SaveEditorAsync` cannot reach a dead link, because its diff reads live links only.
  - `DeleteLinkAsync` still re-stamps a dead link. That's unchanged, and a test pins it so the D-7 fix flips it.
  - After Slice 5, `DeleteLinkAsync` has no production caller.
- D-1 under the executor: `UNIQUE constraint failed: TaskNotes.MaTask`, and the whole request rolls back, including a link add in the same request. Still unfixed (R-9), and still unreachable from the UI (INFERENCE, Phase 0 §3e).
- ~~A links-only edit with no live note still creates a `Content = null` note, so S3 on K(T) rejects it.~~ **Superseded by Q2 (§11):** `PlanSave` no longer creates that note; the pin was flipped.
- The GitNexus index that `detect_changes` reads points at the owner's main checkout, not this worktree. The scope check is therefore the **branch diff** `origin/dev...HEAD` (§10).

**INFERENCE (code read, not run)**
- ~~`LocalSaveRejection.UserMessage` ("đã được đưa về trạng thái đã lưu") isn't literally true for the editor form under R-a.~~ **Superseded by Q1 (§11):** the editor's rejection now has its own wording.
- Pre-existing and unchanged: `ThemTask` skips the editor save when the note is empty and there are no links. Removing the last link of a note-less task is therefore not persisted (R-9).
- A retry after an editor failure fires `task_update` telemetry and the difficulty-label log again; both fire after every successful task save. `DifficultyLabelLoggingTests` doesn't retry, so it's unaffected.
- GitNexus rates `ITaskEditorRepository`'s impact HIGH: 4 direct implementers, 3 of them test doubles. The DIM keeps all of them compiling unchanged.

**NOT RUN**
- No manual UI run of `ThemTask`. The owner accepted the brief "Cập Nhật" flash on a successful create, but I didn't observe it.
- `npx gitnexus analyze` not run. The index is stale; the owner re-runs it after merge.
- No production conflict record exists, so none of this was exercised against real sync data.

## 10. Scope check (branch diff, not `gitnexus_detect_changes`)

`git diff --stat origin/dev...HEAD`:
- **Production:** the six files in §7 (one additive port method; in the VM, `ThemTask` only), plus `Mutations/LocalSaveRejection.cs` for Q1 (§11) — seven, all inside the scope fence.
- **Tests:** five new files.
- **Docs:** this report, the new rulings record, pointers in the 2026-09-14 and 2026-10-03 records, plan §21 row 5 and its Status line, `docs/CHANGELOG.md`, `docs/active/README.md`.
- **Not touched:** `Sync/**`, `AppDbContext`/`SyncStamper`, XAML, schema, packages, any existing test.

## Decisions made (engineering, Phase 0)

- **Probes as throwaway files, not committed.** *Why:* Phase 0 allows no code. The ticket wants OBSERVED
  evidence for D-1 and the partial-write consequence. *What for:* the owner's E/OD-7 choice rests on a measured
  duplicate-on-retry, not on an inference. *Experience:* the VM probe matched the code-read prediction exactly
  (duplicate task + PK collision), so the reading of `ThemTask` holds.
- **No pick among E1/E2/R-a..c.** *Why:* the ticket reserves it for the owner, and E2 crosses L1.
- **Dead-link delete ⇒ no intent, writer unchanged.** *Why:* M-3/A-1 says a re-stamp is not a domain effect, and
  R-9 says adjacent defects are not fixed in fence PRs. *What for:* both rules hold without an owner ruling. The
  ruling is only needed if the owner wants the re-stamp gone.

## Decisions made (engineering, Phases 1–3)

- **`SaveEditorAsync` as a default interface method.** *Why:* the owner ruled one additive method, and the STOP list
  forbids editing existing tests. Three test doubles implement the port. *What for:* the doubles compile and keep
  recording the four calls, so the pre-existing Q-1 retry test still proves its claim without changes. *Experience:*
  the default body is non-atomic by design. Only `SqliteTaskEditorRepository` implements the port in production, and
  it overrides the default; mutant M7 shows the tests notice when it doesn't.
- **One planner and one executor loop for all five entry points.** *Why:* the four single methods and
  `SaveEditorAsync` must agree on intents and writes; otherwise the fence judges something other than what is
  written. *What for:* the intents ⇔ writes test covers every operation once. *Experience:* the planner fixes the new
  note's id up front, which is what makes that test exact.
- **The executor returns its `FenceDecision`.** *Why:* P-CR-5 must see a Passed entry on a call that succeeds. An
  observer callback or a stored "last decision" would make a stateless executor stateful. *What for:* tests read
  decisions without a test-only hook.
- **An end-to-end "writer never ran" test on top of N-6.** *Why:* Slice 4's N-6 test catches a writer moved ahead of
  the gate inside `ApplyIfPassed`. It doesn't catch a writer moved ahead of the fence call, because the rollback hides
  that from the tables. *What for:* mutant M2b. *Experience:* recording tracker state at `Dispose` is enough; no new
  shared test double was needed.
- **The mid-save failure is injected on the link row, not on the first save.** *Why:* a failing note write would pass
  under the old four-call sequence too, since nothing after it ran. *What for:* the rollback and retry tests tell old
  from new behaviour, which is why they were RED in Phase 1.
- **Create-mode behaviour was asked, not inferred.** *Why:* ruling 2 names edit mode only, and making a just-created
  task the edited one is visible UI state. The owner ruled option 1 the same day.

## 11. Owner answers Q1 / Q2 (2026-10-16, after the PR) — applied

Recorded in the [rulings record](../specs/2026-10-16-fence-slice5-owner-rulings.md) §7–§8.

**Commits**
- `1449a62` test: flip the links-only pin to Q2 — **RED by design** (its own commit, as ruled; fails with
  `CONS.ScopeAcquired` until the next commit).
- `7dbcbd0` feat: Q2 in `PlanSave`, plus 4 tests (null/empty content ⇒ no note intent or row; live note edited to
  empty stays `UpdateFields`; `UpsertNoteAsync(null)` still creates the null note).
- `6db70ac` feat: Q1, plus 3 tests (editor rejection via `SaveEditorAsync` and via RouteUnknown carries the
  form-kept wording and not "restored"; a real semester rejection keeps "restored" and not the form-kept line).

**OBSERVED**
- Build 0 errors / 96 warnings (full rebuild, same as baseline). Suite **1156 passed / 0 failed / 1 skipped (1157)**.
  New tests on this branch: 38 (31 + 7). Existing tests changed: **none**. `LocalSaveRejectionTests` was not edited:
  its "restored" assertion uses an unmarked rejection, which keeps that wording.
- After `7dbcbd0`, no pre-existing test went red (STOP check for Q2).
- Mutants (applied after committing, run against the full suite, reverted; tree clean afterwards):

| # | Mutant | Result |
|---|---|---|
| M10 | drop the FormKept marker on the editor's rejection | **RED** (2) |
| M11 | `PlanSave` creates the note unconditionally (pre-Q2) | **RED** (3) |
| M12 | `PlanSave` condition content-only (loses "live note edited to empty") | **RED** (1) |
| M13 | Q2 skip moved into the shared `Builder.Note` (reaches `UpsertNoteAsync`) | **RED** (1) |
| M14 | `UserMessage` ignores the marker | **RED** (2) |

**INFERENCE / not changed**
- The `App.DispatcherUnhandledException` comment ("the executor already restored the in-memory graph before
  throwing") is now inaccurate for the editor path. `App.xaml.cs` is outside the Slice 5 scope fence; not edited.
- The port's default-interface body (test doubles only) still upserts the note unconditionally; the SQLite override
  differs on the no-live-note, empty-content case (ruled "PlanSave only").
- A future rejection producer that does not set the marker gets the "restored" wording.

## Decisions made (engineering, Q1 / Q2)

- **The marker sits on the editor's rejection, not on the semester's.** *Why:* the semester executor's behaviour is
  out of scope, and `MutationRejectedException` (`Sync/Fence`) must not change. `Exception.Data` is the channel Slice 4
  already uses for restore/repair failures. *What for:* the semester wording and its existing test stay untouched.
  *Experience:* the cost is the default — an unmarked future producer reads as "restored"; recorded above.
- **The Q2 condition is in `PlanSave`, not in the shared `Builder.Note`.** *Why:* the ruling keeps `UpsertNoteAsync`
  unchanged, and both share `Builder.Note`. *What for:* M13 shows the `UpsertNoteAsync(null)` test catches the move.
- **The flipped S3 test compares note rows before/after rather than asserting none.** *Why:* the S3 staging leaves a
  non-live note row in K(T); "no rows" would fail for the seed, not for the change. It reads the executor's decision
  to assert `FencePassed` instead of assuming the link add passes under S3.
