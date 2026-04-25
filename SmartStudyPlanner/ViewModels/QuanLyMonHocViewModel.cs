using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using System;
using System.Windows;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class QuanLyMonHocViewModel : ObservableObject
    {
        // Biến lưu trữ gốc
        public HocKy HocKyHienTai { get; set; }
        private MonHoc? _monDangSua;

        // Repository để tương tác với dữ liệu (nếu cần)
        private readonly IStudyRepository _repository = new StudyRepository();

        // 1. DỮ LIỆU HIỂN THỊ TRÊN UI
        [ObservableProperty]
        private string tieuDe;

        [ObservableProperty]
        private string tenMon;

        [ObservableProperty]
        private string soTinChi;

        [ObservableProperty]
        private string textNutThem = "Thêm Môn"; // Mặc định là Thêm

        [ObservableProperty]
        private string mauNutThem = "#2ECC71"; // Mặc định màu Xanh lá

        // 2. CÁC LOA THÔNG BÁO CHO VIEW
        public Action OnGoBack { get; set; }
        public Action<HocKy, MonHoc> OnNavigateToTask { get; set; }
        public Action OnRefreshGrid { get; set; } // Dùng để ép DataGrid vẽ lại khi Sửa xong

        public QuanLyMonHocViewModel(HocKy hocKy)
        {
            HocKyHienTai = hocKy;
            TieuDe = $"DANH SÁCH MÔN HỌC - {HocKyHienTai.Ten.ToUpper()}";
        }

        // 3. CÁC LỆNH (COMMANDS)
        [RelayCommand]
        private void QuayLai() => OnGoBack?.Invoke();

        [RelayCommand]
        private void XemTask(MonHoc monDuocChon)
        {
            if (monDuocChon != null)
            {
                OnNavigateToTask?.Invoke(HocKyHienTai, monDuocChon);
            }
        }

        [RelayCommand]
        private void SuaMon(MonHoc monDuocChon)
        {
            if (monDuocChon != null)
            {
                _monDangSua = monDuocChon;

                // Bơm dữ liệu lên 2 ô TextBox
                TenMon = _monDangSua.TenMonHoc;
                SoTinChi = _monDangSua.SoTinChi.ToString();

                // Phép màu MVVM: Tự động biến hình nút bấm
                TextNutThem = "Cập Nhật";
                MauNutThem = "#3498DB"; // Đổi sang Xanh dương
            }
        }

        [RelayCommand]
        private async Task XoaMon(MonHoc monCanXoa)
        {
            if (monCanXoa != null)
            {
                if (System.Windows.MessageBox.Show($"Xóa môn '{monCanXoa.TenMonHoc}'?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    HocKyHienTai.DanhSachMonHoc.Remove(monCanXoa);
                    await _repository.LuuHocKyAsync(HocKyHienTai); // Đã đổi sang Async
                }
            }
        }

        [RelayCommand]
        private async Task ThemMon()
        {
            if (string.IsNullOrWhiteSpace(TenMon)) return;
            int tinChi = int.TryParse(SoTinChi, out int tc) ? tc : -1;
            if (tinChi <= 0) return;

            if (_monDangSua == null)
            {
                HocKyHienTai.DanhSachMonHoc.Add(new MonHoc(TenMon, tinChi));
            }
            else
            {
                _monDangSua.TenMonHoc = TenMon;
                _monDangSua.SoTinChi = tinChi;
                OnRefreshGrid?.Invoke();

                _monDangSua = null;
                TextNutThem = "Thêm Môn";
                MauNutThem = "#2ECC71";
            }

            await _repository.LuuHocKyAsync(HocKyHienTai); // Đã đổi sang Async

            TenMon = string.Empty;
            SoTinChi = string.Empty;
        }
    }
}