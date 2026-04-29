# M7 — ML Engine: Study Time Predictor
## Design Spec · 2026-04-26

> **Scope MVP**: Sub-model 2 (Study Time Predictor) only.
> Sub-model 1 (Text Classifier) và Sub-model 3 (Weight Optimizer) đưa sang M8.
>
> **Nguyên tắc cốt lõi**: Offline-first vĩnh viễn. Cloud sync là opt-in additive — không bao giờ required.

---

## 0. Tóm tắt quyết định thiết kế

| Câu hỏi | Quyết định |
|---|---|
| Training data ban đầu | Synthetic seed ~180 rows (auto-generated), user cung cấp CSV thực sau |
| Scope MVP | Sub-model 2 (Study Time Predictor) — regression với FastTree |
| Fallback UX | Subtle indicator `*` + tooltip "Dự đoán AI (thử nghiệm)" |
| Model storage | `%AppData%\SmartStudyPlanner\models\` |
| Retrain trigger | Auto (≥50 StudyLogs mới) + nút thủ công trong Analytics page |
| Cloud dependency | **Không có** — app chạy 100% offline vĩnh viễn |
| Hybrid-ready | Interface boundaries đúng chỗ; cloud chỉ register khi user cấu hình |

---

## 1. Kiến trúc tổng quan

### Cấu trúc thư mục mới

```
Services/ML/
├── IStudyTimePredictor.cs          — interface dự đoán thời gian học
├── StudyTimePredictorService.cs    — load zip + predict + confidence check
├── IMLModelManager.cs              — interface quản lý model lifecycle
├── MLModelManager.cs               — train, retrain, load, save, export/import hooks
├── IModelStorageProvider.cs        — interface abstract hóa path resolution
├── LocalModelStorageProvider.cs    — implementation local AppData (dùng trong M7)
└── Schema/
    ├── StudyTimeInput.cs           — 6 input features cho ML.NET
    ├── StudyTimeOutput.cs          — output Score (PredictedMinutes)
    └── ModelMeta.cs                — metadata lưu vào meta.json
```

### Luồng khởi động

```
App.xaml.cs OnStartup()
  └── ServiceLocator.Configure()
        └── services.AddSingleton<IMLModelManager, MLModelManager>()
        └── services.AddSingleton<IStudyTimePredictor, StudyTimePredictorService>()
        └── services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>()

MainWindow Loaded (background Task.Run)
  └── IMLModelManager.InitializeAsync()
        ├── study_time.zip tồn tại? → Load model → IsReady = true
        └── Chưa có → SeedTrainingData() → Train → Save zip → IsReady = true
```

### Luồng dự đoán

```
DecisionEngineService.CalculateRawSuggestedMinutes(task)
  └── IStudyTimePredictor.Predict(input)
        ├── IsReady = false → trả formula cũ, IsMLPrediction = false
        ├── Predict() → Score = predictedMinutes
        ├── confidence = 1 - clamp(|predicted - formula| / formula, 0, 1)
        ├── confidence >= 0.6 → trả predictedMinutes, IsMLPrediction = true
        └── confidence < 0.6  → trả formula cũ, IsMLPrediction = false
```

### Luồng retrain

```
Auto (app startup):
  MLModelManager.InitializeAsync()
    └── GetStudyLogsSinceAsync(lastRetrainedAt) → count >= 50?
          └── Task.Run(() => RetrainAsync(logs))  ← không block UI

Manual (Analytics page):
  Nút "Tối ưu AI" click
    └── IMLModelManager.RetrainAsync(allLogs)
          └── Hiện spinner trên nút → ẩn khi xong
