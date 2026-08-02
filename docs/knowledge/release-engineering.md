# Release Engineering Lessons

> Distilled 2026-07-12 from the Epic 1 closure gate — specifically finding **F5** (WAL backup gap)
> and the T1.8 schema-upgrade seam. Concrete root causes, concrete fixes, reusable principles.
>
> Extended 2026-08-02 from WP-6 (repo & doc hygiene, the last package of the post-Epic-1
> stabilization phase) — the cannot-vs-not-mine-to-decide distinction, the `enforce_admins`
> escape hatch, and the preserve-then-destroy pattern.

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

## "Cannot" and "not mine to decide" look identical from the outside

**Problem.** The post-Epic-1 stabilization plan justified deferring branch-protection setup as
"not an agent action — enabling branch protection needs admin scope on the repository and cannot
be done from the CLI without it." That premise was false: `gh api repos/:owner/:repo` returned
`{"admin":true,...}` — the token had the scope, and the write would have succeeded.

**Why it matters.** The step was still correctly left undone, but for a different reason: a
required status check rejects direct pushes with no passing run, and every commit in the plan's
six packages went straight to `dev`. Enabling it would have converted the owner's daily push-to-
`dev` workflow into PR-per-change — a decision about how someone wants to work, not a technical
gap. Had the permission check gone the other way, the *correct* justification would never have
surfaced, because the plan collapsed two different blockers into one sentence.

**Reusable principle.** *Unable* and *not mine to decide* produce the same visible outcome — the
step doesn't happen — but they are fixed by completely different things: one by finding a token
with more scope, one by asking. Before writing "cannot" as the reason to skip an action, check
whether the actual constraint is authority (you could, but it isn't your call) rather than
capability (you literally cannot). Naming the wrong one either invites someone to fix a
non-problem (find more scope) or lets a real permission gap hide behind a policy-sounding excuse.

## Branch protection's escape hatch exempts the account that actually pushes

**Problem.** `enforce_admins=false` is a reasonable default on a solo repository — it is the
escape hatch that stops a required check from locking the owner out of their own repo. Its
second-order effect is easy to miss: **admins are exempt from the protection entirely**,
including the required check and the force-push block. On a repo whose only pusher is an admin,
the rule is configured and currently binds nobody who uses it.

**How it was caught.** Same standard as the mutation-testing lesson in
[`review-methodology.md`](review-methodology.md#a-green-check-is-evidence-only-after-youve-shown-it-can-go-red) —
*a signal that has not been shown to go red is not yet evidence*. The push that carried the report
recording this went straight to `dev` and succeeded, which is the actual proof: the protection
did not block it. Reading the settings back after writing them (rather than trusting the API's
write response) confirmed the values but not the behavior; only an observed push-that-should-have-
been-blocked-and-wasn't (or, symmetrically, was) closes the gap between configured and enforcing.

**Resolution used here:** split the setting per branch by how it is actually used, not
uniformly. `dev` (`enforce_admins=false`) is daily iteration with no second reviewer, so
enforcement would cost real friction for zero benefit. `main` (`enforce_admins=true`) was
already PR-only in its entire commit history, so enforcement there costs nothing and converts an
existing habit into a guarantee instead of a convention.

**Trap: the endpoint is `POST`, not `PUT`.** `PUT …/branches/{branch}/protection/enforce_admins`
returns `404 Not Found`, which reads like a missing resource — the kind of error that sends the
next person looking for a typo'd branch name rather than a wrong HTTP verb.

```bash
gh api -X POST   repos/:owner/:repo/branches/main/protection/enforce_admins   # enable
gh api -X DELETE repos/:owner/:repo/branches/main/protection/enforce_admins   # the escape hatch
```

**Reusable principle.** When a protection or gate has an admin/owner bypass, state explicitly
who it does and does not bind, not just whether it is "on." "Configured" and "enforcing against
the account that pushes" are different claims, and the second is the one that matters.

## Preserve, then destroy — never verify, then destroy

**Problem.** Retiring the untracked root `Assets/` directory required deleting data `git` cannot
restore. The plan's safety net was `diff -r --brief Assets docs/assets/icon-source` before the
delete, confirming the only differences were an intentionally-not-copied `icon.ico` and a new
`README.md`.

**Why a diff alone is not enough.** A `diff` proves the bytes matched *at that instant*. It does
not survive the deletion that follows it — if the copy silently dropped a file (a shell `cp`
losing a filename with a space in it, for instance — `Icon Preview.html` was exactly this case),
the diff would have already run and passed before anyone looks again.

**How it was solved.** The copy was committed as its own commit *before* the deletion, and the
resulting tree was inspected with `git show --stat` — confirming all 8 files were actually
present in git, including the space-containing filename — before `rm -rf` ran. This converts
"verified, then destroyed" into "preserved, then destroyed": the second state has a durable
witness (a commit) instead of a point-in-time check that a later step could invalidate.

**Reusable principle.** For any step that destroys the only copy of something, prefer a
guarantee that outlives the step (a commit, a backup with its own verification) over a
pre-flight check performed immediately before the destructive action. The check and the action
happen in sequence; a rollback source should not depend on nothing having gone wrong between
them. See also the [WAL backup lesson](#the-wal-backup-lie) above — same shape, different
irreversible step (an in-place file overwrite there, an `rm -rf` here).

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
- [`docs/reports/2026-08-02-wp6-repo-doc-hygiene.md`](../reports/2026-08-02-wp6-repo-doc-hygiene.md) — §3.1 preserve-then-destroy, §3.5/§3.5a/§3.5b the cannot-vs-not-mine-to-decide distinction and the `enforce_admins` escape hatch
