# E6 cascade coverage test — mutation campaign results

**Date:** 2026-08-20 · **Author:** Claude (agent), on owner instruction
**Design:** [`docs/plans/2026-08-19-e6-cascade-coverage-test.md`](../plans/2026-08-19-e6-cascade-coverage-test.md)
**Origin:** [Epic 3 manual gate closure](2026-08-19-epic3-manual-gate-closure.md) §4.3, as amended
**Commit:** `522043a` · **Branch:** `test/e6-cascade-coverage`

> **Headline: the acceptance bar was not met, and §7 of the plan is invoked.** No mutant was killed by
> the new test *and* survived by the pre-existing suite. The test is **scenario-fidelity coverage**,
> not regression protection, and is reported in those words. A separate finding came out of the
> campaign: **one mutant survived the entire suite.**

---

## 1. Scope

Covers writing the E6 coverage test recommended by the Epic 3 manual gate closure, and running the
five-mutant campaign the plan specified in advance. Does not cover any production change — none was
made, and none is proposed here.

## 2. What was written

One `[Fact]` in `SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs`:
`HocKyRepository_DeleteMonHocWithSeveralTasks_LeavesSiblingSubjectUntouched`, built on the file's
existing pattern (`NewDb()` shared in-memory SQLite connection, real `SqliteHocKyRepository`, fresh
probe context for assertions).

A victim `MonHoc("Toán", 3)` carrying `T1` and `T2` stands beside a sibling `MonHoc("Lý", 3)` carrying
`S1`. The victim is dropped from the in-memory graph and the `HocKy` re-saved — exactly what
`QuanLyMonHocViewModel.XoaMon` does. Through a fresh probe: victim and both its tasks tombstoned,
sibling and its task untouched. Each task is asserted **by `MaTask` individually**; a count would
still pass if the cascade reached only `T1`.

No `Rev` assertion on the sibling, per plan decision D4 — `CopySyncSafeValues` runs over every
surviving `MonHoc` on every save (`SqliteHocKyRepository.cs:180`), so a bump there can be legitimate.

## 3. Findings

### 3.1 Mutation campaign — measured results

Each mutant was applied to production code, then the **new test alone** and the **pre-existing suite
with the new test excluded** were measured separately, so "killed" is never inferred from a red
full-suite run. Every mutant was reverted before the next was applied, verified by an empty
`git diff` over `SmartStudyPlanner/`.

Baselines: new test alone **1 passed**; pre-existing suite **486 passed, 1 skipped**.

| # | Mutant | Where | New test | Pre-existing suite | Clears bar? |
|---|---|---|---|---|---|
| M1 | `if (newMonById.ContainsKey(oldMon.MaMonHoc)) continue;` deleted | `:155` | **RED** | **RED** — 7 failed / 479 passed | No |
| M2 | `db.ChangeTracker.DetectChanges();` moved to *after* the removal loop | `:151` → after `:164` | **green** | **green** — 0 failed / 486 passed | No — **survived** |
| M3 | `.ThenInclude(m => m.DanhSachTask)` dropped from the load | `:100` | **RED** | **RED** — 8 failed / 478 passed | No |
| M4 | FK-healing condition inverted (`== Guid.Empty` → `!=`) | `:118–121` | green | **RED** — 1 failed / 485 passed | No |
| M5 | `db.MonHocs.Remove(oldMon)` → `db.Entry(oldMon).State = Detached` | `:163` | **RED** | **RED** — 3 failed / 483 passed | No |

Tests that went red, by mutant:

- **M1** — `ThemTask_NewTask_PersistsWithOwnerSubjectFk`, `HocKyRepository_LuuVaLayDanhSach_RoundTripVaOverwrite`,
  `LuuHocKyAsync_TaskAddedWithoutFkStamp_PersistsUnderNavigationOwner`, `HocKyRepository_DeleteTaskByAbsence_TombstonesNotHardDeletes`,
  both clone-merge tests, and `HocKyRepository_ResaveWithNoChanges_DoesNotBumpRevOfUnrelatedRows`.
- **M3** — the above minus the FK-stamp test, plus `HocKyRepository_DeleteMonHocByAbsence_CascadesTombstoneToItsTasks`,
  `TaskNotesRepositoryTests.DeleteTask_CascadesToNoteAndLinks` and `HocKyRepository_DeleteTaskByAbsence_CascadesToNoteAndLinks`.
