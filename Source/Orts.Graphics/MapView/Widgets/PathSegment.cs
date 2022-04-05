
using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class PathSegment : TrackSegment
    {
        private protected PathSegment()
        { }

        public PathSegment(TrackSegment source, float remainingLength, float startOffset, bool reverse) : base(source, remainingLength, startOffset, reverse)
        {
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PathSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }

    internal class BrokenPathSegment : PathSegment
    {
        public BrokenPathSegment(in WorldLocation location) : base()
        {
            base.location = PointD.FromWorldLocation(location);
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PathSegment>(colorVariation);
            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => 40,
                double i when i < 0.75 => 25,
                double i when i < 1 => 18,
                double i when i < 3 => 12,
                double i when i < 5 => 8,
                double i when i < 8 => 6,
                _ => 4,
            };
            BasicShapes.DrawTexture(BasicTextureType.RingCrossed, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }

}
