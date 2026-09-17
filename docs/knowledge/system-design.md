# System Design Lessons

> Distilled 2026-05-21 from the refactor sequence M1 → Slice 4 + M5 pipeline + M7 ML integration.

## Layering

### Layers that earn their keep
```
Views → ViewModels → Services → Core/* → Infrastructure
```
- **Views**: rendering + event forwarding only.
- **ViewModels**: state + command orchestration (no domain rules).
- **Services**: app-level orchestration + lifecycle management.
- **Core/***: pure domain — no UI, no DB, no I/O.
- **Infrastructure**: storage + filesystem + future cloud adapters.

Movement is one-directional. Violating this direction was the #1 source of god objects.

### When a "god object" is forming, split by responsibility, not by file size
`DecisionEngineService` was 92 lines but owned **4 responsibilities**: priority scoring, raw minute estimation, study-time suggestion formatting, ML prediction passthrough. Slice 2 split it into:
- `PriorityEvaluator` (scoring)
- `RawMinutesCalculator` (formula)
- `StudyTimeSuggestionEngine` (formatting)
- `SchedulingOrchestrator` (composition root for these three + ML predictor)

The facade shrunk to 42 lines and the `IDecisionEngine` contract did not change. Lesson: **split by reason-to-change**, not by LOC.

## Patterns that paid off

### Strategy + Chain of Responsibility for urgency rules
The original `CalculatePriority` mixed 5 if-else branches (`overdue`, `just-overdue`, `imminent`, `completed`, `beyond-horizon`) with weighted component scoring. The refactor extracted each rule into `IUrgencyRule.TryApply(task, daysLeft, cfg, out score)`:
```csharp
private readonly IReadOnlyList<IUrgencyRule> _urgencyRules = new IUrgencyRule[] {
    new OverdueRule(),
    new JustOverdueRule(),
    new ImminentRule(),
    new CompletedRule(),
    new BeyondHorizonRule(),
};
```
Adding "task paused" became a 1-class + 1-line change instead of editing the core scoring loop.

### Component pattern for weighted scoring
4 priority dimensions (`Time`, `TaskType`, `Credit`, `Difficulty`) each implement `IPriorityComponent.Score(...)` + `Weight(cfg)`. The engine does `Σ(c.Score × c.Weight)`. Adding a new dimension is one class. Tuning weights is editing `WeightConfig`.

### Dictionary lookup beats `switch` on enum
`DefaultTaskTypeWeightProvider` uses `IReadOnlyDictionary<LoaiCongViec, double>`. Adding `LoaiCongViec.ThuyetTrinh` is one dictionary entry. A `switch` would have been an open-closed-principle violation across multiple files.

### Stage-based pipeline > monolith for sequential flows
`PipelineOrchestrator` runs `IPipelineStage[]` ordered by `Order`. Each stage owns its slice of `PipelineContext`. Benefits observed in practice:
- Stage isolation made `AssessRiskStage` testable without booting the whole pipeline.
- Skip-by-policy lets you A/B different stages.
- Errors collect into `context.Errors` instead of crashing the pipeline.
- Adding `AdaptStage` was a new class + 1 line in the registration — no surgery on existing stages.

### What the view was rendered against is a second variable, not the same one the user is editing
The Workload Balancer's `CapacityHours` served two roles at once: the value the slider targets, *and*
the yardstick the chart is drawn against. With no change handler, dragging the slider never rebuilt
the schedule — it only re-ran the `[TotalMinutes, CapacityHours]` converters, so the screen showed
the **old allocation measured against the new ceiling**. That state is internally consistent,
visually plausible, and describes a schedule the algorithm never produced, which is what makes this
class of bug expensive: it is not detectable by looking at it, and every manual observation taken
through the screen inherits the fault ([`qa-gates.md`](qa-gates.md)).

The fix was to name the second role — `RenderedCapacityHours`, assigned unconditionally inside
`BuildSchedule`, with every measurement binding repointed at it and a badge shown while the two
diverge. Two things generalise:
- **State the invariant that bounds the divergence.** Because `BuildSchedule` *unconditionally*
  re-syncs rendered to target, the divergence is always clearable and no user can be trapped in a
  permanently stale view. That single invariant is what later downgraded a residual finding (the
  slider only stops on whole hours, so a `4.5` read from disk cannot be dialled back in) from defect
  to enhancement candidate. Splitting one variable into two is only safe once you can say what
  bounds their disagreement.
- **Prefer making staleness legible over recomputing behind a gesture.** Rebuilding on every slider
  change was the better UX and was rejected: it would have put a disk write and a database
  write-through behind a drag, on a path with no test coverage. A mutation probe now pins the
  rejection, so a future "helpful" change cannot quietly reintroduce it.

Same principle, different domain: *never let one scalar answer two questions* — see the `Rev`
counter in [`sync-data-model.md`](sync-data-model.md), where one number tried to be both a local
change count and a cross-device ordering.

### Every layer defensible, the composition still loses information
Slice 2 of the structural conflict fence shipped four components that each reviewed clean — an
explicit dependency registry with a guard test, a resolver, a selector, a router with no early exit —
and 942 green tests. An independent review then found three HIGH defects, all of them *between* the
components, and every probe written for them went RED on the first run:

- the selector fed only `edge.ChildType`/`edge.ChildId` into its predicates, so a conflict reachable
  only as an edge's **parent** endpoint was never selected — which left a tested Slice-1 code path
  dead in integration;
- the resolver expanded each intent against live database state **in isolation**, so a request that
  reparents a task *out of* a subtree and then tombstones the old parent invented a cascade over rows
  the real write set never touches;
- the row-effect merge had no defined "strongest effect", so the same logical request produced a
  different reported stage depending on the order the intents arrived in.

None of these is visible from inside the component that hosts it. Each one is a piece of information
that one layer holds and the next layer never receives.

**Principle.** A composition has its own contract, and a suite made of per-component tests does not
test it. For every seam, name the information that has to cross it — here: *an edge has two
endpoints*, *intents in one request modify each other's envelope*, *merging two effects requires an
order* — and write the test at the composition level, with an input that only the seam can get wrong.
A green per-component suite is fully consistent with a broken composition; that is the normal case,
not the surprising one.

### One authority per question — routability is not merge class
`RouteKnown` (may the fence route this mutation at all?) was implemented by asking
`MergeSurfaceRegistry` whether the relation field was `Structural`/`ConstraintScope`. That registry
answers a different question — *how does this field merge?* — and the two answers disagree in both
directions: a legitimate `AddLinkAsync` was rejected because its FK is `CopyOnCreate`, while
`StudyLog` writes the plan explicitly excludes would have been routable for free.

The ruling split the question three ways, one authority each, **with no fallback**: merge semantics
(`MergeSurfaceRegistry`, never consulted by routing), structural topology
(`StructuralDependencyRegistry.Edges` membership), and fence-route eligibility (an explicit
`FenceRoutable` flag per edge). Membership in either of the first two grants nothing. A fourth
column, `CascadesOnTombstone`, agrees with `FenceRoutable` on all five of today's edges — recorded in
the ruling as *a coincidence of the current domain, not a rule*, which is what stops the next
engineer from collapsing them.

Two things generalise:
- **Reusing a classification because it currently correlates is a borrowed invariant.** It fails at
  the first row where the two questions diverge, and the failure reads as a mysterious rejection far
  from the registry.
- **Prove a separation by mutating one side.** Changing the field's *merge* class turned the merge
  guards RED while every *routing* test stayed GREEN. That asymmetry is the separation, demonstrated
  rather than asserted — see [`review-methodology.md`](review-methodology.md).

Same principle, different domain: *never let one scalar answer two questions* — the `Rev` counter in
[`sync-data-model.md`](sync-data-model.md).

## Dependency injection

### Composition root pattern (`ServiceLocator`)
WPF has no built-in `HostBuilder`. The temporary fix: a `ServiceLocator` static class wrapping `IServiceProvider`. Acceptable for now; the cost is some ViewModels still pull via `ServiceLocator.Get<T>()`. Plan to migrate to constructor injection per ViewModel during the next UI sweep.

### Register adapters and facades alongside their core
When refactoring, register both the new contract and the legacy adapter so both call sites work during migration:
```csharp
services.AddSingleton<ISchedulingOrchestrator, SchedulingOrchestrator>();
services.AddSingleton<IDecisionEngine, DecisionEngineService>(); // facade
```
This lets you migrate callers one at a time.

## Refactoring strategy

### Facade-bridge pattern enables zero-breaking refactors
Every god-object split followed the same shape:
1. Add new domain types under `Core/<area>/Models`.
2. Implement new leaf classes (evaluator, engine, orchestrator).
3. Shrink the old service to a thin facade delegating to the new orchestrator.
4. Keep the **public contract identical** so external callers don't change.
5. Tests on the old contract still pass; new tests cover the new leaves.

This is how the risk extraction (2026-05-12) and the scheduling extraction (Slice 2) both stayed green throughout.

### Slice work into shippable commits
The god-object plan is 8 slices, each a single commit, each green. This prevents the "10 days of broken `dev`" failure mode. The slice schedule + commit log are visible in `CHANGELOG.md`.

### Use `gitnexus_impact` to size every change before editing
Before modifying any symbol, run `gitnexus_impact({target: "X", direction: "upstream"})`. Reports:
- direct callers
- affected execution flows
- LOW / MEDIUM / HIGH / CRITICAL risk

`SmartParser` returned LOW (only 1 method-level call site). `DecisionEngineService` returned LOW (only `DecisionEngineTests`). These reports gave confidence to ship Slices 2-3 as single commits.

### Verify with `gitnexus_detect_changes` before commit
After editing, this tool tells you which flows are now in scope. If the report shows surfaces you didn't intend to touch, you have a bug or a leak. See [`review-methodology.md`](review-methodology.md) for the wider discipline this is one instance of: independent verification instead of trusting a self-report.

## Offline-first principles

### Hard rule: the app must boot without the network
Every external boundary is wrapped behind an interface (`IModelStorageProvider`, `IStudyRepository`). Local file storage and SQLite are the defaults. Cloud adapters can be registered later in `App.xaml.cs`:
```csharp
if (appSettings.CloudEnabled && ConnectivityHelper.IsAvailable())
    services.AddSingleton<IModelStorageProvider, CloudModelStorageProvider>();
else
    services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>();
```

### ML is an enhancement, not a dependency
Three independent fallback layers:
1. If `IMLModelManager.IsReady == false` → formula fallback.
2. If model is ready but prediction confidence < 0.6 → formula fallback.
3. If prediction throws → formula fallback.

The app remains 100% usable with no model file on disk.

### Async warm-up keeps startup snappy
`MLModelManager.InitializeAsync()` runs on `Task.Run(...)` from `App.xaml.cs`. Exceptions are swallowed there. The UI launches even if ML never warms.

## Data + persistence

### Cascade rules belong in `OnModelCreating`
Greppable, explicit, version-controlled. M6.1 added cascade rules for `TaskNote` and `TaskReferenceLink` so deleting a task drops its dependents. See [`sync-data-model.md`](sync-data-model.md) for how this same config was repurposed — kept, not removed — to drive EF's in-memory cascade *fixup* once deletes became soft tombstones instead of real `DELETE`s.

### Note + reference link storage choice: separate tables
Considered: stuff notes and links into JSON columns on `StudyTask`. Rejected because:
- query performance for "tasks with > 3 links" is fine on a normalized table; messy on JSON.
- migrations stay simple.
- parser-isolation invariant (quick parser must not touch notes/links) was easier to enforce when notes/links lived in different aggregates.

Lesson: only fold side data into the main entity if you will never query it independently.

### Aggregate snapshot pattern for cross-cutting metrics
`UserStatsSnapshot` (built in Slice 4) is a flat DTO holding `MissRate`, `AverageDelayDays`, `FocusStreakDays`, `TotalStudyMinutesLast30Days`, ... It is what M8-B Weight Optimizer needs as features. Designing the snapshot up front before M8-B lets the optimizer be implemented as `snapshot → suggestion` without touching the DB.

## Testing strategy

### Test the leaves, smoke the facade
After Slice 2, `RawMinutesCalculatorTests` (4) + `StudyTimeSuggestionEngineTests` (5) directly cover the new leaves. The legacy `DecisionEngineTests` keeps protecting the public contract by exercising the facade. Result: 9 new fine-grained tests + 0 changes to legacy tests + 0 regression.

### In-memory SQLite for repository tests
`RepositoriesTests` runs against an in-memory SQLite instance. Each `Sqlite*Repository` accepts `Func<AppDbContext>` so the test can supply a context with `UseSqlite("Data Source=:memory:")`. Real SQL semantics, no disk I/O.

### One-shot dev seed lives in `DevTools` and is tagged
`DbSeedTests` is tagged `[Trait("Category", "Seed")]` and excluded from CI. It seeds an isolated in-memory SQLite DB (180 logs across 3 difficulty groups) — useful as a schema smoke test and data-generation sanity check without touching the real app database.

### Test count is a health metric
156 (Slice 4) ← 152 (Slice 3) ← 147 (Slice 2) ← 146 (URL fix) ← 138 (risk extraction) ← 128 (M7) ← 119 (M5) ← 87 (pre-M4). Every refactor either adds tests or holds the line. Never let regressions count as "neutral".

## Documentation as a load-bearing artifact

- **Canonical convention: [`docs/README.md`](../README.md) §Artifact types** — which artifact type answers which question, where it lives, and the two cross-cutting rules (claim → evidence → scope → uncertainty; amend rather than rewrite). Written 2026-08-19; the bullets below are the older, narrower lifecycle summary and defer to it.
- Every milestone gets a row in `docs/CHANGELOG.md`; in-flight work lives in `docs/active/`; current state in `docs/architecture/`.
- Active plans link to specs and file maps. Once shipped, the active doc is condensed into a `CHANGELOG.md` row and deleted.
- The change log is the source of truth for "is M6.1 done?" — *when it is current*, which is not automatic: as of 2026-08-19 it ended at 2026-08-02 and carried no record of Epic 3, which had closed. Check its last entry against `git log` before trusting a "not done" reading; a milestone missing from it may simply never have been written down.
- `gitnexus_query({query: "concept"})` finds execution flows faster than `grep`; use it before reading docs.