- **M4** — `LuuHocKyAsync_TaskAddedWithoutFkStamp_PersistsUnderNavigationOwner` only.
- **M5** — `HocKyRepository_DeleteMonHocByAbsence_CascadesTombstoneToItsTasks` and both clone-merge tests.

### 3.2 Two of the plan's predictions were wrong, and one right

- **M1 predicted "suite probably also red via the `Rev` test" — confirmed**, and by six other tests too.
- **M2 predicted "new test likely green; clone-merge tests likely red" — half wrong.** The new test
  was green as predicted, but **nothing went red anywhere.**
- **M3 predicted red on both — confirmed.**

### 3.3 Finding: M2 survives the entire suite

Moving `db.ChangeTracker.DetectChanges()` from before the removal loop to after it changes nothing
that any of the 487 tests can see. Per the plan's own instruction — *"a mutant nothing kills is a
finding about the suite, not a failed experiment"* — this is recorded as a finding.

**What is established:** no test in the suite, new or pre-existing, distinguishes the two orderings.

**What is not established:** whether the explicit call is redundant. The obvious hypothesis is that
EF Core's automatic change detection on `DbSet.Remove` already runs detection at the point the
cascade fixup resolves dependents, making the explicit call defensive rather than load-bearing.
**That is a hypothesis; it was not tested here**, and distinguishing it from "the ordering genuinely
does not matter" needs a probe this package did not run.

> ⚠️ **Do not delete the explicit `DetectChanges()` on the strength of this result.** A surviving
> mutant means the suite does not cover the line — not that the line is dead. The comment at
> `SqliteHocKyRepository.cs:136–141` records a real bug this ordering was introduced to fix.

### 3.4 The acceptance bar was not met — §7 invoked

The bar, written before the test existed: *at least one mutant the new test kills and the
pre-existing suite survives.* Every mutant fell into one of three other categories — killed by both
(M1, M3, M5), killed by neither (M2), or killed by the suite alone (M4).

Per plan §7, pre-authorised, the honest conclusion:

> **This test is scenario-fidelity coverage.** It pins E6's shape — a multi-task victim beside a
> surviving sibling — so the next reader can see from the suite that the scenario is represented, and
> so a future change to the reconcile has something shaped like the real user path to break. That is a
> real, modest value, and it is stated in those words rather than dressed up as regression protection.

A structural reason is worth recording, **labelled as the inference it is.** *Observed:* the
production code has no branch keyed on how many children a subject has, or on whether a sibling
exists — the cascade is EF's and uniform. *Inferred from that plus the five measured mutants:* no
mutation of this code is likely to be uniquely sensitive to the two shapes the test adds, so the bar
was probably unreachable here rather than missed for want of the right mutant.

**That inference is not established.** It is a claim about the space of all possible mutants drawn
from a sample of five, and M2 has already demonstrated that predictions about this code can be wrong
in exactly the "nothing happened" direction. It is recorded because it is the most useful thing to
tell the next person, not because it was proved — and it should be treated as falsifiable by anyone
who finds a sixth mutant that clears the bar. Distinguishing it from the M2 hypothesis in §3.3 would
be arbitrary: both are inferences, and both are marked as such.

What *is* established is the measured table in §3.1 and the §7 fallback it triggers. Deciding that
fallback in advance is what let this be reported as a modest result instead of rationalised into a
strong one.

## 4. Verification

| Check | Result |
|---|---|
| Build (`dotnet build`, test project) | 0 errors, 94 pre-existing warnings |
| New test alone | 1 passed |
| Full suite, final state | **487 passed, 1 skipped, 488 total** |
| Production diff after campaign | **empty** — every mutant reverted, verified by `git diff -- SmartStudyPlanner/` |

All runs on `net10.0`, in an isolated git worktree off `origin/dev` (`33c0ffe`), so the mutation edits
never touched the primary working tree.

**On the `gitnexus_impact` convention:** no production symbol was edited by this change — the commit
adds a test method only. The campaign did edit `LuuHocKyAsync` five times, but those edits were
temporary and reverted, so impact analysis over them would describe code that no longer exists. Noted
rather than skipped silently.

## 5. Follow-ups

Non-blocking, none owned by this package:

