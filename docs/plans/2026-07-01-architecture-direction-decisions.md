# Architecture Direction Decisions — 2026-07-01

> **Status:** Accepted (D-A … D-D)
> **Scope:** Documentation & roadmap direction only. **No code changes in this pass.**
> **Anchors / supersedes:** corrects the parser finding in
> [`../review/2026-07-01-architecture-spec-review.md`](../review/2026-07-01-architecture-spec-review.md)
> and reshapes several items in
> [`../review/2026-07-01-architecture-reconciliation.md`](../review/2026-07-01-architecture-reconciliation.md).
> **Related proposal:** [`2026-06-30-workload-optimizer-proposal.md`](2026-06-30-workload-optimizer-proposal.md) (SOE).
> **Amended 2026-07-02:** the D-E/D-F open sub-decisions are resolved — except the SOE pass
> accept/commit semantics, which were **reopened** — by
> [`2026-07-02-architecture-freeze-decisions.md`](2026-07-02-architecture-freeze-decisions.md)
> (D-G…D-J). Inline *Update 2026-07-02* markers below point to the resolutions; bodies are preserved.

---

## 0. Purpose

Capture four project-direction decisions and the verified code reality that grounds
them, so the subsequent documentation edits are executed against a single agreed
baseline rather than re-litigated per file. This record is the "decide" step; the
per-file edits (Tier 1–4 in §4) are the "update" step that follows.

Two of these decisions were previously left open in the reconciliation:

- The parser-identity fork (reconciliation **D1**) is resolved here as **D-D**.
- The "declare a single source of truth" governance point is resolved here as **D-C**.

Two reconciliation forks remain **open** and are explicitly *not* decided here — see §5.

---

## 1. Decisions (ADR-style)

### D-A — Persistence target is multi-device, two-way LAN sync

**Decision.** The long-term persistence target is synchronization of the same
data set across multiple devices the user owns, with **two-way merge** (any device
may edit). This replaces the previously-assumed local-only / hybrid-DB direction,
which was never written into any spec.

**Rationale.** Chosen over the lighter alternatives (self-hosted LAN server,
one-way replica, manual export) because the intended usage is editing on more than
one machine, which requires conflict resolution rather than a single writer.

**Consequences.**
- The data model must become **sync-ready** (see D-B and §2 for what already exists).
- The privacy positioning changes: "offline / no cloud" survives; **"nothing leaves
  the device / all data local" does not** and must be reworded.
- Elevates the persistence findings from cleanup to foundation (see §3: **A6, B3**).
- Introduces a new, undecided sub-problem: the **conflict-resolution policy**
  (last-writer-wins vs. field/row merge). Flagged as open in §5 — it needs its own
  design note before implementation.

### D-B — Sync-ready data model lands before the Study Optimization Engine

**Decision.** The sync-readiness data-model work is sequenced **ahead of** the SOE.
Both need data-model changes; the sync layer is more foundational (it touches every
entity) and the SOE's session-history needs can be co-designed on top of it.

**Rationale.** Retrofitting identity/merge semantics onto entities after the SOE has
built on them is more expensive than doing it first. The SOE is additive on top of a
sync-ready model, not blocked by it conceptually — only by ordering.

**Consequences.**
- The immediate build queue is: sync-ready data model → (debt: B3/A6) → SOE on top.
- Does **not** commit to building LAN sync itself next — only that *when* new
  persistence work happens, the sync-ready shape comes before SOE-specific tables.

### D-C — Code is normative; docs are narrative

**Decision.** Where documentation and shipped code disagree, **code is the source of
truth**. Architecture docs (`docs/architecture/*`) are **descriptive** and may lag the
code; the roadmap is **aspirational** and may run ahead of it.

**Rationale.** Docs and code are updated on separate cadences by design, so drift is
the default state, not a defect. Treating every divergence as an equal-weight finding
(as the original review implicitly did) is wrong. What matters is distinguishing
**lag** (a decision already made, doc just trails — self-heals) from a **fork** (doc
and code encode different intent, no decision was ever made — the only real finding).

**Consequences.**
- Each architecture doc gets an explicit status label:
  *"Descriptive — reflects code as of <commit>; may lag."*
- The roadmap gets: *"Aspirational — target direction; not a description of current code."*
- Pure-lag items become low-severity **sync debt**, batch-fixable (see §4 Tier 3).
- The roadmap surfaces are consolidated to **one** canonical file (see D below).

### D-C.1 — Single canonical roadmap = `docs/specs/system_roadmap.md`

**Decision.** The three competing roadmap surfaces (README "What's coming next",
`docs/ROADMAP.md`, `docs/specs/system_roadmap.md`) collapse into
**`docs/specs/system_roadmap.md`** as the single aspirational roadmap.
`docs/ROADMAP.md` is **retired** (replaced with a one-line pointer). The README keeps
only a short marketing-level "coming next" that links to the canonical file.

