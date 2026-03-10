using System;
using System.Collections.Generic;
using System.Text;

namespace SmartStudyPlanner
{
    class HocKy
    {
        public string Ten { get; set; }
        public DateTime NgayBatDau { get; set; }
        public HocKy() { }

        public HocKy(string ten, DateTime ngayBatDau)
        {
            Ten = ten;
            NgayBatDau = ngayBatDau;
        }
    }
}
