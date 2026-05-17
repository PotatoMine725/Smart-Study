# Phase Refactor Final Report
## Report · 2026-05-12

## Mục tiêu phase

Tách dần ranh giới Core cho risk/scheduling mà vẫn giữ build/test xanh, đồng thời bảo toàn tương thích với layer legacy trong lúc refactor.

## Hiện trạng sau khi chốt phase

- `Core/Risk/Models` đã có model chuẩn cho `RiskAssessment` và `RiskLevel`.
- `Services/RiskAnalyzer` vẫn tồn tại như lớp tương thích để không phá các caller cũ.
- Adapter trong `RiskAnalyzerService` đã map đúng giữa Core model và legacy contract.
- Các test liên quan đến risk/pipeline đã được cập nhật theo hướng dùng Core/legacy bridge mới.
- Build và test hiện đã xanh lại.

## Những gì đã làm

### 1) Tách model risk sang Core
- Thêm `SmartStudyPlanner/Core/Risk/Models/RiskLevel.cs`
- Thêm `SmartStudyPlanner/Core/Risk/Models/RiskAssessment.cs`

### 2) Giữ compatibility layer cho legacy
- Giữ `SmartStudyPlanner/Services/RiskAnalyzer/RiskAssessment.cs` làm class tương thích
- Giữ `SmartStudyPlanner/Services/RiskAnalyzer/RiskLevel.cs` làm enum tương thích
- Giữ `SmartStudyPlanner/Services/RiskAnalyzer/IRiskAnalyzer.cs` để các caller cũ không phải đổi ngay

### 3) Sửa orchestrator/aggregator theo Core
- `Core/Risk/Aggregators/RiskAggregator.cs` dùng model Core
- `Core/Risk/RiskOrchestrator.cs` dùng Core model và Core flow

### 4) Sửa adapter ở service layer
- `SmartStudyPlanner/Services/RiskAnalyzer/RiskAnalyzerService.cs` map từ Core assessment về legacy assessment
- Enum level được map rõ ràng để tránh lỗi chồng namespace/kiểu

### 5) Cập nhật test liên quan
- `SmartStudyPlanner.Tests/RiskAnalyzer/RiskAnalyzerTests.cs`
- `SmartStudyPlanner.Tests/Pipeline/PipelineStageTests.cs`

Các test này được chỉnh để phù hợp với state mới của risk bridge và kiểu enum/model đang dùng.

## Verification

### Build
- `dotnet build SmartStudyPlanner.slnx`
- Kết quả: pass

### Test
- `dotnet test SmartStudyPlanner.slnx --no-build`
- Kết quả: pass
- Summary: `138` passed, `0` failed

## Ghi chú hiện trạng

- Project vẫn còn một số warning tồn tại trước đó, chủ yếu là nullable/reference type và package vulnerability warning từ `System.Drawing.Common`.
- Những warning này chưa được xử lý trong phase này vì mục tiêu chính là chốt refactor boundary an toàn và không làm gãy build/test.
- Phase này dừng ở mức an toàn: Core đã được dựng lên, legacy vẫn sống, và đường nâng cấp sang Core được bảo toàn.

## Kết luận

Phase này đã được chốt sạch theo tiêu chí an toàn:
- refactor được tách từng bước
- không phá compile
- không phá test
- giữ tương thích với code cũ
- tạo nền cho bước tiếp theo là tách tiếp `DecisionEngine` hoặc `Scheduling` theo cùng mô hình bridge/adaptor
