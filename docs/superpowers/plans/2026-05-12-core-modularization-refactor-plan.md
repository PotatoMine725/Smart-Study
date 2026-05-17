# Core Modularization Refactor Plan
## Plan · 2026-05-12

> **Goal:** split the current monolithic decision/risk logic into small, testable domain modules under `/Core`, while keeping the app MVVM-based, offline-first, and deterministic at the scheduling core.

> **Status:** planned

> **Primary architectural direction:**
> - Replace “God Objects” such as `DecisionEngineService` and `RiskAnalyzerService` with composable domain modules.
> - Keep the scheduling core rule-based and explainable.
> - Use ML.NET as an augmenting layer only, not as the source of truth for scheduling.
> - Keep SQLite as the working database in a local-first architecture.
> - Add PostgreSQL later only behind a sync/cloud layer.
> - Preserve MVVM boundaries: Views → ViewModels → Application/Services → Core/Infrastructure.
> - Protect the ML delivery path: this refactor must not break M8-A/M8-B parser, classifier, or optimizer work.

---

## 1. What the codebase currently tells us

A quick scan of the current repository shows the architecture is already halfway toward the target shape, but still concentrated in a few service classes:

- `SmartStudyPlanner/Services/DecisionEngineService.cs` mixes priority scoring, raw minute estimation, study-time suggestion, and ML-backed prediction.
- `SmartStudyPlanner/Services/RiskAnalyzer/RiskAnalyzerService.cs` is already component-based, but still acts as the top-level risk facade.
- `SmartStudyPlanner/Services/Pipeline/` already contains stage-based flow pieces, which is a strong starting point for formalizing a pipeline.
- `SmartStudyPlanner/Data/` already exists for persistence, which fits the SQLite-first/repository direction.
- `SmartStudyPlanner/ViewModels/` and `SmartStudyPlanner/Views/` are separated, so the main refactor should avoid pushing domain logic into the UI layer.
- `SmartParser`, `PipelineOrchestrator`, `StudyTimePredictorService`, and the existing strategy interfaces indicate the solution already has useful seams for extraction.
- The M8 ML plan already assumes offline-first ML artifacts, stable fallback behavior, and careful DI wiring; those assumptions must remain intact while refactoring the core.

This means the refactor should be an extraction and boundary-fixing exercise, not a rewrite.

---

## 2. Refactor constraints

These constraints should remain true throughout all phases:

1. **MVVM stays intact**
   - Views should only render and forward interaction.
   - ViewModels should orchestrate application use-cases, not implement domain rules.
   - Core logic should move below ViewModels, never into them.

2. **Offline-first stays default**
   - SQLite remains the working database.
   - The app must continue to function without network access.
   - Sync must be additive and optional.

3. **Scheduling core stays deterministic**
   - Rule-based parsing, scoring, and balancing remain the source of truth.
   - ML.NET can assist intent, effort, and difficulty prediction.
   - ML must never become the only path for core schedule generation.

4. **ML delivery is a protected dependency**
   - Refactor work must not rename, relocate, or hard-couple the existing M8 ML entry points without an adapter.
   - Parser, classifier, optimizer, model storage, and schema contracts should stay stable until M8 is complete.
   - If a refactor touches ML-adjacent code, it must keep backwards-compatible interfaces or provide wrappers.

5. **Refactor must be incremental**
   - Existing features should keep working during migration.
   - Each phase should be shippable on its own.
   - New modules should be introduced behind interfaces.

---

## 3. Target architecture

### 3.1 Core module layout

Create a dedicated `/Core` area with these top-level domains:

- `Core/Parsing`
- `Core/Scheduling`
- `Core/Risk`
- `Core/Capacity`
- `Core/ML`
- `Core/Sync`

Each module should own its own contracts, engines, models, and policies.

### 3.2 Supporting layers

Keep the rest of the app organized around clean responsibilities:

