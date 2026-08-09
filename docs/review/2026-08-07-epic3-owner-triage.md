# Epic 3 — Owner Triage (Phase 0, pre-convergence)

**Date:** 2026-08-07 · **Branch:** `worktree-epic-3-soe` @ `82155d9` · **Prepared by:** independent
reviewer (Engineering Judge role), continuing from
[the closure verdict](2026-08-07-epic3-closure-verdict.md).

**What this document is.** A decision aid. It restates every finding from the verdict with its cost
and its consequence-if-unchanged, so the owner can set the convergence scope. Every disposition below
is a **recommendation only**.

**What this document is not.** It does not redesign Epic 3, reopen any ratified decision (CP-2, CP-3,
G2, G3, D7), implement anything, or sequence the work. Derivations are not repeated here — each row
points at the verdict section that carries the evidence.

---

## Cost basis

Costs are stated as **(edit size · elapsed time · re-verification burden)**.

**Re-verification is zero** for any row whose numbers already appear in
[verdict §1](2026-08-07-epic3-closure-verdict.md#1-what-i-verified-independently-all-confirmed) —
those were re-derived on this branch by running the cited command, so amending a document to state
them requires no new test run. Rows where that is *not* true say so explicitly.

Ten of the eleven rows below are **documentation edits**. Exactly one (F-D) is engineering work.

---

## Owner triage table

| ID | Current class | Summary | Supporting evidence | Engineering impact | Cost | Risk if unchanged | Recommendation |
|---|---|---|---|---|---|---|---|
| **F-A.1a** | Fix before release | Closing note's evidence-scoping statement discloses only that `ScheduleOptimizer`/`SoeWeights` are unwired — not that the allocator's deadline clause is provably output-inert. Reader concludes placement became deadline-aware; it became chronological. | Verdict §2 F-A.1; proof in [`2026-08-06-deadline-tier-provably-inert.md`](../plans/2026-08-06-deadline-tier-provably-inert.md); `WorkloadServiceImpl.cs` two-tier selection + its own comment | None on behavior. Corrects what the ship-readiness document claims shipped. | 1 paragraph · ~15 min · **zero** | The one document written to be read before shipping overstates the delivered capability. Every downstream reader inherits it. | **Include in convergence scope** |
| **F-A.1b** | Fix before release | `docs/specs/system_roadmap.md` §A.3 lists "**deadline-aware allocator rework (T3.3)**" as shipped work. Not true in any output-affecting sense. | Verdict §2 F-A.1; roadmap diff at `d257351` | None on behavior. Removes a false statement from the canonical document. | 1 line · ~5 min · **zero** | Highest-consequence row. A future planner reading the roadmap alone builds on a deadline-aware allocator that does not exist. This is the "docs describe a system that isn't there" failure mode, already one document deep. | **Include in convergence scope** |
| **F-A.2** | Fix before release | `A6CrossCheck_…` emits a boundary finding (harness `>=` vs production `UniformDeadlinePolicy` `>`, **delta 113 chunks / 6 440 min**) with the comment "Named as a finding for the closing report." It never reached the closing report. | Verdict §2 F-A.2; `SoeT34InvariantTests.cs` A6 test output | Frozen baseline's `TotalDHViolationChunks` (974) / `TotalOverdueMinutes` (57 865) are measured on a **stricter** predicate than the ratified production one. Any *future* D-H comparison against that artifact is off by this delta. (The 250→220 inversion delta is **not** affected — verdict §1.) | 1 short section · ~10 min · **zero** (emitted by a passing test) | A disclosure the test author deliberately routed forward is silently lost — the exact failure the repo's disclosure discipline exists to prevent. Leaves a mis-calibrated artifact undocumented. | **Include in convergence scope** |
| **F-A.3** | Fix before release | Card G's stop condition was "all 13 acceptance criteria in §3.8 demonstrated." The closing note reports 1–9. Criteria 10–13 are absent. | Verdict §1 (all four independently verified as **holding**); execution plan §3.8 | **Substantively none** — I verified 10–13 myself and all four pass. It is a reporting gap. But it is the gap that let F-A.1 and F-A.2 pass unnoticed. | See cost fork below · **zero** re-verification for the statement form | Deliverable incomplete against its own stop condition. Absent a stated result, a later reader cannot distinguish "unreported" from "unmet." | **Include in convergence scope** |
| **F-A.4** | Fix before release | F3 says re-deriving `Score(B)` "would require resurrecting the retired pre-T3.3 allocator, which PD-3/R8 forbids." PD-3/R8 forbids *capturing a baseline after* T3.3 — not deriving further metrics from frozen code at the recorded SHA. | Verdict §2 F-A.4; artifact records `HeadSha 2d40d95`, `Seed 12345`, `TodayReference 2026-08-03` | F3's **claim** is correct (`Score(B)` is not in the artifact). Only its **stated reason** is wrong. Honest wording: "not evaluated; recoverable at moderate cost." | 2 sentences · ~5 min · **zero** | A future reader believes metric #4 is permanently impossible and does not attempt it. **This is what makes F-D recoverable — see dependencies.** | **Include in convergence scope** (low cost; see D2 below for why it sits below A.1–A.3) |
| **F-B** | Accept as known limitation | The inertness itself, the 220 residual inversions, the 4 arm-3 items (`MIX-016/019/024/047`). | Verdict §2 F-B; execution plan §3.4 (A1 named as a falsifiable assumption up front); CP-3; D7 | All three trace to **A1**. Ratified with the numbers in view. The team implemented what was approved, proved a property of it, and pinned the numbers as live assertions so drift goes red. | N/A | None. **No new evidence has appeared since D7.** | **Keep accepted** — no action, and no re-litigation invited by this row |
| **F-C** | Accept, but record | At `N = 1` (single `LoadRebalanceStage`), G2-1's "run-all, commit-best-prefix" **is** per-step accept/reject — the mechanism G2 §2 rejected. `Superseded_ByLaterCheckpoint` is structurally unreachable; the branching-factor assertion reduces to `states == passes × 2`. | Verdict §2 F-C; branching-factor test reports N=1, 279 passes, 558 checkpoints | **Nothing is wrong today** — at N=1 all candidate mechanisms coincide. The machinery has simply never run in the mode that justifies it. | 1 paragraph in the T3.9 design note · ~15 min · **zero** | A stage-2 author assumes veto-avoidance and superseded-gain retention are proven behavior. They are unexercised. | **Include in convergence scope** if the owner takes the optional tier; otherwise defer with F-D/F-E (shared trigger) |
| **F-D** | Defer | Metric #4 / G2-5 arm-2 unevaluated on **207/230 (90 %)**. The quality floor G2 was chartered to finalize is unmeasured on most of the corpus. | Verdict §2 F-D; `2026-08-07-soe-t34-corpus-report.json` → `Arm1=19, Arm2=207, Arm3=4` | Real gap, low urgency: arm 2 was ratified as **reporting, not blocking**, and there are zero production callers. | **The only non-documentation row.** New harness work: re-derive `B` at `2d40d95` with seed 12345 emitting `ScheduledItem` lists, score with current `ObjectiveEvaluator`. Order of a day, not minutes. | Epic 3's own quality metric stays unmeasured. Costless today (nothing consumes it); becomes a real blind spot at wiring time. | **Keep deferred** |
| **F-E** | Defer | All-zero `w1…w5` passes `SoeWeights.IsValid()`; would silently neutralize the quality comparator. | Verdict §2 F-E; G3-2's own disclosed finding | Unreachable today — zero production callers. G3 ratified the deferral. | Guard + test, ~30 min — but correctly done **at wiring time**, against the real call site | Silent degeneration if someone configures zeros after wiring. Not reachable before then. | **Keep deferred** |
| **F-F** | Proposal only | G2-5's arm-3 cell still reads "Hard fail, no waiver." D7 waived it, in the same document — but a reader of the table alone gets the un-amended rule. | Verdict §2 F-F; G2 note §G2-5 table vs. D7 | None. A cross-reference, not a decision change. | 1 line · ~5 min · **zero** — **but it edits a ratified gate document** | Low. Self-correcting for anyone who reads D7; misleading for anyone who reads only the table. | **Reject as proposal, or promote deliberately** — the cost is trivial, but amending a ratified artifact is a category the owner should authorize explicitly rather than absorb into a docs pass |
| **F-G** | Proposal only | Runtime figures quoted as stable. Closing note: p95 0.146 ms / max 19.0 ms. My run: **0.1056 ms / 20.2 ms**. | Verdict §1 (row 7) and §2 F-G | Run-to-run variance. The ~13 700× margin against the 2 000 ms budget makes metric #5's verdict robust either way. | 1 clause · ~2 min · **zero** | Very low — but the baseline artifact already labels its own wall-clock block as varying, and the note does not. Inconsistent convention. | **Include in convergence scope** if the optional tier is taken (2-minute edit rides along) |

### F-A.3 cost fork — the owner's call

Card G's stop condition said criteria were to be "demonstrated **with output pasted**." Two standards
apply differently to an *amendment*:

- **Statement form** — state each of 10–13 with its result and cite verdict §1 for the derivation:
  ~10 min, zero re-verification.
- **Original form** — re-run four filtered tests and paste console output into the note: ~30 min.

Both are defensible. The second matches the letter of the card's stop condition; the first matches
what the closing note does for criteria 1–9 (which cite test names, not pasted output). I lean to the
first for internal consistency, but this is a standards question, not an engineering one.

---

## Dependencies between rows

Three couplings the owner should see before scoping, because deferring items *together* has a
different consequence than deferring them separately:

1. **F-A.4 → F-D.** F3's wrong reason is precisely what would stop a future engineer from attempting
   metric #4. Deferring **both** leaves a recoverable gap documented as structurally impossible —
   the worst of the available states. If F-D stays deferred (recommended), F-A.4 is what keeps the
   door open. Taking A.4 costs 5 minutes and makes the F-D deferral honest.
2. **F-A.2 → any future D-H comparison.** Until the boundary delta is recorded, the frozen artifact
   silently reports on a stricter predicate than production. Anyone re-baselining later inherits a
   113-chunk / 6 440-minute offset with nothing to warn them.
3. **F-A.1a ↔ F-A.1b.** Same fact, two documents. Fixing only the roadmap leaves the closing note
   incomplete; fixing only the closing note leaves the false statement in the canonical document.
   They are separable in cost but not in value.

---

## Observation on the deferred set

**F-C, F-D and F-E all become live at the same moment — when the SOE seam is wired to a production
caller.** No document currently owns that moment. G3-1 established that wiring is "separate,
unscheduled integration work, not part of Epic 3's task cards," which is correct scoping, but it
means three deferrals point at a trigger that has no home.

Stated as a property of the deferred set, not a recommendation. What (if anything) should own that
trigger is a scope decision outside this triage.

---

## Required output

### Mandatory — should become Fix Before Release if the owner agrees

| ID | What changes | What breaks if it doesn't |
|---|---|---|
| **F-A.1b** | Roadmap §A.3: "deadline-aware allocator rework (T3.3)" → an accurate description of what shipped | The canonical document asserts a capability the system does not have |
| **F-A.1a** | Closing note's evidence-scoping statement gains the inertness fact | The ship-readiness document overstates delivered capability |
| **F-A.2** | Closing note gains the A6 boundary finding as F4, with its forward consequence | A deliberately-routed disclosure is lost; a mis-calibrated artifact stays undocumented |
| **F-A.3** | Closing note gains a criteria-10–13 block (form per the fork above) | Deliverable incomplete against its own stop condition; "unreported" indistinguishable from "unmet" |

**Scope:** one commit, documentation only, two files. **No code change. No test change. No ratified
decision reopened.** These four are what verdict §3 called "the gate," unchanged.

### Optional — worth including because cost is very low

| ID | Cost | Why it belongs in the same pass |
|---|---|---|
| **F-A.4** | ~5 min | Makes the F-D deferral honest rather than a documented dead end (dependency 1) |
| **F-G** | ~2 min | Aligns the note with the baseline artifact's own varying-figures convention |
| **F-C** | ~15 min | Only item touching the T3.9 note; the `N = 1` caveat is cheapest to write while the context is loaded |

### Deferred — should remain outside the current convergence scope

| ID | Why |
|---|---|
| **F-D** | The only row that is engineering work, not an edit. Arm 2 is reporting-not-blocking; zero production callers. Order-of-a-day cost against no current consumer. |
| **F-E** | Unreachable today. The guard belongs at the real call site, where it can be tested against actual wiring rather than hypothetically. |

### Proposed — should remain proposals only

| ID | Why it stays a proposal |
|---|---|
| **F-F** | The edit is one line, but it modifies a **ratified** gate document. Trivial cost is not a reason to fold a ratified-artifact amendment into a reporting pass; it needs its own authorization or none. |

---

## Decisions made (ADR-style)

### D1 — Decompose F-A into four separately-triageable rows

- **Why:** F-A shipped in the verdict as one "Fix before release" finding, but its four parts have
  different costs (5 min to 30 min), different consequences (a false statement in the canonical
  roadmap vs. a wording correction), and different dependencies. Presented as a bundle it forces an
  all-or-nothing scope decision on items that do not deserve one.
- **What for:** the owner can take F-A.1b alone (5 minutes, highest value) if that is all they want,
  or the full four, and can see that the recommendation is unchanged from verdict §3 — A.1/A.2/A.3
  were "the gate," A.4 was already item 5 in the same plan's optional tier. No position moved.
- **Experience:** the roadmap line is a five-minute edit carrying more risk than the other three
  combined. Bundling by *finding* rather than by *consequence* had hidden that entirely.

### D2 — Cost stated as edit size · time · re-verification, not t-shirt sizes

- **Why:** every row but one is a documentation edit, so the differences between them are minutes,
  and a S/M/L scale would flatten a 2-minute edit and a day of harness work into adjacent buckets.
  The load-bearing figure is re-verification: it is **zero** for every row whose numbers are already
  re-derived in verdict §1, which is what makes the mandatory tier genuinely one commit.
- **What for:** the owner can see that the entire mandatory tier costs well under an hour with no
  test run required, and that F-D is the only row where "defer" is about effort rather than value.
- **Experience:** stating the unit matters. "~15 min" reads as a guess unless the basis is on the
  page; with the re-verification column it is a derived figure.

### D3 — Give F-B a row whose job is to be boring

- **Why:** F-B is settled — CP-3 ratified it, D7 ruled on it with the numbers in view, and no new
  evidence has appeared. Omitting it from a triage table would make it look like a live question by
  its absence; expanding it would invite re-litigation of a decision the owner already made.
- **What for:** one short row that closes it, with "no new evidence since D7" stated plainly, so the
  owner spends zero attention there.
- **Experience:** in a document whose purpose is scope selection, the accepted items need to be
  visible *and* visibly closed. Silence is not the same as closure.
