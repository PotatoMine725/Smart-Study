# Runbook — DFD-9a end-to-end check: does a real focus session log its prediction?

> **Why this exists.** The DFD-9a fix carries the prediction across three layers, and automated tests
> cover each hop plus their composition against stubs. **None of them exercises the production DI
> wiring** — `App.xaml.cs` / `ServiceLocator` resolving the same `IDecisionEngine` the dashboard uses,
> against the real SQLite file. A suite of 492 green tests cannot tell you the shipped app writes a
> non-null `PredictedMinutes`. Only this can.
>
> Defect record: [`2026-08-26-prediction-instrumentation-defect.md`](2026-08-26-prediction-instrumentation-defect.md) §6, §9.4
> (the one gate left open). Ruling: [`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) §13.

**Time:** ~10 minutes, of which ~3 are sitting watching a timer.

> **Shell: Windows PowerShell**, which is what this machine opens by default. Every command below was
> executed in PowerShell before being written down, not translated into it.
>
> *Corrected 2026-08-27.* The first version of this runbook was written in bash — `&&`, `ls -la`,
> `$APPDATA`, and a `<<'PY'` heredoc, **five of its six commands** — and the read command in §2 was
> verified with a bash tool rather than in the shell the operator actually uses. It stopped the first
> run at §2. A check whose instructions cannot be executed by the person running them has not been
> verified, however carefully its logic was reasoned. See §8.
>
> Using Git Bash instead? Then `python tools/qa/read_outcome_logs.py` still works unchanged — it takes
> no shell syntax — and the rest are ordinary file operations you can translate freely.

---

## 1. Preconditions

### 1.1 Build the tree you think you are testing

```powershell
rtk dotnet build SmartStudyPlanner.slnx
```

Expect **0 errors**.

### 1.2 Provenance check — the binary must be newer than the fix

The whole check is void if you run yesterday's executable. Confirm the exe was written by the build
you just ran, and that the DFD-9a commit is actually in your tree:

```powershell
Set-Location "D:\Code\C#\SmartStudyPlanner"
Get-Item "SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyPlanner.exe" |
    Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
git log -1 --date=short --format="%h %ad %s" -- SmartStudyPlanner/ViewModels/FocusViewModel.cs
```

The exe's `LastWriteTime` must be **later** than the commit date `git log` prints. If it is earlier,
the build did not run or wrote somewhere else — stop and fix that before going further.

**The exe's mtime must be later than the commit date.** If it is older, the build did not run or you
are about to launch a different copy — **stop and rebuild**.

### 1.3 Pin the database, and take a baseline

The app opens `SmartStudyData.db` next to its own executable
(`AppDbContext.OnConfiguring` → `AppDomain.CurrentDomain.BaseDirectory`). Running the **Debug** exe
therefore reads and writes:

```
SmartStudyPlanner/bin/Debug/net10.0-windows10.0.19041.0/SmartStudyData.db
```

**There is more than one `SmartStudyData.db` in this repo** — a Release copy and a
`manual-backup-pre-epic1/` copy. Reading the wrong one is the easiest way to get a confident wrong
answer, so the read command below prints the file's path and mtime every time. Check them.

**Back it up first.** §3's Scenario 2 marks a task complete, and both scenarios add studied minutes —
real changes to real test data:

```powershell
$db = "SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db"
Copy-Item $db "$db.pre-dfd9a-$(Get-Date -Format 'yyyyMMdd-HHmm').bak"
Get-ChildItem "$db.pre-dfd9a-*.bak" | Select-Object Name, Length | Format-Table -AutoSize
```

> A backup taken **2026-08-27 16:26** already exists — it was created while verifying these commands,
> from the same untouched database. Taking another is harmless; the timestamp is in the filename.

Now take the baseline with the read command from §2:

**Expected baseline, measured 2026-08-26** (this exact command was run against the Debug database
while writing this runbook): **2 rows**, dated `2026-07-15` and `2026-07-20`, **both with
`PredictedMinutes = NULL` and `Confidence = NULL`** — written before the fix.

Note what those two rows already show: one has `WasMlPrediction = 1`, the other `0`, and **both have
`Confidence = NULL`**. That is the defect itself, sitting in your database — the row knows a
prediction happened and cannot say what it was.

> If your baseline differs (more rows, or some already non-null), **that is not a failure** — someone
> has used the app since. Write down what you actually saw and use *your* number as the baseline.
> Never adjust the observation to match this document.

### 1.4 Know which branch you are on before you start

The ML path only runs when a trained model exists at
`%APPDATA%\SmartStudyPlanner\models\study_time.zip`. On the owner's machine this file is present
(dated 2026-06-27), so predictions should take the real ML path.

```powershell
$model = "$env:APPDATA\SmartStudyPlanner\models\study_time.zip"
if (Test-Path $model) { Get-Item $model | Select-Object FullName, Length, LastWriteTime | Format-List }
else { Write-Output "ABSENT: $model" }
```

*Verified 2026-08-27:* **present**, 12 607 bytes, dated 2026-06-27 — so this run should take the real
ML branch, and `Confidence` should be a genuine agreement score rather than the fallback `0`.

| Model file | What you will see | Does the check still work? |
|---|---|---|
| **Present** | `WasMlPrediction` may be `1`, `Confidence` a real agreement score | Yes — the richer case |
| **Absent** | `IsReady` is false → the predictor returns its fallback: `WasMlPrediction = 0`, `Confidence = 0` | **Yes.** `PredictedMinutes` must *still* be non-null — the fallback estimate is a prediction and gets logged |

**This matters for reading the result.** `Confidence = 0` with `WasMlPrediction = 0` is the fallback
path: correctly instrumented, but it says nothing about the model. It is **not** a failure. See §5.

### 1.5 Explicitly not in scope

- Any judgement about whether the predicted number is *good*. This checks that it is **recorded**.
- Any threshold: not the inline `≥ 0.6` ML-vs-formula switch, not `DefaultMlConfidencePolicy`.
- **F-1** (M8-A confidence-gate calibration) — still deferred.
- **DFD-5 row-level provenance** — deliberately not implemented; `Confidence = 0` stays ambiguous.
- Backfilling old rows. Impossible; that is why the defect was urgent.

---

## 2. The read command

Run this from the repository root, **with the app closed**, both for the baseline and after each
scenario. Closing the app also guarantees the write is flushed.

```powershell
python tools/qa/read_outcome_logs.py
```

It is a committed script — [`../../tools/qa/read_outcome_logs.py`](../../tools/qa/read_outcome_logs.py) —
not a pasted snippet, so it carries no shell syntax and behaves identically in PowerShell, cmd and
bash. It resolves the Debug database from its **own** location, so it works from any working
directory, and it opens the file **read-only**. Pass a path to read a different database.

**Verified output, 2026-08-27, run in PowerShell against the untouched Debug database:**

```text
FILE : D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db
MTIME: 2026-07-26 20:44:36.097919
ROWS : 2
CreatedUtc               Actual  Predicted  WasML  Confidence
2026-07-15 11:36:31.39      1.0       NULL      0        NULL
2026-07-20 13:28:20.53      1.0       NULL      1        NULL
```

**That is your baseline, and it is also the negative control.** Both rows predate the fix. The second
one is the defect stated in data: `WasML = 1` says a model prediction happened, `Confidence = NULL`
says the app cannot tell you what it was.

If the script prints an `ERROR:` block, read which one. *"zero tables"* means the instrument is
broken and you should record **no verdict**; *"table not in this database"* with a table count means
you are reading the wrong file.

**Read `NULL` literally.** The script prints the string `NULL` only where the column really is null —
it never coerces a null to `0`. That distinction is the entire check, which is why the two pre-fix
rows are worth keeping in view: they are the proof that this command *can* display a failure.

---

## 3. Scenarios

> **Order matters.** Scenario 1 uses *Thoát khẩn cấp*, which leaves the task active. Scenario 2 uses
> *Đã Xong*, which marks the task **complete** and removes it from the dashboard's active list. Run
> them in this order, or use two different tasks.
>
> **A session shorter than 60 seconds writes no row at all** — `LuuThoiGianThucTe` guards on
> `_tongGiayDaHoc / 60 > 0`. The timer counts one second per tick while running. Every scenario below
> therefore says *at least 70 seconds*, and "no row appeared" after a 30-second session is a
> **mis-run, not a failure**.

### Scenario 1 — emergency-exit path writes an instrumented row

**Steps**

1. Launch `SmartStudyPlanner/bin/Debug/net10.0-windows10.0.19041.0/SmartStudyPlanner.exe`.
2. Open the **Dashboard** (it loads on start). Pick any active task in the top-5 table and press
   **`HỌC NGAY`**. The focus window opens, maximized.
3. Press **`▶ Bắt Đầu`**.
4. **Wait at least 70 seconds.** Watch *Tiến độ: Đã học N phút* tick over — that is your confirmation
   the timer is actually running and not paused.
5. Press **`✖ Thoát khẩn cấp`**. The window closes.
6. Close the application.
7. Run the read command (§2).

**Expected result**

- Row count = **baseline + 1**.
- The newest row: `ActualMinutes` = **`1.0`** (70 s ÷ 60, integer division; the column is `REAL`, so
  it prints as `1.0`), `PredictedMinutes` **NOT NULL**, `Confidence` **NOT NULL**.

**What a failure looks like**

| Observation | Meaning |
|---|---|
| New row present, `PredictedMinutes = NULL` | **FAIL — the defect is still live in the shipped app.** The fix works in tests but the production wiring does not carry the value. This is the outcome this runbook exists to be able to detect |
| New row present, `Confidence = NULL` while `PredictedMinutes` is set | **FAIL — half-carried.** Only one of the two assignments is reaching the write site |
| **No new row**, and a *"Lỗi lưu dữ liệu"* dialog appeared | **FAIL — the write threw.** Record the dialog text verbatim |
| **No new row**, no dialog | **Mis-run**, almost certainly a session under 60 s or a timer that was never started. Redo — do not record a verdict |
| Row count jumped by more than 1 | You ran extra sessions, or the app was open twice. Note it; the newest row is still the one to read |

---

### Scenario 2 — completion path writes an instrumented row

Same as Scenario 1, but at step 5 press **`✅ Đã Xong`** instead.

`HoanThanh` and `ThoatKhanCap` reach the write site through two different commands. Both must log the
prediction — checking only one leaves half the production surface unverified.

**Expected result:** row count = **baseline + 2**; newest row has `ActualMinutes` = `1.0` and both
columns non-null.

**Additionally expected (not part of the verdict):** the task is now marked complete and drops off the
dashboard's active list. That is `HoanThanh`'s normal behaviour, not a side effect of this fix.

**What a failure looks like:** as Scenario 1. If Scenario 1 passed and Scenario 2 fails, the two
command paths diverge — record which one.

---

### Scenario 3 — channel integrity: the old rows must still read NULL

Not a separate run. Read the same output from Scenario 2 and check the **oldest** rows.

**Expected result:** the pre-fix rows (baseline rows, `CreatedUtc` earlier than today) still show
`PredictedMinutes = NULL` and `Confidence = NULL`, **in the same output** where the new rows show
values.

**Why this scenario exists.** It is the independent proof that the observation channel can display
both outcomes. If every row — old and new — came back non-null, the reader would be fabricating
values rather than reporting them. If every row came back NULL, the fix did not land. Seeing **both
in one output** is what makes a PASS on Scenarios 1–2 mean something.

**What a failure looks like**

| Observation | Meaning |
|---|---|
| Old rows now show values | **FAIL — instrument broken.** Nothing backfills old rows; if they changed, you are reading a different file, or the script is coercing nulls. **Withdraw the Scenario 1–2 verdicts** and re-establish the channel first |
| Old rows absent entirely | You are reading a different database (or a restored backup). Check the `FILE:` and `MTIME:` lines |

---

### Scenario 4 — conditional: was this the ML branch or the fallback?

Not pass/fail. It records **what the row is evidence of**, and takes one glance at the output.

| `WasMlPrediction` | `Confidence` | What the row means |
|---|---|---|
| `1` | > 0 | Real ML prediction above the `0.6` switch. `Confidence` is a genuine agreement score |
| `0` | > 0 | ML ran and was **rejected** (< 0.6); the logged minutes are the formula estimate. **This is a valid, wanted row** — it is the population F-1 will eventually need |
| `0` | `0` | **Fallback** — no model was ready. Correctly instrumented, but says nothing about the model. Expected if §1.4 found no `study_time.zip` |

Record which line you got. A run that only ever produces the third line still **passes** the check
(the columns are populated) while proving nothing about the predictor — and a later reader needs to
know which of those two things your run established.

---

## 4. Pass / fail criteria — fixed before the run

**PASS** requires **all four**:

1. Scenario 1 produced exactly one new row with `PredictedMinutes` **NOT NULL** and `Confidence`
   **NOT NULL**.
2. Scenario 2 produced exactly one further new row, same two columns non-null.
3. Scenario 3: the pre-fix rows still read `NULL` in the same output.
4. `ActualMinutes` on both new rows matches the time actually sat (**`1.0`** for a ~70-second
   session; the two baseline rows read `1.0` for the same reason).

**FAIL** if any new row has `PredictedMinutes` or `Confidence` NULL, or if a save-error dialog
appeared, or if Scenario 3's control rows changed.

**NOT RUN** is a valid outcome and must be written as such. A blank cell that reads as a pass is not.

**These criteria are fixed. Do not adjust them after seeing the output** — if the result is
surprising, the surprise is the finding.

---

## 5. Result table — leave blank until the run happens

| # | Scenario | Verdict | Row count before → after | `PredictedMinutes` | `Confidence` | `WasMlPrediction` | Notes |
|---|---|---|---|---|---|---|---|
| — | Baseline read (§1.3) | | | | | | |
| 1 | Emergency-exit path | | | | | | |
| 2 | Completion path | | | | | | |
| 3 | Channel integrity (old rows NULL) | | — | | | — | |
| 4 | Branch taken (record only) | n/a | — | — | | | |

**Run by:** ______________  **Date:** ______________  **Build / exe mtime:** ______________

---

## 6. Where the result goes — not here

**Results do not live in this runbook.** Per `docs/reports/README.md`, what the person at the keyboard
saw is an **evidence record**, in their own words:

> `docs/reports/2026-08-26-dfd9a-instrumentation-observation.md`

Write down what appeared on screen, including anything unexpected, and **do not** reformat it into
report sections — an evidence record is exempt from them, and tidying it corrupts the thing it exists
to preserve. The table above may be copied in as a summary; the raw read-command output is the actual
evidence.

Then, and only then, update the two documents that currently say this gate is open:

- [`2026-08-26-prediction-instrumentation-defect.md`](2026-08-26-prediction-instrumentation-defect.md) §9.4 — *"The end-to-end check has NOT been run"*
- [`../active/README.md`](../active/README.md) — the DFD-9a row's *"One gate still open"*

**If the run fails**, do not edit the fix into looking correct. File the observation, and the defect
record reopens on that evidence.

## 7. Restoring afterwards

Scenario 2 marks a task complete and both scenarios add studied minutes — real changes to your test
data. To undo, close the app and restore the backup from §1.3:

```powershell
$db  = "SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db"
$bak = Get-ChildItem "$db.pre-dfd9a-*.bak" | Sort-Object Name | Select-Object -Last 1
Write-Output "restoring from: $($bak.Name)"
Copy-Item $bak.FullName $db -Force
```

**Sort by `Name`, not by `LastWriteTime`.** `Copy-Item` preserves the *source* file's timestamp, so
every backup carries the database's mtime and sorting by time picks an arbitrary one. The name holds
the real stamp, and `yyyyMMdd-HHmm` sorts correctly as text.

Restoring **deletes the evidence rows** as well. Read and record them first — §6 before §7.

---

## 8. Correction record — 2026-08-27

The first version of this runbook, written 2026-08-26, **could not be executed by its own operator.**
It stopped the first run at §2. Recorded here rather than silently rewritten, because the failure is
not a typo and the same mistake is available to any future runbook.

### 8.1 What was wrong

| # | Where | Written as | Fails because |
|---|---|---|---|
| 1 | §1.2 | `ls -la "…"` | `ls` is an alias for `Get-ChildItem`; `-la` is not one of its parameters |
| 2 | §1.3 | `cd … && cp … "$(date +%Y%m%d-%H%M)"` | `&&` is a parser error in Windows PowerShell 5.1; `date +%…` is not `Get-Date` |
| 3 | §1.4 | `ls -la "$APPDATA/…"` | PowerShell reads environment variables as `$env:APPDATA` |
| 4 | **§2** | `cd … && python - <<'PY'` | Both halves fail: `&&`, and bash heredocs do not exist in PowerShell. **This is where the run stopped** |
| 5 | §7 | `cd … && cp ….bak …` | `&&` again |

**Five of six commands.** Only `rtk dotnet build` was portable, by accident rather than by design.

### 8.2 The root cause is not the syntax

§2 of the original said the read command had been *"run verbatim to prove it executes."* It had — in
a bash tool, which is not the shell the operator uses. **The verification and the operator were on
different instruments**, so what it proved was that the logic was sound, not that the runbook was
runnable.

`[observation]` This is the project's own standing rule about manual checks — *test the observation
channel by an independent path, and verify the instrument before trusting a result* — failing at a
step earlier than the one it is usually applied to. The instrument here was not the SQL query. **It
was the shell.** Reasoning about a command is not the same as running it, and running it somewhere
else is not the same as running it here.

### 8.3 Two silent faults found while fixing it

Both were introduced by the fix and caught by running it. Both are worth knowing about because they
fail **quietly, in the direction of a false verdict**:

1. **`sqlite3.connect("file:" + windows_path, uri=True)` opens an empty database.** `file:D:/x.db` is
   not a valid URI, and SQLite does not complain — it reports **zero tables**, which reads exactly
   like *"the migration never ran."* The first run of the new script produced a confident
   *"StudyTimeOutcomeLogs is not in this database"* about a database that contains it.
2. **The `#` in `C#` truncates a hand-built URI.** `#` opens a fragment, so the path silently ends at
   `D:/Code/C`. Specific to this repository's location, and invisible until something downstream is
   empty.

Both are fixed by building the URI with `pathlib.Path(p).as_uri()`, and the script now treats *"zero
tables"* as **a broken instrument that must not produce a verdict**, rather than as a finding.

`[inference]` The general shape is the one worth carrying: **a diagnostic that reports "absent" is
making a claim, and it can be wrong in the same ways any other claim can.** An error path that
confidently names a cause — *"wrong file, or an older schema"* — is more dangerous than one that
admits it does not know, because it hands the operator a conclusion to record.

### 8.4 What is now verified, and how

Every command in this runbook was executed in **Windows PowerShell** on 2026-08-27 before being
written down, against the real Debug database, and its actual output is reproduced where it matters
(§1.4, §2). The read command is a committed script rather than a pasted snippet, so what is verified
and what is run are the same bytes.

**What is still unverified is the thing the runbook is for:** the scenarios in §3 need the
application driven by a human, and the result table in §5 is still blank.
