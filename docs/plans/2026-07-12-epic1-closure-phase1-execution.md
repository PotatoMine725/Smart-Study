# Epic 1 Closure Gate — Phase 1 Execution Plan

> **For implementation agents:** you execute exactly one task card from §Agent dispatch, in a fresh
> session, doing your own task breakdown. Read §Context, §Roles & execution protocol, your task's
> spec, and your agent card before touching anything. Stop for PM review at your card's stop
> condition. Plan authored + reviewed by the PM session ("epic1"); PM does **not** implement.

## Context

Epic 1 (Sync-Ready Data Model) is code complete and merged to `ui_rf` (`a3a0a3d` + post-close fix
`101aaa3`), verified 330/330 green. The PM closure verdict
([`../review/2026-07-11-epic1-closure-verdict.md`](../review/2026-07-11-epic1-closure-verdict.md))
ratified the close **with conditions C1–C3**, and the owner's release gate
([`2026-07-11-epic-1-closure-gate.md`](2026-07-11-epic-1-closure-gate.md)) structures the remaining
work into three phases. **This plan covers Phase 1 only** — the agent-executed tasks (A1–A4) that
must finish before the owner's Phase 2 (supervised first real launch, B1–B4).

The urgency driver is **F5**: the dev DB is a real pre-Epic-1 database (5,402 StudyLogs, no `Rev`
column), so the next app launch fires the first real in-place upgrade — and its safety net
(`DbBackup.CreateBackup`) currently copies only the main `.db` file, silently dropping any
un-checkpointed WAL content. Task A1 closes that hole before the owner launches. **Standing rule
until A1 lands: do not launch the app** (interim protection: verified manual backup at
`bin/Debug/net10.0-windows10.0.19041.0/manual-backup-pre-epic1/SmartStudyData.db`).

Mapping to verdict conditions: **A1 = C3a** (critical) · **A2 = C2 + F3 + ledger #8** ·
**A3, A4 = gate-doc scope** (knowledge distillation + audit). **C1 (epic closing note) is NOT
Phase 1** — it needs Phase 2's results (gate Phase 3, Task C1).

## Scope

- **In:** Tasks A1–A4 from the gate doc, dispatched per §Agent dispatch (Wave 1 parallel: A1 ∥ A3;
  then A2; then A4), one commit per task/concern, PM review gate after each task.
- **Out:** Phase 2 (owner: B1–B4), Phase 3 (closing note, archive, next-epic prep), any epic feature
  work — the gate's Execution Rules prohibit opening Epic 3/2, feature dev, architecture changes,
  SOE/sync/ML work until Epic 1 is released.
- **Untouched:** owner's local modified files in git status (`.claude/settings.json`, `AGENTS.md`,
  `CLAUDE.md`, `Assets/`) — never fold into Phase 1 commits.

## Roles & execution protocol

- **PM:** authored this plan; reviews each task's result against its acceptance criteria before the
  next wave starts; merges A1's worktree branch after code review; runs the Phase 1 exit review.
- **Implementation agent (fresh session per task card):** reads this plan + referenced docs, does its
  own breakdown, implements, self-reports, stops.
