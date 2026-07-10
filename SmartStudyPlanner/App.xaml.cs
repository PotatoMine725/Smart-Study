using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Telemetry;

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

                // Full bootstrap sequence lives in AppStartup.EnsureDatabaseReady so an
                // integration test can drive the exact same sequence against a real file-based
                // DB (OnStartup is WPF lifecycle, not callable from a unit test).
                var dbPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "SmartStudyData.db");
                AppStartup.EnsureDatabaseReady(db, dbPath);
            }

            // KHỞI TẠO DI CONTAINER
            // Đây là điểm duy nhất toàn app cấu hình dependency injection.
            // Mọi service đăng ký ở đây đều được resolve qua ServiceLocator.Get<T>()
            // hoặc qua constructor injection khi ViewModel được tạo thủ công.
            ServiceLocator.Configure();

            // M7 + M8-A: warm up model managers in background, không block UI startup
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

            _ = Task.Run(async () =>
            {
                try
                {
                    // M8-A: load existing text_classifier.zip or train from embedded seed (offline-first).
                    await ServiceLocator.Get<ITextClassifierModelManager>().InitializeAsync();
                }
                catch
                {
                    // Classifier is an enhancement on top of the heuristic parser; never block launch.
                }
            });

            // M8-B: opportunistic outcome maturation — fill WeightChangeLog entries whose 14d window elapsed.
            _ = Task.Run(async () =>
            {
                try
                {
                    await ServiceLocator.Get<IOutcomeMaturationService>().MatureAsync(System.DateTime.UtcNow);
                }
                catch
                {
                    // Maturation is an enhancement; never block launch.
                }
            });
        }
    }
}
