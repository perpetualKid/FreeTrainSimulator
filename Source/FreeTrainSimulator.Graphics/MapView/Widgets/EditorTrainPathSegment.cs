using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record EditorTrainPathSegment : TrainPathSegmentBase, IDrawable<VectorPrimitive>
    {
        public EditorTrainPathSegment(TrackSegmentBase source) : base(source)
        {
        }

        public EditorTrainPathSegment(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
        }

        public EditorTrainPathSegment(in PointD start, in PointD end) : base(start, end)
        {
        }

        public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<EditorTrainPathSegment>.Colors[colorVariation];
            Size = MathHelper.Max(0.5f, (float)(2 / renderer.Scale));

            // this is bit of a hack to visualize invalid path segments, using a negative scaleFactor as flag to mark them invalid
            if (scaleFactor < 0)
            {
                scaleFactor = -scaleFactor;
                // since those are straight line only, we can just use DrawDashedLine and don't need to care for curved segments
                renderer.DrawDashedLine(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenCoordinates(in Vector));
                return;
            }

            if (Curved)
                renderer.DrawArc(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenSize(Radius), Direction, Angle);
            else
                renderer.DrawLine(renderer.WorldToScreenSize(Size * scaleFactor), drawColor, renderer.WorldToScreenCoordinates(in Location), renderer.WorldToScreenSize(Length), Direction);
        }
    }
}
