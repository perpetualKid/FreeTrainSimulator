using System;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class PlayerStateMessage : MultiPlayerMessageContent
    {
        private readonly object lockObjPlayer = new object();

        public string Route { get; set; }
        public double ClockTime { get; set; }
        public SeasonType Season { get; set; }
        public WeatherType WeatherType { get; set; }
        public TrainStateMessage TrainState { get; set; }
        public LocomotiveStateMessage PlayerLocomotive { get; set; }
        public LocomotiveStateMessage LeadLocomotive { get; set; }
        public string ConsistFile { get; set; }
        public string PathFile { get; set; }
        public int ProtocolVersion { get; set; }
        public string RouteTdbHash { get; set; }

        [MemoryPackConstructor]
        public PlayerStateMessage() { }

        public PlayerStateMessage(Train train)
        {
            ArgumentNullException.ThrowIfNull(train, nameof(train));

            Route = Simulator.Instance.RouteModel.Name;
            ClockTime = Simulator.Instance.ClockTime;
            Season = Simulator.Instance.Season;
            WeatherType = Simulator.Instance.WeatherType;

            ConsistFile = Path.GetRelativePath(Simulator.Instance.RouteFolder.ContentFolder.ConsistsFolder, Simulator.Instance.ConsistFileName);
            PathFile = Simulator.Instance.PlayerPath.Id;
            TrainState = new TrainStateMessage(train)
            {
                Speed = train.TrainMaxSpeedMpS //overwriting Speed from base message as we need the Max Speed here
            };

            PlayerLocomotive = new LocomotiveStateMessage(Simulator.Instance.PlayerLocomotive);

            if (train.LeadLocomotive != Simulator.Instance.PlayerLocomotive)
                LeadLocomotive = new LocomotiveStateMessage(train.LeadLocomotive);

            ProtocolVersion = MultiPlayerManager.ProtocolVersion;

            RouteTdbHash = multiPlayerManager.RouteTdbHash;

        }

        public override void HandleMessage()
        {
            if (ProtocolVersion != MultiPlayerManager.ProtocolVersion)
            {
                if (multiPlayerManager.IsDispatcher)
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(User, ControlMessageType.Error, "Wrong version of protocol, please update to version " + MultiPlayerManager.ProtocolVersion));//server will broadcast this error
                    return;
                }
                else
                {
                    Trace.WriteLine("Wrong version of protocol, will play in single mode, please update to version " + MultiPlayerManager.ProtocolVersion);
                    throw new MultiPlayerException();//client, close the connection
                }
            }
            if (multiPlayerManager.IsDispatcher && multiPlayerManager.RouteTdbHash != "NA")//I am the server and have MD5 check values, client should have matching MD5, if file is accessible
            {
                if ((RouteTdbHash != "NA" && RouteTdbHash != multiPlayerManager.RouteTdbHash) || !Simulator.Instance.RouteFolder.RouteName.Equals(Route, StringComparison.OrdinalIgnoreCase))
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(User, ControlMessageType.Error, "Wrong route dir or TDB file, the dispatcher uses a different route"));//server will broadcast this error
                    return;
                }
            }
            //check if other players with the same name is online
            if (multiPlayerManager.IsDispatcher)
            {
                //if someone with the same name is there, will throw a fatal error
                if (MultiPlayerManager.FindPlayerTrain(User) != null || multiPlayerManager.UserName == User)
                {
                    MultiPlayerManager.OnlineTrains.Players[User].Protected = true;
                    MultiPlayerManager.Broadcast(new ControlMessage(User, ControlMessageType.SameNameError, "A user with the same name exists"));
                    return;
                    //throw new SameNameException("Same Name");
                }
            }
            lock (lockObjPlayer)
            {

                if (MultiPlayerManager.FindPlayerTrain(User) != null)
                    return; //already added the player, ignore
                //if the client comes back after disconnected within 10 minutes
                if (multiPlayerManager.IsDispatcher && multiPlayerManager.LostPlayer != null && multiPlayerManager.LostPlayer.TryGetValue(User, out OnlinePlayer onlinePlayer))
                {
                    Train p1Train = onlinePlayer.Train;

                    //if distance is higher than 1 Km from starting point of path
                    if (WorldLocation.GetDistanceSquared2D(TrainState.RearLocation, p1Train.RearTDBTraveller.WorldLocation) > 1000000)
                    {
                        MultiPlayerManager.OnlineTrains.Players.Add(User, onlinePlayer);
                        onlinePlayer.CreatedTime = Simulator.Instance.GameTime;
                        // re-insert train reference in cars
                        InsertTrainReference(p1Train);
                        multiPlayerManager.AddOrRemoveTrain(p1Train, true);
                        if (multiPlayerManager.IsDispatcher)
                            multiPlayerManager.AddOrRemoveLocomotives(User, p1Train, true);
                        multiPlayerManager.LostPlayer.Remove(User);
                    }
                    else//if the player uses different train cars
                    {
                        multiPlayerManager.LostPlayer.Remove(User);
                    }
                }
                MultiPlayerManager.OnlineTrains.AddPlayers(this);

                if (multiPlayerManager.IsDispatcher)
                {
                    MultiPlayerManager.Broadcast(new SwitchStateMessage(true));
                    MultiPlayerManager.Instance().PlayerAdded = true;
                }
                else //client needs to handle environment
                {
                    if (multiPlayerManager.UserName == User && !multiPlayerManager.Connected) //a reply from the server, update my train number
                    {
                        multiPlayerManager.Connected = true;
                        Train train = Simulator.Instance.PlayerLocomotive == null ? Simulator.Instance.Trains[0] : Simulator.Instance.PlayerLocomotive.Train;
                        train.Number = TrainState.TrainNumber;

                        if (WorldLocation.GetDistanceSquared2D(TrainState.RearLocation, train.RearTDBTraveller.WorldLocation) > 1000000)
                        {
                            train.UpdateTrainJump(TrainState.RearLocation, TrainState.TrainDirection, TrainState.DistanceTravelled, TrainState.Speed);
                            //check to see if the player gets back with the same set of cars
                            bool identical = true;
                            if (TrainState.TrainCars != null && TrainState.TrainCars.Count != train.Cars.Count)
                            {
                                string wagonFilePath = Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder;
                                for (int i = 0; i < TrainState.TrainCars.Count; i++)
                                {
                                    if (wagonFilePath + TrainState.TrainCars[i].WagonFilePath != train.Cars[i].RealWagFilePath)
                                    {
                                        identical = false;
                                        break;
                                    }
                                }
                            }
                            if (!identical)
                            {
                                train.Cars.RemoveRange(0, train.Cars.Count);
                                for (int i = 0; i < TrainState.TrainCars.Count; i++)
                                {
                                    string wagonFilePath = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, TrainState.TrainCars[i].WagonFilePath);
                                    if (!File.Exists(wagonFilePath))
                                    {
                                        Trace.TraceWarning($"Ignored missing rolling stock {wagonFilePath}");
                                        continue;
                                    }

                                    try // Load could fail if file has bad data.
                                    {
                                        TrainCar car = RollingStock.Load(train, wagonFilePath);
                                        car.Flipped = TrainState.TrainCars[i].Flipped;
                                        car.CarID = TrainState.TrainCars[i].TrainCarId;
                                        string carID = car.CarID;
                                        carID = carID.Remove(0, carID.LastIndexOf('-') + 2);
                                        if (int.TryParse(carID, out int uid))
                                            car.UiD = uid;

                                        if (car.CarID == PlayerLocomotive.LocomotiveId)
                                        {
                                            train.LeadLocomotiveIndex = i;
                                            Simulator.Instance.PlayerLocomotive = train.LeadLocomotive;

                                        }
                                    }
                                    catch (IOException error)
                                    {
                                        Trace.WriteLine(new FileLoadException(wagonFilePath, error));
                                    }
                                }
                            }
                            else
                            {
                                var i = 0;
                                foreach (var car in train.Cars)
                                {
                                    car.CarID = TrainState.TrainCars[i].TrainCarId;
                                    i++;
                                }
                            }
                            train.UpdateMSGReceived = true;
                            train.RequestJump = true; // server has requested me to jump after I re-entered the game
                        }
                    }
                    Simulator.Instance.ClockTime = ClockTime;
                    Simulator.Instance.SetWeather(WeatherType, Season);
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
}
