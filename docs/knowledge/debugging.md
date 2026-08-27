# Debugging Lessons

> Distilled 2026-05-21 from actual bugs and review observations on this codebase. Concrete root causes, concrete fixes.

## Bugs that bit us

### `SqliteException: no such column: s.NgayHoanThanh`

- **Symptom**: querying `HocKy` with `DanhSachMonHoc` + `DanhSachTask` navigation crashed at runtime after `StudyTask.NgayHoanThanh` was added.
- **Root cause**: `App.xaml.cs` used `db.Database.EnsureCreated()`. `EnsureCreated` **only creates the DB if it doesn't already exist** — it never alters schema. Old local DBs were stuck on the pre-`NgayHoanThanh` shape.
- **Fix**: `using Microsoft.EntityFrameworkCore;` + `db.Database.Migrate()` in `App.xaml.cs`. Migrations now run on launch.
- **Generalized lesson**: `EnsureCreated` is a **prototype-only** API. Past the first schema change, switch to `Migrate()` and accept that the migration assembly is real source code.
- **See also**: [`release-engineering.md`](release-engineering.md) — the idempotent `SyncSchema.EnsureColumns` upgrade seam this project actually shipped instead of EF migrations, and the WAL backup gap found alongside it.

### URL assertion failure in `TaskNotesViewModelTests`

- **Symptom**: test expected `"https://example.com"` but got `"https://example.com/"`.
- **Root cause**: `QuanLyTaskViewModel.AddLink()` called `uri.ToString()`, which normalizes URIs.
- **Fix**: store `uri.OriginalString` instead.
- **Generalized lesson**: any time you persist user-typed strings, prefer the API that preserves the exact input. `Uri.ToString`, `Uri.AbsoluteUri`, and friends all normalize.

### Stale ML artifacts after DB reset

- **Symptom**: after wiping the DB, `meta.json` still said `LogsUsedCount=180, SeedOnly=false` — the post-reset DB had zero logs.
- **Root cause**: DB reset path cleaned SQLite but left `%AppData%\SmartStudyPlanner\models\study_time.zip` + `meta.json` in place.
- **Fix**: `DbSeedTests.DeleteMlArtifacts()` removes both files before seeding so `MLModelManager.InitializeAsync` re-bootstraps cleanly.
- **Generalized lesson**: any feature that owns files outside the DB needs to be on the reset checklist. A "wipe DB" command that doesn't wipe siblings on disk is a hidden state bug.

### `EnumValues<T>()` requires .NET 5+

- **Symptom**: `Enum.GetValues<LoaiCongViec>()` failed to compile on older TFMs.
- **Fix**: use `(LoaiCongViec[])Enum.GetValues(typeof(LoaiCongViec))` for backward compatibility, or confirm the TFM is ≥ `net5.0`.

### Workload capacity survives DB reset (intentional, but surprising)

- **Symptom**: after wiping the DB, workload capacity was still set to the user's previous value.
- **Root cause**: capacity is stored in a separate `capacity.txt` file, not in SQLite. Behavior is correct — the doc was missing.
- **Fix**: documented in CHANGELOG + ROADMAP. Lesson: when settings span multiple stores, document each store's reset behavior.

## Reading the codebase efficiently

### Use the graph first
`gitnexus_query`, `gitnexus_context`, `gitnexus_impact` answer "what depends on X?" instantly. `grep`/`Read` answer "where is the literal string X?" — useful but secondary. The CLAUDE.md in this repo enforces this order.

### Run `gitnexus_impact` before edits
Pre-edit:
```
gitnexus impact <Symbol> --direction upstream --repo Smart-Study
```
Reports direct callers, affected processes, risk level. HIGH / CRITICAL → confirm scope before proceeding.

**Read the score, don't obey it — it measures fan-out, not meaning.** Both readings happen. On the
2026-08-14 balancer fix, `GetCapacity` came back **CRITICAL** and the score was right for a reason
the design had missed: three production callers, not the one assumed (`WorkloadBalancerViewModel`,
`DashboardViewModel`, `BalanceWorkloadStage`) — escalated to the owner before editing. In the same
run `WorkloadBalancerViewModel` came back **HIGH** and was noise: 24 of its 25 edges were `IMPORTS`
from unrelated files with a `using` line, `processes_affected: 0`. Open the edge list before you
either panic or proceed.

### Run `gitnexus_detect_changes` before commit
Verifies the change touched only the intended flows. If you see unexpected flows in the report, you have a hidden coupling.

### Re-index when stale
`npx gitnexus analyze` if the graph warns about staleness (file hooks auto-update but big rebases can skip them).

## Reproducing ML state

The complete ML reset + verify loop:

```bash
# 1. Wipe artifacts and seed 180 logs
dotnet test --filter "Category=Seed"

# 2. Launch the app — MLModelManager will bootstrap from SeedDataGenerator.Generate(180)

# 3. Navigate to Analytics → click "Tối ưu AI" → wait ~3-5s

# 4. Inspect %AppData%\SmartStudyPlanner\models\meta.json
#    Expected: SeedOnly=false, LogsUsedCount=180, ModelVersion >= 2
```

