# Evidence record — DFD-9a end-to-end check, run 2026-08-27

> **This is an evidence record, not a report.** Per [`README.md`](README.md) it is exempt from the
> Scope / Verification / Decisions sections, and its wording must not be tidied by anyone else.
>
> **Who wrote what.** §1 and §2 are machine output and file metadata, captured by Claude directly
> from the build and the database — reproducible by re-running the commands. **§3, §4 and §5 are the
> owner's to write**, in the owner's own words. The blanks are deliberate; a blank that reads as a
> pass is worse than an empty one.
>
> Runbook: [`../plans/2026-08-26-dfd9a-instrumentation-runbook.md`](../plans/2026-08-26-dfd9a-instrumentation-runbook.md)
> Defect record: [`../plans/2026-08-26-prediction-instrumentation-defect.md`](../plans/2026-08-26-prediction-instrumentation-defect.md) §9.4

---

## 1. Provenance — what was actually run

| Item | Value | Checked how |
|---|---|---|
| Executable | `SmartStudyPlanner/bin/Debug/net10.0-windows10.0.19041.0/SmartStudyPlanner.exe` | |
| Exe size / mtime | 178 176 bytes, **2026-08-27 15:57:57** | `Get-Item` |
| Fix commit in tree | `bb5d241` (2026-08-26), `FocusViewModel.cs` | `git log -1` |
| Exe newer than fix? | **Yes** — 08-27 15:57 > 08-26 | runbook §1.2 |
| Database | `…/net10.0-windows10.0.19041.0/SmartStudyData.db` | printed by the read script |
| Model file | `%APPDATA%\SmartStudyPlanner\models\study_time.zip` — **present**, 12 607 bytes, 2026-06-27 | runbook §1.4 |
| Backup taken | `SmartStudyData.db.pre-dfd9a-20260827-1626.bak` | runbook §1.3 |

---

## 2. Raw read-command output

Command: `python tools/qa/read_outcome_logs.py`

### 2.1 Baseline, before any scenario

```text
FILE : D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db
MTIME: 2026-07-26 20:44:36.097919
ROWS : 2
CreatedUtc               Actual  Predicted  WasML  Confidence
2026-07-15 11:36:31.39      1.0       NULL      0        NULL
2026-07-20 13:28:20.53      1.0       NULL      1        NULL
```

### 2.2 After Scenario 1 and Scenario 2

Captured 2026-08-27 17:51 from the same database, after both sessions:

```text
FILE : D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db
MTIME: 2026-08-27 17:51:37.427884
ROWS : 4
CreatedUtc               Actual  Predicted  WasML  Confidence
2026-07-15 11:36:31.39      1.0       NULL      0        NULL
2026-07-20 13:28:20.53      1.0       NULL      1        NULL
2026-08-27 10:24:35.24     16.0        0.0      0         0.0
2026-08-27 10:38:29.93      2.0        0.0      0         0.0
```

*(`CreatedUtc` is UTC; local time was +7, so the two sessions ended 17:24 and 17:38 local.)*

**Owner: paste your own two terminal outputs here if they differ from the above in any way.**

<!-- paste run-1 (after Scenario 1) and run-2 (after Scenario 2) outputs here -->

### 2.3 Full column dump of the two new rows

```text
Id | CreatedUtc | MaTask | TaskType | Difficulty | Credits | DaysLeft | StudiedMinutesSoFar | ActualMinutes | PredictedMinutes | WasMlPrediction | Confidence
7C6FD64F-… | 2026-08-27 10:24:35.24 | 533482D5-… | 3 | 5.0 | 4.0 | -77.0 | 1.0 | 16.0 | 0.0 | 0 | 0.0
B2403F0D-… | 2026-08-27 10:38:29.93 | 6E9F9920-… | 4 | 5.0 | 4.0 | -77.0 | 0.0 |  2.0 | 0.0 | 0 | 0.0
```

Both tasks: `HanChot = 2026-06-11`, i.e. **77 days past deadline**, `DiemUuTien = 0.0`.

