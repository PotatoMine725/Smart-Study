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

            // Kiểm tra tên môn có bị trống không
            if (string.IsNullOrWhiteSpace(tenMon))
            {
                MessageBox.Show("Vui lòng nhập tên môn học!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Dừng hàm ngay tại đây
            }

            // Lấy số tín chỉ, nếu nhập sai (chữ) thì gán là -1
            int soTinChi = int.TryParse(txtSoTinChi.Text, out int tinChi) ? tinChi : -1;

            // Kiểm tra số tín chỉ (phải lớn hơn 0)
            if (soTinChi <= 0)
            {
                MessageBox.Show("Số tín chỉ phải là một số lớn hơn 0!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Dừng hàm ngay tại đây
            }

            // --- NẾU VƯỢ QUA 2 BÀI KIỂM TRA TRÊN THÌ MỚI CHẠY XUỐNG ĐÂY ---

            MonHoc monHocMoi = new MonHoc(tenMon, soTinChi);
            danhSachMonHoc.Add(monHocMoi);

            MessageBox.Show($"Môn học '{monHocMoi.TenMonHoc}' với {monHocMoi.SoTinChi} tín chỉ đã được thêm vào danh sách.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

            // Dọn dẹp form để nhập môn tiếp theo
            txtTenMon.Clear();
            txtSoTinChi.Clear(); // Nhớ xóa luôn cả ô tín chỉ nhé
            txtTenMon.Focus();   // Đưa con trỏ chuột quay lại ô Tên Môn
        }

        // HÀM MỚI: Xử lý sự kiện khi bấm nút Xóa trên từng dòng
        private void BtnXoaMon_Click(object sender, RoutedEventArgs e)
        {
            // 1. Lấy ra cái nút bấm vừa bị click
            Button btn = sender as Button;

            // 2. Trích xuất dữ liệu của cái dòng chứa nút bấm đó
            MonHoc monCanXoa = btn.DataContext as MonHoc;

            // Kiểm tra chắc chắn là đã tìm thấy dữ liệu
            if (monCanXoa != null)
            {
                // 3. Hiển thị hộp thoại HỎI XÁC NHẬN (Cực kỳ quan trọng cho UX)
                // Chú ý: Ta dùng MessageBoxButton.YesNo để hiện ra 2 nút "Có" và "Không"
                MessageBoxResult ketQua = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa môn '{monCanXoa.TenMonHoc}' không?",
                    "Xác nhận xóa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // 4. Nếu người dùng chọn Yes thì mới xóa
                if (ketQua == MessageBoxResult.Yes)
                {
                    // Phép màu của ObservableCollection: Chỉ cần xóa khỏi danh sách, 
                    // DataGrid trên màn hình sẽ tự động làm biến mất dòng đó!
                    danhSachMonHoc.Remove(monCanXoa);
                }
            }
        }
    }
}