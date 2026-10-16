# Epic 2 / T2.4 — Fence Slice 5 (task editor): Phase 0 recon + proposal

| Field | Content |
|---|---|
| **Status** | Phase 0 delivered 2026-10-16 — **waiting for owner rulings** (§4). No production or test code written. |
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

## 4. Owner decisions needed before Phase 1

1. **E-option**: E1 / E2 / (E3 out of scope) — §3b.
2. **OD-7 on the editor path**: R-a / R-b / R-c — §3c. Plus **Q-1′** (task save vs whole command), and whether a
   `catch (Exception) { restore; throw; }` in `ThemTask` is acceptable under N-10.
3. **X-19**: allowlist `SqliteStudyLogRepository.cs`? Accept the proposed scanner scope (§3d)?
4. **D-7 candidate** (§3f): preserve the re-stamp (default) or rule "dead ⇒ no write"?

## 5. Not run

- No implementation, no Phase-1 tests, no mutants. Probes were throwaway files, deleted; the worktree is clean.
- The GitNexus index was not refreshed (`npx gitnexus analyze` not run). Every impact result above was cross-checked by grep.

## Decisions made (engineering, Phase 0)

- **Probes as throwaway files, not committed.** *Why:* Phase 0 allows no code. The ticket wants OBSERVED
  evidence for D-1 and the partial-write consequence. *What for:* the owner's E/OD-7 choice rests on a measured
  duplicate-on-retry, not on an inference. *Experience:* the VM probe matched the code-read prediction exactly
  (duplicate task + PK collision), so the reading of `ThemTask` holds.
- **No pick among E1/E2/R-a..c.** *Why:* the ticket reserves it for the owner, and E2 crosses L1.
- **Dead-link delete ⇒ no intent, writer unchanged.** *Why:* M-3/A-1 says a re-stamp is not a domain effect, and
  R-9 says adjacent defects are not fixed in fence PRs. *What for:* both rules hold without an owner ruling. The
  ruling is only needed if the owner wants the re-stamp gone.
