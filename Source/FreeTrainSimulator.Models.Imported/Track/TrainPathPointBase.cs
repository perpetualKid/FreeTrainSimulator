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
        public PathNodeType NodeType { get; init; }

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

        protected TrainPathPointBase(in PointD location, TrackSegmentBase trackSegment, TrackModel trackModel) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            JunctionNode = trackModel.JunctionAt(Location);
            NodeType = JunctionNode != null ? PathNodeType.Junction : PathNodeType.Intermediate;

            ConnectedSegments = trackModel.OtherSegmentsAt(location, trackSegment).Prepend(trackSegment).ToImmutableArray();
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(JunctionNodeBase junction, TrackModel trackModel) : base(junction?.Location ?? throw new ArgumentNullException(nameof(junction)))
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            JunctionNode = junction;
            NodeType = JunctionNode != null ? PathNodeType.Junction : PathNodeType.Intermediate;

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        public bool ValidatePathItem(int index)
        {
            if (ValidationResult == PathNodeInvalidReasons.NoJunctionNode)
            {
                Debug.WriteLine($"Path point #{index} is marked as junction but not actually located on junction.");
                return true;
            }
            else if (ValidationResult != PathNodeInvalidReasons.None)
            {
                Debug.WriteLine($"Path item #{index} is not on track.");
                return false;
            }
            return true;
        }

        private ImmutableArray<TrackSegmentBase> GetConnectedNodes(TrackModel trackModel)
        {
            return JunctionNode?.ConnectedSegments(trackModel).ToImmutableArray() ?? trackModel.SegmentsAt(Location).ToImmutableArray();
        }
    }
}
