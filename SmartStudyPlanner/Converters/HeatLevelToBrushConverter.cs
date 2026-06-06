using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartStudyPlanner.Converters
{
    public class HeatLevelToBrushConverter : IValueConverter
    {
        private static readonly string[] LightColors = { "#EBEDF0", "#9BE9A8", "#40C463", "#30A14E", "#216E39" };
        private static readonly string[] DarkColors  = { "#161B22", "#0E4429", "#006D32", "#26A641", "#39D353" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int level = value is int l ? Math.Clamp(l, 0, 4) : 0;
            bool isDark = System.Windows.Application.Current.Resources.MergedDictionaries
                .Any(d => d.Source?.OriginalString.Contains("DarkTheme") == true);
            string hex = isDark ? DarkColors[level] : LightColors[level];
            return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
