using System;
using System.Windows;

namespace SmartStudyPlanner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // KIỂM TRA TÌM FILE SAVE CŨ
            HocKy hocKyCu = DataManager.DocHocKy();

            // Nếu phát hiện có dữ liệu cũ
            if (hocKyCu != null)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Tìm thấy dữ liệu của '{hocKyCu.Ten}'. Bạn có muốn tiếp tục quản lý học kỳ này không?",
                    "Phát hiện dữ liệu cũ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // Nếu người dùng bấm YES -> Nhảy thẳng sang màn hình Môn Học luôn!
                if (result == MessageBoxResult.Yes)
                {
                    QuanLyMonHocWindow window = new QuanLyMonHocWindow(hocKyCu);
                    window.Show();
                    this.Close(); // Đóng màn hình MainWindow (tạo mới) lại
                }
            }
        }

        private void BtnTaoHocKy_Click(object sender, RoutedEventArgs e)
        {
            string tenHK = txtTenHocKy.Text;
            DateTime? ngayChon = dpNgayBatDau.SelectedDate;

            if (string.IsNullOrEmpty(tenHK) || ngayChon == null)
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tên học kỳ và ngày bắt đầu", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // 1. Chỉ khởi tạo Học kỳ MỘT LẦN (đã xóa txtTrangThai và biến khởi tạo thừa)
                HocKy hocKyMoi = new HocKy(tenHK, ngayChon.Value);

                // 2. MỞ CỬA SỔ MỚI và BƠM object hocKyMoi vào cửa sổ đó
                QuanLyMonHocWindow cuaSoMonHoc = new QuanLyMonHocWindow(hocKyMoi);
                cuaSoMonHoc.Show();

                // 3. ĐÓNG CỬA SỔ CŨ LẠI
                this.Close();
            }
        }
    }
}