# Sync Data Model Lessons

> Distilled 2026-07-12 from Epic 1 (Sync-Ready Data Model: M1.1–M1.3) and the architecture-freeze
> lesson that predates it (L6). These are the durable mechanics of "how do you make local-first
> data safe to sync later" — the normative schema is
> [`../architecture/data-model.md`](../architecture/data-model.md); this article distills why it
> looks the way it does.

## Revision counters are local clocks — never compare them across devices

**Problem.** An early sync-metadata design considered a per-entity monotonic revision counter
(`Rev`) as the mechanism to arbitrate which of two devices' edits to a record was "newer" —
compare `Rev` across devices, higher wins.

**Why it was hard.** On a single device this is *literally true* — the counter is a perfect edit
clock — and it mirrors EF Core's familiar `RowVersion` optimistic-concurrency idiom, which lends it
false authority in a .NET codebase. It is also more attractive than timestamps because it is
skew-free: no clock synchronization problem to worry about. The flaw only appears when you ask what
a cross-device comparison actually proves.

**Wrong assumption.** That "device A is at `Rev 40`, device B is at `Rev 3`" tells you which edit
happened later. It does not — each device increments its own counter with no shared origin and no
communication, so the comparison proves only that A edits more often. Cross-device comparison of
local counters isn't weak evidence; it's no evidence. A related mistake: assuming a single
timestamp+tiebreaker answers both "which is newer" and "did one edit see the other" — those are
different questions (recency vs. concurrency), and no single scalar answers both.

**How it was solved.** The metadata was split into three roles, deliberately assigned to three
different mechanisms so no one field is asked to do more than one job — one field for change
enumeration and same-device ordering, a structurally different mechanism for cross-device
concurrency detection, and a third, narrower mechanism reserved only for tie-breaking a genuine
same-field conflict once concurrency is already established. The exact role-to-mechanism mapping is
the frozen decision (D-I, "Accepted #3") — see
[`../architecture/lessons-learned.md`](../architecture/lessons-learned.md#l6--revision-counters-are-local-clocks-they-cannot-order-events-across-devices)
for the table, rather than restating it here. Every synced entity (`HocKy`, `MonHoc`, `StudyTask`,
`StudyLog`, `TaskNote`, `TaskReferenceLink`) now carries the full metadata block, stamped through
exactly one seam.

**Principle.** Never let a single scalar answer two different questions. A local monotonic counter
is real information (it enumerates what changed, and orders edits *on that device*) — it becomes
noise the moment you compare it across an origin it was never synchronized against.

**How to avoid it next time.** Anyone tempted to "simplify" conflict resolution by ordering on
`Rev` across devices should be redirected to this lesson before writing that code.

## Single stamping seam: one interception point for every write

Rather than have every repository remember to stamp `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId` at
its own call site, `AppDbContext` overrides both `SaveChanges` overloads — the two entry points
every EF write actually routes through — and calls `SyncStamper.Apply(ChangeTracker, Clock,
DeviceId)` before the base save. This is the same shape as the review discipline in
[`review-methodology.md`](review-methodology.md): a correctness invariant that depends on "every
call site remembers to do X" is fragile; moving X to the one chokepoint every call site already
passes through removes the dependency on remembering. Verifying this seam's precondition — that no
`ExecuteUpdate`/`ExecuteDelete`/raw-SQL write on a synced entity bypasses it — was re-checked at
every milestone boundary rather than assumed to still hold once checked.

## Cascade-tombstone: converting deletes from `DELETE` to `UPDATE` breaks two things silently

**Problem.** Making deletes soft (tombstone instead of hard `DELETE`, required so a sync peer can
see "this record was deleted" rather than just missing it) breaks two independent things that both
depended on real `DELETE` semantics: EF's SQL-level `ON DELETE CASCADE` stops firing (children
become live-but-orphaned), and any "remove old rows, re-insert new ones with the same primary key"
persistence pattern now collides — the "removed" rows never actually leave the table.

**Why it was hard.** Both breakages are invisible until the exact path that depends on real-delete
semantics executes. The cascade gap (`DeleteAsync` skipping `TaskNote`/`TaskReferenceLink`) shipped
once already, undetected, because that repository method had zero production callers — its one
existing test used a childless task, so the orphan never manifested; green but blind. The
remove-then-recreate collision would only fail on *every subsequent save* of an already-migrated
row — a scenario no single-shot round-trip test exercises.

