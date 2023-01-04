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
            Direction = (trackSegment?.DirectionAt(Location) ?? 0) + (reverseDirection ? MathHelper.Pi : 0) + MathHelper.PiOver2;
        }

        public void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = Math.Max(3, (float)(8 / contentArea.Scale));
            contentArea.BasicShapes.DrawTexture(textureType, contentArea.WorldToScreenCoordinates(in Location), Direction, contentArea.WorldToScreenSize(Size * scaleFactor), Color.White, contentArea.SpriteBatch);
        }
    }
}