- `Application/` for app-level orchestration/use-cases
- `Infrastructure/` for SQLite, file storage, and future PostgreSQL sync adapters
- `ViewModels/` for MVVM orchestration and UI state
- `Views/` for presentation only
- `Services/LegacyAdapters/` for transitional wrappers around old service APIs

---

## 4. Current pipeline to preserve and formalize

The intended runtime pipeline is:

1. `Text Input`
2. `Intent Classification` using ML.NET
3. `Time Parsing Engine` using rule-based parsing
4. `Task Extraction`
5. `Workload Balancer`
6. `Adaptive Capacity`
7. `Schedule Output`
8. `Feedback Loop`

### Meaning of each stage

- **Text Input**: raw user task text, note text, or quick-add command.
- **Intent Classification**: infer user intent such as create task, update task, reschedule, parse deadline, estimate effort.
- **Time Parsing Engine**: deterministic date/time extraction and normalization.
- **Task Extraction**: build canonical domain tasks from text and intent.
- **Workload Balancer**: distribute work based on urgency, dependency, effort, and available capacity.
- **Adaptive Capacity**: adjust scheduling using user availability and historical throughput.
- **Schedule Output**: produce the final recommended schedule.
- **Feedback Loop**: record outcomes and use them to improve future parsing/scheduling.

---

## 5. Detailed map from current code to target modules

### 5.1 Current code → target module mapping

- `SmartStudyPlanner/Services/DecisionEngineService.cs` → `Core/Scheduling`, `Core/Capacity`, `Core/ML`
- `SmartStudyPlanner/Services/RiskAnalyzer/RiskAnalyzerService.cs` → `Core/Risk`
- `SmartStudyPlanner/Services/SmartParser.cs` → `Core/Parsing`
- `SmartStudyPlanner/Services/Pipeline/` → `Application/UseCases` or `Core/*/Orchestrators`
- `SmartStudyPlanner/Data/` → `Infrastructure/Persistence/SQLite` + repositories
- `SmartStudyPlanner/ViewModels/` → keep, but thin out logic further
- `SmartStudyPlanner/Services/ML/` → keep stable during M8, then gradually move behind `Core/ML` adapters rather than a hard cutover

### 5.2 Existing service responsibilities → future home

#### `DecisionEngineService`
Current responsibilities:
- priority scoring
- raw minute estimation
- study-time suggestion formatting
- ML-backed study-time prediction

Future split:
- `Core/Scheduling/Evaluators/*` for score calculation
- `Core/Scheduling/Engines/IRawMinutesCalculator.cs`
- `Core/Scheduling/Engines/IStudyTimeSuggestionEngine.cs`
- `Core/ML/Predictors/IStudyTimePredictor.cs` as a wrapped augmentation dependency
- `Services/LegacyAdapters/DecisionEngineServiceAdapter.cs` if the old API must remain temporarily

#### `RiskAnalyzerService`
Current responsibilities:
- compose risk components
- aggregate risk score
- expose risk assessment to ViewModels and other services

Future split:
- `Core/Risk/Evaluators/*` for factor-specific scoring
- `Core/Risk/Aggregators/IRiskAggregator.cs`
- `Core/Risk/Orchestrators/IRiskOrchestrator.cs`
- `Services/LegacyAdapters/RiskAnalyzerServiceAdapter.cs` if a facade is still needed

#### `SmartParser`
Current responsibilities:
- interpret user text
- infer task fields
- incorporate heuristic parsing and deadline hints
- potentially consult ML helpers

Future split:
- `Core/Parsing/Engines/ITimeParsingEngine.cs`
- `Core/Parsing/Engines/IIntentClassifier.cs`
- `Core/Parsing/Engines/ITaskExtractionEngine.cs`
- `Core/Parsing/Orchestrators/IParsingOrchestrator.cs`

#### `PipelineOrchestrator` and `Services/Pipeline/Stages/*`
Current responsibilities:
- stage sequencing
- workload balancing
- risk assessment integration
- adaptive flow

Future split:
- `Application/UseCases/*` for app-level workflow orchestration
- `Core/Scheduling/Orchestrators/*` for schedule generation
- `Core/Capacity/Engines/*` for capacity adaptation

