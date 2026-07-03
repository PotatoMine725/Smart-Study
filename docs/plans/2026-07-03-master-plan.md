# Master Plan — 2026-07-03

> **Status:** Proposed. **Scope:** execution decomposition of the frozen architecture decisions
> ([D-A…D-F](2026-07-01-architecture-direction-decisions.md), [D-G…D-J](2026-07-02-architecture-freeze-decisions.md))
> and the canonical backlog ([`system_roadmap.md`](../specs/system_roadmap.md) §A.3).
> The roadmap stays canonical for *ordering* (D-C.1); this plan adds task breakdown, gates and
> acceptance criteria. If the two diverge, update A.3 first.

## Current Architecture (baseline — commit `5e54220`, v1.5.0)

- MVVM + layered, post-monolith: 5-stage Pipeline Orchestrator, DecisionEngine facade,
  `ParsingOrchestrator` (ML-first per-field, ≥0.60 gate), Risk Analyzer, injectable StreakManager.
- **Sync-ready half done:** Guid PKs on every entity; but deletes are hard cascades, `StudyLog.DeviceId`
  is never populated, the study-log write is fire-and-forget (**A6**), and no change tracking exists.
- **Known SOE violation source:** the allocator places tasks deadline-blind (least-loaded day;
  `WorkloadServiceImpl.cs:77-91`) — a task due in 3 days can land on day 9.
- Build/tests green; GitNexus index 3,333 symbols / 7,953 relationships / 127 flows.

## Plan structure

```
Current Architecture (v1.5.0, post-M8)
        │
        ▼
Epic 1 — Sync-Ready Data Model  (foundation — D-B)
        │
        ├── T1.1  D-I metadata block on every synced entity (Rev, ModifiedAtUtc, ModifiedByDeviceId)
        ├── T1.2  Tombstones: soft delete (IsDeleted + DeletedAtUtc) replacing hard cascade deletes
        ├── T1.3  Identity semantics per entity (dedup-cloned-MonHoc class of duplicates)
        ├── T1.4  Last-synced base-snapshot store per peer (3-way-merge substrate)
        ├── T1.5  Fix A6 (fire-and-forget StudyLog write) + populate DeviceId on the write path
        ├── T1.6  B3: finish repository-layer consolidation
        └── T1.7  DECIDE: tombstone retention/purge authority + soft-delete cascade policy

Epic 2 — LAN Sync Engine  (D-A / D-F / D-I)
        │
        ├── T2.1  DECIDE: transport / discovery / trigger model + max offline window
        ├── T2.2  Change enumeration per peer (Rev watermark)
        ├── T2.3  3-way field-level merge; LWW tie-break ModifiedAtUtc → DeviceId (no HLC)
        ├── T2.4  Conflict records (losing side preserved); delete-vs-edit → tombstone wins
        └── T2.5  Convergence & no-data-loss property tests; offline-window handling

Epic 3 — Study Optimization Engine  (D-G / D-H / D-J)
        │
        ├── T3.0  GATE — DECIDE pass accept/commit semantics (freeze record §3; blocks T3.1+)
        ├── T3.1  Constraint Validator seam (deadline / capacity / calendar as hard constraints)
        ├── T3.2  Objective Evaluator seam (w1…w5, quality only; no score buys a violation)
        ├── T3.3  Fix the deadline-blind allocator placement
        ├── T3.4  D-H invariant tests: violations(out) ≤ violations(in); inversion scenario;
        │         infeasible-semester fixtures
        └── T3.5  DECIDE: B5 weight-vector governance (w1…w5 vs priority weights vs WeightOptimizer)

Epic 4 — ML Maturation  (M8-C / M9)
        │
        ├── T4.1  M8-C: retrain Study Time Predictor on real Focus-session telemetry
        ├── T4.2  Difficulty model (fills the D-D per-field gap; heuristic stays fallback)
        └── T4.3  M9: natural-language deadline parsing + cross-semester analytics
```

---

## Epic 1 — Sync-Ready Data Model

**Goal.** Make every synced entity mergeable: D-I metadata, tombstones, identity semantics and a
base-snapshot store, plus the two persistence prerequisites (A6, B3). No sync transport yet.

