using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SmartStudyPlanner
{
    public partial class QuanLyMonHocPage : Page
    {
        private QuanLyMonHocViewModel _viewModel;

        public QuanLyMonHocPage(HocKy hocKyTruyenSang)
        {
            InitializeComponent();

            _viewModel = new QuanLyMonHocViewModel(hocKyTruyenSang);

            // 1. Lắng nghe chuyển trang
            _viewModel.OnGoBack = () => NavigationService.GoBack();

            _viewModel.OnNavigateToTask = (hk, mh) =>
            {
                NavigationService.Navigate(new QuanLyTaskPage(hk, mh));
            };

            // 2. Lắng nghe lệnh vẽ lại bảng khi sửa dữ liệu
            _viewModel.OnRefreshGrid = () => dgDanhSachMon.Items.Refresh();

            // 3. Loa thông báo (vd: cảnh báo trùng môn học ở prevent-at-source)
            _viewModel.OnThongBao = msg =>
                System.Windows.MessageBox.Show(msg, "Trùng môn học", System.Windows.MessageBoxButton.OK);

            this.DataContext = _viewModel;
        }
    }
}