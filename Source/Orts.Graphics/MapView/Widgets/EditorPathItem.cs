using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Graphics.MapView.Shapes;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EditorPathItem : PointPrimitive, IDrawable<PointPrimitive>
    {
        private protected BasicTextureType textureType;
        private protected float Direction;
        public TrainPathNodeInvalidReasons ValidationResult{ get; set; }

        internal EditorPathItem(in PointD location, TrackSegmentBase trackSegment, PathNodeType nodeType, bool reverseDirection): base(location)
        {
            textureType = TextureFromNodeType(nodeType);
            Direction = (trackSegment?.DirectionAt(Location) ?? 0) + (reverseDirection ? MathHelper.Pi : 0) + MathHelper.PiOver2;
        }

        internal EditorPathItem(in PointD location, in PointD vector, PathNodeType nodeType) : base(location)
        {
            textureType = TextureFromNodeType(nodeType);
            PointD origin = vector - location;
            Direction = (float)Math.Atan2(origin.X, origin.Y);
        }

        public void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = Math.Max(1.5f, (float)(8 / contentArea.Scale));
            Color color = ValidationResult switch
            {
                TrainPathNodeInvalidReasons.None => Color.White,
                TrainPathNodeInvalidReasons.NoJunctionNode => Color.Yellow,
                _ => Color.Red,
            };

            contentArea.BasicShapes.DrawTexture(textureType, contentArea.WorldToScreenCoordinates(in Location), Direction, contentArea.WorldToScreenSize(Size * scaleFactor), color, contentArea.SpriteBatch);
        }


        internal void UpdateLocation(in PointD location)
        {
            SetLocation(location);
        }

        internal void UpdateLocation(TrackSegmentBase trackSegment, in PointD location)
        {            
            SetLocation(trackSegment?.SnapToSegment(location) ?? location);
            if (null == trackSegment)
            {
                ValidationResult = TrainPathNodeInvalidReasons.NotOnTrack;
                textureType = TextureFromNodeType(PathNodeType.Temporary);
            }
            else
            {
                ValidationResult = TrainPathNodeInvalidReasons.None;
                textureType = TextureFromNodeType(PathNodeType.Intermediate);
            }
        }


        internal void UpdateNodeType(PathNodeType nodeType)
        {
            textureType = TextureFromNodeType(nodeType);
        }

        private static BasicTextureType TextureFromNodeType(PathNodeType nodeType)
        {
            return nodeType switch
            {
                PathNodeType.Start => BasicTextureType.PathStart,
                PathNodeType.End => BasicTextureType.PathEnd,
                PathNodeType.Normal => BasicTextureType.PathNormal,
                PathNodeType.Intermediate => BasicTextureType.PathNormal,
                PathNodeType.Wait => BasicTextureType.PathWait,
                PathNodeType.SidingStart => BasicTextureType.PathNormal,
                PathNodeType.SidingEnd => BasicTextureType.PathNormal,
                PathNodeType.Reversal => BasicTextureType.PathReverse,
                PathNodeType.Temporary => BasicTextureType.RingCrossed,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
