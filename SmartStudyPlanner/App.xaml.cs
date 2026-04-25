using System.Windows;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services;

namespace SmartStudyPlanner
{
    public partial class App : System.Windows.Application
    {
        // Hàm này tự động chạy trước khi mở cửa sổ đầu tiên
        protected override void OnStartup(StartupEventArgs e)
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
        }
    }
}