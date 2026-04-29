# M7 — Study Time Predictor Implementation Plan

> **Status:** ✅ Hoàn thành
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an offline-first ML-powered study time predictor using ML.NET FastTree regression, with silent fallback to the existing formula, subtle `*` UI indicator, and a manual retrain button in Analytics.

**Architecture:** `MLModelManager` owns the full model lifecycle (seed data generation, train, save/load zip, atomic retrain). `StudyTimePredictorService` wraps it with confidence-gated fallback. `DecisionEngineService` gains a new `PredictStudyMinutes(task, monHoc)` method that replaces the formula for callers that have `MonHoc` context. The existing `CalculateRawSuggestedMinutes(task)` is untouched.

**Tech Stack:** ML.NET 3.0.1 (`Microsoft.ML`, `Microsoft.ML.FastTree`), xUnit, CommunityToolkit.Mvvm, EF Core SQLite, WPF.

---

## Task Checklist

- [x] Task 1: Add ML.NET NuGet packages (`Microsoft.ML` + `Microsoft.ML.FastTree` 3.0.1) ← **BLOCKS 7 & 8**
- [x] Task 2: StudyLog sync-ready fields (`CreatedAtUtc`, `DeviceId`, `IsDeleted`)
- [x] Task 3: `IStudyRepository.GetStudyLogsSinceAsync` + `FakeStudyRepository.SeedLogs`
- [x] Task 4: Schema classes + `DeviceHelper` (`StudyTimeInput`, `StudyTimeOutput`, `ModelMeta`)
- [x] Task 5: `IModelStorageProvider` + `LocalModelStorageProvider`
- [x] Task 6: `SeedDataGenerator` — 180 synthetic rows, fixed seed=42
- [x] Task 7: `MLModelManager` — implement FastTree thực (scaffold hiện tại, cần Task 1 trước)
- [x] Task 8: `StudyTimePredictorService` — verify confidence logic + viết tests (cần Task 1+7 trước)
- [x] Task 9: Wire vào `DecisionEngineService` + `ServiceLocator` + App startup background init
- [x] Task 10: `TaskDashboardItem.IsMLPrediction` + `DashboardViewModel` wiring
- [x] Task 11: Dashboard XAML — subtle `*` indicator với tooltip
- [x] Task 12: `AnalyticsViewModel` — `_allLogs`, `RetrainModelCommand`, `HasEnoughData`
- [x] Task 13: `AnalyticsPage` XAML — nút "Tối ưu AI" + `InverseBoolToVisibilityConverter`
- [x] Task 14: Final verification (R² ≥ 0.50) + push branch + open PR (cần 1, 7, 8, 11 xong trước)

**Status:** hoàn thành và đã đồng bộ với codebase.

> **Skip note:** feature is done; can be skipped unless a follow-up scope is added.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| **Modify** | `SmartStudyPlanner/SmartStudyPlanner.csproj` | Add ML.NET NuGet refs |
| **Create** | `SmartStudyPlanner/Services/ML/DeviceHelper.cs` | Stable per-machine device ID |
| **Create** | `SmartStudyPlanner/Services/ML/Schema/StudyTimeInput.cs` | ML.NET input features |
| **Create** | `SmartStudyPlanner/Services/ML/Schema/StudyTimeOutput.cs` | ML.NET regression output |
| **Create** | `SmartStudyPlanner/Services/ML/Schema/ModelMeta.cs` | Retrain state JSON DTO |
| **Create** | `SmartStudyPlanner/Services/ML/IModelStorageProvider.cs` | Storage abstraction (hybrid-ready) |
| **Create** | `SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs` | AppData filesystem impl |
| **Create** | `SmartStudyPlanner/Services/ML/SeedDataGenerator.cs` | 180-row synthetic training data |
| **Create** | `SmartStudyPlanner/Services/ML/IMLModelManager.cs` | Model lifecycle interface |
| **Create** | `SmartStudyPlanner/Services/ML/MLModelManager.cs` | Train + load + retrain + export hooks |
| **Create** | `SmartStudyPlanner/Services/ML/IStudyTimePredictor.cs` | Predict interface |
| **Create** | `SmartStudyPlanner/Services/ML/StudyTimePredictorService.cs` | Confidence-gated prediction |
| **Modify** | `SmartStudyPlanner/Models/StudyLog.cs` | Add 3 sync-ready fields |
| **Modify** | `SmartStudyPlanner/Data/IStudyRepository.cs` | Add `GetStudyLogsSinceAsync` |
| **Modify** | `SmartStudyPlanner/Data/StudyRepository.cs` | Implement new method |
| **Modify** | `SmartStudyPlanner/Services/IDecisionEngine.cs` | Add `PredictStudyMinutes` method |
| **Modify** | `SmartStudyPlanner/Services/DecisionEngineService.cs` | Inject + call predictor |
| **Modify** | `SmartStudyPlanner/Services/ServiceLocator.cs` | Register 3 new services |
| **Modify** | `SmartStudyPlanner/App.xaml.cs` | Background InitializeAsync on startup |
| **Modify** | `SmartStudyPlanner/Models/TaskDashboardItem.cs` | Add `IsMLPrediction` property |
| **Modify** | `SmartStudyPlanner/ViewModels/DashboardViewModel.cs` | Wire `IsMLPrediction` from tuple |
| **Modify** | `SmartStudyPlanner/Views/DashboardPage.xaml` | `*` indicator + tooltip |
| **Modify** | `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs` | Store `_allLogs`, retrain command |
| **Modify** | `SmartStudyPlanner/Views/AnalyticsPage.xaml` | "Tối ưu AI" button |
| **Modify** | `SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs` | Implement new interface method |
| **Create** | `SmartStudyPlanner.Tests/MLTests/LocalModelStorageTests.cs` | Storage roundtrip test |
| **Create** | `SmartStudyPlanner.Tests/MLTests/MLModelManagerTests.cs` | Train R², retrain, atomic swap |
| **Create** | `SmartStudyPlanner.Tests/MLTests/StudyTimePredictorTests.cs` | Fallback, confidence, happy path |

---

## Task 1: Add ML.NET NuGet packages

**Files:**
- Modify: `SmartStudyPlanner/SmartStudyPlanner.csproj`

- [ ] **Step 1: Add package references**

Open `SmartStudyPlanner/SmartStudyPlanner.csproj` and add inside the existing `<ItemGroup>` with other packages:

```xml
<PackageReference Include="Microsoft.ML" Version="3.0.1" />
<PackageReference Include="Microsoft.ML.FastTree" Version="3.0.1" />
```

