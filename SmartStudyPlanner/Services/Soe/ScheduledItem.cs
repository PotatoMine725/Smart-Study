using System;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Một "chunk" lịch học mang theo identity của task nguồn — <see cref="MaTask"/> +
    /// <see cref="HanChot"/> — thứ mà <see cref="SmartStudyPlanner.Models.ScheduledTask"/>
    /// (UI-only, xem Models/ScheduleModels.cs) không có.
    ///
    /// Đây là internal representation nội bộ của <c>WorkloadServiceImpl.GenerateSchedule</c>
    /// (T3.8, Epic 3 Card C): allocator build danh sách này trước, rồi CHIẾU (project) từng
    /// phần tử sang <see cref="SmartStudyPlanner.Models.ScheduledTask"/> cho seam công khai
    /// <c>List&lt;ScheduleDay&gt;</c> — seam đó KHÔNG đổi shape. <c>IConstraintValidator</c>
    /// (T3.1) và <c>IObjectiveEvaluator</c> (T3.2) sẽ tiêu thụ trực tiếp kiểu này (qua
    /// <c>WorkloadServiceImpl.GenerateScheduleWithIdentity</c>, internal + InternalsVisibleTo
    /// SmartStudyPlanner.Tests), vì cả hai đều cần biết "chunk này thuộc task nào" / "hạn chót
    /// bao giờ" — hai câu hỏi List&lt;ScheduleDay&gt; hiện tại không trả lời được.
    ///
    /// Namespace đặt cạnh <c>SmartStudyPlanner.Tests.Services.Soe</c> (đã tồn tại từ Card A,
    /// SoeCorpusGenerator.cs) theo đúng quy ước mirror-namespace 1:1 của repo — Card D/E sẽ
    /// tiếp tục ở namespace này.
    /// </summary>
    public sealed class ScheduledItem
    {
        /// <summary>Khóa chính của <see cref="SmartStudyPlanner.Models.StudyTask"/> nguồn.
        /// Mọi phần bị cắt nhỏ ("(Phần n)") của cùng một task chia sẻ đúng một MaTask.</summary>
        public Guid MaTask { get; init; }

        /// <summary>Hạn chót của task nguồn — copy nguyên giá trị từ <c>StudyTask.HanChot</c>
        /// tại thời điểm lên lịch. GenerateSchedule không dùng giá trị này để quyết định vị trí
        /// (đó là việc của T3.1/T3.3, không phải của card này) — chỉ mang nó đi theo.</summary>
        public DateTime HanChot { get; init; }

        /// <summary>Tên gốc của task, KHÔNG kèm hậu tố " (Phần n)" — bằng <c>StudyTask.TenTask</c>.</summary>
        public string TenTaskGoc { get; init; } = string.Empty;

        /// <summary>Tên hiển thị cho chunk này — bằng <see cref="TenTaskGoc"/> nếu task không bị
        /// cắt qua nhiều ngày, hoặc kèm " (Phần n)" nếu bị cắt. Đây chính là giá trị được chiếu
        /// thẳng vào <c>ScheduledTask.TenTask</c> — giữ nguyên logic đặt tên hiện tại của
        /// GenerateSchedule (WorkloadServiceImpl.cs, khối phân bổ).</summary>
        public string TenHienThi { get; init; } = string.Empty;

        /// <summary>Tên môn học sở hữu task — bằng <c>MonHoc.TenMonHoc</c>.</summary>
        public string TenMon { get; init; } = string.Empty;

        /// <summary>Ngày (đã .Date) mà chunk này được xếp vào.</summary>
        public DateTime Date { get; init; }

        /// <summary>Số phút của chunk này.</summary>
        public int SoPhut { get; init; }
    }
}
