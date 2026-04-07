using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services; // Nhúng thư viện Dịch vụ vừa tạo
using System.Collections.ObjectModel;

namespace SmartStudyPlanner.ViewModels
{
    public partial class WorkloadBalancerViewModel : ObservableObject
    {
        private HocKy _hocKy;

        [ObservableProperty] private double capacityHours;
        [ObservableProperty] private ObservableCollection<ScheduleDay> schedule = new ObservableCollection<ScheduleDay>();

        public WorkloadBalancerViewModel(HocKy hocKy)
        {
            _hocKy = hocKy;
            CapacityHours = WorkloadService.GetCapacity(); // Lấy lại số lần trước đã lưu
            GenerateSchedule();
        }

        [RelayCommand]
        private void GenerateSchedule()
        {
            WorkloadService.SaveCapacity(CapacityHours); // Lưu lại mỗi khi bấm nút

            var generatedList = WorkloadService.GenerateSchedule(_hocKy, CapacityHours);

            Schedule.Clear();
            foreach (var day in generatedList)
            {
                if (day.Tasks.Count > 0) Schedule.Add(day);
            }

            System.Windows.MessageBox.Show($"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!", "Workload Balancer");
        }
    }
}