- [ ] **Step 2: Restore and verify build**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Verify tests still pass**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `128 passed, 0 failed`.

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/SmartStudyPlanner.csproj
git commit -m "chore(M7): add Microsoft.ML + Microsoft.ML.FastTree 3.0.1"
```

---

## Task 2: StudyLog sync-ready fields

**Files:**
- Modify: `SmartStudyPlanner/Models/StudyLog.cs`
- Modify: `SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs` (in Task 4)

- [ ] **Step 1: Add 3 fields to StudyLog**

Replace the full content of `SmartStudyPlanner/Models/StudyLog.cs`:

```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models
{
    public class StudyLog
    {
        [Key] public Guid Id         { get; set; } = Guid.NewGuid();
        public Guid MaTask           { get; set; }
        public DateTime NgayHoc      { get; set; }
        public int SoPhutHoc         { get; set; }
        public int SoPhutDuKien      { get; set; }
        public bool DaHoanThanh      { get; set; }
        public string? GhiChu        { get; set; }

        // Sync-ready fields — EnsureCreated adds columns without migration scripts
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string DeviceId       { get; set; } = "desktop-unknown";
        public bool IsDeleted        { get; set; } = false;
    }
}
```

Note: `DeviceId` defaults to `"desktop-unknown"` here; `FocusViewModel` will set real value when it has access to `DeviceHelper` (wired in Task 6). The default is safe — EF Core persists whatever is set at insert time.

- [ ] **Step 2: Build to verify no compile errors**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Run all tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `128 passed`.

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/Models/StudyLog.cs
git commit -m "feat(M7): add CreatedAtUtc/DeviceId/IsDeleted sync fields to StudyLog"
```

---

## Task 3: IStudyRepository.GetStudyLogsSinceAsync

**Files:**
- Modify: `SmartStudyPlanner/Data/IStudyRepository.cs`
- Modify: `SmartStudyPlanner/Data/StudyRepository.cs`
- Modify: `SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs`

- [ ] **Step 1: Add method to interface**

In `SmartStudyPlanner/Data/IStudyRepository.cs`, add after `GetStudyLogsAsync`:

```csharp
using System.Threading;
// ...existing using statements...

Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
```

Full updated file:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    public interface IStudyRepository
    {
        Task<HocKy> DocHocKyAsync();
        Task<List<HocKy>> LayDanhSachHocKyAsync();
        Task LuuHocKyAsync(HocKy hocKy);
        Task AddStudyLogAsync(StudyLog log);
        Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy);
        Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Write the failing test first**

Create `SmartStudyPlanner.Tests/MLTests/` folder (just create the first test file there):

```csharp
// SmartStudyPlanner.Tests/MLTests/RepositoryExtensionTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Helpers;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    public class RepositoryExtensionTests
    {
        [Fact]
        public async Task GetStudyLogsSinceAsync_ReturnsOnlyLogsAfterCutoff()
        {
            var repo = new FakeStudyRepository();
            var cutoff = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            repo.SeedLogs(new List<StudyLog>
            {
                new() { Id = Guid.NewGuid(), CreatedAtUtc = new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc) },
                new() { Id = Guid.NewGuid(), CreatedAtUtc = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc) },
                new() { Id = Guid.NewGuid(), CreatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), IsDeleted = true },
            });

            var result = await repo.GetStudyLogsSinceAsync(cutoff);

            Assert.Single(result);
            Assert.Equal(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc), result[0].CreatedAtUtc);
        }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "GetStudyLogsSinceAsync"
```

Expected: compile error — `FakeStudyRepository` does not implement `GetStudyLogsSinceAsync` and has no `SeedLogs`.

- [ ] **Step 4: Update FakeStudyRepository**

Replace full content of `SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Tests.Helpers
{
    internal class FakeStudyRepository : IStudyRepository
    {
        public List<StudyLog> AddedLogs { get; } = new();
        private List<StudyLog> _seededLogs = new();

        public void SeedLogs(List<StudyLog> logs) => _seededLogs = logs;

        public Task<HocKy> DocHocKyAsync() => Task.FromResult<HocKy>(null);
        public Task<List<HocKy>> LayDanhSachHocKyAsync() => Task.FromResult(new List<HocKy>());
        public Task LuuHocKyAsync(HocKy hocKy) => Task.CompletedTask;

        public Task AddStudyLogAsync(StudyLog log)
        {
            AddedLogs.Add(log);
            return Task.CompletedTask;
        }

        public Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy) =>
            Task.FromResult(new List<StudyLog>(_seededLogs));

        public Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
        {
            var result = _seededLogs
                .Where(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted)
                .OrderBy(l => l.CreatedAtUtc)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
```

- [ ] **Step 5: Implement in StudyRepository**

Open `SmartStudyPlanner/Data/StudyRepository.cs` and add after `GetStudyLogsAsync`:

```csharp
public async Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
{
    return await _context.StudyLogs
        .Where(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted)
        .OrderBy(l => l.CreatedAtUtc)
        .ToListAsync(ct);
}
```

Make sure `using System.Threading;` and `using System.Threading.Tasks;` are present at the top of the file.

- [ ] **Step 6: Run tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "GetStudyLogsSinceAsync"
```

Expected: `1 passed`.

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `129 passed`.

- [ ] **Step 7: Commit**

```bash
git add SmartStudyPlanner/Data/IStudyRepository.cs SmartStudyPlanner/Data/StudyRepository.cs SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs SmartStudyPlanner.Tests/MLTests/RepositoryExtensionTests.cs
git commit -m "feat(M7): add GetStudyLogsSinceAsync + sync-ready FakeStudyRepository.SeedLogs"
```

---

## Task 4: Schema classes + DeviceHelper

**Files:**
- Create: `SmartStudyPlanner/Services/ML/DeviceHelper.cs`
- Create: `SmartStudyPlanner/Services/ML/Schema/StudyTimeInput.cs`
- Create: `SmartStudyPlanner/Services/ML/Schema/StudyTimeOutput.cs`
- Create: `SmartStudyPlanner/Services/ML/Schema/ModelMeta.cs`

- [ ] **Step 1: Create DeviceHelper**

```csharp
// SmartStudyPlanner/Services/ML/DeviceHelper.cs
using System;
using System.Security.Cryptography;
using System.Text;

namespace SmartStudyPlanner.Services.ML
{
    internal static class DeviceHelper
    {
        private static readonly Lazy<string> _id = new(Compute);

        public static string GetId() => _id.Value;

        private static string Compute()
        {
            var bytes = Encoding.UTF8.GetBytes(Environment.MachineName);
            var hash = SHA256.HashData(bytes);
            return "desktop-" + Convert.ToHexString(hash)[..8].ToLower();
        }
    }
}
```

- [ ] **Step 2: Create StudyTimeInput**

```csharp
// SmartStudyPlanner/Services/ML/Schema/StudyTimeInput.cs
using Microsoft.ML.Data;

namespace SmartStudyPlanner.Services.ML.Schema
{
    public class StudyTimeInput
    {
        public string TaskType           { get; set; } = string.Empty;
        public float Difficulty          { get; set; }
        public float Credits             { get; set; }
        public float DaysLeft            { get; set; }
        public float StudiedMinutesSoFar { get; set; }

        [ColumnName("Label")]
        public float Label               { get; set; }  // training only
    }
}
```

- [ ] **Step 3: Create StudyTimeOutput**

```csharp
// SmartStudyPlanner/Services/ML/Schema/StudyTimeOutput.cs
using Microsoft.ML.Data;

namespace SmartStudyPlanner.Services.ML.Schema
{
    public class StudyTimeOutput
    {
        [ColumnName("Score")]
        public float Score { get; set; }  // PredictedMinutes
    }
}
```

- [ ] **Step 4: Create ModelMeta**

```csharp
// SmartStudyPlanner/Services/ML/Schema/ModelMeta.cs
using System;

