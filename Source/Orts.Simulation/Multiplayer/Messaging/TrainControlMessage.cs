using System.Collections.Generic;

using MemoryPack;

using Orts.Common;
using Orts.Simulation.Commanding;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class TrainControlMessage : MultiPlayerMessageContent
    {
        public TrainControlRequestType RequestType { get; set; }
        public float TrainMaxSpeed { get; set; }
        public int TrainNumber { get; set; }

        public override void HandleMessage()
        {
            if (multiPlayerManager.UserName == User && RequestType == TrainControlRequestType.Confirm)
            {
                Train train = Simulator.Instance.PlayerLocomotive.Train;
                train.TrainType = TrainType.Player;
                train.LeadLocomotive = Simulator.Instance.PlayerLocomotive;
                InitializeBrakesCommand.Receiver = Simulator.Instance.PlayerLocomotive.Train;
                train.InitializeSignals(false);
                Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("You gained back the control of your train"));
                multiPlayerManager.RemoveUncoupledTrains(train);
            }
            else if (RequestType == TrainControlRequestType.Confirm) //server inform me that a train is now remote
            {
                foreach (KeyValuePair<string, OnlinePlayer> player in MultiPlayerManager.OnlineTrains.Players)
                {
                    if (player.Key == User)
                    {
                        foreach (Train train in Simulator.Instance.Trains)
                        {
                            if (train.Number == TrainNumber)
                            {
                                player.Value.Train = train;
                                break;
                            }
                        }
                        multiPlayerManager.RemoveUncoupledTrains(player.Value.Train);
                        player.Value.Train.TrainType = TrainType.Remote;
                        player.Value.Train.TrainMaxSpeedMpS = TrainMaxSpeed;
                        break;
                    }
                }
            }
            else if (multiPlayerManager.IsDispatcher && RequestType == TrainControlRequestType.Request)
            {
                foreach (KeyValuePair<string, OnlinePlayer> player in MultiPlayerManager.OnlineTrains.Players)
                {
                    if (player.Key == User)
                    {
                        foreach (Train train in Simulator.Instance.Trains)
                        {
                            if (train.Number == TrainNumber)
                            {
                                player.Value.Train = train;
                                break;
                            }
                        }
                        player.Value.Train.TrainType = TrainType.Remote;
                        player.Value.Train.TrainMaxSpeedMpS = TrainMaxSpeed;
                        player.Value.Train.InitializeSignals(false);
                        multiPlayerManager.RemoveUncoupledTrains(player.Value.Train);
                        MultiPlayerManager.Broadcast(new TrainControlMessage()
                        {
                            User = User,
                            RequestType = TrainControlRequestType.Confirm,
                            TrainNumber = player.Value.Train.Number,
                            TrainMaxSpeed = player.Value.Train.TrainMaxSpeedMpS
                        });
                        break;
                    }
                }
            }
        }
    }
}
