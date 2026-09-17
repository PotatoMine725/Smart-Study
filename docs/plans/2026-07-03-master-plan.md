# Master Plan — 2026-07-03 (v2, Execution Ready)

> **Status: Execution Ready** — v2 after the 2026-07-03 execution-planning review (v1 proposed
> same day). **Architecture unchanged:** decisions
> [D-A…D-F](2026-07-01-architecture-direction-decisions.md) and
> [D-G…D-J](2026-07-02-architecture-freeze-decisions.md) remain frozen; this revision refines
> **execution only** — milestones, order, decision gates, success metrics, DoR, critical path.
> The roadmap ([`system_roadmap.md`](../specs/system_roadmap.md) §A.3) stays canonical for
> ordering (D-C.1) and was updated in the same pass to match the v2 execution order.
> v1→v2 change log: §"Changes in v2" at the end.

## Current Architecture (baseline — commit `5e54220`, v1.5.0)

- MVVM + layered, post-monolith: 5-stage Pipeline Orchestrator, DecisionEngine facade,
  `ParsingOrchestrator` (ML-first per-field, ≥0.60 gate), Risk Analyzer, injectable StreakManager.
- **Sync-ready half done:** Guid PKs on every entity; but deletes are hard cascades, `StudyLog.DeviceId`
  is never populated, the study-log write is fire-and-forget (**A6**), and no change tracking exists.
- **Known SOE violation source:** the allocator places tasks deadline-blind (least-loaded day;
  `WorkloadServiceImpl.cs:77-91`) — a task due in 3 days can land on day 9.
- **Schema mechanism (found in the v2 review):** `Database.EnsureCreated()` only (`App.xaml.cs:28`),
  **no EF migrations** — a no-op on existing DBs. Existing databases are patched by the ad-hoc
  `TelemetrySchema.EnsureTables` seam (M8 precedent). Any schema change to alpha DBs needs an
  upgrade mechanism first (→ T1.8).
- Build/tests green; GitNexus index 3,333 symbols / 7,953 relationships / 127 flows.

## Execution order

Epic IDs are stable (E1–E4 as in v1); **execution order is E1 → E3 → E2 → E4**:

- **D-B requires only the data model first.** Its consequences state the build queue verbatim:
  *"sync-ready data model → (debt: B3/A6) → SOE on top"* and *"does **not** commit to building LAN
  sync itself next."* v1's E2-before-E3 came from A.3's listing, not from a frozen decision.
- **The closed alpha is single-desktop.** LAN sync delivers zero alpha-visible value; the SOE (and the
  deadline-blind-allocator fix inside it) is the alpha's flagship correctness improvement.
- **Lower risk:** the SOE exercises the migrated schema under real usage before the far riskier
  merge engine builds on it; the transport/offline-window decisions (G4) get made with real data.

