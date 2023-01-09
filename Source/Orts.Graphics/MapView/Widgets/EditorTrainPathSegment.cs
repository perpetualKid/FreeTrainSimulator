
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EditorTrainPathSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
    {
        public EditorTrainPathSegment(TrackSegmentBase source) : base(source)
        {
        }

        public EditorTrainPathSegment(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
        }

        public EditorTrainPathSegment(in PointD start, in PointD end): base(start, end)
        {
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<EditorTrainPathSegment>(colorVariation);

            Size = MathHelper.Max(0.5f, (float)(2 / contentArea.Scale));
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
