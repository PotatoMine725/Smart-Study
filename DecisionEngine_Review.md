# DecisionEngine — Senior C# Architect Review

> Phân tích kiến trúc và hướng dẫn refactor Decision Engine của SmartStudyPlanner sang Strategy Pattern.

---

## 1. Dependency Graph — Trạng thái hiện tại

```
                    ┌─────────────────────┐
                    │   Models (Domain)   │
                    │  StudyTask · MonHoc │
                    │  HocKy · LoaiCongViec│
                    └──────────▲──────────┘
                               │ (đọc)
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
┌───────┴────────┐    ┌────────┴────────┐    ┌────────┴────────┐
│  SmartParser   │    │  DecisionEngine │    │  StreakManager  │
│  (static)      │    │  (static)       │    │  (static)       │
│  Parse()       │    │  +WeightConfig  │    │  +UserStreakData│
└────────────────┘    └────────▲────────┘    └─────────────────┘
                               │ (gọi CalculatePriority,
                               │  CalculateRawSuggestedMinutes)
                      ┌────────┴─────────┐
                      │ WorkloadService  │◄──── ScheduleDay / ScheduledTask
                      │ (static)         │      (POCO nằm chung file)
                      │ GenerateSchedule │
                      └────────▲─────────┘
                               │
   ┌───────────────────────────┼───────────────────────────┐
   │                           │                           │
┌──┴──────────────┐  ┌─────────┴──────────┐  ┌─────────────┴────┐
│ DashboardVM     │  │ QuanLyTaskVM       │  │ MainWindow.xaml.cs│
│ (3 call points) │  │ (1 call point)     │  │ (1 call point)    │
└─────────────────┘  └────────────────────┘  └───────────────────┘

ThemeManager (static, độc lập, chỉ phụ thuộc WPF Application)
```

### Call sites của DecisionEngine (đã grep xác thực)

| File | Dòng | Hàm gọi |
|---|---|---|
| `Services/WorkloadService.cs` | 54 | `CalculatePriority` |
| `Services/WorkloadService.cs` | 72 | `CalculateRawSuggestedMinutes` |
| `ViewModels/DashboardViewModel.cs` | 86 | `CalculatePriority` |
| `ViewModels/DashboardViewModel.cs` | 89 | `CalculateRawSuggestedMinutes` |
| `ViewModels/DashboardViewModel.cs` | 122 | `SuggestStudyTime` |
| `ViewModels/QuanLyTaskViewModel.cs` | 65 | `CalculatePriority` |
| `Views/MainWindow.xaml.cs` | 81 | `CalculatePriority` |

### Điểm nghẽn kiến trúc (code smells)

| # | Smell | Mức độ | Vị trí |
|---|---|---|---|
| A | **Static God Class** — `DecisionEngine` là `static class`, không inject được, không mock test được | Cao | `DecisionEngine.cs:24` |
| B | **Global mutable state** — `DecisionEngine.Config` là static setter, race condition tiềm tàng | Cao | `DecisionEngine.cs:26` |
| C | **Switch-on-enum** vi phạm Open/Closed — thêm `LoaiCongViec` mới phải sửa 2-3 chỗ | Cao | `DecisionEngine.cs:31-39` |
| D | **Magic-number chain** trong `CalculatePriority` (−3, 0, 1, HorizonDays) — trộn lẫn rule urgency với tính điểm trọng số | Cao | `DecisionEngine.cs:55-60` |
| E | **Keyword if-else rừng** trong `SmartParser.Parse` — 20+ nhánh, khó mở rộng ngôn ngữ | Trung | `SmartParser.cs:20-52` |
| F | **Coupling chéo** — `WorkloadService`, 3 ViewModel, `MainWindow.xaml.cs` đều gọi thẳng static ⇒ khó refactor không breaking | Cao | nhiều file |

---

## 2. Phân tích technical debt — 3 ổ switch/if-else chính

### Ổ 1 — `LayHeSoQuanTrong` (switch-case thuần) — `DecisionEngine.cs:29-40`

```csharp
switch (loaiTask) {
    case LoaiCongViec.ThiCuoiKy: return 1.0;
    case LoaiCongViec.DoAnCuoiKy: return 0.8;
    case LoaiCongViec.ThiGiuaKy: return 0.6;
    case LoaiCongViec.KiemTraThuongXuyen: return 0.3;
    case LoaiCongViec.BaiTapVeNha: return 0.1;
    default: return 0.1;
}
```

**Vấn đề**: Mỗi lần thêm loại công việc mới (vd `ThuyetTrinh`) phải sửa file này. Vi phạm OCP. Hệ số bị hard-code, không cấu hình runtime được.

### Ổ 2 — Urgency rule chain — `DecisionEngine.cs:55-60`

```csharp
if (soNgayConLai < -3) return 0.0;
if (soNgayConLai < 0)  return 100.0;
if (soNgayConLai < 1)  return 95.0;
if (task.TrangThai == "Hoàn thành") return 0.0;
if (soNgayConLai > Config.HorizonDays) return 1.0;
```

