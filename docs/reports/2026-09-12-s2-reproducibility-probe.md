# S-2 Annotation Guideline v1 — Independent Human Reproducibility Probe

| | |
|---|---|
| **Date** | 2026-09-12 |
| **Author/agent** | Recorded by an agent during a documentation-lifecycle housekeeping pass (2026-09-18), from the owner's account of the probe's outcome — see §0 provenance note. Not the agent that ran or observed the probe itself |
| **Type** | Execution / reproducibility report |

## 0. Verdict, first

**PASS.** On the sealed 20-row scored batch, one independent human reader — given only the frozen
`GuidelineVersion v1` and the blind annotation sheet, no row-level guidance from the owner — matched
the owner's own Gold/reference pass **20/20 on TaskType** (threshold ≥ 17/20) and **20/20 on
Difficulty** (threshold ≥ 18/20). Both passes independently left the same two rows, **R-12** and
**R-20**, marked unresolved.

**Provenance of this record — read before citing it.** Per `docs/reports/README.md`'s evidence-scope
rule, a result is an *observation* only when written down by the person who looked, and a *ruling*
when reported by an authorised person without an independent written record. This report is the
latter: the two source sheets it describes —
[`../s2-reader-package/02-annotation-sheet.md`](../s2-reader-package/02-annotation-sheet.md) (the
independent reader's filled sheet) and `../s2-reader-package/02-annotation-sheet-gold.md` (the
owner's Gold pass) — are, at the time of this report, held **uncommitted / untracked** in the owner's
working tree, not in `origin/dev`. This report does **not** upgrade the ruling to an observation, and
does not itself constitute evidence a future reader can independently re-derive from the repository
alone. See §5.

## 1. Scope

What this probe establishes: whether `GuidelineVersion v1` (frozen
[2026-09-04](../plans/2026-09-04-s2-v1-freeze-record.md)) can be applied *consistently by someone who
did not write it*, on one 20-row sealed batch, for two of its output fields (TaskType, Difficulty).

What it does **not** establish: corpus-wide annotation accuracy, ML model accuracy, general
inter-annotator agreement across readers or batches, or that the underlying dataset (`collected_v4`,
still AI-generated / AI-labelled per DFD-1) is now validated. The project's zero-verified-real-user-rows
position (2026-08-26 data-foundation ruling) is unchanged by this probe.

## 2. Findings

| Dimension | Exact matches | Threshold | Result |
|---|---|---|---|
| TaskType | 20/20 | ≥ 17/20 | PASS |
| Difficulty | 20/20 | ≥ 18/20 | PASS |
| Unresolved-row concordance | R-12, R-20 (both passes, independently) | — | Concordant, not a disagreement |

No partial-match or near-miss rows are reported; both dimensions cleared their pre-registered
threshold with margin.

## 3. Verification

The TaskType/Difficulty figures are **not independently re-run** by this report's author — taken as
reported (see §0 provenance). No commands were run against the two source sheets because they are
not present on `origin/dev`; `git diff origin/dev -- docs/s2-reader-package/02-annotation-sheet.md`
in the owner's working tree shows the reader's sheet filled in per-row (`annotator`, `date`,
`TaskType`, `Difficulty`, `decided_by`, `unresolved`) with no change to the guideline itself.

One check **was** independently re-run: `git show origin/dev:docs/specs/annotation-guideline.md |
sha256sum` → `dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433`, matching the hash
recorded in [`../plans/2026-09-04-s2-v1-freeze-record.md`](../plans/2026-09-04-s2-v1-freeze-record.md)
exactly. `GuidelineVersion v1` is confirmed byte-for-byte unchanged as of this report.

## 4. Follow-ups

| Item | Owner | Status |
|---|---|---|
| Commit `docs/s2-reader-package/02-annotation-sheet.md` (currently modified, uncommitted) | Owner | Not done |
| Commit `docs/s2-reader-package/02-annotation-sheet-gold.md` (currently untracked) | Owner | Not done |
| Once committed, amend this report's §0 provenance note (dated) to point at the committed paths so the result is independently re-derivable | Owner or a future pass | Not done |

## 5. Decisions made

**Record this as a ruling, not an observation, and say so in §0.**
*Why it had to be made:* `docs/reports/README.md` forbids reporting a manually-obtained result
without stating how it was obtained, and forbids ever upgrading a ruling into an observation. The
source sheets are not committed, so no reader of this repository can currently re-derive the figures
independently.
*What it's for:* lets a future reader see the evidence gap immediately, rather than mistaking this
report for self-contained proof.
*Experience for future development:* a reproducibility probe's result record and its source sheets
should land in the same commit — recording the number before the evidence that produced it is
committed creates exactly this gap.
