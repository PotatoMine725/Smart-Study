# E-2 (c): Derived-only and no-change local saves no longer stamp Rev/provenance

**Date:** 2026-10-03 · **Author:** Claude Code agent (owner-dispatched, ticket
`Prompt/2026-10-03-e2-derived-no-stamp.md`) · **PR:** #109 → `dev` (draft) · **Branch:**
`fix-e2-derived-no-stamp` (worktree `.claude/worktrees/e2-stamp`, from `origin/dev` `3c49924`)

Labels: **OBSERVED** (seen in a run in this session) · **FACT** (read in the tree) · **INFERENCE**
(reasoned, not run) · **NOT RUN**.

## 1. Scope

Implements ruling §1 of `docs/specs/2026-10-03-fence-slice4-followup-owner-rulings.md` (E-2 (c)) with
the owner's Phase 0 decisions of 2026-10-03:
- **D-1:** reference `MergeSurfaceRegistry` directly, plus a guard test.
- **D-2:** "modified" means `IsModified` *and* the value differs by the EF comparer.
- **D-3:** flip the three `*_Observed` tests in their own commit.
- **D-4:** the vacuous case is in scope. A save in which no value changed is not stamped either.

There is one production file, `SmartStudyPlanner/Data/SyncStamper.cs` (+47 lines):
- `TryGetSpec` is the registry lookup, internal and guarded.
- `HasStampableChange` holds the rule.
- One `continue` sits on the local Modified branch.

Not touched: `Sync/Apply/**`, fence types, fingerprint/D8-H, `EntitySnapshotMapper`, VMs, XAML, schema,
`SemesterGraphWriter`, `SqliteStudyTaskRepository`.

Commits on the branch:

| Commit | Content |
|---|---|
| `8387f03` | D-4 OBSERVED characterization on unmodified origin/dev |
| `4597459` | D-3: the three E-2 `*_Observed` tests flipped (own commit) |
| `e300f71` | New tests and the D-4 flip, tests first (8 RED on origin/dev) |
| `4212dd5` | `SyncStamper` change plus classification guard test |
| (this) | Report, CHANGELOG, rulings amendment |

## 2. Findings

### 2.1 Phase 0 answers (reported 2026-10-03, before "go")

- **Baseline (OBSERVED).** `3c49924`: 1095 passed / 1 skipped / 0 failed / 1096 total.
- **Dependency direction (FACT).** It is one assembly, so the question is about namespaces. `Data → Sync` already exists: `AppDbContext.cs` references
  `Sync.SyncBaseSnapshotRow`, `Sync.SyncConflictRecordRow` and `Sync.ConflictLocalWithdrawal` by qualified name.
  `Sync/Merge/MergeSurfaceRegistry.cs` depends on nothing in the project, so it is a leaf. A non-Sync precedent also exists:
  `SemesterReconcilePlanner.MergeFieldsOf` reads the registry. Referencing the registry from `Data/` therefore creates no cycle.
- **Impact.**
  - Phase 0, OBSERVED: GitNexus `impact` upstream on `SyncStamper.Apply` (4-arg) at the default depth was **CRITICAL**: 7 processes, all in Sync/Apply.
  - Re-run before the edit, OBSERVED: at depth 2 it reports LOW, with `AppDbContext.SaveChanges`/`SaveChangesAsync` as the only production callers.
  - GitNexus misses the local-save chain `LocalSemesterSaveExecutor → SemesterGraphWriter → SaveChangesAsync`. Grep confirms it. In practice this is the seam under every `SaveChanges`.
- **Non-Sync readers of `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId` (FACT, grep):**
  - DDL in `SyncSchema` and `SyncBaseSnapshotSchema`.
  - The value-preserving snapshot and restore in `SemesterGraphWriter.CopySyncSafeValues`.
  - There are no analytics, telemetry, ML, UI-sort or XAML readers, so the STOP condition was not hit.
  - On the Sync side, `SyncChangeEnumerator` selects `row.Rev > snapshot.Rev`, so an unstamped Derived-only edit is no longer enumerated. That is the intent of the ruling.
