# WP-5 — Runtime Robustness

**Date:** 2026-08-02
**Package:** WP-5 (Category B) of `docs/plans/2026-07-27-post-epic1-stabilization.md`
**Commits:** `9a175b9` (5.1 timer guard) · `54f64ca` (5.2 capacity round-trip + tests) · `c3f2286` (non-finite guard, §3.3a) · `0c489cf` (criteria signed)
**Post-review (owner-directed):** `0e5d448` (`GenerateSchedule` termination guard) · `d425068` (single toast source) · `866b5be` (scan once per process)
**Suite:** 368 → **391** passing, green in Debug and Release
**Manual criteria:** #11 re-verified by the owner on 2026-08-02 after the post-review commits — **PASS** (§3.5a). #12 is covered by the automated suite. No manual check remains outstanding.

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

Added by the post-review commits (`0e5d448`, `d425068`, `866b5be` — see §5):

| # | Before | After |
|---|---|---|
| 9 | Opening the dashboard fired a **second** toast from `DashboardViewModel`, near-identical wording | That source is deleted; the timer is the only one |
| 10 | Urgent tasks announced immediately on dashboard load | Announced immediately on **app launch** (`MainWindow_Loaded`), before any semester is chosen |
| 11 | All-clear toast *"✅ Mọi thứ đang trong tầm kiểm soát"* on dashboard load | Removed entirely — not migrated |
| 12 | Toast title read `🔥 CẢNH BÁO DEADLINE (Chạy ngầm)!` | `🔥 CẢNH BÁO DEADLINE!` — the old label implied the app was minimised, which was never what it meant |
| 13 | Dashboard toast and timer cooldown were **independent clocks**: a steady urgent task produced one on open *and* one at the 5-minute tick | The launch scan starts the same 30-minute cooldown the timer uses, so the next repeat is at +30 min, not +5. Escalation (urgent count **grows**) still bypasses it |
| 14 | `capacityHours` below the floor / `NaN` / `+∞` passed to `GenerateSchedule` directly → hang | Clamped inside the method; `+∞` saturates to `int.MaxValue` (terminates, everything on day 0) |

**Items 2, 3, 9–13 are user-visible and belong in the release note.** Deadline
notifications become materially less frequent, and there is now exactly one of them per
event rather than two. The plan flagged the frequency change; it remains true.

**Row 13 is the one most likely to be misread as a bug.** With a steady urgent count, one
running instance alerts at launch and then not again for 30 minutes — the cooldown state
lives in `MainWindow`'s instance fields, and a tray-minimised app is still the same instance.
A genuine restart via *Thoát hoàn toàn* destroys those fields, so **every fresh launch alerts
again**, however soon it follows the last one. Restarting is not covered by the cooldown, and
§3.5a depends on that.

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
from a missing icon on the executable. **Verify that the artifact you are about to run was
produced by the build you just ran** — compare its mtime against the build. That is the
general form; resolving the directory from the csproj's `TargetFramework` is one instance of
it, and it does not cover the Debug/Release or stale-publish variants of the same mistake.
Both stale directories (`bin/` and `obj/Debug/net10.0-windows`) were deleted afterwards, so
the specific trap is gone structurally rather than by remembering to avoid it.

### 3.5a Re-verification after the post-review commits

Criterion #11's signature covers the *timer* path. The post-review commits added a second
emission point (launch) and deleted a third (dashboard), so the criterion needs one more
observation. Recipe, built so it can fail:

> **Setup.** Confirm the executable is fresh: read `<TargetFramework>` from the csproj, then
> check `SmartStudyPlanner.exe` under `bin/Debug/<that TFM>/` has an mtime later than the
> newest source file — not later than the last commit, which proves nothing about the binary.
> Have at least one task scoring ≥ 80. Confirm **nothing is already running**: there is no
> single-instance mutex, so launching while a tray instance is alive gives two processes and
> a false FAIL = 2. Check the tray, and Task Manager for `SmartStudyPlanner.exe`.
>
> **Observation.** Launch the app. **PASS = exactly 1 toast.** **FAIL = 2** (a second
> emission source survived) **or 0** (the launch scan never ran, or `Loaded` did not fire).
> Read the count in the toast body against the number of urgent tasks the UI shows — a
> mismatch means the scan is reading different data than the page.
>
> **Liveness, after the count.** Click the window's X. A *"đã được thu nhỏ"* toast must
> appear. This proves delivery works via `OnClosing`, a path unrelated to the one under test,
> so a 0-result is interpretable rather than ambiguous. Then tray → *Thoát hoàn toàn* to end
> the process.
>
> **On a 0-result, read `crash.log` before concluding anything.**
> `QuetVaCanhBaoDeadlineAsync` swallows every exception into
> `CrashLogger.Log("MainWindow.BackgroundTimer_Tick", …)`, so a failed scan is on disk, not
> silent.

**The wording is no longer a discriminator.** The launch toast now reads exactly what the
deleted dashboard toast read, because `(Chạy ngầm)` was dropped. The only signal is **count
and timing**, so counting is the whole check.

