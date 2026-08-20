using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartStudyPlanner.Controls
{
    /// <summary>
    /// Single-value percent ring (0-100). WPF has no conic-gradient brush, so this draws
    /// two concentric arcs with Path/ArcSegment — the same technique DonutChart already
    /// uses for its multi-segment status ring. Geometry matches DonutChart exactly
    /// (160x160 canvas, R=60, 22px stroke) so the two controls read as one family
    /// wherever they appear side by side.
    ///
    /// This control draws ONLY the ring. Center text (percent value + caption) is placed
    /// by the caller as an overlay — see AnalyticsPage.xaml's completion panel:
    ///   <Grid>
    ///     <controls:RadialProgressRing Percent="{Binding FocusedCompletionPercent}"
    ///                                   RingBrush="{DynamicResource SuccessColor}"/>
    ///     <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"> ... </StackPanel>
    ///   </Grid>
    /// </summary>
    public partial class RadialProgressRing : UserControl
    {
        private const double Cx = 80, Cy = 80, R = 60, RingThickness = 22;

        public static readonly DependencyProperty PercentProperty =
            DependencyProperty.Register(nameof(Percent), typeof(double), typeof(RadialProgressRing),
                new PropertyMetadata(0.0, (d, e) => ((RadialProgressRing)d).Render()));

        public static readonly DependencyProperty RingBrushProperty =
            DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(RadialProgressRing),
                new PropertyMetadata(null, (d, e) => ((RadialProgressRing)d).Render()));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(RadialProgressRing),
                new PropertyMetadata(null, (d, e) => ((RadialProgressRing)d).Render()));

        /// <summary>0-100. Values outside the range are clamped.</summary>
        public double Percent
        {
            get => (double)GetValue(PercentProperty);
            set => SetValue(PercentProperty, value);
        }

        /// <summary>Filled arc colour. Defaults to the theme's SuccessColor if unset.</summary>
        public Brush? RingBrush
        {
            get => (Brush?)GetValue(RingBrushProperty);
            set => SetValue(RingBrushProperty, value);
        }

        /// <summary>Background track colour. Defaults to the theme's BorderColor if unset.</summary>
        public Brush? TrackBrush
        {
            get => (Brush?)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public RadialProgressRing()
        {
            InitializeComponent();
            Loaded += (s, e) => Render();
        }

        private void Render()
        {
            if (RingCanvas == null) return;
            RingCanvas.Children.Clear();

            var track = TrackBrush ?? (TryFindResource("BorderColor") as Brush) ?? SystemColors.ControlLightBrush;
            var ring = RingBrush ?? (TryFindResource("SuccessColor") as Brush) ?? SystemColors.HighlightBrush;

            AddEllipse(track);

            double pct = Math.Max(0, Math.Min(100, Percent));
            if (pct <= 0) return;

            if (pct >= 100)
            {
                AddEllipse(ring);
                return;
            }

            double startA = -Math.PI / 2;
            double endA = startA + 2 * Math.PI * (pct / 100.0);

            var figure = new System.Windows.Media.PathFigure
            {
                StartPoint = ArcPoint(startA),
                IsClosed = false
            };
            figure.Segments.Add(new System.Windows.Media.ArcSegment
            {
                Point = ArcPoint(endA),
                Size = new Size(R, R),
                IsLargeArc = (endA - startA) > Math.PI,
                SweepDirection = SweepDirection.Clockwise
            });

            RingCanvas.Children.Add(new Path
            {
                Data = new System.Windows.Media.PathGeometry(new[] { figure }),
                Stroke = ring,
                StrokeThickness = RingThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = null
            });
        }

        private void AddEllipse(Brush stroke)
        {
            var e = new Ellipse
            {
                Width = R * 2,
                Height = R * 2,
                Stroke = stroke,
                StrokeThickness = RingThickness,
                Fill = null
            };
            Canvas.SetLeft(e, Cx - R);
            Canvas.SetTop(e, Cy - R);
            RingCanvas.Children.Add(e);
        }

        private static Point ArcPoint(double angle)
            => new Point(Cx + R * Math.Cos(angle), Cy + R * Math.Sin(angle));
    }
}
