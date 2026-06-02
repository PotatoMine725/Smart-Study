using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Telemetry;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class QuanLyTaskViewModel : ObservableObject
    {
        public HocKy HocKyHienTai { get; set; }
        public MonHoc MonHocHienTai { get; set; }
        private StudyTask? _taskDangSua;
        private Guid? _editingTaskId;

        private readonly IHocKyRepository _hocKyRepository;
        private readonly ITaskEditorRepository _taskEditorRepository;
        private readonly IDecisionEngine _decisionEngine;
        private readonly IStudyTelemetry _telemetry;

        // 1. DỮ LIỆU HIỂN THỊ (BINDING)
        [ObservableProperty]
        private string tieuDe;

        [ObservableProperty]
        private string tenTask;

        [ObservableProperty]
        private DateTime? hanChot;

        [ObservableProperty]
        private int loaiTaskIndex = 0;

        [ObservableProperty]
        private string doKho;

        [ObservableProperty]
        private string textNutThem = "Thêm Deadline";

        [ObservableProperty]
        private string mauNutThem = "#9B59B6";

        [ObservableProperty]
        private string vanBanNhapNhanh;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool hasData = true;
        [ObservableProperty] private bool hasError;
        [ObservableProperty] private string emptyStateMessage = "Chưa có task nào cho môn học này.";
        [ObservableProperty] private string quickInputHint = "Parser chỉ tự điền thông tin cốt lõi. Hãy thêm Notes/Links ở các vùng bên dưới.";

        // M6.1 — Notes & Links
        [ObservableProperty]
        private string? noteContent;

        [ObservableProperty]
        private string newLinkTitle = string.Empty;

        [ObservableProperty]
        private string newLinkUrl = string.Empty;

        public ObservableCollection<TaskReferenceLinkItemVm> StudyLinks { get; } = [];

        // 2. LOA THÔNG BÁO CHO VIEW
        public Action OnGoBack { get; set; }
        public Action OnRefreshGrid { get; set; }

        public QuanLyTaskViewModel(HocKy hocKy, MonHoc monHoc)
            : this(hocKy, monHoc, ServiceLocator.Get<IHocKyRepository>(), ServiceLocator.Get<ITaskEditorRepository>(), ServiceLocator.Get<IDecisionEngine>(), ServiceLocator.Get<IStudyTelemetry>()) { }
        public QuanLyTaskViewModel(HocKy hocKy, MonHoc monHoc, IHocKyRepository hocKyRepository, ITaskEditorRepository taskEditorRepository, IDecisionEngine decisionEngine)
            : this(hocKy, monHoc, hocKyRepository, taskEditorRepository, decisionEngine, new NullStudyTelemetry()) { }

        public QuanLyTaskViewModel(HocKy hocKy, MonHoc monHoc, IHocKyRepository hocKyRepository, ITaskEditorRepository taskEditorRepository, IDecisionEngine decisionEngine, IStudyTelemetry telemetry)
        {
            HocKyHienTai = hocKy;
            MonHocHienTai = monHoc;
            _hocKyRepository = hocKyRepository;
            _taskEditorRepository = taskEditorRepository;
            _decisionEngine = decisionEngine;
            _telemetry = telemetry;
            TieuDe = $"QUẢN LÝ DEADLINE - MÔN {MonHocHienTai.TenMonHoc.ToUpper()}";

            TinhDiemVaSapXep();
            HasData = MonHocHienTai.DanhSachTask.Count > 0;
        }

        private void TinhDiemVaSapXep()
        {
            foreach (var task in MonHocHienTai.DanhSachTask)
            {
                task.DiemUuTien = _decisionEngine.CalculatePriority(task, MonHocHienTai);

                if (task.TrangThai == StudyTaskStatus.HoanThanh) task.MucDoCanhBao = "Đã xong";
                else if (task.DiemUuTien >= 80) task.MucDoCanhBao = "Khẩn cấp";
                else if (task.DiemUuTien >= 50) task.MucDoCanhBao = "Chú ý";
                else task.MucDoCanhBao = "An toàn";
            }

            var danhSachDaSapXep = MonHocHienTai.DanhSachTask.OrderByDescending(t => t.DiemUuTien).ToList();
            MonHocHienTai.DanhSachTask.Clear();
            foreach (var task in danhSachDaSapXep)
                MonHocHienTai.DanhSachTask.Add(task);
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
                    await _hocKyRepository.LuuHocKyAsync(HocKyHienTai);
                    HasData = MonHocHienTai.DanhSachTask.Count > 0;
                }
            }
        }

        [RelayCommand]
        private async Task HoanThanhTask(StudyTask taskDaXong)
        {
            if (taskDaXong != null && taskDaXong.TrangThai != StudyTaskStatus.HoanThanh)
            {
                taskDaXong.TrangThai = StudyTaskStatus.HoanThanh;
                TinhDiemVaSapXep();
                OnRefreshGrid?.Invoke();
                await _hocKyRepository.LuuHocKyAsync(HocKyHienTai);
            }
        }

        [RelayCommand]
        private async Task SuaTask(StudyTask taskCanSua)
        {
            if (taskCanSua == null) return;

            _taskDangSua = taskCanSua;
            _editingTaskId = taskCanSua.MaTask;
            _telemetry.Track("task_click_edit", new System.Collections.Generic.Dictionary<string, string> { ["task"] = taskCanSua.TenTask });

            TenTask = _taskDangSua.TenTask;
            HanChot = _taskDangSua.HanChot;
            LoaiTaskIndex = (int)_taskDangSua.LoaiTask;
            DoKho = _taskDangSua.DoKho.ToString();

            TextNutThem = "Cập Nhật";
            MauNutThem = "#3498DB";

            // Load notes & links for the task being edited
            var bundle = await _taskEditorRepository.GetBundleAsync(taskCanSua.MaTask);
            NoteContent = bundle?.Note?.Content;
            StudyLinks.Clear();
            if (bundle?.Links is { Count: > 0 } links)
                foreach (var l in links)
                    StudyLinks.Add(TaskReferenceLinkItemVm.FromModel(l));
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

            StudyTask savedTask;
            if (_taskDangSua == null)
            {
                savedTask = new StudyTask(TenTask, HanChot.Value, loaiTask, doKhoInt);
                MonHocHienTai.DanhSachTask.Add(savedTask);
                _telemetry.Track("task_add");
            }
            else
            {
                _taskDangSua.TenTask = TenTask;
                _taskDangSua.HanChot = HanChot.Value;
                _taskDangSua.LoaiTask = loaiTask;
                _taskDangSua.DoKho = doKhoInt;
                savedTask = _taskDangSua;

                _taskDangSua = null;
                TextNutThem = "Thêm Deadline";
                MauNutThem = "#9B59B6";
                _telemetry.Track("task_update");
            }

            TinhDiemVaSapXep();
            OnRefreshGrid?.Invoke();
            await _hocKyRepository.LuuHocKyAsync(HocKyHienTai);
            HasData = MonHocHienTai.DanhSachTask.Count > 0;

            // Save notes & links (for both new and existing tasks)
            var taskId = _editingTaskId ?? savedTask.MaTask;
            _editingTaskId = null;
            if (!string.IsNullOrEmpty(NoteContent) || StudyLinks.Count > 0)
            {
                await _taskEditorRepository.UpsertNoteAsync(taskId, NoteContent);
                foreach (var (vm, i) in StudyLinks.Select((vm, i) => (vm, i)))
                {
                    vm.SortOrder = i;
                    vm.MaTask = taskId;
                }
                var existingLinks = await _taskEditorRepository.GetLinksAsync(taskId);
                var incomingIds = StudyLinks.Select(vm => vm.Id).ToHashSet();
                foreach (var dead in existingLinks.Where(l => !incomingIds.Contains(l.Id)))
                    await _taskEditorRepository.DeleteLinkAsync(dead.Id);
                foreach (var vm in StudyLinks)
                {
                    var model = vm.ToModel();
                    if (existingLinks.Any(l => l.Id == vm.Id))
                        await _taskEditorRepository.UpdateLinkAsync(model);
                    else
                        await _taskEditorRepository.AddLinkAsync(model);
                }
            }

            // Dọn dẹp form
            TenTask = string.Empty;
            DoKho = string.Empty;
            HanChot = null;
            LoaiTaskIndex = 0;
            NoteContent = null;
            StudyLinks.Clear();
            NewLinkTitle = string.Empty;
            NewLinkUrl = string.Empty;
        }

        [RelayCommand]
        private void PhanTichNhapNhanh()
        {
            if (string.IsNullOrWhiteSpace(VanBanNhapNhanh)) return;

            var ketQua = SmartParser.Parse(VanBanNhapNhanh);

            // Parser chỉ điền vào core fields — không bao giờ chạm NoteContent/StudyLinks
            TenTask = ketQua.TenTask;
            HanChot = ketQua.HanChot;
            LoaiTaskIndex = (int)ketQua.Loai;
            DoKho = ketQua.DoKho.ToString();

            TextNutThem = "Lưu Deadline (Hãy kiểm tra lại)";
            MauNutThem = "#E67E22";
            QuickInputHint = "Đã điền nhanh xong. Tiếp theo: bổ sung ghi chú và link học tập bên dưới (nếu cần).";

            VanBanNhapNhanh = string.Empty;
        }

        // M6.1 — Note & Link commands

        [RelayCommand]
        private void AddLink()
        {
            if (string.IsNullOrWhiteSpace(NewLinkUrl)) return;
            if (!Uri.TryCreate(NewLinkUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                System.Windows.MessageBox.Show("URL chưa hợp lệ. Vui lòng nhập link bắt đầu bằng http:// hoặc https://", "URL không hợp lệ",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StudyLinks.Add(new TaskReferenceLinkItemVm
            {
                MaTask = _editingTaskId ?? Guid.Empty,
                Title = string.IsNullOrWhiteSpace(NewLinkTitle) ? uri.Host : NewLinkTitle,
                Url = uri.OriginalString,
                SortOrder = StudyLinks.Count,
            });
            _telemetry.Track("task_add_link");
            NewLinkTitle = string.Empty;
            NewLinkUrl = string.Empty;
        }

        [RelayCommand]
        private void RemoveLink(TaskReferenceLinkItemVm item) => StudyLinks.Remove(item);

        [RelayCommand]
        private void OpenLink(TaskReferenceLinkItemVm item)
        {
            if (!string.IsNullOrWhiteSpace(item?.Url))
                Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
        }

        [RelayCommand]
        private void CopyLink(TaskReferenceLinkItemVm item)
        {
            if (!string.IsNullOrWhiteSpace(item?.Url))
                System.Windows.Clipboard.SetText(item.Url);
        }

        [RelayCommand]
        private void ClearNote() => NoteContent = null;

        private sealed class NullStudyTelemetry : IStudyTelemetry
        {
            public void Track(string eventName, System.Collections.Generic.IDictionary<string, string>? properties = null) { }
        }
    }
}
