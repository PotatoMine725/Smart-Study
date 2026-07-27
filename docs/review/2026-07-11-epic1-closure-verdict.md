# PM Verdict — Epic 1 (Sync-Ready Data Model) Closure

**Reviewer:** PM (Fable 5 via Claude Code) — epic-level closure review, distinct from the per-milestone chief-engineer reviews
**Date:** 2026-07-11
**Under review:** `ui_rf` — Epic 1 range `f611653..a3a0a3d` (M1.1 `e968033`+`6e1c51f`, M1.2 `6734177`+`180999f`, M1.3 `d269cb4`…`fccb42f`, merge `a3a0a3d`) + post-close fix `101aaa3`
**Against:** [`docs/plans/2026-07-03-master-plan.md`](../plans/2026-07-03-master-plan.md) (source of truth) §Epic 1 + §DoD; [`docs/plans/2026-07-03-epic-1-execution-plan.md`](../plans/2026-07-03-epic-1-execution-plan.md)
**Inputs:** all four milestone reviews (M1.1 · M1.2 · M1.2-R1 · M1.3), all five milestone/remediation reports, the G1 decision note, and independent code + build/test verification (below)

---

## Verdict

> **✅ RATIFY THE CLOSE — no code refinement stage.** Epic 1's code substrate is verified sound
> against every epic-level acceptance criterion; the milestone review process caught and closed its
> defects before merge. **Two closeout conditions remain, both documentation-only (~1 hour):**
> (C1) an epic closing note reporting the measured success metrics — a master-plan **DoD-7**
> requirement that is currently unmet — including an explicit waiver for the three surviving
> telemetry fire-and-forget writes; (C2) roadmap A.2/A.3 + CHANGELOG sync for M1.3/Epic-1 closure
> (DoD-5 is stale at the epic boundary). Neither warrants re-opening the code.
>
> **AMENDED same day (closure debate):** finding **F5** — `DbBackup` misses WAL content on backup,
> and a **real pre-Epic-1 dev DB exists** (5,402 StudyLogs), so the first real in-place upgrade is
> still ahead, firing on the next app launch. Adds condition **C3**: one-line `DbBackup` hardening
> + a supervised first real upgrade + Epic-1-targeted GUI smoke pass. C3 is small code + one manual
> test session — still far short of a refinement stage, but the close is not signable before it.

## How this was verified (independent, not trust-the-reviews)

- **Build + full suite re-run on the merged tree** (`ui_rf` @ `101aaa3`, post-merge — a state no
  milestone review saw, since all four reviewed the worktree): `dotnet build` **0 errors**,
  `dotnet test --no-build` **330/330 passed**.
- **Read the shipped code** for every epic acceptance criterion: `ISyncMetadata` + all six entity
  implementations, `SyncStamper`, `SyncSchema` (6 tables × 5 columns, idempotent, independent
  backfills, `UpdatedAtUtc` reconcile), `TaskCascadeHelper`, `App.xaml.cs`/`AppStartup`.
- **Own grep sweeps:** fire-and-forget writes (`_ = …Async` — found 3 survivors, see F2);
  Epic-1 status in `system_roadmap.md` and `CHANGELOG.md` (stale, see F3).
- **Read the full review/report/decision-note trail** and cross-checked each review's claims
  against the code and against the master plan's acceptance criteria and DoD.
- **Report-sourced (not re-run):** the M1.1 p95 timing numbers; the M1.3 old-tree reproduction;
  the milestone-time `gitnexus_impact`/`detect_changes` runs.

---

## Epic acceptance criteria (master plan) — all met

| Criterion | State | Evidence |
|---|---|---|
| Every synced entity carries `Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`; `Rev` bumps on every local write, never compared across devices (L6) | ✅ | Code-verified: all 6 entities implement `ISyncMetadata`; `SyncStamper.Apply` stamps Added/Modified/Deleted; L6 noted at the seam |
| No hard delete on any synced entity; tombstones honor G1 | ✅ | Seam converts `Deleted`→tombstone; all 3 delete paths cascade via shared `TaskCascadeHelper` (M1.2-R1); child set proven complete against `OnModelCreating` |
| A6 closed: awaited, failures surfaced, `DeviceId` populated | ✅ | Awaited in `HoanThanh`/`ThoatKhanCap`; R5 option (b) — telemetry + user notice; failed persist no longer marks complete; DeviceId test-pinned |
| Pre-upgrade DB (fixture) upgrades in place, app runs, suite green | ✅* | `SyncSchemaDualPathTests` + `MigrationReporterTests` + file-based `AppStartupFileBasedTests`; *"no real pre-upgrade DB exists" turned out to be stale — see F5: the dev DB is one, and the first real upgrade is condition C3* |