#### `Data/AppDbContext.cs` and `Data/StudyRepository.cs`
Current responsibilities:
- local persistence
- repository access
- storage concerns

Future split:
- `Infrastructure/Persistence/SQLite/*`
- `Infrastructure/Persistence/Repositories/*`
- `Infrastructure/Persistence/Migrations/*`

#### `Services/ML/*`
Current responsibilities:
- model loading/storage
- training / prediction
- schema contracts
- seed data / model manager

Future split:
- keep exact files stable through M8
- add `Core/ML` wrappers/adapters around them after M8 contracts settle
- only then move orchestration outward if needed

---

## 6. Folder structure with detailed submap

```text
SmartStudyPlanner/
  Core/
    Parsing/
      Contracts/
        IParsingOrchestrator.cs
        IIntentClassifier.cs
        ITimeParsingEngine.cs
        ITaskExtractionEngine.cs
        ITextNormalizer.cs
        IDeadlineHintResolver.cs
      Engines/
        TextNormalizer.cs
        RuleBasedTimeParsingEngine.cs
        TaskExtractionEngine.cs
        DeadlineHintResolver.cs
      Models/
        ParsedInput.cs
        ParsedIntent.cs
        ParsedTaskCandidate.cs
        ParsingConfidence.cs
      Policies/
        IntentConfidencePolicy.cs
        ParsingFallbackPolicy.cs
      Orchestrators/
        ParsingOrchestrator.cs
    Scheduling/
      Contracts/
        ISchedulingOrchestrator.cs
        IPriorityEvaluator.cs
        IRawMinutesCalculator.cs
        IStudyTimeSuggestionEngine.cs
        IWorkloadBalancer.cs
      Evaluators/
        Priority/
        Time/
        Type/
        Difficulty/
      Engines/
        RawMinutesCalculator.cs
        StudyTimeSuggestionEngine.cs
        WorkloadBalancer.cs
      Models/
        ScheduleCandidate.cs
        SchedulingScoreBreakdown.cs
        SchedulePlan.cs
      Orchestrators/
        SchedulingOrchestrator.cs
    Risk/
      Contracts/
        IRiskOrchestrator.cs
        IRiskAggregator.cs
        IRiskExplanationBuilder.cs
      Evaluators/
        DeadlineUrgency/
        ProgressGap/
        PerformanceDrop/
      Aggregators/
        RiskAggregator.cs
      Models/
        RiskAssessment.cs
        RiskFactorBreakdown.cs
        RiskExplanation.cs
      Orchestrators/
        RiskOrchestrator.cs
    Capacity/
      Contracts/
        ICapacityEstimator.cs
        IAdaptiveCapacityEngine.cs
        IAvailabilityCalendarAdapter.cs
        IFocusWindowDetector.cs
      Engines/
        CapacityEstimator.cs
        AdaptiveCapacityEngine.cs
        FocusWindowDetector.cs
      Models/
        CapacitySnapshot.cs
        AvailabilityWindow.cs
        CapacitySignal.cs
      Policies/
        CapacityNormalizationPolicy.cs
        LoadSheddingPolicy.cs
    ML/
      Contracts/
        IIntentClassifier.cs
        IEffortPredictor.cs
        IDifficultyPredictor.cs
        IStudyTimePredictor.cs
        IMlConfidencePolicy.cs
        IMlModelLifecycleManager.cs
      Predictors/
        IntentClassifier.cs
        EffortPredictor.cs
        DifficultyPredictor.cs
        StudyTimePredictor.cs
      Models/
        PredictionResult.cs
        PredictionConfidence.cs
      Lifecycle/
        ModelLifecycleManager.cs
        ModelArtifactLocator.cs
      FeatureEngineering/
        FeatureAssembler.cs
        FeatureNormalizer.cs
    Sync/
      Contracts/
        ISyncQueue.cs
        IChangeTracker.cs
        ISyncConflictResolver.cs
        IPostgresSyncClient.cs
      Queue/
        SyncQueue.cs
      ConflictResolution/
        LastWriteWinsResolver.cs
        DomainAwareConflictResolver.cs
      Clients/
        PostgresSyncClient.cs
      Models/
        SyncChangeSet.cs
        SyncOperation.cs
        SyncStatus.cs
  Application/
    UseCases/
      QuickAddTaskUseCase.cs
      GenerateScheduleUseCase.cs
      AssessRiskUseCase.cs
      BalanceWorkloadUseCase.cs
    Orchestrators/
      AppWorkflowOrchestrator.cs
    DTOs/
      TaskParseRequest.cs
      ScheduleRequest.cs
      RiskRequest.cs
  Infrastructure/
    Persistence/
      SQLite/
        AppDbContext.cs
        Migrations/
      Repositories/
        StudyRepository.cs
        TaskRepository.cs
        RiskSnapshotRepository.cs
      ChangeTracking/
        EntityChangeTracker.cs
    Sync/
      PostgreSQL/
        PostgresSyncContext.cs
        PostgresSyncRepository.cs
      BackgroundJobs/
        SyncBackgroundWorker.cs
  ViewModels/
  Views/
  Services/
    LegacyAdapters/
      DecisionEngineServiceAdapter.cs
      RiskAnalyzerServiceAdapter.cs
      SmartParserAdapter.cs
``` 

