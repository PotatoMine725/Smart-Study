# WP-5 — Runtime Robustness

**Date:** 2026-08-02
**Package:** WP-5 (Category B) of `docs/plans/2026-07-27-post-epic1-stabilization.md`
**Commits:** `9a175b9` (5.1 timer guard) · `54f64ca` (5.2 capacity round-trip + tests) · `+1` (non-finite guard, §3.3a)
**Suite:** 368 → 383 passing, green in Debug and Release

---

## 1. Implementation Summary

WP-5 was scoped to two latent runtime faults. It closed **four**, because the
`capacity.txt` reader turned out to be the ingress for two defects WP-4 had handed
over, and a third surfaced while testing it.

| # | Defect | Task | Was it in the plan's scope? |
|---|---|---|---|
| 1 | `BackgroundTimer_Tick` is `async void` with no `try`/`catch` → modal error dialog every tick | 5.1 | Yes |
| 2 | Toast re-fires on every tick while any task is urgent | 5.1 | Yes |
| 3 | `capacity.txt` round-trips only on the machine that wrote it | 5.2 | Yes |
| 4 | `double.TryParse`'s implicit `AllowThousands` silently multiplies capacity by 10 | 5.2 | Handed over by WP-4 §3.2 |
| 5 | `capacityHours < 1/60` makes `GenerateSchedule` loop until it exhausts memory | 5.2 | Handed over by WP-4 §3.1 |

Defects 3–5 all live in `GetCapacity`, so one rewrite closes all three. That is why
the 5.2 commit message says it fixes three bugs rather than the one it was written for.

**Production diff:** `Views/MainWindow.xaml.cs` (`SetupBackgroundWorker`,
`BackgroundTimer_Tick`) and `Services/WorkloadServiceImpl.cs` (`GetCapacity`,
`SaveCapacity`, two new constants). `GenerateSchedule` is **not** touched — verified by
hunk inspection, so WP-4's 13 characterization tests still pin exactly what they pinned.

**New coverage:** `SmartStudyPlanner.Tests/Services/WorkloadServiceCapacityTests.cs`,
11 tests. Contrary to the plan's decision, `GetCapacity`/`SaveCapacity` **are** testable
and required no production change to become so (§3.1).

**Non-vacuity, per `docs/knowledge/review-methodology.md`.** A passing suite is not
evidence that a fix is pinned. Three mutations, each reverting one half of the fix:

| Mutation | Result | Tests that went red |
|---|---|---|
| Restore the two-argument `double.TryParse` | RED | `..._DauPhayTrenMayEnUS_...`, `..._SoInvariant_DocDungTrenMoiCulture` |
| Drop `Math.Max(val, MinCapacityHours)` | RED | `..._GiaTriQuaNho_BiKepLenSanToiThieu` (all 3 `InlineData` rows) |
| Restore `capacity.ToString()` | RED | `SaveCapacity_LuonGhiDauCham...`, `Capacity_GhiTrenViVN_DocLaiTrenEnUS...` |
| Drop `!double.IsFinite(val)` | RED | `..._GiaTriKhongHuuHan_VanTraVeSoDungDuoc` (3 of 4 rows) |
| **Control — no mutation** | **GREEN** | — |

The control row matters: without it, three REDs only prove the harness reports RED,
not that it discriminates.

---

## 2. Behaviour Changes

| # | Before | After |
|---|---|---|
| 1 | Fault in the tick → modal `MessageBox`, re-raised every 60 s | Fault appended to `crash.log`, no dialog |
| 2 | Toast on every tick while urgent tasks exist | One toast, then 30-minute cooldown; bypassed early only when the urgent count **grows** |
| 3 | Timer interval 1 minute | 5 minutes |
| 4 | `SaveCapacity` wrote the current culture's format | Always writes invariant (`4.5`) |
| 5 | `"4,5"` on en-US → **45.0**; `"4.5"` on vi-VN → **45.0** | Both fail to parse → 3.0 default |
| 6 | `"4,5"` on vi-VN → 4.5 | Unchanged (backward compatible), then migrated to `4.5` on next save |
| 7 | `capacity.txt` = `0.0001` → scheduler hangs until OOM | Clamped to 1.0 |
| 8 | Garbage / missing file → 3.0 | Unchanged |

