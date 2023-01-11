using System;
using System.Collections.Generic;
using System.Linq;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    public class TrainPathItem : PointPrimitive
    {
        public PathNode PathNode { get; }

        public bool Invalid { get; set; }

        public bool Junction => PathNode.Junction;

        public JunctionNodeBase JunctionNode { get; }

        public IList<TrackSegmentBase> ConnectedSegments { get; }

        public TrainPathItem NextMainItem { get; internal set; }
        public TrainPathItem NextSidingItem { get; internal set; }

        internal TrainPathItem(PathNode node, TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(node);
            this.SetLocation(PointD.FromWorldLocation(node.Location));
            PathNode = node;

            JunctionNode = node.Junction ? trackModel.JunctionAt(Location) : null;
            Invalid |= node.Junction && JunctionNode == null;

            ConnectedSegments = trackModel.SegmentsAt(Location).ToList();
            Invalid |= !ConnectedSegments.Any();
        }
    }
}
