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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;
using Orts.Simulation.World;

namespace Orts.Simulation.Multiplayer
{
    public abstract class Message
    {
        private protected static readonly Encoding messageEncoding = Encoding.UTF8;
        private protected const int maxSizeDigits = 6;
        private protected const string separator = ": ";

        public static Message Decode(ReadOnlySpan<byte> messageType, ReadOnlySpan<byte> content)
        {
            return messageEncoding.GetString(messageType) switch
            {
                "MOVE" => new MSGMove(messageEncoding.GetString(content)),
                "SWITCHSTATES" => new MSGSwitchStatus(messageEncoding.GetString(content)),
                "SIGNALSTATES" => new MSGSignalStatus(messageEncoding.GetString(content)),
                "LOCOINFO" => new MSGLocoInfo(messageEncoding.GetString(content)),
                "TRAIN" => new MSGTrain(messageEncoding.GetString(content)),
                "PLAYER" => new MSGPlayer(messageEncoding.GetString(content)),
                "PLAYERTRAINSW" => new MSGPlayerTrainSw(messageEncoding.GetString(content)),
                "ORGSWITCH" => new MSGOrgSwitch(messageEncoding.GetString(content)),
                "SWITCH" => new MSGSwitch(messageEncoding.GetString(content)),
                "UNCOUPLE" => new MSGUncouple(messageEncoding.GetString(content)),
                "COUPLE" => new MSGCouple(messageEncoding.GetString(content)),
                "GETTRAIN" => new MSGGetTrain(messageEncoding.GetString(content)),
                "UPDATETRAIN" => new MSGUpdateTrain(messageEncoding.GetString(content)),
                "LOCCHANGE" => new MSGLocoChange(messageEncoding.GetString(content)),
                "SIGNALCHANGE" => new MSGSignalChange(messageEncoding.GetString(content)),
                "FLIP" => new MSGFlip(messageEncoding.GetString(content)),
                "MOVINGTBL" => new MSGMovingTable(messageEncoding.GetString(content)),
                _ => throw new ProtocolException($"Unknown Message type {messageEncoding.GetString(messageType)}"),
            };
        }

        public abstract void HandleMsg();

        public virtual int EstimatedMessageSize => 0;

        protected static int TranslateMidpointDirection(MidpointDirection direction)
        {
            return direction == MidpointDirection.N ? 2 : direction == MidpointDirection.Forward ? 0 : 1;
        }

        protected static MidpointDirection TranslateMidpointDirection(int direction)
        {
            return direction == 0 ? MidpointDirection.Forward : direction == 1 ? MidpointDirection.Reverse : MidpointDirection.N;
        }

    }

    #region MSGMove
    public class MSGMove : Message
    {
        private class MSGMoveItem
        {
            public string user;
            public float speed;
            public float travelled;
            public int num, count;
            public int TileX, TileZ, direction, tdbDir;
            public int trackNodeIndex;
            public float X, Z;
            public float Length;
            public MSGMoveItem(string u, float s, float t, int n, int tX, int tZ, float x, float z, int tni, int cnt, int dir, int tDir, float len)
            {
                user = u;
                speed = s;
                travelled = t;
                num = n;
                TileX = tX;
                TileZ = tZ;
                X = x;
                Z = z;
                trackNodeIndex = tni;
                count = cnt;
                direction = dir;
                tdbDir = tDir;
                Length = len;
            }
            public override string ToString()
            {
                return user + " " + speed.ToString(CultureInfo.InvariantCulture) + " " + travelled.ToString(CultureInfo.InvariantCulture) + " " + num + " " +
                    TileX + " " + TileZ + " " + X.ToString(CultureInfo.InvariantCulture) + " " + Z.ToString(CultureInfo.InvariantCulture) + " " + trackNodeIndex + " " +
                    count + " " + direction + " " + tdbDir + " " + Length.ToString(CultureInfo.InvariantCulture);
            }
        }

        private List<MSGMoveItem> items;

