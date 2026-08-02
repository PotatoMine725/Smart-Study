using Microsoft.Toolkit.Uwp.Notifications;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.ViewModels;
using SmartStudyPlanner.Views;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Threading.Tasks;

// Sử dụng alias để phân biệt các hàm của WPF và Windows Forms
using WinForms = System.Windows.Forms;

namespace SmartStudyPlanner
{
    public partial class MainWindow : Window
    {
        private WinForms.NotifyIcon _notifyIcon;
        private DispatcherTimer _backgroundTimer;
        private bool _thucSuMuonTat = false;
        private HocKy? _currentHocKy;
        private WeightOptimizerWindow? _weightOptimizerWindow;
        private readonly IStudyTelemetry _telemetry;

        public MainWindow()
        {
            InitializeComponent();
            _telemetry = ServiceLocator.Get<IStudyTelemetry>();
            this.Loaded += MainWindow_Loaded;
            MainFrame.Navigated += MainFrame_Navigated;

            // 1. Cài đặt System Tray (Khay hệ thống)
            SetupSystemTray();

            // 2. Cài đặt vòng lặp kiểm tra ngầm định kỳ
            SetupBackgroundWorker();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SetupPage());
            _telemetry.Track("app_main_window_loaded");

            // Quét một lượt ngay khi mở app. Trước đây việc này do DashboardViewModel làm
            // (toast riêng của nó), nhưng bản đó chỉ nhìn 1 học kỳ, chỉ đếm tối đa 5 task
            // và đọc DiemUuTien đã lưu — nên nó vừa trùng vừa yếu hơn. Giờ chỉ còn một
            // nguồn cảnh báo duy nhất, và nó cần chạy ở đây để người dùng không phải chờ
            // hết 5 phút mới biết mình có deadline gấp.
            //
            // Đặt ở Loaded chứ KHÔNG ở constructor: SetupBackgroundWorker() chạy trong
            // ctor, resolve ServiceLocator sớm như vậy có thể hỏng, mà try/catch bên dưới
            // sẽ nuốt vào crash.log — hỏng im lặng, nhìn y hệt như chạy tốt.
            await QuetVaCanhBaoDeadlineAsync();
        }

        private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            string page = e.Content?.GetType().Name ?? "Unknown";
            _telemetry.Track("navigate_page", new System.Collections.Generic.Dictionary<string, string> { ["page"] = page });
            if (e.Content is DashboardPage dp)
                _currentHocKy = dp.HocKy;
            else if (e.Content is AnalyticsPage ap)
                _currentHocKy = ap.HocKy;
            else if (e.Content is WorkloadBalancerPage wp)
                _currentHocKy = wp.HocKy;

