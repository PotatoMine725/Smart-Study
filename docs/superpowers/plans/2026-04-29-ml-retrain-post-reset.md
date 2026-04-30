# ML Retrain Post-Reset — Pipeline Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a manually-run dev seed test that cleans stale ML artifacts, inserts 180 synthetic StudyLogs into the real app DB, and enables end-to-end verification of the ML retrain pipeline via the "Tối ưu AI" button.

**Architecture:** Single xUnit test tagged `[Trait("Category", "Seed")]` in a new `DevTools/` folder inside the test project. The test opens the real app SQLite DB via `AppDbContext` with explicit options (not the test-bin DB). It deletes stale AppData ML artifacts, creates the HocKy → MonHoc → StudyTask scaffold if absent, then inserts 180 StudyLogs across 3 difficulty groups using a fixed random seed for reproducibility.

**Tech Stack:** xUnit, EF Core SQLite, `AppDbContext` (direct instantiation), `Microsoft.EntityFrameworkCore`.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `SmartStudyPlanner.Tests/DevTools/DbSeedTests.cs` | Artifact cleanup + DB scaffold + 180 StudyLog seed |

No production files are modified.

---

## Task 1: DbSeedTests — cleanup artifacts + seed 180 StudyLogs

**Files:**
- Create: `SmartStudyPlanner.Tests/DevTools/DbSeedTests.cs`

**Prerequisites:**
- Run the app at least once so `SmartStudyData.db` exists in `SmartStudyPlanner/bin/`.
- The app DB must have the full schema (EnsureCreated or Migrate already ran).

---

- [ ] **Step 1: Create the DevTools folder and test file**

Create `SmartStudyPlanner.Tests/DevTools/DbSeedTests.cs` with the full content below.

