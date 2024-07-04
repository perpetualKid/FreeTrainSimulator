using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Simulation.World
{
    public class RoadCarSpawner
    {
        private double lastSpawnedTime;
        private double nextSpawnTime;

        public const float StopDistance = 10;

        internal const float RampLength = 2;
        internal const float TrackHalfWidth = 1;
        internal const float TrackMergeDistance = 7; // Must be >= 2 * (RampLength + TrackHalfWidth).
        internal const float TrackRailHeight = 0.275f;
        internal const float TrainRailHeightMaximum = 1;

        public CarSpawnerObject CarSpawnerObj { get; }

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        public List<RoadCar> Cars { get; } = new List<RoadCar>();
        // Level crossing which interact with this spawner. Distances are used for speed curves and the list must be sorted by distance from spawner.
        public List<RoadCarCrossing> Crossings { get; } = new List<RoadCarCrossing>();

        public Traveller Traveller { get; }
        public float Length { get; }

        public RoadCarSpawner(in WorldPosition position, CarSpawnerObject carSpawnerObj)
        {
            Debug.Assert(TrackMergeDistance >= 2 * (RampLength + TrackHalfWidth), "TrackMergeDistance is less than 2 * (RampLength + TrackHalfWidth); vertical inconsistencies will occur at close, but not merged, tracks.");
            CarSpawnerObj = carSpawnerObj;

            if (RuntimeData.Instance.RoadTrackDB == null || Simulator.Instance.CarSpawnerLists == null)
                throw new InvalidOperationException("RoadCarSpawner requires a RDB and CARSPAWN.DAT");

            int start = CarSpawnerObj.TrackItemIds.RoadDbItems.Count > 0 ? CarSpawnerObj.TrackItemIds.RoadDbItems[0] : -1;
            int end = CarSpawnerObj.TrackItemIds.RoadDbItems.Count > 1 ? CarSpawnerObj.TrackItemIds.RoadDbItems[1] : -1;
            List<TrackItem> trItems = RuntimeData.Instance.RoadTrackDB.TrackItems;
            ref readonly WorldLocation startLocation = ref trItems[start].Location;
            ref readonly WorldLocation endLocation = ref trItems[end].Location;

            Traveller = new Traveller(startLocation, true);
            Length = Traveller.DistanceTo(endLocation);
            if (Length < 0)
            {
                Traveller.ReverseDirection();
                Length = Traveller.DistanceTo(endLocation);
                if (Length < 0)
                    Trace.TraceWarning("{0} car spawner {1} doesn't have connected road route between {2} and {3}", position, carSpawnerObj.UiD, startLocation, endLocation);
            }

            SortedList<float, LevelCrossingItem> sortedLevelCrossings = new SortedList<float, LevelCrossingItem>();
            for (Traveller crossingTraveller = new Traveller(Traveller); crossingTraveller.NextSection();)
                if (crossingTraveller.TrackNode is TrackVectorNode trackVectorNode && trackVectorNode.TrackItemIndices != null)
                {
                    foreach (int trItemRef in trackVectorNode.TrackItemIndices)
                    {
                        if (Simulator.Instance.LevelCrossings.RoadCrossingItems.TryGetValue(trItemRef, out LevelCrossingItem value))
                            sortedLevelCrossings[value.DistanceTo(Traveller)] = value;
                    }

                }

            Crossings = sortedLevelCrossings.Select(slc => new RoadCarCrossing(slc.Value, slc.Key, float.NaN)).ToList();
        }

        public void Update(in ElapsedTime elapsedTime)
        {
            foreach (RoadCar car in Cars)
                car.Update(elapsedTime);

            lastSpawnedTime += elapsedTime.ClockSeconds;
            if (Length > 0 && lastSpawnedTime >= nextSpawnTime && (Cars.Count == 0 || Cars[^1].Travelled > Cars[^1].Length))
            {
                Cars.Add(new RoadCar(this, CarSpawnerObj.CarAverageSpeed, CarSpawnerObj.CarSpawnerListIndex));

                lastSpawnedTime = 0;
                nextSpawnTime = CarSpawnerObj.CarFrequency * (0.75 + StaticRandom.NextDouble() / 2);
            }

            Cars.RemoveAll(car => car.Travelled >= Length);

            List<RoadCarCrossing> crossings = Crossings;
            if (crossings.Any(c => float.IsNaN(c.TrackHeight)))
            {
                Crossings.Clear();
                Crossings.AddRange(crossings.Select(c =>
                {
                    if (!float.IsNaN(c.TrackHeight) || !Simulator.Instance.LevelCrossings.RoadToTrackCrossingItems.TryGetValue(c.Item, out LevelCrossingItem value))
                        return c;
                    float height = value.Location.Location.Y + TrackRailHeight - c.Item.Location.Location.Y;
                    return new RoadCarCrossing(c.Item, c.Distance, height <= TrainRailHeightMaximum ? height : 0);
                }));
            }
        }

        internal float GetRoadHeightAdjust(float distance)
        {
            List<RoadCarCrossing> crossings = Crossings;
            for (var i = 0; i < crossings.Count; i++)
            {
                // Crossing is too far down the path, we can quit.
                if (distance <= crossings[i].DistanceAdjust1)
                    break;
                if (!float.IsNaN(crossings[i].TrackHeight))
                {
                    // Location is approaching a track.
                    if (crossings[i].DistanceAdjust1 <= distance && distance <= crossings[i].DistanceAdjust2)
                        return MathHelper.Lerp(0, crossings[i].TrackHeight, (distance - crossings[i].DistanceAdjust1) / RampLength);
                    // Location is crossing a track.
                    if (crossings[i].DistanceAdjust2 <= distance && distance <= crossings[i].DistanceAdjust3)
                        return crossings[i].TrackHeight;
                    // Crossings are close enough to count as joined.
                    if (i + 1 < crossings.Count && !float.IsNaN(crossings[i + 1].TrackHeight) && crossings[i + 1].Distance - crossings[i].Distance < TrackMergeDistance)
                    {
                        // Location is between two crossing tracks.
                        if (crossings[i].DistanceAdjust3 <= distance && distance <= crossings[i + 1].DistanceAdjust2)
                            return MathHelper.Lerp(crossings[i].TrackHeight, crossings[i + 1].TrackHeight, (distance - crossings[i].DistanceAdjust3) / (crossings[i + 1].DistanceAdjust2 - crossings[i].DistanceAdjust3));
                    }
                    else
                    {
                        // Location is passing a track.
                        if (crossings[i].DistanceAdjust3 <= distance && distance <= crossings[i].DistanceAdjust4)
                            return MathHelper.Lerp(crossings[i].TrackHeight, 0, (distance - crossings[i].DistanceAdjust3) / RampLength);
                    }
                }
            }
            return 0;
        }
    }
}
