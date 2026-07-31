# WP-4 — Scheduling Characterization Tests

**Date:** 2026-07-31
**Package:** WP-4 of `docs/plans/2026-07-27-post-epic1-stabilization.md` (Category B)
**Commit:** `e89f0ec`
**Status:** Complete. Entry Criterion #7 met.
**Suite:** 355 → 368 passing, green in Debug and Release.
**Production code changed:** none — `git diff SmartStudyPlanner/` is empty.

---

## 1. Implementation Summary

Created `SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs`: 13 characterization
tests over `WorkloadServiceImpl.GenerateSchedule`, the method the CSA named as *"the most
behaviour-defining method in scheduling, exercised by nothing"* (Key Finding 4).

The method is directly constructible — `IDecisionEngine` and `IClock` are ctor-injected and
`capacityHours` is a parameter — so no seam had to be created and no production file was
touched. The suite uses the existing `FakeClock` from `SmartStudyPlanner.Tests.TestDoubles`
and one inlined table-driven `StubDecisionEngine`, per the repo convention that a double used
by a single file stays in that file.

The plan drafted 7 tests. All 7 held against the real implementation and were kept unchanged
in substance. Reading `WorkloadServiceImpl.cs:40-109` produced 6 more, listed in §2.

### Verification that the tests are worth having

"`WorkloadServiceScheduleTests` passes" is a weak criterion for a characterization suite —
a suite of vacuous assertions also passes. The acceptance criterion that matters is *"future
behavioural regressions would cause meaningful test failures,"* so it was checked directly by
mutating the production method and confirming the suite goes red, then reverting:

| Mutation of `WorkloadServiceImpl` | Result |
|---|---|
| `OrderBy(d => d.TotalMinutes)` → `OrderByDescending` (most-loaded placement) | 1 failed |
| `int nextOffset = days.Count` → `days.Count + 1` (overflow off-by-one) | 1 failed |
| Drop `.Where(t => t.TrangThai != StudyTaskStatus.HoanThanh)` | 2 failed |
| Drop `- task.ThoiGianDaHoc` | 2 failed |
| `OrderByDescending(t => t.DiemUuTien)` → `OrderBy` | 2 failed |
| Drop the `task.DiemUuTien =` write-back | 2 failed |
| `for (int i = 0; i < 7; i++)` → `< 5` | 1 failed |

Seven for seven. The production file was restored from git afterwards and confirmed clean.

The off-by-one row is the one that justifies an addition the plan did not have: the day-count
assertion (`days.Count == 10`) still passes under `days.Count + 1`, because the *number* of
days is unchanged — only the dates are wrong. Nothing but the contiguity assertion catches it.

---

## 2. Characterized Behaviours

Each row is now pinned by at least one test. **These record what the method does today. None
of them asserts the behaviour is correct.**

| # | Behaviour | Source |
|---|---|---|
| 1 | Always emits **at least 7 days**, starting at `_clock.Now.Date`, even with no tasks at all | `:60-65` |
| 2 | Day naming: `"Hôm nay"`, `"Ngày mai"`, then `dd/MM/yyyy` | `:63` |
| 3 | Days are **contiguous** — `days[i].Date == today.AddDays(i)` for the whole list, including days opened past the seventh | `:62`, `:83-84` |
| 4 | Tasks with `TrangThai == HoanThanh` are excluded entirely | `:48` |
| 5 | **`GenerateSchedule` is not pure**: it writes `CalculatePriority`'s result back into `task.DiemUuTien` on the caller's model — and only for tasks that survive the completion filter | `:50` |
| 6 | Scheduled minutes are `CalculateRawSuggestedMinutes(task) - task.ThoiGianDaHoc`; a task already studied enough is skipped | `:69-70` |
| 7 | No day ever exceeds `(int)(capacityHours * 60)` minutes, and total scheduled minutes are conserved | `:93-94, 102` |
| 8 | Work exceeding one day's capacity is split and each piece suffixed `" (Phần n)"`, numbered from 1 | `:98` |
| 9 | A task that fits **exactly** into the remaining space keeps its bare name — the suffix appears only on a real split | `:98` |
| 10 | When the 7 days fill up, further days are appended on demand rather than overflowing a day | `:81-91` |
| 11 | Tasks are allocated in descending `DiemUuTien` order | `:56` |
| 12 | Placement targets the **least-loaded** day, not the earliest day with room — so a later empty day wins over an earlier partly-filled one that could hold the task whole | `:77-79` |
| 13 | `ScheduledTask.TenMon` comes from the `MonHoc` that owns the task, across multiple subjects | `:99` |

