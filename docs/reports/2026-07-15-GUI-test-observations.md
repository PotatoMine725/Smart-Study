# Owner's checklist (based on the runbook)

**Date** 15/07/2026
**reference file** : docs/plans/2026-07-15-epic1-phase2-owner-runbook.md

---

## Step-0 Pre-flight results

| # | Do | Expected | Result |
|---|---|---|---|
| 0.1 | `rtk git status` — confirm branch `ui_rf`, no unexpected changes beyond your local `.claude/*`, `AGENTS.md`, `CLAUDE.md`, `Assets/`, and the two new QA files (`docs/plans/…runbook.md`, `tools/epic1_b2_verify.py`) | Matches | Verified - True |
| 0.2 | **Recommended:** `rtk git push` — `ui_rf` is 39 commits ahead of origin; all Epic 1 gate work exists only on this machine, and the next step is the riskiest of the epic | Pushed | Not yet - will be delivered to another agent in charge of epic 1 for clean pushing |
| 0.3 | `rtk dotnet build SmartStudyPlanner.slnx` | 0 errors (NU1903/NU1904 warnings are known + tracked) | Verified -True |
| 0.4 | Confirm the safety copy exists: `SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\manual-backup-pre-epic1\SmartStudyData.db` (1,310,720 bytes) | Present | Verified - True

--> Conclusion : Step 0 passed.

---

## B1 — Supervised first launch (the first real in-place upgrade)

Steps form B1.1 to B1.3 works as expected.

**important**

HOWEVER, in B1.4, the app becomes unresponsive (Not responding) and crash shortly after (~30 seconds) whenever i use smart add feature to create a task. Second, heuristic rule for smart add has a critical logic hole in processing input.
Explanation: in Vietnamese, using the word "Không" or "chẳng" or  "Không hề / chẳng hề" and slangs like "đéo" may turn the meaning of a sentence; for example "btvn ngày mai không dễ đâu" means "tomorrow's homework is not easy"; in this case, the app does not understand.

In work balancer page, there is no arrangements of tasks displayed despite there are tasks and subjects in both dashboard and " Môn học và bài tập". Currently there are 3 subjects appear, among them, "A" is created by me, the others were created by data seed to train ML.

In "trọng số AI" page, the UI is unfamiliar to me, i remember it was a page like others, not a small window

final verdict for this step: This needs deeper inspections before coming into any conclusions.  

---

## B2 — Database verification (app closed)

all checks in the two commands passed, here's the result of the second py command: 

```powershell
== Backup snapshot verification: SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.20260715-112034.bak.db

  [PASS] PRAGMA integrity_check == ok — ok
  [PASS] HocKys has NO Rev column (pre-upgrade snapshot)

Row counts vs pre-upgrade baseline:
  [PASS] HocKys: 1 == 1
  [PASS] MonHocs: 3 == 3
  [PASS] StudyTasks: 11 == 11
  [PASS] StudyLogs: 5402 == 5402
  [PASS] TaskNotes: 0 == 0
  [PASS] TaskReferenceLinks: 0 == 0

ALL CHECKS PASSED
```

## B3 — GUI smoke test (Epic-1-targeted only — no exploratory testing)

no .bak.db file was modified/created after the second launch.

| # | Scenario | Do | Expected | Result |
|---|---|---|---|---|
| B3.1 | Startup + theme | Click **Giao Diện** (theme toggle), toggle back | Both themes render; no layout/contrast breakage | Pass|
| B3.2 | Duplicate subject warning (M1.3 prevent-at-source) | Sidebar → **Môn Học & Bài Tập** → add a subject named exactly `Toán Rời Rạc` | Warning **"Môn 'Toán Rời Rạc' đã tồn tại."** — subject NOT added (list stays 3) | Failed - mentioned in B1|
| B3.3 | Duplicate variant (normalized identity) | Same, but type `  toán rời rạc ` (different case + extra spaces) | Same warning — normalization catches it; list stays 3 | Pass
| B3.4 | Semester save repeatedly (M1.3 `LuuHocKyAsync` fix) | On the same page, save the semester (**Lưu học kỳ**) **twice in a row** | Both saves succeed; no error dialog; no duplicate semester appears | Unknown - no "Lưu học kỳ" button in any pages/UI, there's only "Lưu tiến trình" button but it's not responsive so i cannot know whether it is working or not |
| B3.5 | Analytics dropdown + filter | Sidebar → **Analytics**; open the subject dropdown; then change subject + time-range filters | Dropdown lists each subject **exactly once** (3 entries); charts + heatmap re-render with real data; an empty filter result shows empty-state, no crash/binding break | Partially pass - heat map does not rerender when changing options in drop down |
| B3.6 | Focus session completion (A6 fix) | Dashboard → start a Focus session on any task → complete it | Completion feedback appears; no error. (Proof lands in B3.8: StudyLogs 5402 → **5403**, new row carries a real DeviceId) | 
| B3.7 | Delete task → restart (tombstones) | In **Môn Học & Bài Tập**, delete **one** task you can spare — note its name → confirm it disappears → **close and relaunch** the app | Task stays gone after restart; again NO new `.bak` file | Unknown - all expectation met excecpt the .bak file, the timestamp in the file's name is correct, but there are TWO .bak, one is WAL, another is SHM | 
| B3.8 | Post-smoke DB check | Close the app; rerun `python tools\epic1_b2_verify.py` | **ALL CHECKS PASSED** with exactly these deltas: StudyLogs **5403** · StudyTasks still **11 physical** but **tombstoned: 1** (soft delete — the row is kept, flagged) · newest StudyLog `DeviceId='<real-guid>'` (non-empty!) · MonHocs still 3 · HocKys still 1 | Unknown - will add details in conclusion |

