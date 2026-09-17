# Report — Epic 3 manual QA gate: closure

**Date:** 2026-08-19 · **Branch:** `docs/epic3-manual-gate-closure` (off `dev` at `208c7f1`)
**Runbook:** [`docs/plans/2026-08-10-epic-3-manual-qa-runbook.md`](../plans/2026-08-10-epic-3-manual-qa-runbook.md)
**Owner records:** [2026-08-10](2026-08-10-epic3-soe-manual-observation.md) ·
[2026-08-19](2026-08-19-epic3-manual-observation-updated.md)
**Reads with:** [Epic 3 closing note](2026-08-07-epic3-closing-note.md) ·
[automated QA gate](2026-08-10-epic3-automated-qa-gate.md) ·
[stale-chart fix report](2026-08-14-workload-balancer-stale-chart-fix-report.md)

**Amended 2026-08-19, after closure.** §3 item 3 and §4.3 overstated the size of E6's automated
coverage gap, and mis-attributed it to the cascade-fixup ordering. Corrected in §4.3. **The outcome
in §1 is unchanged** — the correction makes a finding smaller and more precise; it does not touch a
verdict.

---

## 1. Outcome

**PASS WITH FINDINGS. The gate is CLOSED.**

Every scenario passed. Nothing failed, and no scenario produced a defect. The findings in §4 are
non-defects: a UX enhancement candidate, a ratified limitation observed in the running product, and a
gap in *automated* coverage behind a manual check that passed.

**E1–E4 close on an owner ruling, not on a written observation** — recorded as such in §6, because
the two are not the same artifact and this note should not blur them.

