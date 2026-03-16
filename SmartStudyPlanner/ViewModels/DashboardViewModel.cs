using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace SmartStudyPlanner.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private HocKy _hocKyHienTai;

        [ObservableProperty] private string tieuDe;
        [ObservableProperty] private string thongKe;
        [ObservableProperty] private ObservableCollection<TaskDashboardItem> top5Task;

        // --- BIẾN MỚI CHO BIỂU ĐỒ ---
        [ObservableProperty] private ISeries[] bieuDoTrangThai; // Biểu đồ tròn
        [ObservableProperty] private ISeries[] bieuDoMonHoc;    // Biểu đồ cột
        [ObservableProperty] private Axis[] trucXMonHoc;        // Tên các môn học ở đáy biểu đồ cột

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

            // Biến mảng cho biểu đồ cột
            List<string> tenCacMon = new List<string>();
            List<int> soTaskCacMon = new List<int>();

            foreach (var mon in _hocKyHienTai.DanhSachMonHoc)
            {
                tenCacMon.Add(mon.TenMonHoc);
                int taskCuaMon = 0;

                foreach (var task in mon.DanhSachTask)
                {
                    taskCuaMon++;
                    task.DiemUuTien = DecisionEngine.CalculatePriority(task, mon);

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
                            // Gợi ý thời gian học dựa trên thuật toán của DecisionEngine
                            ThoiGianGoiY = DecisionEngine.SuggestStudyTime(task)
                        });
                    }
                }
                soTaskCacMon.Add(taskCuaMon);
            }

            ThongKe = $"Bạn đang quản lý {tongSoMon} môn học và có {tatCaTask.Count} deadline chưa hoàn thành.";

            var top5KhẩnCấp = tatCaTask.OrderByDescending(t => t.DiemUuTien).Take(5).ToList();
            Top5Task.Clear();
            foreach (var item in top5KhẩnCấp) Top5Task.Add(item);

            // --- VẼ BIỂU ĐỒ TRÒN (Tô màu chuẩn hệ thống) ---
            BieuDoTrangThai = new ISeries[]
            {
                new PieSeries<int> { Values = new int[] { countKhanCap }, Name = "Khẩn cấp", Fill = new SolidColorPaint(SKColors.Crimson) },
                new PieSeries<int> { Values = new int[] { countChuY }, Name = "Chú ý", Fill = new SolidColorPaint(SKColors.Orange) },
                new PieSeries<int> { Values = new int[] { countAnToan }, Name = "An toàn", Fill = new SolidColorPaint(SKColors.MediumSeaGreen) },
                new PieSeries<int> { Values = new int[] { countDaXong }, Name = "Đã xong", Fill = new SolidColorPaint(SKColors.Gray) }
            };

            // --- VẼ BIỂU ĐỒ CỘT ---
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
        }

        [RelayCommand]
        private void MoQuanLyMonHoc() => OnNavigateToMonHoc?.Invoke(_hocKyHienTai);

        [RelayCommand]
        private void LuuDuLieu()
        {
            DataManager.LuuHocKy(_hocKyHienTai);
            MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}