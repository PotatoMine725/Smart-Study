using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models
{
    public class MonHoc
    {
        // KHÓA CHÍNH
        [Key] public Guid MaMonHoc { get; set; }

        // KHÓA NGOẠI (Móc vào HocKy)
        public Guid MaHocKy { get; set; }

        public string TenMonHoc { get; set; }
        public int SoTinChi { get; set; }

        public ObservableCollection<StudyTask> DanhSachTask { get; set; }

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