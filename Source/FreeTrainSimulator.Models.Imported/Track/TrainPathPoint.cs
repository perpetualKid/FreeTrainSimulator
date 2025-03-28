using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Imported.Track
{
    internal record TrainPathPoint : TrainPathPointBase
    {
        public TrainPathPoint(TrainPathPointBase node) : base(node)
        {
        }

        public TrainPathPoint(PathNode node, TrackModel trackModel) : base(node, trackModel)
        {
        }

        public TrainPathPoint(in PointD location, TrackModel trackModel) : base(location, trackModel)
        {
        }

        public TrainPathPoint(JunctionNodeBase junction, TrackModel trackModel) : base(junction?.Location ?? throw new ArgumentNullException(nameof(junction)), junction, null, trackModel)
        {
        }
    }
}
