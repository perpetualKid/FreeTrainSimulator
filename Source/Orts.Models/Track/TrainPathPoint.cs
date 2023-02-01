using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.XPath;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    public class TrainPathPoint : PointPrimitive
    {
        [Flags]
        public enum InvalidReasons
        {
            None = 0,
            NoJunctionNode = 0x1,
            NotOnTrack = 0x2,
            NoConnectionPossible = 0x4,
            Invalid = 0x8,
        }

        private readonly int nextMainNode;
        private readonly int nextSidingNode;

        public PathNodeType NodeType { get; private set; }

        public JunctionNodeBase JunctionNode { get; }

        public IReadOnlyList<TrackSegmentBase> ConnectedSegments { get; }

        public TrainPathPoint NextMainItem { get; internal set; }
        public TrainPathPoint NextSidingItem { get; internal set; }

        public InvalidReasons ValidationResult { get; set; }

        internal TrainPathPoint(PathNode node, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(node);
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

        public TrainPathPoint(in PointD location, TrackModel trackModel)
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

        internal static void LinkPathPoints(List<TrainPathPoint> pathPoints)
        {
            TrainPathPoint beforeEndNode = null;

            //linking path item nodes to their next path item node
            //on the end node, set to the previous (inbound) node instead, required for TrainPathItem direction/alignment
            //nb: inbound to the end node may not need to be the node just before in the list, so as we iterate the list, 
            //we keep a reference to the one which has the end node as successor
            //it's assumed that passing paths will reconnct to main node, and not ending on it's own
            foreach (TrainPathPoint node in pathPoints)
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

        internal TrainPathPoint(TrackModel trackModel, PointD location) : base(location)
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

        private IReadOnlyList<TrackSegmentBase> GetConnectedNodes(TrackModel trackModel)
        {
            return JunctionNode != null ? JunctionNode.ConnectedSegments(trackModel).ToList() : (IReadOnlyList<TrackSegmentBase>)trackModel.SegmentsAt(Location).ToList();
        }
    }
}
