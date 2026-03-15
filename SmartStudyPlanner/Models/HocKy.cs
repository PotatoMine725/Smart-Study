using System;
using System.Collections.ObjectModel; // Nhớ thêm thư viện này

namespace SmartStudyPlanner.Models
{
    public class HocKy
    {
        public string Ten { get; set; }
        public DateTime NgayBatDau { get; set; }

        // MỚI: Cái "ba lô" để chứa các môn học thuộc về học kỳ này
        public ObservableCollection<MonHoc> DanhSachMonHoc { get; set; }

        // Constructor mặc định (nếu cần)
        public HocKy()
        {
            DanhSachMonHoc = new ObservableCollection<MonHoc>();
        }

        public HocKy(string ten, DateTime ngayBatDau)
        {
            Ten = ten;
            NgayBatDau = ngayBatDau;

            // Khởi tạo cái ba lô trống ngay khi học kỳ được tạo ra
            DanhSachMonHoc = new ObservableCollection<MonHoc>();
        }
    }
}