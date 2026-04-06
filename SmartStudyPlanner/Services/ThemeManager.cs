using System;
using System.Windows;

namespace SmartStudyPlanner.Services
{
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = false;

        public static void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;

            // SỬ DỤNG ĐƯỜNG DẪN PACK URI TUYỆT ĐỐI
            string themeFile = IsDarkMode
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();

            // Đổi UriKind.Relative thành UriKind.Absolute
            var newDict = new ResourceDictionary() { Source = new Uri(themeFile, UriKind.Absolute) };
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(newDict);
        }
    }
}