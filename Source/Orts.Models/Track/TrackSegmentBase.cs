using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    /// <summary>
    /// A single segment along a track, covering a single <see cref="TrackVectorSection"/> as part of a <see cref="TrackNode"/><br/>
    /// Main properties are Length, Orientation (Heading) at starting point, the endpoint
    /// and if this is a curved segment, Radius and the Angle (angular size).<br/>
    /// This is a base class for derived types like rail tracks, road tracks.<br/><br/>
    /// Multiple segments will form a path as part of a <see cref="TrackSegmentSectionBase{T}"/>, for paths following a track such as train paths, platforms, sidings.
    /// </summary>
    public abstract class TrackSegmentBase : VectorPrimitive
    {
        private protected PointD centerPoint;
        private protected float centerToStartDirection;
        private protected float centerToEndDirection;

        public bool Curved { get; }

        /// <summary>
        /// 2D-Orientation in Rad from -π to π from North (0) to South 
        /// </summary>
        public float Direction { get; private protected set; }
        /// <summary>
        /// Straigth length or Segment length of the arc on a curved track section
        /// </summary>
        public float Length { get; private protected set; }
        /// <summary>
        /// Angular Size (opening) of the Arc in Radians for curved segments
        /// </summary>
        public float Angle { get; private protected set; }
        /// <summary>
        /// Radius size (length) for curved Segments
        /// </summary>
        public float Radius { get; private protected set; }

        public int TrackNodeIndex { get; }
        public int TrackVectorSectionIndex { get; }

        protected TrackSegmentBase() : base(PointD.None, PointD.None)
        { }

        protected TrackSegmentBase(in PointD start, in PointD end) : base(start, end)
        {
            Length = (float)Vector.Distance(Location);

            PointD origin = end - start;
            Direction = (float)Math.Atan2(origin.X, origin.Y) - MathHelper.PiOver2;
        }

        protected TrackSegmentBase(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex)
        {
            ArgumentNullException.ThrowIfNull(trackVectorSection);
            ArgumentNullException.ThrowIfNull(trackSections);

            ref readonly WorldLocation location = ref trackVectorSection.Location;
            double cosA = Math.Cos(trackVectorSection.Direction.Y);
            double sinA = Math.Sin(trackVectorSection.Direction.Y);

            SetLocation(location);

            TrackNodeIndex = trackNodeIndex;
            TrackVectorSectionIndex = trackVectorSectionIndex;

            TrackSection trackSection = trackSections.TryGet(trackVectorSection.SectionIndex);

            if (null == trackSection)
            {
                Trace.TraceError($"TrackVectorSection {trackVectorSection.SectionIndex} not found in TSection.dat for section index {trackVectorSectionIndex} in track node {trackNodeIndex}.");
                return;
            }

            Size = trackSection.Width;
            Curved = trackSection.Curved;
            Direction = MathHelper.WrapAngle(trackVectorSection.Direction.Y - MathHelper.PiOver2);

            if (trackSection.Curved)
            {
                Angle = MathHelper.ToRadians(trackSection.Angle);
                Radius = trackSection.Radius;
                Length = trackSection.Length;

                int sign = -Math.Sign(trackSection.Angle);

                double cosArotated = Math.Cos(trackVectorSection.Direction.Y + Angle);
                double sinArotated = Math.Sin(trackVectorSection.Direction.Y + Angle);
                double deltaX = sign * trackSection.Radius * (cosA - cosArotated);
                double deltaZ = sign * trackSection.Radius * (sinA - sinArotated);
                SetVector(new PointD(location.TileX * WorldLocation.TileSize + location.Location.X - deltaX, location.TileZ * WorldLocation.TileSize + location.Location.Z + deltaZ));

                centerPoint = base.Location - (new PointD(Math.Sin(Direction), Math.Cos(Direction)) * -sign * Radius);
                centerToStartDirection = MathHelper.WrapAngle(Direction + (sign * MathHelper.PiOver2));
                centerToEndDirection = MathHelper.WrapAngle(centerToStartDirection + Angle);
            }
            else
            {
                Length = trackSection.Length;

                // note, angle is 90 degrees off, and different sign. 
                // So Delta X = cos(90-A)=sin(A); Delta Y,Z = sin(90-A) = cos(A)    
                SetVector(new PointD(location.TileX * WorldLocation.TileSize + location.Location.X + sinA * Length, location.TileZ * WorldLocation.TileSize + location.Location.Z + cosA * Length));
            }
        }

        protected TrackSegmentBase(TrackSegmentBase source) : base(source?.Location ?? throw new ArgumentNullException(nameof(source)), source.Vector)
        {
            Size = source.Size;
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

        /// <summary>
        /// length is in m down the track, startOffset is either in m for straight, or in Rad for Curved
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        protected TrackSegmentBase(TrackSegmentBase source, float length, float startOffset, bool reverse) : this(source)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (startOffset == 0 && length >= Length)//full path segment
                return;
            if (Curved)
            {
                int sign = Math.Sign(Angle);
                float remainingArc = length / Radius * sign;

                if (reverse)
                {
                    if (startOffset != 0)
                        Angle = startOffset * sign;
                    if (Math.Abs(remainingArc) < Math.Abs(Angle))
                    {
                        Direction += Angle - remainingArc;
                        Angle = remainingArc;
                        SetLocation(centerPoint + new PointD(sign * Math.Sin(Direction) * Radius, sign * Math.Cos(Direction) * Radius));
                    }
                }
                else
                {
                    Direction += sign * startOffset;
                    SetLocation(centerPoint + new PointD(sign * Math.Sin(Direction) * Radius, sign * Math.Cos(Direction) * Radius));
                    Angle -= sign * startOffset;
                    if (Math.Abs(remainingArc) < Math.Abs(Angle))
                        Angle = remainingArc;
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
                    if (length < startOffset)
                    {
                        endOffset = startOffset - length;
                        Length = length;
                    }
                    (startOffset, endOffset) = (endOffset, startOffset);
                }
                else
                {
                    Length -= startOffset;
                    endOffset = Length;
                    if (length + startOffset < Length)
                    {
                        endOffset = length + startOffset;
                        Length = length;
                    }
                }

                double dx = Vector.X - Location.X;
                double dy = Vector.Y - Location.Y;
                double scale = startOffset / source.Length;
                SetLocation(new PointD(Location.X + dx * scale, Location.Y + dy * scale));
                scale = endOffset / source.Length;
                SetVector(new PointD(Location.X + dx * scale, Location.Y + dy * scale));
            }
        }

        protected TrackSegmentBase(TrackSegmentBase source, in PointD start, in PointD end) : this(source)
        {
            bool reverse = false;

            //figure which end is closer to start vs end
            if (start.DistanceSquared(Location) > end.DistanceSquared(Location) && start.DistanceSquared(Vector) < end.DistanceSquared(Vector))
                reverse = true;

            //TODO 20220407 may need/want to map the start/end point onto the actual track, as they may be slightly skewed/offset from the track
            //however at this point it should already be determined that the points are perpendicular to (along) the track, and within a certain distance limit

            if (reverse)
            {
                SetVector(end, start);
            }
            else
            {
                SetVector(start, end);
            }

            if (Curved)
            {
                PointD deltaStart = Location - centerPoint;
                float deltaAngle = (float)Math.Atan2(deltaStart.X, deltaStart.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(centerToStartDirection - deltaAngle);
                Direction -= deltaAngle;
                Angle += deltaAngle;
                PointD deltaEnd = Vector - centerPoint;
                deltaAngle = (float)Math.Atan2(deltaEnd.X, deltaEnd.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(deltaAngle - centerToEndDirection);
                Angle += deltaAngle;

                int sign = -Math.Sign(Angle);
                centerPoint = base.Location - (new PointD(Math.Sin(Direction), Math.Cos(Direction)) * -sign * Radius);
                centerToStartDirection = MathHelper.WrapAngle(Direction + (sign * MathHelper.PiOver2));
                centerToEndDirection = MathHelper.WrapAngle(centerToStartDirection + Angle);
                Length = Radius * Math.Abs(Angle);
            }
            else
            {
                Length = (float)end.Distance(start);
            }
        }

        #region math
        /// <summary>
        /// Returns the distance (squared) of the given point from this track segment at the closest point, 
        /// or NaN if the point is not along (perpedicular) the track</returns>
        /// </summary>
        public override double DistanceSquared(in PointD point)
        {
            double distanceSquared;

            //if ((distanceSquared = point.DistanceSquared(Location)) < ProximityTolerance)
            //    return distanceSquared;
            //else if ((distanceSquared = point.DistanceSquared(Vector)) < ProximityTolerance)
            //    return distanceSquared;

            if (Curved)
            {
                PointD delta = point - centerPoint;
                float angle = MathHelper.WrapAngle((float)Math.Atan2(delta.X, delta.Y) - MathHelper.PiOver2);
                if (Angle < 0 && ((angle < centerToStartDirection && angle > centerToEndDirection)
                    || (centerToStartDirection < centerToEndDirection && (angle > centerToEndDirection || angle < centerToStartDirection)))
                    || (Angle > 0 && ((angle > centerToStartDirection && angle < centerToEndDirection)
                    || (centerToStartDirection > centerToEndDirection && (angle > centerToStartDirection || angle < centerToEndDirection)))))
                    return (distanceSquared = centerPoint.Distance(point) - Radius) * distanceSquared;

                if ((distanceSquared = point.DistanceSquared(Location)) < ProximityTolerance)
                    return distanceSquared;
                else if ((distanceSquared = point.DistanceSquared(Vector)) < ProximityTolerance)
                    return distanceSquared;

                //if (Angle > 0 && ((angle < centerToStartDirection) || (centerToStartDirection > centerToEndDirection && (angle > centerToStartDirection || angle < centerToEndDirection))))
                //    return (distanceSquared = point.DistanceSquared(Location)) > ProximityTolerance ? double.NaN : distanceSquared;
                //if (Angle < 0 && ((angle < centerToEndDirection) || (centerToEndDirection > centerToStartDirection && (angle > centerToEndDirection || angle < centerToStartDirection))))
                //    return (distanceSquared = point.DistanceSquared(Vector)) > ProximityTolerance ? double.NaN : distanceSquared;

                return double.NaN;
            }
            else
            {
                distanceSquared = Length * Length;
                // Calculate the t that minimizes the distance.
                double t = (point - Location).DotProduct(Vector - Location) / distanceSquared;

                // if t < 0 or > 1 the point is basically not perpendicular to the line, so we return NaN if this is even beyond the tolerance
                // (else if needed could return the distance from either start or end point)
                if (t < 0)
                    return ((distanceSquared = point.DistanceSquared(Location)) < ProximityTolerance) ? distanceSquared : double.NaN;
                else if (t > 1)
                    return ((distanceSquared = point.DistanceSquared(Vector)) < ProximityTolerance) ? distanceSquared : double.NaN;
                else
                    return point.DistanceSquared(Location + (Vector - Location) * t);
                //return (t < 0 || t > 1 || (distanceSquared = point.DistanceSquared(Location + (Vector - Location) * t)) > ProximityTolerance) ? double.NaN : distanceSquared;
                //return (t < 0 || t > 1) ? double.NaN : point.DistanceSquared(Location + (Vector - Location) * t);
            }
        }
        #endregion

        /// <summary>
        /// Returns the segment at a given location, or null of not found
        /// </summary>
        public static TrackSegmentBase SegmentBaseAt(in PointD location, IEnumerable<TrackSegmentBase> segments)
        {
            foreach (TrackSegmentBase segment in segments ?? Enumerable.Empty<TrackSegmentBase>())
            {
                if (segment.DistanceSquared(location) <= ProximityTolerance)
                {
                    return segment;
                }
            }
            return null;
        }

        public bool TrackSegmentAt(in PointD location)
        {
            return DistanceSquared(location) <= ProximityTolerance;
        }

        /// <summary>
        /// Direction (Heading from North) at an arbitrary point along the current track segment
        /// </summary>
        public float DirectionAt(in PointD location)
        {
            if (Curved)
            {
                PointD delta = location - centerPoint;
                float deltaAngle = (float)Math.Atan2(delta.X, delta.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(centerToStartDirection - deltaAngle);
                return Direction - deltaAngle;
            }
            else
            {
                return Direction;
            }
        }

        /// <summary>
        /// Direction (Heading from North) at point at distance along the current track segment
        /// </summary>
        public float DirectionAt(float distance)
        {
            return Curved ? Direction + Math.Sign(Angle) * distance / Radius : Direction;
        }

        public PointD LocationAt(float distance)
        {
            if (Curved)
            {
                int sign = Math.Sign(Angle);
                double direction = Direction + sign * distance / Radius;
                return (centerPoint + new PointD(sign * Math.Sin(direction) * Radius, sign * Math.Cos(direction) * Radius));
            }
            else
            {
                double dx = Vector.X - Location.X;
                double dy = Vector.Y - Location.Y;
                return (new PointD(Location.X + dx * distance / Length, Location.Y + dy * distance / Length));
            }
        }

        /// <summary>
        /// On a single track segment section (same track node index), checks if direction from start to end aligns with track direction or is reverse
        /// </summary>
        public bool IsReverseDirectionTowards(TrainPathPointBase start, TrainPathPointBase end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            TrackSegmentBase otherNodeSegment = end.ConnectedSegments.Where(s => s.TrackNodeIndex == TrackNodeIndex).FirstOrDefault();
            return otherNodeSegment != null && (otherNodeSegment.TrackVectorSectionIndex < TrackVectorSectionIndex ||
                (otherNodeSegment.TrackVectorSectionIndex == TrackVectorSectionIndex &&
                start.Location.DistanceSquared(Location) > end.Location.DistanceSquared(Location)));
        }

        /// <summary>
        /// On a single track segment section (same track node index), checks if direction from start to end aligns with track direction or is reverse
        /// </summary>
        public bool IsReverseDirectionTowards(in PointD start, in PointD end)
        {
            return start.DistanceSquared(Location) > end.DistanceSquared(Location);
        }

        /// <summary>
        /// Assuming <paramref name="location"/> is within the segment, returns the projected point/location on the segment, 
        /// aka snapping to the segment if close by
        /// </summary>
        public PointD SnapToSegment(in PointD point)
        {
            if (Curved)
            {
                PointD deltaStart = point - centerPoint;
                float deltaAngle = (float)Math.Atan2(deltaStart.X, deltaStart.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(deltaAngle - centerToStartDirection + Direction);
                int sign = Math.Sign(Angle);
                return centerPoint + new PointD(sign * Math.Sin(deltaAngle) * Radius, sign * Math.Cos(deltaAngle) * Radius);
            }
            else
            {
                double distanceSquared = Length * Length;
                // Calculate the t that minimizes the distance.
                double t = (point - Location).DotProduct(Vector - Location) / distanceSquared;
                return Location + (Vector - Location) * t;
            }
        }
    }
}
