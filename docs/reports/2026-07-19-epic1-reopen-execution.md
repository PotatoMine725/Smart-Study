# Epic 1 Reopen — Execution Report (Slices R1 + R2)

**Date:** 2026-07-19
**Plan:** [`../plans/2026-07-19-epic1-reopen-fix-plan.md`](../plans/2026-07-19-epic1-reopen-fix-plan.md)
**Branch:** `reopen/fk-fix` → merged to `ui_rf` as **`37f9678`** (`--no-ff`)
**Result:** 336 tests pass (331 pre-existing + 5 new), build 0 errors, warnings at the 96 baseline.
**Status:** agent-side work complete. **Owner re-closure re-run is the only remaining gate item.**

---

## 1. What shipped

| Slice | Change | Commits |
|---|---|---|
| R1-A | `QuanLyTaskViewModel.ThemTask` stamps `MaMonHoc` via object initializer (D-R1); no `StudyTask` ctor change | `3bb56c6` |
| R1-B | `SqliteHocKyRepository.LuuHocKyAsync` reconcile: normalize pass heals `Guid.Empty` FK from navigation position before the FK-keyed diff; owner lookup becomes `FirstOrDefault ?? throw` with task/FK/`HocKy` context (D-R2) | `63b9611` |
| R2-A | `Services/CrashLogger` — minimal last-resort fault sink (`%AppData%\SmartStudyPlanner\crash.log`, path overridable for tests) + 3 tests (D-R4) | `b0061e7` |
| R2-B | `DispatcherUnhandledException` / `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` wired at the top of `OnStartup`; the 3 waived fire-and-forget telemetry sites now observed (F2 nuance, D-R5) | `c18e1e7` |
| docs | CHANGELOG rows + roadmap §A.4 backlog entry | `eb87623` |

Plan Status was flipped `draft → approved` by the owner before any code started (`81c47ae`), satisfying
the plan's hard constraint.

## 2. Verification evidence

### RED steps (acceptance gate 1)

Both R1 RED steps failed with `InvalidOperationException: Sequence contains no matching element`
originating at `SqliteHocKyRepository.cs:184` — the `.First()` reconcile owner lookup. **Origin was
proven by stack frame, not by message**, because a rolled-back `SingleAsync` throws the near-identical
`Sequence contains no elements`. R2's RED was a build failure (`CS0103: The name 'CrashLogger' does not
exist`), which is the correct RED for a not-yet-existing type.

### Independent PM re-verification (not delegated)

Agent R1 had to add one line to the R1-B test — `MucDoCanhBao = "An toàn"` — after the FK heal worked
and the test then failed downstream on `SQLite Error 19: NOT NULL constraint failed`. Because that line
edits the test's setup, the PM re-verified the claim that it does not mask the FK failure: reverting
**only** the repository fix (via `git checkout ui_rf -- <file>`, not `git stash`) and re-running the
**final** test body still REDs at line 184 on the FK. The claim holds. The shared stash stack was
confirmed empty afterwards — relevant because this repo has multiple live worktrees.

### Fixture-bias check (the reason this bug escaped 331 green tests)

`MaMonHoc` appears in `QuanLyTaskViewModelTests.cs` **only in comments and the assertion** — never
assigned in the acted-on path. The R1-B test sets only `MucDoCanhBao`, leaving the FK `Guid.Empty`.
Verified by reading the diff, not by trusting the report.

### Other checks

- Vietnamese crash dialog string: **strict UTF-8, BOM-less, diacritics intact** (byte-level decode, not
  visual inspection) — this repo has a documented history of encoding corruption.
- Only **one** `catch` changed in `App.xaml.cs` (the Maturation block). The two ML warm-up silent
  catches are untouched, as the plan required.
- No `Co-Authored-By` trailer on any commit.
- Post-merge re-verification on the merged `ui_rf`: build 0 errors, 336 pass.

## 3. Acceptance gates

| # | Gate | Status |
|---|---|---|
| 1 | Both RED steps failed as predicted | ✅ verified independently |
| 2 | Suite green at every commit, final ≈336 | ✅ 336 (331 + 5) |
| 3 | No file outside agents' Write scopes | ✅ 10 files, all authorized (see D5 for the tooling substitution) |
| 4 | Owner re-run passes B1.4 + retests #2/#3 | ⏳ owner |

## 4. Coverage of the owner's B1.4 scenario

The owner re-run exercises create-task via **manual entry and quick-input smart add**; only the manual
path has an automated test. Quick-input is nonetheless covered, verified rather than assumed:

- `PhanTichNhapNhanh()` (the quick-input command) is `private void` and only *populates* `TenTask`,
  `HanChot`, `LoaiTaskIndex`, `DoKho`. It never persists. The user then presses the add button, which
  runs `ThemTask()` — the exact path R1-A fixes and the new test drives.
- R1-B is a universal backstop independent of entry mode: R1's impact analysis showed **all 16 save
  paths funnel through `LuuHocKyAsync`**, and the heal fires on any `Guid.Empty` FK reachable in the
  `hocKy → mon → task` navigation graph. Any task that persists is therefore either healed or fails
  with a loud contextual error — never the silent process death.

## 5. Findings logged, deliberately not fixed

1. **The two fix layers overlap.** After R1-B landed, reverting *only* the VM fix leaves the VM-level
   test green — the repository heal masks it. Verified empirically. This is defense-in-depth working as
   designed (D-R1 and D-R2 each independently prevent the crash), and gate 1 requires RED *before the
   fix*, which R1-A satisfied at commit time since it predates R1-B. See D3 for why no test was added.
2. **`StudyTask.MucDoCanhBao` has the same latent shape as the bug just fixed** — not stamped by the
   4-arg constructor, `NOT NULL` in schema, safe today only because `TinhDiemVaSapXep()` stamps it
   before every ViewModel save. Any non-ViewModel persistence path would hit the NOT NULL constraint.
   Recorded in [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.4. **No call-site survey was
   performed** — this is a known-unknown, not a cleared risk.

## 6. Residual risk

- **Global exception handlers are not unit-live-fired.** No unit test can drive WPF `Application`
  handlers headlessly. Coverage is `CrashLoggerTests` (behavior) + PM diff review (wiring) + the owner's
  real launch. This was anticipated by the plan and is carried into the re-closure checklist.
- **`ui_rf` is merged locally but not pushed** — pushing remains assigned elsewhere.
- If anything misbehaves during the re-run, there is now a trace at
  `%AppData%\SmartStudyPlanner\crash.log`. That file is the entire point of R2.

---

## Decisions made (ADR-style)

### D1 — Worktree created with `git worktree add`, not the native `EnterWorktree` tool
- **Why:** the plan requires branching off `ui_rf`. `EnterWorktree` derives its base from the
  `worktree.baseRef` setting, which is unset and therefore defaults to `origin/<default-branch>` —
  `origin/main`. No setting existed to redirect it, and adding one would mutate user config for a
  one-off.
- **What for:** correct base ref with no config side effects. Placed at `D:/Code/C#/ssp-reopen-fk-fix`
  (sibling, matching the existing `ssp-merge` convention) rather than inside the repo, which avoids a
  `.gitignore` change; entered afterwards via `EnterWorktree {path}`, which is explicitly supported.
