# WP-5 — Runtime Robustness

**Date:** 2026-08-02
**Package:** WP-5 (Category B) of `docs/plans/2026-07-27-post-epic1-stabilization.md`
**Commits:** `9a175b9` (5.1 timer guard) · `54f64ca` (5.2 capacity round-trip + tests)
**Suite:** 368 → 379 passing, green in Debug and Release

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
`PipelineUserSettings`, feeding `BalanceWorkloadStage:40`). Clamping the reader closes
all of them.

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

**Criterion #11 (deadline toast fires once) is not verified, and I did not verify it.**
It needs a running WPF app, a database containing a task scoring ≥80, and a human
watching Windows notifications. I did not run it, so I have not signed it. The
discriminating recipe for whoever does — knowing what failure looks like is what makes
the observation evidence:

> Temporarily set the interval in `SetupBackgroundWorker` to `TimeSpan.FromSeconds(10)`,
> launch with at least one urgent task present, and watch for ~60 s. **Expected: exactly
> one toast, then silence.** The pre-fix behaviour was one toast per tick — so if you see
> six, the de-dup is not wired. Then restore `FromMinutes(5)` and confirm with `git diff`
> that nothing else moved.

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
5. **Entry criterion #11 needs an owner verifier and date** (§3.5).

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
than a demonstrated fix.

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
