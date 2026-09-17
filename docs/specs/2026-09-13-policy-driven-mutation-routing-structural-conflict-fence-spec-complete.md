# Policy-driven Mutation Routing + Structural Conflict Fence

| Field | Value |
|---|---|
| Date | 2026-09-13 |
| Status | Semantic / architecture specification |
| Applies to | T2.4 mutation entry points and the future integration boundary around them |
| Does not amend | D1–D9 or D9-T1…T6 |
| Decision authority | The owner-approved direction in the referenced conversation, read together with the frozen records below |

## 1. Purpose

This document defines a decision boundary for mutations which could alter state protected by an unresolved StructuralConflict or ConstraintConflict.

It formalizes the owner's approved direction:

1. protection is defined per conflict shape and protected contract, not as a global lock on an entity or tree;
2. each mutation is evaluated through an explicit impact set;
3. overlapping conflicts are evaluated by a routed policy matrix, without a global conflict priority;
4. local deletion uses the same structural-conflict fence as other mutation paths;
5. a policy pass is necessary but never sufficient authorization to persist a change;
6. every materially different case has its own policy/abstraction;
7. the boundary is `application/business → router/policies → decision → executor → repository`;
8. routers and policies are read-only with respect to persistent state; and
9. Restore remains a separate, future lifecycle flow, not a generic resurrection capability in T2.4; and
10. resolving a conflict record is distinct from deciding or executing a domain mutation.

The document deliberately specifies semantic contracts and integration boundaries. It does not prescribe class names, EF queries, schema changes, UI behavior, or a pull-request sequence.

## 2. Authority and terminology

### 2.1 Authority labels

The labels in this document are normative for interpreting its statements:

| Label | Meaning |
|---|---|
| **[R] Owner-ratified** | An owner decision already recorded in D1–D9/D9-T1…T6 or agreed in the referenced conversation. |
| **[D] Derived design** | An engineering formalization needed to implement the ratified direction. It must not be represented as an amendment to the frozen record. |
| **[A] Assumption** | A bounded implementation assumption, explicitly revisitable without changing the semantic contract. |
| **[O] Open question** | A matter intentionally left for a later owner decision or engineering design. |

### 2.2 Source hierarchy

| Source | Use in this specification |
|---|---|
| `T2.3-T2.4-D1-D9-Decision-Record-updated.md`, especially §23A | Frozen semantic authority: D1–D9 and D9-T1…T6. |
| `T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md` and `2026-09-11-t2.4-pr6-conflict-resolver-dor.md` | Existing PR-6 resolution contracts, record shapes, and engineering constraints. |
| `2026-09-13-w2-f2-semantic-analysis.md` | Analysis of the D9-T1/D9-T4 crossing; facts and unratified alternatives only. |
| `2026-09-12-t2.5-recon.md` and related review/recon material | Test, convergence, and integration evidence; not new semantic authority. |
| Referenced owner conversation | Ratifies the architectural direction summarized in §1. |

Where this specification and a frozen D1–D9/D9-T1…T6 rule appear to conflict, the frozen record wins. A genuine conflict requires a dated owner amendment; it is not solved by interpretation in code.

### 2.3 Existing frozen semantics preserved here

The fence is an addition to the mutation decision boundary; it does not alter these rules:

- **[R] D1:** T2.3 remains a pure three-way merge, with no persistence, transaction, network, or UI work.
- **[R] D4/D5:** Structural and constraint conflicts do not receive a generic automatic winner.
- **[R] D8/D9-T1:** an unresolved StructuralConflict or ConstraintConflict stages immutable Base/Local/Remote evidence; live state equals Base at staging.
- **[R] D8-H:** resolution rejects live-state drift; it does not force or rebase.
- **[R] D9-T4:** a live orphan under a missing or tombstoned required parent is forbidden.
- **[R] D9-T5:** AutoLww and AutoTombstone are already resolved evidence and are not user-resolvable.
- **[R] D9-T6:** there is at most one unresolved record per logical conflict scope; no supersede/reopen behavior is introduced in v1.
- **[R] D7/D8:** evidence is immutable, a resolved record is terminal, and staging/resolution atomicity remains intact.