The runbook's instruction on partial runs is followed literally: *"Do not summarise a
partially-executed run as a pass — record which scenarios were skipped and why."* Nothing here is
recorded as passing on evidence it does not have.

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
| E1–E4 | Dashboard · Analytics · CRUD · focus & streak | owner ruling 2026-08-19 (§6) | pass **by ruling**, not by written observation |
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
   shape E6 exercises — a multi-task victim standing beside a surviving sibling — had no automated
   reach. *(This item originally read "the over-cascade class E6 exists to catch was not covered by
   the suite at all." That was too strong; see §4.3 as amended.)*

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

**Amended 2026-08-19, after closure — two corrections, neither of which changes the finding's
status.** This section and §3 item 3 originally said the over-cascade class E6 exists to catch "was
not covered by the suite at all," and attributed the gap to the `LuuHocKyAsync` cascade-fixup
ordering. Both were re-derived while designing the follow-up test, and both were wrong.

*Correction 1 — the gap is smaller than stated.* The clone-merge tests
(`RepositoriesTests.cs:350` and `:393`, asserting through `AssertClonesMergedWithoutLoss` at `:432`)
already cover "a `MonHoc` is removed and a task that was under it survives un-tombstoned." What the
suite genuinely does not cover is exactly two things:

1. a **sibling** subject, carrying its own tasks, untouched by another subject's deletion — with no
   reparenting anywhere in the save;
2. a victim subject carrying **two or more** tasks. Every existing removal test gives it one, so a
   cascade that reached only the first child would pass the suite.

*Correction 2 — it is not the cascade-fixup regression class.* That ordering (FK reassignment plus
`DetectChanges()` **before** `Remove()`, `SqliteHocKyRepository.cs:136–151`) only bites when a task
changes parent, and **no GUI path moves a task between subjects**: `MaMonHoc` is assigned once at
creation (`QuanLyTaskViewModel.cs:194`) and nothing under `Views/` binds it, so that branch is
reachable only through `LayDanhSachHocKyAsync`'s dedup merge — which is what the clone-merge tests
drive. The user-reachable E6 path can fail only by **over-cascade**. A red-before-green run against
the pre-fix ordering, which is what this section's recommendation was originally read as asking for,
would stay green in E6's shape and prove nothing.

**The finding stands, at its corrected size.** The GUI run passed, so the behaviour is right today;
the suite still would not notice if the two shapes above stopped being true. **Recommended:** one
repository-level test, designed in
[`docs/plans/2026-08-19-e6-cascade-coverage-test.md`](../plans/2026-08-19-e6-cascade-coverage-test.md),
which carries the mutation campaign, the acceptance bar it must clear, and the honest fallback if no
mutant turns out to be uniquely killed. Still cheap, and it still converts a passing manual
observation into a standing guard.

### 4.4 No semester-management UI
Unchanged. Semesters can be created but not renamed or deleted — the reason the original E6 was
unexecutable and had to be retargeted. Already filed as its own proposal
(`docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` §7); deliberately not absorbed
into a bug-fix package.

## 5. Decisions made

**D1 — Put E1–E4 in front of the owner instead of assuming them.** *(Resolved same day by ruling —
§6.)*
*Why:* §6.1 names E1–E6 as a condition and E1–E4 had no written observation. They may well have been
exercised on 2026-08-10 without being written down, but an unwritten check and a passed check are not
the same artifact, and the difference is invisible six months from now.
*What for:* so that "the Epic 3 manual gate passed" cannot later be read as covering screens nobody
recorded looking at.
*Experience:* the withdrawal of C2's first "Met" is the precedent — a recorded verdict read through a
faulty instrument cost a full re-run. Unrecorded verdicts are the same failure one step earlier. The
question cost one line to ask and was answered in one line; assuming the answer would have cost
nothing today and been unfalsifiable later.

**D4 — Close E1–E4 on a ruling, and label it a ruling.**
*Why:* only the person who sat in front of the application can say which screens they opened. Their
statement is the best artifact obtainable now; reconstructing per-screen detail after the fact would
be manufacturing, not recording.
*What for:* the ledger stays readable as two distinct kinds of entry — observed, and ruled — so a
later reader can weigh them differently without having to re-derive which was which.
*Experience:* the alternative on offer was to have the owner re-run three minutes of screens whose
result nobody doubts. A ruling that is honestly labelled costs less and claims less.

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

**D5 — Amend §4.3 in place and label the amendment, rather than rewriting it or leaving it.**
*(Added 2026-08-19, after closure.)*
*Why:* designing the follow-up test re-derived §4.3's claim and found it overstated in size and wrong
in attribution. Leaving it would have let a citable overstatement harden — it had already been copied
into `docs/knowledge/qa-gates.md`. Silently rewriting it would erase the fact that the closure once
said something stronger, which is exactly the provenance this document exists to keep.
*What for:* a later reader sees the corrected finding *and* that it was corrected, and can tell that
the outcome in §1 never depended on the overstatement.
*Experience:* the correction cost two file reads. The claim had survived a runbook row, a PR body and
a distilled lesson without anyone re-deriving it — which is how a plausible sentence becomes a fact.
The counterpart lesson in `qa-gates.md` still carries the original wording and is flagged for the
same amendment.

## 6. E1–E4 — closed by owner ruling, 2026-08-19

> **Owner's ruling, given in session on 2026-08-19: the 2026-08-10 session covered E1–E4.**

Recorded as a **ruling**, and deliberately not upgraded into an observation. What exists is the
owner's statement that those screens were exercised during the 2026-08-10 run; what does not exist is
a written per-screen observation from that session — its Group E section notes only the missing
semester-management UI. The ruling is sufficient to close §6.1: the person who sat in front of the
application is the only one who can say what they looked at, and they have said it.

Supporting circumstance, not evidence: the 2026-08-10 session launched the app repeatedly across
A1–A3, created and completed tasks to build the Group C/D data (which passes through the CRUD and
task-management screens), and reported no error anywhere.

E1 was in any case not implicated by the stale-chart change: the new `GetCapacity` ceiling clamp only
alters behaviour for a `capacity.txt` outside 1–8, and the file holds `5`, so the Dashboard cannot
reach the changed path as things stand.

**Consequence: every §6.1 condition is satisfied and the manual gate is closed.**

## 7. What this unblocks, and what it does not

- **Unblocked: Epic 3 is fully closed.** The code landed 2026-08-07 (convergence), the last behaviour
  fix merged as PR #54 on 2026-08-19, and this gate closed the same day.
- **Not unblocked — G3-1 remains unscheduled.** `ScheduleOptimizer`, `LoadRebalanceStage`,
  `ConstraintValidator`, `ObjectiveEvaluator`, `SoeWeights` and `OptimizerRunLogWriter` still have
  **zero production call sites**, and `OptimizerRunLogs` stays empty by design. Nothing in this gate
  changes that; wiring the optimizer to the GUI is separate, unscheduled integration work.
- **Next by the master plan:** the order is E1 → E3 → E2 → E4, so **Epic 2** is next. Its entry
  criteria were recorded as 12/12 when the stabilization plan closed on 2026-08-02.
