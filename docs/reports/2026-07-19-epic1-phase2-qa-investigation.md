# Epic 1 Phase 2 — QA investigation of owner GUI-test observations

**Date:** 2026-07-19
**Author:** QA (Claude), read-only investigation — no code changes made
**Inputs:** `docs/reports/2026-07-15-GUI-test-observations.md` (owner's B1–B4 results), `docs/plans/2026-07-15-epic1-phase2-owner-runbook.md`, Windows Application event log (crash records 2026-07-15), git history on `ui_rf`, source at HEAD `cdc09ee`+local
**Method:** systematic-debugging discipline; 3 parallel read-only Explore agents + direct evidence capture (event log, DB sidecar files, git archaeology). Every claim below carries file:line or log evidence.

> **Status update (2026-07-19, post-owner-review):** the owner accepted this investigation in full
> ([`../specs/2026-07-19-owner-epic-1-decisions.md`](../specs/2026-07-19-owner-epic-1-decisions.md)) —
> §4 classification approved, §6 sequence accepted, and §5 question 1 **closed: B3.2 passed,
> expected behavior met** (the duplicate warning worked; §2.5's artifact explanation stands).
> Lessons distilled to [`../knowledge/incident-investigation.md`](../knowledge/incident-investigation.md);
> fix planning authorized after distillation — see
> [`../plans/2026-07-19-epic1-reopen-fix-plan.md`](../plans/2026-07-19-epic1-reopen-fix-plan.md).
> Remaining §5 retests (#2 heatmap, #3 Lưu Tiến Trình) fold into the re-closure gate re-run.

---

## 1. Executive verdict

Owner's B4 call was **Reopen Epic 1**. QA **concurs**. Exactly one Reopen-grade defect exists — a regression introduced by M1.2 — and it fully explains the B1.4 crash. Every other observation resolved to either a pre-existing design gap, a data/perception artifact, or a runbook error on QA's side.

| # | Observation (owner) | Verdict | Class |
|---|---|---|---|
| 1 | B1.4: app "Not responding" ~30s then crashes on smart-add task create | **CONFIRMED BUG — regression from M1.2** (Reopen-grade) | P0 |
| 2 | B1.4: smart add misreads Vietnamese negation ("không dễ" → easy) | **CONFIRMED — pre-existing design gap**, not a regression | P1 |
| 3 | B1: Cân Bằng Tải shows no arrangements | **Works-as-designed with stale seeded data** + empty-state UX gap | P1 (UX) |
| 4 | B1: Trọng Số AI is a window, "I remember a page" | **Expectation mismatch** — it has been a window since Slice 8 (2026-06-06); only modal→non-modal changed | No action / P2 |
| 5 | B3.2: exact-duplicate subject warning "Failed" | **Code correct; observation artifact** — DB proves no duplicate persisted | Needs owner answer |
| 6 | B3.4: no "Lưu học kỳ" button found | **Runbook error (QA's)** — no such button exists; save is implicit. "Lưu Tiến Trình" silently no-ops off-Dashboard | P2 (UX) |
| 7 | B3.5: heatmap doesn't re-render on filter change | **Perceptual/data artifact — wiring is correct**; bucket saturation makes rebuilds look identical | P2 (UX) + retest |
| 8 | B3.7: "TWO .bak" files (WAL/SHM) | **Benign** — SQLite sidecars from the read-only verify script, 0 B / 32 KB, not backups | No action |
| 9 | B3.6/B3.8: focus session + DB deltas | **PASS** — StudyLogs 5402→5403, tombstoned 1, `DeviceId='desktop-49b42d8f'` is the intended format (`DeviceHelper.cs:12`) | Closed |

---

## 2. Finding detail

### 2.1 P0 — Task-create crash: MaMonHoc = Guid.Empty vs. M1.2 reconcile (regression)

**Symptom:** creating any task (smart add *or* manual — same path) freezes the app ~30 s, then it dies. Owner hit it repeatedly; Windows Application event log records **5 identical .NET crashes 2026-07-15 18:23–18:39 (+07)**: `InvalidOperationException` at `SqliteHocKyRepository.LuuHocKyAsync`.

**Causal chain (each link verified):**
1. `StudyTask` 4-arg constructor sets MaTask/TenTask/HanChot/LoaiTask/DoKho/TrangThai but **never `MaMonHoc`** → stays `Guid.Empty` (`SmartStudyPlanner\Models\StudyTask.cs:45-53`).
2. `QuanLyTaskViewModel.ThemTask()` constructs the task, adds it to `MonHocHienTai.DanhSachTask` **without stamping the FK**, then calls `LuuHocKyAsync` (`ViewModels\QuanLyTaskViewModel.cs:192-193, :212`). No try/catch.
3. M1.2's in-place reconcile (commit `6734177`, 2026-07-05) resolves each new task's owner subject **by scalar FK**: `hocKyCu.DanhSachMonHoc.First(m => m.MaMonHoc == newTask.MaMonHoc)` (`Infrastructure\Persistence\SQLite\Repositories\SqliteHocKyRepository.cs:184`). With `Guid.Empty` no subject matches → `First()` throws `InvalidOperationException`.
4. The app has **no global exception handler** (no `DispatcherUnhandledException`, `AppDomain.UnhandledException`, or `UnobservedTaskException` anywhere) and `AsyncRelayCommand` rethrows on the dispatcher → process dies. The "~30 s Not responding" is Windows Error Reporting collecting the crash dump, not a hang.

**Why it's a regression:** pre-M1.2 (`946799b`), save was remove-then-recreate (`db.HocKys.Remove` → re-add whole graph); EF's graph fixup derived the FK from navigation position, silently healing the empty FK. M1.2 switched to FK-based matching without adding the stamp at the source.

**Why 331 tests missed it:** every `new StudyTask(...)` in test fixtures explicitly sets `MaMonHoc = monHoc.MaMonHoc` in the initializer (`SmartStudyPlanner.Tests\Fixtures\TestDb.cs:36-38` and ~10 sites in `RepositoriesTests.cs`) — the suite tests a contract the ViewModel doesn't honor. And no GUI run was possible between M1.2 (07-05) and B1 (07-15) because the gate itself forbade launching the app.

**Why it looked smart-add-specific:** it isn't. `ThemTask` is the **sole** create path for both manual and smart add ("Tự điền" only pre-fills the form; the crash happens on "Lưu Deadline"). Edit/delete of *existing* tasks carry real FKs — which is why B3.7 (delete + restart) passed.

### 2.2 P1 — Vietnamese negation hole in smart-add difficulty heuristic (pre-existing)

Difficulty rules are bare substring matches with no negation awareness: `ContainsAnyRule<int>(5, "khó","kho","căng","chết")`, `ContainsAnyRule<int>(1, "dễ","de","chill","nhàn","ez")` (`Services\Strategies\IDifficultyKeywordParser.cs:13-17`), matcher = plain `lowerInput.Contains(k)` (`IKeywordRule.cs:28-40`). No token for "không / chẳng / không hề / chẳng hề / đéo" exists anywhere in the parsing layer. The ML classifier cannot rescue it — it hard-codes `DoKho = null` (`Services\ML\TextClassifierService.cs:35`), so the heuristic always wins. "btvn ngày mai không dễ đâu" → difficulty 1 (easy), exactly as the owner reported. No negation test exists; one written today would fail. This predates Epic 1 (Slice 6 parser design), so it is a gap, not a regression.

### 2.3 P1 (UX) — Cân Bằng Tải blank: overdue seeded tasks are zero-priority by design

The balancer receives the **same in-memory HocKy instance** the Dashboard and Môn Học pages render from (`Views\MainWindow.xaml.cs:196-202, :54-59`), so the data reaches it — that alternative is evidence-ruled-out. The pipeline then excludes every task:

- `OverdueRule`: deadline **>3 days past → priority 0**, short-circuits (`Services\Strategies\IUrgencyRule.cs:15`); completed tasks also 0.
- Priority ≤0 → suggested minutes 0 (`Core\Scheduling\Engines\RawMinutesCalculator.cs:11`) → task skipped (`Services\WorkloadServiceImpl.cs:69-70`).
- The ViewModel then drops empty days entirely (`ViewModels\WorkloadBalancerViewModel.cs:42`), turning "no arrangements" into a fully blank page with no explanation.

The seeded tasks were minted with `hanChot = seedDate + 30 days` (seeder shape: `SmartStudyPlanner.Tests\Data\DbSeedTests.cs:53`) — long overdue by the 2026-07-15 run. Corroboration that this is complete, not partial: any *pending* future task would render (BeyondHorizonRule → 1.0) and any 0–3-day-overdue task would render (JustOverdueRule → 100); a **totally** blank board is only reachable when every task is >3 days overdue or completed. **Verdict: not a code defect**, but two follow-ups are warranted: (a) an empty-state message instead of a silent blank page; (b) an owner decision on whether ">3 days overdue = invisible" is the desired product behavior.

### 2.4 P2 — Analytics heatmap "not re-rendering": bucket saturation, wiring is correct

- **Only the Analytics page has a heatmap** (`Views\AnalyticsPage.xaml:187`, `ItemsSource="{Binding HeatmapCells}"`, 7×52 UniformGrid). The Dashboard has none — an earlier investigation direction targeting `DashboardViewModel.HeatmapCells` was the wrong page; that property does not exist.
- Wiring: both filter handlers call `ApplyFilters()` (`ViewModels\AnalyticsViewModel.cs:108-109`), which synchronously rebuilds *everything* including `BuildHeatmap` (`:171`), and `BuildHeatmap` **replaces** the `[ObservableProperty]` collection (`:226`) — change notification fires, identical in mechanism to the charts. No async, no try/catch that could swallow a fault on this path. Unchanged since 2026-06-25 (`2f0e51e`).
- Why it *looks* frozen: bucket levels are 0 / ≤30 / ≤60 / ≤120 / **>120 min-per-day = level 4 darkest green** (`AnalyticsViewModel.cs:218-222`, `Converters\HeatLevelToBrushConverter.cs:11`). The ~5402 seeded logs are back-dated over only ~60 days (~90 logs/day, 20–240 min each), so **every populated day is pinned at level 4 for every subject selection**, and ~44 of the 52 columns are always empty. The rebuild genuinely happens; the pixels just come out identical. Meanwhile the weekly-minutes bar chart shows *raw* sums (not bucketed) and is subject-sensitive, so it visibly changes — matching the owner's "charts re-render, heatmap doesn't."
- Two adjacent (minor) findings from the same trace: the subject-completion chart is computed from **task status** and doesn't respond to either filter (`Services\...\StudyAnalyticsService` `ComputeSubjectInsights`), and the weekly chart always shows the **last 7 days** regardless of the range filter (`ComputeWeeklyMinutes`). Worth an owner decision on intended semantics, but cosmetic.
- **Discriminating retest:** filter to subject **"A"** (it has exactly one real focus-session log and no seed logs). Correctly wired ⇒ the heatmap collapses from the dark-green block to a single dim cell. If it does NOT collapse, only then suspect a stale build of the tested exe.

### 2.5 B3.2 exact-duplicate subject — code correct, observation unexplained

Exact and normalized inputs take the **identical** code branch (normalize → compare → `OnThongBao` → skip add; `ViewModels\QuanLyMonHocViewModel.cs:114-119`, `Models\MonHocIdentity.cs:16-23`), and B3.3 (normalized variant) passed — so the mechanism works. The B3.8 DB dump proves **no duplicate was ever persisted** (3 subjects until the owner added A1/B; normalized-dup check PASS). Two candidate explanations for the perceived failure: (a) the app was already wedged/crashing from finding 2.1 so the modal warning never got seen; (b) **Tín chỉ (credits) left blank** — a blank name *or credits* triggers a silent early-return *before* the dup check (`QuanLyMonHocViewModel.cs:104-106`), so nothing happens at all: no warning, no add. Questions for owner in §5.

### 2.6 B3.4 "Lưu học kỳ" — runbook error (QA's), plus one real UX finding

There is no "Lưu học kỳ" button; the runbook step was mis-specified — QA's error, logged as such. `LuuHocKyAsync` fires implicitly from 8 call sites (setup save, subject add/delete, task add/edit/delete, dashboard save, focus-mode exit). The real finding the owner surfaced: **"Lưu Tiến Trình" silently does nothing unless the Dashboard page is showing** — the click handler routes to `DashboardViewModel.LuuDuLieuCommand` only `if (MainFrame.Content is DashboardPage)` (`Views\MainWindow.xaml.cs:230-236`); on any other page it's a genuine silent no-op, which is exactly the "not responsive" the owner described. Correct retest: on the **Dashboard**, click it twice → two "Đã lưu tiến trình thành công!" dialogs.

### 2.7 Settled small items

- **Trọng Số AI:** `WeightOptimizerWindow` has been a separate window since its first commit `c9b4724` (Slice 8, 2026-06-06); full-history sweep found no page version ever. Only change since: modal `ShowDialog()` → non-modal `Show()`. Expectation mismatch, not a regression.
- **"TWO .bak":** `SmartStudyData.20260715-112034.bak.db-wal` (0 B) and `-shm` (32 KB) are SQLite sidecars created by the verify script's read-only opens of the WAL-mode backup — harmless, deletable, not backups. The real backup remains the single 1,310,720 B `.bak.db`, verified ALL PASS.
- **A6 evidence:** `DeviceId='desktop-49b42d8f'` matches the intended format `"desktop-" + first-8-hex-of-hash` (`Services\ML\DeviceHelper.cs:12`). The runbook's `<real-guid>` phrasing was imprecise; the check passes. B3.6/B3.8 pass via the deltas (5403 logs, 1 tombstone, 5 live subjects, no normalized dups).

### 2.8 Latent hardening findings (out of Phase-2 scope, recorded for the fix cycle)

1. **No global exception handler** — any unhandled exception kills the process with no dialog and no log. This turned a recoverable bug into five hard crashes.
2. `SchedulingOrchestrator.cs:78` uses `.GetAwaiter().GetResult()` on the Dashboard path (sync-over-async).
3. `App.xaml.cs` `OnStartup` is `async void` with three fire-and-forget startup jobs.
4. The crash path is untestable with the current in-memory fakes; a file-based SQLite test (feasible per `TestDb.cs`) would have caught 2.1 and should be part of the fix.

---

## 3. F2 waiver — plain-language explanation (owner requested before deciding)

**What F2 is.** Three telemetry writes run fire-and-forget — started but never awaited, results discarded:

1. `OutcomeMaturationService.MatureAsync` (startup) — matures past predictions into ML training outcomes.
2. `LogDifficultyLabelAsync` (after task create) — records the difficulty label you accepted, as a future training example.
3. `LogWeightChangeAsync` (weight optimizer) — records weight-tuning history.

**What can go wrong.** If the app exits before one completes, or a write throws, that row is silently lost. Nobody is notified; nothing retries. Since .NET's unobserved-task behavior does not crash the process, the *only* consequence is a missing telemetry row.

**What is NOT at risk.** Your actual study data — semesters, subjects, tasks, logs, streaks — is written on awaited paths with error propagation. F2 cannot lose or corrupt user data. The lost rows only thin out future ML training samples marginally; the ML layer is statistical and already tolerates missing samples.

**Waiver granted means:** Epic 1 closes without changing these three call sites; the design is accepted as intentionally loss-tolerant. **Waiver declined means:** a small hardening task enters the reopen fix cycle (observe the tasks' exceptions and log failures — roughly an hour of work; full await-ing is not recommended since it would block the UI for zero user benefit).

**QA recommendation:** grant the waiver, with one nuance — when the global exception handler from §2.8-1 is added, attach a cheap `ContinueWith`-on-fault logger to these three calls so losses at least leave a trace. That captures ~all the value of declining at ~none of the cost. The decision can also simply ride along to the re-closure gate, since B4 = Reopen anyway.

---

## 4. B4 QA verdict and reopen scope

**Verdict: Reopen Epic 1 — concur with owner.** Sole reopen driver: finding 2.1 (M1.2 FK regression). Proposed severity ranking for the fix cycle (ranking only — implementation planning is deliberately not started, per owner's "investigate before planning"):

| Pri | Item | Source |
|---|---|---|
| P0 | Stamp `MaMonHoc` at task creation + defensive handling in reconcile + file-based SQLite regression test | 2.1 |
| P0-adj | Global exception handler (dialog + log instead of process death) | 2.8-1 |
| P1 | Vietnamese negation handling in difficulty heuristic + tests | 2.2 |
| P1 | Balancer empty-state message; owner decision on overdue-task visibility | 2.3 |
| P2 | "Lưu Tiến Trình" off-Dashboard: disable or make global | 2.6 |
| P2 | Heatmap bucket scale/legend; chart filter-semantics decisions | 2.4 |
| — | Feature requests (Học ngay everywhere, Toàn bộ task page, theme persistence) → "UI fidelity + mobile-ready polish" PROPOSED row (kept as proposed, per owner) | owner report |

## 5. Questions / retests for the owner

1. **B3.2:** Right after typing exact `Toán Rời Rạc` and confirming — did the subject list show 3 or 4 entries? Did any "Trùng môn học" dialog appear? Was **Tín chỉ** filled in at the time?
2. **Heatmap:** Filter Analytics to subject **"A"** — does the heatmap collapse to a single dim cell? (Expected: yes ⇒ confirms §2.4, closes the item.)
3. **B3.4 retest:** On the **Dashboard**, click "Lưu Tiến Trình" twice → expect two success dialogs, no errors.

## 6. Next-epic recommendation (C3 prep input)

1. **First, the Epic 1 reopen fix cycle** (P0 + P0-adj at minimum) with a re-run of the failed runbook steps — this is also the owner's stated priority ("prioritize fixing mentioned problems in B1 and B3").
2. **Then Epic 3 (SOE) as the next epic**, per the master-plan order E1→E3→E2→E4 — with one gate: **G2 (SOE pass semantics) is still OPEN and blocks M3.2**, so resolving G2 must be the first milestone of Epic 3 prep, before any SOE implementation. If the owner is not ready to decide G2, the fallback is pulling Epic 2 forward; QA does not recommend that inversion unless G2 stays undecidable, because the master plan sequenced E3 first deliberately.
3. The three feature requests stay in the polish row and should not jump the queue.

---

## Decisions made

- **Root causes were confirmed against external evidence (Windows event log, DB dumps, git history) rather than by running the app.** *Why:* the gate reserves GUI runs for the owner, and reproduce-before-escalate requires baseline evidence, not inference. *What for:* every Reopen-grade claim in this report is backed by a crash record or file:line chain, so the fix cycle can start without re-litigating diagnosis. *Experience:* the event log turned a "maybe a hang?" report into a precise exception + frame in minutes — worth making it a standard QA step.
- **The balancer and heatmap observations were classified as data/perception artifacts, not defects, and are closed with discriminating retests instead of code changes.** *Why:* the wiring was verified correct with file:line evidence, and the blank/frozen appearance is fully explained by stale seeded data (overdue deadlines; saturated buckets). *What for:* keeps the reopen scope minimal — only 2.1 forces the reopen — and prevents the fix cycle from "fixing" working code. *Experience:* both artifacts exist because seeded ML data ages badly; any future GUI test should either reseed relative to the run date or use a fresh profile.
- **Fix planning was deliberately not started.** *Why:* owner instructed "investigate before planning." *What for:* the owner can now approve/trim the §4 ranking before any plan is written; the plan will go to `docs/plans/` with per-agent task cards per convention. *Experience:* separating verdict from plan kept this report decision-ready instead of solution-biased.
- **B3.4 is recorded as a QA runbook error, not an app bug.** *Why:* the runbook referenced a button that never existed; the owner's confusion was induced by QA. *What for:* honest attribution keeps the reopen scope clean and flags runbook review as part of the re-closure gate. *Experience:* runbook steps must be written against the actual UI, not against repository semantics (implicit saves ≠ a save button).
