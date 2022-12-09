
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrainPathSegment : TrackSegmentBase, IDrawable<VectorPrimitive>
    {
        public TrainPathSegment(TrackSegmentBase source) : base(source)
        {
        }

        public TrainPathSegment(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
        }

        public TrainPathSegment(in PointD start, in PointD end): base(start, end)
        {
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<TrainPathSegment>(colorVariation);
            Size = contentArea.Scale switch
            {
                double i when i < 0.02 => 50,
                double i when i < 0.03 => 40,
                double i when i < 0.05 => 30,
                double i when i < 0.1 => 20,
                double i when i < 0.2 => 15,
                double i when i < 0.3 => 10,
                double i when i < 0.5 => 7,
                double i when i < 1 => 5,
                double i when i < 3 => 2,
                _ => 1,
            };
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