**Items 2 and 3 are user-visible and belong in the release note.** Deadline
notifications become materially less frequent. The plan flagged this; it remains true.

---

## 3. Engineering Notes

### 3.1 The plan's untestability premise was wrong, and it was wrong in an instructive way

The plan decided: *"do not add a test, and do not refactor `FilePath` to make one
possible"*, on the grounds that `FilePath` is a `private static readonly` bound to
`AppDomain.CurrentDomain.BaseDirectory`. That reasoning conflates **"the path is not
injectable"** with **"the path is not reachable."**

Under test, `AppDomain.CurrentDomain.BaseDirectory` *is* the test assembly's own output
directory. It is writable, and it is emphatically not the real user profile — which is
the thing WP-2.2 existed to stop tests touching. The concern was correct in general and
misapplied here, and the cost of the misapplication was that the plan handed WP-5's
only two exit criteria to a human with no automated backstop.

Eleven tests now cover the round-trip with zero production change. The test class
snapshots any pre-existing `capacity.txt` and restores it in `Dispose`, so a leftover
file cannot seed the next run, and all capacity tests live in **one class** because
xUnit parallelises across classes but not within one.

The general lesson is recorded in `docs/knowledge/review-methodology.md`: an
untestability claim is a claim like any other, and the check is cheap.

### 3.2 The `AllowThousands` misparse runs in both directions

WP-4 §3.2 measured that `"4,5"` parses to `45` under en-US. Writing the tests surfaced
the mirror case, which no one had stated: invariant **`"4.5"` parses to `45` under
vi-VN**, because `.` is that culture's group separator.

This matters for how the defect is described. It is not "a Vietnamese file breaks on an
English machine." It is: *once the writer is invariant, the old reader corrupts the new
format on the very machines the app is mostly used on.* Had 5.2 landed the invariant
write without the `NumberStyles.Float` read, it would have **introduced** a 45-hour
capacity on every vi-VN install. The two halves were correctly specified as inseparable;
this is the concrete reason why.

**But the app itself never writes a value any of this can affect.** The capacity slider is
`Minimum="1" Maximum="8" TickFrequency="1" IsSnapToTickEnabled="True"`
(`WorkloadBalancerPage.xaml:68`), so every value the UI produces is an integer — and
integers format identically in every culture (measured: `5.0.ToString(vi-VN)` and
`.ToString(Invariant)` are both `"5"`; only `4.5` diverges to `"4,5"`/`"4.5"`).

So the plan's framing — *"a locale change, a hand edit, or any config sync breaks it
silently"* — overstates the locale half. A locale change alone cannot break a file
containing `5`. The actual threat model for **all three** `capacity.txt` defects is the
same one: a **non-integer or otherwise hand-authored file**. That unifies them into one
story rather than three, and it is why defect 5 (the hang) is the sharpest of the three —
hand-editing the file is precisely how a value below 1/60 arrives.

### 3.3 The clamp goes in the reader, at the floor only

`WorkloadBalancerViewModel`'s constructor does `CapacityHours = GetCapacity()` and then
calls `BuildSchedule` → `GenerateSchedule` immediately — **before** the slider exists.
The slider's `Minimum="1"` therefore never constrains the initial value, and a
hand-edited or misparsed `capacity.txt` reaches the allocator unfiltered. The hang WP-4
classified as "not UI-reachable" is reachable through the file, which is exactly the
surface this task owns.

Every production path into `GenerateSchedule`'s `capacityHours` was traced and each
originates at `GetCapacity` or the slider: `WorkloadBalancerViewModel:26`,
`DashboardViewModel:116` (which is the only production site constructing
`PipelineUserSettings`, feeding `BalanceWorkloadStage:40`). Closing the reader therefore
closes all of them — but *closing the reader* took two guards, not one (§3.3a).

Three deliberate choices:

- **Reader, not `GenerateSchedule`.** A guard in the allocator would be defence in
  depth, but it cannot be tested — asserting termination means risking the hang. The
  clamp *can* be tested, and was. Fix what you can prove; §4 records the residual.
- **Floor only, no ceiling.** The defect is `(int)(h*60) == 0`. A ceiling fixes nothing
  and would silently rewrite a hand-set 10 down to 8.
