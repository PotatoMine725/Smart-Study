using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Một vi phạm hạn chót cụ thể: đúng MỘT <see cref="ScheduledItem"/> (một chunk) bị xếp vào
    /// ngày sau hạn chót của task nguồn, theo <see cref="IDeadlinePolicy"/> đang dùng.
    ///
    /// Đây là chi tiết ở mức CHUNK (per-item), không phải mức task — một task bị cắt nhỏ thành
    /// nhiều "(Phần n)" có thể góp NHIỀU <see cref="DeadlineViolation"/> (một cho mỗi chunk trễ
    /// hạn), dù <see cref="ConstraintValidationResult.ViolatingTaskCount"/> chỉ đếm task đó MỘT
    /// lần (xem doc comment của <see cref="ConstraintValidationResult"/> để hiểu vì sao hai con số
    /// này KHÔNG bằng nhau, và tại sao đó là thiết kế đúng chứ không phải bug).
    /// </summary>
    /// <param name="MaTask">Task nguồn của chunk vi phạm — bằng <see cref="ScheduledItem.MaTask"/>.</param>
    /// <param name="TenTaskGoc">Tên gốc của task, không hậu tố "(Phần n)" — bằng
    /// <see cref="ScheduledItem.TenTaskGoc"/>, phục vụ explainability (D-J: "explanations
    /// distinguish rejected: infeasible from rejected: lower score").</param>
    /// <param name="Date">Ngày chunk này bị xếp vào — bằng <see cref="ScheduledItem.Date"/>.</param>
    /// <param name="HanChot">Hạn chót của task nguồn — bằng <see cref="ScheduledItem.HanChot"/>.</param>
    /// <param name="SoPhut">Số phút của chunk vi phạm này — đây chính là phần đóng góp của chunk
    /// vào <see cref="ConstraintValidationResult.TotalOverdueMinutes"/>.</param>
    public sealed record DeadlineViolation(
        Guid MaTask,
        string TenTaskGoc,
        DateTime Date,
        DateTime HanChot,
        int SoPhut);

    /// <summary>
    /// Kết quả validate một lịch (một danh sách <see cref="ScheduledItem"/>) qua
    /// <see cref="IConstraintValidator"/>. Đây là seam D-J/D-H cần: một predicate khả thi/không
    /// khả thi (<see cref="IsFeasible"/>), CỘNG với đủ dữ liệu để một consumer tương lai (T3.4)
    /// tính được cả hai khóa so sánh của D-H —
    /// <c>violations(output) &lt;= violations(input)</c>, so trước theo SỐ LƯỢNG vi phạm cứng, sau
    /// theo TỔNG SỐ PHÚT TRỄ HẠN.
    ///
    /// <b>Vì sao <see cref="ViolatingTaskCount"/> KHÔNG bằng <c>Violations.Count</c>:</b> một task
    /// bị cắt thành nhiều chunk ("(Phần n)") có thể có NHIỀU chunk trễ hạn — mỗi chunk trễ hạn là
    /// một phần tử riêng trong <see cref="Violations"/> (mức chunk, cần cho magnitude), nhưng
    /// <see cref="ViolatingTaskCount"/> đếm theo <c>MaTask</c> PHÂN BIỆT (mức task, cần cho D-H's
    /// đếm số vi phạm cứng). Lý do đếm theo task, không theo chunk: cách allocator cắt một task
    /// thành bao nhiêu chunk là chi tiết triển khai của T3.3 (kích thước capacity/ngày quyết định
    /// số chunk), KHÔNG phải một khía cạnh của tính khả thi. Nếu đếm theo chunk, hai lịch biểu thị
    /// hiện đúng cùng một vấn đề thật (cùng một task trễ hạn) nhưng bị cắt khác nhau sẽ cho ra số
    /// vi phạm khác nhau — con số đó sẽ không còn phản ánh "có bao nhiêu ràng buộc cứng bị vi
    /// phạm" mà phản ánh một quyết định chunking không liên quan. Đếm theo task giữ
    /// <see cref="ViolatingTaskCount"/> bất biến trước việc re-chunking của T3.3, đúng tinh thần
    /// D-H's "violation count" là một phát biểu về tính khả thi, không phải về hình dạng lịch.
    /// (Đọc bị-từ-chối: đếm theo chunk từng được cân nhắc và loại bỏ vì lý do trên — xem seam
    /// decision doc, T3.1, để có lập luận đầy đủ.)
    ///
    /// <see cref="TotalOverdueMinutes"/> NGƯỢC LẠI cố tình cộng theo chunk (không theo task): đây
    /// là magnitude, không phải count, nên "trễ bao nhiêu phút" tự nhiên là tổng số phút của mọi
    /// chunk bị xếp sau hạn chót — không có lý do gì để chỉ đếm chunk trễ nhất của mỗi task.
    /// </summary>
    /// <param name="IsFeasible">true khi không có chunk nào vi phạm hạn chót (tương đương
    /// <c>ViolatingTaskCount == 0</c>).</param>
    /// <param name="ViolatingTaskCount">Số lượng task PHÂN BIỆT (theo <c>MaTask</c>) có ít nhất một
    /// chunk bị xếp sau hạn chót — khóa so sánh THỨ NHẤT của D-H.</param>
    /// <param name="TotalOverdueMinutes">Tổng số phút của MỌI chunk bị xếp sau hạn chót, cộng dồn
    /// trên toàn bộ danh sách — khóa so sánh THỨ HAI của D-H.</param>
    /// <param name="Violations">Chi tiết từng chunk vi phạm, mức chunk (không phải mức task) — phục
    /// vụ explainability; rỗng khi <see cref="IsFeasible"/> là true.</param>
    public sealed record ConstraintValidationResult(
        bool IsFeasible,
        int ViolatingTaskCount,
        int TotalOverdueMinutes,
        IReadOnlyList<DeadlineViolation> Violations);

    /// <summary>
    /// Bộ lọc cứng (hard filter) cho một lịch đã lên (<see cref="ScheduledItem"/> list) — seam
    /// D-J: Constraint Validation là một predicate khả thi/không khả thi ĐỘC LẬP với
    /// <c>IObjectiveEvaluator</c> (T3.2, Card E). KHÔNG BAO GIỜ nhận trọng số, điểm số, hay bất cứ
    /// thứ gì từ phía objective làm input — "no score can purchase a violation" (D-J).
    ///
    /// Epic 3 (T3.1) chỉ ship predicate HẠN CHÓT (deadline). Interface được thiết kế trung lập với
    /// LOẠI ràng buộc — chữ ký <c>Validate</c> không nhắc gì tới "deadline" — để dự phòng capacity
    /// và calendar predicate (nêu tên trong D-G/D-J) có thể thêm SAU mà không cần đổi chữ ký này;
    /// KHÔNG có logic capacity/calendar nào được xây trong Epic 3 (không có nhu cầu thật nào trong
    /// codebase hiện tại để lấp chỗ đó — xem seam decision doc, T3.1, §"scope").
    /// </summary>
    public interface IConstraintValidator
    {
        /// <summary>Validate toàn bộ danh sách chunk của một lịch. KHÔNG có tham số trọng số/điểm
        /// số — nhận đúng và chỉ <see cref="ScheduledItem"/>, đúng tinh thần D-J.</summary>
        ConstraintValidationResult Validate(IReadOnlyList<ScheduledItem> items);
    }
}
