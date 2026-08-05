# T3.2 — `IObjectiveEvaluator` seam decisions (Card E, Epic 3)

> **Status:** Implemented. Documentation of design decisions made while operationalizing the
> five objective terms and the SOE weight vector — none of this is pinned by any prior ADR;
> D-G/D-J only pin the formula shape and the seam boundary, not the term definitions.
> **Scope:** T3.2 only (`IObjectiveEvaluator`, `ObjectiveEvaluator`, `SoeWeights`,
> `ObjectiveScore`), all in `SmartStudyPlanner.Services.Soe`. Does not touch T3.1
> (`IConstraintValidator`, Card D, parallel), T3.3 (allocator wiring, future), or T3.5/GATE G3
> (weight *values*/governance, future).

## 0. Frozen inputs this design must respect

- **D-G** (`2026-07-02-architecture-freeze-decisions.md:28-55`): objective is
  `Score = w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty + w5·FragmentationPenalty`,
  **quality only** — no deadline term, ever, anywhere.
- **D-J** (`2026-07-02-architecture-freeze-decisions.md:117-134`): Constraint Validation
  (Card D) and Objective Evaluation (this card) are independent seams. The evaluator ranks only
  candidates the validator already admitted; it does not decide feasibility and does not
  reference `IConstraintValidator`.
- `ScheduledItem` (`SmartStudyPlanner/Services/Soe/ScheduledItem.cs`, Card C/T3.8) is the input
  shape: `MaTask, HanChot, TenTaskGoc, TenHienThi, TenMon, Date, SoPhut`. Its own doc comment
  states `Date` is the only sound join key between chunk lists and day lists — never list
  position. Its hạn-chót field exists on the type but is off-limits to this card's scoring logic
  per D-G.

## 1. Namespace placement

Kept flat in `SmartStudyPlanner.Services.Soe` (no `.Objective` sub-namespace). `ScheduledItem`
already lives there from Card C, and the whole SOE surface so far (production + the mirrored
`SmartStudyPlanner.Tests.Services.Soe`) is four files total pre-this-card — a sub-namespace for
one more interface + three small types would fragment a namespace that isn't crowded yet.
Revisit if T3.1 lands a comparably-sized surface and the combined namespace starts to feel
crowded; that's a call for whoever does that consolidation, not a pre-emptive split here.

## 2. Evaluator input shape: `ScheduledItem` only, no `ScheduleDay`

`IObjectiveEvaluator.Evaluate` takes `IReadOnlyList<ScheduledItem>` and nothing else (plus
`SoeWeights`). It deliberately does **not** take `List<ScheduleDay>` (the allocator's other
output, `SmartStudyPlanner/Models/ScheduleModels.cs`), even though Card A's baseline metrics
(`SoeBaselineMetrics.cs`) computed some raw inputs — like per-day variance — by iterating
`ScheduleDay` directly.

Reasons:

1. `ScheduleDay.Tasks` holds `ScheduledTask` (UI-only, no `MaTask`) — useless for
   `FragmentationPenalty`, which needs true task identity, not name-string matching.
2. Everything the five terms need (`Date`, `SoPhut`, `TenMon`, `MaTask`) is already on
   `ScheduledItem`. Requiring both types would force test fixtures to hand-build two correlated
   collections instead of one, for no informational gain.
3. It keeps the evaluator decoupled from the allocator's day-bucket representation — the exact
   kind of internal-representation coupling `ScheduledItem`'s own doc comment warns against
   (`Date`, not position, is the sound join key; depending on `ScheduleDay.Tasks` order would
   reintroduce a positional dependency in a different guise).

Consequence (see §3.3): `ContextContinuity` cannot use Card A's intra-day *adjacent-pair*
subject-switch definition, because that requires knowing chunk order within a day, which
`ScheduledItem` does not carry. This is a real, documented capability loss, not an oversight —
see §3.3 for the substitute operationalization.

## 3. The five terms

All five reward terms/penalty terms are computed independently per-schedule (no shared mutable
state), each documented in its own XML-doc block in `ObjectiveEvaluator.cs`. Summary below;
XML docs are the source of truth for exact formulas.