**The check is repeatable.** `_lanCanhBaoGanNhat` and `_soTaskKhanCapDaBao` are *instance*
fields of `MainWindow`, so after *Thoát hoàn toàn* a fresh launch starts at `DateTime.MinValue`
and toasts again immediately. The 30-minute cooldown (behaviour row 13) applies **within one
process** — a tray-minimised app — not across restarts. An earlier draft of this section said
to expect nothing on a second launch inside 30 minutes; that was wrong, and following it would
have turned a correct PASS into a recorded FAIL.

**The tray-restore leg has been removed, because it cannot fail.** It previously read
*"restore from the tray a few times → PASS = 0 additional toasts — this is what
`_daQuetLanDau` exists for."* Trace it: after the launch toast, `_soTaskKhanCapDaBao = N` and
`_lanCanhBaoGanNhat = now`, so on restore with an unchanged urgent count
`dangKhanCapHon = N > N` is false and `hetCooldown` is false — line 168 returns **whether or
not `_daQuetLanDau` exists**. The cooldown subsumes the flag for 30 minutes, and at the
30-minute mark the 5-minute timer fires on its own and resets it. There is no steady-state
window in which the flag is observable.

Lowering `ToastCooldown` to rescue the observation does not work either: a no-toast result
still cannot separate *"the flag suppressed a second scan"* from *"`Loaded` never re-fires on
restore anyway"* — and the latter is exactly the unproven premise the flag exists to sidestep.
The experiment could only ever show the flag is **unnecessary**, never that it is load-bearing.

