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

using Orts.Formats.Msts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Track;
using Orts.Formats.Msts.Models;

namespace Orts.Simulation.MultiPlayer
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

            foreach (OnlinePlayer o in Players.Values.ToList())
            {
                if (o.Train == t)
                    return true;
            }
            return false;
        }

        public string MoveTrains(MSGMove move)
        {
            string tmp = "";
            if (move == null)
                move = new MSGMove();
            foreach (OnlinePlayer p in Players.Values)
            {
                if (p.Train != null && Simulator.Instance.PlayerLocomotive != null && !(p.Train == Simulator.Instance.PlayerLocomotive.Train && p.Train.TrainType != TrainType.Remote))
                {
                    if (Math.Abs(p.Train.SpeedMpS) > 0.001 || Math.Abs(p.Train.LastReportedSpeed) > 0)
                    {
                        move.AddNewItem(p.Username, p.Train);
                    }
                }
            }
            foreach (Train t in Simulator.Instance.Trains)
            {
                if (Simulator.Instance.PlayerLocomotive != null && t == Simulator.Instance.PlayerLocomotive.Train)
                    continue;//player drived train
                if (t == null || FindTrain(t))
                    continue;//is an online player controlled train
                if (Math.Abs(t.SpeedMpS) > 0.001 || Math.Abs(t.LastReportedSpeed) > 0)
                {
                    move.AddNewItem("0xAI" + t.Number, t);
                }
            }
            tmp += move.ToString();
            return tmp;

        }
        public string MoveAllPlayerTrain(MSGMove move)
        {
            string tmp = "";
            if (move == null)
                move = new MSGMove();
            foreach (OnlinePlayer p in Players.Values)
            {
                if (p.Train == null)
                    continue;
                if (Math.Abs(p.Train.SpeedMpS) > 0.001 || Math.Abs(p.Train.LastReportedSpeed) > 0)
                {
                    move.AddNewItem(p.Username, p.Train);
                }
            }
            tmp += move.ToString();
            return tmp;
        }

        public static string MoveAllTrain(MSGMove move)
        {
            string tmp = "";
            if (move == null)
                move = new MSGMove();
            foreach (Train t in Simulator.Instance.Trains)
            {
                if (t != null && (Math.Abs(t.SpeedMpS) > 0.001 || Math.Abs(t.LastReportedSpeed) > 0))
                {
                    move.AddNewItem("AI" + t.Number, t);
                }
            }
            tmp += move.ToString();
            return tmp;
        }

        public string AddAllPlayerTrain() //WARNING, need to change
        {
            string tmp = "";
            foreach (OnlinePlayer p in Players.Values)
            {
                if (p.Train != null)
                {
                    MSGPlayer player = new MSGPlayer(p.Username, "1234", p.Consist, p.Path, p.Train, p.Train.Number, p.AvatarUrl);
                    tmp += player.ToString();
                }
            }
            return tmp;
        }

        public void AddPlayers(MSGPlayer player)
        {
            if (Players.ContainsKey(player.user))
                return;
            if (player.user == MultiPlayerManager.Instance().UserName)
                return; //do not add self//WARNING: may need to worry about train number here
            OnlinePlayer p = new OnlinePlayer(player.user,
                Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.ConsistsFolder, player.con),
                Path.Combine(Simulator.Instance.RouteFolder.PathsFolder, player.path));
            p.AvatarUrl = player.url;
            p.LeadingLocomotiveID = player.leadingID;
            Train train = new Train();
            train.TrainType = TrainType.Remote;
            if (MultiPlayerManager.IsServer()) //server needs to worry about correct train number
            {
            }
            else
            {
                train.Number = player.num;
            }
            if (player.con.Contains("tilted", StringComparison.OrdinalIgnoreCase))
                train.IsTilting = true;
            int direction = player.dir;
            train.DistanceTravelled = player.Travelled;
            train.TrainMaxSpeedMpS = player.trainmaxspeed;

            if (MultiPlayerManager.IsServer())
            {
                try
                {
                    AIPath aiPath = new AIPath(p.Path, Simulator.Instance.TimetableMode);
                }
                catch (Exception) { MultiPlayerManager.BroadCast((new MSGMessage(player.user, "Warning", "Server does not have path file provided, signals may always be red for you.")).ToString()); }
            }

            try
            {
                train.RearTDBTraveller = new Traveller(player.Location, direction == 1 ? Direction.Forward : Direction.Backward);
            }
            catch (Exception e) when (MultiPlayerManager.IsServer())
            {
                MultiPlayerManager.BroadCast((new MSGMessage(player.user, "Error", "MultiPlayer Error：" + e.Message)).ToString());
            }
            string[] faDiscreteSplit;
            List<LoadData> loadDataList = new List<LoadData>();
            for (var i = 0; i < player.cars.Length; i++)// cars.Length-1; i >= 0; i--) {
            {

                string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, player.cars[i]);
                TrainCar car;
                try
                {
                    car = RollingStock.Load(train, wagonFilePath);
                    car.CarID = player.ids[i];
                    car.CarLengthM = player.lengths[i] / 100.0f;
                    if (player.faDiscretes[i][0] != '0')
                    {
                        int numDiscretes = player.faDiscretes[i][0];
                        // There are discrete freight animations, add them to wagon
                        faDiscreteSplit = player.faDiscretes[i].Split('&');
                        loadDataList.Clear();
                        for (int j = 1; j < faDiscreteSplit.Length; j++)
                        {
                            var faDiscrete = faDiscreteSplit[j];
                            string[] loadDataItems = faDiscrete.Split('%');
                            EnumExtension.GetValue(loadDataItems[2], out LoadPosition loadPosition);
                            LoadData loadData = new LoadData(loadDataItems[0], loadDataItems[1], loadPosition);
                            loadDataList.Add(loadData);
                        }
                        car.FreightAnimations?.Load(loadDataList);
                    }
                }
                catch (Exception error)
                {
                    Trace.WriteLine(error.Message);
                    car = MultiPlayerManager.Instance().SubCar(train, wagonFilePath, player.lengths[i]);
                }
                if (car == null)
                    continue;

                car.Flipped = player.flipped[i] != 0;

                if (car is MSTSWagon w)
                {
                    w.SignalEvent((player.pantofirst == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 1);
                    w.SignalEvent((player.pantosecond == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 2);
                    w.SignalEvent((player.pantothird == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 3);
                    w.SignalEvent((player.pantofourth == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 4);
                }
            }

            if (train.Cars.Count == 0)
            {
                throw (new Exception("The train of player " + player.user + " is empty from "));
            }

            train.ControlMode = TrainControlMode.Explorer;
            train.CheckFreight();
            train.InitializeBrakes();
            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTrainPosition();
            if (tempRoute.Count == 0)
            {
                MultiPlayerManager.BroadCast((new MSGMessage(p.Username, "Error", "Cannot be placed into the game")).ToString());//server will broadcast this error
                throw new InvalidDataException("Remote train original position not clear");
            }

            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars();
            train.ResetInitialTrainRoute(tempRoute);

            train.CalculatePositionOfCars();
            train.AITrainBrakePercent = 100;

            //if (MPManager.Instance().AllowedManualSwitch) train.InitializeSignals(false);
            for (int iCar = 0; iCar < train.Cars.Count; iCar++)
            {
                var car = train.Cars[iCar];
                if (car.CarID == p.LeadingLocomotiveID)
                {
                    train.LeadLocomotive = car as MSTSLocomotive;
                    train.LeadLocomotive.Headlight = player.headlight;
                    train.LeadLocomotive.UsingRearCab = player.frontorrearcab == "R" ? true : false;
                }
                if (car is MSTSLocomotive && MultiPlayerManager.IsServer())
                    MultiPlayerManager.Instance().AddOrRemoveLocomotive(player.user, train.Number, iCar, true);
            }
            if (train.LeadLocomotive == null)
            {
                train.LeadNextLocomotive();
                p.LeadingLocomotiveID = train.LeadLocomotive?.CarID ?? "NA";
            }

            if (train.LeadLocomotive != null)
            {
                train.Name = Train.GetTrainName(train.LeadLocomotive.CarID);
            }
            else if (train.Cars != null && train.Cars.Count > 0)
            {
                train.Name = Train.GetTrainName(train.Cars[0].CarID);
            }
            else if (player?.user != null)
            {
                train.Name = player.user;
            }

            if (MultiPlayerManager.IsServer())
            {
                train.InitializeSignals(false);
            }
            p.Train = train;

            Players.Add(player.user, p);
            MultiPlayerManager.Instance().AddOrRemoveTrain(train, true);

        }

        public void SwitchPlayerTrain(MSGPlayerTrainSw player)
        {
            // find info about the new player train
            // look in all trains

            if (player.user == MultiPlayerManager.Instance().UserName)
                return; //do not add self//WARNING: may need to worry about train number here
            OnlinePlayer p;
            var doesPlayerExist = Players.TryGetValue(player.user, out p);
            if (!doesPlayerExist)
                return;
            if (player.oldTrainReverseFormation)
                p.Train.ReverseFormation(false);
            p.LeadingLocomotiveID = player.leadingID;
            Train train;

            if (MultiPlayerManager.IsServer()) //server needs to worry about correct train number
            {
                train = Simulator.Instance.Trains.Find(t => t.Number == player.num);
                train.TrainType = TrainType.Remote;
            }
            else
            {
                train = Simulator.Instance.Trains.Find(t => t.Number == player.num);
                train.TrainType = TrainType.Remote;
            }
            p.Train = train;
            if (player.newTrainReverseFormation)
                p.Train.ReverseFormation(false);
        }

        public string ExhaustingLocos(MSGExhaust exhaust)
        {
            string tmp = "";
            if (exhaust == null)
                exhaust = new MSGExhaust();
            foreach (OnlineLocomotive l in OnlineLocomotives)
            {
                if (l.userName != MultiPlayerManager.GetUserName())
                {
                    Train t = MultiPlayerManager.FindPlayerTrain(l.userName);
                    if (t != null && l.trainCarPosition < t.Cars.Count && (Math.Abs(t.SpeedMpS) > 0.001 || Math.Abs(t.LastReportedSpeed) > 0))
                    {
                        if (t.Cars[l.trainCarPosition] is MSTSDieselLocomotive)
                        {
                            exhaust.AddNewItem(l.userName, t, l.trainCarPosition);
                        }
                    }
                }
            }
            tmp += exhaust.ToString();
            return tmp;
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
