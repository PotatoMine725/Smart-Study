using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SmartStudyPlanner
{
    public partial class DashboardPage : Page
    {
        private DashboardViewModel _viewModel;
        public HocKy HocKy { get; }

        public DashboardPage(HocKy hocKy)
        {
            InitializeComponent();
            HocKy = hocKy;

            // 1. Tạo "Bộ não"
            _viewModel = new DashboardViewModel(hocKy);

            // 2. Lắng nghe tiếng hét chuyển trang từ ViewModel
            _viewModel.OnNavigateToMonHoc = (hk) =>
            {
                NavigationService.Navigate(new QuanLyMonHocPage(hk));
            };

            _viewModel.OnNavigateToTask = (hk, mh) =>
            {
                NavigationService.Navigate(new QuanLyTaskPage(hk, mh));
            };

            // 3. Gắn não vào Giao diện
            this.DataContext = _viewModel;
        }

        // Hàm này vẫn cần thiết vì mỗi khi người dùng ấn nút "Back" từ trang khác về đây, 
        // ta phải bảo ViewModel cập nhật lại bảng xếp hạng Top 5.
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.LoadDuLieuDashboard();
        }
    }
}