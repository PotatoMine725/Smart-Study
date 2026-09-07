# Data Foundation — DFD-1 correction pass

**Date:** 2026-08-26 · **Author:** agent (Claude Opus 5), on owner instruction ·
**Type:** execution report

## 0. What was done

The owner ruling of 2026-08-26 (**DFD-1**) requires that documents asserting `collected_v4` is real
user data be corrected — *"Do not rewrite history. Use dated amendments/corrections according to
project documentation convention."*

**Executed: 12 documents and 3 tool files corrected.** No document was rewritten into a cleaner story;
every superseded passage is still readable where it was written, marked as superseded.

**One correction turned out to be correcting a correction.** The roadmap footnote and the encoder
spec both already carried a 2026-08-24 annotation explaining why the 96.2% figure is not a
generalization number. The conclusion was right; the mechanism they gave was **chronologically
impossible**, and both have now been replaced with the verified chronology.

## Scope

**In scope:** the DFD-1 lifecycle correction, and the two documentation defect candidates the decision
brief listed as inside DFD-1's scope (its Follow-ups #3 and #4).

**Out of scope, deliberately:**

- Retracting any measurement. Nothing measured was wrong — provenance changed, arithmetic did not.
- Amending normative requirements. The encoder specification's `EVA-*` requirements stand as ratified;
  only factual prose around them is corrected.
- The encoder initiative's status. It stays `stopped_at_s0`; §18 of the ruling says these decisions do
  not reopen it.
- `Prompt/` — gitignored, therefore untracked and uncitable. This is why the ruling was filed into
  `docs/plans/` carrying its full text rather than as a link.

## Findings

### 1. The audit's §F.3 list was incomplete — 8 named, 12 documents actually affected

§F.3 of the audit listed eight documents asserting `collected_v4` is real. A fresh sweep
(`grep` for `real`/`collected`/`user-authored` co-occurring with `collected_v4`, plus `205 real`,
`real held-out`, `real input`, `real rows` across `docs/`, `datasheets/`, `tools/`, `SmartStudyPlanner/`)
found **four more**:

| Additional site | Why §F.3 missed it |
|---|---|
| `docs/plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md:419, 506` | Says *"real collected input"* / *"the real held-out subset"* without the literal string `collected_v4` adjacent |
| `docs/CHANGELOG.md:30, 34` | *"real test rows"*, *"real held-out rows"* — the corpus is named nowhere near the word |
| `docs/active/README.md:56, 57, 71` | Same shape, in the highest-traffic status document in the repository |
| `docs/knowledge/ml-experimentation.md:87, 244, 264` | *"the real evaluation set"*, *"real collected data"* — a **knowledge article**, i.e. the artifact type explicitly meant to outlive the initiative |

`[analysis]` The claim propagated in two forms: as *"real `collected_v4`"* (which §F.3's search shape
caught) and as a bare *"the real rows"* once the reader was assumed to know which rows those were. The
second form is the more durable error, because it survives into documents that never name the corpus —
including the knowledge article, whose whole purpose is to be read after everything else is archived.

### 2. Two tiers were applied, by artifact type

Per `docs/README.md`: *live artifacts are edited in place, because their job is to be current; dated
artifacts are corrected by appending a dated amendment and marking the superseded passage in place.*

**Tier 1 — live artifacts, corrected in place:**

| File | What changed |
|---|---|
| `docs/specs/system_roadmap.md` | Footnote ¹ (96.2% provenance) rewritten with the verified chronology; §A.4's F-1 entry — *"measured on real input against a model trained on synthetic rows"* — corrected, with the widened gap stated |
| `docs/knowledge/machine-learning.md` | The confidence-curve section: *"205 real held-out rows"* and *"real `collected_v4` input"* |
| `docs/knowledge/ml-experimentation.md` | Three passages + a `> Correction` block on the dataset-maturity lesson, which restates the lesson in a form that does not depend on the test set being real |
| `docs/active/README.md` | Three status rows |
| `docs/CHANGELOG.md` | Two 2026-08-25 bullets marked in place; a new dated `2026-08-26` entry added |
| `datasheets/vn_input_fixtures.md` | The datasheet's provenance sentence — a **file-level datasheet** under DFD-5, so wrong provenance here is the policy's own failure mode |

**Tier 2 — dated artifacts, amendment appended + superseded passage marked:**

