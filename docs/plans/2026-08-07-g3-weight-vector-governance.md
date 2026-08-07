# GATE G3 — `w1…w5` weight-vector governance

**Date:** 2026-08-07 · **Gate:** G3 (master plan) · **Closes:** architecture-freeze record §4 item
"B5 — weight-vector governance", execution-plan line 35 ("G3 — `w1…w5` weight-vector governance.
Blocks M3.2 ship.") and CP-4 · **Status:** **Drafted, pending owner ratification at CP-4.** This note
records recommended decisions **G3-1 … G3-3**; nothing here is ratified until the owner reviews it —
same posture Card B's G2 note held before CP-1. No code changes in this pass.

**Reads with:**
[architecture freeze 2026-07-02](2026-07-02-architecture-freeze-decisions.md) D-G/D-H/D-J and §4 (B5);
[G2 note](2026-08-04-g2-optimization-pass-semantics.md) (the structural template this note follows,
scaled to G3's narrower scope) — in particular its §4 "frozen guardrails" table and §7 ADR format;
[epic-3 execution plan](2026-08-04-epic-3-execution-plan.md) lines 35, 95-97, 396, 474, §5.4 DP-2;
[T3.2 seam decisions](2026-08-05-t32-objective-evaluator-seam-decisions.md) §4 (`SoeWeights`, already
ratifies the non-sum-to-1.0 divergence from `WeightConfig` — not relitigated here).

---

## 0. What was open, and what this note has to produce

D-G froze the objective's *shape* (`w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality +
w4·FatiguePenalty + w5·FragmentationPenalty`) on 2026-07-02 but explicitly left the vector's
**governance** open as B5: "ownership, guardrails, relation to `WeightOptimizer`." Card E (T3.2)
built `SoeWeights` and `ObjectiveEvaluator` against that frozen shape, and `SoeWeights`'s own XML doc
comments already defer three things by name to "a future gate (T3.5/GATE G3)": the concrete values,
the governance mechanism, and whether any constraint beyond non-negativity is warranted. This note is
that gate. It must decide, and record as ADR-style decisions:

1. **Ownership** — where the shipped `w1…w5` default values live and what/who can change them.
2. **Guardrails** — ratify or extend the non-negativity-only rule already in `SoeWeights.IsValid()`,
   and explicitly rule on the all-zero degenerate case rather than leaving it unmentioned.
3. **Relation to `WeightOptimizer`** — state plainly, grounded in the code, whether the existing
   `WeightOptimizer` machinery touches `SoeWeights` today and whether it should in Epic 3.

Per DP-2 (execution plan §5.4), the plan's own working answer is: governance is this gate's job,
**tuning is a separate, later question**, Epic 3 ships a fixed default, and `WeightOptimizer`
extension to `w1…w5` is excluded from Epic 3, data-gated on `OptimizerRunLog` accrual. This note
verifies that answer against the code (as of this session's HEAD) rather than restating it from the
plan, and finds it holds — more strongly than the plan itself argued, per §1's G3-3.

---

## 1. Decisions

### G3-1 — Ownership: the compiled record defaults on `SoeWeights` are the shipped default; changing them is a code change

**Decision.** The concrete, shipped `w1…w5` values are the C# default-parameter values on the
`SoeWeights` record itself
(`SmartStudyPlanner/Services/Soe/SoeWeights.cs:41-45` — `LoadBalanceWeight = 1.0`,
`ContextContinuityWeight = 1.0`, `SessionQualityWeight = 1.0`, `FatiguePenaltyWeight = 1.0`,
`FragmentationPenaltyWeight = 1.0`, i.e. equal weighting across all five terms). There is no config
file, no database row, no app-settings entry, and no user-facing tuning surface for this vector — the
type itself *is* the storage. Ownership of the values is **the code owner of `SoeWeights.cs`**, and
changing the shipped default is an ordinary code change (edit the record's default arguments, or the
composition root that constructs it once one exists — see below), reviewed and merged like any other
production change. No runtime/administrative override mechanism is in scope for Epic 3.

**Why an explicit decision is needed rather than a description of what already is.** `IsValid()`'s
non-negativity rule and the `1.0`-each defaults already exist from Card E — but existing-by-default is
not the same as *decided*. Before this note, nothing had ruled out, say, moving the defaults to a
`SoeWeightsDefaults` static class, an `appsettings`-style file, or a DB-seeded row the way
`WeightConfig` arguably could be. This decision closes that off: **the record's own default parameters
are the ratified home**, not a placeholder pending a "real" mechanism. A future governance mechanism
(persistence, user tuning) is DP-2's problem, data-gated as below — it is not something this gate
leaves ambiguous in the meantime.

**Verified in code — the sharper finding.** Tracing every construction site of `SoeWeights` and
`ScheduleOptimizer` repo-wide (`grep -r "new SoeWeights(" `, `grep -r "new ScheduleOptimizer("`) finds
**zero production call sites**. All thirteen construction sites are under
`SmartStudyPlanner.Tests/Services/Soe/` (`ObjectiveEvaluatorTests.cs`, `ScheduleOptimizerTests.cs`,
`SoeOptimizeCorpusMetrics.cs`, `SoeT34InvariantTests.cs`). Confirmed further: `WorkloadServiceImpl.cs`
contains no reference to `ScheduleOptimizer`/`IScheduleOptimizer`/`SoeWeights` at all, and the
production pipeline's only allocator call site,
`BalanceWorkloadStage.cs:41` (`schedule = _workloadService.GenerateSchedule(context.Semester,
capacity)`), still calls `IWorkloadService.GenerateSchedule` directly — the same pre-Epic-3 method,
not `ScheduleOptimizer.Optimize`. **`ScheduleOptimizer` (and therefore `SoeWeights`) is not wired into
any production pipeline as of this session's HEAD.** That wiring is separate, unstarted integration
work — this note does not schedule it and it is not in this card's scope (Card H is docs-only), but it
is recorded here because it changes what "ownership" means today: there is currently no production
code path that reads a `SoeWeights` value at all, so today's "shipped default" is inert — exercised
only by tests — until that wiring lands. **The decision above (record defaults are the ratified home)
is written so that whoever does that wiring inherits a decided default, exactly the way G3-1 is
supposed to work — a decision made ahead of the code that will consume it**, mirroring D2's framing in
the G2 note (ratify ahead of implementation, don't leave the implementer to invent it).

**A second, finer-grained finding.** Not even the tests rely on the compiled defaults implicitly —
`grep -r "new SoeWeights()"` (zero-argument construction) returns **no matches** anywhere in the repo.
Every call site passes explicit constructor arguments, including the two call sites that happen to
reproduce the default numerically: `SoeOptimizeCorpusMetrics.DefaultWeights = new(1.0, 1.0, 1.0, 1.0,
1.0)` (`SmartStudyPlanner.Tests/Services/Soe/SoeOptimizeCorpusMetrics.cs:87`, doc-commented
"trọng số mặc định `SoeWeights` — G3/T3.5 chưa ratify") and `ScheduleOptimizerTests.cs`'s
`UnitWeights` constant. Both are named, explicit, test-local constants that *happen to equal* the
record defaults — they do not depend on the record's own default-parameter mechanism. This does not
change the ownership decision, but it means the record defaults are, right now, unexercised by any
runtime code path; the operative "default" actually driving today's T3.4 corpus measurements is
`SoeOptimizeCorpusMetrics.DefaultWeights`, a test-only constant. This note ratifies that the two must
be — and currently are — numerically identical, and that any future change to the shipped default
(`SoeWeights.cs`'s record defaults) must be mirrored in `SoeOptimizeCorpusMetrics.DefaultWeights` (or
its replacement) so the corpus measurements stay representative of what would actually ship.

### G3-2 — Guardrails: non-negativity is ratified as sufficient for Epic 3; the all-zero case is a named, deferred limitation

**Decision.** `SoeWeights.IsValid()`'s existing single rule — all five weights `>= 0` — is **ratified
as the complete guardrail set for Epic 3**. No additional constraint is added by this note (this card
makes no code changes, per its scope, and this decision would not add one even if it could — see
below). This is a direct extension of the reasoning already committed in the T3.2 seam-decision doc
(§4): a negative weight silently flips a term's semantic direction (negative `w1` would *reward*
imbalance) and is a footgun, not a legitimate tuning lever; nothing about closing G3 changes that
argument, so it is ratified rather than re-argued.

**The all-zero case — named explicitly, not silently omitted.** A `SoeWeights` with all five weights
`= 0` passes `IsValid()` today and is a real degenerate case: `ObjectiveEvaluator.Evaluate`'s `Total`
becomes `0` for **every** schedule regardless of its actual quality (`ObjectiveEvaluator.cs:35-40`),
so `G2-2`'s comparator (`IsBetterThan`, defined in the G2 note and implemented in
`OptimizerComparator`) can never find one checkpoint strictly better than another on quality grounds —
every tie resolves to the lowest `k` (G2-2's own tie-break rule), which in practice means the pass
loop stops contributing quality-driven improvements and only ever acts through the
feasibility/admissibility gate (which does not depend on `SoeWeights` at all — `CompareFeasibility`
reads only `ViolationCount`/`OverdueMinutes` from the validator). **This is named here, mirroring D6's
pattern in the G2 note, precisely so nobody rediscovers it later as an unreported bug.**

**Ruling: deferred, not blocking, and here is why the deferral is safe today specifically.** Three
reasons, all grounded in G3-1's own findings:

1. **This card cannot add a guard even if it wanted to.** Card H's scope is `docs/plans/` only, zero
   production files — the instruction under which this note is written explicitly forbids editing
   `SoeWeights.IsValid()`.
2. **The all-zero vector is not reachable through any production code path today.** G3-1 established
   that no production code constructs a `SoeWeights` at all, and the only value that will ship —
   ratified by G3-1 — is the `1.0`-each default. There is no tuning surface, no user input, and no
   `WeightOptimizer` integration (G3-3) that could produce an all-zero vector in Epic 3. The guard
   would protect against a state that cannot currently occur outside a test author deliberately
   constructing one.
3. **The guard becomes relevant exactly when DP-2's tuning work starts, and that work is itself
   data-gated and out of Epic 3 (G3-3).** The natural place to add a "not all zero" rule is the same
   change that first makes the vector reachable by something other than a hardcoded default — i.e.,
   whatever eventually implements DP-2. Adding the guard now, ahead of any caller that could trigger
   it, would be speculative hardening with no path to being exercised.

**Disposition, stated the way D6 stated its residual loss:** the all-zero degenerate case is an
accepted, disclosed limitation of `SoeWeights.IsValid()`'s current single-rule guardrail. It does not
block Epic 3 ship. A follow-up — most naturally bundled with whichever card implements DP-2's
`WeightOptimizer`-to-`w1…w5` extension — owes either a `IsValid()` "not all zero" rule or an explicit
re-ruling that it is still unnecessary at that point (e.g., if the tuning mechanism it ships already
structurally excludes an all-zero output). This is named here so that follow-up work finds it already
written down instead of filing it as a freshly-discovered defect.

### G3-3 — Relation to `WeightOptimizer`: two unrelated mechanisms today; extension excluded from Epic 3, and the data-gate is stronger than DP-2 stated

**Decision.** Ratify DP-2 as written, with the code-verification DP-2 itself did not carry out in the
execution plan. Verified in code (not merely re-asserted from the plan):

- `IWeightOptimizerService.SuggestAsync(WeightConfig current, CancellationToken ct)`
  (`SmartStudyPlanner/Core/ML/Contracts/IWeightOptimizerService.cs:14`) takes and returns only
  `WeightConfig`/`WeightConfigSuggestion` — the Decision Engine's four-weight priority vector
  (Time/TaskType/Credit/Difficulty), never `SoeWeights`.
- `WeightOptimizerService.SuggestAsync` (`SmartStudyPlanner/Services/ML/WeightOptimizer/
  WeightOptimizerService.cs:26-33`) is a thin async wrapper: fetch a `UserStatsSnapshot`, then
  `WeightRuleEngine.Compute(current, stats)` — `current` is the `WeightConfig` parameter throughout;
  nothing in the method touches `SoeWeights`, `ScheduleOptimizer`, `IObjectiveEvaluator`, or any SOE
  type.
- Repo-wide, `SoeWeights` appears **only** under `SmartStudyPlanner/Services/Soe/` (production) and
  `SmartStudyPlanner.Tests/Services/Soe/` (tests) — it does not appear anywhere under
  `Core/ML/Contracts/` or `Services/ML/WeightOptimizer/`, and `WeightConfig`/`WeightOptimizer` do not
  appear anywhere under `Services/Soe/`.

**Conclusion, stated plainly per the task's own framing.** `WeightOptimizer` and `SoeWeights` are
**two fully separate mechanisms today** — not the same mechanism extended, not sharing an interface,
base type, converter, or any cross-reference — that merely happen to share the surface shape "a named
record of weights consumed by a scoring computation." The code is **consistent** with DP-2's claim;
this note upgrades that claim from planning-time assertion to code-verified fact.

**Extending `WeightOptimizer` to `w1…w5` is excluded from Epic 3**, per DP-2, and this note ratifies
that exclusion rather than reopening it. The reason DP-2 gives — no data exists to tune against until
`OptimizerRunLog` has accrued rows, the same data-gated pattern as M8-B — is verified here to be
**stronger than DP-2 itself stated**, not weaker: `IOptimizerRunLogWriter`'s own doc comment
(`SmartStudyPlanner/Services/Soe/OptimizerRunLogWriter.cs:9-12`) states explicitly that it is "KHÔNG
phải một seam công khai đã được wire vào bất kỳ pipeline sản phẩm nào ở Card G này" (not a seam wired
into any production pipeline in Card G) — it is exercised only by the T3.4 test harness's corpus run,
the same way `IConstraintValidator`/`IObjectiveEvaluator` existed before being consumed. Combined with
G3-1's finding that `ScheduleOptimizer` itself has no production caller, **zero `OptimizerRunLog` rows
exist in any real production database today** — not "too few to tune from," but categorically none.
DP-2's data-gate is therefore not merely unmet, it has not yet started accumulating, which makes
excluding the `WeightOptimizer` extension from Epic 3 an even easier call than the plan argued: there
is no partial dataset to be tempted into tuning against prematurely.

**What this note does not decide about a future extension.** Whether a future `WeightOptimizer`
extension to `w1…w5` should reuse `IWeightOptimizerService`'s shape, be a sibling service, or something
else entirely is unscoped here — that is a design question for whoever picks up DP-2, gated on two
prerequisites this note now makes explicit: (a) `ScheduleOptimizer` must actually be wired into a
production pipeline (G3-1's finding), and (b) `OptimizerRunLog` rows must actually begin accruing from
real usage of that wiring (this decision's finding) before there is any data to design against.

---

## 2. What this note does **not** decide

- **The numeric tuning of `w1…w5` beyond the shipped default.** DP-2's business — excluded from
  Epic 3, data-gated as above.
- **Any persistence or user-facing configuration mechanism for the vector.** Not needed while the
  vector has no production caller (G3-1) and no tuning mechanism (G3-3); would be speculative to design
  now.
- **Wiring `ScheduleOptimizer`/`SoeWeights` into the production pipeline** (`BalanceWorkloadStage.cs`,
  `WorkloadServiceImpl.cs`, or their DI composition). This note surfaces the gap as a fact material to
  G3-1 and G3-3's reasoning; closing it is separate implementation work with its own task card, out of
  this docs-only card's scope, and its owner/timing is for the person picking up remaining T3.3-adjacent
  integration work to name — not decided here.
- **Whether the all-zero `IsValid()` guard should ever be added**, only that it is not added *now* and
  the reasoning for why not (G3-2). The eventual ruling belongs to whoever implements DP-2.
- **`SoeWeights`'s non-sum-to-1.0 divergence from `WeightConfig.IsValid()`.** Already ratified in the
  T3.2 seam-decision doc; not relitigated, only referenced (per the task's own instruction).
- **D-G/D-H/D-J** and the `Optimize(schedule) → (schedule, report)` seam. Untouched — see §3.

---

## 3. Frozen guardrails — confirmation, not relitigation

| Frozen item | Status after G3 | Why it is untouched |
|---|---|---|
| **D-G** — objective formula shape, quality only, no deadline term | **Preserved** | This note discusses ownership/guardrails/relation of the vector `w1…w5` already names; it adds no term and changes no term's meaning. |
| **D-J** — validator and evaluator independent, no score can purchase a violation | **Preserved** | G3-2's all-zero discussion is entirely about the *quality* comparator; it does not touch `CompareFeasibility`/`IsAdmissible`, which read only the validator's `Eval` fields and never `SoeWeights`. |
| **T3.2's non-sum-to-1.0 decision** | **Preserved, referenced** | Not reopened (task instruction); G3-1/G3-2 build on it without restating its argument. |
| **`SoeWeights.IsValid()`** (non-negativity only) | **Ratified as sufficient for Epic 3 (G3-2)** | No code change; the existing single rule is confirmed complete for this gate, with the one known gap (all-zero) named and deferred rather than silently accepted. |
| **`Optimize(schedule) → (schedule, report)` seam** | **Unchanged** | Nothing in this note touches the seam's signature or the `ScheduleOptimizer` implementation. |

---

## 4. Consequences for downstream work

- **CP-4 (ship gate)** — this note is the artifact CP-4 asks for ("G3 ratified — `w1…w5`
  governance"). It is **drafted**, not ratified; the owner's review at CP-4 is what changes its status
  line at the top of this file, mirroring how the G2 note records its own ratification date once given.
- **Whoever wires `ScheduleOptimizer` into production** inherits a decided default (G3-1) instead of
  having to invent one, and inherits the obligation to keep `SoeOptimizeCorpusMetrics.DefaultWeights`
  (or its replacement) numerically synced with `SoeWeights`'s record defaults if either changes.
- **Whoever picks up DP-2** (WeightOptimizer extension to `w1…w5`) inherits: (a) confirmation that no
  code relationship exists to build on top of today — a genuinely new integration, not an extension of
  existing plumbing; (b) two concrete prerequisites named in G3-3 (production wiring, then real
  `OptimizerRunLog` accrual) before there is data to design against; (c) the open all-zero guardrail
  question from G3-2, to close in the same change.
- **On ratification**, this line stops being true and must be updated in the same commit as the owner's
  sign-off: the status line at the top of this file, and the execution-plan's CP-4 row
  (`2026-08-04-epic-3-execution-plan.md` §3.10) if the plan is later touched to record gate closures the
  way it records G2's.

---

## 5. Decisions made (ADR-style)

### D1 — Ratify the record's own default parameters as the shipped default, rather than invent a separate mechanism

- **Why:** `SoeWeights` already carries default constructor arguments from Card E, and a corpus-test
  constant (`SoeOptimizeCorpusMetrics.DefaultWeights`) already reproduces them numerically and is
  already doc-commented as the provisional default pending G3. Inventing a new storage mechanism
  (config file, DB row, static defaults class) would create a second place to keep in sync with the
  first for no expressed need — there is no tuning UI, no persistence requirement, and no production
  caller yet (G3-1) that would benefit from indirection.
- **What for:** whoever eventually wires `ScheduleOptimizer` into production has exactly one place to
  read the default from, already decided, instead of a design question bundled into their integration
  work.
- **Experience:** the temptation with an "ownership" question is to reach for a more elaborate
  mechanism because it sounds more governed. The actual governance gap here was never storage — it was
  that nobody had said "this is the answer" out loud. Saying it costs nothing extra when the simplest
  mechanism (compiled defaults) already matches what every call site does today.

### D2 — Trace every construction site before answering "who owns the default," rather than reason from the type's doc comments alone

- **Why:** `SoeWeights`'s own XML doc comments already gesture at deferring governance to G3, which
  could have been treated as sufficient grounding to write this note from prose alone. Tracing
  `grep -r "new SoeWeights("` and `grep -r "new ScheduleOptimizer("` instead surfaced two facts the
  prose did not state: zero production call sites, and zero *zero-argument* constructions anywhere —
  even tests are explicit. Both facts change what "ownership" means in practice (there is currently no
  runtime owner, only a compile-time one) and both are load-bearing for G3-1 and G3-3's data-gate
  argument.
- **What for:** the note's central claims are falsifiable against a `grep`, the same standard the G2
  note's D5 set with `IWorkloadService.GenerateSchedule`'s signature.
- **Experience:** the task brief anticipated exactly this ambiguity ("a bare `new SoeWeights()`
  somewhere, a test-only construction, or is nothing in production wired yet") — which of the three
  turned out to be true was not obvious from reading the type alone, and the actual answer (the third
  option, with the added nuance that even tests are fully explicit) is more informative than any of the
  three options read in isolation.

### D3 — Name the all-zero guardrail gap instead of treating non-negativity as automatically sufficient

- **Why:** "ratify the existing rule" is the path of least resistance for a docs-only gate, and it would
  have been easy to stop there. Working through what an all-zero vector actually does to `G2-2`'s
  comparator (every checkpoint ties on quality, ties keep the lowest `k`) surfaced a real, if currently
  unreachable, degenerate case. Not naming it would have left a reader of `SoeWeights.IsValid()` to
  discover it independently, exactly the failure mode D6 in the G2 note was written to prevent for the
  residual-loss limitation.
- **What for:** whoever implements DP-2 finds this already written down, with the specific reason it
  was safe to defer (unreachable today) rather than having to re-derive both the risk and the argument
  for deferring it.
- **Experience:** the same discipline that requires verifying code claims (D2) also requires tracing
  code *consequences* — what does the existing rule actually permit? — not just what the existing rule
  says. The gap was invisible from the rule's text and only visible from tracing what the comparator
  does with the value the rule allows through.

### D4 — Verify DP-2's data-gate claim against the code, and report that it is stronger than stated, not merely confirmed

- **Why:** the task instruction explicitly asked to verify the plan's own claim rather than restate it,
  the same posture D5 in the G2 note modeled for the D-H scope claim. `OptimizerRunLogWriter`'s doc
  comment turned out to make the case more strongly than DP-2's prose — it names explicitly that it has
  no production caller yet, which combined with G3-1's finding about `ScheduleOptimizer` means the
  data-gate has not started, not merely that it has produced too little data so far.
- **What for:** a reader deciding whether to fast-track DP-2 gets the accurate, stronger reason to wait,
  instead of the plan's own slightly softer framing ("no data exists to tune against yet," which could
  be misread as "a little exists").
- **Experience:** verifying an already-plausible claim can still find a stronger version of it. The
  value of the check was not in catching an error — DP-2 was correct — but in finding the sharper,
  more falsifiable statement underneath the correct one.
