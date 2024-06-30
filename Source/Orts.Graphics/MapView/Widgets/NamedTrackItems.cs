using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal abstract class NamedTrackItem : PointPrimitive, IDrawable<PointPrimitive>
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

    internal class StationNameItem : NamedTrackItem
    {
        public StationNameItem(in PointD location, string name, int count = 1) : base(location, name, count)
        {

        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<StationNameItem>(colorVariation);
            if ((Count > 2 && contentArea.Scale < 0.3) || (Count > 1 && contentArea.Scale < 0.1) || contentArea.Scale >= 0.02)
                contentArea.DrawText(Location, fontColor, Name, contentArea.ConstantSizeFont, Vector2.One, 0, HorizontalAlignment.Center, VerticalAlignment.Top);
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

    internal class PlatformNameItem : NamedTrackItem
    {
        public PlatformNameItem(PlatformPath source) : base(MidPointLocationOnSegment(source), source.PlatformName)
        {
            direction = source.DirectionAt(source.Length / 2);
            if (System.Math.Abs(direction) > MathHelper.PiOver2)
                direction -= (MathHelper.Pi);
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<PlatformPath>(colorVariation);
            contentArea.DrawText(Location, fontColor, Name, contentArea.CurrentFont, Vector2.One, direction, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    internal class SidingNameItem : NamedTrackItem
    {
        public SidingNameItem(SidingPath source) : base(MidPointLocationOnSegment(source), source.SidingName)
        {
            direction = source.DirectionAt(source.Length / 2);
            if (System.Math.Abs(direction) > MathHelper.PiOver2)
                direction -= (MathHelper.Pi);
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<SidingPath>(colorVariation);
            contentArea.DrawText(Location, fontColor, Name, contentArea.CurrentFont, Vector2.One, direction, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

}
