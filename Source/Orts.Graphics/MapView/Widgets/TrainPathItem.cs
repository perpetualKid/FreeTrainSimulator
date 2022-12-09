using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Graphics.MapView.Shapes;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrainPathItem : PointPrimitive, IDrawable<PointPrimitive>
    {
        private protected readonly BasicTextureType textureType;
        private protected float Direction;

        internal TrainPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection): base(location)
        {
            textureType = nodeType switch
            {
                PathNodeType.Start => BasicTextureType.PathStart,
                PathNodeType.End => BasicTextureType.PathEnd,
                PathNodeType.Normal => BasicTextureType.PathNormal,
                PathNodeType.Intermediate => BasicTextureType.PathNormal,
                PathNodeType.Wait => BasicTextureType.PathWait,
                PathNodeType.SidingStart => BasicTextureType.PathNormal,
                PathNodeType.SidingEnd => BasicTextureType.PathNormal,
                PathNodeType.Reversal => BasicTextureType.PathReverse,
                PathNodeType.Temporary => BasicTextureType.RingCrossed,
                _ => throw new NotImplementedException(),
            };
            Direction = trackSegment.DirectionAt(Location) + (reverseDirection ? MathHelper.Pi : 0) + MathHelper.PiOver2;
        }

        public void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.3 => 30,
                double i when i < 0.5 => 20,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };
            contentArea.BasicShapes.DrawTexture(textureType, contentArea.WorldToScreenCoordinates(in Location), Direction, contentArea.WorldToScreenSize(Size * scaleFactor), Color.White, contentArea.SpriteBatch);
        }
    }
}
