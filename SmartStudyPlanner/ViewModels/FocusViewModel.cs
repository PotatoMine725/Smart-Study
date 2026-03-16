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
        private int _thoiGianConLai; // Tính bằng giây
        private bool _dangHoc = true; // true = 25p học, false = 5p nghỉ

        public TaskDashboardItem TaskHienTai { get; set; }

        [ObservableProperty]
        private string tieuDeTask;

        [ObservableProperty]
        private string thoiGianHienThi;

        [ObservableProperty]
        private string trangThaiText;

        [ObservableProperty]
        private string mauTrangThai;

        public Action OnKetThuc { get; set; }

        public FocusViewModel(TaskDashboardItem task)
        {
            TaskHienTai = task;
            TieuDeTask = $"Đang Focus: {task.TenTask} ({task.TenMonHoc})";

            ThiếtLậpPomodoro(true);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        private void ThiếtLậpPomodoro(bool laHoc)
        {
            _dangHoc = laHoc;
            _thoiGianConLai = laHoc ? 25 * 60 : 5 * 60; // 25p học hoặc 5p nghỉ
            TrangThaiText = laHoc ? "THỜI GIAN TẬP TRUNG" : "GIẢI LAO";
            MauTrangThai = laHoc ? "#E74C3C" : "#2ECC71"; // Đỏ lúc học, Xanh lúc nghỉ
            CậpNhậtGiaoDiệnThờiGian();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _thoiGianConLai--;
            CậpNhậtGiaoDiệnThờiGian();

            if (_thoiGianConLai <= 0)
            {
                _timer.Stop();
                // Hết giờ -> Kêu bíp và Đổi trạng thái
                System.Media.SystemSounds.Exclamation.Play();
                ThiếtLậpPomodoro(!_dangHoc); // Đảo ngược trạng thái
                _timer.Start();
            }
        }

        private void CậpNhậtGiaoDiệnThờiGian()
        {
            int phut = _thoiGianConLai / 60;
            int giay = _thoiGianConLai % 60;
            ThoiGianHienThi = $"{phut:D2}:{giay:D2}";
        }

        [RelayCommand]
        private void BatDau() => _timer.Start();

        [RelayCommand]
        private void TamDung() => _timer.Stop();

        [RelayCommand]
        private void HoanThanh()
        {
            _timer.Stop();
            OnKetThuc?.Invoke();
        }
    }
}