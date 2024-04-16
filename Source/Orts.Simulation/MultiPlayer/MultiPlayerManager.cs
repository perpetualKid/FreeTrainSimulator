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

/* MPManager
 * 
 * Contains code to manager multiplayer sessions, especially to hide server/client mode from other code.
 * For example, the Notify method will check if it is server (then broadcast) or client (then send msg to server)
 * but the caller does not need to care.
 * 
 * 
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GetText;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Formats.Msts.Parsers;
using Orts.Simulation.Commanding;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer
{
    public enum MultiplayerState
    {
        None,
        Client,
        Dispatcher,
    }

    //a singleton class handles communication, update and stop etc.
    public class MultiPlayerManager : IDisposable
    {
        private double lastMoveTime;

        private string metric = "";
        private static MultiPlayerManager localUser;

        private readonly List<Train> addedTrains;
        private readonly List<OnlineLocomotive> addedLocomotives;

        private readonly List<Train> uncoupledTrains;
        public MultiPlayerClient MultiPlayerClient { get; private set; }

        public const int ProtocolVersion = 24;

        public static ICatalog Catalog { get; private set; } = CatalogManager.Catalog;

        public bool Connected { get => MultiPlayerClient?.Connected ?? false; set => MultiPlayerClient.Connected = value; }
        public bool IsDispatcher { get; set; }

        public static OnlineTrains OnlineTrains = new OnlineTrains();
        public double lastSwitchTime;
        public double lastSyncTime;
        public List<Train> removedTrains;
        public List<OnlineLocomotive> removedLocomotives;

        public double ServerTimeDifference { get; internal set; }

        public double lastPlayerAddedTime;
        public int MPUpdateInterval { get; } = 10;
        public bool AllowedManualSwitch = true;
        public bool TrySwitch = true;
        public string lastSender = ""; //who last sends me a message
        public bool AmAider; //am I aiding the dispatcher?
        public List<string> aiderList;
        public Dictionary<string, OnlinePlayer> lostPlayer = new Dictionary<string, OnlinePlayer>();
        public bool CheckSpad = true;
        public bool PreferGreen = true;
        public string MD5Check { get; } = HashRouteFile();

        public string UserName { get; private set; } = string.Empty;
        public string Code { get; private set; }

        public void AddUncoupledTrains(Train t)
        {
            lock (uncoupledTrains)
            {
                uncoupledTrains.Add(t);
            }
        }

        public void RemoveUncoupledTrains(Train t)
        {
            lock (uncoupledTrains)
            {
                uncoupledTrains.Remove(t);
            }
        }

        public string OriginalSwitchState = "";

        public void RememberOriginalSwitchState()
        {
            MSGSwitchStatus msg = new MSGSwitchStatus();
            var str = msg.ToString();
            var index = str.IndexOf("SWITCHSTATES ");
            OriginalSwitchState = str.Remove(0, index + 13);
        }

        //handles singleton
        private MultiPlayerManager()
        {
            playersRemoved = new List<OnlinePlayer>();
            uncoupledTrains = new List<Train>();
            addedTrains = new List<Train>();
            removedTrains = new List<Train>();
            addedLocomotives = new List<OnlineLocomotive>();
            removedLocomotives = new List<OnlineLocomotive>();
            aiderList = new List<string>();
            users = new SortedList<double, string>();
        }

        public static MultiPlayerManager Instance()
        {
            if (localUser == null)
            {
                Catalog = CatalogManager.Catalog;
                localUser = new MultiPlayerManager();
            }
            return localUser;
        }

        public static void RequestControl()
        {
            try
            {
                Train train = Simulator.Instance.PlayerLocomotive.Train;

                MSGControl msgctl;
                //I am the server, I have control
                if (IsServer())
                {
                    train.TrainType = TrainType.Player;
                    train.LeadLocomotive = Simulator.Instance.PlayerLocomotive;
                    InitializeBrakesCommand.Receiver = Simulator.Instance.PlayerLocomotive.Train;
                    train.InitializeSignals(false);
                    Simulator.Instance.Confirmer?.Information(Catalog.GetString("You gained back the control of your train"));
                    msgctl = new MSGControl(GetUserName(), "Confirm", train);
                    BroadCast(msgctl.ToString());
                }
                else //client, send request
                {
                    msgctl = new MSGControl(GetUserName(), "Request", train);
                    BroadCast(msgctl.ToString());
                }
            }
            catch (Exception)
            { }
        }

        private double previousSpeed;
        private double begineZeroTime;

        /// <summary>
        /// Update. Determines what messages to send every some seconds
        /// 1. every one second will send train location
        /// 2. by default, every 10 seconds will send switch/signal status, this can be changed by in the menu of setting MPUpdateInterval
        /// 3. housekeeping (remove/add trains, remove players)
        /// 4. it will also capture key stroke of horn, panto, wiper, bell, headlight etc.
        /// </summary>
        public void Update(in ElapsedTime elapsedTime)
        {
            double newtime = Simulator.Instance.GameTime;
            if (begineZeroTime == 0)
                begineZeroTime = newtime - 10;

            CheckPlayerTrainSpad();//over speed or pass a red light

            //server update train location of all
            if (IsDispatcher && newtime - lastMoveTime >= 1f)
            {
                MSGMove move = new MSGMove();
                if (Simulator.Instance.PlayerLocomotive.Train.TrainType != TrainType.Remote)
                    move.AddNewItem(GetUserName(), Simulator.Instance.PlayerLocomotive.Train);
                BroadCast(OnlineTrains.MoveTrains(move));
                MSGExhaust exhaust = new MSGExhaust(); // Also updating loco exhaust
                Train t = Simulator.Instance.PlayerLocomotive.Train;
                for (int iCar = 0; iCar < t.Cars.Count; iCar++)
                {
                    if (t.Cars[iCar] is MSTSDieselLocomotive)
                    {
                        exhaust.AddNewItem(GetUserName(), t, iCar);
                    }
                }
                // Broadcast also exhaust
                var exhaustMessage = OnlineTrains.ExhaustingLocos(exhaust);
                if (!string.IsNullOrEmpty(exhaustMessage))
                    BroadCast(exhaustMessage);

                lastMoveTime = newtime;
            }

            //server updates switch
            if (IsDispatcher && newtime - lastSwitchTime >= MPUpdateInterval)
            {
                lastSwitchTime = newtime;
                var switchStatus = new MSGSwitchStatus();

                if (switchStatus.OKtoSend)
                    BroadCast(switchStatus.ToString());
                var signalStatus = new MSGSignalStatus();
                if (signalStatus.OKtoSend)
                    BroadCast(signalStatus.ToString());

            }

            //client updates itself
            if (MultiPlayerClient != null && !IsDispatcher && newtime - lastMoveTime >= 1f)
            {
                Train t = Simulator.Instance.PlayerLocomotive.Train;
                MSGMove move = new MSGMove();
                MSGExhaust exhaust = new MSGExhaust(); // Also updating loco exhaust
                //if I am still controlling the train
                if (t.TrainType != TrainType.Remote)
                {
                    if (Math.Abs(t.SpeedMpS) > 0.001 || newtime - begineZeroTime < 5f || Math.Abs(t.LastReportedSpeed) > 0)
                    {
                        move.AddNewItem(GetUserName(), t);
                        for (int iCar = 0; iCar < t.Cars.Count; iCar++)
                        {
                            if (t.Cars[iCar] is MSTSDieselLocomotive)
                            {
                                exhaust.AddNewItem(GetUserName(), t, iCar);
                            }
                        }
                    }
                    else
                    {
                        if (Math.Abs(previousSpeed) > 0.001)
                        {
                            begineZeroTime = newtime;
                        }
                    }

                }
                //MoveUncoupledTrains(move); //if there are uncoupled trains
                //if there are messages to send
                if (move.OKtoSend())
                {
                    MultiPlayerClient.SendLegacyMessage(move.ToString());
                    if (exhaust.OKtoSend())
                        MultiPlayerClient.SendLegacyMessage(exhaust.ToString());
                    lastMoveTime = newtime;
                }
                previousSpeed = t.SpeedMpS;
            }

            //some players are removed
            //need to send a keep-alive message if have not sent one to the server for the last 30 seconds
            if (IsDispatcher && newtime - lastSyncTime >= 60f)
            {
                MultiPlayerClient.SendMessage(new TimeCheckMessage() { DispatcherTime = Simulator.Instance.ClockTime });
                lastSyncTime = newtime;
            }
            RemovePlayer();

            //some players are disconnected more than 1 minute ago, will not care if they come back later
            CleanLostPlayers();

            //some trains are added/removed
            HandleTrainList();

            //some locos are added/removed
            if (IsDispatcher)
                HandleLocoList();

            AddPlayer(); //a new player joined? handle it

            /* will have this in the future so that helpers can also control
			//I am a helper, will see if I need to update throttle and dynamic brake
			if (Simulator.PlayerLocomotive.Train != null && Simulator.PlayerLocomotive.Train.TrainType == Train.TRAINTYPE.REMOTE) 
			{

			}
			 * */

            // process incoming messages
            MultiPlayerClient.Update(elapsedTime);
        }

        private void CheckPlayerTrainSpad()
        {
            if (CheckSpad == false)
                return;
            var Locomotive = (MSTSLocomotive)Simulator.Instance.PlayerLocomotive;
            if (Locomotive == null)
                return;
            var train = Locomotive.Train;
            if (train == null || train.TrainType == TrainType.Remote)
                return;//no train or is remotely controlled

            //var spad = false;
            var maxSpeed = Math.Abs(train.AllowedMaxSpeedMpS) + 3;//allow some margin of error (about 10km/h)
            var speed = Math.Abs(Locomotive.SpeedMpS);
            //if (speed > maxSpeed) spad = true;
            //if (train.TMaspect == ORTS.Popups.TrackMonitorSignalAspect.Stop && Math.Abs(train.distanceToSignal) < 2*speed && speed > 5) spad = true; //red light and cannot stop within 2 seconds, if the speed is large

        }
        //check if it is in the server mode
        public static bool IsServer()
        {
            return localUser?.IsDispatcher ?? false;
        }

        //check if it is in the server mode && they are players && not allow autoswitch
        public static bool NoAutoSwitch()
        {
            if (!MultiPlayerManager.IsMultiPlayer() || MultiPlayerManager.IsServer())
                return false;

            return !MultiPlayerManager.Instance().AllowedManualSwitch; //allow manual switch or not
        }

        //user name
        public static string GetUserName()
        {
            return localUser?.UserName ?? string.Empty;
        }

        //check if it is in the multiplayer session
        public static bool IsMultiPlayer()
        {
            return (localUser?.MultiPlayerClient != null);
        }

        public static MultiplayerState MultiplayerState => (localUser?.IsDispatcher ?? false) ? MultiplayerState.Dispatcher : (localUser?.MultiPlayerClient != null) ? MultiplayerState.Client : MultiplayerState.None;

        public static void BroadCast(string m)
        {
            if (m == null || localUser?.MultiPlayerClient == null)
                return;
            if (MultiplayerState == MultiplayerState.Dispatcher)
                localUser.MultiPlayerClient.SendLegacyMessage(m);
        }

        //notify others (server will broadcast, client will send msg to server)
        public static void Notify(string m)
        {
            if (m == null || localUser?.MultiPlayerClient == null)
                return;
            //client notify server
            localUser.MultiPlayerClient.SendLegacyMessage(m);
        }

        public static void Broadcast(MultiPlayerMessageContent message)
        {
            localUser?.MultiPlayerClient?.SendMessage(message);
        }

        //nicely shutdown listening threads, and notify the server/other player
        public static void Stop()
        {
            localUser?.Quit();
        }

        public static void Start(string hostname, int port, string userName, string code)
        {
            if (localUser == null)
            {
                localUser = new MultiPlayerManager
                {
                    UserName = userName,
                    Code = code,
                    MultiPlayerClient = new MultiPlayerClient(),
                };
                MultiPlayerMessageContent.SetMultiPlayerManager(localUser);
                if (!Instance().MultiPlayerClient.Connect(hostname, port))
                {
                    localUser.MultiPlayerClient = null;
                    localUser = null;
                }
            }
        }

        private void Quit()
        {
            if (MultiPlayerClient != null)
            {
                MultiPlayerClient.SendMessage(new QuitMessage() { User = UserName });   //client notify server
                MultiPlayerClient.Stop();
            }
            MultiPlayerClient = null;
        }

        //when two player trains connected, require decouple at speed 0.
        public static bool TrainOK2Decouple(Confirmer confirmer, Train t)
        {
            if (t == null)
                return false;
            if (Math.Abs(t.SpeedMpS) < 0.001)
                return true;
            try
            {
                var count = 0;
                foreach (var p in OnlineTrains.Players.Keys)
                {
                    string p1 = p + " ";
                    foreach (var car in t.Cars)
                    {
                        if (car.CarID.Contains(p1))
                            count++;
                    }
                }
                if (count >= 2)
                {
                    if (confirmer != null)
                        confirmer.Information(MultiPlayerManager.Catalog.GetPluralString("Cannot decouple: train has {0} player, need to completely stop.", "Cannot decouple: train has {0} players, need to completely stop.", count));
                    return false;
                }
            }
            catch { return false; }
            return true;
        }

        public bool PlayerAdded;

        public async ValueTask SendMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            await MultiPlayerClient.SendMessageAsync(new ChatMessage(text)).ConfigureAwait(false);
        }

        public void Connect()
        {
            MultiPlayerClient.JoinGame(GetUserName(), Simulator.Instance.Route.Name, Code);
        }

        public void AddPlayer()
        {
            if (!IsServer())
                return;
            if (PlayerAdded == true)
            {
                PlayerAdded = false;
                Instance().lastPlayerAddedTime = Simulator.Instance.GameTime;
                Instance().lastSwitchTime = Simulator.Instance.GameTime;

                MSGPlayer host = new MSGPlayer(GetUserName(), "1234", Simulator.Instance.ConsistFileName, Simulator.Instance.PathFileName, Simulator.Instance.PlayerLocomotive.Train,
                    Simulator.Instance.PlayerLocomotive.Train.Number);
                BroadCast(host.ToString());
                foreach (MSGPlayer player in OnlineTrains.AllPlayerTrains())
                    BroadCast(player.ToString());
                //                BroadCast(host.ToString() + OnlineTrains.AddAllPlayerTrain());
                foreach (Train t in Simulator.Instance.Trains)
                {
                    if (Simulator.Instance.PlayerLocomotive != null && t == Simulator.Instance.PlayerLocomotive.Train)
                        continue; //avoid broadcast player train
                    if (FindPlayerTrain(t))
                        continue;
                    if (removedTrains.Contains(t))
                        continue;//this train is going to be removed, should avoid it.
                    BroadCast((new MSGTrain(t, t.Number)).ToString());
                }
                if (CheckSpad)
                {
                    Broadcast(new ControlMessage(ControlMessageType.NoOverspeed, "Penalty for overspeed and passing stop light"));
                }
                else
                {
                    Broadcast(new ControlMessage(ControlMessageType.OverspeedOK, "OK to go overspeed and pass stop light"));
                }
                Broadcast(new WeatherMessage(Simulator.Instance.Weather));
            }
        }

        //this will be used in the server, in Simulator.cs
        public static bool TrainOK2Couple(Simulator simulator, Train t1, Train t2)
        {
            //if (Math.Abs(t1.SpeedMpS) > 10 || Math.Abs(t2.SpeedMpS) > 10) return false; //we do not like high speed punch in MP, will mess up a lot.

            if (t1.TrainType != TrainType.Remote && t2.TrainType != TrainType.Remote)
                return true;

            bool result = true;
            try
            {
                foreach (var p in OnlineTrains.Players)
                {
                    if (p.Value.Train == t1 && simulator.GameTime - p.Value.CreatedTime < 120)
                    { result = false; break; }
                    if (p.Value.Train == t2 && simulator.GameTime - p.Value.CreatedTime < 120)
                    { result = false; break; }
                }
            }
            catch (Exception)
            {
            }
            return result;
        }

        /// <summary>
        /// Return a string of information of how many players online and those users who are close
        /// </summary>

        private readonly SortedList<double, string> users;

        public string GetMultiPlayerStatus()
        {
            return MultiplayerState != MultiplayerState.None
                ? MultiplayerState == MultiplayerState.Dispatcher
                ? $"{Catalog.GetString("Dispatcher")}"
                : AmAider || (Simulator.Instance.PlayerLocomotive.Train.TrainType == TrainType.Remote) ? Catalog.GetString("Helper")
                : $"{Catalog.GetString("Client")}"
                : $"{Catalog.GetString("Connection to the server lost")}";
        }

        public string GetOnlineUsersInfo()
        {

            StringBuilder info = new StringBuilder();
            if (Simulator.Instance.PlayerLocomotive.Train.TrainType == TrainType.Remote)
                info.Append("Your locomotive is a helper\t");
            info.Append(CultureInfo.InvariantCulture, $"{OnlineTrains.Players.Count + 1}{(OnlineTrains.Players.Count <= 0 ? " player " : "  players ")}");
            info.Append(CultureInfo.InvariantCulture, $"{Simulator.Instance.Trains.Count}{(Simulator.Instance.Trains.Count <= 1 ? " train" : "  trains")}");
            TrainCar mine = Simulator.Instance.PlayerLocomotive;
            users.Clear();
            try//the list of players may be changed during the following process
            {
                //foreach (var train in Simulator.Trains) info += "\t" + train.Number + " " + train.Cars.Count;
                //info += "\t" + MPManager.OnlineTrains.Players.Count;
                //foreach (var p in MPManager.OnlineTrains.Players) info += "\t" + p.Value.Train.Number + " " + p.Key;
                foreach (OnlinePlayer p in OnlineTrains.Players.Values)
                {
                    if (p.Train == null)
                        continue;
                    if (p.Train.Cars.Count <= 0)
                        continue;
                    double d = WorldLocation.GetDistanceSquared(p.Train.RearTDBTraveller.WorldLocation, mine.Train.RearTDBTraveller.WorldLocation);
                    users.Add(Math.Sqrt(d) + StaticRandom.NextDouble(), p.Username);
                }
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrEmpty(metric))
            {
                metric = Simulator.Instance.Route.MilepostUnitsMetric ? " m" : " yd";
            }

            foreach (KeyValuePair<double, string> pair in users.Take(10))
            {
                info.Append(CultureInfo.InvariantCulture, $"\t{pair.Value}: distance of {(int)(Simulator.Instance.Route.MilepostUnitsMetric ? pair.Key : Size.Length.ToYd(pair.Key)) + metric}");
            }
            if (OnlineTrains.Players.Count > 10)
            {
                info.Append("\t ...");
            }
            return info.ToString();
        }

        private List<OnlinePlayer> playersRemoved;
        public void AddRemovedPlayer(OnlinePlayer p)
        {
            lock (playersRemoved)
            {
                if (playersRemoved.Contains(p))
                    return;
                playersRemoved.Add(p);
            }
        }

        private void CleanLostPlayers()
        {
            //check if any of the lost player list has been lost for more than 600 seconds. If so, remove it and will not worry about it anymore
            if (lostPlayer.Count > 0)
            {
                List<string> removeLost = null;
                foreach (var x in lostPlayer)
                {
                    if (Simulator.Instance.GameTime - x.Value.QuitTime > 600) //within 10 minutes it will be held
                    {
                        if (removeLost == null)
                            removeLost = new List<string>();
                        removeLost.Add(x.Key);
                    }
                }
                if (removeLost != null)
                {
                    foreach (var name in removeLost)
                    {
                        lostPlayer.Remove(name);
                    }
                }
            }
        }
        //only can be called by Update
        private void RemovePlayer()
        {
            //if (Server == null) return; //client will do it by decoding message
            if (playersRemoved.Count == 0)
                return;

            try //do it with lock, but may still have exception
            {
                lock (playersRemoved)
                {
                    foreach (OnlinePlayer p in playersRemoved)
                    {
                        //player is not in this train
                        if (p.Train != null && p.Train != Simulator.Instance.PlayerLocomotive.Train)
                        {
                            //make sure this train has no other player on it
                            bool hasOtherPlayer = false;
                            foreach (var p1 in OnlineTrains.Players)
                            {
                                if (p == p1.Value)
                                    continue;
                                if (p1.Value.Train == p.Train)
                                { hasOtherPlayer = true; break; }//other player has the same train
                            }
                            if (hasOtherPlayer == false)
                            {
                                AddOrRemoveLocomotives(p.Username, p.Train, false);
                                if (p.Train.Cars.Count > 0)
                                {
                                    foreach (TrainCar car in p.Train.Cars)
                                    {
                                        car.Train = null; // WorldPosition.XNAMatrix.M42 -= 1000;
                                        car.IsPartOfActiveTrain = false;  // to stop sounds
                                                                          // remove containers if any
                                        if (car.FreightAnimations?.Animations != null)
                                            car.FreightAnimations?.HideDiscreteFreightAnimations();
                                    }
                                }
                                p.Train.RemoveFromTrack();
                                Simulator.Instance.Trains.Remove(p.Train);
                            }
                        }
                        OnlineTrains.Players.Remove(p.Username);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e + e.StackTrace);
                return;
            }
            playersRemoved.Clear();

        }

        public bool AddOrRemoveTrain(Train t, bool add)
        {
            if (add)
            {
                lock (addedTrains)
                {
                    foreach (var t1 in addedTrains)
                    {
                        if (t1.Number == t.Number)
                            return false;
                    }
                    addedTrains.Add(t);
                    return true;
                }
            }
            else
            {
                lock (removedTrains)
                {
                    removedTrains.Add(t);
                    return true;
                }
            }
        }

        public bool AddOrRemoveLocomotive(string userName, int tNumber, int trainCarPosition, bool add)
        {
            if (add)
            {
                lock (addedLocomotives)
                {
                    foreach (var l1 in addedLocomotives)
                    {
                        if (l1.trainNumber == tNumber && l1.trainCarPosition == trainCarPosition)
                            return false;
                    }
                    OnlineLocomotive newLoco;
                    newLoco.userName = userName;
                    newLoco.trainNumber = tNumber;
                    newLoco.trainCarPosition = trainCarPosition;
                    addedLocomotives.Add(newLoco);
                    return true;
                }
            }
            else
            {
                lock (removedLocomotives)
                {
                    OnlineLocomotive removeLoco;
                    removeLoco.userName = userName;
                    removeLoco.trainNumber = tNumber;
                    removeLoco.trainCarPosition = trainCarPosition;
                    removedLocomotives.Add(removeLoco);
                    return true;
                }
            }
        }

        public bool AddOrRemoveLocomotives(string userName, Train t, bool add)
        {
            for (int iCar = 0; iCar < t.Cars.Count; iCar++)
            {
                if (t.Cars[iCar] is MSTSLocomotive)
                {
                    AddOrRemoveLocomotive(userName, t.Number, iCar, add);
                }

            }
            return true;
        }

        //only can be called by Update
        private void HandleTrainList()
        {
            if (addedTrains.Count != 0)
            {

                try //do it without lock, so may have exception
                {
                    foreach (var t in addedTrains)
                    {
                        var hasIt = false;
                        foreach (var t1 in Simulator.Instance.Trains)
                        {
                            if (t1.Number == t.Number)
                            { hasIt = true; break; }
                        }
                        if (!hasIt)
                            Simulator.Instance.Trains.Add(t);
                    }
                    addedTrains.Clear();
                }
                catch (Exception) { }
            }
            if (removedTrains.Count != 0)
            {

                try //do it without lock, so may have exception
                {
                    foreach (var t in removedTrains)
                    {
                        t.RemoveFromTrack();
                        Simulator.Instance.Trains.Remove(t);
                    }
                    removedTrains.Clear();
                }
                catch (Exception) { }
            }
        }

        //only can be called by Update
        private void HandleLocoList()
        {
            if (removedLocomotives.Count != 0)
            {

                try //do it without lock, so may have exception
                {
                    foreach (var l in removedLocomotives)
                    {
                        for (int index = 0; index < OnlineTrains.OnlineLocomotives.Count; index++)
                        {
                            var thisOnlineLocomotive = OnlineTrains.OnlineLocomotives[index];
                            if (l.trainNumber == thisOnlineLocomotive.trainNumber && l.trainCarPosition == thisOnlineLocomotive.trainCarPosition)
                            {
                                OnlineTrains.OnlineLocomotives.RemoveAt(index);
                                break;
                            }
                        }
                    }
                    removedLocomotives.Clear();
                }
                catch (Exception) { }
            }
            if (addedLocomotives.Count != 0)
            {

                try //do it without lock, so may have exception
                {
                    foreach (var l in addedLocomotives)
                    {
                        var hasIt = false;
                        foreach (var l1 in OnlineTrains.OnlineLocomotives)
                        {
                            if (l1.trainNumber == l.trainNumber && l1.trainCarPosition == l.trainCarPosition)
                            { hasIt = true; break; }
                        }
                        if (!hasIt)
                            OnlineTrains.OnlineLocomotives.Add(l);
                    }
                    addedLocomotives.Clear();
                }
                catch (Exception) { }
            }
        }

        public static Train FindPlayerTrain(string user)
        {
            return OnlineTrains.FindTrain(user);
        }

        public static bool FindPlayerTrain(Train t)
        {
            return OnlineTrains.FindTrain(t);
        }

        public static void LocoChange(Train t, TrainCar lead)
        {
            var frontOrRearCab = (lead as MSTSLocomotive).UsingRearCab ? "R" : "F";
            Notify((new MSGLocoChange(GetUserName(), lead.CarID, frontOrRearCab, t)).ToString());
        }

        public TrainCar SubCar(Train train, string wagonFilePath, int length)
        {
            Trace.WriteLine("Will substitute with your existing stocks\n.");
            try
            {
                char type = 'w';
                if (wagonFilePath.Contains(".eng", StringComparison.OrdinalIgnoreCase))
                    type = 'e';
                string newWagonFilePath = SubMissingCar(length, type);
                TrainCar car = RollingStock.Load(train, newWagonFilePath);
                car.CarLengthM = length;
                car.RealWagFilePath = wagonFilePath;
                Simulator.Instance.Confirmer?.Information(Catalog.GetString("Missing car, have substituted with other one."));
                return car;
            }
            catch (Exception error)
            {
                Trace.WriteLine(error.Message + "Substitution failed, will ignore it\n.");
                return null;
            }
        }

        private SortedList<double, string> coachList;
        private SortedList<double, string> engList;
        private bool disposedValue;

        public string SubMissingCar(int length, char type)
        {

            type = char.ToLowerInvariant(type);
            SortedList<double, string> copyList;
            if (type == 'w')
            {
                if (coachList == null)
                    coachList = GetList(Simulator.Instance, type);
                copyList = coachList;
            }
            else
            {
                if (engList == null)
                    engList = GetList(Simulator.Instance, type);
                copyList = engList;
            }
            string bestName = "Default\\default.wag";
            double bestDist = 1000;

            foreach (var item in copyList)
            {
                var dist = Math.Abs(item.Key - length);
                if (dist < bestDist)
                { bestDist = dist; bestName = item.Value; }
            }
            return Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, bestName);

        }

        private static SortedList<double, string> GetList(Simulator simulator, char type)
        {
            string ending = "*.eng";
            if (type == 'w')
                ending = "*.wag";
            string[] filePaths = Directory.GetFiles(simulator.RouteFolder.ContentFolder.TrainSetsFolder, ending, SearchOption.AllDirectories);
            string temp;
            List<string> allEngines = new List<string>();
            SortedList<double, string> carList = new SortedList<double, string>();
            for (var i = 0; i < filePaths.Length; i++)
            {
                int index = filePaths[i].LastIndexOf("\\trains\\trainset\\");
                temp = filePaths[i].Substring(index + 17);
                if (!temp.Contains('\\'))
                    continue;
                allEngines.Add(temp);
            }
            foreach (string name in allEngines)
            {
                double len = 0.0f;
                Microsoft.Xna.Framework.Vector3 def = new Microsoft.Xna.Framework.Vector3();

                try
                {
                    using (var stf = new STFReader(Path.Combine(simulator.RouteFolder.ContentFolder.TrainSetsFolder, name), false))
                        stf.ParseFile(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("wagon", ()=>{
                                stf.ReadString();
                                stf.ParseBlock(new STFReader.TokenProcessor[] {
                                    new STFReader.TokenProcessor("size", ()=>{ def = stf.ReadVector3Block(STFReader.Units.Distance, def); }),
                                });
                            }),
                        });

                    len = def.Z;
                    carList.Add(len + StaticRandom.NextDouble() / 10.0, name);
                }
                catch { }
            }
            return carList;
        }

        private static string HashRouteFile()
        {
            try
            {
                string fileName = Simulator.Instance.RouteFolder.TrackDatabaseFile(Simulator.Instance.Route.FileName);
                using (FileStream file = new FileStream(fileName, FileMode.Open))
                {
                    XxHash64 hashing = new XxHash64();
                    hashing.Append(file);

                    return Convert.ToBase64String(hashing.GetCurrentHash());
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("{0} Cannot get hash of TDB file, use NA instead but server may not connect you.", e.Message);
                return "NA";
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    MultiPlayerClient?.Dispose();
                    // TODO: dispose managed state (managed objects)
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
