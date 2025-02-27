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

        //internal static void LinkPathPoints(List<TrainPathPointBase> pathPoints, List<(int NextMainNode, int NextSidingNode)> pathPointConnections)
        //{
        //    if (pathPoints.Count != pathPointConnections.Count)
        //        throw new ArgumentOutOfRangeException(nameof(pathPointConnections), pathPointConnections.Count, "Linking path points collection needs to have same size as path points");

        //    //linking path item nodes to their next path item node
        //    //on the end node, set to the previous (inbound) node instead, required for TrainPathItem direction/alignment
        //    //nb: inbound to the end node may not need to be the node just before in the list, so as we iterate the list, 
        //    //we keep a reference to the one which has the end node as successor
        //    //it's assumed that passing paths will reconnct to main node, and not ending on it's own

        //    int index;
        //    int beforeEndNode = -1;

        //    for (int i = 0; i < pathPoints.Count; i++)
        //    {
        //        if ((index = pathPointConnections[i].NextMainNode) != -1)
        //        {
        //            pathPoints[i] = pathPoints[i] with
        //            {
        //                NextMainItem = pathPoints[index]
        //            };
        //            if ((pathPoints[i].NextMainItem.NodeType & PathNodeType.End) == PathNodeType.End)
        //                beforeEndNode = i;
        //        }
        //        else if ((pathPoints[i].NodeType & PathNodeType.End) == PathNodeType.End)
        //            pathPoints[i] = pathPoints[i] with
        //            {
        //                NextMainItem = pathPoints[beforeEndNode]
        //            };

        //        if ((index = pathPointConnections[i].NextSidingNode) != -1)
        //            pathPoints[i] = pathPoints[i] with { NextSidingItem = pathPoints[index] };
        //    }
        //}

        private ImmutableArray<TrackSegmentBase> GetConnectedNodes(TrackModel trackModel)
        {
            return JunctionNode?.ConnectedSegments(trackModel).ToImmutableArray() ?? trackModel.SegmentsAt(Location).ToImmutableArray();
        }
    }
}
