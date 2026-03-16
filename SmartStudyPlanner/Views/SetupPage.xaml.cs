using System.Windows.Controls;
using System.Windows.Navigation;
using SmartStudyPlanner.ViewModels; // Khai báo đường dẫn tới thư mục ViewModels

namespace SmartStudyPlanner
{
    public partial class SetupPage : Page
    {
        public SetupPage()
        {
            InitializeComponent();

            // 1. Khởi tạo "Bộ não" ViewModel
            SetupViewModel vm = new SetupViewModel();

            // 2. Lắng nghe tiếng hét của ViewModel: Nếu tạo xong Học kỳ thì View sẽ tự động chuyển trang
            vm.OnSetupCompleted = (hocKyMoi) =>
            {
                NavigationService.Navigate(new DashboardPage(hocKyMoi));
            };

            // 3. Gắn bộ não vào giao diện (Bắt buộc phải có dòng này thì các sợi dây Binding mới hoạt động)
            this.DataContext = vm;
        }
    }
}