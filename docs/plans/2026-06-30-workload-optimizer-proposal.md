# Study Optimization Engine Design Proposal

> **⚠️ Superseded in part (2026-07-02) — read first.** This proposal is accepted as direction, with
> three amendments frozen in
> [`2026-07-02-architecture-freeze-decisions.md`](2026-07-02-architecture-freeze-decisions.md):
>
> 1. §6's objective **drops `w6·DeadlineUrgency`** (**D-G**): deadline feasibility is a **hard
>    constraint** owned by the Constraint Evaluator; the objective scores quality only (`w1…w5`).
> 2. §5.6's "constraints must never be violated" is refined to the **feasibility invariant** (**D-H**):
>    the engine *preserves or improves* feasibility (`violations(out) ≤ violations(in)`) — absolute
>    feasibility is undefined on already-infeasible inputs.
> 3. §6's "the planner should search for the highest overall score" is **not** the compute model
>    (**D-E**): deterministic ordered pipeline, never a global search. The pass accept/commit
>    semantics remain **OPEN** (freeze record §3).
>
> Scope note: the six sub-engines are in tension with roadmap §13 — see
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §7.3. Body preserved unchanged below.

> Status: Architecture Refinement Proposal
>
> Priority: High
>
> Affected Components:
> - Planner Engine
> - Decision Engine
> - Study Optimization Engine (formerly Workload Balancer)
> - Risk Analyzer
> - Adaptive Rule Engine

---

# 1. Background

The original Workload Balancer was designed to distribute study workload evenly across available days.

This solved the previous imbalance issue where some days remained underutilized despite available study capacity.

However, balancing study time alone does not necessarily produce an effective learning schedule.

The planner should optimize for **human learning efficiency**, not simply mathematical fairness.

---

# 2. Problem Statement

Current optimization objective:

```
Minimize Daily Workload Variance
```

While mathematically correct, this ignores several important cognitive factors.

Examples:

- Context switching
- Deep work sessions
- Subject continuity
- Task fragmentation
- Mental fatigue

As a result, the planner may produce schedules that appear balanced but are less effective in real-world study scenarios.

---

# 3. Design Philosophy

The planner should behave more like an experienced academic mentor than a mathematical scheduler.

Instead of asking:

> "How can I distribute minutes evenly?"

it should ask:

> "How can I maximize the student's learning efficiency while respecting all constraints?"

Workload balancing therefore becomes only **one heuristic** inside a larger optimization process.

---

# 4. Architecture Refinement

The existing Workload Balancer should evolve into a higher-level component:

> Study Optimization Engine

Architecture:

```text
Planner Engine
│
├── Decision Engine
│      ├── Priority Scoring
│      ├── Deadline Evaluation
│      └── Competency Evaluation
│
└── Study Optimization Engine
       ├── Load Balancer
       ├── Session Optimizer
       ├── Context Switch Analyzer
       ├── Fragmentation Analyzer
       ├── Fatigue Evaluator
       └── Constraint Evaluator
```

Planner Engine remains responsible for orchestration.

Study Optimization Engine is responsible for transforming an initial schedule into a practical, cognitively efficient schedule.

---

# 5. Responsibilities

## Planner Engine

Responsible for

- orchestrating the planning pipeline
- coordinating Decision Engine
- coordinating Study Optimization Engine
- producing the final schedule

---

## Decision Engine

Responsible for

- priority scoring
- urgency calculation
- competency evaluation
- progress evaluation

Output

```
PriorityScore
```

Decision Engine must remain deterministic.

It should never allocate sessions.

---

## Study Optimization Engine

Transforms the initial schedule into a schedule optimized for real-world learning.

It combines multiple heuristics rather than relying on workload balancing alone.

---

### 5.1 Load Balancer

Goal

Balance study workload while respecting capacity constraints.

Metric

Daily workload variance.

---

### 5.2 Session Optimizer

Goal

Improve study quality.

Responsibilities

- preserve deep work sessions
- merge fragmented sessions
- avoid ineffective short sessions
- recommend optimal study duration

---

