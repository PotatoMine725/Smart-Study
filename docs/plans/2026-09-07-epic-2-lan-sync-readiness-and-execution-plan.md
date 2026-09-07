# Epic 2 — LAN Sync Engine: Readiness Assessment & Execution Plan

> **Status: Readiness/planning pass. No production code changed.** Scope: T2.1–T2.5 (master plan
> numbering; also touches T1.4 and T2.6, which the master plan already folds into Epic 2's
> milestones — see [`2026-07-03-master-plan.md`](2026-07-03-master-plan.md) §"Epic 2"). This
> document does **not** decide anything. It records what's true in the tree today, what's still
> open, and a proposed sequence — for owner review before any implementation starts.
>
> **Independent workstream.** No Data Maturation artifact, S-2 guideline, S-0 snapshot, reader
> package, scoring key, or S-1/S-2 execution record was read or touched to produce this document.
> Written on a new branch (`docs/epic2-lan-sync-readiness`, off `dev`) in an isolated worktree, so
> the concurrent S-2 branch's working tree is untouched.
>
> **Sources read to produce this report:** [`../specs/system_roadmap.md`](../specs/system_roadmap.md)
> §A.3 item 3, [`2026-07-03-master-plan.md`](2026-07-03-master-plan.md) ("Epic 2 — LAN Sync Engine"),
> [`2026-07-01-architecture-direction-decisions.md`](2026-07-01-architecture-direction-decisions.md)
> (D-A, D-B, D-F), [`2026-07-02-architecture-freeze-decisions.md`](2026-07-02-architecture-freeze-decisions.md)
> (D-I), [`../architecture/data-model.md`](../architecture/data-model.md) §8,
> [`../architecture/lessons-learned.md`](../architecture/lessons-learned.md) (L6, L7, L9),
> [`../knowledge/sync-data-model.md`](../knowledge/sync-data-model.md),
> [`2026-07-27-post-epic1-stabilization.md`](2026-07-27-post-epic1-stabilization.md) ("Epic 2 Entry
> Criteria"), [`../active/README.md`](../active/README.md), and the current tree via GitNexus
> (`context`/`query`/`impact` on `Smart-Study`) plus direct reads of
> `SmartStudyPlanner/Data/SyncStamper.cs`, `Models/ISyncMetadata.cs`,
> `Services/ML/DeviceIdentity.cs`, and the six `SqliteXxxRepository.cs` files.

---

## 1. Readiness — what's actually in the tree

Verified against the current `dev` tip (`33c0ffec2`), not against docs alone.

### 1.1 Present and working (Epic 1 substrate)

| Foundation | Evidence |
|---|---|
| `ISyncMetadata` (`Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`) on all 6 synced entities | `Models/ISyncMetadata.cs`; implemented by `HocKy`, `MonHoc`, `StudyTask`, `StudyLog`, `TaskNote`, `TaskReferenceLink` (grep confirms all 6 + the interface file, 12 hits) |
| Single stamping seam | `Data/SyncStamper.cs:20-42`, called from both `AppDbContext.SaveChanges` overloads — confirmed via GitNexus, no second call site |
| Tombstones replace hard deletes | `SyncStamper.Apply`: a `Deleted` tracker entry is rewritten to `Modified` + `IsDeleted=true` before save. Cascade-tombstone for FK-only children via `TaskCascadeHelper` |
| Every UI/business read path filters `!IsDeleted` | Verified across all 6 `SqliteXxxRepository.cs` files by direct read — every query has a `.Where(... !x.IsDeleted)` clause. The only two unfiltered `DbSet` reads are `SqliteHocKyRepository.cs:98` and `SqliteStudyTaskRepository.cs:56`, both single-row upsert/delete lookups that **must** see tombstoned rows to reconcile against them — not display leaks |
| Persistent, LAN-safe device identity | `Services/ML/DeviceIdentity.cs` — file-backed (`device-id.txt` under `%APPDATA%`), seeded once from the old `Environment.MachineName`-derived value, degrades gracefully on I/O failure. Explicitly written to replace `MachineName`-based identity *because* two machines can share a hostname, which "LAN sync … does not accept" (comment at `DeviceIdentity.cs:9-11`) |
| Schema upgrade mechanism for existing DBs | `Data/SyncSchema.EnsureColumns` + `MigrationReporter`, exercised by `SyncSchemaDualPathTests` |
| Epic 1 released, Epic 3 code-complete + QA-closed, WP-1…WP-6 stabilization closed | `system_roadmap.md` §A.2; **Epic 2 entry criteria 12/12**, closed 2026-08-02 (`2026-08-02-wp6-repo-doc-hygiene.md` §Follow-ups) |

**Net:** the data-model half of Epic 2's prerequisite (D-B: "sync-ready data model first") is real, tested, and load-bearing in production — not aspirational documentation. This matches what `data-model.md` §8 and `sync-data-model.md` claim; nothing in the docs overstates the code.

### 1.2 Absent — confirmed by GitNexus query, zero hits

Queried `Smart-Study` for `merge`, `conflict record`, `snapshot`, `watermark`, `peer`, `transport`, `discovery`, `LAN` — **zero processes, zero definitions**. Confirmed independently by `Grep` for `Snapshot|LastSynced|BaseSnapshot` in `SmartStudyPlanner/` — the 9 hits are all unrelated (`ScheduleOptimizer`'s `Optimize` snapshots, `UserStatsSnapshot`, `MigrationReporter.Capture`'s table-snapshot). **No sync module, no base-snapshot store (T1.4), no change-enumeration query, no merge/conflict/transport code exists anywhere in the tree.** This matches `system_roadmap.md`'s "not started: no task planned, no branch, no code" (2026-08-20) and `active/README.md`'s "has not been started" (2026-08-27) — still true today.

### 1.3 A concrete gap the docs don't yet name

**`SyncStamper.Apply` is unconditional, and that's a problem for T2.3, not just a fact about T1.1.**

```csharp
// Data/SyncStamper.cs:20-42
if (entry.State is EntityState.Added or EntityState.Modified)
{
    meta.Rev++;
    meta.ModifiedAtUtc = utcNow();
    meta.ModifiedByDeviceId = deviceId;
}
```

Every `Added`/`Modified` entry passing through `AppDbContext.SaveChanges*` gets stamped with **this device's** clock and **this device's** id, unconditionally. That's correct for a local edit. It is wrong for applying a merge result: when the merge engine (T2.3) writes the winning value of a field-level or LWW conflict onto the local DB, the winning `ModifiedAtUtc`/`ModifiedByDeviceId` **must be preserved as the remote device's original values** — those are exactly the inputs the *next* sync round's LWW tie-break reads (per D-I: `ModifiedAtUtc` → `DeviceId`). If `SyncStamper` re-stamps them with "now" and "local device," the provenance a future 3-way merge needs is destroyed on the first apply.

`Rev`, on the other hand, is documented as a purely local, per-device enumeration counter (L6) — so a merge-applied write plausibly *should* still bump local `Rev`, to mark "this row changed locally and needs re-offering to other peers next round." That's a different rule from `ModifiedAtUtc`/`ModifiedByDeviceId`, which the merge write must *not* let the seam overwrite.

`SqliteHocKyRepository.cs:224-241` (`CopySyncSafeValues`) already solves the mirror-image problem for the existing single-device write path: it protects a tracked entity's `ISyncMetadata` snapshot from being stomped by an incoming detached POCO's stale/default values, restoring `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId`/`IsDeleted`/`DeletedAtUtc` right after `SetValues(source)`. A sync-apply path needs the same shape of guard, in the other direction — preserve *incoming* remote metadata against the seam's own stamping, rather than protect existing local metadata against an incoming POCO.

This is **not** an owner policy question — it's an engineering seam gap sitting directly under T2.3, and it belongs in that task's Definition-of-Ready as a named design point, with (at least) two candidate mechanisms to choose between at implementation time:
- A scoped suppression/flag on `SyncStamper`'s stamping (e.g., an "applying remote state" mode that skips the `ModifiedAtUtc`/`DeviceId` overwrite but still bumps `Rev`), or
- A separate write path for merge-apply that bypasses `AppDbContext.SaveChanges*`'s normal stamping call entirely (raw `ExecuteUpdate` or a dedicated context method) — which would need its own audit against the "single write path" invariant (`sync-data-model.md`, WP-3's "no `ExecuteUpdate`/`ExecuteDelete`/raw-SQL bypass on synced entities").

