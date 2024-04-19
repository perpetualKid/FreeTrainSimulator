using System;
using System.Linq;

using GetText;

using MemoryPack;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class SwitchChangeMessage : MultiPlayerMessageContent
    {
        public SwitchState SwitchState { get; set; }
        public bool ManuallySet { get; set; } // Hand Thrown
        public int JunctionNodeIndex { get; set; }

        [MemoryPackConstructor]
        public SwitchChangeMessage() { }

        public SwitchChangeMessage(IJunction junction, SwitchState targetState, bool handThrown)
        {
            ArgumentNullException.ThrowIfNull(junction,nameof(junction));

            if (!multiPlayerManager.AmAider && !multiPlayerManager.TrySwitch && ManuallySet)
            {
                Simulator.Instance.Confirmer?.Information(CatalogManager.Catalog.GetString("Dispatcher does not allow hand throw at this time"));
                return;
            }
            JunctionNodeIndex = (junction as TrackJunctionNode).Index;
            SwitchState = targetState;
            ManuallySet = handThrown;
        }

        public override void HandleMessage()
        {
            if (multiPlayerManager.IsDispatcher) //server got this message from Client
            {
                //if a normal user, and the dispatcher does not want hand throw, just ignore it
                if (ManuallySet && !multiPlayerManager.AllowedManualSwitch && !multiPlayerManager.aiderList.Contains(User))
                {
                    MultiPlayerManager.Broadcast(new ControlMessage(User, ControlMessageType.SwitchWarning, "Server does not allow hand thrown of switch"));
                    return;
                }
                TrackJunctionNode junctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[JunctionNodeIndex];
                if (!Simulator.Instance.SignalEnvironment.RequestSetSwitch(junctionNode, SwitchState))
                    MultiPlayerManager.Broadcast(new ControlMessage(User, ControlMessageType.Warning, "Train on the switch, cannot throw"));
            }
            else
            {
                TrackJunctionNode junctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[JunctionNodeIndex];
                TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[junctionNode.TrackCircuitCrossReferences[0].Index];
                RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[switchSection.OriginalIndex].SelectedRoute = switchSection.JunctionSetManual = (int)SwitchState;
                switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

                // update linked signals
                foreach (int signalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
                {
                    Simulator.Instance.SignalEnvironment.Signals[signalIndex].Update();
                }
                //junctionNode.SelectedRoute = Selection; //although the new signal system request Signals.RequestSetSwitch, client may just change
                if (User == multiPlayerManager.UserName && ManuallySet)//got the message with my name, will confirm with the player
                {
                    Simulator.Instance.Confirmer.Information(CatalogManager.Catalog.GetString("Switched, current route is {0}",
                        SwitchState == SwitchState.MainRoute ? CatalogManager.Catalog.GetString("main route") : CatalogManager.Catalog.GetString("side route")));
                }
            }
        }
    }
}