```

---

## 2. Offline Contract

**Cam kết**: Không có network call nào trong toàn bộ M7. Cloud là opt-in additive.

| Component | Offline behavior |
|---|---|
| `StudyTimePredictorService` | Đọc `study_time.zip` từ AppData local. Không có network call. |
| `MLModelManager` | Đọc SQLite local + ghi filesystem local. Không có network call. |
| `LocalModelStorageProvider` | `%AppData%\SmartStudyPlanner\models\` — filesystem only. |
| `StudyLog` sync fields | Chỉ là cột thêm trên SQLite. Không trigger request nào. |
| Export/Import hooks | No-op implementation trong M7. |

**Quy tắc DI cho cloud tương lai:**

```csharp
// App.xaml.cs — chỉ register cloud nếu user tự cấu hình
if (appSettings.CloudEnabled && ConnectivityHelper.IsAvailable())
    services.AddSingleton<IModelStorageProvider, CloudModelStorageProvider>();
else
    services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>(); // ← M7 luôn nhánh này
```

Sync failure **không bao giờ block UI** — `StudyLogSyncService` (tương lai) chạy fire-and-forget.

---

## 3. Schema

### 3.1 StudyLog — thêm 3 sync-ready fields

```csharp
// Models/StudyLog.cs — bổ sung vào entity đã có từ M6
public class StudyLog
{
    [Key] public Guid Id          { get; set; } = Guid.NewGuid();
    public Guid MaTask            { get; set; }
    public DateTime NgayHoc       { get; set; }
    public int SoPhutHoc          { get; set; }
    public int SoPhutDuKien       { get; set; }
    public bool DaHoanThanh       { get; set; }
    public string? GhiChu         { get; set; }

