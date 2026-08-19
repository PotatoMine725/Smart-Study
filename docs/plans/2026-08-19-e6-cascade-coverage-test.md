# Plan — closing E6's automated coverage gap

**Date:** 2026-08-19 · **Status: NOT YET IMPLEMENTED — this document is the design, not the change.**
**Origin:** [Epic 3 manual gate closure](../reports/2026-08-19-epic3-manual-gate-closure.md) §4.3,
finding carried out of the manual QA gate.
**Target file:** `SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs`
**Production path under test:** `SqliteHocKyRepository.LuuHocKyAsync`
**Reads with:** [`docs/knowledge/qa-gates.md`](../knowledge/qa-gates.md) §"A passing manual observation
is not a standing guard" · [cascade-fixup bug note](../reports/2026-08-14-workload-balancer-stale-chart-fix-report.md)

---

## 1. Why this exists

Manual scenario E6 — *delete a subject that has at least two tasks; its tasks go with it, every other
subject and its tasks are untouched* — passed when the owner ran it on 2026-08-19. The behaviour is
right today. Nothing in the suite would notice if it stopped being right.

This plan closes that with one repository-level test. It is deliberately scoped to **one shape the
suite does not exercise**, not to "test subject deletion" in general — that is already partly covered,
and §2 says exactly how much.

## 2. The gap, stated precisely

The closure note and the distilled lesson both say the over-cascade class "has no automated coverage
at all." **That is too strong, and this plan corrects it** (§3). What is actually true:

**Already covered — do not re-test:**

| Behaviour | Covered by |
|---|---|
| Removing a subject tombstones it *and* its (single) task | `RepositoriesTests.cs:276` `HocKyRepository_DeleteMonHocByAbsence_CascadesTombstoneToItsTasks` |
| A subject is removed while a task that was under it **survives un-tombstoned**, having moved to another subject in the same save | `RepositoriesTests.cs:350` and `:393` (clone-merge tests), via `AssertClonesMergedWithoutLoss` at `:432`, which asserts `taskAProbe.IsDeleted == false` / `taskBProbe.IsDeleted == false` while the losing clone is tombstoned |
| An unrelated re-save does not bump `Rev` on untouched rows | `RepositoriesTests.cs:453` `HocKyRepository_ResaveWithNoChanges_DoesNotBumpRevOfUnrelatedRows` |

**Genuinely uncovered — what this test adds:**

1. **A sibling subject, with its own tasks, untouched by another subject's deletion**, with *no
   reparenting anywhere in the save.* Every existing removal test has exactly one subject in play, or
   two subjects that merge into one.
2. **A victim subject carrying two or more tasks.** Every existing removal test gives the victim one
   task, so a cascade that reached only the first child would pass the suite.

That is the whole delta. It is narrow on purpose.

## 3. Correction to two existing documents

Two documents overstate the gap in the same way — the clone-merge tests do cover "a `MonHoc` is
removed and a task that was under it survives un-tombstoned" (§2, row 2), so the accurate statement
is the two-line delta in §2, not "no coverage at all". Both also mis-attribute the gap to the
cascade-fixup ordering, which §4 shows E6's shape never exercises.

- **APPLIED 2026-08-19** —
  [`docs/reports/2026-08-19-epic3-manual-gate-closure.md`](../reports/2026-08-19-epic3-manual-gate-closure.md).
  §4.3 amended in place with both corrections and a pointer here; §3 item 3 corrected; an amendment
  note added to the header; decision D5 records why it was amended-and-labelled rather than rewritten
  or left. **The §1 outcome was not touched** — the finding got smaller and more precise, not
  withdrawn.
- **PENDING** — [`docs/knowledge/qa-gates.md`](../knowledge/qa-gates.md) lines 149–151 carry the same
  claim, inherited. Not amended here: the file is uncommitted work belonging to another line of work
  (§8). Whoever lands it should fold the correction in.

## 4. Reachability bound — why this test is about over-cascade, not reparenting

**No GUI path moves a task between subjects.** `MaMonHoc` is assigned once, at task creation, from
the currently-selected subject (`ViewModels/QuanLyTaskViewModel.cs:194`); there is no `MaMonHoc`
binding anywhere under `SmartStudyPlanner/Views/`. The reparent branch at
`SqliteHocKyRepository.cs:136–150` is therefore reachable **only** through
`LayDanhSachHocKyAsync`'s dedup merge — which is precisely what the clone-merge tests drive.