If `meta.json` still says `SeedOnly=true`, the manual retrain didn't run — check the click handler and that `HasEnoughData == true`.

## Test debugging

### Filters to slice your test run

```bash
# All
dotnet test

# Skip the slow ML training tests
dotnet test --filter "Category!=ML"

# Skip the dev-only DB seed test
dotnet test --filter "Category!=Seed"

# Run only seed (dev tool)
dotnet test --filter "Category=Seed"

# Run only ML
dotnet test --filter "Category=ML"
```

### `FakeStudyRepository` for ViewModel tests

When testing `FocusViewModel_WritesStudyLog_OnHoanThanh`, use `FakeStudyRepository` (in `SmartStudyPlanner.Tests/Helpers/`). It implements `IStudyRepository` in memory so tests assert on the recorded calls.

### In-memory SQLite for repository tests

The new Slice 4 repos accept `Func<AppDbContext>`. Test code creates a context with `UseSqlite("Data Source=:memory:")` and exercises real SQL semantics with no disk I/O.

### `Random(seed)` for reproducible failures

When a test depends on randomness, **always** seed the RNG. `new Random(42)` in `DbSeedTests` means every developer sees the same 180 rows. A failing test that can't be reproduced is a long debugging tunnel.

## Build / dependency gotchas

- Solution file is **`SmartStudyPlanner.slnx`**, not `.sln`. Most `dotnet` commands accept the `.slnx`. Don't accidentally generate or pass a stale `.sln`.
- Project version is currently `1.5.0` — bump on shipping milestones.
- Pre-existing warnings:
  - Nullable reference type warnings across files predating `nullable enable`.
  - `NU1904 — System.Drawing.Common 4.7.0` vulnerability (~30 min upgrade, tracked as backlog item N6).
- Sandbox builds may fail with `NU1301` (NuGet restore blocked). Verify locally before assuming red is real.
- **`SmartStudyData.db` runs in `journal_mode = wal`, so the file's mtime lies about data recency.** Recent commits sit in `SmartStudyData.db-wal` until a checkpoint (normally the last connection closing), so the main `.db` can read an hour stale while a query returns rows written seconds ago. Two consequences: never use mtime to judge whether the app wrote something, and **never copy a backup over the `.db` while a `-wal`/`-shm` pair exists** — SQLite replays the stale sidecar onto the file you just restored. Close the app, confirm the sidecars are gone, then restore. Deleting a sidecar that still holds uncheckpointed commits destroys them.
- **Building a SQLite URI by string concatenation fails twice on this repo's path, both silently.** `sqlite3.connect("file:" + path, uri=True)` — `file:D:/x.db` is not a valid URI, so SQLite opens an *empty* database reporting zero tables rather than erroring; and the `#` in `C#` starts a URI fragment, truncating the path at `D:/Code/C`. Use `pathlib.Path(p).as_uri()`. Both faults present as "the table isn't there," which reads like a real finding.

## WPF / theming

### Theme switch silently broken on a page → check shell handler placement
If the theme toggle works on Dashboard but not other pages, the click handler is probably scoped to `DashboardViewModel`. Move it to `MainWindow.xaml.cs` and call `ThemeManager.ToggleTheme()` directly.

### Custom converter not finding the right palette → log `MergedDictionaries`
`Application.Current.Resources.MergedDictionaries` is the source of truth for "what theme is active right now". If theme detection is wrong, inspect this collection — usually you'll find a stale dictionary in the wrong slot.

### Empty state vs missing data
If a chart looks empty, check the ViewModel's `HasData` / `EmptyStateMessage`. Phase C added these states explicitly so an empty result no longer looks like a bug.

## Reset checklist (when something is "weird")

1. Stop the app.
2. `dotnet test --filter "Category=Seed"` (smoke-tests the schema against an in-memory DB).
3. Optionally delete `SmartStudyPlanner/bin/Debug/<tfm>/SmartStudyData.db` for a totally clean DB.
4. Launch the app — ML re-bootstraps from seed, DB schema regenerates via `Migrate()`.
5. Verify dashboard + analytics render with the seed data.

## Triage decision tree

- **Build red** → check `dotnet build SmartStudyPlanner.slnx` locally (sandbox may be lying).
- **Test red** → check `dotnet test --filter "Category!=Seed&Category!=ML"` first for fast signal.
- **Runtime crash** → check `EnsureCreated` vs `Migrate` mismatch; check schema drift; check ML artifact corruption (delete + restart).
- **UI quiet** → check `IsLoading` / `HasData` / `HasError`; the data probably loaded fine but the view is in an unhandled state.
- **ML giving weird numbers** → check `IsMLPrediction`; if true, check `confidence` calc; if false, the formula is winning (probably correct).
- **Performance regression** → run `gitnexus_query({pattern: "callers_of"})` on hot symbols; see if a new call site was added.
