# Session report — Card G verification, GATE G3, Epic 3 closing note, roadmap fix

**Date:** 2026-08-07 · **Branch:** `worktree-epic-3-soe` · **Purpose:** handoff for an independent
review session — this is an activity report of what was done and how it was checked, not the review
itself. Every claim below states how it was verified; nothing here should be taken on trust without
re-running the cited command if the reviewer wants to confirm it independently.

**Commits this session:** `eabea1e` → `d257351` (5 commits, listed per section below).
`a8076e0` (Card G's code-quality fix round) was made by a prior agent dispatch **before** this
session's continuation began; this session's first action was independently verifying it, so it's
included in the verification account below even though it isn't one of this session's own commits.

---

## 1. Card G (T3.4 D-H invariant suite + T3.7 `OptimizerRunLog`) — verified and closed

**What existed going in:** commit `a8076e0`, the implementer's self-reported fix round addressing
3 code-quality findings in `SoeT34InvariantTests.cs` (a naming/assertion contradiction, a
cross-item-cancellation risk in the branching-factor test, three missing non-empty guards).

**Independently verified this session (not trusted from the agent's report):**
- `git show --stat a8076e0` + full diff read: confirmed the diff matches all three findings exactly,
  touches only `SoeT34InvariantTests.cs` (28 insertions / 4 deletions).
- `git diff 961f453 HEAD --stat -- <baseline artifact> <WorkloadServiceImpl.cs> <LoadRebalanceStage.cs>`:
  empty output — confirmed the frozen baseline and the two forbidden-scope production files remain
  untouched across all of Card G's commits.
- `dotnet build` (0 errors) and `dotnet test` (**470 passed / 0 failed / 1 skipped / 471 total**),
  run directly, not copied from the agent's number.

**Decision made this session:** treated the code-quality loop as closed without dispatching a fresh
reviewer pass, given the narrow/mechanical nature of the three fixes and that the diff was read in
full. Recorded as a judgment call, not a shortcut taken silently.

**Owner ruling captured (`eabea1e`):** presented Card G's two disclosed findings to the owner via
explicit stop-and-ask and got a ruling on both. Written up as **D7** in
[`docs/plans/2026-08-04-g2-optimization-pass-semantics.md`](../plans/2026-08-04-g2-optimization-pass-semantics.md):
1. **Residual inversions** (220 total, 85 on the feasible subset) — accepted as a known allocator
   limitation, not blocking ship.
2. **G2-5 arm 3** (4 hard-fail items: `MIX-016/019/024/047`) — accepted as a documented exception,
   not blocking ship.

Both trace to the same root cause (**A1** — `WorkloadServiceImpl` uses priority, not deadline, as the
task-ordering key). A reviewer who wants to re-check this ruling should re-read D7 and, if they
disagree with the disposition, that's a re-opening decision for the owner, not something this session
treated as settled beyond the owner's stated ruling.

---

## 2. GATE G3 (T3.5, `w1…w5` weight-vector governance) — drafted, verified, ratified

**Dispatched a fresh implementer** to draft a decision note (following Card B/G2's structural
template) resolving three questions the master plan left open as B5: ownership, guardrails, relation
to `WeightOptimizer`. Result: `c6e64d3`,
[`docs/plans/2026-08-07-g3-weight-vector-governance.md`](../plans/2026-08-07-g3-weight-vector-governance.md).

**Independently re-verified this session** (not the agent's self-report) — three factual claims the
note's decisions depend on, each checked by direct grep/read against the code at this HEAD:
- `grep -rn "new SoeWeights|new ScheduleOptimizer" SmartStudyPlanner/` → **no matches** outside tests.
  Confirmed `BalanceWorkloadStage.cs:41` still calls `IWorkloadService.GenerateSchedule` directly, not
  `ScheduleOptimizer.Optimize` — i.e., **zero production call sites** for the SOE seam as of this HEAD.
- `grep "SoeWeights" WeightOptimizerService.cs` → no matches — confirmed `WeightOptimizer` and
  `SoeWeights` are unrelated mechanisms today.
- `grep -rln "OptimizerRunLogWriter" SmartStudyPlanner/` → only its own definition file — confirmed
  it isn't called from anywhere in production (`AppStartup.cs` doesn't wire it), so the note's claim
  that zero `OptimizerRunLog` rows exist in production today checks out.

**Ratified by the owner** (`1e18bb7`) via explicit stop-and-ask, one question per decision
(G3-1/G3-2/G3-3), all three "ratify as written." Status line updated in the note itself.

**A reviewer should know:** the note names, but does not fix, a real gap — an all-zero `w1…w5` vector
passes `SoeWeights.IsValid()` today and would silently neutralize the quality comparator. Ruled
deferred (unreachable through any current production path; the card's scope forbade code changes
anyway). This is G3-2's finding, not something buried — worth a second look if the reviewer's
standard for "acceptable deferral" differs from what was applied here.

---

## 3. Epic 3 closing note (DoD-7) — written and accepted

[`docs/reports/2026-08-07-epic3-closing-note.md`](2026-08-07-epic3-closing-note.md) (`6e35420`).

**Every number in it was re-derived this session**, not carried forward from any prior agent report —
each cited test was run directly with `dotnet test --filter ... --logger "console;verbosity=detailed"`
and the console output read. The two numbers most worth a reviewer's independent re-check:

- **Deadline inversions (metric #1b):** baseline (pre-T3.3) self=17/pairwise=233/total=250 →
  current self=0/pairwise=220/total=220. Self-miss class eliminated by construction; pairwise only
  reduced 5.6%. Re-run: `dotnet test --filter "FullyQualifiedName~SoeT34InvariantTests.Inversion_Allocator_TotalAcrossCorpus_ComparedAgainstFrozenBaseline"`.
- **Metric #4 (objective delta vs. baseline)** is reported as structurally unevaluable for 207/230
  (90%) of the corpus, sourced from `docs/reports/data/2026-08-07-soe-t34-corpus-report.json`'s
  `Aggregate.ScoreBComputable: false` field, re-read directly with `grep -n` this session (not copied
  from an earlier summary).

**Accepted by the owner as written**, including Findings F1 (roadmap staleness) and F2/F3
(informational — restating that metric #1 is two facts and metric #4 is 90% unevaluable, both already
owner-ruled elsewhere).

**A reviewer should know:** this closing note is *this session's* synthesis. It cites the underlying
test/artifact for every number so a reviewer can re-derive independently, but the aggregation,
wording, and "met/not-met" judgment calls are this session's, not a second independent pass.

---

## 4. Roadmap staleness fix (DoD-5, Finding F1) — fixed

[`docs/specs/system_roadmap.md`](../specs/system_roadmap.md) (`d257351`). It had been stale since
2026-08-04 (before G2 was even ratified) — still read "G2 still OPEN, implementation blocked" in two
places (§A.3 item 2, §7.4's SOE reconciliation note). Updated both to state G2/G3 ratified, Cards A–H
shipped, and explicitly restated the zero-production-callers fact so a future reader of the roadmap
alone (without the closing note) doesn't overestimate how integrated the SOE currently is.

---

## 5. Verification hygiene this session

- GitNexus reindexed (`npx gitnexus analyze`) after every commit this session, per this repo's
  `CLAUDE.md`.
- Final sanity pass after all docs-only commits: `dotnet build` (0 errors) + `dotnet test` (still
  470/0/1/471) — confirmed the docs-only changes didn't regress anything (expected, but checked
  rather than assumed).

## 6. What is explicitly NOT done, for the reviewer to weigh

- No independent verdict-style review document (the `docs/review/` pattern Epic 1 used,
  `2026-07-11-epic1-closure-verdict.md`) exists for Epic 3 yet — this report is an activity account,
  not that second, independent pass.
- `ScheduleOptimizer`/`SoeWeights` are **not wired into production**. Nothing in this session's work
  changes that; it is named repeatedly (G3-1, the closing note's evidence-scoping statement, the
  roadmap fix) rather than left implicit, but it means every metric reported this epic is
  harness-measured, not field-measured.
- The all-zero `w1…w5` guardrail gap (G3-2) and the roadmap's other sections beyond the two SOE
  mentions were not swept further than what's described above.
