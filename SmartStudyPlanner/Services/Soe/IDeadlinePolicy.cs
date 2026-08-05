using System;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Điểm mở rộng (extension point) cho predicate "chunk này có trễ hạn không" — tách riêng
    /// khỏi <see cref="ConstraintValidator"/> theo đúng khuyến nghị của execution plan §2.2:
    /// Epic 3 ship ĐÚNG MỘT implementation đồng nhất (<see cref="UniformDeadlinePolicy"/>), không
    /// có hành vi theo <c>LoaiCongViec</c> (per-type). Một chính sách ân hạn theo loại task
    /// (per-type grace/urgency-decay — bị loại khỏi Epic 3 bởi §2.1/§2.4 của execution plan và bởi
    /// việc D-G bỏ <c>w6</c>) sẽ là implementation THỨ HAI của interface này, ở một card SAU —
    /// không đụng tới <see cref="ConstraintValidator"/> hay <see cref="IConstraintValidator"/>.
    ///
    /// <b>Vì sao chữ ký nhận <see cref="ScheduledItem"/> nguyên khối, không phải hai
    /// <c>DateTime</c> rời:</b> một chính sách ân hạn theo loại task cần biết <c>LoaiCongViec</c>
    /// của task nguồn để quyết định — nhưng <c>LoaiCongViec</c> không có mặt trên
    /// <see cref="ScheduledItem"/> hôm nay. Nếu chữ ký là <c>IsViolation(DateTime placementDate,
    /// DateTime hanChot)</c>, thêm implementation thứ hai bắt buộc phải ĐỔI chữ ký interface này để
    /// nhét thêm loại task vào — đúng cái rewrite mà §2.2 dựng extension point này để tránh. Nhận
    /// <see cref="ScheduledItem"/> nguyên khối giữ chữ ký ổn định: một implementation tương lai chỉ
    /// cần đọc thêm field nó cần (một khi field đó tồn tại trên <c>ScheduledItem</c> hoặc một kiểu
    /// mang theo nó), không cần đổi <see cref="IDeadlinePolicy"/>.
    /// </summary>
    public interface IDeadlinePolicy
    {
        /// <summary>true nếu chunk này (một <see cref="ScheduledItem"/>) bị coi là xếp sau hạn
        /// chót của task nguồn nó — false nếu chưa/đúng hạn.</summary>
        bool IsViolation(ScheduledItem item);
    }

    /// <summary>
    /// Implementation DUY NHẤT của <see cref="IDeadlinePolicy"/> trong Epic 3 — đồng nhất, không
    /// phân biệt <c>LoaiCongViec</c>, KHÔNG "ân hạn" (grace period) nào.
    ///
    /// <b>Quyết định granularity (T3.1, §2.2 của execution plan — bucket ③ của M3.0 doc):
    /// DATE-ONLY, không phải time-of-day.</b> Predicate: <c>item.Date.Date &gt; item.HanChot.Date</c>
    /// — một chunk bị coi là vi phạm CHỈ KHI nó nằm ở một NGÀY sau ngày của hạn chót; thành phần
    /// giờ:phút của <c>HanChot</c> bị bỏ qua hoàn toàn. Nghĩa là với hạn chót 09:00 ngày 5, đặt một
    /// chunk VÀO ngày 5 KHÔNG phải vi phạm (đọc "360 phút khả dụng" trong ví dụ bucket ③), không
    /// phải đọc "300 phút" (ngày 5 coi như mất vì hạn chót rơi vào buổi sáng).
    ///
    /// Lý do chọn date-only (không phải một sở thích, mà một ràng buộc từ chính seam đang tiêu
    /// thụ):
    /// 1. <see cref="ScheduledItem.Date"/> được tài liệu hoá là "đã .Date" — bản thân mô hình lịch
    ///    của allocator hoạt động ở độ chi tiết CẢ NGÀY, không có khái niệm "chunk này nằm ở giờ
    ///    nào trong ngày". Một predicate time-of-day sẽ phải BỊA thêm một giả định (ví dụ: "chunk
    ///    luôn được coi là xảy ra vào cuối ngày" hay "hạn chót buổi sáng làm mất cả ngày hôm đó")
    ///    mà chính seam T3.1 đang tiêu thụ (<see cref="ScheduledItem"/>/<c>GenerateScheduleWithIdentity</c>,
    ///    T3.8) không hề mang thông tin đó. Dựng predicate trên một giả định không được mô hình hoá
    ///    ở seam nguồn là bịa dữ liệu, không phải quyết định thiết kế.
    /// 2. Khớp tiền lệ đã có trong chính codebase: <c>DeadlineUrgencyRiskEvaluator</c> (Risk
    ///    Analyzer) tính <c>task.HanChot.Date - clock.Now.Date</c> — thuần phép trừ NGÀY, bỏ qua
    ///    giờ:phút. Dùng cùng phép toán ngày-thuần cho predicate của validator giữ nhất quán với
    ///    định nghĩa "overdue" gần nhất trong hệ thống (xem thêm doc comment của
    ///    <see cref="ConstraintValidator"/> về việc căn chỉnh với DF-1).
    /// 3. Bucket ③ (M3.0 doc) tự nó là "bất khả thi cả hai cách đọc" — quyết định này KHÔNG lật
    ///    ngược nhãn khả thi của fixture đã pin (600 phút cần > cả 360 lẫn 300 phút khả dụng).
    ///    Nghĩa là lựa chọn ở đây có hệ quả CHUNG (mọi fixture biên khác), không phải một cứu vãn
    ///    cho riêng fixture đó — cách đọc còn lại (time-of-day) không được chọn không phải vì nó
    ///    làm hỏng test nào, mà vì lý do (1)/(2) ở trên.
    /// </summary>
    public sealed class UniformDeadlinePolicy : IDeadlinePolicy
    {
        public bool IsViolation(ScheduledItem item) => item.Date.Date > item.HanChot.Date;
    }
}
