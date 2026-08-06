using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// T3.9, Candidate 1 (N=1, DUY NHẤT stage của stage list, ratified 2026-08-06 —
    /// <c>docs/plans/2026-08-06-t3.9-optimize-stage-list-design.md</c> §2/§8, KHÔNG redesign).
    /// Nhắm số hạng <c>LoadBalance = 1/(1+CV)</c> (<see cref="ObjectiveEvaluator"/>, dòng 57-74):
    /// dời NGUYÊN một chunk từ ngày tải nặng nhất (D_max) sang ngày tải nhẹ nhất trong số các
    /// ngày ĐANG DÙNG (D_min), có eligibility filter để KHÔNG BAO GIỜ làm rộng thêm chênh lệch
    /// max-min.
    ///
    /// <b>Rule (đúng nguyên văn §2 Candidate 1, đã sửa hai lần qua review — xem §7 D0 của DoR
    /// note):</b>
    /// <list type="number">
    /// <item>Xếp hạng các ngày ĐANG DÙNG theo (TotalMinutes desc, Date asc) -&gt; D_max là ngày đầu
    /// tiên, <c>maxTotal</c> là tổng của nó.</item>
    /// <item>Xếp hạng các ngày ĐANG DÙNG CÒN LẠI (khác D_max) theo (TotalMinutes asc, Date asc)
    /// -&gt; D_min LUÔN LUÔN là đích, KHÔNG ĐIỀU KIỆN — không có bước ưu tiên theo hạn chót ở đây
    /// (một bản nháp trước từng có, và nó phá vỡ chính bằng chứng "không làm rộng thêm chênh
    /// lệch" mà stage này tồn tại để cung cấp — xem DoR note §2 "Correction").</item>
    /// <item>Trên D_max, xếp hạng item theo (SoPhut desc, MaTask asc), lấy item ĐẦU TIÊN X thoả
    /// <c>X.SoPhut &lt;= maxTotal - minTotal</c> (ngưỡng eligibility tính trên maxTotal/minTotal
    /// CỐ ĐỊNH trước khi scan — một bộ lọc, không phải một simulation kết quả từng candidate).
    /// Nếu không item nào trên D_max thoả, stage là no-op cho pass này.</item>
    /// <item>Dời X sang D_min TOÀN BỘ (không cắt nhỏ — D1, DoR note §3). Nếu chỉ có &lt;=1 ngày
    /// đang dùng, stage là no-op.</item>
    /// </list>
    ///
    /// <b>Vì sao ngưỡng eligibility, không phải "item lớn nhất":</b> nếu <c>X.SoPhut</c> vượt quá
    /// khoảng cách hiện tại giữa ngày nặng nhất và nhẹ nhất, dời nó sẽ OVERSHOOT — D_min trở
    /// thành ngày nặng nhất mới, D_max trở nên nhẹ hơn D_min từng là, và chênh lệch có thể RỘNG
    /// HƠN trước, không hẹp hơn. Chặn candidate ở đúng kích thước khoảng cách là điều duy nhất
    /// đảm bảo move không bao giờ làm rộng thêm chênh lệch max-min.
    ///
    /// <b>Feasibility:</b> vì không có ưu tiên hạn chót trong việc chọn đích, việc dời X sang
    /// D_min có thể rơi vào bất kỳ phía nào của <c>X.HanChot</c> — stage này hoàn toàn dựa vào
    /// admissibility gate của G2-2 (<c>IsAdmissible</c>, không đổi ở đây) để bảo vệ tính khả thi;
    /// cổng đó đủ bảo vệ trong MỌI trường hợp (xem DoR note §2 "Feasibility").
    ///
    /// KHÔNG đọc <see cref="SoeWeights"/> (D-G/D-J — chỉ checkpoint mechanism ngoài stage mới
    /// weight-aware). KHÔNG đọc <see cref="ScheduledItem.HanChot"/> — bước ưu tiên hạn chót từng
    /// cần nó đã bị loại theo correction ở trên.
    /// </summary>
    public sealed class LoadRebalanceStage : IOptimizerStage
    {
        public IReadOnlyList<ScheduledItem> Apply(IReadOnlyList<ScheduledItem> schedule)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            if (schedule.Count == 0) return schedule;

            var perDay = schedule
                .GroupBy(i => i.Date)
                .Select(g => (Date: g.Key, Total: g.Sum(i => i.SoPhut)))
                .ToList();

            if (perDay.Count <= 1) return schedule; // <=1 ngày dùng -> vacuously no-op (§2)

            var dMax = perDay
                .OrderByDescending(d => d.Total)
                .ThenBy(d => d.Date)
                .First();

            var dMin = perDay
                .Where(d => d.Date != dMax.Date)
                .OrderBy(d => d.Total)
                .ThenBy(d => d.Date)
                .First();

            int gap = dMax.Total - dMin.Total;

            // Rank các item trên D_max theo (SoPhut desc, MaTask asc), scan tuyến tính lấy item
            // ĐẦU TIÊN thoả ngưỡng -- rank-and-take-first-eligible, không phải search kết quả.
            var maxDayIndicesRanked = Enumerable.Range(0, schedule.Count)
                .Where(idx => schedule[idx].Date == dMax.Date)
                .OrderByDescending(idx => schedule[idx].SoPhut)
                .ThenBy(idx => schedule[idx].MaTask)
                .ToList();

            int candidateIndex = -1;
            foreach (var idx in maxDayIndicesRanked)
            {
                if (schedule[idx].SoPhut <= gap)
                {
                    candidateIndex = idx;
                    break;
                }
            }

            if (candidateIndex == -1) return schedule; // không item nào eligible -> no-op

            var result = schedule.ToList();
            result[candidateIndex] = result[candidateIndex] with { Date = dMin.Date };
            return result;
        }
    }
}