        public MSGMove(string m)
        {
            m = m.Trim();
            string[] areas = m.Split(' ');
            if (areas.Length % 13 != 0 && !(areas.Length == 1 && areas[0].Length == 0)) //check for correct formatting
            {
                throw new ProtocolException("Parsing error " + m);
            }
            try
            {
                int i = 0;
                items = new List<MSGMoveItem>();
                if (areas.Length > 1)
                    for (i = 0; i < areas.Length / 13; i++)
                        items.Add(new MSGMoveItem(areas[13 * i], float.Parse(areas[13 * i + 1], CultureInfo.InvariantCulture), float.Parse(areas[13 * i + 2], CultureInfo.InvariantCulture), int.Parse(areas[13 * i + 3]),
                            int.Parse(areas[13 * i + 4]), int.Parse(areas[13 * i + 5]), float.Parse(areas[13 * i + 6], CultureInfo.InvariantCulture), float.Parse(areas[13 * i + 7], CultureInfo.InvariantCulture),
                            int.Parse(areas[13 * i + 8]), int.Parse(areas[13 * i + 9]), int.Parse(areas[13 * i + 10]), int.Parse(areas[13 * i + 11]), float.Parse(areas[13 * i + 12], CultureInfo.InvariantCulture)));
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static Dictionary<int, int> MissingTimes = new Dictionary<int, int>();

        //a train is missing, but will wait for 10 messages then ask
        private static bool CheckMissingTimes(int TNumber)
        {
            if (MissingTimes.TryGetValue(TNumber, out int value))
            {
                if (value < 10)
                {
                    MissingTimes[TNumber]++;
                    return false;
                }
                else
                {
                    MissingTimes[TNumber] = 0;
                    return true;
                }
            }
            else
            {
                MissingTimes.Add(TNumber, 1);
                return false;
            }

        }

        public MSGMove()
        {
        }

        public void AddNewItem(string u, Train t)
        {
            if (items == null)
                items = new List<MSGMoveItem>();
            items.Add(new MSGMoveItem(u, t.SpeedMpS, t.DistanceTravelled, t.Number, t.RearTDBTraveller.TileX, t.RearTDBTraveller.TileZ, t.RearTDBTraveller.X, t.RearTDBTraveller.Z, t.RearTDBTraveller.TrackNode.Index, t.Cars.Count, TranslateMidpointDirection(t.MUDirection), (int)t.RearTDBTraveller.Direction.Reverse(), t.Length));
            t.LastReportedSpeed = t.SpeedMpS;
        }

        public bool OKtoSend()
        {
            if (items != null && items.Count > 0)
                return true;
            return false;
        }
        public override string ToString()
        {
            string tmp = "MOVE ";
            if (items != null && items.Count > 0)
                for (var i = 0; i < items.Count; i++)
                    tmp += items[i].ToString() + " ";
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            foreach (MSGMoveItem m in items)
            {
                bool found = false; //a train may not be in my sim
                if (m.user == MultiPlayerManager.GetUserName())//about itself, check if the number of car has changed, otherwise ignore
                {
                    //if I am a remote controlled train now
                    if (Simulator.Instance.PlayerLocomotive.Train.TrainType == TrainType.Remote)
                    {
                        Simulator.Instance.PlayerLocomotive.Train.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, TranslateMidpointDirection(m.direction), m.tdbDir, m.Length);
                    }
                    found = true;/*
                    try
                    {
                        if (m.count != MPManager.Simulator.PlayerLocomotive.Train.Cars.Count)
                        {
                            if (!MPManager.IsServer() && CheckMissingTimes(MPManager.Simulator.PlayerLocomotive.Train.Number)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), MPManager.Simulator.PlayerLocomotive.Train.Number)).ToString());
                        }
                    }
                    catch (Exception) { }*/
                    continue;
                }
                if (m.user.Contains("0xAI") || m.user.Contains("0xUC"))
                {
                    foreach (Train t in Simulator.Instance.Trains)
                    {
                        if (t.Number == m.num)
                        {
                            found = true;
                            if (t.Cars.Count != m.count) //the number of cars are different, client will not update it, ask for new information
                            {
                                if (!MultiPlayerManager.IsServer())
                                {
                                    if (CheckMissingTimes(t.Number))
                                        MultiPlayerManager.Notify((new MSGGetTrain(MultiPlayerManager.GetUserName(), t.Number)).ToString());
                                    continue;
                                }
                            }
                            if (t.TrainType == TrainType.Remote)
                            {
                                var reverseTrav = false;
                                //                                 Alternate way to check for train flip
                                //                                if (m.user.Contains("0xAI") && m.trackNodeIndex == t.RearTDBTraveller.TrackNodeIndex && m.tdbDir != (int)t.RearTDBTraveller.Direction)
                                //                                {
                                //                                    reverseTrav = true;
                                //                                }
                                t.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, TranslateMidpointDirection(m.direction), m.tdbDir, m.Length, reverseTrav);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Train t = MultiPlayerManager.FindPlayerTrain(m.user);
                    if (t != null)
                    {
                        // skip the case where this train is merged with yours and you are the boss of that train
                        if (t.Number == Simulator.Instance.PlayerLocomotive.Train.Number &&
                            Simulator.Instance.PlayerLocomotive == Simulator.Instance.PlayerLocomotive.Train.LeadLocomotive &&
                            t.TrainType != TrainType.Remote && t.TrainType != TrainType.Static)
                            continue;
                        found = true;
                        t.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, TranslateMidpointDirection(m.direction), m.tdbDir, m.Length);
                        // This is necessary as sometimes a train isn't in the Trains list
                        MultiPlayerManager.Instance().AddOrRemoveTrain(t, true);
                        //                       if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(m.user, t, true);
                    }
                }
                if (found == false) //I do not have the train, tell server to send it to me
                {
                    if (!MultiPlayerManager.IsServer() && CheckMissingTimes(m.num))
                        MultiPlayerManager.Notify((new MSGGetTrain(MultiPlayerManager.GetUserName(), m.num)).ToString());
                }
            }
        }
    }
    #endregion MSGMove

    #region MSGRequired
    public abstract class MSGRequired : Message
    {

    }
    #endregion

    #region MSGPlayer
    public class MSGPlayer : MSGRequired
    {
        private readonly object lockObjPlayer = new object();
        private WorldLocation location;
        public ref readonly WorldLocation Location => ref location;
        public string user = "";
        public string code = "";
        public int num; //train number
        public string con; //consist
        public string path; //path consist and path will always be double quoted
        public string route;
        public int dir; //direction
        public float Travelled;
        public double seconds;
        public int season, weather;
        public int pantofirst, pantosecond, pantothird, pantofourth;
        public string frontorrearcab;
        public int headlight;
        public float trainmaxspeed;
        public string leadingID;
        public string[] cars;
        public string[] ids;
        public int[] flipped;
        public int[] lengths;
        public string[] faDiscretes;
        public string url;
        public int version;
        public string MD5 = "";
        public MSGPlayer() { }
        public MSGPlayer(string m)
        {
            string[] areas = m.Split('\r');
            if (areas.Length <= 6)
            {
                throw new ProtocolException("Parsing error in MSGPlayer" + m);
            }
            try
            {
                var tmp = areas[0].Trim();
                string[] data = tmp.Split(' ');
                user = data[0];
                code = data[1];
                num = int.Parse(data[2]);
                location = new WorldLocation(int.Parse(data[3]), int.Parse(data[4]), float.Parse(data[5], CultureInfo.InvariantCulture), 0, float.Parse(data[6], CultureInfo.InvariantCulture));
                Travelled = float.Parse(data[7], CultureInfo.InvariantCulture);
                trainmaxspeed = float.Parse(data[8], CultureInfo.InvariantCulture);
                seconds = double.Parse(data[9], CultureInfo.InvariantCulture);
                season = int.Parse(data[10]);
                weather = int.Parse(data[11]);
                pantofirst = int.Parse(data[12]);
                pantosecond = int.Parse(data[13]);
                pantothird = int.Parse(data[14]);
                pantofourth = int.Parse(data[15]);
                frontorrearcab = data[16];
                headlight = int.Parse(data[17]);
                //user = areas[0].Trim();
                con = areas[2].Trim();
                route = areas[3].Trim();
                path = areas[4].Trim();
                dir = int.Parse(areas[5].Trim());
                url = areas[6].Trim();
                ParseTrainCars(areas[7].Trim());
                leadingID = areas[1].Trim();
                int index = path.LastIndexOf("\\PATHS\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    path = path.Remove(0, index + 7);
                }
                index = con.LastIndexOf("\\CONSISTS\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    con = con.Remove(0, index + 10);
                }
                if (areas.Length >= 9)
                { version = int.Parse(areas[8]); }
                if (areas.Length >= 10)
                {
                    MD5 = areas[9];
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void ParseTrainCars(string m)
        {
            string[] areas = m.Split('\t');
            var numCars = areas.Length;
            cars = new string[numCars];//with an empty "" at end
            ids = new string[numCars];
            flipped = new int[numCars];
            lengths = new int[numCars];
            faDiscretes = new string[numCars];
            int index, last;
            for (var i = 0; i < numCars; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
                lengths[i] = int.Parse(carinfo[2]);
                faDiscretes[i] = carinfo[3];
            }

        }
        public MSGPlayer(string n, string cd, string c, string p, Train t, int tn)
        {
            route = Simulator.Instance.RouteFolder.RouteName;
            int index = p.LastIndexOf("\\PATHS\\", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                p = p.Remove(0, index + 7);
            }
            index = c.LastIndexOf("\\CONSISTS\\", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                c = c.Remove(0, index + 10);
            }
            user = n;
            code = cd;
            con = c;
            path = p;
            if (t != null)
            {
                dir = (int)t.RearTDBTraveller.Direction.Reverse();
                num = tn;
                location = t.RearTDBTraveller.WorldLocation;
                Travelled = t.DistanceTravelledM;
                trainmaxspeed = t.TrainMaxSpeedMpS;
            }
            seconds = (int)Simulator.Instance.ClockTime;
            season = (int)Simulator.Instance.Season;
            weather = (int)Simulator.Instance.WeatherType;
            pantofirst = pantosecond = pantothird = pantofourth = 0;
            MSTSWagon w = (MSTSWagon)Simulator.Instance.PlayerLocomotive;
            if (w != null)
            {
                pantofirst = w.Pantographs[1].CommandUp ? 1 : 0;
                pantosecond = w.Pantographs[2].CommandUp ? 1 : 0;
                pantothird = w.Pantographs.List.Count > 2 && w.Pantographs[3].CommandUp ? 1 : 0;
                pantofourth = w.Pantographs.List.Count > 3 && w.Pantographs[4].CommandUp ? 1 : 0;
                frontorrearcab = (w as MSTSLocomotive).UsingRearCab ? "R" : "F";
                headlight = (int)w.Headlight;
            }

            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            lengths = new int[t.Cars.Count];
            faDiscretes = new string[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                if (t.Cars[i].Flipped == true)
                    flipped[i] = 1;
                else
                    flipped[i] = 0;
                lengths[i] = (int)(t.Cars[i].CarLengthM * 100);
                faDiscretes[i] = "0";
                if (t.Cars[i].FreightAnimations != null)
                    faDiscretes[i] = t.Cars[i].FreightAnimations.FreightAnimationDiscretesString();
            }
            if (t.LeadLocomotive != null)
                leadingID = t.LeadLocomotive.CarID;
            else
                leadingID = "NA";

            version = MultiPlayerManager.ProtocolVersion;

            MD5 = MultiPlayerManager.Instance().MD5Check;
        }
        public override string ToString()
        {
            string tmp = "PLAYER " + user + " " + code + " " + num + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture)
                + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + trainmaxspeed.ToString(CultureInfo.InvariantCulture) + " " + seconds.ToString(CultureInfo.InvariantCulture) + " " + season + " " + weather + " " + pantofirst + " " + pantosecond + " " + pantothird + " " + pantofourth + " " + frontorrearcab + " " + headlight + " \r" +
                leadingID + "\r" + con + "\r" + route + "\r" + path + "\r" + dir + "\r" + url + "\r";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + lengths[i] + "\n" + faDiscretes[i] + "\t";
            }

            tmp += "\r" + MultiPlayerManager.ProtocolVersion + "\r" + MD5;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (version != MultiPlayerManager.ProtocolVersion)
            {
                if (MultiPlayerManager.IsServer())
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(user, ControlMessageType.Error, "Wrong version of protocol, please update to version " + MultiPlayerManager.ProtocolVersion));//server will broadcast this error
                    return;
                }
                else
                {
                    Trace.WriteLine("Wrong version of protocol, will play in single mode, please update to version " + MultiPlayerManager.ProtocolVersion);
                    throw new MultiPlayerException();//client, close the connection
                }
            }
            if (MultiPlayerManager.IsServer() && MultiPlayerManager.Instance().MD5Check != "NA")//I am the server and have MD5 check values, client should have matching MD5, if file is accessible
            {
                if ((MD5 != "NA" && MD5 != MultiPlayerManager.Instance().MD5Check) || !Simulator.Instance.RouteFolder.RouteName.Equals(route, StringComparison.OrdinalIgnoreCase))
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(user, ControlMessageType.Error, "Wrong route dir or TDB file, the dispatcher uses a different route"));//server will broadcast this error
                    return;
                }
            }
            //check if other players with the same name is online
            if (MultiPlayerManager.IsServer())
            {
                //if someone with the same name is there, will throw a fatal error
                lock (lockObjPlayer)
                {
                    if (MultiPlayerManager.FindPlayerTrain(user) != null || MultiPlayerManager.GetUserName() == user)
                    {
                        try
                        {
                            MultiPlayerManager.OnlineTrains.Players[user].Protected = true;
                        }
                        catch { }
                        MultiPlayerManager.Broadcast(new ControlMessage(user, ControlMessageType.SameNameError, "A user with the same name exists"));
                        return;
                        //throw new SameNameException("Same Name");
                    }
                }
            }
            lock (lockObjPlayer)
            {

                if (MultiPlayerManager.FindPlayerTrain(user) != null)
                    return; //already added the player, ignore
                //if the client comes back after disconnected within 10 minutes
                if (MultiPlayerManager.IsServer() && MultiPlayerManager.Instance().lostPlayer != null && MultiPlayerManager.Instance().lostPlayer.TryGetValue(user, out OnlinePlayer onlinePlayer))
                {
                    var p1Train = onlinePlayer.Train;

                    //check to see if the player gets back with the same set of cars
                    bool identical = true;
                    if (cars.Length != p1Train.Cars.Count)
                        identical = false;
                    if (identical != false)
                    {
                        string wagonFilePath = Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder;
                        for (int i = 0; i < cars.Length; i++)
                        {
                            if (wagonFilePath + cars[i] != p1Train.Cars[i].RealWagFilePath)
                            { identical = false; break; }
                        }
                    }

                    //if distance is higher than 1 Km from starting point of path

                    if (WorldLocation.GetDistanceSquared2D(location, p1Train.RearTDBTraveller.WorldLocation) > 1000000)
                    {
                        MultiPlayerManager.OnlineTrains.Players.Add(user, onlinePlayer);
                        onlinePlayer.CreatedTime = Simulator.Instance.GameTime;
                        // re-insert train reference in cars
                        InsertTrainReference(p1Train);
                        MultiPlayerManager.Instance().AddOrRemoveTrain(p1Train, true);
                        if (MultiPlayerManager.IsServer())
                            MultiPlayerManager.Instance().AddOrRemoveLocomotives(user, p1Train, true);
                        MultiPlayerManager.Instance().lostPlayer.Remove(user);
                    }
                    else//if the player uses different train cars
                    {
                        MultiPlayerManager.Instance().lostPlayer.Remove(user);
                    }
                }
                MultiPlayerManager.OnlineTrains.AddPlayers(this);

                //Trace.WriteLine(this.ToString());
                if (MultiPlayerManager.IsServer())// && MPManager.Server.IsRemoteServer())
                {
                    MultiPlayerManager.BroadCast((new MSGOrgSwitch(user, MultiPlayerManager.Instance().OriginalSwitchState)).ToString());
                    MultiPlayerManager.Instance().PlayerAdded = true;
                }
                else //client needs to handle environment
                {
                    if (MultiPlayerManager.GetUserName() == this.user && !MultiPlayerManager.Instance().Connected) //a reply from the server, update my train number
                    {
                        MultiPlayerManager.Instance().Connected = true;
                        Train t = null;
                        if (Simulator.Instance.PlayerLocomotive == null)
                            t = Simulator.Instance.Trains[0];
                        else
                            t = Simulator.Instance.PlayerLocomotive.Train;
                        t.Number = this.num;
                        if (WorldLocation.GetDistanceSquared2D(location, t.RearTDBTraveller.WorldLocation) > 1000000)
                        {
                            t.UpdateTrainJump(location, dir, Travelled, trainmaxspeed);
                            //check to see if the player gets back with the same set of cars
                            bool identical = true;
                            if (cars.Length != t.Cars.Count)
                                identical = false;
                            if (identical != false)
                            {
                                string wagonFilePath = Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder;
                                for (int i = 0; i < cars.Length; i++)
                                {
                                    if (wagonFilePath + cars[i] != t.Cars[i].RealWagFilePath)
                                    { identical = false; break; }
                                }
                            }
                            if (!identical)
                            {
                                var carsCount = t.Cars.Count;
                                t.Cars.RemoveRange(0, carsCount);
                                for (int i = 0; i < cars.Length; i++)
                                {
                                    string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, cars[i]);
                                    if (!File.Exists(wagonFilePath))
                                    {
                                        Trace.TraceWarning($"Ignored missing rolling stock {wagonFilePath}");
                                        continue;
                                    }

                                    try // Load could fail if file has bad data.
                                    {
                                        TrainCar car = RollingStock.Load(t, wagonFilePath);
                                        car.Flipped = flipped[i] != 0;
                                        car.CarID = ids[i];
                                        var carID = car.CarID;
                                        carID = carID.Remove(0, carID.LastIndexOf('-') + 2);
                                        var uid = 0;
                                        if (int.TryParse(carID, out uid))
                                            car.UiD = uid;

                                        if (car.CarID == leadingID)
                                        {
                                            t.LeadLocomotiveIndex = i;
                                            Simulator.Instance.PlayerLocomotive = t.LeadLocomotive as MSTSLocomotive;

                                        }
                                    }
                                    catch (Exception error)
                                    {
                                        Trace.WriteLine(new FileLoadException(wagonFilePath, error));
                                    }
                                }
                            }
                            else
                            {
                                var i = 0;
                                foreach (var car in t.Cars)
                                {
                                    car.CarID = ids[i];
                                    i++;
                                }
                            }
                            t.UpdateMSGReceived = true;
                            t.RequestJump = true; // server has requested me to jump after I re-entered the game
                        }
                    }
                    Simulator.Instance.ClockTime = this.seconds;
                    Simulator.Instance.SetWeather((WeatherType)weather, (SeasonType)season);
                }
            }
        }

        private static void InsertTrainReference(Train train)
        {
            foreach (TrainCar car in train.Cars)
            {
                car.Train = train;
                car.IsPartOfActiveTrain = true;
                car.FreightAnimations?.ShowDiscreteFreightAnimations();
            }
        }
    }
    #endregion MSGPlayer

    #region MSGPlayerTrainSw
    public class MSGPlayerTrainSw : MSGRequired
    {
        public string user = "";
        public int num; //train number
        public bool oldTrainReverseFormation;
        public bool newTrainReverseFormation;
        public string leadingID;
        public MSGPlayerTrainSw() { }
        public MSGPlayerTrainSw(string m)
        {
            string[] areas = m.Split('\r');
            if (areas.Length <= 1)
            {
                throw new InvalidDataException($"Parsing error in MSGPlayerTrainSw {m}");
            }
            try
            {
                var tmp = areas[0].Trim();
                string[] data = tmp.Split(' ');
                user = data[0];
                num = int.Parse(data[1]);
                oldTrainReverseFormation = bool.Parse(data[2]);
                newTrainReverseFormation = bool.Parse(data[3]);
                leadingID = areas[1].Trim();
            }
            catch (Exception)
            {
                throw;
            }
        }


        public MSGPlayerTrainSw(string n, Train t, int tn, bool oldRevForm, bool newRevForm)
        {
            user = n;
            if (t != null)
                num = tn;
            if (t.LeadLocomotive != null)
                leadingID = t.LeadLocomotive.CarID;
            else
                leadingID = "NA";
            oldTrainReverseFormation = oldRevForm;
            newTrainReverseFormation = newRevForm;
        }

        public override string ToString()
        {
            string tmp = "PLAYERTRAINSW " + user + " " + num + " " + oldTrainReverseFormation + " " + newTrainReverseFormation + " " + "\r" + leadingID + "\r";
            return " " + tmp.Length + ": " + tmp;
        }

        private object lockObjPlayer = new object();
        public override void HandleMsg()
        {
            lock (lockObjPlayer)
            {
                MultiPlayerManager.OnlineTrains.SwitchPlayerTrain(this);

                if (MultiPlayerManager.IsServer())
                {
                    MultiPlayerManager.Instance().PlayerAdded = true;
                }
                else //client needs to handle environment
                {
                    if (MultiPlayerManager.GetUserName() == this.user && !MultiPlayerManager.Instance().Connected) //a reply from the server, update my train number
                    {
                        MultiPlayerManager.Instance().Connected = true;
                        Train t = null;
                        if (Simulator.Instance.PlayerLocomotive == null)
                            t = Simulator.Instance.Trains[0];
                        else
                            t = Simulator.Instance.PlayerLocomotive.Train;
                    }
                }
            }
        }

        //this version only intends for the one started on server computer
        /*        public void HandleMsg(OnlinePlayer p)
                {
                    if (!MPManager.IsServer()) return; //only intended for the server, when it gets the player message in OnlinePlayer Receive

                    //client connected directly to the server, thus will send the game status to the player directly (avoiding using broadcast)
                    MPManager.OnlineTrains.SwitchPlayerTrain(this);

                    MSGPlayerTrainSw host = new MSGPlayerTrainSw(MPManager.GetUserName(), MPManager.Simulator.PlayerLocomotive.Train, MPManager.Simulator.PlayerLocomotive.Train.Number,);
                    p.Send(host.ToString());

                    //send the new player information to everyone else
                    host = new MSGPlayerTrainSw(p.Username, p.Train, p.Train.Number);
                    var players = MPManager.OnlineTrains.Players.ToArray();
                    string newPlayer = host.ToString();
                    foreach (var op in players)
                    {
                        op.Value.Send(newPlayer);
                    }
                }*/
    }
    #endregion MSGPlayerTrainSw

    #region MGSwitch
    public class MSGSwitch : Message
    {
        public string user;
        public int TileX, TileZ, WorldID, Selection;
        public bool HandThrown;
        private bool OK = true;

        public MSGSwitch(string m)
        {

            string[] tmp = m.Split(' ');
            if (tmp.Length != 6)
                throw new ArgumentOutOfRangeException("Parsing error " + m);
            user = tmp[0];
            TileX = int.Parse(tmp[1]);
            TileZ = int.Parse(tmp[2]);
            WorldID = int.Parse(tmp[3]);
            Selection = int.Parse(tmp[4]);
            HandThrown = bool.Parse(tmp[5]);
        }

        public MSGSwitch(string n, int tX, int tZ, int u, int s, bool handThrown)
        {
            if (!MultiPlayerManager.Instance().AmAider && MultiPlayerManager.Instance().TrySwitch == false)
            {
                if (handThrown && Simulator.Instance.Confirmer != null)
                    Simulator.Instance.Confirmer.Information(CatalogManager.Catalog.GetString("Dispatcher does not allow hand throw at this time"));
                OK = false;
                return;
            }
            user = n;
            WorldID = u;
            TileX = tX;
            TileZ = tZ;
            Selection = s;
            HandThrown = handThrown;
        }

        public MSGSwitch(string user, IJunction junction, SwitchState targetState, bool handThrown)
        {
            if (!MultiPlayerManager.Instance().AmAider && MultiPlayerManager.Instance().TrySwitch == false)
            {
                if (handThrown && Simulator.Instance.Confirmer != null)
                    Simulator.Instance.Confirmer.Information(CatalogManager.Catalog.GetString("Dispatcher does not allow hand throw at this time"));
                OK = false;
                return;
            }
            this.user = user;
            TrackJunctionNode junctionNode = (junction as TrackJunctionNode) ?? throw new InvalidCastException(nameof(junction));
            WorldID = junctionNode.UiD.WorldId;
            TileX = junctionNode.UiD.Location.TileX;
            TileZ = junctionNode.UiD.Location.TileZ;
            Selection = (int)targetState;
            HandThrown = handThrown;
        }

        public override string ToString()
        {
            if (!OK)
                return null;
            string tmp = "SWITCH " + user + " " + TileX + " " + TileZ + " " + WorldID + " " + Selection + " " + HandThrown;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            //Trace.WriteLine(this.ToString());
            if (MultiPlayerManager.IsServer()) //server got this message from Client
            {
                //if a normal user, and the dispatcher does not want hand throw, just ignore it
                if (HandThrown == true && !MultiPlayerManager.Instance().AllowedManualSwitch && !MultiPlayerManager.Instance().aiderList.Contains(user))
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(user, ControlMessageType.SwitchWarning, "Server does not allow hand thrown of switch"));
                    return;
                }
                TrackJunctionNode trj = RuntimeData.Instance.TrackDB.GetJunctionNode(TileX, TileZ, WorldID);
                bool state = Simulator.Instance.SignalEnvironment.RequestSetSwitch(trj, this.Selection);
                if (state == false)
                    MultiPlayerManager.Broadcast(new ControlMessage(user, ControlMessageType.Warning, "Train on the switch, cannot throw"));
                else
                    MultiPlayerManager.BroadCast(this.ToString()); //server will tell others
            }
            else
            {
                TrackJunctionNode trj = RuntimeData.Instance.TrackDB.GetJunctionNode(TileX, TileZ, WorldID);
                SetSwitch(trj, Selection);
                //trj.SelectedRoute = Selection; //although the new signal system request Signals.RequestSetSwitch, client may just change
                if (user == MultiPlayerManager.GetUserName() && HandThrown == true)//got the message with my name, will confirm with the player
                {
                    Simulator.Instance.Confirmer.Information(CatalogManager.Catalog.GetString("Switched, current route is {0}",
                        Selection == 0 ? CatalogManager.Catalog.GetString("main route") : CatalogManager.Catalog.GetString("side route")));
                    return;
                }
            }
        }

        public static void SetSwitch(TrackNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[switchSection.OriginalIndex].SelectedRoute = switchSection.JunctionSetManual = desiredState;
            switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

            // update linked signals
            foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = Simulator.Instance.SignalEnvironment.Signals[thisSignalIndex];
                thisSignal.Update();
            }
        }
    }
    #endregion MGSwitch

    #region MSGOrgSwitch
    public class MSGOrgSwitch : MSGRequired
    {
        private SortedList<int, TrackJunctionNode> SwitchState;
        public string msgx = "";
        private string user = "";
        private byte[] switchStatesArray;
        public MSGOrgSwitch(string u, string m)
        {
            user = u;
            msgx = m;
        }

        public MSGOrgSwitch(string m)
        {
            string[] tmp = m.Split('\t');
            user = tmp[0].Trim();
            byte[] gZipBuffer = Convert.FromBase64String(tmp[1]);
            using (var memoryStream = new MemoryStream())
            {
                int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                switchStatesArray = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(switchStatesArray, 0, switchStatesArray.Length);
                }
            }
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.IsServer() || user != MultiPlayerManager.GetUserName())
                return; //server will ignore it
            int key = 0;
            SwitchState = new SortedList<int, TrackJunctionNode>();
            try
            {
                foreach (TrackJunctionNode trackJunctionNode in RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes)
                {
                    key = trackJunctionNode.Index;
                    SwitchState.Add(key, trackJunctionNode);
                }
            }
            catch (Exception) { SwitchState = null; throw; } //if error, clean the list and wait for the next signal

            int i = 0, state = 0;
            foreach (KeyValuePair<int, TrackJunctionNode> t in SwitchState)
            {
                state = (int)switchStatesArray[i];
                if (t.Value.SelectedRoute != state)
                {
                    SetSwitch(t.Value, state);
                    //t.Value.SelectedRoute = state;
                }
                i++;
            }
        }

        public static void SetSwitch(TrackNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[switchSection.OriginalIndex].SelectedRoute = switchSection.JunctionSetManual = desiredState;
            switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

            // update linked signals
            foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = Simulator.Instance.SignalEnvironment.Signals[thisSignalIndex];
                thisSignal.Update();
            }
        }

        public override string ToString()
        {
            string tmp = "ORGSWITCH " + user + "\t" + msgx;
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGOrgSwitch

    #region MSGSwitchStatus
    public class MSGSwitchStatus : Message
    {
        private static byte[] preState;
        private static SortedList<int, TrackJunctionNode> SwitchState;
        public bool OKtoSend;
        private static byte[] switchStatesArray;
        public MSGSwitchStatus()
        {
            var i = 0;
            if (SwitchState == null)
            {
                SwitchState = new SortedList<int, TrackJunctionNode>();
                int key = 0;
                foreach (TrackJunctionNode trackJunctionNode in RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes)
                {
                    key = trackJunctionNode.Index;
                    SwitchState.Add(key, trackJunctionNode);
                }
                switchStatesArray = new byte[SwitchState.Count + 2];
            }
            if (preState == null)
            {
                preState = new byte[SwitchState.Count + 2];
                for (i = 0; i < preState.Length; i++)
                    preState[i] = 0;
            }
            i = 0;
            foreach (KeyValuePair<int, TrackJunctionNode> t in SwitchState)
            {
                switchStatesArray[i] = (byte)t.Value.SelectedRoute;
                i++;
            }
            OKtoSend = false;
            for (i = 0; i < SwitchState.Count; i++)
            {
                if (switchStatesArray[i] != preState[i])
                { OKtoSend = true; }//something is different, will send
                preState[i] = switchStatesArray[i];
            }
            if (OKtoSend == false)
            {
                //new player added, will keep sending for a while
                if (Simulator.Instance.GameTime - MultiPlayerManager.Instance().lastPlayerAddedTime < 3 * MultiPlayerManager.Instance().MPUpdateInterval)
                    OKtoSend = true;
            }
        }

        public MSGSwitchStatus(string m)
        {
            if (SwitchState == null)
            {
                int key = 0;
                SwitchState = new SortedList<int, TrackJunctionNode>();
                try
                {
                    foreach (TrackJunctionNode trackJunctionNode in RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes)
                    {
                        key = trackJunctionNode.Index;
                        SwitchState.Add(key, trackJunctionNode);
                    }
                    switchStatesArray = new byte[SwitchState.Count + 128];//a bit more for safety
                }
                catch (Exception) { SwitchState = null; throw; } //if error, clean the list and wait for the next signal

            }
            byte[] gZipBuffer = Convert.FromBase64String(m);
            using (var memoryStream = new MemoryStream())
            {
                BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(switchStatesArray, 0, switchStatesArray.Length);
                }

            }
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.IsServer())
                return; //server will ignore it


            int i = 0, state = 0;
            foreach (KeyValuePair<int, TrackJunctionNode> t in SwitchState)
            {
                state = (int)switchStatesArray[i];
                if (t.Value.SelectedRoute != state)
                {
                    if (!SwitchOccupiedByPlayerTrain(t.Value))
                    {
                        SetSwitch(t.Value, state);
                        //t.Value.SelectedRoute = state;
                    }
                }
                i++;
            }

        }

        public static void SetSwitch(TrackNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[switchSection.OriginalIndex].SelectedRoute = switchSection.JunctionSetManual = desiredState;
            switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

            // update linked signals
            foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = Simulator.Instance.SignalEnvironment.Signals[thisSignalIndex];
                thisSignal.Update();
            }
        }

        private static bool SwitchOccupiedByPlayerTrain(TrackJunctionNode junctionNode)
        {
            if (Simulator.Instance.PlayerLocomotive == null)
                return false;
            Train train = Simulator.Instance.PlayerLocomotive.Train;
            if (train == null)
                return false;
            if (train.FrontTDBTraveller.TrackNode.Index == train.RearTDBTraveller.TrackNode.Index)
                return false;
            Traveller traveller = new Traveller(train.RearTDBTraveller);
            while (traveller.NextSection())
            {
                if (traveller.TrackNode.Index == train.FrontTDBTraveller.TrackNode.Index)
                    break;
                if (traveller.TrackNode == junctionNode)
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            byte[] buffer = switchStatesArray;
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);

            string tmp = "SWITCHSTATES " + Convert.ToBase64String(gZipBuffer);
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGSwitchStatus

    #region MSGTrain
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGTrain : Message
    {
        private string[] cars;
        private string[] ids;
        private int[] flipped; //if a wagon is engine
        private int[] lengths;
        private string[] faDiscretes;
        private int TrainNum;
        private int direction;
        private float Travelled;
        private int mDirection;
        private string name;

        private WorldLocation location;

        public MSGTrain(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' ');
            int last = 0;
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float x = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            location = new WorldLocation(tileX, tileZ, x, 0, z);
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 2];//with an empty "" at end
            ids = new string[areas.Length - 2];
            flipped = new int[areas.Length - 2];
            lengths = new int[areas.Length - 2];
            faDiscretes = new string[areas.Length - 2];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
                lengths[i] = int.Parse(carinfo[2]);
                faDiscretes[i] = carinfo[3];
            }
            index = areas[areas.Length - 2].IndexOf('\n');
            last = areas[areas.Length - 2].Length;
            name = areas[areas.Length - 2].Substring(index + 1, last - index - 1);

            //Trace.WriteLine(this.ToString());

        }

        public MSGTrain(Train t, int n)
        {
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            lengths = new int[t.Cars.Count];
            faDiscretes = new string[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                lengths[i] = (int)t.Cars[i].CarLengthM;
                if (t.Cars[i].Flipped == true)
                    flipped[i] = 1;
                else
                    flipped[i] = 0;
                faDiscretes[i] = "0";
                if (t.Cars[i].FreightAnimations != null)
                    faDiscretes[i] = t.Cars[i].FreightAnimations.FreightAnimationDiscretesString();
            }
            TrainNum = n;
            direction = t.RearTDBTraveller.Direction == Direction.Forward ? 1 : 0;
            location = t.RearTDBTraveller.WorldLocation;
            Travelled = t.DistanceTravelled;
            mDirection = TranslateMidpointDirection(t.MUDirection); //(int)t.MUDirection;
            name = t.Name;
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.IsServer())
                return; //server will ignore it
            //Trace.WriteLine(this.ToString());
            // construct train data
            Train train = new Train
            {
                Number = this.TrainNum,

                TrainType = TrainType.Remote,
                DistanceTravelled = Travelled,
                MUDirection = TranslateMidpointDirection(this.mDirection), // (MidpointDirection)this.mDirection;
                RearTDBTraveller = new Traveller(location, direction == 1 ? Direction.Forward : Direction.Backward)
            };
            string[] faDiscreteSplit;
            List<LoadData> loadDataList = new List<LoadData>();
            //if (consistDirection != 1)
            //	train.RearTDBTraveller.ReverseDirection();
            for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
            {
                string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, cars[i]);
                TrainCar car = null;
                try
                {
                    car = RollingStock.Load(train, wagonFilePath);
                    car.CarLengthM = lengths[i];
                    if (faDiscretes[i][0] != '0')
                    {
                        int numDiscretes = faDiscretes[i][0];
                        // There are discrete freight animations, add them to wagon
                        faDiscreteSplit = faDiscretes[i].Split('&');
                        loadDataList.Clear();
                        for (int j = 1; j < faDiscreteSplit.Length; j++)
                        {
                            string faDiscrete = faDiscreteSplit[j];
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
                    Trace.WriteLine(wagonFilePath + " " + error);
                    car = MultiPlayerManager.Instance().SubCar(train, wagonFilePath, lengths[i]);
                }
                if (car == null)
                    continue;

                car.Flipped = flipped[i] != 0;
                car.CarID = ids[i];
            }

            if (train.Cars.Count == 0)
                return;
            train.Name = name;

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
            MultiPlayerManager.Instance().AddOrRemoveTrain(train, true);
        }

        public override string ToString()
        {
            string tmp = "TRAIN " + TrainNum + " " + direction + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + mDirection + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + lengths[i] + "\n" + faDiscretes[i] + "\t";
            }
            tmp += "\n" + name + "\t";
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGTrain

    #region MSGUpdateTrain
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGUpdateTrain : Message
    {
        private string[] cars;
        private string[] ids;
        private int[] flipped; //if a wagon is engine
        private int[] lengths; //if a wagon is engine
        private int TrainNum;
        private string[] faDiscretes;
        private int direction;
        private float Travelled;
        private int mDirection;
        private string user;
        private WorldLocation location;

        public MSGUpdateTrain(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' ');
            int last = 0;
            user = m.Substring(0, index + 1);
            m = m.Remove(0, index + 1);
            user = user.Trim();

            index = m.IndexOf(' ');
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float x = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            location = new WorldLocation(tileX, tileZ, x, 0, z);
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 1];//with an empty "" at end
            ids = new string[areas.Length - 1];
            flipped = new int[areas.Length - 1];
            lengths = new int[areas.Length - 1];
            faDiscretes = new string[areas.Length - 1];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
                lengths[i] = int.Parse(carinfo[2]);
                faDiscretes[i] = carinfo[3];
            }

            //Trace.WriteLine(this.ToString());

        }
        public MSGUpdateTrain(string u, Train t, int n)
        {
            user = u;
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            lengths = new int[t.Cars.Count];
            faDiscretes = new string[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                lengths[i] = (int)t.Cars[i].CarLengthM;
                if (t.Cars[i].Flipped == true)
                    flipped[i] = 1;
                else
                    flipped[i] = 0;
                faDiscretes[i] = "0";
                if (t.Cars[i].FreightAnimations != null)
                    faDiscretes[i] = t.Cars[i].FreightAnimations.FreightAnimationDiscretesString();
            }
            TrainNum = n;
            direction = t.RearTDBTraveller.Direction == Direction.Forward ? 1 : 0;
            location = t.RearTDBTraveller.WorldLocation;
            Travelled = t.DistanceTravelled;
            mDirection = TranslateMidpointDirection(t.MUDirection);// (int)t.MUDirection;
        }

        private static TrainCar FindCar(Train t, string name)
        {
            return t.Cars.FirstOrDefault(car => car.CarID == name);
        }

        private object lockObj = new object();

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.IsServer())
                return; //server will ignore it
            if (user != MultiPlayerManager.GetUserName())
                return; //not the one requested GetTrain
            Train train;
            lock (lockObj)
            {
                train = Simulator.Instance.Trains.FirstOrDefault(t => t.Number == TrainNum);
            }
            if (train != null)
            {
                Traveller traveller = new Traveller(location, direction == 1 ? Direction.Forward : Direction.Backward);
                List<TrainCar> tmpCars = new List<TrainCar>();
                string[] faDiscreteSplit;
                List<LoadData> loadDataList = new List<LoadData>();
                for (var i = 0; i < cars.Length; i++)
                {
                    string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, cars[i]);
                    TrainCar car = FindCar(train, ids[i]);
                    try
                    {
                        if (car == null)
                            car = RollingStock.Load(train, wagonFilePath);
                        car.CarLengthM = lengths[i];
                        if (faDiscretes[i][0] != '0')
                        {
                            int numDiscretes = faDiscretes[i][0];
                            // There are discrete freight animations, add them to wagon
                            faDiscreteSplit = faDiscretes[i].Split('&');
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
                        Trace.WriteLine(wagonFilePath + " " + error);
                        car = MultiPlayerManager.Instance().SubCar(train, wagonFilePath, lengths[i]);
                    }
                    if (car == null)
                        continue;

                    car.Flipped = (flipped[i] != 0);
                    car.CarID = ids[i];
                    tmpCars.Add(car);
                }

                if (tmpCars.Count == 0)
                    return;

                // Replace the train car list (after loading, the order needs to be the same as in the message)
                train.Cars.Clear();
                train.Cars.AddRange(tmpCars);
                train.MUDirection = TranslateMidpointDirection(mDirection); // (MidpointDirection)mDirection;
                train.RearTDBTraveller = traveller;
                train.CalculatePositionOfCars();
                train.DistanceTravelled = Travelled;
                train.CheckFreight();
                train.SetDistributedPowerUnitIds();
                train.ReinitializeEOT();
            }
            // New train
            else
            {
                train = new Train()
                {
                    Number = TrainNum,
                    TrainType = TrainType.Remote,
                    DistanceTravelled = Travelled,
                    RearTDBTraveller = new Traveller(location, direction == 1 ? Direction.Forward : Direction.Backward)
                };

                for (var i = 0; i < cars.Length; i++)
                {

                    string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, cars[i]);
                    TrainCar car = null;
                    try
                    {
                        car = RollingStock.Load(train, wagonFilePath);
                        car.CarLengthM = lengths[i];
                    }
                    catch (Exception error)
                    {
                        Trace.WriteLine(wagonFilePath + " " + error);
                        car = MultiPlayerManager.Instance().SubCar(train, wagonFilePath, lengths[i]);
                    }
                    if (car == null)
                        continue;

                    car.Flipped = flipped[i] != 0;
                    car.CarID = ids[i];
                }

                if (train.Cars.Count == 0)
                    return;
                train.MUDirection = TranslateMidpointDirection(mDirection);// (MidpointDirection)mDirection;
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
                MultiPlayerManager.Instance().AddOrRemoveTrain(train, true);
                if (MultiPlayerManager.IsServer())
                    MultiPlayerManager.Instance().AddOrRemoveLocomotives(user, train, true);
            }
        }

        public override string ToString()
        {
            string tmp = "UPDATETRAIN " + user + " " + TrainNum + " " + direction + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture)
                + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + mDirection + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + lengths[i] + "\n" + faDiscretes[i] + "\t";
            }
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGUpdateTrain

    #region MSGLocoChange
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGLocoChange : Message
    {
        private int num;
        private string engine;
        private string user;
        private string frontOrRearCab;
        public MSGLocoChange(string m)
        {
            m.Trim();
            string[] t = m.Split('\t');
            user = t[0];
            engine = t[1];
            frontOrRearCab = t[2];
            num = int.Parse(t[3]);
        }

        public MSGLocoChange(string u, string l, string f, Train t)
        {
            user = u;
            engine = l;
            frontOrRearCab = f;
            num = t.Number;
        }

        public override void HandleMsg()
        {
            foreach (var t in Simulator.Instance.Trains)
            {
                foreach (var car in t.Cars)
                {
                    if (car.CarID == engine)
                    {
                        car.Train.LeadLocomotive = car as MSTSLocomotive ?? throw new InvalidCastException(nameof(car));
                        car.Train.LeadLocomotive.UsingRearCab = frontOrRearCab != "F";
                        foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                        {
                            if (p.Value.Train == t)
                            {
                                p.Value.LeadingLocomotiveID = car.CarID;
                                break;
                            }
                        }
                        if (MultiPlayerManager.IsServer())
                            MultiPlayerManager.BroadCast((new MSGLocoChange(user, engine, frontOrRearCab, t)).ToString());
                        return;
                    }
                }
            }
        }

        public override string ToString()
        {
            string tmp = "LOCCHANGE " + user + "\t" + engine + "\t" + frontOrRearCab + "\t" + num;
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGLocoChange

    #region MSGGetTrain
    public class MSGGetTrain : Message
    {
        public int num;
        public string user;

        public MSGGetTrain(string u, int m)
        {
            user = u;
            num = m;
        }

        public MSGGetTrain(string m)
        {
            string[] tmp = m.Split(' ');
            user = tmp[0];
            num = int.Parse(tmp[1]);
        }

        public override string ToString()
        {

            string tmp = "GETTRAIN " + user + " " + num;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (MultiPlayerManager.IsServer())
            {
                foreach (Train t in Simulator.Instance.Trains)
                {
                    if (t == null)
                        continue;
                    if (t.Number == num) //found it, broadcast to everyone
                    {
                        MultiPlayerManager.BroadCast((new MSGUpdateTrain(user, t, t.Number)).ToString());
                    }
                }
            }
        }

    }
    #endregion MSGGetTrain

    #region MSGUncouple
    public class MSGUncouple : Message
    {
        public string user, newTrainName, carID, firstCarIDOld, firstCarIDNew;
        public int mDirection1;
        public float Travelled1, Speed1;
        public int trainDirection;
        public int mDirection2;
        public float Travelled2, Speed2;
        public int train2Direction;
        public int newTrainNumber;
        public int oldTrainNumber;
        public int whichIsPlayer;
        private string[] ids1;
        private string[] ids2;
        private int[] flipped1;
        private int[] flipped2;

        private WorldLocation location1;
        private WorldLocation location2;

        private static TrainCar FindCar(List<TrainCar> list, string id)
        {
            foreach (TrainCar car in list)
                if (car.CarID == id)
                    return car;
            return null;
        }
        public MSGUncouple(string m)
        {
            string[] areas = m.Split('\t');
            user = areas[0].Trim();

            whichIsPlayer = int.Parse(areas[1].Trim());

            firstCarIDOld = areas[2].Trim();

            firstCarIDNew = areas[3].Trim();

            string[] tmp = areas[4].Split(' ');
            location1 = new WorldLocation(int.Parse(tmp[0]), int.Parse(tmp[1]), float.Parse(tmp[2], CultureInfo.InvariantCulture), 0, float.Parse(tmp[3], CultureInfo.InvariantCulture));
            Travelled1 = float.Parse(tmp[4], CultureInfo.InvariantCulture);
            Speed1 = float.Parse(tmp[5], CultureInfo.InvariantCulture);
            trainDirection = int.Parse(tmp[6]);
            oldTrainNumber = int.Parse(tmp[7]);
            mDirection1 = int.Parse(tmp[8]);
            tmp = areas[5].Split('\n');
            ids1 = new string[tmp.Length - 1];
            flipped1 = new int[tmp.Length - 1];
            for (var i = 0; i < ids1.Length; i++)
            {
                string[] field = tmp[i].Split('\r');
                ids1[i] = field[0].Trim();
                flipped1[i] = int.Parse(field[1].Trim());
            }

            tmp = areas[6].Split(' ');
            location2 = new WorldLocation(int.Parse(tmp[0]), int.Parse(tmp[1]), float.Parse(tmp[2], CultureInfo.InvariantCulture), 0, float.Parse(tmp[3], CultureInfo.InvariantCulture));
            Travelled2 = float.Parse(tmp[4], CultureInfo.InvariantCulture);
            Speed2 = float.Parse(tmp[5], CultureInfo.InvariantCulture);
            train2Direction = int.Parse(tmp[6]);
            newTrainNumber = int.Parse(tmp[7]);
            mDirection2 = int.Parse(tmp[8]);

            tmp = areas[7].Split('\n');
            ids2 = new string[tmp.Length - 1];
            flipped2 = new int[tmp.Length - 1];
            for (var i = 0; i < ids2.Length; i++)
            {
                string[] field = tmp[i].Split('\r');
                ids2[i] = field[0].Trim();
                flipped2[i] = int.Parse(field[1].Trim());
            }
        }

        public MSGUncouple(Train t, Train newT, string u, string ID, TrainCar car)
        {
            if (t.Cars.Count == 0 || newT.Cars.Count == 0)
            { user = ""; return; }//no cars in one of the train, not sure how to handle, so just return;
            Train temp = null;
            int tmpNum;
            if (!t.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {//the old train should have the player, otherwise, 
                tmpNum = t.Number;
                t.Number = newT.Number;
                newT.Number = tmpNum;
                temp = t;
                t = newT;
                newT = temp;
            }
            carID = ID;
            user = u;
            //TileX1 = t.RearTDBTraveller.TileX; TileZ1 = t.RearTDBTraveller.TileZ; X1 = t.RearTDBTraveller.X; Z1 = t.RearTDBTraveller.Z;
            Travelled1 = t.DistanceTravelled;
            Speed1 = t.SpeedMpS;
            trainDirection = t.RearTDBTraveller.Direction == Direction.Forward ? 0 : 1;//0 forward, 1 backward
            mDirection1 = TranslateMidpointDirection(t.MUDirection);// (int)t.MUDirection;
            //TileX2 = newT.RearTDBTraveller.TileX; TileZ2 = newT.RearTDBTraveller.TileZ; X2 = newT.RearTDBTraveller.X; Z2 = newT.RearTDBTraveller.Z;
            Travelled2 = newT.DistanceTravelled;
            Speed2 = newT.SpeedMpS;
            train2Direction = newT.RearTDBTraveller.Direction == Direction.Forward ? 0 : 1;//0 forward, 1 backward
            mDirection2 = TranslateMidpointDirection(newT.MUDirection); // (int)newT.MUDirection;

            if (MultiPlayerManager.IsServer())
                newTrainNumber = newT.Number;//serer will use the correct number
            else
            {
                newTrainNumber = 1000000 + StaticRandom.Next(1000000);//client: temporary assign a train number 1000000-2000000, will change to the correct one after receiving response from the server
                newT.TrainType = TrainType.Remote; //by default, uncoupled train will be controlled by the server
            }
            if (!newT.Cars.Contains(Simulator.Instance.PlayerLocomotive)) //if newT does not have player locomotive, it may be controlled remotely
            {
                if (newT.LeadLocomotive == null)
                { newT.LeadLocomotiveIndex = -1; newT.LeadNextLocomotive(); }

                foreach (TrainCar car1 in newT.Cars)
                {
                    car1.Train = newT;
                    foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                    {
                        if (car1.CarID.StartsWith(p.Value.LeadingLocomotiveID))
                        {
                            p.Value.Train = car1.Train;
                            car1.Train.TrainType = TrainType.Remote;
                            break;
                        }
                    }
                }
            }

            if (!t.Cars.Contains(Simulator.Instance.PlayerLocomotive)) //if t (old train) does not have player locomotive, it may be controlled remotely
            {
                if (t.LeadLocomotive == null)
                { t.LeadLocomotiveIndex = -1; t.LeadNextLocomotive(); }

                foreach (TrainCar car1 in t.Cars)
                {
                    car1.Train = t;
                    foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                    {
                        if (car1.CarID.StartsWith(p.Value.LeadingLocomotiveID))
                        {
                            p.Value.Train = car1.Train;
                            car1.Train.TrainType = TrainType.Remote;
                            break;
                        }
                    }
                }
            }


            if (t.Cars.Contains(Simulator.Instance.PlayerLocomotive) || newT.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                if (Simulator.Instance.Confirmer != null)
                    Simulator.Instance.Confirmer.Information(MultiPlayerManager.Catalog.GetString("Trains uncoupled, gain back control by Alt-E"));
            }

            /*
            //if one of the train holds other player's lead locomotives
            foreach (var pair in MPManager.OnlineTrains.Players)
            {
                string check = pair.Key + " - 0";
                foreach (var car1 in t.Cars) if (car1.CarID.StartsWith(check)) { t.TrainType = Train.TRAINTYPE.REMOTE; break; }
                foreach (var car1 in newT.Cars) if (car1.CarID.StartsWith(check)) { newT.TrainType = Train.TRAINTYPE.REMOTE; break; }
            }*/
            oldTrainNumber = t.Number;
            newTrainName = "UC" + newTrainNumber;
            newT.Number = newTrainNumber;

            if (newT.LeadLocomotive != null)
                firstCarIDNew = "Leading " + newT.LeadLocomotive.CarID;
            else
                firstCarIDNew = "First " + newT.Cars[0].CarID;

            if (t.LeadLocomotive != null)
                firstCarIDOld = "Leading " + t.LeadLocomotive.CarID;
            else
                firstCarIDOld = "First " + t.Cars[0].CarID;

            ids1 = new string[t.Cars.Count];
            flipped1 = new int[t.Cars.Count];
            for (var i = 0; i < ids1.Length; i++)
            {
                ids1[i] = t.Cars[i].CarID;
                flipped1[i] = t.Cars[i].Flipped == true ? 1 : 0;
            }

            ids2 = new string[newT.Cars.Count];
            flipped2 = new int[newT.Cars.Count];
            for (var i = 0; i < ids2.Length; i++)
            {
                ids2[i] = newT.Cars[i].CarID;
                flipped2[i] = newT.Cars[i].Flipped == true ? 1 : 0;
            }

            //to see which train contains the car (PlayerLocomotive)
            if (t.Cars.Contains(car))
                whichIsPlayer = 0;
            else if (newT.Cars.Contains(car))
                whichIsPlayer = 1;
            else
                whichIsPlayer = 2;
        }

        private string FillInString(int i)
        {
            string tmp = "";
            if (i == 1)
            {
                for (var j = 0; j < ids1.Length; j++)
                {
                    tmp += ids1[j] + "\r" + flipped1[j] + "\n";
                }
            }
            else
            {
                for (var j = 0; j < ids2.Length; j++)
                {
                    tmp += ids2[j] + "\r" + flipped2[j] + "\n";
                }
            }
            return tmp;
        }
        public override string ToString()
        {
            if (string.IsNullOrEmpty(user))
                return "5: ALIVE"; //wrong, so just return an ALIVE string
            string tmp = "UNCOUPLE " + user + "\t" + whichIsPlayer + "\t" + firstCarIDOld + "\t" + firstCarIDNew
                + "\t" + location1.TileX + " " + location1.TileZ + " " + location1.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location1.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled1.ToString(CultureInfo.InvariantCulture) + " " + Speed1.ToString(CultureInfo.InvariantCulture) + " " + trainDirection + " " + oldTrainNumber + " " + mDirection1 + "\t"
                + FillInString(1)
                + "\t" + location2.TileX + " " + location2.TileZ + " " + location2.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location2.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled2.ToString(CultureInfo.InvariantCulture) + " " + Speed2.ToString(CultureInfo.InvariantCulture) + " " + train2Direction + " " + newTrainNumber + " " + mDirection2 + "\t"
                + FillInString(2);
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            bool oldIDIsLead = true, newIDIsLead = true;
            if (firstCarIDNew.StartsWith("First "))
            {
                firstCarIDNew = firstCarIDNew.Replace("First ", "");
                newIDIsLead = false;
            }
            else
                firstCarIDNew = firstCarIDNew.Replace("Leading ", "");
            if (firstCarIDOld.StartsWith("First "))
            {
                firstCarIDOld = firstCarIDOld.Replace("First ", "");
                oldIDIsLead = false;
            }
            else
                firstCarIDOld = firstCarIDOld.Replace("Leading ", "");

            if (user == MultiPlayerManager.GetUserName()) //received from the server, but it is about mine action of uncouple
            {
                foreach (Train t in Simulator.Instance.Trains)
                {
                    foreach (TrainCar car in t.Cars)
                    {
                        if (car.CarID == firstCarIDOld)//got response about this train
                        {
                            t.Number = oldTrainNumber;
                            if (oldIDIsLead == true)
                                t.LeadLocomotive = car as MSTSLocomotive;
                        }
                        if (car.CarID == firstCarIDNew)//got response about this train
                        {
                            t.Number = newTrainNumber;
                            if (newIDIsLead == true)
                                t.LeadLocomotive = car as MSTSLocomotive;
                        }
                    }
                }

            }
            else
            {
                MSTSLocomotive lead = null;
                Train train = null;
                List<TrainCar> trainCars = null;
                bool canPlace = true;
                TrackCircuitPartialPathRoute tempRoute;
                foreach (Train t in Simulator.Instance.Trains)
                {
                    var found = false;
                    foreach (TrainCar car in t.Cars)
                    {
                        if (car.CarID == firstCarIDOld)//got response about this train
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {
                        train = t;
                        lead = train.LeadLocomotive;
                        trainCars = t.Cars;
                        List<TrainCar> tmpcars = new List<TrainCar>();
                        for (var i = 0; i < ids1.Length; i++)
                        {
                            TrainCar car = FindCar(trainCars, ids1[i]);
                            if (car == null)
                                continue;
                            car.Flipped = flipped1[i] != 0;
                            tmpcars.Add(car);
                        }
                        if (tmpcars.Count == 0)
                            return;
                        if (MultiPlayerManager.IsServer())
                            MultiPlayerManager.Instance().AddOrRemoveLocomotives(user, train, false);
                        t.Cars.Clear();
                        t.Cars.AddRange(tmpcars);
                        Direction d1 = Direction.Forward;
                        if (trainDirection == 1)
                            d1 = Direction.Backward;
                        t.RearTDBTraveller = new Traveller(location1, d1);
                        t.DistanceTravelled = Travelled1;
                        t.SpeedMpS = Speed1;
                        t.LeadLocomotive = lead;
                        t.MUDirection = TranslateMidpointDirection(mDirection1);// (MidpointDirection)mDirection1;
                        train.ControlMode = TrainControlMode.Explorer;
                        train.CheckFreight();
                        train.SetDistributedPowerUnitIds();
                        train.ReinitializeEOT();
                        train.InitializeBrakes();
                        tempRoute = train.CalculateInitialTrainPosition();
                        if (tempRoute.Count == 0)
                        {
                            throw new InvalidDataException("Remote train original position not clear");
                        }

                        train.SetInitialTrainRoute(tempRoute);
                        train.CalculatePositionOfCars();
                        train.ResetInitialTrainRoute(tempRoute);

                        train.CalculatePositionOfCars();
                        train.AITrainBrakePercent = 100;
                        //train may contain myself, and no other players, thus will make myself controlling this train
                        if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                        {
                            Simulator.Instance.PlayerLocomotive.Train = train;
                            //train.TrainType = Train.TRAINTYPE.PLAYER;
                            train.InitializeBrakes();
                        }
                        foreach (var c in train.Cars)
                        {
                            if (c.CarID == firstCarIDOld && oldIDIsLead)
                                train.LeadLocomotive = c as MSTSLocomotive;
                            foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                            {
                                if (p.Value.LeadingLocomotiveID == c.CarID)
                                    p.Value.Train = train;
                            }
                        }
                        break;
                    }
                }

                if (train == null || trainCars == null)
                    return;

                Train train2 = new Train();
                List<TrainCar> tmpcars2 = new List<TrainCar>();
                for (var i = 0; i < ids2.Length; i++)
                {
                    TrainCar car = FindCar(trainCars, ids2[i]);
                    if (car == null)
                        continue;
                    tmpcars2.Add(car);
                    car.Flipped = flipped2[i] != 0;
                }
                if (tmpcars2.Count == 0)
                    return;
                train2.Cars.Clear();
                train2.Cars.AddRange(tmpcars2);
                train2.Name = String.Concat(train.Name, Train.TotalNumber.ToString());
                train2.LeadLocomotive = null;
                train2.LeadNextLocomotive();
                train2.CheckFreight();
                train2.SetDistributedPowerUnitIds();
                train.ReinitializeEOT();

                //train2 may contain myself, and no other players, thus will make myself controlling this train
                /*if (train2.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
                {
                    var gainControl = true;
                    foreach (var pair in MPManager.OnlineTrains.Players)
                    {
                        string check = pair.Key + " - 0";
                        foreach (var car1 in train2.Cars) if (car1.CarID.StartsWith(check)) { gainControl = false; break; }
                    }
                    if (gainControl == true) { train2.TrainType = Train.TRAINTYPE.PLAYER; train2.LeadLocomotive = MPManager.Simulator.PlayerLocomotive; }
                }*/
                Direction d2 = Direction.Forward;
                if (train2Direction == 1)
                    d2 = Direction.Backward;

                // and fix up the travellers
                train2.RearTDBTraveller = new Traveller(location2, d2);
                train2.DistanceTravelled = Travelled2;
                train2.SpeedMpS = Speed2;
                train2.MUDirection = TranslateMidpointDirection(mDirection2); // (MidpointDirection)mDirection2;
                train2.ControlMode = TrainControlMode.Explorer;
                train2.CheckFreight();
                train2.SetDistributedPowerUnitIds();
                train.ReinitializeEOT();
                train2.InitializeBrakes();
                tempRoute = train2.CalculateInitialTrainPosition();
                if (tempRoute.Count == 0)
                {
                    throw new InvalidDataException("Remote train original position not clear");
                }

                train2.SetInitialTrainRoute(tempRoute);
                train2.CalculatePositionOfCars();
                train2.ResetInitialTrainRoute(tempRoute);

                train2.CalculatePositionOfCars();
                train2.AITrainBrakePercent = 100;
                if (train2.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                {
                    Simulator.Instance.PlayerLocomotive.Train = train2;
                    //train2.TrainType = Train.TRAINTYPE.PLAYER;
                    train2.InitializeBrakes();
                }
                foreach (TrainCar car in train2.Cars)
                {
                    if (car.CarID == firstCarIDNew && newIDIsLead)
                        train2.LeadLocomotive = car as MSTSLocomotive;
                    car.Train = train2;
                    foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                    {
                        if (car.CarID.StartsWith(p.Value.LeadingLocomotiveID))
                        {
                            p.Value.Train = car.Train;
                            //car.Train.TrainType = Train.TRAINTYPE.REMOTE;
                            break;
                        }
                    }
                }

                train.UncoupledFrom = train2;
                train2.UncoupledFrom = train;

                if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive) || train2.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                {
                    if (Simulator.Instance.Confirmer != null)
                        Simulator.Instance.Confirmer.Information(MultiPlayerManager.Catalog.GetString("Trains uncoupled, gain back control by Alt-E"));
                }

                //if (whichIsPlayer == 0 && MPManager.OnlineTrains.findTrain(user) != null) MPManager.OnlineTrains.Players[user].Train = train;
                //else if (whichIsPlayer == 1 && MPManager.OnlineTrains.findTrain(user) != null) MPManager.OnlineTrains.Players[user].Train = train2; //the player may need to update the train it drives
                MultiPlayerManager.Instance().AddOrRemoveTrain(train2, true);

                if (MultiPlayerManager.IsServer())
                {
                    this.newTrainNumber = train2.Number;//we got a new train number, will tell others.
                    train2.TrainType = TrainType.Static;
                    this.oldTrainNumber = train.Number;
                    train2.LastReportedSpeed = 1;
                    if (train2.Name.Length < 4)
                        train2.Name = String.Concat("STATIC-", train2.Name);
                    Simulator.Instance.AI.TrainListChanged = true;
                    MultiPlayerManager.Instance().AddOrRemoveLocomotives(user, train, true);
                    MultiPlayerManager.Instance().AddOrRemoveLocomotives(user, train2, true);
                    MultiPlayerManager.BroadCast(this.ToString());//if server receives this, will tell others, including whoever sent the information
                }
                else
                {
                    train2.TrainType = TrainType.Remote;
                    train2.Number = this.newTrainNumber; //client receives a message, will use the train number specified by the server
                    train.Number = this.oldTrainNumber;
                }
                //if (MPManager.IsServer() && MPManager.Instance().AllowedManualSwitch) train2.InitializeSignals(false);
            }
        }
    }
    #endregion MSGUncouple


    #region MSGCouple
    public class MSGCouple : Message
    {
        private string[] cars;
        private string[] ids;
        private int[] flipped; //if a wagon is engine
        private int TrainNum;
        private int RemovedTrainNum;
        private int direction;
        private int Lead, mDirection;
        private float Travelled;
        private string whoControls;

        private WorldLocation location;

        public MSGCouple(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' ');
            int last = 0;
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            RemovedTrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float x = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            location = new WorldLocation(tileX, tileZ, x, 0, z);
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            Lead = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            whoControls = m.Substring(0, index + 1).Trim();
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 1];//with an empty "" at end
            ids = new string[areas.Length - 1];
            flipped = new int[areas.Length - 1];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
            }

            //Trace.WriteLine(this.ToString());

        }

        public MSGCouple(Train t, Train oldT, bool remove)
        {
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                if (t.Cars[i].Flipped == true)
                    flipped[i] = 1;
                else
                    flipped[i] = 0;
            }
            TrainNum = t.Number;
            RemovedTrainNum = oldT.Number;
            direction = t.RearTDBTraveller.Direction == Direction.Forward ? 0 : 1;
            location = t.RearTDBTraveller.WorldLocation;
            Travelled = t.DistanceTravelled;
            MultiPlayerManager.Instance().RemoveUncoupledTrains(t); //remove the trains from uncoupled train lists
            MultiPlayerManager.Instance().RemoveUncoupledTrains(oldT);
            var j = 0;
            Lead = -1;
            foreach (TrainCar car in t.Cars)
            {
                if (car == t.LeadLocomotive)
                { Lead = j; break; }
                j++;
            }
            whoControls = "NA";
            var index = 0;
            if (t.LeadLocomotive != null)
                index = t.LeadLocomotive.CarID.IndexOf(" - 0");
            if (index > 0)
            {
                whoControls = t.LeadLocomotive.CarID.Substring(0, index);
            }
            foreach (var p in MultiPlayerManager.OnlineTrains.Players)
            {
                if (p.Value.Train == oldT)
                { p.Value.Train = t; break; }
            }
            mDirection = TranslateMidpointDirection(t.MUDirection); // (int)t.MUDirection;
            if (t.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                if (Simulator.Instance.Confirmer != null)
                    Simulator.Instance.Confirmer.Information(MultiPlayerManager.Catalog.GetString("Trains coupled, hit \\ then Shift-? to release brakes"));
            }
            if (!MultiPlayerManager.IsServer() || !(oldT.TrainType == TrainType.AiIncorporated))
            {
                if (MultiPlayerManager.IsServer())
                {
                    MultiPlayerManager.Instance().AddOrRemoveLocomotives("", oldT, false);
                    MultiPlayerManager.Instance().AddOrRemoveLocomotives("", t, false);
                    var player = "";
                    foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                    {
                        if (p.Value.Train == t)
                        { player = p.Key; break; }
                    }
                    MultiPlayerManager.Instance().AddOrRemoveLocomotives(player, t, true);
                }
                MultiPlayerManager.Instance().AddOrRemoveTrain(oldT, false); //remove the old train
            }
        }

        public override string ToString()
        {
            string tmp = "COUPLE " + TrainNum + " " + RemovedTrainNum + " " + direction + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + Lead + " " + whoControls + " " + mDirection + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\t";
            }
            return " " + tmp.Length + ": " + tmp;
        }

        private static TrainCar FindCar(Train t1, Train t2, string carID)
        {
            foreach (TrainCar c in t1.Cars)
                if (c.CarID == carID)
                    return c;
            foreach (TrainCar c in t2.Cars)
                if (c.CarID == carID)
                    return c;
            return null;
        }
        public override void HandleMsg()
        {
            if (MultiPlayerManager.IsServer())
                return;//server will not receive this from client
            Train train = null, train2 = null;

            foreach (Train t in Simulator.Instance.Trains)
            {
                if (t.Number == this.TrainNum)
                    train = t;
                if (t.Number == this.RemovedTrainNum)
                    train2 = t;
            }

            TrainCar lead = train.LeadLocomotive;
            if (lead == null)
                lead = train2.LeadLocomotive;

            /*if (MPManager.Simulator.PlayerLocomotive != null && MPManager.Simulator.PlayerLocomotive.Train == train2)
            {
                Train tmp = train2; train2 = train; train = tmp; MPManager.Simulator.PlayerLocomotive.Train = train;
            }*/

            if (train == null || train2 == null)
                return; //did not find the trains to op on

            //if (consistDirection != 1)
            //	train.RearTDBTraveller.ReverseDirection();
            List<TrainCar> tmpCars = new List<TrainCar>();
            for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
            {
                TrainCar car = FindCar(train, train2, ids[i]);
                if (car == null)
                    continue;
                bool flip = true;
                if (flipped[i] == 0)
                    flip = false;
                car.Flipped = flip;
                car.CarID = ids[i];
                tmpCars.Add(car);
                car.Train = train;

            }// for each rail car
            if (tmpCars.Count == 0)
                return;
            //List<TrainCar> oldList = train.Cars;
            train.Cars.Clear();
            train.Cars.AddRange(tmpCars);

            train.DistanceTravelled = Travelled;
            train.MUDirection = TranslateMidpointDirection(mDirection); // (MidpointDirection)mDirection;
            train.RearTDBTraveller = new Traveller(location, direction == 0 ? Direction.Forward : Direction.Backward);
            train.CheckFreight();
            train.SetDistributedPowerUnitIds();
            train.ReinitializeEOT();
            train.CalculatePositionOfCars();
            train.LeadLocomotive = null;
            train2.LeadLocomotive = null;
            if (Lead != -1 && Lead < train.Cars.Count)
                train.LeadLocomotive = train.Cars[Lead] as MSTSLocomotive;

            if (train.LeadLocomotive == null)
                train.LeadNextLocomotive();

            //mine is not the leading locomotive, thus I give up the control
            if (train.LeadLocomotive != Simulator.Instance.PlayerLocomotive)
            {
                train.TrainType = TrainType.Remote; //make the train remote controlled
            }

            if (MultiPlayerManager.FindPlayerTrain(train2))
            {
                int count = 0;
                while (count < 3)
                {
                    try
                    {
                        foreach (var p in MultiPlayerManager.OnlineTrains.Players)
                        {
                            if (p.Value.Train == train2)
                                p.Value.Train = train;
                        }
                        break;
                    }
                    catch (Exception) { count++; }
                }
            }

            //update the remote user's train
            if (MultiPlayerManager.FindPlayerTrain(whoControls) != null)
                MultiPlayerManager.OnlineTrains.Players[whoControls].Train = train;
            if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
                Simulator.Instance.PlayerLocomotive.Train = train;

            if (MultiPlayerManager.IsServer())
                MultiPlayerManager.Instance().AddOrRemoveLocomotives("", train2, false);
            MultiPlayerManager.Instance().AddOrRemoveTrain(train2, false);


            if (train.Cars.Contains(Simulator.Instance.PlayerLocomotive))
            {
                if (Simulator.Instance.Confirmer != null)
                    Simulator.Instance.Confirmer.Information(MultiPlayerManager.Catalog.GetString("Trains coupled, hit \\ then Shift-? to release brakes"));
            }
        }
    }
    #endregion MSGCouple

    #region MSGSignalStatus
    public class MSGSignalStatus : Message
    {
        private static byte[] preState;
        private static SortedList<long, SignalHead> signals;
        public bool OKtoSend;
        private bool SendEverything;
        private static byte[] signalsStates;
        private static List<int> changedAspectIndex = new List<int>();
        private static string[] signalTextStates;
        private static string[] preTextState;

        /// <summary>
        /// Constructor to create a message from signal data
        /// </summary>
        public MSGSignalStatus()
        {
            var i = 0;
            if (signals == null)
            {
                signals = new SortedList<long, SignalHead>();
                if (Simulator.Instance.SignalEnvironment.Signals != null)
                {
                    foreach (var s in Simulator.Instance.SignalEnvironment.Signals)
                    {
                        if (s != null && (s.SignalType == SignalCategory.Signal || s.SignalType == SignalCategory.SpeedSignal) && s.SignalHeads != null)
                            foreach (var h in s.SignalHeads)
                            {
                                //Trace.WriteLine(h.TDBIndex);
                                signals.Add(h.TDBIndex * 100000 + h.TrackItemIndex, h);
                            }
                    }
                }
                signalsStates = new byte[signals.Count * 2];
                signalTextStates = new string[signals.Count];
            }
            if (preState == null)
            {
                preState = new byte[signals.Count * 2 + 2];
                for (i = 0; i < preState.Length; i++)
                    preState[i] = 0;
            }
            if (preTextState == null)
            {
                preTextState = new string[signals.Count];
                for (i = 0; i < preTextState.Length; i++)
                    preTextState[i] = String.Empty;
            }

            i = 0;
            foreach (var t in signals)
            {
                signalsStates[2 * i] = (byte)(t.Value.SignalIndicationState + 1);
                signalsStates[2 * i + 1] = (byte)(t.Value.DrawState + 1);
                signalTextStates[i] = t.Value.TextSignalAspect;
                i++;
            }
            OKtoSend = false;
            SendEverything = false;
            for (i = 0; i < signals.Count * 2; i++)
            {
                if (signalsStates[i] != preState[i])
                { OKtoSend = true; }//something is different, will send
                preState[i] = signalsStates[i];
            }
            changedAspectIndex.Clear();
            for (i = 0; i < signals.Count; i++)
            {
                if (signalTextStates[i] != preTextState[i])
                {
                    changedAspectIndex.Add(i);
                    OKtoSend = true;
                }
            }
            //new player added, will keep sending for a while
            if (Simulator.Instance.GameTime - MultiPlayerManager.Instance().lastPlayerAddedTime < 3 * MultiPlayerManager.Instance().MPUpdateInterval)
            {
                OKtoSend = true;
                SendEverything = true;
            }
        }

        /// <summary>
        /// Constructor to decode the message "m"
        /// </summary>
        public MSGSignalStatus(string m)
        {
            if (signals == null)
            {
                signals = new SortedList<long, SignalHead>();
                try
                {
                    if (Simulator.Instance.SignalEnvironment?.Signals != null)
                    {
                        foreach (var s in Simulator.Instance.SignalEnvironment.Signals)
                        {
                            if (s != null && (s.SignalType == SignalCategory.Signal || s.SignalType == SignalCategory.SpeedSignal) && s.SignalHeads != null)
                                foreach (var h in s.SignalHeads)
                                {
                                    //Trace.WriteLine(h.TDBIndex);
                                    signals.Add(h.TDBIndex * 100000 + h.TrackItemIndex, h);
                                }
                        }
                    }
                    signalsStates = new byte[signals.Count * 2];
                    signalTextStates = new string[signals.Count];
                }
                catch (Exception) { signals = null; throw; }//error, clean the list, so we can get another signal
            }
            byte[] gZipBuffer = Convert.FromBase64String(m);
            using (var memoryStream = new MemoryStream())
            {
                BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(signalsStates, 0, signalsStates.Length); // Read integer values for aspect and draw state

                    using (var reader = new BinaryReader(gZipStream, System.Text.Encoding.UTF8))
                    {
                        int numChangedSignals = reader.ReadInt32();
                        for (int i = 0; i < numChangedSignals; i++)
                        {
                            int signalIndex = reader.ReadInt32();
                            signalTextStates[signalIndex] = reader.ReadString();
                        }
                    }
                }
            }
        }

        //how to handle the message?
        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.Instance().IsDispatcher)
                return; //server will ignore it

            //if (signals.Count != readed/2-2) { Trace.WriteLine("Error in synchronizing signals " + signals.Count + " " + readed); return; }
            int i = 0;
            foreach (var t in signals)
            {
                t.Value.SignalIndicationState = (SignalAspectState)(signalsStates[2 * i] - 1); //we added 1 when build the message, need to subtract it out
                t.Value.DrawState = (int)(signalsStates[2 * i + 1] - 1);
                t.Value.TextSignalAspect = signalTextStates[i];
                i++;
            }
            //Trace.Write("\n");
        }

        public override string ToString()
        {
            byte[] buffer = signalsStates;
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length); // Send integer values for aspect and draw state first

                using (var writer = new BinaryWriter(gZipStream, System.Text.Encoding.UTF8))
                {
                    int numToSend = SendEverything ? signals.Count : changedAspectIndex.Count;
                    writer.Write(numToSend);
                    for (int i = 0; i < numToSend; i++)
                    {
                        int signalIndex = SendEverything ? i : changedAspectIndex[i];
                        writer.Write(signalIndex);
                        writer.Write(signalTextStates[signalIndex]);
                        preTextState[signalIndex] = signalTextStates[signalIndex];
                    }
                }
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            string tmp = "SIGNALSTATES " + Convert.ToBase64String(gZipBuffer); // fill in the message body here
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGSignalStatus

    #region MSGLocoInfo
    public class MSGLocoInfo : Message
    {
        private float EB, DB, TT, VL, CC, BC, DC, FC, I1, I2, SH, SE, LE;
        private string user;
        private int tnum; //train number

        //constructor to create a message from signal data
        public MSGLocoInfo(TrainCar c, string u)
        {
            MSTSLocomotive loco = (MSTSLocomotive)c;
            EB = DB = TT = VL = CC = BC = DC = FC = I1 = I2 = SH = SE = LE = 0.0f;
            if (loco is MSTSSteamLocomotive)
            {
                MSTSSteamLocomotive loco1 = (MSTSSteamLocomotive)loco;
                loco1.GetLocoInfo(ref CC, ref BC, ref DC, ref FC, ref I1, ref I2, ref SE, ref LE);
            }
            if (loco.SteamHeatController != null)
            {
                SH = loco.SteamHeatController.CurrentValue;
            }
            if (loco.EngineBrakeController != null)
            {
                EB = loco.EngineBrakeController.CurrentValue;
            }
            if (loco.DynamicBrakeController != null)
            {
                DB = loco.DynamicBrakeController.CurrentValue;
            }
            TT = loco.ThrottleController.CurrentValue;
            if (loco is MSTSElectricLocomotive)
            {
                VL = (loco as MSTSElectricLocomotive).ElectricPowerSupply.FilterVoltageV;
            }
            tnum = loco.Train.Number;
            user = u;
        }

        //constructor to decode the message "m"
        public MSGLocoInfo(string m)
        {
            string[] tmp = m.Split('\t');
            user = tmp[0].Trim();
            tnum = int.Parse(tmp[1]);
            EB = float.Parse(tmp[2], CultureInfo.InvariantCulture);
            DB = float.Parse(tmp[3], CultureInfo.InvariantCulture);
            TT = float.Parse(tmp[4], CultureInfo.InvariantCulture);
            VL = float.Parse(tmp[5], CultureInfo.InvariantCulture);
            CC = float.Parse(tmp[6], CultureInfo.InvariantCulture);
            BC = float.Parse(tmp[7], CultureInfo.InvariantCulture);
            DC = float.Parse(tmp[8], CultureInfo.InvariantCulture);
            FC = float.Parse(tmp[9], CultureInfo.InvariantCulture);
            I1 = float.Parse(tmp[10], CultureInfo.InvariantCulture);
            I2 = float.Parse(tmp[11], CultureInfo.InvariantCulture);
            SH = float.Parse(tmp[12], CultureInfo.InvariantCulture);
            SE = float.Parse(tmp[13], CultureInfo.InvariantCulture);
            LE = float.Parse(tmp[14], CultureInfo.InvariantCulture);
        }

        //how to handle the message?
        public override void HandleMsg() //only client will get message, thus will set states
        {
            foreach (Train t in Simulator.Instance.Trains)
            {
                if (t.TrainType != TrainType.Remote && t.Number == tnum)
                {
                    foreach (var car in t.Cars)
                    {
                        if (car.CarID.StartsWith(user) && car is MSTSLocomotive)
                        {
                            UpdateValue((MSTSLocomotive)car);
                        }
                    }
                    return;
                }
            }
        }

        private void UpdateValue(MSTSLocomotive loco)
        {
            if (loco is MSTSSteamLocomotive)
            {
                MSTSSteamLocomotive loco1 = (MSTSSteamLocomotive)loco;
                loco1.GetLocoInfo(ref CC, ref BC, ref DC, ref FC, ref I1, ref I2, ref SE, ref LE);
            }
            if (loco.SteamHeatController != null)
            {
                SH = loco.SteamHeatController.CurrentValue;
            }
            if (loco.EngineBrakeController != null)
            {
                loco.EngineBrakeController.CurrentValue = EB;
                loco.EngineBrakeController.UpdateValue = 0.0f;
            }
            if (loco.DynamicBrakeController != null)
            {
                loco.DynamicBrakeController.CurrentValue = DB;
                loco.DynamicBrakeController.UpdateValue = 0.0f;
            }

            loco.ThrottleController.CurrentValue = TT;
            loco.ThrottleController.UpdateValue = 0.0f;
            if (loco is MSTSElectricLocomotive)
            {
                (loco as MSTSElectricLocomotive).ElectricPowerSupply.FilterVoltageV = VL;
            }
        }
        public override string ToString()
        {
            string tmp = "LOCOINFO " + user + "\t" + tnum + "\t" + EB.ToString(CultureInfo.InvariantCulture) + "\t" + DB.ToString(CultureInfo.InvariantCulture) + "\t" +
                TT.ToString(CultureInfo.InvariantCulture) + "\t" + VL.ToString(CultureInfo.InvariantCulture) + "\t" + CC.ToString(CultureInfo.InvariantCulture) + "\t" +
                BC.ToString(CultureInfo.InvariantCulture) + "\t" + DC.ToString(CultureInfo.InvariantCulture) + "\t" + FC.ToString(CultureInfo.InvariantCulture) + "\t" +
                I1.ToString(CultureInfo.InvariantCulture) + "\t" + I2.ToString(CultureInfo.InvariantCulture); // fill in the message body here
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGLocoInfo

    #region MSGSignalChange
    public class MSGSignalChange : Message
    {
        private int index;
        private int pick;
        private string sender;

        //constructor to create a message from signal data
        public MSGSignalChange(Signal signal, int p)
        {
            index = signal.Index;
            pick = p;
            sender = MultiPlayerManager.GetUserName();
        }

        //constructor to decode the message "m"
        public MSGSignalChange(string m)
        {
            string[] tmp = m.Split(' ');
            sender = tmp[0].Trim();
            index = int.Parse(tmp[1]);
            pick = int.Parse(tmp[2]);
        }

        //how to handle the message?
        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.Instance().IsDispatcher && !MultiPlayerManager.Instance().aiderList.Contains(sender))
                return; //client will ignore it, also if not an aider, will ignore it

            var signal = Simulator.Instance.SignalEnvironment.Signals[index];
            switch (pick)
            {
                case 0:
                    signal.HoldState = SignalHoldState.None;
                    break;

                case 1:
                    signal.HoldState = SignalHoldState.ManualLock;
                    break;

                case 2:
                    signal.RequestApproachAspect();
                    break;

                case 3:
                    signal.RequestLeastRestrictiveAspect();
                    break;

                case 4:
                    signal.SetManualCallOn(true);
                    break;
            }
        }

        public override string ToString()
        {

            string tmp = "SIGNALCHANGE " + sender + " " + index + " " + pick; // fill in the message body here
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGSignalChange

    #region MSGFlip
    //message to indicate that a train has been flipped (reverse formation)
    // message contains data before flip
    public class MSGFlip : Message
    {
        private string[] cars;
        private string[] ids;
        private int[] flipped; //if a wagon is engine
        private int TrainNum;
        private int direction;
        private int TileX, TileZ;
        private float X, Z, Travelled;
        private int mDirection;
        private float speed;
        private int tni;
        private int count;
        private int tdir;
        private float len;
        private int reverseMU;

        public MSGFlip(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' ');
            int last = 0;
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            TileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            TileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            X = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            Z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            speed = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            tni = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            count = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            tdir = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            len = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            reverseMU = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 1];//with an empty "" at end
            ids = new string[areas.Length - 1];
            flipped = new int[areas.Length - 1];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
            }
        }

        public MSGFlip(Train t, bool setMUParameters, int n)
        {
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            var carGroup = Math.Min(t.Cars.Count, 20); // it is not needed to check for real flip on a full, long consist
            for (var i = 0; i < carGroup; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                if (t.Cars[i].Flipped == true)
                    flipped[i] = 1;
                else
                    flipped[i] = 0;
            }
            TrainNum = n;
            direction = t.RearTDBTraveller.Direction == Direction.Forward ? 1 : 0;
            TileX = t.RearTDBTraveller.TileX;
            TileZ = t.RearTDBTraveller.TileZ;
            X = t.RearTDBTraveller.X;
            Z = t.RearTDBTraveller.Z;
            Travelled = t.DistanceTravelled;
            mDirection = TranslateMidpointDirection(t.MUDirection); // (int)t.MUDirection;
            speed = t.SpeedMpS;
            tni = t.RearTDBTraveller.TrackNode.Index;
            count = t.Cars.Count;
            tdir = (int)t.RearTDBTraveller.Direction.Reverse();
            len = t.Length;
            reverseMU = (setMUParameters ? 1 : 0);
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MultiPlayerManager.IsServer())
                return; //server will ignore it
                        //Trace.WriteLine(this.ToString());
                        // construct train data
            foreach (Train t in Simulator.Instance.Trains)
            {
                if (t.Number == TrainNum)
                {
                    // Check if real flip
                    var realFlip = false;
                    // if different number of cars, most likely there was a couple/uncouple, so let's assume real flip
                    if (t.Cars.Count != count)
                        realFlip = true;
                    else
                    {
                        for (var i = 0; i < Math.Min(20, t.Cars.Count - 1); i++)
                        {
                            var c = t.Cars[i].RealWagFilePath;
                            var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                            if (index > 0)
                            {
                                c = c.Remove(0, index + 17);
                            }//c: wagon path without folder name
                            if (t.Cars[i].Flipped != (flipped[i] != 0))
                            {
                                Trace.TraceWarning("Invalid data have prevented flipping: car number {0} local flip state {1} remote flip state {2}",
                                    i, t.Cars[i].Flipped, flipped[i]);
                                return;
                            }
                            if (!c.Equals(cars[i], StringComparison.OrdinalIgnoreCase))
                            {
                                Trace.TraceWarning("Invalid data have prevented flipping: car number {0} local filepath {1} remote filepath {2}",
                                    i, t.Cars[i].Flipped, flipped[i]);
                                return;
                            }
                        }
                        realFlip = true;
                    }
                    if (realFlip)
                        t.ToDoUpdate(tni, TileX, TileZ, X, Z, Travelled, speed, TranslateMidpointDirection(direction), tdir, len, true, reverseMU);
                    return;
                }
            }
        }

        public override string ToString()
        {
            string tmp = "FLIP " + TrainNum + " " + direction + " " + TileX + " " + TileZ + " " + X.ToString(CultureInfo.InvariantCulture) + " " + Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + mDirection + " " +
                speed.ToString(CultureInfo.InvariantCulture) + " " + tni + " " + count + " " + tdir + " " + len.ToString(CultureInfo.InvariantCulture) + " " + reverseMU + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + "\t";
            }
            return " " + tmp.Length + ": " + tmp;
        }
    }
    #endregion MSGFlip

    #region MSGMovingTbl

    public class MSGMovingTable : Message
    {
        private string user, newTrainName, carID, firstCarIDOld, firstCarIDNew;
        public MovingTable.MessageCode subMessageCode;
        private int movingTableIndex;
        private bool clockwise;
        private float yangle;

        public MSGMovingTable(string m)
        {
            string[] areas = m.Split('\t');

            movingTableIndex = int.Parse(areas[0].Trim());
            user = areas[1].Trim();
            subMessageCode = (MovingTable.MessageCode)int.Parse(areas[2].Trim());
            clockwise = int.Parse(areas[3].Trim()) == 0 ? false : true;
            yangle = float.Parse(areas[4].Trim());

        }

        public MSGMovingTable(int mti, string u, MovingTable.MessageCode smc, bool cw, float y)
        {
            movingTableIndex = mti;
            user = u;
            subMessageCode = smc;
            clockwise = cw;
            yangle = y;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(user))
                return "5: ALIVE"; //wrong, so just return an ALIVE string
            string tmp = "MOVINGTBL " + movingTableIndex + "\t" + user + "\t" + (int)subMessageCode + "\t" + (clockwise ? 1 : 0) + "\t" + yangle + "\t";
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (user != MultiPlayerManager.GetUserName())
            {
                Simulator.Instance.ActiveMovingTable = Simulator.Instance.MovingTables[movingTableIndex];
                if (Simulator.Instance.ActiveMovingTable is TurnTable turntable)
                {
                    switch (subMessageCode)
                    {
                        case MovingTable.MessageCode.GoToTarget:
                            turntable.RemotelyControlled = true;
                            if (Math.Abs(MathHelper.WrapAngle(turntable.YAngle - yangle)) > 0.2f)
                            {
                                turntable.YAngle = yangle;
                                turntable.TargetY = yangle;
                                turntable.AlignToRemote = true;
                            }
                            turntable.GeneralComputeTarget(clockwise);
                            break;
                        case MovingTable.MessageCode.StartingContinuous:
                            turntable.YAngle = yangle;
                            turntable.TargetY = yangle;
                            turntable.AlignToRemote = true;
                            turntable.GeneralStartContinuous(clockwise);
                            break;
                        default:
                            break;
                    }
                }
                else if (Simulator.Instance.ActiveMovingTable is TransferTable transfertable)
                {
                    switch (subMessageCode)
                    {
                        case MovingTable.MessageCode.GoToTarget:
                            transfertable.RemotelyControlled = true;
                            if (Math.Abs(transfertable.OffsetPos - yangle) > 2.8f)
                            {
                                transfertable.OffsetPos = yangle;
                                transfertable.TargetOffset = yangle;
                                transfertable.AlignToRemote = true;
                            }
                            transfertable.GeneralComputeTarget(clockwise);
                            break;
                        case MovingTable.MessageCode.StartingContinuous:
                            transfertable.OffsetPos = yangle;
                            transfertable.TargetOffset = yangle;
                            transfertable.AlignToRemote = true;
                            transfertable.GeneralStartContinuous(clockwise);
                            break;
                        default:
                            break;
                    }
                }
                if (MultiPlayerManager.IsServer())
                {
                    MultiPlayerManager.BroadCast(this.ToString());//if server receives this, will tell others, including whoever sent the information
                }
            }
        }
    }

    #endregion MSGMovingTbl
}
