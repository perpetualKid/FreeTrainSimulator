// COPYRIGHT 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.AIs;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer
{
    public class OnlineTrains
    {
        public Dictionary<string, OnlinePlayer> Players { get; } = new Dictionary<string, OnlinePlayer>();

        public IList<OnlineLocomotive> OnlineLocomotives { get; } = new List<OnlineLocomotive>();

        public OnlineTrains()
        {
        }

        public static void Update()
        {

        }

        public Train FindTrain(string name)
        {

            Players.TryGetValue(name, out OnlinePlayer player);
            return player?.Train;
        }

        public bool FindTrain(Train t)
        {

            foreach (OnlinePlayer o in Players.Values)
            {
                if (o.Train == t)
                    return true;
            }
            return false;
        }

        public Collection<MoveMessage> MoveTrains()
        {
            Collection<MoveMessage> result = new Collection<MoveMessage>();

            foreach (Train train in Simulator.Instance.Trains)
            {
                if (Simulator.Instance.PlayerLocomotive != null && train == Simulator.Instance.PlayerLocomotive.Train)
                    continue;//player drived train
                if (train == null || FindTrain(train))
                    continue;//is an online player controlled train
                if (train.SpeedMpS != 0 || train.LastReportedSpeed != 0)
                {
                    result.Add(new MoveMessage(train) { User = "0xAI" });
                }
            }
            return result;
        }

        public IEnumerable<PlayerStateMessage> AllPlayerTrains()
        {
            return Players.Values.Where(player => player != null).Select(player => new PlayerStateMessage(player.Train) { User = player.Username });
        }

        public void AddPlayers(PlayerStateMessage playerState)
        {
            if (Players.ContainsKey(playerState.User))
                return;
            if (playerState.User == MultiPlayerManager.Instance().UserName)
                return; //do not add self//WARNING: may need to worry about train number here
            OnlinePlayer p = new OnlinePlayer(
                playerState.User,
                Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.ConsistsFolder, playerState.ConsistFile),
                Path.Combine(Simulator.Instance.RouteFolder.PathsFolder, playerState.PathFile))
            {
                LeadingLocomotiveID = playerState.PlayerLocomotive.LocomotiveId
            };
            Train train = new Train
            {
                TrainType = TrainType.Remote
            };
            if (MultiPlayerManager.IsServer()) //server needs to worry about correct train number
            {
            }
            else
            {
                train.Number = playerState.TrainState.TrainNumber;
            }
            if (playerState.ConsistFile.Contains("tilted", StringComparison.OrdinalIgnoreCase))
                train.IsTilting = true;
            train.DistanceTravelled = playerState.TrainState.DistanceTravelled;
            train.TrainMaxSpeedMpS = playerState.TrainState.Speed;

            if (MultiPlayerManager.IsServer())
            {
                try
                {
                    AIPath aiPath = new AIPath(p.Path, Simulator.Instance.TimetableMode);
                }
                catch (Exception)
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(playerState.User, ControlMessageType.Warning, "Server does not have path file provided, signals may always be red for you."));
                }
            }

            try
            {
                train.RearTDBTraveller = new Traveller(playerState.TrainState.RearLocation, playerState.TrainState.TrainDirection.Reverse());
            }
            catch (Exception e) when (MultiPlayerManager.IsServer())
            {
                MultiPlayerManager.Broadcast(new ControlMessage(playerState.User, ControlMessageType.Error, "Multiplayer Error：" + e.Message));
            }
            foreach (TrainCarItem trainCarItem in playerState.TrainState.TrainCars ?? Enumerable.Empty<TrainCarItem>())
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

                if (car is MSTSWagon w)
                {
                    for (int i = 0; i < (playerState.PlayerLocomotive.Pantographs?.Count ?? 0); i++)
                    {
                        w.SignalEvent(playerState.PlayerLocomotive.Pantographs[i].CommandUp() ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, i + 1);
                    }
                }
            }

            if (train.Cars.Count == 0)
            {
                throw (new ArgumentOutOfRangeException("The train of player " + playerState.User + " is empty"));
            }

            train.ControlMode = TrainControlMode.Explorer;
            train.CheckFreight();
            train.InitializeBrakes();
            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTrainPosition();
            if (tempRoute.Count == 0)
            {
                MultiPlayerManager.Broadcast(new ControlMessage(p.Username, ControlMessageType.Error, "Cannot be placed into the game"));//server will broadcast this error
                throw new InvalidDataException("Remote train original position not clear");
            }

            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars();
            train.ResetInitialTrainRoute(tempRoute);

            train.CalculatePositionOfCars();
            train.AITrainBrakePercent = 100;

            //if (MPManager.Instance().AllowedManualSwitch) train.InitializeSignals(false);
            for (int i = 0; i < train.Cars.Count; i++)
            {
                TrainCar trainCar = train.Cars[i];
                if (trainCar.CarID == p.LeadingLocomotiveID)
                {
                    train.LeadLocomotive = trainCar as MSTSLocomotive;
                    train.LeadLocomotive.Headlight = (playerState.LeadLocomotive ?? playerState.PlayerLocomotive).HeadLight;
                    train.LeadLocomotive.UsingRearCab = (playerState.LeadLocomotive ?? playerState.PlayerLocomotive).ActiveCabView == CabViewType.Rear;
                }
                if (trainCar is MSTSLocomotive && MultiPlayerManager.IsServer())
                    MultiPlayerManager.Instance().AddOrRemoveLocomotive(playerState.User, train.Number, i, true);
            }
            if (train.LeadLocomotive == null)
            {
                train.LeadNextLocomotive();
                p.LeadingLocomotiveID = playerState.LeadLocomotive?.LocomotiveId ?? "NA";
            }

            if (train.LeadLocomotive != null)
            {
                train.Name = Train.GetTrainName(train.LeadLocomotive.CarID);
            }
            else if (train.Cars != null && train.Cars.Count > 0)
            {
                train.Name = Train.GetTrainName(train.Cars[0].CarID);
            }
            else if (playerState?.User != null)
            {
                train.Name = playerState.User;
            }

            if (MultiPlayerManager.IsServer())
            {
                train.InitializeSignals(false);
            }
            p.Train = train;

            Players.Add(playerState.User, p);
            MultiPlayerManager.Instance().AddOrRemoveTrain(train, true);
        }

        public void SwitchPlayerTrain(PlayerTrainChangeMessage switchMessage)
        {
            // find info about the new player train
            // look in all trains

            if (switchMessage.User == MultiPlayerManager.Instance().UserName)
                return; //do not add self//WARNING: may need to worry about train number here
            bool doesPlayerExist = Players.TryGetValue(switchMessage.User, out OnlinePlayer player);
            if (!doesPlayerExist)
                return;
            player.LeadingLocomotiveID = switchMessage.LocomotiveId;
            Train train;

            if (MultiPlayerManager.IsServer()) //server needs to worry about correct train number
            {
                train = Simulator.Instance.Trains.Find(t => t.Number == switchMessage.TrainNumber);
                train.TrainType = TrainType.Remote;
            }
            else
            {
                train = Simulator.Instance.Trains.Find(t => t.Number == switchMessage.TrainNumber);
                train.TrainType = TrainType.Remote;
            }
            player.Train = train;
        }

        // Save
        public void Save(BinaryWriter outf)
        {
            outf.Write(Players.Count);
            foreach (var onlinePlayer in Players.Values)
            {
                onlinePlayer.Save(outf);
            }
        }

        // Restore
        public void Restore(BinaryReader inf)
        {
            var onlinePlayersCount = inf.ReadInt32();
            if (onlinePlayersCount > 0)
            {
                while (onlinePlayersCount > 0)
                {
                    OnlinePlayer player = new OnlinePlayer(inf);
                    Players.Add(player.Username, player);
                    onlinePlayersCount -= 1;
                }
            }
        }
    }

    public struct OnlineLocomotive
    {
        public string userName;
        public int trainNumber;
        public int trainCarPosition;
    }
}
