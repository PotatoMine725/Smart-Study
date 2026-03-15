using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SmartStudyPlanner
{
    public partial class DashboardWindow : Window
    {
        private HocKy hocKyHienTai;

        public DashboardWindow(HocKy hocKyTruyenSang)
        {
            InitializeComponent();
            hocKyHienTai = hocKyTruyenSang;

            LoadDuLieuDashboard();
        }

        // Hàm này sẽ tự chạy mỗi khi mở Dashboard lên
        private void LoadDuLieuDashboard()
        {
            txtTieuDe.Text = $"TỔNG QUAN - {hocKyHienTai.Ten.ToUpper()}";

            int tongSoMon = hocKyHienTai.DanhSachMonHoc.Count;
            int tongSoTask = 0;

            // 1. Tạo một cái rổ lớn để gom tất cả bài tập của mọi môn học
            List<TaskDashboardItem> tatCaTask = new List<TaskDashboardItem>();

            // 2. Đi dạo qua từng môn học
            foreach (var mon in hocKyHienTai.DanhSachMonHoc)
            {
                tongSoTask += mon.DanhSachTask.Count;

                // Đi dạo qua từng bài tập của môn đó
                foreach (var task in mon.DanhSachTask)
                {
                    // Chấm điểm lại cho chắc ăn
                    task.DiemUuTien = DecisionEngine.CalculatePriority(task, mon);

                    // Phân tích rủi ro (Risk Analyzer)
                    string mucDo = "An toàn";
                    if (task.TrangThai == "Hoàn thành") mucDo = "Đã xong";
                    else if (task.DiemUuTien >= 80) mucDo = "Khẩn cấp";
                    else if (task.DiemUuTien >= 50) mucDo = "Chú ý";

                    // NẾU CHƯA XONG THÌ MỚI ĐƯA LÊN BẢNG XẾP HẠNG
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

            // 3. Sắp xếp toàn bộ rổ bài tập từ cao xuống thấp, và LẤY TOP 5
            var top5KhẩnCấp = tatCaTask.OrderByDescending(t => t.DiemUuTien).Take(5).ToList();

            // 4. Hiển thị lên DataGrid
            dgTop5Task.ItemsSource = top5KhẩnCấp;
        }

        // Nút Mở cửa sổ quản lý Môn Học
        private void BtnQuanLyMonHoc_Click(object sender, RoutedEventArgs e)
        {
            QuanLyMonHocWindow cuaSoMonHoc = new QuanLyMonHocWindow(hocKyHienTai);
            cuaSoMonHoc.ShowDialog(); // Mở cửa sổ Môn học lên

            // KHI TẮT CỬA SỔ MÔN HỌC ĐI -> CẬP NHẬT LẠI BẢNG XẾP HẠNG TRÊN DASHBOARD
            LoadDuLieuDashboard();
        }

        // Nút Save Game đưa ra ngoài màn hình chính cho tiện
        private void BtnLuuDuLieu_Click(object sender, RoutedEventArgs e)
        {
            DataManager.LuuHocKy(hocKyHienTai);
            MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}