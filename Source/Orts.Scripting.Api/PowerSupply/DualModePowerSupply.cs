//// COPYRIGHT 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;

using FreeTrainSimulator.Common;

namespace Orts.Scripting.Api.PowerSupply
{
    /// <summary>
    /// Power supply for dual mode locomotives
    /// </summary>
    public abstract class DualModePowerSupply : ElectricPowerSupply
    {
        /// <summary>
        /// Current state of the diesel engine
        /// </summary>
        public Func<DieselEngineState> CurrentDieselEngineState { get; set; }
        /// <summary>
        /// Current state of the circuit breaker
        /// </summary>
        public Func<TractionCutOffRelayState> CurrentTractionCutOffRelayState { get; set; }
        /// <summary>
        /// Driver's closing order of the traction cut-off relay
        /// </summary>
        public Func<bool> TractionCutOffRelayDriverClosingOrder { get; set; }
        /// <summary>
        /// Driver's opening order of the traction cut-off relay
        /// </summary>
        public Func<bool> TractionCutOffRelayDriverOpeningOrder { get; set; }
        /// <summary>
        /// Driver's closing authorization of the traction cut-off relay
        /// </summary>
        public Func<bool> TractionCutOffRelayDriverClosingAuthorization { get; set; }
        /// <summary>
        /// Current mode of the power supply
        /// </summary>
        public Func<PowerSupplyMode> CurrentPowerSupplyMode { get; set; }

        /// <summary>
        /// Sets current mode of the power supply
        /// </summary>
        public Action<PowerSupplyMode> SetCurrentPowerSupplyMode { get; set; }
        /// <summary>
        /// Sends an event to the traction cut-off relay
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToTractionCutOffRelay { get; set; }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            base.HandleEvent(evt);

            // By default, send the event to every component
            SignalEventToTractionCutOffRelay(evt);
        }
    }

}
