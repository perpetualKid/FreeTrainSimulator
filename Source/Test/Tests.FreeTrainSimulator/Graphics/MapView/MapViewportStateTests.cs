using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Graphics.MapView
{
    [TestClass]
    public class MapViewportStateTests
    {
        [TestMethod]
        [DataRow(1040, 600)]
        [DataRow(560, 600)]
        [DataRow(800, 840)]
        [DataRow(800, 360)]
        [DataRow(1040, 840)]
        [DataRow(560, 360)]
        [DataRow(800, 600)]
        public void WhenViewportResizesThenWorldContentKeepsItsScreenPosition(int width, int height)
        {
            MapViewportState viewport = CreateViewport();
            PointD worldPoint = new(120, 240);
            PointD screenPosition = viewport.WorldToScreenCoordinates(worldPoint);

            viewport.UpdateWindowSize(new MapViewportSize(width, height));

            Assert.AreEqual(screenPosition, viewport.WorldToScreenCoordinates(worldPoint));
        }

        [TestMethod]
        [DataRow(1040, 600)]
        [DataRow(560, 600)]
        [DataRow(800, 840)]
        [DataRow(800, 360)]
        [DataRow(1040, 840)]
        [DataRow(560, 360)]
        public void WhenViewportResizesThenTheSamePixelStillIdentifiesTheSameWorldPoint(int width, int height)
        {
            MapViewportState viewport = CreateViewport();
            PointD worldPosition = viewport.ScreenToWorldCoordinates(150, 250);

            viewport.UpdateWindowSize(new MapViewportSize(width, height));

            Assert.AreEqual(worldPosition, viewport.ScreenToWorldCoordinates(150, 250));
        }

        [TestMethod]
        public void WhenViewportResizesThenZoomRemainsUnchanged()
        {
            MapViewportState viewport = CreateViewport();

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual(2.5, viewport.Scale);
        }

        [TestMethod]
        public void WhenViewportResizesThenItsCenterMovesWithTheVisibleArea()
        {
            MapViewportState viewport = CreateViewport();

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual(new PointD(148, 152), viewport.CenterPoint);
        }

        [TestMethod]
        public void WhenViewportResizesThenItsTopLeftWorldBoundRemainsUnchanged()
        {
            MapViewportState viewport = CreateViewport();
            PointD topLeft = viewport.TopLeftBound;

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual(topLeft, viewport.TopLeftBound);
        }

        [TestMethod]
        public void WhenViewportResizesThenItsBottomRightWorldBoundCoversTheNewArea()
        {
            MapViewportState viewport = CreateViewport();

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual(new PointD(356, -16), viewport.BottomRightBound);
        }

        [TestMethod]
        public void WhenAnOutOfRouteViewResizesThenContentIsNotClampedBackIntoTheRoute()
        {
            MapViewportState viewport = CreateViewport();
            viewport.PresetPosition(new PointD(10000, 20000), 2.5);
            PointD screenPosition = viewport.WorldToScreenCoordinates(new PointD(10020, 20040));

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual(screenPosition, viewport.WorldToScreenCoordinates(new PointD(10020, 20040)));
        }

        [TestMethod]
        public void WhenDuplicateResizeNotificationsArriveThenContentDoesNotMoveAgain()
        {
            MapViewportState viewport = CreateViewport();
            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));
            PointD screenPosition = viewport.WorldToScreenCoordinates(new PointD(120, 240));

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual(screenPosition, viewport.WorldToScreenCoordinates(new PointD(120, 240)));
        }

        [TestMethod]
        public void WhenViewportSizeMakesARoundTripThenItsOriginalBoundsReturn()
        {
            MapViewportState viewport = CreateViewport();
            PointD topLeft = viewport.TopLeftBound;
            PointD bottomRight = viewport.BottomRightBound;

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));
            viewport.UpdateWindowSize(new MapViewportSize(560, 360));
            viewport.UpdateWindowSize(new MapViewportSize(800, 600));

            Assert.AreEqual((topLeft, bottomRight), (viewport.TopLeftBound, viewport.BottomRightBound));
        }

        [TestMethod]
        public void WhenUninitializedViewportResizesThenWindowSizeIsRecordedWithoutCreatingAnInvalidScale()
        {
            MapViewportState viewport = new(new MapViewportBounds(-1000, 1000, 1000, -1000));

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            Assert.AreEqual((new MapViewportSize(1040, 840), 0d), (viewport.WindowSize, viewport.Scale));
        }

        [TestMethod]
        public void WhenControllerSynchronizesResizedViewportThenContentRemainsStableAndRedrawIsRequested()
        {
            MapViewportBounds bounds = new(-1000, 1000, 1000, -1000);
            MapViewController controller = new(bounds);
            controller.ResetSize(new Point(800, 600), 0, new Point(0, 0));
            controller.PresetPosition(new PointD(100, 200), 2.5);
            _ = controller.UpdateFrameState();
            controller.NotifyFrameRendered();
            Vector2 screenPosition = controller.WorldToScreenCoordinates(new PointD(120, 240));

            controller.SyncViewport(bounds, new Point(1040, 840));
            _ = controller.UpdateFrameState();

            Assert.AreEqual((screenPosition, true),
                (controller.WorldToScreenCoordinates(new PointD(120, 240)), controller.ConsumeRedrawRequested()));
        }

        [TestMethod]
        public void WhenResizedViewportPansThenContentMovesOnlyByTheRequestedPixelDelta()
        {
            MapViewportState viewport = new(new MapViewportBounds(-1000, -1000, 1000, 1000));
            viewport.ResetSize(new MapViewportSize(800, 600), 0);
            viewport.PresetPosition(new PointD(100, 200), 1.3);
            PointD worldPoint = new(120, 240);
            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));
            PointD original = viewport.WorldToScreenCoordinates(worldPoint);

            viewport.UpdatePosition(20, -35);

            PointD actual = viewport.WorldToScreenCoordinates(worldPoint);
            double error = Math.Max(Math.Abs(actual.X - (original.X + 20)), Math.Abs(actual.Y - (original.Y - 35)));
            Assert.AreEqual(0, error, 1e-8);
        }

        [TestMethod]
        public void WhenResizedViewportZoomsAtThePointerThenItsWorldPointRemainsUnderThePointer()
        {
            MapViewportState viewport = CreateViewport();
            viewport.PresetPosition(new PointD(100, 200), 1.3);
            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));
            PointD pointerWorldPoint = viewport.ScreenToWorldCoordinates(150, 250);

            viewport.UpdateScaleAt(150, 250, 1, 100);

            PointD actual = viewport.WorldToScreenCoordinates(pointerWorldPoint);
            double error = Math.Max(Math.Abs(actual.X - 150), Math.Abs(actual.Y - 250));
            Assert.AreEqual(0, error, 1e-8);
        }

        [TestMethod]
        public void WhenZoomedViewportResizesThenWorldContentKeepsItsScreenPosition()
        {
            MapViewportState viewport = CreateViewport();
            viewport.PresetPosition(new PointD(100, 200), 1.3);
            viewport.UpdateScaleAt(150, 250, 1, 100);
            PointD worldPoint = new(120, 240);
            PointD original = viewport.WorldToScreenCoordinates(worldPoint);

            viewport.UpdateWindowSize(new MapViewportSize(1040, 840));

            PointD actual = viewport.WorldToScreenCoordinates(worldPoint);
            double error = Math.Max(Math.Abs(actual.X - original.X), Math.Abs(actual.Y - original.Y));
            Assert.AreEqual(0, error, 1e-8);
        }

        private static MapViewportState CreateViewport()
        {
            MapViewportState viewport = new(new MapViewportBounds(-1000, 1000, 1000, -1000));
            viewport.ResetSize(new MapViewportSize(800, 600), 0);
            viewport.PresetPosition(new PointD(100, 200), 2.5);
            return viewport;
        }
    }
}