**Result — PASS, owner-run 2026-08-02.** Exactly **1** toast at launch. `crash.log` does not
exist under `%AppData%\SmartStudyPlanner\`, so the scan completed rather than failing into the
swallow at `:180`. Criterion #11 now covers both emission points.

**The liveness step failed on that run, and the PASS survives it.** Clicking X hid the window
but produced no *"đã được thu nhỏ"* toast. It worked normally on a later launch in the same
Windows session; the failing run was **the first launch after a machine reboot**.

Why this does not qualify the result: the liveness step exists to disambiguate a **0**-result.
The observation was 1, and that toast came from the same process and the same
`ToastContentBuilder` API minutes earlier — delivery is proven by the measurement itself, not
by the insurance. `OnClosing` (`:187-203`) is also unconditional, untouched by WP-5 (last
changed in `c7898be`), and no part of the deadline mechanism.

Why it is not dismissed either: *"it worked the second time"* is a different finding from
*"it works"*, and first-launch-after-boot is the case a real user hits every morning. Carried
to §4 as a follow-up. It also indicts the step design — a liveness probe is only useful if it
is more reliable than the thing it vouches for, and this one was picked without checking that.

**Scoped claim for `866b5be`.** Its commit message says *"without the flag, every tray restore
with an urgent task present would fire another toast."* **That is not true** — the cooldown
already blocks it. `_daQuetLanDau` is defense in depth against a scenario that has never been
observed (a `Loaded` re-raise combined with an expired cooldown or a risen urgent count), it is
cheap, and it is **not verified by any check in this report**. Recorded rather than quietly
carried, per the evidence-scoped-claims standard in §5.

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

1. ~~**`GenerateSchedule` still has no internal guard against `capacityMinutes <= 0`.**~~
   **CLOSED 2026-08-02** by `ClampCapacityMinutes` — see §5, *Decision: add the
   `GenerateSchedule` guard after all*. The reasoning recorded here ("it cannot be tested
   without risking the hang") was not wrong so much as **unpriced**: the hang only happens
   when the guard is broken, which is a loud CI failure, not a silent pass. That is an
   affordable price, and it was never named before declining to pay it.
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
   notification service, so each path throttles only itself.

   **CLOSED 2026-08-02**, owner-directed after review — see §5, *Decision: delete the
   dashboard toast rather than coordinate the two sources*. The dashboard copy is gone,
   the timer now scans once from `MainWindow_Loaded`, and `(Chạy ngầm)` is dropped from the
   title: it read as "the app is minimised", but `SetupBackgroundWorker` starts in the
   constructor and the timer runs whether the window is open or not — precisely what made
   the duplicate look like a de-dup failure during verification.
6. **The minimize-to-tray toast did not fire on the first launch after a machine reboot.**
   Observed once, during the §3.5a re-verification. `OnClosing`
   (`MainWindow.xaml.cs:187-203`) hid the window as expected but showed no toast; the same
   action worked on a later launch in the same Windows session. **Not a WP-5 regression** —
   that block last changed in `c7898be`, and this package never touched it.

   Unexplained, and deliberately left that way rather than guessed at. What is ruled out:
   delivery was working in that process (the launch toast fired minutes earlier through the
   same API), the path has no de-dup guard and no dependence on `_thucSuMuonTat` once past
   `:190`, and nothing reached `crash.log` — though `OnClosing` has no `try`/`catch`, so a
   throw there would surface as an `App` `MessageBox`, not a log line. Two candidates worth
   separating if it recurs: the notification platform not yet ready that early after boot,
   versus Windows delivering a *silent* banner (this toast carries no `.AddAudio`, unlike the
   deadline toast) straight to the notification centre where it is easy to miss.

   Cost of chasing it now is a reboot per observation, on a cosmetic path outside this
   package. Recording it so a second sighting is recognised as a pattern instead of
   re-investigated from zero.
7. **The liveness probe in §3.5a was chosen without checking it was more reliable than the
   thing it vouched for.** It is only worth adding an independent confirmation channel if
   that channel is *sturdier* than the measurement — this one turned out to be flakier. Had
   the observation been 0, the probe would have produced a false "delivery is broken"
   diagnosis. The principle for next time: pick the probe by evidence of reliability, not by
   the fact that it is architecturally unrelated.

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

---

### Post-review decisions (2026-08-02, after owner debate)

The four items below were settled with the owner after WP-5 had already been signed off,
in a review of this report's own process failures. They amend §3.3a and Follow-ups 1 and 5.

**Decision: add the `GenerateSchedule` guard after all.**
*Why:* Follow-up 1 declined it on the grounds that it "cannot be tested without risking the
hang." That reasoning **mispriced a cost rather than identifying a blocker**. A test for the
guard genuinely can hang — but only when the guard is broken, and a hung CI job is a loud,
visible failure, not a silent pass. The price was affordable and was never named.
*What for:* So the loop's termination is a property of the method rather than a property of
its two current callers. `GetCapacity` and the slider are both clean today; nothing makes
them stay clean.
*Experience:* This is the same shape of error as the plan's untestability premise in §3.1 —
which I had caught, and then reproduced one section later. The tell is identical both times:
an unfalsifiable-sounding word ("untestable", "cannot") standing in for a measurement never
taken. Worth noting that the guard is *inert* — every input above the floor is unaffected —
so WP-4's characterization still pins exactly what it pinned.

**Decision: `+∞` saturates to `int.MaxValue` instead of being clamped to the floor.**
*Why:* Measured, not reasoned: the first draft's theory listed `+∞` as a below-floor input,
and the test **failed**. `∞ >= 1.0` is true, so it never enters the clamp branch. The real
hazard is the cast — `(int)(∞*60)` is undefined out of range and yields `int.MinValue`,
making `remainingMinutes` grow. Saturation terminates and preserves the natural reading of
"unlimited capacity": everything lands on day 0.
*What for:* The method's invariant is termination, not plausibility. `GetCapacity` already
rejects non-finite values at the file boundary; this is a second line, not a replacement.
*Experience:* The failing test was right and my expectation was wrong — the third time in
this package that an input I had classified by reasoning turned out to belong in a different
class when actually run.

**Decision: delete the dashboard toast rather than coordinate the two sources.**
*Why:* Measurement settled it. `DashboardViewModel`'s copy is weaker on all three axes —
one semester vs. all, capped at 5 by `Take(5)` vs. uncapped, stored `DiemUuTien` vs. freshly
computed `CalculatePriority` — at an identical `>= 80` threshold. So this was never two views
of one thing; it was one mechanism and a strictly worse subset of it.
*What for:* A shared notification service would have been a new seam (excluded by the plan's
*Out of Scope*) for a problem whose real shape was "delete the worse one." Deletion cannot
regress and needs no abstraction.
*Experience:* Two things nearly went wrong. The startup scan belongs in `MainWindow_Loaded`,
**not** the constructor where `SetupBackgroundWorker` lives — an early `ServiceLocator`
resolve can fail and the tick's own `try`/`catch` would bury it in `crash.log`, failing
silently in a way that looks like success. And `RaiseNotification` had a **second branch**
(the all-clear toast) with no duplicate anywhere; deleting the method without surfacing that
would have dropped user-facing behaviour nobody agreed to drop. The owner's call was to drop
it deliberately: notifications should be actionable, and routine all-clear messages are
notification fatigue.

**Decision: delete the stale build directories instead of relying on a rule not to glob them.**
*Why:* §3.5's false start came from resolving `bin/` with a glob and hitting a five-month-old
output. The memory rule written afterwards ("resolve from the csproj TFM") trains around the
trap instead of removing it. The TFM is pinned, so `net10.0-windows/` will never be recreated.
*What for:* Structural rather than behavioural: with one directory per configuration, no
future session can pick the wrong one.
*Experience:* There were **two** stale directories, not one — `bin/Debug/net10.0-windows` and
`obj/Debug/net10.0-windows` — so deleting only the one that burned me would have left the
ambiguity in place. The rule is kept but generalised: *verify the artifact you are about to
run was produced by the build you just ran* (compare mtimes). That catches the Release/Debug
and stale-publish variants too, which the TFM-specific wording does not.

**Standard adopted: evidence-scoped claims.**
Any claim that a change closes a *class* of defects must name the test or measurement that
covers that class, and state what remains uncovered. §3.3a exists because `54f64ca` claimed
the floor clamp "closes the file ingress to the hang" while the only test named for it
covered the floor — `NaN` and `Infinity` were never in scope of any assertion. The check is
mechanical at writing time: if a commit message says *X closes Y*, there must be a test named
for Y. "Be more careful" is not a check; this is.
