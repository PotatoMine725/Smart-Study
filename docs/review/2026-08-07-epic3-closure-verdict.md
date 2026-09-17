# Epic 3 (Study Optimization Engine) — Independent Closure Verdict

**Date:** 2026-08-07 · **Reviewer:** independent convergence review (Engineering Judge role) ·
**Branch reviewed:** `worktree-epic-3-soe` @ `82155d9` · **Merge-base:** `1a5ad7d` (= `dev` HEAD, so
the branch fast-forwards cleanly).

**Format precedent:** [Epic 1 closure verdict](2026-07-11-epic1-closure-verdict.md) — the second,
independent pass the [session report](../reports/2026-08-07-epic3-cardg-g3-closure-session-report.md)
§6 correctly names as missing.

**Method.** Every number below was re-derived on this branch by running the cited command and reading
its output, or by reading the cited file. No conclusion is carried over from the closing note or the
session report; where my number differs from theirs, I say so.

---

## Verdict

**Epic 3 has NOT yet reached the Engineering Quality Gate — but the sole blocker is one editing pass
on one document. No code change is required, and no architectural decision needs reopening.**

The code does what it was approved to do. The tests demonstrate what they claim. The single failure
is that **the closing note — DoD-7's actual deliverable — materially understates what shipped**, and
that understatement has already propagated into the canonical roadmap.

---

## 1. What I verified independently (all confirmed)

| Claim | Reported | My re-derivation | |
|---|---|---|---|
| Build | 0 errors | 0 errors, 53 warnings | ✅ |
| Suite | 470 / 0 / 1 skipped / 471 | identical | ✅ |
| Metric #1a — D-H breaches | 0 / 230 | `D-H (Optimize seam): 230 corpus item, 0 breach(es)` | ✅ |
| Metric #1b — inversions | 250 → 220 (self 17→0, pairwise 233→220) | identical; feasible subset 85 residual across 43 of 104 items | ✅ |
| Metric #2 — determinism | 230 × 3, 0 mismatches | identical | ✅ |
| Metric #3 — explainability | 558 checkpoints, 0 missing/spurious | identical | ✅ |
| Metric #5 — runtime | p95 0.146 ms, max 19.0 ms | p95 **0.1056** ms, max **20.2** ms | ⚠️ see F-G |
| Criterion 10 — A6 cross-check | (not reported) | implemented and passing: 862 chunks / 50 480 min on **both** paths | ✅ |
| Criterion 11 — bucket ⑤ green | (not reported) | no bucket ⑤ assertion touched in the diff | ✅ |
| Criterion 12 — ①/② triaged + mutation check | (not reported) | ① rewritten discriminatingly; ② decoupled per the prescribed pattern | ✅ |
| Criterion 13 — suite ≥ 391, no test deleted | (not reported) | 470 ≥ 391; zero deletions (one rename, one delete-then-restore under CP-2) | ✅ |
| Criterion 3 — D-J, no score buys a violation | asserted structurally | **verified in code** (below) | ✅ |
| PD-3 / R8 — baseline frozen pre-T3.3 | asserted | artifact last touched `8d938b9`, before T3.3 (`5197784`) | ✅ |

Three of these deserve their own sentence, because they are the ones a reviewer would most reasonably
suspect:

- **The 250 → 220 delta is apples-to-apples.** Both sides are computed by the same harness code
  (`SoeScheduleMetrics`) under the same `>=` boundary. I checked this specifically because the A6
  cross-check reveals a boundary disagreement elsewhere (F-A.2); it does **not** contaminate this
  comparison.
- **D-J holds structurally, not just by assertion.** `OptimizerComparator.CompareFeasibility` reads
  only `Eval.ViolationCount` and `Eval.OverdueMinutes`; `IsAdmissible` delegates wholly to it; and
  `IsBetterThan` reaches its `Score` line only when the feasibility comparison returns 0. No weight
  vector can reach the admissibility decision. This upgrades acceptance criterion #3 from an argument
  to a verified fact.
- **F3 is accurate.** I tested the obvious escape route — the frozen artifact stores
  `LoadBalanceVariance`, `FragmentedTaskCount`, `SubjectSwitchCount`, which look like the objective
  inputs. They are not sufficient: `ObjectiveEvaluator`'s `ContextContinuity` (per-day distinct-subject
  concentration), `SessionQuality` (per-chunk trapezoid) and `FatiguePenalty` (calendar-adjacent
  heavy-day pairs) all require the full `ScheduledItem` list, and Card A captured adjacency-based
  proxies instead. `Score(B)` genuinely cannot be reconstructed from the artifact.

The quality of the underlying work is high, and unusually well-disclosed. Card F caught and fixed a
**tautological assertion** in the bucket-① rewrite (a filter-by-name-then-assert-that-name pattern
that could never go red) — that is the repo's "signal must be able to fail" discipline working
without supervision.

---

## 2. Findings

### F-A — **Fix before release** — the closing note understates what shipped (one editing pass)

