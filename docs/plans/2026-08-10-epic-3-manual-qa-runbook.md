# Epic 3 (SOE) — Owner-led Manual QA Runbook

**Date written:** 2026-08-10 · **Branch:** `dev` · **Status of this document: NOT YET EXECUTED.**
Every result cell below is deliberately blank. Nothing in this runbook has been performed — it
describes tests the owner is being asked to run, and no observation in it may be filled in by
anyone who did not actually sit in front of the running application.

**Precondition for using this runbook:** the automated QA gate is satisfied — see
[`docs/reports/2026-08-10-epic3-automated-qa-gate.md`](../reports/2026-08-10-epic3-automated-qa-gate.md)
for what was verified automatically, what could not be, and why each scenario below exists.

**Reads with:** [Epic 3 closing note](../reports/2026-08-07-epic3-closing-note.md) (what shipped and
its ratified limitations) · [master plan](2026-07-03-master-plan.md) §"Epic 3".

---

## 1. Purpose and scope

Epic 3 shipped a large amount of code behind strategy interfaces. **Only two of its changes can
affect a running application**, and this runbook tests exactly those two:

| # | Change | Where the owner can see it |
|---|---|---|
| 1 | **Allocator placement rework (T3.3)** — day selection changed from *least-loaded* to *earliest-feasible* | Workload Balancer screen ("CÂN BẰNG TẢI") |
| 2 | **`OptimizerRunLogs` telemetry table (T3.7)** — created at every startup | Nothing visible; DB file only |

### 1.1 Explicitly NOT in scope — read before logging any defect

Three things will look wrong to a careful tester and are **not** defects. They are ratified
decisions with written owner rulings. Please do not log them as bugs.

1. **The SOE optimizer is not reachable from the GUI in this release.**
   `ScheduleOptimizer`, `LoadRebalanceStage`, `ConstraintValidator`, `ObjectiveEvaluator`,
   `SoeWeights` and `OptimizerRunLogWriter` have **zero production call sites** — verified by source
   search during the automated gate. There is no screen, button or menu that runs `Optimize()`.
   Wiring it up is separate, unscheduled integration work (G3-1). **There is nothing to manually
   test in the optimizer itself.**

2. **The `OptimizerRunLogs` table will exist but stay permanently empty.**
   The table is created on every launch; the only thing that writes to it (`OptimizerRunLogWriter`)
   has no production caller, per point 1. An empty table is the **expected** outcome of Scenario B2,
   not a failure.

3. **A task scheduled onto a day after its own deadline is a known, ratified limitation.**
   Root cause **A1**: priority (`DiemUuTien`) is the sole task-ordering key; the deadline only
   selects which day inside a window a chunk lands on. The owner ratified this as a disclosed,
   characterization-tested allocator limitation — **not a defect** — on 2026-08-07 (Decision D7 in
   the G2 note). Epic 3 reduced these inversions from 250 to 220 (12%) and eliminated the
   "self-miss" class entirely (17 → 0); it did not eliminate the remaining pairwise class.
   See Scenario D3, which asks you to *observe* this rather than treat it as a fault.

---

## 2. Preconditions and test data

### 2.1 Build

```bash
cd "D:/Code/C#/SmartStudyPlanner"
dotnet build SmartStudyPlanner.slnx --configuration Release
dotnet run --project SmartStudyPlanner/SmartStudyPlanner.csproj --configuration Release
```

### 2.2 The database file — know which one you are testing

The app stores its SQLite DB **next to the executable**, not in your user profile. That means there
is one DB *per build configuration*, and they are unrelated files:

```
SmartStudyPlanner/bin/Debug/net10.0-windows10.0.19041.0/SmartStudyData.db     ← long-lived data
SmartStudyPlanner/bin/Release/net10.0-windows10.0.19041.0/SmartStudyData.db   ← often empty
```

The Release DB is **not** your accumulated data — day-to-day use runs under Debug. Expect the
Release file to be empty or near-empty on a machine that has only ever run Debug builds, and do not
read that as data loss.

**Before starting, copy both files somewhere safe.** Scenario A1 exercises the schema-upgrade path
against a real DB, and you want a restore point independent of the app's own `.bak.db` mechanism —
that one only writes when the Epic 1 column upgrade actually runs, which on an
already-upgraded DB is never (see A3).

**Close the app before copying either file.** A running app can leave a `-wal` sidecar holding
committed data; copying the `.db` alone while that exists silently loses it.

### 2.3 Test data to prepare

