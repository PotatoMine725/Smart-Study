using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using System;
using System.Windows.Threading;

namespace SmartStudyPlanner.ViewModels
{
    public partial class FocusViewModel : ObservableObject
    {
        private DispatcherTimer _timer;
        private int _thoiGianConLai;
        private bool _dangHoc = true;

        // BIẾN MỚI: Đếm tổng số giây THỰC TẾ đã ngồi học
        private int _tongGiayDaHoc = 0;

        public TaskDashboardItem TaskHienTai { get; set; }

        [ObservableProperty] private string tieuDeTask;
        [ObservableProperty] private string thoiGianHienThi;
        [ObservableProperty] private string trangThaiText;
        [ObservableProperty] private string mauTrangThai;
        [ObservableProperty] private string tienDoText;

        public Action OnKetThuc { get; set; }

        public FocusViewModel(TaskDashboardItem task)
        {
            TaskHienTai = task;
            TieuDeTask = $"Đang Focus: {task.TenTask} ({task.TenMonHoc})";

            ThietLapPomodoro(true);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        private void ThietLapPomodoro(bool laHoc)
        {
            _dangHoc = laHoc;
            _thoiGianConLai = laHoc ? 25 * 60 : 5 * 60;
            TrangThaiText = laHoc ? "THỜI GIAN TẬP TRUNG" : "GIẢI LAO";
            MauTrangThai = laHoc ? "#E74C3C" : "#2ECC71";
            CapNhatGiaoDienThoiGian();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _thoiGianConLai--;

            // NẾU ĐANG LÀ CHẾ ĐỘ HỌC THÌ CỘNG DỒN THỜI GIAN ĐÃ NGỒI
            if (_dangHoc)
            {
                _tongGiayDaHoc++;

                // HIỂN THỊ TIẾN ĐỘ THỰC TẾ
                int tongPhutHienTai = TaskHienTai.TaskGoc.ThoiGianDaHoc + (_tongGiayDaHoc / 60);
                TienDoText = $"Tiến độ: Đã học {tongPhutHienTai} phút";
            }

            CapNhatGiaoDienThoiGian();

            if (_thoiGianConLai <= 0)
            {
                _timer.Stop();
                System.Media.SystemSounds.Exclamation.Play();
                ThietLapPomodoro(!_dangHoc);
                _timer.Start();
            }
        }

        private void CapNhatGiaoDienThoiGian()
        {
            int phut = _thoiGianConLai / 60;
            int giay = _thoiGianConLai % 60;
            ThoiGianHienThi = $"{phut:D2}:{giay:D2}";
        }

        // --- CÁC HÀM XỬ LÝ KẾT THÚC VÀ LƯU DỮ LIỆU ---
        private void LuuThoiGianThucTe()
        {
            int phutDaHoc = _tongGiayDaHoc / 60;
            if (phutDaHoc > 0)
            {
                // Cộng dồn vào task gốc (để nếu học nhiều lần thì vẫn cộng dồn)
                TaskHienTai.TaskGoc.ThoiGianDaHoc += phutDaHoc;
                // 🔥 KÍCH HOẠT STREAK
                Services.StreakManager.UpdateStreak();
            }
        }

        [RelayCommand] private void BatDau() => _timer.Start();
        [RelayCommand] private void TamDung() => _timer.Stop();

        [RelayCommand]
        private void HoanThanh()
        {
            _timer.Stop();
            LuuThoiGianThucTe();

            // Tự động đánh dấu Hoàn thành cho rảnh tay
            TaskHienTai.TaskGoc.TrangThai = "Hoàn thành";

            OnKetThuc?.Invoke();
        }

        // LỆNH MỚI: Chỉ lưu thời gian, không đánh dấu hoàn thành
        [RelayCommand]
        private void ThoatKhanCap()
        {
            _timer.Stop();
            LuuThoiGianThucTe();
            OnKetThuc?.Invoke();
        }
    }
}