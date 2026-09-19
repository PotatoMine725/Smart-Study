# Decision Governance Lessons

> Distilled 2026-09-17 from the Epic 2 / T2.4 structural-conflict-fence cycle (PRs #88–#95): a
> semantic analysis that refused to rule, two rounds of owner rulings, and two slices built against
> them. This article is about **how a project holds decisions** — who may make one, how a ruling is
> recorded so the next slice can rely on it, and what an agent does when two ratified sources
> disagree. The rulings themselves are normative and live in `docs/specs/`; read this for the
> reusable process.
>
> Companion articles: [`architecture-process.md`](architecture-process.md) — how a *direction* is
> decided and frozen; [`review-methodology.md`](review-methodology.md) — how a reviewer tells a
> defect from a specification gap in the first place.

## Two ratified sources that disagree is an owner question, not an implementation choice

**Problem.** The fence specification said in plain text that an ordinary non-structural edit to a row
held at Base by an unresolved conflict is fence-**passing**. The frozen decision record said the live
domain state **must equal Base** while a record is unresolved — and shipped, tested code read that as
the *whole row*. Both documents are ratified. Both are current. They give opposite answers for the
same mutation, and the implementation had to do one of them.

**Why it was hard.** Either answer is defensible, and each looks like a small local choice at the line
where it is made — a `Passed` versus a `Blocked` in one policy. The consequences are not local: the
permissive reading lets a local field edit drift a resolution fingerprint, after which every
resolution kind is rejected and the conflict record becomes **permanently unresolvable**. That is a
data-loss-shaped outcome arrived at through a one-line default.

**How it was solved.** Three moves, in order. (1) The tension was **surfaced in the plan** as a named
open decision (SB-3) with the two readings, their consequences and the precedence rule that applies,
rather than resolved in code. (2) While it was open, the plan carried a **single named switch**
(`FencePendingDecisions`) as the only place the undecided semantics could live — visible in one spot
and provable by a mutant, rather than scattered through four policies — and when the ruling arrived
before implementation the switch was **dropped rather than flipped**, with *"no such type exists"*
made an acceptance criterion. (3) The owner ruled, and the ruling was recorded in a dated file that
says exactly which spec cells it narrows.

**Principle.** An agent may implement a decision and may *frame* one; it may not make one by choosing
a branch. Where two authoritative sources disagree, the code must not pick — and "the plan is
ambiguous so I chose the safer-looking option" is picking. Surface it as a question with its
consequences priced, and let the decision arrive from the person who owns it.

**How to avoid it next time.** Every spec should carry an explicit **precedence rule** (this one says:
where the spec and a frozen decision appear to conflict, the frozen record wins). Precedence turns an
argument into a lookup. Where a question cannot be answered even with precedence, it belongs in the
plan's open-decisions table, not in a policy method.

## Narrow a document by precedence; do not edit it

**Problem.** The SB-3 ruling changes how five specific cells of the canonical fence spec must be read.
The obvious move is to edit the spec so it says the right thing.

**How it was solved.** The spec was left byte-unchanged. The ruling record carries a table — *spec
location · spec text (abridged) · v1 reading under this ruling* — listing every narrowed cell, and
states that every other cell is unaffected. The same precedent governs the frozen decision record:
every amendment and ruling since is a dated file in `docs/specs/`, and the record itself is not
edited.

**Principle.** A ratified document plus a dated ruling that narrows it preserves two things an edited
document destroys: what was originally agreed, and the fact that someone later had to decide
otherwise. It also bounds the change — a reader can see that *five cells* moved, which an edit cannot
show. This is the governance-level form of the project's cross-cutting rule *amendments, not
rewrites*.

**How to avoid it next time.** When a ruling changes how existing text reads, ask whether the text was
wrong (fix it, dated) or whether it was *outranked* (leave it, and list the cells). The second is much
more common than it looks, and the tell is that the original text is still correct in every case the
ruling does not reach.

## A ruling that confirms existing text amends nothing — and the record has to prove it

**Problem.** Rulings arrive in rounds, and a round that lands during implementation is the most
dangerous kind: the implementer wants to start, and nobody yet knows whether the new ruling has
quietly moved a frozen semantic that other packages depend on.

**How it was solved.** Every ruling record in this cycle opens with a fixed header block —
**Closes · Amends · Does not amend · Leaves open · Implemented by** — and closes with a
**consistency check** table that was filled in *before* implementation: each ruling against the
tracked text it touches, with the outcome stated as `confirms` or `narrows`. Both rulings of the final
round came out as **confirms**: one restated the plan's own cascade table, the other restated a
non-goal (*"no routing of `StudyLog` writes"*) that the pre-ruling code had drifted away from. The
round changed no frozen semantics, and the record says so in a form a reader can check rather than
trust.

**Principle.** "This decision changes nothing else" is a claim like any other and needs its evidence
attached. A ruling record that names what it does *not* amend, and shows the check, is what lets a
later slice cite it without re-deriving the whole chain. The `Leaves open` line matters as much: it is
how a reader learns that the round they are reading did not close everything.

**How to avoid it next time.** Reuse the header block verbatim; it is four lines and it has now caught
one real drift. Where a ruling turns out to *confirm* text that the code contradicts, that is not a
no-op — it is a defect report, and the code changes while the documents do not.

## Write the analysis so it cannot be mistaken for a decision

**Problem.** The document that unblocked this cycle is a semantic analysis of a collision between two
frozen rules. It runs to 47 KB, measures the current behaviour, enumerates six candidate resolutions
with their consequences, and recommends none. A document like that is one careless sentence away from
being cited later as the decision it deliberately is not.

**How it was solved.** Four devices, all cheap:

- **A status banner that says what the document is not** — *"analysis only; no owner ruling made,
  proposed as made, or implied."*
- **Per-claim evidence labels**, applied to every statement: `FROZEN TEXT` (quoted from a ratified
  source) · `FACT` (read in the tree at a named baseline) · `MEASURED` (a command was run here and its
  output observed) · `INFERENCE` (composition by the author) · `UNRESOLVED` (the documents do not
  determine it). The labels are what make the later question *"do the documents determine this?"*
  answerable without re-reading everything.
- **Candidate ruling texts, written out, none preferred** — so the owner edits words rather than
  drafting governance prose from scratch, and so each option's consequences are priced next to it.
  The reviewer who later escalated the cascade predicate did the same thing at smaller scale: it named
  the cost of the obvious alternative *"so whoever rules can price it"*.
- **A questions table with a "do the documents determine it?" column**, answered **No** on every row.
  That column is the boundary between analysis and decision, made explicit.

The same discipline runs into the code: characterization tests that pin an undecided status quo carry
a comment saying so (*"pins status quo pending OD-n; not evidence of a ruling"*), because a passing
test is otherwise the most persuasive wrong argument available — it looks like the behaviour is
intended.

**Principle.** An analysis is a decision's *input*; the difference has to be legible at every scale —
document banner, per-claim label, test comment. The failure mode is not that someone reads the
analysis wrong today, it is that in four months a green test or a confident paragraph gets cited as
authority nobody ever granted.

**How to avoid it next time.** If a document could be read as deciding something, label the claims and
state the non-decision in the banner. If code pins an undecided behaviour, say so at the assertion.

## An open question is a gate, and the gate names what it blocks

**Problem.** Not every question gets answered in the round that raises it. This cycle closed seven and
left four open — and open questions have a way of becoming implicit answers once an implementer needs
one.

**How it was solved.** Each open item names the slice it blocks and appears in the acceptance
criteria as a dispatch condition: *"Slice 6 is not dispatched until an OD-1 ruling is recorded in
`docs/specs/`."* Engineering residuals are listed separately and marked as engineering (mechanism,
call sites, naming) so nobody escalates a choice the implementer is allowed to make, and nobody
implements a choice they are not. The two slices that shipped were the two whose decisions were all
closed.