Only unresolved StructuralConflict and ConstraintConflict records participate in this fence. Auto-resolved field/tombstone evidence is not a fence input.

### 2.4 Vocabulary boundary

This specification uses the following terms deliberately:

| Term | Meaning | Must not be conflated with |
|---|---|---|
| `ConflictRecord` | Immutable staged Base/Local/Remote evidence plus the record lifecycle (`Unresolved` or terminal `Resolved`). | The domain object, relation, scope, or absence state protected by the record. |
| **Protected subject / reference frame** | The concrete entity, identity-absence, relation, or constraint scope whose state is interpreted by a shape-specific policy. | A universal “resolution target” or an entity lock. |
| **Protected contract** | The protected subject/reference frame together with the state predicates that a mutation must not violate while the record remains unresolved. | A request to resolve the `ConflictRecord`. |
| **Fence** | Read-only evaluation of `MutationIntent × ProtectedContract`. It passes, blocks, or fails closed; it does not settle a conflict. | `ConflictResolver` or a record lifecycle transition. |
| **Conflict resolution** | An explicit choice under the existing resolution semantics that terminally transitions a `ConflictRecord`; it may or may not require a domain-state change. | Any mutation that happens to be near a conflict. |

## 3. Model

### 3.1 Mutation intent and impact set

**[D]** A `MutationIntent` is a declarative request, before persistence. It contains at least:

```text
operation kind
origin (local UI/use case, sync integration, resolver, future lifecycle flow)
direct target identities
before and proposed-after relationship values
requested lifecycle effect
```

It is not an EF entity mutation and must not have durable side effects merely by being routed.

For an intent `m`, the router derives an **impact set**:

```text
I(m) = Rows(m) ∪ Edges(m) ∪ ConstraintScopes(m) ∪ LifecycleEffects(m)
```

| Element | Meaning |
|---|---|
| `Rows(m)` | Directly changed rows plus rows affected by the actual domain cascade of `m`. |
| `Edges(m)` | Old and proposed structural/FK relationships, including both endpoints. |
| `ConstraintScopes(m)` | Each constraint scope released, acquired, created, withdrawn, or otherwise changed by `m`. |
| `LifecycleEffects(m)` | Create, tombstone, hard-delete where already authorized, and any attempted materialization of an absent logical identity. |

`I(m)` includes both the before and after values. A reparent from `P1` to `P2`, for example, touches the child row, the `P1 → child` edge, the `P2 → child` edge, and any child-owned dependent scopes reached by the domain operation.

**[D] Actual cascade, not invented closure.** `Rows(m)` expands only through the cascade/dependency rules already defined for that operation. The fence must not create a universal “descendant closure” simply because an unresolved conflict exists somewhere in a tree.

### 3.2 Conflict record, protected subject, and resolution

**[D]** The fence is contract-centric, not record-centric. A `ConflictRecord` identifies evidence and lifecycle state; it does not by itself say that every mutation touching the record, entity, or hierarchy must resolve it.

For each unresolved record `c`, its shape-specific policy derives a protected subject/reference frame `S(c)`:

| Shape | `S(c)` — protected subject/reference frame | State interpreted by the contract |
|---|---|---|
| **S1-CR** | Conflicted entity `E` and its contested D4 structural edge `E → P`. | `E` remains present and the contested edge is not replaced, deleted, or bypassed. |
| **S1-PT** | Staged entity `E` in the frame of its required structural parent `P`. | The candidate remains subject to `E` live and `E → P` structurally valid; a generic path cannot bypass the missing/tombstoned-parent case. |
| **AL-PT** | Logical identity `E` in its live-absence state, together with any attempted `E → P` materialization under the tombstoned parent. | `E` remains absent from live state unless a separately authorized lifecycle flow exists. |
| **Constraint** | Concrete constraint scope `K` and the staged candidate/occupancy identities in that scope. | The staged occupancy/candidate relation for `K` is not changed. |

