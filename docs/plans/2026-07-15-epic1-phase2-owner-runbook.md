# Epic 1 — Phase 2 Owner Runbook (B1–B3 + record sheet for B4)

> **Who runs this:** the project owner, manually — the gate doc
> ([`2026-07-11-epic-1-closure-gate.md`](2026-07-11-epic-1-closure-gate.md) §Phase 2) requires
> these steps NOT be delegated to AI. **Prepared by QA 2026-07-15** after the Phase 1 exit review
> passed (A1–A4 done; fresh build 0 errors, 331/331 tests green at `4e4e687`).
>
> **Standing rule lifted:** A1 (WAL-safe backup) is merged (`8740350`) — launching the app is now
> permitted. Do the steps **in order**, tick as you go, and fill every `___` blank. If any step
> shows ❌ **STOP** and jump to §Rollback.

**Pre-verified state (QA, 2026-07-15, read-only):** the live dev DB
`SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db` is intact and
untouched since 2026-07-11 19:25 (+07): integrity ok, pre-upgrade schema (no `Rev`), row counts
exactly **HocKys 1 · MonHocs 3 · StudyTasks 11 · StudyLogs 5,402 · TaskNotes 0 ·
TaskReferenceLinks 0**, subjects `Lập Trình Nâng Cao` / `Toán Rời Rạc` / `A` with no duplicates.
The manual safety copy in `manual-backup-pre-epic1\` verifies identically.

*Note:* running the verify script (or the app) may leave empty `SmartStudyData.db-wal` /
`-shm` sidecar files next to the DB — that is normal SQLite WAL behavior, not corruption.

---

## Step 0 — Pre-flight (5 min)

| # | Do | Expected |
|---|---|---|
| 0.1 | `rtk git status` — confirm branch `ui_rf`, no unexpected changes beyond your local `.claude/*`, `AGENTS.md`, `CLAUDE.md`, `Assets/`, and the two new QA files (`docs/plans/…runbook.md`, `tools/epic1_b2_verify.py`) | Matches |
| 0.2 | **Recommended:** `rtk git push` — `ui_rf` is 39 commits ahead of origin; all Epic 1 gate work exists only on this machine, and the next step is the riskiest of the epic | Pushed |
| 0.3 | `rtk dotnet build SmartStudyPlanner.slnx` | 0 errors (NU1903/NU1904 warnings are known + tracked) |
| 0.4 | Confirm the safety copy exists: `SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\manual-backup-pre-epic1\SmartStudyData.db` (1,310,720 bytes) | Present |

---

## B1 — Supervised first launch (the first real in-place upgrade)

This launch fires `AppStartup.EnsureDatabaseReady`: it detects the missing `Rev` column, creates a
timestamped backup (**now WAL-safe** per A1), then patches the 5 sync columns onto all 6 tables
and backfills stamps on all existing rows (incl. the 5,402 StudyLogs).

| # | Do | Expected |
|---|---|---|
| B1.1 | Open the output folder `SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\` in Explorer (keep it visible) | — |
| B1.2 | Launch **`SmartStudyPlanner.exe`** from that folder | App starts with **no error dialog**; main window renders; migration is fast (≤ a few seconds — likely not visibly noticeable) |
| B1.3 | Watch the folder during/after launch | Exactly **one new file** `SmartStudyData.<yyyyMMdd-HHmmss>.bak.db` appears, ~1.3 MB. The timestamp is **UTC** (7 h behind your local +07 clock) — that is correct, not a bug |
| B1.4 | Glance at the app: Dashboard loads, semester context label shows your semester (not an empty/blank state) | Data visible — subjects and tasks present |
| B1.5 | **Close the app** (needed so B2 reads a settled file) | — |

**Record:** backup filename `___________________________` · size `________` bytes ·
launch clean? `☐ yes ☐ no` · anything unusual: `___________________________`

❌ If the app fails to start, shows an error dialog, or opens with empty data → **STOP → §Rollback.**

---

## B2 — Database verification (app closed)

Run the QA-prepared read-only script from the repo root (`D:\Code\C#\SmartStudyPlanner`):

```powershell
python tools\epic1_b2_verify.py
python tools\epic1_b2_verify.py --backup "SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.<yyyyMMdd-HHmmss>.bak.db"
```

(second command: use the actual B1.3 filename)

| Check | Expected after first launch |
|---|---|
| Live DB — integrity | PASS `ok` |
| Live DB — sync columns | **all 6 tables PASS** (these were the 6 FAILs pre-launch — the flip is the migration evidence) |
| Live DB — row counts | HocKys **1** · MonHocs **3** · StudyTasks **11** · StudyLogs **5402** · TaskNotes **0** · TaskReferenceLinks **0**, tombstoned **0** everywhere |
| Live DB — backfill | PASS: 0 NULL `ModifiedAtUtc` / `ModifiedByDeviceId` on every table |
| Live DB — MonHoc identity | PASS, subjects `Lập Trình Nâng Cao` / `Toán Rời Rạc` / `A` |
| Live DB — newest StudyLog | `DeviceId=''` is **expected** here (old pre-Epic-1 row; the A6 fix stamps *new* rows — proven in B3.6) |
| Live DB — verdict line | **ALL CHECKS PASSED** |
| `--backup` run | **ALL CHECKS PASSED**: integrity ok, **no `Rev` column** (proves it snapshotted *before* the upgrade), counts = baseline |

**Record:** paste both full script outputs into your notes / reply.

❌ Any FAIL, any count mismatch → **STOP → §Rollback** and send QA the output.

---

## B3 — GUI smoke test (Epic-1-targeted only — no exploratory testing)

Relaunch `SmartStudyPlanner.exe`.
**First expected outcome: NO second `.bak` file appears** (the upgrade already ran; backup only
fires when an upgrade is needed).

Do the scenarios in this order — destructive step last:

| # | Scenario | Do | Expected |
|---|---|---|---|
| B3.1 | Startup + theme | Click **Giao Diện** (theme toggle), toggle back | Both themes render; no layout/contrast breakage |
| B3.2 | Duplicate subject warning (M1.3 prevent-at-source) | Sidebar → **Môn Học & Bài Tập** → add a subject named exactly `Toán Rời Rạc` | Warning **"Môn 'Toán Rời Rạc' đã tồn tại."** — subject NOT added (list stays 3) |
| B3.3 | Duplicate variant (normalized identity) | Same, but type `  toán rời rạc ` (different case + extra spaces) | Same warning — normalization catches it; list stays 3 |
| B3.4 | Semester save repeatedly (M1.3 `LuuHocKyAsync` fix) | On the same page, save the semester (**Lưu học kỳ**) **twice in a row** | Both saves succeed; no error dialog; no duplicate semester appears |
| B3.5 | Analytics dropdown + filter | Sidebar → **Analytics**; open the subject dropdown; then change subject + time-range filters | Dropdown lists each subject **exactly once** (3 entries); charts + heatmap re-render with real data; an empty filter result shows empty-state, no crash/binding break |
| B3.6 | Focus session completion (A6 fix) | Dashboard → start a Focus session on any task → complete it | Completion feedback appears; no error. (Proof lands in B3.8: StudyLogs 5402 → **5403**, new row carries a real DeviceId) |
| B3.7 | Delete task → restart (tombstones) | In **Môn Học & Bài Tập**, delete **one** task you can spare — note its name → confirm it disappears → **close and relaunch** the app | Task stays gone after restart; again NO new `.bak` file |
| B3.8 | Post-smoke DB check | Close the app; rerun `python tools\epic1_b2_verify.py` | **ALL CHECKS PASSED** with exactly these deltas: StudyLogs **5403** · StudyTasks still **11 physical** but **tombstoned: 1** (soft delete — the row is kept, flagged) · newest StudyLog `DeviceId='<real-guid>'` (non-empty!) · MonHocs still 3 · HocKys still 1 |

**Record:** per-scenario ☐ PASS / ☐ FAIL + notes · deleted task name `___________________` ·
post-smoke script output pasted · screenshots of anything anomalous.

❌ B3.8 showing StudyTasks < 11 physical rows (hard delete!) or DeviceId still `''` on the new
log is an **Epic 1 acceptance failure** → record it; that is a "Reopen Epic 1" signal for B4.

---

## Rollback (only if something went ❌)

1. Close the app.
2. In the output folder: delete `SmartStudyData.db`, `SmartStudyData.db-wal`, `SmartStudyData.db-shm`.
3. Copy a known-good snapshot back as `SmartStudyData.db` — preferred: the B1 auto-backup
   `SmartStudyData.<ts>.bak.db`; equivalent: `manual-backup-pre-epic1\SmartStudyData.db`.
4. Do **not** relaunch the app (it would re-fire the upgrade). Send QA the failing step + outputs.

---

## B4 — Release decision (yours) & what QA needs back

Everything below feeds the Phase 3 closing note (C1) — reply with:

1. **The completed record sheet** — B1 blanks, both B2 outputs, B3 per-scenario PASS/FAIL, the
   post-smoke (B3.8) output, screenshots of anomalies if any.
2. **Your B4 decision:** ✅ *Epic 1 Released* or ❌ *Reopen Epic 1* — with reasons if reopening.
   An explicit sign-off sentence is required by the gate's success criteria.
3. **F2 waiver confirmation** for the closing note: the 3 telemetry-class fire-and-forget writes
   (`OutcomeMaturationService.MatureAsync`, `LogDifficultyLabelAsync`, `LogWeightChangeAsync`)
   stay as designed (loss-tolerant telemetry), metric "0 fire-and-forget writes" recorded as met
   for synced entities + waived for telemetry. Confirm or veto.
4. **Audit finding #6:** the `PROPOSED` "UI fidelity + mobile-ready polish" row in
   `docs/active/README.md` — keep as proposed / schedule / drop?
5. **Push status:** confirm `ui_rf` is pushed (Step 0.2), or authorize the push.
6. **Next epic for Phase 3 / C3 prep:** the frozen master plan orders **E1 → E3 (SOE) → E2 → E4**,
   but gate **G2 (SOE pass accept/commit semantics) is still OPEN** and blocks M3.2 — decide:
   close G2 first, start with the parallel-safe M3.0 (corpus/baselines), or override the order.
   (C3 stops before any code regardless — implementation needs your separate approval.)

Minor cleanups QA will fold into Phase 3 (no action needed from you unless you object): stale
"A4 pending" wording in `docs/active/README.md:14`; removing the leftover June `ssp-merge`
worktree; committing this runbook + `tools/epic1_b2_verify.py`.
