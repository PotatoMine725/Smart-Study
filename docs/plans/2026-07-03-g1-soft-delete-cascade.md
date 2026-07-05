# G1 — Soft-Delete Cascade Policy (decision note)

**Gate:** G1 (per [`2026-07-03-master-plan.md`](2026-07-03-master-plan.md))
**Closes in:** M1.2 (per [`2026-07-03-epic-1-execution-plan.md`](2026-07-03-epic-1-execution-plan.md))
**Decision:** Cascade-tombstone

## Decision

When a parent entity with live children is soft-deleted, all live descendants are tombstoned
in the same `SaveChanges` transaction — mirroring today's hard-cascade UX (deleting a `HocKy`
today wipes its `MonHoc` and `StudyTask` rows) without EF's `OnDelete(Cascade)` ever reaching
the DB as a real `DELETE`.

## Why cascade, not orphan-in-place

- **Preserves existing UX.** Users already expect deleting a semester/subject to remove
  everything nested inside it. Orphaning children (leaving them live with a dead parent)
  would be a silent behavior change no one asked for.
- **Sync correctness.** An orphaned live child with no parent is a state a merge peer can't
  reason about. A cascaded tombstone is an unambiguous "this whole subtree is gone."

## Mechanism

`AppDbContext`'s `OnModelCreating` already configures `OnDelete(DeleteBehavior.Cascade)` for
every parent/child relationship in the synced graph (`HocKy`→`MonHoc`, `MonHoc`→`StudyTask`,
`StudyTask`→`TaskNote`/`TaskReferenceLink`). That configuration is **kept, not removed** — it
now drives EF Core's in-memory `ChangeTracker` cascade *fixup* (when a tracked parent is
`Remove()`d, EF automatically marks tracked/loaded children as `Deleted` too) rather than a
real SQL cascade. `SyncStamper.Apply` then converts **every** `Deleted`-state entry it finds
(parent and EF-cascaded children alike) into a tombstone: `State = Modified`, `IsDeleted =
true`, `DeletedAtUtc` stamped, `Rev` incremented. No entity is issued a real `DELETE`
statement; the FK constraint's `ON DELETE CASCADE` in the generated schema never fires.

**Precondition:** cascade fixup only marks children EF has *loaded* into the tracker as
`Deleted`. A `Remove()` on an untracked/unloaded parent will not cascade in-memory — callers
must load the subtree (as `SqliteHocKyRepository` already does via `.Include()`) before
removing.

## Consequence surfaced during M1.2 (not assumed by the plan)

`HocKy`/`MonHoc`/`StudyTask` deletes are not expressed as explicit `Remove()` calls today —
`XoaTask`/`XoaMon` delete by *absence*: the item is dropped from the in-memory graph, then the
whole graph is re-persisted via `LuuHocKyAsync`, which previously hard-deleted the entire old
subtree and re-inserted the new one with the same GUIDs. Once deletes become tombstones (an
`UPDATE`, not a `DELETE`), that remove-then-recreate pattern would collide on the primary key
of every unchanged row on every single save. `LuuHocKyAsync` was reconciled (diff new graph
vs. DB: update existing rows in place, add new rows, `Remove()` rows absent from the new graph)
so genuine deletions still cascade-tombstone via the mechanism above, while unchanged rows keep
their identity and `Rev` history instead of being churned every save.
