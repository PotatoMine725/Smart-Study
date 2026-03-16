using System;
using Xunit;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;

namespace SmartStudyPlanner.Tests
{
    public class DecisionEngineTests
    {
        // 1. TEST LỖI NULL (Bảo vệ app khỏi Crash)
        [Fact]
        public void CalculatePriority_TaskHoacMonHocNull_TraVe0()
        {
            var monHocMock = new MonHoc("Toán", 3);
            var taskMock = new StudyTask("BT", DateTime.Now, LoaiCongViec.BaiTapVeNha, 3);

            Assert.Equal(0.0, DecisionEngine.CalculatePriority(null, monHocMock));
            Assert.Equal(0.0, DecisionEngine.CalculatePriority(taskMock, null));
            Assert.Equal(0.0, DecisionEngine.CalculatePriority(null, null));
        }

        // 2. TEST LỖI DẤU PHẨY ĐỘNG (Trễ hạn)
        [Fact]
        public void CalculatePriority_TaskQuaHan_TraVe100()
        {
            var monHoc = new MonHoc("Lý", 2);
            var task = new StudyTask("Trễ hạn", DateTime.Now.AddDays(-2), LoaiCongViec.ThiCuoiKy, 5);

            double score = DecisionEngine.CalculatePriority(task, monHoc);
            Assert.Equal(100.0, score); // Phải réo còi 100 điểm
        }

        // 3. TEST HẠN CHÓT TRONG NGÀY HÔM NAY
        [Fact]
        public void CalculatePriority_TaskToiHanHomNay_TraVe95()
        {
            var monHoc = new MonHoc("Hóa", 2);
            var task = new StudyTask("Hôm nay", DateTime.Now.AddHours(5), LoaiCongViec.BaiTapVeNha, 2);

            double score = DecisionEngine.CalculatePriority(task, monHoc);
            Assert.Equal(95.0, score);
        }

        // 4. TEST TẦNG VETO (Nhiệm vụ đã xong)
        [Fact]
        public void CalculatePriority_TaskDaHoanThanh_TraVe0()
        {
            var monHoc = new MonHoc("Sinh", 2);
            var task = new StudyTask("Đã xong", DateTime.Now.AddDays(5), LoaiCongViec.ThiGiuaKy, 3)
            {
                TrangThai = "Hoàn thành"
            };

            double score = DecisionEngine.CalculatePriority(task, monHoc);
            Assert.Equal(0.0, score);
        }

        // 5. TEST VÙNG CHẾT 31-60 NGÀY
        [Fact]
        public void CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0()
        {
            var monHoc = new MonHoc("Toán", 3);
            var task = new StudyTask("Bài tập 45 ngày", DateTime.Now.AddDays(45), LoaiCongViec.BaiTapVeNha, 3);

            double score = DecisionEngine.CalculatePriority(task, monHoc);
            // Ngày xưa vùng này bị âm điểm, giờ phải lớn hơn 0
            Assert.True(score > 0.0);
        }

        // 6. TEST BẢO MẬT CONFIG (Trọng số sai)
        [Fact]
        public void CalculatePriority_WeightConfigBiLoi_TuDongSuaLoi()
        {
            // Cố tình phá hỏng Config (Tổng các hệ số > 1.0)
            DecisionEngine.Config = new WeightConfig
            {
                TimeWeight = 0.9,
                TaskTypeWeight = 0.9,
                CreditWeight = 0.9,
                DifficultyWeight = 0.9
            };

            var monHoc = new MonHoc("Toán", 3);
            var task = new StudyTask("BT", DateTime.Now.AddDays(10), LoaiCongViec.BaiTapVeNha, 3);

            // Chạy AI
            double score = DecisionEngine.CalculatePriority(task, monHoc);

            // Sau khi chạy hàm Calculate, AI phải tự động phát hiện lỗi và reset Config về chuẩn
            Assert.True(DecisionEngine.Config.IsValid());
        }
    }
}