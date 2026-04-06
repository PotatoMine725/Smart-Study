using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.Notifications;
// THÊM 3 THƯ VIỆN NÀY CHO BIỂU ĐỒ
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private HocKy _hocKyHienTai;
        private static bool _daThongBao = false; // Biến static để nhớ là đã báo rồi
        private readonly IStudyRepository _repository = new StudyRepository();

        [ObservableProperty] private string tieuDe;
        [ObservableProperty] private string thongKe;
        [ObservableProperty] private ObservableCollection<TaskDashboardItem> top5Task;

        // --- BIẾN MỚI CHO BIỂU ĐỒ ---
        [ObservableProperty] private ISeries[] bieuDoTrangThai; // Biểu đồ tròn
        [ObservableProperty] private ISeries[] bieuDoMonHoc;    // Biểu đồ cột
        [ObservableProperty] private Axis[] trucXMonHoc;        // Tên các môn học ở đáy biểu đồ cột
        // --- BIẾN MỚI CHO BIỂU ĐỒ SO SÁNH THỜI GIAN ---
        [ObservableProperty] private ISeries[] bieuDoThoiGian;
        [ObservableProperty] private Axis[] trucXThoiGian;

        [ObservableProperty] private string chuoiStreak;

        public Action<HocKy> OnNavigateToMonHoc { get; set; }
        public Action<HocKy, MonHoc> OnNavigateToTask { get; set; }

        public DashboardViewModel(HocKy hocKy)
        {
            _hocKyHienTai = hocKy;
            Top5Task = new ObservableCollection<TaskDashboardItem>();
            LoadDuLieuDashboard();
        }

        public void LoadDuLieuDashboard()
        {
            TieuDe = $"TỔNG QUAN - {_hocKyHienTai.Ten.ToUpper()}";

            int tongSoMon = _hocKyHienTai.DanhSachMonHoc.Count;
            List<TaskDashboardItem> tatCaTask = new List<TaskDashboardItem>();

            // Biến đếm cho biểu đồ tròn
            int countKhanCap = 0, countChuY = 0, countAnToan = 0, countDaXong = 0;

            // Biến mảng cho biểu đồ cột khối lượng bài tập
            List<string> tenCacMon = new List<string>();
            List<int> soTaskCacMon = new List<int>();

            // Biến mảng cho biểu đồ so sánh thời gian (TÍNH NĂNG MỚI)
            List<double> thoiGianKyVong = new List<double>();
            List<double> thoiGianThucTe = new List<double>();

            foreach (var mon in _hocKyHienTai.DanhSachMonHoc)
            {
                // 🔥 SỬA LỖI UX: Cắt ngắn tên môn học nếu dài hơn 15 ký tự
                string tenMonNganGon = mon.TenMonHoc.Length > 15
                                     ? mon.TenMonHoc.Substring(0, 12) + "..."
                                     : mon.TenMonHoc;
                tenCacMon.Add(tenMonNganGon); // Đưa tên đã cắt ngắn vào trục X của biểu đồ

                int taskCuaMon = 0;
                double tongKyVongMon = 0;
                double tongThucTeMon = 0;

                foreach (var task in mon.DanhSachTask)
                {
                    taskCuaMon++;
                    task.DiemUuTien = DecisionEngine.CalculatePriority(task, mon);

                    // 🔥 THU THẬP DỮ LIỆU THỜI GIAN CHO BIỂU ĐỒ MỚI
                    tongKyVongMon += DecisionEngine.CalculateRawSuggestedMinutes(task);
                    tongThucTeMon += task.ThoiGianDaHoc;

                    string mucDo = "An toàn";
                    if (task.TrangThai == "Hoàn thành")
                    {
                        mucDo = "Đã xong";
                        countDaXong++;
                    }
                    else if (task.DiemUuTien >= 80)
                    {
                        mucDo = "Khẩn cấp";
                        countKhanCap++;
                    }
                    else if (task.DiemUuTien >= 50)
                    {
                        mucDo = "Chú ý";
                        countChuY++;
                    }
                    else
                    {
                        countAnToan++;
                    }

                    if (task.TrangThai != "Hoàn thành")
                    {
                        tatCaTask.Add(new TaskDashboardItem
                        {
                            TenMonHoc = mon.TenMonHoc,
                            TenTask = task.TenTask,
                            HanChot = task.HanChot,
                            DiemUuTien = task.DiemUuTien,
                            MucDoCanhBao = mucDo,
                            ThoiGianGoiY = DecisionEngine.SuggestStudyTime(task),
                            TaskGoc = task,
                            MonHocGoc = mon
                        });
                    }
                }

                soTaskCacMon.Add(taskCuaMon);
                thoiGianKyVong.Add(tongKyVongMon);
                thoiGianThucTe.Add(tongThucTeMon);
            }

            ThongKe = $"Bạn đang quản lý {tongSoMon} môn học và có {tatCaTask.Count} deadline chưa hoàn thành.";

            // ĐÃ SỬA THÀNH KHÔNG DẤU
            var top5KhanCap = tatCaTask.OrderByDescending(t => t.DiemUuTien).Take(5).ToList();
            Top5Task.Clear();
            foreach (var item in top5KhanCap) Top5Task.Add(item);

            // --- VẼ BIỂU ĐỒ TRÒN (Tô màu chuẩn hệ thống) ---
            BieuDoTrangThai = new ISeries[]
            {
                new PieSeries<int> { Values = new int[] { countKhanCap }, Name = "Khẩn cấp", Fill = new SolidColorPaint(SKColors.Crimson) },
                new PieSeries<int> { Values = new int[] { countChuY }, Name = "Chú ý", Fill = new SolidColorPaint(SKColors.Orange) },
                new PieSeries<int> { Values = new int[] { countAnToan }, Name = "An toàn", Fill = new SolidColorPaint(SKColors.MediumSeaGreen) },
                new PieSeries<int> { Values = new int[] { countDaXong }, Name = "Đã xong", Fill = new SolidColorPaint(SKColors.Gray) }
            };

            // --- VẼ BIỂU ĐỒ CỘT (Khối lượng bài tập) ---
            BieuDoMonHoc = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = soTaskCacMon.ToArray(),
                    Name = "Số bài tập",
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                }
            };

            TrucXMonHoc = new Axis[]
            {
                new Axis { Labels = tenCacMon.ToArray(), LabelsRotation = 15 } // Xoay chữ nhẹ cho khỏi đè nhau
            };

            // --- 🔥 VẼ BIỂU ĐỒ SO SÁNH THỜI GIAN (TÍNH NĂNG MỚI) ---
            BieuDoThoiGian = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = thoiGianKyVong.ToArray(),
                    Name = "Kỳ vọng (phút)",
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                },
                new ColumnSeries<double>
                {
                    Values = thoiGianThucTe.ToArray(),
                    Name = "Thực tế đã học (phút)",
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen)
                }
            };
            TrucXThoiGian = new Axis[] { new Axis { Labels = tenCacMon.ToArray(), LabelsRotation = 15 } };

            // --- CẬP NHẬT NGỌN LỬA STREAK ---
            var dataStreak = Services.StreakManager.GetCurrentStreak();
            ChuoiStreak = $"🔥 {dataStreak.StreakCount} Ngày";

            // --- HỆ THỐNG WINDOWS TOAST NOTIFICATION ---
            if (!_daThongBao)
            {
                // ĐÃ SỬA TÊN BIẾN
                int soTaskKhanCap = top5KhanCap.Count(t => t.MucDoCanhBao == "Khẩn cấp");

                if (soTaskKhanCap > 0)
                {
                    // Lắp ráp và bắn thông báo ra Desktop
                    new ToastContentBuilder()
                        .AddText("🔥 CẢNH BÁO DEADLINE!")
                        .AddText($"Bạn đang có {soTaskKhanCap} bài tập KHẨN CẤP cần xử lý ngay lập tức!")
                        .AddText("Hãy kiểm tra Smart Study Planner để xem gợi ý lịch học.")
                        .AddAudio(new Uri("ms-winsoundevent:Notification.Default"))
                        .Show();

                    _daThongBao = true;
                }
                else if (tatCaTask.Count > 0)
                {
                    new ToastContentBuilder()
                        .AddText("✅ Mọi thứ đang trong tầm kiểm soát!")
                        .AddText($"Bạn có {tatCaTask.Count} bài tập, nhưng chưa có gì quá hạn.")
                        .Show();

                    _daThongBao = true;
                }
            }
        }

        [RelayCommand]
        private void MoQuanLyMonHoc() => OnNavigateToMonHoc?.Invoke(_hocKyHienTai);

        [RelayCommand]
        private async Task LuuDuLieu() // Đổi từ void sang async Task
        {
            await _repository.LuuHocKyAsync(_hocKyHienTai);
            System.Windows.MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void DiToiTask(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon != null)
            {
                MonHoc monHocCanTim = _hocKyHienTai.DanhSachMonHoc.FirstOrDefault(m => m.TenMonHoc == taskDuocChon.TenMonHoc);
                if (monHocCanTim != null) OnNavigateToTask?.Invoke(_hocKyHienTai, monHocCanTim);
            }
        }

        [RelayCommand]
        private async Task MoFocusMode(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon != null)
            {
                var focusWin = new Views.FocusWindow(taskDuocChon);
                focusWin.ShowDialog(); // App sẽ dừng ở dòng này chờ đến khi cửa sổ đóng

                // KHI CỬA SỔ ĐÓNG, TỰ ĐỘNG LƯU DATABASE NGAY LẬP TỨC
                await _repository.LuuHocKyAsync(_hocKyHienTai);

                // Tải lại bảng xếp hạng
                LoadDuLieuDashboard();
            }
        }
    }
}