- **Floor = 1.0**, read off the slider's own `Minimum` rather than invented. It is the
  app's existing statement of its smallest supported capacity, and because
  `BuildSchedule` calls `SaveCapacity` immediately, the clamped value persists as
  something the UI can actually display.

### 3.3a The floor alone did not close the hang — `NaN` and `Infinity` walked straight through it

**This corrects a claim that was published in `54f64ca` before it was checked.** That
commit's message and this report's first draft both said the clamp closed the file
ingress to the hang. It did not, and the gap was found in review afterwards.

`NumberStyles.Float` still accepts `"NaN"`, `"Infinity"` and `"-Infinity"`, and on .NET
Core an overflowing literal like `"1e400"` returns `true` with `+∞` rather than failing.
`Math.Max` **propagates** `NaN` instead of choosing the non-`NaN` operand, so
`Math.Max(NaN, 1.0)` is `NaN`. Measured, not reasoned — the test printed
`GetCapacity trả về NaN cho input NaN` and `∞ cho input 1e400`.

Downstream, both are worse than the original defect:

| Input | `capacityMinutes = (int)(h * 60)` | Effect in the allocator |
|---|---|---|
| `NaN` | `0` | Exactly the WP-4 §3.1 infinite loop the clamp existed to close |
| `Infinity` | `int.MinValue` | `spaceLeft` negative → `chunk` negative → `remainingMinutes` **grows** each iteration |
| `-Infinity` | — | Caught: `Math.Max(-∞, 1.0)` is `1.0` |

Three of the four cases were live. Fixed with a `!double.IsFinite(val)` guard that treats
non-finite values as garbage, i.e. the same as an unparseable file.

The tests assert the **postcondition** — `GetCapacity` returns a finite value ≥
`MinCapacityHours` for every input — rather than trying to observe the hang. Same shape as
the floor test, and mutation-verifiable the same way (dropping the guard turns 3 rows red).

**Why this is worth a section.** `docs/knowledge/review-methodology.md` says a claim that
sets another package's severity must be measured, not derived. "Clamping the reader closes
all of them" is exactly such a claim: it set the residual risk that Follow-up 1 hands to
Epic 3. It was derived from the shape of the fix rather than from trying inputs, and a
two-minute test refuted it. The lesson from WP-4 was applied to the *tests* and not to the
*prose*, which is where it was needed.

### 3.4 The cooldown keys on growth, not on the count

The plan's snippet suppressed a toast when `soTaskKhanCap == _soTaskKhanCapDaBao` and
the cooldown had not elapsed — i.e. **any** change in the count re-fires immediately.
Since `CalculatePriority` is time-sensitive, tasks cross the ≥80 threshold one at a time
as deadlines approach, and completing a task also changes the count. That policy
re-notifies on every such transition, including downward ones.

Implemented instead: bypass the cooldown only when the count has **grown**. A task newly
becoming urgent is news; three urgent tasks becoming two is not. Same feature, strictly
less spam, one comparison different from the plan. Flagged here because it is a
deviation from the written plan, not an implementation detail.

### 3.5 What is *not* verified — entry criterion #11

**Criterion #12 (`capacity.txt` survives a restart) is now covered automatically.**
`GetCapacity` reads the file on every call and holds no cache, so write-then-read-from-disk
is the whole of the round-trip; a process restart adds only the assertion that the
installed layout's `BaseDirectory` is writable. The 30-second manual check is still
worth doing, but it is no longer the only evidence.

**The plan's manual recipe for #12 cannot be performed as written.** It says to *"set
capacity to a fractional value (e.g. 4.5 hours) in the workload UI"* — but the slider
snaps to integers (§3.2), so 4.5 is not reachable through the UI, and an integer file
would demonstrate nothing anyway. Replacement recipe, which is discriminating *and*
exercises the legacy-migration branch the original never touched:

> Put `4,5` (comma) in `capacity.txt` next to the executable, launch, and open the workload
> page. **Expected: the capacity reads 4.5 — not 45, and not the 3.0 default — and the file
> is rewritten as `4.5` with a dot.** Pre-fix, the same file read as 45 on an en-US machine.

