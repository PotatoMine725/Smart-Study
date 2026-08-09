# Epic 3 — Convergence Plan

**Date:** 2026-08-07 · **Branch:** `worktree-epic-3-soe` @ `82155d9` (merge-base = `dev` HEAD `1a5ad7d`, fast-forwards cleanly)
**Scope approved by owner:** Mandatory + Optional tiers of [`2026-08-07-epic3-owner-triage.md`](../review/2026-08-07-epic3-owner-triage.md)

> **Two copies exist by design.** The canonical copy lives on the Epic 3 worktree branch alongside the
> documents it amends; a reference copy sits in the prod repo working tree so the plan is readable
> without checking out the branch. The prod copy's `../review/2026-08-07-epic3-*` links resolve only
> after the branch merges to `dev`.

> **Status: EXECUTED 2026-08-07.** Owner approved. Four commits on `worktree-epic-3-soe`:
> `c0ec38a` (WP-1 + WP-2 — closing note, roadmap) · `d0fc968` (WP-3 — T3.9 note) ·
> `881f498` (commit the review documents WP-1 cites; fix an inexact quotation) ·
> `8cf53da` (owner-directed: correct F1, flip DoD-5 to ✅).
> Zero `.cs`, zero test files, frozen baseline untouched, no ratified decision amended.
> Suite re-verified after every stage: **470 passed / 0 failed / 1 skipped / 471 total**, unchanged.
> Every checklist item in §3 holds. §5 item 5 resolved during execution — see the entry.
>
> **Three deviations from the plan as written**, all recorded in §2's WP-1 notes: seven edits to the
> closing note instead of five; the three review documents committed rather than left untracked
> (WP-1's text cites the verdict, so leaving it untracked would have shipped a dangling citation —
> the same defect class this plan exists to fix); and one finding the plan did not anticipate —
> the closing note's F1 and DoD-5 ❌ were themselves false, corrected on the owner's instruction.

---

## Context

Why this work exists, in one paragraph.

The [independent closure verdict](../review/2026-08-07-epic3-closure-verdict.md) found Epic 3's **engineering** sound — every reported number reproduced independently, all 13 acceptance criteria hold, D-J verified structurally in code — but its **closing documentation materially understates what shipped**. T3.3 changed allocator placement from least-loaded to earliest-day-with-room; the deadline clause it added is *provably output-inert* (proved in [`2026-08-06-deadline-tier-provably-inert.md`](2026-08-06-deadline-tier-provably-inert.md), and disclosed in the production code comment and the test class doc). That fact is absent from the closing note — DoD-7's actual deliverable — and has already propagated into `system_roadmap.md` §A.3 as the claim "deadline-aware allocator rework (T3.3)", which is not true in any output-affecting sense.

**Intended outcome:** the Epic 3 documentation set describes the system that actually exists. Nothing else changes.

**This is a convergence pass, not an improvement pass.** No code, no tests, no behaviour, no ratified decision reopened (CP-2, CP-3, G2, G3, D7 all stand). The underlying limitation is *accepted* — A1 was named as a falsifiable assumption in the execution plan §3.4, ratified at CP-3, and ruled on at D7 with the numbers in view. This plan fixes the *disclosure*, not the allocator.

---

## 1. Convergence Summary

**Objective:** bring Epic 3 to the Engineering Quality Gate by making three documents accurate.

- **7 edits · 3 files · 2 commits · documentation only.**
- Zero re-verification required — every number to be written is already re-derived on this branch in verdict §1.
- Expected suite state before and after: **470 passed / 0 failed / 1 skipped / 471 total**, unchanged.
- Estimated effort: **under one hour**, dominated by writing prose, not by verification.

| WP | File | Edits | Commit |
|---|---|---|---|
| WP-1 | `docs/reports/2026-08-07-epic3-closing-note.md` | 5 | 1 |
| WP-2 | `docs/specs/system_roadmap.md` | 1 | 1 |
| WP-3 | `docs/plans/2026-08-06-t3.9-optimize-stage-list-design.md` | 1 | 2 |

