# Epic 2 / T2.4–T2.5 — W-2 / F-2 Semantic Analysis: D9-T1 ("live = Base while Unresolved") vs D9-T4 / cascade ("no live child under a tombstoned parent")

> **Status: analysis only. No production code changed. No owner ruling made, proposed as made, or
> implied.** This document reconstructs the W-2/F-2 collision, measures it, and lays out candidate
> resolutions with their consequences so the owner can rule. Recommendations appear only as
> engineering trade-offs (§10) and are not a selection.
>
> Labels: **FROZEN TEXT** (quoted from a ratified source) · **FACT** (read from the tree at the
> baseline) · **MEASURED** (a command was run in this session and its output observed) ·
> **INFERENCE** (composition by the author; not measured) · **UNRESOLVED** (the documents do not
> determine it).

| | |
|---|---|
| **Date** | 2026-09-13 |
| **Baseline** | `origin/dev` = `d0de8534cb8e1013a9ee3406be9b09df7c865a2c` (Merge PR #87). Local `dev` was equal to `origin/dev` at fetch time; not touched |
| **Worktree / branch** | `.claude/worktrees/w2-f2-analysis`, `docs/epic2-w2-f2-semantic-analysis`, created from the fetched `origin/dev` |
| **Scope reviewed** | W-2/F-2 only. `SyncApplySession.ApplyEntityAsync`, `CascadeTombstoneAsync`, `TombstoneCascadeChildAsync`, `StructuralScopeKeyOf`; `ConflictResolver.ResolveInTransactionAsync`; `ThreeWayMerge.MergeTombstones`; `SyncBaseFingerprint` |
| **Authoritative sources** | `docs/specs/T2.3-T2.4-D1-D9-Decision-Record-updated.md` (§23A D9-T1..T6, D4, D6-H, D8-G, D8-H, §17) · `docs/specs/T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md` (B-1..B-4, E-3) · `docs/review/2026-09-11-t2.4-pr6-conflict-resolver-dor.md` (§2.1, §7, §10, §11, §15) · `docs/review/2026-09-12-t2.5-recon.md` (§3 P6/P10, §6.2, §8 P-3) |
| **Supporting sources (cited, not authoritative for W-2)** | `docs/specs/T2.3-T2.4-D4-D9-T4-Amendment-2026-09-10.md` (owner-ratified; its last sentence is load-bearing, §1.2) · `docs/review/2026-09-08-t2.3-t2.4-engineering-dor.md` §5.3, §12.2–§12.5, §13 · `docs/architecture/data-model.md` §3–§4 |
| **Out of scope** | W-1/F-1 and W-3/OPEN-2, except where a W-2 option directly changes them (flagged inline) |
| **Verdict** (README vocabulary, applied to *writing the T2.5 implementation plan*) | **`ship-with-followups`**. The collision is real and now **MEASURED**. T2.5 planning may proceed under recon P-3's scoped wording (P6 excludes cascade crossings; P10 is characterization). The follow-up is the owner ruling in §9. Nothing in this document is a ruling |

---

## 1. Exact conflict statement

### 1.1 The two rules, verbatim

**FROZEN TEXT — D9-T1** (Decision Record §23A, lines 1259–1265):

> For `StructuralConflict` and `ConstraintConflict`, while the conflict is `Unresolved`, the live
> domain state **must equal Base**. The conflict candidates are not allowed to remain materialised as
> competing live domain state. `ConflictRecord` is the staging boundary holding the recoverable
> Base/Local/Remote evidence until explicit resolution.
>
> This is a semantic invariant; the concrete staging mechanism is engineering design, subject to the
> Epic-1 tombstone/no-hard-delete constraints and the existing `TaskNote` uniqueness invariant.
>
> For a null Base, "live = Base" means that the conflict scope has no live domain row. […]

**FROZEN TEXT — D9-T4** (Decision Record §23A, lines 1304–1308):

> If a structural target required by `D4` exists only as a tombstone, the incoming child/relationship
> is **not** materialised as a live orphan. The apply outcome is a `StructuralConflict`.
>
> A missing required parent remains a fail-closed/deferred apply concern governed by apply ordering; no
> implicit live orphan is permitted.

### 1.2 What "D9-T4" means in the collision (a wording fact, not a reinterpretation)

The PR-6 DoR (§11.3) and the T2.5 recon (§6.2) name the collision "D9-T1 vs D9-T4 / the Epic-1
cascade". The rule that the cascade actually enforces is spread across four texts, and they are not
equally strong:

| Text | Status | What it says | Covers the cascade of an *existing* local child? |
|---|---|---|---|
| D9-T4 (§23A) | owner-ratified | the **incoming** child is not materialised as a live orphan; outcome is a StructuralConflict | **Not by its words.** It speaks of the incoming child at apply |
| D4/D9-T4 Amendment 2026-09-10, final sentence | owner-ratified | *"it does not change the meaning of D9-T4 that a tombstoned structural parent can never have a live child"* | **Yes** — universal form |
| D8-G (Decision Record lines 836–850) | owner-frozen | *"StudyTask tombstone + dependent cascade tombstones = one logical operation = one transaction"* | Yes — assumes the cascade is part of the operation |
| Engineering DoR §12.3 | [ENG], **ACK A2-b** | the session loads the parent's live local children by FK and tombstones them in the same transaction | Yes — the mechanism |
| `data-model.md` §4 | Epic-1 architecture | *"Deleting a subject tombstones its tasks (+ their notes + links)"* | Yes — the domain rule |

**FACT.** The universal "no live child under a tombstoned structural parent" is owner-ratified text,
through the amendment's last sentence. It is not an engineering extrapolation. This document
therefore uses the name "D9-T4" for that bundle, as the DoR and the recon do. It does **not** treat the
narrower wording of §23A D9-T4 as a way out. That reading is examined, and set aside, as option D-c
(§3.6).

### 1.3 The collision, stated exactly

> Take a Structural (or Constraint) ConflictRecord that is `Unresolved`, and whose scope holds a live
> row at Base. Suppose that row's **Base** structural parent becomes tombstoned. Then D9-T1 requires
> the row to stay equal to Base, which is live, and D9-T4 (amended meaning) + D8-G + Epic-1 require it
> to be tombstoned. **No live state satisfies both.** The shipped code picks D9-T4 (§2).

---

## 2. Reproduced state transition — **MEASURED**

### 2.1 How it was measured

Both previous documents label F-2 **INFERENCE + recipe**. The PR-6 DoR did so because its task
forbade test code. This task forbids only production changes and committing anything except this
file. So the DoR §11.3 recipe was run as a **throwaway xunit probe** in this worktree, at the baseline
above, against unmodified production code. The probe was then deleted. `git status --porcelain` was
empty before this document was written.

Command: `dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter
"FullyQualifiedName~W2ProbeTests" --logger "console;verbosity=detailed"` ⇒ **3 passed, exit 0**. The
probe body is summarised in Appendix A so that it can be re-created.

**The probe can tell the two cases apart.** The control leg and the crossing leg share the same
staging. They differ in exactly one input, the remote tombstone of the Base parent, and they give
opposite results on every assertion. A third leg tombstones T directly and gives the scope-lock
result.

### 2.2 Setup (identical for all three legs)

`SyncApplyFixture`, with distinct device ids (`LOCAL-DEVICE`, `PEER-DEVICE`, `RESOLVER-DEVICE`):

1. Seed `HocKy H → MonHoc A → StudyTask T` through the ordinary local path. Add live `MonHoc B` and
   `MonHoc C` under `H`.
2. Baseline for the peer: `T` (parent A) and `A`.
3. Local edit: `T.MaMonHoc = B`.
4. Remote change set: `T` with `MaMonHoc = C`. ⇒ **MEASURED** `StudyTask T = ConflictStaged`, an
   S1-CR record (ConcurrentReparent). `T` is rewritten to the whole Base row (`MaMonHoc = A`). The
   record is `Unresolved`.

### 2.3 Transition table

| Step | Input | Observed state | Label |
|---|---|---|---|
| S0 | after staging | `record.Status = Unresolved`; `T.IsDeleted = false`; `T.MaMonHoc = A`; `Matches(BaseFingerprint, snapshot(T)) = true` | **MEASURED** (control leg) |
| S0′ (control) | `KeepLocal` | `Applied` | **MEASURED**. Positive discriminator: the resolver does not reject everything |
| S1 (direct leg) | remote tombstone of **T itself** | `StudyTask T = Rejected/ScopeHasUnresolvedConflict`; `T.IsDeleted = false`; `Matches = true` | **MEASURED** |
| S1 (crossing leg) | remote tombstone of **A** (the Base parent) | `MonHoc A = Applied`; `A.IsDeleted = true` | **MEASURED** |
| S2 | same run | `T.IsDeleted = true`, `T.MaMonHoc = A` (unchanged), `T.ModifiedByDeviceId = PEER-DEVICE` (the parent's tombstone provenance, A2-b), `T.Rev = 4` | **MEASURED** |
| S3 | same run | `record.Status = Unresolved`; `Matches(BaseFingerprint, snapshot(T)) = false` ⇒ **D9-T1 no longer holds** | **MEASURED** |
| S4 | same run | conflict-record count 1 → 1; **0** `TombstoneConflict/AutoTombstone` rows for T | **MEASURED** |
| S5 | same run | peer baseline for T advanced: `rev = 4`, snapshot `isDeleted = true` | **MEASURED** |
| S6 | `ResolveAsync` × {KeepLocal, KeepRemote, KeepBase, ManualMerge(T under live B)} | **all four `Rejected/LiveStateDrift`**; record still `Unresolved` | **MEASURED** |
| S7 | next inbound op: peer's live T (`MaMonHoc = C`, edited) | `Rejected/ContractViolation` | **MEASURED** |

### 2.4 Why each step happens (code trace, FACT)

- **S1 direct is refused by the lock.** `ThreeWayMerge` yields a one-sided tombstone with no structural
  candidate. `SyncApplySession.cs:208-212` then finds an Unresolved record on `StudyTask|T|MaMonHoc`.
- **S1 crossing passes.** The op is `ApplyEntityAsync(…, A, …, StructuralScopeKeyOf("MonHoc", A))`
  (`:153`), so its lock key is `MonHoc|A|MaHocKy`, not T's key, and `:208` finds nothing. The result
  is a tombstone, so the parent block at `:217` is skipped.
- **S2.** `:279-280` cascades on A's live→dead transition. `:603` selects
  `StudyTasks.Where(MaMonHoc == A && !IsDeleted)`. T qualifies *because* staging held it at Base,
  whose parent is A. `TombstoneCascadeChildAsync` (`:620-645`) marks T with **no lock lookup**.
- **S4.** Evidence is written only if a baseline exists **and** `!SnapshotFieldsEqual(base, before)`
  (`:632-633`). Staging set the baseline to canonical(Base) and live is Base, so the fields are equal
  and no evidence is written.
- **S5.** `st.Written` includes cascade children, so `UpsertBaselinesAsync` (`:647-661`) advances T's
  baseline to the tombstone.
- **S6.** The drift gate is step 6 (`ConflictResolver.cs:142-145`), and it precedes the B-2 parent check
  at step 8 (`:157-174`). `SyncBaseFingerprint.Matches` compares the stored fingerprint with the
  fingerprint of the **whole snapshot, provenance included** (`ConflictStaging.cs:185-186`). So every
  kind stops at drift, and **B-2 is never evaluated**.
- **S7.** The new Base (the baseline) is dead while remote is live. `ThreeWayMerge.cs:45-53` throws
  "resurrection does not exist in this model" before the lock is reached (`:188` runs before `:208`).

### 2.5 Reachability bounds

| Variant | Crosses? | Basis |
|---|---|---|
| S1-CR, Base parent tombstoned (the recipe) | **Yes** | **MEASURED** |
| S1-CR, **Local** or **Remote** candidate's parent (B or C) tombstoned | **No.** T's live FK is A, so the cascade does not select it. Resolution to that candidate is then `ResultParentTombstoned` (B-2), which is not W-2 | FACT (`:603`) + B-2 |
| S1-PT where Base's parent ≠ the tombstoned remote parent, and Base's parent is tombstoned later | Yes, by the same mechanism | INFERENCE |
| S1-PT test-M shape (Base parent already dead at staging) | No new crossing. The cascade fires only on a live→dead transition (`:279`), which has already happened | FACT |
| AL-PT (no local row) | No. The scope has no live row, so there is nothing to cascade | FACT (§2.1 of PR-6 DoR) |
| S3 TaskNote (Base null, `N1` hard-deleted by M5) | No. The scope has no live note, so `:611` selects nothing. Resolution: KeepLocal/KeepRemote ⇒ `ResultParentTombstoned` (B-2/E-13); `KeepBase(null)` ⇒ applied | INFERENCE (code path; not measured) |
| S2 TaskNote (`N0` held live at Base) | Yes (`:611`) | INFERENCE. **S2 is unreachable in v1 by construction** (PR-6 DoR §2.1) |
| S1 on a **MonHoc** under a tombstoned **HocKy**; S1 StudyTask record under a MonHoc under a tombstoned HocKy (grandchild) | Yes. Recursion at `:598`, `:606` | FACT (mechanism) + INFERENCE (end state) |
| Parent tombstoned by a **local UI delete** (Epic-1 EF fixup / `TaskCascadeHelper`), not by sync | Same end state. Nothing outside `Sync/` consults the lock (PR-6 DoR §10.3, FACT). Reachable once M2.2 exists | INFERENCE. This overlaps W-1 and is noted only because every option below must say whether it covers this path |

**The bound that matters.** A crossing requires a **live row held at Base** whose **Base-side parent**
takes a live→dead transition while the record is `Unresolved`. W-2 is not "any cascade near any
unresolved scope".

---

## 3. Candidate resolutions

Each option is described as the owner might select it. The consequences are engineering analysis.

### 3.1 Option A — D9-T1 precedence: cascade does not enter an Unresolved scope

**Statement.** The cascade skips any row that is the live occupant of a scope holding an `Unresolved`
record. That row stays at Base, which is D9-T1. This is an explicit, bounded exception to D9-T4's
amended meaning and to the Epic-1 cascade.

**Consequences.**

- **End state.** T stays live at Base under tombstoned A. That is **the same physical shape as test M**
  (`SyncApplyParentHandlingTests.cs:112-141`): a live child under a tombstoned structural parent. PR-6
  DoR §11.1 already accepts that shape at resolution time for `KeepBase`. The difference matters: in
  test M the shape comes from the documented Epic-1 gap, but under A it would be **created by policy**.
  B-2 (i)'s stated rationale, "creates no *new* orphan", then describes a state the owner has
  sanctioned, not an accident it tolerates.
- **After resolution nothing re-cascades.** The cascade fires only on the parent's live→dead
  transition (`:279`), and A is already dead. Resolving by `KeepBase`, or by a ManualMerge that keeps
  `MaMonHoc = A` (rejected by B-2 anyway), would leave a **permanent** live orphan. Closing that gap
  would need one of:
  - a cascade at resolution time, which B-2 forbids ("no cascade-tombstone at resolution time",
    PR-6 DoR §14) and which B-3 excludes;
  - removing B-2 (i)'s exemption for this shape;
  - or accepting the orphan.

  Each of these is a further owner ruling.
- **Subtree question (UNRESOLVED).** Take a HocKy tombstone that reaches a MonHoc whose StudyTask
  holds an Unresolved record. Does A skip only T, or also T's notes and links? Does it still tombstone
  T's siblings? The documents do not say.
- **Evidence (UNRESOLVED).** Is the skipped cascade recorded anywhere (a new evidence kind, or a note
  on the record)? If it is not, nothing records that A's tombstone "owes" T a cascade.
- **Peer divergence (INFERENCE, W-3-coupled).** The peer that deleted A has already tombstoned its
  own T. After a local `KeepLocal`, the live T (Rev+1) reaches the peer. There, the local tombstone
  against a live edit resolves by tombstone-wins. The human resolution is overridden on the peer and
  the devices diverge. This is a new instance of W-3, stated and not solved.

### 3.2 Option B — D9-T4 precedence: the cascade always applies (**current shipped behaviour**)

**Statement.** Parent tombstone cascade applies into Unresolved scopes. D9-T1 holds *except* when a
structural ancestor of the scope's live row is tombstoned.

**Consequences.**

- **This is what the code does today** (§2, MEASURED). Selecting B alone requires **no production
  change**.
- **D9-T1 guarantees that become invalid** for a crossed scope:
  1. "live domain state must equal Base" — false from S3 onward, permanently. No resurrection exists
     (`ThreeWayMerge.cs:45-53`; PR-6 DoR §11.1).
  2. "`ConflictRecord` is the staging boundary holding the recoverable evidence **until explicit
     resolution**". The evidence is still there (I-2, triggers), but **no explicit resolution can
     reach it**. S6 is MEASURED.
  3. "Resolution may subsequently materialise Local, Remote, Base(null), or a new ManualMerge result"
     — none can be materialised.
- **BaseFingerprint.** It stays immutable and still means "live state at staging". It stops being a
  predicate that can ever become true again, because live is a tombstone and resurrection is
  impossible. The record's `Base` candidate becomes, in effect, "the pre-deletion state".
- **ConflictResolver.** Every kind returns `LiveStateDrift` (MEASURED), and B-2 / B-4 / E-3 never run.
  PR-6 code stays **consistent** with B. It does exactly what B plus frozen D8-H imply.
- **Record lifecycle.** The record is `Unresolved` forever: D7-D forbids reopen, D9-T6 forbids
  supersede, D8-H forbids force and rebase. Its scope lock is permanent. **Inbound traffic for T is
  refused anyway**, by the merge-core resurrection guard, before the lock is reached (S7, MEASURED).
- **No-data-loss angle.** Without a conflict, a delete-vs-edit cascade would record `AutoTombstone`
  evidence as `Resolved` (D9-T5). Under B the withdrawn candidates survive only as the evidence
  columns of a permanently `Unresolved` record, and no `AutoTombstone` row is written (S4, MEASURED).
- **Sub-variants the owner may want to consider (each needs a ruling beyond W-2):**
  - **B1: pure precedence.** Accept the permanently Unresolved record. Behaviour: today's.
  - **B2: precedence plus mechanical closure.** The cascade moves the crossed record to a terminal
    state representing tombstone-wins, for example `Resolved` with `AutoTombstone`. This conflicts
    with D9-T5's *"written directly as `Status = Resolved`"* (auto records are born Resolved, not
    transitioned) and with the D9 §16 lifecycle, which lists only human kinds out of `Unresolved`.
    It would need a lifecycle ruling and a production change.
  - **B3: precedence plus a narrowed drift gate.** Treat a tombstoned live row as "Base-compatible"
    for a no-write `KeepBase` only. "Base-compatible" (D8-H, §17) is not defined by the frozen text;
    byte-equality is engineering choice E-4. It would still need a ruling, because it changes what
    D8-H rejects.
- **Coupling with W-1 (direct consequence only).** Under B, W-2's end state *is* W-1's end state. Any
  later W-1 ruling (abandon, rebase, …) would therefore decide what happens to W-2 records as well.

### 3.3 Option C — Parent tombstone blocked while a dependent Unresolved scope exists

**Statement.** An operation that would tombstone a structural ancestor of a live row occupying an
Unresolved scope is not applied while that record is `Unresolved`. Both D9-T1 and D9-T4 hold
continuously, because the parent never becomes dead while the child is held live.

There are two ways to represent the remote tombstone intent:

- **C1: reject** (for example `ScopeHasUnresolvedConflict`, or a new reason). No new state.
  - **FACT.** A rejected op rolls back (`SyncApplySession.cs:122`), so A's peer baseline is **not**
    advanced.
  - **FACT.** `SyncChangeEnumerator` emits a row whose `Rev` exceeds that peer's baseline.
  - **INFERENCE.** The peer re-offers A's tombstone on every run until T's record is resolved. This is
    the retry mechanism v1 already relies on for ops refused by the D9-T6 lock (S1 direct leg,
    MEASURED). After resolution:
    - `KeepLocal` or `KeepRemote` moves T under B or C, so A's tombstone applies later **without**
      touching T. This is ordinary D4 behaviour: T was reparented.
    - `KeepBase` leaves T under A, and A's tombstone later cascades T normally.

    All four resolution kinds stay meaningful, and D8-H stays intact.
- **C2: defer.** Persist the remote tombstone intent as a new deferred item and apply it when the
  scope resolves. This is **new state with a new lifecycle**. The only deferral v1 has is per-run
  (DoR §12.5 step 3), and D9-T6 puts supersede/reopen out of scope. It needs a schema/lifecycle ruling
  and design.

**Consequences.**

- **Tombstone-wins.** The tombstone is delayed, not lost, for any row still under A when the scope
  resolves. But a *resolution* can move T away from A before the tombstone lands. On the peer, T is
  already tombstoned by its own cascade, so a later exchange resolves by tombstone-wins there and the
  devices diverge. This is W-3-coupled INFERENCE, stated and not solved.
- **D8-G.** The parent tombstone and its cascade are one logical operation. C1 rejects it
  **atomically**, which is consistent with D8-G. The side effect is that **siblings of T under A that
  are not in conflict also stay live** until T's record resolves, and so do all of A's other
  descendants. For a HocKy tombstone the delay reaches the whole semester subtree.
- **Scope definition.** The lock for A's operation must find Unresolved records held by **any live
  descendant**. That is not D9-T6's per-`ScopeKey` lookup: it is a new notion of "dependent scope"
  (see D-b, §3.5). By INFERENCE it needs no schema change, since `EntityType` / `EntityId` /
  `ConstraintValue` are already columns. It is still new policy.
- **Local UI deletes.** C1 only governs sync apply. A local UI delete of A does not consult locks
  (FACT) and would still cross (§2.5 last row). A C ruling must say whether M2.2 blocks "delete subject
  while a task under it has an unresolved conflict". Otherwise C protects only one of two paths.
  **UNRESOLVED.**
- **ConflictResolver.** It sees what the control leg sees (§2.3 S0/S0′, MEASURED for KeepLocal). The
  other cells follow the PR-6 DoR §7.3 S1-CR row (INFERENCE). The resolver does not read incoming
  queues, so it is unaffected by C1.

### 3.4 Fourth interpretation D-a — D9-T1 "scope" read as the conflicted **field**

**Textual support.** `ScopeKey(Structural) = EntityType | EntityId | FieldName` (engineering DoR §5.3,
[ENG]). "Conflict scope" in D9-T1 is not defined by the frozen text.

**Against it (FACT).** Shipped and tested behaviour reads D9-T1 as the **whole row**. PR-5 rewrites the
entire row to Base (`SyncApplySession.cs:342-345`, "read literally means no other field may be
partially applied either"), and test L asserts that `TenTask` is rolled back
(`SyncApplyParentHandlingTests.cs:48-103`). Adopting D-a would contradict shipped, tested
interpretation. It is permitted by the words and contradicted by the implementation.

**Effect on W-2 (INFERENCE).** The cascade changes provenance (`IsDeleted`), not `MaMonHoc`, which
stays A (MEASURED, S2). So a field-scoped D9-T1 is **not violated** by the crossing, and the textual
collision dissolves. **Resolvability is not restored.** The drift gate fingerprints the whole snapshot
including provenance (E-4; `ConflictStaging.cs:185-186`), so S6 is unchanged. Restoring resolvability
would additionally require narrowing D8-H or E-4, which is a separate rule and a separate ruling.
Even then, `KeepLocal`, `KeepRemote` and `ManualMerge` would write a live row over a tombstone:
**resurrection**, which the model forbids. Only a no-write `KeepBase` could close the record. So D-a
changes *which* invariant is violated without changing the end state, unless B3-style drift narrowing
is also ruled.

### 3.5 Fourth interpretation D-b — "scope" as the **structural closure** (row + its structural ancestors)

**Textual support: none in the authoritative documents.** D9-T6 says "logical conflict scope" without
defining it. Its operative sentence concerns *"a new candidate pair [that] maps to the same logical
scope"*, not cascades. The only written definitions (engineering DoR §5.3, §12.4: *"for entities the
entity itself, for TaskNotes also the `MaTask` scope"*) are entity-scoped.

**Effect.** Under closure scope, A's tombstone is a write into T's scope, and D9-T6's existing
"rejected" wording applies. **D-b is option C1 obtained by redefining scope** rather than by a new
blocking rule. The consequences are C1's, plus a definitional ruling on "conflict scope". It is listed
separately because the owner may prefer to express the choice as a definition.

### 3.6 Fourth interpretation D-c — D9-T4 read by its §23A words only (incoming children)

**Textual support.** §23A D9-T4 speaks only of *"the incoming child/relationship"* (§1.2).

**Against it.** The owner-ratified amendment of 2026-09-10 restates D9-T4's *meaning* universally:
*"a tombstoned structural parent can never have a live child"*. Reading D9-T4 narrowly would silently
set aside ratified text. **D-c is therefore not available as an interpretation.** It would itself be
an amendment of the 2026-09-10 amendment. Even if ruled, it only relabels the collision as D9-T1 vs
D8-G + Epic-1 cascade (+ A2-b); it decides nothing. It is recorded so that nobody later treats it as
a free exit.

---

## 4. Invariant impact matrix

`✔` preserved · `✖` violated for crossed scopes · `~` strained or partially changed · `R` requires a
new owner ruling to be coherent · `=` unaffected

| Invariant / ruling | Source | **A** | **B (B1, status quo)** | **C1** | **C2** | **D-a** | **D-b** |
|---|---|---|---|---|---|---|---|
| live = Base while Unresolved | D9-T1 | ✔ | ✖ | ✔ | ✔ | ✔ (field) / ✖ (row) | ✔ |
| no live orphan for the incoming child | D9-T4 (§23A words) | = | = | = | = | = | = |
| tombstoned structural parent never has a live child | D4/D9-T4 amendment (meaning) | ✖ (bounded exception) R | ✔ | ✔ | ✔ | ✔ | ✔ |
| parent tombstone + dependent cascade = one op | D8-G | ~ (op is partial) R | ✔ | ✔ (rejected atomically) | ~ R | ✔ | ✔ |
| Epic-1 cascade "deleting a subject tombstones its tasks" | `data-model.md` §4; DoR §12.3 / ACK A2-b | ✖ R | ✔ | ~ (delayed) | ~ (delayed) | ✔ | ~ (delayed) |
| one Unresolved per scope; ops into scope rejected | D9-T6; DoR §12.4 | ✔ | ~ (cascade bypasses the lock; direct op does not — MEASURED asymmetry) | ✔ (scope widened) R | ✔ | ✔ | ✔ (scope redefined) R |
| reject on drift; no force/rebase | D8-H, §17 | ✔ (never drifts) | ✔ (is what rejects) | ✔ | ✔ | ✖ unless drift narrowed R | ✔ |
| no reopen / supersede | D7-D, D9-T6 | ✔ | ✔ (record stuck forever) | ✔ | ~ (new deferred state) R | ✔ | ✔ |
| tombstone-wins (delete-vs-edit) | D9-T5, DoR §7.3 | ✔ (entity level) | ✔ | ~ (delayed; §5) | ~ (delayed) | ✔ | ~ (delayed) |
| no resurrection | DoR §19; `ThreeWayMerge.cs:45-53` | ✔ | ✔ | ✔ | ✔ | ✖ if KeepLocal/Remote allowed R | ✔ |
| B-2 (i) KeepBase-no-write exempt | owner ruling | ~ (exempts a policy-created orphan) R | = (never reached) | ✔ | ✔ | ✔ | ✔ |
| B-2 no cascade at resolution; B-3 no cascade in resolver | owner rulings | ✖ if post-resolution cascade wanted R | ✔ | ✔ | ✔ | ✔ | ✔ |
| resolution can materialise Local/Remote/Base/Manual | D9-T1 ¶3, D6-H | ✔ | ✖ | ✔ | ✔ | ✖ (KeepBase only) | ✔ |
| no-data-loss: withdrawn candidates recoverable by resolution | D9-T1 ¶1, D6 | ✔ | ✖ (evidence only; S4, S6) | ✔ | ✔ | ~ | ✔ |
| local UI delete path covered | — | UNRESOLVED | ✔ (same as sync) | UNRESOLVED | UNRESOLVED | = | UNRESOLVED |
| **Production change needed** | — | **yes** (PR-5 cascade) | **no** | **yes** (PR-5 ancestor lock) | **yes** (+ schema/state) | **yes** (drift gate) | **yes** (= C1) |

---

## 5. Tombstone-wins impact

**FROZEN TEXT.** Tombstone-wins is the T2.4 policy (Decision Record line 17). D9-T5 records it as
*"`delete-vs-edit` evidence represents the tombstone-wins decision"*. The merge core applies it before
any field merge (`ThreeWayMerge.cs:37-90`).

**FACT.** It is an **entity-level** rule: the same row, one side deleted and one side edited. The
parent→child cascade is a separate rule (Epic-1 / D8-G / A2-b). Tombstone-wins reaches children only
through that cascade.

| Option | Entity-level tombstone-wins | Cascade-carried tombstone | Net |
|---|---|---|---|
| A | intact | **suspended** for the crossed row, and possibly for ever (§3.1) | parent dies, child lives; tombstone-wins "intact" only in the narrow entity sense |
| B | intact | intact (MEASURED) | intact; the cost is paid by D9-T1 and resolvability |
| C1 / D-b | intact for the parent **eventually**; the tombstone is refused for now and re-offered (INFERENCE) | delayed; can be **escaped** if a resolution reparents the child first | tombstone never *loses* locally, but it can arrive after the child has left; on the peer, divergence is W-3-coupled |
| C2 | as C1, with the intent persisted | as C1 | as C1 plus new state |
| D-a | intact | intact | as B, unless drift narrowing lets a no-write KeepBase close the record |

**Existing asymmetry, MEASURED (S1 direct vs S1 crossing).** Today a remote tombstone of T itself is
refused by T's lock, while a remote tombstone of T's parent tombstones T anyway. Whatever the owner
rules, one of these two outcomes will look inconsistent with the other unless the ruling names both:
A and C make the cascade match the direct case; B keeps the asymmetry.

---

## 6. Resolver impact matrix

Shape: S1-CR from §2.2, after "A's tombstone arrives". For C1, the column is the state *while the
tombstone is being refused*.

| Option | KeepLocal (T → B, B live) | KeepRemote (T → C, C live) | KeepBase | ManualMerge (T, parent live) | Record end state | Label |
|---|---|---|---|---|---|---|
| **B1 (today)** | `Rejected/LiveStateDrift` | `Rejected/LiveStateDrift` | `Rejected/LiveStateDrift` | `Rejected/LiveStateDrift` | `Unresolved` forever; B-2 / B-4 / E-3 never evaluated | **MEASURED** |
| **A** | `Applied` (B-2 passes) | `Applied` | `Applied`, no write (B-2 (i)); leaves T live under dead A **permanently** | `Applied`; to A ⇒ `ResultParentTombstoned` | `Resolved` | INFERENCE (PR-6 DoR §7.3 S1-CR row + B-2) |
| **B2** | `Rejected/NotResolvable` (record auto-closed) | same | same | same | `Resolved(AutoTombstone)`, if ruled | INFERENCE; needs a ruling |
| **B3** | `LiveStateDrift` (would resurrect) | `LiveStateDrift` | `Applied` (no write; tombstone stays) | `LiveStateDrift` | `Resolved(KeepBase)` | INFERENCE; needs a ruling |
| **C1 / D-b** | `Applied` (control leg, **MEASURED**) | `Applied` | `Applied`, no write | `Applied` | `Resolved`; A's tombstone applies on a later run | MEASURED (KeepLocal) + INFERENCE |
| **C2** | as C1 | as C1 | as C1 | as C1 | `Resolved`; deferred tombstone replayed | INFERENCE; needs a design |
| **D-a** | resurrection ⇒ must reject | must reject | only exit, if drift narrowed | must reject | `Resolved(KeepBase)` or stuck | INFERENCE; needs a ruling |

**Is shipped PR-6 code semantically invalidated?**

- **B1:** no. It is exactly B1's behaviour.
- **A, C1, C2, D-b:** no. The resolver's own rules (B-1..B-4, E-3) are untouched. What changes is PR-5's
  cascade and lock behaviour, which feeds it.
  - Under A, B-2 (i)'s *rationale* ("no new orphan") would need restating.
  - Under A with post-resolution cascade, B-2's "no cascade at resolution time" and B-3 would need
    amending.
- **D-a / B3:** the drift gate (step 6) changes. That touches D8-H/E-4, not B-1..B-4.

---

## 7. T2.5 impact

### 7.1 What T2.5 can do now, under any ruling (no production change)

- **P10 as characterization (recon §6.2, unchanged).** The probe in Appendix A *is* that test. Keep all
  three legs: control, direct, crossing. The control leg is the positive discriminator recon §7.4
  asks for.
- **P6 scoped.** "No cascade crosses the scope" becomes a **generator precondition**, not a prose
  caveat, in the style of recon §7.4 item 3. A generated case whose Base parent takes a live→dead
  transition while its record is `Unresolved` is excluded from P6 and routed to P10.
- **Name the asymmetry** (S1 direct vs S1 crossing) in the characterization, so a future change to
  either leg turns it red.

### 7.2 Per option

| Option | P6 (D9-T1) | P10 (D9-T4) | Characterizable without production change? | Later M2.2 consequence |
|---|---|---|---|---|
| **B1** | normative, **scoped** (excludes crossings) | normative, universal for sync-applied cascades | **Yes.** Today's behaviour | Conflict UI must show records that can never be resolved; W-1 ruling decides their fate |
| **B2 / B3** | as B1 until implemented | as B1 | only B1's half; the closure leg needs production | lifecycle / drift PR before M2.2 |
| **A** | normative, universal | normative **with the exception** | **No.** The characterization pins the opposite; flip after the PR-5 change | UI delete path policy; orphan display after KeepBase |
| **C1 / D-b** | normative, universal | normative, universal | **No.** Today the parent op is `Applied` (MEASURED); flip after the PR-5 change | UI must block or confirm deleting an ancestor of a conflicted row, or the local path still crosses |
| **C2** | as C1 | as C1 | No | as C1, plus deferred-item surfacing |
| **D-a** | normative per field | as B1 | P6-per-field yes; resolvability leg no | drift gate PR |

**Engineering observation.** If no ruling is made before T2.5 lands, the suite will pin B1. That is
correct as characterization. It must be labelled as characterization so that it is not later cited
as evidence that the owner chose B.

---

## 8. Proposed amendment wording (governance form only; the selection is blank)

Repository precedent (the D4/D9-T4 amendment, the PR-5 context-lifetime amendment, and the PR-6
rulings) is to **leave the Decision Record unedited** and add a dated file in `docs/specs/`. That file
quotes the owner's selection verbatim and adds an authorised/forbidden table. The skeleton below
follows that pattern. **It is not created by this PR.**

```markdown
# T2.3 / T2.4 — D9-T1 / D9-T4 Collision Amendment: Parent Tombstone Crossing an Unresolved Scope

| | |
|---|---|
| **Date** | <YYYY-MM-DD> |
| **Status** | RATIFIED — owner ruling |
| **Amends** | D9-T1 and/or D9-T4 (as amended 2026-09-10) — <which, per selection> |
| **Resolves** | W-2 / F-2 (`docs/review/2026-09-11-t2.4-pr6-conflict-resolver-dor.md` §11.3, §15.3; `docs/review/2026-09-13-w2-f2-semantic-analysis.md`) |
| **Does not amend** | D8-H, D7-D, D9-T5, D9-T6 wording, B-1..B-4 — <unless the selection says otherwise> |
| **Implemented by** | <future PR; "none — current behaviour" only for B1> |

## The ruling (verbatim)
> <owner selection>
```

Candidate ruling texts, one per option. They are offered so that the owner edits words rather than
drafting from scratch. **None is preferred.**

- **A.**
  > While a StructuralConflict or ConstraintConflict is Unresolved, a cascade tombstone generated by
  > the tombstoning of a structural ancestor MUST NOT be applied to the live row occupying that
  > conflict's scope. That row remains equal to Base (D9-T1). This is an explicit exception to the
  > meaning of D9-T4 "a tombstoned structural parent can never have a live child", limited to rows
  > occupying an Unresolved scope. After resolution: <the orphan is accepted / … — requires answer to
  > Q-A1>. Descendants of the protected row: <Q-A2>. Applies to sync-applied and locally generated
  > tombstones: <Q-6>.
- **B1.**
  > A cascade tombstone generated by the tombstoning of a structural ancestor applies to every live
  > descendant, including one occupying an Unresolved scope. For such a scope D9-T1 ceases to hold
  > from that point; the ConflictRecord remains Unresolved, its evidence immutable, and every
  > resolution is rejected under D8-H. No force, rebase, reopen or auto-closure is introduced by this
  > ruling.
- **B2.** Text as B1, plus:
  > …and the ConflictRecord transitions to <terminal state>, which is a new lifecycle transition
  > amending D9 §16 / D9-T5.
- **C1.**
  > An operation that would tombstone a structural ancestor of a live row occupying an Unresolved
  > scope MUST be rejected (<reason>) and MUST NOT apply any part of its cascade. The remote tombstone
  > intent is not persisted; it is re-offered by the peer's enumerator. Local deletes of such an
  > ancestor: <Q-C2>.
- **C2.** As C1, but:
  > …the tombstone intent MUST be persisted as <deferred item> and applied when the scope is
  > resolved.
- **D-a** (definitional; must be combined with a drift ruling to change outcomes):
  > "Conflict scope" in D9-T1 means the conflicted field (the `ScopeKey` field), not the whole row.
  > <plus D8-H / E-4 narrowing text>.
- **D-b** (definitional; equivalent to C1):
  > "Logical conflict scope" in D9-T1 and D9-T6 includes the structural ancestors of the scope's live
  > row.

---

## 9. Questions the owner must answer

| # | Question | Needed for | Documents determine it? |
|---|---|---|---|
| **Q-1** | When a structural ancestor of a row occupying an Unresolved scope is tombstoned, which yields: D9-T1 (A), D9-T4's amended meaning (B), or the ancestor tombstone (C)? Or something else? | all | **No.** DoR §11.3: "an owner question" |
| **Q-2** | Does "conflict scope" (D9-T1) / "logical conflict scope" (D9-T6) mean entity-row (as shipped), field (D-a), or structural closure (D-b)? | D-a, D-b; also frames A and C | **No.** Only engineering definitions exist (DoR §5.3, §12.4) |
| **Q-3** | Does the ruling cover **local** (UI/repository) tombstones as well as sync-applied ones? | A, C, D-b | **No.** Locks are sync-only (FACT) |
| **Q-4** | Does the ruling apply equally to Constraint scopes (S2) and to grandchildren (HocKy → MonHoc → StudyTask records)? | all | **No** |
| **Q-B1** | *If B:* is a permanently `Unresolved`, unresolvable record acceptable in v1? Or is closure (B2) or drift narrowing (B3) required? | B | **No.** D8-H, D7-D and D9-T6 together imply B1 but do not say it is intended |
| **Q-A1** | *If A:* after `KeepBase` the row stays live under a dead parent for ever. Accept it, cascade at resolution (amends B-2/B-3), or remove B-2 (i) for this shape? | A | **No** |
| **Q-A2** | *If A:* does the protection extend to the protected row's own descendants (notes, links) and spare its siblings? | A | **No** |
| **Q-C1** | *If C:* reject-and-re-offer (C1) or persisted deferral (C2)? Is delaying non-conflicted siblings and subtree acceptable? | C | **No** |
| **Q-C2** | *If C:* must M2.2 block or confirm a local delete of an ancestor of a conflicted row? | C | **No** |
| **Q-5** | May T2.5 pin today's behaviour (B1) as **characterization** before the ruling, labelled as not-a-ruling? | T2.5 | Recon §6.2 / P-3 assumes yes; **confirmation requested** |

---

## 10. Engineering trade-offs (not a ruling)

These are the costs as engineering sees them. They are stated so that the owner can weigh them. The
owner may weigh them differently.

1. **Deferral defaults to B1.** The code implements B1 today, so leaving W-2 unruled is behaviourally
   the same as choosing B1 for M2.1. It is *not* the same governance-wise. That is why §7.2 asks that
   T2.5 label its tests as characterization.
2. **B1 is the cheapest option and moves the cost into W-1.** It needs no production change and keeps
   every Epic-1 and tombstone rule. But it ratifies records that can never be resolved, and it keeps
   the direct-vs-cascade asymmetry (§5). Its user-facing cost is deferred to M2.2 and to whatever W-1
   ruling follows.
3. **A is the most locally resolvable option and the most rule-expensive.** Every resolution kind
   works. But A sanctions a live orphan, needs at least Q-A1 and Q-A2, likely touches B-2 (i)'s
   rationale, and changes the PR-5 cascade (HIGH blast radius per PR-6 DoR §5: `CascadeTombstoneAsync`
   HIGH, 4 impacted). It also opens a peer-divergence path of the W-3 class.
4. **C1 keeps both invariants and reuses v1's existing retry.** No new state is needed for remote
   tombstones, and resolution stays fully meaningful. Its costs are:
   - a new "dependent scope" lookup in the apply path (production change);
   - delaying non-conflicted siblings and subtrees;
   - a mandatory M2.2 decision on local deletes, without which it protects only the sync path;
   - W-3-class divergence when a resolution reparents the child away from the parent being deleted.
5. **C2 and B2 each introduce a new lifecycle.** They are the largest design surface and are the
   options most likely to reopen D9-T5, D9-T6 or D9 §16.
6. **D-a and D-b are mainly framing devices.**
   - D-a does not change outcomes without a separate drift ruling, and it contradicts shipped, tested
     behaviour.
   - D-b is C1 expressed as a definition.

   Either can be the *form* of a ruling. Neither is a cheaper *substance*.

**Sequencing observation.** W-2 under B collapses into W-1. Under A or C it stays separate from W-1.
If the owner intends to rule W-1 before M2.2 anyway, ruling W-2 at the same time avoids deciding the
same end state twice. This is a sequencing note only; W-1 is not analysed here.

---

## 11. Strengths · Risks · Follow-ups · Final notes (docs/review required sections)

**Strengths.**

- F-2 is no longer inference. The recipe is **MEASURED** with a paired control leg and a direct leg.
- The measurement tightened three earlier claims:
  - the resolver never reaches B-2 on a crossed scope;
  - no `AutoTombstone` evidence is written;
  - the next inbound op fails in the merge core, not at the lock.
- The collision's text is pinned precisely (§1.2). This prevents a silent narrow reading of D9-T4
  (D-c).

**Risks / watchouts.**

| Risk | Severity | Where |
|---|---|---|
| T2.5 tests that pin B1 get cited as "the owner chose B" | Medium | §7.2, §10.1 |
| An implementer "fixes" F-2 before a ruling, by skipping the cascade or blocking the parent | Medium | PR-6 DoR §14 fence; recon §9.4 item 8 |
| Local UI delete crosses under every option except B unless Q-3 is answered | Medium (M2.2) | §2.5, Q-3 |
| S3 no-crossing and HocKy-level crossing are INFERENCE, not measured | Low | §2.5 |
| Peer-side divergence under A and C is W-3-class INFERENCE | Low (v1 claims no post-resolution convergence) | §3.1, §3.3 |

**Suggested follow-ups (non-blocking).**

- Owner ticket for Q-1..Q-5.
- T2.5 lands the Appendix A probe as `FrozenDesignCharacterizationTests` (P10), with the three legs
  and the characterization label.
- If A or C is selected, a dedicated PR-5 amendment PR with its own GitNexus impact on
  `CascadeTombstoneAsync` / `ApplyEntityAsync`.

**Final notes.** The collision is real, reachable through an ordinary two-device sequence, and
measured. Today it resolves as B1: the cascade wins, D9-T1 silently stops holding for that scope, and
the record becomes permanently unresolvable. Each alternative restores one ratified guarantee by
spending another, or by adding new rulings (§4). No option is free, and the documents do not choose
among them. That choice is Q-1.

---

## Decisions made (ADR-style, engineering only)

| Decision | Why it had to be made | What it is for | Experience it draws on |
|---|---|---|---|
| Measure the F-2 recipe with a throwaway, uncommitted probe instead of repeating "INFERENCE + recipe" | Both prior documents left the premise unmeasured, because their tasks forbade test code. This task forbids only production changes and extra commits | the owner rules on an observed state, not a derivation | reproduce before escalating; the PR-2 residuals → PR-5 "later measured" precedent |
| Pair the crossing leg with a control leg and a direct-tombstone leg | a probe with one leg cannot show the cascade is the cause | discrimination: the legs differ in one input and give opposite results; the direct leg exposed the lock asymmetry | a signal must be able to go red; pass ≠ evidence |
| Name the collision's second side as the ratified bundle (D9-T4 + amendment meaning + D8-G + A2-b), and set aside the narrow §23A reading as D-c | the §23A words alone do not cover existing children, but the owner-ratified amendment does | stops a silent reinterpretation from being mistaken for an exit | "do not silently reinterpret ratified rules" (task governance) |
| Treat B as the status quo explicitly, and state that no ruling behaves like B1 | the trade-off between options is asymmetric in production cost | the owner sees that inaction is a behavioural choice | evidence-scoped claims; observation vs ruling |
| Offer amendment wording as a `docs/specs/` skeleton with the selection blank, and do not create the file | three repository precedents keep the frozen record unedited, and the task forbids choosing | the owner edits words instead of drafting, with no pre-selection | D4/D9-T4, PR-5 and PR-6 amendment pattern |
| Keep W-1 and W-3 to inline "direct consequence" notes | the task fences them | a focused decision surface | scope discipline from PR-6 DoR §14 |

---

## Appendix A — probe (not committed; re-creatable)

File: `SmartStudyPlanner.Tests/Sync/Apply/W2ProbeTests.cs` (deleted after the run). It uses only
existing public/test APIs: `SyncApplyFixture`, `SyncApplySession`, `ConflictResolver`,
`SyncBaseFingerprint`, `IncomingChanges`, `CanonicalJson`.

```text
StageS1CrAsync():
  (H, A, T) = SeedTreeAsync(); add MonHoc B, C under H
  SetBaselineAsync(Peer, T); SetBaselineAsync(Peer, A)
  local: T.MaMonHoc = B
  apply remote T{MaMonHoc = C} @RemoteLater by PEER-DEVICE   -> ConflictStaged (S1-CR)

Control:   assert Unresolved, !T.IsDeleted, Matches(BaseFp, snap(T)); KeepLocal -> Applied
Direct:    apply remote T tombstone (MaMonHoc = A)           -> Rejected/ScopeHasUnresolvedConflict; Matches true
Crossing:  apply remote A tombstone @2026-06-05 by PEER-DEVICE -> Applied
           assert T.IsDeleted, record Unresolved, !Matches(BaseFp, snap(T))
           observe: TombstoneConflict rows for T = 0; T baseline rev 4, isDeleted true
           ResolveAsync KeepLocal / KeepRemote / KeepBase / ManualMerge(T under B) -> all Rejected/LiveStateDrift
           apply remote T{"peer edit", MaMonHoc = C}         -> Rejected/ContractViolation
```

Observed output (verbatim excerpts, `--logger "console;verbosity=detailed"`):

```text
staging: StudyTask …=ConflictStaged/None
control: status=Unresolved T.IsDeleted=False T.MaMonHoc==A:True matches=True
control KeepLocal: Applied/None
direct T tombstone: StudyTask …=Rejected/ScopeHasUnresolvedConflict; T.IsDeleted=False status=Unresolved matches=True
parent tombstone apply: MonHoc …=Applied/None
after: M.IsDeleted=True T.IsDeleted=True T.MaMonHoc==A:True T.ModifiedBy=PEER-DEVICE T.Rev=4
record: status=Unresolved matches=False
records before=1 after=1; TombstoneConflict rows for T=0
T baseline: rev=4 baselineIsTombstone=True json={…"isDeleted":true…}
resolve KeepLocal: Rejected/LiveStateDrift
resolve KeepRemote: Rejected/LiveStateDrift
resolve KeepBase: Rejected/LiveStateDrift
resolve ManualMerge(parent B live): Rejected/LiveStateDrift
record after resolves: status=Unresolved
next inbound T: StudyTask …=Rejected/ContractViolation
```