- **What `IsModified` means here (OBSERVED, probe, reverted).** The writer copies the caller graph onto freshly loaded tracked rows with `SetValues`, then restores the five sync columns through the setters.
  - The restore does **not** clear `IsModified`.
  - The caller graph keeps its pre-save `Rev` after a save. So the second save from the same graph flags `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId` as modified even though their values are unchanged.
  - An `IsModified`-only rule is therefore defeated by the normal long-lived-graph pattern. That is the basis for D-2.
  - No owned or complex properties exist. Shadow properties, if any, are unclassified and therefore stamp.
- **Correction to Phase 0 (FACT).** Phase 0 reported "no `Update()`/`Attach()`/`State = Modified` in production outside Sync/Apply". That was wrong.
  - `SqliteStudyTaskRepository.UpdateAsync` calls `db.StudyTasks.Update(task)` on a detached `StudyTask`.
  - It has no production caller on `3c49924`. The only caller is the test `RepositoriesTests.cs:64`, which does not assert `Rev`.
  - This led to decision 6.3.

### 2.2 D-4 evidence: the inference turned into observation

These tests were committed as `8387f03` and ran green on unmodified origin/dev. The second save runs on a later clock, so a restamp is visible.

| Test (as committed in 8387f03) | Observation on origin/dev |
|---|---|
| `D4_NoChangeSave_FromPreviouslySavedGraph_Observed` | Save 1 renames the task, so it is stamped. Save 2 comes from the **same graph with no change**: `Rev` +1 again and `ModifiedAtUtc` = the later clock. **OBSERVED: restamped.** |
| `D4_NoChangeSave_AfterDerivedOnlySave_OnHeldRow_Observed` | S1-CR held row. Save 1 is Derived-only, so it is stamped and drifts (E-2). Save 2 is a no-change save from the same graph: `Rev` +1 again, provenance moves again, and the live fingerprint changes **between save 1 and save 2**. **OBSERVED: the no-change save itself drifts a held row.** |

Answer to the owner's question: **yes, it drifts, but on origin/dev only as a compounding of E-2.**
- A graph loaded after staging is stale on the held row only if an earlier save from it stamped that row.
- The only fence-passing edit on a held row is Derived-only.
- INFERENCE, not measured: a graph loaded *before* S1-CR staging differs from the live row in content, because staging resets the row to Base. Its save is therefore not a no-change save, so it does not give an independent D-4 path to a held row.
- After this change the Derived-only save is not stamped and the graph never goes stale on the held row.
- On non-held rows D-4 is independent of E-2. Every row stamped by an earlier save from a long-lived graph was restamped by every later save from it.

**`HocKyRepository_ResaveWithNoChanges_DoesNotBumpRevOfUnrelatedRows` (RepositoriesTests.cs:520)** passes only because it reloads a fresh graph between saves (FACT). The long-lived-graph pattern, the way a VM keeps its `HocKy` across saves, was untested before this PR. It stays green.

### 2.3 The rule as implemented

In `SyncStamper.Apply`, on the local, unmarked path, a `Modified` entry skips stamping when `HasStampableChange(entry)` is false. For each property with `IsModified`:
- If the value differs from the original by the EF value comparer and the field is not `Derived` (an unclassified field counts as not Derived), the entry is **stamped**.
- If the value is unchanged and the field is not `SyncMetadata`/`Tombstone`, the entry is **stamped**. This is the fail-safe of decision 6.3.
- Otherwise the property is ignored. That covers a Derived value change, and a restored provenance column flagged with an unchanged value.

When nothing triggers a stamp, the entry is Derived-only or vacuous and keeps `Rev`, `ModifiedAtUtc` and `ModifiedByDeviceId`. EF still issues the UPDATE: the Derived values are written, and the unchanged columns are rewritten with the same bytes. Entity state is not altered (decision 6.4).

Unchanged paths:
- Added: stamped.
- Deleted: becomes a stamped tombstone.
- Marked sync-apply entries: `ApplyPreservingProvenance` (`Rev++`) runs before the new check.

## 3. Verification

