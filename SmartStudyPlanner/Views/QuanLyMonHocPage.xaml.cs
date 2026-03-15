using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SmartStudyPlanner
{
    public partial class QuanLyMonHocPage : Page
    {
        private HocKy hocKyHienTai;
        private MonHoc monDangSua = null;

        public QuanLyMonHocPage(HocKy hocKyTruyenSang)
        {
            InitializeComponent();
            hocKyHienTai = hocKyTruyenSang;
            txtTieuDe.Text = $"DANH SÁCH MÔN HỌC - {hocKyHienTai.Ten.ToUpper()}";
            dgDanhSachMon.ItemsSource = hocKyHienTai.DanhSachMonHoc;
        }

        // HÀM MỚI: Nút Quay lại
        private void BtnQuayLai_Click(object sender, RoutedEventArgs e)
        {
            // Bấm nút này sẽ lùi về kênh trước đó (Dashboard)
            NavigationService.GoBack();
        }

        private void BtnThemMon_Click(object sender, RoutedEventArgs e)
        {
            string tenMon = txtTenMon.Text;
            if (string.IsNullOrWhiteSpace(tenMon)) return;
            int soTinChi = int.TryParse(txtSoTinChi.Text, out int tinChi) ? tinChi : -1;
            if (soTinChi <= 0) return;

            if (monDangSua == null)
            {
                hocKyHienTai.DanhSachMonHoc.Add(new MonHoc(tenMon, soTinChi));
            }
            else
            {
                monDangSua.TenMonHoc = tenMon;
                monDangSua.SoTinChi = soTinChi;
                dgDanhSachMon.Items.Refresh();
                monDangSua = null;
                btnThemMon.Content = "Thêm Môn";
                btnThemMon.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            }

            // AUTO-SAVE: Cứ thêm/sửa xong là tự động lưu ngầm!
            DataManager.LuuHocKy(hocKyHienTai);

            txtTenMon.Clear();
            txtSoTinChi.Clear();
            txtTenMon.Focus();
        }

        private void BtnSuaMon_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            monDangSua = btn.DataContext as MonHoc;
            if (monDangSua != null)
            {
                txtTenMon.Text = monDangSua.TenMonHoc;
                txtSoTinChi.Text = monDangSua.SoTinChi.ToString();
                btnThemMon.Content = "Cập Nhật";
                btnThemMon.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219));
            }
        }

        private void BtnXoaMon_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            MonHoc monCanXoa = btn.DataContext as MonHoc;
            if (monCanXoa != null)
            {
                if (MessageBox.Show($"Xóa môn '{monCanXoa.TenMonHoc}'?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    hocKyHienTai.DanhSachMonHoc.Remove(monCanXoa);

                    // AUTO-SAVE: Xóa xong cũng lưu ngầm!
                    DataManager.LuuHocKy(hocKyHienTai);
                }
            }
        }

        private void BtnXemTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            MonHoc monDuocChon = btn.DataContext as MonHoc;
            if (monDuocChon != null)
            {
                // CHUYỂN KÊNH SANG TRANG BÀI TẬP VÀ TRUYỀN CẢ HỌC KỲ VÀ MÔN HỌC
                NavigationService.Navigate(new QuanLyTaskPage(hocKyHienTai, monDuocChon));
            }
        }
    }
}