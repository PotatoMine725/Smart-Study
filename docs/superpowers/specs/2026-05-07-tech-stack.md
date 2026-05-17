# Smart Study Planner — Tech Stack
## Spec · 2026-05-07

## 1. Purpose
Tài liệu này liệt kê và giải thích stack công nghệ hiện tại của dự án, dựa trên file project, package references và cách code đang được tổ chức.

## 2. Product / runtime stack
- `WPF` trên `.NET 10` là UI runtime hiện tại.
- `WinExe`, `UseWPF`, `UseWindowsForms` cho thấy app chạy như desktop Windows application.
- `C#` là ngôn ngữ chính.
- App mang tính desktop-first, offline-first, local-first.

## 3. Data and persistence stack
- `SQLite` là database local chính.
- `Entity Framework Core` được dùng để map entity và truy cập dữ liệu.
- `AppDbContext` tự cấu hình connection string trỏ về file `SmartStudyData.db` trong thư mục chạy ứng dụng.
- Repository pattern được dùng qua `IStudyRepository` / `StudyRepository`.

## 4. MVVM and UI stack
- `CommunityToolkit.Mvvm` hỗ trợ `ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`.
- `LiveChartsCore.SkiaSharpView.WPF` được dùng cho chart trong dashboard / analytics.
- `Microsoft.Toolkit.Uwp.Notifications` dùng cho toast notification trên Windows.
- Themes và styles nằm trong `Themes/`.

## 5. Dependency injection and composition
- `Microsoft.Extensions.DependencyInjection` cung cấp DI container.
- `ServiceLocator` là lớp composition root hiện tại.
- Services được đăng ký singleton theo mô hình shared application services.

## 6. ML stack
- `Microsoft.ML` và `Microsoft.ML.FastTree` dùng cho dự đoán thời gian học.
- `MLModelManager` quản lý training, retraining, load/save model.
- Model và metadata được lưu local bằng filesystem provider.
- ML được thiết kế như enhancement, không phải hard dependency.

## 7. Testing stack
- `xUnit` là framework test.
- `Microsoft.NET.Test.Sdk` và `xunit.runner.visualstudio` hỗ trợ chạy test.
- `coverlet.collector` hỗ trợ coverage.
- `Verify.CommunityToolkit.Mvvm` phục vụ snapshot/verification cho ViewModel-related output.

## 8. Project configuration stack
`SmartStudyPlanner.csproj` cho thấy các cấu hình quan trọng:
- target framework: `net10.0-windows10.0.19041.0`
- nullable reference types: enabled
- implicit usings: enabled
- desktop host: WPF + Windows Forms enabled
- versioning: `1.5.0`

## 9. Package-level summary
Các dependency chính hiện tại có thể nhóm như sau:
- UI / MVVM: `CommunityToolkit.Mvvm`, `LiveChartsCore.SkiaSharpView.WPF`
- Storage: `Microsoft.EntityFrameworkCore.Sqlite`
- ML: `Microsoft.ML`, `Microsoft.ML.FastTree`
- DI: `Microsoft.Extensions.DependencyInjection`
- Notifications: `Microsoft.Toolkit.Uwp.Notifications`
- Tests: `xUnit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`

## 10. Practical implications
- Stack hiện tại tối ưu cho Windows desktop.
- ML local giúp giữ privacy và offline capability.
- SQLite + EF Core giúp dữ liệu đơn giản, portable, dễ reset dev.
- Toolkit-based MVVM giúp ViewModel code ngắn và testable.
- Charts / notifications cho thấy app có dashboard-driven UX.
