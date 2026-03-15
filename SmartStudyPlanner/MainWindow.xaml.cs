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

                // Nếu người dùng bấm YES -> Nhảy thẳng sang màn hình Dashboard (Trang chủ) luôn!
                if (result == MessageBoxResult.Yes)
                {
                    // ĐÃ SỬA: Chuyển hướng sang DashboardWindow
                    DashboardWindow window = new DashboardWindow(hocKyCu);
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
                // 1. Chỉ khởi tạo Học kỳ MỘT LẦN 
                HocKy hocKyMoi = new HocKy(tenHK, ngayChon.Value);

                // 2. MỞ CỬA SỔ DASHBOARD và BƠM object hocKyMoi vào cửa sổ đó
                // ĐÃ SỬA: Đổi QuanLyMonHocWindow thành DashboardWindow
                DashboardWindow cuaSoChinh = new DashboardWindow(hocKyMoi);
                cuaSoChinh.Show();

                // 3. ĐÓNG CỬA SỔ CŨ LẠI
                this.Close();
            }
        }
    }
}