            CurrentContextText.Text = _currentHocKy == null
                ? "Chưa chọn học kỳ"
                : $"Học kỳ: {_currentHocKy.Ten}";
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
            // 5 phút: vẫn kịp thời cho cảnh báo deadline, nhưng không quét lại toàn bộ học kỳ
            // (mọi HocKy × MonHoc × Task + CalculatePriority) mỗi phút. Giá trị 1 phút cũ là
            // giá trị debug, chính comment cũ đã ghi là "tạm thời để em test".
            _backgroundTimer.Interval = TimeSpan.FromMinutes(5);
            _backgroundTimer.Tick += BackgroundTimer_Tick;
            _backgroundTimer.Start();
        }

        // Chống spam toast: cùng một tình trạng khẩn cấp không réo lại trong 30 phút.
        private static readonly TimeSpan ToastCooldown = TimeSpan.FromMinutes(30);
        private DateTime _lanCanhBaoGanNhat = DateTime.MinValue;
        private int _soTaskKhanCapDaBao = 0;

        private async void BackgroundTimer_Tick(object sender, EventArgs e)
            => await QuetVaCanhBaoDeadlineAsync();

        private async Task QuetVaCanhBaoDeadlineAsync()
        {
            // async void trên DispatcherTimer: exception ở đây không ai await, nó rơi thẳng
            // vào DispatcherUnhandledException của App (App.xaml.cs:23) — mà handler đó bật
            // MessageBox. Tức là một lỗi DB thoáng qua sẽ dựng modal dialog LẶP LẠI mỗi lượt
            // tick, người dùng không thao tác được gì. Nuốt tại chỗ và ghi crash.log.
            try
            {
                var repo = ServiceLocator.Get<IHocKyRepository>();
                var decisionEngine = ServiceLocator.Get<IDecisionEngine>();

                var danhSachHocKy = await repo.LayDanhSachHocKyAsync();
                int soTaskKhanCap = 0;

                foreach (var hk in danhSachHocKy)
                {
                    foreach (var mon in hk.DanhSachMonHoc)
                    {
                        foreach (var task in mon.DanhSachTask)
                        {
                            if (task.TrangThai != StudyTaskStatus.HoanThanh)
                            {
                                double diem = decisionEngine.CalculatePriority(task, mon);
                                if (diem >= 80) soTaskKhanCap++;
                            }
                        }
                    }
                }

                if (soTaskKhanCap == 0)
                {
                    // Hết khẩn cấp -> quên trạng thái cũ, lần khẩn cấp sau được báo ngay.
                    _soTaskKhanCapDaBao = 0;
                    return;
                }

                // Báo lại sớm CHỈ khi mức khẩn cấp tăng (có task mới vượt ngưỡng — đó là tin
                // mới). Số giảm đi thì không đáng để réo, cứ chờ hết cooldown.
                bool dangKhanCapHon = soTaskKhanCap > _soTaskKhanCapDaBao;
                bool hetCooldown = DateTime.Now - _lanCanhBaoGanNhat >= ToastCooldown;
                if (!dangKhanCapHon && !hetCooldown) return;

                _lanCanhBaoGanNhat = DateTime.Now;
                _soTaskKhanCapDaBao = soTaskKhanCap;

                new ToastContentBuilder()
                    .AddText("🔥 CẢNH BÁO DEADLINE!")
                    .AddText($"Bạn đang có {soTaskKhanCap} bài tập KHẨN CẤP chưa làm!")
                    .AddText("Click vào đây để mở app và giải quyết ngay!")
                    .AddAudio(new Uri("ms-winsoundevent:Notification.Default"))
                    .Show();
            }
            catch (Exception ex)
            {
                CrashLogger.Log("MainWindow.BackgroundTimer_Tick", ex);
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
            this.WindowState = WindowState.Maximized;
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

        // ── Sidebar Navigation ──

        private void SetActiveNav(ToggleButton active)
        {
            foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload, NavAnalytics, NavWeightOptimizer })
                btn.IsChecked = false;
            active.IsChecked = true;
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHocKy == null) return;
            _telemetry.Track("click_nav_dashboard");
            SetActiveNav(NavDashboard);
            MainFrame.Navigate(new DashboardPage(_currentHocKy));
        }

        private void NavMonHoc_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHocKy == null) return;
            _telemetry.Track("click_nav_subjects");
            SetActiveNav(NavMonHoc);
            MainFrame.Navigate(new QuanLyMonHocPage(_currentHocKy));
        }

        private void NavWorkload_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHocKy == null) return;
            _telemetry.Track("click_nav_workload");
            SetActiveNav(NavWorkload);
            MainFrame.Navigate(new WorkloadBalancerPage(_currentHocKy));
        }

        private void NavWeightOptimizer_Click(object sender, RoutedEventArgs e)
        {
            _telemetry.Track("click_nav_weight_optimizer");
            NavWeightOptimizer.IsChecked = false;   // opens a window, not a nav page
            if (_weightOptimizerWindow == null || !_weightOptimizerWindow.IsLoaded)
            {
                _weightOptimizerWindow = new WeightOptimizerWindow();
                _weightOptimizerWindow.Closed += (_, _) => WeightOptimizerOpenBadge.Visibility = Visibility.Collapsed;
                _weightOptimizerWindow.Show();
                WeightOptimizerOpenBadge.Visibility = Visibility.Visible;
            }
            else
            {
                _weightOptimizerWindow.Activate();
                WeightOptimizerOpenBadge.Visibility = Visibility.Visible;
            }
        }

        private void NavAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHocKy == null) return;
            _telemetry.Track("click_nav_analytics");
            SetActiveNav(NavAnalytics);
            MainFrame.Navigate(new AnalyticsPage(_currentHocKy));
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            _telemetry.Track("click_save_sidebar");
            if (MainFrame.Content is DashboardPage dp &&
                dp.DataContext is DashboardViewModel vm)
                vm.LuuDuLieuCommand.Execute(null);
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            _telemetry.Track("click_theme_toggle");
            ThemeManager.ToggleTheme();

            // Update icon: sun for dark mode (switch to light), moon for light mode (switch to dark)
            var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            bool isDark = mergedDicts.Any(d => d.Source?.OriginalString.Contains("DarkTheme") == true);
            ThemeIcon.Text = isDark ? "" : ""; // moon vs. brightness/sun
        }
    }
}