    // Sync-ready fields (thêm M7) — EnsureCreated tự thêm cột, không breaking
    public DateTime CreatedAtUtc  { get; set; } = DateTime.UtcNow;
    public string DeviceId        { get; set; } = DeviceHelper.GetId();
    public bool IsDeleted         { get; set; } = false;
}
```

**`DeviceHelper.GetId()`**: `"desktop-" + sha256(Environment.MachineName)[..8]` — stable per machine, không PII.

### 3.2 StudyTimeInput

```csharp
public class StudyTimeInput
{
    public string TaskType           { get; set; }  // LoaiCongViec.ToString()
    public float Difficulty          { get; set; }  // DoKho 1.0–5.0
    public float Credits             { get; set; }  // MonHoc.SoTinChi 1.0–5.0
    public float DaysLeft            { get; set; }  // (Deadline - Now).Days
    public float StudiedMinutesSoFar { get; set; }  // StudyTask.ThoiGianDaHoc
    [ColumnName("Label")]
    public float Label               { get; set; }  // Training only: SoPhutHoc thực tế
}
```

### 3.3 StudyTimeOutput

```csharp
public class StudyTimeOutput
{
    [ColumnName("Score")]
    public float Score { get; set; }  // PredictedMinutes
}
```

### 3.4 ModelMeta

```json
// %AppData%\SmartStudyPlanner\models\meta.json
{
  "lastRetrainedAt": "2026-04-26T10:00:00Z",
  "logsUsedCount": 52,
  "modelVersion": 3,
  "seedOnly": false,
  "deviceId": "desktop-a1b2c3d4",
  "modelHash": "sha256-of-zip-bytes"
}
```

---

## 4. Synthetic Seed Data

**180 rows** chia 3 nhóm × 60 dòng, mỗi dòng có Gaussian noise ±15%:

| Nhóm | Điều kiện | Label range |
|---|---|---|
| Nhẹ | Difficulty ≤ 2, Credits ≤ 2, DaysLeft ≥ 7 | 20–60 phút |
| Trung | Difficulty = 3, Credits = 3, DaysLeft 3–7 | 60–120 phút |
| Nặng | Difficulty ≥ 4, Credits ≥ 4, DaysLeft ≤ 3 | 120–240 phút |

**Khi retrain với data thực**: Merge 70% real logs + 30% seed để tránh catastrophic forgetting khi data thực còn ít (< 100 rows).

---

## 5. Module-by-module: Nhiệm vụ, Mô tả, Hướng dẫn

### M7-1 — NuGet + Directory Scaffold

**Nhiệm vụ**: Cài ML.NET packages và tạo cấu trúc thư mục `Services/ML/`.

**Mô tả**: Đây là bước nền tảng. Không có logic nào, chỉ thêm dependencies và tạo file stub để M7-2 trở đi có thể compile.

**Hướng dẫn**:
1. Thêm vào `SmartStudyPlanner.csproj`:
   ```xml
   <PackageReference Include="Microsoft.ML" Version="3.0.1" />
   <PackageReference Include="Microsoft.ML.FastTree" Version="3.0.1" />
   ```
2. Tạo tất cả file trong `Services/ML/` với body stub `throw new NotImplementedException()`.
3. Tạo `DeviceHelper.cs` trong `Services/ML/`:
   ```csharp
   internal static class DeviceHelper
   {
       public static string GetId()
       {
           var hash = System.Security.Cryptography.SHA256.HashData(
               System.Text.Encoding.UTF8.GetBytes(Environment.MachineName));
           return "desktop-" + Convert.ToHexString(hash)[..8].ToLower();
       }
   }
   ```
4. Thêm `SmartStudyPlanner.Tests/MLTests/` folder với `.gitkeep`.

**Verification**: `dotnet build` thành công, 128 tests vẫn pass.

---

### M7-2 — StudyLog Sync Fields + IStudyRepository.GetStudyLogsSinceAsync

**Nhiệm vụ**: Bổ sung 3 sync fields vào `StudyLog`, thêm `GetStudyLogsSinceAsync` vào interface và implementation.

**Mô tả**: Đây là migration schema duy nhất của M7. `EnsureCreated()` sẽ tự thêm cột mới vào SQLite — không breaking với data cũ (nullable/default). Method `GetStudyLogsSinceAsync` dùng ngay trong M7-5 (retrain) và tương lai trong sync service.

**Hướng dẫn**:
1. Mở `Models/StudyLog.cs`, thêm sau field `GhiChu`:
   ```csharp
   public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
   public string DeviceId       { get; set; } = DeviceHelper.GetId();
   public bool IsDeleted        { get; set; } = false;
   ```
2. Thêm vào `Data/IStudyRepository.cs`:
   ```csharp
   Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
   ```
3. Implement trong `Data/StudyRepository.cs`:
   ```csharp
   public async Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
   {
       await using var db = _contextFactory.CreateDbContext();
       return await db.StudyLogs
           .Where(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted)
           .OrderBy(l => l.CreatedAtUtc)
           .ToListAsync(ct);
   }
   ```
4. Cập nhật `FakeStudyRepository` trong Tests để implement method mới (trả `new List<StudyLog>()`).

**Verification**: `dotnet test` → 128 tests pass. App khởi động, `StudyLog` table có 3 cột mới.

---

### M7-3 — Schema Classes + IModelStorageProvider

**Nhiệm vụ**: Implement `StudyTimeInput`, `StudyTimeOutput`, `ModelMeta`, `IModelStorageProvider`, `LocalModelStorageProvider`.

**Mô tả**: Các lớp này là building blocks thuần túy, không có business logic phức tạp. `IModelStorageProvider` là interface boundary quan trọng nhất cho hybrid-readiness — tất cả I/O đến filesystem đi qua đây.

**Hướng dẫn**:
1. Implement `Schema/StudyTimeInput.cs` và `Schema/StudyTimeOutput.cs` theo spec §3.2 và §3.3.
2. Implement `Schema/ModelMeta.cs`:
   ```csharp
   public class ModelMeta
   {
       public DateTime LastRetrainedAt { get; set; }
       public int LogsUsedCount        { get; set; }
       public int ModelVersion         { get; set; }
       public bool SeedOnly            { get; set; } = true;
       public string DeviceId          { get; set; } = DeviceHelper.GetId();
       public string ModelHash         { get; set; } = string.Empty;
   }
   ```
3. Implement `IModelStorageProvider.cs`:
   ```csharp
   public interface IModelStorageProvider
   {
       string GetModelPath(string modelName);
       Task<byte[]?> ReadAsync(string modelName);
       Task WriteAsync(string modelName, byte[] data);
       Task<ModelMeta?> ReadMetaAsync();
       Task WriteMetaAsync(ModelMeta meta);
   }
   ```
4. Implement `LocalModelStorageProvider.cs`: base path = `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartStudyPlanner", "models")`. `EnsureDirectoryExists()` trong constructor.
5. Register trong `ServiceLocator`: `services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>()`.

