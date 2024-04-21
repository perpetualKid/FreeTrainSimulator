using System;
using System.Collections.ObjectModel;

using Orts.Common;
using Orts.Common.Position;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    public abstract class TrainStateBaseMessage: MultiPlayerMessageContent
    {
        public Collection<TrainCarItem> TrainCars { get; private protected set; }
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public float Speed { get; set; }
        public int CarCount { get; set; }
        public Direction TrainDirection { get; set; }
        public WorldLocation RearLocation { get; set; }
        public int TrackNodeIndex { get; set; }
        public float Length { get; set; }
        public float DistanceTravelled { get; set; }
        public MidpointDirection MultiUnitDirection { get; set; }

        protected TrainStateBaseMessage() { }

        protected TrainStateBaseMessage(Train train, bool initializeCars = false) 
        {
            ArgumentNullException.ThrowIfNull(train, nameof(train));

            TrainNumber = train.Number;
            TrainName = train.Name;
            Speed = train.SpeedMpS;
            CarCount = train.Cars.Count;
            DistanceTravelled = train.DistanceTravelled;
            RearLocation = train.RearTDBTraveller.WorldLocation;
            TrackNodeIndex = train.RearTDBTraveller.TrackNode.Index;
            TrainDirection = train.RearTDBTraveller.Direction.Reverse();
            Length = train.Length;

            if (initializeCars)
            {
                TrainCars = new Collection<TrainCarItem>();
                foreach (TrainCar trainCar in train.Cars)
                {
                    TrainCars.Add(new TrainCarItem(trainCar));
                }
            }
        }
    }
}
