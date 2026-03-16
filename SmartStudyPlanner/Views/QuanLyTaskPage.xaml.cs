using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SmartStudyPlanner
{
    public partial class QuanLyTaskPage : Page
    {
        private QuanLyTaskViewModel _viewModel;

        public QuanLyTaskPage(HocKy hocKy, MonHoc monHoc)
        {
            InitializeComponent();

            _viewModel = new QuanLyTaskViewModel(hocKy, monHoc);

            // Bắt sự kiện lùi trang
            _viewModel.OnGoBack = () => NavigationService.GoBack();

            // Bắt sự kiện vẽ lại bảng khi sửa hoặc đánh dấu Hoàn thành
            _viewModel.OnRefreshGrid = () => dgDanhSachTask.Items.Refresh();

            this.DataContext = _viewModel;
        }
    }
}