**Verification**: `dotnet build`. Unit test `LocalModelStorageProvider_WriteRead_Roundtrip` — ghi bytes → đọc lại → assert equal.

---

### M7-4 — IMLModelManager + MLModelManager (Train + Load + Retrain)

**Nhiệm vụ**: Implement toàn bộ model lifecycle — sinh seed data, train FastTree, save/load zip, atomic swap khi retrain.

**Mô tả**: Đây là module phức tạp nhất M7. `MLModelManager` chứa toàn bộ ML.NET pipeline. `InitializeAsync()` phải non-blocking với app startup — gọi từ background Task.

**Hướng dẫn**:
1. Implement `IMLModelManager.cs`:
   ```csharp
   public interface IMLModelManager
   {
       bool IsReady { get; }
       int ModelVersion { get; }
       Task InitializeAsync();
       Task RetrainAsync(IEnumerable<StudyLog> logs);
       ITransformer? GetModel();

       // Hybrid-ready hooks — no-op trong M7
       Task<byte[]> ExportModelBytesAsync();
       Task ImportModelAsync(byte[] modelBytes, int version);
   }
   ```
2. `MLModelManager` constructor nhận `IModelStorageProvider`. Dùng `MLContext` với `seed: 42` để reproducible.
3. `InitializeAsync()`:
   - Đọc `study_time.zip` qua `IModelStorageProvider.ReadAsync("study_time")`
   - Tồn tại → `_model = mlContext.Model.Load(stream, out _)` → `IsReady = true`
   - Không tồn tại → gọi `TrainOnSeedAsync()`
   - Check auto-retrain: `GetStudyLogsSinceAsync(meta.LastRetrainedAt).Count >= 50` → `Task.Run(RetrainAsync)`
4. `TrainOnSeedAsync()`: Gọi `SeedDataGenerator.Generate(180)` → train → validate R² ≥ 0.55 → save.
5. `RetrainAsync(logs)`: Merge 70% real + 30% seed → train → validate R² ≥ 0.50 → atomic swap (ghi `.tmp` trước → rename).
6. `TrainPipeline()`:
   ```csharp
   var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("TaskType")
       .Append(mlContext.Transforms.Concatenate("Features", "TaskType", "Difficulty", "Credits", "DaysLeft", "StudiedMinutesSoFar"))
       .Append(mlContext.Regression.Trainers.FastTree(numberOfLeaves: 20, numberOfTrees: 100));
   ```
7. `ExportModelBytesAsync()` → đọc zip bytes từ storage, trả về. `ImportModelAsync()` → ghi bytes → reload. (Cả hai dùng được ngay, chỉ là không có caller trong M7.)

**Verification**:
- `MLModelManager_TrainsOnSeedData_AchievesMinR2`: train 180 rows → R² ≥ 0.55.
- `MLModelManager_RetrainAsync_UpdatesMeta`: retrain với 10 logs → `meta.LogsUsedCount == 10`, `meta.SeedOnly == false`.
- `MLModelManager_AtomicSwap_PreservesOldModelOnFailure`: mock R² = 0.1 (fail) → zip cũ không bị overwrite.

---

### M7-5 — IStudyTimePredictor + StudyTimePredictorService

**Nhiệm vụ**: Implement prediction service với confidence check và fallback về formula cũ.

**Mô tả**: Layer mỏng wrap `IMLModelManager`. Đây là điểm tích hợp duy nhất vào `DecisionEngineService` — mọi thứ bên dưới đều ẩn sau interface này.

