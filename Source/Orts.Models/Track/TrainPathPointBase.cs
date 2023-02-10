using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    public abstract class TrainPathPointBase: PointPrimitive
    {
        private readonly int nextMainNode;
        private readonly int nextSidingNode;

        public PathNodeType NodeType { get; protected set; }

        public JunctionNodeBase JunctionNode { get; }

        public IReadOnlyList<TrackSegmentBase> ConnectedSegments { get; }

        public TrainPathPointBase NextMainItem { get; internal set; }
        public TrainPathPointBase NextSidingItem { get; internal set; }

        public PathNodeInvalidReasons ValidationResult { get; set; }

        protected TrainPathPointBase(PathNode node, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(trackModel);

            SetLocation(PointD.FromWorldLocation(node.Location));
            NodeType = node.NodeType;
            nextMainNode = node.NextMainNode;
            nextSidingNode = node.NextSidingNode;

            JunctionNode = node.Junction ? trackModel.JunctionAt(Location) : null;
            if (node.Junction && JunctionNode == null)
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

            nextMainNode = -1;
            nextSidingNode = -1;

            JunctionNode = trackModel.JunctionAt(Location);
            NodeType = JunctionNode != null ? PathNodeType.Junction : PathNodeType.Intermediate;

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(in PointD location, TrackSegmentBase trackSegment, TrackModel trackModel) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            nextMainNode = -1;
            nextSidingNode = -1;

            JunctionNode = trackModel.JunctionAt(Location);
            NodeType = JunctionNode != null ? PathNodeType.Junction : PathNodeType.Intermediate;

            ConnectedSegments = trackModel.OtherSegmentsAt(location, trackSegment).Prepend(trackSegment).ToList();
            if (!ConnectedSegments.Any())
                ValidationResult |= PathNodeInvalidReasons.NotOnTrack;
        }

        protected TrainPathPointBase(JunctionNodeBase junction, TrackModel trackModel): base(junction?.Location ?? throw new ArgumentNullException(nameof(junction)))
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            nextMainNode = -1;
            nextSidingNode = -1;

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
                Trace.TraceWarning($"Path point #{index} is marked as junction but not actually located on junction.");
                return true;
            }
            else if (ValidationResult != PathNodeInvalidReasons.None)
            {
                Trace.TraceWarning($"Path item #{index} is not on track.");
                return false;
            }
            return true;
        }

        internal static void LinkPathPoints(List<TrainPathPointBase> pathPoints)
        {
            TrainPathPointBase beforeEndNode = null;

            //linking path item nodes to their next path item node
            //on the end node, set to the previous (inbound) node instead, required for TrainPathItem direction/alignment
            //nb: inbound to the end node may not need to be the node just before in the list, so as we iterate the list, 
            //we keep a reference to the one which has the end node as successor
            //it's assumed that passing paths will reconnct to main node, and not ending on it's own
            foreach (TrainPathPointBase node in pathPoints)
            {
                if (node.nextMainNode != -1)
                {
                    node.NextMainItem = pathPoints[node.nextMainNode];
                    if (node.NextMainItem.NodeType == PathNodeType.End)
                        beforeEndNode = node;
                }
                else if (node.NodeType == PathNodeType.End)
                    node.NextMainItem = beforeEndNode;

                if (node.nextSidingNode != -1)
                    node.NextSidingItem = pathPoints[node.nextSidingNode];
            }
        }

        private IReadOnlyList<TrackSegmentBase> GetConnectedNodes(TrackModel trackModel)
        {
            return JunctionNode?.ConnectedSegments(trackModel).ToList() ?? (IReadOnlyList<TrackSegmentBase>)trackModel.SegmentsAt(Location).ToList();
        }
    }

    internal class TrainPathPoint : TrainPathPointBase
    {
        public TrainPathPoint(PathNode node, TrackModel trackModel) : base(node, trackModel)
        {
        }

        public TrainPathPoint(in PointD location, TrackModel trackModel) : base(location, trackModel)
        {
        }

        public TrainPathPoint(JunctionNodeBase junction, TrackModel trackModel) : base(junction, trackModel)
        {
        }
    }
}
