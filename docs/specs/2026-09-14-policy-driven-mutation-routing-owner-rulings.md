# T2.4 Structural Conflict Fence — Owner Rulings: SB-3/OD-3, OD-4, OD-2, OD-7

| | |
|---|---|
| **Date** | 2026-09-14 |
| **Status** | SB-3/OD-3, OD-4, OD-2, OD-7: **CLOSED — owner ruling** |
| **Goal** | Record, in one dated tracked file, the four owner rulings that close the open semantic/integration choices of the fence implementation plan, so no slice has to infer them |
| **Scope** | T2.4 Policy-driven Mutation Routing + Structural Conflict Fence, implementation boundary only |
| **Closes** | §20 rows SB-3/OD-3, OD-2, OD-4, OD-7 of `docs/plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md` |
| **Narrows (SB-3 only)** | `docs/specs/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-spec-complete.md` §4 (S1-CR and Constraint "not protected" cells), §4.1 bullet 2, §6 row 1, §7.3 row 2, for a row held at Base, by the spec's own §2.2 precedence rule. **The spec file is not edited** (§4 below) |
| **Does not amend** | D1–D9 or D9-T1..D9-T6 of `docs/specs/T2.3-T2.4-D1-D9-Decision-Record-updated.md`; `docs/specs/T2.3-T2.4-D4-D9-T4-Amendment-2026-09-10.md`; `docs/specs/T2.4-PR5-ContextLifetime-Amendment-2026-09-11.md`; `docs/specs/T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md` (B-1..B-4, E-3) |
| **Leaves open** | **SB-2/OD-1** (sync-origin fence = W-2 option C1; gates Slice 6). OD-5, OD-6, OD-8 (plan §20) |
| **Implemented by** | Plan Slices 1, 4, 5 (not yet implemented). This file changes no code and no test |

This file follows the pattern of the PR-6 rulings record. It is the dated owner record for choices the
plan surfaced but could not make. Labels: **[R]** owner ruling (verbatim below) · **[D]** derived from a
ruling plus tracked text, not itself a ruling · **FACT** read in the tree at `origin/dev` `c2931ca` ·
**INFERENCE** code/text reading, not run.

---

## 1. The rulings (verbatim)

> **SB-3 / OD-3 — Non-structural edit on a held-at-Base unresolved row.** Ruling: BLOCK.
> When an unresolved StructuralConflict/ConstraintConflict holds an entity row at Base, a mutation to
> that same entity row is blocked even if the mutation is non-structural and does not change the
> contested structural relationship.
> Do not introduce field-scoped D9-T1 semantics. Do not reinterpret D9-T1 as protecting only the
> contested structural fields. The purpose is to preserve the existing whole-row `live == Base` contract
> while unresolved and prevent W-1 drift that would make the conflict permanently unresolvable.
> This ruling is semantic. Do not attempt to solve it by changing PR-6 fingerprint semantics.
>
> **OD-4 — Empty Constraint scope + parent tombstone.** Ruling: PASS, subject to normal evaluation of any
> other protected contracts actually affected by the mutation.
> If a ConstraintConflict scope has no occupant and a parent tombstone does not alter the protected
> occupancy/state of that empty scope, the empty Constraint scope itself does not block the parent
> tombstone.
> Do not turn ConstraintConflict into a generic subtree lock. Do not infer that every descendant of a
> conflicted entity is automatically protected. The actual mutation impact and protected-contract
> applicability still determine whether another conflict blocks the same mutation.
>
> **OD-2 — Local save integration shape.** Ruling: L1.
> Keep the existing application/repository port. Do not introduce a new application-service/use-case
> layer solely for T2.4 fence integration. The intended local path remains conceptually:
> `existing local save entry point -> reconcile/planning -> mutation intent / impact derivation ->
> read-only fence decision -> existing write/executor path`.
> The fence must not become a repository-side hidden policy. Do not perform an unrelated
> application-layer architecture refactor.
>
> **OD-7 — Rejected local mutation and stale in-memory graph.** When a local mutation is rejected by the
> structural conflict fence:
> `fence rejects -> attempted persistence is not committed -> in-memory graph must be brought back into
> consistency with persisted state -> rejection is surfaced to the caller/UI path -> no automatic retry`.
> The implementation may choose the safest existing mechanism for restoring the graph, but it must
> preserve the semantic contract above. In particular, prevent the X-20 sticky-rejection scenario where an
> already-rejected deletion remains absent from the in-memory semester graph and causes an unrelated
> later save to submit the same rejected deletion again.
> Do not invent a new UI/UX framework. If the exact call-site mechanism is an implementation detail still
> requiring engineering work, record that as such rather than inventing it.

