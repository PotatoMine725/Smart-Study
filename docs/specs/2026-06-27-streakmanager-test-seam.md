# StreakManager — Injectable Store (test seam)

> **Trạng thái: ĐÃ CHỐT HƯỚNG, CHƯA IMPLEMENT.** Ghi nhận thiết kế để triển khai
> **Hướng A** trong tương lai. Hiện tại không thay đổi code.

## Scope

**In-scope:** tách lưu trữ của `Services/StreakManager.cs` ra sau một abstraction
(`IStreakStore` / path-provider) để (1) loại bỏ tranh ghi file khi test chạy song song
và (2) mở khoá unit-test cho logic streak.

**Out-of-scope:** thay đổi luật streak (cộng dồn khi cách đúng 1 ngày, reset khi bỏ bê),
schema `streak_data.json`, hay UI hiển thị streak.

## Goal

Làm cho nơi lưu trữ streak **injectable** để `StreakManager` không còn phụ thuộc một
`FilePath` static cố định, qua đó test cô lập được và hết flaky.

## Vấn đề hiện tại (root cause)

`StreakManager` là **static class** ghi vào **đường dẫn cố định**
(`Services/StreakManager.cs:17` → `AppDomain.CurrentDomain.BaseDirectory/streak_data.json`).
xUnit chạy các test collection **song song**; mọi test đi qua
`FocusViewModel.HoanThanh → StreakManager.UpdateStreak → Save → File.WriteAllText`
tranh ghi cùng một file → `IOException`.

- Biểu hiện: `AnalyticsServiceTests.FocusViewModel_WritesStudyLog_OnHoanThanh` **flaky**
  khi chạy full `dotnet test`, **pass khi chạy riêng**.
- Hiện `StreakManager` chỉ có seam cho `_clock` (`Services/StreakManager.cs:19`), **không**
  có seam cho đường dẫn lưu trữ.
- Không liên quan tới feature đang phát triển (UI redesign / dedup môn học). Tham chiếu
  memory `bug-streakmanager-test-file-contention`.

## Contracts (Hướng A — mục tiêu)

```csharp
public interface IStreakStore
{
    UserStreakData Load();
    void Save(UserStreakData data);
}
```

- `StreakManager` chuyển từ **static** sang **instance** (hoặc giữ facade static mỏng bọc
  một instance có thể thay), nhận `IStreakStore` + `IClock` qua constructor.
- **Production:** `JsonFileStreakStore` ghi đúng `BaseDirectory/streak_data.json` (giữ
  nguyên hành vi & schema hiện tại — không phá dữ liệu người dùng đang có).
- **Test:** `InMemoryStreakStore` (hoặc temp-file store theo `Path.GetTempFileName()`),
  loại bỏ hoàn toàn tranh chấp file.
- Đăng ký DI 1 lần; 3 call site đổi từ gọi static sang resolve instance.

**Blast radius (đã khảo sát):**
- `ViewModels/DashboardViewModel.cs:279` — `StreakManager.GetCurrentStreak()`
- `ViewModels/AnalyticsViewModel.cs:154` — `StreakManager.GetCurrentStreak().StreakCount`
- `ViewModels/FocusViewModel.cs:113` — `StreakManager.UpdateStreak()`
- + đăng ký DI / `ServiceLocator`.

**Invariant phải giữ:** schema `streak_data.json` và luật streak không đổi; production
vẫn ghi đúng file cũ tại đúng vị trí.

## Acceptance criteria

- [ ] `IStreakStore` tồn tại; `StreakManager` nhận store + clock qua constructor.
- [ ] Production dùng store ghi `BaseDirectory/streak_data.json` — hành vi/byte output
      giống bản static hiện tại (round-trip test).
- [ ] `FocusViewModel_WritesStudyLog_OnHoanThanh` **pass trong full `dotnet test`** chạy
      song song, lặp lại nhiều lần không flaky.
- [ ] Có unit-test cho luật streak (cộng dồn cách 1 ngày, reset khi > 1 ngày) dùng
      `InMemoryStreakStore` + `FakeClock`, không chạm đĩa.
- [ ] 3 call site build & chạy đúng sau khi chuyển sang instance/DI.

## Non-goals

- Không đổi luật streak hay schema file.
- Không gộp/serialize test bằng `[Collection]` (đó là **Hướng B**, vá tạm; spec này theo
  **Hướng A** — diệt gốc).
- Không đụng pipeline ML / dedup môn học (độc lập hoàn toàn).
