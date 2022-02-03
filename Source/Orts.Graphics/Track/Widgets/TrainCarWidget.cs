using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.Track.Shapes;

namespace Orts.Graphics.Track.Widgets
{
    public class TrainCarWidget : PointWidget
    {
        private readonly float angle;
        private readonly float length;

        public TrainCarWidget(in WorldPosition position, float length, bool flipped)
        {
            Size = 3.2f;

            this.length = length;
            tile = new Tile(position.TileX, position.TileZ);
            angle = flipped ? (float)Math.Atan2(position.XNAMatrix.Backward.Z, position.XNAMatrix.Backward.X) : (float)Math.Atan2(position.XNAMatrix.Forward.Z, position.XNAMatrix.Forward.X);
            location = PointD.FromWorldLocation(position.WorldLocation) + new PointD((-length * Math.Cos(angle) / 2.0), length * Math.Sin(angle) / 2);
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size), Color.OrangeRed, contentArea.WorldToScreenCoordinates(in location),
                contentArea.WorldToScreenSize(length), angle, contentArea.SpriteBatch);
        }
    }
}
