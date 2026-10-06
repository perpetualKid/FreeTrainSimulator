using System;
using System.Drawing;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    public class HostedViewportGeometryTests
    {
        [TestMethod]
        [DataRow(1040, 600, -120, 180)]
        [DataRow(560, 600, 120, 180)]
        [DataRow(800, 360, 120, 180)]
        [DataRow(800, 600, 237, 180)]
        [DataRow(800, 600, 120, 109)]
        [DataRow(800, 600, 237, 109)]
        [DataRow(1041, 361, 237, 109)]
        [DataRow(561, 841, -121, 251)]
        public void WhenHostedClientGeometryChangesThenFeatureKeepsItsWholeWindowRelativePixelPosition(int width, int height, int originX, int originY)
        {
            HostedViewportGeometry previous = CreateGeometry();
            HostedViewportGeometry current = new(new Size(width, height), new Point(originX, originY), previous.WindowCenter);
            MapViewportState viewport = CreateViewport(previous, new PointD(100.25, -200.75), 1.3);
            PointD feature = new(120.375, -240.125);
            PointD original = WholeWindowRelativePosition(viewport, previous, feature);

            ApplyGeometry(viewport, previous, current);

            AssertPixelPosition(original, WholeWindowRelativePosition(viewport, current, feature));
        }

        [TestMethod]
        [DataRow(-1800, -900)]
        [DataRow(173, 291)]
        public void WhenTheWholeWindowMovesThenFeatureKeepsItsWholeWindowRelativePixelPosition(int deltaX, int deltaY)
        {
            HostedViewportGeometry previous = CreateGeometry();
            HostedViewportGeometry current = new(previous.ClientSize,
                new Point(previous.ClientOrigin.X + deltaX, previous.ClientOrigin.Y + deltaY),
                new PointD(previous.WindowCenter.X + deltaX, previous.WindowCenter.Y + deltaY));
            MapViewportState viewport = CreateViewport(previous, new PointD(100.25, -200.75), 1.3);
            PointD feature = new(120.375, -240.125);
            PointD original = WholeWindowRelativePosition(viewport, previous, feature);

            ApplyGeometry(viewport, previous, current);

            AssertPixelPosition(original, WholeWindowRelativePosition(viewport, current, feature));
        }

        [TestMethod]
        public void WhenDockingChangesOnANegativeScreenThenFeatureKeepsItsWholeWindowRelativePixelPosition()
        {
            HostedViewportGeometry previous = new(new Size(801, 601), new Point(-1820, -720), new PointD(-1339.5, -449.5));
            HostedViewportGeometry current = new(new Size(563, 359), new Point(-1697, -681), new PointD(-1312.5, -420.5));
            MapViewportState viewport = CreateViewport(previous, new PointD(-100.125, 200.375), 1.7);
            PointD feature = new(-79.875, 160.625);
            PointD original = WholeWindowRelativePosition(viewport, previous, feature);

            ApplyGeometry(viewport, previous, current);

            AssertPixelPosition(original, WholeWindowRelativePosition(viewport, current, feature));
        }

        [TestMethod]
        public void WhenHostedGeometryChangesThenScaleRemainsUnchanged()
        {
            HostedViewportGeometry previous = CreateGeometry();
            HostedViewportGeometry current = new(new Size(1041, 361), new Point(237, 109), new PointD(617.5, 429.5));
            MapViewportState viewport = CreateViewport(previous, new PointD(100.25, -200.75), 1.3);
            double original = viewport.Scale;

            ApplyGeometry(viewport, previous, current);

            Assert.AreEqual(original, viewport.Scale);
        }

        [TestMethod]
        public void WhenDuplicateHostedSnapshotsArriveThenFeatureDoesNotMoveAgain()
        {
            HostedViewportGeometry previous = CreateGeometry();
            HostedViewportGeometry current = new(new Size(1041, 361), new Point(237, 109), new PointD(617.5, 429.5));
            MapViewportState viewport = CreateViewport(previous, new PointD(100.25, -200.75), 1.3);
            PointD feature = new(120.375, -240.125);
            PointD original = WholeWindowRelativePosition(viewport, previous, feature);

            ApplyGeometry(viewport, previous, current);
            ApplyGeometry(viewport, current, current);
            ApplyGeometry(viewport, current, current);

            AssertPixelPosition(original, WholeWindowRelativePosition(viewport, current, feature));
        }

        [TestMethod]
        public void WhenHostedGeometryMakesASequenceRoundTripThenFeatureReturnsToItsOriginalPixelPosition()
        {
            HostedViewportGeometry originalGeometry = CreateGeometry();
            HostedViewportGeometry expanded = new(new Size(1041, 841), new Point(-121, 109), originalGeometry.WindowCenter);
            HostedViewportGeometry moved = new(new Size(563, 359), new Point(-1697, -681), new PointD(-1312.5, -420.5));
            MapViewportState viewport = CreateViewport(originalGeometry, new PointD(100.25, -200.75), 1.3);
            PointD feature = new(120.375, -240.125);
            PointD original = WholeWindowRelativePosition(viewport, originalGeometry, feature);

            ApplyGeometry(viewport, originalGeometry, expanded);
            ApplyGeometry(viewport, expanded, moved);
            ApplyGeometry(viewport, moved, originalGeometry);

            AssertPixelPosition(original, WholeWindowRelativePosition(viewport, originalGeometry, feature));
        }

        [TestMethod]
        [DataRow(10000.25, 20000.75)]
        [DataRow(-10000.25, -20000.75)]
        public void WhenAnOutOfRouteHostedViewChangesThenFeatureIsNotClampedBackIntoTheRoute(double centerX, double centerY)
        {
            HostedViewportGeometry previous = CreateGeometry();
            HostedViewportGeometry current = new(new Size(1041, 361), new Point(237, 109), previous.WindowCenter);
            MapViewportState viewport = CreateViewport(previous, new PointD(centerX, centerY), 1.3);
            PointD feature = new(centerX + 20.125, centerY - 40.375);
            PointD original = WholeWindowRelativePosition(viewport, previous, feature);

            ApplyGeometry(viewport, previous, current);

            AssertPixelPosition(original, WholeWindowRelativePosition(viewport, current, feature));
        }

        [TestMethod]
        [DataRow(-100, 200, 0, 0)]
        [DataRow(-100, 0, 0, 200)]
        [DataRow(0, 200, 100, 0)]
        public void WhenHostedGeometryPreservesAZeroCenterCoordinateThenControllerKeepsFeaturePositionAndZoom(int deltaX, int deltaY, int centerX, int centerY)
        {
            HostedViewportGeometry previous = CreateGeometry();
            HostedViewportGeometry current = new(previous.ClientSize,
                new Point(previous.ClientOrigin.X + deltaX, previous.ClientOrigin.Y + deltaY), previous.WindowCenter);
            MapViewController controller = new(new MapViewportBounds(-1000, 1000, 1000, -1000));
            controller.ResetSize(new Microsoft.Xna.Framework.Point(previous.ClientSize.Width, previous.ClientSize.Height), 0,
                Microsoft.Xna.Framework.Point.Zero);
            controller.PresetPosition(new PointD(100, 200), 1);
            PointD feature = new(120, 160);
            PointD original = WholeWindowRelativePosition(controller, previous, feature);
            double originalScale = controller.Scale;
            _ = controller.UpdateFrameState();
            _ = controller.ConsumeScaleChanged();
            controller.NotifyFrameRendered();

            PointD preservedCenter = current.PreserveWindowCenter(previous, controller.CenterPoint, controller.Scale);
            controller.UpdateViewportWindowSize(new Microsoft.Xna.Framework.Point(current.ClientSize.Width, current.ClientSize.Height));
            controller.SetTrackingPosition(preservedCenter);
            _ = controller.UpdateFrameState();

            Assert.AreEqual(new PointD(centerX, centerY), controller.CenterPoint);
            AssertPixelPosition(original, WholeWindowRelativePosition(controller, current, feature));
            Assert.AreEqual(originalScale, controller.Scale);
            Assert.IsFalse(controller.ConsumeScaleChanged());
            Assert.IsTrue(controller.ConsumeRedrawRequested());
        }

        private static HostedViewportGeometry CreateGeometry()
        {
            return new HostedViewportGeometry(new Size(800, 600), new Point(120, 180), new PointD(600.5, 450.5));
        }

        private static MapViewportState CreateViewport(HostedViewportGeometry geometry, PointD center, double scale)
        {
            MapViewportState viewport = new(new MapViewportBounds(-1000, 1000, 1000, -1000));
            viewport.ResetSize(new MapViewportSize(geometry.ClientSize.Width, geometry.ClientSize.Height), 0);
            viewport.PresetPosition(center, scale);
            return viewport;
        }

        private static void ApplyGeometry(MapViewportState viewport, HostedViewportGeometry previous, HostedViewportGeometry current)
        {
            PointD previousCenter = viewport.CenterPoint;
            double scale = viewport.Scale;
            PointD preservedCenter = current.PreserveWindowCenter(previous, previousCenter, scale);
            viewport.UpdateWindowSize(new MapViewportSize(current.ClientSize.Width, current.ClientSize.Height));
            viewport.SetTrackingPosition(preservedCenter);
        }

        private static PointD WholeWindowRelativePosition(MapViewportState viewport, HostedViewportGeometry geometry, PointD feature)
        {
            PointD clientPixel = viewport.WorldToScreenCoordinates(feature);
            return new PointD(clientPixel.X + geometry.ClientOrigin.X - geometry.WindowCenter.X,
                clientPixel.Y + geometry.ClientOrigin.Y - geometry.WindowCenter.Y);
        }

        private static PointD WholeWindowRelativePosition(MapViewController controller, HostedViewportGeometry geometry, PointD feature)
        {
            Microsoft.Xna.Framework.Vector2 clientPixel = controller.WorldToScreenCoordinates(feature);
            return new PointD(clientPixel.X + geometry.ClientOrigin.X - geometry.WindowCenter.X,
                clientPixel.Y + geometry.ClientOrigin.Y - geometry.WindowCenter.Y);
        }

        private static void AssertPixelPosition(PointD expected, PointD actual)
        {
            double error = Math.Max(Math.Abs(expected.X - actual.X), Math.Abs(expected.Y - actual.Y));
            Assert.AreEqual(0d, error, 1e-8, $"Expected whole-window-relative pixel {expected}; actual {actual}.");
        }
    }
}