### 2.4 Second pass — two new tasks with deadlines in the future

The owner did **not** restore the backup, and instead ran Scenarios 1 and 2 again against two
**newly created, non-overdue** tasks — runbook §3.1. Captured 2026-08-27 18:34 local:

```text
FILE : D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db
MTIME: 2026-08-27 17:51:37.427884
ROWS : 6
CreatedUtc               Actual  Predicted  WasML  Confidence
2026-07-15 11:36:31.39      1.0       NULL      0        NULL
2026-07-20 13:28:20.53      1.0       NULL      1        NULL
2026-08-27 10:24:35.24     16.0        0.0      0         0.0
2026-08-27 10:38:29.93      2.0        0.0      0         0.0
2026-08-27 11:30:16.24      3.0      132.0      1  0.8999999761581421
2026-08-27 11:32:35.20      1.0       88.0      1  0.7333333492279053
```

Full columns, and the tasks they point at:

```text
CreatedUtc              MaTask     Type Diff Cred DaysLeft Studied Actual Predicted ML Confidence
2026-08-27 11:30:16.24  59AE3156-…    1  3.0  4.0      5.0     0.0    3.0     132.0  1  0.8999999761581421
2026-08-27 11:32:35.20  51C13862-…    0  3.0  4.0      4.0     0.0    1.0      88.0  1  0.7333333492279053

MaTask      TenTask   HanChot       TrangThai    DiemUuTien  DoKho  ThoiGianDaHoc
59AE3156-…  Task A    2026-09-01    Chưa làm          73.19      3              3
51C13862-…  Task B    2026-08-31    Hoàn thành        68.38      3              1
```

Task B is `Hoàn thành` because Scenario 2's `✅ Đã Xong` completed it — expected, per runbook §3.

### 2.5 The logged confidences reproduce exactly from the source arithmetic

`confidence = 1 - clamp(|predicted - formula| / max(formula, 1), 0, 1)`, and
`formula = round(((DiemUuTien/100)·120 + (DoKho/5)·60) / 15) · 15`:

| Task | `DiemUuTien` | `formula` | `predicted` | Expected confidence | **Logged** |
|---|---|---|---|---|---|
| A | 73.19 | `round(123.83/15)·15 = 120` | 132 | `1 − 12/120` = **0.9** | `0.8999999761581421` |
| B | 68.38 | `round(118.06/15)·15 = 120` | 88 | `1 − 32/120` = **0.73333…** | `0.7333333492279053` |

Both match to `float32` precision — the trailing digits are the single-precision representation of
`0.9` and `11/15`, not noise. **This is independent confirmation that the logged `Confidence` is the
real computed agreement score**, not a placeholder or a rounded display value: the number could not
reproduce from the inputs by coincidence.

Both are ≥ `0.6`, so the ML branch was **accepted** and `PredictedMinutes` holds the model's own
output (132, 88) rather than the formula's 120.

### 2.6 Note on the database mtime — WAL

The main `.db` mtime (17:51) is **older than the last two rows** (18:30, 18:32 local). This is not an
anomaly: the database runs in `journal_mode = wal`, so recent commits live in
`SmartStudyData.db-wal` (104.6 KB at time of capture) until a checkpoint folds them into the main
file. The read script sees them because SQLite reads the WAL transparently.

Re-checked at 18:37 after the application was closed: the sidecars are **gone**, the main file's mtime
is `2026-08-27 18:36:59`, and all **6 rows** read back from it. SQLite checkpointed on the last
connection close, so the evidence is durable in the `.db` itself — nothing is stranded in a WAL.

**This still matters for §7 of the runbook.** Copying a `.bak` over the `.db` while a stale
`-wal`/`-shm` pair is present lets SQLite replay that WAL against the restored file. It did not
happen here only because the app was closed first. The restore procedure has been corrected to check
for the sidecars explicitly rather than rely on that.

---