Behaviour 12 is the one most likely to surprise a future reader: the allocator spreads work
across empty days in preference to packing earlier ones, so a task can be split even when a
single earlier day had enough free space to hold it whole.

**Deliberately not asserted:** any relationship between `HanChot` and placement. `HanChot`
appears nowhere in the allocator (CSA Key Finding 3). Adding such an assertion would encode a
requirement the method does not implement and would pre-empt Epic 3 / SOE, which is blocked on
gate G2.

---

## 3. Engineering Notes

### 3.1 `capacityHours` below one minute hangs the app — found, deliberately not fixed

`capacityMinutes = (int)(capacityHours * 60)` truncates, so **any** `capacityHours < 1.0/60`
yields `0`, not just exactly zero. With `capacityMinutes == 0`:

- `days.Where(d => d.TotalMinutes < 0)` never matches, so `targetDay` is always `null`;
- a new `ScheduleDay` is appended every iteration;
- `spaceLeft = 0 - 0 = 0`, so `chunk = Math.Min(remainingMinutes, 0) = 0`;
- `remainingMinutes -= 0` — the loop condition never advances.

The result is an infinite loop that appends `ScheduleDay` objects until the process exhausts
memory. It triggers whenever at least one task has `minutesNeeded > 0`.

**Not fixed here**, for two reasons: the acceptance criteria require that no production
behaviour change, and a guard clause is a behavioural change. **Not tested here either** — a
test pinning this would hang the test host. `[Fact(Timeout = …)]` does not help: xUnit's
timeout does not interrupt a running loop, so the runaway task keeps allocating in the
background.

**Reachability, which decides the severity:** the capacity input is
`WorkloadBalancerPage.xaml:68`, a `Slider` with `Minimum="1"`, `TickFrequency="1"` and
`IsSnapToTickEnabled="True"` — the UI can only produce integers 1 through 8. So this is **not
reachable through the UI**. The exposure is `WorkloadServiceImpl.GetCapacity()`, which returns
whatever parses out of `capacity.txt` with no lower-bound validation, and
`BalanceWorkloadStage.cs:40`, which passes `context.Settings.CapacityHours` straight through.
A hand-edited or corrupted `capacity.txt` containing e.g. `0.005` hangs schedule generation.

Flagged to **WP-5.2**, which is the package that opens `GetCapacity`/`SaveCapacity`. A
one-line clamp there is the natural home; it is not WP-4's to make.

### 3.2 A second `capacity.txt` hazard, also for WP-5.2

While establishing the above, a separate defect in the current `GetCapacity` surfaced.
`double.TryParse(raw, out val)` — the two-argument overload at `WorkloadServiceImpl.cs:30` —
uses `NumberStyles.Float | NumberStyles.AllowThousands`. On an `en-US` machine, a `vi-VN`-written
file containing `4,5` therefore parses as **45**, not as a failure: the comma is read as a
thousands separator. Capacity silently becomes 45 hours/day rather than falling back to the
3.0 default.

This strengthens the case for WP-5.2 rather than changing it. Its planned reader is
`double.TryParse(raw, NumberStyles.Float, InvariantCulture, …)` with a `CurrentCulture`
fallback — `NumberStyles.Float` excludes `AllowThousands`, so the invariant attempt on `"4,5"`
correctly fails and the current-culture fallback recovers `4.5`. The planned fix closes this
too; it is worth knowing that it fixes *two* bugs, and that the more severe of them is silent
rather than merely lossy.

### 3.3 The plan's draft tests needed no correction