---

## 2. Ruling records

### 2.1 SB-3 / OD-3 — CLOSED: Block

| Field | Content |
|---|---|
| **ID** | SB-3 / OD-3 |
| **Status** | `CLOSED` 2026-09-14 |
| **Owner ruling** | **Block.** A mutation to an entity row that an unresolved StructuralConflict/ConstraintConflict holds at Base is `Blocked`, including a non-structural field edit that leaves the contested relation unchanged |
| **Which rows are "held at Base"** [D] | Shapes whose staging rewrites a live row to its whole Base row (plan §4.3, PR-6 DoR §2.1): **S1-CR** (`E`), **S1-PT** (`E`), **S2** (`N0`; unreachable via sync in v1 by construction). **AL-PT** and **S3** hold no live row, so this ruling does not apply to them; their existing rules stand (`ALPT.*`, `CONS.ScopeAcquired`) |
| **Semantic rationale** | D9-T1 (frozen): while Unresolved, "the live domain state **must equal Base**". Shipped and tested PR-5 reads that as the **whole row**: it rewrites the entire row to Base (`SyncApplySession.cs:342-345`) and test L asserts the non-structural `TenTask` is rolled back (`SyncApplyParentHandlingTests.cs:48-103`; W-2/F-2 analysis §3.4 D-a "Against it"). A local field edit on the held row breaks `live == Base` and changes the D8-H fingerprint (MEASURED, PR-6 DoR §10.3 F-1 leg 1). D8-H then rejects every resolution kind, with no reopen or supersede (D7-D, D9-T6). The record becomes permanently unresolvable (W-1). Blocking the edit keeps the frozen contract intact without touching resolution |
| **Relationship to the fence spec** | The spec's plain text makes this edit **Pass** (§6 row 1; §4.1 bullet 2). Spec §2.2 says: "Where this specification and a frozen D1–D9/D9-T1…T6 rule appear to conflict, the frozen record wins." The ruling applies that precedence. It narrows the spec cells in §4 below for held rows only. The spec file is unchanged |
| **Implementation consequence** | (a) Plan §10.1 `S1CR.NonStructuralFields`, §10.2 `S1PT.NonStructuralFields`, §10.4 `CONS.OccupantContent` return **`Blocked`**. (b) P-CR-6, P-PT-6, P-K-4 expect `Blocked` and are ruling tests, not characterization. Their mutant is "the policy returns `Passed` for `UpdateFields`/`OccupantContentChanged` on the held row". (c) Plan §11.2 aggregates for "edit N non-structural fields" and "edit N's note content (S2)" become **Blocked**. (d) `FencePendingDecisions` has no remaining pending outcome and is dropped from the plan. Policies encode the ruled outcome directly (plan §10.0) |
| **Consequences recorded, not new decisions** [D] | (1) Plan R-4 (W-1 via local content edits on held rows) is prevented on the local path once Slices 4–5 wire the fence. W-1 reached by other paths is unchanged: the W-2 sync cascade stays gated on OD-1, and D-4 (resolver + stale UI graph) acts after resolution. (2) Under L1 (OD-2) a local save is a whole-semester compound save. One field edit on a held row therefore rejects the entire save. That raises the stakes of OD-7's restoration, alongside R-6/OD-8. No new OD is opened |
| **Explicitly out of scope** | Field-scoped D9-T1 (W-2 Q-2 / D-a). Any change to `ConflictResolver`, the resolution fingerprint (E-4), D8-H drift semantics, `ConflictRecord` lifecycle, or PR-6 persistence. A lock on E's siblings, ancestors, descendants, or subtree: a child create/delete/reparent that names E only as a parent endpoint stays `Passed` `*.ChildEdgeOnly` (P-CR-5, P-PT-5, §11.2 "delete N" vs S1-PT(T)). Edits to unrelated rows (spec §6 row 1 "unrelated row" cells stay Pass) |
| **Affected plan sections / tests** | §3.1, §10 intro, §10.0, §10.1, §10.2, §10.4, §11.2, §14, §15.1, §16.1 P-CR-6 / P-PT-6 / P-K-4, §19 R-4, §20, §22, §25, §26 |
| **Amends frozen D1–D9?** | **No amendment to frozen D1–D9.** It applies D9-T1 as already interpreted and tested by PR-5 |

### 2.2 OD-4 — CLOSED: Pass

