
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrackSegment : VectorWidget, INameValueInformationProvider
    {
        [ThreadStatic]
        private protected static NameValueCollection debugInformation = new NameValueCollection() { ["Track Node Information"] = null, ["Node Type"] = "Track Vector Section" };
        private protected static Dictionary<string, FormatOption> formattingOptions = new Dictionary<string, FormatOption>() { ["Track Node Information"] = FormatOption.Bold };

        internal readonly bool Curved;

        internal readonly float Direction;
        internal float Length { get; private protected set; }
        internal float Angle { get; private protected set; }
        internal float Radius { get; private protected set; }

        public NameValueCollection DebugInfo
        {
            get
            {
                debugInformation["Track Node Index"] = TrackNodeIndex.ToString(CultureInfo.InvariantCulture);
                debugInformation["Track Section Index"] = TrackVectorSectionIndex.ToString(CultureInfo.InvariantCulture);
                debugInformation["Curved"] = Curved.ToString(CultureInfo.InvariantCulture);
                debugInformation["Length"] = $"{Length:F1}m";
                debugInformation["Direction"] = $"{Direction:F3}º";
                debugInformation["Radius"] = Curved ? $"{Radius:F1}m" : null;
                debugInformation["Angle"] = Curved ? $"{Angle:F3}º" : null;
                return debugInformation;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions => formattingOptions;

        internal readonly int TrackNodeIndex;
        internal readonly int TrackVectorSectionIndex;

        public TrackSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex)
        {
            ref readonly WorldLocation location = ref trackVectorSection.Location;
            double cosA = Math.Cos(trackVectorSection.Direction.Y);
            double sinA = Math.Sin(trackVectorSection.Direction.Y);

            TrackSection trackSection = trackSections.TryGet(trackVectorSection.SectionIndex);

            if (null == trackSection)
                throw new System.IO.InvalidDataException($"TrackVectorSection {trackVectorSection.SectionIndex} not found in TSection.dat");
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

        private protected TrackSegment()
        { }

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
        public RoadSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex) :
            base(trackVectorSection, trackSections, trackNodeIndex, trackVectorSectionIndex)
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
        private protected PathSegment()
        { }

        public PathSegment(TrackSegment source, float remainingLength, float startOffset, bool reverse) : base(source)
        {
            if (startOffset == 0 && remainingLength >= Length)//full path segment
                return;
            //remainingLength is in m down the track, startOffset is either in m for straight, or in Rad for Curved
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
                Angle += 0.05f * sign;  // there seems to be a small rounding error somewhere leading to tiny gap in some cases
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PathSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, startOffsetDeg, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }

    internal class BrokenPathSegment : PathSegment
    {
        public BrokenPathSegment(in WorldLocation location) : base()
        {
            base.location = PointD.FromWorldLocation(location);
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PathSegment>(colorVariation);
            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => 40,
                double i when i < 0.75 => 25,
                double i when i < 1 => 18,
                double i when i < 3 => 12,
                double i when i < 5 => 8,
                double i when i < 8 => 6,
                _ => 4,
            };
            BasicShapes.DrawTexture(BasicTextureType.RingCrossed, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }
}
