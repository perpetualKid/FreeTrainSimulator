using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    internal record TrainPathPoint : TrainPathPointBase
    {
        public TrainPathPoint(TrainPathPointBase node) : base(node)
        {
        }

        public TrainPathPoint(PathNode node, TrackWorld trackWorld) : base(node, trackWorld)
        {
        }

        public TrainPathPoint(in PointD location, TrackWorld trackWorld) : base(location, trackWorld)
        {
        }

        public TrainPathPoint(JunctionNodeBase junction, TrackWorld trackWorld) : base(junction?.Location ?? throw new ArgumentNullException(nameof(junction)), junction, null, trackWorld)
        {
        }
    }
}