Two consequences, both load-bearing:

1. The user-reachable E6 path (`QuanLyMonHocViewModel.XoaMon` → `LuuHocKyAsync` → `db.MonHocs.Remove`)
   never reparents anything. Its risk is **over-cascade**: EF sweeping rows it should not.
2. **The mutation the closure note prescribed cannot work.** Closure §4.3 asks for a red-before-green
   run "against the pre-fix cascade ordering (removing the reparent + `DetectChanges()` block)." In
   the sibling shape every surviving task satisfies `oldTask.MaMonHoc == newTask.MaMonHoc`, so that
   loop is a no-op and deleting it changes nothing — the test would stay green and the exercise would
   be ceremonial. §6 replaces it with candidates that can actually kill.

Mechanism worth keeping in view while reading §6: `SyncStamper` converts `Remove()` into a soft
`IsDeleted` update and relies on EF's own in-memory cascade fixup having already resolved children to
`Deleted` — *"only reaches children that are loaded/tracked"* (`Data/SyncStamper.cs:13–15`).
`LuuHocKyAsync` loads with `.Include(...).ThenInclude(...)`, so every task is tracked and reachable
by the cascade. That `Include` is itself part of what the ≥2-task assertion pins.

## 5. Test design

One `[Fact]` in `SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs`, on the
existing in-file pattern: `NewDb()` for the shared in-memory SQLite connection, a real
`SqliteHocKyRepository`, and a fresh probe context for the assertions.

**Proposed name:** `HocKyRepository_DeleteMonHocWithSeveralTasks_LeavesSiblingSubjectUntouched`

**Arrange** — one `HocKy` holding two subjects:

| | Subject | Tasks |
|---|---|---|
| victim | `MonHoc("Toán", 3)` | `T1`, `T2` (≥2 — this is the point) |
| sibling | `MonHoc("Lý", 3)` | `S1` (≥1 — this is the other point) |

Save once via `repo.LuuHocKyAsync(hocKy)`.

**Act** — `hocKy.DanhSachMonHoc.Remove(victim)`, save again. This is exactly what
`QuanLyMonHocViewModel.XoaMon` does: drop from the in-memory graph, re-save the whole `HocKy`.

**Assert**, through a fresh probe context (`factory()`), against rows — never against the in-memory
graph, which is the thing under test:

- victim `MonHoc` present and `IsDeleted == true` (tombstoned, not hard-deleted)
- `T1` **and** `T2` present and `IsDeleted == true` — asserted individually, not as a count
- sibling `MonHoc` present and `IsDeleted == false`
- `S1` present and `IsDeleted == false`

**Do not assert `Rev` on the sibling.** `CopySyncSafeValues` runs over every surviving `oldMon` on
every save (`SqliteHocKyRepository.cs:180`), so a `Rev` bump on the sibling may be legitimate. If the
implementer wants that assertion, measure the actual value first and only pin it if it is stable —
an assertion that encodes an accident is worse than no assertion.

## 6. Mutation campaign — the evidence, to be run when the test is written

A green test is not evidence until it has been shown to go red for the right reason
([`docs/knowledge/qa-gates.md`](../knowledge/qa-gates.md); prior art: the six mutations of the
2026-08-10 automated gate). Each mutant below is applied to production code, the **new test alone**
and then the **full suite** are run, and both columns are filled in.

**Acceptance bar:** at least one mutant that the new test kills **and** the pre-existing suite
survives. That is what proves the test adds coverage rather than duplicating it. If no such mutant is
found, see §7 — do not paper over it.

| # | Mutant | Where | New test | Pre-existing suite | Prediction |
|---|---|---|---|---|---|
| M1 | Delete the `if (newMonById.ContainsKey(oldMon.MaMonHoc)) continue;` guard so every old subject is removed | `SqliteHocKyRepository.cs:155` | _(blank)_ | _(blank)_ | new test red; **suite probably also red** via `ResaveWithNoChanges_DoesNotBumpRevOfUnrelatedRows` — a prediction to measure, not a conclusion |
| M2 | Move `db.ChangeTracker.DetectChanges();` to *after* the removal loop | `SqliteHocKyRepository.cs:151` → after `:164` | _(blank)_ | _(blank)_ | new test likely green (no reparenting in this shape); clone-merge tests likely red — if so, M2 confirms the closure's prescribed mutation is inert here, which is itself worth recording |
| M3 | Drop `.ThenInclude(m => m.DanhSachTask)` from the load | `SqliteHocKyRepository.cs:100` | _(blank)_ | _(blank)_ | tasks untracked ⇒ cascade cannot reach them ⇒ new test red on `T1`/`T2`; existing single-task test `:276` probably red too |
| M4 | Invert the FK-healing loop so a `Guid.Empty` FK is left unhealed | `SqliteHocKyRepository.cs:118–121` | _(blank)_ | _(blank)_ | unknown — measure |
| M5 | Break the cascade at its source: change `db.MonHocs.Remove(oldMon)` to detach instead of remove | `SqliteHocKyRepository.cs:163` | _(blank)_ | _(blank)_ | unknown — measure |