The table defines subjects independently because an entity, an edge, live absence, and a constraint bucket are not interchangeable. It also means that a mutation can be relevant to a record yet pass the fence when it leaves that record's protected subject/reference frame unchanged.

`ConflictResolver` operates on `ConflictRecord` evidence and its lifecycle. The fence operates on `MutationIntent × ProtectedContract`. A fence policy never “resolves a subject,” and an ordinary domain mutation never resolves a record merely because it reaches that subject.

### 3.3 Protected contract

For an unresolved conflict `c`, its policy defines a protected contract:

```text
P(c) = { S(c), protected relations/scopes, protected state predicates }
```

The policy decides whether `m` violates `P(c)` using its shape-specific rules. Intersection is a routing signal, not a decision by itself:

```text
Relevant(c, m)  := I(m) can affect an object, relation, or scope named by P(c)
Violates(c, m)  := Relevant(c, m) AND policy(c).DetectsProtectedViolation(m)
```

This distinction is essential. Conflict dependency says which policies must inspect a mutation; mutation impact says what the mutation can change. Neither is a substitute for the other.

### 3.4 Fence outcome

The fence returns one of the following values per applicable policy:

| Outcome | Meaning |
|---|---|
| `NotApplicable` | The mutation cannot affect this policy's protected contract. |
| `Passed` | This policy found no protected-contract violation. This is not execution authorization. |
| `Blocked` | The mutation would violate the protected contract. The result includes conflict id, shape, protected subject, and rule. |
| `Unsupported` | The router found an unresolved shape without a registered policy. This fails closed. |

The aggregate fence result is:

```text
FencePassed(m) ⇔ every applicable policy returns Passed or NotApplicable
```

`FencePassed(m)` is only a necessary condition for execution:

```text
MayPersist(m) ⇔
    RouteKnown(m)
    ∧ FencePassed(m)
    ∧ DomainAuthorization(m)
    ∧ BusinessValidation(m)
    ∧ PersistencePreconditions(m)
    ∧ TransactionAndConcurrencyChecks(m)
```

The word `Allowed` must not be used as a shorthand for `MayPersist` when it only means that a single fence policy passed.

### 3.5 Resolution is not a domain mutation

**[R]** Resolution is a terminal `ConflictRecord` lifecycle transition under the frozen resolution semantics. A domain-state mutation is a separate outcome, evaluated and executed through its own applicable contracts and gates.

**[D]** Therefore, a successful resolution need not change domain state:

```text
AL-PT evidence: Base = ∅, Local = ∅, Remote = tombstone
KeepBase:       live domain state remains ∅
                 ConflictRecord: Unresolved → Resolved
```

The resolution is semantically real because the record lifecycle changed, even though the domain-state projection is a no-op. Conversely, a fence pass does not resolve a record, and a resolution choice does not authorize an unrelated mutation.

## 4. Protected contracts by conflict shape

The following table intentionally does not collapse all unresolved records into an `EntityLocked` abstraction.

| Shape | Protected object/scope and contract | Fence-sensitive effects | Explicitly not protected merely by this shape | Required policy |
|---|---|---|---|---|
| **S1-CR — ConcurrentReparent** | The conflicted entity `E`, its D4 structural edge, and its lifecycle presence as required to preserve the staged structural conflict. | Reparenting `E`; tombstoning/deleting `E`; an ancestor delete whose actual cascade includes `E`; any write that replaces the contested structural edge. | Unrelated siblings; descendants not reached by `m`; ordinary non-structural fields solely because `E` has a conflict. | `ConcurrentReparentFencePolicy` |
| **S1-PT — ParentTombstoned with a local row** | The staged entity `E` and the structural-parent validity of the candidate represented by the record. A mutation may not use a generic local path to bypass the missing/tombstoned-parent contract. | Materializing/keeping `E` under an invalid parent; changing or tombstoning `E` in a way that crosses the staged contract; ancestor delete whose cascade includes `E`. | The whole parent subtree; a sibling of `E`; a descendant that is not in `I(m)` and whose own contract is not affected. | `ParentTombstoneFencePolicy` |
| **AL-PT — Absent-local ParentTombstoned** | The absence of logical identity `E` in live state and the candidate's structural relation to a tombstoned parent. No normal create/update path may silently materialize `E`. | Same-identity create/materialization; a generic un-tombstone; writing a relationship that would create live `E` under an invalid parent. | Mutations to unrelated live identities; a future explicit Restore flow after it has its own contract. | `AbsentLocalParentTombstonePolicy` |
| **Constraint conflict — S2/S3, including null-Base forms** | The concrete constraint scope `K` and the staged candidate identities/occupancy represented by the record. For the known TaskNote case, `K` is the relevant `TaskNote` uniqueness/ownership scope, not every TaskNote or every task descendant. | Insert, delete, reassignment, or parent cascade that acquires/releases/changes occupancy of `K`; changing the constrained relation of a candidate or current occupant. | Ordinary properties which do not affect `K`; another constraint scope; arbitrary descendants of a task. | `ConstraintOccupancyFencePolicy` |

