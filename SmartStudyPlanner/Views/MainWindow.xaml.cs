using Microsoft.Toolkit.Uwp.Notifications;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services;
using System;
using System.Windows;
using System.Windows.Threading;

// Sử dụng alias để phân biệt các hàm của WPF và Windows Forms
using WinForms = System.Windows.Forms;

namespace SmartStudyPlanner
{
    public partial class MainWindow : Window
    {
        private WinForms.NotifyIcon _notifyIcon;
        private DispatcherTimer _backgroundTimer;
        private bool _thucSuMuonTat = false;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;

            // 1. Cài đặt System Tray (Khay hệ thống)
            SetupSystemTray();

            // 2. Cài đặt vòng lặp kiểm tra ngầm định kỳ
            SetupBackgroundWorker();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SetupPage());
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new WinForms.NotifyIcon();

            // Tự động lấy icon gốc của app để nhét xuống góc màn hình
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            _notifyIcon.Text = "Smart Study Planner (Đang chạy ngầm)";
            _notifyIcon.Visible = true;

            // Cho phép người dùng Click đúp để mở lại app nhanh
            _notifyIcon.DoubleClick += (s, e) => HienThiUngDung();

            // TẠO MENU CHUỘT PHẢI THEO YÊU CẦU
            var contextMenu = new WinForms.ContextMenuStrip();
            contextMenu.Items.Add("Mở ứng dụng", null, (s, e) => HienThiUngDung());
            contextMenu.Items.Add("Thoát hoàn toàn", null, (s, e) => ThoatHoanToan());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void SetupBackgroundWorker()
        {
            _backgroundTimer = new DispatcherTimer();
            // Tạm thời để 1 phút réo 1 lần để em test.
            _backgroundTimer.Interval = TimeSpan.FromMinutes(1);
            _backgroundTimer.Tick += BackgroundTimer_Tick;
            _backgroundTimer.Start();
        }

        private async void BackgroundTimer_Tick(object sender, EventArgs e)
        {
            var repo = ServiceLocator.Get<IStudyRepository>();
            var decisionEngine = ServiceLocator.Get<IDecisionEngine>();

            var danhSachHocKy = await repo.LayDanhSachHocKyAsync();
            int soTaskKhanCap = 0;

            foreach (var hk in danhSachHocKy)
            {
                foreach (var mon in hk.DanhSachMonHoc)
                {
                    foreach (var task in mon.DanhSachTask)
                    {
                        if (task.TrangThai != "Hoàn thành")
                        {
                            double diem = decisionEngine.CalculatePriority(task, mon);
                            if (diem >= 80) soTaskKhanCap++;
                        }
                    }
                }
            }

            // Nếu phát hiện có deadline rực lửa, bắn thông báo hệ thống!
            if (soTaskKhanCap > 0)
            {
                new ToastContentBuilder()
                    .AddText("🔥 CẢNH BÁO DEADLINE (Chạy ngầm)!")
                    .AddText($"Bạn đang có {soTaskKhanCap} bài tập KHẨN CẤP chưa làm!")
                    .AddText("Click vào đây để mở app và giải quyết ngay!")
                    .AddAudio(new Uri("ms-winsoundevent:Notification.Default"))
                    .Show();
            }
        }

        // MA THUẬT NẰM Ở ĐÂY: Ghi đè sự kiện khi người dùng bấm nút [X] ở góc phải
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // THÊM DÒNG NÀY: Nếu là tắt thật thì thả cửa cho đóng luôn, không chạy code bên dưới nữa
            if (_thucSuMuonTat) return;

            // 1. Chặn lại, KHÔNG cho app tắt (NẾU CHỈ BẤM NÚT X BÌNH THƯỜNG)
            e.Cancel = true;

            // 2. Giấu cửa sổ đi
            this.Hide();

            // 3. Thông báo cho người dùng khỏi hoang mang
            new ToastContentBuilder()
                .AddText("Smart Study Planner đã được thu nhỏ")
                .AddText("Trợ lý ảo vẫn đang chạy ngầm để bảo vệ deadline cho bạn!")
                .Show();
        }

        private void HienThiUngDung()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate(); // Bật nó nổi lên trên cùng
        }

        private void ThoatHoanToan()
        {
            _thucSuMuonTat = true;
            // Nhớ dọn dẹp cái icon rác dưới khay hệ thống trước khi ngắt thở
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            // ĐÃ SỬA LỖI 2: Chỉ định rõ Application của thằng WPF
            System.Windows.Application.Current.Shutdown();
        }
    }
}