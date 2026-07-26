# Epic 1 Reopen — Owner Re-Closure Runbook

> **Who runs this:** you (owner), manually. The gate reserves GUI runs for the owner.
> **What it validates:** that the reopen fix (merged to `ui_rf` as `37f9678`) actually closes the
> B1.4 crash on your machine, and that the two remaining retests behave as QA predicted.
> **Source:** the handoff section of
> [`../reports/2026-07-19-epic1-reopen-execution.md`](../reports/2026-07-19-epic1-reopen-execution.md).
> **Est. time:** ~30 minutes.

Do the steps **in order**. Each step has four parts: **Do → Expect → If unexpected → Send me.**
"Send me" is what I need back to write the Phase 3 closing note (or to diagnose a failure).

---

## The chain, at a glance

```
Step 0  Rebuild the exe  ──────────────►  (without this, you re-run the OLD crashing binary)
Step 1  Create a task (manual + smart)  ─►  the P0 that reopened Epic 1 — must NOT crash
Step 2  Heatmap, filter to "A"  ────────►  confirm it was a perception artifact, not a bug
Step 3  Lưu Tiến Trình on Dashboard ×2  ─►  confirm the button works when on the right page
Step 4  F2 waiver — sign off or veto
Step 5  Acknowledge one residual risk
Step 6  Record your B4 re-decision  ────►  Release → I start Phase 3 (C1–C3)
```

---

## ⚠️ Read first — what was fixed, and what was deliberately NOT

The reopen was scoped to **one P0 crash + crash-visibility only**. It did **not** touch the P1/P2
items you reported. Knowing this up front stops a working result from looking like a failure.

| Area | Status in this build | So expect… |
|---|---|---|
| Task-create crash (B1.4) | **FIXED** — R1-A stamps the subject FK at creation; R1-B heals/loudly-fails in the repo | No crash on create. **This is the whole point of the re-run.** |
| Silent process death | **FIXED** — R2 adds a global handler: an unhandled error now shows a Vietnamese dialog **and** writes `crash.log` instead of vanishing | If anything ever crashes, you get a dialog + a log, not a 30-second freeze into nothing |
| Vietnamese negation ("không dễ" → easy) | **NOT fixed** (P1, out of scope) | Smart-add will still misread negation. Ignore it for this run. |
| Cân Bằng Tải shows no tasks | **NOT fixed** (P1 UX, out of scope) | Still blank with the old overdue seed data. Ignore for this run. |
| Heatmap bucket look / "Lưu Tiến Trình" off-Dashboard | **NOT fixed** (P2, out of scope) | Steps 2–3 only confirm these were *artifacts*, not new code |

If you hit one of the "NOT fixed" items, it is **not** a re-run failure — note it and move on.

---

## Step 0 — Rebuild the fixed exe  *(CRITICAL — do not skip)*

Last time you ran `SmartStudyPlanner.exe` directly from the `bin\Debug\…` folder and "no other entry
was touched." That folder still holds the **old, crashing** binary. The fix lives in source on `ui_rf`;
it only reaches the exe when you rebuild.