---

## 7. Phase plan

## Phase 0 — Baseline audit and dependency shielding

### Goal
Map the current responsibilities and explicitly protect the ML feature path before extraction begins.

### Tasks
- Inventory current services, pipeline stages, repositories, and ViewModel dependencies.
- Document which logic belongs to parsing, scheduling, risk, capacity, ML, and sync.
- Identify the first safe seams inside `DecisionEngineService` and `RiskAnalyzerService`.
- Confirm the current SQLite/data access path and where repository abstraction is missing.
- Freeze ML-facing contracts used by M8 (`SmartParser`, classifier schema, optimizer schema, model storage interfaces) behind adapters if they need to be referenced by refactor work.

### Deliverables
- Module dependency map.
- Extraction order list.
- List of classes that are safe to move first.
- ML compatibility checklist showing which contracts must remain backward-compatible during refactor.

### Exit criteria
- The team can point to a clear home for each major responsibility.
- M8 ML work can continue without depending on unfinished refactor slices.

---

## Phase 1 — Introduce `Core` contracts without changing behavior

### Goal
Create the new module boundaries first, then route existing code through them.

### Tasks
- Create `/Core` folder structure.
- Define interfaces for parsing, scheduling, risk, capacity, ML, and sync.
- Add lightweight adapter classes that forward to current implementations.
- Keep the current public behavior unchanged.
- For ML-related services, introduce adapter interfaces only; do not move the actual M8 implementation files yet.

### Recommended extraction targets
- `IDecisionEngine` remains as an app-facing facade temporarily.
- `IRiskAnalyzer` remains as an app-facing facade temporarily.
- Introduce new internal interfaces for smaller units, such as:
  - `IPriorityEvaluator`
  - `IRawMinutesCalculator`
  - `IStudyTimeSuggestionEngine`
  - `IRiskFactorEvaluator`
  - `IParsingOrchestrator`
  - `ITimeParsingEngine`

### Deliverables
- New contracts under `/Core`.
- Adapter layer from current services to new contracts.
- Compatibility wrappers for any ML-facing calls needed by M8.

### Exit criteria
- Existing views and ViewModels still compile and behave the same.
- No user-visible behavior changes yet.
- ML contracts remain intact and callable through adapters.

---

## Phase 2 — Extract parsing into `Core/Parsing`

### Goal
Make text parsing modular and deterministic, with ML as an optional helper.

### Current code signals
- Existing parser flow and pipeline stage files suggest parsing already has hidden stages.
- ML intent support already exists as a concept, so it should be isolated instead of spread across task creation.

