using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record EditorPathPoint : TrainPathPointBase, IDrawable<PointPrimitive>
    {
        private protected BasicTextureType textureType;
        private protected float Direction;

        internal EditorPathPoint(PathNode pathNode, TrackModel trackModel) : base(pathNode, trackModel)
        {
            textureType = TextureFromNodeType(NodeType);
        }

        internal EditorPathPoint(in PointD location, TrackModel trackModel) : base(location, trackModel)
        { }

        internal EditorPathPoint(in PointD location, JunctionNodeBase junctionNode, TrackSegmentBase trackSegment, TrackModel trackModel) : 
            base(location, junctionNode, trackSegment, trackModel)
        { 
            Direction = trackSegment?.DirectionAt(Location) + MathHelper.PiOver2 ?? Direction;
        }

        internal EditorPathPoint(in PointD location, in PointD vector, PathNodeType nodeType) : base(location, nodeType)
        {
            textureType = TextureFromNodeType(nodeType);
            PointD origin = vector - location;
            Direction = (float)Math.Atan2(origin.X, origin.Y);
        }

        public void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            if (textureType == BasicTextureType.BlankPixel)
                ResetTexture();
            Size = Math.Max(1.5f, (float)(8 / contentArea.Scale));
            Color color = ValidationResult switch
            {
                PathNodeInvalidReasons.None => Color.White,
                PathNodeInvalidReasons.NoJunctionNode => Color.Yellow,
                _ => Color.Red,
            };

            contentArea.BasicShapes.DrawTexture(textureType, contentArea.WorldToScreenCoordinates(in Location), Direction, contentArea.WorldToScreenSize(Size * scaleFactor), color, contentArea.SpriteBatch);
        }

        internal void UpdateLocation(in PointD location)
        {
            SetLocation(location);
        }

        internal void UpdateLocation(in PointD location, TrackSegmentBase trackSegment)
        {
            SetLocation(location);

            if (null != trackSegment)
            {
                ValidationResult = PathNodeInvalidReasons.None;
                textureType = TextureFromNodeType(PathNodeType.Intermediate);
                Direction = trackSegment.DirectionAt(Location) + MathHelper.PiOver2;
            }
            else
            {
                ValidationResult = PathNodeInvalidReasons.NotOnTrack;
                textureType = TextureFromNodeType(PathNodeType.None);
            }
        }

        internal void UpdateDirection(in PointD nextLocation)
        {
            PointD origin = nextLocation - Location;
            Direction = (float)Math.Atan2(origin.X, origin.Y);
        }

        internal void UpdateDirectionTowards(in TrainPathPointBase nextPathPoint, bool alongTrack, bool reverse)
        {
            if (alongTrack && nextPathPoint.ValidationResult == PathNodeInvalidReasons.None)
            {
                TrackSegmentBase trackSegment = ConnectedSegments[0];
                TrackDirection directionOnSegment = trackSegment.TrackDirectionOnSegment(this, nextPathPoint);
                if (reverse)
                    directionOnSegment = directionOnSegment.Reverse();
                Direction = (trackSegment?.DirectionAt(Location) ?? 0) + (directionOnSegment == TrackDirection.Reverse ? MathHelper.Pi : 0) + MathHelper.PiOver2;
            }
            else
            {
                PointD origin = nextPathPoint.Location - Location;
                Direction = (float)Math.Atan2(origin.X, origin.Y) + (reverse ? MathHelper.Pi : 0);
            }
        }

        protected void ResetTexture() // if NodeType is set through copy constructor, the texture Type is not updated, hence we need to check
        {
            textureType = TextureFromNodeType(NodeType);
        }

        private static BasicTextureType TextureFromNodeType(PathNodeType nodeType)
        {
            return nodeType switch
            {
                PathNodeType _ when (nodeType & PathNodeType.Start) == PathNodeType.Start => BasicTextureType.PathStart,
                PathNodeType _ when (nodeType & PathNodeType.End) == PathNodeType.End => BasicTextureType.PathEnd,
                PathNodeType _ when (nodeType & PathNodeType.Junction) == PathNodeType.Junction => BasicTextureType.PathNormal,
                PathNodeType _ when (nodeType & PathNodeType.Intermediate) == PathNodeType.Intermediate => BasicTextureType.PathNormal,
                PathNodeType _ when (nodeType & PathNodeType.Wait) == PathNodeType.Wait => BasicTextureType.PathWait,
                PathNodeType _ when (nodeType & PathNodeType.Reversal) == PathNodeType.Reversal => BasicTextureType.PathReverse,
                PathNodeType _ when (nodeType & PathNodeType.None) == PathNodeType.None => BasicTextureType.RingCrossed,
                PathNodeType _ when (nodeType & PathNodeType.Invalid) == PathNodeType.Invalid => BasicTextureType.RingCrossed,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