---

## B4 — Release decision (mine) & what QA needs back
- Re-open Epic 1 
- i need to be explained carefully to understand before making any decisions about **F2 waiver confirmation**
- the `PROPOSED` "UI fidelity + mobile-ready polish" row in `docs/active/README.md` — keep as proposed
- **Next epic for Phase 3 / C3 prep:** : your recommendation 


---
# Final conclusion and recommendations

**attention** all runs is done by running the exe file in D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0, no other entry was touched, so i don't know what backup you mean.

- prioritize fixing mentioned problems in B1 and B3
- add a "Học ngay" button for all tasks, not just the top 5 deadlines
- add an "Toàn bộ task" page, grouped by subjects (add some space between each subjects or color them differently), ordered by deadline, allow to filter by time range (3/7/10/30/all) to prevent UI bloat
- Make color theme consistent every time the user open the app (if dark mode is chosen, the next open should also be dark mode)
- B3.8 results (**Important**: I created 2 more subjects namely "A1" and "B" with no task due to task create failure, then i clicked "xong" with a task in subject "A", deleted "Task lập trình nâng cao K3" in "Lập trình nâng cao"): 

```powershell
PS D:\Code\C#\SmartStudyPlanner> python tools\epic1_b2_verify.py

== Live DB verification: D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db

Integrity:
  [PASS] PRAGMA integrity_check == ok — ok

Sync-metadata columns (D-I):
  [PASS] HocKys has all 5 sync columns
  [PASS] MonHocs has all 5 sync columns
  [PASS] StudyTasks has all 5 sync columns
  [PASS] StudyLogs has all 5 sync columns
  [PASS] TaskNotes has all 5 sync columns
  [PASS] TaskReferenceLinks has all 5 sync columns

Row counts (physical, incl. tombstones):
  HocKys: 1 rows (tombstoned: 0) — baseline was 1
  MonHocs: 5 rows (tombstoned: 0) — baseline was 3
  StudyTasks: 11 rows (tombstoned: 1) — baseline was 11
  StudyLogs: 5403 rows (tombstoned: 0) — baseline was 5402
  TaskNotes: 0 rows (tombstoned: 0) — baseline was 0
  TaskReferenceLinks: 0 rows (tombstoned: 0) — baseline was 0

Backfill (no NULL stamps allowed):
  [PASS] HocKys: 0 NULL ModifiedAtUtc / ModifiedByDeviceId — ModifiedAtUtc NULL: 0, ModifiedByDeviceId NULL: 0
  [PASS] MonHocs: 0 NULL ModifiedAtUtc / ModifiedByDeviceId — ModifiedAtUtc NULL: 0, ModifiedByDeviceId NULL: 0
  [PASS] StudyTasks: 0 NULL ModifiedAtUtc / ModifiedByDeviceId — ModifiedAtUtc NULL: 0, ModifiedByDeviceId NULL: 0
  [PASS] StudyLogs: 0 NULL ModifiedAtUtc / ModifiedByDeviceId — ModifiedAtUtc NULL: 0, ModifiedByDeviceId NULL: 0
  [PASS] TaskNotes: 0 NULL ModifiedAtUtc / ModifiedByDeviceId — ModifiedAtUtc NULL: 0, ModifiedByDeviceId NULL: 0
  [PASS] TaskReferenceLinks: 0 NULL ModifiedAtUtc / ModifiedByDeviceId — ModifiedAtUtc NULL: 0, ModifiedByDeviceId NULL: 0

MonHoc identity (no normalized duplicates):
  [PASS] no two live MonHocs share a normalized name — dups: []
  live subjects: ['Lập Trình Nâng Cao', 'Toán Rời Rạc', 'A', 'A1', 'B']

Newest StudyLog (A6 evidence — DeviceId stamped):
  DeviceId='desktop-49b42d8f', Rev=1, ModifiedAtUtc='2026-07-15 11:36:31.4822114', ModifiedByDeviceId='desktop-49b42d8f'

ALL CHECKS PASSED
```

```powershell
PS D:\Code\C#\SmartStudyPlanner> python tools\epic1_b2_verify.py --backup "SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.20260715-112034.bak.db"

== Backup snapshot verification: SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.20260715-112034.bak.db

  [PASS] PRAGMA integrity_check == ok — ok
  [PASS] HocKys has NO Rev column (pre-upgrade snapshot)

Row counts vs pre-upgrade baseline:
  [PASS] HocKys: 1 == 1
  [PASS] MonHocs: 3 == 3
  [PASS] StudyTasks: 11 == 11
  [PASS] StudyLogs: 5402 == 5402
  [PASS] TaskNotes: 0 == 0
  [PASS] TaskReferenceLinks: 0 == 0

ALL CHECKS PASSED
```