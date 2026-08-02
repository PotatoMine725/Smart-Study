# WP-3 — Persistence & Sync Identity: execution report

**Date:** 2026-07-31
**Package:** WP-3 of `docs/plans/2026-07-27-post-epic1-stabilization.md` (Category A)
**Range:** `1854e54..16be27f` — 14 files, +348 / −20
**Status:** ☑ complete. Suite 348 → 355, CI green on `windows-latest`, Entry Criteria #4/#5/#6 met.

---

## 1. What was delivered

| Commit | Task | Change |
|---|---|---|
| `bbb3c29` | 3.1 | `!IsDeleted` added to the two leaking read paths; `SoftDeleteReadPathTests` created with one regression test per path |
| `6704773` | 3.2 | `DeviceIdentity` (persisted, seeded from `DeviceHelper.GetId()`); routed through a new `AppDbContext.DeviceIdProvider` seam |
| `78f16bb` | 3.3 | `MucDoCanhBao` defaulted to `"An toàn"` on the property |
| `bcc950b` | — | Container-built `AppDbContext` wired to the same seam; Entry Criteria ticked and #5's enumeration corrected |
| `16be27f` | — | CI step failing the build on any test write to the real user profile |

The plan's internal ordering held: 3.1 before 3.3 (hard — 3.3's test reuses 3.1's `NewDb()` fixture), and WP-2.3 before 3.2 (already satisfied, 2.3 landed last session).

---

## 2. What I stumbled against

### 2.1 The one that mattered: WP-3.2 reintroduced the defect WP-2.2 exists to remove

**The plan's Step 5 is wrong, and following it literally produced a green suite that was writing into the developer's real user profile.**

Plan text says to call `new DeviceIdentity()` directly inside `SyncSchema.cs:56` and `FocusViewModel.cs:158`. `DeviceIdentity`'s default directory is the real `%APPDATA%/SmartStudyPlanner`. I made both edits, ran the full suite, got **353 passed / 0 failed**, and would have committed on that evidence.

The plan *did* anticipate this defect class — its "Cross-package ordering" row exists precisely to stop 3.2 from making tests write `device-id.txt`. But it identified exactly one door (`ServiceLocator`, closed by WP-2.3) and there were three:

| Seam | Test callers | Named in the plan? |
|---|---|---|
| `ServiceLocator.Get<IRiskAnalyzer>()` | 0 (removed by WP-2.3) | yes |
| `SyncSchema.EnsureColumns(db)` | 6 (`SyncSchemaDualPathTests`, `MigrationReporterTests`) | no |
| `FocusViewModel` write site | 4 (`FocusViewModelOutcomeLogTests`) | no |

Found by checking the disk, not the test output: `%APPDATA%/SmartStudyPlanner/device-id.txt` had a creation timestamp inside the window of the test run I had just watched pass.

**Fix.** Both seams now read a provider whose default does no I/O:

- `SyncSchema` reads `db.DeviceIdProvider()` — exactly symmetric with the `db.Clock()` call on the line above it, and test contexts get the no-I/O default. `App.xaml.cs` wires the bootstrap context by hand, since startup precedes DI configuration.
- `FocusViewModel` takes an optional `Func<string>` defaulting to `DeviceHelper.GetId`; only the production ctor passes `DeviceIdentity`. This extends the file's own existing convention — it already has a cascade of test-facing ctors defaulting to `Null*` doubles, commented *"để không chạm đĩa"*.

**Verification.** Deleted `device-id.txt`, ran the full suite, confirmed it was not recreated. Then generalised: snapshotted every file under `%APPDATA%` and `%LOCALAPPDATA%\SmartStudyPlanner` before and after a run — nothing touched.

**Why `StudyLog.DeviceId` was worth wiring at all.** It is a *distinct* field from the `ModifiedByDeviceId` that `SyncStamper` applies — the model comments it as creation-device vs. last-writer. Leaving it on `DeviceHelper` would let a single row carry two different device identities after a hostname change, which Epic 2 has no way to reconcile.

### 2.2 The blast radius is wider than the plan states

`gitnexus` rated `GetSnapshotAsync` **HIGH** upstream. Its direct production caller is `WeightOptimizerService.SuggestAsync`, not only Analytics. Adding `!t.IsDeleted` therefore shifts `TotalTaskCount`, `OverdueTaskCount`, `MissRate` and `AverageDelayDays` — all four of which feed weight suggestions.

Correct behaviour (tombstoned tasks should not influence weights) and no code change followed from it, but the plan's WP-3.1 reasoning and its release-note guidance both describe Analytics only. **Two user-visible surfaces will move, not one.**

### 2.3 Entry Criterion #5 was unsatisfiable as written