**Architecture Impact.** Touches every entity (`HocKy`, `MonHoc`, `StudyTask`, `StudyLog`,
`TaskNote`, `TaskReferenceLink`) + `AppDbContext` cascade rules (§3 hard deletes become soft) +
repository layer + EF migration with backfill. Highest-blast-radius epic — run `gitnexus_impact`
per entity before edits.

**Dependencies.** None upstream (first per D-B). T1.7 decisions must close **during** this epic
(they shape the tombstone schema). T1.2 blocks on T1.7's cascade policy.

**Deliverables.** Migrated schema + backfill; soft-delete write paths; awaited, DeviceId-stamping
StudyLog write; consolidated repositories; decision note for T1.7 in `docs/plans/`;
`data-model.md` §§3/8 updated (D-C: descriptive, after code lands).

**Acceptance Criteria.**
- Every synced entity carries `Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`;
  `Rev` increments on every local write and is never compared across devices (L6).
- No hard delete remains on any synced entity; deletes produce tombstones honoring the T1.7 cascade policy.
- A6 closed: study-log write is awaited, failures surfaced; `DeviceId` populated on every new row.
- Existing DBs migrate losslessly (backfill fixture test); build + full test suite green.

**Out of Scope.** Sync transport/merge execution (Epic 2); conflict-record UI; edit history;
SOE-specific tables.

**Risks.** Migration corrupts existing user DBs (mitigate: backup + fixture-based migration tests);
identity-semantics scope creep on T1.3 (bound it to the observed `MonHoc`-clone class);
per-write Rev/timestamp stamping regressing UI responsiveness.

---

## Epic 2 — LAN Sync Engine

**Goal.** Two-way multi-device merge over LAN implementing the frozen D-I mechanics — no cloud,
offline-first preserved.

**Architecture Impact.** New sync module (engine + transport adapter) over the Epic 1 substrate;
conflict-record persistence; minimal sync status/trigger UI. Read-side only for domain logic —
engines/pipeline unchanged.

**Dependencies.** Epic 1 complete (hard). T2.1 decisions (transport/discovery/trigger, max offline
window — feeds tombstone retention). Parallelizable with Epic 3 in principle; A.3 orders it first.

**Deliverables.** Sync engine (enumerate → 3-way diff → merge → conflict records); LAN transport +
discovery; sync trigger UX; decision note for T2.1; property-test suite.

**Acceptance Criteria.**
- Two devices editing different fields of the same row both keep their edits (field-level merge).
- Concurrent same-field edit resolves by LWW (`ModifiedAtUtc` → `DeviceId`), loser lands in a
  conflict record — no silent loss, ever.
- Delete-vs-edit: tombstone wins; losing edit preserved in the conflict record.
- Convergence property test: any two devices reaching the same sync watermark hold identical data.
- Devices offline longer than the retention window are handled per T1.7 policy (defined behavior, tested).

**Out of Scope.** Cloud relay/accounts; >2-device mesh topologies beyond pairwise merge; append-only
edit history (v1 explicitly ships conflict records only); mobile clients.

**Risks.** Clock skew biasing LWW (accepted by design — no HLC unless a concrete failure demands it;
conflict record bounds the damage); transport flakiness on real LANs (keep trigger manual-first);
tombstone purge racing a long-offline device.

---

## Epic 3 — Study Optimization Engine

**Goal.** Evolve the Balancer into the SOE under the frozen guardrails: hard constraints via the
Constraint Validator, quality-only objective `w1…w5`, D-H feasibility invariant, deterministic
ordered pipeline (never a global search).

**Architecture Impact.** New seams `IConstraintValidator` / `IObjectiveEvaluator` (names indicative,
not frozen); stable boundary `Optimize(schedule) → (schedule, report)`; rework of
`WorkloadServiceImpl` placement; behind an `IScheduleOptimizer` strategy seam per roadmap §7.3 —
respecting §13 (no micro-engine fragmentation).

**Dependencies.** **T3.0 is a hard gate** — implementation is blocked until the pass accept/commit
semantics decision closes (freeze record §3, L8). Epic 1 complete (D-B). Epic 2 not required.

**Deliverables.** T3.0 decision note; validator + evaluator implementations with per-seam tests;
deadline-aware allocator; SOE report ("rejected: infeasible" vs "rejected: lower score");
B5 governance note; roadmap §7.3 + architecture docs updated after code lands.