### 4.1 Consequences of the per-shape contracts

- **[R] Direction:** an unresolved conflict does not lock an entire entity tree.
- **[D]** An ordinary content edit can be fence-passing when it does not affect the contract above. It may still fail later domain validation or produce D8-H live-state drift at resolution; the fence is not a drift bypass.
- **[D]** A delete is analyzed by its actual cascade envelope. It is blocked only when that envelope violates one or more protected contracts, not because it is a delete in the same general hierarchy.
- **[D]** A policy must state why a particular relation, row, constraint bucket, or absence state is protected. “It is close to a conflict” is not a valid reason.

### 4.2 No implicit resurrection

**[R] Current capability boundary:** T2.4 has no generic resurrection model. The merge contract remains fail-closed for a live candidate over a tombstoned Base identity; `ConflictRecord` is sync-state evidence, not lifecycle history.

**[D]** AL-PT therefore has a distinct policy. It must never be weakened into “an entity is absent, so create is harmless.” A same-identity materialization is routed out of the ordinary create path rather than inferred to be a new identity.

## 5. Structural dependency and impact matrix

### 5.1 Known dependency graph

The matrix uses the existing domain relationships evidenced in the T2.3–T2.4 material:

```text
HocKy
  └─ MonHoc
       └─ StudyTask
            ├─ TaskNote        (FK child and known constraint participant)
            └─ TaskReferenceLink (FK child)
```

The diagram is an impact-routing aid, not a statement that all descendants are protected by every conflict.

### 5.2 Required impact expansion

| Mutation class | Minimum impact expansion | Relevant protected contracts it can reach |
|---|---|---|
| Update a non-structural scalar | Target row and changed scalar only. | Usually none; only a policy that explicitly protects that field/scope may inspect it. |
| Reparent `MonHoc` or `StudyTask` | Entity row; old and new D4 edges; old and new structural parent identities; any operation-defined dependents. | S1-CR/S1-PT/AL-PT on the entity or targeted structural relation. |
| Create a child with a parent relation | New identity; proposed parent edge; any acquired uniqueness/constraint scope. | AL-PT for same identity; S1-PT/AL-PT for invalid parent relation; Constraint policy for acquired scope. |
| Delete/tombstone `TaskNote` | Note row; `TaskNote → StudyTask` FK; constraint scope before withdrawal. | Constraint policy for that exact scope; a parent-policy only if its actual cascade/edge is affected. |
| Delete/tombstone `StudyTask` | Task row; task's structural parent edge; all actual TaskNote and TaskReferenceLink cascade targets; each TaskNote constraint scope released or changed. | S1 policies on the task; Constraint policies for each reached TaskNote scope; policies on explicitly reached dependents. |
| Delete/tombstone `MonHoc` | Subject row; parent edge; actual descendant StudyTasks, TaskNotes, links, and their impacted constraint scopes. | Every unresolved contract whose protected subject/scope lies in the actual cascade envelope. |
| Delete/tombstone `HocKy` | Semester row and the actual cascaded MonHoc/StudyTask/TaskNote/link targets; their affected edges/scopes. | Same rule as `MonHoc`, potentially including grandchildren and TaskNote constraints. |
| Change a TaskNote constrained relation | Note row; old constraint scope; new constraint scope; owning task FK where changed. | Constraint policies for both old and new scopes. |
| Delete an unrelated sibling | That sibling and its own dependencies only. | No inherited relevance from an unresolved conflict on another sibling or cousin. |