### D-D — Parser is ML-first with a confidence-gated fallback, **per output field**

**Decision.** The parser's target and current behavior is: an ML classifier is
consulted first and **overrides** the heuristic when it is present and confident
(≥ 0.60); below threshold, or when the model is absent, the heuristic wins.
This is applied **per output field**, not to the parse as a whole.

**Rationale.** This matches the shipped code (see §2). The earlier review's claim
that the parser is "heuristic-first" was factually wrong: the heuristic is the
always-on **baseline/fallback**, and ML has **precedence** when confident.

**Consequences.**
- Reconciliation **C2 / D1** is **not a fork and needs no code change** for task type
  — it is a documentation correction.
- The target is only **partially implemented today**: it holds for **task type**;
  **difficulty** and **deadline** are still heuristic (see §2). The roadmap therefore
  keeps two open items: train a **difficulty** model, and natural-language **deadline**
  parsing (already the M9 candidate).
- The correct normative sentence to write into the docs is:
  > *"A heuristic baseline always runs. When the intent-classifier model is loaded,
  > its **task-type** prediction overrides the heuristic at confidence ≥ 0.60;
  > below that, or if the model is absent, the heuristic wins. **Difficulty** and
  > **deadline** are always rule-based today."*

### D-E — Study Optimization Engine computation model

**Decision.** The SOE uses **deterministic sequential optimization with objective evaluation after
each step**: the §8 ordered pipeline *is* the algorithm, and the `LearningEfficiencyScore` (§6) is an
**evaluation function applied after each optimizer**, **not** a global-search / argmax target.
Resolves reconciliation **D2 / N1**.

**Rationale.** Preserves the roadmap's **deterministic + explainable** mandate (§6/§15), which a search
over a weighted objective would weaken. §6's score becomes a per-step diagnostic/guardrail, not the goal.

**Consequences.**
- The §8 optimizer **order is normative** (order-dependent greedy transform) and must be fixed and
  specified — including where the Constraint checks sit (reconciliation N11).
- Architecture fit (N10): `IScheduleOptimizer.Apply(schedule, ctx)` per step + a separate
  `IObjectiveEvaluator.Score(schedule, ctx)` invoked after each. Testable and deterministic.
- **N4 must be resolved when the objective is written:** deadline urgency currently lives in both
  `PriorityScore.TimeComponent` and `w6·DeadlineUrgency`.
  *Update 2026-07-02: resolved by **D-G** — `w6` is dropped; deadline re-enters the SOE as a hard
  constraint, so the double-count is gone by construction.*
- **Open sub-decision** (§5): is the per-step evaluation an **accept/reject gate** (roll back a
  score-lowering step — hill-climbing) or a **measured guardrail** (bounds soft-balancing / explains only)?
  *Update 2026-07-02: **reopened and broadened**, not resolved — per-step gating and whole-pass
  accept/reject both have identified defects; see the freeze record §3 (OPEN) and lessons-learned L8.*

### D-F — LAN sync merge strategy

**Decision.** **Field-level merge by default; Last-Writer-Wins only when the same field is modified
concurrently** on two devices. Resolves the D-A conflict-resolution policy.

**Rationale.** Auto-merges the common case (edits to *different* fields on different devices) and falls
back to LWW only for true same-field collisions.

**Consequences.**
- **Upgrades the data-model requirement** (§3 / `data-model.md` §8): field-level merge needs
  **field-level change detection**, not just a per-row timestamp — a last-synced **base snapshot**
  (3-way merge) or **per-field versioning**.
- **Open sub-decision** (§5) — the LWW clock: wall-clock + `DeviceId` tiebreak (simple; `StudyLog`
  already carries both) vs. a hybrid logical clock (robust against skew).
- **Open sub-decision** (§5) — **delete-vs-edit** (tombstone on A, field edit on B) is not covered by
  the rule: tombstone wins vs. edit resurrects.
- Guid-per-entity helps: notes/links merge as independent rows; only same-row same-field collisions hit LWW.

---

## 2. Verified code reality (the code-normative baseline)

Established by reading the source at the current commit — this is the ground truth the
Tier 1 doc edits must describe.

**Entity identity — Guid PKs already present (the ID half of sync-readiness is done):**