| Check | Result |
|---|---|
| Build `SmartStudyPlanner.Tests` | OBSERVED: 0 errors. No warning comes from a touched file (96 pre-existing analyzer warnings). |
| Full suite after the change (`4212dd5`) | OBSERVED: **1107 passed / 1 skipped / 1108 total**, 0 failed. Baseline 1095 / 1 / 1096, plus 12 new: 2 D-4, 8 in `SyncStamperDerivedOnlyTests`, 2 guard. |
| Existing assertions changed | Only the three E-2 `*_Observed` tests (D-3, `4597459`). No other existing test changed. `TaskReferenceLinkUpdateTests` (Rev-reset history) and `ResaveWithNoChanges` pass unchanged. |
| Tests first | OBSERVED: on unmodified origin/dev, 8 of the 14 new/flipped tests fail, and these are exactly the behaviour-change tests. The 6 that pin today's behaviour pass: control, mixed, added, tombstone, marked, detached update. |
| Unchanged assertions can fail | Unstamped saves in the new non-held tests run on a second local identity (`LocalNow+1h`, `OTHER-LOCAL`), so each stamp column can show a restamp on its own. On the S1-CR held row, `ModifiedAtUtc` (staged `2026-05-01 10:00`) and `Rev` discriminate. `ModifiedByDeviceId` there is `LOCAL-DEVICE` on both sides, so that one assertion cannot fail on that row. |
| Branch diff | `git diff --stat origin/dev...HEAD` before docs: 5 files, all within the ticket's MAY-edit list (`SyncStamper.cs` + 4 test files). |
| GitNexus `detect_changes` | **NOT RUN on this branch.** The tool analyses the owner's main checkout (it listed that checkout's uncommitted `AGENTS.md`/`CLAUDE.md`/annotation-sheet edits), not this worktree. The git diff above is the scope check. |

### 3.1 Mutants (each applied alone, full suite run, then reverted with `git checkout`; tree clean afterwards)

| # | Mutant | RED (full suite) | Discriminating tests |
|---|---|---|---|
| M1 | Every property classified Derived | 24 | `MixedEdit_DerivedPlusMerge_IsStamped`, plus 23 existing stamping tests (`SyncMetadataStampingTests`, `SemesterSaveRegressionSnapshotTests`, `SyncApplySeamTests`, `ConflictResolverTests` …) |
| M2 | Drop the Derived check (any value change stamps) | 7 | The three E-2 flips, `D4_…OnHeldRow_NoDrift`, `DerivedOnlyEdit_StudyTask_*`, `DerivedOnlyEdit_HocKyIsSeeded_*`, `DerivedOnlyEdit_AfterAStampedSave_*` |
| M3 | `IsModified` alone, no value compare (D-2) | 2 | `DerivedOnlyEdit_AfterAStampedSave_FromTheSameGraph_IsNotStamped`, `D4_NoChangeSave_FromPreviouslySavedGraph_IsNotStamped` |
| M4 | Drop the vacuous case: no value changed ⇒ stamp (D-4) | 1 | `D4_NoChangeSave_FromPreviouslySavedGraph_IsNotStamped` (the only test that covers it; the held-row D-4 test cannot, because after the fix its graph never goes stale) |
| M5 | Drop the detached-update fail-safe | 1 | `DetachedUpdate_WithARealChange_IsStamped` |
| M6 | Classification lookup by `ClrType.FullName` (D-1 guard) | 10 | Both `SyncStamperClassificationGuardTests`, plus every Derived-only/no-change test (an unclassified entity falls back to stamping) |

## 4. NOT RUN

- The real `DecisionEngine` in the `QuanLyTaskViewModel` path. The E-2 VM test uses a fixed-priority stub. Whether the real engine's value differs from the stored one on a given day is still INFERENCE.
- Any manual UI run.
- Sync transport end to end. There is none yet, so "an unstamped Derived edit is not enumerated" rests on reading `SyncChangeEnumerator` (FACT), not on a run.
- GitNexus `detect_changes` on the branch (see §3).
- A backfill or measurement of rows already re-stamped in existing databases. Nothing is rewritten; restamped rows keep their values.
- An independent D-4 path to a held row through a graph loaded before staging. INFERENCE that none exists; see §2.2.

## 5. Follow-ups (non-blocking)

