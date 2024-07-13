using System;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class LocomotiveChangeMessage : LocomotiveStateBaseMessage
    {

        [MemoryPackConstructor]
        public LocomotiveChangeMessage() { }

        public LocomotiveChangeMessage(MSTSLocomotive locomotive) : base(locomotive)
        { 
        }

        public override void HandleMessage()
        {
            foreach (Train train in Simulator.Instance.Trains)
            {
                foreach (TrainCar trainCar in train.Cars)
                {
                    if (trainCar.CarID == LocomotiveId)
                    {
                        trainCar.Train.LeadLocomotive = trainCar as MSTSLocomotive ?? throw new InvalidCastException(nameof(trainCar));
                        trainCar.Train.LeadLocomotive.UsingRearCab = ActiveCabView == CabViewType.Rear;
                        foreach (OnlinePlayer player in MultiPlayerManager.OnlineTrains.Players.Values)
                        {
                            if (player.Train == train)
                            {
                                player.LeadingLocomotiveID = trainCar.CarID;
                                break;
                            }
                        }
                        return;
                    }
                }
            }
        }
    }
}