## 3. What the person at the keyboard saw — **owner's attestation**

Given by the owner, 2026-08-27 18:58, verbatim:

> "i ran for a few minutes, not just 70 secs, and recorded the changes in numbers"

**What that covers.** The 70-second figure in runbook §3 is a *minimum* (a session under 60 s writes
no row at all), not a target. Every session exceeded it. The owner watched the on-screen progress
counter change during the sessions and confirms the logged `ActualMinutes` are consistent with what
was displayed — which is what runbook §3 step 4 asks for, and it is the reason the "no row appeared"
mis-run case does not apply to any of these four rows.

**Scope of this attestation.** It is *"the displayed counter tracked the session and the logged
numbers match what I saw"* — **not** an independently stopwatched duration per session. No separate
timing instrument was used, and none was called for: `ActualMinutes` is itself the recorded duration,
and the attestation is that it agrees with the observation.

**Not separately reported by the owner, so not recorded as observed:** whether any dialog or error
appeared during each individual session. No save-error dialog is reported, and the successful writes
are inconsistent with the *"Lỗi lưu dữ liệu"* failure mode.

**Machine-observed, alongside the attestation** (not owner testimony): Task B reads
`TrangThai = 'Hoàn thành'` after Scenario 2, so the `✅ Đã Xong` completion path did what §3 says it
does.

---

## 4. Pre-registered criteria — **owner's ruling, 2026-08-27**

The criteria were fixed in runbook §4 before the run and were not adjusted after seeing the output.

| # | Criterion | What the output shows | Owner's ruling |
|---|---|---|---|
| 1 | S1: exactly one new row, `PredictedMinutes` and `Confidence` both NOT NULL | 2 → 3 rows; new row `0.0` / `0.0` — non-null | **PASS** |
| 2 | S2: exactly one further new row, same two columns non-null | 3 → 4 rows; new row `0.0` / `0.0` — non-null | **PASS** |
| 3 | Pre-fix rows still read `NULL` in the same output | Both 2026-07 rows still `NULL` / `NULL` | **PASS** |
| 4 | `ActualMinutes` matches the time actually sat | `16.0` and `2.0` — attested in §3 | **PASS** |

**Second pass (§2.4), against non-overdue tasks — the same four criteria, independently:**

| # | Criterion | What the output shows | Owner's ruling |
|---|---|---|---|
| 1 | S1 on Task A | 4 → 5 rows; `Predicted = 132.0`, `Confidence = 0.9`, both non-null | **PASS** |
| 2 | S2 on Task B | 5 → 6 rows; `Predicted = 88.0`, `Confidence = 0.733…`, both non-null | **PASS** |
| 3 | Pre-fix rows still `NULL` | Both 2026-07 rows still `NULL` / `NULL` in the 6-row output | **PASS** |
| 4 | `ActualMinutes` matches time sat | `3.0` and `1.0` — attested in §3 | **PASS** |

**Overall verdict: PASS** — ruled by the owner, 2026-08-27, on both passes.

**What this PASS is evidence of, precisely.** The shipped application, through its production DI
wiring and against the real SQLite file, writes a non-null `PredictedMinutes` and `Confidence` on both
the emergency-exit and the completion command paths. DFD-9a's remaining gate — the one no automated
test could close, because none of the 492 exercises the production wiring — is closed.

**What it is not evidence of:** that the predicted numbers are *good*. Runbook §1.5 puts that out of
scope, and nothing here bears on it.

---

## 5. Scenario 4 — which branch produced these rows

**Result: DETERMINED by the second pass — the real ML branch ran.**

> **Owner's ruling, 2026-08-27:** *"the 2nd branch (3-7 days out)"* — i.e. the determinate answer is
> taken from the second pass, the tasks with runway remaining, and **not** from the first pass's
> overdue tasks. Recorded as the answer to Scenario 4.

