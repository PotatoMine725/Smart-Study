# Slice-1 Review: Findings A (unreadable evidence / N-3) and B (Constraint otherwiseReached)

**Date:** 2026-09-14
**Author/agent:** Claude (background review job), no owner ruling made
**Scope:** Independent-reviewer hypotheses on the three Slice-1 files under review —
`ConflictShapeClassifier.cs`, `Policies/ConcurrentReparentFencePolicy.cs`,
`Policies/ConstraintOccupancyFencePolicy.cs` (branch `feat/epic2-fence-s1-policies`). Read-only
investigation; no code changed.

## Finding A — unreadable evidence / N-3

**Verdict: needs owner/plan decision (not a Slice-1 code defect, but N-3 is unimplementable under the
current Slice-1 interface as literally worded).**

Trace:
1. `ConflictShapeClassifier.Classify` never reads snapshot JSON — it only inspects row columns
   (`EntityId`, `FieldName`, `StructuralReason`, `ConstraintKey`/`ConstraintValue`,
   `Local/BaseEntityId` presence). Malformed *evidence JSON* cannot be detected here by construction.
2. Snapshot JSON is only parsed later, inside a policy's `Derive`, via
   `SnapshotField.ReadGuid` → `CanonicalJson.Read` (`ConcurrentReparentFencePolicy.cs:86-91`).
3. `CanonicalJson.Read` (`CanonicalJson.cs:125-133`) calls `JsonDocument.Parse` unguarded — malformed
   JSON throws `System.Text.Json.JsonException`; structurally-invalid-but-parseable JSON throws
   `SnapshotContractException` (many sites through the file). Neither is caught anywhere in the three
   reviewed files, in `IConflictFencePolicy`, or in `FencePolicyRegistry`.
4. There is **no Router in this codebase yet.** `FenceResults.cs:5-6` states outright:
   "`FenceRouter`/`FenceDecision` aggregation is Slice 2." The spec's own scope table confirms it —
   `docs/specs/...-spec-complete.md` §2.1: row S-2 (`ConflictShapeClassifier`, policies,
   `FencePolicyRegistry`) = Slice 1; row S-3 (`ImpactResolver`, `ConflictDependencySelector`,
   `FenceRouter`, `FenceDecision`) = Slice 2. `**/ImpactResolver*.cs` and `**/FenceRouter.cs` do not
   exist in the tree.
5. Plan §17 N-3: *"Unreadable evidence JSON in `Derive` (`SnapshotContractException`/`JsonException`)
   → policy returns `Unsupported` with `RuleId = "*.EvidenceUnreadable"`; never throws past the
   router."*

The literal contradiction: `Derive` returns `ProtectedContract` (`ProtectedContract.cs:37-45`), which
carries no `Outcome`/`RuleId` field. Only `PolicyResult` (`FenceResults.cs:12-20`, returned by
`Evaluate`) carries `Outcome`+`RuleId`. So a policy **cannot** "return `Unsupported` with
`RuleId = "*.EvidenceUnreadable"`" from `Derive` as the interface is currently shaped — N-3 names a
per-policy rule-id prefix (`S1CR.`, `S1PT.`, `ALPT.`, `CONS.` are all policy-owned prefixes elsewhere in
this codebase) but the only place that shape of value can be constructed today is a `PolicyResult`,
which nothing calls `Derive` and wraps yet.

Sub-cases inside "malformed/missing/wrong-type," which N-3 does not distinguish:
- **Missing** (`BaseSnapshotJson is null`) → `SnapshotField.ReadGuid` returns `null` legitimately
  (`HeldParentId` is `Guid?`). Not a defect, not N-3.
- **Malformed** (not valid JSON, or fails a `SnapshotContractException` rule) → uncaught exception
  propagates out of `Derive`. This is the actual N-3 case, and today nothing in Slice 1 catches it.
