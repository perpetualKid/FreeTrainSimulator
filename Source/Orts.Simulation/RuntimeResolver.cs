using System;

using Orts.Common;
using Orts.Formats.Msts;

namespace Orts.Simulation
{
    internal class RuntimeResolver : IRuntimeReferenceResolver
    {
        private readonly Simulator simulator = Simulator.Instance;

        public ISignal SignalById(int signalId)
        {
            return simulator.SignalEnvironment.Signals[signalId];
        }

        public ISwitch SwitchById(int switchId)
{
            return RuntimeData.Instance.TrackDB.TrackNodes[switchId] as ISwitch;
        }
    }
}
