
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrackSegment : VectorWidget, INameValueInformationProvider
    {
        [ThreadStatic]
        private protected static NameValueCollection debugInformation = new NameValueCollection() { ["Node Type"] = "Vector Section" };

        internal readonly bool Curved;

        // Direction in Rad from -π to π from North (0) to South
        internal float Direction;
        internal float Length { get; private protected set; }
        // Angular Size (Length) of the Arc in Degree
        internal float Angle { get; private protected set; }
        internal float Radius { get; private protected set; }

        private protected PointD centerPoint;
        private protected float centerToStartDirection;
        private protected float centerToEndDirection;

        public NameValueCollection DebugInfo
        {
            get
            {
                debugInformation["Node Index"] = TrackNodeIndex.ToString(CultureInfo.InvariantCulture);
                debugInformation["Section Index"] = TrackVectorSectionIndex.ToString(CultureInfo.InvariantCulture);
                debugInformation["Curved"] = Curved.ToString(CultureInfo.InvariantCulture);
                debugInformation["Length"] = $"{Length:F1}m";
                debugInformation["Direction"] = $"{MathHelper.ToDegrees(MathHelper.WrapAngle(Direction - MathHelper.PiOver2)):F1}º";
                debugInformation["Radius"] = Curved ? $"{Radius:F1}m" : "n/a";
                debugInformation["Angle"] = Curved ? $"{MathHelper.ToDegrees(Angle):F1}º" : "n/a";
                return debugInformation;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions => null;

        internal readonly int TrackNodeIndex;
        internal readonly int TrackVectorSectionIndex;

        public TrackSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex)
        {
            ref readonly WorldLocation location = ref trackVectorSection.Location;
            double cosA = Math.Cos(trackVectorSection.Direction.Y);
            double sinA = Math.Sin(trackVectorSection.Direction.Y);

            base.location = PointD.FromWorldLocation(location);
            tile = new Tile(location.TileX, location.TileZ);

            TrackSection trackSection = trackSections.TryGet(trackVectorSection.SectionIndex);

            if (null == trackSection)
                return;
//                throw new System.IO.InvalidDataException($"TrackVectorSection {trackVectorSection.SectionIndex} not found in TSection.dat");

            Size = trackSection.Width;
            Curved = trackSection.Curved;
            Direction = MathHelper.WrapAngle(trackVectorSection.Direction.Y - MathHelper.PiOver2);
            TrackNodeIndex = trackNodeIndex;
            TrackVectorSectionIndex = trackVectorSectionIndex;

            if (trackSection.Curved)
            {
                Angle = MathHelper.ToRadians(trackSection.Angle);
                Radius = trackSection.Radius;
                Length = trackSection.Length;

                int sign = -Math.Sign(trackSection.Angle);

                double angleRadians = MathHelper.ToRadians(trackSection.Angle);
                double cosArotated = Math.Cos(trackVectorSection.Direction.Y + angleRadians);
                double sinArotated = Math.Sin(trackVectorSection.Direction.Y + angleRadians);
                double deltaX = sign * trackSection.Radius * (cosA - cosArotated);
                double deltaZ = sign * trackSection.Radius * (sinA - sinArotated);
                vectorEnd = new PointD(location.TileX * WorldLocation.TileSize + location.Location.X - deltaX, location.TileZ * WorldLocation.TileSize + location.Location.Z + deltaZ);

                centerPoint = base.location - (new PointD(Math.Sin(Direction), Math.Cos(Direction)) * Math.Sign(Angle) * Radius);
                centerToStartDirection = MathHelper.WrapAngle(Direction - (Math.Sign(Angle) * MathHelper.PiOver2));
                centerToEndDirection = MathHelper.WrapAngle(centerToStartDirection + Angle);
            }
            else
            {
                Length = trackSection.Length;

                // note, angle is 90 degrees off, and different sign. 
                // So Delta X = cos(90-A)=sin(A); Delta Y,Z = sin(90-A) = cos(A)    
                vectorEnd = new PointD(location.TileX * WorldLocation.TileSize + location.Location.X + sinA * Length, location.TileZ * WorldLocation.TileSize + location.Location.Z + cosA * Length);
            }

            otherTile = new Tile(Tile.TileFromAbs(vectorEnd.X), Tile.TileFromAbs(vectorEnd.Y));
        }

        protected TrackSegment(TrackSegment source)
        {
            location = source.location;
            tile = source.tile;
            otherTile = source.otherTile;
            Size = source.Size;
            vectorEnd = source.vectorEnd;
            Curved = source.Curved;
            Direction = source.Direction;
            Length = source.Length;
            TrackNodeIndex = source.TrackNodeIndex;
            TrackVectorSectionIndex = source.TrackVectorSectionIndex;
            Angle = source.Angle;
            Radius = source.Radius;
            centerPoint = source.centerPoint;
            centerToStartDirection = source.centerToStartDirection;
            centerToEndDirection = source.centerToEndDirection;
        }

        private protected TrackSegment()
        { }

        private protected TrackSegment(TrackSegment source, float remainingLength, float startOffset, bool reverse) : this(source)
        {
            if (startOffset == 0 && remainingLength >= Length)//full path segment
                return;
            //remainingLength is in m down the track, startOffset is either in m for straight, or in Rad for Curved
            if (Curved)
            {
                int sign = Math.Sign(Angle);
                float remainingArc = remainingLength / Radius * sign;
                float remainingDeg = MathHelper.ToDegrees(remainingArc);

                if (reverse)
                {
                    if (startOffset != 0)
                        Angle = startOffset * sign;
                    if (Math.Abs(remainingDeg) < Math.Abs(Angle))
                    {
                        Direction += Angle - remainingArc;
                        Angle = remainingArc;
                        location = centerPoint + new PointD(-sign * Math.Cos(Direction + MathHelper.PiOver2) * Radius, sign * Math.Sin(Direction + MathHelper.PiOver2) * Radius);
                    }
                }
                else
                {
                    Direction += sign * startOffset;
                    location = centerPoint + new PointD(-sign * Math.Cos(Direction + MathHelper.PiOver2) * Radius, sign * Math.Sin(Direction + MathHelper.PiOver2) * Radius);
                    Angle -= sign * startOffset;
                    if (Math.Abs(remainingDeg) < Math.Abs(Angle))
                        Angle = remainingDeg;
                }
                Angle += 0.01f * sign;  // there seems to be a small rounding error somewhere leading to tiny gap in some cases
                Length = Radius * Angle * sign;
            }
            else
            {
                float endOffset = 0;
                if (reverse)
                {
                    if (startOffset == 0)
                        startOffset = Length;
                    else
                        Length = startOffset;
                    if (remainingLength < startOffset)
                    {
                        endOffset = startOffset - remainingLength;
                        Length = remainingLength;
                    }
                    (startOffset, endOffset) = (endOffset, startOffset);
                }
                else
                {
                    Length -= startOffset;
                    endOffset = Length;
                    if (remainingLength + startOffset < Length)
                    {
                        endOffset = remainingLength + startOffset;
                        Length = remainingLength;
                    }
                }

                double dx = vectorEnd.X - location.X;
                double dy = vectorEnd.Y - location.Y;
                double scale = startOffset / source.Length;
                location = new PointD(location.X + dx * scale, location.Y + dy * scale);
                scale = endOffset / source.Length;
                vectorEnd = new PointD(location.X + dx * scale, location.Y + dy * scale);
            }
        }

        private protected TrackSegment(TrackSegment source, in PointD start, in PointD end) : this(source)
        {
            bool reverse = false;

            //figure which end is closer to start vs end
            if (start.DistanceSquared(location) > start.DistanceSquared(vectorEnd) && end.DistanceSquared(location) < end.DistanceSquared(vectorEnd))
                reverse = true;

            if (reverse)
            {
                location = end;
                vectorEnd = start;
            }
            else
            {
                location = start;
                vectorEnd = end;
            }

            if (Curved)
            {
                PointD deltaStart = location - centerPoint;
                float deltaAngle = (float)Math.Atan2(deltaStart.X, deltaStart.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(centerToStartDirection - deltaAngle);
                Direction -= deltaAngle;
                Angle += deltaAngle;
                PointD deltaEnd = vectorEnd - centerPoint;
                deltaAngle = (float)Math.Atan2(deltaEnd.X, deltaEnd.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(deltaAngle - centerToEndDirection);
                Angle += deltaAngle;
            }
            else
            {
                Length = (float)end.Distance(start);
            }
        }


        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<TrackSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

        #region math
        public override double DistanceSquared(in PointD point)
        {
            if (Curved)
            {
                PointD delta = point - centerPoint;
                float angle = MathHelper.WrapAngle((float)Math.Atan2(delta.X, delta.Y) - MathHelper.PiOver2);
                if (Angle < 0 && ((angle < centerToStartDirection && angle > centerToEndDirection) ||
                    (centerToStartDirection < centerToEndDirection && (angle > centerToEndDirection || angle < centerToStartDirection)))
                   || (Angle > 0 && ((angle > centerToStartDirection && angle < centerToEndDirection) ||
                   (centerToStartDirection > centerToEndDirection && (angle > centerToStartDirection || angle < centerToEndDirection)))))
                    return Math.Abs((centerPoint.Distance(point) - Radius) * (centerPoint.Distance(point) - Radius));

                return double.NaN;
            }
            else
            {
                double distanceSquared = vectorEnd.DistanceSquared(location);
                if (distanceSquared < double.Epsilon)
                {
                    // It's a point not a line segment.
                    return point.DistanceSquared(location);
                }
                // Calculate the t that minimizes the distance.
                double t = (point - location).DotProduct(vectorEnd - location) / distanceSquared;

                // if t < 0 or > 1 the point is basically not perpendicular to the line, so we return NaN
                // (else if needed should return the distance from either start or end point)
                if (t < 0 || t > 1)
                    return double.NaN;
                return point.DistanceSquared(location + (vectorEnd - location) * t);
            }
        }
        #endregion
    }

    internal class RoadSegment : TrackSegment
    {
        public RoadSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex) :
            base(trackVectorSection, trackSections, trackNodeIndex, trackVectorSectionIndex)
        {
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<RoadSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
