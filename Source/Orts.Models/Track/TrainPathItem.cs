using System;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    public class TrainPathItem: PointPrimitive
    {
        public PathNode PathNode { get; }

        public bool NotOnTrack { get; set; }
        public bool Junction { get; private set; }

        public TrainPathItem(PathNode node)
        { 
            ArgumentNullException.ThrowIfNull(node);
            this.SetLocation(PointD.FromWorldLocation(node.Location));
            PathNode = node;
        }
    }
}
