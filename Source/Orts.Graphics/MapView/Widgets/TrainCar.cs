using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    /// <summary>
    /// a whole train as composition of multiple <see cref="TrainCar"/> widgets
    /// </summary>
    public class Train : VectorWidget
    {
#pragma warning disable CA1002 // Do not expose generic lists
        public Dictionary<int, TrainCar> Cars { get; } = new Dictionary<int, TrainCar>();
#pragma warning restore CA1002 // Do not expose generic lists

        private readonly ITrain train; 

        public Train(in WorldLocation front, in WorldLocation rear, ITrain train)
        {
            location = PointD.FromWorldLocation(front);
            tile = new Tile(front.TileX, front.TileZ);
            vectorEnd = PointD.FromWorldLocation(rear);
            otherTile = new Tile(rear.TileX, rear.TileZ);
            this.train = train;
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach(TrainCar car in Cars.Values)
            {
                car.Draw(contentArea, colorVariation, scaleFactor);
            }
        }
    }

    public class TrainCar : PointWidget
    {
        // maximum width for unrestricted movement in the US, Canada, and Mexico is 10 feet, 6 inches
        // Loads less than 11 feet wide can generally move without restriction as to train handling
        private const float carSize = 3.2f;

        private float angle;
        private readonly float length;
        private Color color;

        public TrainCar(in WorldPosition position, float length, WagonType wagonType)
        {
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

        public void UpdatePosition(in WorldPosition position)
        {
            tile = new Tile(position.TileX, position.TileZ);
            //using the rotation vector 2D to get the car orientation
            angle = (float)Math.Atan2(position.XNAMatrix.Forward.Z, position.XNAMatrix.Forward.X);
            // offsetting the starting point location half the car length, since position is centre of the car
            location = PointD.FromWorldLocation(position.WorldLocation) + new PointD((-this.length * Math.Cos(angle) / 2.0), this.length * Math.Sin(angle) / 2);
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => carSize * 10f,
                double i when i < 0.75 => carSize * 5f,
                double i when i < 1 => carSize * 2f,
                double i when i < 3 => carSize * 1.5f,
                double i when i < 5 => carSize * 1.2f,
                double i when i < 8 => carSize * 1.1f,
                _ => carSize,
            };
            BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), color, contentArea.WorldToScreenCoordinates(in location),
                contentArea.WorldToScreenSize(length), angle, contentArea.SpriteBatch);
        }
    }
}
