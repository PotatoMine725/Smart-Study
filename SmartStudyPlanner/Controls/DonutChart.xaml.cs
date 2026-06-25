using SmartStudyPlanner.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using WpfBrush  = System.Windows.Media.Brush;
using WpfPoint  = System.Windows.Point;
using WpfSize   = System.Windows.Size;

namespace SmartStudyPlanner.Controls
{
    public partial class DonutChart : System.Windows.Controls.UserControl
    {
        // ─── ring geometry constants ──────────────────────────────────────────
        private const double Cx              = 80;
        private const double Cy              = 80;
        private const double R               = 60;
        private const double RingThickness   = 22;
        private const double GapRad          = 0.04;   // ~2.3° gap between segments

        // ─── dependency property ──────────────────────────────────────────────
        public static readonly System.Windows.DependencyProperty SegmentsProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Segments),
                typeof(System.Collections.Generic.IEnumerable<StatusSegment>),
                typeof(DonutChart),
                new System.Windows.PropertyMetadata(null, OnSegmentsChanged));

        public IEnumerable<StatusSegment>? Segments
        {
            get => (IEnumerable<StatusSegment>?)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        public DonutChart()
        {
            InitializeComponent();
        }

        // ─── collection-change wiring ─────────────────────────────────────────
        private static void OnSegmentsChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var chart = (DonutChart)d;

            if (e.OldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= chart.OnSegmentsCollectionChanged;

            if (e.NewValue is INotifyCollectionChanged newCol)
                newCol.CollectionChanged += chart.OnSegmentsCollectionChanged;

            chart.RenderArcs();
        }

        private void OnSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => RenderArcs();

        // ─── rendering ───────────────────────────────────────────────────────
        private void RenderArcs()
        {
            ArcCanvas.Children.Clear();

            var all    = Segments?.ToList() ?? new List<StatusSegment>();
            var active = all.Where(s => s.Count > 0).ToList();
            int total  = active.Sum(s => s.Count);

            TotalLabel.Text = total.ToString();

            if (total == 0)
            {
                DrawEmptyRing();
                return;
            }

            if (active.Count == 1)
            {
                // ArcSegment degenerates at exactly 360° → use Ellipse
                DrawFullEllipse(GetBrushForKey(active[0].Key));
                return;
            }

            // Cumulative angles; last segment closes ring exactly to avoid rounding drift
            double[] angles = new double[active.Count + 1];
            angles[0] = -Math.PI / 2;
            double cumFraction = 0;
            for (int i = 0; i < active.Count - 1; i++)
            {
                cumFraction += (double)active[i].Count / total;
                angles[i + 1] = -Math.PI / 2 + 2 * Math.PI * cumFraction;
            }
            angles[active.Count] = -Math.PI / 2 + 2 * Math.PI;

            for (int i = 0; i < active.Count; i++)
            {
                double startA = angles[i] + GapRad / 2;
                double endA   = angles[i + 1] - GapRad / 2;
                double sweep  = endA - startA;
                if (sweep <= 0) continue;

                ArcCanvas.Children.Add(BuildArcPath(startA, endA, sweep, GetBrushForKey(active[i].Key)));
            }
        }

        private System.Windows.Shapes.Path BuildArcPath(double startA, double endA, double sweep, WpfBrush stroke)
        {
            var figure = new System.Windows.Media.PathFigure
            {
                StartPoint = ArcPoint(startA),
                IsClosed   = false
            };
            figure.Segments.Add(new System.Windows.Media.ArcSegment
            {
                Point          = ArcPoint(endA),
                Size           = new WpfSize(R, R),
                IsLargeArc     = sweep > Math.PI,
                SweepDirection = System.Windows.Media.SweepDirection.Clockwise,
                RotationAngle  = 0
            });

            return new System.Windows.Shapes.Path
            {
                Data = new System.Windows.Media.PathGeometry(new[] { figure }),
                Stroke             = stroke,
                StrokeThickness    = RingThickness,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Flat,
                StrokeEndLineCap   = System.Windows.Media.PenLineCap.Flat,
                Fill               = null
            };
        }

        private WpfPoint ArcPoint(double angle)
            => new WpfPoint(Cx + R * Math.Cos(angle), Cy + R * Math.Sin(angle));

        private void DrawEmptyRing()
        {
            var e = new System.Windows.Shapes.Ellipse
            {
                Width           = R * 2,
                Height          = R * 2,
                Stroke          = System.Windows.SystemColors.ControlLightBrush,
                StrokeThickness = RingThickness,
                Fill            = null
            };
            System.Windows.Controls.Canvas.SetLeft(e, Cx - R);
            System.Windows.Controls.Canvas.SetTop(e, Cy - R);
            ArcCanvas.Children.Add(e);
        }

        private void DrawFullEllipse(WpfBrush stroke)
        {
            var e = new System.Windows.Shapes.Ellipse
            {
                Width           = R * 2,
                Height          = R * 2,
                Stroke          = stroke,
                StrokeThickness = RingThickness,
                Fill            = null
            };
            System.Windows.Controls.Canvas.SetLeft(e, Cx - R);
            System.Windows.Controls.Canvas.SetTop(e, Cy - R);
            ArcCanvas.Children.Add(e);
        }

        private WpfBrush GetBrushForKey(string key)
        {
            var resKey = key switch
            {
                "Urgent" => "SeverityUrgent",
                "Warn"   => "SeverityWarn",
                "Safe"   => "SeveritySafe",
                "Done"   => "SeverityDone",
                _        => "SeveritySafe"
            };
            return TryFindResource(resKey) as WpfBrush ?? System.Windows.SystemColors.HighlightBrush;
        }
    }
}
