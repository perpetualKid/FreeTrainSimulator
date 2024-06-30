
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{

    internal class PlatformSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
    {
        public PlatformSegment(TrackSegmentBase source) : base(source)
        {
            Size = 3;
        }

        public PlatformSegment(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
            Size = 3;
        }

        public PlatformSegment(in PointD start, in PointD end) : base(start, end)
        {
            Size = 3;
        }


        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<PlatformSegment>(colorVariation);
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
