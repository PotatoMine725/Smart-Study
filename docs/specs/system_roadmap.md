# Smart Study Planner — System Roadmap & Architecture Direction

---

# 1. Current Project State

The project has successfully transitioned away from a monolithic “god object” architecture into a more modular system.

Current strengths:

* pipeline-oriented structure
* strategy-based logic separation
* parser orchestration
* telemetry awareness
* testing structure
* adaptive planning direction

The project is no longer a CRUD-style student application.

It is evolving into:

> A modular local-first intelligent planning system.

---

# 2. Current Architectural Assessment

Current architecture style:

> MVVM + Layered Architecture + Domain-driven modularization

The project is currently in a:

> “Post-monolith refactor phase”

Meaning:

* responsibilities are being separated
* domain logic is emerging
* orchestration layers are forming
* engine boundaries are not yet fully stabilized

---

# 3. Current Architectural Risks

---

## 3.1 Semi-God Service Layer

The `Services/` folder still contains:

* orchestration
* business logic
* engine implementation
* adapters

This creates:

* unclear boundaries
* coupling
* refactor instability

---

## 3.2 Naming Inconsistency

Current naming:

```text
DecisionEngineService
WorkloadServiceImpl
SmartParser
```

These names suggest:

* generic services
* infrastructure-style components

While they are actually:

* domain engines
* orchestration modules

---

## 3.3 Service Locator Technical Debt

Current architecture still depends on:

```text
ServiceLocator.cs
```

This creates:

* hidden dependencies
* difficult testing
* weak DI boundaries

---

# 4. Recommended Architecture Direction

The system should evolve toward:

> Modular Domain-Driven MVVM Architecture

with:

* heuristic orchestration
* adaptive ML support
* pipeline-based processing
* strict engine isolation

---

# 5. Recommended Folder Structure

```text
SmartStudyPlanner
│
├── Core
│   ├── Engines
│   │   ├── Decision
│   │   ├── Planner
│   │   ├── Balancer
│   │   ├── Parser
│   │   └── Risk
│   │
│   ├── Rules
│   ├── Strategies
│   ├── Pipelines
│   ├── Algorithms
│   ├── Contracts
│   └── Models
│
├── Application
│   ├── Services
│   ├── DTOs
│   ├── UseCases
│   └── Interfaces
│
├── Infrastructure
│   ├── Persistence
│   ├── Repositories
│   ├── Logging
│   ├── Telemetry
│   ├── Notifications
│   └── Configuration
│
├── Presentation
│   ├── Views
│   ├── ViewModels
│   ├── Converters
│   └── Themes
│
└── Tests
```

---

# 6. Core System Philosophy

The planner system MUST remain:

* deterministic
* explainable
* testable
* heuristic-first

ML components should:

* support
* predict
* assist adaptation

ML components MUST NOT:

* autonomously generate schedules
* replace planner logic
* bypass heuristic validation

---

# 7. Stable Core Engines

These engines should become the long-term stable system backbone.

---

## 7.1 Decision Engine

Responsibility:

* priority scoring
* urgency evaluation
* competency gap calculation

Output:

```text
PriorityScore
```

Constraints:

* deterministic
* no ML dependency
* pure logic only

---

## 7.2 Planner Engine

Responsibility:

* orchestrate scheduling flow
* coordinate engines
* generate plans

This is the central orchestrator.

---

## 7.3 Balancer Engine

Responsibility:

* workload distribution
* overload prevention
* realistic scheduling

Constraints:

* max hours/day
* avoid burnout
* avoid repetition

---

## 7.4 Risk Analyzer

Responsibility:

* detect procrastination
* detect overload
* detect deadline risk

Outputs:

```text
RiskLevel
RiskReason
```

---

## 7.5 Adaptive Rule Engine

Responsibility:

* adjust weights
* trigger re-planning
* apply adaptive heuristics

Uses:

* telemetry
* progress tracking
* ML advisory signals

BUT remains:

* deterministic
* rule-driven

---

# 8. Recommended ML Strategy

---

## IMPORTANT PRINCIPLE

The project is NOT an AI scheduling system.

It is:

