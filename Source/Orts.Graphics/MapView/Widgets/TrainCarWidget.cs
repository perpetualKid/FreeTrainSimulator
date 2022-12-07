using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;

namespace Orts.Graphics.MapView.Widgets
{
    /// <summary>
    /// a whole train as composition of multiple <see cref="TrainCarWidget"/> widgets
    /// </summary>
    public class TrainWidget : VectorPrimitive, IDrawable<VectorPrimitive>
    {
        public Dictionary<int, TrainCarWidget> Cars { get; } = new Dictionary<int, TrainCarWidget>();

        public ITrain Train { get; }

        public TrainWidget(in WorldLocation front, in WorldLocation rear, ITrain train)
        {
            UpdatePosition(front, rear);
            Train = train;
        }

        public void UpdatePosition(in WorldLocation front, in WorldLocation rear)
        {
            SetVector(front, rear);
        }

        public override double DistanceSquared(in PointD point)
        {
            double distance = double.MaxValue;
            foreach (TrainCarWidget car in Cars.Values)
            {
                double current = car.DistanceSquared(point);
                if (current < distance)
                    distance = current;
                if (distance <= ProximityTolerance)
                    break;
            }
            return distance; ;
        }

        public void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrainCarWidget car in Cars.Values)
            {
                ((IDrawable<PointPrimitive>)car).Draw(contentArea, colorVariation, scaleFactor);
            }
        }

        internal void DrawName(ContentArea contentArea)
        {
            Color fontColor = Color.Red;
            contentArea.DrawText(Location, fontColor, $"{Train.Number} - {Train.Name}", contentArea.ConstantSizeFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    public class TrainCarWidget : PointPrimitive, IDrawable<PointPrimitive>
    {
        // maximum width for unrestricted movement in the US, Canada, and Mexico is 10 feet, 6 inches
        // Loads less than 11 feet wide can generally move without restriction as to train handling
        private const float carSize = 3.2f;

        private float angle;
        private readonly float length;
        private Color color;

        public TrainCarWidget(in WorldPosition position, float length, WagonType wagonType): base()
        {
            this.length = length > 3 ? length - 1f : length - 0.5f; //visually shortening traincar a bit to have a visible space between them
            //using the rotation vector 2D to get the car orientation
            angle = (float)Math.Atan2(position.XNAMatrix.Forward.Z, position.XNAMatrix.Forward.X);
            // offsetting the starting point location half the car length, since position is centre of the car
            SetLocation(PointD.FromWorldLocation(position.WorldLocation) + new PointD((-this.length * Math.Cos(angle) / 2.0), this.length * Math.Sin(angle) / 2));
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
            //using the rotation vector 2D to get the car orientation
            angle = (float)Math.Atan2(position.XNAMatrix.Forward.Z, position.XNAMatrix.Forward.X);
            // offsetting the starting point location half the car length, since position is centre of the car
            SetLocation(PointD.FromWorldLocation(position.WorldLocation) + new PointD((-this.length * Math.Cos(angle) / 2.0), this.length * Math.Sin(angle) / 2));
        }

        void IDrawable<PointPrimitive>.Draw(ContentArea contentArea, ColorVariation colorVariation, double scaleFactor)
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
            contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), color, contentArea.WorldToScreenCoordinates(in Location),
                contentArea.WorldToScreenSize(length), angle, contentArea.SpriteBatch);
        }

        #region math
        public override double DistanceSquared(in PointD point)
        {
            double distanceSquared;
            PointD vectorEnd = new PointD(Location.X + Math.Cos(angle) * length, Location.Y - Math.Sin(angle) * length);
            distanceSquared = length * length;
            // Calculate the t that minimizes the distance.
            double t = (point - Location).DotProduct(vectorEnd - Location) / distanceSquared;

            // if t < 0 or > 1 the point is basically not perpendicular to the line, so we return NaN if this is even beyond the tolerance
            // (else if needed could return the distance from either start or end point)
            if (t < 0)
                return (distanceSquared = point.DistanceSquared(Location)) > ProximityTolerance ? double.NaN : distanceSquared;
            else if (t > 1)
                return (distanceSquared = point.DistanceSquared(vectorEnd)) > ProximityTolerance ? double.NaN : distanceSquared;
            return point.DistanceSquared(Location + (vectorEnd - Location) * t);
        }
        #endregion

    }
}