- **Experience:** the superpowers worktree skill says prefer native tooling, and that is right by
  default — but a native tool that cannot honor a hard plan constraint is the wrong tool. Verify the
  base ref before trusting worktree automation.

### D2 — PM/QA review performed in-session, not by reviewer subagents
- **Why:** the plan assigns PM/QA to the orchestrating session, and the owner confirmed that reading
  explicitly. The subagent-driven-development skill prescribes two reviewer subagents per task; user
  instruction outranks skill default.
- **What for:** one reviewer holding the full plan context across both slices, rather than four
  cold-start reviewers re-deriving it.
- **Experience:** reviewing in-session made the cross-slice finding in §5.1 visible — a per-task
  reviewer would not have tested how R1-B changes the meaning of R1-A's test.

### D3 — No discriminating VM-level test added despite the layer overlap
- **Why:** the only regression such a test would catch is "VM stamp removed, repo heal intact," which
  produces **no crash and correct data**. The heal is itself tested and independently RED-verified.
  Gate 1's requirement (RED before the fix) was already satisfied at R1-A's commit time.
- **What for:** keeps the reopen minimal, as the owner mandate required. R1's write scope was closed and
  committed; adding a fake-repo test afterwards is scope expansion on an approved plan.
- **Experience:** "the test no longer fails when I break this" is worth *investigating* every time, but
  the response should be calibrated — here it was evidence the design works, not that coverage is
  missing. Log, don't gate.

### D4 — Accepted R2's two deviations from the plan's literal code, after validating each
- **Why:** the plan's `MessageBox.Show` does not compile — `<UseWindowsForms>true</UseWindowsForms>` in
  the csproj makes it `CS0104`-ambiguous with WinForms; fully qualifying to `System.Windows.MessageBox`
  was forced. Separately, the plan dropped the `_ =` discard the original lines carried, raising a new
  `CS4014` in the async `ThemTask`; restoring it matches the file's own convention.
- **What for:** compiles clean and returns the warning count to its exact 96 baseline, so no new
  warnings hide in the noise.
- **Experience:** "it had to compile" is where scope quietly creeps, so each deviation was checked
  against a primary source (the csproj, the surrounding code) rather than accepted on the agent's
  explanation. Both survived; neither changed behavior.

### D5 — `git diff` substituted for `gitnexus_detect_changes()` in acceptance gate 3
- **Why:** the GitNexus index is bound to the main checkout and is blind to linked worktrees — it
  reported the main repo's unrelated dirty files and saw none of the branch's changes. Re-pointing or
  re-indexing it mid-slice would mutate state shared with other worktrees.
- **What for:** `git diff --stat ui_rf...HEAD` is authoritative for file-level scope and satisfies the
  gate's intent; the complete 244-line diff was additionally read by the PM.
- **Experience:** worth knowing before the next worktree-based plan — any plan step naming
  `gitnexus_detect_changes` as a gate needs this substitution written in, or the gate reads as failed
  when it is merely inapplicable.

### D6 — Sequential dispatch honored; CHANGELOG and roadmap committed on the branch before merging
- **Why:** the plan's parallel-dispatch decision is explicit (R1 and R2 both edit
  `QuanLyTaskViewModel.cs`). Committing the docs on the branch first means `ui_rf` receives code and
  record in a single merge rather than a code merge followed by a loose docs commit.
- **What for:** `ui_rf` is never in a state where the fix is present but unrecorded.
- **Experience:** `--no-ff` was chosen so the reopen has one citable SHA, matching how this CHANGELOG
  already references prior work ("merged `8740350`").

---

## Handoff — owner re-closure checklist

- [ ] B1.4 create-task end-to-end, **manual entry and quick-input smart add** — no crash; task visible
      after restart under the correct subject.
- [ ] Retest #2 — heatmap, subject named "A" (expected: the single-dim-cell observation was a crash artifact).
- [ ] Retest #3 — Lưu Tiến Trình on Dashboard ×2 → two success dialogs (same).
- [ ] Formal F2 waiver sign-off (D-R5).
- [ ] Acknowledge residual risk: global handlers are review- and launch-verified, not unit-live-fired.
- [ ] Record the **B4 re-decision** in the gate doc; if Release → Phase 3 (C1–C3).

Estimated owner time: ~30 minutes.
