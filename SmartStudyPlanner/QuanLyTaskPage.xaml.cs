using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation; // Dùng cho nút Quay lại

namespace SmartStudyPlanner
{
    public partial class QuanLyTaskPage : Page
    {
        private HocKy hocKyHienTai; // Cần giữ lại để phục vụ Save Game
        private MonHoc monHocHienTai;
        private StudyTask taskDangSua = null;

        // Constructor ĐÃ ĐƯỢC SỬA LẠI ĐỂ NHẬN 2 THAM SỐ
        public QuanLyTaskPage(HocKy hocKyTruyenSang, MonHoc monHocDuocTruyen)
        {
            InitializeComponent();
            hocKyHienTai = hocKyTruyenSang;
            monHocHienTai = monHocDuocTruyen;

            txtTieuDe.Text = $"QUẢN LÝ DEADLINE - MÔN {monHocHienTai.TenMonHoc.ToUpper()}";

            TinhDiemVaSapXep();
            dgDanhSachTask.ItemsSource = monHocHienTai.DanhSachTask;
        }

        // HÀM MỚI: Bắt sự kiện quay lại trang trước
        private void BtnQuayLai_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void BtnXoaTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            StudyTask taskCanXoa = btn.DataContext as StudyTask;

            if (taskCanXoa != null)
            {
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa bài tập '{taskCanXoa.TenTask}'?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    monHocHienTai.DanhSachTask.Remove(taskCanXoa);
                    DataManager.LuuHocKy(hocKyHienTai); // AUTO-SAVE
                }
            }
        }

        private void BtnSuaTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            taskDangSua = btn.DataContext as StudyTask;

            if (taskDangSua != null)
            {
                txtTenTask.Text = taskDangSua.TenTask;
                dpHanChot.SelectedDate = taskDangSua.HanChot;
                cmbLoaiTask.SelectedIndex = (int)taskDangSua.LoaiTask;
                txtDoKho.Text = taskDangSua.DoKho.ToString();

                btnThemTask.Content = "Cập Nhật";
                btnThemTask.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219));
            }
        }

        private void BtnThemTask_Click(object sender, RoutedEventArgs e)
        {
            string tenTask = txtTenTask.Text;
            DateTime? hanChot = dpHanChot.SelectedDate;

            if (string.IsNullOrWhiteSpace(tenTask) || hanChot == null)
            {
                MessageBox.Show("Vui lòng nhập Tên bài tập và Hạn chót!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int doKho = int.TryParse(txtDoKho.Text, out int parsedDoKho) ? parsedDoKho : 1;
            if (doKho < 1 || doKho > 5) doKho = 1;

            LoaiCongViec loaiTask = (LoaiCongViec)cmbLoaiTask.SelectedIndex;

            if (taskDangSua == null)
            {
                StudyTask taskMoi = new StudyTask(tenTask, hanChot.Value, loaiTask, doKho);
                monHocHienTai.DanhSachTask.Add(taskMoi);
            }
            else
            {
                taskDangSua.TenTask = tenTask;
                taskDangSua.HanChot = hanChot.Value;
                taskDangSua.LoaiTask = loaiTask;
                taskDangSua.DoKho = doKho;

                dgDanhSachTask.Items.Refresh();

                taskDangSua = null;
                btnThemTask.Content = "Thêm Deadline";
                btnThemTask.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 89, 182));
            }

            TinhDiemVaSapXep();
            DataManager.LuuHocKy(hocKyHienTai); // AUTO-SAVE

            txtTenTask.Clear();
            txtDoKho.Clear();
            dpHanChot.SelectedDate = null;
            cmbLoaiTask.SelectedIndex = 0;
            txtTenTask.Focus();
        }

        private void TinhDiemVaSapXep()
        {
            foreach (var task in monHocHienTai.DanhSachTask)
            {
                task.DiemUuTien = DecisionEngine.CalculatePriority(task, monHocHienTai);

                if (task.TrangThai == "Hoàn thành")
                {
                    task.MucDoCanhBao = "Đã xong";
                }
                else if (task.DiemUuTien >= 80)
                {
                    task.MucDoCanhBao = "Khẩn cấp";
                }
                else if (task.DiemUuTien >= 50)
                {
                    task.MucDoCanhBao = "Chú ý";
                }
                else
                {
                    task.MucDoCanhBao = "An toàn";
                }
            }

            var danhSachDaSapXep = monHocHienTai.DanhSachTask.OrderByDescending(t => t.DiemUuTien).ToList();
            monHocHienTai.DanhSachTask.Clear();

            foreach (var task in danhSachDaSapXep)
            {
                monHocHienTai.DanhSachTask.Add(task);
            }
        }

        private void BtnHoanThanh_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            StudyTask taskDaXong = btn.DataContext as StudyTask;

            if (taskDaXong != null)
            {
                if (taskDaXong.TrangThai == "Hoàn thành") return;

                taskDaXong.TrangThai = "Hoàn thành";
                TinhDiemVaSapXep();
                dgDanhSachTask.Items.Refresh();

                DataManager.LuuHocKy(hocKyHienTai); // AUTO-SAVE
            }
        }
    }
}