**Hướng dẫn**:
1. Implement `IStudyTimePredictor.cs`:
   ```csharp
   public interface IStudyTimePredictor
   {
       (int Minutes, bool IsMLPrediction) Predict(StudyTimeInput input, int formulaFallback);
   }
   ```
2. `StudyTimePredictorService` constructor nhận `IMLModelManager`.
3. `Predict()`:
   ```csharp
   if (!_manager.IsReady) return (formulaFallback, false);

   var engine = _mlContext.Model.CreatePredictionEngine<StudyTimeInput, StudyTimeOutput>(_manager.GetModel());
   var result = engine.Predict(input);
   var predicted = Math.Max(10, (int)result.Score);

   float confidence = 1f - Math.Clamp(
       Math.Abs(predicted - formulaFallback) / (float)Math.Max(1, formulaFallback), 0f, 1f);

   return confidence >= 0.6f ? (predicted, true) : (formulaFallback, false);
   ```
4. Register: `services.AddSingleton<IStudyTimePredictor, StudyTimePredictorService>()`.
5. Cập nhật signature `DecisionEngineService.CalculateRawSuggestedMinutes(task, monHoc)` (thêm `MonHoc` tham số thứ hai — nhất quán với `CalculatePriority` đã có). Trả về tuple `(int Minutes, bool IsML)`:
   ```csharp
   public (int Minutes, bool IsML) CalculateRawSuggestedMinutes(StudyTask task, MonHoc monHoc)
   {
       int formulaResult = /* công thức cũ */;
       var input = new StudyTimeInput
       {
           TaskType = task.LoaiCongViec.ToString(),
           Difficulty = task.DoKho,
           Credits = monHoc.SoTinChi,
           DaysLeft = task.HanChot.HasValue ? (float)(task.HanChot.Value - _clock.Now).TotalDays : 7f,
           StudiedMinutesSoFar = task.ThoiGianDaHoc,
       };
       return _studyTimePredictor.Predict(input, formulaResult);
   }
   ```
   Caller (ViewModel) nhận tuple và set `taskDashboardItem.IsMLPrediction = isML` — **không** đặt trên `StudyTask` (domain model không chứa UI state).

**Verification**:
- `StudyTimePredictorService_ReturnsFallback_WhenModelNotReady`: `IsReady = false` → `IsMLPrediction = false`.
- `StudyTimePredictorService_ReturnsFallback_WhenLowConfidence`: mock model trả 9999 → formula thắng.
- `StudyTimePredictorService_ReturnsMlResult_WhenHighConfidence`: mock model trả gần formula → `IsMLPrediction = true`.

---

### M7-6 — Subtle Indicator UI

**Nhiệm vụ**: Hiện dấu `*` và tooltip "Dự đoán AI (thử nghiệm)" cạnh số phút gợi ý trong Dashboard.

**Mô tả**: UI change tối thiểu — chỉ thêm 1 property `IsMLPrediction` vào `TaskDashboardItem` và sửa binding trong XAML.

**Hướng dẫn**:
1. Thêm `public bool IsMLPrediction { get; set; }` vào `Models/TaskDashboardItem.cs`.
2. Set property này từ `DecisionEngineService` khi tính suggested minutes.
3. Trong `DashboardPage.xaml`, cột "Thời gian gợi ý":
   ```xml
   <StackPanel Orientation="Horizontal">
       <TextBlock Text="{Binding ThoiGianGoiY}"/>
       <TextBlock Text=" *" Foreground="{DynamicResource AccentColor}"
                  Visibility="{Binding IsMLPrediction, Converter={StaticResource BoolToVisibility}}">
           <TextBlock.ToolTip>
               <ToolTip Content="Dự đoán bằng AI (thử nghiệm) — dựa trên lịch sử học của bạn"/>
           </TextBlock.ToolTip>
       </TextBlock>
   </StackPanel>
   ```

**Verification**: Chạy app → Dashboard → cột thời gian gợi ý hiện `*` với tooltip khi model sẵn sàng.