It enumerated four surviving `DeviceHelper.GetId` sites. There are six: the criterion omitted `DeviceIdentity`'s own `IOException` fallback, and could not have known about the `FocusViewModel` default that 2.1's fix introduced. All six are defaults or fallbacks; no live identity source remains outside `DeviceIdentity`.

Corrected the enumeration in the plan rather than recording the discrepancy elsewhere — consistent with the position taken last session on Criterion #2, that the plan must not carry a statement that is false.

### 2.4 An unverified Category C claim that gated my own completion

The plan's Category C table calls `ServiceLocator.cs:40` (`services.AddSingleton<AppDbContext>()`) a "dead DI registration." Nobody had verified it, and Criterion #5 claims *every* write is routed through `DeviceIdProvider` — a container-built context uses the parameterless ctor and would get the derived default.

Grepped: zero hits for `Get<AppDbContext>` / `GetRequiredService<AppDbContext>`. Dead confirmed, criterion genuinely met. Wired the registration anyway (`bcc950b`), because it remains *reachable*: a future resolve would silently stamp the derived ID while every other write used the persisted one, and — per §2.5 — nothing would catch it.

### 2.5 A structural blind spot, inherited and now larger

After WP-2.3 removed the last `ServiceLocator` resolve, **nothing in the suite exercises the DI container at all.** Two changes this session depend on registrations no test touches (`DeviceIdentity`'s singleton, the `AppDbContext` wiring), and last session's `LocalModelStorageProvider` optional-parameter change is in the same position.

Not fixed here. Restoring container coverage means building the production composition root in tests, which is precisely what WP-2.3 removed for good reason. Recorded as an accepted gap; see §5.

### 2.6 Minor friction

- `NullStreakManager` is a **private nested** class inside `FocusViewModel`, so the 6-arg ctor could not be called from a test without a local double. Added `NullStreakManagerForTest`, per the repo's inline-hand-written-double convention.
- `TrangThai` is a `string`, but `StudyTaskStatus` is a `static class` of `const string`, so the plan's WP-3/WP-4 snippets using `StudyTaskStatus.HoanThanh` compile verbatim. **This corrects a claim I made at the end of the WP-2 report** that WP-4's code would need adjusting. It does not.
- `rtk grep` / `rtk find` fail (`Binary 'rg' not found on PATH`), and `rtk` mangles multi-line C# in `grep` output. Used the Grep tool or plain `grep` throughout. Pre-existing, non-blocking.

---

## 3. Fixes applied beyond the plan's letter

| Fix | Commit | Why |
|---|---|---|
| `SyncSchema` reads `db.DeviceIdProvider()`; `App.xaml.cs` wires bootstrap | `6704773` | Plan's literal text leaked into the real profile from 6 tests |
| `FocusViewModel` optional provider param | `6704773` | Same, from 4 tests; plus one-row-two-identities correctness |
| Container-built `AppDbContext` wired | `bcc950b` | Latent trap; no test could catch a future regression |
| Criterion #5 enumeration corrected, #4/#5/#6 ticked | `bcc950b` | Criterion was unsatisfiable as written |
| CI profile-write guard | `16be27f` | The defect in §2.1 was invisible to a green suite |
| `NullStudyLogRepository` → `CapturingStudyLogRepository` | `16be27f` | It accumulates now; the old name said the opposite |

**On the CI guard specifically.** A source-scanning guard (the shape WP-2.1 used for `DateTime.Now`) would need a hand-maintained list of profile-backed types and would go stale the moment someone adds a seam — which is exactly the failure mode that produced §2.1. The CI step checks the *outcome* instead: the runner never launches the app, so any `SmartStudyPlanner` directory under `APPDATA`/`LOCALAPPDATA` was created by a test. Confirmed non-vacuous by running its logic against a profile that does contain those files — it fires and names all six.

---

## 4. Verification

| Check | Result |
|---|---|
| Full suite, Debug | 355 passed, 0 failed |
| Full suite, Release (CI parity) | 355 passed, 0 failed |
| CI on `dev` (`30630189401`) | `success` — 355 passed on `windows-latest` |
| CI profile guard | `No profile writes detected.` |
| Profile snapshot before/after local run | no file under `%APPDATA%`/`%LOCALAPPDATA%\SmartStudyPlanner` touched |
| Read-path sweep (Criterion #4, re-run independently) | only `SqliteHocKyRepository:98` and `SqliteStudyTaskRepository:56` unfiltered — both upsert/delete lookups that must see tombstones |
| `AppDbContext` resolved from container | zero hits |
| App launch (the one file with no automated coverage) | clean start, no `crash.log`, `device-id.txt` written as `desktop-49b42d8f` — seeded-from-derived, so backward compatibility holds on a real install |

Each red-before-green step failed for the predicted reason, not an incidental one: 3.1 failed with 2 logs / `TotalTaskCount` 2 (**not** 0, which would have meant the fixture wasn't persisting), 3.2 with `CS0246` on `DeviceIdentity` (and notably *not* on `DeviceHelper`, confirming `InternalsVisibleTo` covers it), 3.3 with `SQLite Error 19: NOT NULL constraint failed: StudyTasks.MucDoCanhBao`.

---

## 5. Carry-forwards

- **Hostname collision remains unfixed by design.** Two machines sharing a hostname seed to the same persisted ID. Randomising would fix it and break every existing install's row provenance. Epic 2 needs duplicate-peer detection at handshake.
- **Gate G4 (tombstone retention)** still an explicit Epic 2 planning item. WP-3 makes tombstones invisible to readers; nothing decides how long they live.
- **Nothing exercises the DI container** (§2.5). Accepted, not fixed. If Epic 2 adds composition-root complexity, this should be revisited before it does.
- **Two user-visible surfaces move**, not one: Analytics *and* weight suggestions (§2.2). Belongs in the release note.
- **Branch protection for `build-test` is still outstanding** (WP-1 Step 6, owner action). CI measures; it does not yet enforce.
- **GitNexus index is stale** (last analyzed `d3deca7`). WP-6.2 refreshes it.

---

## Decisions made

**Decision: fix the plan's Step 5 rather than follow it, and say so in the commit.**
*Why:* Following it literally made six tests write `device-id.txt` into the real user profile — the exact defect class WP-2.2 was executed to remove, one package earlier. The plan anticipated the risk but enumerated one of three doors.
*What for:* So WP-2.2's guarantee survives WP-3 instead of being quietly undone by it.
*Experience:* The suite was green through all of this. A green suite is evidence about assertions, not about side effects, and this package had no assertion anywhere about what the tests do to the filesystem. The only reason it was caught is that I looked at the disk after watching a passing run — a habit worth keeping when a change touches a default path.

**Decision: use a CI outcome check as the guard, not a source-scanning test.**
*Why:* The repo has precedent for source-scan guards (`TestFile_KhongDungWallClock`). But a source scan needs a maintained list of profile-backed types, and "the list was incomplete" is the root cause of §2.1 — the guard would inherit the bug it is meant to prevent.
*What for:* A check that catches seams nobody has thought of yet, including ones added after this is written.
*Experience:* The runner's never-launched app makes the signal unambiguous in a way it can't be locally, where legitimate app use creates the same files. Worth verifying the detector actually fires before trusting it — I ran its logic against a dirty profile and confirmed it names all six files, because a guard that silently passes is worse than none.

**Decision: give `FocusViewModel` an optional provider parameter instead of calling `DeviceIdentity` inline.**
*Why:* Its write site is reached four times by tests. The class already solves this shape of problem — a cascade of test-facing ctors defaulting to `Null*` doubles, with an explicit comment that they exist so tests don't touch disk.
*What for:* Extends an existing convention rather than introducing a second mechanism for the same concern.
*Experience:* The alternative — leaving `StudyLog.DeviceId` derived — was tempting, since the plan explicitly leaves the two `ModelMeta` provenance sites alone and this field is *also* provenance. What decides it is that `StudyLog` implements `ISyncMetadata`: after 3.2 its `ModifiedByDeviceId` comes from `DeviceIdentity`, so leaving `DeviceId` derived puts two identities on one row the moment a hostname changes.

**Decision: wire the container-built `AppDbContext` even though the registration is provably dead.**
*Why:* Dead today, reachable tomorrow, and §2.5 means no test would catch the regression.
*What for:* Removes a trap whose cost is one line now and a silent identity split later.
*Experience:* This one only surfaced because Criterion #5's wording — "every write" — is stronger than what I had actually verified. Worth reading acceptance criteria as claims to be checked rather than boxes to be ticked; the grep took thirty seconds and the claim was load-bearing.

**Decision: correct Criterion #5's enumeration in the plan.**
*Why:* It named four sites where there are six, so it could never be satisfied as written.
*What for:* The criteria list is the Epic 2 gate someone reads later; a progress-table footnote is not where a correction belongs.
*Experience:* Directly consistent with last session's argument against the architecture review on Criterion #2 — that the plan must not contain a false statement. The difference is that #2 turned out to be satisfiable and #5 was not, which is why this one got edited and that one didn't.

**Decision: do not restore DI-container coverage.**
*Why:* The only way to cover it is to build the production composition root in tests, which is what WP-2.3 deliberately removed.
*What for:* Avoids reversing a Category A decision from the previous package to close a gap the CSA already classifies as intentional debt.
*Experience:* This is the second package in a row to trip over the same blind spot, and it now hides three changes rather than one. It is correctly deferred, not correctly ignored — if Epic 2 grows the composition root, it should be reconsidered first.