Create (or reuse) one semester with **one subject** and the following tasks. The exact minute
figures the app derives are computed from priority and difficulty, so the point of this data is the
*shape*, not exact numbers: enough work to overflow several days at a low capacity.

| Task | Deadline | Difficulty | Purpose |
|---|---|---|---|
| T-A | today + 5 days | 4–5 (high) | bulk work, forces multi-day spill |
| T-B | today + 5 days | 3 | fills gaps |
| T-C | today + 5 days | 4 | more bulk |
| T-D | today + 1 day | 2 (low) | low priority + near deadline → Scenario D3 |
| T-E | **yesterday** (already overdue) | 3 | past-deadline behaviour |
| T-F | today + 6 days | 2 | small tail task |
| T-G (completed) | any | any | mark **Hoàn thành** — must never be scheduled |

Set capacity to **1 giờ/ngày** for the placement scenarios; that makes day-packing visible without
needing many tasks.

---

## 3. Ordered test scenarios

Run in order. A–B establish the app boots and migrates; C–D are the behaviour under test; E is
regression.

### Group A — Startup and schema migration

#### A1 — Existing (pre-Epic-3) database upgrades cleanly

**Setup:** A1 needs a **real, long-lived, pre-Epic-3** DB — one with actual content, created by an
older version. An empty DB will pass this scenario without testing anything, because "all
pre-existing data survived" cannot fail when there is no pre-existing data. Since the long-lived DB
lives under `bin/Debug` (§2.2), stage it explicitly:

1. Close the app.
2. If the current Release DB holds your Group C/D test data, rename it aside (e.g.
   `soe-testdata.db`) so you can swap it back afterwards.
3. **Copy** — never move — `bin/Debug/.../SmartStudyData.db` to
   `bin/Release/.../SmartStudyData.db`. That Debug file plus your §2.2 backup are the only two
   copies of your real study history.

A fixture that already carries the Epic 1 sync columns is still a valid A1 subject, and is in fact
the sharper test: `SyncSchema.NeedsUpgrade` returns false, so no `.bak.db` is written and the *only*
migration that fires is the new `CREATE TABLE IF NOT EXISTS OptimizerRunLogs` — the exact step this
scenario exists to check.

**Steps:** launch the app; let it reach the main window; close it.

**Expected:**
- App starts with no error dialog, no crash, no hang.
- All pre-existing semesters, subjects and tasks are still present and unchanged.
- Study logs / streak / focus history intact.

**What to observe:** specifically that *old data survived*. The startup path now runs one extra
`CREATE TABLE IF NOT EXISTS` before the Epic 1 column patch; the automated gate proved the table
gets created and that the sequence does not throw, but only you can confirm a **real** long-lived
database still opens with its content intact.

**Evidence:** screenshot of the semester list before (from your backup copy, if available) and
after.

#### A2 — Fresh database, first launch

**Setup:** move `SmartStudyData.db` aside so the app creates a new one.

**Expected:** app creates a fresh DB, starts to a usable empty state, no error.

#### A3 — Second launch is idempotent

**Steps:** close and relaunch twice more.

**Expected:** no error; no growing pile of `SmartStudyData.*.bak.db` files appearing on every
launch (a backup is only taken when an upgrade actually runs).

---

### Group B — Telemetry table (invisible change)

#### B1 — `OptimizerRunLogs` table exists after launch

**Steps:** after A1, open `SmartStudyData.db` in any SQLite browser (e.g. DB Browser for SQLite).

**Expected:** a table named `OptimizerRunLogs` exists, with columns `Id, RunId, CreatedUtc,
Termination, PassCount, PassIndex, KStar, CheckpointIndex, ViolationCount, OverdueMinutes, Score,
Reason`.

#### B2 — `OptimizerRunLogs` is empty, and stays empty

**Steps:** use the app normally for a few minutes — open the Workload Balancer, press "XẾP LỊCH
LẠI" several times, visit the Dashboard. Re-check the table.

**Expected:** **0 rows.** This is correct (see §1.1 point 2). Rows appearing here would be the
surprise, not their absence.

**Evidence:** screenshot of the row count.

---

### Group C — Workload Balancer placement (the headline behaviour change)

Navigate to **"CÂN BẰNG TẢI"** (Workload Balancer). Set the capacity slider to **1 giờ/ngày**.

#### C1 — Work packs into the earliest days, densely and contiguously

**Expected (this is the T3.3 change):**
- Day cards with work start at **Hôm nay** and run **consecutively** — no empty day sits between
  two days that have work.
