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
    public sealed partial class TrainUpdateMessage : TrainStateBaseMessage
    {
        [MemoryPackConstructor]
        public TrainUpdateMessage() { }

        public TrainUpdateMessage(string user, Train train) : base(train, true)
        { 
            User = user;
        }

        public override void HandleMessage()
        {
            if (User != multiPlayerManager.UserName)
                return; //not the one requested GetTrain
            Train train;
            train = Simulator.Instance.Trains.FirstOrDefault(t => t.Number == TrainNumber);
            if (train != null) //existing train
            {
                Traveller traveller = new Traveller(RearLocation, TrainDirection.Reverse());
                List<TrainCar> trainCars = new List<TrainCar>();

                foreach (TrainCarItem trainCarItem in TrainCars ?? Enumerable.Empty<TrainCarItem>())
                {
                    string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, trainCarItem.WagonFilePath);
                    TrainCar car = train.Cars.FirstOrDefault(car => car.CarID == trainCarItem.TrainCarId);
                    try
                    {
                        car ??= RollingStock.Load(train, wagonFilePath);
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
                    trainCars.Add(car);
                }

                if (trainCars.Count == 0)
                    return;

                // Replace the train car list (after loading, the order needs to be the same as in the message)
                train.Cars.Clear();
                train.Cars.AddRange(trainCars);
                train.MUDirection = MultiUnitDirection;
                train.RearTDBTraveller = traveller;
                train.CalculatePositionOfCars();
                train.DistanceTravelled = DistanceTravelled;
                train.CheckFreight();
                train.SetDistributedPowerUnitIds();
                train.ReinitializeEOT();
            }
            // New train
            else
            {
                train = new Train()
                {
                    Number = TrainNumber,
                    TrainType = TrainType.Remote,
                    DistanceTravelled = DistanceTravelled,
                    RearTDBTraveller = new Traveller(RearLocation, TrainDirection.Reverse())
                };

                foreach (TrainCarItem trainCarItem in TrainCars ?? Enumerable.Empty<TrainCarItem>())
                {

                    string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, trainCarItem.WagonFilePath);
                    TrainCar car = null;
                    try
                    {
                        car = RollingStock.Load(train, wagonFilePath);
                        car.CarLengthM = trainCarItem.Length;
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
                train.MUDirection = MultiUnitDirection;
                //train1.CalculatePositionOfCars(0);
                train.InitializeBrakes();
                //train1.InitializeSignals(false);
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
                multiPlayerManager.AddOrRemoveTrain(train, true);
                if (multiPlayerManager.IsDispatcher)
                    multiPlayerManager.AddOrRemoveLocomotives(User, train, true);
            }
        }
    }
}