| Entity | Key | File |
|---|---|---|
| `HocKy` | `[Key] Guid MaHocKy` | `SmartStudyPlanner/Models/HocKy.cs:11` |
| `MonHoc` | `[Key] Guid MaMonHoc` | `SmartStudyPlanner/Models/MonHoc.cs:10` |
| `StudyTask` | `[Key] Guid MaTask` | `SmartStudyPlanner/Models/StudyTask.cs:17` |
| `StudyLog` | `[Key] Guid Id` | `SmartStudyPlanner/Models/StudyLog.cs:8` |
| `TaskNote` / `TaskReferenceLink` | `Guid Id = Guid.NewGuid()` | `.../TaskNote.cs:5`, `.../TaskReferenceLink.cs:5` |
| Telemetry logs | `[Key] Guid Id` | `SmartStudyPlanner/Models/Telemetry/*.cs` |

**Parser behavior — ML-first override, per field:**

- `ParsingOrchestrator.Parse` computes the heuristic baseline unconditionally, then
  `prediction?.Loai ?? loaiHeuristic` / `prediction?.DoKho ?? doKhoHeuristic` — ML
  **overrides** when present. `SmartStudyPlanner/Core/Parsing/Orchestrators/ParsingOrchestrator.cs:41-56`.
- `IntentClassifierAdapter` drops any prediction below 0.60 (policy `Reject`) so the
  heuristic takes over; any failure returns null (offline-first).
  `SmartStudyPlanner/Services/ML/IntentClassifierAdapter.cs:33-34`.
- The classifier is **DI-wired into the orchestrator in production** (not the null
  default): `SmartStudyPlanner/Services/ServiceLocator.cs:85-97`.
- The classifier only emits **task type** today — `TextClassifierService` sets
  `DoKho = null` ("difficulty prediction is deferred to a separate model") and returns
  null unless `IsModelLoaded`. `SmartStudyPlanner/Services/ML/TextClassifierService.cs:25,35`.
- **Therefore, at runtime:** task type = ML-first+fallback (model-load conditional);
  difficulty = always heuristic; deadline = always rule-based.

---

## 3. How these reshape the prior findings

Delta against [`2026-07-01-architecture-reconciliation.md`](../review/2026-07-01-architecture-reconciliation.md).

| Finding | Prior status | New status after these decisions + §2 |
|---|---|---|
| **C2 / D1** parser | fork / "roadmap ML-first vs heuristic-first code" | **Resolved (D-D).** Doc correction, no code change for task type. Roadmap keeps difficulty-model + M9 deadline as open items. Original review's "heuristic-first" claim is **retracted**. |
| **A6** fire-and-forget study-log writes | Still Valid, low tier | **Severity ↑.** Under two-way sync a lost/duplicated write **diverges replicas**, not just "loses one log." Becomes a prerequisite to LAN sync. |
| **B3** half-migrated repositories | Still Valid | **Promoted to prerequisite.** A clean single repository layer is required before a merge/sync layer. |
| **N7** data model must grow | Still Valid (SOE-driven) | **Reframed & partly done.** ID foundation already exists (Guid PKs). Remaining sync-readiness = identity **semantics** + **tombstones** + **change tracking** (see below). Sequenced first (D-B). |
| Governance / "one source of truth" | Open | **Resolved (D-C / D-C.1):** code-normative; single canonical roadmap. |
| Privacy positioning | (implicit, accurate) | **Now must change (D-A):** "all data local" is false under LAN sync. |

**Remaining sync-readiness work (not the ID column — that is done):**
1. **Identity semantics.** The dedup-cloned-`MonHoc` fix (commit `946799b`) is the
   preview: two rows meaning the *same* subject. Guids do not solve this; two-way
   merge amplifies it. Needs a defined equality/merge identity per entity.
2. **Tombstones.** Today's cascade deletes are **hard** deletes and cannot propagate
   through sync. Target: soft-delete + tombstone rows.
3. **Change tracking (field-level, per D-F).** Enough to detect *which fields* changed — a last-synced
   base snapshot (3-way merge) or per-field versioning — plus a LWW tiebreak clock; a bare per-row
   timestamp is insufficient for field-level merge.

---

## 4. Execution checklist (Tiers)

- [x] **Tier 0 — this document.**
- [x] **Tier 1 — normative corrections (`docs/architecture/*`)** *(applied 2026-07-01)*:
  - **Discovery:** `pipeline.md` §2 and `overview.md` §5.5 **already** described the per-field
    parser behavior correctly (heuristic baseline; ML overrides task type at ≥0.60;
    difficulty/deadline rule-based). The parser error was in the *review*, not the docs — no
    parser rewrite was needed, which confirms D-C: code and these two docs were already in sync.
  - `pipeline.md`: added the D-C "descriptive / may-lag" label; replaced the now-stale §6
    "Drift đã biết" note (it flagged `overview.md` errors that are already fixed) with a
    reconciliation-status note.
  - `overview.md`: added the D-C "descriptive / may-lag" label. Parser wording untouched.
  - `data-model.md`: rewrote §8 as "Future-proofing & sync-readiness" (D-A LAN-sync target;
    Guid-PK identity already present; `StudyLog` partial sync fields; remaining = identity
    semantics + tombstones + change tracking + conflict policy); added a §3 hard-delete
    sync-incompatibility note.