### Tasks
- Split text normalization from intent classification.
- Move time parsing into a dedicated rule-based engine.
- Move task extraction into a separate engine that consumes parsed intent + time + raw text.
- Keep deadline hint resolution separate from base text parsing.
- Ensure parser output includes confidence/source metadata.

### Proposed parsing subcomponents
- `ITextNormalizer`
- `IIntentClassifier`
- `ITimeParsingEngine`
- `ITaskExtractionEngine`
- `IDeadlineHintResolver`
- `IParsingOrchestrator`

### Deliverables
- Parsing engines in `Core/Parsing`.
- Existing parser wrapped by an orchestrator or adapter.

### Exit criteria
- The parser works offline.
- ML improves parsing but is not required.
- ViewModels still talk to a simple application-facing API.

---

## Phase 3 — Extract scheduling into `Core/Scheduling`

### Goal
Turn the current decision engine into a set of small evaluators and one orchestrator.

### Current code signals
`DecisionEngineService` currently does too much:
- priority scoring
- raw minute calculation
- study-time suggestion formatting
- ML-backed prediction

### Tasks
- Split score calculation into focused evaluators.
- Move raw minute estimation into its own engine.
- Move study-time suggestion formatting into a separate domain helper.
- Keep the final schedule decision in a small orchestrator.
- Make sure ML predictions are optional inputs, not the decision source.

### Proposed scheduling subcomponents
- `IPriorityEvaluator`
- `IUrgencyEvaluator`
- `IEffortEvaluator`
- `IDifficultyEvaluator`
- `ITaskTypeEvaluator`
- `IRawMinutesCalculator`
- `IStudyTimeSuggestionEngine`
- `IWorkloadBalancer`
- `ISchedulingOrchestrator`

### Deliverables
- Split implementation of `DecisionEngineService` responsibilities.
- Testable evaluator classes.

### Exit criteria
- No single scheduling class owns the whole flow.
- Every evaluator can be unit tested independently.
- Scheduling output remains deterministic.

---

## Phase 4 — Rehome risk logic into `Core/Risk`

### Goal
Keep risk analysis explainable and componentized.

### Current code signals
`RiskAnalyzerService` already composes risk components, so this phase is mostly about formalizing the domain boundary and making the components first-class.

### Tasks
- Make each risk factor its own evaluator.
- Add a dedicated aggregator for final risk score composition.
- Add an explanation builder so risk output remains interpretable.
- Ensure risk remains reusable by scheduling and analytics.

### Proposed risk subcomponents
- `IDeadlineUrgencyRiskEvaluator`
- `IProgressGapRiskEvaluator`
- `IPerformanceDropRiskEvaluator`
- `IRiskAggregator`
- `IRiskExplanationBuilder`
- `IRiskOrchestrator`

### Deliverables
- Risk engine split into leaf evaluators.
- `RiskAnalyzerService` reduced to a facade or removed.

### Exit criteria
- Risk is no longer a monolithic service.
- Final risk score still matches the existing contract.
- Risk outputs include rationale details.

---

## Phase 5 — Introduce `Core/Capacity`

### Goal
Make capacity a first-class domain so scheduling can adapt to availability and throughput.

### Tasks
- Define capacity metrics such as available minutes, focus windows, overload risk, and recovery time.
- Add a capacity estimator separate from scheduling and risk.
- Add normalization/policy components for translating raw availability into usable planning capacity.
- Feed capacity output into workload balancing and schedule output.

### Proposed capacity subcomponents
- `ICapacityEstimator`
- `IAvailabilityCalendarAdapter`
- `IFocusWindowDetector`
- `IBreakRecoveryEstimator`
- `ICapacityNormalizer`
- `IAdaptiveCapacityEngine`

### Deliverables
- Capacity module under `/Core/Capacity`.
- Scheduling can consume capacity as an input.

### Exit criteria
- Capacity can be evolved without rewriting the scheduler.
- Capacity logic remains separate from UI and storage.

---

## Phase 6 — Formalize `Core/ML` as augmentation only

### Goal
Keep ML.NET behind contracts and confidence policies, but do not disrupt the existing M8 implementation path.

