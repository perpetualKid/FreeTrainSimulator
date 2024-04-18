using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MemoryPack;

using Orts.Common;
using Orts.Common.Position;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class MoveMessage : MultiPlayerMessageContent
    {
        private static readonly Dictionary<int, int> missingTimes = new Dictionary<int, int>();

        public int TrainNumber { get; set; }
        public float Speed { get; set; }
        public float DistanceTravelled { get; set; }
        public int CarCount { get; set; }
        public Direction TrainDirection { get; set; }
        public WorldLocation RearLocation { get; set; }
        public int TrackNodeIndex { get; set; }
        public float Length { get; set; }
        public MidpointDirection MultiUnitDirection { get; set; }

        [MemoryPackConstructor]
        public MoveMessage() { }

        public MoveMessage(Train train)
        {
            ArgumentNullException.ThrowIfNull(train, nameof(train));

            TrainNumber = train.Number;
            Speed = train.SpeedMpS;
            DistanceTravelled = train.DistanceTravelled;
            RearLocation = train.RearTDBTraveller.WorldLocation;
            TrackNodeIndex = train.RearTDBTraveller.TrackNode.Index;
            TrainDirection = train.RearTDBTraveller.Direction.Reverse();
            CarCount = train.Cars.Count;
            Length = train.Length;
            MultiUnitDirection = train.MUDirection;

            train.LastReportedSpeed = train.SpeedMpS;
        }

        public override void HandleMessage()
        {
            bool found = false; //a train may not be in my sim
            if (User == multiPlayerManager.UserName)//about itself, check if the number of car has changed, otherwise ignore
            {
                //if I am a remote controlled train now
                if (Simulator.Instance.PlayerLocomotive.Train.TrainType == TrainType.Remote)
                {
                    Simulator.Instance.PlayerLocomotive.Train.ToDoUpdate(TrackNodeIndex, RearLocation.TileX, RearLocation.TileZ, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length);
                }
                found = true;
                return;
            }
            if (User == "0xAI")
            {
                Train train = Simulator.Instance.Trains.GetTrainByNumber(TrainNumber);
                if (train != null)
                {
                    found = true;
                    if (train.Cars.Count != CarCount) //the number of cars are different, client will not update it, ask for new information
                    {
                        if (!MultiPlayerManager.IsServer())
                        {
                            if (CheckMissingTimes(train.Number))
                                MultiPlayerManager.Notify((new MSGGetTrain(MultiPlayerManager.GetUserName(), train.Number)).ToString());
                            return;
                        }
                    }
                    //if (train.TrainType == TrainType.Remote)
                    //{
                    //    var reverseTrav = false;
                    //    //                                 Alternate way to check for train flip
                    //    //                                if (m.user.Contains("0xAI") && m.trackNodeIndex == train.RearTDBTraveller.TrackNodeIndex && m.tdbDir != (int)train.RearTDBTraveller.Direction)
                    //    //                                {
                    //    //                                    reverseTrav = true;
                    //    //                                }
                    //}
                    train.ToDoUpdate(TrackNodeIndex, RearLocation.TileX, RearLocation.TileZ, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length);
                    return;
                }
            }
            else
            {
                Train train = MultiPlayerManager.FindPlayerTrain(User);
                if (train != null)
                {
                    // skip the case where this train is merged with yours and you are the boss of that train
                    if (train.Number == Simulator.Instance.PlayerLocomotive.Train.Number &&
                        Simulator.Instance.PlayerLocomotive == Simulator.Instance.PlayerLocomotive.Train.LeadLocomotive &&
                        train.TrainType != TrainType.Remote && train.TrainType != TrainType.Static)
                        return;
                    found = true;
                    train.ToDoUpdate(TrackNodeIndex, RearLocation.TileX, RearLocation.TileZ, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length);
                    // This is necessary as sometimes a train isn'train in the Trains list
                    multiPlayerManager.AddOrRemoveTrain(train, true);
                }
            }
            if (found == false) //I do not have the train, tell server to send it to me
            {
                if (!multiPlayerManager.IsDispatcher && CheckMissingTimes(TrainNumber))
                    MultiPlayerManager.Notify((new MSGGetTrain(MultiPlayerManager.GetUserName(), TrainNumber)).ToString());
            }
        }

        //a train is missing, but will wait for 10 messages then ask
        private static bool CheckMissingTimes(int trainNumber)
        {
            if (missingTimes.TryGetValue(trainNumber, out int value))
            {
                if (value < 10)
                {
                    missingTimes[trainNumber]++;
                    return false;
                }
                else
                {
                    missingTimes[trainNumber] = 0;
                    return true;
                }
            }
            else
            {
                missingTimes.Add(trainNumber, 1);
                return false;
            }
        }
    }
}