**Criterion #11 (deadline toast fires once) — PASSED, verified by the owner on 2026-08-02.**
The recipe used, and why each step was needed:

> Interval temporarily at `TimeSpan.FromSeconds(10)`, run against the real database with
> one task scoring 100. **Expected: exactly one toast; pre-fix behaviour was one per tick.**
> Result: **one** background toast across 12+ ticks over several minutes. Interval restored
> to `FromMinutes(5)` and the tree confirmed identical to HEAD.

Two things made the observation evidence rather than an impression:

1. **A stated failure count.** "One toast" only means something next to "six would mean
   broken." Without the second number the observation cannot fail.
2. **An independent liveness check.** Zero toasts would have looked identical to a working
   cooldown, and unpackaged WPF toast delivery can fail for unrelated reasons. Clicking the
   window's X button fires a toast from a completely separate path (`OnClosing`), which
   establishes delivery works before the timer result is read. Without it, a pass would
   have been unfalsifiable.

The run also cost one false start worth recording: `bin/Debug/` held **two** output
directories, `net10.0-windows` (stale, five months old, from before the TFM was pinned) and
`net10.0-windows10.0.19041.0` (the real one, matching the csproj). The agent picked the
first glob match and pointed the owner at the stale build. Running it would have exhibited
the *pre-fix* behaviour and produced a confident FAIL on a working fix. The owner caught it
from a missing icon on the executable. **Resolve a build output directory from the csproj's
`TargetFramework`, never from a glob.**

### 3.6 A revert that was not clean

The mutation sweep was scripted in Python, and the script rewrote
`WorkloadServiceImpl.cs` with a UTF-8 **BOM** the file never had. The content restored
perfectly; the encoding did not. It was caught only by inspecting `git diff` hunks after
the sweep and noticing a hunk at line 1 that no mutation should have produced.

This is the second time an encoding hazard has bitten this repo from a scripted rewrite
(see the PowerShell UTF-8 CSV incident). The existing rule — *revert with `git checkout --`,
then verify with `git diff`, do not trust the script* — is what caught it, and it caught
something the naive check ("does the file compile and do the tests pass?") would have
missed entirely: a BOM breaks neither.

---

## 4. Follow-ups

1. **`GenerateSchedule` still has no internal guard against `capacityMinutes <= 0`.** The
   reachable path is closed at the reader, but a future caller passing `0.0` directly
   still hangs. Belongs to whoever next opens that method — realistically Epic 3 / SOE.
   Deliberately not fixed here: it cannot be tested without risking the hang, and WP-4
   pinned that method's behaviour.
2. **The current-culture read branch is transitional.** It exists so a legacy vi-VN
   `"4,5"` survives one app open, after which `SaveCapacity` rewrites it as invariant.
   It can be deleted once every install has opened the workload page since this release —
   there is no way to detect that, so the practical answer is "at the next major version".
3. **A vi-VN file opened on en-US still falls back to 3.0** rather than recovering 4.5.
   This is a safe fallback, not cross-locale recovery, and matches the caveat WP-4
   already recorded. Not worth fixing: after this release the file is invariant.
4. **`Views/` remains a zero-coverage directory** (Category C). WP-5.1's de-dup is the
   first piece of non-trivial *logic* to live there. If more accumulates, the Category C
   entry deserves revisiting — but establishing an STA/UI test harness is a project of
   its own and is correctly out of scope here.
5. **Two independent toast sources announce the same thing, seconds apart — found during
   the #11 verification.** `MainWindow.BackgroundTimer_Tick:150` emits *"CẢNH BÁO DEADLINE
   (Chạy ngầm)! … 1 bài tập KHẨN CẤP chưa làm"* while `DashboardViewModel.RaiseNotification:294`
   emits *"CẢNH BÁO DEADLINE! … 1 bài tập KHẨN CẤP cần xử lý ngay lập tức"*. Opening the
   dashboard with an urgent task present produces both in the same second, about the same
   task, in near-identical words.

   Neither is a defect on its own — the dashboard toast is guarded by a `private static
   bool _daThongBao` (`DashboardViewModel.cs:30`), so it fires at most once per process, and
   the timer's cooldown now works. The gap is that **nothing coordinates them**: there is no
   notification service, so each path throttles only itself. Deliberately not fixed — the
   CSA scoped WP-5 to the timer, and a shared throttle is a new seam, which the plan's *Out
   of Scope* excludes. It belongs wherever notifications get an owner.

   Cosmetic sub-item for the same fix: **"(Chạy ngầm)" is misleading.** It reads as "the app
   is minimised", but `SetupBackgroundWorker` starts in the constructor and the timer runs
   whether the window is open or not. This is precisely what made the duplicate look like a
   de-dup failure during verification.

