using System;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record EditorPathPoint : TrainPathPointBase, IDrawable<PointPrimitive>
    {
        private protected BasicTextureType textureType;
        private protected float Direction;
        private bool flipHorizontal;
        private bool flipVertical;

        public override PathNodeType NodeType
        {
            get => base.NodeType;
            init
            {
                base.NodeType = value;
                textureType = TextureFromNodeType(NodeType);
            }
        }

        internal EditorPathPoint(PathNode pathNode, TrackWorld trackWorld) : base(pathNode, trackWorld)
        {
            textureType = TextureFromNodeType(NodeType);
        }

        internal EditorPathPoint(in PointD location, TrackWorld trackWorld) : base(location, trackWorld)
        { }

        internal EditorPathPoint(in PointD location, Runtime.Track.JunctionNodeBase junctionNode, TrackSegmentBase trackSegment, TrackWorld trackWorld) :
            base(location, junctionNode, trackSegment, trackWorld)
        {
            textureType = TextureFromNodeType(NodeType);
            Direction = trackSegment?.DirectionAt(Location) + MathHelper.PiOver2 ?? Direction;
        }

        internal EditorPathPoint(TrainPathPointBase trainPathPoint) : base(trainPathPoint)
        {
        }

        internal EditorPathPoint(in PointD location, in PointD vector, PathNodeType nodeType) : base(location, nodeType)
        {
            textureType = TextureFromNodeType(nodeType);
            PointD origin = vector - location;
            Direction = (float)Math.Atan2(origin.X, origin.Y);
        }

        public void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Debug.Assert(textureType != BasicTextureType.BlankPixel);

            Size = Math.Max(1.5f, (float)(8 / renderer.Scale));
            Color color = ValidationResult switch
            {
                PathNodeInvalidReasons.None => Color.White,
                PathNodeInvalidReasons.NoJunctionNode => Color.Yellow,
                _ => Color.Red,
            };

            renderer.DrawTexture(textureType, renderer.WorldToScreenCoordinates(in Location), Direction, renderer.WorldToScreenSize(Size * scaleFactor), color,
                flipHorizontal, flipVertical);
        }

        internal void UpdateDirection(in PointD nextLocation)
        {
            PointD origin = nextLocation - Location;
            Direction = (float)Math.Atan2(origin.X, origin.Y);
        }

        internal void UpdateDirectionTowards(in TrainPathPointBase nextPathPoint, bool alongTrack, bool reverse)
        {
            UpdateDirectionTowards(nextPathPoint, alongTrack, reverse, null, TrackDirection.Ahead, null);
        }

        internal void UpdateDirectionTowards(in TrainPathPointBase nextPathPoint, bool alongTrack, bool reverse, TrackSegmentBase routedSegment,
            TrackDirection routedDirection, ConnectorType? connectorType)
        {
            if (nextPathPoint == null)
                return;

            if (alongTrack && routedSegment != null)
            {
                if (reverse)
                    routedDirection = routedDirection.Reverse();
                Direction = routedSegment.DirectionAt(Location) + (routedDirection == TrackDirection.Reverse ? MathHelper.Pi : 0) + MathHelper.PiOver2;

                if (JunctionNode != null && connectorType.HasValue)
                {
                    bool facing = reverse
                        ? connectorType.Value == ConnectorType.InPin
                        : connectorType.Value == ConnectorType.OutPin;
                    bool usesMainRoute = routedSegment.TrackNodeIndex == JunctionNode.MainRoute;
                    (flipHorizontal, flipVertical) = JunctionIconFlip(JunctionNode.OpeningAngle, usesMainRoute, facing);
                }
                return;
            }

            if (alongTrack && nextPathPoint.ValidationResult == PathNodeInvalidReasons.None &&
                !ConnectedSegments.IsDefaultOrEmpty && !nextPathPoint.ConnectedSegments.IsDefaultOrEmpty)
            {
                TrackSegmentBase trackSegment = ConnectedSegments.Length == 1 ? ConnectedSegments[0] :
                    ConnectedSegments.IntersectBy(nextPathPoint.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex).FirstOrDefault();
                if (trackSegment == null)
                {
                    PointD origin = nextPathPoint.Location - Location;
                    Direction = (float)Math.Atan2(origin.X, origin.Y) + (reverse ? MathHelper.Pi : 0);
                }
                else
                {
                    TrackDirection directionOnSegment = trackSegment.TrackDirectionOnSegment(this, nextPathPoint);
                    if (reverse)
                        directionOnSegment = directionOnSegment.Reverse();
                    Direction = trackSegment.DirectionAt(Location) + (directionOnSegment == TrackDirection.Reverse ? MathHelper.Pi : 0) + MathHelper.PiOver2;
                }
            }
            else
            {
                PointD origin = nextPathPoint.Location - Location;
                Direction = (float)Math.Atan2(origin.X, origin.Y) + (reverse ? MathHelper.Pi : 0);
            }
        }

        internal static (bool FlipHorizontal, bool FlipVertical) JunctionIconFlip(float openingAngle, bool usesMainRoute, bool facing)
        {
            bool flipForOpeningDirection = openingAngle > 0;
            return (flipForOpeningDirection ^ !usesMainRoute ^ !facing, !facing);
        }

        private static BasicTextureType TextureFromNodeType(PathNodeType nodeType)
        {
            return nodeType switch
            {
                PathNodeType _ when nodeType.Includes(PathNodeType.Start) => BasicTextureType.PathStart,
                PathNodeType _ when nodeType.Includes(PathNodeType.End) => BasicTextureType.PathEnd,
                PathNodeType _ when nodeType.Includes(PathNodeType.Reversal) => BasicTextureType.PathReverse,
                PathNodeType _ when nodeType.Includes(PathNodeType.Junction) => BasicTextureType.PathJunction,
                PathNodeType _ when nodeType.Includes(PathNodeType.Wait) => BasicTextureType.PathWait,
                PathNodeType _ when nodeType.Includes(PathNodeType.Via) => BasicTextureType.PathVia,
                PathNodeType _ when nodeType.Includes(PathNodeType.None) => BasicTextureType.RingCrossed,
                PathNodeType _ when nodeType.Includes(PathNodeType.Invalid) => BasicTextureType.RingCrossed,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
