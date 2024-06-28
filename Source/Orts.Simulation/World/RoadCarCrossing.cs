namespace Orts.Simulation.World
{
    public class RoadCarCrossing
    {
        public LevelCrossingItem Item { get; }
        public float Distance { get; }
        public float DistanceAdjust1 { get; }
        public float DistanceAdjust2 { get; }
        public float DistanceAdjust3 { get; }
        public float DistanceAdjust4 { get; }
        public float TrackHeight { get; }

        internal RoadCarCrossing(LevelCrossingItem item, float distance, float trackHeight)
        {
            Item = item;
            Distance = distance;
            DistanceAdjust1 = distance - RoadCarSpawner.TrackHalfWidth - RoadCarSpawner.RampLength;
            DistanceAdjust2 = distance - RoadCarSpawner.TrackHalfWidth;
            DistanceAdjust3 = distance + RoadCarSpawner.TrackHalfWidth;
            DistanceAdjust4 = distance + RoadCarSpawner.TrackHalfWidth + RoadCarSpawner.RampLength;
            TrackHeight = trackHeight;
        }
    }
}
