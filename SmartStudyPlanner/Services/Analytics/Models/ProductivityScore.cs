namespace SmartStudyPlanner.Services.Analytics.Models
{
    public sealed class ProductivityScore
    {
        public int Value { get; init; }  // [0, 100]
        public string Label => Value switch
        {
            >= 85 => "Xuất sắc",
            >= 70 => "Tốt",
            >= 50 => "Trung bình",
            >= 30 => "Cần cải thiện",
            _     => "Chưa có dữ liệu"
        };
    }
}