```
Current Architecture (v1.5.0, post-M8)
        │
        ▼
Epic 1 — Sync-Ready Data Model  (foundation — D-B)
        │
        ├── M1.1  Single write path
        │         ├── T1.6  B3: finish repository-layer consolidation (first — one write path
        │         │         to instrument, not several)
        │         └── T1.5  Fix A6 (awaited StudyLog write) + populate DeviceId
        ├── M1.2  Schema upgrade + D-I metadata + tombstones   [gate G1]
        │         ├── T1.8  Schema-upgrade mechanism (extend the TelemetrySchema patch-seam
        │         │         pattern or adopt EF migrations — decided at DoR; new in v2)
        │         ├── T1.1  D-I metadata block on every synced entity (Rev, ModifiedAtUtc,
        │         │         ModifiedByDeviceId) — one upgrade step together with:
        │         └── T1.2  Tombstones: soft delete (IsDeleted + DeletedAtUtc) replacing
        │                   hard cascade deletes (per the G1 cascade policy)
        └── M1.3  T1.3  Identity semantics per entity (dedup-cloned-MonHoc class)

Epic 3 — Study Optimization Engine  (D-G / D-H / D-J)   ← executes second
        │
        ├── M3.0  Corpus & baselines  (parallel-safe — may run during Epic 1)
        │         ├── T3.6  Benchmark corpus (incl. infeasible semesters) + current-allocator
        │         │         baseline capture + characterization tests (new in v2)
        │         └── T3.0  GATE G2 — close pass accept/commit semantics (freeze §3)
        ├── M3.1  Frozen-semantics stages (pure, independently tested)
        │         ├── T3.1  Constraint Validator seam (deadline / capacity / calendar)
        │         └── T3.2  Objective Evaluator seam (w1…w5, quality only)
        └── M3.2  Optimizer assembly  [gates G2, G3]
                  ├── T3.3  Deadline-aware allocator rework + pass loop per G2 outcome
                  ├── T3.4  D-H invariant + inversion tests on the T3.6 corpus
                  ├── T3.7  OptimizerRunLog telemetry (M8 telemetry pattern; new in v2)
                  └── T3.5  GATE G3 — B5 weight-vector governance (before ship)

Epic 2 — LAN Sync Engine  (D-A / D-F / D-I)   ← executes third
        │
        ├── M2.1  Merge engine, no network (pure, fixture peers)
        │         ├── T1.4  Last-synced base-snapshot store (moved from E1 — co-designed
        │         │         with its only consumer, the 3-way diff)
        │         ├── T2.2  Change enumeration per peer (Rev watermark)
        │         ├── T2.3  3-way field-level merge; LWW ModifiedAtUtc → DeviceId (no HLC)
        │         ├── T2.4  Conflict records; delete-vs-edit → tombstone wins
        │         └── T2.5  Convergence & no-data-loss property suite (starts here)
        ├── M2.2  Transport & retention  [gate G4]
        │         ├── T2.1  GATE G4 — transport / discovery / trigger + max offline window
        │         └── T2.6  Tombstone retention/purge job (moved from E1; default until
        │                   G4 closes: never purge — a single-device alpha never purges)
        └── M2.3  Two-real-machines end-to-end + sync status/trigger UX

Epic 4 — ML Maturation  (M8-C / M9)   ← executes last; telemetry accrues from M1.1
        │
        ├── T4.1  M8-C: retrain Study Time Predictor on real Focus-session telemetry
        ├── T4.2  Difficulty model (D-D per-field gap; heuristic stays fallback)
        └── T4.3  M9: natural-language deadline parsing + cross-semester analytics
```

---

## Decision gates (front-load — all are discussions, not code)

| Gate | Decision | Blocks | When to close |
|---|---|---|---|
| **G1** | Soft-delete cascade policy (parent with live children) | M1.2 | **Now** (during M1.1) |
| **G2** | SOE pass accept/commit semantics (freeze record §3, L8) | M3.2 | **Now** (during Epic 1) |
| **G3** | B5 weight-vector governance (`w1…w5` vs priority weights vs WeightOptimizer) | M3.2 ship | During M3.1 |
| **G4** | LAN transport / discovery / trigger + max offline window (feeds retention) | M2.2 | During Epic 3 / M2.1 |

