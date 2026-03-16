using System;

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
        public Guid MaTask { get; set; }

        // KHÓA NGOẠI (Móc vào MonHoc)
        public Guid MaMonHoc { get; set; }

        public string TenTask { get; set; }
        public DateTime HanChot { get; set; }
        public string TrangThai { get; set; }
        public LoaiCongViec LoaiTask { get; set; }
        public double DiemUuTien { get; set; }
        public string MucDoCanhBao { get; set; }
        public int DoKho { get; set; }

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
            TrangThai = "Chưa làm";
        }

        public double LayHeSoQuanTrong()
        {
            switch (LoaiTask)
            {
                case LoaiCongViec.ThiCuoiKy: return 1.0;
                case LoaiCongViec.DoAnCuoiKy: return 0.8;
                case LoaiCongViec.ThiGiuaKy: return 0.6;
                case LoaiCongViec.KiemTraThuongXuyen: return 0.3;
                case LoaiCongViec.BaiTapVeNha: return 0.1;
                default: return 0.1;
            }
        }
    }
}