### 5.3 Constraint and grandchildren rule

**[R] Direction:** constraints and grandchildren cannot be ignored when computing impact.

**[D]** The implementation must calculate them through the operation's real dependency graph:

1. A parent tombstone that cascades to a `TaskNote` affects that note's constraint scope.
2. A `TaskNote` constraint conflict does not, by itself, protect every sibling note or every grandchild of its task.
3. A grandchild is relevant when the mutation actually reaches it, changes its relation, or changes the constraint scope it occupies.
4. Constraint scopes are independent from hierarchy. They are added because the operation changes their occupancy, not merely because the entity is descended from a conflicted row.

This preserves D9-T4's no-orphan invariant and prevents the opposite error: treating a `ConstraintConflict` as a lock on an entire structural subtree.

## 6. Mutation × conflict-shape policy matrix

This matrix converts the per-shape contracts into routing expectations for the principal mutations. It is evaluated per matching `ConflictRecord` and its `S(c)`, never as a global rule based only on conflict kind.

| Status | Meaning |
|---|---|
| **Block** | The described mutation necessarily violates the matching protected contract. |
| **Pass** | The described mutation leaves the matching protected contract unchanged. Normal authorization and validation still apply. |
| **NA** | The stated mutation cannot reach this shape's protected subject/reference frame. |
| **Conditional** | The policy evaluates the actual `I(m)` against the particular `S(c)` and returns `Blocked`, `Passed`, or `NotApplicable` with evidence. |

| Mutation, scoped to the named protected subject/reference frame | S1-CR | S1-PT | AL-PT | Constraint |
|---|---|---|---|---|
| Edit an ordinary non-structural field on `E` or an unrelated row | **Pass** when it does not replace the contested edge. | **Pass** when it does not change candidate parent validity. | **NA** for absent `E`; **Pass** for an unrelated live row. | **Pass** when `K` occupancy/candidate relation is unchanged. |
| Reparent the protected entity `E` | **Block**: replaces the contested `E → P` edge. | **Block**: changes the staged candidate's required-parent relation. | **Block** if the request materializes `E`; otherwise **NA**. | **Conditional** when the reparent changes a concrete `K`; otherwise **NA**. |
| Tombstone/delete the protected entity `E` | **Block**: removes the staged conflicted entity/edge. | **Block** when it crosses the staged candidate contract. | **NA** for already-absent `E`; a generic un-tombstone/materialization is **Block**. | **Conditional** if deleting `E` withdraws or changes occupancy of `K`. |
| Tombstone/delete the protected parent or ancestor | **Conditional**: **Block** when the actual cascade reaches `E` or its contested edge. | **Conditional**: **Block** when the actual cascade crosses `E` or its required-parent frame. | **Conditional** only when the operation changes the protected absence/reference frame; it never authorizes materialization. | **Conditional** for every `K` whose occupancy is reached by the actual cascade. |
| Reparent an unrelated descendant or sibling | **Pass** when `I(m)` does not reach `E` or its contested edge. | **Pass** when `I(m)` does not change `E` or its required-parent frame. | **Pass** when live absence of `E` is unchanged. | **Conditional** if the descendant participates in `K`; otherwise **NA**. |
| Same-identity create/materialization of `E` | **NA** unless it would replace the existing protected subject, in which case it is routed as that structural mutation. | **NA** unless it bypasses the staged candidate relation, in which case the S1-PT policy evaluates it. | **Block**: ordinary create/update cannot materialize absent `E`. | **Conditional** if the materialization acquires or changes `K`; otherwise **NA**. |
| Change a TaskNote's constrained relation or occupancy | **NA** unless the change also alters the S1-CR protected edge. | **NA** unless the change also alters the S1-PT protected candidate relation. | **NA** unless the change materializes the absent identity. | **Block** when it changes the matching staged `K`; **NA** for another scope. |

