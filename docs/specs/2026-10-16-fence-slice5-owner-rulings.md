# T2.4 Fence Slice 5 (task editor) — Owner Rulings: E2 / L1 amendment, R-a + Q-1′, create mode, X-19, D-7 (recorded)

| | |
|---|---|
| **Date** | 2026-10-16 |
| **Status** | E2: **CLOSED — E2, with a dated L1 amendment** · OD-7 on the editor path: **CLOSED — R-a, Q-1′ = whole command** · create mode: **CLOSED — extension of the R-a ruling** · X-19: **CLOSED — allowlist + scanner scope accepted** · D-7: **FILED — separate ticket** |
| **Closes** | The four Phase 0 questions of fence Slice 5 ([report](../reports/2026-10-16-epic2-fence-s5-task-editor.md) §4), plus the create-mode question raised after Phase 1 |
| **Amends** | OD-2 / **L1** (2026-09-14 §2.3) — *additive only* (§1 below). OD-7 (2026-09-14 §2.4) and Q-1 (2026-10-03 §4) — *scope extension only* to the editor's form state (§2–§3) |
| **Does not amend** | D1–D9 frozen record; SB-3, OD-4; D-5 V2; M-3/A-1 live-only predicate; D8-H fingerprint definition; `ConflictResolver`; any `Sync/**` semantics; any existing `ITaskEditorRepository` signature |
| **Leaves open** | SB-2/OD-1 (Slice 6), OD-6, OD-8, G4; D-1 (R-9); D-6; D-7 (§5) |
| **Implemented by** | fence Slice 5, branch `feat-epic2-fence-s5-task-editor` (PR <PR>) |

Labels: **[R]** owner ruling · **[D]** derived from a ruling, not itself a ruling.

## 1. E2 — CLOSED, with a dated L1 amendment

**[R] Ruling (2026-10-16).** Add ONE new `ITaskEditorRepository` method that applies the whole note + links diff in
one executor transaction (one `MutationRequest`, one fence decision). Additive only: the four existing methods keep
their signatures and are fenced individually. `ThemTask` switches to the new method.

**[R] L1 amendment (2026-10-16).** *Additive port method allowed; no existing signature changes.* L1 (2026-09-14 §2.3)
otherwise stands: no application-service layer, no VM call-site migration beyond `ThemTask`.

**[D] Shape.** `SaveEditorAsync(Guid taskId, string? noteContent, IReadOnlyList<TaskReferenceLink> links, ct)` — a
default interface method whose body is the pre-Slice-5 four-call sequence (so the existing test doubles compile
unchanged), overridden by `SqliteTaskEditorRepository` with one fenced transaction.

**Why (Phase 0 §3b).** With one call per write, a conflict reaching part of an editor change committed the rest
(the "3rd of 5" case), and an editor failure mid-command left a partial state the retry then duplicated (OBSERVED).

## 2. OD-7 on the editor path — CLOSED: R-a, Q-1′ = whole command

**[R] Ruling (2026-10-16).** `ThemTask` leaves edit mode and clears the form only after BOTH the task save and the
editor save succeed. On any failure (rejection or generic): stay in edit mode, keep the form as typed, no reload, no
catch in the VM (N-10 stays true). Test: editor failure mid-command, next click updates the same task (1 task row,
no duplicate, no UNIQUE error).

**[D] Consequence.** Under R-a a rejected editor edit stays in the form; the next click re-submits it and is rejected
again while the record stays unresolved. The rejection is still surfaced through `App.DispatcherUnhandledException`.
`LocalSaveRejection.UserMessage` says the data "đã được đưa về trạng thái đã lưu", which is not literally true for
the editor's form under R-a — reported, not changed (it would change `LocalSaveRejectionTests`).

## 3. Create mode — CLOSED: extension of §2

**[R] Ruling (2026-10-16), option 1.** Once the create-mode task save succeeds, `ThemTask` treats the new task as the
one being edited until the whole command succeeds — same invariant as §2 (Q-1′ = whole command). Set before the
editor call, no catch in the VM. Test: create mode, editor fails/rejected, next click updates that task (1 task row,
no duplicate). A brief "Cập Nhật" flash on success is accepted.

## 4. X-19 — CLOSED

**[R] Ruling (2026-10-16).** Allowlist `SqliteStudyLogRepository.cs` with a comment (append-only, outside the fence by
design). The proposed scanner scope is accepted. Raw SQL, generic `db.Add` and `Entry().State` are listed as known
blind spots in the report.

**[D] Scanner scope.** DbSet-typed writes on the six synced tables; the generic form on `db`/`ctx`/`context`/`.Db`
receivers; member-access `.MaMonHoc =` / `.MaHocKy =`; a stale-allowlist check; a non-vacuity self-check.

## 5. D-7 — FILED

**[R] Ruling (2026-10-16).** File it: re-tombstoning an already-deleted link re-stamps `Rev`. Intended fix "already
deleted ⇒ no write", like D-2, in a separate ticket. Behaviour unchanged in Slice 5 unless E2's live-only diff makes
it unreachable — report which.

**[D] Outcome.** Both: unreachable through `SaveEditorAsync` (its diff reads live links only; pinned by
`D7_SaveEditorAsync_NeverTouchesADeadLink`), still present on `DeleteLinkAsync` (unchanged; pinned by
`D7_DeleteLinkAsync_OnAlreadyDeletedLink_StillRestamps_Unchanged`, which the D-7 fix flips). After Slice 5
`DeleteLinkAsync` has no production caller.

## 6. Consistency check

| Check | Result |
|---|---|
| Any frozen D1–D9 text changed? | No |
| Any existing port signature changed? | No — one additive method (§1) |
| Any `Sync/**` change? | No |
| N-10 (no production catch of `MutationRejectedException`) | Still true: `ThemTask` has no catch |
| OD-7 semester-save behaviour (R2, D-5) changed? | No |
| Anything authorised beyond Slice 5? | No |
