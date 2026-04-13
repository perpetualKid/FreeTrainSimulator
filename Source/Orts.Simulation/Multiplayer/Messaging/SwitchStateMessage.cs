using System.Collections.ObjectModel;
using System.Linq;

using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using MemoryPack;

using Orts.Simulation.Physics;
using Orts.Simulation.Track;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class SwitchStateMessage : MultiPlayerMessageContent
    {
        public Collection<(int JunctionIndex, int SwitchState)> SwitchStates { get; private set; }

        [MemoryPackConstructor]
        public SwitchStateMessage() { }

        public SwitchStateMessage(bool initialize) 
        {
            if (initialize)
            {
                SwitchStates = new Collection<(int JunctionIndex, int SwitchState)>
                    (RuntimeDataResolver.Instance.TrackWorld.SwitchStates.Select(switchState => (switchState.Key, switchState.Value)).ToList());
            }
        }

        public override void HandleMessage()
        {
            foreach ((int JunctionIndex, int SwitchState) in SwitchStates ?? Enumerable.Empty<(int JunctionIndex, int SwitchState)>())
            {
                SetSwitch(JunctionIndex, SwitchState);
            }
        }

        private static void SetSwitch(int junctionNodeIndex, int desiredState)
        {
            if (RuntimeDataResolver.Instance.TrackWorld.SwitchStates[junctionNodeIndex] != desiredState)
            {
                if (!SwitchOccupiedByPlayerTrain(junctionNodeIndex))
                {
                    TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[Simulator.Instance.SignalEnvironment.NodeCrossReferences[junctionNodeIndex][0].Index];
                    switchSection.JunctionSetManual = desiredState;
                    RuntimeDataResolver.Instance.TrackWorld.SwitchStates[junctionNodeIndex] = desiredState;
                    switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

                    // update linked signals
                    foreach (int signalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
                    {
                        Simulator.Instance.SignalEnvironment.Signals[signalIndex].Update();
                    }
                }
            }
        }

        private static bool SwitchOccupiedByPlayerTrain(int junctionNodeIndex)
        {
            Train train = Simulator.Instance.PlayerLocomotive?.Train;
            if (train == null)
                return false;
            if (train.FrontTrackNodeIndex == train.RearTrackNodeIndex)
                return false;

            if (train.RearTrackTraveller is TrackTraveller rtt)
            {
                TrackTraveller walker = rtt;
                while (true)
                {
                    (JunctionNode Junction, VectorNode ApproachNode)? jResult = walker.NextJunction();
                    if (!jResult.HasValue)
                        return false;
                    if (jResult.Value.Junction.NodeIndex == junctionNodeIndex)
                        return true;
                    // Advance past the junction to the next VectorNode
                    int currentNodeIdx = walker.TrackNodeIndex;
                    while (walker.TrackNodeIndex == currentNodeIdx)
                    {
                        TrackTraveller? next = walker.AdvanceToNextSection();
                        if (!next.HasValue)
                            return false;
                        walker = next.Value;
                    }
                    if (walker.TrackNodeIndex == train.FrontTrackNodeIndex)
                        return false;
                }
            }
            return false;
        }

    }
}