1. **`SqliteStudyTaskRepository.UpdateAsync`** uses a detached `DbSet.Update` and has no production caller. This PR keeps it stamping (decision 6.3), but on that path a Derived-only or no-change call is still stamped, so E-2 (c) does not apply to it. Either delete it or move it to load-then-`SetValues` like the semester writer. That needs an owner call; the file is outside this ticket's scope.
2. The ruling's note, option (d): stop persisting time-dependent Derived values. This is still the root-cause direction and is unchanged here.
3. `IsSeeded`'s production writer is raw SQL (`AppStartup.cs:31`) and bypasses the stamper, so it was never stamped. Unchanged.
4. The Slice 4 report §7.1 and fence-plan pointers to "E-2 open" are now stale. They are not edited here because they are outside the ticket's doc list.

## 6. Decisions made

### 6.1 Classify through the registry directly, behind a guard on the stamper's own lookup (owner D-1)
- **Why:** a Data-side copy of the Derived set would be a second definition that can drift. The registry is a leaf, and `Data → Sync` already exists.
- **For:** one definition. `TryGetSpec` is the single lookup.
- **Experience:** the existing `MergeSurfaceRegistryGuardTests` uses its own type-name map, so a CLR rename would have passed it while silently turning E-2 off. The new guard calls `SyncStamper.TryGetSpec` itself, and M6 proves it can go RED.

### 6.2 "Modified" is value-compared, and the vacuous case is not stamped (owner D-2, D-4)
- **Why:** the writer's restore leaves `IsModified` set. A long-lived graph therefore flags the provenance columns on every later save. That was measured, not assumed (§2.2).
- **For:** a no-change save writes no new provenance, so held rows keep their Base, and non-held rows stop accumulating spurious Rev bumps. INFERENCE: those bumps would otherwise be sent to peers as fresh, newer changes once sync exists.
- **Experience:** a test that reloads between saves (`ResaveWithNoChanges`) cannot see a defect that lives in the caller's long-lived graph. Write the test the way the VM uses the API.

### 6.3 Fail-safe: a flagged-but-unchanged non-provenance property stamps (agent decision, RATIFIED by owner 2026-10-03)
- **Ratified (owner, 2026-10-03):** entries whose original values cannot be trusted (detached `DbSet.Update` / `Attach`) are stamped as today, failing toward stamping. Recorded as a dated clarification at the end of rulings §1.
- **Why:** with `DbSet.Update` on a detached instance, EF sets Original = Current for every property. Value comparison then sees "no change" even for a real edit. Under a literal D-2/D-4 rule, `SqliteStudyTaskRepository.UpdateAsync` would persist the edit but never stamp it, so sync would never enumerate it, silently.
- **What:** a property that is flagged modified with an unchanged value only counts as harmless when it is in the provenance block the writer restores (`SyncMetadata`/`Tombstone`). Anything else means the originals are unknown, so the entry is stamped.
- **Direction:** this only adds stamping relative to the literal rule. It never removes a stamp the ruling requires, and it is the same "fail toward today's behaviour" principle the ruling applies to unclassified properties.
- **Not done instead:** editing `SqliteStudyTaskRepository`, which is out of scope, and stopping. A conservative default existed, so it shipped for owner review, and the owner ratified it.
- **Experience:** in an EF value-compare rule, "no difference" can mean "EF does not know the database value". Grep for `Update(`/`Attach(` before trusting `OriginalValue`. This was found by re-grepping after Phase 0's claim, which turned out wrong.

### 6.4 No entity-state handling (owner instruction: minimal)
- **Why:** the skipped entry stays `Modified`, and EF writes the Derived values plus unchanged bytes for the rest. That is harmless for the fingerprint and for `Rev`.
- **For:** forcing `Unchanged` would also drop the Derived write, which the ruling requires to persist. Per-property `IsModified = false` adds a second mechanism with no observed need.

### 6.5 Tests make every "unchanged" claim falsifiable
- **Why:** every local write uses `LOCAL-DEVICE`, and seeded rows already carry fixed timestamps. An "unchanged" assertion on the same identity cannot go RED for `ModifiedByDeviceId`, and with the fixed clock it cannot for `ModifiedAtUtc` either.
- **What:** the save under test runs on a second identity.
- **Experience:** the mutant table maps each mutant to the test that caught it. M4 is caught by exactly one test, so that test carries the D-4 guarantee and must not be weakened.
