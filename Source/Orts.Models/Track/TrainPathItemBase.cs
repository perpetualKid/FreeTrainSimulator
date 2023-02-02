using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    public abstract class TrainPathItemBase: PointPrimitive
    {
        private readonly int nextMainNode;
        private readonly int nextSidingNode;

        public PathNodeType NodeType { get; private set; }

        public JunctionNodeBase JunctionNode { get; }

        public IReadOnlyList<TrackSegmentBase> ConnectedSegments { get; }

        public TrainPathItemBase NextMainItem { get; internal set; }
        public TrainPathItemBase NextSidingItem { get; internal set; }

        public InvalidReasons ValidationResult { get; set; }

        protected TrainPathItemBase(PathNode node, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(trackModel);

            SetLocation(PointD.FromWorldLocation(node.Location));
            NodeType = node.NodeType;
            nextMainNode = node.NextMainNode;
            nextSidingNode = node.NextSidingNode;

            JunctionNode = node.Junction ? trackModel.JunctionAt(Location) : null;
            if (node.Junction && JunctionNode == null)
                ValidationResult |= InvalidReasons.NoJunctionNode;

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= InvalidReasons.NotOnTrack;
        }

        protected TrainPathItemBase(in PointD location, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            SetLocation(location);
            NodeType = PathNodeType.Normal;
            nextMainNode = -1;
            nextSidingNode = -1;

            JunctionNode = trackModel.JunctionAt(Location);

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= InvalidReasons.NotOnTrack;
        }

        protected TrainPathItemBase(TrackModel trackModel, PointD location) : base(location)
        {
            ArgumentNullException.ThrowIfNull(trackModel);
            JunctionNode = trackModel.JunctionAt(location);

            ConnectedSegments = GetConnectedNodes(trackModel);
            if (!ConnectedSegments.Any())
                ValidationResult |= InvalidReasons.NotOnTrack;
        }

        public bool CheckPathItem(int index)
        {
            if (ValidationResult == InvalidReasons.NoJunctionNode)
            {
                Trace.TraceWarning($"Path point #{index} is marked as junction but not actually located on junction.");
                return true;
            }
            else if (ValidationResult != InvalidReasons.None)
            {
                Trace.TraceWarning($"Path item #{index} is not on track.");
                return false;
            }
            return true;
        }

        protected TrainPathItemBase(in PointD location, PathNodeType nodeType) : base(location)
        {
            NodeType = nodeType;
        }

        internal void UpdateLocation(in PointD location)
        {
            SetLocation(location);
        }

        protected void UpdateLocation(TrackSegmentBase trackSegment, in PointD location)
        {
            SetLocation(trackSegment?.SnapToSegment(location) ?? location);
            ValidationResult = null == trackSegment ? InvalidReasons.NotOnTrack : InvalidReasons.None;
        }

        internal static void LinkPathPoints(List<TrainPathItemBase> pathPoints)
        {
            TrainPathItemBase beforeEndNode = null;

            //linking path item nodes to their next path item node
            //on the end node, set to the previous (inbound) node instead, required for TrainPathItem direction/alignment
            //nb: inbound to the end node may not need to be the node just before in the list, so as we iterate the list, 
            //we keep a reference to the one which has the end node as successor
            //it's assumed that passing paths will reconnct to main node, and not ending on it's own
            foreach (TrainPathItemBase node in pathPoints)
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
            return JunctionNode != null ? JunctionNode.ConnectedSegments(trackModel).ToList() : (IReadOnlyList<TrackSegmentBase>)trackModel.SegmentsAt(Location).ToList();
        }
    }

    public class TrainPathItemPoint : TrainPathItemBase
    {
        public TrainPathItemPoint(PathNode node, TrackModel trackModel) : base(node, trackModel)
        {
        }

        public TrainPathItemPoint(in PointD location, TrackModel trackModel) : base(location, trackModel)
        {
        }

        public TrainPathItemPoint(TrackModel trackModel, PointD location) : base(trackModel, location)
        {
        }

        public TrainPathItemPoint(in PointD location, PathNodeType nodeType) : base(location, nodeType)
        {
        }
    }
}
