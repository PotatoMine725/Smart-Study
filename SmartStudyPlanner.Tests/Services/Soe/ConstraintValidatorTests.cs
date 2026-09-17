using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Services.Soe;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.1 (Epic 3, Card D) — per-seam unit tests cho <see cref="IConstraintValidator"/> /
    /// <see cref="ConstraintValidator"/> / <see cref="IDeadlinePolicy"/> /
    /// <see cref="UniformDeadlinePolicy"/>. Test trực tiếp trên <see cref="ScheduledItem"/> hand-built
    /// (không qua allocator thật — T3.1 KHÔNG wire vào <c>WorkloadServiceImpl</c>, đó là việc của
    /// T3.3), theo đúng "Scope" của Card D.
    ///
    /// Nhóm test bám sát các câu hỏi bắt buộc của task card:
    /// 1. Lịch khả thi rõ ràng.
    /// 2. Lịch không khả thi rõ ràng (vi phạm hạn chót).
    /// 3. Case biên đúng granularity đã chọn (hạn chót 09:00 ngày 5 — bucket ③, M3.0 doc).
    /// 4. Task bị cắt nhỏ (nhiều <see cref="ScheduledItem"/> chung một <c>MaTask</c>) — pin rõ vì
    ///    sao <c>ViolatingTaskCount</c> KHÔNG bằng <c>Violations.Count</c>.
    /// 5. Magnitude (tổng số phút trễ hạn) đúng, không chỉ đúng bool khả thi/không khả thi.
    /// </summary>
    public class ConstraintValidatorTests
    {
        private static readonly DateTime Day0 = new DateTime(2026, 8, 5); // "today" mốc cho corpus/fixture

        private static ScheduledItem Item(
            Guid maTask, DateTime hanChot, string tenTaskGoc, string tenHienThi, DateTime date, int soPhut)
            => new ScheduledItem(maTask, hanChot, tenTaskGoc, tenHienThi, "Toán", date, soPhut);

        private static IConstraintValidator Sut(IDeadlinePolicy? policy = null) => new ConstraintValidator(policy);

        // ---- 1. Lịch khả thi rõ ràng ----

        [Fact]
        public void Validate_MoiChunkNamTruocHoacDungNgayHanChot_TraVeKhaThi()
        {
            var task1 = Guid.NewGuid();
            var task2 = Guid.NewGuid();
            var hanChot1 = Day0.AddDays(5); // hạn chót thuần ngày, không time-of-day
            var hanChot2 = Day0.AddDays(3);

            var items = new List<ScheduledItem>
            {
                Item(task1, hanChot1, "Bài A", "Bài A", Day0.AddDays(0), 60),
                Item(task1, hanChot1, "Bài A", "Bài A (Phần 2)", Day0.AddDays(5), 30), // đúng ngày hạn chót -> OK
                Item(task2, hanChot2, "Bài B", "Bài B", Day0.AddDays(1), 45),
                Item(task2, hanChot2, "Bài B", "Bài B (Phần 2)", Day0.AddDays(3), 20), // đúng ngày hạn chót -> OK
            };

            var result = Sut().Validate(items);

            Assert.True(result.IsFeasible);
            Assert.Equal(0, result.ViolatingTaskCount);
            Assert.Equal(0, result.TotalOverdueMinutes);
            Assert.Empty(result.Violations);
        }

        // ---- 2. Lịch không khả thi rõ ràng ----

        [Fact]
        public void Validate_MotChunkXepSauNgayHanChot_TraVeKhongKhaThi_VoiSoLieuDung()
        {
            var task = Guid.NewGuid();
            var hanChot = Day0.AddDays(5);

            var items = new List<ScheduledItem>
            {
                Item(task, hanChot, "Đồ án", "Đồ án", Day0.AddDays(7), 90), // 2 ngày sau hạn chót
            };

            var result = Sut().Validate(items);

            Assert.False(result.IsFeasible);
            Assert.Equal(1, result.ViolatingTaskCount);
            Assert.Equal(90, result.TotalOverdueMinutes);
            var v = Assert.Single(result.Violations);
            Assert.Equal(task, v.MaTask);
            Assert.Equal("Đồ án", v.TenTaskGoc);
            Assert.Equal(Day0.AddDays(7), v.Date);
            Assert.Equal(hanChot, v.HanChot);
            Assert.Equal(90, v.SoPhut);
        }

        // ---- 3. Case biên đúng granularity (bucket ③: hạn chót 09:00 ngày 5) ----

        [Fact]
        public void Validate_HanChot09hNgay5_ChunkDatDungNgay5_KhongLaViPham()
        {
            // Đây chính xác là bucket ③ của M3.0 doc: hạn chót có time-of-day 09:00, nhưng chunk
            // được đặt vào ĐÚNG NGÀY 5 (ScheduledItem.Date đã là .Date, không time-of-day). Theo
            // quyết định date-only, đây KHÔNG phải vi phạm -- đọc "360 phút khả dụng", không phải
            // đọc "300 phút" (ngày 5 bị coi là mất vì hạn chót buổi sáng).
            var task = Guid.NewGuid();
            var hanChot = Day0.AddDays(5).AddHours(9); // 09:00 ngày 5

            var items = new List<ScheduledItem>
            {
                Item(task, hanChot, "Rất dài", "Rất dài (Phần 6)", Day0.AddDays(5), 60),
            };

            var result = Sut().Validate(items);

            Assert.True(result.IsFeasible);
            Assert.Equal(0, result.ViolatingTaskCount);
            Assert.Equal(0, result.TotalOverdueMinutes);
        }

        [Fact]
        public void Validate_HanChot09hNgay5_ChunkDatVaoNgay6_LaViPham()
        {
            // Phản chứng cho case ngay phía trên: cùng hạn chót 09:00 ngày 5, nhưng chunk rơi vào
            // ngày 6 (SAU ngày của hạn chót) -- đây LÀ vi phạm bất kể giờ:phút, đúng bản chất
            // date-only của predicate.
            var task = Guid.NewGuid();
            var hanChot = Day0.AddDays(5).AddHours(9);

            var items = new List<ScheduledItem>
            {
                Item(task, hanChot, "Rất dài", "Rất dài (Phần 7)", Day0.AddDays(6), 60),
            };

            var result = Sut().Validate(items);

            Assert.False(result.IsFeasible);
            Assert.Equal(1, result.ViolatingTaskCount);
            Assert.Equal(60, result.TotalOverdueMinutes);
        }

        // ---- 4. Task bị cắt nhỏ -- ViolatingTaskCount đếm theo task, Violations đếm theo chunk ----

        [Fact]
        public void Validate_TaskBiCatNhoNhieuChunkTreHan_ViolatingTaskCountDemMotTask_ViolationsDemMoiChunk()
        {
            var task = Guid.NewGuid();
            var hanChot = Day0.AddDays(5);

            var items = new List<ScheduledItem>
            {
                Item(task, hanChot, "Dài", "Dài (Phần 1)", Day0.AddDays(2), 60),  // đúng hạn -> không vi phạm
                Item(task, hanChot, "Dài", "Dài (Phần 2)", Day0.AddDays(6), 40),  // trễ 1 ngày -> vi phạm #1
                Item(task, hanChot, "Dài", "Dài (Phần 3)", Day0.AddDays(7), 50),  // trễ 2 ngày -> vi phạm #2
            };

            var result = Sut().Validate(items);

            Assert.False(result.IsFeasible);
            // Cốt lõi của test này: MỘT task, HAI chunk trễ hạn -> ViolatingTaskCount == 1 (đếm
            // theo task, bất biến trước re-chunking), nhưng Violations.Count == 2 (đếm theo chunk,
            // cần cho magnitude/explainability). Hai con số này CỐ Ý không bằng nhau.
            Assert.Equal(1, result.ViolatingTaskCount);
            Assert.Equal(2, result.Violations.Count);
            Assert.Equal(90, result.TotalOverdueMinutes); // 40 + 50, CẢ HAI chunk trễ, không phải chunk trễ nhất
            Assert.All(result.Violations, v => Assert.Equal(task, v.MaTask));
        }

        // ---- 5. Magnitude đúng khi nhiều task cùng vi phạm ----

        [Fact]
        public void Validate_NhieuTaskCungViPham_TongSoPhutTreHan_CongDungTuMoiChunkViPham()
        {
            var taskA = Guid.NewGuid();
            var taskB = Guid.NewGuid();
            var hanChotA = Day0.AddDays(2);
            var hanChotB = Day0.AddDays(4);

            var items = new List<ScheduledItem>
            {
                Item(taskA, hanChotA, "A", "A", Day0.AddDays(3), 70),   // A trễ 1 ngày, 70 phút
                Item(taskB, hanChotB, "B", "B", Day0.AddDays(1), 25),   // B đúng hạn, không vi phạm
                Item(taskB, hanChotB, "B", "B (Phần 2)", Day0.AddDays(9), 55), // B trễ 5 ngày, 55 phút
            };

            var result = Sut().Validate(items);

            Assert.False(result.IsFeasible);
            Assert.Equal(2, result.ViolatingTaskCount); // A và B đều góp mặt, mỗi task 1 lần
            Assert.Equal(2, result.Violations.Count);   // đúng 2 chunk vi phạm (không tính chunk B đúng hạn)
            Assert.Equal(125, result.TotalOverdueMinutes); // 70 + 55
        }

        // ---- Unit test riêng cho UniformDeadlinePolicy (độc lập với ConstraintValidator) ----

        [Fact]
        public void UniformDeadlinePolicy_ChunkDungNgayHanChot_KhongViPham()
        {
            var policy = new UniformDeadlinePolicy();
            var hanChot = Day0.AddHours(9); // có time-of-day, phải bị bỏ qua
            var item = Item(Guid.NewGuid(), hanChot, "X", "X", Day0, 30);

            Assert.False(policy.IsViolation(item));
        }

        [Fact]
        public void UniformDeadlinePolicy_ChunkSauNgayHanChot_LaViPham()
        {
            var policy = new UniformDeadlinePolicy();
            var hanChot = Day0;
            var item = Item(Guid.NewGuid(), hanChot, "X", "X", Day0.AddDays(1), 30);

            Assert.True(policy.IsViolation(item));
        }

        [Fact]
        public void UniformDeadlinePolicy_ChunkTruocNgayHanChot_KhongViPham()
        {
            var policy = new UniformDeadlinePolicy();
            var hanChot = Day0.AddDays(5);
            var item = Item(Guid.NewGuid(), hanChot, "X", "X", Day0.AddDays(4), 30);

            Assert.False(policy.IsViolation(item));
        }

        // ---- Extension point thực sự pluggable qua constructor (không phải một tuyên bố suông) ----

        [Fact]
        public void ConstraintValidator_NhanIDeadlinePolicyTuyChinhQuaConstructor_DungPolicyDoThayViMacDinh()
        {
            // Fake policy coi MỌI chunk là vi phạm, kể cả chunk rõ ràng nằm trước hạn chót --
            // chứng minh ConstraintValidator thực sự ủy quyền quyết định cho IDeadlinePolicy được
            // tiêm vào, không hardcode UniformDeadlinePolicy bên trong.
            var alwaysViolates = new AlwaysViolatesPolicy();
            var task = Guid.NewGuid();
            var items = new List<ScheduledItem>
            {
                Item(task, Day0.AddDays(30), "An toàn", "An toàn", Day0, 15), // rất xa trước hạn chót
            };

            var result = new ConstraintValidator(alwaysViolates).Validate(items);

            Assert.False(result.IsFeasible);
            Assert.Equal(1, result.ViolatingTaskCount);
            Assert.Equal(15, result.TotalOverdueMinutes);
        }

        [Fact]
        public void Validate_DanhSachRong_TraVeKhaThi_KhongLoi()
        {
            var result = Sut().Validate(Array.Empty<ScheduledItem>());

            Assert.True(result.IsFeasible);
            Assert.Equal(0, result.ViolatingTaskCount);
            Assert.Equal(0, result.TotalOverdueMinutes);
            Assert.Empty(result.Violations);
        }

        private sealed class AlwaysViolatesPolicy : IDeadlinePolicy
        {
            public bool IsViolation(ScheduledItem item) => true;
        }
    }
}
