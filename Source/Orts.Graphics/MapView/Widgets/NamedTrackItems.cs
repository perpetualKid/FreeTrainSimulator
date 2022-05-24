using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Graphics.DrawableComponents;

namespace Orts.Graphics.MapView.Widgets
{
    internal abstract class NamedTrackItem : PointPrimitive, IDrawable<PointPrimitive>
    {
        public string Name { get; }

        internal int Count { get; }

        protected NamedTrackItem(in PointD location, string name, int itemCount = 1): base(location)
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
            Color fontColor = this.GetColor<PlatformPath>(colorVariation);
            if ((Count > 2 && contentArea.Scale < 0.3) || (Count > 1 && contentArea.Scale < 0.1) || contentArea.Scale >= 0.1)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(Location), fontColor, Name, contentArea.ConstantSizeFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
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
        public PlatformNameItem(PlatformPath source): base(source.MidPoint, source.PlatformName)
        { }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<PlatformPath>(colorVariation);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(Location), fontColor, Name, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Bottom, SpriteEffects.None, contentArea.SpriteBatch);
        }
    }

    internal class SidingNameItem : NamedTrackItem
    {
        public SidingNameItem(SidingPath source) : base(source.MidPoint, source.SidingName)
        { }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = this.GetColor<SidingPath>(colorVariation);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(Location), fontColor, Name, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Center, SpriteEffects.None, contentArea.SpriteBatch);
        }
    }

}
