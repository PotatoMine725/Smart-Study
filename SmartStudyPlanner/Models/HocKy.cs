using System;
using System.Collections.ObjectModel;

namespace SmartStudyPlanner.Models
{
    public class HocKy
    {
        // KHÓA CHÍNH
        public Guid MaHocKy { get; set; }

        public string Ten { get; set; }
        public DateTime NgayBatDau { get; set; }

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
            DanhSachMonHoc = new ObservableCollection<MonHoc>();
        }
    }
}