using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SmartStudyPlanner
{
    public partial class DashboardPage : Page
    {
        private HocKy hocKyHienTai;

        public DashboardPage(HocKy hocKy)
        {
            InitializeComponent();
            hocKyHienTai = hocKy;
        }

        // Sự kiện này chạy mỗi khi Page này được hiển thị lên màn hình Tivi
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDuLieuDashboard();
        }

        private void LoadDuLieuDashboard()
        {
            txtTieuDe.Text = $"TỔNG QUAN - {hocKyHienTai.Ten.ToUpper()}";

            int tongSoMon = hocKyHienTai.DanhSachMonHoc.Count;
            int tongSoTask = 0;
            List<TaskDashboardItem> tatCaTask = new List<TaskDashboardItem>();

            foreach (var mon in hocKyHienTai.DanhSachMonHoc)
            {
                tongSoTask += mon.DanhSachTask.Count;
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

            txtThongKe.Text = $"Bạn đang quản lý {tongSoMon} môn học và có {tatCaTask.Count} deadline chưa hoàn thành.";
            var top5KhẩnCấp = tatCaTask.OrderByDescending(t => t.DiemUuTien).Take(5).ToList();
            dgTop5Task.ItemsSource = top5KhẩnCấp;
        }

        private void BtnQuanLyMonHoc_Click(object sender, RoutedEventArgs e)
        {
            // CHUYỂN KÊNH SANG TRANG MÔN HỌC (Không mở cửa sổ mới)
            NavigationService.Navigate(new QuanLyMonHocPage(hocKyHienTai));
        }

        private void BtnLuuDuLieu_Click(object sender, RoutedEventArgs e)
        {
            DataManager.LuuHocKy(hocKyHienTai);
            MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // HÀM MỚI: Điều hướng sâu (Deep Linking)
        private void BtnDiToiTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            // Lấy dòng dữ liệu (TaskDashboardItem) đang được bấm
            TaskDashboardItem taskDuocChon = btn.DataContext as TaskDashboardItem;

            if (taskDuocChon != null)
            {
                // Dùng LINQ để dò tìm lại cái object Môn Học gốc bên trong ba lô HocKyHienTai
                // Thuật toán: Tìm môn nào có Tên bằng với Tên môn học hiển thị trên dòng này
                MonHoc monHocCanTim = hocKyHienTai.DanhSachMonHoc.FirstOrDefault(m => m.TenMonHoc == taskDuocChon.TenMonHoc);

                if (monHocCanTim != null)
                {
                    // TÌM THẤY RỒI! Chuyển kênh thẳng sang trang Quản lý Bài Tập của môn đó luôn!
                    NavigationService.Navigate(new QuanLyTaskPage(hocKyHienTai, monHocCanTim));
                }
                else
                {
                    MessageBox.Show("Không tìm thấy môn học gốc. Dữ liệu có thể bị lỗi!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}