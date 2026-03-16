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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Luôn luôn mở SetupPage đầu tiên. 
            // SetupPage sẽ lo việc hiển thị danh sách học kỳ cũ hoặc tạo mới.
            MainFrame.Navigate(new SetupPage());
        }
    }
}