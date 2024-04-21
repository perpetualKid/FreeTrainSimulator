using System;

using MemoryPack;

using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class LocomotiveChangeMessage : MultiPlayerMessageContent
    {
        public CabViewType ActiveCabView { get; set; }
        public int TrainNumber { get; set; }
        public string LocomotiveId { get; set; }


        [MemoryPackConstructor]
        public LocomotiveChangeMessage() { }

        public LocomotiveChangeMessage(MSTSLocomotive locomotive) 
        { 
            ArgumentNullException.ThrowIfNull(locomotive, nameof(locomotive));

            ActiveCabView = locomotive.UsingRearCab ? CabViewType.Rear : CabViewType.Front;
            TrainNumber = locomotive.Train.Number;
            LocomotiveId = locomotive.CarID;
        }

        public override void HandleMessage()
        {
            foreach (Train train in Simulator.Instance.Trains)
            {
                foreach (TrainCar car in train.Cars)
                {
                    if (car.CarID == LocomotiveId)
                    {
                        car.Train.LeadLocomotive = car as MSTSLocomotive ?? throw new InvalidCastException(nameof(car));
                        car.Train.LeadLocomotive.UsingRearCab = ActiveCabView == CabViewType.Rear;
                        foreach (OnlinePlayer player in MultiPlayerManager.OnlineTrains.Players.Values)
                        {
                            if (player.Train == train)
                            {
                                player.LeadingLocomotiveID = car.CarID;
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