| Field | Content |
|---|---|
| **ID** | OD-4 |
| **Status** | `CLOSED` 2026-09-14 |
| **Owner ruling** | **Pass**, subject to normal evaluation of every other protected contract the mutation actually affects |
| **Semantic rationale** | An S3 ConstraintConflict protects occupancy of scope `K = TaskNote|MaTask=t`: under D9-T1 with a null Base, the scope has no live row (plan §10.4). Tombstoning the owning task neither acquires nor releases an occupant of an empty scope, so the protected occupancy/state is unchanged. Spec §5.3 item 4: constraint scopes are added to impact "because the operation changes their occupancy, not merely because the entity is descended from a conflicted row". Spec §4, Constraint row: `K` is "not every TaskNote or every task descendant" |
| **Implementation consequence** | (a) Plan §10.4 `CONS.EmptyScopeParentTombstoned` returns **`Passed`** as a ruled outcome, not a pending switch. (b) P-K-5 is a ruling test. Its mutant is "the policy returns `Blocked` for an owning-task tombstone over an empty S3 scope". (c) The `Passed` is **this Constraint policy's contribution only**. The same tombstone intent is still routed to every other applicable policy (plan §11.3, INV-4). Examples: an S1-CR/S1-PT record on that task or on a cascading ancestor ⇒ `Blocked`; an occupied (S2) scope released by the cascade ⇒ `Blocked` `CONS.ScopeReleased` (P-K-2, P-K-3) |
| **Consequence recorded, not a new decision** [D] | After the owning task is tombstoned, only a no-write `KeepBase(null)` can resolve the S3 record (existing PR-6 ruling B-2; plan §20 OD-4; ZPT2). This ruling does not change B-2 |
| **Explicitly out of scope** | A descendant/subtree exemption: other contracts on the task or its descendants are evaluated normally. Treating ConstraintConflict as a subtree lock. Any `Blocked` reading of "candidate made unmaterialisable". Any change to PR-6 resolution rules |
| **Affected plan sections / tests** | §10 intro, §10.0, §10.4, §16.1 P-K-5, §20, §22, §26 |
| **Amends frozen D1–D9?** | **No amendment to frozen D1–D9.** |

### 2.3 OD-2 — CLOSED: L1

| Field | Content |
|---|---|
| **ID** | OD-2 |
| **Status** | `CLOSED` 2026-09-14 |
| **Owner ruling** | **L1.** Keep the existing application/repository ports (`IHocKyRepository.LuuHocKyAsync`, the four `ITaskEditorRepository` mutation methods). No new application-service/use-case layer for fence integration. Plan §12.2 alternative L2 is **not adopted** |
| **Semantic rationale** | Fence placement is engineering (spec O-1, A-4). The spec's boundary is conceptual: `application → router/policies → decision → executor → repository`. L1 realises it behind the existing port without migrating the 7 VM call sites. The plan already prices L1: HIGH blast radius on `LuuHocKyAsync`, mitigated by the behaviour-preserving extraction in Slice 3 |
| **Implementation consequence** | The local path is: existing port → `LocalSemesterSaveExecutor`/`LocalTaskEditorExecutor` → planner (intents) → `FenceRouter` (read-only decision) → gates → writer → save/commit (plan §5). Plan boundaries that keep the fence from becoming a **repository-side hidden policy** [D]: fence semantics live only in `Sync/Fence/` (pure policies, `AsNoTracking` readers; plan §13). Repository classes delegate and contain no reconcile or conflict logic (plan §13, X-19). The decision is an explicit `FenceDecision` value. A rejection is surfaced, never swallowed (OD-7; plan §17 N-10) |
| **Explicitly out of scope** | Any application-layer architecture refactor. VM call-site migration to use cases. `ServiceLocator` changes (under L1 executors are built by the repositories from the same factory; plan §15.2). Renaming the "repository" port (plan §12.2 follow-up, not in this scope) |
| **Affected plan sections / tests** | §5, §12.2, §20, §21 (Slice 4 gate), §24.2 A4, §26 |
| **Amends frozen D1–D9?** | **No amendment to frozen D1–D9.** |

### 2.4 OD-7 — CLOSED: reject + restore in-memory graph consistency + no automatic retry

