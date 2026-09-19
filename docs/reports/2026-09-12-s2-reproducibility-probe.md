# S-2 Annotation Guideline v1 — Independent Human Reproducibility Probe

| | |
|---|---|
| **Date** | 2026-09-12 (probe performed; in-sheet date of the independent pass) |
| **Author/agent** | Recorded by an agent during a documentation-lifecycle housekeeping pass (2026-09-18), from the owner's account of the probe's outcome; reconciled before merge on 2026-09-19 against owner ruling `F-1`/`D-6` — see the note below. Not the agent that ran or observed the probe itself |
| **Type** | Execution / reproducibility report — **subordinate** to the formal result record |

> **Reconciled 2026-09-19, before this report was merged.** It was first drafted in parallel with,
> and without sight of, the owner's `F-1`/`D-6` ruling (2026-09-18). That ruling is now recorded in
> [`../plans/2026-09-05-s2-owner-decisions.md`](../plans/2026-09-05-s2-owner-decisions.md) §`F-1`, and
> **the formal §10 result, with every required companion figure, is
> [`2026-09-18-epic4-s2-section10-result.md`](2026-09-18-epic4-s2-section10-result.md) — cite that
> record, not this one.** This report is kept as the dated note that the probe was performed and
> records its scope and evidence-custody limits. It does not restate figures that the formal record
> requires to travel together.

## 0. Verdict, first

**PASS under owner ruling `F-1`/`D-6` (Reading A)** — a shared `unresolved=true` between the Owner
Gold pass and the independent reader counts as exact agreement for §10 scoring.

What was observed: on the sealed 20-row scored batch, one independent human reader — given only the
frozen `GuidelineVersion v1` as rendered in Vietnamese and the blind annotation sheet, no row-level
guidance from the owner — and the owner's own Gold pass agree **cell-for-cell on all four recorded
fields across all 20 rows**. Both passes independently left the same two rows, **R-12** and **R-20**,
marked `unresolved=true`; they remain so, unedited, in both sheets.

**The verdict depends on the ruling.** On 2026-09-12 the result was **not yet scorable**: `D-6` had
deliberately left open how an `unresolved` response scores, and under the other available reading
the primary 16-row Difficulty figure falls below its threshold. The owner ruled Reading A on
2026-09-18. The 20-row and primary 16-row figures, and the `G2`/`G4`/`C4`/`D-2` companions, are in the
formal record linked above and are not to be quoted apart from one another.

**Provenance — three separate things, read before citing.**

1. **The owner ruling is formal and recorded.** `F-1` is written into the D-series decision file
   (linked above), verbatim, as a post-hoc ruling on record as such.
2. **The result is documented in the repository.** The formal result record reports figures that an
   agent checked read-only against the two sheets in the owner's working tree (sheet hashes, the
   `unresolved` field on every row, and the reader-package validator), graded there by how each fact
   is known.
3. **Source-sheet custody is still limited.** The two filled sheets — the independent reader's
   returned `docs/s2-reader-package/02-annotation-sheet.md` (a *modified, uncommitted* working-tree
   file; the committed file at that path is the **blank** instrument, not the returned sheet) and the
   owner's Gold pass `docs/s2-reader-package/02-annotation-sheet-gold.md` (untracked) — are **not in
   `origin/dev`**. A future reader cannot re-derive the figures from the repository alone. Where and
   whether to commit them is the open owner decision **`F-4`**.

## 1. Scope

What this probe establishes: whether `GuidelineVersion v1` (frozen
[2026-09-04](../plans/2026-09-04-s2-v1-freeze-record.md)) as rendered in Vietnamese can be applied
*consistently by someone who did not write it*, on one sealed 20-row batch.

What it does **not** establish: corpus-wide annotation accuracy, ML model accuracy, general
inter-annotator agreement across readers or batches, the behaviour of any future annotator, or that
the underlying dataset (`collected_v4`, still AI-generated / AI-labelled per DFD-1) is now validated.
The project's zero-verified-real-user-rows position (2026-08-26 data-foundation ruling) is unchanged
by this probe.

What it does **not** authorize: the §10 PASS satisfies `DFD-2`'s single stated condition and nothing
further. It does not authorize `S-3` implementation, labelled-data collection/import/generation,
dataset promotion, `S-7.1` Gold-A eligibility, controlled expansion, or canonical Epic 4
(`T4.1`–`T4.3`, ML Maturation), which has **not started** — see the freeze record's
"Gates — status update, appended 2026-09-18".

## 2. Findings

| Item | Finding |
|---|---|
| Agreement | Cell-for-cell on all four recorded fields (`TaskType`, `Difficulty`, `decided_by`, `unresolved`), all 20 rows |
| Unresolved rows | `R-12`, `R-20` — `unresolved=true` in both passes, independently |
| Scoring basis | Owner ruling `F-1` (Reading A), 2026-09-18 |
| §10 gate | **PASS** under Reading A — figures and companions in [`2026-09-18-epic4-s2-section10-result.md`](2026-09-18-epic4-s2-section10-result.md) §3 |

## 3. Verification

The agreement figures were **not independently re-run by this report's author** — the independent
read-only checks are recorded in the formal result record's §8. No commands were run against the two
source sheets by this pass; `git diff origin/dev -- docs/s2-reader-package/02-annotation-sheet.md` in
the owner's working tree shows the reader's sheet filled in per-row (`annotator`, `date`, `TaskType`,
`Difficulty`, `decided_by`, `unresolved`) with no change to the guideline itself.

One check **was** independently re-run: `git show origin/dev:docs/specs/annotation-guideline.md |
sha256sum` → `dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433`, matching the hash
recorded in [`../plans/2026-09-04-s2-v1-freeze-record.md`](../plans/2026-09-04-s2-v1-freeze-record.md)
exactly. `GuidelineVersion v1` is confirmed byte-for-byte unchanged as of this report.

## 4. Follow-ups

| Item | Owner | Status |
|---|---|---|
| Decide where the two annotation sheets should live, and whether/when to commit them (`F-4`) | Owner | **Open — needs an owner decision**; not taken here |
| Once `F-4` is settled and the sheets are committed, append a dated amendment here pointing at the committed paths | Owner or a future pass | Recommendation, deferred until `F-4` |

## 5. Decisions made

### 5.1 Keep this report as a subordinate dated record rather than a second result record

*Why it had to be made:* this report and the formal result record were drafted in parallel, and this
one first stated the PASS without the ruling it depends on and without the primary 16-row figures that
`D-7` requires to travel with the 20-row ones. Two records reporting the same result differently would
drift.
*What it's for:* the formal record is the one place the result and its companions live; this report
keeps what it alone added — the note that the probe was performed, its scope, and the custody gap —
and points there for everything else.
*Experience for future development:* when two sessions document one result in parallel, reconcile to
a single authoritative record before merge and make the other point at it, rather than keeping two
tables of figures.

### 5.2 Keep the ruling, the result documentation and source-sheet custody separate

*Why it had to be made:* the first draft called the result "an owner-reported ruling, not an
observation", which was accurate before `F-1` was written down but would read afterwards as if the
ruling itself were informal. `docs/reports/README.md` also forbids upgrading a ruling into an
observation.
*What it's for:* a reader can see exactly which part is formal (the ruling), which is documented (the
result, with graded checks) and which is still limited (the source sheets are uncommitted, `F-4`).
*Experience for future development:* a reproducibility probe's result record and its source sheets
should land in the same commit — recording the number before the evidence that produced it is
committed creates exactly this gap.
