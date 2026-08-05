using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Implementation DUY NHẤT của <see cref="IConstraintValidator"/> trong Epic 3 — bộ lọc cứng
    /// theo hạn chót, được ủy quyền cho <see cref="IDeadlinePolicy"/> (mặc định
    /// <see cref="UniformDeadlinePolicy"/>) để quyết định "chunk này có vi phạm không". Không có
    /// capacity/calendar predicate nào được xây trong Epic 3 (không có nhu cầu thật nào trong
    /// codebase hiện tại để lấp chỗ đó — xem <see cref="IConstraintValidator"/> và seam decision
    /// doc, T3.1).
    ///
    /// <b>Căn chỉnh với DF-1 (hai định nghĩa "overdue" cùng tồn tại trong codebase):</b>
    /// <c>IUrgencyRule.JustOverdueRule</c>/<c>OverdueRule</c> (Decision Engine) hỏi "HÔM NAY có
    /// qua hạn chót không" và áp một "vách đá" 3 ngày (quá 3 ngày trễ, điểm ưu tiên rơi về 0 — task
    /// coi như bị "bỏ cuộc" khỏi việc xếp ưu tiên). <c>DeadlineUrgencyRiskEvaluator</c> (Risk
    /// Analyzer) cũng hỏi "HÔM NAY có qua hạn chót không" nhưng KHÔNG có vách đá (
    /// <c>daysLeft &lt; 0 =&gt; 1.0</c>, mãi mãi). Predicate của validator này hỏi một câu KHÁC vẫn
    /// dùng đúng phép toán ngày-thuần đó: không phải "hôm nay có qua hạn chót" mà "CHUNK NÀY (đã
    /// được XẾP LỊCH vào một ngày cụ thể) có nằm sau hạn chót". Cùng phép trừ ngày, khác điểm tham
    /// chiếu — điểm tham chiếu ở đây là <c>ScheduledItem.Date</c> (ngày xếp), không phải "hôm nay"
    /// theo đồng hồ hệ thống.
    ///
    /// Predicate này CĂN THEO định nghĩa KHÔNG VÁCH ĐÁ của <c>DeadlineUrgencyRiskEvaluator</c>, và
    /// từ chối định nghĩa "vách đá 3 ngày" của <c>IUrgencyRule</c> — không phải vì thích hơn, mà vì
    /// vách đá phá vỡ chính TÍNH TOÀN VẸN của một hard filter (D-J), không phải vì lý do
    /// "monotonicity" của magnitude (đã sửa — xem CHÚ Ý bên dưới). Nếu áp vách đá 3-ngày (một chunk
    /// quá 3 ngày trễ không còn được coi là "vi phạm" nữa, đúng như <c>OverdueRule</c> làm với ĐIỂM
    /// ưu tiên — rơi về 0 nghĩa là "bỏ cuộc", không phải "hết hạn"), một chunk trễ 30 ngày có thể
    /// đọc thành KHÔNG VI PHẠM, khiến <c>IsFeasible</c> trả về true cho một lịch biểu trễ hạn thảm
    /// khốc — đây mới là lý do loại vách đá: nó làm biến mất chính predicate feasible/infeasible mà
    /// D-J yêu cầu là tuyệt đối, không phải một chuyện về độ lớn magnitude. Việc HAI component
    /// (Decision Engine, Risk Analyzer) bất đồng với nhau là DF-1 — không phải việc của T3.1 để hoà
    /// giải (đã hoãn, chủ sở hữu: DP-1).
    ///
    /// <b>CHÚ Ý — lỗi lập luận đã sửa (2026-08-05, sau code review):</b> bản gốc của đoạn trên từng
    /// lập luận rằng vách đá bị loại vì nó phá "tính đơn điệu theo mức độ trễ" của
    /// <see cref="ConstraintValidationResult.TotalOverdueMinutes"/>. Sai: <c>TotalOverdueMinutes</c>
    /// cộng dồn <c>item.SoPhut</c> — THỜI LƯỢNG của chunk — chứ không phải MỨC ĐỘ TRỄ của nó (số
    /// ngày trễ). Nó đo "bao nhiêu phút công việc bị xếp sau hạn chót", không đo "xếp sau hạn chót
    /// bao xa". Test <c>Validate_NhieuTaskCungViPham_TongSoPhutTreHan_CongDungTuMoiChunkViPham</c>
    /// (<c>ConstraintValidatorTests.cs</c>) tự nó đã chứng minh KHÔNG có tính đơn điệu đó: task A
    /// trễ 1 ngày góp 70 phút, task B trễ 5 ngày (trễ HƠN) chỉ góp 55 phút — trễ nhiều hơn nhưng
    /// đóng góp magnitude nhỏ hơn. Kết luận (loại vách đá) vẫn ĐÚNG, nhưng vì lý do integrity của
    /// hard filter ở đoạn trên, không phải vì lý do monotonicity đã bị gỡ bỏ ở đây.
    /// </summary>
    public sealed class ConstraintValidator : IConstraintValidator
    {
        private readonly IDeadlinePolicy _deadlinePolicy;

        public ConstraintValidator(IDeadlinePolicy? deadlinePolicy = null)
        {
            _deadlinePolicy = deadlinePolicy ?? new UniformDeadlinePolicy();
        }

        public ConstraintValidationResult Validate(IReadOnlyList<ScheduledItem> items)
        {
            var violations = new List<DeadlineViolation>();
            var violatingTasks = new HashSet<Guid>();
            long totalOverdueMinutes = 0;

            foreach (var item in items)
            {
                if (!_deadlinePolicy.IsViolation(item)) continue;

                violations.Add(new DeadlineViolation(item.MaTask, item.TenTaskGoc, item.Date, item.HanChot, item.SoPhut));
                violatingTasks.Add(item.MaTask);
                totalOverdueMinutes += item.SoPhut;
            }

            int totalOverdueMinutesClamped = totalOverdueMinutes >= int.MaxValue
                ? int.MaxValue
                : (int)totalOverdueMinutes;

            return new ConstraintValidationResult(
                IsFeasible: violatingTasks.Count == 0,
                ViolatingTaskCount: violatingTasks.Count,
                TotalOverdueMinutes: totalOverdueMinutesClamped,
                Violations: violations);
        }
    }
}