| Field | Content |
|---|---|
| **ID** | OD-7 |
| **Status** | `CLOSED` 2026-09-14 |
| **Owner ruling** | On a fence rejection of a local mutation: (1) the attempted persistence is **not committed**; (2) the in-memory graph is **brought back into consistency with persisted state**; (3) the rejection is **surfaced** to the caller/UI path; (4) there is **no automatic retry**. Reject ≠ retry |
| **Semantic rationale** | Spec §9.2 already forbids partial tombstones, deferred mutations, automatic retry, and substitute reparent/resurrection. On the local path, planning is a whole-graph diff (plan §4.1, §12.2). A rejected deletion left absent from the VM's `HocKy` graph is re-derived as the same `Tombstone` intent on every later save, so an unrelated sibling edit is refused too (sticky rejection, plan R-3 / X-20). That would make spec §13.5 false on the local path. Restoring consistency removes the stale intent at its source. "No automatic retry" keeps the restoration from turning into a resubmission |
| **Implementation consequence** | (a) Slice 4 must implement restoration and surfacing. X-20 becomes an acceptance test with one expected outcome: after the rejected save the in-memory graph matches persisted state, and a later unrelated sibling edit + save persists while the rejected deletion is **not** resubmitted. Mutant: remove the restoration step ⇒ X-20 RED. (b) Spec §13.5 on the local path becomes a Slice-4 exit criterion, not a conditional. (c) The executor stays stateless and records nothing about the rejected request (plan §12.2, X-13) |
| **Consequence recorded, not a new decision** [D] | The rejected save is atomic (D8-G; plan INV-12), and the graph is restored to persisted state. Other edits bundled into the same rejected compound save are therefore not persisted and not automatically re-applied: re-applying them would be an automatic retry of part of the rejected request. How the user is told is UX wording, which is not ruled (spec §12) |
| **Engineering detail, still open (not ruled)** | The restoration **mechanism** and exact call sites, chosen in Slice 4 as "the safest existing mechanism". The plan's earlier candidate stays a candidate: catch `MutationRejectedException` at the existing save commands, reload the graph from the repository, surface the rule ids. The surfacing channel is also open: whether the existing `App.DispatcherUnhandledException` path is enough, given the unverified `args.Handled` pre-check in plan §4.4 |
| **Explicitly out of scope** | Any XAML change. Any new UI/UX framework, prompt design, or wording. Unrelated ViewModel architecture changes. Queued or deferred mutations. Retry after resolution. Swallowing the rejection |
| **Affected plan sections / tests** | §2.2, §12.2, §15.2, §15.3, §16.2 X-20, §17 N-10, §19 R-3, §20, §21 (Slice 4), §22 item 5, §24.2 A4, §26 |
| **Amends frozen D1–D9?** | **No amendment to frozen D1–D9.** |

---

## 3. Guardrails that stay explicit

These restate the spec and plan. They are listed because each ruling above must be read inside them.

| # | Guardrail | Source |
|---|---|---|
| A | **Selection is not violation.** The dependency selector may over-approximate candidates. Candidate selection ≠ policy applicability ≠ protected-contract violation. Ancestor/descendant/dependency proximity never blocks by itself; the concrete policy decides | spec §3.3, §7.2; plan §8, INV-3 |
| B | **No global conflict priority.** No `ConstraintConflict > StructuralConflict`, no `ParentTombstoned > ConcurrentReparent`. Every applicable policy is evaluated. One `Blocked` blocks the mutation, and every other result is kept as evidence | spec §7.1–§7.2; plan §11.3, INV-4, X-4 |
| C | **Structural closure is not a global lock.** An unresolved conflict does not lock its subtree, ancestors, siblings, or descendants. Mutation impact + protected contract = fence decision. SB-3 locks only the held row itself; OD-4 exempts only the empty scope itself | spec §4.1, §5.3; plan INV-1 |
| D | **No PR-6 reopening.** None of these rulings changes `ConflictResolver`, the resolution fingerprint (E-4), D8-H, the `ConflictRecord` lifecycle, PR-6 persistence, or B-1..B-4/E-3 | PR-6 rulings; plan §2.2 |

---

## 4. Fence spec cells narrowed by SB-3 (spec text unchanged)

Where spec and frozen record appear to conflict, spec §2.2 makes the frozen record win. The cells below
are read for v1 as stated. Every other spec cell is unaffected.

| Spec location | Spec text (abridged) | v1 reading under SB-3 |
|---|---|---|
| §4, S1-CR row, "Explicitly not protected" | "ordinary non-structural fields solely because `E` has a conflict" | Field edits on held `E` are blocked. The reason is D9-T1 whole-row `live == Base`, not proximity |
| §4, Constraint row, "Explicitly not protected" | "Ordinary properties which do not affect `K`" | Blocked for the held S2 occupant row `N0`. Other rows' properties are unaffected |
| §4.1, bullet 2 | "An ordinary content edit can be fence-passing…" | Not fence-passing on a held-at-Base row. Still fence-passing elsewhere |
| §6, row 1, S1-CR / S1-PT / Constraint | **Pass** when edge / parent validity / occupancy unchanged | **Block** when the edited row is the held-at-Base row. The "unrelated row" parts stay **Pass**. AL-PT cell unchanged |
| §7.3, row 2 | edit `N.Description` only: "Both policies may be `NotApplicable`/`Passed`" | S1-CR(N) returns `Blocked`. S1-PT(T) is unchanged |

