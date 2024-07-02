using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;

namespace Orts.Simulation.World
{
    public class RoadCar : IWorldPosition
    {
        public const float VisualHeightAdjustment = 0.1f;
        private const float AccelerationFactor = 5;
        private const float BrakingFactor = 5;
        private const float BrakingMinFactor = 1;
        private float speedMax;
        private int nextCrossingIndex;

        private WorldPosition position;

        public RoadCarSpawner Spawner { get; }

        public int CarSpawnerListIdx { get; }
        public int Type { get; }
        public float Length { get; }
        public float Travelled { get; private set; }
        public bool IgnoreXRotation { get; }
        public bool CarriesCamera { get; set; }

        public Traveller FrontTraveller { get; }
        public Traveller RearTraveller { get; }
        public float Speed { get; private set; }

        public Vector3 FrontLocation
        {
            get
            {
                return new Vector3(FrontTraveller.WorldLocation.Location.X,
                    FrontTraveller.WorldLocation.Location.Y + Math.Max(Spawner.GetRoadHeightAdjust(Travelled - Length * 0.25f), 0) + VisualHeightAdjustment,
                    FrontTraveller.WorldLocation.Location.Z);
            }
        }
        public Vector3 RearLocation
        {
            get
            {
                WorldLocation location = RearTraveller.WorldLocation.NormalizeTo(FrontTraveller.Tile);
                return new Vector3(location.Location.X,
                    location.Location.Y + Math.Max(Spawner.GetRoadHeightAdjust(Travelled + Length * 0.25f), 0) + VisualHeightAdjustment,
                    location.Location.Z);
            }
        }

        public ref readonly WorldPosition WorldPosition
        {
            get
            {
                // TODO: Add 0.1f to Y to put wheels above road. Matching MSTS?
                Vector3 front = FrontLocation;
                Vector3 rear = RearLocation;
                float frontY = front.Y;
                float rearY = rear.Y;
                if (IgnoreXRotation)
                {
                    frontY -= VisualHeightAdjustment;
                    rearY -= VisualHeightAdjustment;
                    if (Math.Abs(frontY - rearY) > 0.01f)
                    {
                        if (frontY > rearY)
                            rearY = frontY;
                        else
                            frontY = rearY;
                    }
                }
                position = new WorldPosition(FrontTraveller.Tile, MatrixExtension.XNAMatrixFromMSTSCoordinates(front.X, frontY, front.Z, rear.X, rearY, rear.Z));
                return ref position;
            }
        }

        public RoadCar(RoadCarSpawner spawner, float averageSpeed, int carSpawnerListIdx)
        {
            Spawner = spawner;
            CarSpawnerListIdx = carSpawnerListIdx;
            Type = StaticRandom.Next() % Simulator.Instance.CarSpawnerLists[this.CarSpawnerListIdx].Count;
            Length = Simulator.Instance.CarSpawnerLists[this.CarSpawnerListIdx][Type].Distance;
            // Front and rear travellers approximate wheel positions at 25% and 75% along vehicle.
            FrontTraveller = new Traveller(spawner.Traveller);
            FrontTraveller.Move(Length * 0.15f);
            RearTraveller = new Traveller(spawner.Traveller);
            RearTraveller.Move(Length * 0.85f);
            // Travelled is the center of the vehicle.
            Travelled = Length * 0.50f;
            Speed = speedMax = averageSpeed * (0.75f + (float)StaticRandom.NextDouble() / 2);
            IgnoreXRotation = Simulator.Instance.CarSpawnerLists[this.CarSpawnerListIdx].IgnoreXRotation;
        }

        public void Update(in ElapsedTime elapsedTime)
        {
            List<RoadCarCrossing> crossings = Spawner.Crossings;

            // We skip any crossing that we have passed (Travelled + Length / 2) or are too close to stop at (+ Speed * BrakingMinFactor).
            // We skip any crossing that is part of the same group as the previous.
            while (nextCrossingIndex < crossings.Count
                && ((Travelled + Length / 2 + Speed * BrakingMinFactor > crossings[nextCrossingIndex].Distance)
                || (nextCrossingIndex > 0 && crossings[nextCrossingIndex].Item.CrossingGroup != null && crossings[nextCrossingIndex].Item.CrossingGroup == crossings[nextCrossingIndex - 1].Item.CrossingGroup)))
            {
                nextCrossingIndex++;
            }

            // Calculate all the distances to items we need to stop at (level crossings, other cars).
            List<float> stopDistances = new List<float>();
            for (int i = nextCrossingIndex; i < crossings.Count; i++)
            {
                if (crossings[i].Item.CrossingGroup != null && crossings[i].Item.CrossingGroup.HasTrain)
                {
                    // TODO: Stopping distance for level crossings!
                    stopDistances.Add(crossings[i].Distance - RoadCarSpawner.StopDistance);
                    break;
                }
            }
            // TODO: Maybe optimise this?
            List<RoadCar> cars = Spawner.Cars;
            int spawnerIndex = cars.IndexOf(this);
            if (spawnerIndex > 0)
            {
                if (!cars[spawnerIndex - 1].CarriesCamera)
                    stopDistances.Add(cars[spawnerIndex - 1].Travelled - cars[spawnerIndex - 1].Length / 2);
                else
                    stopDistances.Add(cars[spawnerIndex - 1].Travelled - cars[spawnerIndex - 1].Length * 0.65f - 4 - cars[spawnerIndex - 1].Speed * 0.5f);
            }

            // Calculate whether we're too close to the minimum stopping distance (and need to slow down) or going too slowly (and need to speed up).
            var stopDistance = stopDistances.Count > 0 ? stopDistances.Min() - Travelled - Length / 2 : float.MaxValue;
            var slowingDistance = BrakingFactor * Length;
            if (stopDistance < slowingDistance)
                Speed = speedMax * (float)Math.Sin((Math.PI / 2) * (stopDistance / slowingDistance));
            else if (Speed < speedMax)
                Speed = (float)Math.Min(Speed + AccelerationFactor / Length * elapsedTime.ClockSeconds, speedMax);
            else if (Speed > speedMax)
                Speed = (float)Math.Max(Speed - AccelerationFactor / Length * elapsedTime.ClockSeconds * 2, speedMax);

            double distance = elapsedTime.ClockSeconds * Speed;
            Travelled += (float)distance;
            FrontTraveller.Move(distance);
            RearTraveller.Move(distance);
        }

        public void ChangeSpeed(float speed)
        {
            if (speed > 0)
            {
                if (speedMax < Spawner.CarSpawnerObj.CarAverageSpeed * 1.25f)
                    speedMax = Math.Min(speedMax + speed * 2, Spawner.CarSpawnerObj.CarAverageSpeed * 1.25f);
            }
            else if (speed < 0)
            {
                if (speedMax > Spawner.CarSpawnerObj.CarAverageSpeed * 0.25f)
                    speedMax = Math.Max(speedMax + speed * 2, Spawner.CarSpawnerObj.CarAverageSpeed * 0.25f);
            }
        }
    }
}
