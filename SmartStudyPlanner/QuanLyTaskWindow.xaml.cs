using System;
using System.Windows;
using System.Windows.Controls;

namespace SmartStudyPlanner
{
    public partial class QuanLyTaskWindow : Window
    {
        // Nhớ môn học hiện tại đang được quản lý
        private MonHoc monHocHienTai;

        private StudyTask taskDangSua = null;

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
        // 1. HÀM XÓA TASK
        private void BtnXoaTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            StudyTask taskCanXoa = btn.DataContext as StudyTask;

            if (taskCanXoa != null)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa bài tập '{taskCanXoa.TenTask}'?",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    monHocHienTai.DanhSachTask.Remove(taskCanXoa);
                }
            }
        }

        // 2. HÀM BẤM NÚT SỬA TRÊN DÒNG (Đẩy dữ liệu lên form)
        private void BtnSuaTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            taskDangSua = btn.DataContext as StudyTask;

            if (taskDangSua != null)
            {
                // Đẩy dữ liệu ngược lên các ô nhập liệu
                txtTenTask.Text = taskDangSua.TenTask;
                dpHanChot.SelectedDate = taskDangSua.HanChot;
                cmbLoaiTask.SelectedIndex = (int)taskDangSua.LoaiTask; // Ép kiểu Enum về số thứ tự
                txtDoKho.Text = taskDangSua.DoKho.ToString();

                // Đổi nút "Thêm" thành "Cập Nhật" (Màu xanh dương)
                btnThemTask.Content = "Cập Nhật";
                btnThemTask.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219));
            }
        }

        // 3. CẬP NHẬT HÀM THÊM TASK (Xử lý cả 2 trường hợp Thêm và Cập Nhật)
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

            // --- KIỂM TRA ĐANG Ở TRẠNG THÁI NÀO ---
            if (taskDangSua == null)
            {
                // TRƯỜNG HỢP A: THÊM MỚI
                StudyTask taskMoi = new StudyTask(tenTask, hanChot.Value, loaiTask, doKho);
                monHocHienTai.DanhSachTask.Add(taskMoi);
            }
            else
            {
                // TRƯỜNG HỢP B: CẬP NHẬT
                taskDangSua.TenTask = tenTask;
                taskDangSua.HanChot = hanChot.Value;
                taskDangSua.LoaiTask = loaiTask;
                taskDangSua.DoKho = doKho;

                // Bắt DataGrid vẽ lại dữ liệu mới
                dgDanhSachTask.Items.Refresh();

                // Xóa trí nhớ, trả nút bấm về trạng thái Thêm mới (Màu tím)
                taskDangSua = null;
                btnThemTask.Content = "Thêm Deadline";
                btnThemTask.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 89, 182));
            }

            // Dọn dẹp form cho lần nhập tiếp theo
            txtTenTask.Clear();
            txtDoKho.Clear();
            dpHanChot.SelectedDate = null; // Xóa luôn ngày
            cmbLoaiTask.SelectedIndex = 0; // Trả ComboBox về mặc định (Bài tập về nhà)
            txtTenTask.Focus();
        }
    }
}