OD-4, OD-2 and OD-7 narrow no spec cell. OD-4 is the literal §5.3/§6 reading. OD-2 is engineering placement
under §8 / O-1 / A-4. OD-7 operates inside §9.2 and §12 (no UI prescription).

---

## 5. Acceptance criteria (checkable against the implementing slices)

1. `S1CR.NonStructuralFields`, `S1PT.NonStructuralFields`, `CONS.OccupantContent` return `Blocked`. P-CR-6, P-PT-6 and P-K-4 assert it, and each goes RED under the mutant "returns `Passed`".
2. `CONS.EmptyScopeParentTombstoned` returns `Passed`. P-K-5 asserts it and goes RED under "returns `Blocked`". P-K-2 and P-K-3 (occupied scope) still expect `Blocked`.
3. No `FencePendingDecisions` type (or any equivalent "pending" switch for SB-3/OD-4) exists in `Sync/Fence/`.
4. No field-scoped D9-T1 logic, and no diff to `ConflictResolver.cs`, `ConflictStaging.cs`, or the fingerprint/D8-H code in any fence slice.
5. VM-facing port signatures are unchanged. No new application-service layer. No reconcile/conflict logic in `Repositories/` (X-19).
6. X-20 passes with restoration: rejected deletion not resubmitted, later sibling edit persisted. Removing the restoration step turns it RED.
7. No XAML diff. No automatic retry or deferred record after rejection (X-13, N-10).
8. Slice 6 is not dispatched until an OD-1 ruling is recorded in `docs/specs/`.

---

## 6. Non-goals of this record

- No production code, test code, schema, EF model, or UI change.
- No closure of SB-2/OD-1, OD-5, OD-6, or OD-8.
- No edit to the canonical fence spec, the frozen Decision Record (either copy), the D4/D9-T4 amendment, the PR-5 amendment, or the PR-6 rulings.
- No ruling on W-1 for paths other than local held-row edits (W-2 sync cascade; D-4).
- No restoration mechanism, UI wording, or call-site design (OD-7 engineering detail).

---

## 7. What these rulings do not decide (engineering residuals)

| # | Residual | Status |
|---|---|---|
| E-1 | OD-7 restoration mechanism, call sites, surfacing channel | Engineering, Slice 4 |
| E-2 | **Derived-only saves on a held row.** FACT: plan §6 derives no intent from Derived fields (`DiemUuTien`, `MucDoCanhBao`, `IsSeeded`; D9-T3, `MergeSurfaceRegistry.cs:63,83-84`), and Derived columns are excluded from snapshots (`EntitySnapshotMapper.cs:167-171`). NOT VERIFIED: whether such a save still stamps provenance (`Rev`/`ModifiedAtUtc`) on the held row. If it does, it drifts the D8-H fingerprint, which includes provenance (W-2/F-2 §3.4; `ConflictStaging.cs:185-186`), with no intent to route. The existing no-change resave test does not cover a Derived-only change | Measure in Slice 0/4. Report the finding; do not decide it in code |
| E-3 | Rule-id names (the existing `*.NonStructuralFields` ids are kept, now as `Blocked` rules) | Engineering naming |

---

## 8. Consistency check performed for this record (stop conditions)

| Check | Outcome |
|---|---|
| Any ruling contradicts D9-T1..T6 | No. SB-3 applies D9-T1's shipped whole-row reading. OD-4 is consistent with D9-T1 null-Base. OD-2 and OD-7 are outside D1–D9 |
| SB-3 needs a PR-6/D8-H change | No. Blocking prevents the drift; resolution is untouched |
| OD-7 needs a new semantic contract | No. It restates spec §9.2 plus graph consistency; the mechanism stays engineering (§7 E-1) |
| The canonical spec requires a different owner decision | Its §6 row 1 / §4.1 text reads Pass. Its §2.2 precedence resolves the tension toward the frozen record, which is exactly the SB-3 framing in plan §20 that the owner ruled on. Recorded in §4, spec not edited |
| Production/test code required | No |
| Another unresolved semantic conflict required to close these | None found. E-2 is an unmeasured engineering residual, not a precondition of the ruling |
