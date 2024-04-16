using System.Diagnostics;

using GetText;

using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{

    [MemoryPackable]
    public partial class ServerMessage : MultiPlayerMessageContent
    {
        public string Dispatcher {  get; set; }

        public override void HandleMessage()
        {
            if (multiPlayerManager.UserName == Dispatcher)
            {
                if (multiPlayerManager.IsDispatcher)
                    return; //already a dispatcher, not need to worry
                multiPlayerManager.Connected = true;
                multiPlayerManager.IsDispatcher = true;
                multiPlayerManager.RememberOriginalSwitchState();
                Trace.TraceInformation("You are the new dispatcher. Enjoy!");
                Simulator.Instance.Confirmer?.Information(CatalogManager.Catalog.GetString("You are the new dispatcher. Enjoy!"));
            }
            else
            {
                multiPlayerManager.IsDispatcher = false;
                Simulator.Instance.Confirmer?.Information(CatalogManager.Catalog.GetString("New dispatcher is {0}", Dispatcher));
                Trace.TraceInformation("New dispatcher is {0}", Dispatcher);
            }
        }
    }
}
