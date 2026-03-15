using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;


namespace SmartStudyPlanner
{
    public class MonHoc
    {
        public string TenMonHoc { get; set; }
        public int SoTinChi { get; set; }
        public Guid MaMonHoc { get; set; }

        public ObservableCollection<StudyTask> DanhSachTask { get; set; }

        public MonHoc()
        {
            // SỬA LỖI Ở ĐÂY
            DanhSachTask = new ObservableCollection<StudyTask>();
        }

        public MonHoc(string tenMonHoc, int soTinChi)
        {
            TenMonHoc = tenMonHoc;
            SoTinChi = soTinChi;
            MaMonHoc = Guid.NewGuid();

            // SỬA LỖI Ở ĐÂY
            DanhSachTask = new ObservableCollection<StudyTask>();
        }

        // Optional: construct with an explicit id (useful when loading from storage)
        public MonHoc(string tenMonHoc, int soTinChi, Guid maMonHoc)
        {
            TenMonHoc = tenMonHoc;
            SoTinChi = soTinChi;
            MaMonHoc = maMonHoc;

            // SỬA LỖI Ở ĐÂY
            DanhSachTask = new ObservableCollection<StudyTask>();
        }
    }
}
