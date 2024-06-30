using System.Collections.Generic;

using MemoryPack;

using Orts.Common;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class MoveMessage : TrainStateBaseMessage
    {
        private static readonly Dictionary<int, int> missingTimes = new Dictionary<int, int>();

        [MemoryPackConstructor]
        public MoveMessage() { }

        public MoveMessage(Train train): base(train, false)
        {
            train.LastReportedSpeed = train.SpeedMpS;
        }

        public override void HandleMessage()
        {
            if (User == multiPlayerManager.UserName)//about itself, check if the number of car has changed, otherwise ignore
            {
                //if I am a remote controlled train now
                if (Simulator.Instance.PlayerLocomotive.Train.TrainType == TrainType.Remote)
                {
                    Simulator.Instance.PlayerLocomotive.Train.ToDoUpdate(TrackNodeIndex, RearLocation.Tile, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length);
                }
                return;
            }
            if (User == "0xAI")
            {
                Train train = Simulator.Instance.Trains.GetTrainByNumber(TrainNumber);
                if (train != null)
                {
                    if (train.Cars.Count != CarCount) //the number of cars are different, client will not update it, ask for new information
                    {
                        if (!multiPlayerManager.IsDispatcher)
                        {
                            if (CheckMissingTimes(train.Number))
                                MultiPlayerManager.Broadcast(new TrainRequestMessage() { TrainNumber = train.Number });
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
                    train.ToDoUpdate(TrackNodeIndex, RearLocation.Tile, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length);
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
                    train.ToDoUpdate(TrackNodeIndex, RearLocation.Tile, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length);
                    // This is necessary as sometimes a train isn'train in the Trains list
                    multiPlayerManager.AddOrRemoveTrain(train, true);
                    return;
                }
            }
            //I do not have the train, tell server to send it to me
            if (!multiPlayerManager.IsDispatcher && CheckMissingTimes(TrainNumber))
                MultiPlayerManager.Broadcast(new TrainRequestMessage() { TrainNumber = TrainNumber });
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