### 3.1 LoadBalance — `[0, 1]`, 1 = best

Group chunks by `Date`, sum `SoPhut` per day-used. Compute the **coefficient of variation**
(population stddev / mean) across those day-totals, then `1 / (1 + CV)`.

*Why CV, not raw stddev or variance (Card A's raw metric)*: raw stddev is denominated in
minutes, so its "goodness" depends on the absolute scale of the schedule (a 10-minute stddev is
excellent for a 600-min/day schedule and terrible for a 20-min/day schedule) — not comparable
across schedules of different total load, which a scoring term meant to rank arbitrary
candidates needs to be. CV is scale-free. `1/(1+CV)` is a standard bounded monotonic-decreasing
transform: 1.0 at CV=0 (perfectly even), asymptotic toward 0 as CV grows, no arbitrary cap
needed.

*Edge case*: 0 or 1 days used → vacuously perfect (1.0) — no unevenness is observable with fewer
than two data points.

### 3.2 SessionQuality — `[0, 1]`, 1 = best

Per-chunk trapezoidal membership over `SoPhut`: ramps 0→1 linearly over `(0, 25]` minutes, flat
`1.0` over `[25, 90]`, ramps 1→0 linearly over `(90, 180)`, `0` at `>= 180`. Schedule score is
the mean across all chunks.

*Why this shape, why these numbers*: this term wasn't hinted at by any of Card A's raw metrics
— it's operationalized from scratch here. The grounding: a study session that's too short
(under roughly 15-25 minutes) spends a disproportionate share of its time on context-loading
overhead relative to actual work done, and a session that runs unbroken for multiple hours (past
roughly 90-120 minutes) sees well-documented attention/retention drop-off — the "sweet spot" for
sustained focused study session length converges around 25-90 minutes across common
study-technique guidance (Pomodoro-style blocks and typical classroom/lecture-length framing
both land in this band). The exact boundary numbers (25/90/180) are a defensible starting point,
not an empirically-tuned constant — T3.5/G3 owns weight *values*; if a future card wants to make
these three boundary constants configurable too, that's a reasonable extension, but out of scope
here (the task was to make the term exist and be independently computable, not to perfect it).

*Distinction from FatiguePenalty*: this operates at **per-chunk** granularity (is this one
session's length in a good range?); FatiguePenalty (below) operates at **per-day** granularity
(is today, in aggregate, an unsustainably heavy day relative to other days in this schedule?).
They are not measuring the same thing and are not expected to be redundant — a schedule could
have all-ideal-length chunks (`SessionQuality = 1.0`) that are nonetheless stacked into
back-to-back heavy days (`FatiguePenalty < 0`), or vice versa.

*Edge case*: empty schedule → vacuously perfect (1.0) — no chunk to be poorly-sized.

### 3.3 ContextContinuity — `[0, 1]`, 1 = best

For each day used, `1 / (distinct subjects that day)`; schedule score is the mean across days
used.

*Why not Card A's adjacent-pair subject-switch count*: as noted in §2, that metric needs
intra-day chunk *order*, which `ScheduledItem` does not carry (by design — `Date` is the only
sound join key, per the type's own doc comment). Reaching into `ScheduleDay.Tasks` to recover an
order would smuggle allocator-internal representation into an otherwise-independent evaluator
and reintroduce a positional-correlation dependency this codebase already got bitten by once
(see `WorkloadServiceIdentityTests` history referenced in `ScheduledItem.cs`'s doc comment).
Per-day subject concentration is the strongest continuity signal computable from `Date` +
`TenMon` alone: a day touching one subject is maximally contiguous; a day juggling four subjects
is maximally fragmented, regardless of the (unknowable, from this type) order they were studied
in.

*Known limitation, documented rather than hidden*: this does **not** capture cross-day
continuity (e.g., the same subject appearing on consecutive calendar days, which arguably also
represents good context retention). A cross-day component (e.g., subject-set overlap between
adjacent used days) was considered and deliberately left out — it adds a second signal for a
single term whose job here is "exist and be independently computable" (per the task brief), not
"be the best possible continuity metric." A future card can extend this if the single-signal
version proves too coarse in practice.

### 3.4 FatiguePenalty — `[-1, 0]`, 0 = best

**Self-relative, not capacity-relative.** Group chunks by `Date` → per-day-used total minutes,
sorted chronologically. A day is "heavy" if its total exceeds the mean day-load *of this same
schedule*. Walk consecutive **calendar-adjacent** used days (`Date[i+1] == Date[i].AddDays(1)`
— a gap breaks the streak); count what fraction of those adjacent pairs are both heavy. Negate.

*Why self-relative instead of "load / capacity" as the task brief's example framing suggested*:
using absolute capacity would require adding a second required input to `Evaluate` (capacity
minutes or hours) beyond `IReadOnlyList<ScheduledItem>`, which (a) complicates every test
fixture that doesn't care about fatigue, and (b) risks blurring the D-J seam boundary — "how much
capacity is available" is squarely hard-constraint-validator territory (Card D owns capacity
limits per D-G's own text), and a capacity-aware fatigue term would need to reproduce some of
that reasoning inside the evaluator. A self-relative "are heavy days clustered without a break,
by this schedule's own standard of heavy" signal avoids needing to know anything about capacity
at all, keeping the evaluator's public surface minimal (`ScheduledItem` list + weights, nothing
else) and staying unambiguously on the "quality among already-feasible candidates" side of the
seam.

*Why calendar adjacency, not sorted-list adjacency*: two used days that are three calendar days
apart (a rest day and a following light day in between) are not an unbroken heavy streak even if
they're adjacent entries in a sorted "days used" list; only true back-to-back calendar dates
represent "no rest in between."

*Edge case*: zero calendar-adjacent day-pairs among days used (e.g., every other day) → 0 — there
is no observed streak to penalize.

### 3.5 FragmentationPenalty — `[-1, 0]`, 0 = best

Group chunks by `MaTask` (true task identity, not the display-name string). Each task
contributes `(chunk count − 1)` "extra chunks." `FragmentationPenalty = -(total extra chunks /
total chunks in schedule)`.

This is a direct, minimally-transformed continuation of Card A's own raw-metric proxy
(`FragmentedTaskCount`/`TotalFragmentChunks` in `SoeBaselineMetrics.cs`) — the task brief called
this out as "a natural fit," and no alternative operationalization was seriously considered.
Grouping by `MaTask` rather than by stripped task-name string (Card A's approach, necessary
there because it didn't have `MaTask` available pre-Card-C) is a strict improvement now that
`ScheduledItem` carries real identity — no string-stripping/regex fragility.

*Edge case*: empty schedule → 0 (vacuous — no chunk to be fragmented).

## 4. The weight vector: `SoeWeights`

New record type, `SmartStudyPlanner/Services/Soe/SoeWeights.cs`:

```csharp
public sealed record SoeWeights(
    double LoadBalanceWeight = 1.0,
    double ContextContinuityWeight = 1.0,
    double SessionQualityWeight = 1.0,
    double FatiguePenaltyWeight = 1.0,
    double FragmentationPenaltyWeight = 1.0);
```

**Separate from `WeightConfig`** (`SmartStudyPlanner/Services/WeightConfig.cs`), which holds
the *four* Decision Engine priority weights (`TimeWeight`/`TaskTypeWeight`/`CreditWeight`/
`DifficultyWeight`). No fields were added to `WeightConfig`; `SoeWeights` does not wrap or
reference it. These vectors serve different pipeline stages (task-ordering priority vs.
post-feasibility schedule quality) and merging them would erase exactly the seam D-G/D-J just
drew.

**No sum-to-1.0 constraint**, unlike `WeightConfig.IsValid()`. This is a deliberate divergence,
not an omission:

- `WeightConfig`'s four weights are a homogeneous mixture over one concept (priority
  contribution), so normalizing to a 0-1-scaled sum is meaningful and keeps `PriorityScore`
  interpretable as a weighted average.
- The SOE's five terms are **heterogeneous**: three reward terms in `[0,1]` and two
  already-negative-signed penalty terms in `[-1,0]` (see §5 on sign convention). Their linear
  combination isn't a mixture/average of a single homogeneous quantity — it's an unconstrained
  scalar used purely for **relative ranking** among feasible candidates (D-J: the objective never
  decides feasibility, only orders already-admitted candidates). There's no requirement that the
  combination live on a normalized scale.

**One constraint kept**: `IsValid()` requires all five weights `>= 0`. A negative weight would
silently flip a term's semantic direction (negative `w1` would *reward* imbalance) — a footgun,
not a legitimate tuning lever. If a future governance gate (T3.5/G3) decides signed weights are
actually wanted, that's their call to relax this — not today's default.

**Ownership of actual values**: explicitly out of scope. Defaults here (`1.0` each — equal
weighting) exist only so the type is usable/testable; T3.5/GATE G3 owns tuning, persistence, and
governance of the real values.

## 5. Result type and sign convention: `ObjectiveScore`

```csharp
public sealed record ObjectiveScore(
    double LoadBalance, double ContextContinuity, double SessionQuality,
    double FatiguePenalty, double FragmentationPenalty, double Total);
```

Holds all five raw (pre-weight) terms *and* `Total`, not just `Total` — so a test (or a future
T3.3 explanation surface) can assert on/report an individual term without a weight-isolation
trick (e.g., setting four weights to zero to inspect the fifth).

**Sign convention**, stated once here because it's easy to get backwards: the two penalty terms
are defined on `[-1, 0]`, not `[0, 1]` — i.e., they are already negative-oriented. This lets
`Total = w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty +
w5·FragmentationPenalty` use the **literal `+` from D-G's formula verbatim**, while positive
weights (`w4, w5 >= 0`) still correctly *reduce* `Total` as penalty severity increases — matching
what "penalty" means in ordinary usage. The alternative (defining the penalty terms as positive
magnitudes and expecting callers to remember to subtract them) would make D-G's "+" formula
silently add badness to the score, which is exactly backwards.

## 6. D-G enforcement: two independent, falsifiable checks

Per the task brief's explicit ask ("the intent must be demonstrably tested, not just asserted in
prose"), `ObjectiveEvaluatorTests` (in `SmartStudyPlanner.Tests/Services/Soe/`) enforces the
no-deadline rule two ways:

1. **Reflection** over every public member (properties, fields, non-special methods +
   parameters, constructors) of `IObjectiveEvaluator`, `ObjectiveEvaluator`, `SoeWeights`, and
   `ObjectiveScore`: rejects any member/parameter whose type is `DateTime`/`DateTime?`, and any
   member/parameter whose *name* contains deadline-shaped vocabulary (`deadline`, `hanchot`,
   `urgency`, `duedate`, case-insensitive).
2. **Textual scan** of the four committed production source files for the literal tokens
   `HanChot`, `Deadline`/`deadline`, `Urgency`/`urgency` — this is what caught, during
   development of this very card, that an early draft's own XML-doc comments (explaining the
   D-G rule in English/mixed prose) contained the word "deadline" and the identifier "HanChot"
   as plain text. The comments were rewritten in Vietnamese-only phrasing that describes the
   concept without spelling the forbidden identifiers, and the scan now passes. This is recorded
   here as a concrete example of the check doing its job, not a hypothetical.

Both checks were verified to be able to fail (not just pass by construction): the reflection/name
check is exercised by construction (it did fail once, as described above, before the comment
fix); a mutation test was additionally run against `ComputeFragmentationPenalty` (short-circuited
to always return `0.0`) to confirm `FragmentationPenalty_HeavilyFragmentedSchedule_ScoresPoorly`
goes red under a real regression — it did, then the mutation was reverted.

## 7. What was deliberately not built

- No deadline logic, anywhere — see §6.
- No reference to `IConstraintValidator` or any constraint/feasibility type — the evaluator's
  only production dependency is `ScheduledItem` (Card C) and its own two new types.
- No weight *tuning*, no `WeightOptimizer` integration, no persistence for `SoeWeights` — T3.5/G3.
- No wiring into `WorkloadServiceImpl` or the allocator — T3.3. `WorkloadServiceImpl.cs`,
  `ScheduledItem.cs`, `ScheduleModels.cs`, and `WeightConfig.cs` were not modified; neither
  `GenerateSchedule` call site was touched.
