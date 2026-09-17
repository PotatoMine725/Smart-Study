using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System;
using System.Collections.ObjectModel;

namespace SmartStudyPlanner.ViewModels
{
    public partial class WorkloadBalancerViewModel : ObservableObject
    {
        private readonly HocKy _hocKy;
        private readonly IWorkloadService _workloadService;
        private readonly Action<string> _notify;

        /// <summary>Mức sức học slider đang trỏ tới. Đổi ngay khi người dùng kéo.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsScheduleStale))]
        private double capacityHours;

        /// <summary>
        /// Mức sức học mà <see cref="Schedule"/> hiện tại THỰC SỰ được dựng bằng. Biểu đồ và
        /// meter phải đo theo giá trị này, không phải theo <see cref="CapacityHours"/>: một
        /// property phục vụ hai vai chính là lỗi gốc — kéo slider chỉ chạy lại converter, nên
        /// màn hình vẽ phân bổ CŨ đo bằng mức trần MỚI.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsScheduleStale))]
        private double renderedCapacityHours;

        [ObservableProperty] private ObservableCollection<ScheduleDay> schedule = new();

        /// <summary>
        /// Biểu đồ đang mô tả một mức sức học khác mức slider đang trỏ tới. 0.01 là chặn
        /// so-sánh-float, không phải ngưỡng: slider snap theo tick nguyên (IsSnapToTickEnabled,
        /// TickFrequency=1) nên chênh lệch thật nhỏ nhất là 1.0.
        /// </summary>
        public bool IsScheduleStale => Math.Abs(CapacityHours - RenderedCapacityHours) > 0.01;

        // Constructor mặc định — resolve từ DI
        public WorkloadBalancerViewModel(HocKy hocKy)
            : this(hocKy, ServiceLocator.Get<IWorkloadService>()) { }

        // Constructor có injection — dùng cho unit test.
        // notify: seam để test chạy headless; mặc định giữ nguyên MessageBox, không đổi UX.
        public WorkloadBalancerViewModel(
            HocKy hocKy,
            IWorkloadService workloadService,
            Action<string>? notify = null)
        {
            _hocKy = hocKy;
            _workloadService = workloadService;
            _notify = notify ?? (m => System.Windows.MessageBox.Show(m, "Workload Balancer"));
            CapacityHours = _workloadService.GetCapacity();
            BuildSchedule(notify: false);   // khởi tạo: không popup, tránh modal mỗi lần nav
        }

        [RelayCommand]
        private void GenerateSchedule() => BuildSchedule(notify: true);

        private void BuildSchedule(bool notify)
        {
            _workloadService.SaveCapacity(CapacityHours);

            var generatedList = _workloadService.GenerateSchedule(_hocKy, CapacityHours);

            Schedule.Clear();
            foreach (var day in generatedList)
            {
                if (day.Tasks.Count > 0) Schedule.Add(day);
            }

            // Sau điểm này biểu đồ mới được phép đo theo mức vừa dùng.
            RenderedCapacityHours = CapacityHours;

            if (notify)
                _notify($"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!");
        }
    }
}
