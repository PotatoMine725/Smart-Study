using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models
{
    public class MonHoc : ISyncMetadata
    {
        // KHÓA CHÍNH
        [Key] public Guid MaMonHoc { get; set; }

        // KHÓA NGOẠI (Móc vào HocKy)
        public Guid MaHocKy { get; set; }

        public string TenMonHoc { get; set; }
        public int SoTinChi { get; set; }

        public ObservableCollection<StudyTask> DanhSachTask { get; set; }

        // D-I sync metadata (Epic 1 / M1.2, T1.1) — stamped by SyncStamper.
        public long Rev { get; set; }
        public DateTime ModifiedAtUtc { get; set; }
        public string ModifiedByDeviceId { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public MonHoc()
        {
            MaMonHoc = Guid.NewGuid();
            DanhSachTask = new ObservableCollection<StudyTask>();
        }

        public MonHoc(string tenMonHoc, int soTinChi)
        {
            MaMonHoc = Guid.NewGuid();
            TenMonHoc = tenMonHoc;
            SoTinChi = soTinChi;
            DanhSachTask = new ObservableCollection<StudyTask>();
        }
    }
}