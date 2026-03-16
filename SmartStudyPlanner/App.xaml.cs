using System.Windows;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner
{
    public partial class App : Application
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
        }
    }
}