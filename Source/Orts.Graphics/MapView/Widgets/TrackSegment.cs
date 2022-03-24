
using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrackSegment : VectorWidget
    {
        internal readonly bool Curved;

        internal readonly float Direction;
        internal float Length { get; private protected set; }
        internal float Angle { get; private protected set; }
        internal float Radius { get; private protected set; }

        internal readonly int TrackNodeIndex;
        internal readonly int TrackVectorSectionIndex;


        internal readonly int TrackSectionIndex;

        public TrackSegment(TrackVectorSection trackVectorSection, TrackSection trackSection, int trackNodeIndex, int trackVectorSectionIndex)
        {
            ref readonly WorldLocation location = ref trackVectorSection.Location;
            double cosA = Math.Cos(trackVectorSection.Direction.Y);
            double sinA = Math.Sin(trackVectorSection.Direction.Y);

            TrackSectionIndex = trackSection.SectionIndex;

            if (trackSection.Curved)
            {
                Angle = trackSection.Angle;
                Radius = trackSection.Radius;
                Length = trackSection.Length;

                int sign = -Math.Sign(trackSection.Angle);

                double angleRadians = MathHelper.ToRadians(trackSection.Angle);
                double cosArotated = Math.Cos(trackVectorSection.Direction.Y + angleRadians);
                double sinArotated = Math.Sin(trackVectorSection.Direction.Y + angleRadians);
                double deltaX = sign * trackSection.Radius * (cosA - cosArotated);
                double deltaZ = sign * trackSection.Radius * (sinA - sinArotated);
                vectorEnd = new PointD(location.TileX * WorldLocation.TileSize + location.Location.X - deltaX, location.TileZ * WorldLocation.TileSize + location.Location.Z + deltaZ);
            }
            else
            {
                Length = trackSection.Length;

                // note, angle is 90 degrees off, and different sign. 
                // So Delta X = cos(90-A)=sin(A); Delta Y,Z = sin(90-A) = cos(A)    
                vectorEnd = new PointD(location.TileX * WorldLocation.TileSize + location.Location.X + sinA * Length, location.TileZ * WorldLocation.TileSize + location.Location.Z + cosA * Length);
            }

            base.location = PointD.FromWorldLocation(location);
            tile = new Tile(location.TileX, location.TileZ);
            otherTile = new Tile(Tile.TileFromAbs(vectorEnd.X), Tile.TileFromAbs(vectorEnd.Y));
            Size = trackSection.Width;
            Curved = trackSection.Curved;
            Direction = trackVectorSection.Direction.Y - MathHelper.PiOver2;
            TrackNodeIndex = trackNodeIndex;
            TrackVectorSectionIndex = trackVectorSectionIndex;
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
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<TrackSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, 0, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }

    internal class RoadSegment : TrackSegment
    {
        public RoadSegment(TrackVectorSection trackVectorSection, TrackSection trackSection, int trackNodeIndex, int trackVectorSectionIndex) :
            base(trackVectorSection, trackSection, trackNodeIndex, trackVectorSectionIndex)
        {
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<RoadSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, 0, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }

    internal class PathSegment : TrackSegment
    {
        private readonly float startOffsetDeg;

        public PathSegment(TrackSegment source, float remainingLength, float startOffset, bool reverse) : base(source)
        {
            if (startOffset == 0 && remainingLength >= Length)//full path segment
                return;

            if (Curved)
            {
                int sign = Math.Sign(Angle);
                float remainingDeg = MathHelper.ToDegrees(remainingLength / Radius) * sign;

                if (reverse)
                {
                    if (startOffset != 0)
                        Angle = MathHelper.ToDegrees(startOffset) * sign;
                    if (Math.Abs(remainingDeg) < Math.Abs(Angle))
                    {
                        startOffsetDeg = Angle - remainingDeg;
                        Angle = remainingDeg;
                    }
                }
                else
                {
                    startOffsetDeg = sign * MathHelper.ToDegrees(startOffset);
                    Angle -= startOffsetDeg;
                    if (Math.Abs(remainingDeg) < Math.Abs(Angle))
                        Angle = remainingDeg;
                }
                Length = Radius * MathHelper.ToRadians(Angle) * sign;
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PathSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, startOffsetDeg, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

    }
}