> A heuristic scheduling system augmented by lightweight adaptive ML.

---

# 9. Recommended ML Submodels

Maximum:

* 1–2 ML models

Avoid overengineering.

---

## 9.1 Smart Parser (Primary ML Component)

This is the ONLY ML-first subsystem.

Purpose:

* parse natural language deadlines
* infer scheduling intent
* resolve temporal expressions

Examples:

* “finish report before next Friday”
* “study OOP after midterm”
* “math exam in 2 weeks”

---

### Parser Pipeline

```text
Raw Input
↓
Tokenizer
↓
Intent Classification
↓
Entity Extraction
↓
Temporal Resolution
↓
Confidence Scoring
↓
Structured Output
```

---

### Output

```text
TaskName
Deadline
EstimatedUrgency
ConfidenceScore
```

---

### Parser Isolation Rule

The parser MUST NOT:

* schedule tasks
* allocate workload
* modify planner logic

Parser responsibilities end after:

* extraction
* inference
* confidence estimation

---

## 9.2 Performance Predictor (Optional)

Purpose:

* predict schedule success probability
* estimate workload tolerance
* detect failure likelihood

This model only provides:

* advisory signals
* adaptive hints

It MUST NOT:

* directly modify plans

---

# 10. ML Confidence & Fallback Policy

---

## ML Outputs Are Advisory

All ML outputs MUST include:

* confidence score
* uncertainty estimation

---

## Confidence Rules

### High Confidence

System MAY apply suggestion automatically.

---

### Medium Confidence

System SHOULD ask for user confirmation.

---

### Low Confidence

System MUST fallback to:

* heuristic logic
* safe defaults
* user input

---

## Fallback Pipeline

```text
ML Output
↓
Confidence Validation
↓
If valid:
    Apply Suggestion
Else:
    Fallback → Heuristic Engine
↓
If still ambiguous:
    Ask User
```

---

# 11. Telemetry & Analytics Direction

The system should track:

* study consistency
* completion rate
* parser correction frequency
* workload deviation
* user overrides

Telemetry should support:

* adaptive heuristics
* future ML improvements
* behavior analysis

---

# 12. Immediate Refactor Priorities

---

## PRIORITY 1 — Freeze Core Boundaries

Stabilize:

* Decision Engine
* Planner Engine
* Balancer Engine
* Risk Analyzer

Before adding more features.

---

## PRIORITY 2 — Split Orchestration vs Logic

Avoid:

```text
DecisionEngineService
    handles everything
```

Move toward:

```text
DecisionEngine
PriorityCalculator
UrgencyRule
```

---

## PRIORITY 3 — Replace Service Locator

Migrate to:

```csharp
Microsoft.Extensions.DependencyInjection
```

Use:

* constructor injection
* interface-based contracts

---

## PRIORITY 4 — Stabilize Parser Pipeline

Finalize:

```text
Tokenizer
↓
Intent Classifier
↓
Entity Extractor
↓
Temporal Resolver
↓
Confidence Validator
```

Before:

* ML retraining
* NLP optimization

---

# 13. Anti-Overengineering Rules

DO NOT:

* introduce deep learning
* create autonomous planners
* tightly couple ML to scheduling core
* fragment engines excessively
* create unnecessary micro-engines

The project should remain:

* local-first
* maintainable
* explainable
* deterministic

---

# 14. Recommended Development Roadmap

---

## v1.0

* stable heuristic planner
* engine separation
* balancing logic
* parser pipeline skeleton

---

## v1.2

* adaptive rule engine
* telemetry integration
* parser ML integration

---

## v1.5

* workload prediction
* performance estimation
* smarter adaptive weighting

---

## v2

* advanced analytics
* optional cloud sync
* enhanced recommendation system

---

# 15. Final System Identity

This system should behave like:

> A deterministic planning engine assisted by adaptive intelligence.

NOT:

> An autonomous AI scheduler.

The planner remains:

* deterministic
* explainable
* stable

The ML layer improves:

* usability
* adaptability
* prediction quality

---

# 16. Final Engineering Principle

```text
Plan → Execute → Measure → Adapt → Re-plan
```

This feedback loop defines the entire system architecture.

---
