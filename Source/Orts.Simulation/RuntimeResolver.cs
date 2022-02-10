using System;
using System.Collections.Generic;
using System.Text;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Simulation.Signalling;

namespace Orts.Simulation
{
    internal class RuntimeResolver : IRuntimeReferenceResolver
    {
        private readonly Simulator simulator = Simulator.Instance;

        public ISignal SignalById(int signalId)
        {
            return simulator.SignalEnvironment.Signals[Convert.ToInt32(signalId)] as ISignal;
        }

        public ISwitch SwitchByUId(uint switchId)
{
            return RuntimeData.Instance.TrackDB.TrackNodes[Convert.ToInt32(switchId)] as ISwitch;
        }
    }
}