- Every day that has work, **except the last one**, is filled to the capacity ceiling (60 phút at
  1 giờ/ngày).
- Only the final used day is partially filled.

**What the OLD behaviour looked like (for contrast):** work spread thinly and evenly across all
seven days, with many days part-filled. If you see that, the placement rework has regressed.

**Evidence:** screenshot of the "PHÂN BỔ TẢI THEO NGÀY" bar section plus the detailed day list.

#### C2 — Capacity slider changes density, not the packing rule

> ⚠ **The 2026-08-10 result for this scenario is withdrawn and must be re-run.** It was recorded
> before the stale-chart defect was understood, so it may have been read off a chart that was never
> rebuilt. See `docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` §2.4 and D4.
>
> *Re-run by the owner on 2026-08-19 with the corrected procedure. The result is recorded in the
> owner's own words at `docs/reports/2026-08-19-epic3-manual-observation-updated.md` and is not
> transcribed here.*

**Steps:** move the slider through **1 → 3 → 8 giờ/ngày**. **Press "XẾP LỊCH LẠI" after every
slider change, before reading the chart.**

Moving the slider alone does *not* rebuild the schedule — it only rescales the drawing. Until you
press the button you are looking at the previous allocation measured against the new ceiling, and a
stale reading will *systematically* look like a C1-shape violation. A warning badge above the chart
tells you when this is the case; if you can see the badge, the chart is not answering this scenario.

**Expected:** at every setting, the C1 shape still holds (contiguous prefix; all but the last used
day at the ceiling). Higher capacity ⇒ fewer, fatter days. No day ever exceeds the ceiling shown
next to the slider.

#### C3 — Slider bounds

**Expected:** slider will not go below **1** or above **8**. The app does not hang or freeze at
either extreme. (A hang at the low end would be the specific failure this bound exists to prevent.)

#### C4 — Task ordering is still by priority

**Expected:** within the schedule, higher-priority tasks are placed before lower-priority ones. The
section is labelled "sắp theo Điểm ưu tiên" and that is still accurate — Epic 3 changed *which day*
a chunk lands on, never the ordering key.

#### C5 — Split tasks are labelled

**Expected:** a task too large for one day appears as "«Tên task» (Phần 1)", "(Phần 2)", … across
consecutive days. Part numbers ascend and no part is lost.

#### C6 — Completed tasks never appear

**Expected:** **T-G** (marked Hoàn thành) appears nowhere in the schedule.

#### C7 — ⚠ Header text vs. observed behaviour — **known finding, please rule on it**

**Steps:** read the sentence under the page title, then look at your C1 screenshot.

The header currently reads:

> "Thuật toán **rải các bài tập chưa hoàn thành đều khắp** những ngày tới — theo điểm ưu tiên và
> sức học mỗi ngày của bạn."

**Expected observation:** the screen shows work **packed densely into the earliest days**, which is
not "rải đều khắp những ngày tới". The sentence describes the *pre-Epic-3* least-loaded rule.

**This is finding QA-1** (MEDIUM, deferred to you — see the QA report §4). It was deliberately
**not** changed during the automated gate: it is user-facing Vietnamese product copy, the feature is
still named "CÂN BẰNG TẢI", and picking accurate replacement wording is a product decision, not a
mechanical correction. **Your task here is to decide the wording**, not to confirm a bug.

A proposed replacement, offered only as a starting point:

> "Thuật toán xếp các bài tập chưa hoàn thành vào những ngày sớm nhất còn chỗ — theo điểm ưu tiên và
> không vượt quá sức học mỗi ngày của bạn."

**Record your decision in the observation table.**

*Ruled 2026-08-10: keep the algorithm, fix the copy. The header goes claim-neutral and the bottom
information note becomes the single place that explains the mechanism. Implemented — C8–C10 below
cover the behaviour that made the old copy misleading.*

---

#### C8 — Moving the slider does not move the chart

**Steps:** open the page and note the height of each bar. Drag the slider to a different tick.
**Do not press the button.**

**Expected:** the 38pt readout and the slider move. **No chart bar moves, and no bar changes
colour.** A badge appears above the chart naming the capacity the schedule was actually built with
— the *old* value, not the one the slider now shows.

**Fails if:** any bar changes height or colour, or the "ĐÃ ĐẠT MỨC TỐI ĐA" labels appear or vanish.
That is the original defect: the chart redrawing the old allocation against a new ceiling.

#### C9 — Pressing the button clears the badge and rescales

