
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Models.Track;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal class EditorTrainPathSegment : TrainPathSegmentBase, IDrawable<VectorPrimitive>
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

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<EditorTrainPathSegment>(colorVariation);
            Size = MathHelper.Max(0.5f, (float)(2 / contentArea.Scale));

            // this is bit of a hack to visualize invalid path segments, using a negative scaleFactor as flag to mark them invalid
            if (scaleFactor < 0)
            {
                scaleFactor = -scaleFactor;
                // since those are straight line only, we can just use DrawDashedLine and don't need to care for curved segments
                contentArea.BasicShapes.DrawDashedLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenCoordinates(in Vector), contentArea.SpriteBatch);
                return;
            }

            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