**Wrong assumption.** That `AppDbContext.OnModelCreating`'s `OnDelete(DeleteBehavior.Cascade)`
configuration became irrelevant once deletes stopped being real SQL deletes. It didn't — its role
changed. That configuration still drives EF's **in-memory** cascade fixup (when a tracked parent is
`Remove()`d, EF marks its *loaded* tracked children `Deleted` too); `SyncStamper` then converts
every `Deleted`-state entry it finds — parent and EF-cascaded children alike — into a tombstone
instead of letting a real `DELETE` reach the DB. The config is kept, not removed; its job is now
"drive fixup," not "produce a SQL cascade."

**How it was solved.**
- **FK-only children unreachable by EF's own fixup** (`TaskNote`, `TaskReferenceLink` have no
  navigation property back from `StudyTask`, only an FK) are hand-cascaded via a single shared
  `TaskCascadeHelper`, called from every task-removal path so the two delete paths cannot silently
  diverge again.
- **Completeness was proven, not assumed** — see [`review-methodology.md`](review-methodology.md#completeness-checks-against-ground-truth-not-against-the-one-site-you-touched)
  for how the full cascade child set was enumerated against `OnModelCreating` itself.
- **`LuuHocKyAsync`** (the app's single most-called write path — ten callers, including every
  subject/task add, edit, complete, and delete) was rewritten from remove-then-recreate to a
  Guid-keyed diff against the loaded DB graph: unchanged rows update in place, absent rows are
  `Remove()`d (tombstoned by the seam), new rows are `Add()`ed. This was found and fixed *before*
  M1.2 shipped, via `gitnexus_impact` surfacing the write path as a correctness blocker the plan
  text hadn't anticipated.

**Principle.** A cascade invariant is only as strong as its weakest call site — verifying it means
checking every relationship the schema itself declares, not just the one path a bug report named.
And: when "delete" changes its underlying mechanism (SQL `DELETE` → soft-delete `UPDATE`), every
persistence pattern that assumed deleted rows physically vanish must be re-audited, not just the
delete path itself.

**How to avoid it next time.** Whenever a new delete path is added to a synced entity, route it
through the shared cascade helper, and re-derive the full child set from `OnModelCreating` rather
than copying whatever the last delete path happened to handle.

## EF cascade-fixup reads the tracked snapshot, not the live collection

**Problem.** Reparenting a child entity to a surviving parent (updating its FK scalar and removing
it from the doomed parent's in-memory collection), then calling `Remove()` on the doomed parent,
*still* cascade-tombstoned the reparented child — even though it had, by every visible measure,
already been moved.

**Why it was hard.** It looks like ordering shouldn't matter: the child's `ObservableCollection`
membership was updated first, so intuitively the doomed parent no longer "owns" it by the time
`Remove()` runs. But EF Core's cascade-on-remove fixup for snapshot-tracked POCOs does not consult
live collection contents at all — it resolves a removed parent's dependents from its own **tracked
relationship snapshot**, current only as of the last `ChangeTracker.DetectChanges()` call.

**Wrong assumption.** That mutating a plain `ObservableCollection` navigation property is
sufficient to change what EF's cascade fixup "sees" at `Remove()` time. It is not, for
snapshot-based (not notification-based) change tracking — the FK scalar must have moved *and*
`DetectChanges()` must have run, or the fixup still reads the stale relationship snapshot.

**How it was solved.** Reorder the operation: reassign the child's FK scalar to the surviving
parent **and** force `ChangeTracker.DetectChanges()` **before** calling `Remove()` on the doomed
parent. By the time cascade fixup runs, the tracked snapshot already shows the child belonging
elsewhere, so fixup only touches genuinely-orphaned rows. This was only caught because the
discriminating test asserted actual row counts and specific rows read back correctly — not merely
"no exception thrown," which a first, wrong implementation attempt already satisfied while silently
losing a task.

**Principle.** With snapshot-based EF Core change tracking, collection mutation is not the same as
tracking-state mutation. Only `DetectChanges()` (or `SaveChanges`, which calls it internally)
reconciles the two. When reordering a parent-removal-plus-child-reparent sequence, the reparent (FK
reassignment + explicit `DetectChanges()`) must happen *before* the parent's `Remove()`, not merely
before the eventual `SaveChanges()`.

**How to avoid it next time.** "No exception" is not sufficient verification for any reconcile or
merge fix — assert the actual resulting data shape (counts, specific surviving/tombstoned rows). A
swallowed data-loss bug throws nothing; only an assertion on the real shape of the data catches it.

## Identity semantics: normalize keys, and defend at both ends

**Problem.** The same real-world subject existed as multiple database rows differing only in case
or whitespace ("Toán" / "toán " / "Toán  "), which silently broke every naive equality-based
grouping or `Distinct()` call across four independent read sites.

**Why it was hard.** Guid primary keys already solve *key-collision* identity (no two rows can
share an ID), which makes it easy to conflate "has a stable ID" with "has a correct semantic
identity." A schema/ID fix does nothing about two *different* IDs meaning the same real-world
thing.

**Wrong assumption.** That four separately-written, ad-hoc dedup implementations (raw-string
`GroupBy`/`Distinct` at each read site) would stay consistent by convention over time; and that
fixing the one call site literally named in a spec (a dropdown's `Distinct()`) was the same as
fixing the feature — the dropdown's output immediately fed a downstream filter comparing against
raw strings, so deduping only the display left the filter silently excluding logs from whichever
clone wasn't the chosen display representative. A dedup fixed at only one consuming site is a dedup
that half-works.

**How it was solved.** One normalization function (`MonHocIdentity.Normalize`: NFC-normalize → trim
→ collapse internal whitespace → invariant-culture lowercase, **diacritics preserved** — an
explicit, owner-locked design choice) behind a single `IEqualityComparer`, routed through by every
consumer: all four read-side dedup sites, *and* an add-time prevent-at-source check that rejects a
new subject name normalizing-equal to an existing live one before it is ever persisted. Widening
the dedup key surfaced a pre-existing reconcile gap in `LuuHocKyAsync` — see the cascade-fixup
lesson above and [`review-methodology.md`](review-methodology.md#reproduce-before-escalating-the-protocol-for-a-suspected-pre-existing-bug-in-accepted-code)
for how that finding was verified before being folded into the fix.

**Principle.** Prevent-at-source (reject the duplicate before it exists) and read-side dedup (merge
duplicates that already exist) are two different defenses for the same identity problem, and an
alpha-stage system without cross-device identity-merge needs both: prevent-at-source does not
retroactively fix rows that already diverged before the check existed, and read-side dedup alone
lets duplicate rows accumulate without bound. Centralize the identity definition exactly once, and
audit every *consumer* of the field it groups by — not just the site literally named in a spec —
because a downstream filter reading the same raw field is exactly where a "half-fixed" dedup hides.

**How to avoid it next time.** When centralizing an identity/equality definition, grep for every
consumer of the field being grouped/compared, not just the call site a ticket names. This is
explicitly bounded to `MonHoc` in the alpha; true cross-device identity-merge is out of scope until
Epic 2's merge engine exists.

## See also

- [`../architecture/data-model.md`](../architecture/data-model.md) — the current, normative schema
  (§2 entities, §3 cascade rules, §8 sync-readiness state).
- [`../plans/2026-07-03-g1-soft-delete-cascade.md`](../plans/2026-07-03-g1-soft-delete-cascade.md) — the G1 decision record (cascade-tombstone chosen over orphan-in-place).
- [`release-engineering.md`](release-engineering.md) — the upgrade seam that backfills this
  metadata onto pre-existing rows, and the backup gap found alongside it.

## Sources

- [`../architecture/lessons-learned.md`](../architecture/lessons-learned.md) — L6 (`Rev` is a local
  clock), L7 (merge granularity bounded by tracking granularity), L9 (guarantee trilemma)
- [`docs/review/2026-07-11-epic1-closure-verdict.md`](../review/2026-07-11-epic1-closure-verdict.md) — L6 code-verification, G1 status
- [`docs/reports/2026-07-05-epic1-m1.2-schema-upgrade-tombstones-metadata.md`](../reports/2026-07-05-epic1-m1.2-schema-upgrade-tombstones-metadata.md) — `ISyncMetadata`, cascade-tombstone, `LuuHocKyAsync` reconcile-rewrite
- [`docs/review/2026-07-10-epic1-m1.2-r1-remediation-review.md`](../review/2026-07-10-epic1-m1.2-r1-remediation-review.md) — `TaskCascadeHelper`, completeness check
- [`docs/reports/2026-07-10-epic1-m1.3-monhoc-identity-dedup.md`](../reports/2026-07-10-epic1-m1.3-monhoc-identity-dedup.md) — D1–D4, `MonHocIdentity`, the cascade-fixup timing fix (D3)
- [`docs/review/2026-07-11-epic1-m1.3-review.md`](../review/2026-07-11-epic1-m1.3-review.md) — independent hand-trace of the reconcile fix
