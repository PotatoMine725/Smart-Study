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
    /// vách đá KHÔNG DÙNG ĐƯỢC ở đây: D-H's khóa so sánh thứ hai (tổng số phút trễ hạn) đòi hỏi
    /// magnitude phải ĐƠN ĐIỆU TĂNG theo mức độ trễ — một task trễ 10 ngày phải nặng hơn một task
    /// trễ 1 ngày. Nếu áp vách đá 3-ngày (biến "vi phạm" thành 0 sau khi quá 3 ngày, đúng như
    /// <c>OverdueRule</c> làm với ĐIỂM ưu tiên), một chunk trễ 10 ngày và một chunk trễ 30 ngày sẽ
    /// không thể phân biệt được nữa (cả hai đều "quá vách đá") — vi phạm thẳng chính bất biến D-H
    /// mà validator này tồn tại để phục vụ. Đây là một khẳng định về TÍNH TƯƠNG THÍCH cấu trúc, không
    /// phải một sở thích: định nghĩa vách đá không tương thích với một magnitude đơn điệu; định
    /// nghĩa không-vách-đá thì có. Việc HAI component (Decision Engine, Risk Analyzer) bất đồng với
    /// nhau là DF-1 — không phải việc của T3.1 để hoà giải (đã hoãn, chủ sở hữu: DP-1).
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
