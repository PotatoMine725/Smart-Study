using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System.Collections.ObjectModel;

namespace SmartStudyPlanner.ViewModels
{
    public partial class WorkloadBalancerViewModel : ObservableObject
    {
        private readonly HocKy _hocKy;
        private readonly IWorkloadService _workloadService;

        [ObservableProperty] private double capacityHours;
        [ObservableProperty] private ObservableCollection<ScheduleDay> schedule = new();

        // Constructor mặc định — resolve từ DI
        public WorkloadBalancerViewModel(HocKy hocKy)
            : this(hocKy, ServiceLocator.Get<IWorkloadService>()) { }

        // Constructor có injection — dùng cho unit test
        public WorkloadBalancerViewModel(HocKy hocKy, IWorkloadService workloadService)
        {
            _hocKy = hocKy;
            _workloadService = workloadService;
            CapacityHours = _workloadService.GetCapacity();
            GenerateSchedule();
        }

        [RelayCommand]
        private void GenerateSchedule()
        {
            _workloadService.SaveCapacity(CapacityHours);

            var generatedList = _workloadService.GenerateSchedule(_hocKy, CapacityHours);

            Schedule.Clear();
            foreach (var day in generatedList)
            {
                if (day.Tasks.Count > 0) Schedule.Add(day);
            }

            System.Windows.MessageBox.Show(
                $"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!",
                "Workload Balancer");
        }
    }
}