**Two commits, not one.** Slightly against the "fewest commits" principle, and deliberately: WP-1+WP-2 are one concern (*Epic 3's closure documents state what shipped*) and constitute the gate; WP-3 is a different document with a different audience (a future stage-2 implementer) and is in the optional tier. Separating them gives a clean rollback boundary — if the owner later disputes the `N=1` framing, reverting commit 2 leaves the gate intact. Collapsing to a single commit is acceptable if preferred; nothing else in this plan changes.

---

## 2. Ordered Work Packages

Execution order is **WP-1 → WP-2 → WP-3**. The WP-1→WP-2 order is a real dependency, not a preference: the roadmap's replacement text cites the closing note, so the closing note must already carry the statement being cited.

---

### WP-1 — Closing note: state what actually shipped

**Objective.** Make DoD-7's deliverable complete and accurate, so a reader deciding ship-readiness reaches the correct conclusion about the delivered capability.

**Files likely affected.** `docs/reports/2026-08-07-epic3-closing-note.md` (only).

**Dependencies.** None. This is the root of the chain.

**Implementation notes.** Five edits, all additive or wording-level. No table restructuring, no renumbering of existing findings.

1. **(F-A.1a) Extend the "Evidence-scoping statement"** (currently lines 21–32). It today discloses only that `ScheduleOptimizer`/`SoeWeights` have zero production call sites. Add a second paragraph stating that the allocator's own deadline clause is **provably output-inert**: `GenerateScheduleWithIdentity`'s tier-1 filter cannot disagree with tier-2 on any input, so what T3.3 shipped in production is *chronological earliest-feasible placement*, not deadline-aware placement, and deadline-as-hard-constraint ships entirely unwired. Cite `../plans/2026-08-06-deadline-tier-provably-inert.md`. Target reader takeaway, stated explicitly: *"placement became chronological; the allocator's deadline clause provably cannot change any output; deadline-as-hard-constraint ships entirely unwired."*

2. **(F-A.2) Add finding `F4`** after F3 in the Findings section. Content: `A6CrossCheck_…` emits a harness/production boundary disagreement (harness `>=` vs production `UniformDeadlinePolicy` `>`), **delta 113 chunks / 6 440 minutes**, flagged in-test with "Named as a finding for the closing report" but never reported. Consequence: the frozen baseline's `TotalDHViolationChunks` (974) and `TotalOverdueMinutes` (57 865) are measured on a **stricter** predicate than the ratified production one, so any *future* D-H comparison against that artifact carries the offset. **Must state explicitly that the 250→220 inversion delta is unaffected** — both sides use the same `SoeScheduleMetrics` harness code, so that comparison is apples-to-apples (verdict §1). Severity: INFORMATIONAL, consistent with F2/F3.

3. **(F-A.3) Add a criteria-10–13 block.** Card G's stop condition was "all 13 acceptance criteria in §3.8 demonstrated"; the note reports 1–9. Use **statement form** — result plus a citation to verdict §1 — matching how criteria 1–9 already cite test names rather than pasting console output. The four results, all verified independently:
   - **10** (A6 cross-check) — implemented and passing; 862 chunks / 50 480 min on **both** paths.
   - **11** (bucket ⑤ green) — no bucket ⑤ assertion touched in the diff.
   - **12** (①/② triaged + mutation check) — ① rewritten discriminatingly (a pre-existing tautological assertion was caught and fixed in the process); ② decoupled per the prescribed pattern.
   - **13** (suite ≥ 391, no test deleted) — 470 ≥ 391; zero deletions (one rename, one delete-then-restore under CP-2).

4. **(F-A.4) Reword F3's reason.** F3's *claim* is correct and stays — `Score(B)` is genuinely not in the frozen artifact, because `ObjectiveEvaluator`'s `ContextContinuity`, `SessionQuality` and `FatiguePenalty` all need the full `ScheduledItem` list and Card A captured adjacency-based proxies instead. Only the reason changes: PD-3/R8 forbids *capturing a baseline after* T3.3 changed the allocator; it does **not** forbid deriving further metrics from the same frozen code at the recorded SHA. Replace "would require resurrecting the retired pre-T3.3 allocator, which the frozen-baseline discipline (PD-3/R8) forbids" with wording to the effect of *"not evaluated; recoverable at moderate cost"*, naming the reproduction coordinates already in the artifact: `HeadSha 2d40d95`, `Seed 12345`, `TodayReference 2026-08-03`.

5. **(F-G) Label metric #5's wall-clock figures as varying.** Two observations exist: p95 0.146 ms / max 19.0 ms (closing note) and p95 0.1056 ms / max 20.2 ms (verdict §1). Present as run-to-run variance, matching the baseline artifact's own convention of labelling its wall-clock block as varying. Note the ~13 700× margin against the 2 000 ms budget makes metric #5's verdict robust either way.

**Explicitly do not touch.** `docs/reports/data/2026-08-05-soe-t36-baseline.json` (frozen under PD-3/R8) · any `.cs` file · the G2 note (F-F is out of scope) · D1/D2's existing ADR entries · the owner-ruled D7 disposition, which this note reports but does not relitigate.

**Executed as seven edits, not five (2026-08-07).** Two additions, both required to make the five above internally consistent rather than to widen scope:

- **6 — metric #4's table cell.** Edit 4 corrected F3's reason, but the success-metrics row still read "❌ **Not evaluable** for 90% of the corpus", which a reader of the table alone would take as permanent. Changed to "Not evaluated … (recoverable — see F3)" and scoped "not computable" to "not computable *from the frozen artifact*". Twelve words; without it, WP-1's judgment condition fails for anyone who reads the table and skips the finding.
- **7 — F1 extended.** Edit 1 states that the omission propagated into the roadmap "see F1", but F1 covered only the *staleness* defect (G2 described as open). The roadmap's false-capability claim is a different failure mode — a doc that stated something the code never did, which unlike staleness does not self-correct — so F1 now records both, and the cross-reference in edit 1 is true. Without this, edit 1 pointed at a finding that did not contain what it claimed.

**The three review documents were committed after all (`881f498`).** They were initially left untracked, since this plan's acceptance criteria fix commit 1 at exactly two files and commit 2 at one. That was wrong: WP-1's criteria-10–13 block *cites* `docs/review/2026-08-07-epic3-closure-verdict.md` as the independent re-derivation behind all four rows, so leaving it untracked shipped a citation that resolved in one working tree and nowhere else — the same defect class this plan exists to fix. Committing them breaches no acceptance criterion, which pinned *per-commit* file counts, not the branch total. Repo precedent agrees: the closing note already links the tracked Epic 1 closure verdict. All relative links in all four Epic 3 documents were then verified to resolve to tracked files.

**A finding this plan did not anticipate (`8cf53da`, owner-directed).** The closing note's **F1 and DoD-5 ❌ were themselves false.** F1 reported DoD-5 unmet because the roadmap "still reads *Pass accept/commit semantics still OPEN — implementation blocked on it*" — strings that return zero matches and were already gone at `82155d9~1`, fixed by `d257351` earlier on the same branch. The session report's §4 is titled "Roadmap staleness fix (DoD-5, Finding F1) — **fixed**" and names that commit: two documents from the same session disagreed about whether DoD-5 was met, and neither the drafting pass nor the independent verdict caught it — the verdict *accepted* F1 without testing its premise. Surfaced only when WP-1's edit 7 extended F1 and a post-commit review checked what it was extending. **Generalisable lesson: extending a finding without verifying its premise silently ratifies that premise.** DoD is now 7/7.

**Verification.**
- `rtk git diff --stat` → exactly one file changed.
- Read-back check on the amended evidence-scoping statement (see acceptance criteria — this one is a judgment check, not automatable).
- Deferred to WP-2's verification block: build/test/scope checks run once for the combined commit.

**Acceptance criteria.**
- A reader of the amended evidence-scoping statement **alone** can correctly state all three of: placement is chronological; the deadline clause cannot change output; deadline-as-hard-constraint is unwired.
- F4 exists, carries both numbers (113 / 6 440), and explicitly exempts the 250→220 delta.
- Criteria 10–13 each have a stated result.
- F3 no longer asserts that PD-3/R8 forbids re-derivation.
- Metric #5's figures are presented as varying.
- No existing finding renumbered; no table row deleted.

**Rollback.** `git revert <commit-1-sha>`. Documentation-only, so rollback carries zero behavioural risk and cannot affect the frozen artifact or any test.

---

### WP-2 — Roadmap: remove the false capability claim

**Objective.** Remove the one factually incorrect statement from the canonical document. Highest-consequence edit in this plan; smallest.

**Files likely affected.** `docs/specs/system_roadmap.md` (only) — §A.3, **line 91**, single occurrence (confirmed: `deadline-aware` matches exactly once in the file).

**Dependencies.** **WP-1 must land first** — the replacement text cites the closing note's new statement.

**Implementation notes.** One line. Current text reads `… schedule identity seam (T3.8), deadline-aware allocator rework (T3.3), the Optimize(schedule) → (schedule, report) seam (T3.9) …`. Replace the T3.3 clause with wording to the effect of:

> allocator placement rework (T3.3: least-loaded → earliest-feasible; the deadline clause is present but provably output-inert today — see the closing note)

Preserve the surrounding list structure and the sentence's existing zero-production-call-sites statement, which is already correct and already present at lines 93–95.

**Verification.**
- `rtk grep "deadline-aware" docs/specs/system_roadmap.md` → **0 matches**.
- `rtk git diff --stat` → 2 files total for commit 1 (closing note + roadmap).
- `rtk git status --porcelain` → confirm `AGENTS.md`, `CLAUDE.md` and `.claude/skills/**` remain **unstaged**. Stage by explicit path; never `git add .`.
- `dotnet build SmartStudyPlanner.slnx` → 0 errors.
- `dotnet test --no-build` → **470 / 0 / 1 skipped / 471**. Framing matters: for a docs-only change this run is a **scope** check, not a behaviour check — its value is proving nothing unintended was touched, and it can only fail if the change was not in fact docs-only.
- `gitnexus_detect_changes()` before committing, per repo `CLAUDE.md`.

**Acceptance criteria.**
- A reader of the roadmap line **alone**, without the closing note, does not conclude that deadline-aware placement exists.
- Commit 1 touches exactly two files.
- Suite unchanged at 470 / 0 / 1 / 471.
- Commit message omits the `Co-Authored-By` trailer (repo convention). Suggested: `docs(epic3): state what T3.3 actually shipped in the closing note and roadmap`.

**Rollback.** `git revert <commit-1-sha>` — same commit as WP-1, reverts both together, which is correct since they are one fact in two documents.

---

### WP-3 — T3.9 note: complete the `N = 1` cost paragraph

**Objective.** Ensure a future stage-2 implementer does not read a passing branching-factor test as evidence that G2-1's distinguishing guarantees are exercised.

**Files likely affected.** `docs/plans/2026-08-06-t3.9-optimize-stage-list-design.md` (only) — §4, the existing paragraph **"What `N = 1` costs, honestly"** (lines 340–346).

**Dependencies.** None. Independent of WP-1/WP-2 and separately revertible.

**Implementation notes.** **Smaller than originally scoped.** The note already carries an honest cost paragraph stating that at `N = 1` the prefix-selection machinery "only ever has `C₀` and `C₁` to choose between within a single pass." This is an *extension of that existing paragraph*, not a new section. Three consequences it does not currently name:

1. At `N = 1`, "run-all, commit-best-prefix" is **operationally identical** to per-step accept/reject — the mechanism G2 §2 explicitly rejected. Nothing is wrong: at `N = 1` all candidate mechanisms coincide. Say both halves.
2. `Superseded_ByLaterCheckpoint` (a G2-6 reason code) is **structurally unreachable** at `N = 1`.
3. T3.4's branching-factor assertion `states == passes × N + 1` reduces to `states == passes × 2` — close to tautological in its current form.

Close with the consequence for the reader: whoever adopts Candidate 2 (`N = 2`, already fully specified in §2 and deferred in §8) must know this machinery has **never run in the mode that justifies it**. Do **not** modify §8's ratification, Candidate 2's spec, or §6's open questions.

**Verification.**
- `rtk git diff --stat` → exactly one file, in a plan document.
- No build or test impact — this file is not compiled or referenced by any test. Stating that explicitly rather than running a redundant suite is the honest scope claim here; the combined-commit suite run in WP-2 already covers the branch state.

**Acceptance criteria.**
- §4's cost paragraph names all three consequences.
- The paragraph still reads as the author's own honest accounting, not as an external correction bolted on.
- §8's ratified decisions are byte-identical.
- Commit message omits the `Co-Authored-By` trailer. Suggested: `docs(t3.9): record what N=1 leaves unexercised in the G2 pass machinery`.

**Rollback.** `git revert <commit-2-sha>`. Independent of the gate — reverting this does not reopen commit 1.

---

## 3. Quality Gate Checklist

Epic 3 is converged when **all** of the following hold.

**Gate conditions (from the Mandatory tier):**

- [ ] Closing note's evidence-scoping statement carries the inertness fact and cites the proof note.
- [ ] `rg "deadline-aware" docs/specs/system_roadmap.md` returns **0 matches**; §A.3 describes T3.3 accurately.
- [ ] Closing note carries F4 with the 113-chunk / 6 440-minute delta **and** the explicit exemption for the 250→220 inversion comparison.
- [ ] Closing note reports acceptance criteria 10–13 with stated results.

**Optional-tier conditions (approved into scope):**

- [ ] F3's reason corrected to "not evaluated; recoverable at moderate cost", with reproduction coordinates.
- [ ] Metric #5's wall-clock figures labelled as varying.
- [ ] T3.9 §4 names the three `N = 1` consequences.

**Integrity conditions (must hold throughout):**

- [ ] `dotnet build` → 0 errors; `dotnet test` → **470 / 0 / 1 skipped / 471**, unchanged.
- [ ] Zero `.cs` files modified. Zero test files modified.
- [ ] `docs/reports/data/2026-08-05-soe-t36-baseline.json` untouched (PD-3/R8).
- [ ] No ratified decision amended: CP-2, CP-3, G2 (incl. G2-5's table), G3, D7 all byte-identical.
- [ ] `AGENTS.md`, `CLAUDE.md`, `.claude/skills/**` not staged into either commit.
- [ ] `gitnexus_detect_changes()` run before each commit.
- [ ] Commit messages omit the `Co-Authored-By` trailer.

**Judgment condition (not automatable, and the one that actually matters):**

- [ ] A reader of the amended closing note alone, and a reader of the amended roadmap line alone, both reach the accurate conclusion about what T3.3 delivered. This is the failure the verdict found; a grep cannot confirm it was fixed.

---

## 4. Risks

Remaining after this convergence work completes.

| # | Risk | Severity | Disposition |
|---|---|---|---|
| R1 | **The gate is met by disclosure, not by capability.** Epic 3's motivating defect — the allocator places tasks deadline-blind — is still present in production after this plan. | Accepted, by owner ruling | Ratified: A1 named up front (execution plan §3.4), CP-3 ratified earliest-feasible, D7 ruled with the numbers in view. Stated here so the gate is not mistaken for a fix. |
| R2 | **Metric #4 stays unmeasured on 207/230 (90 %).** The quality floor G2 was chartered to finalize is unverified on most of the corpus. | Low today | F-D deferred. WP-1 edit 4 (F-A.4) is what keeps it recoverable — without it, the gap stays documented as impossible. |
| R3 | **All-zero `w1…w5` passes `SoeWeights.IsValid()`.** Would silently neutralize the quality comparator. | None today, real at wiring | F-E deferred; unreachable with zero production callers. Guard belongs at the real call site. |
| R4 | **G2-5's arm-3 cell still reads "Hard fail, no waiver"** with no pointer to D7, which waived it. | Low | F-F stays a proposal — amending a ratified gate document is a category needing its own authorization. Self-correcting for anyone who reads D7. |
| R5 | **R2, R3 and the newly-recorded `N=1` caveat all become live at the same moment** — SOE wiring — and no document owns that trigger. G3-1 correctly scoped wiring out of Epic 3, which is what leaves the trigger homeless. | Low, structural | After WP-3 the `N=1` item is at least written down. R2 and R3 remain pointed at an unowned trigger. Surfaced, not resolved — assigning that trigger is a scope decision beyond convergence. |
| R6 | **The frozen baseline artifact remains calibrated on a stricter predicate than production** (the 113 / 6 440 delta). | Low | Documented by WP-1 edit 2 rather than corrected — correcting it would require re-capture, which PD-3/R8 forbids. Disclosure is the correct treatment. |

No new risk is introduced by this plan. Every edit is additive or wording-level in a document; the executable surface of the repository is untouched.

---

## 5. Future Work

Intentionally outside convergence scope.

1. **F-D — evaluate metric #4 on the remaining 207 items.** Re-derive `B` at `2d40d95` with `Seed 12345` / `TodayReference 2026-08-03`, emitting `ScheduledItem` lists, and score with the current `ObjectiveEvaluator`. Order of a day. The only non-documentation item in the entire finding set. Low urgency: G2-5 arm 2 was ratified as *reporting, not blocking*, and there are zero production callers.
2. **F-E — add the all-zero `w1…w5` guard**, at wiring time, tested against the real call site rather than hypothetically.
3. **F-F — cross-reference D7 from G2-5's arm-3 table cell.** One line; needs explicit authorization to amend a ratified artifact.
4. **An owner for the wiring-time trigger.** Items 1, 2 and the `N=1` caveat all activate when the SOE seam gains a production caller. No document currently owns that moment.
5. ~~**Metric numbering inconsistency**~~ — **RESOLVED during execution, 2026-08-07. No action needed.** The objective-delta metric is cited as **#4** in the closing note and **#8** in the T3.9 note (§5 check 3, §6 OQ1), which looked like a contradiction. It is not: there are two numbering schemes and both are internally correct. The master plan numbers its success metrics **#1–#5** (what the closing note uses); the execution plan §3.8 folds the epic criteria and the success metrics into **one list of 13**, where criteria 1–4 are the epic criteria and 5–9 are the same success metrics renumbered — making the objective-delta metric **#8** there. Neither document is wrong. This was established while writing WP-1's criteria-10–13 block, which is also *why* only 10–13 were missing: they are §3.8's own additions and have no master-plan counterpart. The closing note now states the mapping explicitly, so the next reader will not have to re-derive it.

---

## Verification summary (end-to-end)

Run once, after commit 1, before commit 2:

```
rtk git status --porcelain                               # AGENTS.md / CLAUDE.md / .claude/skills still unstaged
rtk git diff --stat HEAD~1                               # exactly 2 files
rtk grep "deadline-aware" docs/specs/system_roadmap.md   # 0 matches
dotnet build SmartStudyPlanner.slnx                      # 0 errors
dotnet test --no-build                                   # 470 / 0 / 1 skipped / 471
```

Then `gitnexus_detect_changes()`, then commit 2, then re-run `git diff --stat HEAD~1` to confirm it touches only the T3.9 note.

The build/test pair proves the change was docs-only. The two reader checks in §3's judgment condition prove the change was *correct* — and only a person can run those.
