using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Services.Soe;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.9 -- per-seam unit tests cho <see cref="LoadRebalanceStage"/>, DUY NHẤT stage của N=1
    /// stage list ratified 2026-08-06
    /// (<c>docs/plans/2026-08-06-t3.9-optimize-stage-list-design.md</c> §2 Candidate 1, §8).
    /// Test trực tiếp trên <see cref="ScheduledItem"/> hand-built, KHÔNG qua allocator thật hay
    /// <see cref="IScheduleOptimizer"/> -- mirror namespace 1:1, cùng phong cách
    /// <c>Sut</c>-field + hand-built-fixture như <c>ConstraintValidatorTests</c>/
    /// <c>ObjectiveEvaluatorTests</c>.
    ///
    /// Hai case bắt buộc theo DoR note §5: (a) item quá khổ trên ngày bận nhất phải bị BỎ QUA, không
    /// dời (eligibility-filter defect đã sửa) -- <see cref="Apply_OversizedItemOnBusiestDay_IsSkipped_SmallerEligibleItemMovesInstead"/>.
    /// (b) fixture 3+ ngày dùng nơi một đích ưu tiên hạn chót (naive) sẽ làm rộng thêm chênh lệch --
    /// pin đích LUÔN LUÔN là D_min toàn cục, không điều kiện --
    /// <see cref="Apply_DestinationIsAlwaysGlobalMinimum_IgnoresDeadlineCompatibility"/>.
    /// </summary>
    public sealed class LoadRebalanceStageTests
    {
        private static readonly DateTime Day0 = new(2026, 8, 5);

        private static ScheduledItem Item(
            Guid maTask, DateTime hanChot, string tenTaskGoc, DateTime date, int soPhut)
            => new(maTask, hanChot, tenTaskGoc, tenTaskGoc, "Toán", date, soPhut);

        private static IOptimizerStage Sut() => new LoadRebalanceStage();

        // ---- <=1 ngày dùng -> vacuously no-op ----

        [Fact]
        public void Apply_SingleUsedDay_IsNoOp()
        {
            var t1 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                Item(t1, Day0.AddDays(30), "A", Day0, 60),
                Item(t1, Day0.AddDays(30), "A", Day0, 40),
            };

            var result = Sut().Apply(schedule);

            Assert.Equal(schedule, result);
        }

        [Fact]
        public void Apply_EmptySchedule_IsNoOp()
        {
            var result = Sut().Apply(Array.Empty<ScheduledItem>());

            Assert.Empty(result);
        }

        // ---- (a) DoR §5: item quá khổ trên D_max phải bị bỏ qua, không dời ----

        [Fact]
        public void Apply_OversizedItemOnBusiestDay_IsSkipped_SmallerEligibleItemMovesInstead()
        {
            var big = Guid.NewGuid();
            var small = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60); // xa, không ảnh hưởng move

            // D_max = Day0: 300 phút (item lớn 250 + item nhỏ 50). D_min = Day1: 60 phút.
            // gap = 300 - 60 = 240. Item 250 KHÔNG eligible (250 > 240) dù được rank trước
            // (SoPhut desc) -- phải bị bỏ qua. Item 50 mới là item thực sự bị dời.
            var schedule = new List<ScheduledItem>
            {
                Item(big, farHanChot, "Quá khổ", Day0, 250),
                Item(small, farHanChot, "Nhỏ", Day0, 50),
                Item(big, farHanChot, "Ngày nhẹ", Day0.AddDays(1), 60),
            };

            var result = Sut().Apply(schedule);

            // Item quá khổ vẫn còn nguyên trên Day0.
            Assert.Contains(result, i => i.MaTask == big && i.TenTaskGoc == "Quá khổ" && i.Date == Day0 && i.SoPhut == 250);
            // Item nhỏ đã dời sang Day1 (ngày nhẹ nhất), nguyên vẹn 50 phút.
            Assert.Contains(result, i => i.MaTask == small && i.Date == Day0.AddDays(1) && i.SoPhut == 50);
            // Tổng phút Day0 giảm đúng 50 (chỉ còn item quá khổ + phần "ngày nhẹ" gốc không nằm ở Day0).
            Assert.Equal(250, result.Where(i => i.Date == Day0).Sum(i => i.SoPhut));
            Assert.Equal(110, result.Where(i => i.Date == Day0.AddDays(1)).Sum(i => i.SoPhut)); // 60 + 50
        }

        [Fact]
        public void Apply_NoItemOnBusiestDayIsEligible_IsNoOp()
        {
            var t1 = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);

            // D_max = Day0: một item duy nhất 250 phút. D_min = Day1: 60 phút. gap = 190.
            // 250 > 190 -> không item nào eligible -> no-op tuyệt đối (khác reference nhưng equal).
            var schedule = new List<ScheduledItem>
            {
                Item(t1, farHanChot, "Quá khổ", Day0, 250),
                Item(t1, farHanChot, "Nhẹ", Day0.AddDays(1), 60),
            };

            var result = Sut().Apply(schedule);

            Assert.Equal(schedule, result);
        }

        // ---- (b) DoR §5: đích LUÔN LUÔN là D_min toàn cục, không ưu tiên hạn chót ----

        [Fact]
        public void Apply_DestinationIsAlwaysGlobalMinimum_IgnoresDeadlineCompatibility()
        {
            var moved = Guid.NewGuid();
            var filler = Guid.NewGuid();
            var otherTask = Guid.NewGuid();

            // Counter-example đúng theo DoR note §2 "Correction": maxTotal=300, minTotal=60,
            // một đích "tương thích hạn chót" (destTotal=280) tồn tại nhưng KHÔNG được chọn --
            // đích thật là D_min toàn cục (60), dù ngày đó rơi SAU hạn chót của item bị dời.
            var hanChotMoved = Day0.AddDays(2); // hạn chót của item bị dời

            var dMaxDate = Day0.AddDays(1);      // D_max = 300 phút (200 + 100), TRƯỚC hạn chót
            var dCompatDate = Day0;              // "đích tương thích hạn chót" = 280 phút, TRƯỚC hạn chót
            var dGlobalMinDate = Day0.AddDays(5); // D_min toàn cục = 60 phút, SAU hạn chót -- vẫn phải là đích

            var schedule = new List<ScheduledItem>
            {
                Item(moved, hanChotMoved, "Bị dời", dMaxDate, 200),
                Item(filler, hanChotMoved.AddDays(60), "Filler D_max", dMaxDate, 100),
                Item(otherTask, hanChotMoved.AddDays(60), "Đích tương thích", dCompatDate, 280),
                Item(otherTask, hanChotMoved.AddDays(60), "D_min toàn cục", dGlobalMinDate, 60),
            };

            var result = Sut().Apply(schedule);

            // Item "Bị dời" phải nằm ở D_min TOÀN CỤC (dGlobalMinDate, sau hạn chót của chính nó),
            // KHÔNG phải ở "đích tương thích hạn chót" (dCompatDate).
            var relocated = Assert.Single(result, i => i.MaTask == moved);
            Assert.Equal(dGlobalMinDate, relocated.Date);
            Assert.Equal(200, relocated.SoPhut); // nguyên vẹn, không cắt (D1)
            Assert.NotEqual(dCompatDate, relocated.Date);
        }

        // ---- Whole-item move, không cắt (D1) ----

        [Fact]
        public void Apply_MovesWholeItem_PreservesSoPhutAndItemCount()
        {
            var t1 = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);

            // D_max = Day0 (150+50=200), D_min = Day1 (20). gap = 180 -> item 150 (rank đầu theo
            // SoPhut desc) eligible, phải là item được dời.
            var schedule = new List<ScheduledItem>
            {
                Item(t1, farHanChot, "A", Day0, 150),
                Item(t1, farHanChot, "B", Day0, 50),
                Item(t1, farHanChot, "C", Day0.AddDays(1), 20),
            };

            var result = Sut().Apply(schedule);

            Assert.Equal(schedule.Count, result.Count); // không tạo/xoá item nào
            Assert.Equal(220, result.Sum(i => i.SoPhut)); // tổng phút bất biến qua move
        }

        // ---- Case tích cực đơn giản: item eligible dời đúng sang D_min ----

        [Fact]
        public void Apply_EligibleItem_MovesFromBusiestToLeastLoadedDay()
        {
            var t1a = Guid.NewGuid();
            var t1b = Guid.NewGuid();
            var t1c = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);

            // D_max = Day0 (150+50=200), D_min = Day1 (20). gap = 180 -> item 150 eligible.
            // Sau move: Day0 còn 50, Day1 = 20+150 = 170.
            var schedule = new List<ScheduledItem>
            {
                Item(t1a, farHanChot, "Nặng1", Day0, 150),
                Item(t1b, farHanChot, "Nặng2", Day0, 50),
                Item(t1c, farHanChot, "Nhẹ", Day0.AddDays(1), 20),
            };

            var result = Sut().Apply(schedule);

            Assert.Equal(50, result.Where(i => i.Date == Day0).Sum(i => i.SoPhut));
            Assert.Equal(170, result.Where(i => i.Date == Day0.AddDays(1)).Sum(i => i.SoPhut));
        }

        // ---- Tie-break xác định (determinism) ----

        [Fact]
        public void Apply_TiedBusiestDays_BreaksByEarliestDate()
        {
            var t1 = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);

            // Day0 và Day1 hoà nhau ở 150 phút (đều "nặng nhất") -- tie-break (Date asc) phải
            // chọn Day0 làm D_max. Day2 = 20, D_min rõ ràng, không hoà. gap = 150 - 20 = 130.
            // Nếu stage chọn NHẦM Day1 làm D_max, item bị dời (và tổng còn lại của từng ngày) sẽ
            // khác hẳn -- test này phân biệt được cả hai khả năng.
            var schedule = new List<ScheduledItem>
            {
                Item(t1, farHanChot, "A_Day0", Day0, 100),
                Item(t1, farHanChot, "B_Day0", Day0, 50),
                Item(t1, farHanChot, "C_Day1", Day0.AddDays(1), 90),
                Item(t1, farHanChot, "D_Day1", Day0.AddDays(1), 60),
                Item(t1, farHanChot, "E_Day2", Day0.AddDays(2), 20),
            };

            var result = Sut().Apply(schedule);

            // Đúng: D_max = Day0 (tie-break Date asc), item 100 (rank đầu, eligible <=130) dời đi.
            Assert.Equal(50, result.Where(i => i.Date == Day0).Sum(i => i.SoPhut));
            // Day1 hoàn toàn không bị đụng tới (nó KHÔNG phải D_max được chọn).
            Assert.Equal(150, result.Where(i => i.Date == Day0.AddDays(1)).Sum(i => i.SoPhut));
            Assert.Equal(120, result.Where(i => i.Date == Day0.AddDays(2)).Sum(i => i.SoPhut)); // 20 + 100
        }

        [Fact]
        public void Apply_TiedLeastLoadedDays_BreaksByEarliestDate()
        {
            var t1 = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);

            // D_max = Day0 (150, không hoà). Day1 và Day2 hoà nhau ở 30 phút -- tie-break (Date
            // asc) phải chọn Day1 làm D_min, KHÔNG phải Day2. gap = 150 - 30 = 120.
            var schedule = new List<ScheduledItem>
            {
                Item(t1, farHanChot, "A_Day0", Day0, 100),
                Item(t1, farHanChot, "B_Day0", Day0, 50),
                Item(t1, farHanChot, "C_Day1", Day0.AddDays(1), 30),
                Item(t1, farHanChot, "D_Day2", Day0.AddDays(2), 30),
            };

            var result = Sut().Apply(schedule);

            Assert.Equal(50, result.Where(i => i.Date == Day0).Sum(i => i.SoPhut));
            Assert.Equal(130, result.Where(i => i.Date == Day0.AddDays(1)).Sum(i => i.SoPhut)); // 30 + 100 -> đích ĐÚNG
            Assert.Equal(30, result.Where(i => i.Date == Day0.AddDays(2)).Sum(i => i.SoPhut));  // Day2 không bị đụng
        }

        [Fact]
        public void Apply_TiedItemSizesOnBusiestDay_BreaksByMaTaskAscending()
        {
            var taskLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var taskHigh = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var farHanChot = Day0.AddDays(60);

            // Hai item cùng 90 phút trên D_max -- rank theo MaTask asc phải chọn taskLow trước.
            var schedule = new List<ScheduledItem>
            {
                Item(taskHigh, farHanChot, "Cao", Day0, 90),
                Item(taskLow, farHanChot, "Thấp", Day0, 90),
                Item(taskLow, farHanChot, "Nhẹ", Day0.AddDays(1), 10),
            };

            var result = Sut().Apply(schedule);

            // gap = 180 - 10 = 170, cả hai đều eligible (90 <= 170) -> taskLow (MaTask nhỏ hơn)
            // phải là item được dời, dù cả hai đều rank ngang nhau theo SoPhut.
            var relocated = Assert.Single(result, i => i.Date == Day0.AddDays(1) && i.MaTask == taskLow && i.TenTaskGoc == "Thấp");
            Assert.Equal(90, relocated.SoPhut);
            // taskHigh vẫn còn nguyên trên D_max.
            Assert.Contains(result, i => i.MaTask == taskHigh && i.Date == Day0);
        }
    }
}
