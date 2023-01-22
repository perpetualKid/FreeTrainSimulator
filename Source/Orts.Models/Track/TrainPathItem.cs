using System;
using System.Collections.Generic;
using System.Linq;

using Orts.Common.Position;
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

    public class TrainPathItem : PointPrimitive
    {
        public PathNode PathNode { get; }

        public bool Junction => PathNode.Junction;

        public JunctionNodeBase JunctionNode { get; }

        public IList<TrackSegmentBase> ConnectedSegments { get; }

        public TrainPathItem NextMainItem { get; internal set; }
        public TrainPathItem NextSidingItem { get; internal set; }

        public TrainPathNodeInvalidReasons ValidationResult { get; set; }
        
        internal TrainPathItem(PathNode node, TrackModel trackModel)
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
    }
}
