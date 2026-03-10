using System;
using System.Windows;
using System.Windows.Controls;

namespace SmartStudyPlanner
{
    public partial class QuanLyMonHocWindow : Window
    {
        // 1. Tạo một biến toàn cục để lưu Học kỳ được truyền từ màn hình kia sang
        private HocKy hocKyHienTai;

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

            MonHoc monHocMoi = new MonHoc(tenMon, soTinChi);

            // SỬA Ở ĐÂY: Thêm vào ba lô của hocKyHienTai
            hocKyHienTai.DanhSachMonHoc.Add(monHocMoi);

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
    }
}