**Vấn đề**: Trộn 5 rule khác nhau (quá hạn, cận kề, hoàn thành, ngoài horizon) vào 1 hàm. Muốn thêm rule "task bị pause" phải đụng hàm cốt lõi.

### Ổ 3 — 4 công thức tính điểm trọng số — `DecisionEngine.cs:62-77`

4 biểu thức (`diemThoiGian`, `diemLoaiTask`, `diemTinChi`, `diemDoKho`) cộng lại với `Config.*Weight`. Đây chính là "components" — mỗi component là 1 Strategy.

---

## 3. Hướng dẫn Refactor — Strategy Pattern, từng bước

**Triết lý**: tách `DecisionEngine` thành 3 tầng Strategy có thể test/mở rộng độc lập:

- `ITaskTypeWeightProvider` — thay cho Ổ 1
- `IUrgencyRule` (chain) — thay cho Ổ 2
- `IPriorityComponent` — thay cho Ổ 3

### Bước 1 — Tạo thư mục `Services/Strategies/`

Giữ nguyên `DecisionEngine.cs` làm facade để không breaking 7 call sites đã liệt kê ở bảng trên.

### Bước 2 — Refactor Ổ 1 (switch → Dictionary Strategy)

Tạo `Services/Strategies/ITaskTypeWeightProvider.cs`:

```csharp
using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface ITaskTypeWeightProvider
    {
        double GetWeight(LoaiCongViec loai);
    }

    public class DefaultTaskTypeWeightProvider : ITaskTypeWeightProvider
    {
        private readonly IReadOnlyDictionary<LoaiCongViec, double> _map =
            new Dictionary<LoaiCongViec, double>
            {
                [LoaiCongViec.ThiCuoiKy]          = 1.0,
                [LoaiCongViec.DoAnCuoiKy]         = 0.8,
                [LoaiCongViec.ThiGiuaKy]          = 0.6,
                [LoaiCongViec.KiemTraThuongXuyen] = 0.3,
                [LoaiCongViec.BaiTapVeNha]        = 0.1,
            };

        public double GetWeight(LoaiCongViec loai) =>
            _map.TryGetValue(loai, out var w) ? w : 0.1;
    }
}
```

**Lợi**: thêm loại mới = thêm 1 dòng dictionary. Muốn A/B test? Inject provider khác. Load từ JSON được.

### Bước 3 — Refactor Ổ 2 (if-chain → Chain of Strategies)

```csharp
public interface IUrgencyRule
{
    bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score);
}

public class OverdueRule : IUrgencyRule
{
    public bool TryApply(StudyTask t, double d, WeightConfig c, out double s)
    { s = 0; if (d < -3) { s = 0.0; return true; } return false; }
}

public class JustOverdueRule : IUrgencyRule
{
    public bool TryApply(StudyTask t, double d, WeightConfig c, out double s)
    { s = 0; if (d < 0) { s = 100.0; return true; } return false; }
}

public class ImminentRule : IUrgencyRule
{
    public bool TryApply(StudyTask t, double d, WeightConfig c, out double s)
    { s = 0; if (d < 1) { s = 95.0; return true; } return false; }
}

public class CompletedRule : IUrgencyRule
{
    public bool TryApply(StudyTask t, double d, WeightConfig c, out double s)
    { s = 0; if (t.TrangThai == "Hoàn thành") { s = 0.0; return true; } return false; }
}

public class BeyondHorizonRule : IUrgencyRule
{
    public bool TryApply(StudyTask t, double d, WeightConfig c, out double s)
    { s = 0; if (d > c.HorizonDays) { s = 1.0; return true; } return false; }
}
```

Engine tiêu thụ rule theo thứ tự ưu tiên:

```csharp
private readonly IReadOnlyList<IUrgencyRule> _urgencyRules = new IUrgencyRule[]
{
    new OverdueRule(),
    new JustOverdueRule(),
    new ImminentRule(),
    new CompletedRule(),
    new BeyondHorizonRule(),
};
```

**Lợi**: mỗi rule test unit độc lập. Thêm rule "task pause" = thêm 1 class + 1 dòng danh sách.

### Bước 4 — Refactor Ổ 3 (4 công thức → Component Strategies)