Neither option is chosen here.

### 1.4 T2.2's enumeration surface doesn't exist yet, and it's a new capability, not a small extension

Entry criterion #4's own wording ("every repository read path filters `!IsDeleted`... with a regression test per previously-leaking path") is about *keeping tombstones invisible to the app* — it is not evidence that a *tombstone-inclusive* enumeration path exists anywhere. It doesn't. T2.2 ("change enumeration per peer using the Rev watermark") needs a genuinely new query surface — "give me every row of entity X with `Rev > watermark`, tombstones included" — across all six synced entities, that no current repository interface exposes. Budget it as new surface area, not a filter tweak.

---

## 2. Dependency graph, T1.4 + T2.1–T2.6

```
                    Epic 1 (Released) ── hard prerequisite for all of Epic 2
                          │
                          ▼
        ┌─────────────────────────────────┐
        │  T1.4 — last-synced base-snapshot│   moved from Epic 1 to M2.1 by design
        │  store (per-peer)                │   ("co-designed with its only consumer")
        └────────────────┬─────────────────┘
                          │  defines the diff surface
                          ▼
        ┌─────────────────────────────────┐
        │  T2.2 — change enumeration per   │◄── new query surface (§1.4); reads Rev
        │  peer via Rev watermark          │    watermark + (with T1.4) the base snapshot
        └────────────────┬─────────────────┘
                          │  enumerated changes feed the diff
                          ▼
        ┌─────────────────────────────────┐
        │  T2.3 — 3-way field-level merge, │◄── needs the SyncStamper fix/seam (§1.3)
        │  LWW ModifiedAtUtc→DeviceId      │    before it can safely write results
        └────────────────┬─────────────────┘
                          │  produces conflicts as a side output
                          ▼
        ┌─────────────────────────────────┐
        │  T2.4 — conflict records;        │◄── consumes T2.3's LWW losers +
        │  delete-vs-edit → tombstone wins │    tombstone-vs-edit races
        └────────────────┬─────────────────┘
                          │  needs a system under test
                          ▼
        ┌─────────────────────────────────┐
        │  T2.5 — convergence / no-loss /  │◄── exercises T1.4+T2.2+T2.3+T2.4 together,
        │  offline-window property suite   │    fixture peers, no network (M2.1, pure)
        └────────────────┬─────────────────┘
                          │  M2.1 complete and gate-free
                          ▼
        ┌─────────────────────────────────┐
        │  T2.1 — GATE G4: transport /     │  BLOCKED on an owner decision (§4).
        │  discovery / trigger + max       │  Not code-dependent on T2.2–T2.5, but the
        │  offline window                  │  master plan places it in M2.2, after M2.1,
        └────────────────┬─────────────────┘  because "the transport/offline-window
                          │                     decisions get made with real [merge-engine]
                          ▼                     data" (master plan, execution-order rationale)
        ┌─────────────────────────────────┐
        │  T2.6 — retention/purge job      │  gated on G4's outcome; safe default
        │  (moved from Epic 1)             │  (never purge) until G4 closes
        └─────────────────────────────────┘
```