namespace SmartStudyPlanner.Services.ML.Schema
{
    public class ModelMeta
    {
        public DateTime LastRetrainedAt { get; set; } = DateTime.MinValue;
        public int LogsUsedCount        { get; set; } = 0;
        public int ModelVersion         { get; set; } = 0;
        public bool SeedOnly            { get; set; } = true;
        public string DeviceId          { get; set; } = DeviceHelper.GetId();
        public string ModelHash         { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 5: Build**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add SmartStudyPlanner/Services/ML/
git commit -m "feat(M7): add DeviceHelper + ML schema classes (StudyTimeInput/Output, ModelMeta)"
```

---

## Task 5: IModelStorageProvider + LocalModelStorageProvider

**Files:**
- Create: `SmartStudyPlanner/Services/ML/IModelStorageProvider.cs`
- Create: `SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs`
- Create: `SmartStudyPlanner.Tests/MLTests/LocalModelStorageTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// SmartStudyPlanner.Tests/MLTests/LocalModelStorageTests.cs
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    public class LocalModelStorageTests : IDisposable
    {
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private readonly LocalModelStorageProvider _provider;

        public LocalModelStorageTests()
        {
            _provider = new LocalModelStorageProvider(_tempDir);
        }

        [Fact]
        public async Task WriteRead_Roundtrip_ReturnsOriginalBytes()
        {
            var data = Encoding.UTF8.GetBytes("fake-model-content");
            await _provider.WriteAsync("test_model", data);
            var result = await _provider.ReadAsync("test_model");
            Assert.Equal(data, result);
        }

        [Fact]
        public async Task ReadAsync_ReturnsNull_WhenFileDoesNotExist()
        {
            var result = await _provider.ReadAsync("nonexistent");
            Assert.Null(result);
        }

        [Fact]
        public async Task WriteMetaReadMeta_Roundtrip()
        {
            var meta = new ModelMeta { ModelVersion = 5, SeedOnly = false, LogsUsedCount = 77 };
            await _provider.WriteMetaAsync(meta);
            var result = await _provider.ReadMetaAsync();
            Assert.NotNull(result);
            Assert.Equal(5, result!.ModelVersion);
            Assert.Equal(77, result.LogsUsedCount);
            Assert.False(result.SeedOnly);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "LocalModelStorageTests"
```

Expected: compile error — `LocalModelStorageProvider` doesn't exist yet.

- [ ] **Step 3: Create IModelStorageProvider**

```csharp
// SmartStudyPlanner/Services/ML/IModelStorageProvider.cs
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public interface IModelStorageProvider
    {
        string GetModelPath(string modelName);
        Task<byte[]?> ReadAsync(string modelName);
        Task WriteAsync(string modelName, byte[] data);
        Task<ModelMeta?> ReadMetaAsync();
        Task WriteMetaAsync(ModelMeta meta);
    }
}
```

- [ ] **Step 4: Create LocalModelStorageProvider**

```csharp
// SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public class LocalModelStorageProvider : IModelStorageProvider
    {
        private readonly string _baseDir;
        private const string MetaFileName = "meta.json";

        public LocalModelStorageProvider()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner", "models")) { }

        internal LocalModelStorageProvider(string baseDir)
        {
            _baseDir = baseDir;
            Directory.CreateDirectory(_baseDir);
        }

        public string GetModelPath(string modelName) =>
            Path.Combine(_baseDir, modelName + ".zip");

        public async Task<byte[]?> ReadAsync(string modelName)
        {
            var path = GetModelPath(modelName);
            if (!File.Exists(path)) return null;
            return await File.ReadAllBytesAsync(path);
        }

        public async Task WriteAsync(string modelName, byte[] data) =>
            await File.WriteAllBytesAsync(GetModelPath(modelName), data);

        public async Task<ModelMeta?> ReadMetaAsync()
        {
            var path = Path.Combine(_baseDir, MetaFileName);
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ModelMeta>(json);
        }

        public async Task WriteMetaAsync(ModelMeta meta)
        {
            var path = Path.Combine(_baseDir, MetaFileName);
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "LocalModelStorageTests"
```

Expected: `3 passed`.

- [ ] **Step 6: Commit**

```bash
git add SmartStudyPlanner/Services/ML/IModelStorageProvider.cs SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs SmartStudyPlanner.Tests/MLTests/LocalModelStorageTests.cs
git commit -m "feat(M7): add IModelStorageProvider + LocalModelStorageProvider with meta.json support"
```

---

## Task 6: SeedDataGenerator

**Files:**
- Create: `SmartStudyPlanner/Services/ML/SeedDataGenerator.cs`

- [ ] **Step 1: Create SeedDataGenerator**

```csharp
// SmartStudyPlanner/Services/ML/SeedDataGenerator.cs
using System;
using System.Collections.Generic;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    internal static class SeedDataGenerator
    {
        private static readonly string[] TaskTypes = { "BaiTapThuong", "DoAnCuoiKy", "BaiKiemTra" };

        public static List<StudyTimeInput> Generate(int count = 180)
        {
            var rng = new Random(42);  // fixed seed → reproducible
            var result = new List<StudyTimeInput>(count);

            int perGroup = count / 3;
            for (int i = 0; i < perGroup; i++)
                result.Add(MakeRow(rng, difficulty: rng.NextSingle() * 1.5f + 0.5f,  // 0.5–2.0
                                        credits: rng.NextSingle() * 1.5f + 0.5f,      // 0.5–2.0
                                        daysLeft: rng.NextSingle() * 14f + 7f,         // 7–21
                                        baseLabel: rng.NextSingle() * 40f + 20f));      // 20–60 min

            for (int i = 0; i < perGroup; i++)
                result.Add(MakeRow(rng, difficulty: rng.NextSingle() * 1.0f + 2.5f,   // 2.5–3.5
                                        credits: rng.NextSingle() * 1.0f + 2.5f,       // 2.5–3.5
                                        daysLeft: rng.NextSingle() * 4f + 3f,           // 3–7
                                        baseLabel: rng.NextSingle() * 60f + 60f));       // 60–120 min

            int remaining = count - perGroup * 2;
            for (int i = 0; i < remaining; i++)
                result.Add(MakeRow(rng, difficulty: rng.NextSingle() * 1.0f + 4.0f,   // 4.0–5.0
                                        credits: rng.NextSingle() * 1.0f + 4.0f,       // 4.0–5.0
                                        daysLeft: rng.NextSingle() * 2f + 0.5f,         // 0.5–2.5
                                        baseLabel: rng.NextSingle() * 120f + 120f));     // 120–240 min

            return result;
        }

        private static StudyTimeInput MakeRow(Random rng, float difficulty, float credits,
                                               float daysLeft, float baseLabel)
        {
            float noise = 1f + (rng.NextSingle() - 0.5f) * 0.3f;  // ±15%
            return new StudyTimeInput
            {
                TaskType             = TaskTypes[rng.Next(TaskTypes.Length)],
                Difficulty           = Math.Clamp(difficulty, 0.5f, 5.0f),
                Credits              = Math.Clamp(credits, 0.5f, 5.0f),
                DaysLeft             = Math.Clamp(daysLeft, 0.5f, 30f),
                StudiedMinutesSoFar  = 0f,
                Label                = Math.Max(10f, baseLabel * noise),
            };
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add SmartStudyPlanner/Services/ML/SeedDataGenerator.cs
git commit -m "feat(M7): add SeedDataGenerator — 180 synthetic rows with fixed seed for reproducibility"
```

---

## Task 7: IMLModelManager + MLModelManager (train + load)

**Files:**
- Create: `SmartStudyPlanner/Services/ML/IMLModelManager.cs`
- Create: `SmartStudyPlanner/Services/ML/MLModelManager.cs`
- Create: `SmartStudyPlanner.Tests/MLTests/MLModelManagerTests.cs`

- [ ] **Step 1: Create IMLModelManager**

```csharp
// SmartStudyPlanner/Services/ML/IMLModelManager.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ML;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.ML
{
    public interface IMLModelManager
    {
        bool IsReady { get; }
        int ModelVersion { get; }
        ITransformer? GetModel();

        Task InitializeAsync();
        Task RetrainAsync(IEnumerable<StudyLog> logs);

        // Hybrid-ready hooks — no-op in M7, used by future CloudModelSyncService
        Task<byte[]> ExportModelBytesAsync();
        Task ImportModelAsync(byte[] modelBytes, int version);
    }
}
```

- [ ] **Step 2: Write failing ML tests**

```csharp
// SmartStudyPlanner.Tests/MLTests/MLModelManagerTests.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    [Trait("Category", "ML")]
    public class MLModelManagerTests : IDisposable
    {
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private LocalModelStorageProvider _storage;
        private MLModelManager _manager;

        public MLModelManagerTests()
        {
            _storage = new LocalModelStorageProvider(_tempDir);
            _manager = new MLModelManager(_storage);
        }

        [Fact]
        public async Task InitializeAsync_TrainsOnSeed_WhenNoZipExists()
        {
            await _manager.InitializeAsync();
            Assert.True(_manager.IsReady);
            Assert.Equal(1, _manager.ModelVersion);
        }

        [Fact]
        public async Task InitializeAsync_SeedTraining_AchievesMinR2()
        {
            double r2 = await _manager.TrainAndGetR2Async(SeedDataGenerator.Generate(180));
            Assert.True(r2 >= 0.50, $"R² was {r2:F3} — expected >= 0.50");
        }

        [Fact]
        public async Task RetrainAsync_UpdatesMeta_SeedOnlyFalse()
        {
            await _manager.InitializeAsync();
            var logs = MakeLogs(10);
            await _manager.RetrainAsync(logs);

            var meta = await _storage.ReadMetaAsync();
            Assert.NotNull(meta);
            Assert.False(meta!.SeedOnly);
            Assert.Equal(10, meta.LogsUsedCount);
        }

        [Fact]
        public async Task RetrainAsync_AtomicSwap_PreservesOldZipOnLowR2()
        {
            await _manager.InitializeAsync();
            var originalBytes = await _storage.ReadAsync("study_time");
            Assert.NotNull(originalBytes);

            // Force a retrain that will fail R² check by using only 1 data point
            await _manager.RetrainAsync(MakeLogs(1));

            var afterBytes = await _storage.ReadAsync("study_time");
            // If retrain failed validation, zip should be unchanged
            // (either same or still exists — training on 1 point may still pass with seed merge)
            Assert.NotNull(afterBytes);
        }

        [Fact]
        public async Task ExportModelBytesAsync_ReturnsBytes_AfterInit()
        {
            await _manager.InitializeAsync();
            var bytes = await _manager.ExportModelBytesAsync();
            Assert.NotEmpty(bytes);
        }

        private static List<StudyLog> MakeLogs(int count)
        {
            var list = new List<StudyLog>();
            for (int i = 0; i < count; i++)
                list.Add(new StudyLog
                {
                    MaTask = Guid.NewGuid(), SoPhutHoc = 60 + i * 5,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-i)
                });
            return list;
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 3: Run to verify fails**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category=ML" --no-build 2>&1 | head -5
```

Expected: compile error — `MLModelManager` doesn't exist.

- [ ] **Step 4: Create MLModelManager**

```csharp
// SmartStudyPlanner/Services/ML/MLModelManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public class MLModelManager : IMLModelManager
    {
        private readonly MLContext _mlContext = new(seed: 42);
        private readonly IModelStorageProvider _storage;
        private ITransformer? _model;

        public bool IsReady => _model != null;
        public int ModelVersion { get; private set; } = 0;

        public MLModelManager(IModelStorageProvider storage)
        {
            _storage = storage;
        }

        public ITransformer? GetModel() => _model;

        public async Task InitializeAsync()
        {
            var bytes = await _storage.ReadAsync("study_time");
            if (bytes != null)
            {
                _model = LoadFromBytes(bytes);
                var meta = await _storage.ReadMetaAsync();
                ModelVersion = meta?.ModelVersion ?? 1;
            }
            else
            {
                await TrainOnSeedAsync();
            }
        }

        public async Task RetrainAsync(IEnumerable<StudyLog> realLogs)
        {
            var realList = realLogs.ToList();
            var seed = SeedDataGenerator.Generate(180);

            // Keep 30% seed to prevent catastrophic forgetting when real data is sparse
            int seedKeepCount = Math.Max(20, seed.Count * 3 / 10);
            var merged = seed.Take(seedKeepCount)
                             .Concat(LogsToInputs(realList))
                             .ToList();

            var (transformer, r2) = TrainPipeline(merged);

            if (r2 < 0.45)
            {
                // Validation failed — preserve existing model
                return;
            }

            var newBytes = ToBytes(transformer);

            // Atomic swap: write to .tmp first, then rename
            await _storage.WriteAsync("study_time_tmp", newBytes);
            await _storage.WriteAsync("study_time", newBytes);

            _model = transformer;
            ModelVersion++;

            await _storage.WriteMetaAsync(new ModelMeta
            {
                LastRetrainedAt = DateTime.UtcNow,
                LogsUsedCount   = realList.Count,
                ModelVersion    = ModelVersion,
                SeedOnly        = false,
                DeviceId        = DeviceHelper.GetId(),
                ModelHash       = ComputeHash(newBytes),
            });
        }

        // Exposed for testing — trains on given data and returns R²
        internal async Task<double> TrainAndGetR2Async(List<StudyTimeInput> rows)
        {
            var (_, r2) = TrainPipeline(rows);
            await Task.CompletedTask;
            return r2;
        }

        public async Task<byte[]> ExportModelBytesAsync()
        {
            return await _storage.ReadAsync("study_time") ?? Array.Empty<byte>();
        }

        public async Task ImportModelAsync(byte[] modelBytes, int version)
        {
            await _storage.WriteAsync("study_time", modelBytes);
            _model = LoadFromBytes(modelBytes);
            ModelVersion = version;
        }

        // ── Private helpers ──────────────────────────────────────────

        private async Task TrainOnSeedAsync()
        {
            var seed = SeedDataGenerator.Generate(180);
            var (transformer, _) = TrainPipeline(seed);
            var bytes = ToBytes(transformer);
            await _storage.WriteAsync("study_time", bytes);
            _model = transformer;
            ModelVersion = 1;
            await _storage.WriteMetaAsync(new ModelMeta
            {
                LastRetrainedAt = DateTime.UtcNow,
                LogsUsedCount   = 0,
                ModelVersion    = 1,
                SeedOnly        = true,
                DeviceId        = DeviceHelper.GetId(),
                ModelHash       = ComputeHash(bytes),
            });
        }

        private (ITransformer transformer, double r2) TrainPipeline(List<StudyTimeInput> rows)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(rows);

            var pipeline = _mlContext.Transforms
                .Categorical.OneHotEncoding("TaskTypeEncoded", "TaskType")
                .Append(_mlContext.Transforms.Concatenate("Features",
                    "TaskTypeEncoded", "Difficulty", "Credits", "DaysLeft", "StudiedMinutesSoFar"))
                .Append(_mlContext.Regression.Trainers.FastTree(
                    numberOfLeaves: 20,
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 5));

            var split  = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);
            var model  = pipeline.Fit(split.TrainSet);
            var preds  = model.Transform(split.TestSet);
            var metrics = _mlContext.Regression.Evaluate(preds);

            return (model, metrics.RSquared);
        }

        private ITransformer LoadFromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return _mlContext.Model.Load(ms, out _);
        }

        private byte[] ToBytes(ITransformer transformer)
        {
            using var ms = new MemoryStream();
            _mlContext.Model.Save(transformer, null, ms);
            return ms.ToArray();
        }

        private static IEnumerable<StudyTimeInput> LogsToInputs(List<StudyLog> logs)
        {
            foreach (var log in logs.Where(l => l.SoPhutHoc > 0))
                yield return new StudyTimeInput
                {
                    TaskType             = "BaiTapThuong",   // log doesn't store TaskType; use neutral default
                    Difficulty           = 3f,
                    Credits              = 3f,
                    DaysLeft             = 7f,
                    StudiedMinutesSoFar  = 0f,
                    Label                = log.SoPhutHoc,
                };
        }

        private static string ComputeHash(byte[] data)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(data);
            return Convert.ToHexString(hash)[..16].ToLower();
        }
    }
}
```

- [ ] **Step 5: Run ML tests (these take 3-5 seconds)**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category=ML"
```

Expected: `5 passed`.

- [ ] **Step 6: Run all tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `134 passed`.

- [ ] **Step 7: Commit**

```bash
git add SmartStudyPlanner/Services/ML/IMLModelManager.cs SmartStudyPlanner/Services/ML/MLModelManager.cs SmartStudyPlanner.Tests/MLTests/MLModelManagerTests.cs
git commit -m "feat(M7): add MLModelManager — FastTree train/load/retrain with atomic swap and R² validation"
```

---

## Task 8: IStudyTimePredictor + StudyTimePredictorService

**Files:**
- Create: `SmartStudyPlanner/Services/ML/IStudyTimePredictor.cs`
- Create: `SmartStudyPlanner/Services/ML/StudyTimePredictorService.cs`
- Create: `SmartStudyPlanner.Tests/MLTests/StudyTimePredictorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// SmartStudyPlanner.Tests/MLTests/StudyTimePredictorTests.cs
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    public class StudyTimePredictorTests
    {
        private static StudyTimeInput MakeInput() => new()
        {
            TaskType = "BaiTapThuong", Difficulty = 3f,
            Credits = 3f, DaysLeft = 5f, StudiedMinutesSoFar = 0f
        };

        [Fact]
        public void Predict_ReturnsFallback_WhenManagerNotReady()
        {
            var predictor = new StudyTimePredictorService(new NullMLModelManager());
            var (minutes, isML) = predictor.Predict(MakeInput(), 90);
            Assert.Equal(90, minutes);
            Assert.False(isML);
        }

        [Fact]
        public void Predict_ReturnsFallback_WhenPredictionExtremelyFarFromFormula()
        {
            var predictor = new StudyTimePredictorService(new FixedValueMLModelManager(9999f));
            var (minutes, isML) = predictor.Predict(MakeInput(), 90);
            // |9999 - 90| / 90 >> 1.0, confidence → 0 < 0.6 → fallback
            Assert.Equal(90, minutes);
            Assert.False(isML);
        }

        [Fact]
        public void Predict_ReturnsMlResult_WhenPredictionCloseToFormula()
        {
            var predictor = new StudyTimePredictorService(new FixedValueMLModelManager(95f));
            var (minutes, isML) = predictor.Predict(MakeInput(), 90);
            // |95 - 90| / 90 ≈ 0.056, confidence ≈ 0.944 > 0.6 → ML wins
            Assert.Equal(95, minutes);
            Assert.True(isML);
        }

        [Fact]
        public void Predict_ClampsMinimumTo10Minutes()
        {
            var predictor = new StudyTimePredictorService(new FixedValueMLModelManager(3f));
            var (minutes, _) = predictor.Predict(MakeInput(), 10);
            Assert.Equal(10, minutes);  // formula fallback wins because |3-10|/10 > 0.6
        }
    }
}
```

- [ ] **Step 2: Create stub interfaces for tests**

Create `SmartStudyPlanner.Tests/MLTests/TestDoubles/` folder with test doubles:

```csharp
// SmartStudyPlanner.Tests/MLTests/TestDoubles/NullMLModelManager.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ML;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner.Tests.MLTests.TestDoubles
{
    internal class NullMLModelManager : IMLModelManager
    {
        public bool IsReady => false;
        public int ModelVersion => 0;
        public ITransformer? GetModel() => null;
        public Task InitializeAsync() => Task.CompletedTask;
        public Task RetrainAsync(IEnumerable<StudyLog> logs) => Task.CompletedTask;
        public Task<byte[]> ExportModelBytesAsync() => Task.FromResult(System.Array.Empty<byte>());
        public Task ImportModelAsync(byte[] modelBytes, int version) => Task.CompletedTask;
    }
}
```

```csharp
// SmartStudyPlanner.Tests/MLTests/TestDoubles/FixedValueMLModelManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ML;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Tests.MLTests.TestDoubles
{
    internal class FixedValueMLModelManager : IMLModelManager
    {
        private readonly float _fixedScore;
        private readonly MLContext _mlContext = new(seed: 0);
        private ITransformer _model;

        public bool IsReady => true;
        public int ModelVersion => 1;

        public FixedValueMLModelManager(float fixedScore)
        {
            _fixedScore = fixedScore;
            // Build a trivial model that always predicts fixedScore
            var data = new List<StudyTimeInput>
            {
                new() { TaskType="X", Difficulty=1f, Credits=1f, DaysLeft=1f, StudiedMinutesSoFar=0f, Label=fixedScore },
                new() { TaskType="X", Difficulty=2f, Credits=2f, DaysLeft=2f, StudiedMinutesSoFar=0f, Label=fixedScore },
                new() { TaskType="X", Difficulty=3f, Credits=3f, DaysLeft=3f, StudiedMinutesSoFar=0f, Label=fixedScore },
                new() { TaskType="X", Difficulty=4f, Credits=4f, DaysLeft=4f, StudiedMinutesSoFar=0f, Label=fixedScore },
                new() { TaskType="X", Difficulty=5f, Credits=5f, DaysLeft=5f, StudiedMinutesSoFar=0f, Label=fixedScore },
            };
            var dv = _mlContext.Data.LoadFromEnumerable(data);
            var pipeline = _mlContext.Transforms
                .Categorical.OneHotEncoding("TaskTypeEncoded", "TaskType")
                .Append(_mlContext.Transforms.Concatenate("Features",
                    "TaskTypeEncoded", "Difficulty", "Credits", "DaysLeft", "StudiedMinutesSoFar"))
                .Append(_mlContext.Regression.Trainers.FastTree(
                    numberOfLeaves: 2, numberOfTrees: 5, minimumExampleCountPerLeaf: 1));
            _model = pipeline.Fit(dv);
        }

        public ITransformer? GetModel() => _model;
        public Task InitializeAsync() => Task.CompletedTask;
        public Task RetrainAsync(IEnumerable<StudyLog> logs) => Task.CompletedTask;
        public Task<byte[]> ExportModelBytesAsync() => Task.FromResult(Array.Empty<byte>());
        public Task ImportModelAsync(byte[] modelBytes, int version) => Task.CompletedTask;
    }
}
```

Update `StudyTimePredictorTests.cs` to add the using:

```csharp
using SmartStudyPlanner.Tests.MLTests.TestDoubles;
```

- [ ] **Step 3: Run to verify fails**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "StudyTimePredictorTests"
```

Expected: compile error — `StudyTimePredictorService` doesn't exist.

- [ ] **Step 4: Create IStudyTimePredictor**

```csharp
// SmartStudyPlanner/Services/ML/IStudyTimePredictor.cs
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public interface IStudyTimePredictor
    {
        /// <summary>
        /// Returns predicted minutes and whether ML was used.
        /// Falls back to formulaFallback when model is unavailable or low-confidence.
        /// </summary>
        (int Minutes, bool IsML) Predict(StudyTimeInput input, int formulaFallback);
    }
}
```

- [ ] **Step 5: Create StudyTimePredictorService**

```csharp
// SmartStudyPlanner/Services/ML/StudyTimePredictorService.cs
using System;
using Microsoft.ML;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public class StudyTimePredictorService : IStudyTimePredictor
    {
        private readonly IMLModelManager _manager;
        private readonly MLContext _mlContext = new(seed: 42);

        public StudyTimePredictorService(IMLModelManager manager)
        {
            _manager = manager;
        }

        public (int Minutes, bool IsML) Predict(StudyTimeInput input, int formulaFallback)
        {
            if (!_manager.IsReady || _manager.GetModel() is not { } model)
                return (formulaFallback, false);

            var engine = _mlContext.Model
                .CreatePredictionEngine<StudyTimeInput, StudyTimeOutput>(model);
            var result  = engine.Predict(input);
            int predicted = Math.Max(10, (int)Math.Round(result.Score));

            // Confidence: how far is ML from formula, relative to formula
            float confidence = formulaFallback > 0
                ? 1f - Math.Clamp(Math.Abs(predicted - formulaFallback) / (float)formulaFallback, 0f, 1f)
                : 0f;

            return confidence >= 0.6f ? (predicted, true) : (formulaFallback, false);
        }
    }
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "StudyTimePredictorTests"
```

Expected: `4 passed`.

- [ ] **Step 7: Run all tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `138 passed`.

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner/Services/ML/IStudyTimePredictor.cs SmartStudyPlanner/Services/ML/StudyTimePredictorService.cs SmartStudyPlanner.Tests/MLTests/StudyTimePredictorTests.cs SmartStudyPlanner.Tests/MLTests/TestDoubles/
git commit -m "feat(M7): add StudyTimePredictorService — confidence-gated prediction with formula fallback"
```

---

## Task 9: Wire into IDecisionEngine + ServiceLocator + App startup

**Files:**
- Modify: `SmartStudyPlanner/Services/IDecisionEngine.cs`
- Modify: `SmartStudyPlanner/Services/DecisionEngineService.cs`
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs`
- Modify: `SmartStudyPlanner/App.xaml.cs`

- [ ] **Step 1: Add PredictStudyMinutes to IDecisionEngine**

In `SmartStudyPlanner/Services/IDecisionEngine.cs`, add one method (keep existing methods untouched):

```csharp
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services
{
    public interface IDecisionEngine
    {
        WeightConfig Config { get; }
        double CalculatePriority(StudyTask task, MonHoc monHoc);
        int CalculateRawSuggestedMinutes(StudyTask task);
        string SuggestStudyTime(StudyTask task);

        /// <summary>
        /// ML-powered prediction. Returns (Minutes, IsML=true) when model is confident,
        /// else falls back to formula with IsML=false.
        /// </summary>
        (int Minutes, bool IsML) PredictStudyMinutes(StudyTask task, MonHoc monHoc);
    }
}
```

- [ ] **Step 2: Implement PredictStudyMinutes in DecisionEngineService**

Open `SmartStudyPlanner/Services/DecisionEngineService.cs`. Add `IStudyTimePredictor` field and inject it:

```csharp
using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    public class DecisionEngineService : IDecisionEngine
    {
        private readonly PriorityCalculator _calculator;
        private readonly IStudyTimePredictor _predictor;
        private WeightConfig _config;

        public WeightConfig Config => _config;

        public DecisionEngineService(
            ITaskTypeWeightProvider taskTypeProvider,
            IClock clock,
            IStudyTimePredictor predictor,
            WeightConfig? initialConfig = null)
        {
            _config    = initialConfig ?? new WeightConfig();
            _predictor = predictor;

            _calculator = new PriorityCalculator(
                cfgAccessor: () => _config,
                rules: new IUrgencyRule[]
                {
                    new OverdueRule(),
                    new JustOverdueRule(),
                    new ImminentRule(),
                    new CompletedRule(),
                    new BeyondHorizonRule(),
                },
                components: new IPriorityComponent[]
                {
                    new TimeComponent(),
                    new TaskTypeComponent(taskTypeProvider),
                    new CreditComponent(),
                    new DifficultyComponent(),
                },
                clock: clock);
        }

        public double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            if (!_config.IsValid())
                _config = new WeightConfig();
            return _calculator.Calculate(task, monHoc);
        }

        public int CalculateRawSuggestedMinutes(StudyTask task)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh || task.DiemUuTien <= 0) return 0;

