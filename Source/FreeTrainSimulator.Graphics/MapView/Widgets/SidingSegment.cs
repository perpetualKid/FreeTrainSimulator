using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{

    internal record SidingSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
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

        public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<SidingSegment>.Colors[colorVariation];
            if (Curved)
                renderer.DrawArc(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenSize(Radius), Direction, Angle);
            else
                renderer.DrawLine(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenSize(Length), Direction);
        }
    }
}
