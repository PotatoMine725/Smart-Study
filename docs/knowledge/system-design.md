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

- Every milestone gets a row in `docs/CHANGELOG.md`; in-flight work lives in `docs/active/`; current state in `docs/architecture/`.
- Active plans link to specs and file maps. Once shipped, the active doc is condensed into a `CHANGELOG.md` row and deleted.
- The change log is the source of truth for "is M6.1 done?" — not commit messages.
- `gitnexus_query({query: "concept"})` finds execution flows faster than `grep`; use it before reading docs.
