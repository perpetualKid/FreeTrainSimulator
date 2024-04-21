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
using System.Globalization;
using System.IO;
using System.Text;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Track;

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
                "UNCOUPLE" => new MSGUncouple(messageEncoding.GetString(content)),
                "COUPLE" => new MSGCouple(messageEncoding.GetString(content)),
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

    #region MSGRequired
    public abstract class MSGRequired : Message
    {

    }
    #endregion

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
}
