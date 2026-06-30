using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SmartStudyPlanner.Converters
{
    // =====================================================================
    //  Cân Bằng Tải (Workload Balancer) redesign — value converters
    //  ---------------------------------------------------------------
    //  Tất cả nhận [TotalMinutes, CapacityHours] để quy đổi "tải/ngày" về tỉ lệ
    //  so với mức trần sức học (CapacityHours * 60 phút). Brush được Freeze.
    //
    //  Converter tái dùng từ app (KHÔNG khai báo lại ở đây):
    //    • SubjectToBrushConverter — tô màu chip theo TenMon (Converters/DashboardConverters.cs)
    // =====================================================================

    /// <summary>
    /// [minutes, capacityHours] → chiều dài thanh (DIP) theo tỉ lệ so với sức học.
    /// Truyền độ dài track qua ConverterParameter (vd 150 cho meter ngang,
    /// 150 cho chiều cao cột biểu đồ). Kẹp tỉ lệ trong [0,1] — ngày đầy chạm trần.
    /// Dùng cho Border.Width (meter) và Border.Height (cột biểu đồ cân bằng tải).
    /// </summary>
    public sealed class LoadToLengthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double minutes = ToDouble(values.Length > 0 ? values[0] : null);
            double capMinutes = ToDouble(values.Length > 1 ? values[1] : null) * 60.0;
            double track = parameter != null
                ? double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture)
                : 100.0;
            if (capMinutes <= 0) return 0.0;
            double ratio = Math.Max(0.0, Math.Min(1.0, minutes / capMinutes));
            return ratio * track;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static double ToDouble(object? v)
            => v == null ? 0 : System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// [minutes, capacityHours] → brush cho thanh tải.
    /// Ngày đạt/đầy sức học (minutes ≥ capacity*60) → đỏ (SeverityUrgent);
    /// còn lại → tím accent của trang (#8B5CF6).
    /// </summary>
    public sealed class LoadToBrushConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush PlanAccent = Frozen("#8B5CF6");

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double minutes = ToDouble(values.Length > 0 ? values[0] : null);
            double capMinutes = ToDouble(values.Length > 1 ? values[1] : null) * 60.0;
            bool full = capMinutes > 0 && minutes >= capMinutes - 0.5;
            if (full)
            {
                return Application.Current?.TryFindResource("SeverityUrgent") as Brush
                       ?? Frozen("#FB3B53");
            }
            return PlanAccent;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static double ToDouble(object? v)
            => v == null ? 0 : System.Convert.ToDouble(v, CultureInfo.InvariantCulture);

        private static SolidColorBrush Frozen(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    /// <summary>
    /// [minutes, capacityHours] → Visibility. Visible khi ngày đã đạt mức tải tối đa
    /// (minutes ≥ capacity*60), dùng cho nhãn "ĐÃ ĐẠT MỨC TỐI ĐA". Ngược lại Collapsed.
    /// </summary>
    public sealed class FullDayToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double minutes = ToDouble(values.Length > 0 ? values[0] : null);
            double capMinutes = ToDouble(values.Length > 1 ? values[1] : null) * 60.0;
            bool full = capMinutes > 0 && minutes >= capMinutes - 0.5;
            return full ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static double ToDouble(object? v)
            => v == null ? 0 : System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }
}
