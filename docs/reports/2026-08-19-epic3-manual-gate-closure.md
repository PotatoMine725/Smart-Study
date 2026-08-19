# Report — Epic 3 manual QA gate: closure

**Date:** 2026-08-19 · **Branch:** `docs/epic3-manual-gate-closure` (off `dev` at `208c7f1`)
**Runbook:** [`docs/plans/2026-08-10-epic-3-manual-qa-runbook.md`](../plans/2026-08-10-epic-3-manual-qa-runbook.md)
**Owner records:** [2026-08-10](2026-08-10-epic3-soe-manual-observation.md) ·
[2026-08-19](2026-08-19-epic3-manual-observation-updated.md)
**Reads with:** [Epic 3 closing note](2026-08-07-epic3-closing-note.md) ·
[automated QA gate](2026-08-10-epic3-automated-qa-gate.md) ·
[stale-chart fix report](2026-08-14-workload-balancer-stale-chart-fix-report.md)

---

## 1. Outcome

**PASS WITH FINDINGS — one condition of §6.1 is unrecorded rather than failed.**

Every scenario that has a recorded observation passed. Nothing in the gate failed, and no scenario
produced a defect. The single reason this note does not say "closed" outright is **E1–E4**, which
have no written observation in either owner record (§6).

The runbook's own instruction on this point is followed literally: *"Do not summarise a
partially-executed run as a pass — record which scenarios were skipped and why."*

## 2. Scenario ledger

Verdicts belong to the owner; this table points at where each one is recorded and does not restate
the owner's wording.

| # | Scenario | Recorded where | Status |
|---|---|---|---|
| A1–A3 | Startup, schema migration, idempotent relaunch | owner 2026-08-10 | pass |
| B1 | `OptimizerRunLogs` exists | owner 2026-08-10 | pass |
| B2 | `OptimizerRunLogs` empty (0 rows **is** the pass) | owner 2026-08-10 + screenshot | pass |
| C1 | Dense contiguous packing from today | owner 2026-08-10 | pass |
| C2 | Shape holds at 1 / 3 / 8 giờ | owner 2026-08-19 (re-run) | pass |
| C3–C6 | Bounds · priority order · part labels · completed excluded | owner 2026-08-10 | pass |
| C7 | Header copy ruling (finding QA-1) | ruled 2026-08-10, shipped `f120df5` | answered |
| C8 | Drag moves readout + badge, no bar moves | owner 2026-08-19 | pass |
| C9 | Button clears badge, rescales **and** re-allocates | owner 2026-08-19 | pass |
| C10 | `capacity.txt` = `12` → 8.0 no badge; `4.5` → 4.5 no badge | owner 2026-08-19 | pass |
| D1–D2, D4–D5 | Empty · unscored task · overdue · >7 days | owner 2026-08-10 | pass |
| D3 | Past-deadline placement | owner 2026-08-10 | observed; ratified limitation A1 / Decision D7 |
| E1–E4 | Dashboard · Analytics · CRUD · focus & streak | **nowhere** | **not recorded** (§6) |
| E5 | Capacity survives restart | write half: owner 2026-08-19; restart half: owner in session 2026-08-19 | pass |
| E6 | Subject delete with ≥2 tasks | owner in session 2026-08-19 | pass |