Four omissions, all the same class: DoD-7's deliverable is incomplete. Ranked by consequence.

**A.1 — The allocator's deadline filter is provably inert, and the closing note does not say so.**

`GenerateScheduleWithIdentity`'s two-tier day selection is:

```csharp
days.Where(d => d.TotalMinutes < capacityMinutes && d.Date <= hanChotDate).OrderBy(d => d.Date).FirstOrDefault()
    ?? days.Where(d => d.TotalMinutes < capacityMinutes).OrderBy(d => d.Date).FirstOrDefault()
```

I re-derived the proof against the code: `days` is seeded in date order and only ever appended at
`today.AddDays(days.Count)`; capacity only fills. So filtering a totally-ordered set by an upper bound
and taking the minimum either returns the same minimum or nothing — tier-1 **cannot** disagree with
tier-2, on any input. What T3.3 actually changed is *least-loaded → earliest-day-with-room*
(chronological first-fit). **Deadline does not affect placement output today.**

This is honestly documented in three places — the production code comment, the test-suite class doc,
and [`2026-08-06-deadline-tier-provably-inert.md`](../plans/2026-08-06-deadline-tier-provably-inert.md).
It is absent from the one document written to be read for ship-readiness. The closing note's
evidence-scoping statement discloses only that `ScheduleOptimizer`/`SoeWeights` are unwired; a reader
therefore concludes *"the allocator became deadline-aware; only the pass-loop seam is unwired."* The
accurate statement is: *"placement became chronological; the allocator's deadline clause provably
cannot change any output; deadline-as-hard-constraint ships entirely unwired."*

**This has already propagated.** `docs/specs/system_roadmap.md` §A.3 now reads "**deadline-aware
allocator rework (T3.3)**" in its shipped-work list. The roadmap is the canonical document; that line
is not true in any output-affecting sense.

**A.2 — A finding the test explicitly routes to the closing report never arrived.**
`A6CrossCheck_...` emits: harness boundary `>=` vs production `UniformDeadlinePolicy` `>`, **delta =
113 chunks / 6 440 minutes**, with the comment "Named as a finding for the closing report." It is not
in the closing report. Consequence worth recording: the frozen baseline's `TotalDHViolationChunks`
(974) and `TotalOverdueMinutes` (57 865) are measured on a stricter predicate than the ratified
production one, so any *future* D-H before/after comparison against that artifact is off by this
delta. (The inversion delta is unaffected — see §1.)

**A.3 — Criteria 10–13 are unreported.** Card G's stop condition was "all 13 acceptance criteria in
§3.8 demonstrated with output pasted." The note reports 1–9. I verified 10–13 myself and **all four
hold** — so this is a reporting gap, not a substantive one, but it is the gap that let A.1 and A.2
pass unnoticed.

**A.4 — F3's stated reason is wrong.** The note says re-deriving `Score(B)` "would require
resurrecting the retired pre-T3.3 allocator, which the frozen-baseline discipline (PD-3/R8) forbids."
PD-3/R8 forbids *capturing a baseline after T3.3 changed the allocator* — it does not forbid deriving
additional metrics from the same frozen code at the recorded SHA. With `HeadSha = 2d40d95`,
`Seed = 12345` and `TodayReference = 2026-08-03`, `B` is exactly reproducible. The honest wording is
"not evaluated; recoverable at moderate cost," not "structurally unevaluable."

### F-B — **Accept as known limitation** — the inertness itself, the 220 residual inversions, the 4 arm-3 items

All three trace to **A1** (priority as the sole task-ordering key), which the execution plan named as
a falsifiable assumption up front (§3.4). CP-3 ratified earliest-feasible; D7 ruled on its
consequences with the numbers in view. The team implemented what was ratified, then *proved* a
property of it and pinned the numbers as real assertions so drift goes red. That is correct
engineering conduct, not a defect. Reopening CP-3 would be redesigning the epic, which is out of
scope for this review and unsupported by the evidence.

### F-C — **Accept, but record** — at `N = 1`, G2-1 has never run in its distinguishing mode

The branching-factor test reports `N = 1` (a single `LoadRebalanceStage`), 279 passes, 558
checkpoints. At `N = 1`, "run-all, commit-best-prefix" **is** per-step accept/reject — the mechanism
G2 §2 explicitly rejected — because the candidate set is just `{C₀, C₁}`. G2-1's distinguishing
value (all-or-nothing-veto avoidance, superseded-gain retention) is entirely unexercised,
`Superseded_ByLaterCheckpoint` is structurally unreachable, and the branching-factor assertion reduces
to `states == passes × 2`. Nothing is wrong today — at `N = 1` all candidate mechanisms coincide — but
whoever adds stage 2 must know this machinery has never been exercised in the mode that justifies it.
Belongs in the T3.9 design note, not the code.

### F-D — **Defer** — metric #4 / G2-5 arm-2 unevaluated on 207/230 (90 %)