## Success metrics (master plan) — measured state

| Metric | State |
|---|---|
| 100% upgrade fixtures lossless (row counts + checksums) | ✅ fixture-level; "one per alpha-tester DB shape" deferred — no alpha testers exist yet |
| 0 fire-and-forget persistence writes remain | ⚠️ **Literally unmet — see F2.** Met for all 6 synced entities (the A6 class); 3 telemetry-class writes survive by prior M8 design |
| 100% new `StudyLog` rows carry `DeviceId` | ✅ test-pinned (`FocusViewModelA6Tests`) |
| p95 task-save ≤ 1.2× baseline | ✅ 0.663 → 0.515 ms (report-sourced; baseline not re-derivable from HEAD — R3, accepted) |

## Definition of Done (master plan, 7 items)

| # | Item | State |
|---|---|---|
| 1 | `gitnexus_impact` before edits, HIGH/CRITICAL surfaced | ✅ per milestone reports (M1.3's runs report-sourced — index was stale) |
| 2 | `gitnexus_detect_changes` before commits | ✅ per milestone reports |
| 3 | Build + tests green | ✅ independently re-verified on the merged tree (330/330) |
| 4 | Acceptance-criteria tests present | ✅ stamping, dual-path upgrade, A6, cascade, discriminating dedup tests |
| 5 | Architecture docs + roadmap A.3 updated after code lands | ⚠️ `data-model.md` ✅ (all milestones); **roadmap A.3 stale after M1.3** — see F3 |
| 6 | Open decisions closed in decision notes | ✅ G1 note merged before M1.2 |
| 7 | **Success metrics measured and reported in the epic's closing note** | ❌ **No epic closing note exists** — see F1 |

---

## Findings

### F1 — MEDIUM (process/docs) — DoD-7 unmet: no epic closing note

The master plan (DoD-7) and the epic execution plan's own DoD both require the epic's success
metrics *measured and reported in its closing note*. Epic 1 was closed by an owner decision inside
the M1.3 milestone review; no epic-level closing note exists. The metric evidence is real but
scattered across four reviews and five reports — and one metric (F2) was never reconciled at all.
**Condition C1:** write `docs/reports/` closing note (with the ADR-style "Decisions made" section
per the 2026-07-07 convention) aggregating the four metrics above + the F2 waiver.

### F2 — LOW-MEDIUM (metric integrity) — 3 fire-and-forget telemetry writes survive

The metric says "0 fire-and-forget persistence writes remain (review sweep)". A sweep of the merged
tree finds three: `App.xaml.cs:68` (`OutcomeMaturationService.MatureAsync` — writes matured
`WeightChangeLog` outcomes), `QuanLyTaskViewModel.cs:216` (`LogDifficultyLabelAsync`),
`WeightOptimizerViewModel.cs:123` (`LogWeightChangeAsync`). All three are **telemetry-class tables,
not the six synced entities**; all predate Epic 1 and were *deliberately* fire-and-forget per M8's
"enhancement, never block" design (documented in the 2026-06-13 M8 report). The A6 class the metric
was aimed at — synced-entity data loss — is fully closed. But the epic-level metric as written was
never formally concluded, and these three swallow failures silently. **Recommendation: waiver, not
code change** — record in the closing note that the metric is met for synced entities and
deliberately waived for telemetry (loss-tolerant by design); revisit only if Epic 4's
telemetry-hungry training finds gaps in accrued data.

### F3 — LOW (docs) — Roadmap/CHANGELOG stale at the epic boundary

`system_roadmap.md` A.3 item 1 still reads "identity semantics … remain M1.3" (shipped 2026-07-11);
the A.2 shipped table ends at pre-Epic-1 UI work; `CHANGELOG.md` has no Epic 1 entry. M1.1/M1.2
updated the roadmap per DoD-5; M1.3/closure did not. **Condition C2:** one docs pass.

### F4 — INFORMATIONAL — One post-close escape, fixed same day (`101aaa3`)

Accepted M1.3 code called `MessageBox.Show` directly in `ThemMon`'s prevent-at-source path, popping
a real modal during headless test runs. The M1.3 review explicitly blessed the pattern
("consistent with the VM's existing convention") — the miss was its test-runtime consequence.
Fixed same day via the `OnThongBao` seam (matching the VM's other callbacks), test now asserts
through the seam. **One minor escape across ~1,900 inserted lines and four review passes is a
healthy escape rate** — this is evidence the review process works, not that it failed.

### F5 — MEDIUM (code + fact, found post-verdict during the closure debate) — `DbBackup` misses WAL content; a real pre-upgrade DB exists

Two related discoveries, same day as the original verdict:

1. **A real pre-Epic-1 database exists.** The dev DB
   (`bin/Debug/net10.0-windows10.0.19041.0/SmartStudyData.db`, 1.2 MB) has no `Rev` column and
   holds organically grown data: **HocKys 1 · MonHocs 3 · StudyTasks 11 · StudyLogs 5,402 ·
   TaskNotes 0 · TaskReferenceLinks 0** (post-upgrade reference counts). The M1.2 report's "no real
   pre-Epic-1 `SmartStudyData.db` exists to copy" is factually stale. Consequence: **the first real
   in-place upgrade has not happened yet — it fires on the next app launch.**
2. **`DbBackup.CreateBackup` is a `File.Copy` of the main `.db` only** — no `-wal`/`-shm`, no
   checkpoint. The DB runs in WAL mode, and `AppStartup.EnsureDatabaseReady` commits writes
   (IsSeeded, telemetry DDL) on an open connection *before* the backup copy. Any user data still
   sitting in an un-checkpointed WAL — **observed live: 213 KB of WAL beside this very DB, left by
   a previous session** — is absent from the backup; restoring it would silently roll back to the
   last checkpoint. The file-based tests could not catch this: their fixture DBs were cleanly
   closed, so no WAL was ever pending. This is a hole in Epic 1's **named top-risk mitigation**
   ("backup-before-upgrade").

**Fix (C3a):** checkpoint before copying — `PRAGMA wal_checkpoint(TRUNCATE)` immediately before
`File.Copy` (or switch to `SqliteConnection.BackupDatabase`), plus a test with a live-WAL fixture.
Land it **before** the first real upgrade runs so alpha testers get the fixed path.
**Interim mitigation (done during this review):** a manual full backup taken after a checkpoint —
`manual-backup-pre-epic1/SmartStudyData.db`, verified pre-upgrade shape + counts intact. (The
observed 213 KB WAL was checkpointed into the main file when this review's read connection closed,
so the *current* exposure is zero; the mechanism remains for any future unclean shutdown.)

*Honesty note:* the original verdict's "no known defect open in shipped Epic 1 code" is amended by
this finding. It was found not by the four milestone reviews but by the owner's closure question
("should I GUI-test first?") — which is itself an argument for C3's supervised manual pass.

---

## Consolidated carry-forward ledger (entering Epic 3 / Epic 2)

Single authoritative list — items verified still open, none lost:

1. **Real-DB in-place upgrade check** — ~~blocked: no real DB exists~~ **unblocked by F5**: the dev
   DB is a real 5,402-row pre-upgrade database; the supervised first upgrade is now condition
   **C3b**. Alpha-tester DB shapes remain future work when testers exist. (M1.2 → C3)
2. **Delete-HocKy FK-only cascade note** — any future HocKy-delete path must route task children
   through `TaskCascadeHelper`, or M1.2-R1's orphan class reappears. (M1.2-R1 review §2)
3. **R3 — p95 baseline not re-derivable from HEAD** — pre-seam number lives in report prose only.
   Persist both numbers as an artifact if the metric ever matters again. (M1.1)
4. **"One fixture per alpha-tester DB shape"** — blocked on alpha testers existing. (metric)
5. **F2 telemetry fire-and-forget waiver** — record in the closing note (C1).
6. **Rev churn on dead clones / `DeleteAsync` re-tombstone** — accepted, benign under LWW;
   revisit only if Epic 2's conflict records get noisy. (M1.2-R1 obs. 1, M1.3 obs. 1)
7. **GitNexus index staleness** — index lagged the worktree during the M1.3 review, forcing
   report-sourced impact claims. Reindex (`npx gitnexus analyze`) before Epic 3 code starts.
8. **NU1903 `SQLitePCLRaw` high-severity advisory** — visible in every build; the tech-debt list
   carries only NU1904 (`System.Drawing.Common`). Add it. (not Epic-1-caused)

---

## Decisions made (ADR-style)

**D-V1 — Ratify the close; no refinement stage.**
*Why:* Every epic acceptance criterion is code-verified on the merged tree; the full suite is green
(330/330) on a state none of the milestone reviews saw; the three in-epic defect cycles (R1/R5,
M1.2-R1, the M1.3 folded reconcile fix) were each caught pre-merge by the review process and
independently verified closed. The remaining gaps are paperwork.
*What for:* Unblocks the alpha critical path (M1.1→M1.2→M1.3→**M3.1**). A refinement stage would
hold the SOE for zero new information — Epic 1's substrate meets its real judge (Epic 2's merge
engine) only after Epic 3, by deliberate master-plan sequencing, and the SOE does not consume the
sync metadata at all.
*Experience:* Re-opening verified code without a failing signal invites churn on a hub write path
(`LuuHocKyAsync`) that three reviews just stabilized; the M1.3 review's Option C ("refine further")
was already considered and rejected by both reviewer and owner with a concrete rationale.

**D-V2 — Two docs-only closeout conditions (C1 closing note + F2 waiver; C2 roadmap/CHANGELOG).**
*Why:* DoD-7 is unambiguous ("success metrics measured and reported in its closing note") and DoD-5
is half-done at the boundary; the master plan is the source of truth, so the close isn't formally
complete until these land.
*What for:* The closing note is Epic 2/4's evidence base (what "mergeable" was verified to mean, and
which telemetry loss was accepted); the roadmap is canonical ordering (D-C.1) and currently
misinforms.
*Experience:* Scattered-but-real evidence rots fast — M1.3's review already had to reconstruct an
M1.2 claim from source because the index was stale. One page now is cheap; archaeology in Epic 2
is not.