| Pass | Tasks | Row reads | Scenario 4 line |
|---|---|---|---|
| First (§2.2) | 77 days overdue | `WasML = 0`, `Confidence = 0.0`, `Predicted = 0.0` | **Undetermined** — see below |
| Second (§2.4) | 4–5 days of runway | `WasML = 1`, `Confidence = 0.90` / `0.73`, `Predicted = 132` / `88` | **Real ML prediction above the `0.6` switch** |

The second pass lands on the **first** line of runbook §3's Scenario-4 table: `WasMlPrediction = 1`
with a confidence strictly between 0 and 1. That combination is reachable only through the ML branch —
`Fallback()` hard-codes `0f`, and the rejected branch hard-codes `IsMLPrediction = false`. Together
with §2.5's arithmetic reproduction, the model demonstrably ran, scored, and was accepted.

`[inference]` It follows that `IsReady` was true throughout the session, and therefore almost
certainly during the first pass an hour earlier as well — the model file did not change between them.
Still an inference: the first pair of rows remains individually unfalsifiable, and is not upgraded
retroactively.

### 5.1 Why the first pass could not answer it

Both first-pass rows read `WasMlPrediction = 0`, `Confidence = 0.0`, `PredictedMinutes = 0.0`. For a task
more than 3 days overdue this output is *arithmetically forced*, and two different code paths
produce it identically:

1. `OverdueRule` (`daysLeft < -3`) sets `DiemUuTien = 0`.
2. `ComputeFormulaMinutes` returns `0` when `DiemUuTien <= 0`, so `formula = 0`.
3. `confidence = 1 - clamp(|predicted - formula| / max(formula, 1), 0, 1)` collapses to
   `1 - clamp(predicted)` — **exactly `0`** for any ML prediction of 1 minute or more.
4. `0 < 0.6`, so the ML branch is rejected and logs `(0, false, 0f)`.
5. `Fallback()` — taken when no model is ready — returns `(0, false, 0f)` as well.

The two are byte-identical in the row. This is DFD-5 (row-level provenance), already declared out of
scope at runbook §1.5.

`[inference]` `MLModelManager.InitializeAsync` retrains from seed data and sets `IsReady = true` even
when no valid model is on disk, and §1.4 confirmed a model file is present — so the ML branch
probably *did* run. **Not verified against the production DI wiring, and unfalsifiable from this
row.** Do not record it as an observation.

**What the logged `0` is not:** it is not a dropped value. `PredictedMinutes` is `int?` and
`Confidence` is `float?` end to end; a value dropped in transit arrives as `NULL`, which is exactly
what the two pre-fix rows show. `NULL → 0.0` in the same output is the transition the check was
looking for. `StudiedMinutesSoFar` independently corroborates it — `1.0` on the first new row and
`0.0` on the second, each matching its own task's pre-session `ThoiGianDaHoc`, so a per-task value is
demonstrably flowing through that write block rather than a constant.

### 5.2 What the second pass added

The first pass left one thing unshown: no row carried a *non-zero* prediction, because every task in
the database was past its deadline. The second pass closes it, and closes more than was asked:

- `PredictedMinutes` takes **two different non-zero values** (132, 88) on two different tasks —
  a per-task number, not a constant.
- `Confidence` takes **two different fractional values** that reproduce exactly from each task's own
  `DiemUuTien` (§2.5). A hard-coded or mis-wired value could not do that.
- `WasMlPrediction = 1` distinguishes the branch, which the first pass could not.

Taken with the pre-fix rows still reading `NULL` in the same output, the observation channel has now
displayed **three distinct states** — `NULL`, a real zero, and a real non-zero — which is what makes a
PASS on it mean something.

---

## 6. Follow-ups raised by this run — not part of the verdict

- The confidence expression can only ever return `0` or `1` when `formula = 0`. That is **F-1**
  (M8-A confidence-gate calibration), already deferred; runbook §1.5 puts it out of scope.
- Every task in the Debug database is overdue and every `DiemUuTien` is `0.0`. That is test-data
  staleness, not a defect — but it means the dashboard's top-5 ordering is currently all ties.