The matrix is not a substitute for case-policy evidence. Its `Conditional` cells require the same impact expansion in §5, including old/new scopes, actual cascades, and each independently protected contract.

## 7. Overlapping-conflict routing and policy matrix

### 7.1 Router algorithm

**[D]** For a mutation intent `m`, the router performs this read-only sequence:

```text
1. Normalize request and derive I(m).
2. Load every unresolved StructuralConflict and ConstraintConflict record whose
   S(c), protected relation, lifecycle identity, or constraint scope may be reached by I(m).
3. Classify each record into its concrete shape and derive its protected
   subject/reference frame S(c).
4. Enumerate potentially relevant pairs in deterministic routing order:
   direct protected subject/reference frame, inherited or cascade-reached
   subject/reference frame, then other affected constraint scopes.
5. De-duplicate only identical (ConflictRecord, protected-contract) pairs;
   do not collapse different records or scopes merely because they share a kind.
6. Route every remaining pair to exactly one registered case policy.
7. Evaluate every applicable policy against the same immutable mutation intent.
8. Aggregate results deterministically by routing stage, logical scope key, then conflict id.
9. Return FencePassed, Blocked evidence, or Unsupported.
```

No step writes a domain row, changes a ConflictRecord, creates an evidence record, advances a baseline, retries a mutation, or changes the status of a conflict.

**[D] Routing order is not semantic priority.** The ordering above exists only to make discovery, de-duplication, and diagnostics deterministic. A direct match is not a winner over an inherited match; an earlier `Blocked` result does not permit the router to skip another applicable policy; and no conflict kind gains permission to suppress another kind's result.

### 7.2 Matrix rules

| Situation | Routing rule | Result rule |
|---|---|---|
| One relevant conflict | Route to that shape's policy. | Its result decides the fence contribution. |
| Several conflicts, different shapes | Route to every matching policy; do not select a winner by kind. | Any `Blocked` blocks the fence. All results are retained as evidence. |
| Several conflicts, same shape but different scope | Invoke the same policy once per record/scope. | Each protected contract is evaluated independently. |
| No relevant unresolved conflict | No fence policy is applicable. | `FencePassed`; ordinary authorization/validation still runs. |
| Unrecognized unresolved shape | No fallback “universal conflict policy.” | `Unsupported` and fail closed. |
| Duplicate/replayed unresolved scope | D9-T6 remains the authoritative staging rule. | This fence does not create supersede, reopen, or second-record behavior. |

The matrix has **no global priority order** such as `Constraint > Structural` or `ParentTombstoned > ConcurrentReparent`. Routing order is only for deterministic discovery, de-duplication, diagnostics, and reporting, never for skipping a policy.

### 7.3 Illustrative overlap cases

| Topology and intent | Impact/policies | Fence result |
|---|---|---|
| `A → T (S1-PT) → N (S1-CR)`; delete `A` | Actual cascade includes `T` and `N`; route both `ParentTombstoneFencePolicy(T)` and `ConcurrentReparentFencePolicy(N)`, plus any reached TaskNote constraint scopes. | Block if either protected contract is crossed. Do not report only the first kind. |
| Same topology; edit `N.Description` only | `I(m)` contains a non-structural field on `N`; it does not alter `T`'s relation or `N`'s contested structural edge. | Both policies may be `NotApplicable`/`Passed`; this is not a promise that later drift or business checks pass. |
| Same topology; delete `N` only | `I(m)` includes `N`'s lifecycle and its actual descendants. `S1-CR(N)` is relevant; `S1-PT(T)` is not automatically relevant solely because `N` is under `T`. | Block if the S1-CR contract is violated; inspect any reached descendant constraint scope independently. |
| A TaskNote conflict at scope `K`; delete its owning StudyTask | Actual cascade includes the note and withdraws/changes `K`. | Route the exact `ConstraintOccupancyFencePolicy(K)`; do not rely on “task is an ancestor” as the reason. |
| Two TaskNote conflicts at `K1` and `K2`; reassign a note `K1 → K2` | Both old and new constraint scopes are in `I(m)`. | Evaluate both policies; one pass cannot authorize the other. |