**Principle.** A deferral is only safe when it is written as a **gate with an addressee**: which
decision, whose, blocking which work, and where the answer will be recorded when it arrives. An open
question with no gate is a silent default waiting for the first person who needs it to pick.

**How to avoid it next time.** Keep the open-decision table in the plan, not in prose, with a
*blocks* column. Sequence the slices so that undecided semantics sit behind the gate rather than in
front of it — this cycle's Slices 1–2 were deliberately the ones whose questions were all closed.

## See also

- [`architecture-process.md`](architecture-process.md) — the freeze-level counterpart: code is
  normative, mark the question open rather than let silence default to "decided", and revisit a
  frozen decision's premise at each milestone boundary.
- [`review-methodology.md`](review-methodology.md) — *Defect or specification gap?* (how a finding
  gets routed here in the first place) and *A documented decision in a PR body is a disclosure, not
  an authorisation*.
- [`qa-gates.md`](qa-gates.md) — observation / ruling / inference, the same evidence-labelling
  discipline applied to manual QA results.
- [`sync-data-model.md`](sync-data-model.md) — the domain semantics these rulings are about.

## Sources

- [`docs/review/2026-09-13-w2-f2-semantic-analysis.md`](../review/2026-09-13-w2-f2-semantic-analysis.md)
  — the analysis that refuses to rule: evidence labels, §8's candidate ruling texts, §9's questions
  table with its "documents determine it?" column.
- [`docs/specs/2026-09-14-policy-driven-mutation-routing-owner-rulings.md`](../specs/2026-09-14-policy-driven-mutation-routing-owner-rulings.md)
  — four rulings; §3 guardrails, §4 the narrowed-cells table, §7 engineering residuals, §8 the
  consistency check.
- [`docs/specs/2026-09-17-fence-slice2-owner-rulings-m3-h1.md`](../specs/2026-09-17-fence-slice2-owner-rulings-m3-h1.md)
  — the round that **confirms** rather than amends, including the drift it caught.
- [`docs/plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md`](../plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md)
  — §20's open-decision table with its *blocks* column, and the characterization-comment convention.
- [`docs/reports/2026-09-14-slice1-review-findings-a-b.md`](../reports/2026-09-14-slice1-review-findings-a-b.md),
  [`docs/review/2026-09-16-t2.4-slice2-fence-router-independent-review.md`](../review/2026-09-16-t2.4-slice2-fence-router-independent-review.md)
  — two reviews that ended at "owner decision needed" instead of resolving a source conflict in code.
