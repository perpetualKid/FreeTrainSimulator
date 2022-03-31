
using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
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
