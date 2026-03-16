using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using System.Windows;

namespace SmartStudyPlanner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Gắn sự kiện Loaded để chạy hàm Async khi cửa sổ vừa mở lên
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Khởi tạo Repository
            IStudyRepository repository = new StudyRepository();

            // 2. Đọc dữ liệu bất đồng bộ (UI vẫn mượt mà trong lúc chờ đợi)
            HocKy hocKyCu = await repository.DocHocKyAsync();

            if (hocKyCu != null)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Tìm thấy dữ liệu của '{hocKyCu.Ten}'. Bạn có muốn tiếp tục quản lý học kỳ này không?",
                    "Phát hiện dữ liệu cũ", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    MainFrame.Navigate(new DashboardPage(hocKyCu));
                else
                    MainFrame.Navigate(new SetupPage());
            }
            else
            {
                MainFrame.Navigate(new SetupPage());
            }
        }
    }
}