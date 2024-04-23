using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MemoryPack;

using Microsoft.VisualBasic.ApplicationServices;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer.Messaging
{

    [MemoryPackable]
    public sealed partial class TrainUncoupleMessage : TrainStateBaseMessage
    {
        public TrainStateMessage DetachedTrain { get; set; }

        public int DetachedTrainNumber { get; set; }

        public string CurrentTrainFirstCarId { get; set; }
        public string DetachedTrainFirstCarId { get; set; }
        public DecoupleTrainOwner TrainOwner { get; set; }

        [MemoryPackConstructor]
        public TrainUncoupleMessage() { }

        public TrainUncoupleMessage(Train train, Train detachedTrain) : base(train, true)
        {
            ArgumentNullException.ThrowIfNull(detachedTrain, nameof(detachedTrain));

            if (!train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                //the old train should have the player, else switch train and numbers
                (train, detachedTrain) = (detachedTrain, train);
                (train.Number, detachedTrain.Number) = (detachedTrain.Number, train.Number);
            }

            DetachedTrain = new TrainStateMessage(detachedTrain);

            if (multiPlayerManager.IsDispatcher)
                DetachedTrainNumber = detachedTrain.Number;//server will use the correct number
            else
            {
                DetachedTrainNumber = 1000000 + StaticRandom.Next(1000000);//client: temporary assign a train number 1000000-2000000, will change to the correct one after receiving response from the server
                detachedTrain.TrainType = TrainType.Remote; //by default, uncoupled train will be controlled by the server
            }

            static void UpdateLeadLocomotive(Train updateTrain)
            {
                if (!updateTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive)) //if detachedTrain does not have player locomotive, it may be controlled remotely
                {
                    if (updateTrain.LeadLocomotive == null)
                    {
                        updateTrain.LeadLocomotiveIndex = -1;
                        updateTrain.LeadNextLocomotive();
                    }

                    foreach (TrainCar car in updateTrain.Cars)
                    {
                        car.Train = updateTrain;
                        foreach (KeyValuePair<string, OnlinePlayer> player in MultiPlayerManager.OnlineTrains.Players)
                        {
                            if (car.CarID.StartsWith(player.Value.LeadingLocomotiveID))
                            {
                                player.Value.Train = car.Train;
                                car.Train.TrainType = TrainType.Remote;
                                break;
                            }
                        }
                    }
                }
            }

            UpdateLeadLocomotive(detachedTrain); //if detachedTrain does not have player locomotive, it may be controlled remotely
            UpdateLeadLocomotive(train); //if train (old train) does not have player locomotive, it may be controlled remotely

            if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive) || detachedTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("Trains uncoupled, gain back control by Alt-E"));
            }

            detachedTrain.Number = DetachedTrainNumber;
            DetachedTrainFirstCarId = detachedTrain.LeadLocomotive != null ? "Leading " + detachedTrain.LeadLocomotive.CarID : "First " + detachedTrain.Cars[0].CarID;
            CurrentTrainFirstCarId = train.LeadLocomotive != null ? "Leading " + train.LeadLocomotive.CarID : "First " + train.Cars[0].CarID;

            //to see which train contains the car (PlayerLocomotive)
            TrainOwner = train.Cars.Contains(Simulator.Instance.PlayerLocomotive)
                ? DecoupleTrainOwner.OriginalTrain
                : detachedTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive) ? DecoupleTrainOwner.DetachedTrain : DecoupleTrainOwner.None;

        }

        public override void HandleMessage()
        {
            bool oldIDIsLead = true, newIDIsLead = true;
            if (DetachedTrainFirstCarId.StartsWith("First ", StringComparison.OrdinalIgnoreCase))
            {
                DetachedTrainFirstCarId = DetachedTrainFirstCarId.Replace("First ", "", StringComparison.OrdinalIgnoreCase);
                newIDIsLead = false;
            }
            else
                DetachedTrainFirstCarId = DetachedTrainFirstCarId.Replace("Leading ", "", StringComparison.OrdinalIgnoreCase);
            if (CurrentTrainFirstCarId.StartsWith("First "))
            {
                CurrentTrainFirstCarId = CurrentTrainFirstCarId.Replace("First ", "", StringComparison.OrdinalIgnoreCase);
                oldIDIsLead = false;
            }
            else
                CurrentTrainFirstCarId = CurrentTrainFirstCarId.Replace("Leading ", "", StringComparison.OrdinalIgnoreCase);

            if (User == multiPlayerManager.UserName) //received from the server, but it is about mine action of uncouple
            {
                foreach (Train train in Simulator.Instance.Trains)
                {
                    foreach (TrainCar car in train.Cars)
                    {
                        if (car.CarID == CurrentTrainFirstCarId)//got response about this train
                        {
                            train.Number = TrainNumber;
                            if (oldIDIsLead == true)
                                train.LeadLocomotive = car as MSTSLocomotive;
                        }
                        if (car.CarID == DetachedTrainFirstCarId)//got response about this train
                        {
                            train.Number = DetachedTrain.TrainNumber;
                            if (newIDIsLead == true)
                                train.LeadLocomotive = car as MSTSLocomotive;
                        }
                    }
                }
            }
            else
            {
                Train currentTrain = null;
                List<TrainCar> trainCars = null;
                TrackCircuitPartialPathRoute tempRoute;
                foreach (Train train in Simulator.Instance.Trains)
                {
                    bool found = false;
                    foreach (TrainCar car in train.Cars)
                    {
                        if (car.CarID == CurrentTrainFirstCarId)//got response about this train
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {
                        currentTrain = train;
                        MSTSLocomotive lead = currentTrain.LeadLocomotive;
                        trainCars = train.Cars;
                        List<TrainCar> tmpcars = new List<TrainCar>();
                        foreach(TrainCarItem trainCarItem in TrainCars ?? Enumerable.Empty<TrainCarItem>())
                        {
                            TrainCar car = FindCarById(trainCars, trainCarItem.TrainCarId);
                            if (car == null)
                                continue;
                            car.Flipped = trainCarItem.Flipped;
                            tmpcars.Add(car);
                        }
                        if (tmpcars.Count == 0)
                            return;
                        if (multiPlayerManager.IsDispatcher)
                            multiPlayerManager.AddOrRemoveLocomotives(User, currentTrain, false);
                        train.Cars.Clear();
                        train.Cars.AddRange(tmpcars);
                        train.RearTDBTraveller = new Traveller(RearLocation, TrainDirection.Reverse());
                        train.DistanceTravelled = DistanceTravelled;
                        train.SpeedMpS = Speed;
                        train.LeadLocomotive = lead;
                        train.MUDirection = MultiUnitDirection;
                        currentTrain.ControlMode = TrainControlMode.Explorer;
                        currentTrain.CheckFreight();
                        currentTrain.SetDistributedPowerUnitIds();
                        currentTrain.ReinitializeEOT();
                        currentTrain.InitializeBrakes();
                        tempRoute = currentTrain.CalculateInitialTrainPosition();
                        if (tempRoute.Count == 0)
                        {
                            throw new InvalidDataException("Remote train original position not clear");
                        }

                        currentTrain.SetInitialTrainRoute(tempRoute);
                        currentTrain.CalculatePositionOfCars();
                        currentTrain.ResetInitialTrainRoute(tempRoute);

                        currentTrain.CalculatePositionOfCars();
                        currentTrain.AITrainBrakePercent = 100;
                        //train may contain myself, and no other players, thus will make myself controlling this train
                        if (currentTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                        {
                            Simulator.Instance.PlayerLocomotive.Train = currentTrain;
                            //train.TrainType = Train.TRAINTYPE.PLAYER;
                            currentTrain.InitializeBrakes();
                        }
                        foreach (TrainCar c in currentTrain.Cars)
                        {
                            if (c.CarID == CurrentTrainFirstCarId && oldIDIsLead)
                                currentTrain.LeadLocomotive = c as MSTSLocomotive;
                            foreach (OnlinePlayer player in MultiPlayerManager.OnlineTrains.Players.Values)
                            {
                                if (player.LeadingLocomotiveID == c.CarID)
                                    player.Train = currentTrain;
                            }
                        }
                        break;
                    }
                }

                if (currentTrain == null || trainCars == null)
                    return;

                Train detachedTrain = new Train();
                List<TrainCar> tmpcars2 = new List<TrainCar>();
                foreach (TrainCarItem trainCarItem in DetachedTrain.TrainCars ?? Enumerable.Empty<TrainCarItem>())
                {
                    TrainCar car = FindCarById(trainCars, trainCarItem.TrainCarId);
                    if (car == null)
                        continue;
                    car.Flipped = trainCarItem.Flipped;
                    tmpcars2.Add(car);
                }
                if (tmpcars2.Count == 0)
                    return;
                detachedTrain.Cars.Clear();
                detachedTrain.Cars.AddRange(tmpcars2);
                detachedTrain.Name = string.Concat(currentTrain.Name, Train.TotalNumber.ToString());
                detachedTrain.LeadLocomotive = null;
                detachedTrain.LeadNextLocomotive();
                detachedTrain.CheckFreight();
                detachedTrain.SetDistributedPowerUnitIds();
                currentTrain.ReinitializeEOT();

                // and fix up the travellers
                detachedTrain.RearTDBTraveller = new Traveller(DetachedTrain.RearLocation, DetachedTrain.TrainDirection.Reverse());
                detachedTrain.DistanceTravelled = DetachedTrain.DistanceTravelled;
                detachedTrain.SpeedMpS = DetachedTrain.Speed;
                detachedTrain.MUDirection = DetachedTrain.MultiUnitDirection;
                detachedTrain.ControlMode = TrainControlMode.Explorer;
                detachedTrain.CheckFreight();
                detachedTrain.SetDistributedPowerUnitIds();
                currentTrain.ReinitializeEOT();
                detachedTrain.InitializeBrakes();
                tempRoute = detachedTrain.CalculateInitialTrainPosition();
                if (tempRoute.Count == 0)
                {
                    throw new InvalidDataException("Remote train original position not clear");
                }

                detachedTrain.SetInitialTrainRoute(tempRoute);
                detachedTrain.CalculatePositionOfCars();
                detachedTrain.ResetInitialTrainRoute(tempRoute);

                detachedTrain.CalculatePositionOfCars();
                detachedTrain.AITrainBrakePercent = 100;
                if (detachedTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                {
                    Simulator.Instance.PlayerLocomotive.Train = detachedTrain;
                    //train2.TrainType = Train.TRAINTYPE.PLAYER;
                    detachedTrain.InitializeBrakes();
                }
                foreach (TrainCar car in detachedTrain.Cars)
                {
                    if (car.CarID == DetachedTrainFirstCarId && newIDIsLead)
                        detachedTrain.LeadLocomotive = car as MSTSLocomotive;
                    car.Train = detachedTrain;
                    foreach (OnlinePlayer player in MultiPlayerManager.OnlineTrains.Players.Values)
                    {
                        if (car.CarID.StartsWith(player.LeadingLocomotiveID))
                        {
                            player.Train = car.Train;
                            //car.Train.TrainType = Train.TRAINTYPE.REMOTE;
                            break;
                        }
                    }
                }

                currentTrain.UncoupledFrom = detachedTrain;
                detachedTrain.UncoupledFrom = currentTrain;

                if (currentTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive) || detachedTrain.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                {
                    Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("Trains uncoupled, gain back control by Alt-E"));
                }

                multiPlayerManager.AddOrRemoveTrain(detachedTrain, true);

                if (multiPlayerManager.IsDispatcher)
                {
                    DetachedTrain.TrainNumber = detachedTrain.Number;//we got a new train number, will tell others.
                    detachedTrain.TrainType = TrainType.Static;
                    TrainNumber = currentTrain.Number;
                    detachedTrain.LastReportedSpeed = 1;
                    if (detachedTrain.Name.Length < 4)
                        detachedTrain.Name = string.Concat("STATIC-", detachedTrain.Name);
                    Simulator.Instance.AI.TrainListChanged = true;
                    multiPlayerManager.AddOrRemoveLocomotives(User, currentTrain, true);
                    multiPlayerManager.AddOrRemoveLocomotives(User, detachedTrain, true);
                    MultiPlayerManager.Broadcast(this);//if server receives this, will tell others, including whoever sent the information
                }
                else
                {
                    detachedTrain.TrainType = TrainType.Remote;
                    detachedTrain.Number = DetachedTrain.TrainNumber; //client receives a message, will use the train number specified by the server
                    currentTrain.Number = TrainNumber;
                }
            }
        }

        private static TrainCar FindCarById(List<TrainCar> list, string carId)
        {
            foreach (TrainCar car in list)
                if (car.CarID == carId)
                    return car;
            return null;
        }

    }
}