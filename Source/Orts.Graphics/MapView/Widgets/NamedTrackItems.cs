using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Graphics.DrawableComponents;

namespace Orts.Graphics.MapView.Widgets
{
    internal abstract class NamedTrackItem : PointWidget
    {
        public string Name { get; }

        internal int Count { get; }

        protected NamedTrackItem(in PointD location, in Tile tile, string name, int itemCount = 1)
        {
            base.location = location;
            base.tile = tile;
            Name = name;
            Count = itemCount;
        }
    }

    internal class StationNameItem : NamedTrackItem
    {
        public StationNameItem(in PointD location, in Tile tile, string name, int count = 1) : base(location, tile, name, count)
        {

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color fontColor = GetColor<PlatformPath>(colorVariation);
            if ((Count > 2 && contentArea.Scale < 0.3) || (Count > 1 && contentArea.Scale < 0.1) || contentArea.Scale >= 0.1)
                TextShape.DrawString(contentArea.WorldToScreenCoordinates(location), fontColor, Name, contentArea.ConstantSizeFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Center, SpriteEffects.None, contentArea.SpriteBatch);
        }

        public static List<StationNameItem> CreateStationItems(IEnumerable<IGrouping<string, PlatformPath>> stationPlatforms)
        {
            List<StationNameItem> result = new List<StationNameItem>();
            foreach(IGrouping<string, PlatformPath> item in stationPlatforms)
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
                WorldLocation worldLocation = PointD.ToWorldLocation(location);
                Tile tile = new Tile(worldLocation.TileX, worldLocation.TileZ);
                result.Add(new StationNameItem(location, tile, item.Key, count));
            }
            return result;
        }
    }
}
