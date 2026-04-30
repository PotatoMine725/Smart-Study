using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
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
            // Mặc định giữ dữ liệu học kỳ/task qua các lần mở app.
            // Nếu cần clean reset dev, có thể bật bằng biến môi trường DEV_RESET_DB=1.
            using (var db = new AppDbContext())
            {
                if (System.Environment.GetEnvironmentVariable("DEV_RESET_DB") == "1")
                {
                    db.Database.EnsureDeleted();
                }

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
