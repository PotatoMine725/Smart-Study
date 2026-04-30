using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace SmartStudyPlanner.ViewModels
{
    public partial class SetupViewModel : ObservableObject
    {
        private readonly IStudyRepository _repository = new StudyRepository();

        [ObservableProperty]
        private string tenHocKy;

        [ObservableProperty]
        private DateTime? ngayBatDau = DateTime.Now;

        [ObservableProperty]
        private DateTime? ngayKetThuc;

        [ObservableProperty]
        private bool isNgayKetThucAuto = true;
        // DANH SÁCH HỌC KỲ CŨ ĐỂ CHỌN
        [ObservableProperty]
        private ObservableCollection<HocKy> danhSachHocKyCu = new ObservableCollection<HocKy>();

        [ObservableProperty]
        private HocKy hocKyDuocChon;

        [ObservableProperty]
        private bool coHocKyCu = false;

        public Action<HocKy> OnSetupCompleted { get; set; }

        public SetupViewModel()
        {
            TaiDanhSachHocKy();
            CapNhatNgayKetThucMacDinh();
        }

        private async void TaiDanhSachHocKy()
        {
            var list = await _repository.LayDanhSachHocKyAsync();
            foreach (var hk in list)
            {
                DanhSachHocKyCu.Add(hk);
            }

            if (DanhSachHocKyCu.Count > 0)
            {
                CoHocKyCu = true;
                HocKyDuocChon = DanhSachHocKyCu[0]; // Chọn sẵn mục đầu tiên
            }
        }

        private void CapNhatNgayKetThucMacDinh()
        {
            if (NgayBatDau.HasValue)
            {
                if (IsNgayKetThucAuto || NgayKetThuc == null)
                {
                    NgayKetThuc = NgayBatDau.Value.AddDays(150);
                    IsNgayKetThucAuto = true;
                }
            }
        }

        [RelayCommand]
        private async Task TaoHocKy()
        {
            if (string.IsNullOrWhiteSpace(TenHocKy) || NgayBatDau == null || NgayKetThuc == null)
            {
                System.Windows.MessageBox.Show("Vui lòng nhập đầy đủ tên học kỳ, ngày bắt đầu và ngày kết thúc", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            HocKy hocKyMoi = new HocKy(TenHocKy, NgayBatDau.Value)
            {
                NgayKetThuc = NgayKetThuc.Value,
                IsNgayKetThucAuto = IsNgayKetThucAuto
            };

            await _repository.LuuHocKyAsync(hocKyMoi); // Lưu DB ngay lập tức

            OnSetupCompleted?.Invoke(hocKyMoi);
        }

        [RelayCommand]
        private void TiepTucHocKyCu()
        {
            if (HocKyDuocChon != null)
            {
                OnSetupCompleted?.Invoke(HocKyDuocChon);
            }
        }

        partial void OnNgayBatDauChanged(DateTime? value)
        {
            if (IsNgayKetThucAuto)
                CapNhatNgayKetThucMacDinh();
        }

        partial void OnNgayKetThucChanged(DateTime? value)
        {
            if (value.HasValue && NgayBatDau.HasValue)
            {
                IsNgayKetThucAuto = value.Value.Date == NgayBatDau.Value.AddDays(150).Date;
            }
        }

        [RelayCommand]
        private void TuDongTinhLai()
        {
            IsNgayKetThucAuto = true;
            CapNhatNgayKetThucMacDinh();
        }
    }
}