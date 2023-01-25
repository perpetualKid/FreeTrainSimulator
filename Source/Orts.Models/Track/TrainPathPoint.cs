using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    [Flags]
    public enum TrainPathNodeInvalidReasons
    {
        None = 0,
        NoJunctionNode = 0x1,
        NotOnTrack = 0x2,
        NoConnectionPossible=0x4,
        Invalid = 0x8,
    }

    public class TrainPathPoint : PointPrimitive
    {
        public PathNode PathNode { get; }

        public PathNodeType NodeType => PathNode?.NodeType ?? PathNodeType.Temporary;

        public bool Junction => PathNode?.Junction ?? JunctionNode != null;

        public JunctionNodeBase JunctionNode { get; }

        public IList<TrackSegmentBase> ConnectedSegments { get; }

        public TrainPathPoint NextMainItem { get; internal set; }
        public TrainPathPoint NextSidingItem { get; internal set; }

        public TrainPathNodeInvalidReasons ValidationResult { get; set; }
        
        internal TrainPathPoint(PathNode node, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(node);
            this.SetLocation(PointD.FromWorldLocation(node.Location));
            PathNode = node;

            JunctionNode = node.Junction ? trackModel.JunctionAt(Location) : null;
            if (node.Junction && JunctionNode == null)
                ValidationResult |= TrainPathNodeInvalidReasons.NoJunctionNode;

            ConnectedSegments = trackModel.SegmentsAt(Location).ToList();
            if (!ConnectedSegments.Any())
                ValidationResult |= TrainPathNodeInvalidReasons.NotOnTrack;
        }

        internal TrainPathPoint(TrackModel trackModel, PointD location): base(location)
        {
            ArgumentNullException.ThrowIfNull(trackModel);
            JunctionNode = trackModel.JunctionAt(location);

            ConnectedSegments = trackModel.SegmentsAt(Location).ToList();
            if (!ConnectedSegments.Any())
                ValidationResult |= TrainPathNodeInvalidReasons.NotOnTrack;
        }

        public bool CheckPathItem(int index)
        {
            if (ValidationResult == TrainPathNodeInvalidReasons.NoJunctionNode)
            {
                Trace.TraceWarning($"Path point #{index} is marked as junction but not actually located on junction.");
                return true;
            }
            else if (ValidationResult != TrainPathNodeInvalidReasons.None)
            {
                Trace.TraceWarning($"Path item #{index} is not on track.");
                return false;
            }
            return true;
        }
    }
}
