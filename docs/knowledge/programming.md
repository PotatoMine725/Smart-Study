# Programming Lessons — C# / WPF / EF Core

> Distilled 2026-05-21 from real bugs, refactors, and reviews shipped on this codebase.

## C# language

### Prefer instance + interface over `static class` for any logic with > 1 caller
A `static class` cannot be injected and cannot be mocked. In M1→M4.6 the entire `DecisionEngine` / `WorkloadService` / `SmartParser` / `StreakManager` family was static — making them untestable. Lesson: even for "obvious utility" code, if the function depends on state (`DateTime.Now`, a `WeightConfig`, a DB context), wrap it in an instance + interface from day one. Static helpers should only hold pure stateless pure functions.

### Use `DateTime.Now` through an `IClock`
Every direct `DateTime.Now` call in domain code is a hidden non-determinism. Introduce `IClock { DateTime Now { get; } }` with `SystemClock` for production and `FakeClock` for tests. This unlocked the entire urgency-rule chain test suite.

### Tuple returns are fine for small adapter boundaries
`(int Minutes, bool IsML)` from `StudyTimePredictorService.Predict` is a clean two-value return. Don't reach for a class until you have ≥ 3 fields or need behavior.

### Nullable reference types catch real bugs but require discipline
The project has `nullable enable`. Most pre-existing warnings are about pre-`enable` code. New code should resolve `?` correctly and not chase warnings into the codebase.

### `Uri.ToString()` mutates user-typed strings
`new Uri("https://example.com").ToString()` returns `"https://example.com/"`. Storing this loses what the user typed. **Use `uri.OriginalString` when persisting user-facing values.** This was the actual fix in `QuanLyTaskViewModel.AddLink()`.

### `Random(seed)` for reproducible test data
`DbSeedTests` uses `new Random(42)` so the same 180 synthetic logs are produced every run. Gaussian-style noise via `(rng.NextSingle() - 0.5f) * 0.3f` gives ±15% variation without external dependencies. Reproducible tests are debuggable; non-deterministic ones rot.

### `[Trait("Category", "...")]` on xUnit lets you slice CI
- `[Trait("Category", "Seed")]` for dev tools you only run manually.
- `[Trait("Category", "ML")]` for slow training tests (~2-3 s each).
- Filter with `dotnet test --filter "Category!=Seed"` for fast CI loops.

## WPF / XAML

### Hook globally available actions at the shell level
The theme toggle was first wired into `DashboardViewModel`, which meant it only worked from the dashboard. Moved into `MainWindow.xaml.cs` calling `ThemeManager.ToggleTheme()` directly → works from every page. Lesson: global UX (theme, telemetry, dialogs) belongs on the shell, not on the page.

### `ToggleButton` + ControlTemplate beats per-button event handlers for nav
The sidebar refactor replaced 6 hand-styled `Button`s with `ToggleButton` + a shared `SidebarStyles.xaml` template. Active state is now `IsChecked = (btn == active)` instead of per-button background mutation. Less code, supports keyboard + screen reader naturally.

### `ItemsControl` + `UniformGrid` for fixed-grid visualizations
The 52×7 study heatmap is just an `ItemsControl` with `<ItemsPanelTemplate><UniformGrid Rows="7" Columns="52"/></ItemsPanelTemplate>`. No chart library needed, no D3, no canvas math.

### Theme-aware converters detect the active resource dictionary
`HeatLevelToBrushConverter` picks light/dark palette by inspecting `Application.Current.Resources.MergedDictionaries`:
```csharp
bool isDark = Application.Current.Resources.MergedDictionaries
    .Any(d => d.Source?.OriginalString.Contains("DarkTheme") == true);
```
Avoid coupling converters to specific brush keys when a 2-line check is enough.

### Always provide an empty / loading state on data-bound views
Phase C added `IsLoading`, `HasData`, `HasError`, `EmptyStateMessage` to the Dashboard ViewModel. Before this, "no data yet" looked like a bug. Every async-loaded view should expose these four states.

## CommunityToolkit.Mvvm

- `[ObservableProperty] private string foo;` generates `Foo { get; set; }` + `OnFooChanged` partial method hooks. Use them for `OnFooChanged(string value) { ApplyFilter(); }` rather than overriding setters.
- `[RelayCommand(CanExecute = nameof(CanRetrain))]` keeps button-enabling logic in one place.
- Async commands: `[RelayCommand] private async Task LoadAsync() { ... }` generates an async `RelayCommand`. Bind via `{Binding LoadCommand}`.

## EF Core

### `Migrate()` over `EnsureCreated()` past the prototype stage
`EnsureCreated()` only creates the DB if missing — it never applies schema changes. Adding `StudyTask.NgayHoanThanh` broke every old local DB with `SqliteException: no such column: s.NgayHoanThanh`. Fix: `db.Database.Migrate()` runs pending migrations on launch. Keep migration assets in source control; they are not "production-only" boilerplate.

### Provide a `DbContextOptions` constructor on every `DbContext`
The default `AppDbContext()` configures its own connection string. That's fine for runtime but blocks in-memory SQLite tests. Adding `AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}` unlocks `UseSqlite("Data Source=:memory:")` in tests. The new Slice 4 repositories take `Func<AppDbContext>` factory exactly so callers can swap implementations per test.

### Configure cascades explicitly via Fluent API
M6.1 declared `TaskNote` 1-1 and `TaskReferenceLink` 1-N with explicit `OnDelete(DeleteBehavior.Cascade)` in `OnModelCreating`. Don't rely on convention — write it so the cascade story is greppable. See [`sync-data-model.md`](sync-data-model.md) for what happens to this config once deletes stop being real SQL deletes (it drives EF's in-memory fixup instead, and FK-only children need a hand-cascade helper).

### Atomic file swap for any model artifact
`MLModelManager.RetrainAsync` writes to `model.tmp`, then `File.Move(tmp, canonical, overwrite: true)`. A crash during training leaves the old good file untouched. Apply the same pattern any time you serialize a model / config / cache.

## Repository pattern

- Don't pre-allocate a wide `IStudyRepository` for every aggregate from day one; the M1-M5 era taught us that a single mega-repo accumulates 20+ methods. Slice 4 split the new layer into per-aggregate repos (`IStudyTaskRepository`, `IStudyLogRepository`, `IMonHocRepository`, `IUserStatsRepository`).
- Pure aggregation services (`StudyAnalyticsService` is `IEnumerable<StudyLog> → reports`) are not repository consumers — keep them function-style and pass data in.

## Telemetry minimalism

`IStudyTelemetry` with a `DebugStudyTelemetry` no-op implementation costs almost nothing and lets you swap a real backend later. Track:
- view opens (`dashboard_open`, `analytics_open`)
- navigation (`nav_click_<id>`)
- core user actions (`task_add`, `task_update`, `task_delete`)
- focus mode lifecycle (`focus_start`, `focus_complete`, `focus_abort`)
- filter / parameter changes (with the parameters as properties)

## Validation rules from the project

- Don't change priority formula / risk calculation / balancer logic without an explicit instruction.
- No `DateTime.Now`, no `new Random()`, no DB calls inside algorithm logic.
- No `new DecisionEngine()` — depend on `IDecisionEngine`.
- Every engine change ships with unit tests + edge cases.
- `gitnexus_impact` before editing; `gitnexus_detect_changes` before committing.
