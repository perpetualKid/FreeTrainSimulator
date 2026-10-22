using System;

using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Graphics.MapView
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class MapRenderAdapterOwnershipTests
    {
        [TestMethod]
        [DataRow(ColorSetting.StationItem)]
        [DataRow(ColorSetting.PlatformItem)]
        [DataRow(ColorSetting.SidingItem)]
        [DataRow(ColorSetting.SpeedPostItem)]
        [DataRow(ColorSetting.MilePostItem)]
        public void OutlineReplacementAndDisableReleaseSupersededGdiOptions(ColorSetting setting)
        {
            switch (setting)
            {
                case ColorSetting.StationItem:
                    CheckReplacement<StationNameItem>(setting, 3);
                    break;
                case ColorSetting.PlatformItem:
                    CheckReplacement<PlatformNameItem>(setting, 2);
                    break;
                case ColorSetting.SidingItem:
                    CheckReplacement<SidingNameItem>(setting, 2);
                    break;
                case ColorSetting.SpeedPostItem:
                    CheckReplacement<SpeedPostTrackItem>(setting, 2);
                    break;
                case ColorSetting.MilePostItem:
                    CheckReplacement<MilePostTrackItem>(setting, 2);
                    break;
            }
        }

        private static void CheckReplacement<T>(ColorSetting setting, float expectedWidth)
        {
            OutlineRenderOptions original = WidgetDrawingOptions<T>.OutlineRenderOptions;
            // Preserve global widget state, including colors changed as a side effect by the adapter.
            Color[] colors = (Color[])WidgetDrawingOptions<T>.Colors.ToArray().Clone();
            Color[] platformPathColors = (Color[])WidgetDrawingOptions<PlatformPath>.Colors.ToArray().Clone();
            Color[] platformSegmentColors = (Color[])WidgetDrawingOptions<PlatformSegment>.Colors.ToArray().Clone();
            Color[] sidingPathColors = (Color[])WidgetDrawingOptions<SidingPath>.Colors.ToArray().Clone();
            Color[] sidingSegmentColors = (Color[])WidgetDrawingOptions<SidingSegment>.Colors.ToArray().Clone();
            TrackingOutlineOptions previous = new TrackingOutlineOptions();
            OutlineRenderOptions latest = previous;

            void Update(MapRenderAdapter map, Color color, bool outline)
            {
                try
                {
                    map.UpdateColor(null, setting, color, outline);
                }
                finally
                {
                    latest = WidgetDrawingOptions<T>.OutlineRenderOptions ?? latest;
                }
            }

            WidgetDrawingOptions<T>.OutlineRenderOptions = previous;
            try
            {
                MapRenderAdapter firstMap = new MapRenderAdapter(null, null);
                MapRenderAdapter siblingMap = new MapRenderAdapter(null, null);
                Update(firstMap, Color.White, true);
                OutlineRenderOptions first = latest;
                Assert.AreEqual(1, previous.ReleaseCount);
                Assert.AreEqual(expectedWidth, first.OutlineWidth);

                // The shared slot, not either map instance, owns the currently published options.
                Update(siblingMap, Color.Black, true);
                OutlineRenderOptions second = latest;
                Assert.AreNotSame(first, second);
                AssertDisposed(first);
                Assert.AreEqual(expectedWidth, second.OutlineWidth);

                Update(firstMap, Color.White, false);
                Assert.IsNull(WidgetDrawingOptions<T>.OutlineRenderOptions);
                AssertDisposed(second);
                Update(firstMap, Color.White, false);
                Assert.AreEqual(1, previous.ReleaseCount);
            }
            finally
            {
                try
                {
                    latest.Dispose();
                }
                finally
                {
                    WidgetDrawingOptions<T>.OutlineRenderOptions = original;
                    WidgetDrawingOptions<T>.Colors.FromArray(colors);
                    WidgetDrawingOptions<PlatformPath>.Colors.FromArray(platformPathColors);
                    WidgetDrawingOptions<PlatformSegment>.Colors.FromArray(platformSegmentColors);
                    WidgetDrawingOptions<SidingPath>.Colors.FromArray(sidingPathColors);
                    WidgetDrawingOptions<SidingSegment>.Colors.FromArray(sidingSegmentColors);
                }
            }
        }

        private static void AssertDisposed(OutlineRenderOptions options)
        {
            // GDI rejects operations on the released pen and brush; no GraphicsDevice is involved.
            _ = Assert.ThrowsExactly<ArgumentException>(() => _ = options.Pen.Width);
            _ = Assert.ThrowsExactly<ArgumentException>(() =>
            {
                using (System.Drawing.Brush clone = (System.Drawing.Brush)options.FillBrush.Clone())
                {
                }
            });
        }

        private sealed class TrackingOutlineOptions : OutlineRenderOptions
        {
            /// <summary>Number of replacement-triggered disposal calls.</summary>
            public int ReleaseCount { get; private set; }

            public TrackingOutlineOptions() : base(2, System.Drawing.Color.Black, System.Drawing.Color.White)
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    ReleaseCount++;
                base.Dispose(disposing);
            }
        }
    }
}
