using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Orts.Common;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct BrakeStatus
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public ControllerState State { get; }

        public int StatePercentage { get; }

        public BrakeStatus(ControllerState state, float fraction)
        { 
            State = state;
            StatePercentage = float.IsNaN(fraction) ? -1 : (int)(fraction * 100);
        }

        public static BrakeStatus Empty { get; } = new BrakeStatus(ControllerState.Dummy, float.NaN);

        public string ToShortString()
        {
            return State == ControllerState.Dummy && StatePercentage < 0
                ? string.Empty
                : StatePercentage < 0
                ? State.GetLocalizedDescription()
                : State == ControllerState.Dummy ? $"{StatePercentage:N0}%" : $"{State.GetLocalizedDescription()} {StatePercentage:N0}%";
        }
}
}