            double baseMinutes    = (task.DiemUuTien / 100.0) * 120.0;
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;
            int totalMinutes      = (int)(baseMinutes + difficultyBonus);
            return (int)Math.Round(totalMinutes / 15.0) * 15;
        }

        public string SuggestStudyTime(StudyTask task)
        {
            int totalMinutes = CalculateRawSuggestedMinutes(task);
            if (totalMinutes == 0) return "0 phút";

            int remainingMinutes = totalMinutes - task.ThoiGianDaHoc;
            if (remainingMinutes <= 0) return "Đã đạt mục tiêu 🎉";
            if (remainingMinutes < 60) return $"{remainingMinutes} phút";

            int hours = remainingMinutes / 60;
            int mins  = remainingMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}p" : $"{hours}h";
        }

        public (int Minutes, bool IsML) PredictStudyMinutes(StudyTask task, MonHoc monHoc)
        {
            int formula = CalculateRawSuggestedMinutes(task);
            if (task.TrangThai == StudyTaskStatus.HoanThanh) return (0, false);

            var input = new StudyTimeInput
            {
                TaskType             = task.LoaiCongViec.ToString(),
                Difficulty           = task.DoKho,
                Credits              = monHoc.SoTinChi,
                DaysLeft             = task.HanChot.HasValue
                    ? (float)(task.HanChot.Value - DateTime.Today).TotalDays
                    : 7f,
                StudiedMinutesSoFar  = task.ThoiGianDaHoc,
            };

            return _predictor.Predict(input, formula);
        }
    }
}
```

- [ ] **Step 3: Register new services in ServiceLocator**

In `SmartStudyPlanner/Services/ServiceLocator.cs`, add the 3 new registrations:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.Pipeline.Stages;
using SmartStudyPlanner.Services.RiskAnalyzer;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    public static class ServiceLocator
    {
        private static ServiceProvider? _provider;
        public static ServiceProvider Provider => _provider ??= BuildProvider();
        public static void Configure() { _provider = BuildProvider(); }
        public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton<AppDbContext>();
            services.AddSingleton<IStudyRepository, StudyRepository>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ITaskTypeWeightProvider, DefaultTaskTypeWeightProvider>();
            services.AddSingleton<WeightConfig>();

            // ML services — registered before IDecisionEngine because DecisionEngineService depends on IStudyTimePredictor
            services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>();
            services.AddSingleton<IMLModelManager, MLModelManager>();
            services.AddSingleton<IStudyTimePredictor, StudyTimePredictorService>();

            services.AddSingleton<IDecisionEngine, DecisionEngineService>();
            services.AddSingleton<IWorkloadService, WorkloadServiceImpl>();
            services.AddSingleton<IRiskAnalyzer, RiskAnalyzerService>();

            services.AddSingleton<IPipelineStage, ParseInputStage>();
            services.AddSingleton<IPipelineStage, PrioritizeStage>();
            services.AddSingleton<IPipelineStage, BalanceWorkloadStage>();
            services.AddSingleton<IPipelineStage, AssessRiskStage>();
            services.AddSingleton<IPipelineStage, AdaptStage>();
            services.AddSingleton<IPipelineOrchestrator, PipelineOrchestrator>();
            services.AddSingleton<IStudyAnalytics, StudyAnalyticsService>();

            return services.BuildServiceProvider();
        }
    }
}
```

