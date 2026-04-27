using System.Threading.Tasks;
using System.Windows;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner
{
    public partial class App : System.Windows.Application
    {
        // Hàm này tự động chạy trước khi mở cửa sổ đầu tiên
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // KÍCH HOẠT DATABASE
            using (var db = new AppDbContext())
            {
                // Lệnh ma thuật: Nếu file .db chưa tồn tại, nó sẽ tự động tạo mới dựa trên AppDbContext!
                db.Database.EnsureCreated();
            }

            // KHỞI TẠO DI CONTAINER
            // Đây là điểm duy nhất toàn app cấu hình dependency injection.
            // Mọi service đăng ký ở đây đều được resolve qua ServiceLocator.Get<T>()
            // hoặc qua constructor injection khi ViewModel được tạo thủ công.
            ServiceLocator.Configure();

            // M7: warm up model manager in background, không block UI startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await ServiceLocator.Get<IMLModelManager>().InitializeAsync();
                }
                catch
                {
                    // Silent fallback: ML luôn là enhancement, không được chặn app launch.
                }
            });
        }
    }
}