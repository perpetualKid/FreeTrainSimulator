using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{

    internal class SidingSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
    {
        public SidingSegment(TrackSegmentBase source) : base(source)
        {
            Size = 3;
        }

        public SidingSegment(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
            Size = 3;
        }

        public SidingSegment(in PointD start, in PointD end) : base(start, end)
        {
            Size = 3;
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<SidingSegment>(colorVariation);
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