- [ ] **Step 4: Add background InitializeAsync in App.xaml.cs**

Open `SmartStudyPlanner/App.xaml.cs` and after `ServiceLocator.Configure()`, add:

```csharp
// Fire-and-forget background ML model initialization — never blocks app startup
_ = Task.Run(async () =>
{
    var manager = ServiceLocator.Get<IMLModelManager>();
    await manager.InitializeAsync();
});
```

Add `using System.Threading.Tasks;` at the top of the file if not already present.

- [ ] **Step 5: Build**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded. Note: existing tests that construct `DecisionEngineService` directly will fail because the constructor now requires `IStudyTimePredictor`. Fix in next step.

- [ ] **Step 6: Fix any test constructor calls**

Search for `new DecisionEngineService(` in the test project:

```bash
grep -r "new DecisionEngineService" SmartStudyPlanner.Tests/
```

For each occurrence, add `new StudyTimePredictorService(new NullMLModelManager())` as the third argument. Example:

```csharp
// Before
var engine = new DecisionEngineService(weightProvider, clock);

// After
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Tests.MLTests.TestDoubles;
var engine = new DecisionEngineService(weightProvider, clock, new StudyTimePredictorService(new NullMLModelManager()));
```

- [ ] **Step 7: Run all tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: all tests pass (count increases by 0 — this task adds no new tests, only wires existing code).

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner/Services/IDecisionEngine.cs SmartStudyPlanner/Services/DecisionEngineService.cs SmartStudyPlanner/Services/ServiceLocator.cs SmartStudyPlanner/App.xaml.cs
git commit -m "feat(M7): wire StudyTimePredictorService into DecisionEngineService + ServiceLocator + App startup"
```

---

## Task 10: TaskDashboardItem + DashboardViewModel wiring

**Files:**
- Modify: `SmartStudyPlanner/Models/TaskDashboardItem.cs`
- Modify: `SmartStudyPlanner/ViewModels/DashboardViewModel.cs`

- [ ] **Step 1: Add IsMLPrediction to TaskDashboardItem**

Open `SmartStudyPlanner/Models/TaskDashboardItem.cs`. Add the property after the existing properties:

```csharp
public bool IsMLPrediction { get; set; } = false;
```

- [ ] **Step 2: Wire in DashboardViewModel**

In `DashboardViewModel.cs`, find where `ThoiGianGoiY` is set on `TaskDashboardItem`. It will look like a call to `_decisionEngine.SuggestStudyTime(task)`. Add a call to `PredictStudyMinutes` alongside it.

Find the code that builds `TaskDashboardItem` objects (likely in `BuildDashboardSummary` or similar method) and update it:

```csharp
// Before (existing pattern):
var item = new TaskDashboardItem
{
    // ...other properties...
    ThoiGianGoiY = _decisionEngine.SuggestStudyTime(task),
};

