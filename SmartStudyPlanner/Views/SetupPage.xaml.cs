using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation; // Bắt buộc phải có

namespace SmartStudyPlanner
{
    public partial class SetupPage : Page
    {
        public SetupPage()
        {
            InitializeComponent();
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
                HocKy hocKyMoi = new HocKy(tenHK, ngayChon.Value);

                // PHÉP MÀU: Đổi kênh sang DashboardPage mà KHÔNG CẦN mở cửa sổ mới
                NavigationService.Navigate(new DashboardPage(hocKyMoi));
            }
        }
    }
}