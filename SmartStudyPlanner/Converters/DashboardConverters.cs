using System;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace SmartStudyPlanner.Converters
{
    // =====================================================================
    //  Dashboard redesign — value converters
    //  All brushes are Frozen for performance: the dashboard regenerates its
    //  data continuously, so converters run often. Freezing avoids per-call
    //  allocation churn on the UI thread.
    // =====================================================================

    /// <summary>
    /// Maps a subject name deterministically to one of the identity colours in
    /// Themes/SubjectPalette.xaml. Hashes the NAME (not the row index) so a
    /// subject keeps its colour even when the source collection is rebuilt.
    /// </summary>
    public sealed class SubjectToBrushConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush[] Palette = BuildPalette(
            "#6366F1", // 1 · indigo
            "#14B8A6", // 2 · teal
            "#F59E0B", // 3 · amber
            "#F43F5E", // 4 · rose
            "#8B5CF6", // 5 · violet
            "#06B6D4"  // 6 · cyan (overflow)
        );

        private static SolidColorBrush[] BuildPalette(params string[] hex)
            => hex.Select(h =>
            {
                var b = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));
                b.Freeze();
                return b;
            }).ToArray();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value as string ?? string.Empty;
            int hash = 0;
            foreach (var ch in name) hash = (hash * 31 + ch) & 0x7FFFFFFF;
            return Palette[hash % Palette.Length];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    /// <summary>
    /// 0–100 score → bar fill width in DIPs. Pass the track width as parameter
    /// (e.g. ConverterParameter=40). Clamps to [0, track].
    /// </summary>
    public sealed class ScoreToWidthConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = ToDouble(value);
            double track = parameter != null
                ? double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture)
                : 100.0;
            double pct = Math.Max(0.0, Math.Min(1.0, v / 100.0));
            return pct * track;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;

        private static double ToDouble(object v)
            => v == null ? 0 : System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Priority score (0–100, DiemUuTien) → semantic brush.
    /// ≥80 urgent · ≥50 attention · else safe. Mirrors GetWarningLevel() in
    /// DashboardViewModel so the meter colour matches the "Mức độ" pill.
    /// </summary>
    public sealed class PriorityToBrushConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush Urgent = Frozen("#FB3B53");
        private static readonly SolidColorBrush Warn   = Frozen("#F0A92B");
        private static readonly SolidColorBrush Safe   = Frozen("#1FB87A");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = value == null ? 0 : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (v >= 80) return Urgent;
            if (v >= 50) return Warn;
            return Safe;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;

        private static SolidColorBrush Frozen(string hex)
        {
            var b = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    /// <summary>
    /// Risk score (0.0–1.0, RiskScore) → severity brush for the "Rủi ro" text.
    /// ≥.75 very-high · ≥.55 high · ≥.35 medium · else low.
    /// </summary>
    public sealed class RiskScoreToBrushConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush VeryHigh = Frozen("#FB3B53");
        private static readonly SolidColorBrush High     = Frozen("#FF7A45");
        private static readonly SolidColorBrush Medium   = Frozen("#F0A92B");
        private static readonly SolidColorBrush Low      = Frozen("#1FB87A");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = value == null ? 0 : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (v >= 0.75) return VeryHigh;
            if (v >= 0.55) return High;
            if (v >= 0.35) return Medium;
            return Low;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;

        private static SolidColorBrush Frozen(string hex)
        {
            var b = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    /// <summary>
    /// Strips a leading emoji/glyph from a label so the icon-free redesign can
    /// reuse ChuoiStreak ("🔥 12 Ngày") and TieuDeLichHomNay ("🎯 KẾ HOẠCH…")
    /// without touching the ViewModel. Returns text from the first
    /// letter/digit onward, e.g. "12 Ngày".
    /// </summary>
    public sealed class StripLeadingGlyphConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string ?? string.Empty;
            int i = 0;
            while (i < s.Length && !char.IsLetterOrDigit(s[i])) i++;
            return s.Substring(i).Trim();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    /// <summary>
    /// IMultiValueConverter: (value, max) + ConverterParameter track →
    /// track * clamp(value/max, 0, 1). Guards max=0 → returns 0 (avoids NaN).
    /// Used for native bar charts (Border.Height, Border.Width) where track is
    /// the pixel length of the chart area passed as ConverterParameter.
    /// </summary>
    public sealed class RatioToLengthConverter : System.Windows.Data.IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double val = ToDouble(values.Length > 0 ? values[0] : null);
            double max = ToDouble(values.Length > 1 ? values[1] : null);
            double track = parameter != null
                ? double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture)
                : 100.0;
            if (max <= 0) return 0.0;
            double ratio = Math.Max(0.0, Math.Min(1.0, val / max));
            return ratio * track;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static double ToDouble(object? v)
            => v == null ? 0 : System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Maps a StatusSegment.Key string ("Urgent","Warn","Safe","Done") to the
    /// corresponding SeverityXxx brush from SubjectPalette.xaml.
    /// Used in the donut legend ItemsControl so legend swatches match arc colours.
    /// </summary>
    public sealed class StatusKeyToSeverityBrushConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value as string ?? string.Empty;
            var resKey = key switch
            {
                "Urgent" => "SeverityUrgent",
                "Warn"   => "SeverityWarn",
                "Safe"   => "SeveritySafe",
                "Done"   => "SeverityDone",
                _        => "SeveritySafe"
            };
            return System.Windows.Application.Current.TryFindResource(resKey) as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