### 5.3 Context Switch Analyzer

Goal

Reduce unnecessary switching between subjects.

Example

Avoid

```
OS

↓

Database

↓

OS
```

Prefer

```
OS

↓

OS

↓

Database
```

Possible metrics

- subjects per day
- context switches
- session continuity

---

### 5.4 Fragmentation Analyzer

Goal

Reduce unnecessary task splitting.

Instead of

```
Assignment

↓

Part 1

↓

Part 2

↓

Part 3
```

prefer

```
Assignment

↓

Continuous Session
```

unless constrained by deadlines or workload.

---

### 5.5 Fatigue Evaluator

Goal

Estimate mental workload.

Future heuristics may include

- task type
- task difficulty
- historical fatigue
- user behavior

Mental fatigue should not be treated the same as study duration.

---

### 5.6 Constraint Evaluator

Highest priority component.

Responsible for enforcing

- deadlines
- maximum workload
- unavailable dates
- exams
- user preferences

Constraints must never be violated.

---

# 6. New Optimization Objective

The planner should no longer optimize only

```
Daily Workload Variance
```

Instead it should maximize

```
LearningEfficiencyScore
```

Possible heuristic

```
Score =
w1 * LoadBalance
+w2 * ContextContinuity
+w3 * SessionQuality
+w4 * FatiguePenalty
+w5 * FragmentationPenalty
+w6 * DeadlineUrgency
```

The planner should search for the highest overall score instead of the flattest workload distribution.

---

# 7. Scheduling Strategy

The planner should implement

> Soft Balancing

instead of strict equal distribution.

Example

Target

```
45 minutes/day
```

Acceptable range

```
40–60 minutes/day
```

Deviation within this range is acceptable if it significantly improves

- deep work preservation
- context continuity
- reduced fragmentation
- session quality

---

# 8. Scheduling Pipeline

```
Priority Calculation
        │
        ▼
Constraint Evaluation
        │
        ▼
Initial Allocation
        │
        ▼
Study Optimization Engine
        │
        ├── Load Balancer
        ├── Session Optimization
        ├── Context Optimization
        ├── Fragmentation Reduction
        ├── Fatigue Evaluation
        └── Constraint Validation
        │
        ▼
Schedule Validation
        │
        ▼
Final Schedule
```

Balancing becomes only one optimization stage.

---

# 9. Suggested Project Structure

```
Core
│
├── Engines
│   ├── Planner
│   ├── Decision
│   ├── Parser
│   ├── Risk
│   ├── Adaptive
│   └── StudyOptimization
│       ├── LoadBalancer
│       ├── SessionOptimizer
│       ├── ContextSwitchAnalyzer
│       ├── FragmentationAnalyzer
│       ├── FatigueEvaluator
│       └── ConstraintEvaluator
│
├── Heuristics
│   ├── Priority
│   ├── Optimization
│   ├── Context
│   ├── Session
│   ├── Fatigue
│   └── Fragmentation
│
├── Rules
│
├── Constraints
│
├── Pipelines
│
└── Models
```

This organization separates

- orchestration
- optimization
- heuristics
- constraints
- domain rules

making future improvements easier without modifying Planner Engine.

---

# 10. Engineering Principles

The planner should always prioritize

1. Hard Constraints
2. Learning Efficiency
3. Workload Balance
4. User Convenience

Workload balance is **not** the primary objective.

It is only one optimization heuristic.

---

# 11. Long-term Vision

The project should not evolve into a better workload balancer.

It should evolve into a

> Study Optimization System

that assists students by

- respecting deadlines
- balancing workload
- preserving focus
- minimizing context switching
- encouraging deep work
- adapting to user behavior

instead of merely distributing study minutes evenly.

---

# 12. Expected Benefits

This refinement provides:

- Better separation of responsibilities
- Easier heuristic expansion
- Improved maintainability
- More realistic scheduling
- Reduced future refactoring
- Architecture that better reflects the project vision

Study Optimization Engine becomes the long-term extension point for future scheduling intelligence while Planner Engine remains stable and focused on orchestration.