using System;
using System.Linq;
using System.Windows;

namespace SmartStudyPlanner.Services
{
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = false;

        public static void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;

            string themeFile = IsDarkMode
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

            var appResources = System.Windows.Application.Current.Resources;
            var merged = appResources.MergedDictionaries;

            // Chỉ thay dictionary theme, giữ nguyên SidebarStyles/CommonStyles để tránh crash StaticResource khi navigate.
            var existingTheme = merged.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)));

            var newThemeDict = new ResourceDictionary { Source = new Uri(themeFile, UriKind.Absolute) };

            if (existingTheme != null)
            {
                var idx = merged.IndexOf(existingTheme);
                merged.RemoveAt(idx);
                merged.Insert(idx, newThemeDict);
            }
            else
            {
                merged.Insert(0, newThemeDict);
            }
        }
    }
}
