using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;

using FreeTrainSimulator.Runtime.Track;

using GetText;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>Native, aligned main-route plots with a shared distance viewport and lightweight cursor.</summary>
    public sealed class PathChartControl : FrameworkElement
    {
        /// <summary>The immutable route profile; changing the cursor never changes this source.</summary>
        public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(nameof(Profile), typeof(PathChartProfile), typeof(PathChartControl), new FrameworkPropertyMetadata(null, ProfileChanged));

        /// <summary>Selected distance in metres, shared with station selection and map navigation.</summary>
        public static readonly DependencyProperty CursorDistanceProperty = DependencyProperty.Register( nameof(CursorDistance), typeof(double), typeof(PathChartControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CursorDistanceChanged, CoerceCursorDistance));

        /// <summary>Whether distance, elevation and speed labels use metric application units.</summary>
        public static readonly DependencyProperty UseMetricUnitsProperty = DependencyProperty.Register(
            nameof(UseMetricUnits), typeof(bool), typeof(PathChartControl),
            new FrameworkPropertyMetadata(true, DisplayUnitsChanged));

        private static readonly Typeface labelTypeface = new Typeface("Segoe UI");
        private static readonly Pen gridPen = CreatePen(Brushes.LightGray, 0.5);
        private static readonly Pen zeroLinePen = CreatePen(Brushes.DimGray, 1);
        private static readonly Pen dividerPen = CreatePen(Brushes.SlateGray, 1);
        private static readonly Pen cursorPen = CreatePen(Brushes.Black, 1);
        private static readonly Pen stationPen = CreatePen(Brushes.DarkGray, 0.5);
        private static readonly Pen elevationPen = CreatePen(Brushes.SteelBlue, 1.5);
        private static readonly Pen gradePen = CreatePen(Brushes.DarkGreen, 1.5);
        private static readonly Pen curvaturePen = CreatePen(Brushes.DarkOrchid, 1.5);
        private DrawingGroup profileDrawing;
        private Rect plotBounds;
        private double viewportStart;
        private double viewportLength;
        private bool updatingCursor;
        private bool dragging;
        private Point dragOrigin;
        private double dragViewportStart;
        private string drawingCulture;
        private double drawingPixelsPerDip;

        /// <summary>Creates the chart without child visuals or a hosted native window.</summary>
        public PathChartControl()
        {
            Focusable = true;
            ClipToBounds = true;
            Unloaded += ChartUnloaded;
        }

        /// <summary>The immutable route profile to plot.</summary>
        public PathChartProfile Profile
        {
            get => (PathChartProfile)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        /// <summary>Selected distance along the main route, in metres.</summary>
        public double CursorDistance
        {
            get => (double)GetValue(CursorDistanceProperty);
            set => SetValue(CursorDistanceProperty, value);
        }

        /// <summary>Metric labels when true; miles, feet and mph otherwise. Curvature remains explicitly SI.</summary>
        public bool UseMetricUnits
        {
            get => (bool)GetValue(UseMetricUnitsProperty);
            set => SetValue(UseMetricUnitsProperty, value);
        }

        /// <summary>First visible main-route distance, in metres.</summary>
        public double ViewportStart => viewportStart;

        /// <summary>Visible main-route length, in metres.</summary>
        public double ViewportLength => viewportLength;

        /// <summary>Last permitted viewport start, in metres; zero when the whole route is visible.</summary>
        public double ViewportMaximum => HasProfile ? Math.Max(0, Profile.Length - viewportLength) : 0;

        /// <summary>Raised after zooming, panning, selection reveal or profile/reset changes the viewport.</summary>
        public event EventHandler ViewportChanged;

        /// <summary>Pans to a clamped main-route distance without changing zoom or cursor selection.</summary>
        public void PanViewportTo(double distance)
        {
            if (!double.IsFinite(distance))
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (!HasProfile)
                throw new InvalidOperationException("An available main-route profile is required to pan the chart.");
            EndDrag();
            SetViewport(distance, viewportLength);
        }

        /// <summary>Shows the entire main route without changing its selected distance.</summary>
        public void ResetViewport()
        {
            EndDrag();
            viewportStart = 0;
            viewportLength = HasProfile ? Profile.Length : 0;
            InvalidateProfileDrawing();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Refreshes drawing labels after the owning shell changes the GetText catalog.</summary>
        public void RefreshLocalization()
        {
            InvalidateProfileDrawing();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new FrameworkElementAutomationPeer(this);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateProfileDrawing();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            ArgumentNullException.ThrowIfNull(drawingContext, nameof(drawingContext));

            base.OnRender(drawingContext);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            string culture = CultureInfo.CurrentCulture.Name + "|" + CultureInfo.CurrentUICulture.Name;
            if (profileDrawing == null || drawingCulture != culture || drawingPixelsPerDip != pixelsPerDip)
            {
                drawingCulture = culture;
                drawingPixelsPerDip = pixelsPerDip;
                profileDrawing = CreateProfileDrawing(pixelsPerDip);
            }
            drawingContext.DrawDrawing(profileDrawing);
            if (!HasProfile || plotBounds.IsEmpty)
                return;

            double x = DistanceToX(CursorDistance);
            if (x >= plotBounds.Left && x <= plotBounds.Right)
                DrawVerticalGuideSegments(drawingContext, cursorPen, x);
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(e));
            base.OnPreviewMouseWheel(e);
            e.Handled = true;
            if (HasProfile && !plotBounds.IsEmpty)
                ZoomViewport(DistanceAtX(e.GetPosition(this).X), Math.Pow(1.2, -e.Delta / 120.0));
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(e));
            base.OnPreviewMouseDown(e);
            e.Handled = true;
            if (e.ChangedButton != MouseButton.Left || !HasProfile || plotBounds.IsEmpty)
                return;
            Focus();
            dragOrigin = e.GetPosition(this);
            dragViewportStart = viewportStart;
            UpdateCursor(DistanceAtX(dragOrigin.X));
            dragging = CaptureMouse();
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(e));
            base.OnPreviewMouseMove(e);
            e.Handled = true;
            if (!HasProfile || plotBounds.IsEmpty)
                return;
            Point position = e.GetPosition(this);
            if (dragging)
                SetViewport(dragViewportStart - ((position.X - dragOrigin.X) * viewportLength / plotBounds.Width), viewportLength);
            if (dragging || plotBounds.Contains(position))
                UpdateCursor(DistanceAtX(position.X));
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(e));
            base.OnPreviewMouseUp(e);
            e.Handled = true;
            if (e.ChangedButton == MouseButton.Left)
                EndDrag();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(e));
            base.OnLostMouseCapture(e);
            dragging = false;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e, nameof(e));
            base.OnPreviewKeyDown(e);
            if (!HasProfile)
                return;
            double step = viewportLength / 100;
            switch (e.Key)
            {
                case Key.Left:
                    SetCurrentValue(CursorDistanceProperty, CursorDistance - step);
                    break;
                case Key.Right:
                    SetCurrentValue(CursorDistanceProperty, CursorDistance + step);
                    break;
                case Key.Home:
                    SetCurrentValue(CursorDistanceProperty, 0.0);
                    break;
                case Key.End:
                    SetCurrentValue(CursorDistanceProperty, Profile.Length);
                    break;
                case Key.PageUp:
                    SetViewport(viewportStart - (viewportLength / 2), viewportLength);
                    break;
                case Key.PageDown:
                    SetViewport(viewportStart + (viewportLength / 2), viewportLength);
                    break;
                case Key.Add:
                case Key.OemPlus:
                    ZoomViewport(CursorDistance, 1 / 1.2);
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    ZoomViewport(CursorDistance, 1.2);
                    break;
                default:
                    return;
            }
            EndDrag();
            e.Handled = true;
        }

        private bool HasProfile => Profile?.IsAvailable == true && Profile.Length > 0
            && double.IsFinite(Profile.Length) && !Profile.Samples.IsDefaultOrEmpty;

        private static void ProfileChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            PathChartControl chart = (PathChartControl)sender;
            chart.EndDrag();
            chart.ResetViewport();
            chart.CoerceValue(CursorDistanceProperty);
        }

        private static void DisplayUnitsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((PathChartControl)sender).InvalidateProfileDrawing();
        }

        private static object CoerceCursorDistance(DependencyObject sender, object value)
        {
            PathChartControl chart = (PathChartControl)sender;
            double distance = (double)value;
            return chart.HasProfile && double.IsFinite(distance) ? Math.Clamp(distance, 0, chart.Profile.Length) : 0.0;
        }

        private static void CursorDistanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            PathChartControl chart = (PathChartControl)sender;
            if (!chart.updatingCursor && chart.HasProfile && chart.viewportLength > 0)
            {
                double distance = (double)e.NewValue;
                if (distance < chart.viewportStart || distance > chart.viewportStart + chart.viewportLength)
                    chart.SetViewport(distance - (chart.viewportLength / 2), chart.viewportLength);
            }
            chart.InvalidateVisual();
        }

        private void UpdateCursor(double distance)
        {
            updatingCursor = true;
            try
            {
                SetCurrentValue(CursorDistanceProperty, distance);
            }
            finally
            {
                updatingCursor = false;
            }
        }

        private void ZoomViewport(double anchor, double factor)
        {
            EndDrag();
            double fraction = Math.Clamp((anchor - viewportStart) / viewportLength, 0, 1);
            double length = Math.Clamp(viewportLength * factor, Math.Min(Profile.Length, Math.Max(1, Profile.Length / 1000)), Profile.Length);
            SetViewport(anchor - (fraction * length), length);
        }

        private void SetViewport(double start, double length)
        {
            start = Math.Clamp(start, 0, Math.Max(0, Profile.Length - length));
            if (start == viewportStart && length == viewportLength)
                return;
            viewportStart = start;
            viewportLength = length;
            InvalidateProfileDrawing();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        private double DistanceAtX(double x)
        {
            return Math.Clamp(viewportStart + ((x - plotBounds.Left) * viewportLength / plotBounds.Width), 0, Profile.Length);
        }

        private double DistanceToX(double distance)
        {
            return plotBounds.Left + ((distance - viewportStart) * plotBounds.Width / viewportLength);
        }

        private void EndDrag()
        {
            dragging = false;
            if (IsMouseCaptured)
                ReleaseMouseCapture();
        }

        private void ChartUnloaded(object sender, RoutedEventArgs e)
        {
            EndDrag();
        }

        private void InvalidateProfileDrawing()
        {
            profileDrawing = null;
            InvalidateVisual();
        }

        private DrawingGroup CreateProfileDrawing(double pixelsPerDip)
        {
            DrawingGroup drawing = new DrawingGroup();
            plotBounds = Rect.Empty;
            using (DrawingContext context = drawing.Open())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
                if (HasProfile && RenderSize.Width > 140 && RenderSize.Height > 160 && viewportLength > 0)
                {
                    plotBounds = new Rect(82, 52, RenderSize.Width - 100, RenderSize.Height - 90);
                    DrawDistanceAxis(context, pixelsPerDip);
                    DrawLane(context, 0, UseMetricUnits ? CatalogManager.Catalog.GetString("Elevation (m)") : CatalogManager.Catalog.GetString("Elevation (ft)"),
                        elevationPen, sample => UseMetricUnits ? sample.Elevation : sample.Elevation / 0.3048, false, pixelsPerDip);
                    DrawLane(context, 1, CatalogManager.Catalog.GetString("Grade (%; + uphill)"), gradePen, sample => sample.GradePercent, true, pixelsPerDip);
                    DrawLane(context, 2, CatalogManager.Catalog.GetString("Curvature (1/km; + right)"), curvaturePen, sample => sample.CurvaturePerKilometre, true, pixelsPerDip);
                    DrawRouteItems(context, pixelsPerDip);
                }
            }
            drawing.Freeze();
            return drawing;
        }

        private void DrawDistanceAxis(DrawingContext context, double pixelsPerDip)
        {
            double distanceUnit = UseMetricUnits ? 1000 : 1609.344;
            double step = NiceTickStep(viewportLength / distanceUnit / Math.Max(2, plotBounds.Width / 90)) * distanceUnit;
            double end = viewportStart + viewportLength;
            for (double distance = Math.Ceiling(viewportStart / step) * step; distance <= end; distance += step)
            {
                double x = DistanceToX(distance);
                DrawVerticalGuideSegments(context, gridPen, x);
                FormattedText text = FormatLabel((distance / distanceUnit).ToString("0.###", CultureInfo.CurrentCulture), pixelsPerDip);
                context.DrawText(text, new Point(x - (text.Width / 2), plotBounds.Bottom + 3));
            }
            DrawLabel(context, UseMetricUnits ? CatalogManager.Catalog.GetString("Distance along main route (km)") : CatalogManager.Catalog.GetString("Distance along main route (mi)"),
                new Point(plotBounds.Left, plotBounds.Bottom + 20), pixelsPerDip);
        }

        private Rect GetLaneBounds(int lane)
        {
            double availableHeight = plotBounds.Height - 48;
            double curvatureHeight = Math.Min(availableHeight / 3, Math.Max(28, availableHeight / 5));
            double mainLaneHeight = (availableHeight - curvatureHeight) / 2;
            return new Rect(plotBounds.Left, plotBounds.Top + (lane * (mainLaneHeight + 24)), plotBounds.Width,
                lane == 2 ? curvatureHeight : mainLaneHeight);
        }

        private void DrawVerticalGuideSegments(DrawingContext context, Pen pen, double x)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                Rect bounds = GetLaneBounds(lane);
                context.DrawLine(pen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
            }
        }

        private void DrawLane(DrawingContext context, int lane, string label, Pen pen,
            Func<PathChartSample, double> valueSelector, bool signed, double pixelsPerDip)
        {
            Rect bounds = GetLaneBounds(lane);
            if (lane > 0)
                context.DrawLine(dividerPen, new Point(0, bounds.Top - 20), new Point(RenderSize.Width, bounds.Top - 20));
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            // Include adjacent samples: a long section can cross the viewport without a sample inside it.
            for (int index = 0; index < Profile.Samples.Length; index++)
            {
                PathChartSample sample = Profile.Samples[index];
                if (index + 1 < Profile.Samples.Length && Profile.Samples[index + 1].Distance < viewportStart)
                    continue;
                if (index > 0 && Profile.Samples[index - 1].Distance > viewportStart + viewportLength)
                    break;
                double value = valueSelector(sample);
                if (!double.IsFinite(value))
                    continue;
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            if (!double.IsFinite(minimum))
                minimum = maximum = 0;
            if (signed)
            {
                maximum = Math.Max(0.1, Math.Max(Math.Abs(minimum), Math.Abs(maximum)));
                minimum = -maximum;
            }
            else if (maximum - minimum < 1)
            {
                minimum -= 0.5;
                maximum += 0.5;
            }
            double range = maximum - minimum;
            context.DrawRectangle(null, gridPen, bounds);
            DrawLabel(context, label, new Point(bounds.Left, bounds.Top - 15), pixelsPerDip);
            DrawLabel(context, maximum.ToString("0.##", CultureInfo.CurrentCulture), new Point(3, bounds.Top), pixelsPerDip);
            DrawLabel(context, minimum.ToString("0.##", CultureInfo.CurrentCulture), new Point(3, bounds.Bottom - 14), pixelsPerDip);
            if (minimum <= 0 && maximum >= 0)
            {
                double zero = bounds.Bottom - ((0 - minimum) * bounds.Height / range);
                context.DrawLine(signed ? zeroLinePen : gridPen, new Point(bounds.Left, zero), new Point(bounds.Right, zero));
                if (signed && bounds.Height >= 42)
                    DrawLabel(context, "0", new Point(3, zero - 7), pixelsPerDip);
            }

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext path = geometry.Open())
            {
                bool begun = false;
                for (int index = 0; index < Profile.Samples.Length; index++)
                {
                    PathChartSample sample = Profile.Samples[index];
                    if (index + 1 < Profile.Samples.Length && Profile.Samples[index + 1].Distance < viewportStart)
                        continue;
                    if (index > 0 && Profile.Samples[index - 1].Distance > viewportStart + viewportLength)
                        break;
                    double value = valueSelector(sample);
                    if (!double.IsFinite(value) || !double.IsFinite(sample.Distance))
                    {
                        begun = false;
                        continue;
                    }
                    Point point = new Point(DistanceToX(sample.Distance), bounds.Bottom - ((value - minimum) * bounds.Height / range));
                    if (!begun)
                        path.BeginFigure(point, false, false);
                    else
                        path.LineTo(point, true, false);
                    begun = true;
                }
            }
            geometry.Freeze();
            context.PushClip(new RectangleGeometry(bounds));
            context.DrawGeometry(null, pen, geometry);
            context.Pop();
        }

        private void DrawRouteItems(DrawingContext context, double pixelsPerDip)
        {
            Rect elevationBounds = GetLaneBounds(0);
            double stationLabelEnd = double.NegativeInfinity;
            foreach (PathChartStationVisit station in Profile.Stations.OrderBy(station => station.Distance))
            {
                if (station.Distance < viewportStart || station.Distance > viewportStart + viewportLength)
                    continue;
                double x = DistanceToX(station.Distance);
                DrawVerticalGuideSegments(context, stationPen, x);
                if (string.IsNullOrEmpty(station.StationName) || elevationBounds.Height < 20)
                    continue;
                FormattedText name = new FormattedText(station.StationName, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    labelTypeface, 11, Brushes.Firebrick, pixelsPerDip)
                {
                    MaxTextWidth = Math.Max(1, elevationBounds.Height - 6),
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis,
                };
                double left = Math.Clamp(x + 3, elevationBounds.Left, Math.Max(elevationBounds.Left, elevationBounds.Right - name.Height));
                if (left < stationLabelEnd)
                    continue;
                context.PushTransform(new TranslateTransform(left, elevationBounds.Bottom - 3));
                context.PushTransform(new RotateTransform(-90));
                context.DrawText(name, new Point());
                context.Pop();
                context.Pop();
                stationLabelEnd = left + name.Height + 4;
            }
            double speedLabelEnd = double.NegativeInfinity;
            double mileLabelEnd = double.NegativeInfinity;
            foreach (PathChartMarker marker in Profile.Markers)
            {
                if (marker.Distance < viewportStart || marker.Distance > viewportStart + viewportLength)
                    continue;
                double x = DistanceToX(marker.Distance);
                bool speed = marker.Kind == PathChartMarkerKind.Speedpost;
                double y = speed ? 0 : 16;
                context.DrawLine(stationPen, new Point(x, y + 12), new Point(x, y + 16));
                string label = speed
                    ? UseMetricUnits
                        ? CatalogManager.Catalog.GetString("{0} km/h", (marker.Value * (marker.IsMiles ? 1.609344 : 1)).ToString("0.#", CultureInfo.CurrentCulture))
                        : CatalogManager.Catalog.GetString("{0} mph", (marker.Value / (marker.IsMiles ? 1 : 1.609344)).ToString("0.#", CultureInfo.CurrentCulture))
                    : marker.IsMiles
                        ? CatalogManager.Catalog.GetString("Milepost {0} mi", marker.Value.ToString("0.###", CultureInfo.CurrentCulture))
                        : CatalogManager.Catalog.GetString("Milepost {0} km", marker.Value.ToString("0.###", CultureInfo.CurrentCulture));
                FormattedText text = FormatLabel(label, pixelsPerDip);
                double left = Math.Clamp(x - (text.Width / 2), plotBounds.Left, Math.Max(plotBounds.Left, plotBounds.Right - text.Width));
                if (left < (speed ? speedLabelEnd : mileLabelEnd))
                    continue;
                context.DrawText(text, new Point(left, y));
                if (speed)
                    speedLabelEnd = left + text.Width + 8;
                else
                    mileLabelEnd = left + text.Width + 8;
            }
        }

        private static double NiceTickStep(double desired)
        {
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(desired)));
            double fraction = desired / magnitude;
            return magnitude * (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10);
        }

        private static Pen CreatePen(Brush brush, double thickness)
        {
            Pen pen = new Pen(brush, thickness);
            pen.Freeze();
            return pen;
        }

        private static FormattedText FormatLabel(string text, double pixelsPerDip)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                labelTypeface, 11, Brushes.Black, pixelsPerDip);
        }

        private static void DrawLabel(DrawingContext context, string text, Point position, double pixelsPerDip)
        {
            context.DrawText(FormatLabel(text, pixelsPerDip), position);
        }
    }
}