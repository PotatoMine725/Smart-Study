using System;
using System.ComponentModel.DataAnnotations;
namespace SmartStudyPlanner.Models
{
    public enum LoaiCongViec
    {
        BaiTapVeNha,
        KiemTraThuongXuyen,
        ThiGiuaKy,
        DoAnCuoiKy,
        ThiCuoiKy
    }

    public class StudyTask : ISyncMetadata
    {
        // KHÓA CHÍNH
        [Key] public Guid MaTask { get; set; }

        // KHÓA NGOẠI (Móc vào MonHoc)
        public Guid MaMonHoc { get; set; }

        public string TenTask { get; set; }
        public DateTime HanChot { get; set; }
        public string TrangThai { get; set; }
        public LoaiCongViec LoaiTask { get; set; }
        public double DiemUuTien { get; set; }
        // NOT NULL trong schema. VM stamp lại giá trị thật qua TinhDiemVaSapXep()
        // (QuanLyTaskViewModel:107-110); default ở đây để mọi write path KHÔNG qua UI
        // — sync-apply, import, test — không đâm vào SQLite Error 19.
        public string MucDoCanhBao { get; set; } = "An toàn";
        public int DoKho { get; set; }

        public int ThoiGianDaHoc { get; set; } = 0;
        public DateTime? NgayHoanThanh { get; set; }

        // D-I sync metadata (Epic 1 / M1.2, T1.1) — stamped by SyncStamper.
        public long Rev { get; set; }
        public DateTime ModifiedAtUtc { get; set; }
        public string ModifiedByDeviceId { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public StudyTask()
        {
            MaTask = Guid.NewGuid();
        }

        public StudyTask(string tenTask, DateTime hanChot, LoaiCongViec loaiTask, int doKho)
        {
            MaTask = Guid.NewGuid();
            TenTask = tenTask;
            HanChot = hanChot;
            LoaiTask = loaiTask;
            DoKho = doKho;
            TrangThai = StudyTaskStatus.ChuaLam;
        }     
    }
}