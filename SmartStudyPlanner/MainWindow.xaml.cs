using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartStudyPlanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
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
            else {
                HocKy hocKyMoi = new HocKy(tenHK, ngayChon.Value);

                string chuoiNgay = hocKyMoi.NgayBatDau.ToString("dd/MM/yyyy");

                txtTrangThai.Text = $"Đã tạo học kỳ: {hocKyMoi.Ten} bắt đầu từ ngày {chuoiNgay}";

                MessageBox.Show($"Học kỳ '{hocKyMoi.Ten}' đã được tạo với ngày bắt đầu là {chuoiNgay}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}