**Steps:** from C8's end state, press **XẾP LỊCH LẠI**.

**Expected:** the confirmation dialog names the **new** capacity. The badge disappears. The chart
rescales *and* re-allocates — bars move because the schedule was rebuilt, not merely redrawn.

#### C10 — An out-of-range `capacity.txt` does not raise a false badge

**Steps:** close the app. Open `capacity.txt` in the build output directory
(`bin\Release\net10.0-windows10.0.19041.0\`) and set it to `12`. Relaunch and open the page.
Then repeat the whole thing with **`4.5`**.

**Expected, `12`:** the readout shows **8.0** and **no badge is visible** on a page you have not
touched.

**Expected, `4.5`:** the readout shows **4.5** and **no badge is visible**. If it reads **5.0** and
a badge appears, record it as a FAIL — do not dismiss it as rounding.

**Why this exists:** the badge must mean exactly one thing — *the chart is stale* — and a warning
that also fires for unrelated reasons stops being read. Two ways it could fire on an untouched page:

- **above the ceiling (`12`)**: `GetCapacity` now clamps the file to the slider's own bounds, so the
  constructor and the slider agree. Without that clamp the schedule would be built at 12 while the
  slider coerced its value to 8 and wrote back.
- **between ticks (`4.5`)**: an in-range value the app itself can write (`SaveCapacity` produces
  `4.5`, and `GetCapacity` is tested to read it back). The slider has
  `TickFrequency="1" IsSnapToTickEnabled="True"`. If it snaps `4.5` to `5.0` and writes back, the
  chart says 4.5 and the slider says 5.0 — false badge. **This path is untested and unproven;
  this scenario is what settles it.**

---

### Group D — Edge cases

#### D1 — No tasks at all

**Setup:** a semester with no incomplete tasks.

**Expected:** the Workload Balancer opens without error and shows an empty schedule area. No crash,
no empty-collection exception.

#### D2 — Tasks that have never been scored elsewhere

**Setup:** create a brand-new task and go **straight** to the Workload Balancer without visiting the
Dashboard or the task-management screen first (i.e. without anything having calculated its priority).

**Expected:** the new task **is scheduled** and appears in the day cards.

**Why this scenario exists:** the allocator writes the priority score onto the task model before
computing minutes, and the minutes calculator returns 0 for any task whose score is ≤ 0. If that
write-through were ever removed, unscored tasks would **silently vanish** from the schedule — no
error, just an emptier screen. The automated gate now covers this at two levels, but this is the
GUI-level confirmation that the paths line up in the real app.

#### D3 — A task placed after its own deadline (**expected, not a bug**)

**Setup:** **T-D** (low priority, deadline tomorrow) together with the high-priority bulk tasks, at
1 giờ/ngày so the early days fill up.

**Expected:** T-D may well be placed on a day **after** its deadline, because the high-priority
tasks consumed the early days first.

**This is ratified limitation A1 (Decision D7, 2026-08-07). Do not log it as a defect.** Note it in
the observation table so we have a real-world instance on record, and move on.

#### D4 — Already-overdue task

**Setup:** **T-E**, deadline yesterday.

**Expected:** it is still scheduled (the allocator never refuses to place work — rejecting
infeasible schedules is the unwired `ConstraintValidator`'s job, which does not run in this
release). No crash, no negative or nonsensical duration.

#### D5 — Very large workload

**Setup:** enough tasks that the total exceeds 7 days at 1 giờ/ngày.

**Expected:** the schedule **grows past 7 days**, and the added days remain consecutive — no gap,
no duplicated date, no day out of order.

---

### Group E — Regression outside SOE

Epic 3 touched a shared allocator and the DB bootstrap, so these screens are checked for collateral
damage.

#### E1 — Dashboard

**Expected:** opens, renders, shows the same summary numbers you would expect from your data. Charts
render without clipping. No error dialog.

#### E2 — Analytics screens

**Expected:** open and render. (Not an Epic 3 area, but they consume schedule-adjacent data and the
Epic 1 reopen fixed a stale-render defect here — worth a glance.)

#### E3 — Task and subject management (CRUD)

**Expected:** create / edit / delete a task and a subject; changes persist across an app restart.

#### E4 — Focus mode / study logging / streak

**Expected:** start and finish a focus session; the log and streak update as before.

#### E5 — Capacity persists across restart

**Steps:** drag the slider to 5 giờ/ngày, **press "XẾP LỊCH LẠI"**, close the app, reopen the
Workload Balancer.

**Expected:** the slider still reads 5.0.

**A drag alone does not persist, and that is by design.** `SaveCapacity` runs only inside
`BuildSchedule`, so nothing reaches disk until you confirm — the same reason the chart does not
rebuild on a drag. Putting a disk write and the CP-2 `DiemUuTien` database write-through behind a
drag gesture was considered and rejected (design D1). If you drag to 5, do *not* press the button,
restart, and the slider reads the old value, that is **not** a failure — it is the badge's whole
point that the unconfirmed value is visibly unapplied.

**Fails if:** you press the button and the value still does not survive a restart.

#### E6 — Deleting a subject that has tasks

> **This scenario was rewritten on 2026-08-14.** It previously asked you to delete a *semester*.
> That is not a defect in the app — **no such capability exists**: there is no `XoaHocKy`,
> `DeleteHocKy`, or `HocKys.Remove` anywhere in production code. The scenario was written against a
> capability that was never built, so it could not be executed. See "Known gaps" below.

**Steps:** in Quản lý môn học, pick a subject that has **at least two tasks**, delete it, then
navigate away and back.

**Expected:** the delete succeeds without an FK/cascade error. The subject's tasks go with it. Every
*other* subject in the semester, and its tasks, are untouched.

**Why this target:** subject deletion (`ViewModels/QuanLyMonHocViewModel.cs` → `XoaMon` →
`Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs` → `db.MonHocs.Remove`) is
the reachable path to the EF cascade-fixup regression this scenario was always meant to guard:
reparenting tasks away from a soon-to-be-deleted parent needs the FK reassignment and
`DetectChanges()` to happen *before* `Remove()`, and mutating only the `ObservableCollection` is not
enough.

---

## 4. Observation and result record

**Fill in during execution. Leave blank until actually run.**

> **Owner's 2026-08-10 run is recorded separately, in the owner's own words, at
> `docs/reports/2026-08-10-epic3-soe-manual-observation.md`.** It is linked rather than transcribed
> here: it is the owner's primary evidence, and copying it into a document authored by someone else
> muddies provenance instead of clarifying it.

> **Owner's 2026-08-19 run of the scenarios changed after 2026-08-10 is recorded, on the same terms,
> at `docs/reports/2026-08-19-epic3-manual-observation-updated.md`.** It covers **C2 (re-run), C8,
> C9, C10 at `12`, C10 at `4.5`, and E5** — six of the seven scenarios that were added or rewritten
> in commits `1afd3fa` and `5008956`. The seventh, **E6 (retargeted from semester delete to subject
> delete on 2026-08-14), has not been run in either form.**

Legend: **P** = pass · **F** = fail · **N/A** = not applicable · **?** = unclear/needs discussion

| # | Scenario | P/F | Observed behaviour | Evidence file | Notes |
|---|---|---|---|---|---|
| A1 | Pre-Epic-3 DB upgrades, data intact | | | | |
| A2 | Fresh DB first launch | | | | |
| A3 | Repeat launches idempotent | | | | |
| B1 | `OptimizerRunLogs` table exists | | | | |
| B2 | Table empty (expected) | | | | |
| C1 | Dense contiguous packing from today | | | | |
| C2 | Holds across capacity 1/3/8 | → 08-19 | _2026-08-10 "Met" withdrawn; re-run 2026-08-19, owner's wording not transcribed_ | `2026-08-19-epic3-manual-observation-updated.md` | Press XẾP LỊCH LẠI after every slider move |
| C3 | Slider bounds 1–8, no hang | | | | |
| C4 | Priority ordering preserved | | | | |
| C5 | Split parts labelled correctly | | | | |
| C6 | Completed tasks excluded | | | | |
| C7 | **Header copy decision (QA-1)** | | | | Wording chosen: |
| C8 | Slider moves readout + badge, no bar | → 08-19 | _owner's record, not transcribed_ | `2026-08-19-epic3-manual-observation-updated.md` | |
| C9 | Button clears badge and rescales | → 08-19 | _owner's record, not transcribed_ | `2026-08-19-epic3-manual-observation-updated.md` | |
| C10 | Out-of-range `capacity.txt` → 8.0, no badge | → 08-19 | _both probes (`12`, `4.5`) run; owner's record, not transcribed_ | `2026-08-19-epic3-manual-observation-updated.md` | Owner notes the slider itself accepts whole hours only — raised there as a UX candidate, not a result |
| D1 | No tasks — no crash | | | | |
| D2 | Never-scored task still scheduled | | | | |
| D3 | Past-deadline placement (expected) | | | | Instance seen? |
| D4 | Already-overdue task handled | | | | |
| D5 | Schedule grows past 7 days | | | | |
| E1 | Dashboard | | | | |
| E2 | Analytics | | | | |
| E3 | Task/subject CRUD | | | | |
| E4 | Focus / logging / streak | | | | |
| E5 | Capacity persists | → 08-19 | _owner's record, not transcribed_ | `2026-08-19-epic3-manual-observation-updated.md` | |
| E6 | Subject delete (was: semester delete) | **NOT RUN** | _retargeted 2026-08-14; never executed in either form_ | | The only rewritten scenario still outstanding — see §6.1 |

### Gate status as of 2026-08-19 (record-keeping, not an observation)

Measured against §6.1, and stating only what the two owner records contain:

- **Satisfied by a recorded run:** A1–A3, B1–B2, C1, C2 (re-run), C3–C7, C8–C10, D1–D5, E5.
- **Not recorded:** **E6** — never executed; and **E1–E4** (Dashboard, Analytics, CRUD, focus/streak),
  which the 2026-08-10 record does not mention: its Group E section notes only the missing
  semester-management UI. They may have been exercised without being written down; absent a written
  observation this runbook treats them as unrecorded rather than as passed.
- **E1 has a reason to exist for the stale-chart branch specifically:** `5bd0a6a` changed
  `GetCapacity`, and `DashboardViewModel` is one of its three production callers. A capacity file
  outside 1–8 now reaches the Dashboard clamped.

### Free-text observations

> _(anything that surprised you, however small — record it here even if it passed)_

### Known gaps — not defects, not in scope here

- **No semester-management UI.** Semesters can be created but not renamed or deleted. This is what
  made the original E6 unexecutable. Recorded as a proposal for its own piece of work
  (`docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` §7), deliberately not
  absorbed into a bug-fix package.

---

## 5. Evidence to capture

Minimum set worth keeping, filed under `docs/reports/data/` or attached to the session report:

1. **C1 screenshot** — the day-distribution bars plus detailed list at 1 giờ/ngày. This is the single
   most valuable artefact: it is the visual record of the placement change.
2. **C2 screenshots** — the same view at 3 and 8 giờ/ngày.
3. **A1 before/after** — semester list from the old DB and after upgrade.
4. **B2 screenshot** — `OptimizerRunLogs` row count (expected 0).
5. **D3 screenshot** — if a past-deadline placement occurs, capture it; it documents A1 in the real
   product rather than only in the corpus harness.
6. **Any failure** — full screenshot including any error dialog text, plus what you did immediately
   before.

---

## 6. Pass / fail criteria

### 6.1 The manual gate PASSES when

- **A1–A3 all pass.** A pre-existing database must upgrade without data loss. This is non-negotiable:
  it touches real user data.
- **C1 passes at 1 giờ/ngày**, and **C2** confirms the same shape at 3 and 8. This is the behaviour
  Epic 3 exists to deliver.
- **C4, C5, C6 pass** — priority ordering, part labelling and completed-task exclusion are unchanged.
- **D1, D2, D4, D5 pass** — no crash, no silent disappearance, no malformed day sequence.
- **E1–E6 pass** — no collateral regression outside SOE.
- **B1 passes**; **B2 shows 0 rows** (0 rows is the pass condition, not a fail).
- **C7 is answered** — a wording decision recorded. C7 cannot "fail"; leaving it undecided simply
  keeps finding QA-1 open.
- **D3 is observed and recorded**, whatever the outcome. It cannot fail — it documents a ratified
  limitation.

### 6.2 The manual gate FAILS if any of these occur

- Any **data loss or corruption** in A1 — immediate stop, restore your backup, report before
  continuing.
- The app **crashes or hangs** in any scenario.
- **C1 shows the old spread-evenly shape** — the placement rework has regressed.
- **D2 shows the task missing** — the priority write-through regression has returned. This one is
  silent by nature; it is the reason D2 is in this runbook at all.
- Any day in the schedule **exceeds the capacity ceiling**.
- **B2 shows rows** in `OptimizerRunLogs` — something is calling a seam that has no production
  caller, meaning the shipped scope is not what the closing note says it is.

### 6.3 Outcome to report back

State plainly: **PASS**, **PASS WITH FINDINGS** (list them), or **FAIL** (list blockers). Attach the
completed table from §4 and the evidence from §5. Do not summarise a partially-executed run as a
pass — record which scenarios were skipped and why.