`docs/specs/2026-08-24-neural-encoder-smart-parser.md` · `docs/reports/2026-08-25-encoder-pilot.md` ·
`docs/reports/2026-06-25-m8a-textclassifier-v4-recall-eval.md` ·
`docs/plans/2026-08-24-edge-ai-encoder-adoption.md` ·
`docs/plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md` ·
`docs/plans/2026-08-24-edge-ai-encoder-owner-decision-handoff.md`

In each, the load-bearing occurrences are marked in place — struck through where the passage is a
short table cell, and left verbatim under a dated *"as written …; superseded"* note where striking
through would have damaged the prose (the encoder spec's `EVA-03` row and its §6.1 paragraph) — and a
single `## Amendment, 2026-08-26` section withdraws the claim document-wide. **Non-load-bearing occurrences
were deliberately left unedited** — a document with twenty scattered instances of "real" would need
twenty rewrites to satisfy a search-and-replace standard, and the result would be a document that no
longer shows what its authors believed. The amendment states this explicitly so a reader knows the
remaining instances are covered, not missed.

`docs/plans/2026-08-24-edge-ai-encoder-owner-decision-handoff.md` is an **owner record**: not one word
of its body was touched. Its amendment says so.

### 3. The 96.2% annotations were themselves wrong — and this is the load-bearing finding

Two live documents already carried a 2026-08-24 correction of the 96.2% figure:

> *"it was measured **after** the 205 real `collected_v4` rows had been merged into the training seed"*
> — `system_roadmap.md:40`
>
> *"The real rows were merged into the training seed **before** it was measured."* — encoder spec §6.1,
> tagged **`[fact]`**

**Both are impossible** `[verified — the brief's §2.1 chronology, re-read against CHANGELOG and git]`:
96.2% was measured **2026-06-05** at the 698-row v3 seed (n=106 fits 698 × 0.15; 903 × 0.15 ≈ 135 does
not), and `collected_v4.csv` entered the repository **2026-06-18** — thirteen days later. At the moment
of measurement the seed contained zero `collected_v4` rows.

**Both are now replaced with the verified chronology**, and both replacements say what the previous
annotation got wrong rather than quietly overwriting it. The conclusion — *not a generalization number,
do not cite as a synthetic→real baseline* — was correct all along and is now **stronger**: the figure
is an in-distribution score over a corpus that was, on that date, entirely authored.

The same impossible mechanism was also embedded in `tools/ml-pilot/split/build_split.py`'s module
docstring and in the `SPLIT.md` it generates. Both corrected.

`[analysis]` **This is why DFD-1 could not be executed as a search-and-replace.** A pass that swapped
`real → synthetic` would have preserved two false statements and re-tagged one of them `[fact]`. The
corrections already in the repository were written from assumption — the identical failure mode as the
original claim, running in the opposite direction.

### 4. Generated artifacts were corrected at the generator, then regenerated

`tools/ml-pilot/split/SPLIT.md` is written by `build_split.py`. Hand-editing it would have been
silently reverted by the next rebuild — and `SPLIT.md` advertises its own rebuild command, so a future
reader would have produced the reversion themselves while believing they were verifying the file.

Corrected in the generator's template strings, then regenerated by running the script. `train.csv` and
`test.csv` came back **byte-identical** (they do not appear in `git status`), which independently
confirms the seed is unchanged and that only prose moved.

### 5. Corrections carry their provenance grade

Every correction states that the new provenance is **owner recall — a ruling, not an observation**,
with no written collection record. Wording used throughout: *AI-generated (Meta AI, from owner
templates) and AI-labelled (GitHub Copilot), established by owner recall 2026-08-26.*

`[analysis]` The failure being corrected is *a claim asserted as `[fact]` with no record behind it*.
Replacing it with a differently-sourced unlabelled claim would repeat the failure with new content. The
recall is strong — it agrees with seven independently measured distributional regularities and an exact
quota match — but agreement is corroboration, not documentation.

### 6. What the corrections change, and what they do not

| | |
|---|---|
| **No measurement retracted** | Row counts, class distributions, unseen-token rates, macro-F1 per arm, the confidence bins, export timings — all computed over repository bytes, all unaffected by provenance |
| **Inferences narrowed** | Every train→test comparison is authored→authored. None of them is evidence about production input |
| **One conclusion strengthened** | 96.2% is *further* from being a generalization number than the previous annotation claimed |
| **One gap widened** | F-1's *"measured on real input"* is withdrawn. The confidence gate's behaviour on real student input has **never** been measured, and cannot be until DFD-9a lands |
| **Nothing reopened** | The Edge AI initiative stays stopped; `ML_Heuristic_design.md` §9.1 stays in force; PD-1…PD-10 are not withdrawn; `EVA-*` requirements are untouched |

## Verification

| Check | How | Result |
|---|---|---|
| Every §F.3 site corrected | Re-grep of all 8 named sites, enumerated below | **8/8** — see the enumeration under this table |
| No uncorrected claim left | `grep -riE "real[^.]{0,60}collected_v4\|205 real\|real held-out"` over `docs/ datasheets/ tools/ SmartStudyPlanner/`, minus corrected/quoted/amended lines | **0 remaining.** All residual hits are (a) the audit's §F.3 evidence quotes, which must stay verbatim, (b) amendment text, (c) non-load-bearing occurrences inside amended documents, covered document-wide |
| Amendments applied exactly once | Script asserts `"## Amendment, 2026-08-26" not in file` before appending | 6/6 appended, no duplicates |
| Every in-place edit matched a unique anchor | Each replacement asserted `count(old) == 1` before substituting | All passed; no silent no-op edits |
| Generated file matches its generator | Ran `python tools/ml-pilot/split/build_split.py` | `wrote train.csv, test.csv, SPLIT.md`; exit 0; counts re-asserted at 698 / 205 / 903; leak 0; **`train.csv` / `test.csv` unchanged in `git status`** |
| Chronology behind the 96.2% correction | Re-read the brief's §2.1 table against `CHANGELOG.md` and the cited commits (`9603c17`, `8855874`, `ab5112c`) | Confirmed: 2026-06-05 vs 2026-06-18, and n=106 fits 698 not 903 |
| Build / test suite | **NOT RUN** | No production code was touched. `build_fixtures.py` and `build_split.py` are outside the solution; `build_split.py` was executed and exited 0. The .NET suite is unaffected by this pass and running it would prove nothing about it |

### The 8 §F.3 sites, enumerated

Asserted as a verification result above, so it is countable here rather than left as a number.

| # | §F.3 site | How it was corrected |
|---|---|---|
| 1 | `docs/specs/2026-08-24-neural-encoder-smart-parser.md:361` (`EVA-03`, tagged `[fact]`) | Row left **verbatim**; dated superseded note added beneath the table withdrawing *real* and its `[fact]` tag, plus the document-level amendment |
| 2 | `tools/ml-pilot/split/SPLIT.md` | Corrected **at its generator** (`build_split.py`) and regenerated |
| 3 | `docs/reports/2026-08-25-encoder-pilot.md:558` | Marked in place (*"as written 2026-08-25 … superseded"*) + amendment |
| 4 | `docs/plans/2026-08-24-edge-ai-encoder-adoption.md:140` | Table cell struck through with the corrected value + amendment |
| 5 | `docs/knowledge/machine-learning.md:105` | Edited in place (live artifact) |
| 6 | `docs/specs/system_roadmap.md:180` | Edited in place (live artifact), with the widened gap stated |
| 7 | `datasheets/vn_input_fixtures.md:80` | Edited in place — provenance sentence replaced and dated |
| 8 | `tools/ml-pilot/build_fixtures.py:13` | Edited in place — module docstring |

Sites 5–8 carry **no strike-through**, by design: they are live artifacts, whose convention is
correction in place. Each states the correction and its date in the replacing text.

## Follow-ups

**Nothing here is committed work.**

| # | Item | Owner | Where it belongs | Status |
|---|---|---|---|---|
| 1 | The audit's §F.3 search shape missed the bare *"the real rows"* form — 4 documents, including a knowledge article | Agent, if it recurs | `docs/knowledge/review-methodology.md` | **Knowledge only — not yet written.** A provenance sweep must search the *claim*, not the corpus name |
| 2 | `datasheets/` has no file-level datasheet for `collected_v4.csv` itself — the corrected provenance lives in `vn_input_fixtures.md`, a *consumer* | Owner | DFD-5 implementation, Data Maturation proposal | **Deferred** — DFD-5 requires one; writing it now would pre-empt the proposal's provenance design |
| 3 | Brief Follow-up #6 (does `FeaturizeText` lowercasing reach the model?) is untouched by this pass | Owner to schedule | Investigation report, ~1h | **Deferred — cheap, unresolved** |
| 4 | Brief Follow-up #8 (lessons about provenance-as-a-control and corrections-written-from-assumption) | Agent | `docs/knowledge/ml-experimentation.md` or `machine-learning.md` | **Knowledge only — not yet written** |
| 5 | **Pre-existing broken link**, found by this pass's link check, not caused by it: `2026-06-25-m8a-textclassifier-v4-recall-eval.md:5` cites `../plans/2026-06-16-m8a-textclassifier-retrain.md`, archived in the 2026-08-02 sweep | Agent | The citing report, or the archive note | **Defect candidate (documentation) — not fixed here**, outside DFD-1's scope |

## Decisions made

### D-1 — Corrected by artifact type, not by document age

**Why it had to be made.** DFD-1 says *"dated amendments/corrections according to project documentation
convention"*, and the convention has two branches: live artifacts edited in place, dated artifacts
amended. Sixteen files needed correcting and the branch is not obvious for all of them — a `stopped_at_s0`
plan is dated in spirit but lives in `plans/`, and a knowledge article is undated but describes a past
experiment.

**What it's for.** The test applied was *"is this document's job to be current?"* — roadmap, knowledge,
`active/README`, datasheets and tool source say yes and were edited in place; the encoder initiative's
spec, proposal, execution plan, handoff and reports say no and were amended. A reader of the roadmap
needs the truth now; a reader of the pilot report needs to know what the pilot believed.

**Experience for future development.** Age is the wrong discriminator; *purpose* is the right one. The
knowledge articles are the case that proves it — undated, describing 2026-08 work, and edited in place
without hesitation, because a knowledge article that preserves a superseded lesson has failed at its
only job.

### D-2 — Marked the load-bearing occurrences and left the rest, saying so

**Why it had to be made.** The encoder proposal alone contains a dozen uses of "real" about
`collected_v4`. Correcting all of them is a rewrite; correcting none leaves a document that reads as
authoritative and is wrong.

**What it's for.** Each amended document strikes through the specific passages a reader would *cite*
(the dataset table row, the split definition, the scope-of-claim sentence) and adds one amendment that
withdraws the claim document-wide, explicitly stating that remaining instances are covered rather than
overlooked. A reader can trust that the absence of a marker means "covered by the amendment", not
"nobody checked".

**Experience for future development.** The rule that made this decidable: **mark what a reader would
quote, amend what a reader would conclude.** Without the explicit "the remainder are deliberately not
edited" sentence, the pass would look half-finished — and the next person would finish it by rewriting
history.

### D-3 — Fixed the generator, not the generated file

**Why it had to be made.** `SPLIT.md` carried two corrections' worth of wrong text and is produced by
`build_split.py`, which `SPLIT.md` itself tells the reader how to run.

**What it's for.** The generator's template strings were corrected and the script re-run. `train.csv`
and `test.csv` came back byte-identical, which turned the regeneration into a verification step as well
as a fix.

**Experience for future development.** A hand-edited generated file is a correction with an expiry
date — and worse, the document invited the reader to run the command that would silently undo it. When
a corrected file names its own rebuild command, correcting the generator is the only fix that holds.

### D-4 — Corrected the correction, and said that it had been wrong

**Why it had to be made.** The 96.2% annotations were not merely incomplete: the mechanism they
asserted is chronologically impossible, and one carried a `[fact]` tag. Silently replacing them with
the right chronology would have hidden that a *correction* had been written from assumption.

**What it's for.** Both replacements name the withdrawn mechanism before giving the verified one. The
project's most reusable lesson from this pass is not *"collected_v4 is synthetic"* — it is *"a
correction written without checking the lineage is another wrong claim, wearing the clothes of a
fix."*

**Experience for future development.** Verify the chronology of a claim before writing its correction,
and date-stamp corrections so the next reader can tell which of two annotations is newer. Two dated
annotations disagreeing is a recoverable situation; one undated annotation silently replacing another
is not.

### D-5 — Ran the extra sweep instead of executing §F.3's list as given

**Why it had to be made.** §F.3 was an audit artifact, not a work order, and audits scope their own
searches. Executing exactly eight corrections would have satisfied the letter of OD-1.

**What it's for.** The sweep searched for the *claim* in every form it might take — `205 real`,
`real held-out`, `real input`, `real rows` — not just for `real` near `collected_v4`. It found four more
documents, including `active/README.md` (the most-read status file in the repository) and a knowledge
article.

**Experience for future development.** When a list of affected sites comes from a document whose author
declared the correction out of scope, treat the list as a starting point. The sites that matter most
are the ones that dropped the subject of the claim — those are the documents that had already absorbed
it as background truth.