- **Wrong-type field** (JSON parses, but the field value isn't a `GuidValue`) → `ReadGuid` silently
  returns `null`, no exception, not covered by N-3's exception list at all. Checked whether this can
  flip an outcome: in CR/PT, `HeldParentId` is carried in `ProtectedContract.Edge` but never read by
  `Evaluate`, so no effect. In AL-PT it gates the `ALPT.FrameUnchanged` `Passed` branch
  (`AbsentLocalParentTombstonePolicy.cs:55-66`); a null `HeldParentId` degrades that branch to
  `NotApplicable` instead of `Passed` — both non-`Blocked`, so this does not fail open on protection.
  Worth a one-line note if Slice 2 revisits `Derive`, not a defect on its own.

**Precedent for where this belongs:** `SyncApplySession.cs:725-744` already does exactly this pattern
— wraps `CanonicalJson.Read` in `catch (SnapshotContractException)` / `catch (JsonException)` at the
**apply/orchestration layer**, with a comment explaining that the *reader* surfaces `JsonException`
by design and it is the caller's job to fold it into a fail-closed classification. That is the
established precedent for where N-3's catch-and-classify step belongs — an orchestration layer above
the pure `Derive`, which today is the not-yet-built `FenceRouter` (Slice 2) — but the rule-id-prefix
detail in N-3 ("`*.EvidenceUnreadable`", using each policy's own prefix) says the *value* should look
policy-owned, and nothing today lets the router produce a policy-prefixed rule id. **This seam
(does `Derive`'s contract change in Slice 2, or does `IConflictFencePolicy` grow a way to expose its
own prefix to a wrapping router, or does N-3's wording just mean "the router assigns a shape-derived
prefix") is unresolved and is an owner/plan decision, not something this review should settle.**

No test in `ConflictShapeClassifierTests.cs`, `ConcurrentReparentFencePolicyTests.cs`, or any Slice-1
policy test exercises malformed snapshot JSON through `Derive` — consistent with N-3 being out of
Slice-1's tested surface today.

## Finding B — Constraint `otherwiseReached`

**Verdict: confirmed spec-conformance deviation on `CONS.RelationUnchanged`; confirmed but currently
unreachable (latent) missing `EntityType` guard.**

### B.1 — `CONS.RelationUnchanged` is not in the canonical spec or plan

`ConstraintOccupancyFencePolicy.Evaluate` (`:63-84`) has two Passed branches from the row loop:

```csharp
if (tombstoned) return ...Passed... "CONS.EmptyScopeParentTombstoned"...
if (otherwiseReached) return ...Passed... "CONS.RelationUnchanged"...
```

- Plan §10.4's Passed row lists exactly one Passed rule: `CONS.EmptyScopeParentTombstoned` (OD-4
  ruling, 2026-09-14). `CONS.RelationUnchanged` does not appear anywhere in
  `docs/specs/2026-09-13-...-fence-spec-complete.md` or the plan.
- Plan §10.4's NotApplicable row: *"impact names neither K nor any candidate id; any other scope."*
  `CandidateIds` for this policy (`ConstraintOccupancyFencePolicy.cs:26-29`) are the TaskNote's
  Base/Local/Remote ids — **not** the owning task's id (`k.ScopeValue`/`T`). By the plan's own
  definition, an impact row on `T` with effect `FieldsChanged`/`Reparented`/`Created` (not a tombstone)
  names neither `K` (the `ScopeKey` string) nor any `CandidateId` → the plan's rule says
  `NotApplicable`. The implementation instead returns `Passed`/`CONS.RelationUnchanged`.
- `docs/specs/2026-09-14-...-owner-rulings.md` §5 acceptance criterion 2 names only
  `CONS.EmptyScopeParentTombstoned` as the Passed cell; it does not ratify a second Passed rule.

This is not proven to fail open (`Passed` and `NotApplicable` are both non-`Blocked` outcomes, so no
currently-protected write is let through that shouldn't be), but it is a real, unratified semantic rule
invented beyond what SB-3/OD-4 authorized, and per this task's governance constraints I am not
resolving it — it needs an owner decision: either ratify `CONS.RelationUnchanged` (and update spec
§10.4 + the owner-rulings doc), or collapse that branch to `NotApplicable` to match the plan as
written.

### B.2 — missing `EntityType` guard (latent, not provably reachable from Slice 1)

`ConstraintOccupancyFencePolicy.Evaluate`'s row loop matches only on `row.EntityId != k.ScopeValue`
(`:65-70`) — no `row.EntityType` check. Compare the sibling policies, which all pair type+id when
matching a row against a specific entity:
- `ConcurrentReparentFencePolicy.cs:45`: `row.EntityType != e.EntityType || row.EntityId != e.EntityId`
- `ParentTombstoneFencePolicy.cs:45`: same pattern
- `AbsentLocalParentTombstonePolicy.cs:48`: same pattern (first loop, direct-identity match)

`AbsentLocalParentTombstonePolicy.cs:60` has the **same gap** as Constraint: its second loop
(`if (row.EntityId != p) continue;`, matching the held-parent frame) also omits `EntityType`. So the
actual pattern is: the two policies that match a row against the *subject itself* (`EntitySubject`
in CR/PT, and AL-PT's first loop) check type+id; the two paths that match a row against a *foreign*
id referenced by the contract (Constraint's `ScopeValue`, AL-PT's `HeldParentId`) do not. Both
unguarded paths only produce non-`Blocked` outcomes (`Passed`/`NotApplicable`), so neither is a
fail-open on protection today, but both are inconsistent with the established defensive pattern and
would misattribute evidence (report "owning task tombstoned" for an unrelated entity) if triggered.

**On reachability:** the task asked to trace `ImpactResolver` and all construction paths for
`ImpactSet.Rows` to determine whether `EntityId == ScopeValue` is guaranteed to mean the owning
`StudyTask`. `ImpactResolver` does not exist yet (`**/ImpactResolver*.cs` returns nothing; it is
Slice 2, `docs/specs/...-spec-complete.md` §2.1 row S-3). In Slice 1, `ImpactSet` is exclusively
hand-constructed in unit tests (`ImpactSet.cs:6-7` comment), so there is no real resolver contract to
verify against yet. A counterexample is *constructible* at the unit-test level (e.g. an `ImpactRow`
with `EntityType = SyncEntityTypes.MonHoc` and `EntityId = T` would still trip `tombstoned`/
`otherwiseReached` and return `Passed`), proving the `Evaluate` method itself does not enforce the
type — but I cannot prove from Slice-1 code alone whether a real `ImpactResolver` could ever produce
such a row (GUIDs are per-row/per-table in this domain, so a genuine cross-type id collision is not
a plausible outcome of normal `Guid.NewGuid()` generation). **Report as a latent, currently-unreachable
gap** — worth closing for defense-in-depth and consistency with the sibling policies, not as a proven
live defect.

## Governance compliance

- No new semantic interpretation or owner ruling was made. Both findings above end at "owner/plan
  decision needed," not a resolution.
- No change to `docs/specs/2026-09-13-...-fence-spec-complete.md` or the owner-rulings doc.
- No Slice-2/SyncApply/PR-6 code was touched (`SyncApplySession.cs`/`SyncApplyModels.cs` were only
  read, as precedent evidence for Finding A).
- No code change made to any of the three reviewed files or their tests.

## If Finding B.1 is ratified as "collapse to NotApplicable" (smallest compliant fix, NOT yet approved)

- Delete the `otherwiseReached` branch and its `CONS.RelationUnchanged` result; let a non-tombstone
  row on `T` fall through to the existing `NotApplicable` return.
- New/changed test: an owning-task `FieldsChanged` (or `Reparented`/`Created`) row on `T` with no
  scope entry for `K` → assert `FenceOutcome.NotApplicable`, `"CONS.NotApplicable"`. This is the
  mutant that must go RED under "returns `Passed`" per this project's mutation-testing convention
  (plan §16, "Behavioural proof per cell").
- Update `docs/specs/...-owner-rulings.md` §5 only if the owner instead ratifies keeping
  `CONS.RelationUnchanged` — in that case add it as a named, ratified Passed rule with its own test,
  rather than leaving it undocumented.

## If Finding B.2 is approved for a fix (smallest compliant fix, NOT yet approved)

- Add `row.EntityType != SyncEntityTypes.StudyTask` to the `continue` guard in
  `ConstraintOccupancyFencePolicy.Evaluate`'s row loop (owner should confirm hardcoding
  `SyncEntityTypes.StudyTask` vs. deriving the expected type some other way — both are testable;
  existing tests already use `SyncEntityTypes.StudyTask` for the owning-task rows,
  `ConstraintOccupancyFencePolicyTests.cs:74,121`).
- Same guard in `AbsentLocalParentTombstonePolicy.cs:60`, if the owner wants the two foreign-id match
  sites treated consistently in the same pass.
- New tests: an `ImpactRow` with a non-`StudyTask` `EntityType` and `EntityId == ScopeValue`/`HeldParentId`
  should not trip the Passed branches; assert `NotApplicable` instead.

## Follow-ups (non-blocking)

- Finding A's seam (how a policy-prefixed `*.EvidenceUnreadable` `PolicyResult` gets produced from an
  exception thrown inside a `ProtectedContract`-returning `Derive`) should be resolved before or
  during Slice 2 design, not deferred silently — otherwise Slice 2's `FenceRouter` may be built
  against an interpretation of N-3 nobody signed off on.
- Recommend the owner also decide, in the same pass as B.1, whether other row-effect combinations on
  a Constraint scope's owning task (e.g. `Reparented`) should ever be distinguishable from
  `FieldsChanged`/`Created` — the current `otherwiseReached` branch treats them identically, which may
  or may not be intended even if the branch is kept.

## Decisions made (ADR-style)

**No semantic or specification decisions were made in this review — by design (governance
constraint).** The only decisions were about review method, recorded here per project convention:

### Decision: verify N-3 against the literal `IConflictFencePolicy` interface shapes, not against the plan's prose alone
- **Why it had to be made:** the plan's N-3 row uses language ("policy returns `Unsupported`") that
  reads naturally as a `Derive`-level behavior, but `Derive`'s return type cannot carry that value.
  Taking the prose at face value would have produced a wrong verdict ("Slice 1 is missing a
  try/catch in `Derive`").
- **What it's for:** distinguishing "this is a Slice-1 bug" from "this is a real spec-vs-interface
  seam that has not been designed yet" — the two require different owner actions.
- **Experience it draws on:** project memory `feedback_reproduce_before_escalating` — reproduce/trace
  the actual contract before escalating a suspected gap; and `feedback_verify_signal_can_fail` — a
  hypothesis that "sounds right" from prose needs to be checked against what the code can actually
  return, not just whether a test currently passes.

### Decision: treat the missing `EntityType` guard (B.2) as latent/unreachable rather than a confirmed live defect
- **Why it had to be made:** the task asked for "a concrete counterexample if the current
  implementation can return Passed for an unrelated entity." A counterexample is trivially
  constructible as a unit test, but `ImpactResolver` (the only real producer of `ImpactSet` in
  production) does not exist yet, so claiming a live exploit would overstate the evidence.
- **What it's for:** keeping the report's severity claim scoped to what Slice 1 alone can prove,
  consistent with this project's evidence-scoping discipline, while still flagging the gap so it is
  not silently carried into Slice 2's `ImpactResolver` design.
- **Experience it draws on:** project memory `feedback_verify_signal_can_fail` — "không test được"
  and "chưa reachable" are still costs to name explicitly, not silence; and `feedback_manual_check_setup`
  — distinguish observation (what the code does when called this way) from inference (what a real
  resolver could ever produce).