Record the **actual** result in each cell, including surviving mutants. A mutant nothing kills is a
finding about the suite, not a failed experiment.

## 7. If no uniquely-killed mutant exists — the honest conclusion

Then the correct thing to write in the report is: **this test is scenario-fidelity coverage.** It
pins E6's shape — a multi-task victim beside a surviving sibling — so that the next reader can see
from the suite that the scenario is represented, and so a future change to the reconcile has
something shaped like the real user path to break. That is a real, modest value, and it should be
stated in those words rather than dressed up as regression protection.

Deciding that in advance is the point. Discovering it afterwards is where reports start overstating.

## 8. Out of scope

- **Writing the test.** This document is the design; closure decision D3 keeps the verdict document
  and the code change apart, and the same reasoning keeps the plan and the change apart.
- **Any production change.** If a mutant reveals a real defect, that is a separate finding and a
  separate package — do not fold a fix into a coverage test.
- **`docs/knowledge/qa-gates.md`.** It is currently uncommitted on branch
  `docs/epic3-knowledge-distillation` and belongs to another line of work. §3's correction is
  recommended, not applied.
- **Semester deletion.** Still no such capability; still filed separately.

## 9. Delivery

- Own branch off `dev`, own PR — repo convention since 2026-08-09 is PR + green CI, no direct push.
- Commit sequence: the test, then the report. The mutation results belong in a short report under
  `docs/reports/` with a **Decisions made** section, per convention since 2026-07-07.
- **Definition of done:** test written; §6 table filled in with measured results, blanks eliminated;
  acceptance bar in §6 met **or** §7 invoked explicitly in the report; §3's remaining PENDING
  correction (`qa-gates.md`) applied or consciously declined; full suite green.
- Estimated size: one `[Fact]` of roughly 40 lines, plus the mutation runs. Small.

## 10. Decisions made

**D1 — Correct the "no coverage at all" claim instead of inheriting it.**
*Why:* the clone-merge tests demonstrably cover "subject removed, its former task survives." Writing
a plan on top of an overstatement would have produced a test justified by a gap that is smaller than
advertised, and the overstatement would have hardened by being cited.
*What for:* the test gets scoped to the real delta (§2), which is what keeps it small.
*Experience:* this is the same failure the gate itself kept catching — a claim that reads true because
nobody re-derived it. Re-deriving it cost two file reads.

**D2 — Discard the mutation the closure prescribed, and say why.**
*Why:* removing the reparent block cannot make a sibling-shape test red, because no task changes
parent in that shape. Running it anyway would produce a green result presented as red-before-green
evidence.
*What for:* the campaign in §6 is made of mutants that can actually kill, and the reason the original
one cannot is on the record — it is a fact about the code's reachability, not an oversight.
*Experience:* a ceremonial mutation is worse than none: it converts "unverified" into "verified" in
the reader's mind at zero epistemic cost.

**D3 — Write the acceptance bar and the fallback before the test exists.**
*Why:* "at least one mutant killed by the new test and survived by the suite" is easy to commit to
now and easy to rationalise away once a test is written and passing.
*What for:* §7 is pre-authorised, so a modest outcome can be reported as modest without it feeling
like a failure on the day.
*Experience:* stating PASS/FAIL criteria in advance is the practice that made the 2026-08-14 fix
report and the manual runbook trustworthy; the same discipline applies to a 40-line test.

**D4 — Leave the sibling `Rev` assertion out of the specification.**
*Why:* `CopySyncSafeValues` touches every surviving subject, so a sibling `Rev` bump may be correct
behaviour. Specifying an assertion without knowing the value invites the implementer to pin whatever
the code currently does.
*What for:* the test asserts the contract (`IsDeleted`), not an artefact of the implementation.
