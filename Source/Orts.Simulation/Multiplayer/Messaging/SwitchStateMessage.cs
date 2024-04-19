using System.Collections.ObjectModel;
using System.Linq;

using MemoryPack;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Physics;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class SwitchStateMessage : MultiPlayerMessageContent
    {
        public Collection<(int JunctionIndex, int SwitchState)> SwitchStates {  get; set; } = new Collection<(int JunctionIndex, int SwitchState)> ();

        [MemoryPackConstructor]
        public SwitchStateMessage() { }

        public SwitchStateMessage(bool initialize) 
        {
            if (initialize)
            {
                foreach (TrackJunctionNode trackJunctionNode in RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes)
                {
                    SwitchStates.Add((trackJunctionNode.Index, trackJunctionNode.SelectedRoute));
                }
            }
        }

        public override void HandleMessage()
        {
            foreach ((int JunctionIndex, int SwitchState) item in SwitchStates)
            {
                SetSwitch(item.JunctionIndex, item.SwitchState);
            }
        }

        private static void SetSwitch(int junctionNodeIndex, int desiredState)
        {
            TrackJunctionNode junctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[junctionNodeIndex];
            if (junctionNode.SelectedRoute != desiredState)
            {
                if (!SwitchOccupiedByPlayerTrain(junctionNode))
                {
                    TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[junctionNode.TrackCircuitCrossReferences[0].Index];
                    RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[switchSection.OriginalIndex].SelectedRoute = switchSection.JunctionSetManual = desiredState;
                    switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

                    // update linked signals
                    foreach (int signalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
                    {
                        Simulator.Instance.SignalEnvironment.Signals[signalIndex].Update();
                    }
                }
            }
        }

        private static bool SwitchOccupiedByPlayerTrain(TrackJunctionNode junctionNode)
        {
            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train == null)
                return false;
            if (train.FrontTDBTraveller.TrackNode.Index == train.RearTDBTraveller.TrackNode.Index)
                return false;
            Traveller traveller = new Traveller(train.RearTDBTraveller);
            while (traveller.NextSection())
            {
                if (traveller.TrackNode.Index == train.FrontTDBTraveller.TrackNode.Index)
                    break;
                if (traveller.TrackNode == junctionNode)
                    return true;
            }
            return false;
        }

    }
}
