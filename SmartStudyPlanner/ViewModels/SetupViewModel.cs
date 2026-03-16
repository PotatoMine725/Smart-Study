using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using System;
using System.Windows;

namespace SmartStudyPlanner.ViewModels
{
    // BẮT BUỘC phải có chữ "partial" và kế thừa "ObservableObject"
    public partial class SetupViewModel : ObservableObject
    {
        // 1. DỮ LIỆU (DATA)
        // Phép màu của thư viện Toolkit: Chỉ cần khai báo biến viết thường có [ObservableProperty]
        // Thư viện sẽ TỰ ĐỘNG sinh ra một thuộc tính viết hoa (TenHocKy) có khả năng báo cáo sự thay đổi lên UI!
        [ObservableProperty]
        private string tenHocKy;

        [ObservableProperty]
        private DateTime? ngayBatDau = DateTime.Now; // Mặc định chọn ngày hôm nay

        // 2. SỰ KIỆN CHUYỂN TRANG
        // Trong MVVM chuẩn, ViewModel KHÔNG ĐƯỢC PHÉP biết về View (không được gọi NavigationService).
        // Ta dùng một cái "Loa thông báo" (Action) để hét lên khi tạo xong Học kỳ.
        public Action<HocKy> OnSetupCompleted { get; set; }

        // 3. NÚT BẤM (COMMAND)
        // Thư viện tự động biến hàm này thành một cái nút bấm tên là "TaoHocKyCommand"
        [RelayCommand]
        private void TaoHocKy()
        {
            // Ta dùng thẳng các thuộc tính (viết hoa chữ cái đầu) mà thư viện tự đẻ ra
            if (string.IsNullOrWhiteSpace(TenHocKy) || NgayBatDau == null)
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tên học kỳ và ngày bắt đầu", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Tạo học kỳ mới
            HocKy hocKyMoi = new HocKy(TenHocKy, NgayBatDau.Value);

            // Hét lên: "Tạo xong rồi, View ơi chuyển trang đi!"
            OnSetupCompleted?.Invoke(hocKyMoi);
        }
    }
}