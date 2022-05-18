using System.Collections.Specialized;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrainPathSegment : SegmentBase
    {
        public override NameValueCollection DebugInfo => null;

        public TrainPathSegment(SegmentBase source) : base(source)
        {
            Size = 5;
        }

        public TrainPathSegment(SegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
            Size = 5;
        }

        public TrainPathSegment(in PointD start, in PointD end): base(start, end)
        {
            Size = 5;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<TrainPathSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