```csharp
public interface IPriorityComponent
{
    double Score(StudyTask task, MonHoc mon, WeightConfig cfg);
    double Weight(WeightConfig cfg);
}

public class TimeComponent : IPriorityComponent
{
    public double Score(StudyTask t, MonHoc m, WeightConfig c)
    {
        double daysLeft = (t.HanChot.Date - System.DateTime.Now.Date).TotalDays;
        return System.Math.Max(0, 100.0 * (1.0 - daysLeft / c.HorizonDays));
    }
    public double Weight(WeightConfig c) => c.TimeWeight;
}

public class TaskTypeComponent : IPriorityComponent
{
    private readonly ITaskTypeWeightProvider _provider;
    public TaskTypeComponent(ITaskTypeWeightProvider p) => _provider = p;

    public double Score(StudyTask t, MonHoc m, WeightConfig c)
        => _provider.GetWeight(t.LoaiTask) * 100;
    public double Weight(WeightConfig c) => c.TaskTypeWeight;
}

public class CreditComponent : IPriorityComponent
{
    public double Score(StudyTask t, MonHoc m, WeightConfig c)
    {
        int tinChiHopLe = System.Math.Max(1, m.SoTinChi);
        double diem = (tinChiHopLe / (double)c.MaxCredits) * 100;
        return diem > 100 ? 100 : diem;
    }
    public double Weight(WeightConfig c) => c.CreditWeight;
}

public class DifficultyComponent : IPriorityComponent
{
    public double Score(StudyTask t, MonHoc m, WeightConfig c)
    {
        int doKhoHopLe = System.Math.Min(c.MaxDifficulty, System.Math.Max(1, t.DoKho));
        return (doKhoHopLe / (double)c.MaxDifficulty) * 100;
    }
    public double Weight(WeightConfig c) => c.DifficultyWeight;
}
```

### Bước 5 — Compose lại engine

Chuyển `DecisionEngine` từ `static class` → `class` có constructor injection, đồng thời giữ wrapper static tạm thời:

```csharp
public class PriorityCalculator
{
    private readonly WeightConfig _cfg;
    private readonly IReadOnlyList<IUrgencyRule> _rules;
    private readonly IReadOnlyList<IPriorityComponent> _components;

    public PriorityCalculator(WeightConfig cfg,
        IReadOnlyList<IUrgencyRule> rules,
        IReadOnlyList<IPriorityComponent> components)
    { _cfg = cfg; _rules = rules; _components = components; }

    public double Calculate(StudyTask task, MonHoc mon)
    {
        if (task == null || mon == null) return 0.0;
        if (!_cfg.IsValid()) return 0.0;

        var daysLeft = (task.HanChot.Date - System.DateTime.Now.Date).TotalDays;

        foreach (var rule in _rules)
            if (rule.TryApply(task, daysLeft, _cfg, out var early))
                return System.Math.Round(early, 2);

        double total = 0;
        foreach (var c in _components)
            total += c.Score(task, mon, _cfg) * c.Weight(_cfg);

        return System.Math.Round(total, 2);
    }
}
```

### Bước 6 — Giữ facade để tương thích ngược

```csharp
public static class DecisionEngine
{
    public static WeightConfig Config { get; set; } = new WeightConfig();
    private static PriorityCalculator _calc = BuildDefault();

    private static PriorityCalculator BuildDefault()
    {
        var provider = new DefaultTaskTypeWeightProvider();
        return new PriorityCalculator(Config,
            new IUrgencyRule[]
            {
                new OverdueRule(), new JustOverdueRule(),
                new ImminentRule(), new CompletedRule(),
                new BeyondHorizonRule()
            },
            new IPriorityComponent[]
            {
                new TimeComponent(),
                new TaskTypeComponent(provider),
                new CreditComponent(),
                new DifficultyComponent()
            });
    }

    public static double CalculatePriority(StudyTask task, MonHoc mon)
        => _calc.Calculate(task, mon);

    // CalculateRawSuggestedMinutes & SuggestStudyTime giữ nguyên
}
```

**Lợi**: 7 call sites cũ (`WorkloadService`, 3 ViewModels, `MainWindow.xaml.cs`) không cần sửa 1 dòng nào — zero breaking.

### Bước 7 — Viết unit test từng Strategy

`PriorityCalculatorTests`:

- `Overdue_3Days_Returns0`
- `DueTomorrow_Returns95`
- `Completed_Returns0`
- `ComponentsSumCorrectly` — inject fake components trả về 100, 0, 0, 0 → verify = TimeWeight × 100

Từ trước không thể test được vì `DecisionEngine` là static + đọc `DateTime.Now` trực tiếp.

### Bước 8 (optional) — Inject `IClock`

Bước 2 và 3 nên đi kèm `IClock { DateTime Now { get; } }` để rule urgency test được deterministic. Đây là cleanup nhỏ nhưng mở đường cho test toàn bộ engine.

---

## 4. Roadmap đề xuất

| Thứ tự | Việc | Rủi ro | Thời gian |
|---|---|---|---|
| 1 | Ổ 1 (TaskType provider) — isolate nhất | Rất thấp | 30 phút |
| 2 | Viết test cho Ổ 1 | — | 20 phút |
| 3 | Ổ 3 (4 Components) | Thấp | 1 giờ |
| 4 | Ổ 2 (Urgency rules) + `IClock` | Trung | 1-2 giờ |
| 5 | Compose `PriorityCalculator` + facade | Thấp (có facade che) | 30 phút |
| 6 | `SmartParser` refactor (Keyword Strategy / Chain) | Trung — độc lập | riêng 1 buổi |

**Khuyến nghị**: làm tuần tự Bước 2 → 4 trước, tạm để `SmartParser` sau vì nó độc lập và không nằm trong Decision Engine.
