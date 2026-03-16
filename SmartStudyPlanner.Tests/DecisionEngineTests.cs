using System;
using Xunit;
using SmartStudyPlanner;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;

namespace SmartStudyPlanner.Tests
{
    public class DecisionEngineTests
    {
        [Fact]
        public void CalculatePriority_TaskTrongVung31Den60Ngay_KhongBih0DiemThoiGian()
        {
            // 1. ARRANGE (Chuẩn bị dữ liệu giả)
            var monHocMock = new MonHoc("Toán Test", 3);

            // Tạo một Task có hạn chót là 45 ngày sau (nằm giữa vùng 31-60)
            var taskMock = new StudyTask("Bài tập 45 ngày", DateTime.Now.AddDays(45), LoaiCongViec.BaiTapVeNha, 3);

            // 2. ACT (Kích hoạt bộ máy chấm điểm)
            double priorityScore = DecisionEngine.CalculatePriority(taskMock, monHocMock);

            // 3. ASSERT (Kiểm chứng kết quả)
            // Ở phiên bản cũ bị lỗi, điểm ưu tiên ở ngày 45 sẽ bị ép về rất thấp vì điểm thời gian bị âm.
            // Ở phiên bản mới, điểm này phải chắc chắn lớn hơn 0.
            Assert.True(priorityScore > 0, "Lỗi: Điểm ưu tiên bị ép về 0 ở ngày 45!");
        }

        [Fact]
        public void CalculatePriority_TaskQuaHan1Ngay_TraVe100DiemBaoDong()
        {
            // 1. ARRANGE
            var monHocMock = new MonHoc("Lý Test", 2);
            var taskMock = new StudyTask("Trễ hạn", DateTime.Now.AddDays(-1), LoaiCongViec.ThiCuoiKy, 5);

            // 2. ACT
            double priorityScore = DecisionEngine.CalculatePriority(taskMock, monHocMock);

            // 3. ASSERT
            // Thuật toán Dictatorship quy định: Trễ 1-3 ngày phải réo còi 100 điểm.
            Assert.Equal(100.0, priorityScore);
        }
    }
}