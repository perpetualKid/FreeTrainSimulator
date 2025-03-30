using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public abstract record TrainPathPointBase : PointPrimitive
    {
        public virtual PathNodeType NodeType { get; init; }

        public JunctionNodeBase JunctionNode { get; }

        public ImmutableArray<TrackSegmentBase> ConnectedSegments { get; }

        public int NextMainNode { get; init; } = -1;
        public int NextSidingNode { get; init; } = -1;

        public PathNodeInvalidReasons ValidationResult { get; set; }

        protected TrainPathPointBase(PathNode node, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(trackModel);

            SetLocation(PointD.FromWorldLocation(node.Location));
            NodeType = node.NodeType;
            NextMainNode = node.NextMainNode;
            NextSidingNode = node.NextSidingNode;

            JunctionNode = (node.NodeType & PathNodeType.Junction) == PathNodeType.Junction ? trackModel.JunctionAt(Location) : null;
            if ((node.NodeType & PathNodeType.Junction) == PathNodeType.Junction && JunctionNode == null)
                ValidationResult |= PathNodeInvalidReasons.NoJunctionNode;

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(in PointD location, PathNodeType nodeType) : base(location)
        {
            NodeType = nodeType;
        }

        protected TrainPathPointBase(in PointD location, TrackModel trackModel) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            JunctionNode = trackModel.JunctionAt(Location);
            NodeType = JunctionNode != null ? PathNodeType.Junction : PathNodeType.Intermediate;

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(in PointD location, JunctionNodeBase junctionNode, TrackSegmentBase trackSegment, TrackModel trackModel) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            JunctionNode = junctionNode;
            if (JunctionNode != null)
            {
                NodeType = PathNodeType.Junction;
                ConnectedSegments = GetConnectedNodes(trackModel);
            }
            else if (trackSegment != null)
            {
                NodeType = PathNodeType.Intermediate;
                ConnectedSegments = ImmutableArray.Create(trackSegment);
            }

            if (ConnectedSegments.IsDefaultOrEmpty)
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        private ImmutableArray<TrackSegmentBase> GetConnectedNodes(TrackModel trackModel)
        {
            return JunctionNode?.ConnectedSegments(trackModel).ToImmutableArray() ?? trackModel.SegmentsAt(Location).ToImmutableArray();
        }
    }
}
