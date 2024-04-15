using System;

using MemoryPack;

namespace Orts.Simulation.MultiPlayer.Messaging
{
    [MemoryPackable]
    public partial class AiderMessage : MultiPlayerMessageContent
    {
        public bool CanbeAdded { get; set; }

        public override void HandleMessage()
        {
            if (multiPlayerManager.IsDispatcher)
                return;
            if (multiPlayerManager.UserName == User)
            {
                if (CanbeAdded)
                {
                    multiPlayerManager.AmAider = true;
                    Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("You are an assistant now, will be able to handle switches and signals."));
                }
                else
                {
                    MultiPlayerManager.Instance().AmAider = false;
                    Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("You are no longer an assistant."));
                }
            }
        }
    }
}
