using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal abstract record NamedTrackItem : PointPrimitive, IDrawable<PointPrimitive>
    {
        private protected float direction;

        public string Name { get; }

        internal int Count { get; }

        private protected static PointD MidPointLocationOnSegment<T>(TrackSegmentPathBase<T> source) where T : TrackSegmentBase
        {
            (T segment, float remainingDistance) = source.SegmentAt(source.Length / 2);
            return segment?.LocationAt(remainingDistance) ?? source.MidPoint;
        }

        protected NamedTrackItem(in PointD location, string name, int itemCount = 1) : base(location)
        {
            Name = name;
            Count = itemCount;
        }

        public abstract void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);
    }

    internal record StationNameItem : NamedTrackItem
    {
        /// <summary>Top-left corner of the station extent: the union of all its platforms' bounds.</summary>
        public PointD TopLeftBound { get; }

        /// <summary>Bottom-right corner of the station extent: the union of all its platforms' bounds.</summary>
        public PointD BottomRightBound { get; }

        public StationNameItem(string name, int count, in PointD topLeftBound, in PointD bottomRightBound)
            : base(topLeftBound + (bottomRightBound - topLeftBound) / 2.0, name, count)
        {
            TopLeftBound = topLeftBound;
            BottomRightBound = bottomRightBound;
        }

        public override void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = WidgetDrawingOptions<StationNameItem>.Colors[colorVariation];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<StationNameItem>.OutlineRenderOptions;
            if ((Count > 2 && renderer.Scale < 0.3) || (Count > 1 && renderer.Scale < 0.1) || renderer.Scale >= 0.02)
                renderer.DrawText(Location, fontColor, Name, renderer.ConstantSizeFont, Vector2.One, 0, HorizontalAlignment.Center, VerticalAlignment.Top, outlineRenderOptions);
        }

        public static IEnumerable<StationNameItem> CreateStationItems(IEnumerable<IGrouping<string, PlatformPath>> stationPlatforms)
        {
            foreach (IGrouping<string, PlatformPath> item in stationPlatforms)
            {
                int count = 0;
                // Accumulate the union of all platform bounds: the station covers the full extent of its
                // platforms, and its center (label/navigation point) is derived from those bounds in the
                // constructor. PointD Y increases upward, so the top-left corner uses the max Y.
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach (PlatformPath platform in item)
                {
                    count++;
                    minX = Math.Min(minX, platform.TopLeftBound.X);
                    maxY = Math.Max(maxY, platform.TopLeftBound.Y);
                    maxX = Math.Max(maxX, platform.BottomRightBound.X);
                    minY = Math.Min(minY, platform.BottomRightBound.Y);
                }

                yield return new StationNameItem(item.Key, count, new PointD(minX, maxY), new PointD(maxX, minY));
            }
        }
    }

    internal record PlatformNameItem : NamedTrackItem
    {
        public PlatformNameItem(PlatformPath source) : base(MidPointLocationOnSegment(source), source.PlatformName)
        {
            direction = source.DirectionAt(source.Length / 2);
            if (System.Math.Abs(direction) > MathHelper.PiOver2)
                direction -= MathHelper.Pi;
        }

        public override void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = WidgetDrawingOptions<PlatformNameItem>.Colors[ColorVariation.None];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<PlatformNameItem>.OutlineRenderOptions;
            renderer.DrawText(Location, fontColor, Name, renderer.CurrentFont, Vector2.One, direction, HorizontalAlignment.Center, VerticalAlignment.Bottom, outlineRenderOptions);
        }
    }

    internal record SidingNameItem : NamedTrackItem
    {
        public SidingNameItem(SidingPath source) : base(MidPointLocationOnSegment(source), source.SidingName)
        {
            direction = source.DirectionAt(source.Length / 2);
            if (System.Math.Abs(direction) > MathHelper.PiOver2)
                direction -= MathHelper.Pi;
        }

        public override void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = WidgetDrawingOptions<SidingNameItem>.Colors[ColorVariation.None];
            OutlineRenderOptions outlineRenderOptions = WidgetDrawingOptions<SidingNameItem>.OutlineRenderOptions;
            renderer.DrawText(Location, fontColor, Name, renderer.CurrentFont, Vector2.One, direction, HorizontalAlignment.Center, VerticalAlignment.Bottom, outlineRenderOptions);
        }
    }

}