The quality floor G2 was chartered to finalize is unmeasured on 90 % of the corpus. Recipe if it is
ever wanted: re-derive `B` at `2d40d95` with the deterministic seed, emitting `ScheduledItem` lists,
and score them with the current `ObjectiveEvaluator`. Low urgency: arm 2 was ratified as *reporting,
not blocking*, and there are zero production callers.

### F-E — **Defer** — G3-2: all-zero `w1…w5` passes `SoeWeights.IsValid()`

Would silently neutralize the quality comparator. Unreachable today (zero callers). Add the guard at
wiring time, not now.

### F-F — **Proposal only** — G2-5's arm-3 cell still reads "Hard fail, no waiver"

D7 waived it, and D7 is recorded in the same note — but a reader of the table alone gets the
un-amended rule. One cross-reference.

### F-G — **Proposal only** — wall-clock figures quoted as if stable

Closing note: p95 = 0.146 ms, max = 19.0 ms. My run: 0.1056 ms and 20.2 ms. Run-to-run variance, and
the ~13 700× margin makes the verdict robust either way — but the baseline artifact labels its own
wall-clock block as varying, and the note should do the same.

---

## 3. Convergence plan (smallest practical path to the gate)

**One commit, documentation only.** Amend `docs/reports/2026-08-07-epic3-closing-note.md`:

1. Extend the evidence-scoping statement with A.1 — the allocator's deadline clause is provably inert;
   what shipped in production is chronological placement, not deadline-aware placement; cite
   `2026-08-06-deadline-tier-provably-inert.md`.
2. Correct `docs/specs/system_roadmap.md` §A.3: "deadline-aware allocator rework (T3.3)" →
   "allocator placement rework (T3.3: least-loaded → earliest-feasible; the deadline clause is
   present but provably output-inert today — see the closing note)."
3. Add A.2's boundary finding (113 chunks / 6 440 min) as **F4**, with its consequence for future
   comparisons against the frozen artifact.
4. Add a short criteria-10–13 row block (the four numbers are in §1 of this verdict).
5. Reword F3 per A.4, and label the runtime figures as varying (F-G).
6. Record F-C in the T3.9 design note.

Items 1–4 are the gate. Items 5–6 are cheap and belong in the same pass.

**Not required for the gate:** any code change, any test change, any reopening of CP-2/CP-3/G2/G3/D7.

**Housekeeping, reviewer's note:** the worktree carries uncommitted `AGENTS.md`, `CLAUDE.md` and
`.claude/skills/**` edits (GitNexus index counts and skill text). They are unrelated to Epic 3 and
should be committed separately or discarded before the merge, not folded into the epic.

---

## 4. Decisions made (ADR-style)

### D1 — Split "the allocator is inert" into an accepted limitation and a blocking disclosure gap

- **Why:** these are two different facts with two different owners. The inertness is the ratified
  consequence of CP-3 + A1, already ruled on at D7 — treating it as a defect would reopen a decision
  the owner made with the numbers in front of them. The *silence* about it in the ship-readiness
  document is nobody's ratified decision; it is an incomplete deliverable.
- **What for:** the owner gets a one-commit convergence plan instead of a reopened epic, and the
  correction lands where the misreading actually occurs.
- **Experience:** the propagation into the roadmap is the proof this matters. A gap that stays inside
  one document is a wording problem; a gap that has already been copied into the canonical roadmap is
  the beginning of the "docs describe a system that doesn't exist" failure this repo has hit before.

### D2 — Verify the two claims the reports could most plausibly have gotten wrong, rather than re-verify everything

- **Why:** the reports are unusually self-critical, which is itself a reason for care — thorough
  disclosure can substitute for correctness in a reader's mind. I picked the two claims where a
  synthesis error would have been both easy and consequential: whether the 250→220 delta was
  measured consistently on both sides, and whether `Score(B)` was genuinely unrecoverable rather than
  merely inconvenient. Both survived; one (`F3`'s *reason*) survived only partly.
- **What for:** the ✅ marks in §1 are load-bearing rather than deferential, and the one soft spot
  found (A.4) is a wording fix, not a re-measurement.
- **Experience:** the escape route worth checking was the one the artifact itself advertises — it
  stores fields *named* like objective inputs. Reading `ObjectiveEvaluator`'s component definitions
  against the artifact schema settled it in two reads. Verify the near-miss, not the headline.

### D3 — Accept `N = 1` rather than call it vacuous, but require it in writing

- **Why:** at `N = 1` the G2 machinery is correct and the tests are honest; the loop simply has no
  second stage to distinguish it from the mechanism G2 rejected. Calling that a defect would demand
  stages nobody has specified. Leaving it unwritten, though, sets up a future reader to trust a
  guarantee that has never been exercised.
- **What for:** the cost is one paragraph in the T3.9 note; the risk avoided is a stage-2 author
  assuming veto-avoidance is proven.
- **Experience:** "the test passes" and "the test could have failed" are different claims, and at
  `N = 1` the branching-factor test is close to tautological. This repo already knows that
  distinction; this is the same lesson at the level of an architectural mechanism rather than a test.