- [x] **Tier 2 — roadmap consolidation into `docs/specs/system_roadmap.md`** *(applied 2026-07-01)*:
  - Restructured `system_roadmap.md` into **Part A — Delivery Status (factual)** + **Part B —
    Architecture Direction (aspirational)** with a canonical-roadmap header (D-C.1). Part A imported
    the `docs/ROADMAP.md` ledger (snapshot facts corrected: index → 3,333/7,953/127; test count
    de-hard-coded) + a rewritten "Next up" (sync-ready data model → LAN sync → SOE → M8-C → M9).
  - Retired `docs/ROADMAP.md` → pointer stub. Shortened README "What's coming next" → teaser + pointer.
  - Added four Part B reconciliation callouts: §9.1 parser (D-D), §7.3 Balancer→SOE (D-A/D-B/D2/**N9
    §13 tension surfaced, not hidden**), §7.1 competency (N5 net-new), §14 v2 cloud→LAN (D-A).
- [x] **Tier 3 — README + lag sweep** *(applied 2026-07-01)*:
  - **Privacy reworded carefully:** kept the durable, true promise (no cloud / no account /
    no third-party / offline); dropped the absolute "nothing is uploaded to any server" and
    "no data sent anywhere" that LAN sync (D-A) would eventually contradict; added a "planned
    multi-device sync stays on your own devices / still no cloud" note. LAN sync is **not**
    claimed as a current feature.
  - Lag fixes: `overview.md` §3 test count de-hard-coded; §5.1 `WorkloadBalancerWindow` →
    `WorkloadBalancerPage` (verified: live class is the Page; `*Window` only in `obj/`+`legacy/`);
    `usecase-flows.md` UC-03 `SmartParser.Parse` → injected `IParsingOrchestrator.Parse` (per D-D).
- [x] **Tier 4 — review docs** *(applied 2026-07-01)*: added non-destructive **erratum banners** to
  the top of both `2026-07-01-architecture-spec-review.md` (C2) and
  `2026-07-01-architecture-reconciliation.md` (C2 row + D1) — correcting the "heuristic-first" parser
  claim to the verified ML-first-with-fallback reality, noting **D1 is resolved by D-D**, and pointing
  to this record. Bodies left intact to preserve the audit trail (the reconciliation's D1
  cross-references stay consistent).
- **Post-pass verification (2026-07-01):** repointed the `docs/README.md` index "reading order" link
  from the retired `ROADMAP.md` to `specs/system_roadmap.md`; corrected `pipeline.md` §2 to note that
  `ParseSource.MlOverridden` is a **declared-but-unused** enum value (the orchestrator only ever
  produces `Heuristic`/`MlAugmented`) — the same code-normative defect class as the Tier 4 fix.

---

## 5. Open — explicitly not decided here

Resolved since the first draft: SOE computation model → **D-E**; LAN conflict-resolution policy → **D-F**.
The two decisions surfaced three new follow-on sub-decisions:

- **SOE per-step evaluation semantics (D-E):** accept/reject gate (roll back a score-lowering step)
  vs. measured guardrail (bounds soft-balancing / explanation only).
  *Update 2026-07-02: still open — reframed as the pass accept/commit-granularity question
  (freeze record §3); both per-step gating and whole-pass accept/reject have identified defects (L8).*
- **LWW clock (D-F):** wall-clock + `DeviceId` tiebreak vs. hybrid logical clock.
  *Update 2026-07-02: resolved by **D-I** — no HLC; tie-break `ModifiedAtUtc` → `DeviceId`; `Rev`
  counters are never ordered across devices (lessons-learned L6).*
- **Delete-vs-edit conflict (D-F):** tombstone wins vs. edit resurrects.
  *Update 2026-07-02: resolved by **D-I** — tombstone wins; the losing edit is preserved in a conflict
  record. Retention/purge + cascade policy remain open.*

Still open from the reconciliation:

- **Second weight-vector governance** (reconciliation **B5**): the SOE's `w1…w6` join
  the existing priority weights and the WeightOptimizer's — governance undecided.
  *Update 2026-07-02: the vector is now `w1…w5` (D-G dropped `w6`); governance itself still open.*
- **Difficulty ML model** timing, and whether NL **deadline** parsing (M9) becomes
  ML-gated like task type.

---

## 6. Non-goals

No source code is modified in this pass. All items above are documentation and
roadmap edits, executed only after Tier 0 is accepted.
