using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartStudyPlanner.Models
{
    public class HocKy
    {
        // KHÓA CHÍNH
        [Key] public Guid MaHocKy { get; set; }

        public string Ten { get; set; }
        public DateTime NgayBatDau { get; set; }

        [NotMapped]
        public DateTime NgayKetThuc { get; set; }

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
            NgayKetThuc = ngayBatDau.AddDays(120);
            DanhSachMonHoc = new ObservableCollection<MonHoc>();
        }
    }
}