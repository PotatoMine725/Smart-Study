# Smart Study Planner — Agent System Directive

---

# 1. Core System Philosophy

Smart Study Planner is a **deterministic heuristic-first adaptive planning system**.

The system is NOT:

* a generic todo app
* a black-box AI scheduler
* a fully ML-driven planner
* an autonomous agentic system

The system IS:

* a rule-based orchestration engine
* behavior-aware
* explainable
* locally adaptive
* modular and testable

---

# 2. Core Architectural Principle

## Heuristic Core Dominance

The main scheduling pipeline MUST remain heuristic-driven.

Core scheduling decisions MUST be:

* deterministic
* explainable
* reproducible
* unit-testable

ML components MUST NOT directly generate schedules.

ML components ONLY:

* assist
* predict
* adapt weights
* improve parsing
* estimate user behavior

---

# 3. Engine Hierarchy

---

## Tier 1 — Core Heuristic Engines (Stable Core)

These engines form the immutable planning backbone.

---

### 3.1 Decision Engine

### Responsibility

* calculate priority score
* evaluate urgency
* evaluate competency gap
* evaluate progress gap

### Output

```text
PriorityScore
```

### Constraints

* deterministic only
* no ML dependency
* no DB access
* pure logic only

---

### 3.2 Planner Engine

### Responsibility

* orchestrate scheduling flow
* coordinate all engines
* generate study plan

This is the central orchestrator.

### Constraints

* no business logic duplication
* no direct ML scheduling
* no UI dependency

---

### 3.3 Balancer Engine

### Responsibility

* distribute workload
* avoid overload
* enforce scheduling constraints

### Constraints

* max hours/day
* avoid burnout
* avoid excessive repetition
* preserve realistic schedules

### Algorithm Style

* greedy balancing
* constraint-aware allocation

---

### 3.4 Risk Analyzer

### Responsibility

* detect deadline risk
* detect procrastination risk
* detect overload patterns

### Output

```text
RiskLevel
RiskReason
```

---

### 3.5 Adaptive Rule Engine

### Responsibility

* adjust weights
* trigger re-planning
* adapt workload

This engine uses:

* heuristics
* telemetry
* ML advisory signals

BUT remains:

* rule-driven
* deterministic

---

# 4. ML Architecture Rules

---

## ML is Support Layer Only

ML MUST NOT:

* replace planner engine
* directly create schedules
* bypass heuristic validation
* silently mutate planner logic

ML SHOULD:

* estimate
* classify
* improve prediction
* support adaptation
* improve NLP parsing

---

# 5. Allowed ML Models

The system should initially contain ONLY 2 ML submodels maximum.

---

## 5.1 Smart Parser Model (PRIMARY ML MODEL)

This is the ONLY ML-first component.

---

### Purpose

Convert natural language into structured scheduling data.

Examples:

* "finish report before next Friday"
* "study OOP after midterm"
* "math exam in 2 weeks"

---

### Responsibilities

* intent extraction
* date extraction
* context resolution
* ambiguity handling
* deadline inference

---

### Pipeline

```text
Raw Input
↓
Tokenization
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

### ML Ownership

The Smart Parser is ML-first.

Heuristics MAY validate parser output,
but parsing intelligence should primarily rely on ML/NLP.

---

### Isolation Rule

The Smart Parser MUST remain isolated from:

* scheduling logic
* balancing logic
* risk calculation

Parser responsibilities end after:

* structured extraction
* confidence generation

The planner decides how to use parser output.

---

## 5.2 User Performance Predictor (Optional)

### Purpose

* predict schedule success probability
* estimate workload tolerance
* detect likely failure patterns

### Input Examples

* completion rate
* study consistency
* workload density
* past performance

### Output

```text
SuccessProbability
RecommendedLoad
```

---

### Constraints

This model MUST NOT:

* directly modify schedules
* bypass heuristic logic

Instead, it only provides advisory signals to:

* Adaptive Engine
* Risk Analyzer

---

# 6. ML Confidence & Fallback Policy

---

## ML Outputs Are Advisory

ML outputs are advisory, not authoritative.

All ML-generated outputs MUST include:

* confidence score
* uncertainty estimation

---

## Confidence-based Execution Rules

---

### High Confidence

If confidence ≥ threshold:

* system MAY apply recommendation automatically

Examples:

* parser confidently extracts valid deadline
* workload prediction is stable

---

### Medium Confidence

If confidence is uncertain:

* system SHOULD ask for user confirmation

Examples:

* ambiguous date interpretation
* unclear task urgency
* uncertain workload estimation

---

### Low Confidence

If confidence is below threshold:

* system MUST fallback to:

  * heuristic logic
  * user input
  * safe default behavior

---

## Fallback Priority Order

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

## Critical Rule

ML MUST NEVER:

* silently override heuristic logic
* create schedules autonomously
* bypass planner constraints
* mutate weights without validation

---

## Reliability Principle

System priorities:

1. Stability
2. Explainability
3. Predictability
4. Adaptability

Adaptability must NEVER compromise deterministic behavior.

---

# 7. Engine Stability Rules

The following engines should remain stable and require minimal refactoring:

* Decision Engine
* Planner Engine
* Balancer Engine
* Risk Analyzer

These engines should:

* evolve incrementally
* avoid architectural rewrites
* avoid ML dependency

---

# 8. Telemetry Requirements

The system should track:

* study consistency
* completion rate
* schedule deviation
* parser correction frequency
* user override behavior

Telemetry is used for:

* adaptive heuristics
* future ML training
* behavior analysis

---

# 9. Anti-Overengineering Constraints

DO NOT:

* introduce deep learning
* create autonomous AI scheduling
* build self-modifying planners
* tightly couple ML with planner core
* over-fragment engines unnecessarily

The system should remain:

* lightweight
* local-first
* maintainable
* explainable
* testable

---

# 10. Recommended Engine Count

---

## Heuristic Engines

Recommended:

* 5–6 stable engines maximum

Examples:

* Decision Engine
* Planner Engine
* Balancer Engine
* Risk Analyzer
* Adaptive Rule Engine

---

## ML Submodels

Recommended:

* 1–2 models maximum

Priority:

1. Smart Parser
2. Performance Predictor

---

# 11. Final System Identity

Smart Study Planner is fundamentally:

> A heuristic scheduling system augmented by lightweight adaptive ML.

NOT:

> An AI-generated planning system.

The planner remains deterministic.
The ML layer improves adaptability and usability.

---

# 12. Core Insight

```text
Plan → Execute → Measure → Adapt → Re-plan
```

---

# 13. Final Engineering Principle

The system should behave like:

* a deterministic planner
* assisted by intelligent prediction
* not an autonomous AI agent

---
