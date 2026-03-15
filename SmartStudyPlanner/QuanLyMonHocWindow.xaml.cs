using System;
using System.Windows;
using System.Windows.Controls;

namespace SmartStudyPlanner
{
    public partial class QuanLyMonHocWindow : Window
    {
        // 1. Tạo một biến toàn cục để lưu Học kỳ được truyền từ màn hình kia sang
        private HocKy hocKyHienTai;

        private MonHoc monDangSua = null;

        // 2. Sửa lại Constructor để ÉP màn hình này phải nhận vào 1 object HocKy khi được mở lên
        public QuanLyMonHocWindow(HocKy hocKyTruyenSang)
        {
            InitializeComponent();

            // Lưu object đó vào biến toàn cục để xài ở các hàm khác
            hocKyHienTai = hocKyTruyenSang;

            // Đổi dòng tiêu đề cho ngầu
            txtTieuDe.Text = $"DANH SÁCH MÔN HỌC - {hocKyHienTai.Ten.ToUpper()}";

            // "Trói" cái DataGrid vào cái "ba lô" danh sách môn học của học kỳ này
            dgDanhSachMon.ItemsSource = hocKyHienTai.DanhSachMonHoc;
        }

        private void BtnThemMon_Click(object sender, RoutedEventArgs e)
        {
            string tenMon = txtTenMon.Text;

            if (string.IsNullOrWhiteSpace(tenMon))
            {
                MessageBox.Show("Vui lòng nhập tên môn học!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int soTinChi = int.TryParse(txtSoTinChi.Text, out int tinChi) ? tinChi : -1;

            if (soTinChi <= 0)
            {
                MessageBox.Show("Số tín chỉ phải là một số lớn hơn 0!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (monDangSua == null)
            {
                // TRƯỜNG HỢP 1: THÊM MỚI (Biến nhớ đang trống)
                MonHoc monHocMoi = new MonHoc(tenMon, soTinChi);
                hocKyHienTai.DanhSachMonHoc.Add(monHocMoi);
                MessageBox.Show("Thêm thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // TRƯỜNG HỢP 2: CẬP NHẬT (Đang có môn được chọn để sửa)
                monDangSua.TenMonHoc = tenMon;
                monDangSua.SoTinChi = soTinChi;

                // Lệnh này bắt DataGrid phải vẽ lại dữ liệu mới vì ta vừa sửa Object ở dưới nền
                dgDanhSachMon.Items.Refresh();

                MessageBox.Show("Cập nhật thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                // Sửa xong thì phải "Xóa trí nhớ" và đưa nút bấm về như cũ
                monDangSua = null;
                btnThemMon.Content = "Thêm Môn";
                btnThemMon.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113)); // Trả lại màu xanh lá
            }

            // Dọn dẹp form 
            txtTenMon.Clear();
            txtSoTinChi.Clear();
            txtTenMon.Focus();
        }

        private void BtnXoaMon_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            MonHoc monCanXoa = btn.DataContext as MonHoc;

            if (monCanXoa != null)
            {
                MessageBoxResult ketQua = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa môn '{monCanXoa.TenMonHoc}' không?",
                    "Xác nhận xóa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (ketQua == MessageBoxResult.Yes)
                {
                    // SỬA Ở ĐÂY: Xóa khỏi ba lô của hocKyHienTai
                    hocKyHienTai.DanhSachMonHoc.Remove(monCanXoa);
                }
            }
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            // Gọi anh DataManager ra để nhờ lưu cái "ba lô" hiện tại
            DataManager.LuuHocKy(hocKyHienTai);

            MessageBox.Show("Đã lưu tiến trình thành công! Bây giờ bạn có thể tắt App.",
                            "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // HÀM MỚI: Bắt sự kiện khi bấm nút Sửa trên dòng
        private void BtnSuaMon_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            monDangSua = btn.DataContext as MonHoc; // Lưu môn học đang chọn vào biến nhớ

            if (monDangSua != null)
            {
                // Đẩy dữ liệu ngược lên 2 ô TextBox
                txtTenMon.Text = monDangSua.TenMonHoc;
                txtSoTinChi.Text = monDangSua.SoTinChi.ToString();

                // Đổi chữ của nút Thêm thành Cập Nhật cho người dùng biết
                btnThemMon.Content = "Cập Nhật";
                btnThemMon.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)); // Đổi sang màu xanh dương
            }
        }

        // HÀM MỚI: Xử lý khi bấm nút Tasks
        private void BtnXemTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            MonHoc monDuocChon = btn.DataContext as MonHoc; // Lấy ra môn học ở dòng bị bấm

            if (monDuocChon != null)
            {
                // Mở cửa sổ quản lý Task và truyền môn học sang
                QuanLyTaskWindow cuaSoTask = new QuanLyTaskWindow(monDuocChon);

                // Dùng ShowDialog() thay vì Show(). 
                // Nghĩa là người dùng phải tắt cửa sổ Task này thì mới được thao tác tiếp ở cửa sổ Môn học.
                cuaSoTask.ShowDialog();
            }
        }
    }
}