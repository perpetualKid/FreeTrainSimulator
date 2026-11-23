using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [STATestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class PathChartDrawingTests
    {
        public TestContext TestContext { get; set; }

        private static readonly string[] expected = new[] { "First", "Close" };

        [TestMethod]
        public void WhenProfileIsDrawnThenAllThreeLanesShareTheExactHorizontalExtent()
        {
            PathChartControl chart = new PathChartControl { Profile = CreateProfile() };
            using (ChartHost host = new ChartHost(chart))
            {
                DrawingGroup drawing = GetField<DrawingGroup>(chart, "profileDrawing");
                GeometryDrawing[] lanes = Drawings(drawing).OfType<GeometryDrawing>()
                    .Where(item => item.Geometry is StreamGeometry && item.Pen?.Thickness == 1.5).ToArray();
                Assert.HasCount(3, lanes);
                Rect plot = GetField<Rect>(chart, "plotBounds");
                foreach (GeometryDrawing lane in lanes)
                {
                    Assert.AreEqual(plot.Left, lane.Geometry.Bounds.Left, 0.001);
                    Assert.AreEqual(plot.Right, lane.Geometry.Bounds.Right, 0.001);
                    Assert.IsTrue(lane.Geometry.IsFrozen);
                }
                Assert.IsTrue(drawing.IsFrozen);
                Rect[] laneBounds = DrawingGroups(drawing).Select(group => group.ClipGeometry).OfType<RectangleGeometry>().Select(clip => clip.Bounds).OrderBy(bounds => bounds.Top).ToArray();
                Assert.HasCount(3, laneBounds);
                Assert.AreEqual(laneBounds[0].Height, laneBounds[1].Height, 0.001);
                Assert.IsLessThanOrEqualTo(laneBounds[0].Height * 0.8, laneBounds[2].Height, "Curvature receives distinctly less height than elevation and grade.");
                GeometryDrawing[] dividers = Drawings(drawing).OfType<GeometryDrawing>().Where(item => item.Pen?.Brush == Brushes.SlateGray).ToArray();
                Assert.HasCount(2, dividers);
                for (int index = 0; index < dividers.Length; index++)
                {
                    Assert.AreEqual(0, dividers[index].Geometry.Bounds.Left, 0.001);
                    Assert.AreEqual(chart.ActualWidth, dividers[index].Geometry.Bounds.Right, 0.001);
                    Assert.IsTrue(dividers[index].Geometry.Bounds.Top > laneBounds[index].Bottom
                        && dividers[index].Geometry.Bounds.Bottom < laneBounds[index + 1].Top);
                }
                Assert.AreEqual(0, VisualTreeHelper.GetChildrenCount(chart), "Samples are not child UI elements.");
                string labels = GetLabels(drawing);
                Assert.Contains("Elevation (m)", labels);
                Assert.Contains("Grade (%; + uphill)", labels);
                Assert.Contains("Curvature (1/km; + right)", labels);
                Assert.Contains("Distance along main route (km)", labels);
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void WhenSignedLanesAreDrawnThenZeroBaselinesAreHighlightedAcrossZoomAndResize(bool metric)
        {
            PathChartProfile profile = CreateProfile();
            PathChartControl chart = new PathChartControl
            {
                Profile = profile with { Samples = profile.Samples.Select(sample => sample with { Elevation = sample.Elevation - 120 }).ToImmutableArray() },
                UseMetricUnits = metric,
            };
            using (ChartHost host = new ChartHost(chart))
            {
                Pen cachedZeroPen = null;
                for (int pass = 0; pass < 3; pass++)
                {
                    if (pass == 1)
                        RaiseKey(chart, Key.Add);
                    else if (pass == 2)
                        host.Window.Width += 150;
                    host.Render();
                    DrawingGroup drawing = GetField<DrawingGroup>(chart, "profileDrawing");
                    Rect[] lanes = DrawingGroups(drawing).Select(group => group.ClipGeometry).OfType<RectangleGeometry>().Select(clip => clip.Bounds).OrderBy(bounds => bounds.Top).ToArray();
                    GeometryDrawing[] zeros = Drawings(drawing).OfType<GeometryDrawing>().Where(item => item.Pen?.Brush == Brushes.DimGray).ToArray();
                    Assert.HasCount(2, zeros, "Only grade and curvature receive highlighted zero lines, not elevation or the grid.");
                    for (int index = 0; index < zeros.Length; index++)
                    {
                        LineGeometry line = (LineGeometry)zeros[index].Geometry;
                        Rect lane = lanes[index + 1];
                        Assert.AreEqual(lane.Left, line.StartPoint.X, 0.001);
                        Assert.AreEqual(lane.Right, line.EndPoint.X, 0.001);
                        Assert.AreEqual(lane.Top + (lane.Height / 2), line.StartPoint.Y, 0.001, "The emphasized line is exactly zero on the symmetric signed scale.");
                        Assert.AreEqual(line.StartPoint.Y, line.EndPoint.Y, 0.001);
                        Assert.AreEqual(1, zeros[index].Pen.Thickness, 0.001);
                        Assert.IsTrue(zeros[index].Pen.IsFrozen);
                        cachedZeroPen ??= zeros[index].Pen;
                        Assert.AreSame(cachedZeroPen, zeros[index].Pen, "Drawing rebuilds reuse the existing frozen baseline pen.");
                    }
                    Assert.Contains(item => item.Pen?.Brush == Brushes.LightGray && item.Pen.Thickness == 0.5, Drawings(drawing).OfType<GeometryDrawing>(),
                        "Ordinary grid lines retain their lighter, thinner appearance.");
                }
            }
        }

        [TestMethod]
        [DataRow(650.0, 240.0, true, 96.0)]
        [DataRow(1000.0, 360.0, false, 144.0)]
        [DataRow(1000.0, 600.0, true, 192.0)]
        [DataRow(650.0, 170.0, false, 120.0)]
        public void WhenVerticalGuidesAreDrawnThenEachGraphContainsItsOwnAlignedSegmentAndDividerGapsStayClear(
            double width, double height, bool metric, double dpi)
        {
            PathChartControl chart = new PathChartControl
            {
                Profile = CreateProfile(), Width = width, Height = height, UseMetricUnits = metric, CursorDistance = 2500,
            };
            using (ChartHost host = new ChartHost(chart))
            {
                DrawingGroup cached = GetField<DrawingGroup>(chart, "profileDrawing");
                for (int pass = 0; pass < 5; pass++)
                {
                    if (pass == 1)
                        chart.CursorDistance = 3750;
                    else if (pass == 2)
                    {
                        RaiseKey(chart, Key.Add);
                        chart.PanViewportTo(1000);
                    }
                    else if (pass == 3)
                    {
                        chart.Width += 120;
                        chart.Height += 80;
                    }
                    else if (pass == 4)
                    {
                        chart.ResetViewport();
                        chart.CursorDistance = chart.Profile.Length;
                    }
                    host.Render();
                    DrawingGroup profile = GetField<DrawingGroup>(chart, "profileDrawing");
                    if (pass == 1)
                        Assert.AreSame(cached, profile, "Segmenting the cursor must not rebuild the cached profile drawing.");
                    Rect plot = GetField<Rect>(chart, "plotBounds");
                    Rect[] lanes = DrawingGroups(profile).Select(group => group.ClipGeometry).OfType<RectangleGeometry>().Select(clip => clip.Bounds).OrderBy(bounds => bounds.Top).ToArray();
                    Assert.HasCount(3, lanes);
                    GeometryDrawing[] guides = Drawings(VisualTreeHelper.GetDrawing(chart)).OfType<GeometryDrawing>()
                        .Where(item => item.Geometry is LineGeometry line && line.StartPoint.X == line.EndPoint.X && line.StartPoint.Y >= plot.Top && line.EndPoint.Y > line.StartPoint.Y).ToArray();
                    foreach (Brush brush in new[] { Brushes.LightGray, Brushes.DarkGray, Brushes.Black })
                        Assert.Contains(guide => guide.Pen?.Brush == brush, guides, "Distance grid, station and cursor guides must remain visible.");
                    foreach (IGrouping<(Brush Brush, double X), GeometryDrawing> guide in guides.GroupBy(item =>
                        (item.Pen.Brush, ((LineGeometry)item.Geometry).StartPoint.X)))
                    {
                        LineGeometry[] segments = guide.Select(item => (LineGeometry)item.Geometry).OrderBy(line => line.StartPoint.Y).ToArray();
                        Assert.HasCount(3, segments, "Each aligned guide must stop and restart at the individual graph boundaries.");
                        for (int index = 0; index < segments.Length; index++)
                        {
                            Assert.AreEqual(lanes[index].Top, segments[index].StartPoint.Y, 0.001);
                            Assert.AreEqual(lanes[index].Bottom, segments[index].EndPoint.Y, 0.001);
                            Assert.AreEqual(guide.Key.X, segments[index].EndPoint.X, 0.001);
                            Assert.IsTrue(guide.Key.X >= plot.Left && guide.Key.X <= plot.Right);
                        }
                    }
                    LineGeometry[] cursor = guides.Where(item => item.Pen.Brush == Brushes.Black).Select(item => (LineGeometry)item.Geometry).ToArray();
                    Assert.HasCount(3, cursor);
                    double cursorX = plot.Left + ((chart.CursorDistance - chart.ViewportStart) * plot.Width / chart.ViewportLength);
                    Assert.IsTrue(cursor.All(line => Math.Abs(line.StartPoint.X - cursorX) < 0.001), "Cursor segmentation preserves exact distance alignment.");
                    LineGeometry[] annotationTicks = Drawings(profile).OfType<GeometryDrawing>()
                        .Where(item => item.Pen?.Brush == Brushes.DarkGray && item.Geometry is LineGeometry line && line.StartPoint.Y < plot.Top)
                        .Select(item => (LineGeometry)item.Geometry).ToArray();
                    Assert.IsNotEmpty(annotationTicks, "Speedpost/milepost annotation ticks are retained separately from the graph guides.");
                    Assert.IsTrue(annotationTicks.All(line => line.EndPoint.Y - line.StartPoint.Y == 4 && line.EndPoint.Y < plot.Top));

                    double scale = dpi / 96;
                    RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(chart.ActualWidth * scale), (int)Math.Ceiling(chart.ActualHeight * scale), dpi, dpi, PixelFormats.Pbgra32);
                    DrawingVisual visual = new DrawingVisual();
                    using (DrawingContext context = visual.RenderOpen())
                    {
                        context.DrawDrawing(VisualTreeHelper.GetDrawing(chart));
                    }
                    bitmap.Render(visual);
                    int stride = bitmap.PixelWidth * 4;
                    byte[] pixels = new byte[stride * bitmap.PixelHeight];
                    bitmap.CopyPixels(pixels, stride, 0);
                    for (int gap = 0; gap < 2; gap++)
                    {
                        int row = (int)Math.Round((lanes[gap].Bottom + 12) * scale);
                        for (int column = (int)Math.Ceiling((plot.Left + 180) * scale); column < (int)Math.Floor((plot.Right - 1) * scale); column++)
                        {
                            int offset = (row * stride) + (column * 4);
                            Assert.IsTrue(pixels[offset] == 255 && pixels[offset + 1] == 255 && pixels[offset + 2] == 255 && pixels[offset + 3] == 255,
                                $"The rendered gap is clear of vertical guide strokes beyond its lane-title area: pass {pass}, gap {gap}, pixel {column}/{row}, BGRA {pixels[offset]}/{pixels[offset + 1]}/{pixels[offset + 2]}/{pixels[offset + 3]}.");
                        }
                    }
                }
            }
        }

        [TestMethod]
        [DataRow(650.0, 240.0)]
        [DataRow(1000.0, 360.0)]
        public void WhenStationsAreDrawnThenNamesAreVerticalBoundedAndCollisionAwareAcrossZoom(double width, double height)
        {
            PathChartProfile profile = CreateProfile();
            PathChartControl chart = new PathChartControl
            {
                Width = width,
                Height = height,
                Profile = profile with
                {
                    Stations = ImmutableArray.Create(
                        new PathChartStationVisit("A very long departure station name", 0, 0, 10, default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty),
                        new PathChartStationVisit("First", 1000, 990, 1010, default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty),
                        new PathChartStationVisit("Close", 1010, 1000, 1020, default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty),
                        new PathChartStationVisit("First", 9500, 9490, 9510, default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty)),
                },
            };
            using (ChartHost host = new ChartHost(chart))
            {
                DrawingGroup[] StationLabels()
                {
                    DrawingGroup drawing = GetField<DrawingGroup>(chart, "profileDrawing");
                    return DrawingGroups(drawing).Where(group => group.Transform is TranslateTransform
                        && group.Children.OfType<DrawingGroup>().Any(child => child.Transform is RotateTransform)).ToArray();
                }

                DrawingGroup[] names = StationLabels();
                Assert.HasCount(3, names, "Nearby station names do not overlap at full route scale; separate return visits remain labelled.");
                Assert.HasCount(2, names.Where(name => GetLabels(name) == "First").ToArray());
                Rect elevation = DrawingGroups(GetField<DrawingGroup>(chart, "profileDrawing")).Select(group => group.ClipGeometry).OfType<RectangleGeometry>().Select(clip => clip.Bounds).OrderBy(bounds => bounds.Top).First();
                double previousRight = double.NegativeInfinity;
                foreach (DrawingGroup name in names)
                {
                    Assert.AreEqual(-90, name.Children.OfType<DrawingGroup>().Select(child => child.Transform).OfType<RotateTransform>().Single().Angle);
                    Assert.IsTrue(elevation.Contains(name.Bounds), "Station text stays inside elevation, clear of marker bands, lane titles and dividers.");
                    Assert.IsGreaterThan(previousRight, name.Bounds.Left);
                    Assert.IsTrue(name.IsFrozen);
                    previousRight = name.Bounds.Right;
                }
                chart.CursorDistance = 1005;
                for (int index = 0; index < 30; index++)
                    RaiseKey(chart, Key.Add);
                chart.PanViewportTo(1005 - (chart.ViewportLength / 2));
                host.Render();
                names = StationLabels();
                Assert.HasCount(2, names, "Zoom reveals nearby encounters without globally deduplicating station names.");
                Assert.AreSequenceEqual(expected, names.Select(GetLabels));
                DrawingGroup cache = GetField<DrawingGroup>(chart, "profileDrawing");
                chart.CursorDistance = 1006;
                host.Render();
                Assert.AreSame(cache, GetField<DrawingGroup>(chart, "profileDrawing"), "Moving the marker reuses station-label drawings.");
                chart.ResetViewport();
                host.Render();
                Assert.HasCount(3, StationLabels());
            }
        }

        [TestMethod]
        [DataRow(650.0, 240.0)]
        [DataRow(1000.0, 360.0)]
        public void WhenOverlappingStationIntervalsHaveUnorderedMidpointsThenBothVerticalNamesAreDrawn(double width, double height)
        {
            PathChartStationVisit outer = new PathChartStationVisit("Outer", 5000, 1000, 9000, default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty);
            PathChartStationVisit inner = new PathChartStationVisit("Inner", 2500, 2000, 3000, default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty);
            ImmutableArray<PathChartStationVisit> visits = ImmutableArray.Create(outer, inner);
            PathChartControl chart = new PathChartControl { Profile = CreateProfile() with { Stations = visits }, Width = width, Height = height };
            using (ChartHost host = new ChartHost(chart))
            {
                DrawingGroup drawing = GetField<DrawingGroup>(chart, "profileDrawing");
                DrawingGroup[] labels = DrawingGroups(drawing).Where(group => group.Transform is TranslateTransform
                    && group.Children.OfType<DrawingGroup>().Any(child => child.Transform is RotateTransform)).ToArray();
                Assert.HasCount(2, labels, "An earlier midpoint must not disappear merely because its interval starts after the outer interval.");
                Assert.AreEqual("Inner", GetLabels(labels[0]));
                Assert.AreEqual("Outer", GetLabels(labels[1]));
                Assert.IsTrue(labels[0].Bounds.Right < labels[1].Bounds.Left, "Both distinct labels fit without colliding.");
                Assert.AreSequenceEqual(visits, chart.Profile.Stations, "Drawing order must not reorder the encounter list or immutable source.");
                chart.CursorDistance = inner.Distance;
                host.Render();
                Assert.AreSame(drawing, GetField<DrawingGroup>(chart, "profileDrawing"));
            }
        }

        [TestMethod]
        [DataRow(170.0)]
        [DataRow(220.0)]
        public void WhenChartIsSmallerThanTheHostedMinimumThenLaneGeometryRemainsSafe(double height)
        {
            PathChartControl chart = new PathChartControl { Profile = CreateProfile(), Height = height };
            using (ChartHost host = new ChartHost(chart))
            {
                Rect[] lanes = DrawingGroups(GetField<DrawingGroup>(chart, "profileDrawing")).Select(group => group.ClipGeometry).OfType<RectangleGeometry>().Select(clip => clip.Bounds).ToArray();
                Assert.HasCount(3, lanes);
                Assert.IsTrue(lanes.All(lane => lane.Height > 0 && lane.Bottom <= height));
            }
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(3000.0)]
        [DataRow(10000.0)]
        public void WhenKeyboardZoomsThenTheCursorAnchorStaysFixedAndViewportClamps(double cursor)
        {
            PathChartControl chart = new PathChartControl { Profile = CreateProfile(), CursorDistance = cursor };
            using (ChartHost host = new ChartHost(chart))
            {
                double fraction = cursor / chart.Profile.Length;
                RaiseKey(chart, Key.Add);
                double length = GetField<double>(chart, "viewportLength");
                double start = GetField<double>(chart, "viewportStart");
                Assert.AreEqual(10000 / 1.2, length, 0.001);
                Assert.AreEqual(cursor - (fraction * length), start, 0.001);
                Assert.AreEqual(cursor, chart.CursorDistance);
                for (int index = 0; index < 70; index++)
                    RaiseKey(chart, Key.Add);
                Assert.AreEqual(10, GetField<double>(chart, "viewportLength"), 0.001);
                for (int index = 0; index < 90; index++)
                    RaiseKey(chart, Key.Subtract);
                Assert.AreEqual(10000, GetField<double>(chart, "viewportLength"), 0.001);
                Assert.AreEqual(0, GetField<double>(chart, "viewportStart"), 0.001);
            }
        }

        [TestMethod]
        public void WhenViewportPansOrAStationDistanceIsSelectedThenBoundsAndCursorStayOnTheMainRoute()
        {
            PathChartControl chart = new PathChartControl { Profile = CreateProfile(), CursorDistance = 5000 };
            using (ChartHost host = new ChartHost(chart))
            {
                RaiseKey(chart, Key.Add);
                double length = GetField<double>(chart, "viewportLength");
                for (int index = 0; index < 5; index++)
                    RaiseKey(chart, Key.PageDown);
                Assert.AreEqual(chart.Profile.Length - length, GetField<double>(chart, "viewportStart"), 0.001);
                for (int index = 0; index < 5; index++)
                    RaiseKey(chart, Key.PageUp);
                Assert.AreEqual(0, GetField<double>(chart, "viewportStart"), 0.001);
                chart.CursorDistance = chart.Profile.Stations[1].Distance;
                Assert.AreEqual(9500, chart.CursorDistance);
                Assert.IsLessThanOrEqualTo(GetField<double>(chart, "viewportStart") + length, chart.CursorDistance);
                chart.CursorDistance = -1;
                Assert.AreEqual(0, chart.CursorDistance);
                chart.CursorDistance = 11000;
                Assert.AreEqual(10000, chart.CursorDistance);
                RaiseKey(chart, Key.Home);
                Assert.AreEqual(0, chart.CursorDistance);
                RaiseKey(chart, Key.End);
                Assert.AreEqual(10000, chart.CursorDistance);
                chart.ResetViewport();
                Assert.AreEqual(10000, GetField<double>(chart, "viewportLength"));
                Assert.AreEqual(0, GetField<double>(chart, "viewportStart"));
                Assert.AreEqual(10000, chart.CursorDistance, "Resetting the viewport does not change map selection.");
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void WhenTheChartZoomsAndScrollsThenTheNativeThumbReflectsDistanceWithoutChangingSelection(bool metric)
        {
            PathChartProfile profile = CreateProfile();
            PathChartToolView view = new PathChartToolView { DataContext = new ChartLayoutData(profile) };
            PathChartControl chart = (PathChartControl)view.FindName("Chart");
            ScrollBar scrollBar = (ScrollBar)view.FindName("ViewportScrollBar");
            Window window = new Window { Content = view, Width = 1000, Height = 450, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                chart.UseMetricUnits = metric;
                Assert.AreEqual(Orientation.Horizontal, scrollBar.Orientation);
                Assert.AreEqual(Visibility.Visible, scrollBar.Visibility);
                Assert.IsFalse(scrollBar.IsEnabled);
                Assert.AreEqual(0, scrollBar.Maximum);
                Assert.AreEqual(profile.Length, scrollBar.ViewportSize);
                Assert.AreEqual("Visible main-route distance range", AutomationProperties.GetName(scrollBar));
                Assert.AreEqual("Scroll to pan the zoomed path chart", scrollBar.ToolTip);
                chart.CursorDistance = 5000;
                double cursor = chart.CursorDistance;
                for (int index = 0; index < 5; index++)
                    RaiseKey(chart, Key.Add);
                window.UpdateLayout();
                Assert.IsTrue(scrollBar.IsEnabled);
                double length = profile.Length / Math.Pow(1.2, 5);
                Assert.AreEqual(length, scrollBar.ViewportSize, 0.001);
                Assert.AreEqual(profile.Length - length, scrollBar.Maximum, 0.001);
                Assert.AreEqual(chart.ViewportStart, scrollBar.Value, 0.001);
                Assert.AreEqual(length / 20, scrollBar.SmallChange, 0.001);
                Assert.AreEqual(length / 2, scrollBar.LargeChange, 0.001);
                Track track = Descendants(scrollBar).OfType<Track>().Single();
                Thumb thumb = track.Thumb;
                Assert.AreEqual(length / profile.Length, thumb.ActualWidth / track.ActualWidth, 0.01,
                    "The actual native thumb must show the fraction of the route currently visible.");

                scrollBar.Value = 0;
                ScrollBar.LineRightCommand.Execute(null, scrollBar);
                Assert.AreEqual(length / 20, chart.ViewportStart, 0.001);
                KeyEventArgs scrollKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(scrollBar), Environment.TickCount, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent };
                int propagatedKeys = 0;
                window.KeyDown += (_, _) => propagatedKeys++;
                scrollBar.RaiseEvent(scrollKey);
                Assert.IsTrue(scrollKey.Handled);
                Assert.AreEqual(length / 10, chart.ViewportStart, 0.001, "Focused scrollbar arrow keys pan rather than selecting a new chart distance.");
                Assert.AreEqual(0, propagatedKeys);
                ScrollBar.PageRightCommand.Execute(null, scrollBar);
                Assert.AreEqual((length / 10) + (length / 2), chart.ViewportStart, 0.001);
                IRangeValueProvider range = (IRangeValueProvider)UIElementAutomationPeer.CreatePeerForElement(scrollBar).GetPattern(PatternInterface.RangeValue);
                range.SetValue(scrollBar.Maximum / 2);
                window.UpdateLayout();
                Assert.AreEqual(scrollBar.Maximum / 2, chart.ViewportStart, 0.001);
                Assert.AreEqual((track.ActualWidth - thumb.ActualWidth) / 2, thumb.TranslatePoint(new Point(), track).X, 1, "The thumb position must show where the viewport lies along the route.");
                double dragDistance = (track.ActualWidth - thumb.ActualWidth) / 4;
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                thumb.RaiseEvent(new DragDeltaEventArgs(dragDistance, 0) { RoutedEvent = Thumb.DragDeltaEvent });
                thumb.RaiseEvent(new DragCompletedEventArgs(dragDistance, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                Assert.AreEqual(scrollBar.Maximum * 0.75, chart.ViewportStart, 0.001, "Dragging the actual scrollbar thumb pans the chart.");
                Assert.AreEqual(cursor, chart.CursorDistance);
                window.UpdateLayout();
                string labels = GetLabels(GetField<DrawingGroup>(chart, "profileDrawing"));
                Assert.Contains(metric ? "Distance along main route (km)" : "Distance along main route (mi)", labels);
                Assert.HasCount(3, Drawings(GetField<DrawingGroup>(chart, "profileDrawing")).OfType<GeometryDrawing>().Where(item => item.Geometry is StreamGeometry && item.Pen?.Thickness == 1.5).ToArray());
                int propagated = 0;
                window.MouseWheel += (_, _) => propagated++;
                double beforeWheel = chart.ViewportStart;
                MouseWheelEventArgs wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                scrollBar.RaiseEvent(wheel);
                Assert.IsTrue(wheel.Handled);
                Assert.AreEqual(beforeWheel - scrollBar.SmallChange, chart.ViewportStart, 0.001);
                wheel.RoutedEvent = Mouse.MouseWheelEvent;
                scrollBar.RaiseEvent(wheel);
                Assert.AreEqual(0, propagated, "Scrollbar wheel gestures must not bubble into layout or native-map navigation.");
                double retainedStart = chart.ViewportStart;
                view.RefreshLocalization();
                chart.UseMetricUnits = !metric;
                Assert.AreEqual(retainedStart, scrollBar.Value, 0.001);
                Assert.AreEqual(length, scrollBar.ViewportSize, 0.001, "Localization and units must not change viewport geometry.");
                chart.UseMetricUnits = metric;
                MouseWheelEventArgs chartWheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                chart.RaiseEvent(chartWheel);
                Assert.IsTrue(chartWheel.Handled);
                Assert.AreEqual(length / 1.2, scrollBar.ViewportSize, 0.001, "Chart wheel zoom updates the same scrollbar extent.");
                Assert.AreEqual(chart.ViewportMaximum, scrollBar.Maximum, 0.001);
                Assert.AreEqual(chart.ViewportStart, scrollBar.Value, 0.001);
                RaiseKey(chart, Key.PageUp);
                Assert.AreEqual(chart.ViewportStart, scrollBar.Value, 0.001);
                chart.CursorDistance = profile.Length;
                Assert.AreEqual(scrollBar.Maximum, scrollBar.Value, 0.001, "Selecting an offscreen station reveals it and updates the scrollbar.");
                chart.ResetViewport();
                Assert.IsFalse(scrollBar.IsEnabled);
                Assert.AreEqual(0, scrollBar.Value);
                Assert.AreEqual(0, scrollBar.Maximum);
                Assert.AreEqual(profile.Length, scrollBar.ViewportSize);
                Assert.AreEqual(profile.Length, chart.CursorDistance, "Reset never changes the selected map distance.");
                Assert.AreSame(profile, chart.Profile);
            }
            finally
            {
                window.Close();
            }
        }

        [TestMethod]
        public void WhenProfilesChangeOrUnloadThenTheScrollbarResetsWithoutRetainingTheOldRange()
        {
            PathChartToolView view = new PathChartToolView();
            PathChartControl chart = (PathChartControl)view.FindName("Chart");
            ScrollBar scrollBar = (ScrollBar)view.FindName("ViewportScrollBar");
            PathChartProfile profile = CreateProfile();
            chart.Profile = profile;
            chart.CursorDistance = 5000;
            Window window = new Window { Content = view, Width = 1000, Height = 450, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                RaiseKey(chart, Key.Add);
                scrollBar.Value = scrollBar.Maximum;
                Assert.IsTrue(scrollBar.IsEnabled);
                chart.Profile = new PathChartProfile(true, string.Empty, profile.Samples, profile.Stations, profile.Markers, 20000, profile.Diagnostics);
                Assert.AreEqual(0, scrollBar.Value);
                Assert.AreEqual(20000, scrollBar.ViewportSize);
                Assert.AreEqual(5000, chart.CursorDistance, "Range synchronization must not change selection.");
                Assert.IsFalse(scrollBar.IsEnabled);
                RaiseKey(chart, Key.Add);
                chart.Profile = null;
                Assert.IsFalse(scrollBar.IsEnabled);
                Assert.AreEqual(0, scrollBar.Maximum);
                Assert.AreEqual(0, scrollBar.Value);
                Assert.AreEqual(0, scrollBar.ViewportSize);
                chart.Profile = profile;
                Assert.AreEqual(profile.Length, scrollBar.ViewportSize);
                Assert.IsFalse(scrollBar.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        }

        [TestMethod]
        public void WhenCursorOrHoverChangesThenTheFrozenProfileDrawingIsReused()
        {
            PathChartProfile profile = CreateProfile();
            PathChartControl chart = new PathChartControl { Profile = profile };
            using (ChartHost host = new ChartHost(chart))
            {
                DrawingGroup cached = GetField<DrawingGroup>(chart, "profileDrawing");
                chart.CursorDistance = 3200;
                host.Render();
                Assert.AreSame(cached, GetField<DrawingGroup>(chart, "profileDrawing"));
                chart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                {
                    RoutedEvent = Mouse.PreviewMouseMoveEvent,
                });
                host.Render();
                Assert.AreSame(cached, GetField<DrawingGroup>(chart, "profileDrawing"));
                host.Render();
                Assert.AreSame(cached, GetField<DrawingGroup>(chart, "profileDrawing"));
                Assert.AreSame(profile, chart.Profile);
                chart.RefreshLocalization();
                host.Render();
                Assert.AreNotSame(cached, GetField<DrawingGroup>(chart, "profileDrawing"));
            }
        }

        [TestMethod]
        [DataRow(true, "Elevation (m)", "Distance along main route (km)", "80.5 km/h")]
        [DataRow(false, "Elevation (ft)", "Distance along main route (mi)", "50 mph")]
        public void WhenApplicationUnitsChangeThenLabelsUseCorrectDistanceHeightAndRawSpeedUnits(
            bool metric, string elevation, string distance, string speed)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                PathChartControl chart = new PathChartControl { Profile = CreateProfile() };
                using (ChartHost host = new ChartHost(chart))
                {
                    DrawingGroup original = GetField<DrawingGroup>(chart, "profileDrawing");
                    chart.UseMetricUnits = metric;
                    host.Render();
                    DrawingGroup drawing = GetField<DrawingGroup>(chart, "profileDrawing");
                    string labels = GetLabels(drawing);
                    Assert.Contains(elevation, labels);
                    Assert.Contains(distance, labels);
                    Assert.Contains(speed, labels);
                    Assert.Contains("Milepost 12 mi", labels, "Mileposts retain their source units, not route-distance units.");
                    if (!metric)
                        Assert.AreNotSame(original, drawing);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void WhenCultureChangesOrControlResizesThenDrawingLabelsAndGeometryAreRebuilt()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                PathChartControl chart = new PathChartControl { Profile = CreateProfile() };
                using (ChartHost host = new ChartHost(chart))
                {
                    DrawingGroup original = GetField<DrawingGroup>(chart, "profileDrawing");
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    chart.InvalidateVisual();
                    host.Render();
                    DrawingGroup localized = GetField<DrawingGroup>(chart, "profileDrawing");
                    Assert.AreNotSame(original, localized);
                    Assert.Contains("80,5 km/h", GetLabels(localized));
                    double width = GetField<Rect>(chart, "plotBounds").Width;
                    host.Window.Width += 150;
                    host.Render();
                    Assert.AreNotSame(localized, GetField<DrawingGroup>(chart, "profileDrawing"));
                    Assert.AreEqual(width + 150, GetField<Rect>(chart, "plotBounds").Width, 0.001);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        [DataRow("mouseup")]
        [DataRow("capture-loss")]
        [DataRow("unload")]
        public void WhenDragEndsThenMouseCaptureAndInteractionStateAreReleased(string reason)
        {
            PathChartControl chart = new PathChartControl { Profile = CreateProfile() };
            using (ChartHost host = new ChartHost(chart))
            {
                MouseButtonEventArgs down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent,
                };
                chart.RaiseEvent(down);
                Assert.IsTrue(down.Handled);
                Assert.IsTrue(chart.IsMouseCaptured);
                Assert.IsTrue(GetField<bool>(chart, "dragging"));
                if (reason == "mouseup")
                {
                    MouseButtonEventArgs up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    {
                        RoutedEvent = Mouse.PreviewMouseUpEvent,
                    };
                    chart.RaiseEvent(up);
                    Assert.IsTrue(up.Handled);
                }
                else if (reason == "capture-loss")
                    chart.ReleaseMouseCapture();
                else
                {
                    host.Window.Content = null;
                    host.Render();
                }
                Assert.IsFalse(chart.IsMouseCaptured);
                Assert.IsFalse(GetField<bool>(chart, "dragging"));
            }
        }

        [TestMethod]
        public void WhenChartHandlesWheelAndKeyboardThenGesturesDoNotBubbleIntoTheMapOwner()
        {
            PathChartControl chart = new PathChartControl { Profile = CreateProfile() };
            using (ChartHost host = new ChartHost(chart))
            {
                int propagated = 0;
                host.Window.MouseWheel += (_, _) => propagated++;
                host.Window.KeyDown += (_, _) => propagated++;
                MouseWheelEventArgs wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent,
                };
                chart.RaiseEvent(wheel);
                Assert.IsTrue(wheel.Handled);
                wheel.RoutedEvent = Mouse.MouseWheelEvent;
                chart.RaiseEvent(wheel);
                KeyEventArgs key = RaiseKey(chart, Key.Right);
                Assert.IsTrue(key.Handled);
                key.RoutedEvent = Keyboard.KeyDownEvent;
                chart.RaiseEvent(key);
                Assert.AreEqual(0, propagated);
            }
        }

        [TestMethod]
        public void WhenViewIsCreatedThenStationsAndCursorUseTheSharedBindingsAndAccessibleValues()
        {
            PathChartToolView view = new PathChartToolView();
            PathChartControl chart = (PathChartControl)view.FindName("Chart");
            ListView stations = (ListView)view.FindName("StationList");
            Assert.IsNull(view.FindName("FullStationListToggle"));
            Assert.AreEqual("Profile", BindingOperations.GetBinding(chart, PathChartControl.ProfileProperty).Path.Path);
            Binding cursor = BindingOperations.GetBinding(chart, PathChartControl.CursorDistanceProperty);
            Assert.AreEqual("CursorDistance", cursor.Path.Path);
            Assert.AreEqual(BindingMode.TwoWay, cursor.Mode);
            Assert.AreEqual("UseMetricUnits", BindingOperations.GetBinding(chart, PathChartControl.UseMetricUnitsProperty).Path.Path);
            Assert.AreEqual("Stations", BindingOperations.GetBinding(stations, ItemsControl.ItemsSourceProperty).Path.Path);
            Binding selection = BindingOperations.GetBinding(stations, ListView.SelectedItemProperty);
            Assert.AreEqual("SelectedStation", selection.Path.Path);
            Assert.AreEqual(BindingMode.TwoWay, selection.Mode);
            GridViewColumn stationColumn = ((GridView)stations.View).Columns[0];
            Assert.IsNull(stationColumn.DisplayMemberBinding, "DisplayMemberBinding would override the disclosure cell template.");
            Grid stationCell = (Grid)stationColumn.CellTemplate.LoadContent();
            Expander disclosure = stationCell.Children.OfType<Expander>().Single();
            Binding expansion = BindingOperations.GetBinding(disclosure, Expander.IsExpandedProperty);
            Assert.AreEqual("IsExpanded", expansion.Path.Path);
            Assert.AreEqual(BindingMode.TwoWay, expansion.Mode);
            Assert.AreEqual("SelectedValuesText", BindingOperations.GetBinding(chart, AutomationProperties.NameProperty).Path.Path);
            Assert.AreEqual(new Size(1000, 450), ToolWindow.GetDefaultFloatingSize(view));
            view.RefreshLocalization();
            Assert.AreEqual("Stations along the main route", AutomationProperties.GetName(stations));
            TextBlock hint = (TextBlock)((ToolTip)chart.ToolTip).Content;
            Assert.Contains("Mouse wheel: zoom", hint.Text);
            Assert.Contains("Stations encountered along the main route are not scheduled stops.", hint.Text);
            Assert.AreEqual(hint.Text, UIElementAutomationPeer.CreatePeerForElement(chart).GetHelpText());
            AutomationProperties.SetName(chart, "Distance 5 km; elevation 100 m");
            Assert.AreEqual("Distance 5 km; elevation 100 m", UIElementAutomationPeer.CreatePeerForElement(chart).GetName());
        }

        [TestMethod]
        [DataRow(96.0)]
        [DataRow(120.0)]
        [DataRow(144.0)]
        [DataRow(192.0)]
        public void WhenStationRowsMixDisclosureAndSingleVisitsThenNamesHeightsAndCompactGlyphsAlign(double dpi)
        {
            PathChartProfile profile = CreateProfile();
            PathChartStationVisit first = profile.Stations[0];
            PathChartStationVisit repeat = new PathChartStationVisit(first.StationName, 2000, 1900, 2100,
                default(WorldLocation), ImmutableArray<PathChartPlatformEncounter>.Empty);
            PathChartStationItemViewModel group = new PathChartStationItemViewModel(ImmutableArray.Create(first, repeat),
                true, (row, expanded) => row.SetExpanded(expanded));
            PathChartToolView view = new PathChartToolView { DataContext = new ChartLayoutData(profile) };
            ListView stations = (ListView)view.FindName("StationList");
            stations.ItemsSource =
                new PathChartStationItemViewModel[] { new PathChartStationItemViewModel(first, true), group,
                    new PathChartStationItemViewModel(profile.Stations[1], true) };
            view.Measure(new Size(1000, 425));
            view.Arrange(new Rect(0, 0, 1000, 425));
            view.UpdateLayout();
            view.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
            view.UpdateLayout();
            Assert.AreEqual(3, stations.Items.Count);
            ListViewItem singleItem = (ListViewItem)stations.ItemContainerGenerator.ContainerFromIndex(0);
            ListViewItem groupItem = (ListViewItem)stations.ItemContainerGenerator.ContainerFromIndex(1);
            ListViewItem returnItem = (ListViewItem)stations.ItemContainerGenerator.ContainerFromIndex(2);
            foreach (ListViewItem item in new[] { singleItem, groupItem, returnItem })
                item.SetCurrentValue(Control.ForegroundProperty, Brushes.DarkBlue);
            view.UpdateLayout();
            TextBlock singleName = Descendants(singleItem).OfType<TextBlock>().Single(text => text.Text == first.StationName && text.Visibility == Visibility.Visible);
            TextBlock groupName = Descendants(groupItem).OfType<TextBlock>().Single(text => text.Text == first.StationName && text.Visibility == Visibility.Visible);
            TextBlock returnName = Descendants(returnItem).OfType<TextBlock>().Single(text => text.Text == profile.Stations[1].StationName && text.Visibility == Visibility.Visible);
            Expander expander = Descendants(groupItem).OfType<Expander>().Single();
            ToggleButton header = Descendants(expander).OfType<ToggleButton>().Single();
            Path glyph = Descendants(header).OfType<Path>().Single();
            IExpandCollapseProvider disclosure = (IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(expander).GetPattern(PatternInterface.ExpandCollapse);

            foreach (bool expanded in new[] { false, true })
            {
                if (expanded)
                    disclosure.Expand();
                view.UpdateLayout();
                Point singleOrigin = singleName.TranslatePoint(new Point(), singleItem);
                Point groupOrigin = groupName.TranslatePoint(new Point(), groupItem);
                Point returnOrigin = returnName.TranslatePoint(new Point(), returnItem);
                Assert.AreEqual(singleOrigin.X, groupOrigin.X, 0.01, "Top-level names must align, with or without disclosure.");
                Assert.AreEqual(singleOrigin.X, returnOrigin.X, 0.01);
                Assert.AreEqual(singleOrigin.Y, groupOrigin.Y, 0.01, "The compact header must not shift the text baseline.");
                Assert.AreEqual(singleItem.ActualHeight, groupItem.ActualHeight, 0.01);
                Assert.AreEqual(singleItem.ActualHeight, returnItem.ActualHeight, 0.01);
                Assert.AreEqual(Brushes.DarkBlue, groupName.Foreground, "Group labels inherit the same row foreground as ordinary encounters.");
                Assert.AreEqual(singleName.Foreground, glyph.Stroke);
                Rect ink = glyph.Data.GetRenderBounds(new Pen(glyph.Stroke, glyph.StrokeThickness));
                Assert.IsTrue(ink.Width <= 8 && ink.Height <= 8, "The disclosure is a compact chevron, not a large circular button.");
                Assert.IsTrue(header.ActualHeight >= 18 && header.ActualWidth >= 16, "A small glyph must retain a usable header click target.");
                Assert.AreEqual(expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed, disclosure.ExpandCollapseState);
                Assert.AreEqual(group.ExpansionActionText, AutomationProperties.GetName(header));
                RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(1000 * dpi / 96),
                    (int)Math.Ceiling(425 * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(view);
            }

            IToggleProvider headerToggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(header).GetPattern(PatternInterface.Toggle);
            headerToggle.Toggle();
            Assert.IsFalse(group.IsExpanded, "Activating the header must propagate through the native Expander to the station run.");
            Assert.AreEqual(ExpandCollapseState.Collapsed, disclosure.ExpandCollapseState);
            headerToggle.Toggle();
            Assert.IsTrue(group.IsExpanded);
            Assert.AreEqual(ExpandCollapseState.Expanded, disclosure.ExpandCollapseState);

            stations.ItemsSource = new PathChartStationItemViewModel[] { group, group.Encounters[0], group.Encounters[1], new PathChartStationItemViewModel(profile.Stations[1], true) };
            view.UpdateLayout();
            ListViewItem childItem = (ListViewItem)stations.ItemContainerGenerator.ContainerFromIndex(1);
            TextBlock childName = Descendants(childItem).OfType<TextBlock>().Single(text => text.Text == first.StationName && text.Visibility == Visibility.Visible);
            ListViewItem anchorItem = (ListViewItem)stations.ItemContainerGenerator.ContainerFromIndex(0);
            TextBlock anchorName = Descendants(anchorItem).OfType<TextBlock>().Single(text => text.Text == first.StationName && text.Visibility == Visibility.Visible);
            Assert.AreEqual(12, childName.TranslatePoint(new Point(), childItem).X - anchorName.TranslatePoint(new Point(), anchorItem).X, 0.01, "Physical children retain deliberate indentation beyond the aligned top-level gutter.");
        }

        [TestMethod]
        [DataRow(650.0)]
        [DataRow(1000.0)]
        [DataRow(2000.0)]
        public void WhenTheChartHasNoWarningsThenLiveValuesUseTheHeaderAndTheNoticeOnlyUsesTheHint(double width)
        {
            PathChartToolView view = new PathChartToolView { DataContext = new ChartLayoutData(CreateProfile(), string.Empty) };
            view.RefreshLocalization();
            view.Measure(new Size(width, 370));
            view.Arrange(new Rect(0, 0, width, 370));
            view.UpdateLayout();
            view.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
            Grid header = (Grid)view.FindName("ChartHeader");
            ScrollViewer values = (ScrollViewer)view.FindName("SelectedValuesScroll");
            ScrollViewer diagnostics = (ScrollViewer)view.FindName("DiagnosticScroll");
            ScrollViewer layout = (ScrollViewer)view.FindName("ChartLayoutScroll");
            PathChartControl chart = (PathChartControl)view.FindName("Chart");
            Assert.AreEqual(ChartLayoutData.ExpectedSelectedValuesText, ((TextBlock)values.Content).Text);
            Assert.AreEqual(Visibility.Collapsed, diagnostics.Visibility, "An empty diagnostic area takes no header space.");
            Assert.AreEqual(2, ((Grid)layout.Content).RowDefinitions.Count, "The footer rows are removed rather than duplicated in the header.");
            Assert.IsTrue(new Rect(header.RenderSize).Contains(new Rect(values.TranslatePoint(new Point(), header), values.RenderSize)));
            Assert.IsGreaterThanOrEqualTo(header.TranslatePoint(new Point(0, header.ActualHeight), view).Y, chart.TranslatePoint(new Point(), view).Y);
            Assert.IsGreaterThanOrEqualTo(300, chart.ActualHeight, "Relocating the live values and removing the permanent notice must free measurable plotting height.");
            Assert.AreEqual(0, layout.ScrollableHeight, 0.001);
            string notice = "Stations encountered along the main route are not scheduled stops. Speedposts are source annotations, not an operating speed profile.";
            Assert.DoesNotContain(text => text.Text.Contains(notice, StringComparison.OrdinalIgnoreCase), Descendants(view).OfType<TextBlock>(), "The interpretation notice is not permanent pane text.");
            Assert.Contains(notice, ((TextBlock)((ToolTip)chart.ToolTip).Content).Text);
            values.ScrollToBottom();
            view.UpdateLayout();
            Assert.AreEqual(values.ScrollableHeight, values.VerticalOffset, 0.1, "Wrapped live values remain fully reachable in narrow panes.");
        }

        [TestMethod]
        public void WhenNoRouteIsLoadedThenUnavailableGuidanceRemainsVisibleInTheHeader()
        {
            PathChartToolWindow bridge = new PathChartToolWindow(() => null, () => null, _ => { });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (PathChartToolWindowViewModel viewModel = new PathChartToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    PathChartToolView view = new PathChartToolView { DataContext = viewModel };
                    view.Measure(new Size(650, 370));
                    view.Arrange(new Rect(0, 0, 650, 370));
                    view.UpdateLayout();
                    ScrollViewer status = (ScrollViewer)view.FindName("DiagnosticScroll");
                    Assert.AreEqual("Load a route to view its path chart.", viewModel.StatusMessage);
                    Assert.AreEqual(viewModel.StatusMessage, ((TextBlock)status.Content).Text);
                    Assert.AreEqual(Visibility.Visible, status.Visibility);
                    Assert.IsGreaterThan(0, status.ActualHeight);
                    Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting((TextBlock)status.Content));
                }
            }
        }

        [TestMethod]
        [DataRow(650.0, 370.0, false)]
        [DataRow(1000.0, 425.0, false)]
        [DataRow(2000.0, 425.0, false)]
        [DataRow(650.0, 330.0, true)]
        public void WhenThePaneIsSizedThenLanesStationsAndScrollableDiagnosticsRemainUsable(double width, double height, bool scrollRequired)
        {
            ChartLayoutData data = new ChartLayoutData(CreateProfile());
            PathChartToolView view = new PathChartToolView { DataContext = data };
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
            view.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);

            PathChartControl chart = (PathChartControl)view.FindName("Chart");
            ListView stations = (ListView)view.FindName("StationList");
            ScrollViewer values = (ScrollViewer)view.FindName("SelectedValuesScroll");
            ScrollViewer diagnostics = (ScrollViewer)view.FindName("DiagnosticScroll");
            ScrollViewer layout = (ScrollViewer)view.FindName("ChartLayoutScroll");
            ScrollBar viewportScrollBar = (ScrollBar)view.FindName("ViewportScrollBar");
            Grid header = (Grid)view.FindName("ChartHeader");
            foreach (ScrollViewer text in new[] { values, diagnostics })
                Assert.IsTrue(new Rect(header.RenderSize).Contains(new Rect(text.TranslatePoint(new Point(), header), text.RenderSize)),
                    "Live values and diagnostics must be inside the header, not below the chart.");
            Assert.IsLessThanOrEqualTo(chart.TranslatePoint(new Point(), view).Y, header.TranslatePoint(new Point(0, header.ActualHeight), view).Y);
            double minimumHeight = ((FrameworkElement)layout.Content).ActualHeight - (chart.ActualHeight - 240) + layout.Padding.Top + layout.Padding.Bottom;
            TestContext.WriteLine("Useful total view minimum at {0} DIPs wide: {1:F1} DIPs", width, minimumHeight);
            Assert.IsLessThanOrEqualTo(370, minimumHeight, "The complete useful view must fit the shared pane's content budget.");
            Assert.IsGreaterThanOrEqualTo(240, chart.ActualHeight, "Manual shrinking must scroll, not squeeze the chart.");
            Rect plot = GetField<Rect>(chart, "plotBounds");
            Assert.AreEqual(chart.TranslatePoint(new Point(plot.Left, 0), view).X, viewportScrollBar.TranslatePoint(new Point(), view).X, 0.1);
            Assert.AreEqual(plot.Width, viewportScrollBar.ActualWidth, 0.1, "The scrollbar is aligned with the chart's distance axis, not the whole pane.");
            Rect[] laneBounds = DrawingGroups(GetField<DrawingGroup>(chart, "profileDrawing")).Select(group => group.ClipGeometry).OfType<RectangleGeometry>().Select(clip => clip.Bounds).OrderBy(bounds => bounds.Top).ToArray();
            Assert.HasCount(3, laneBounds);
            Assert.IsTrue(laneBounds[0].Height >= 37 && laneBounds[1].Height >= 37, "Elevation and grade retain useful vertical plotting ranges.");
            Assert.IsTrue(laneBounds[2].Height >= 28 && laneBounds[2].Height < laneBounds[0].Height, "Curvature is compact without collapsing its signed plot.");
            Assert.HasCount(3, Drawings(GetField<DrawingGroup>(chart, "profileDrawing")).OfType<GeometryDrawing>()
                .Where(item => item.Geometry is StreamGeometry && item.Pen?.Thickness == 1.5).ToArray());
            Assert.AreEqual(2, stations.Items.Count);
            Assert.AreEqual(data.StatusMessage, ((TextBlock)diagnostics.Content).Text, "No diagnostic text is truncated or dropped.");
            Assert.AreEqual(ChartLayoutData.ExpectedSelectedValuesText, ((TextBlock)values.Content).Text);
            Assert.IsGreaterThan(diagnostics.ViewportHeight, diagnostics.ExtentHeight);
            Assert.AreEqual(scrollRequired, layout.ExtentHeight > layout.ViewportHeight + 0.1);
            Rect viewport = new Rect(0, 0, width, height);
            if (!scrollRequired)
            {
                foreach (FrameworkElement element in new FrameworkElement[] { chart, stations, values, diagnostics, viewportScrollBar })
                    Assert.IsTrue(viewport.Contains(new Rect(element.TranslatePoint(new Point(), view), element.RenderSize)), element.Name);
                foreach (Button button in Descendants(view).OfType<Button>())
                    Assert.IsTrue(viewport.Contains(new Rect(button.TranslatePoint(new Point(), view), button.RenderSize)), button.Content?.ToString());
            }
            else
            {
                layout.ScrollToBottom();
                view.UpdateLayout();
                Assert.IsTrue(viewport.Contains(new Rect(viewportScrollBar.TranslatePoint(new Point(), view), viewportScrollBar.RenderSize)),
                    "The bottom chart scrollbar remains reachable when the full pane must scroll.");
                layout.ScrollToTop();
                view.UpdateLayout();
                Assert.IsTrue(viewport.Contains(new Rect(diagnostics.TranslatePoint(new Point(), view), diagnostics.RenderSize)));
            }
            diagnostics.ScrollToBottom();
            view.UpdateLayout();
            Assert.AreEqual(diagnostics.ScrollableHeight, diagnostics.VerticalOffset, 0.1, "The final diagnostic remains reachable.");
        }

        [TestMethod]
        public void WhenProfileIsUnavailableThenDrawingAndPointerInspectionRemainSafe()
        {
            PathChartControl chart = new PathChartControl();
            using (ChartHost host = new ChartHost(chart))
            {
                chart.CursorDistance = double.NaN;
                chart.ResetViewport();
                chart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent,
                });
                host.Render();
                Assert.AreEqual(0, chart.CursorDistance);
                Assert.IsTrue(GetField<Rect>(chart, "plotBounds").IsEmpty);
                Assert.IsEmpty(Drawings(GetField<DrawingGroup>(chart, "profileDrawing")).OfType<GlyphRunDrawing>().ToArray());
                chart.Profile = CreateProfile();
                host.Render();
                Assert.IsFalse(GetField<Rect>(chart, "plotBounds").IsEmpty);
            }
        }

        private static PathChartProfile CreateProfile()
        {
            WorldLocation location = default(WorldLocation);
            return new PathChartProfile(true, string.Empty,
                ImmutableArray.Create(new PathChartSample(0, 100, -1, -2, location), new PathChartSample(5000, 150, 0, 0, location), new PathChartSample(10000, 120, 2, 3, location)),
                ImmutableArray.Create(new PathChartStationVisit("First", 1000, 900, 1100, location, ImmutableArray<PathChartPlatformEncounter>.Empty), new PathChartStationVisit("Last", 9500, 9400, 9600, location, ImmutableArray<PathChartPlatformEncounter>.Empty)),
                ImmutableArray.Create(new PathChartMarker(2500, 50, PathChartMarkerKind.Speedpost, true, location), new PathChartMarker(7500, 12, PathChartMarkerKind.Milepost, true, location)),
                10000, ImmutableArray<string>.Empty);
        }

        private static KeyEventArgs RaiseKey(PathChartControl chart, Key key)
        {
            KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(chart), Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            chart.RaiseEvent(args);
            return args;
        }

        private static T GetField<T>(PathChartControl chart, string name)
        {
            return (T)typeof(PathChartControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chart);
        }

        private static IEnumerable<DrawingGroup> DrawingGroups(DrawingGroup drawing)
        {
            yield return drawing;
            foreach (DrawingGroup child in drawing.Children.OfType<DrawingGroup>())
            {
                foreach (DrawingGroup group in DrawingGroups(child))
                    yield return group;
            }
        }

        private static IEnumerable<Drawing> Drawings(Drawing drawing)
        {
            if (drawing is DrawingGroup group)
            {
                foreach (Drawing child in group.Children)
                {
                    foreach (Drawing descendant in Drawings(child))
                        yield return descendant;
                }
            }
            else
                yield return drawing;
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (DependencyObject descendant in Descendants(child))
                    yield return descendant;
            }
        }

        private static string GetLabels(DrawingGroup drawing)
        {
            return string.Join(" ", Drawings(drawing).OfType<GlyphRunDrawing>()
                .Where(item => item.GlyphRun.Characters != null).Select(item => new string(item.GlyphRun.Characters.ToArray())));
        }

        private sealed class ChartLayoutData
        {
            public PathChartProfile Profile { get; }
            public double CursorDistance { get; set; }
            public PathChartStationItemViewModel[] Stations { get; }
            public PathChartStationItemViewModel SelectedStation { get; set; }
            public const string ExpectedSelectedValuesText = "Distance 5.00 km; elevation 150.0 m; grade 0.00%; curvature 0.00 /km (positive right). Selected main-route geometry values remain accessible.";
#pragma warning disable CA1822 // Mark members as static
            public string SelectedValuesText => ExpectedSelectedValuesText;
#pragma warning restore CA1822 // Mark members as static
            public string StatusMessage { get; }

            public ChartLayoutData(PathChartProfile profile)
                : this(profile, string.Join(Environment.NewLine, Enumerable.Range(1, 12)
                    .Select(index => "Diagnostic " + index + ": source annotations and omitted items remain available for inspection; the final diagnostic must remain reachable.")))
            {
            }

            public ChartLayoutData(PathChartProfile profile, string statusMessage)
            {
                Profile = profile;
                StatusMessage = statusMessage;
                Stations = profile.Stations.Select(station => new PathChartStationItemViewModel(station, true)).ToArray();
                SelectedStation = Stations[0];
                CursorDistance = SelectedStation.Distance;
            }
        }

        private sealed class ChartHost : IDisposable
        {
            public Window Window { get; }

            public ChartHost(PathChartControl chart)
            {
                Window = new Window { Content = chart, Width = 700, Height = 420, ShowInTaskbar = false };
                Window.Show();
                Render();
            }

            public void Render()
            {
                Window.UpdateLayout();
                Window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
                Window.UpdateLayout();
                RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(Window.ActualWidth),
                    (int)Math.Ceiling(Window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(Window);
            }

            public void Dispose()
            {
                Window.Close();
            }
        }
    }
}