**Provenance note on E5's restart half and E6.** Both were run by the owner on 2026-08-19 after the
`fix/workload-balancer-stale-chart` merge, against
`bin/Release/net10.0-windows10.0.19041.0/SmartStudyPlanner.exe` (mtime `2026-08-14 08:38`), which
`git diff origin/dev 768aeb6 -- '*.cs' '*.xaml'` shows to be code-identical to merged `dev`. The
result was reported in session as "every test pass" — a verdict, without the per-step figures (the
sibling subject's task count before and after) the steps asked for. It is recorded here as the owner
gave it. Anyone re-reading this later should know it is a terser record than the two written
observation files, not an equal one.

## 3. What the manual gate proved that automation could not

Three things had no automated reach, and two of them could have changed the code:

1. **M5b / C10 at `4.5`.** Before it ran, acceptance criterion 5 of the stale-chart fix rested on a
   *reading of the WPF source* — that `IsSnapToTickEnabled` snaps on the user-interaction path, not
   in the coercion callback a binding write takes. Had it snapped and written back, a badge would
   have appeared on an untouched page and a further clamp decision was reserved for that case. It did
   not snap. Reasoning was replaced by evidence.
2. **C8 / C9 — the badge's whole reason to exist.** The suite can assert `IsScheduleStale` and the
   binding paths; only a human can confirm that no bar moved on a drag and that the button both
   rescaled and re-allocated.
3. **E6 — subject delete.** `HocKyRepository_DeleteMonHocByAbsence_CascadesTombstoneToItsTasks`
   covers a subject with **one** task and never asserts that a sibling subject survives, so the
   over-cascade class E6 exists to catch was not covered by the suite at all (§4.3).

## 4. Findings carried out of the gate — none are defects

### 4.1 The capacity slider stops only on whole hours
A `capacity.txt` of `4.5` is read, clamped and displayed correctly, but cannot be dialled back in by
hand. Not a stale-chart failure: `BuildSchedule` unconditionally assigns
`RenderedCapacityHours = CapacityHours`, so the badge is always clearable and no user can be trapped
in a stale view. **Owner's ruling: enhancement candidate for a later stage.**

### 4.2 Past-deadline placement (D3)
Observed in the running product, as expected. Ratified limitation A1, Decision D7 of 2026-08-07.
Recorded, not logged as a defect.

### 4.3 E6's automated coverage is narrower than E6 itself — recommended follow-up
The GUI run passed, so the behaviour is right today. The suite would not notice if it stopped being
right: no test deletes a subject with ≥2 tasks, and none asserts that a *sibling* subject's tasks are
untouched. That is precisely the regression class the `LuuHocKyAsync` cascade-fixup ordering exists
to prevent (FK reassignment plus `DetectChanges()` **before** `Remove()`,
`SqliteHocKyRepository.cs:136–151`). **Recommended:** one repository-level test closing that gap.
Cheap, and it converts a passing manual observation into a standing guard.

### 4.4 No semester-management UI
Unchanged. Semesters can be created but not renamed or deleted — the reason the original E6 was
unexecutable and had to be retargeted. Already filed as its own proposal
(`docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` §7); deliberately not absorbed
into a bug-fix package.

## 5. Decisions made

**D1 — Report the gate as PASS WITH FINDINGS, not PASS.**
*Why:* §6.1 names E1–E6 as a condition and E1–E4 have no written observation. They may well have been
exercised on 2026-08-10 without being written down, but an unwritten check and a passed check are not
the same artifact, and the difference is invisible six months from now.
*What for:* so that "the Epic 3 manual gate passed" cannot later be read as covering screens nobody
recorded looking at.
*Experience:* the withdrawal of C2's first "Met" is the precedent — a recorded verdict read through a
faulty instrument cost a full re-run. Unrecorded verdicts are the same failure one step earlier.

**D2 — Record the owner's E5/E6 result as the terse verdict it was, and say so.**
*Why:* the requested steps asked for a sibling task count before and after; the report back was
"every test pass". Writing the missing detail in as though it were observed would manufacture
evidence.
*What for:* the ledger stays a record of what was seen, not of what the procedure hoped would be seen.
*Experience:* provenance survives only if the weaker records are visibly weaker.

**D3 — Recommend the E6 coverage test rather than writing it into this closure.**
*Why:* the gate's job is to report what was observed. Adding production-adjacent test code inside a
closure note would mix a verdict with a change, and the change deserves its own red-before-green
evidence.
*What for:* keeps the closure auditable and the test reviewable on its own merits.

## 6. Open item — E1–E4 need one ruling, not necessarily a run

E1 (Dashboard), E2 (Analytics), E3 (task/subject CRUD), E4 (focus / logging / streak) have no written
observation. Two ways to resolve, both acceptable:

- **Rule that the 2026-08-10 session exercised them.** Plausible — that session opened the app
  repeatedly, created and completed tasks for Group C/D data, and the owner's Group E section is
  simply terse. A one-line owner ruling in this note closes it.
- **Run them.** Roughly three minutes: open each screen, confirm it renders and the numbers look
  right, and that a created/edited/deleted task and subject survive a restart.

Note that E1 is *not* implicated by the stale-chart change in its current state: the new `GetCapacity`
ceiling clamp only alters behaviour for a `capacity.txt` outside 1–8, and the file holds `5`, so the
Dashboard cannot reach the changed path as things stand.

## 7. What this unblocks, and what it does not

- **Unblocked:** Epic 3 is fully closed once §6 is resolved — the code landed 2026-08-07 (convergence)
  and the last behaviour fix merged as PR #54 on 2026-08-19.
- **Not unblocked — G3-1 remains unscheduled.** `ScheduleOptimizer`, `LoadRebalanceStage`,
  `ConstraintValidator`, `ObjectiveEvaluator`, `SoeWeights` and `OptimizerRunLogWriter` still have
  **zero production call sites**, and `OptimizerRunLogs` stays empty by design. Nothing in this gate
  changes that; wiring the optimizer to the GUI is separate, unscheduled integration work.
- **Next by the master plan:** the order is E1 → E3 → E2 → E4, so **Epic 2** is next. Its entry
  criteria were recorded as 12/12 when the stabilization plan closed on 2026-08-02.