**"Rebuild" = just run `rtk dotnet build SmartStudyPlanner.slnx` (step 0.3 below).** It recompiles the
source and **overwrites the exe in place** — the file path does **not** change. You then launch the
*same* exe you ran on 07-15:
`D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyPlanner.exe`.
There is no new or second exe. The only thing that changes is its contents (now with the fix) and its
Modified time. (One-shot alternative if you prefer: `rtk dotnet run --project SmartStudyPlanner` builds
and launches together — it uses the same DB, so it's safe.)

| # | Do | Expect |
|---|---|---|
| 0.1 | `rtk git status` — confirm branch is **`ui_rf`** and the tree has only your usual local noise (`.claude/*`, `AGENTS.md`, `CLAUDE.md`, `Assets/`, `tools/epic1_b2_verify.py`) | On `ui_rf`; no surprise edits |
| 0.2 | `rtk git branch --contains 37f9678` — confirm the reopen merge is in this branch's history (HEAD itself is now `d2df83c`, the report commit, so a plain `log -1` won't show the merge) | Output lists **`ui_rf`** |
| 0.3 | `rtk dotnet build SmartStudyPlanner.slnx` | **0 errors** (NU1903/NU1904 warnings are known + tracked; ~96 warnings is the baseline) |
| 0.4 | In Explorer, open `SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\` and check that **`SmartStudyPlanner.exe`'s Modified time is _just now_** | Timestamp = the build you just ran |

**If unexpected:**
- Not on `ui_rf` → `rtk git switch ui_rf` (commit/stash your local edits first), then rebuild.
- Build shows errors → **stop**, paste the full output to me. Do not run the old exe.
- exe timestamp is old / build said "up-to-date" but you doubt it → delete the `bin\Debug\net10.0-windows10.0.19041.0\SmartStudyPlanner.exe` and rebuild so it's forced to regenerate.

**Send me:** branch + HEAD line, and confirmation the build was 0 errors.

> Note on the database: your live DB was already upgraded on 2026-07-15 (it has the `Rev` columns).
> So this launch does **migration = none** and creates **no new `.bak` file** — that is correct, not a
> regression. The DB currently holds 5 subjects (`Lập Trình Nâng Cao`, `Toán Rời Rạc`, `A`, `A1`, `B`)
> from your last session. You do not need a clean DB for this run.

---

## Step 1 — B1.4: create a task end-to-end  *(the reopen driver — P0)*

This is the exact action that froze and killed the app five times on 2026-07-15. Both the manual form
and smart-add funnel through the **same** save path (`ThemTask` → `LuuHocKyAsync`), so test both.

Launch `SmartStudyPlanner.exe` (the one you just rebuilt). Go to **Môn Học & Bài Tập**.

### 1a — Manual create
| Do | Expect |
|---|---|
| Pick a subject (e.g. **`A1`** or **`B`**, which have no tasks yet), fill the task form by hand, save (**Lưu Deadline**) | Task is added and appears in the list **immediately, no freeze, no crash** |

### 1b — Smart-add create
| Do | Expect |
|---|---|
| Use **smart add / "Tự điền"**, type any Vietnamese task sentence, let it pre-fill, then save (**Lưu Deadline**) | Same — task added, **no freeze, no crash**. (The difficulty it guesses may be wrong for negations — ignore; that's the out-of-scope P1.) |

### 1c — Persistence across restart
| Do | Expect |
|---|---|
| **Close** the app, **relaunch**, return to the subject | Both new tasks are still there, **under the correct subject** (not orphaned, not under the wrong one) |

**Expect overall:** two tasks created without a single crash, both surviving a restart under the right subject.

**If unexpected — app freezes ~30s / crashes / shows an error dialog on create:**
1. This build has R2, so a crash now produces a **Vietnamese error dialog** and a log file. That's the
   safety net working even if the fix underneath didn't — capture both.
2. **Immediately grab the log:** open `%AppData%\SmartStudyPlanner\crash.log`
   (full path: `C:\Users\Wotbl\AppData\Roaming\SmartStudyPlanner\crash.log`). Copy its **newest** entry.
3. Double-check you're on the rebuilt exe (Step 0.4 timestamp). A stale exe is the #1 cause of a
   repeat crash — if the timestamp is old, rebuild and retry before reporting.
4. **Send me** the dialog text + the crash.log entry + the exe timestamp. Do **not** keep retrying.

**Send me (pass case):** "1a/1b/1c all clean, tasks persisted under `<subject>`." Optionally a screenshot.

---

## Step 2 — Retest #2: Analytics heatmap, filter to subject "A"

QA classified your "heatmap doesn't re-render" as a **perception artifact**, not a bug: the seeded logs
saturate every day to the darkest bucket, so rebuilds look identical. Subject **"A"** is the
discriminator — it has only your one real focus-session log, so its heatmap can't be saturated.

| Do | Expect |
|---|---|
| Sidebar → **Analytics** → open the subject dropdown → select **`A`** | The heatmap **collapses** from the dark-green block to a **single dim cell** (or a couple of light cells). That collapse = the wiring is correct; item closes. |

**If unexpected:**
- Heatmap does **not** change at all when you pick "A" → this is the *only* case that suggests a real
  wiring problem (or a stale exe). Re-confirm Step 0.4, then **send me** a screenshot of the "A" view.
- Heatmap looks the same dark block for *every other* subject → that's **expected** (saturation), not a
  failure. Only the "A" behavior is the test.

**Send me:** "A collapses to a dim cell: yes/no" (a screenshot helps).

---

## Step 3 — Retest #3: "Lưu Tiến Trình" on the Dashboard, twice

QA found this button silently no-ops **off** the Dashboard (a known P2, not fixed here), but works
**on** the Dashboard. Test it on the right page.

| Do | Expect |
|---|---|
| Go to the **Dashboard** page → click **Lưu Tiến Trình** → click it **again** | **Two** "Đã lưu tiến trình thành công!" success dialogs, no error |

**If unexpected:**
- No dialog on the Dashboard → real finding; **send me** exactly which page was showing and whether the
  button looked disabled.
- No dialog on some *other* page → **expected** (the off-Dashboard no-op is the known P2). Retest on the
  Dashboard specifically.

**Send me:** "Two success dialogs on Dashboard: yes/no."

---

## Step 4 — F2 waiver: sign off or veto

**Plain version:** three background telemetry writes (`OutcomeMaturationService.MatureAsync`,
`LogDifficultyLabelAsync`, `LogWeightChangeAsync`) are fire-and-forget. If the app closes mid-write, a
single ML-training telemetry row is lost. **Your study data — semesters, subjects, tasks, logs, streaks
— is never at risk** (those are written on awaited, error-propagating paths). This build already added
the QA-recommended nuance: those three writes are now **observed**, so a failure leaves a trace in
`crash.log` instead of vanishing.

- **Sign off (recommended):** "F2 waived — telemetry writes accepted as loss-tolerant; observation
  nuance noted." Epic 1 can close without further change here.
- **Veto:** they'd get full hardening in a follow-up (~1h). QA does not recommend full await-ing (it
  would freeze the UI for zero user benefit).

**Send me:** "F2: waive" or "F2: veto (+ reason)."

---

## Step 5 — Acknowledge one residual risk

The three global exception handlers (Step 1's safety net) are verified by unit tests on the logger + a
diff review of the wiring + your real launch — but they **cannot be unit-tested live** (no way to drive
WPF's `Application` handlers headlessly). Your clean launch in Steps 1–3 is a meaningful part of their
verification. Nothing to do here except **acknowledge** you accept that.

**Send me:** "Residual risk acknowledged."

---

## Step 6 — Record your B4 re-decision

Based on Steps 1–3:

- ✅ **Epic 1 Released** — Step 1 no longer crashes, Steps 2–3 behave as predicted → I proceed to
  **Phase 3 (C1 closing note, C2 archive, C3 next-epic prep)**, stopping before any code.
- ❌ **Reopen again** — with the specific failing step + evidence.

An explicit sign-off sentence is required by the gate's success criteria (per
[`2026-07-11-epic-1-closure-gate.md`](2026-07-11-epic-1-closure-gate.md) Task B4).

**Where to record it:** simplest is to add a short dated decision file
`docs/specs/2026-07-20-owner-epic-1-redecision.md` (matching the existing
`docs/specs/2026-07-19-owner-epic-1-decisions.md`), or just reply here and I'll write it up for your
sign-off. Either way the sentence must be **yours**.

**Send me:** your one-line B4 decision.

---

## If something goes wrong — quick reference

- **crash.log:** `C:\Users\Wotbl\AppData\Roaming\SmartStudyPlanner\crash.log` — this file existing/growing
  is R2 doing its job. Send me the newest entry any time the app misbehaves.
- **DB rollback (only if data looks wrong):** close the app; in `bin\Debug\net10.0-windows10.0.19041.0\`
  replace `SmartStudyData.db` with `manual-backup-pre-epic1\SmartStudyData.db`; don't relaunch until we've
  talked. (Unlikely to be needed — this run creates no migration.)
- **Repeat crash on create:** 99% of the time = stale exe. Re-verify Step 0.4, rebuild, retry once.

---

## What I need back (single summary you can paste)

```
Step 0  build: 0 errors? branch/HEAD:
Step 1  1a manual create: PASS/FAIL   1b smart-add: PASS/FAIL   1c persists after restart: PASS/FAIL
        (if any FAIL) crash.log newest entry + dialog text + exe timestamp:
Step 2  heatmap "A" collapses to dim cell: yes/no
Step 3  Lưu Tiến Trình on Dashboard → two success dialogs: yes/no
Step 4  F2: waive / veto (+reason)
Step 5  residual risk acknowledged: yes
Step 6  B4 decision: Release / Reopen (+reason)
```

Anything outside these steps (negation heuristic, blank balancer, heatmap bucket look, off-Dashboard
button) is a **known out-of-scope** item — note it if you like, but it does not block the B4 decision.
