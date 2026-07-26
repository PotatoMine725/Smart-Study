using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    public class QuanLyMonHocViewModelTests
    {
        private sealed class FakeHocKyRepository : IHocKyRepository
        {
            public int LuuCallCount;

            public Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default) =>
                Task.FromResult(new List<HocKy>());

            public Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
            {
                LuuCallCount++;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task ThemMon_NormalizeEqualNameAlreadyLive_DoesNotAddSecondMonHoc()
        {
            var hocKy = new HocKy("HK1", DateTime.Today);
            hocKy.DanhSachMonHoc.Add(new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy });
            var repo = new FakeHocKyRepository();
            string? thongBao = null;
            var vm = new QuanLyMonHocViewModel(hocKy, repo)
            {
                TenMon = "toán ",
                SoTinChi = "3",
                OnThongBao = msg => thongBao = msg,
            };

            await vm.ThemMonCommand.ExecuteAsync(null);

            Assert.Single(hocKy.DanhSachMonHoc);
            Assert.Equal(0, repo.LuuCallCount);
            // Warning routed through the OnThongBao seam (View wires MessageBox), not raised
            // directly by the VM -- so it never bungs a modal dialog in a headless test run.
            Assert.NotNull(thongBao);
        }

        [Fact]
        public async Task ThemMon_DistinctName_AddsMonHocAndSaves()
        {
            var hocKy = new HocKy("HK1", DateTime.Today);
            hocKy.DanhSachMonHoc.Add(new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy });
            var repo = new FakeHocKyRepository();
            var vm = new QuanLyMonHocViewModel(hocKy, repo)
            {
                TenMon = "Lý",
                SoTinChi = "2",
            };

            await vm.ThemMonCommand.ExecuteAsync(null);

            Assert.Equal(2, hocKy.DanhSachMonHoc.Count);
            Assert.Equal(1, repo.LuuCallCount);
        }

        [Fact]
        public async Task ThemMon_NormalizeEqualToDeletedMonHoc_StillAdds()
        {
            var hocKy = new HocKy("HK1", DateTime.Today);
            hocKy.DanhSachMonHoc.Add(new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy, IsDeleted = true });
            var repo = new FakeHocKyRepository();
            var vm = new QuanLyMonHocViewModel(hocKy, repo)
            {
                TenMon = "toán",
                SoTinChi = "3",
            };

            await vm.ThemMonCommand.ExecuteAsync(null);

            Assert.Equal(2, hocKy.DanhSachMonHoc.Count);
            Assert.Equal(1, repo.LuuCallCount);
        }
    }
}
