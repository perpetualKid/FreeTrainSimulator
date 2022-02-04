using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    public class TrainCarWidget : PointWidget
    {
        private readonly float angle;
        private readonly float length;
        private Color color;

        public TrainCarWidget(in WorldPosition position, float length, WagonType wagonType)
        {
            // maximum width for unrestricted movement in the US, Canada, and Mexico is 10 feet, 6 inches
            // Loads less than 11 feet wide can generally move without restriction as to train handling
            Size = 3.2f; 

            this.length = length > 3 ? length -1f : length - 0.5f; //visually shortening traincar a bit to have a visible space between them
            tile = new Tile(position.TileX, position.TileZ);
            //using the rotation vector 2D to get the car orientation
            angle = (float)Math.Atan2(position.XNAMatrix.Forward.Z, position.XNAMatrix.Forward.X);
            // offsetting the starting point location half the car length, since position is centre of the car
            location = PointD.FromWorldLocation(position.WorldLocation) + new PointD((-this.length * Math.Cos(angle) / 2.0), this.length * Math.Sin(angle) / 2);
            color = wagonType switch
            {
                WagonType.Engine => Color.Red,
                WagonType.Tender => Color.DarkRed,
                WagonType.Passenger => Color.Green,
                WagonType.Freight => Color.Blue,
                _ => Color.Orange,
            };
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size), color, contentArea.WorldToScreenCoordinates(in location),
                contentArea.WorldToScreenSize(length), angle, contentArea.SpriteBatch);
        }
    }
}