Notable because the plan explicitly anticipated the opposite ("if any fail, correct the test to
match the implementation"). All 7 draft tests passed on the first run, including the two whose
expected values depend on `OrderBy` being a stable sort. The plan's reading of
`GenerateSchedule` was accurate.

### 3.4 `WorkloadServiceImpl.FilePath` remains the untestable part

`GetCapacity`/`SaveCapacity` are still uncovered, because `FilePath` is a `private static
readonly` bound to `AppDomain.CurrentDomain.BaseDirectory`. This is already recorded as
Category C and as a note in WP-5.2's commit body; WP-4 does not change it. Worth stating
plainly that `WorkloadServiceImpl` is now *partly* covered, not covered: the scheduling method
is pinned, the two file-I/O methods are not — and §3.1 and §3.2 are both defects in the
uncovered half.

---

## 4. Follow-ups

Recorded, not implemented.

1. **Clamp `capacityHours` to a sane minimum** (§3.1). Belongs to WP-5.2 or later; the hang is
   real but not UI-reachable.
2. **WP-5.2 fixes two bugs, not one** (§3.2). Its commit message should say so — the
   `AllowThousands` misparse is silent and more severe than the locale round-trip it was
   written for.
3. **Deduplicating the two divergent raw-minutes formulas is now safe** — this is the
   unblocking the plan predicted WP-4 would deliver. It remains Category C and deliberately
   unscheduled; the coverage now exists if Epic 3 opens scheduling anyway.
4. **`GenerateSchedule`'s write-back into `task.DiemUuTien`** (behaviour 5) is a side effect on
   a shared model from a method that reads as a pure query. It is now pinned, so any future
   attempt to make the method pure will fail loudly rather than silently changing what
   `QuanLyTaskViewModel` and the dashboard observe. Not a defect today; a hazard worth knowing
   about before Epic 3 touches scheduling.
5. **Least-loaded placement (behaviour 12) will interact with SOE.** A deadline-aware scheduler
   almost certainly wants earliest-fit rather than most-spread placement. That decision belongs
   to Epic 3 behind gate G2; the current behaviour is now documented so the change is a
   deliberate one.

---

## 5. Decisions made

**Decision: extend the plan's 7 draft tests to 13 rather than implementing the plan verbatim.**
*Why:* The plan's tests were written against a reading of `GenerateSchedule`; reading the method
directly surfaced six behaviours none of them constrained — most importantly the `DiemUuTien`
write-back and the day-date contiguity that the overflow path computes from a growing list.
*What for:* The acceptance criterion is that regressions cause failures, not that a test file
exists. Two of the six additions catch mutations that nothing else in the suite catches.
*Experience:* The plan explicitly invited correction of its draft tests if they disagreed with
the implementation. None did. The gap was not error but omission — a plan written from a
reading will under-specify against a plan written from the code, and the honest response is to
add rather than to treat the draft as the scope.

**Decision: verify non-vacuity by mutating production code rather than trusting a green run.**
*Why:* A characterization suite that passes proves nothing on its own; the failure mode is
assertions that hold regardless of what the method does. Mutation is the only direct check.
*What for:* To be able to claim the acceptance criterion truthfully instead of restating that
the tests pass.
*Experience:* It paid for itself immediately. It confirmed the contiguity test earns its place
(the off-by-one mutation is invisible to every other assertion) and it confirmed the
single-task tests are insensitive to the placement strategy — which is correct, since with one
task least-loaded and most-loaded produce identical output. Knowing *which* test covers *which*
behaviour is worth more than the aggregate green.

**Decision: document the `capacityHours == 0` infinite loop; do not fix it and do not test it.**
*Why:* Three constraints converge. The acceptance criteria forbid changing production
behaviour, and a guard clause changes it. A test pinning the hang would hang the test host,
and xUnit's `Timeout` cannot interrupt a tight loop. And the brief says to document broader
issues rather than expand scope.
*What for:* So the defect is on the record with its reachability established, in front of the
package that owns the file it lives in.
*Experience:* Establishing reachability was the part that mattered and it was cheap — one look
at the slider's `Minimum="1"` and `IsSnapToTickEnabled` turned "the scheduler can hang" into
"a corrupted `capacity.txt` can hang the scheduler," which is a different severity and a
different owner. Reporting the hang without that check would have overstated it.

**Decision: assert `dd/MM/yyyy` day names against the format expression, not a date literal.**
*Why:* `DateTime.ToString("dd/MM/yyyy")` renders `/` as the *current culture's* date separator,
not as a literal slash. A hard-coded `"13/04/2026"` would pass locally and break on a runner
whose culture uses a different separator.
*What for:* A CI-portable assertion that still fails if someone changes the format to `MM/dd`
or `ddd`.
*Experience:* Slightly self-referential, and worth the inline comment explaining why — otherwise
it reads like a tautology to the next person and invites "simplification" back into a literal.

**Decision: inline `StubDecisionEngine` rather than promoting it to `TestDoubles/`.**
*Why:* Repo convention is that a double used by exactly one file stays in that file; there are
already several one-off `IDecisionEngine` stubs inlined across the test project
(`TaskNotesTests.cs:264` among them).
*What for:* Consistency, and the stub is table-driven in a way that suits scheduling only.
*Experience:* The brief asked to reuse existing doubles before creating new ones. `FakeClock`
was reused. No shared `IDecisionEngine` double exists to reuse — the four other test files that
need one each inline their own, so adding a shared one would have been a test-project refactor
beyond this package.