- Every task must follow repo rules: `gitnexus_impact` before editing any symbol,
  `gitnexus_detect_changes` before commit (reindex via `npx gitnexus analyze` first — verdict ledger
  #7 says the index is stale), `rtk` prefix on shell commands, **no `Co-Authored-By` trailer**,
  commits split by concern.
- Every task produces a report in `docs/reports/YYYY-MM-DD-<kebab>.md` **with an ADR-style
  "Decisions made" section** (repo convention since 2026-07-07), then **stops for PM review**.

## Decisions locked (PM, before execution)

| # | Decision | Choice | Rationale |
|---|---|---|---|
| D-P1 | A1 fix placement | **Inside `DbBackup.CreateBackup`, signature unchanged** — own short-lived `SqliteConnection`, `PRAGMA wal_checkpoint(TRUNCATE)`, then existing `File.Copy` | Prevent-at-source (same philosophy as M1.3): the utility's contract is "lossless backup"; callers must not need to remember a pre-step. No EF coupling; `AppStartup` diff = zero. Checkpoint is a no-op on non-WAL DBs. |
| D-P2 | A1 failure semantics | **Checkpoint failure propagates** (no catch) | Backup is Epic 1's named top-risk mitigation — fail loudly at startup rather than upgrade against an incomplete backup. Consistent with current `File.Copy` failure behavior. |
| D-P3 | A1 test fixtures | **Convert `DbBackupTests` fake-text fixtures to minimal real SQLite DBs** | The pragma throws on non-DB files; fake-text fixtures are exactly why F5 escaped (clean fixtures, never a live WAL). Same behaviors asserted, honest fixtures. |
| D-P4 | A3 structure | **Flat topic files** in `docs/knowledge/` (owner-confirmed 2026-07-12) | Existing convention (4 flat files, indexed in `docs/README.md`); gate layout was "suggested" only; 5 folders for ~8 files fragments a small knowledge base. |
| D-P5 | Venue | **A1 in a worktree; A2/A3/A4 on `ui_rf` directly** | Owner chose `ui_rf` for the sequential form; amended for parallel dispatch — Wave 1 runs A1 ∥ A3, and two agents must not share one working tree. A1 (code + test runs) isolates in a worktree; A3 creates new files only, safe on the main checkout. |
| D-P6 | Master plan is frozen | **Dated status banner only** on the Epic 1 section — no rewriting frozen content | Gate header: "Master Plan Frozen ✅". Canonical live status belongs to `system_roadmap.md` A.2/A.3 and `active/README.md`. |
| D-P7 | Parallelization | **Partial — Wave 1: A1 ∥ A3; Wave 2: A2; Wave 3: A4** | A1 and A3 touch disjoint files (Data/+Tests/ vs new docs/knowledge/ files). A2 needs A1 merged (CHANGELOG covers the fix) and absorbs the `docs/README.md` index update so Wave 1 stays conflict-free. A4 audits everything, so it is last by construction. Saves ~0.5 day wall-clock; more parallelism buys conflicts, not speed. |

## Agent dispatch — waves, task cards, toolkits

**Kickoff (Step 0, before Wave 1 — orchestrator/owner, on `ui_rf`):** commit the three
currently-untracked gate documents so every agent can reference them:
`docs/review/2026-07-11-epic1-closure-verdict.md`, `docs/plans/2026-07-11-epic-1-closure-gate.md`,
and this plan file. One docs-only commit.

```
Step 0 (baseline docs commit, ui_rf)
  → Wave 1:  Agent W1-A (A1, worktree)  ∥  Agent W1-B (A3, ui_rf)
       → PM code review A1 → merge worktree → PM review A3
  → Wave 2:  Agent W2 (A2, ui_rf) → PM review
  → Wave 3:  Agent W3 (A4, ui_rf) → PM review → PM Phase-1 exit review
  → hand to owner (Phase 2, B1–B4)
```

Environment available to every agent: core tools (Read, Write, Edit, Glob, Grep, Bash/PowerShell
with the **rtk hook** auto-prefixing commands); MCP servers **gitnexus** (`impact`, `detect_changes`,
`context`, `query` — mandatory per CLAUDE.md) and **code-review-graph** (semantic search, review
context); **context-mode** ctx tools (`ctx_batch_execute`, `ctx_execute_file`) for digesting large
outputs without context bloat; **agentmemory** (`memory_recall`); plugins **superpowers** (skills
below), **gitnexus skill pack**, **coderabbit** (PM review use).

### Agent W1-A — Task A1, WAL-safe backup fix (Wave 1, CRITICAL)

| | |
|---|---|
| Mission | Implement + test the C3a backup hardening per §Task A1 spec |
| Venue | Git worktree off `ui_rf`, branch `gate/a1-walfix` |
| Write scope | `SmartStudyPlanner/Data/DbBackup.cs`, `SmartStudyPlanner.Tests/Data/DbBackupTests.cs` only |
| Skills to invoke | `superpowers:using-git-worktrees` (setup) · `superpowers:test-driven-development` (RED-first is mandatory here) · `gitnexus-impact-analysis` · `superpowers:verification-before-completion` |
| Key tools | `gitnexus_impact` on `CreateBackup` before editing · `rtk dotnet build SmartStudyPlanner.slnx` / `rtk dotnet test --no-build` · `gitnexus_detect_changes` before commit · context-mode ctx tools for test output |
| Deliverables | Diff · RED-first evidence (discriminating test failing on baseline) · full suite green · report with "Decisions made" |
| Stop condition | Do **not** merge. Stop after pushing the worktree branch + report; PM runs `/code-review` on the diff and merges |

### Agent W1-B — Task A3, knowledge distillation (Wave 1)

| | |
|---|---|
| Mission | Write the 4 knowledge articles per §Task A3 spec |
| Venue | `ui_rf` main checkout |
| Write scope | **New files in `docs/knowledge/` + cross-link one-liners inside existing `docs/knowledge/*.md` only.** Must NOT touch `docs/README.md`, `CHANGELOG.md`, or anything outside `docs/knowledge/` (A2 owns those — conflict guard for the parallel wave) |
| Skills to invoke | `superpowers:verification-before-completion`; optionally `agentmemory:recall` for Epic 1 history |
| Key tools | Read/Write/Grep · context-mode `ctx_execute_file` to digest the long milestone reviews/reports without loading them whole · `rtk git` for the single docs commit · `gitnexus_detect_changes` before commit |
| Deliverables | 4 articles · one docs commit · report with "Decisions made" |
| Stop condition | Stop after commit + report; PM reviews articles against the six gate questions |

### Agent W2 — Task A2, documentation synchronization (Wave 2)

| | |
|---|---|
| Mission | Execute the §Task A2 edit table (roadmap, CHANGELOG, master-plan banner, active/README, docs/README incl. new knowledge files) |
| Venue | `ui_rf`, after A1 merged and A3 accepted |
| Write scope | Exactly the files in the A2 edit table |
| Skills to invoke | `superpowers:verification-before-completion` |
| Key tools | Read/Edit/Grep (stale-status sweeps) · `rtk git` · `gitnexus_detect_changes` before commit |
| Deliverables | One docs commit · report with "Decisions made" |
| Stop condition | Stop after commit + report; PM read-through of every touched doc |

### Agent W3 — Task A4, documentation consistency audit (Wave 3)

| | |
|---|---|
| Mission | Run the §Task A4 checks against the post-A1/A2/A3 state; apply wording-level fixes; report findings |
| Venue | `ui_rf`, last |
| Write scope | Docs-only wording fixes + the audit report; anything larger is a *finding for PM*, not an edit |
| Skills to invoke | `superpowers:verification-before-completion` |
| Key tools | Grep/Glob/Read · scripted relative-link checker (python via Bash — byte-safe for Vietnamese UTF-8; do not round-trip docs through PowerShell `Get/Set-Content`) · `rtk git` |
| Deliverables | Audit report in `docs/reports/` (doubles as Phase 1 exit evidence) · fixes commit if needed |
| Stop condition | Stop after report; PM runs the Phase 1 exit review |

---

## Task A1 — WAL-safe backup fix (C3a) — spec

**Files:** Modify `SmartStudyPlanner/Data/DbBackup.cs` · Modify
`SmartStudyPlanner.Tests/Data/DbBackupTests.cs` · `AppStartup.cs` unchanged.

**Locked design (D-P1/D-P2):**

```csharp
public static string? CreateBackup(string dbPath, DateTime utcNow)
{
    if (!File.Exists(dbPath)) return null;

    // F5: in WAL mode, committed data may still sit in the -wal sidecar; a naive
    // file copy silently drops it. Checkpoint into the main file first so the copy
    // is complete. TRUNCATE also resets the WAL. No-op on non-WAL databases.
    using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
    {
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
    }

    // ... existing path construction + File.Copy unchanged ...
}
```

**Test requirements:**

1. **Convert the 2 existing tests** to real SQLite fixtures (create via `SqliteConnection`:
   `CREATE TABLE` + `INSERT` a marker row; assert by opening the backup as SQLite and reading the
   row back — replaces `File.ReadAllText`). Missing-file test unchanged in behavior.
2. **New discriminating test (RED-first — prove it fails on unmodified baseline before
   implementing):**
   - Create DB at temp path with `PRAGMA journal_mode=WAL`; on an open writer connection set
     `PRAGMA wal_autocheckpoint=0`, create table, insert marker rows; **keep the writer open**
     (closing would auto-checkpoint and hide the scenario).
   - **Precondition assert:** the `-wal` sidecar exists and is non-empty (this is the live-WAL state
     M1.2's fixtures never had).
   - Call `DbBackup.CreateBackup(dbPath, fixedTimestamp)`; open the `.bak` as SQLite; assert marker
     rows present.
   - Baseline (File.Copy only) → this FAILS. With the checkpoint → PASSES.
   - Known traps: `Microsoft.Data.Sqlite` connection pooling keeps file handles alive — use
     `Pooling=False` in test connection strings and/or `SqliteConnection.ClearAllPools()` (see
     existing `AppStartupFileBasedTests` pattern).

**Acceptance (gate doc + PM):** backup lossless with pending WAL pages (discriminating test GREEN,
RED-first evidence in the report) · existing backup behaviors still asserted and green · full suite
green (330 + new) · build 0 errors · `gitnexus_impact` on `CreateBackup` reported · report + **PM
code review + merge approval** before Wave 2.

**Effort:** ~0.5 day.

---

## Task A2 — Documentation synchronization (C2 + F3 + ledger #8) — spec

Docs-only. Exact edit list (verified stale as of 2026-07-12):

| File | Edit |
|---|---|
| `docs/specs/system_roadmap.md` §A.2 | Append Epic 1 shipped row(s): M1.1 stamping seam + A6, M1.2 schema upgrade + tombstones + G1 (+R1), M1.3 MonHoc identity/dedup (+folded `LuuHocKyAsync` fix), merge `a3a0a3d`, post-close `101aaa3`, A1's DbBackup WAL fix — shipped 2026-07-11/12 |
| `docs/specs/system_roadmap.md` §A.3 item 1 | Rewrite: Epic 1 **done in full** (identity semantics shipped M1.3 — the "remain M1.3" clause at line 51 is stale); state = *code complete, release gate in progress* (link gate doc); keep the base-snapshot-store note (lands with LAN-sync epic) |
| `docs/specs/system_roadmap.md` §A.4 | Add **NU1903 `SQLitePCLRaw`** high-severity advisory beside the NU1904 line (verdict ledger #8) |
| `docs/CHANGELOG.md` | Epic 1 entry in existing format (M1.1/M1.2/M1.3 + `101aaa3` + A1 fix); note NU1903 tracked |
| `docs/plans/2026-07-03-master-plan.md` | **Dated status banner only** (D-P6) on §Epic 1: code complete 2026-07-11, release gate per closure-gate doc, conditions C1–C3 |
| `docs/active/README.md` | Replace stale Epic 1 row ("M1.2 in review") → "Epic 1 release gate" row pointing at gate doc + this plan, per-task state; flag the UI-polish row's status to PM if unclear |
| `docs/README.md` | Reading-order line 4 "Current:" → release gate; knowledge line (5) → mention A3's new files; anything else found stale |
| `docs/architecture/data-model.md` | **Verify-only** (updated per milestone); fix only if the Epic 1 sections misstate shipped behavior |

**Acceptance:** every listed doc reflects the same state; grep sweeps for `remain M1.3`,
`M1.2 in review`, `M1.3 pending` return no live-doc hits (historical reports/reviews are records —
exempt); relative links resolve; report + PM review.

**Effort:** ~0.5 day.

---

## Task A3 — Knowledge distillation (flat files, D-P4) — spec

Create 4 new articles in `docs/knowledge/`, matching the existing distilled style (see
`debugging.md`: symptom → root cause → fix → generalized lesson). Each article answers the gate's
six questions (problem / why hard / wrong assumptions / how solved / principle / how to avoid).
**Distill, don't copy** — reports stay the historical record; every article ends with source links.
**Link to decision records instead of restating decisions** (A4 checks "one authoritative source").

| New file | Content (sources) |
|---|---|
| `release-engineering.md` | WAL backup lesson — why file-copy backups lie under WAL and why clean-fixture tests can't catch it (verdict F5); migration safety — backup-before-upgrade, supervised first launch, reference row counts; "the first real run is a milestone, not a formality" (gate doc, verdict C3) |
| `review-methodology.md` | RED-first discriminating tests; independent verification not trust-the-report; escape analysis (`101aaa3` — healthy escape rate); reproduce-before-escalating (M1.3 pre-existing-bug protocol); folded-fix scrutiny (Option A); completeness checks against `OnModelCreating` ground truth (M1.2-R1 review) |
| `sync-data-model.md` | Sync metadata rationale — Rev never compared across devices (L6), tombstones, G1 cascade-tombstone, single stamping seam; EF cascade-fixup snapshot timing (FK reassign + `DetectChanges()` before `Remove`); identity semantics — normalize keys, prevent-at-source vs read-side dedup (M1.3) |
| `architecture-process.md` | Architecture freeze process (D-A…D-J); hard constraint vs objective; deadline ownership; relative feasibility; constraint ownership — distill the reusable *principles*; link `lessons-learned.md` + the two decision-record files rather than duplicating them |

Cross-link one-liners in existing knowledge files where topics touch (e.g., `debugging.md` → WAL
lesson) — links only, no duplication. **Do not touch `docs/README.md`** (A2 owns the index update —
Wave 1 conflict guard).

**Acceptance:** 4 articles answer all six gate questions; no verbatim report copies; decisions
linked not restated; report + PM review.

**Effort:** ~1 day.

---

## Task A4 — Documentation consistency audit (last) — spec

Docs-only, audits the post-A1/A2/A3 state. Checks (gate doc list, made concrete):

- Stale-status grep sweep across `docs/` excluding `reports/`+`review/` historical records:
  `remain M1.3`, `M1.2 in review`, `M1.3 pending`, `in worktree`, `PROPOSED` (verify each hit is
  intentional).
- Relative-link validation across `docs/` (every `[…](…)` target exists — script it; include the new
  knowledge files and this plan's cross-references).
- Duplicated-decisions check: decision statements (G1, D-A…D-J, C1–C3) have exactly one
  authoritative source; other mentions are links.
- `docs/README.md` indexes the knowledge section (incl. A3's new files); `ROADMAP.md` stub still
  points at `specs/system_roadmap.md`.
- No architectural changes — wording fixes only; anything bigger becomes a finding for PM, not an
  edit.

**Deliverable:** audit report `docs/reports/<execution-date>-epic1-gate-docs-audit.md` (findings +
fixes applied + "Decisions made" section) — doubles as Phase 1 exit evidence.

**Acceptance:** all checks pass or have accepted findings; PM review.

**Effort:** ~0.5 day.

---

## Exit & handoff

Total ≈ 2.5 agent-days, ≈ 2 days wall-clock with Wave 1 parallel. Dependencies: A2 after A1
(CHANGELOG includes the fix) and after A3 (README indexes the new articles) · A4 last (audits
everything).

**Phase 1 exit criteria (PM checklist):** A1 merged with RED-first evidence + suite green · all A2
targets consistent · 4 knowledge articles accepted · audit clean · all four task reports have
"Decisions made" sections · working tree clean of Phase-1 changes (owner's local files untouched) ·
A1 worktree removed after merge.

**Handoff to Phase 2 (owner):** supervised first launch per gate B1–B4. Reference material: manual
backup at `bin/Debug/net10.0-windows10.0.19041.0/manual-backup-pre-epic1/SmartStudyData.db`;
post-upgrade reference counts **HocKys 1 · MonHocs 3 · StudyTasks 11 · StudyLogs 5,402 · TaskNotes
0 · TaskReferenceLinks 0** + `Rev` column present; GUI smoke list in verdict §C3 +
`docs/ux_quality_gate_checklist.md` §Regression.

## Verification

- **A1:** RED-first discriminating test evidence in the report; `rtk dotnet build` 0 errors;
  `rtk dotnet test --no-build` full suite green; PM re-reads the diff (`/code-review`).
- **A2/A3/A4:** grep sweeps + link checks as specified; PM read-through of every touched doc against
  the acceptance tables above.
- **Plan-level:** Phase 1 exit review by PM; Phase 2 outcomes then feed the C1 closing note
  (Phase 3 — out of scope here).