Key design decisions in this file:
- `GetAppDbPath()` walks up from the test binary to find the `.sln` file, then searches `SmartStudyPlanner/bin/` for `SmartStudyData.db`. It picks the most recently modified one if multiple exist (Debug vs Release builds).
- `DeleteMlArtifacts()` removes `study_time.zip` and `meta.json` from `%AppData%\SmartStudyPlanner\models\` before seeding, forcing the app to re-bootstrap cleanly.
- 180 logs are distributed across 3 groups (60 light / 60 medium / 60 heavy) using `Random(42)` so the seed is reproducible across runs.
- `HocKy.NgayKetThuc` is `[NotMapped]` — do not set it; EF Core ignores it.
- `StudyTask.TrangThai` is a string constant from `StudyTaskStatus.ChuaLam`.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.DevTools
{
    [Trait("Category", "Seed")]
    public class DbSeedTests
    {
        // ── DB path helpers ──────────────────────────────────────────

        private static AppDbContext CreateContext()
        {
            var dbPath = GetAppDbPath();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            return new AppDbContext(options);
        }

        private static string GetAppDbPath()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;

            if (dir == null)
                throw new InvalidOperationException(
                    "Cannot locate solution root from: " + AppDomain.CurrentDomain.BaseDirectory);

            var appBinDir = Path.Combine(dir.FullName, "SmartStudyPlanner", "bin");
            if (!Directory.Exists(appBinDir))
                throw new InvalidOperationException(
                    $"App bin dir not found: {appBinDir}. Build the SmartStudyPlanner project first.");

            var dbFiles = Directory.GetFiles(appBinDir, "SmartStudyData.db", SearchOption.AllDirectories);
            if (!dbFiles.Any())
                throw new InvalidOperationException(
                    "SmartStudyData.db not found inside SmartStudyPlanner/bin/. " +
                    "Run the app at least once to create the database.");

            return dbFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).First();
        }

        // ── Artifact cleanup ─────────────────────────────────────────

        private static void DeleteMlArtifacts()
        {
            var modelsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner", "models");

            foreach (var fileName in new[] { "study_time.zip", "meta.json" })
            {
                var path = Path.Combine(modelsDir, fileName);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        // ── Main seed test ───────────────────────────────────────────

        [Fact]
        public async Task Seed_180StudyLogs_ForMlPipelineVerification()
        {
            // Step A: clean stale ML artifacts
            DeleteMlArtifacts();

            await using var db = CreateContext();
            // EnsureCreated is a no-op if tables already exist
            await db.Database.EnsureCreatedAsync();

            // Step B: ensure one HocKy exists
            var hocKy = await db.HocKys.FirstOrDefaultAsync();
            if (hocKy == null)
            {
                hocKy = new HocKy("Học Kỳ Dev Seed", DateTime.Today.AddDays(-90));
                db.HocKys.Add(hocKy);
                await db.SaveChangesAsync();
            }

            // Step C: create 2 MonHoc — one light (2 credits), one heavy (4 credits)
            var monNhe  = new MonHoc("Toán Rời Rạc", 2)          { MaHocKy = hocKy.MaHocKy };
            var monNang = new MonHoc("Lập Trình Nâng Cao", 4)    { MaHocKy = hocKy.MaHocKy };
            db.MonHocs.AddRange(monNhe, monNang);
            await db.SaveChangesAsync();

            // Step D: create 10 StudyTasks — 5 per MonHoc, DoKho spread 1–5
            var tasks = new List<StudyTask>();
            var loaiValues = Enum.GetValues<LoaiCongViec>();

            foreach (var (mon, baseDoKho) in new[] { (monNhe, 1), (monNang, 3) })
            {
                for (int i = 0; i < 5; i++)
                {
                    int doKho = Math.Min(baseDoKho + i, 5);
                    tasks.Add(new StudyTask(
                        tenTask: $"Task {mon.TenMonHoc} K{doKho}",
                        hanChot: DateTime.Today.AddDays(30),
                        loaiTask: loaiValues[i % loaiValues.Length],
                        doKho: doKho)
                    {
                        MaMonHoc = mon.MaMonHoc,
                    });
                }
            }
            db.StudyTasks.AddRange(tasks);
            await db.SaveChangesAsync();

            // Step E: generate 180 StudyLogs across 3 groups
            var rng = new Random(42);   // fixed seed → reproducible

            var tasksLight  = tasks.Where(t => t.DoKho <= 2).ToList();
            var tasksMedium = tasks.Where(t => t.DoKho == 3).ToList();
            var tasksHeavy  = tasks.Where(t => t.DoKho >= 4).ToList();

            var logs = new List<StudyLog>();

            void AddGroup(List<StudyTask> groupTasks, int count, float minMin, float maxMin)
            {
                for (int i = 0; i < count; i++)
                {
                    float noise   = 1f + (rng.NextSingle() - 0.5f) * 0.3f;      // ±15%
                    int soPhut    = (int)Math.Max(10,
                        (minMin + rng.NextSingle() * (maxMin - minMin)) * noise);
                    var task      = groupTasks[i % groupTasks.Count];
                    int daysAgo   = rng.Next(1, 61);

                    logs.Add(new StudyLog
                    {
                        MaTask        = task.MaTask,
                        NgayHoc       = DateTime.Today.AddDays(-daysAgo),
                        SoPhutHoc     = soPhut,
                        SoPhutDuKien  = Math.Max(10, soPhut + rng.Next(-10, 10)),
                        DaHoanThanh   = true,
                        CreatedAtUtc  = DateTime.UtcNow.AddDays(-daysAgo),
                        DeviceId      = "desktop-seed-dev",
                        IsDeleted     = false,
                    });
                }
            }

            AddGroup(tasksLight,  60, 20f,  60f);   // 20–60 min (light)
            AddGroup(tasksMedium, 60, 60f,  120f);  // 60–120 min (medium)
            AddGroup(tasksHeavy,  60, 120f, 240f);  // 120–240 min (heavy)

            db.StudyLogs.AddRange(logs);
            await db.SaveChangesAsync();

            // Step F: verify counts
            var logCount  = await db.StudyLogs.CountAsync();
            var taskCount = await db.StudyTasks.CountAsync();

            Assert.True(logCount  >= 180, $"Expected >= 180 StudyLogs, got {logCount}");
            Assert.True(taskCount >= 10,  $"Expected >= 10 StudyTasks, got {taskCount}");
        }
    }
}
```

