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
            string themeFile = IsDarkMode ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";

            // CHỈ ĐỊNH RÕ SYSTEM.WINDOWS.APPLICATION
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();

            // Nạp từ điển mới
            var newDict = new ResourceDictionary() { Source = new Uri(themeFile, UriKind.Relative) };
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(newDict);
        }
    }
}