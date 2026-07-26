# Release Engineering Lessons

> Distilled 2026-07-12 from the Epic 1 closure gate — specifically finding **F5** (WAL backup gap)
> and the T1.8 schema-upgrade seam. Concrete root causes, concrete fixes, reusable principles.

## The WAL backup lie

**Problem.** `DbBackup.CreateBackup` was a `File.Copy` of the main `.db` file only — no `-wal`/
`-shm`, no checkpoint. The DB runs in SQLite WAL mode, and application startup commits writes on
an open connection before the backup copy runs. Any data still sitting in an un-checkpointed WAL
file is silently absent from the backup; restoring it would roll back to the last checkpoint with
no error. This was discovered live: 213 KB of pending WAL beside the real dev DB, left by a
previous session — and that dev DB turned out to be a genuine pre-Epic-1 database (5,402
`StudyLog` rows), not the fixture-only environment four prior milestone reports had assumed. The
first real in-place schema upgrade against real data had, at closure time, still never happened.

**Why it was hard.** The bug hides exactly where verification stops. Every backup/migration test
in the milestone used fixture databases that were opened, written, and then *cleanly closed*
before the test asserted anything — so a WAL file was never pending when the copy ran. A
file-copy backup and a checkpoint-then-copy backup are indistinguishable on every fixture built
that way. The gap only exists on a *live*, still-open connection with uncommitted WAL pages — a
state no clean round-trip test can produce by construction, and a state that looks, from the
outside, exactly like "the backup works."

**Wrong assumptions.**
- "No real pre-Epic-1 database exists to copy" (stated in the M1.2 report) — true when written,
  false by the time of closure; environment facts like this expire and must be re-checked at every
  milestone boundary, not assumed to still hold.
- "A file-copy of a SQLite `.db` is a backup" — true only when the WAL is empty or checkpointed;
  false in general for a WAL-mode database with any open writer history.
- "Clean-fixture tests validate the backup path" — they validate the *copy* mechanism, not the
  *completeness* of what gets copied.

**How it was solved.** The closure verdict amended itself mid-review (condition **C3**) rather than
reopening the whole milestone: (a) harden `DbBackup` to run `PRAGMA wal_checkpoint(TRUNCATE)` on a
short-lived connection immediately before the file copy — a no-op on non-WAL databases, so
existing behavior for already-checkpointed DBs is preserved; (b) replace the fake-text backup
fixtures with minimal real SQLite databases so a live-WAL scenario is actually representable in a
test; (c) require a supervised first real upgrade against the dev DB, with a manual full backup
already secured as an interim safety net, before signing the release. See the decision record for
the fix's exact placement and failure semantics (checkpoint failures propagate rather than being
swallowed — fail loudly at startup rather than upgrade against an incomplete backup).

**Reusable principle: the first real run is a milestone, not a formality.** A migration or
backup path exercised only against synthetic, cleanly-closed fixtures has not been verified against
production conditions — it has been verified against a *simplified model* of them. The first time
the code runs against genuinely existing user data is a distinct event that deserves its own gate
(supervised launch, reference counts captured before and after, a real rollback path standing by),
not an assumption that passing fixtures already covered it.

**How to avoid it next time.** Before declaring a backup/migration safety net complete, ask: *does
any fixture in this suite ever leave state open, dirty, or pending the way production state can?*
If every fixture is clean-closed, the suite has a structural blind spot regardless of how many
tests it has. Add at least one test that deliberately leaves state "live" (open connection,
uncommitted writes) and drives the safety net against it.

## Migration safety mechanics (T1.8 schema-upgrade seam)

The upgrade seam that ships alongside the backup fix is `Data/SyncSchema.EnsureColumns` — an
idempotent, versioned upgrade path (`ALTER TABLE ... ADD COLUMN`, gated per-column on
`PRAGMA table_info` since SQLite's `ADD COLUMN` has no `IF NOT EXISTS`), backed by:

- **`DbBackup.CreateBackup`** — runs only when `SyncSchema.NeedsUpgrade` is true, so a
  fully-migrated DB launched again does no work and accumulates no backup files.
- **`MigrationReporter`** — captures a per-table row count + content checksum before and after.
  **Row count is the primary lossless-upgrade signal.** The checksum is only meaningful when
  restricted to the columns common to both snapshots — new columns are *expected* to differ
  pre/post (that is the migration, not corruption), so a before/after comparison must pass the
  pre-upgrade column list explicitly.
- **A real file-based startup test**, not just `:memory:` — a pre-commit review during the
  milestone flagged that every test up to that point drove `AppDbContext` against an
  externally-owned, always-open in-memory connection, which can never exercise `File.Copy`
  against a file another connection still holds open. The fix extracted the whole bootstrap
  sequence into a testable `AppStartup.EnsureDatabaseReady(db, dbPath)` and added a test that opens
  and closes a real file-based connection the way `OnStartup` actually does.

**Principle:** independent backfills over combined ones. When two columns can each independently
need a default value, gate each one's backfill on its own null-check — a single combined `WHERE`
across both silently skips rows that only need one of the two backfilled. This class of bug is
exactly what a "backfill from pre-existing column X" plus "backfill missing device id" combination
produces if collapsed into one predicate.

## See also

- [`sync-data-model.md`](sync-data-model.md) — the tombstone semantics this upgrade seam populates
  (`Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId`/`IsDeleted`/`DeletedAtUtc`) and why `LuuHocKyAsync`
  had to move off remove-then-recreate once deletes stopped being real `DELETE`s.
- [`review-methodology.md`](review-methodology.md) — how independent re-verification (re-running
  the touched namespace, not trusting the self-report) is the same discipline that would have
  caught F5 earlier, had the reviewer specifically probed "what does a live WAL do to this."

## Sources

- [`docs/review/2026-07-11-epic1-closure-verdict.md`](../review/2026-07-11-epic1-closure-verdict.md) — finding F5, conditions C1–C3
- [`docs/plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md) — Task A1 (WAL-safe backup fix) requirements
- `docs/plans/2026-07-12-epic1-closure-phase1-execution.md` (archived 2026-07-26 → `legacy/Archived plans/`, local-only) — D-P1–D-P3 (fix placement, failure semantics, fixture honesty)
- [`docs/reports/2026-07-05-epic1-m1.2-schema-upgrade-tombstones-metadata.md`](../reports/2026-07-05-epic1-m1.2-schema-upgrade-tombstones-metadata.md) — T1.8 upgrade seam, `MigrationReporter`, `AppStartupFileBasedTests`
