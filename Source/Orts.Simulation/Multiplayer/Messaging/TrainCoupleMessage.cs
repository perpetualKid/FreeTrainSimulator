using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Formats.Msts;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainCoupleMessage : TrainStateBaseMessage
    {
        public int AttachedTrainNumber { get; set; }
        public LocomotiveStateMessage LeadLocomotive { get; set; }
        public string Controller { get; set; }

        [MemoryPackConstructor]
        public TrainCoupleMessage() { }

        public TrainCoupleMessage(Train train, Train attachedTrain) : base(train, true)
        {
            ArgumentNullException.ThrowIfNull(attachedTrain, nameof(attachedTrain));

            AttachedTrainNumber = attachedTrain.Number;

            MultiPlayerManager.Instance().RemoveUncoupledTrains(train); //remove the trains from uncoupled train lists
            MultiPlayerManager.Instance().RemoveUncoupledTrains(attachedTrain);

            LeadLocomotive = new LocomotiveStateMessage(train.LeadLocomotive);

            Controller = "NA";
            int index = 0;
            if (train.LeadLocomotive != null)
                index = train.LeadLocomotive.CarID.IndexOf(" - 0", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                Controller = train.LeadLocomotive.CarID[..index];
            }

            foreach (OnlinePlayer player in MultiPlayerManager.OnlineTrains.Players.Values)
            {
                if (player.Train == attachedTrain)
                {
                    player.Train = train;
                    break;
                }
            }
            if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("Trains coupled, hit \\ then Shift-? to release brakes"));
            }
            if (!multiPlayerManager.IsDispatcher || !(attachedTrain.TrainType == TrainType.AiIncorporated))
            {
                if (multiPlayerManager.IsDispatcher)
                {
                    multiPlayerManager.AddOrRemoveLocomotives(string.Empty, attachedTrain, false);
                    multiPlayerManager.AddOrRemoveLocomotives(string.Empty, train, false);
                    string player = "";
                    foreach (KeyValuePair<string, OnlinePlayer> playerValues in MultiPlayerManager.OnlineTrains.Players)
                    {
                        if (playerValues.Value.Train == train)
                        {
                            player = playerValues.Key;
                            break;
                        }
                    }
                    multiPlayerManager.AddOrRemoveLocomotives(player, train, true);
                }
                multiPlayerManager.AddOrRemoveTrain(attachedTrain, false); //remove the old train
            }
        }

        public override void HandleMessage()
        {
            Train train = null, train2 = null;

            foreach (Train t in Simulator.Instance.Trains)
            {
                if (t.Number == TrainNumber)
                    train = t;
                if (t.Number == AttachedTrainNumber)
                    train2 = t;
            }

            TrainCar lead = train.LeadLocomotive;
            lead ??= train2.LeadLocomotive;

            if (train == null || train2 == null)
                return; //did not find the trains to op on

            List<TrainCar> trainCars = new List<TrainCar>();
            foreach (TrainCarItem trainCarItem in TrainCars ?? Enumerable.Empty<TrainCarItem>())
            {
                TrainCar car = FindCar(train, train2, trainCarItem.TrainCarId);
                car.Flipped = trainCarItem.Flipped;
                car.CarID = trainCarItem.TrainCarId;
                car.Train = train;
                trainCars.Add(car);
            }

            if (trainCars.Count == 0)
                return;

            train.Cars.Clear();
            train.Cars.AddRange(trainCars);

            train.DistanceTravelled = DistanceTravelled;
            train.MUDirection = MultiUnitDirection;
            train.RearTDBTraveller = new Traveller(RearLocation, TrainDirection.Reverse());
            train.CheckFreight();
            train.SetDistributedPowerUnitIds();
            train.ReinitializeEOT();
            train.CalculatePositionOfCars();
            train.LeadLocomotive = null;
            train2.LeadLocomotive = null;

            foreach (TrainCar trainCar in train.Cars)
            {
                if (trainCar.CarID == LeadLocomotive.LocomotiveId)
                    train.LeadLocomotive = trainCar as MSTSLocomotive;
            }

            if (train.LeadLocomotive == null)
                train.LeadNextLocomotive();

            //mine is not the leading locomotive, thus I give up the control
            if (train.LeadLocomotive != Simulator.Instance.PlayerLocomotive)
            {
                train.TrainType = TrainType.Remote; //make the train remote controlled
            }

            if (MultiPlayerManager.FindPlayerTrain(train2))
            {
                foreach (OnlinePlayer player in MultiPlayerManager.OnlineTrains.Players.Values)
                {
                    if (player.Train == train2)
                        player.Train = train;
                    break;
                }
            }

            //update the remote user's train
            if (MultiPlayerManager.FindPlayerTrain(Controller) != null)
                MultiPlayerManager.OnlineTrains.Players[Controller].Train = train;
            if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                Simulator.Instance.PlayerLocomotive.Train = train;

            if (multiPlayerManager.IsDispatcher)
                multiPlayerManager.AddOrRemoveLocomotives("", train2, false);
            multiPlayerManager.AddOrRemoveTrain(train2, false);


            if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("Trains coupled, hit \\ then Shift-? to release brakes"));
            }
        }

        private static TrainCar FindCar(Train train1, Train train2, string carID)
        {
            foreach (TrainCar c in train1.Cars)
                if (c.CarID == carID)
                    return c;
            foreach (TrainCar c in train2.Cars)
                if (c.CarID == carID)
                    return c;
            return null;
        }

    }
}