**Key structural fact (already frozen by the master plan, re-verified against the current tree):**
T1.4 + T2.2 + T2.3 + T2.4 + T2.5 form **M2.1 — "merge engine, no network (pure, fixture peers)"**.
None of that milestone touches a network socket or requires G4 to be decided. **G4 blocks only M2.2**
(T2.1 itself, plus T2.6). This is a real, code-verifiable separation, not just a planning
convenience: nothing in the current tree or in T2.2/T2.3/T2.4/T2.5's stated shape needs a transport,
discovery mechanism, trigger model, or offline-window number to exist.

---

## 3. Proposed implementation sequence

Follows the master plan's own milestone shape (M2.1 → M2.2 → M2.3); nothing here reorders it.

1. **T1.4 + T2.2 together, as one slice.** See §5 — T2.2's enumeration output shape and T1.4's
   snapshot shape are mutually constraining (the master plan itself says T1.4 was "co-designed with
   its only consumer, the 3-way diff" — T2.2's enumeration is the other half of that same diff).
   Designing one without the other pre-decides an interface that hasn't been reviewed.
2. **T2.3** (3-way field-level merge + LWW), gated on resolving §1.3's `SyncStamper` seam question
   as part of its own DoR.
3. **T2.4** (conflict records; delete-vs-edit → tombstone wins), on top of T2.3's output.
4. **T2.5** (convergence / no-data-loss / offline-window property suite) — can start incrementally
   alongside T2.2–T2.4 (property tests for enumeration and merge don't need conflict records to
   exist yet), but the "offline-window" property in its name implies at least a placeholder
   parameter for whatever G4 eventually sets; full coverage waits on G4 only for that one
   dimension.
5. **T2.1 (GATE G4)** — front-load the *decision* (owner discussion, no code) as early as capacity
   allows, per the master plan's own "Decision gates (front-load)" table; the master plan's stated
   reason to sequence the transport *code* after M2.1 is that "the transport/offline-window
   decisions get made with real [merge-engine] data" — not that the decision can't be *discussed*
   earlier.
6. **T2.6** (retention/purge job) — implements whatever G4 decides; safe no-purge default is already
   the documented fallback and needs no new code to remain in effect in the interim.
7. **M2.3** (two-real-machines end-to-end + sync status/trigger UX) — after T2.1's transport exists.

This is the master plan's own order; this pass does not change it. What this pass adds is the
T1.4+T2.2 pairing in step 1 (§5) and the explicit note that G4's *discussion* need not wait for M2.1
to finish, only G4's *code* does.

---

## 4. Owner decisions required before implementation

None of these are inferred or chosen in this document. Each is either explicitly open in the repo's
own decision records, or newly surfaced by this pass and flagged as needing the same treatment
(a `docs/plans/YYYY-MM-DD-*.md` decision note, per the master plan's DoD-6).

| # | Decision | Status in repo | What's genuinely open |
|---|---|---|---|
| 1 | **T2.1 — LAN transport mechanism** (e.g., a local HTTP/TCP listener, a library-mediated P2P channel) | **Unfrozen.** D-A rejected "self-hosted LAN server / one-way replica / manual export" only as *architectures* (chose two-way merge over them), not as transport candidates for the winning architecture. No later decision record sets a specific transport | Fully open — needs its own design note (D-A explicitly says the conflict-resolution/implementation detail "needs its own design note before implementation") |
| 2 | **T2.1 — discovery mechanism** (mDNS/UDP broadcast, manual IP entry/pairing, QR/code pairing, etc.) | **Unfrozen.** Not mentioned in D-A, D-F, D-I, or the master plan beyond the "T2.1 — GATE G4" label | Fully open |
| 3 | **T2.1 — sync trigger model** (manual button, automatic on LAN presence, periodic background) | **Unfrozen.** Master plan's Epic 2 risk section says "trigger stays manual-first" as a *risk mitigation framing*, not a ratified decision | Whether "manual-first" is accepted as the actual v1 answer, or left open pending the transport/discovery choice |
| 4 | **T2.1 — maximum offline window** | **Unfrozen**, and explicitly gates G4 (retention depends on it) | The number itself; nothing in the repo proposes even a provisional value |
| 5 | **G4 — tombstone retention/purge authority** | **On the record as open** since WP-6 (`system_roadmap.md` §A.3 item 3, `active/README.md`). Two candidate models are already named in the repo's own lessons-learned, not by this pass: **(a)** retention length ≥ maximum offline window, or **(b)** purge only after seen-by-all-devices acknowledgment (`lessons-learned.md` L9, "Impact on future development") | Which model (or hybrid), and the retention parameter itself. This pass does not recommend between them |
| 6 | **Execution sequencing: Epic 2 vs. G3-1** | **Explicitly unresolved** in `active/README.md`: "G3-1 [wiring the SOE optimizer into production]... could reasonably come first. That call is the owner's and has not been made." | Whether Epic 2 implementation (beyond this planning pass) should proceed before, after, or interleaved with G3-1. This pass was scoped as "readiness/planning... in parallel with... S-2" — it says nothing about G3-1's priority, and the master plan's own execution order (E1→E3→E2→E4) is silent on unscheduled follow-on work like G3-1 |
| 7 | **`SyncStamper` merge-apply seam (§1.3)** | Newly surfaced by this pass, not previously documented | Which mechanism (scoped suppression vs. separate write path) T2.3 uses to write merge results without corrupting LWW provenance. Engineering choice, but flagging it here because it affects T2.3's Definition-of-Ready and should be settled in a plan/decision note before T2.3 starts coding, per the project's own DoR convention |

---

## 5. Recommended first slice

**T1.4 + T2.2, taken together, is the smallest independently executable slice** — not T2.2 alone.

Checked against the project's own Definition-of-Ready (master plan §"Definition of Ready"):
- **Decisions closed in its blast radius:** yes — neither T1.4 nor T2.2 touches G4 or any transport
  decision; both live entirely inside M2.1, which the master plan already marks gate-free.
- **Upstream done:** yes — Epic 1 is Released, the D-I metadata block T2.2 reads is live and tested.
- **Interfaces stable for a parallel/first slice:** **this is where T2.2 alone fails.** The master
  plan itself moved T1.4 out of Epic 1 specifically because it should be "co-designed with its only
  consumer, the 3-way diff" (i.e., T2.2's enumeration + T2.3's diff). Building T2.2's enumeration
  query shape first, in isolation, would silently pre-decide what the base-snapshot store looks like
  — the exact mistake the master plan's v1→v2 revision was written to avoid (see master plan
  "Changes in v2," item 4). Pairing T1.4+T2.2 in one slice keeps that co-design honest without
  pulling in T2.3's merge-write concerns (§1.3), which are a separate, harder problem.
- **No network, no transport, network-free per the master plan's own M2.1 framing:** yes, for both.
- **Test strategy nameable up front:** yes — fixture-peer unit/property tests against two or three
  in-memory or SQLite-fixture "peers," per the existing test-structure convention (mirrors
  `Infrastructure/Persistence/` namespaces).

**What this slice should ship:** the base-snapshot store's schema/persistence (T1.4) and a
per-entity, tombstone-inclusive, Rev-watermarked enumeration query (T2.2) across all six synced
entities, plus tests proving enumeration correctness (new rows, tombstoned rows, and rows below the
watermark are each handled correctly) against fixture data. It should explicitly **not** attempt the
3-way diff or any merge-write — that's T2.3, which additionally needs §1.3's `SyncStamper` question
settled first.

---

## 6. Write-scope and merge-conflict surface with other active agents

- **No overlap in source files.** Epic 2 implementation (once started) touches
  `SmartStudyPlanner/Data/*`, `Infrastructure/Persistence/**`, `Models/*.cs` (already touched by
  Epic 1; Epic 2 adds new files rather than editing the sync-metadata shape), and a new sync module
  (likely `Services/Sync/*` or `Infrastructure/Sync/*`, unnamed as of this pass). The concurrent S-2
  workstream's footprint (per its own branch, `docs/encoder-knowledge-consolidation`) is
  `docs/plans/`, `docs/specs/`, `docs/reports/`, and `tools/ml-pilot/` — disjoint from Epic 2's
  production-code surface entirely.
- **The one real collision surface is `docs/specs/system_roadmap.md`.** Both workstreams edit it:
  S-2 for encoder/data-maturation status, Epic 2 for its own "Next up" §A.3 item 3 once work starts.
  The S-2 branch is 28 commits ahead of `origin/docs/encoder-knowledge-consolidation` and unmerged —
  a future edit to this file from Epic 2 work should diff against `dev`'s copy at merge time, not
  assume the S-2 branch's in-flight version.
- **This report itself is collision-free** — new file, new branch (`docs/epic2-lan-sync-readiness`,
  based on `dev`), written in an isolated worktree so the S-2 branch's checked-out working tree was
  never touched to produce it.
- **`docs/active/README.md`** is not edited by this pass. Its own rules reserve a tracker row for
  work actually in progress; this pass produced a plan awaiting review, not started work. Adding a
  row is the natural next step once/if the owner accepts this plan — left to that decision.

---

## 7. What this pass did not do

- No production code was read for editing, and none was changed — `gitnexus_detect_changes` and a
  build/test run are not applicable to this pass (nothing to detect).
- No current suite count is asserted here — `system_roadmap.md` and its A.4 addendum disagree (487
  vs. 492) as of different dates, and this pass ran no test suite; citing a number from memory would
  be a guess dressed as a fact.
- No LAN transport, discovery mechanism, trigger model, or offline-window value was chosen. No
  tombstone retention model was chosen. No call was made on Epic 2 vs. G3-1 sequencing.
- No Data Maturation artifact, S-2 guideline, S-0 snapshot, reader package, scoring key, or S-1/S-2
  execution record was read.

---

## 8. Summary for the owner

Epic 1's sync-ready substrate is real and correctly built — `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId`/
tombstones are stamped through one seam, every display path already hides tombstones, device identity
is LAN-safe. Nothing in Epic 2's scope (T1.4, T2.2–T2.6) is blocked by missing Epic-1 work. What's
missing is exactly what the docs already say is missing: the merge engine itself (zero code), plus one
seam gap this pass found that the docs didn't yet name — `SyncStamper`'s unconditional stamping will
corrupt LWW provenance the first time a merge tries to apply a remote-won value, unless T2.3 adds a
sync-apply path around it. Six decisions are needed before implementation goes past the T1.4+T2.2
slice (transport, discovery, trigger, offline window, retention model, and G3-1-vs-Epic-2 sequencing)
— none of them made here. The recommended first slice (T1.4+T2.2) can start today without any of those
six decisions, is network-free, and does not touch G4's blast radius.