Each closes as a `docs/plans/YYYY-MM-DD-*.md` decision note (DoD-6). Tombstone **retention**
(v1's T1.7 second half) is deliberately deferred to G4: retention length depends on the max
offline window, and the safe default (no purge) is correct for the whole single-device alpha.

---

## Epic 1 — Sync-Ready Data Model

> **Status (2026-07-20): Released.** M1.1/M1.2/M1.3 all merged (`a3a0a3d`); the B4 release-gate
> reopen (a latent M1.2 FK regression) was fixed — R1/R2, merged `37f9678` — and the owner signed
> off **Epic 1 = Released (2026-07-20)** per
> [`2026-07-11-epic-1-closure-gate.md`](2026-07-11-epic-1-closure-gate.md) (conditions
> C1–C3) and the Phase 1 execution plan (`2026-07-12-epic1-closure-phase1-execution.md`, archived
> 2026-07-26 → `legacy/Archived plans/`, local-only). This
> section is frozen planning content (D-P6) — live status lives in
> [`system_roadmap.md`](../specs/system_roadmap.md) §A.2/A.3 and
> [`active/README.md`](../active/README.md).

**Goal.** Make every synced entity mergeable — D-I metadata, tombstones, identity semantics —
plus the two persistence prerequisites (A6, B3). No sync transport, no snapshot store (→ M2.1).

**Architecture Impact.** Touches every entity (`HocKy`, `MonHoc`, `StudyTask`, `StudyLog`,
`TaskNote`, `TaskReferenceLink`) + `AppDbContext` cascade rules + repository layer + the schema
upgrade path for existing DBs. Highest-blast-radius epic — `gitnexus_impact` per entity before edits.

**Dependencies.** None upstream (first per D-B). G1 blocks M1.2. M1.1 → M1.2 → M1.3 sequential.

**Deliverables.** Consolidated repositories; awaited, DeviceId-stamping StudyLog write; schema-upgrade
mechanism (T1.8); upgraded schema with metadata + tombstones in one step; migration report
(pre/post row counts + per-table checksums); G1 decision note; `data-model.md` §§3/8 updated after
code lands (D-C).

**Acceptance Criteria.**
- Every synced entity carries `Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`;
  `Rev` increments on every local write, never compared across devices (L6).
- No hard delete remains on any synced entity; deletes produce tombstones honoring the G1 policy.
- A6 closed: study-log write awaited, failures surfaced; `DeviceId` populated on every new row.
- An existing pre-upgrade DB (fixture) upgrades in place and the app runs against it; suite green.

**Success Metrics.**
- 100% of upgrade fixtures (incl. one per alpha-tester DB shape) upgrade losslessly — row counts and
  per-table checksums match the migration report.
- 0 fire-and-forget persistence writes remain (review sweep); 100% of new `StudyLog` rows carry `DeviceId`.
- p95 task-save latency ≤ 1.2× the M1.1 baseline (baseline captured before stamping lands; provisional
  threshold, may tighten at DoR).

**Out of Scope.** Sync transport/merge execution; base-snapshot store (M2.1); conflict-record UI;
edit history; SOE tables; retention/purge (G4/T2.6).

**Risks.** Upgrade corrupts existing user DBs (mitigate: backup-before-upgrade + fixture suite —
the migration report is the evidence); T1.8 choice done twice (mitigate: decide once at M1.2 DoR;
the `TelemetrySchema` precedent + SQLite `ALTER TABLE ADD COLUMN` cover the D-I columns);
identity-semantics scope creep on T1.3 (bound to the observed `MonHoc`-clone class);
write-stamping regressing UI responsiveness (metric above guards it).

---

## Epic 3 — Study Optimization Engine *(executes second)*

**Goal.** Evolve the Balancer into the SOE under the frozen guardrails: hard constraints via the
Constraint Validator, quality-only objective `w1…w5`, D-H feasibility invariant, deterministic
ordered pipeline (never a global search).

**Architecture Impact.** New seams `IConstraintValidator` / `IObjectiveEvaluator` (names indicative);
stable boundary `Optimize(schedule) → (schedule, report)`; rework of `WorkloadServiceImpl` placement
behind an `IScheduleOptimizer` strategy seam (roadmap §7.3, respecting §13 — no micro-engines).

**Dependencies.** **M3.2 is gated on G2** (and G3 before ship). Epic 1 complete before M3.1+ code
(D-B). Epic 2 **not** required. M3.0 is parallel-safe during Epic 1: the corpus, baseline capture
and characterization tests exercise *current* code only, and G2 is a discussion — the freeze record's
"frozen regardless" list (D-G/D-H/D-J, `w1…w5`, the `Optimize` seam) is untouched by it.

**Deliverables.** G2 + G3 decision notes; benchmark corpus + current-allocator baseline;
validator + evaluator with per-seam tests; deadline-aware allocator; SOE report distinguishing
"rejected: infeasible" from "rejected: lower score"; `OptimizerRunLog` telemetry rows;
roadmap §7.3 + architecture docs updated after code lands.

**Acceptance Criteria.**
- Inversion test: a near-deadline task is never displaced past its deadline by a quality-improving
  rearrangement (D-G).
- Property test on the corpus: `violations(out) ≤ violations(in)` (count, then overdue minutes)
  on every input, including infeasible ones (D-H).
- No objective score can purchase a constraint violation (D-J); validator and evaluator tested
  independently.
- Same input ⇒ same output; every rejection carries a machine-readable reason.

**Success Metrics** *(measured on the T3.6 corpus: ≥ 200 generated schedules, ≥ 25% infeasible —
provisional sizes)*.
- **0** D-H invariant breaches; **0** deadline inversions (baseline: current allocator > 0,
  documented by the characterization tests — the delta is the headline alpha improvement).
- Determinism: byte-identical outputs across 3 repeated full-corpus runs.
- Explainability: 100% of rejected candidates carry a reason code in the report.
- Objective delta vs the current-allocator baseline reported per corpus schedule; the
  non-worsening threshold is finalized in the G2 decision note (it depends on the accept semantics).
- Runtime: full SOE run < 2 s p95 on the reference semester fixture (provisional; desktop UX bound).

**Out of Scope.** Global search / metaheuristics / argmax (D-E core); `w6·DeadlineUrgency` (dropped,
D-G); the proposal's six sub-engines (§13); autonomous ML scheduling (§6).

**Risks.** G2 stalls M3.2 (mitigate: G2 is front-loaded to Epic 1's timeframe; L8 already maps the
defect space); chosen pass semantics reintroducing local-optimum or no-op-iteration defects
(corpus metrics detect both); allocator rework destabilizing shipped Balancer behavior
(characterization tests in M3.0 pin it first).

---

## Epic 2 — LAN Sync Engine *(executes third)*

**Goal.** Two-way multi-device merge over LAN implementing the frozen D-I mechanics — no cloud,
offline-first preserved.

**Architecture Impact.** New sync module (merge engine + transport adapter) over the Epic 1
substrate; base-snapshot store (T1.4); conflict-record persistence; minimal sync status/trigger UI.
Domain engines/pipeline unchanged.

**Dependencies.** Epic 1 complete (hard). G4 blocks M2.2 only — **M2.1 is pure and network-free**
(fixture peers), so it may overlap Epic 3 if a second agent is available; it is off the alpha
critical path entirely. M2.1 → M2.2 → M2.3 sequential.

**Deliverables.** Snapshot store; merge engine (enumerate → 3-way diff → merge → conflict records);
property-test suite; G4 decision note; LAN transport + discovery; retention/purge job (T2.6);
sync trigger UX.

**Acceptance Criteria.**
- Different-field concurrent edits both survive (field-level merge); same-field concurrent edits
  resolve by LWW (`ModifiedAtUtc` → `DeviceId`) with the loser in a conflict record — no silent loss.
- Delete-vs-edit: tombstone wins; losing edit preserved in the conflict record.
- Convergence: two peers at the same watermark hold identical data.
- Offline-beyond-retention behavior is defined and tested per the G4 policy.

**Success Metrics.**
- ≥ 100 randomized two-peer divergence scenarios (property suite) converge to identical databases;
  **0** conflicts without a conflict record; **0** resurrected deletes.
- Merge engine fully validated in M2.1 **before any networking exists** (the property suite passes
  against fixture peers).
- Full sync of the reference DB over LAN < 10 s (provisional; finalized at G4).

**Out of Scope.** Cloud relay/accounts; >2-device mesh beyond pairwise merge; append-only edit
history (v1 ships conflict records only); mobile clients.

**Risks.** Clock skew biasing LWW (accepted by design — no HLC unless a concrete failure demands it;
conflict records bound the damage); transport flakiness on real LANs (merge/transport split in
M2.1/M2.2 isolates it; trigger stays manual-first); purge racing a long-offline device (default
no-purge until G4 sets retention ≥ max offline window).

---

## Epic 4 — ML Maturation *(executes last; telemetry accrues from M1.1)*

> **Amended 2026-08-24** — reconciled with [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md),
> which **governs where this section and it disagree** (owner direction, 2026-08-24). Four clauses
> below were *narrowed, not rewritten*: the deep-learning exclusion now defers to
> [`../specs/ML_Heuristic_design.md`](../specs/ML_Heuristic_design.md) §9.1; the model-count budget
> names its unit (§10); the ≥0.60 figures are marked as today's value; and M8-A's 96.2% carries its
> provenance. **Epic 4's scope, ordering, dependencies and execution position are unchanged.**

**Goal.** Replace synthetic training data with real telemetry (M8-C) and close the two D-D per-field
gaps (difficulty model, M9 NL deadline parsing) — within roadmap §9's 1–2-model budget, counted by
**deployed model artifact** (`ML_Heuristic_design.md` §10).

**Architecture Impact.** Confined to `Services/ML/*` + parser pipeline; per-field confidence gating
already in place (`IntentClassifierAdapter`, ≥0.60 **today** — re-derived under the encoder spec §8
if the neural featurizer ships). No engine/scheduling coupling (§13).

**Dependencies.** M8-C needs enough real Focus-session telemetry — the A6 fix (M1.1) starts the
clock, which is another reason M1.1 goes first. Training work is independent of Epics 2–3 and
parallel-safe once data suffices.

**Deliverables.** Retrained Study Time Predictor + eval report vs synthetic baseline; difficulty
classifier wired per-field with heuristic fallback; M9 deadline parser; cross-semester analytics.

**Acceptance Criteria.**
- Retrained predictor evaluated against the synthetic-seed model on held-out real telemetry;
  formula fallback intact when the model is absent (guardrail A.5-5).
- Difficulty/deadline predictions obey the per-field confidence gate (≥0.60 today; re-derived under
  the encoder spec §8 if the neural featurizer ships); below it, heuristic wins.
- Parser isolation preserved: the parser never schedules or allocates (§9.1).

**Success Metrics.**
- **Ship gate:** retrained predictor MAE on held-out real telemetry ≤ the synthetic-seed model's —
  otherwise it does not ship (no regression by novelty).
- Difficulty model held-out score threshold set at training time (M8-A's 96.2% is the precedent,
  not the promise — and that figure was measured *after* the real rows were merged into training, so
  it is not a synthetic→real generalization number); 100% of parses succeed with all models absent.

**Out of Scope.** Deep learning — **except** the narrow frozen-encoder exception of
[`../specs/ML_Heuristic_design.md`](../specs/ML_Heuristic_design.md) §9.1, on its eight guardrails;
a third+ **deployed model artifact** (§10 defines the unit — heads sharing one encoder do not each
count, and each capability still needs its own owner approval); ML-driven schedule generation; cloud
model storage.

**Risks.** Insufficient telemetry volume (fallback: extend the accrual window; Epic 4 is last for
this reason); model regressions vs heuristic baseline (ship gate above).

---

## Critical path & parallelization

**Alpha critical path (sequential — single agent or serialized):**

```
M1.1 → M1.2 → M1.3 → M3.1 → M3.2  →  closed alpha
                                        (Epic 2 is entirely off this path)
```

Sequential because each step edits the same substrate: the write path (M1.1), then the schema on
top of it (M1.2/M1.3), then the optimizer that consumes it (M3.x). Do not parallelize schema or
write-path work.

**Parallel-safe tracks (separate agents allowed):**

| Track | Work | May run during |
|---|---|---|
| P1 | Decision gates G1–G4 (notes, no code) | G1+G2 now; G3 during M3.1; G4 during Epic 3 |
| P2 | M3.0 — corpus, current-allocator baseline, characterization tests | Epic 1 (touches no production code) |
| P3 | Epic 4 training work | Any time after telemetry suffices (post-M1.1 accrual) |
| P4 | M2.1 — merge engine on fixture peers | Epic 3, if capacity exists (different module) |
| P5 | Docs sync per D-C after each milestone | Continuous |

---

## Definition of Ready (per implementation task)

1. **Decisions closed** — no OPEN gate (G1–G4) in the task's blast radius; decision note merged.
2. **Upstream done** — the preceding milestone is complete; for parallel-track tasks, the consumed
   interfaces are declared and stable.
3. **Acceptance criteria + owning success metric** identified before code is written.
4. **`gitnexus_impact`** run on the target symbols; HIGH/CRITICAL surfaced to the user first.
5. **Test strategy named** (fixture / property / characterization) and its location per the test
   structure convention (mirrors prod namespaces).
6. **Schema-touching tasks only:** upgrade path + backup/rollback story defined before the change.

## Definition of Done (every epic)

1. `gitnexus_impact` before editing any symbol; HIGH/CRITICAL reported before proceeding.
2. `gitnexus_detect_changes` before every commit.
3. `dotnet build SmartStudyPlanner.slnx` + `dotnet test --no-build` green.
4. New behavior covered by the epic's acceptance-criteria tests (property tests where stated).
5. Architecture docs (`docs/architecture/*`) updated **after** code lands (D-C); roadmap A.3 status
   updated (D-C.1).
6. Open decisions closed in a `docs/plans/YYYY-MM-DD-*.md` decision note before dependent tasks start.
7. The epic's **success metrics measured and reported** in its closing note.

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
- **Explicitly rejected observability** (scope discipline): logging frameworks, dashboards,
  crash reporting, metrics pipelines. The SOE report + `OptimizerRunLog` + conflict records +
  migration report are the alpha's entire observability surface.

---

## Changes in v2 (execution review, 2026-07-03)

1. **Execution order E1 → E3 → E2 → E4** (was E1→E2→E3→E4). Restores D-B's literal build queue;
   maximizes closed-alpha value; sync's risk moves behind a schema proven by real usage.
2. **T1.8 added (schema-upgrade mechanism)** — discovered gap: `EnsureCreated()` cannot alter
   existing DBs; without T1.8, Epic 1 could not ship to testers at all.
3. **Epics split into milestones** (M1.1–M1.3, M3.0–M3.2, M2.1–M2.3) — each independently
   verifiable; merge logic separated from networking (M2.1/M2.2).
4. **T1.4 (snapshot store) moved E1 → M2.1** — co-designed with its only consumer (the 3-way diff);
   zero alpha value earlier; D-B still satisfied (entity shape lands in M1.2).
5. **v1's T1.7 split into gates G1 (cascade — Epic 1) and G4-fed retention (Epic 2/T2.6)** with the
   safe no-purge default; all four decisions front-loaded as G1–G4 with explicit blast radii.
6. **Success metrics added per epic**; SOE thresholds that depend on the OPEN G2 decision are
   explicitly deferred to the G2 note rather than invented.
7. **T3.6 (corpus + baselines + characterization) and T3.7 (`OptimizerRunLog`, M8 telemetry
   pattern) added** — the measurement substrate for the metrics; no new subsystems.
8. **DoR (6 checks) and critical-path/parallelization section added**; DoD gains metric reporting.

No new features, no scope expansion: every added task is either a discovered prerequisite (T1.8),
moved scope (T1.4, T2.6), or the measurement infrastructure the plan's own acceptance criteria
already implied (T3.6, T3.7).
