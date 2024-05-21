using System;
using System.Collections.ObjectModel;

using Orts.Common;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.Simulation.Multiplayer.Messaging
{
    public abstract class LocomotiveStateBaseMessage : MultiPlayerMessageContent
    {
        public int TrainNumber { get; set; }
        public string LocomotiveId { get; set; }
        public CabViewType ActiveCabView { get; set; }
        public HeadLightState HeadLight {  get; set; }
        public PantographState Pantograph { get; set; }
        public Collection<PantographState> Pantographs { get; private protected set; }

        protected LocomotiveStateBaseMessage() { }

        protected LocomotiveStateBaseMessage(MSTSLocomotive locomotive) 
        {
            ArgumentNullException.ThrowIfNull(locomotive, nameof(locomotive));

            TrainNumber = locomotive.Train.Number;
            LocomotiveId = locomotive.CarID;
            ActiveCabView = locomotive.UsingRearCab ? CabViewType.Rear : CabViewType.Front;
            HeadLight = locomotive.Headlight;

            if (locomotive.Pantographs?.Count > 0)
            { 
                Pantographs = new Collection<PantographState>();
                foreach (Pantograph pantograph in locomotive.Pantographs)
                    Pantographs.Add(pantograph.State);
            }
        }
    }
}