## 8. Decision and execution boundary

### 8.1 Boundary

```text
Application / business use case
        │  MutationIntent
        ▼
Mutation Router
        │
        ├─ Impact-set resolver
        ├─ Conflict-dependency selector
        └─ Case-policy router
                    │
                    ▼
              Policy result set
                    │
                    ▼
             Mutation decision
                    │
      remaining business authorization,
      validation, preconditions
                    │
                    ▼
             Mutation executor
                    │
                    ▼
                Repository
```

**[R] Direction:** application/business code must not send a protected mutation directly to a repository as though persistence decided the semantic case.

### 8.2 Responsibilities

| Component | Must do | Must not do |
|---|---|---|
| Application/business use case | Express intent, supply caller/business context, run normal business authorization and validation. | Bypass the router for a protected mutation. |
| Impact-set resolver | Derive rows, edges, constraint scopes, and lifecycle effects from a read model. | Mutate tracked entities or infer a generic tree lock. |
| Conflict-dependency selector | Find potentially relevant unresolved records. | Treat a proximity match as a block without the case policy. |
| Case policy | Evaluate one concrete protected contract and return evidence. | Call `SaveChanges`, alter domain rows, alter ConflictRecords, enqueue deferred work, or invoke repositories for mutation. |
| Decision assembler | Aggregate policy outcomes deterministically and expose missing checks. | Conflate `FencePassed` with execution authorization. |
| Executor | Revalidate time-sensitive prerequisites, start/own the transaction required by the operation, perform approved domain writes, and invoke persistence. | Invent a conflict result, rewrite immutable evidence, or silently bypass a blocked decision. |
| Repository | Persist an already-authorized execution plan. | Own conflict semantics, select a policy, or permit a new raw mutation bypass. |

### 8.3 Read-only policy/router invariant

**[R] Direction:** router and policy code must not mutate persistent state.

**[D]** This prohibition includes direct and indirect state changes: no domain writes, conflict status transitions, tombstones, baseline writes, retry records, pending/deferred mutation records, telemetry that changes semantic state, or hidden `SaveChanges` calls. Read-only observation may use a consistent read transaction or snapshot as needed; the executor must revalidate any state vulnerable to time-of-check/time-of-use change.

## 9. Local deletion uses the same fence

### 9.1 Entry-point rule

**[R]** A local delete/tombstone is a `MutationIntent` with the same impact model and the same case-policy registry as a sync-originated or application-originated mutation. `origin` is available for audit and UX, but is not a policy bypass key.

```text
Local delete request
    → derive actual cascade/dependency impact
    → select unresolved conflicts by protected contract
    → route every applicable policy
    → fence decision
    → remaining business/persistence gates
    → executor or rejection
```

The same rule applies to a local delete of an ancestor: the fence evaluates the actual rows, edges, and constraint scopes that would be touched by the cascade. It does not reject simply because a conflict appears somewhere below the ancestor, and it must not permit a cascade that crosses a protected contract merely because the request originated locally.

### 9.2 Rejection semantics

**[D]** A blocked fence decision has no hidden continuation:

- no partial tombstone;
- no implicit deferred mutation;
- no automatic retry after conflict resolution; and
- no substitute resurrection or reparenting.

If a future product capability requires queued/deferred structural mutations, it needs a separate contract and is outside this specification.

## 10. Restore is a future lifecycle flow

**[R]** T2.4 does not open generic resurrection to resolve an AL-PT/S1-PT problem or to work around a fence block.

**[D]** If Restore is later approved, it must enter through a distinct boundary:

```text
Application / business use case
    → LifecycleFacade
    → LifecycleRouter
    → RestoreFlow
    → lifecycle-specific decision and executor
    → repository
```

