using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Imported.Track;

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

        public abstract void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);
    }

    internal record StationNameItem : NamedTrackItem
    {
        public StationNameItem(in PointD location, string name, int count = 1) : base(location, name, count)
        {

        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<StationNameItem>(colorVariation);
            OutlineRenderOptions outlineRenderOptions = this.GetOutlineColorOptions<StationNameItem>();
            if ((Count > 2 && contentArea.Scale < 0.3) || (Count > 1 && contentArea.Scale < 0.1) || contentArea.Scale >= 0.02)
                contentArea.DrawText(Location, fontColor, Name, contentArea.ConstantSizeFont, Vector2.One, 0, HorizontalAlignment.Center, VerticalAlignment.Top, outlineRenderOptions);
        }

        public static IEnumerable<StationNameItem> CreateStationItems(IEnumerable<IGrouping<string, PlatformPath>> stationPlatforms)
        {
            foreach (IGrouping<string, PlatformPath> item in stationPlatforms)
            {
                int count = 0;
                double x = 0, y = 0;
                foreach (PlatformPath platform in item)
                {
                    x += platform.MidPoint.X;
                    y += platform.MidPoint.Y;
                    count++;
                }
                x /= count;
                y /= count;
                PointD location = new PointD(x, y);

                yield return new StationNameItem(location, item.Key, count);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<PlatformPath>(ColorVariation.None);
            OutlineRenderOptions outlineRenderOptions = this.GetOutlineColorOptions<PlatformPath>();
            contentArea.DrawText(Location, fontColor, Name, contentArea.CurrentFont, Vector2.One, direction, HorizontalAlignment.Center, VerticalAlignment.Center, outlineRenderOptions);
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

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<SidingPath>(ColorVariation.None);
            OutlineRenderOptions outlineRenderOptions = this.GetOutlineColorOptions<SidingPath>();
            contentArea.DrawText(Location, fontColor, Name, contentArea.CurrentFont, Vector2.One, direction, HorizontalAlignment.Center, VerticalAlignment.Center, outlineRenderOptions);
        }
    }

}
