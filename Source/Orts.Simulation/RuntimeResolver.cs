using System;

using FreeTrainSimulator.Common;

using Orts.Formats.Msts;
using Orts.Simulation.Track;

namespace Orts.Simulation
{
    internal class RuntimeResolver : IRuntimeReferenceResolver
    {
        private readonly Simulator simulator = Simulator.Instance;

        public ISignal SignalById(int signalId)
        {
            return simulator.SignalEnvironment.Signals[signalId];
        }

        public IJunction SwitchById(int junctionId)
{
            return TrackCircuitSection.TrackCircuitList[junctionId];
        }
    }
}
