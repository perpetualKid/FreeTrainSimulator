using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record PathSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
    {
        private protected PathSegment() : base()
        { }

        public PathSegment(TrackSegmentBase source, float remainingLength, float startOffset, bool reverse) : base(source, remainingLength, startOffset, reverse)
        {
        }

        public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<PathSegment>.Colors[colorVariation];
            if (Curved)
                renderer.DrawArc(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenSize(Radius), Direction, Angle);
            else
                renderer.DrawLine(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenSize(Length), Direction);
        }
    }

    internal record BrokenPathSegment : PathSegment
    {
        public BrokenPathSegment(in WorldLocation location) : base()
        {
            SetLocation(location);
        }

        public override void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<PathSegment>.Colors[colorVariation];
            Size = renderer.Scale switch
            {
                double i when i < 0.5 => 40,
                double i when i < 0.75 => 25,
                double i when i < 1 => 18,
                double i when i < 3 => 12,
                double i when i < 5 => 8,
                double i when i < 8 => 6,
                _ => 4,
            };
            renderer.DrawTexture(BasicTextureType.RingCrossed, renderer.WorldToScreenCoordinates(in Location), 0, renderer.WorldToScreenSize(Size * scaleFactor), drawColor);
        }
    }

}