---

### M7-7 — Nút "Tối ưu AI" trong Analytics Page

**Nhiệm vụ**: Thêm nút retrain thủ công vào `AnalyticsPage.xaml` và wire vào `AnalyticsViewModel`.

**Mô tả**: Nút enabled khi `HasEnoughData` (≥20 logs). Khi bấm: hiện spinner → gọi `RetrainAsync` → ẩn spinner → cập nhật label "Đã cập nhật model lúc HH:mm".

**Hướng dẫn**:
1. Thêm vào `AnalyticsViewModel` (`_allLogs` là `List<StudyLog>` đã được load trong `LoadAsync()` hiện có — lưu vào field thay vì biến local):
   ```csharp
   private List<StudyLog> _allLogs = new();  // set trong LoadAsync()

   [ObservableProperty] private bool isRetraining = false;
   [ObservableProperty] private string retrainStatus = string.Empty;
   public bool HasEnoughData => _allLogs.Count >= 20;

   [RelayCommand(CanExecute = nameof(CanRetrain))]
   private async Task RetrainModel()
   {
       IsRetraining = true;
       await _mlModelManager.RetrainAsync(_allLogs);
       RetrainStatus = $"Đã cập nhật model lúc {DateTime.Now:HH:mm}";
       IsRetraining = false;
   }
   private bool CanRetrain() => HasEnoughData && !IsRetraining;
   ```
2. Trong `AnalyticsPage.xaml`, thêm vào footer card:
   ```xml
   <Button Command="{Binding RetrainModelCommand}" IsEnabled="{Binding HasEnoughData}">
       <StackPanel Orientation="Horizontal">
           <TextBlock Text="Tối ưu AI" Visibility="{Binding IsRetraining, Converter={StaticResource InverseBool}}"/>
           <TextBlock Text="Đang tối ưu..." Visibility="{Binding IsRetraining, Converter={StaticResource BoolToVisibility}}"/>
       </StackPanel>
   </Button>
   <TextBlock Text="{Binding RetrainStatus}" FontSize="11"
              Foreground="{DynamicResource SecondaryText}"/>
   ```
   (Dùng `BoolToVisibilityConverter` đã có — không cần converter mới.)

**Verification**: Mở Analytics → có ≥20 logs → nút enabled → bấm → spinner → label cập nhật.

---

## 6. Thứ tự triển khai và dependencies

```
M7-1 (NuGet + scaffold)
  └── M7-2 (StudyLog fields + GetStudyLogsSinceAsync)
        └── M7-3 (Schema + IModelStorageProvider)
              └── M7-4 (MLModelManager — train/load/retrain)
                    └── M7-5 (StudyTimePredictorService — predict + fallback)
                          ├── M7-6 (UI indicator)
                          └── M7-7 (Analytics retrain button)
```

M7-6 và M7-7 có thể làm song song sau M7-5.

---

## 7. Testing Strategy

### Tổ chức test

```
SmartStudyPlanner.Tests/
├── MLTests/
│   ├── MLModelManagerTests.cs      — train, retrain, atomic swap
│   ├── StudyTimePredictorTests.cs  — fallback, confidence, prediction
│   └── LocalModelStorageTests.cs  — read/write roundtrip
└── (existing test files)
```

### Danh sách test cần viết

| Test | Module | Assert |
|---|---|---|
| `MLModelManager_TrainsOnSeedData_AchievesMinR2` | M7-4 | R² ≥ 0.55 |
| `MLModelManager_RetrainAsync_UpdatesMeta` | M7-4 | `meta.SeedOnly == false` |
| `MLModelManager_AtomicSwap_PreservesOldModelOnFailure` | M7-4 | zip cũ không bị overwrite |
| `StudyTimePredictorService_ReturnsFallback_WhenModelNotReady` | M7-5 | `IsMLPrediction == false` |
| `StudyTimePredictorService_ReturnsFallback_WhenLowConfidence` | M7-5 | formula wins |
| `StudyTimePredictorService_ReturnsMlResult_WhenHighConfidence` | M7-5 | `IsMLPrediction == true` |
| `LocalModelStorageProvider_WriteRead_Roundtrip` | M7-3 | bytes in == bytes out |
| `GetStudyLogsSinceAsync_FiltersCorrectly` | M7-2 | chỉ log sau sinceUtc, IsDeleted=false |

