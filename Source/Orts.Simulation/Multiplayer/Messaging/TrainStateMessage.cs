using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using MemoryPack;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class TrainCarFreightAnimationItem
    {
        public string FileName { get; set; }
        public string DirectoryName { get; set; }
        public LoadPosition LoadPosition { get; set; }

        [MemoryPackConstructor]
        public TrainCarFreightAnimationItem() { }

        public TrainCarFreightAnimationItem(FreightAnimationDiscrete freightAnimation)
        {
            FileName = Path.GetFileNameWithoutExtension(freightAnimation.Container.LoadFilePath);
            DirectoryName = Path.GetRelativePath(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, Path.GetDirectoryName(freightAnimation.Container.LoadFilePath));
            LoadPosition = freightAnimation.LoadPosition;
        }
    }

    [MemoryPackable]
    public partial class TrainCarItem
    {
        public string TrainCarId { get; set; }
        public bool Flipped { get; set; }
        public float Length { get; set; }
        public string WagonFilePath { get; set; }

        public Collection<TrainCarFreightAnimationItem> FreightAnimations { get; private set; }

        [MemoryPackConstructor]
        public TrainCarItem() { }

        public TrainCarItem(TrainCar trainCar)
        {
            TrainCarId = trainCar.CarID;
            Flipped = trainCar.Flipped;
            Length = trainCar.CarLengthM;
            WagonFilePath = trainCar.RealWagFilePath;

            //wagon path without folder name
            int index = WagonFilePath.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                WagonFilePath = WagonFilePath.Remove(0, index + 17);
            }

            if (trainCar.FreightAnimations != null)
            {
                foreach (FreightAnimation animation in trainCar.FreightAnimations.Animations)
                {
                    if (animation is FreightAnimationDiscrete discreteAnimation)
                    {
                        FreightAnimations ??= new Collection<TrainCarFreightAnimationItem>();
                        FreightAnimations.Add(new TrainCarFreightAnimationItem(discreteAnimation));
                    }
                }
            }
        }
    }

    [MemoryPackable]
    public partial class TrainStateMessage : MultiPlayerMessageContent
    {
        public Collection<TrainCarItem> TrainCars { get; private set; }
        public int TrainNumber { get; set; }
        public Direction TrainDirection { get; set; }
        public WorldLocation RearLocation { get; set; }
        public string TrainName { get; set; }
        public float DistanceTravelled { get; set; }
        public MidpointDirection MultiUnitDirection { get; set; }
        public float Length { get; set; }

        [MemoryPackConstructor]
        public TrainStateMessage() { }

        public TrainStateMessage(Train train)
        {
            ArgumentNullException.ThrowIfNull(train, nameof(train));

            TrainNumber = train.Number;
            TrainName = train.Name;
            DistanceTravelled = train.DistanceTravelled;
            RearLocation = train.RearTDBTraveller.WorldLocation;
            TrainDirection = train.RearTDBTraveller.Direction.Reverse();
            Length = train.Length;

            TrainCars = new Collection<TrainCarItem>();
            foreach (TrainCar trainCar in train.Cars)
            {
                TrainCars.Add(new TrainCarItem(trainCar));
            }
        }

        public override void HandleMessage()
        {
            // construct train data
            Train train = new Train
            {
                Number = TrainNumber,

                TrainType = TrainType.Remote,
                DistanceTravelled = DistanceTravelled,
                MUDirection = MultiUnitDirection,
                RearTDBTraveller = new Traveller(RearLocation, TrainDirection)
            };

            foreach (TrainCarItem trainCarItem in TrainCars)
            {
                string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, trainCarItem.WagonFilePath);
                TrainCar car = null;
                try
                {
                    car = RollingStock.Load(train, wagonFilePath);
                    car.CarLengthM = trainCarItem.Length;

                    List<LoadData> loadDataList = null;
                    foreach (TrainCarFreightAnimationItem freightAnimationItem in trainCarItem.FreightAnimations ?? Enumerable.Empty<TrainCarFreightAnimationItem>())
                    {
                        loadDataList ??= new List<LoadData>();
                        LoadData loadData = new LoadData(freightAnimationItem.FileName, freightAnimationItem.DirectoryName, freightAnimationItem.LoadPosition);
                        loadDataList.Add(loadData);
                    }
                    if (loadDataList != null && loadDataList.Count > 0)
                        car.FreightAnimations?.Load(loadDataList);
                }
                catch (Exception error)
                {
                    Trace.WriteLine(wagonFilePath + " " + error);
                    car = MultiPlayerManager.Instance().SubCar(train, wagonFilePath, trainCarItem.Length);
                }
                if (car == null)
                    continue;

                car.Flipped = trainCarItem.Flipped;
                car.CarID = trainCarItem.TrainCarId;
            }

            if (train.Cars.Count == 0)
                return;
            train.Name = TrainName;

            train.InitializeBrakes();
            //train.InitializeSignals(false);//client do it won't have impact
            train.CheckFreight();
            train.SetDistributedPowerUnitIds();
            train.ReinitializeEOT();
            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTrainPosition();

            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars();
            train.ResetInitialTrainRoute(tempRoute);

            train.CalculatePositionOfCars();
            train.AITrainBrakePercent = 100;

            train.LeadLocomotive = train.Cars[0] as MSTSLocomotive;
            if (train.Cars[0].CarID.StartsWith("AI"))
            {
                // It's an AI train for the server, raise pantos and light lights
                train.LeadLocomotive?.SignalEvent(TrainEvent.HeadlightOn);
                if (train.Cars.Exists(x => x is MSTSElectricLocomotive))
                {
                    train.SignalEvent(PowerSupplyEvent.RaisePantograph, 1);
                }
            }
            multiPlayerManager.AddOrRemoveTrain(train, true);
        }
    }
}
