# Report — Epic 3 QA cycle: knowledge distillation and documentation-convention update

**Date:** 2026-08-19 · **Branch:** `docs/epic3-knowledge-distillation` (written off `dev` at
`33c0ffe`; rebased onto `d7a4955` on 2026-08-20 before pushing, after PRs #56/#57 landed — see the
addendum at the end)
**Author:** Claude Opus 5 via Claude Code, single session.
**Sources distilled:** the automated QA gate + its engineering record (2026-08-10), the manual
runbook, the owner's two observation records (2026-08-10, 2026-08-19), the stale-chart fix report
(2026-08-14) and the manual-gate closure (2026-08-19).

---

## Scope

Docs-only. Extract the durable lessons of the Epic 3 QA cycle into `docs/knowledge/`, consolidate
them against what the knowledge base already holds, and update the report-writing convention where
the cycle's own artifacts showed a recurring problem. **No engineering decision was reopened and no
production code was touched**; every ratified decision is linked, not restated.

## Findings

### Knowledge written

| Path | Topic | Why it is durable |
|---|---|---|
| `knowledge/qa-gates.md` *(new)* | Gate scoping by reachability; seam vs. call-site coverage; observation/ruling/inference; withdrawing a pass read through a faulty instrument; a manual pass is not a standing guard | No existing article covered *what a gate must establish* or how owner-led manual evidence is recorded. Independent of the SOE code, which is dormant |
| `knowledge/review-methodology.md` *(+3 sections)* | A surviving mutant is not automatically a coverage gap; pin the reason, not the mechanism; weak red (compile errors, negative assertions) | Sharpens the article's existing "a green check is evidence only after you've shown it can go red" with three cases it did not cover |
| `knowledge/incident-investigation.md` *(extended)* | "QA owns its own errors" gains its second instance — the E6 scenario written against a capability never built — plus a cheap detection check | Second occurrence of a lesson already in the file; recurrence is the argument for the detection rule, not for a new article |
| `knowledge/system-design.md` *(+1 pattern)* | Rendered state and target state are two variables; state the invariant that bounds their divergence | The stale-chart root cause generalises, and echoes `sync-data-model.md`'s "never let one scalar answer two questions" from a different domain |
| `knowledge/programming.md` *(+2 rules)* | Clamp both ends of an untrusted input; guard user-facing copy that asserts behaviour | Both are one-line rules with a concrete failure behind them |
| `knowledge/debugging.md` *(extended)* | An impact score measures fan-out, not meaning — one CRITICAL was real, one HIGH was noise, in the same run | Directly qualifies a `CLAUDE.md`-mandated step |

Consolidation: three candidate lessons were **not** written because the knowledge base already held
them — mutation-as-evidence (`review-methodology.md`), finding classification and runbook-defect
attribution (`incident-investigation.md`), and one-scalar-two-questions (`sync-data-model.md`). Each
was extended in place instead.

### Documentation convention — the recurring problems the artifacts showed

Every item below is a pattern seen more than once in this cycle, not a one-off.

1. **A rule that lost an argument with reality.** `reports/README.md` said *"don't edit old reports,
   write new ones instead."* The 2026-08-14 fix report was amended in place on 2026-08-19 — and did
   it well: the original criteria table left verbatim under *"as written on 2026-08-14; superseded"*,
   with the result appended below. The practice was better than the rule. → **Amendments, not
   rewrites**, now written down for reports and reviews.
2. **Type confusion.** A runbook filed under `plans/` against a plan's required sections; the
   owner's raw evidence filed under `reports/` against a report's required sections. → an
   **artifact-type table** in `docs/README.md`, a **runbook shape** in `plans/README.md`, and an
   **evidence-record exemption** in `reports/README.md` (forcing an ADR template onto an owner's
   record corrupts the evidence).
3. **The runbook became the record.** Results and a gate-status section accumulated in a document
   whose value is being re-runnable. → results live in the evidence record, interpretation in the QA
   report; the runbook carries a pointer row at most.
4. **A required section that was never written down.** "Decisions made" has been a standing owner
   requirement since 2026-07-07 and appears in 42 documents under `docs/`, but no README asked for
   it. → codified, scoped to agent-authored reports.
