
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class PathSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
    {
        private protected PathSegment(): base()
        { }

        public PathSegment(TrackSegmentBase source, float remainingLength, float startOffset, bool reverse) : base(source, remainingLength, startOffset, reverse)
        {
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<PathSegment>(colorVariation);
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }

    internal class BrokenPathSegment : PathSegment
    {
        public BrokenPathSegment(in WorldLocation location) : base()
        {
            SetLocation(location);
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<PathSegment>(colorVariation);
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
            contentArea.BasicShapes.DrawTexture(BasicTextureType.RingCrossed, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }

}
