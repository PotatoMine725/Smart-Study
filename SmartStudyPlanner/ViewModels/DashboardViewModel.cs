using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        // 1. DỮ LIỆU HIỂN THỊ (Sẽ tự động sinh ra TieuDe, ThongKe, Top5Task)
        [ObservableProperty]
        private string tieuDe;

        [ObservableProperty]
        private string thongKe;

        [ObservableProperty]
        private ObservableCollection<TaskDashboardItem> top5Task;

        // 2. LOA THÔNG BÁO (Dùng để bảo View chuyển trang)
        public Action<HocKy> OnNavigateToMonHoc { get; set; }
        public Action<HocKy, MonHoc> OnNavigateToTask { get; set; }

        public DashboardViewModel(HocKy hocKy)
        {
            _hocKyHienTai = hocKy;
            Top5Task = new ObservableCollection<TaskDashboardItem>();
            LoadDuLieuDashboard();
        }

        // Hàm tính toán và cập nhật lại bảng xếp hạng
        public void LoadDuLieuDashboard()
        {
            TieuDe = $"TỔNG QUAN - {_hocKyHienTai.Ten.ToUpper()}";

            int tongSoMon = _hocKyHienTai.DanhSachMonHoc.Count;
            List<TaskDashboardItem> tatCaTask = new List<TaskDashboardItem>();

            foreach (var mon in _hocKyHienTai.DanhSachMonHoc)
            {
                foreach (var task in mon.DanhSachTask)
                {
                    task.DiemUuTien = DecisionEngine.CalculatePriority(task, mon);

                    string mucDo = "An toàn";
                    if (task.TrangThai == "Hoàn thành") mucDo = "Đã xong";
                    else if (task.DiemUuTien >= 80) mucDo = "Khẩn cấp";
                    else if (task.DiemUuTien >= 50) mucDo = "Chú ý";

                    if (task.TrangThai != "Hoàn thành")
                    {
                        tatCaTask.Add(new TaskDashboardItem
                        {
                            TenMonHoc = mon.TenMonHoc,
                            TenTask = task.TenTask,
                            HanChot = task.HanChot,
                            DiemUuTien = task.DiemUuTien,
                            MucDoCanhBao = mucDo
                        });
                    }
                }
            }

            ThongKe = $"Bạn đang quản lý {tongSoMon} môn học và có {tatCaTask.Count} deadline chưa hoàn thành.";
            var top5KhẩnCấp = tatCaTask.OrderByDescending(t => t.DiemUuTien).Take(5).ToList();

            // Xóa rổ cũ và đắp dữ liệu mới vào
            Top5Task.Clear();
            foreach (var item in top5KhẩnCấp)
            {
                Top5Task.Add(item);
            }
        }

        // 3. CÁC NÚT BẤM (COMMANDS)
        [RelayCommand]
        private void MoQuanLyMonHoc()
        {
            OnNavigateToMonHoc?.Invoke(_hocKyHienTai);
        }

        [RelayCommand]
        private void LuuDuLieu()
        {
            DataManager.LuuHocKy(_hocKyHienTai);
            MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Ma thuật Deep Linking: Nút này sẽ nhận vào 1 Object là cái dòng bị bấm!
        [RelayCommand]
        private void DiToiTask(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon != null)
            {
                MonHoc monHocCanTim = _hocKyHienTai.DanhSachMonHoc.FirstOrDefault(m => m.TenMonHoc == taskDuocChon.TenMonHoc);
                if (monHocCanTim != null)
                {
                    OnNavigateToTask?.Invoke(_hocKyHienTai, monHocCanTim);
                }
                else
                {
                    MessageBox.Show("Không tìm thấy môn học gốc. Dữ liệu có thể bị lỗi!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}