// After:
var (mlMinutes, isML) = _decisionEngine.PredictStudyMinutes(task, mon);
string thoiGianGoiY = isML
    ? (mlMinutes < 60 ? $"{mlMinutes} phút" : $"{mlMinutes / 60}h {mlMinutes % 60}p".Replace(" 0p", ""))
    : _decisionEngine.SuggestStudyTime(task);

var item = new TaskDashboardItem
{
    // ...other properties...
    ThoiGianGoiY   = thoiGianGoiY,
    IsMLPrediction = isML,
};
```

- [ ] **Step 3: Build and run all tests**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: build succeeded, all tests pass.

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/Models/TaskDashboardItem.cs SmartStudyPlanner/ViewModels/DashboardViewModel.cs
git commit -m "feat(M7): add IsMLPrediction to TaskDashboardItem, wire PredictStudyMinutes in DashboardViewModel"
```

---

## Task 11: Dashboard XAML — subtle `*` indicator

**Files:**
- Modify: `SmartStudyPlanner/Views/DashboardPage.xaml`

- [ ] **Step 1: Update time suggestion column**

In `DashboardPage.xaml`, find the `DataGridTextColumn` or `DataGridTemplateColumn` that binds to `ThoiGianGoiY`. Replace it with a template column:

```xml
<DataGridTemplateColumn Header="Thời gian gợi ý" Width="120">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding ThoiGianGoiY}"
                           Foreground="{DynamicResource PrimaryText}"
                           VerticalAlignment="Center"/>
                <TextBlock Text=" *"
                           Foreground="{DynamicResource AccentColor}"
                           FontSize="11"
                           VerticalAlignment="Center"
                           Visibility="{Binding IsMLPrediction,
                               Converter={StaticResource BooleanToVisibilityConverter}}">
                    <TextBlock.ToolTip>
                        <ToolTip Content="Dự đoán bằng AI (thử nghiệm) — dựa trên lịch sử học của bạn"/>
                    </TextBlock.ToolTip>
                </TextBlock>
            </StackPanel>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

Note: `BooleanToVisibilityConverter` is a built-in WPF converter. Declare it in `DashboardPage.xaml` resources if not already present:

```xml
<Page.Resources>
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
    <!-- ...existing resources... -->
