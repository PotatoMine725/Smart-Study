using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartStudyPlanner.Models
{
    public class HocKy : ISyncMetadata
    {
        // KHÓA CHÍNH
        [Key] public Guid MaHocKy { get; set; }

        public string Ten { get; set; }
        public DateTime NgayBatDau { get; set; }

        [NotMapped]
        public DateTime NgayKetThuc { get; set; }

        [NotMapped]
        public bool IsNgayKetThucAuto { get; set; } = true;

        public bool IsSeeded { get; set; } = false;

        // D-I sync metadata (Epic 1 / M1.2, T1.1) — stamped by SyncStamper.
        public long Rev { get; set; }
        public DateTime ModifiedAtUtc { get; set; }
        public string ModifiedByDeviceId { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public ObservableCollection<MonHoc> DanhSachMonHoc { get; set; }

        public HocKy()
        {
            MaHocKy = Guid.NewGuid();
            DanhSachMonHoc = new ObservableCollection<MonHoc>();
        }

        public HocKy(string ten, DateTime ngayBatDau)
        {
            MaHocKy = Guid.NewGuid();
            Ten = ten;
            NgayBatDau = ngayBatDau;
            NgayKetThuc = ngayBatDau.AddDays(150);
            IsNgayKetThucAuto = true;
            DanhSachMonHoc = new ObservableCollection<MonHoc>();
        }    }
}