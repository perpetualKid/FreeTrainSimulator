//
// COPYRIGHT 2021 by the Open Rails project.
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

using Orts.Common;

namespace Orts.Scripting.Api.PowerSupply
{
    public abstract class LocomotivePowerSupply : PowerSupply
    {
        /// <summary>
        /// Current state of the main power supply
        /// Main power comes from the pantograph or the diesel generator
        /// </summary>
        public Func<PowerSupplyState> CurrentMainPowerSupplyState { get; set; }
        /// <summary>
        /// Current state of the auxiliary power supply
        /// Auxiliary power is used by auxiliary systems of a locomotive (such as ventilation or air compressor) and by systems of the cars (such as air conditionning)
        /// </summary>
        public Func<PowerSupplyState> CurrentAuxiliaryPowerSupplyState { get; set; }
        /// <summary>
        /// Current state of the cab power supply
        /// </summary>
        public Func<PowerSupplyState> CurrentCabPowerSupplyState { get; set; }
        /// <summary>
        /// Current state of the helper engines
        /// </summary>
        public Func<DieselEngineState> CurrentHelperEnginesState { get; set; }
        /// <summary>
        /// Current availability of the dynamic brake
        /// </summary>
        public Func<bool> CurrentDynamicBrakeAvailability { get; set; }
        /// <summary>
        /// Current throttle percentage
        /// </summary>
        public Func<float> ThrottlePercent { get; set; }
        /// <summary>
        /// Main supply power on delay
        /// </summary>
        public Func<float> PowerOnDelayS { get; set; }
        /// <summary>
        /// Auxiliary supply power on delay
        /// </summary>
        public Func<float> AuxPowerOnDelayS { get; set; }
        /// <summary>
        /// True if the master key is switched on
        /// </summary>
        public Func<bool> MasterKeyOn { get; set; }
        /// <summary>
        /// True if the electric train supply is switched on
        /// </summary>
        public Func<bool> ElectricTrainSupplySwitchOn { get; set; }
        /// <summary>
        /// True if the locomotive is not fitted with electric train supply
        /// </summary>
        public Func<bool> ElectricTrainSupplyUnfitted { get; set; }

        /// <summary>
        /// Sets the current state of the main power supply (power from the pantograph or the generator)
        /// Main power comes from the pantograph or the diesel generator
        /// </summary>
        public Action<PowerSupplyState> SetCurrentMainPowerSupplyState { get; set; }
        /// <summary>
        /// Sets the current state of the auxiliary power supply
        /// Auxiliary power is used by auxiliary systems of a locomotive (such as ventilation or air compressor) and by systems of the cars (such as air conditionning)
        /// </summary>
        public Action<PowerSupplyState> SetCurrentAuxiliaryPowerSupplyState { get; set; }
        /// <summary>
        /// Sets the current state of the cab power supply
        /// </summary>
        public Action<PowerSupplyState> SetCurrentCabPowerSupplyState { get; set; }
        /// <summary>
        /// Sets the current state of the electric train supply
        /// ETS is used by the systems of the cars (such as air conditionning)
        /// </summary>
        public Action<PowerSupplyState> SetCurrentElectricTrainSupplyState { get; set; }
        /// <summary>
        /// Sets the current availability of the dynamic brake
        /// </summary>
        public Action<bool> SetCurrentDynamicBrakeAvailability { get; set; }
        /// <summary>
        /// Sends an event to the master switch
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToMasterKey { get; set; }
        /// <summary>
        /// Sends an event to the electric train supply switch
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToElectricTrainSupplySwitch { get; set; }
        /// <summary>
        /// Sends an event to the train control system
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToTcs { get; set; }
        /// <summary>
        /// Sends an event to the train control system with a message
        /// </summary>
        public Action<PowerSupplyEvent, string> SignalEventToTcsWithMessage { get; set; }
        /// <summary>
        /// Sends an event to the power supplies of other locomotives
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToOtherLocomotives { get; set; }
        /// <summary>
        /// Sends an event to the power supplies of other locomotives
        /// </summary>
        public Action<PowerSupplyEvent, int> SignalEventToOtherLocomotivesWithId { get; set; }
        /// <summary>
        /// Sends an event to the power supplies of other train vehicles
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToOtherTrainVehicles { get; set; }
        /// <summary>
        /// Sends an event to the power supplies of other train vehicles
        /// </summary>
        public Action<PowerSupplyEvent, int> SignalEventToOtherTrainVehiclesWithId { get; set; }
        /// <summary>
        /// Sends an event to all helper engines
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToHelperEngines { get; set; }

        /// <summary>
        /// Called when the driver (or the train's systems) want something to happen on the power supply system
        /// </summary>
        /// <param name="evt">The event</param>
        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.ToggleHelperEngine:
                    switch (CurrentHelperEnginesState())
                    {
                        case DieselEngineState.Stopped:
                        case DieselEngineState.Stopping:
                            SignalEventToHelperEngines(PowerSupplyEvent.StartEngine);
                            Confirm(CabControl.HelperDiesel, CabSetting.On);
                            break;

                        case DieselEngineState.Starting:
                        case DieselEngineState.Running:
                            SignalEventToHelperEngines(PowerSupplyEvent.StopEngine);
                            Confirm(CabControl.HelperDiesel, CabSetting.Off);
                            break;
                    }
                    break;

                default:
                    base.HandleEvent(evt);

                    // By default, send the event to every component
                    SignalEventToMasterKey(evt);
                    SignalEventToElectricTrainSupplySwitch(evt);
                    SignalEventToTcs(evt);
                    SignalEventToOtherTrainVehicles(evt);
                    break;
            }
        }

        public override void HandleEvent(PowerSupplyEvent evt, int id)
        {
            base.HandleEvent(evt, id);

            // By default, send the event to every component
            SignalEventToOtherTrainVehiclesWithId(evt, id);
        }

        public override void HandleEventFromLeadLocomotive(PowerSupplyEvent evt)
        {
            base.HandleEventFromLeadLocomotive(evt);

            // By default, send the event to every component
            SignalEventToElectricTrainSupplySwitch(evt);
            SignalEventToTcs(evt);
        }
    }
}