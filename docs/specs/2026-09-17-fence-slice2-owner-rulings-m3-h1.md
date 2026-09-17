# T2.4 Structural Conflict Fence — Owner Rulings: M-3/A-1, H-1/A-2 clarification

| | |
|---|---|
| **Date** | 2026-09-17 |
| **Status** | M-3/A-1: **CLOSED — owner ruling** · H-1/A-2 clarification: **CLOSED — owner ruling** |
| **Goal** | Record, in one dated tracked file, the two owner decisions that close the Slice-2 review findings left open by the 2026-09-16 decisions, so no later slice has to infer them |
| **Scope** | T2.4 Policy-driven Mutation Routing + Structural Conflict Fence, Slice 2 implementation boundary only |
| **Closes** | Review findings **M-3/A-1** (cascade predicate) and the **H-1/A-2** routing-authority clarification, raised in PR #94 against PR #93 |
| **Amends** | Nothing. Both rulings CONFIRM existing plan text — see §4 |
| **Does not amend** | D1–D9 or D9-T1..D9-T6 of `docs/specs/T2.3-T2.4-D1-D9-Decision-Record-updated.md`; the canonical fence spec `docs/specs/2026-09-13-...-fence-spec-complete.md`; PR-5 semantics; PR-6 semantics (`docs/specs/T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md`) |
| **Leaves open** | **SB-2/OD-1** (sync-origin fence; gates Slice 6). OD-5, OD-6, OD-8 (plan §20) |
| **Implemented by** | PR #95 (stacked on PR #93). This file records the rulings; the code/test change lands in the same PR |

Follows the pattern of `docs/specs/2026-09-14-policy-driven-mutation-routing-owner-rulings.md`.
Labels: **[R]** owner ruling · **[D]** derived from a ruling plus tracked text · **FACT** read in the tree.

---

## 1. M-3 / A-1 — CLOSED: live-only effective cascade

> **[R]** The fence `ImpactSet` models the **semantic domain effects** of the requested mutation, not
> implementation-only re-stamping/provenance changes. For tombstone cascade impact:
>
> - `child.IsDeleted == false` ⇒ the child **is** an effective cascade target;
> - `child.IsDeleted == true` ⇒ the child is **not** an effective new tombstone target.
>
> An already-tombstoned child MUST NOT produce a new `CascadeTombstoned` lifecycle effect, a
> constraint-scope release solely because of the parent tombstone, or a new fence impact merely because
> an implementation path may re-stamp `Rev` or provenance fields.
>
> The effective fence cascade predicate is therefore `child.IsDeleted == false`.

### 1.1 What this resolves

Slice-0 measurement **P0-a** (`CascadePredicateProbeTests`) established as **FACT** that the two
production cascade implementations disagree:

| Path | Predicate | Behaviour on an already-dead child |
|---|---|---|
| Sync — `SyncApplySession.CascadeTombstoneAsync` | live-only | leaves it untouched |
| Local — `TaskCascadeHelper` | unfiltered | re-stamps `Rev` / `ModifiedAtUtc` |

Plan §7.3's "the same predicate the executing path uses" therefore had no single answer. The ruling
decides it on semantics rather than on either implementation: the local path's re-stamp is an
**implementation-level write**, not a lifecycle transition, so it is not a domain effect and does not
belong in `I(m)`.

### 1.2 D9-T1 is preserved — explicitly

**[D]** The ruling does **not** touch the uniqueness semantics of tombstoned `TaskNote` rows. The
`UNIQUE(MaTask)` index is unfiltered, so a tombstoned note **still occupies** its scope. The ruling says
only that *this mutation does not release it*:

| Occupant of `K(T)` | `Tombstone(StudyTask T)` | Scope | Fence result |
|---|---|---|---|
| live `TaskNote N` | cascade reaches `N` | `K(T)` **released** | `CONS.ScopeReleased` may block |
| already-tombstoned `N` | not an effective new target | `K(T)` **NOT released**, still occupied | `CONS.EmptyScopeParentTombstoned` (OD-4 branch) |

> "Scope remains occupied" and "scope does not exist" are different states and must not be conflated.

### 1.3 Implementation and evidence

- Predicate lives at the **`ImpactResolver` boundary** (`LiveChildIdsAsync`, plus the matching liveness
  check on the "moved in" leg of `CascadeChildIdsAsync`). `SyncApplySession` and `TaskCascadeHelper` are
  **unchanged** — the ruling required no production-path change.