**Lưu ý**: Test ML.NET train trên 180 rows synthetic chạy ~2–3 giây — đặt trong `MLTests/` folder và tag `[Trait("Category", "ML")]` để có thể skip khi chỉ chạy unit tests nhanh:
```
dotnet test --filter "Category!=ML"   # fast unit tests only
dotnet test                            # tất cả
```

---

## 8. Ước lượng thời gian

| Module | Effort |
|---|---|
| M7-1: NuGet + scaffold | ~30 phút |
| M7-2: StudyLog fields + repo method | ~30 phút |
| M7-3: Schema + IModelStorageProvider | ~45 phút |
| M7-4: MLModelManager (phức tạp nhất) | ~3–4 giờ |
| M7-5: StudyTimePredictorService | ~1 giờ |
| M7-6: UI indicator | ~30 phút |
| M7-7: Analytics retrain button | ~45 phút |
| **Tổng** | **~7–8 giờ** |

---

## 9. File map đầy đủ

| Action | Path |
|---|---|
| **Create** | `Services/ML/IStudyTimePredictor.cs` |
| **Create** | `Services/ML/StudyTimePredictorService.cs` |
| **Create** | `Services/ML/IMLModelManager.cs` |
| **Create** | `Services/ML/MLModelManager.cs` |
| **Create** | `Services/ML/IModelStorageProvider.cs` |
| **Create** | `Services/ML/LocalModelStorageProvider.cs` |
| **Create** | `Services/ML/DeviceHelper.cs` |
| **Create** | `Services/ML/Schema/StudyTimeInput.cs` |
| **Create** | `Services/ML/Schema/StudyTimeOutput.cs` |
| **Create** | `Services/ML/Schema/ModelMeta.cs` |
| **Modify** | `Models/StudyLog.cs` — thêm 3 sync fields |
| **Modify** | `Data/IStudyRepository.cs` — thêm `GetStudyLogsSinceAsync` |
| **Modify** | `Data/StudyRepository.cs` — implement method mới |
| **Modify** | `Services/ServiceLocator.cs` — register 3 services mới |
| **Modify** | `Services/DecisionEngineService.cs` — inject + gọi `IStudyTimePredictor` |
| **Modify** | `Models/TaskDashboardItem.cs` — thêm `IsMLPrediction` |
| **Modify** | `Views/DashboardPage.xaml` — subtle indicator `*` |
| **Modify** | `ViewModels/AnalyticsViewModel.cs` — retrain command |
| **Modify** | `Views/AnalyticsPage.xaml` — nút "Tối ưu AI" |
| **Create** | `SmartStudyPlanner.Tests/MLTests/MLModelManagerTests.cs` |
| **Create** | `SmartStudyPlanner.Tests/MLTests/StudyTimePredictorTests.cs` |
| **Create** | `SmartStudyPlanner.Tests/MLTests/LocalModelStorageTests.cs` |

---

## 10. Quyết định để ngỏ (cho M8)

- **Sub-model 1 (Text Classifier)**: Cần user cung cấp file CSV ~500 câu tiếng Việt + nhãn `{TaskType, Difficulty}` trước khi implement.
- **Sub-model 3 (Weight Optimizer)**: Cần ≥200 `StudyLog` rows thực từ user để AutoML có ý nghĩa.
- **ONNX export**: Nếu mobile client tương lai cần inference, export model sang ONNX từ `ExportModelBytesAsync()`.
- **Federated learning**: Nếu nhiều user đủ lớn, server tổng hợp model từ nhiều `ExportModelBytesAsync()` rồi `ImportModelAsync()` lại.