6. **Entry criteria #11 and #12 are now both signed** (§3.5), taking Epic 2 entry criteria to
   **9 of 12**: met are #2–#7, #9, #11, #12. The three outstanding are **#1** (require
   `build-test` on `dev`/`main` — owner action, needs admin scope), **#8** (README test count,
   owned by WP-6), and **#10** (gate G4 on the Epic 2 planning agenda — owner). None is
   blocked on engineering work in this plan.

---

## 5. Decisions made

**Decision: test `GetCapacity`/`SaveCapacity` despite the plan explicitly deciding not to.**
*Why:* The plan's premise — that `FilePath` being a static makes them untestable — does
not hold. `BaseDirectory` under test is the test output directory, which is writable and
is not the user profile the surrounding WP-2 work was protecting.
*What for:* Exit criterion #12 stops depending solely on a human remembering to look, and
the three defects in `GetCapacity` become regression-proof.
*Experience:* This is the second time in two packages that a confidently-stated blocker
dissolved on contact (WP-4's was a severity claim derived rather than measured). The
pattern is the same: the reasoning was sound and the trivial unchecked step was the one
that mattered. Checking cost about ten minutes.

**Decision: clamp in `GetCapacity`, floor only, at the slider's `Minimum` of 1.0.**
*Why:* The reader is the untrusted ingress and the constructor path proves it reaches the
allocator before any UI clamp. A floor is what the defect requires; a ceiling would be
symmetry for its own sake and would silently rewrite legitimate hand-set values.
*What for:* Closes the hang on every production path while leaving `GenerateSchedule` — and
therefore WP-4's characterization — untouched.
*Experience:* The deciding argument was testability, not elegance. A guard inside
`GenerateSchedule` is arguably the more robust placement, but it cannot be verified
without risking the hang it prevents, so it would have shipped as an assertion rather
than a demonstrated fix. **The floor alone turned out to be insufficient** (§3.3a): `NaN`
and `Infinity` parse successfully and survive `Math.Max`, so the clamp needed a
finiteness guard beside it. Worth sitting with — the floor was mutation-tested and green,
and it still did not do what its commit message claimed, because the mutation sweep can
only test inputs the tests thought to supply. Non-vacuity proves a test *discriminates*;
it does not prove the input space was covered.

**Decision: key the toast cooldown on urgency *growth* rather than on count equality.**
*Why:* The plan's version re-fires on any count change, and the count changes routinely —
tasks cross the ≥80 threshold one at a time, and completing one moves it down too.
*What for:* Satisfies the package's stated objective (no toast spam) in the cases the
plan's own version would have leaked.
*Experience:* Recorded as an explicit deviation rather than folded in silently, because it
changes observable behaviour and the owner will encounter it before reading any diff.

**Decision: do not extract the de-dup logic into a testable class.**
*Why:* The plan's Out of Scope names new abstractions explicitly, and `Views/` is declared
zero-coverage (Category C).
*What for:* Keeps a robustness package from becoming a refactoring package.
*Experience:* The temptation was real — untested new logic is what WP-2 spent a package
cleaning up after. What settled it is that a unit test on an extracted predicate would
prove the predicate and miss the actual risk, which is in the wiring: setting the
timestamp in the wrong branch, resetting the wrong field. It would have bought coverage
of the part that was not in doubt.

**Decision: leave entry criterion #11 unsigned.**
*Why:* I did not run it. It needs a live WPF app, a seeded urgent task, and a human
watching notifications.
*What for:* A signed criterion has to mean someone observed the thing.
*Experience:* The useful output was not a signature but a *discriminating* recipe (§3.5) —
one that states the expected count and the pre-fix count, so the observation can fail.
"I opened it and it looked fine" would have been worth nothing.