### Tasks
- Move prediction logic behind dedicated ML interfaces.
- Separate model lifecycle/loading from prediction.
- Keep confidence thresholds explicit.
- Ensure models are optional and failure-safe.
- Model outputs should be suggestions or augmentations only.
- Prefer compatibility adapters that call into the existing `Services/ML/*` files until M8 lands.
- Only extract `Core/ML` implementations after the M8 contracts stabilize.

### Proposed ML subcomponents
- `IIntentClassifier`
- `IEffortPredictor`
- `IDifficultyPredictor`
- `IStudyTimePredictor`
- `IMlConfidencePolicy`
- `IMlFeatureAssembler`
- `IMlModelLifecycleManager`

### Deliverables
- ML augmentation layer isolated under `/Core/ML`.
- Compatibility adapters around current `Services/ML/*` implementation.
- No hard cutover of M8 model storage, schema, or prediction entry points.

### Exit criteria
- App still works with no model files.
- ML predictions do not override deterministic logic.
- M8 parser/classifier/optimizer flows continue to run through stable interfaces.

---

## Phase 7 — Lock in SQLite-first repositories under `Infrastructure/Persistence`

### Goal
Protect the offline-first data path with Repository Pattern.

### Current code signals
The current `Data/` folder suggests persistence is already separated, but the architecture should be tightened so domain modules do not touch storage directly.

### Tasks
- Define repository interfaces for the core aggregates used by ViewModels and services.
- Move direct persistence access behind repositories.
- Make SQLite the primary working store.
- Keep migrations and schema logic isolated.
- Ensure domain logic never depends on database implementation details.

### Proposed persistence structure
- `Infrastructure/Persistence/SQLite`
- `Infrastructure/Persistence/Repositories`
- `Infrastructure/Persistence/Migrations`

### Deliverables
- Repository interfaces and SQLite implementations.
- Domain services consume repositories, not tables/contexts.

### Exit criteria
- Offline operation remains the default and primary path.
- Storage implementation can be swapped later without changing core logic.

---

## Phase 8 — Add `Core/Sync` and PostgreSQL as a secondary target

### Goal
Support cloud synchronization without turning PostgreSQL into the working database.

### Tasks
- Add change tracking for local writes.
- Build a sync queue.
- Add conflict resolution rules.
- Implement a PostgreSQL sync client.
- Keep sync background-only and optional.

### Proposed sync subcomponents
- `ISyncQueue`
- `IChangeTracker`
- `ISyncConflictResolver`
- `ISyncProjectionBuilder`
- `IPostgresSyncClient`
- `ISyncScheduler`

### Deliverables
- Sync layer under `/Core/Sync` and/or `Infrastructure/Sync`.
- PostgreSQL adapter for cloud sync only.

### Exit criteria
- App continues to work fully offline.
- Sync failures do not block local usage.
- PostgreSQL is clearly not the authoritative runtime database.

---

## Phase 9 — Feedback loop and adaptive refinement

### Goal
Use user actions to improve future parsing and scheduling without destabilizing the system.

### Tasks
- Record outcome signals after schedule/task actions.
- Feed outcomes into adaptive capacity and ML features.
- Keep feedback writes local-first.
- Use feedback to improve estimates, not to directly mutate core rules.

### Deliverables
- Feedback event models.
- Feedback persistence hooks.
- Future training/suggestion inputs.

### Exit criteria
- The system learns from usage while staying deterministic.

---

## 7. Detailed dependency shield for ML work

This section is the main guardrail that prevents refactor work from harming M8.

### Do not break these current ML surfaces until M8 is complete
- `SmartStudyPlanner/Services/SmartParser.cs` if it currently feeds the text classifier path
- `SmartStudyPlanner/Services/ML/IMLModelManager.cs`
- `SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs`
- `SmartStudyPlanner/Services/ML/StudyTimePredictorService.cs`
- `SmartStudyPlanner/Services/ML/SeedDataGenerator.cs`
- `SmartStudyPlanner/Services/ML/Schema/*`
- Any `ServiceLocator` registrations required by M8