**D-V3 — F2 resolved by waiver, not code.**
*Why:* The three writes are loss-tolerant telemetry by documented M8 design; "fixing" them buys no
alpha value and touches startup + two ViewModels for nothing the plan values.
*What for:* Keeps the metric honest without scope creep.
*Experience:* A6 existed because *synced-entity* loss is user-visible harm; telemetry loss is a
model-training input risk, priced into Epic 4's "extend the accrual window" fallback.

---

## The debate — steelman for a refinement stage, and why I reject it

**The strongest case for refining:** (a) `LuuHocKyAsync` — the app's hub write path — was rewritten
*twice* in one epic (M1.2 reconcile, M1.3 flat-diff + reparent), and the M1.3 fix was folded into
the same milestone that shipped the feature widening its trigger, so no fully independent review of
that final rewrite exists; (b) the in-place upgrade has never touched a real user DB; (c) `101aaa3`
proves escapes happen even through four review passes.

**Why that loses:** (a) the M1.3 reviewer hand-traced the discriminating scenario through the final
reconcile, substantiated the "pre-existing" claim against the M1.2-tip source, and the
`RepositoriesTests` namespace ran 19/19 × 5 in isolation — the folded fix got *more* scrutiny than
a routine remediation, and the owner signed the scope call (Option A) explicitly; (b) is untestable
by construction until an alpha DB exists — a refinement stage cannot manufacture one, only the
alpha can (ledger #1 covers the moment it does); (c) cuts the other way: the escape was found and
fixed within hours *because* the substrate is well-tested — and it was a UI-seam nit, not a data
defect. A refinement stage needs a failing signal to refine *against*; there is none. Every known
defect class has either a closed fix, a green test pinning it, or a tracked ledger entry.

**What would reverse this verdict:** an alpha-tester DB failing the in-place upgrade (ledger #1);
Epic 2's property suite surfacing convergence failures rooted in Rev/tombstone semantics (ledger
#6); or the closing-note metrics reconstruction contradicting a review claim. None of these are
reachable by polishing Epic 1 code today.

**Amendment to the debate (F5):** the strongest new fact is that a *real* upgrade has never run and
its safety net had a hole. Does that justify a refinement stage? Still no — it justifies exactly
**C3**: (a) the one-line `DbBackup` checkpoint fix + live-WAL test, (b) the supervised first real
upgrade against the dev DB (manual full backup already secured), (c) an Epic-1-targeted GUI smoke
pass (duplicate-name warning, delete→restart persistence, multi-resave stability, analytics dedup,
focus completion) plus `docs/ux_quality_gate_checklist.md` §Regression. If all pass, results feed
the C1 closing note and the close is signed; if the real upgrade fails, *that* is the failing
signal that reopens the code — with the backup guaranteeing a lossless retry.

**Outcome (owner's call):** ☐ ratify with C1+C2+C3 · ☐ ratify as-is (waive conditions) · ☐ refinement stage
