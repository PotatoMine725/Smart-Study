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

    public class StudyTask
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
        public string MucDoCanhBao { get; set; }
        public int DoKho { get; set; }

        public int ThoiGianDaHoc { get; set; } = 0;

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