### Safe approach
- Wrap existing ML services with adapters.
- Route new core code to interfaces, not concrete ML service classes.
- Keep schema contracts and artifact paths stable.
- If a type must move, create a forwarding type with the old namespace/API.
- Defer file renames for ML until the parser/classifier/optimizer phases are done.

### Recommended ML refactor rule
- **No direct move** of current M8 files in the same commit/phase that introduces a new Core boundary.
- **No contract shape changes** to ML input/output objects during scheduling/risk extraction.
- **No DI rewiring** that removes the current M8 provider path before replacement adapters are proven.

---

## 8. Recommended decomposition pattern

### Anti-pattern to avoid
One service class that handles:
- parsing
- extraction
- scoring
- risk calculation
- capacity balancing
- ML inference
- persistence orchestration
- sync side effects

### Preferred pattern
Use a small orchestrator plus leaf components:

- **Orchestrator**: coordinates the flow
- **Evaluator**: computes one metric
- **Engine**: transforms one kind of input to one kind of output
- **Builder**: assembles domain output
- **Repository**: persists/retrieves data
- **Adapter**: integrates external systems
- **Policy**: contains thresholds/rules

---

## 9. Priority refactor targets

### High priority
- Split `DecisionEngineService` into scheduling evaluators.
- Split `RiskAnalyzerService` into risk evaluators and aggregator.
- Define explicit parsing orchestration.
- Introduce capacity as a separate concept.
- Establish repository abstractions over local storage.

### Medium priority
- Move ML support into dedicated augmentation services once M8 contracts stabilize.
- Add confidence policies for ML outputs.
- Add feedback collection for schedule outcomes.
- Introduce sync queue and conflict resolution.

### Lower priority
- Optimize cross-module performance.
- Add cloud analytics or reporting.
- Expand PostgreSQL sync coverage.

---

## 10. Refactor acceptance criteria

The refactor is successful when:

- `DecisionEngineService` and `RiskAnalyzerService` are no longer monolithic god objects.
- Each domain module has narrow, testable responsibilities.
- Parsing, scheduling, risk, capacity, ML, and sync are separated cleanly.
- The main pipeline is explicit and deterministic.
- SQLite remains the local working database.
- PostgreSQL exists only behind sync/cloud boundaries.
- MVVM boundaries remain clean: Views stay thin, ViewModels stay orchestration-focused.
- ML.NET augments the system instead of replacing the scheduling core.
- M8 ML parser/classifier/optimizer flows remain stable throughout the refactor.
- Feedback can flow back into parsing and scheduling improvements.

---

## 11. Suggested implementation order

1. Introduce `/Core` contracts and namespaces
2. Add application/infrastructure layer boundaries
3. Extract parsing responsibilities
4. Extract scheduling evaluators from `DecisionEngineService`
5. Extract risk evaluators from `RiskAnalyzerService`
6. Add capacity module
7. Add ML augmentation adapters without moving existing M8 implementation files
8. Add repository abstractions and SQLite-first persistence boundaries
9. Add sync layer with PostgreSQL adapter
10. Add feedback loop hooks
11. Remove leftover monolithic service behavior

---

## 12. Notes for future implementers

- Prefer small, boring, explicit classes over clever abstractions.
- Keep domain rules close to the domain they affect.
- Never let ML become the only path for core decisions.
- Never let sync or cloud concerns leak into local scheduling logic.
- Preserve backward compatibility while migrating.
- Keep ViewModels thin; if a ViewModel becomes a rule engine, it is in the wrong layer.
- Treat the current M8 ML code as protected infrastructure until the ML phase completes.

---

## 13. Immediate next actions

- Finalize the exact `/Core` namespace map.
- Identify the first extraction slice for `DecisionEngineService`.
- Identify the first extraction slice for `RiskAnalyzerService`.
- Define repository interfaces before moving persistence logic.
- Decide which existing pipeline stages should be rehomed first.
- Validate which M8 ML interfaces need adapters before any refactor commit.
