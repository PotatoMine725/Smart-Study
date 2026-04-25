using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class QuanLyTaskViewModel : ObservableObject
    {
        public HocKy HocKyHienTai { get; set; }
        public MonHoc MonHocHienTai { get; set; }
        private StudyTask _taskDangSua = null;

        // Repository để tương tác với dữ liệu (nếu cần)
        private readonly IStudyRepository _repository;
        private readonly IDecisionEngine _decisionEngine;

        // 1. DỮ LIỆU HIỂN THỊ (BINDING)
        [ObservableProperty]
        private string tieuDe;

        [ObservableProperty]
        private string tenTask;

        [ObservableProperty]
        private DateTime? hanChot;

        [ObservableProperty]
        private int loaiTaskIndex = 0; // Để lấy SelectedIndex của ComboBox

        [ObservableProperty]
        private string doKho;

        [ObservableProperty]
        private string textNutThem = "Thêm Deadline";

        [ObservableProperty]
        private string mauNutThem = "#9B59B6"; // Màu Tím

        [ObservableProperty]
        private string vanBanNhapNhanh; // Chuỗi người dùng copy/paste vào

        // 2. LOA THÔNG BÁO CHO VIEW
        public Action OnGoBack { get; set; }
        public Action OnRefreshGrid { get; set; }

        public QuanLyTaskViewModel(HocKy hocKy, MonHoc monHoc)
            : this(hocKy, monHoc, ServiceLocator.Get<IStudyRepository>(), ServiceLocator.Get<IDecisionEngine>()) { }

        public QuanLyTaskViewModel(HocKy hocKy, MonHoc monHoc, IStudyRepository repository, IDecisionEngine decisionEngine)
        {
            HocKyHienTai = hocKy;
            MonHocHienTai = monHoc;
            _repository = repository;
            _decisionEngine = decisionEngine;
            TieuDe = $"QUẢN LÝ DEADLINE - MÔN {MonHocHienTai.TenMonHoc.ToUpper()}";

            TinhDiemVaSapXep();
        }

        // HÀM TÍNH ĐIỂM VÀ SẮP XẾP (Được gọi lại mỗi khi Thêm/Sửa/Hoàn thành Task)
        private void TinhDiemVaSapXep()
        {
            foreach (var task in MonHocHienTai.DanhSachTask)
            {
                task.DiemUuTien = _decisionEngine.CalculatePriority(task, MonHocHienTai);

                if (task.TrangThai == "Hoàn thành") task.MucDoCanhBao = "Đã xong";
                else if (task.DiemUuTien >= 80) task.MucDoCanhBao = "Khẩn cấp";
                else if (task.DiemUuTien >= 50) task.MucDoCanhBao = "Chú ý";
                else task.MucDoCanhBao = "An toàn";
            }

            var danhSachDaSapXep = MonHocHienTai.DanhSachTask.OrderByDescending(t => t.DiemUuTien).ToList();
            MonHocHienTai.DanhSachTask.Clear();
            foreach (var task in danhSachDaSapXep)
            {
                MonHocHienTai.DanhSachTask.Add(task);
            }
        }

        // 3. CÁC NÚT BẤM (COMMANDS)
        [RelayCommand]
        private void QuayLai() => OnGoBack?.Invoke();

        [RelayCommand]
        private async Task XoaTask(StudyTask taskCanXoa)
        {
            if (taskCanXoa != null)
            {
                if (System.Windows.MessageBox.Show($"Xóa bài tập '{taskCanXoa.TenTask}'?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    MonHocHienTai.DanhSachTask.Remove(taskCanXoa);
                    await _repository.LuuHocKyAsync(HocKyHienTai); // Lưu DB không giật lag
                }
            }
        }

        [RelayCommand]
        private async Task HoanThanhTask(StudyTask taskDaXong)
        {
            if (taskDaXong != null && taskDaXong.TrangThai != "Hoàn thành")
            {
                taskDaXong.TrangThai = "Hoàn thành";
                TinhDiemVaSapXep();
                OnRefreshGrid?.Invoke();
                await _repository.LuuHocKyAsync(HocKyHienTai); // Lưu DB không giật lag
            }
        }

        [RelayCommand]
        private void SuaTask(StudyTask taskCanSua)
        {
            if (taskCanSua != null)
            {
                _taskDangSua = taskCanSua;

                TenTask = _taskDangSua.TenTask;
                HanChot = _taskDangSua.HanChot;
                LoaiTaskIndex = (int)_taskDangSua.LoaiTask;
                DoKho = _taskDangSua.DoKho.ToString();

                TextNutThem = "Cập Nhật";
                MauNutThem = "#3498DB"; // Xanh dương
            }
        }

        [RelayCommand]
        private async Task ThemTask()
        {
            if (string.IsNullOrWhiteSpace(TenTask) || HanChot == null)
            {
                System.Windows.MessageBox.Show("Vui lòng nhập Tên bài tập và Hạn chót!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int doKhoInt = int.TryParse(DoKho, out int parsedDoKho) ? parsedDoKho : 1;
            if (doKhoInt < 1 || doKhoInt > 5) doKhoInt = 1;

            LoaiCongViec loaiTask = (LoaiCongViec)LoaiTaskIndex;

            if (_taskDangSua == null)
            {
                StudyTask taskMoi = new StudyTask(TenTask, HanChot.Value, loaiTask, doKhoInt);
                MonHocHienTai.DanhSachTask.Add(taskMoi);
            }
            else
            {
                _taskDangSua.TenTask = TenTask;
                _taskDangSua.HanChot = HanChot.Value;
                _taskDangSua.LoaiTask = loaiTask;
                _taskDangSua.DoKho = doKhoInt;

                _taskDangSua = null;
                TextNutThem = "Thêm Deadline";
                MauNutThem = "#9B59B6"; // Tím
            }

            TinhDiemVaSapXep();
            OnRefreshGrid?.Invoke();
            await _repository.LuuHocKyAsync(HocKyHienTai);

            // Dọn dẹp form
            TenTask = string.Empty;
            DoKho = string.Empty;
            HanChot = null;
            LoaiTaskIndex = 0;
        }

        [RelayCommand]
        private void PhanTichNhapNhanh()
        {
            if (string.IsNullOrWhiteSpace(VanBanNhapNhanh)) return;

            // Truyền câu nói lộn xộn vào cho SmartParser xử lý
            var ketQua = SmartParser.Parse(VanBanNhapNhanh);

            // Bơm kết quả vào lại giao diện để người dùng kiểm tra (Review)
            TenTask = ketQua.TenTask;
            HanChot = ketQua.HanChot;
            LoaiTaskIndex = (int)ketQua.Loai;
            DoKho = ketQua.DoKho.ToString();

            // Nhấn mạnh nút Thêm để nhắc họ lưu
            TextNutThem = "Lưu Deadline (Hãy kiểm tra lại)";
            MauNutThem = "#E67E22"; // Màu cam nổi bật

            VanBanNhapNhanh = string.Empty; // Xóa ô nhập nhanh
        }
    }
}