`RestoreFlow` requires its own owner decision record before implementation, covering at least identity evidence, parent/subtree rules, direct versus cascade tombstone provenance, sync baseline behavior, merge behavior, and coexistence with D9-T4. It is not an extra branch in `ConflictResolver`, `SyncApplySession`, or the ordinary create path.

## 11. Assumptions and open questions

### 11.1 Bounded assumptions

| ID | Assumption |
|---|---|
| A-1 | The current known hierarchy and TaskNote constraint scope are the routing inputs enumerated in §5; new relation types require an explicit impact entry. |
| A-2 | Existing D9-T6 scope identity remains the source for record uniqueness. This document does not redefine `ScopeKey` or `ConflictKey`. |
| A-3 | The executor can perform a final read/precondition check in its transaction where concurrency makes the earlier read decision stale. |
| A-4 | The repository can be made to accept an execution plan/capability rather than being a publicly available semantic bypass. The exact API is engineering design. |

### 11.2 Open questions

| ID | Question | Disposition |
|---|---|---|
| O-1 | Exact class/interface names, query shapes, transaction ownership, and error/result DTOs. | Engineering design; must preserve §§3–9. |
| O-2 | Exact policy for any new unresolved conflict shape or a new constraint type. | Require a new explicit policy and impact-matrix row; no default allow. |
| O-3 | Restore eligibility, provenance, parent/subtree behavior, and sync semantics. | Future owner lifecycle decision; out of T2.4. |
| O-4 | Any desired change to the frozen D9-T1/D9-T4 crossing behavior beyond this decision boundary. | Requires a dated owner amendment; W-2/F-2 alternatives are analysis, not authority. |

## 12. Explicit non-goals

This specification does **not**:

- amend D1–D9 or D9-T1…T6;
- redefine `ConflictRecord` as lifecycle history;
- add generic resurrection, un-tombstoning, or same-identity recreate;
- make all unresolved conflicts entity locks or subtree locks;
- introduce a universal `ConflictPolicy` with switch-based semantic fallback;
- assign priority among overlapping conflict kinds;
- permit a local delete to bypass the structural fence;
- create deferred/retry behavior after a fence rejection;
- redesign existing PR-6 resolution semantics, conflict evidence, or baseline rules; or
- prescribe UI prompts, user-facing wording, database schema, or implementation sequencing.

## 13. Acceptance criteria for a future implementation

A conforming implementation must demonstrate all of the following:

1. Each known unresolved shape in §4 maps to a separate registered policy.
2. Every mutation computes an impact set containing before/after edges, lifecycle effects, and reached constraint scopes.
3. Parent deletes include actual descendants, grandchildren, and TaskNote constraint effects when their cascade reaches them.
4. An overlapping case evaluates every relevant record; no global priority drops a policy.
5. An unrelated sibling mutation is not rejected only because another subtree contains an unresolved conflict.
6. A local ancestor delete and the equivalent non-local mutation reach the same fence policies for the same impact envelope.
7. A fence pass is followed by ordinary authorization, validation, persistence preconditions, and transaction/concurrency checks.
8. Router and policies have no persistent side effects; a blocked decision leaves no partial mutation or implicit deferred work.
9. An AL-PT same-identity materialization is not routed through normal create/update as an inferred new entity.
10. An unregistered unresolved shape fails closed and produces actionable evidence.
11. Every fence decision distinguishes the `ConflictRecord` from its shape-specific protected subject/reference frame and protected contract.
12. The routing traversal may be deterministic, but it evaluates all applicable contracts without assigning a global conflict-kind winner.
13. A successful resolution can transition `ConflictRecord` from unresolved to resolved without a domain-state mutation, including AL-PT `KeepBase` where live absence remains unchanged.

## 14. Final semantic rule

The system must answer the following two questions in order:

```text
1. What rows, relations, constraint scopes, and lifecycle facts can this mutation change?
2. For each unresolved conflict whose protected contract it reaches,
   does the mutation violate that specific contract?
```

It must not replace those questions with:

```text
Does any unresolved conflict exist somewhere near this entity?
```

That distinction is the contract of Policy-driven Mutation Routing + Structural Conflict Fence.
