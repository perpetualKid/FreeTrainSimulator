using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using MemoryPack;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainStateMessage : TrainStateBaseMessage
    {

        [MemoryPackConstructor]
        public TrainStateMessage() { }

        public TrainStateMessage(Train train): base(train, true) { }

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

            foreach (TrainCarItem trainCarItem in TrainCars ?? Enumerable.Empty<TrainCarItem>())
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
                catch (IOException error)
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
            if (train.Cars[0].CarID.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
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