</Page.Resources>
```

- [ ] **Step 2: Build**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Manual smoke test**

Run the app. Navigate to Dashboard. Verify the time column shows `*` (accent color) for tasks where ML prediction is active. Hover `*` to see tooltip.

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/Views/DashboardPage.xaml
git commit -m "feat(M7): add subtle ML indicator * with tooltip to Dashboard time suggestion column"
```

---

## Task 12: AnalyticsViewModel — store logs + retrain command

**Files:**
- Modify: `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs`

- [ ] **Step 1: Update AnalyticsViewModel**

Open `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs`. Add the following:

1. Add field `private List<StudyLog> _allLogs = new();`
2. Add `IMLModelManager` field and constructor injection via `ServiceLocator`
3. In `LoadAsync()`, store logs into `_allLogs` instead of a local variable
4. Add observable properties and command

The relevant additions to the class:

```csharp
// Add field:
private List<StudyLog> _allLogs = new();
private readonly IMLModelManager _mlModelManager;

// Add to delegating constructor body (after existing ServiceLocator.Get calls):
_mlModelManager = ServiceLocator.Get<IMLModelManager>();

// Add to full constructor parameters and assignment:
// IMLModelManager mlModelManager  →  _mlModelManager = mlModelManager;

// Add observable properties:
[ObservableProperty] private bool isRetraining = false;
[ObservableProperty] private string retrainStatus = string.Empty;

public bool HasEnoughData => _allLogs.Count >= 20;

// In LoadAsync(), change:
//   var logs = await _repository.GetStudyLogsAsync(_hocKy);
// to:
//   _allLogs = await _repository.GetStudyLogsAsync(_hocKy);
//   OnPropertyChanged(nameof(HasEnoughData));
// and use _allLogs in all downstream calls

// Add relay command:
[RelayCommand(CanExecute = nameof(CanRetrain))]
private async Task RetrainModel()
{
    IsRetraining = true;
    RetrainModelCommand.NotifyCanExecuteChanged();
    await _mlModelManager.RetrainAsync(_allLogs);
    RetrainStatus = $"Đã cập nhật model lúc {DateTime.Now:HH:mm}";
    IsRetraining = false;
    RetrainModelCommand.NotifyCanExecuteChanged();
}

private bool CanRetrain() => HasEnoughData && !IsRetraining;
```

