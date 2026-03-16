using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System;
using System.Linq;
using System.Windows;

namespace SmartStudyPlanner.ViewModels
{
    public partial class QuanLyTaskViewModel : ObservableObject
    {
        public HocKy HocKyHienTai { get; set; }
        public MonHoc MonHocHienTai { get; set; }
        private StudyTask _taskDangSua = null;

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

        // 2. LOA THÔNG BÁO CHO VIEW
        public Action OnGoBack { get; set; }
        public Action OnRefreshGrid { get; set; }

        public QuanLyTaskViewModel(HocKy hocKy, MonHoc monHoc)
        {
            HocKyHienTai = hocKy;
            MonHocHienTai = monHoc;
            TieuDe = $"QUẢN LÝ DEADLINE - MÔN {MonHocHienTai.TenMonHoc.ToUpper()}";

            TinhDiemVaSapXep();
        }

        // HÀM TÍNH ĐIỂM VÀ SẮP XẾP (Được gọi lại mỗi khi Thêm/Sửa/Hoàn thành Task)
        private void TinhDiemVaSapXep()
        {
            foreach (var task in MonHocHienTai.DanhSachTask)
            {
                task.DiemUuTien = DecisionEngine.CalculatePriority(task, MonHocHienTai);

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
        private void XoaTask(StudyTask taskCanXoa)
        {
            if (taskCanXoa != null)
            {
                if (MessageBox.Show($"Xóa bài tập '{taskCanXoa.TenTask}'?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    MonHocHienTai.DanhSachTask.Remove(taskCanXoa);
                    DataManager.LuuHocKy(HocKyHienTai); // Auto-save
                }
            }
        }

        [RelayCommand]
        private void HoanThanhTask(StudyTask taskDaXong)
        {
            if (taskDaXong != null && taskDaXong.TrangThai != "Hoàn thành")
            {
                taskDaXong.TrangThai = "Hoàn thành";
                TinhDiemVaSapXep();
                OnRefreshGrid?.Invoke();
                DataManager.LuuHocKy(HocKyHienTai); // Auto-save
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
        private void ThemTask()
        {
            if (string.IsNullOrWhiteSpace(TenTask) || HanChot == null)
            {
                MessageBox.Show("Vui lòng nhập Tên bài tập và Hạn chót!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            DataManager.LuuHocKy(HocKyHienTai); // Auto-save

            // Dọn dẹp form
            TenTask = string.Empty;
            DoKho = string.Empty;
            HanChot = null;
            LoaiTaskIndex = 0;
        }
    }
}