**Acceptance Criteria.**
- Inversion test: a near-deadline task is never displaced past its deadline by a quality-improving
  rearrangement (D-G).
- Property test on infeasible fixtures: `violations(out) ≤ violations(in)` (count, then overdue
  minutes) on every input (D-H).
- No objective score can purchase a constraint violation (D-J); validator and evaluator tested
  independently.
- Same input ⇒ same output (deterministic); explanations distinguish infeasible-reject from
  score-reject.

**Out of Scope.** Global search / metaheuristics / argmax over the objective (D-E core);
`w6·DeadlineUrgency` (dropped by D-G); the proposal's six sub-engines (§13 tension — phase behind
one seam); autonomous ML scheduling (§6).

**Risks.** T3.0 stalls the epic (mitigate: schedule the decision session first; L8 documents the
defect space); pass semantics choice reintroducing local-optimum or no-op-iteration defects;
allocator rework destabilizing shipped Balancer behavior (characterization tests before rework).

---

## Epic 4 — ML Maturation

**Goal.** Replace synthetic training data with real telemetry (M8-C) and close the two D-D per-field
gaps (difficulty model, M9 NL deadline parsing) — within roadmap §9's 1–2-model budget.

**Architecture Impact.** Confined to `Services/ML/*` + parser pipeline; per-field confidence gating
already in place (`IntentClassifierAdapter`, ≥0.60). No engine/scheduling coupling (§13).

**Dependencies.** M8-C needs enough real Focus-session telemetry (Epic 1's A6 fix improves capture
fidelity). Independent of Epics 2–3; lowest priority (A.3 items 4–5).

**Deliverables.** Retrained Study Time Predictor + eval report vs synthetic baseline; difficulty
classifier wired per-field with heuristic fallback; M9 deadline parser; cross-semester analytics.

**Acceptance Criteria.**
- Retrained predictor beats the synthetic-seed model on held-out real telemetry; formula fallback
  intact when the model is absent (guardrail A.5-5).
- Difficulty/deadline predictions obey the same per-field ≥0.60 gate; below it, heuristic wins.
- Parser isolation preserved: parser never schedules or allocates (§9.1).

**Out of Scope.** Deep learning; a third+ ML model; ML-driven schedule generation; cloud model storage.

**Risks.** Insufficient real telemetry volume (fallback: extend maturation window; keep M8-C last);
model regressions vs heuristic baseline (confidence gate + A/B eval before wiring).

---

## Technical Debt (tracked, not scheduled)

- `ServiceLocator.cs` residual usage → full constructor injection (roadmap §12 P3).
- `Services/` semi-god layer split + engine naming (`WorkloadServiceImpl` → Balancer/SOE naming) —
  fold the naming into Epic 3's rework rather than a standalone pass.
- Pipeline rehome `Services/Pipeline/*` → `Application/UseCases/*` (deferred, independent plan — A.4).
- `System.Drawing.Common` NU1904 vulnerability (~30 min, independent).
- `ParseSource.MlOverridden` declared-but-unused enum value.
- Date-fragile `DecisionEngineTests` (use `_clock.Now`, not `DateTime.Now`).

## Parking Lot

- Competency-gap calculation (N5 — net-new, zero code today; aspirational until scoped).
- Append-only edit history on top of conflict records (v2+ candidate).
- Cloud model storage via `IModelStorageProvider`; mobile/hybrid clients (revisit after LAN sync).
- Async pipeline end-to-end; `Core/Capacity`.
- Hybrid Logical Clock — only if a concrete LWW failure demands it (D-I).

## Definition of Done (every epic)

1. `gitnexus_impact` before editing any symbol; HIGH/CRITICAL reported before proceeding.
2. `gitnexus_detect_changes` before every commit.
3. `dotnet build SmartStudyPlanner.slnx` + `dotnet test --no-build` green.
4. New behavior covered by the epic's acceptance-criteria tests (property tests where stated).
5. Architecture docs (`docs/architecture/*`) updated **after** code lands (D-C: descriptive);
   roadmap A.3 status updated (D-C.1).
6. Open decisions closed in a `docs/plans/YYYY-MM-DD-*.md` decision note before dependent tasks start.