1. **`docs/knowledge/qa-gates.md:149–151`** still carries the pre-correction overstatement ("this
   class is not covered at all"). **Consciously declined here**, not forgotten: the file is
   uncommitted work on `docs/epic3-knowledge-distillation`. Whoever lands that branch should fold in
   the correction from closure §4.3 as amended.
2. ~~**The plan document's status line** still reads *NOT YET IMPLEMENTED*, and its §6 table still
   has blank result cells.~~ **RESOLVED 2026-08-20.** PR #56 merged (`6ba0f7c`), so the plan is on
   `dev`; this branch was rebased onto it and the plan updated in the same package — status line now
   reads *EXECUTED*, §6's cells carry the measured results, and its prediction column is deliberately
   left intact so the predictions stay comparable against what was measured. §3.1 here and §6 there
   are the same numbers.
3. **M2's surviving mutant** (§3.3) — decide whether to pin the `DetectChanges()` ordering with a
   test, or to establish that it is genuinely redundant. Either resolves the finding; neither is
   urgent.

## 6. Decisions made

**D1 — Run M5 first rather than in listed order.**
*Why:* the plan predicted M1 and M3 would take the pre-existing suite red, which would disqualify them
from the acceptance bar before measuring anything. M5 was the candidate most likely to break the
cascade in the sibling shape specifically, so it was the cheapest route to either clearing the bar
early or learning the bar was in trouble.
*What for:* the risk of the bar being unreachable surfaced at the start of the campaign rather than
after five runs, which is when there is still time to think about it rather than rationalise.
*Experience:* it did not clear the bar, but knowing that first reframed the remaining four runs as
measurement rather than as a search for a result — which is the frame that keeps §7 available.

**D2 — Measure the new test and the pre-existing suite as two separate runs, every time.**
*Why:* a single red full-suite run cannot tell you which test did the killing. The bar is a statement
about two different sets, so it needs two measurements.
*What for:* every cell in §3.1 is an observation rather than an inference; M4 in particular
(new test green, suite red) is invisible to a single-run method.
*Experience:* cost was one extra `dotnet test` per mutant, about three seconds each. This is the
cheapest possible insurance against the campaign's central failure mode.

**D3 — Report the unmet acceptance bar as the headline, not as a caveat near the end.**
*Why:* the plan pre-authorised §7 precisely so a modest outcome could be stated plainly. A reader who
sees "test added, suite green, 487 passing" and has to reach §3.4 to learn the test is not regression
protection has been misled by structure rather than by any false sentence.
*What for:* the claim this package supports — scenario fidelity — is the first thing the document says.
*Experience:* the temptation to lead with the green suite was real, and the only reason it was
resisted is that §7 was written down four days before the result was known.

**D4 — Record M2's survival with an explicit warning against acting on it.**
*Why:* "nothing caught this mutation" reads like "this line does nothing", and the natural next step
would be to delete the `DetectChanges()` call. The evidence does not support that; it supports only
a statement about the suite's coverage.
*What for:* the finding is preserved without arming a bad change, and the untested mechanism is
labelled as a hypothesis rather than an explanation.
*Experience:* this is the same discipline the gate itself repeatedly needed — separating what was
observed from what would explain it. Writing the hypothesis down but marking it untested costs one
sentence and prevents a plausible claim hardening into a fact, which is exactly what closure §4.3
suffered from.

**D5 — Decline both of Sourcery's review suggestions on PR #57, with reasons rather than silently.**
*Why:* (a) *"`DateTime.Today` may be flaky."* Every existing test in this file constructs deadlines the
same way, and `LuuHocKyAsync` never evaluates a deadline — it persists rows. The date is inert here,
so a clock abstraction would buy nothing and would make this one test inconsistent with its
neighbours. The project *does* have a real date-fragility problem, but it is in `DecisionEngineTests`,
where dates drive priority arithmetic; importing that fix into a persistence test would be
cargo-culting the remedy away from the disease. (b) *"the local `NewTask` helper duplicates an existing
pattern."* There is no shared `StudyTask` factory in this suite; `TestDb.SeedTaskAsync` seeds a task
*with its own `MonHoc`*, which is the opposite of what this test needs — it must control which subject
owns each task. The static local function is scoped to the one test that needs it and does not invite
coupling between tests.
*What for:* the review is answered on the merits and the reasoning is on the record, so the same two
suggestions do not have to be re-litigated by the next reviewer or the next bot.
*Experience:* both suggestions were plausible and generic, which is the kind that gets accepted
reflexively. Checking each against what the code under test actually does took one read of the file
and turned two "reasonable changes" into two answered questions.
