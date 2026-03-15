using System;
using System.Windows;
using System.Windows.Controls;

namespace SmartStudyPlanner
{
    public partial class QuanLyTaskWindow : Window
    {
        // Nhớ môn học hiện tại đang được quản lý
        private MonHoc monHocHienTai;

        // Constructor yêu cầu phải truyền MonHoc vào
        public QuanLyTaskWindow(MonHoc monHocDuocTruyen)
        {
            InitializeComponent();
            monHocHienTai = monHocDuocTruyen;

            // Đổi tiêu đề
            txtTieuDe.Text = $"QUẢN LÝ DEADLINE - MÔN {monHocHienTai.TenMonHoc.ToUpper()}";

            // Gắn cái "Ba lô Task" của môn này vào DataGrid
            dgDanhSachTask.ItemsSource = monHocHienTai.DanhSachTask;
        }

        // Bắt sự kiện thêm Task
        private void BtnThemTask_Click(object sender, RoutedEventArgs e)
        {
            string tenTask = txtTenTask.Text;
            DateTime? hanChot = dpHanChot.SelectedDate;

            // Validate cơ bản
            if (string.IsNullOrWhiteSpace(tenTask) || hanChot == null)
            {
                MessageBox.Show("Vui lòng nhập Tên bài tập và Hạn chót!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy độ khó (mặc định là 1 nếu nhập sai)
            int doKho = int.TryParse(txtDoKho.Text, out int parsedDoKho) ? parsedDoKho : 1;
            if (doKho < 1 || doKho > 5) doKho = 1; // Ép về chuẩn 1-5

            // Lấy loại công việc từ ComboBox (chuyển đổi index thành Enum)
            LoaiCongViec loaiTask = (LoaiCongViec)cmbLoaiTask.SelectedIndex;

            // Tạo Task mới và nhét vào ba lô
            StudyTask taskMoi = new StudyTask(tenTask, hanChot.Value, loaiTask, doKho);
            monHocHienTai.DanhSachTask.Add(taskMoi);

            // Dọn form
            txtTenTask.Clear();
            txtDoKho.Clear();
            txtTenTask.Focus();
        }
    }
}