- Pinned by `ImpactCascadeLivenessTests` (real SQLite), which asserts D9-T1 occupancy on an
  **independent channel** (direct DB read + a genuine `UNIQUE` violation) so the dead-child case cannot
  pass by having destroyed the occupancy it protects.

---

## 2. H-1 / A-2 clarification — CLOSED: fence-route eligibility is explicit

> **[R]** Merge classification, structural dependency topology, and fence-route eligibility are three
> logically separate classifications and must not be conflated.
>
> - `MergeSurfaceRegistry` is authoritative for **merge-field semantics**. Do not change a field's merge
>   classification merely to make fence routing work.
> - `StructuralDependencyRegistry` describes **structural dependency topology**. Being present there
>   means the relation is structurally known; it does **not** mean every mutation through it is
>   fence-routable.
> - A relation is `RouteKnown` only when **explicitly registered** as a fence-routable mutation relation.
>
> `structural dependency != automatically fence-routable` and `merge-known != automatically fence-routable`.
>
> **No fallback:** neither `MergeSurfaceRegistry` membership nor `StructuralDependencyRegistry`
> membership may imply `RouteKnown` unless the relation is explicitly marked fence-routable.

### 2.1 Required concrete cases

| Case | Relation | Merge classification | Structural dependency | Fence route |
|---|---|---|---|---|
| **A** | `TaskReferenceLink.MaTask` | `CopyOnCreate` (unchanged) | known | **explicitly registered** ⇒ `RouteKnown == true` |
| **B** | `StudyLog.MaTask` | `CopyOnCreate` (unchanged) | known | **not registered** ⇒ `RouteKnown == false` |
| **C** | merge-known, not explicitly routed | any | — | `RouteKnown == false` |
| **D** | explicitly routable | any (`CopyOnCreate`, `Structural`, `ConstraintScope`, …) | known | `RouteKnown == true` |

### 2.2 Implementation

**[D]** A narrowly scoped, explicit route-eligibility representation was added **inside Slice-2 scope**
rather than a new registry: `StructuralDependencyRegistry.StructuralEdge` gained a `FenceRoutable`
parameter, declared per edge. The separation is preserved — each concern keeps one authority:

| Concern | Authority | Accessor |
|---|---|---|
| Merge semantics | `MergeSurfaceRegistry` | (never consulted by the router) |
| Structural topology | `StructuralDependencyRegistry.Edges` membership | `IsKnownStructuralDependency`, `IsKnownEntityType` |
| Fence-route eligibility | `StructuralEdge.FenceRoutable` | `IsRegisteredStructuralRoute`, `IsFenceRoutableEntityType` |

No universal registry collapsing the three was created, and **no accessor reads another concern's flag**.
`CascadesOnTombstone` remains a fourth, independent column: it happens to agree with `FenceRoutable` on
today's five edges, which is a coincidence of the current domain and not a rule.

`IsFenceRoutableEntityType` gates the intent's entity type, so a bare `Tombstone(StudyLog, …)` naming no
relation is route-unknown too — not merely a `StudyLog` FK write.

---

## 3. Staged router model unchanged

**[D]** Neither ruling collapses the router's stages. The existing invariants stand:

```
RouteKnown == true   does NOT imply   FencePassed == true
FencePassed == true  does NOT imply   MayPersist  == true
```

`RouteKnown`, conflict selection, policy applicability and `FencePassed` remain distinct, per the
canonical spec §3.4 / §7.1.

---

## 4. Consistency check performed for this record (stop conditions)

Both rulings were checked against tracked text before implementation. **Neither amends anything** — each
confirms text that already existed:

| Ruling | Tracked text | Relation |
|---|---|---|
| M-3/A-1 live-only | plan §7.2 table row "Sync": *"`CascadeTombstoneAsync` on live→dead only … live children only"* | **confirms** |
| H-1/A-2 CASE B | plan §2 non-goals: *"No routing of `StudyLog` writes."* | **confirms**; the pre-ruling Slice-2 state contradicted this and is corrected by it |
| H-1/A-2 CASE A | plan §7.2 table row `StudyTask → TaskReferenceLink` (cascade edge) + D9-T3 (`MaTask` `CopyOnCreate`) | **confirms both**, and keeps them separate |

No frozen D1–D9 / D9-T1..T6 semantics, no canonical spec text, and no PR-5 or PR-6 semantics were
changed. No schema or migration change was required. No new application service was added.
