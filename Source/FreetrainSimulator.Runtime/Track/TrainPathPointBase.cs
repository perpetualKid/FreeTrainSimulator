using System;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    public abstract record TrainPathPointBase : PointPrimitive
    {
        public virtual PathNodeType NodeType { get; init; }

        public JunctionNodeBase JunctionNode { get; }

        public ImmutableArray<TrackSegmentBase> ConnectedSegments { get; }

        public int NextMainNode { get; init; } = -1;
        public int NextSidingNode { get; init; } = -1;

        public int NodeIndex { get; init; }

        public PathNodeWaitInfo WaitInfo { get; init; }

        public TrackDistanceDiagnostic NearestTrackDistance { get; }

        public PathNodeInvalidReasons ValidationResult { get; set; }

        protected TrainPathPointBase(PathNode node, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(trackWorld);

            SetLocation(PointD.FromWorldLocation(node.Location));
            NodeType = node.NodeType;
            NextMainNode = node.NextMainNode;
            NextSidingNode = node.NextSidingNode;
            NodeIndex = node.NodeIndex;
            WaitInfo = node.WaitInfo;
            NearestTrackDistance = trackWorld.NearestTrackDistance(Location);

            JunctionNode = node.NodeType.Includes(PathNodeType.Junction) ? trackWorld.JunctionNodeBaseAt(Location) : null;
            if (node.NodeType.Includes(PathNodeType.Junction) && JunctionNode == null)
                ValidationResult |= PathNodeInvalidReasons.NoJunctionNode;

            ConnectedSegments = GetConnectedNodes(trackWorld);
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(in PointD location, PathNodeType nodeType) : base(location)
        {
            NodeType = nodeType;
        }

        protected TrainPathPointBase(in PointD location, TrackWorld trackWorld) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackWorld);

            JunctionNode = trackWorld.JunctionNodeBaseAt(Location);
            NodeType = JunctionNode != null ? PathNodeType.Junction : PathNodeType.Via;
            NearestTrackDistance = trackWorld.NearestTrackDistance(Location);

            ConnectedSegments = GetConnectedNodes(trackWorld);
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(in PointD location, JunctionNodeBase junctionNode, TrackSegmentBase trackSegment, TrackWorld trackWorld) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackWorld);

            NearestTrackDistance = trackWorld.NearestTrackDistance(Location);
            JunctionNode = junctionNode;
            if (JunctionNode != null)
            {
                NodeType = PathNodeType.Junction;
                ConnectedSegments = GetConnectedNodes(trackWorld);
            }
            else if (trackSegment != null)
            {
                NodeType = PathNodeType.Via;
                ConnectedSegments = ImmutableArray.Create(trackSegment);
            }

            if (ConnectedSegments.IsDefaultOrEmpty)
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        private ImmutableArray<TrackSegmentBase> GetConnectedNodes(TrackWorld trackWorld)
        {
            ImmutableArray<TrackSegmentBase> result;
            if (JunctionNode == null || (result = JunctionNode.ConnectedSegments().ToImmutableArray()).IsDefaultOrEmpty)
                result = trackWorld.SegmentBasesAt(Location).ToImmutableArray();
            return result;
        }
    }
}
