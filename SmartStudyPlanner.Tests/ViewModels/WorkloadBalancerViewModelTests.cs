using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// Ghim việc tách hai vai của sức học: <c>CapacityHours</c> là mức slider đang trỏ tới,
    /// <c>RenderedCapacityHours</c> là mức mà <c>Schedule</c> hiện tại THỰC SỰ được dựng bằng.
    /// Bất biến: biểu đồ nói thật đúng khi <c>IsScheduleStale</c> false.
    /// Xem docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md §4.1.
    /// </summary>
    public sealed class WorkloadBalancerViewModelTests
    {
        /// <summary>Fake ghi lại lời gọi. Chỉ dùng trong file này nên khai báo inline
        /// theo quy ước test-doubles của repo.</summary>
        private sealed class RecordingWorkloadService : IWorkloadService
        {
            public double StoredCapacity = 3.0;
            public readonly List<double> SaveCapacityCalls = new();
            public readonly List<double> GenerateScheduleCalls = new();

            public double GetCapacity() => StoredCapacity;

            public void SaveCapacity(double capacity) => SaveCapacityCalls.Add(capacity);

            public List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours)
            {
                GenerateScheduleCalls.Add(capacityHours);
                return new List<ScheduleDay>
                {
                    new ScheduleDay
                    {
                        Date = new DateTime(2026, 8, 10),
                        DisplayName = "T2 10/08",
                        TotalMinutes = (int)(capacityHours * 60),
                        Tasks = { new ScheduledTask { TenTask = "T-A", TenMon = "Toán", SoPhut = 60 } },
                    },
                };
            }
        }

        private static (WorkloadBalancerViewModel Vm, RecordingWorkloadService Svc, List<string> Notified)
            Sut(double stored = 3.0)
        {
            var svc = new RecordingWorkloadService { StoredCapacity = stored };
            var notified = new List<string>();
            var vm = new WorkloadBalancerViewModel(
                new HocKy("HK1", DateTime.Today), svc, notified.Add);
            return (vm, svc, notified);
        }

        [Fact]
        public void Constructor_LichVuaDungXong_KhongBaoStale()
        {
            var (vm, svc, notified) = Sut(stored: 5.0);

            Assert.Equal(5.0, vm.CapacityHours);
            Assert.Equal(5.0, vm.RenderedCapacityHours);
            Assert.False(vm.IsScheduleStale);
            Assert.Single(svc.GenerateScheduleCalls);
            Assert.Empty(notified);   // mở trang không bung dialog
        }

        [Fact]
        public void DoiCapacityHours_ChuaBamXepLai_BaoStaleVaGiuNguyenRendered()
        {
            var (vm, _, _) = Sut(stored: 3.0);

            vm.CapacityHours = 6.0;

            Assert.Equal(3.0, vm.RenderedCapacityHours);
            Assert.True(vm.IsScheduleStale);
        }

        [Fact]
        public void GenerateScheduleCommand_DungLaiLichBangMucMoi_VaTatStale()
        {
            var (vm, svc, notified) = Sut(stored: 3.0);
            vm.CapacityHours = 6.0;

            vm.GenerateScheduleCommand.Execute(null);

            Assert.Equal(6.0, svc.GenerateScheduleCalls.Last());
            Assert.Equal(6.0, svc.SaveCapacityCalls.Last());
            Assert.Equal(6.0, vm.RenderedCapacityHours);
            Assert.False(vm.IsScheduleStale);
            Assert.Single(notified);
        }

        [Fact]
        public void DoiCapacityHours_KhongTuDongXepLaiVaKhongLuu()
        {
            // Ghim quyết định D1: KHÔNG auto-reschedule khi kéo slider. SaveCapacity chạm đĩa
            // và GenerateSchedule kéo theo write-through DiemUuTien (CP-2) xuống database —
            // không đặt hai thứ đó sau một cử chỉ kéo.
            var (vm, svc, _) = Sut(stored: 3.0);
            int genTruoc = svc.GenerateScheduleCalls.Count;
            int saveTruoc = svc.SaveCapacityCalls.Count;

            vm.CapacityHours = 7.0;

            Assert.Equal(genTruoc, svc.GenerateScheduleCalls.Count);
            Assert.Equal(saveTruoc, svc.SaveCapacityCalls.Count);
        }

        [Fact]
        public void DoiCapacityHours_PhatPropertyChangedChoIsScheduleStale()
        {
            // IsScheduleStale là property tính toán, không phải [ObservableProperty]; thông báo
            // đổi của nó đến từ [NotifyPropertyChangedFor] trên hai field nguồn. Mất attribute
            // đó thì badge không bao giờ hiện trong app thật — mà 4 test trên vẫn xanh, vì
            // chúng đọc thẳng property chứ không nghe thông báo.
            var (vm, _, _) = Sut(stored: 3.0);
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.CapacityHours = 4.0;

            Assert.Contains(nameof(WorkloadBalancerViewModel.IsScheduleStale), raised);
        }
    }
}