5. **Verdict vocabulary that did not fit.** `review/README.md` offers `ship`/`ship-with-followups`/
   `block`; the gate needed *PASS WITH FINDINGS*, the closing note *met with accepted limitations*.
   → one table, vocabulary per artifact type.
6. **Evidence scoping practised but unwritten.** These artifacts do it well — `NOT RUN` cells,
   "pass **by ruling**, not by written observation", "supporting circumstance, not evidence", G3-1's
   zero-call-sites statement. It survived because the authors chose to, not because anything asked.
   → **claim → evidence → scope → remaining uncertainty** is now a cross-cutting rule, including the
   observation / ruling / inference labels.

### Artifact actions

Nothing was archived or deleted. This repo's archive (`legacy/Archived plans/`) is local-only and
gitignored, so archiving a file *removes it from the tree that cites it* — which is the wrong trade
while the closure is the newest document in the epic.

| Artifact | Type | Action |
|---|---|---|
| `plans/2026-08-10-epic-3-manual-qa-runbook.md` | Runbook | **Keep, unchanged.** Already headed *EXECUTED AND CLOSED*, links the closure, and states the rule that kept its result cells honest. Its §4 gate-status block predates the "results don't live in the runbook" convention and is grandfathered — it is labelled *record-keeping, not an observation* and is the pointer future readers follow |
| `plans/2026-08-10-…-stale-chart-fix-design.md` | Plan / design | **Keep.** Status line already reads *implemented*; §7 still holds the live semester-management-UI proposal that the closure cites |
| `plans/2026-08-14-…-stale-chart-fix-plan.md` | Plan | **Status line added** (`done`, PR #54, links its verification report) — the one required field it was missing. Archive candidate later |
| `reports/2026-08-10-epic3-automated-qa-gate.md` | QA / gate report | **Canonical, preserved.** The gate verdict |
| `reports/2026-08-10-epic3-qa-session-report.md` | Execution / investigation record | **Canonical, preserved.** Distilled from, not superseded — it holds the raw probe outcomes the knowledge articles cite |
| `reports/2026-08-10-epic3-soe-manual-observation.md`, `reports/2026-08-19-epic3-manual-observation-updated.md` | Evidence record | **Preserved verbatim, now classified as such.** Never reformat; the exemption is written into `reports/README.md` |
| `reports/2026-08-14-…-stale-chart-fix-report.md` | Execution report | **Keep.** Its §3.1 amendment is now the worked example of the amendment rule; open items 3–6 remain live |
| `reports/2026-08-19-epic3-manual-gate-closure.md` | Closing note | **Canonical, preserved.** The gate's final word |
| `reports/2026-08-07-epic3-closing-note.md` | Closing note | **Canonical, not reopened.** Its 470/471 figure stays as measured at its SHA |
| `reports/data/*`, `2026-08-10-epic3-b2-optimizerrunlogs-empty.png` | Evidence | **Preserved.** The frozen baseline JSON is load-bearing — a test asserts it exists |

### Findings not acted on

- **The canonical state documents have no record of Epic 3.** `CHANGELOG.md` ends at the
  post-Epic-1 stabilization (2026-08-02) — nothing for the convergence (2026-08-07), the
  stale-chart fix (PR #54) or the gate closure; `specs/system_roadmap.md` §A.3 item 2 is current
  through 2026-08-07 and does not mention the manual gate; `docs/README.md` §4 and
  `active/README.md` still read *"Epic 1 Released; next: Analytics redesign"*. Deliberately **not**
  fixed here: writing "what shipped" into the canonical history from secondary summaries is exactly
  the drift this cycle's lessons are about, and it deserves its own commit with the owner's eyes on
  it. Recommended as the next docs task.
- **The E6 coverage test** (a repository-level test deleting a subject with ≥2 tasks and asserting a
  sibling survives) stays a recommendation, already recorded in the closure §4.3. It is code, not
  distillation. *While this pass was being written, a parallel session amended §4.3 — the gap is
  smaller than first stated and belongs to the over-cascade class, not the cascade-fixup one — and
  designed the test in `docs/plans/2026-08-19-e6-cascade-coverage-test.md`. The knowledge article was
  written against the amended text; neither file is part of this commit.*

## Verification

- `docs/knowledge/` goes from 9 files to 10. Each edit keeps the style of the file it lands in: the
  narrative articles (`qa-gates.md`, `review-methodology.md`, `incident-investigation.md`) use the
  house six-part rubric — problem / why hard / wrong assumption / how solved / principle / how to
  avoid — while the terse index files (`system-design.md`, `programming.md`, `debugging.md`) keep
  their short-entry form. No second rubric was introduced into any file.
- Every decision referenced (D1–D8 of the fix report, D1–D5 of the gate, the closure's D1–D4, CP-2,
  D7/A1) resolves to a link to its source document; none is restated as fact.
- Cross-links added in both directions between `qa-gates.md`, `review-methodology.md` and
  `incident-investigation.md`, each naming the boundary between them so the next distiller knows
  where to add.
- Claims in the new article were checked against the primary artifacts, not the summaries: the
  reachability list and M5/M6 outcomes against the QA session report §4–§5, the C2 withdrawal
  against fix-report D6, the E1–E4 ruling against closure §6, the E6 coverage gap against closure
  §4.3 **as amended 2026-08-19** — the pre-amendment wording would have put a retracted claim into
  the knowledge base.
- No file outside `docs/` was touched **by this pass**, and pre-existing untracked files
  (`.claude/*`, two handoff notes, the assets zip) were left alone. The working tree is not this
  pass's alone: `reports/2026-08-19-epic3-manual-gate-closure.md` (amended) and
  `plans/2026-08-19-e6-cascade-coverage-test.md` (new) were changed concurrently by another session
  and are deliberately **not** in these commits.

## Follow-ups

1. Propagate Epic 3 into the canonical state documents (CHANGELOG row, roadmap §A.3 item 2,
   `docs/README.md` §4, `active/README.md`) — own commit, see Findings above. **Still open.**
2. ~~Write the E6 repository-level coverage test (closure §4.3).~~ **DONE 2026-08-20** (PR #57) —
   and it did not end the way this report assumed. See the addendum below.
3. Archive candidates once (1) lands: `plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md`
   (shipped). Not candidates: the manual runbook and the stale-chart *design* — the closure cites
   both, and this repo's archive is local-only and gitignored, so archiving them removes them from
   the tree that cites them.

## Decisions made

**D1 — Report the stale canonical-state documents; do not fix them in this pass.**
*Why:* the mission is distillation and convention, and the fix requires asserting what shipped. The
only sources available here are reports *about* the work — the precise substitution this cycle's
central lesson warns against — and `HEAD` currently equals `dev`, which is PR-only.
*What for:* the owner gets a scoped decision instead of a diff mixing history claims with knowledge
edits, and the two land in reviewable pieces.
*Experience:* a distillation pass has a strong pull toward "while I'm in the docs anyway". The test
that settled it: *does this claim come from evidence I read, or from a document summarising evidence
I did not?* Knowledge articles cite the reports; a changelog row asserts the event.

**D2 — Keep the house six-part rubric instead of the format the task suggested.**
*Why:* the project's narrative knowledge articles all use problem / why hard / wrong assumption /
how solved / principle / how to avoid, and four of this cycle's lessons had to be appended *inside*
two of them. A second rubric in the same file is worse for a reader than either rubric consistently
applied.
*What for:* `review-methodology.md` still reads as one article, and a compliance check ("does each
lesson answer the six questions?") still works file-wide.
*Experience:* an external template is a default, not an instruction — the existing artifacts are
evidence about what this project's readers already parse.

**D3 — Extend three existing articles rather than open new ones.**
*Why:* mutation technique, finding classification and the one-scalar-two-questions principle already
had homes. A new file per cycle produces near-duplicate knowledge and splits the search.
*What for:* the second occurrence of the runbook-defect lesson now sits *next to* the first, which
is what turns an anecdote into a pattern and justifies the detection rule attached to it.
*Experience:* the useful question before writing a knowledge file is not "is this lesson new?" but
"is this lesson's *home* new?". Here only one of six was.

**D4 — Keep runbooks in `plans/` with an explicit exemption rather than creating `runbooks/`.**
*Why:* two runbooks exist in the project's history, and both are cited by reports; a folder move
breaks live citations to buy alphabetical tidiness.
*What for:* the shape rules land where a writer already looks, and the alternative is recorded in one
line so a future increase in volume can revisit it cheaply.

**D5 — Exempt evidence records from the report template, and say why in the README.**
*Why:* the owner's two observation files have no Scope/Verification/Decisions sections and should
not. Without the exemption written down, a future agent "fixes" them and the primary evidence
becomes a paraphrase of itself.
*What for:* provenance survives contact with the next tidy-up pass.
*Experience:* the closure already refused to transcribe the owner's wording for this reason; the
convention was practised before it was written, which is generally the moment to write it.

---

## Addendum — 2026-08-20: the E6 follow-up landed, and it changed the lesson

**Author:** Claude Opus 5 (agent) · **Scope:** follow-up 2 above, and the two knowledge edits it forced.

Follow-up 2 was written on the assumption that the E6 test was a small, obvious win — *"cheap, and it
converts a passing manual observation into a standing guard,"* in the closure's words, repeated here.
It was written (PR #57) and it is **not** a standing guard.

The design set an acceptance bar in advance — at least one mutant the new test kills *and* the
pre-existing suite survives — and named the fallback if nothing cleared it. Nothing did. Five
mutants: three took both sets red, one took only the pre-existing suite red, one survived all 487
tests. The test reproduces E6's shape and passes, but catches nothing the suite would have missed,
because the production cascade has no branch keyed on child count or sibling existence. It was filed
as scenario-fidelity coverage, not regression protection.

**What changed in `docs/knowledge/` as a result:**

- `qa-gates.md`, *A passing manual observation is not a standing guard* — the arc now runs to its
  actual end. Its Principle gains the distinction the cycle paid to learn: **closing a scenario gap
  and buying regression protection are two different outcomes, and only the first is in a manual
  gate's gift.** Whether the second follows depends on the shape of the production code, which the
  gate cannot know when it writes the recommendation.
- `review-methodology.md`, *A surviving mutant is not automatically a coverage gap* — gains the third
  answer. The tier-1 survivor was predicted by a ratified decision; E6's `DetectChanges()` survivor
  is predicted by nothing and sits on a line whose comment records a real bug it was added to fix.
  "Not yet determined" is neither a confirmation nor a gap, and its specific failure mode is deleting
  the line on the strength of a green run.
- `review-methodology.md`, *Set the bar before you measure* (new) — the interpretive counterpart to
  *a green check is evidence only after you've shown it can go red*. Measuring honestly and reading
  the measurement honestly are different acts, and only the second happens under pressure.

**This is the follow-up doing its job, in the way the article predicted and one it did not.** The
`qa-gates.md` entry already warned that a gap's *size* moves when someone finally writes the test.
Its *value* moved too, and nothing in the original pass anticipated that — which is why this is an
addendum rather than a rewrite.

### Decisions made

**D6 — Amend this report and the two articles rather than filing a fresh distillation.**
*Why:* the lesson is not new, it is the same lesson finishing. A second report on E6 would split the
arc across two files and leave the first one quietly wrong at its most citable point — the Principle.
*What for:* a reader arriving at the `qa-gates.md` entry gets the whole story, including the part
where the recommended test did not buy what the recommendation implied.
*Experience:* the follow-up list of a distillation pass is itself a set of predictions, and worth
revisiting for the same reason §6's prediction column was preserved in the E6 plan — the ones that
missed are the informative ones.

**D7 — Record the unmet bar in the knowledge base, not just in the report.**
*Why:* reports are read once, by whoever is looking for that cycle; `docs/knowledge/` is what gets
read before the *next* manual gate writes its recommendations. The failure mode this cycle found —
promising protection a gate cannot deliver — is a drafting habit, and drafting habits are corrected
where the drafting guidance lives.
*What for:* the next gate closure can say "recommended test, scenario coverage" and mean it.
*Experience:* a modest or negative result is often more transferable than a win; it names a trap that
a success story leaves invisible.