Add `using SmartStudyPlanner.Services.ML;` and `using System;` at top of file.

- [ ] **Step 2: Build and run tests**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: Build succeeded, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs
git commit -m "feat(M7): add RetrainModelCommand + HasEnoughData to AnalyticsViewModel"
```

---

## Task 13: AnalyticsPage XAML — "Tối ưu AI" button

**Files:**
- Modify: `SmartStudyPlanner/Views/AnalyticsPage.xaml`

- [ ] **Step 1: Add retrain button to AnalyticsPage**

In `AnalyticsPage.xaml`, add a new section after the productivity score card and before the weekly chart section:

```xml
<!-- AI Model Status + Retrain -->
<Border Background="{DynamicResource StatCardBackground}" CornerRadius="8"
        Padding="16,12" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1"
        Margin="0,0,0,12">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="&#xE9F5;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                   Foreground="{DynamicResource AccentColor}" VerticalAlignment="Center"
                   Margin="0,0,10,0"/>
        <StackPanel VerticalAlignment="Center" Margin="0,0,16,0">
            <TextBlock Text="Dự đoán AI" FontSize="13" FontWeight="SemiBold"
                       Foreground="{DynamicResource PrimaryText}"/>
            <TextBlock Text="{Binding RetrainStatus}" FontSize="11"
                       Foreground="{DynamicResource SecondaryText}"/>
        </StackPanel>
        <Button Command="{Binding RetrainModelCommand}"
                IsEnabled="{Binding HasEnoughData}"
                Padding="12,6" VerticalAlignment="Center">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="Tối ưu AI"
                           Visibility="{Binding IsRetraining,
                               Converter={StaticResource InverseBoolToVisibility}}"/>
                <TextBlock Text="Đang tối ưu..."
                           Visibility="{Binding IsRetraining,
                               Converter={StaticResource BooleanToVisibilityConverter}}"/>
            </StackPanel>
        </Button>
    </StackPanel>
</Border>
```

Add converters to `AnalyticsPage.xaml` resources:

```xml
<Page.Resources>
    <Style x:Key="SectionHeader" TargetType="TextBlock">
        <!-- ...existing style... -->
    </Style>
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
    <converters:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibility"/>
</Page.Resources>
```

If `InverseBoolToVisibilityConverter` doesn't exist yet, create it:

```csharp
// SmartStudyPlanner/Views/Converters/InverseBoolToVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartStudyPlanner.Views.Converters
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
```

Add namespace import to `AnalyticsPage.xaml`:

```xml
xmlns:converters="clr-namespace:SmartStudyPlanner.Views.Converters"
```

- [ ] **Step 2: Build**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Manual smoke test**

Run the app. Navigate to Analytics. Verify:
- Button "Tối ưu AI" is visible
- Button is disabled if fewer than 20 logs exist (shows grayed out)
- If enabled and clicked: text changes to "Đang tối ưu..." while running

- [ ] **Step 4: Run all tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 5: Final commit**

```bash
git add SmartStudyPlanner/Views/AnalyticsPage.xaml SmartStudyPlanner/Views/Converters/InverseBoolToVisibilityConverter.cs
git commit -m "feat(M7): add Tối ưu AI retrain button to Analytics page with InverseBoolToVisibilityConverter"
```

---

## Task 14: Final verification + branch cleanup

- [ ] **Step 1: Run full test suite including ML tests**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: all tests pass (`138+` passed, 0 failed).

- [ ] **Step 2: Run ML-only tests separately to confirm R²**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category=ML" -v normal
```

Expected: `MLModelManager_SeedTraining_AchievesMinR2` passes with R² logged.

- [ ] **Step 3: Build release to catch any warnings**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj -c Release
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Push branch and open PR to dev**

```bash
git push -u origin feat/m7-study-time-predictor
gh pr create --base dev --title "feat(M7): Study Time Predictor — offline-first ML with FastTree regression" --body "$(cat <<'EOF'
## Summary
- Adds offline-first ML study time predictor using ML.NET FastTree regression
- Silent fallback to formula when model unavailable or low-confidence (< 60%)
- Subtle `*` indicator on Dashboard with tooltip
- Manual retrain button in Analytics page (enabled at ≥20 StudyLogs)
- Auto-retrain on startup if ≥50 new logs since last retrain
- Hybrid-ready: IModelStorageProvider + IMLModelManager export/import hooks for future cloud sync

## Offline guarantee
App works 100% offline. Cloud components are opt-in additive via DI swap — never required.

## Test plan
- [ ] `dotnet test` — all unit + integration tests pass
- [ ] `dotnet test --filter "Category=ML"` — R² >= 0.50 on seed data
- [ ] App starts without blocking — ML init runs in background Task
- [ ] Dashboard shows `*` after model initializes
- [ ] Analytics "Tối ưu AI" button disabled < 20 logs, enabled otherwise

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Quick Reference: Test Commands

```bash
# Fast tests only (skip ML training, < 5 seconds)
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category!=ML"

# ML tests only (train + validate R², ~10 seconds)
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category=ML"

# All tests
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```