---

- [ ] **Step 2: Build test project to verify it compiles**

```bash
dotnet build SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

If you see `LoaiCongViec does not contain a definition for GetValues` — replace `Enum.GetValues<LoaiCongViec>()` with `(LoaiCongViec[])Enum.GetValues(typeof(LoaiCongViec))` (the generic overload requires .NET 5+; check TFM if needed).

---

- [ ] **Step 3: Run all existing tests to confirm no regression**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category!=Seed"
```

Expected: all existing tests pass. The seed test is excluded.

---

- [ ] **Step 4: Run the app once to ensure SmartStudyData.db exists**

Launch `SmartStudyPlanner` from Visual Studio or:

```bash
dotnet run --project SmartStudyPlanner/SmartStudyPlanner.csproj
```

Close the app immediately after it loads. This ensures `SmartStudyData.db` exists in `SmartStudyPlanner/bin/Debug/<tfm>/`.

---

- [ ] **Step 5: Run the seed test**

```bash
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "Category=Seed" -v normal
```

Expected output:
```
Passed  Seed_180StudyLogs_ForMlPipelineVerification [~2s]

Test Run Successful.
1 passed, 0 failed.
```

If `SmartStudyData.db not found` — ensure Step 4 was done and the DB path in `SmartStudyPlanner/bin/` exists.

---

- [ ] **Step 6: Verify artifact deletion**

Check that both ML artifact files are gone:

```bash
ls "$APPDATA\SmartStudyPlanner\models"
```

Expected: directory is empty or does not exist. If `study_time.zip` or `meta.json` still appear, the path in `DeleteMlArtifacts()` is wrong — check `%AppData%\SmartStudyPlanner\models\` manually.

---

- [ ] **Step 7: End-to-end verification in the app**

1. Launch the app. In the background, `MLModelManager.InitializeAsync` runs:
   - Finds no `study_time.zip` → trains on `SeedDataGenerator.Generate(180)` → model ready
2. Navigate to the **Analytics** page.
   - "Tối ưu AI" button must be **enabled** (≥20 logs exist).
3. Click **"Tối ưu AI"**.
   - Text changes to "Đang tối ưu..." while running.
   - After ~3–5s: label "Đã cập nhật model lúc HH:mm" appears.
4. Inspect `%AppData%\SmartStudyPlanner\models\meta.json`:

```json
{
  "SeedOnly": false,
  "LogsUsedCount": 180,
  "ModelVersion": 2
}
```

Expected: `SeedOnly=false`, `LogsUsedCount=180`, `ModelVersion` incremented to 2.

5. Navigate to **Dashboard** — task list loads normally, time suggestion column shows correctly.

---

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner.Tests/DevTools/DbSeedTests.cs
git commit -m "test(dev): add DbSeedTests — seed 180 synthetic StudyLogs for ML pipeline verification"
```

---

## Quick Reference

```bash
# Run seed (dev tool — run manually after DB reset)
dotnet test --filter "Category=Seed"

# Run all other tests (CI-safe)
dotnet test --filter "Category!=Seed"

# Run ML training tests
dotnet test --filter "Category=ML"

# Run everything
dotnet test
```

## Notes

- Re-run the seed test at any time after a future DB reset to restore a verified baseline.
- The seeded data is dev-only. Clear it before shipping if needed: delete `SmartStudyData.db` and re-run the app.
- Each run of the seed appends new records (does not wipe existing ones). Running twice will result in 360+ logs — which is fine for testing but unnecessary.
