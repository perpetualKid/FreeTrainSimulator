﻿// COPYRIGHT 2021 by the Open Rails project.
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
    public abstract class PassengerCarPowerSupply : PowerSupply
    {
        /// <summary>
        /// Current state of the ventilation system
        /// </summary>
        public Func<PowerSupplyState> CurrentVentilationState { get; set; }
        /// <summary>
        /// Current state of the heating system
        /// </summary>
        public Func<PowerSupplyState> CurrentHeatingState { get; set; }
        /// <summary>
        /// Current state of the air conditioning system
        /// </summary>
        public Func<PowerSupplyState> CurrentAirConditioningState { get; set; }
        /// <summary>
        /// Current power consumed on the electric power supply line
        /// </summary>
        public Func<float> CurrentElectricTrainSupplyPowerW { get; set; }
        /// <summary>
        /// Current thermal power generated by the heating and air conditioning systems
        /// Positive if heating
        /// Negative if air conditioning (cooling)
        /// </summary>
        public Func<float> CurrentHeatFlowRateW { get; set; }
        /// <summary>
        /// Systems power on delay when electric train supply has been switched on
        /// </summary>
        public Func<float> PowerOnDelayS { get; set; }
        /// <summary>
        /// Power consumed all the time on the electric train supply line
        /// </summary>
        public Func<float> ContinuousPowerW { get; set; }
        /// <summary>
        /// Power consumed when heating is on
        /// </summary>
        public Func<float> HeatingPowerW { get; set; }
        /// <summary>
        /// Power consumed when air conditioning is on
        /// </summary>
        public Func<float> AirConditioningPowerW { get; set; }
        /// <summary>
        /// Yield of the air conditioning system
        /// </summary>
        public Func<float> AirConditioningYield { get; set; }
        /// <summary>
        /// Desired temperature inside the passenger car
        /// </summary>
        public Func<float> DesiredTemperatureC { get; set; }
        /// <summary>
        /// Current temperature inside the passenger car
        /// </summary>
        public Func<float> InsideTemperatureC { get; set; }
        /// <summary>
        /// Current temperature outside the passenger car
        /// </summary>
        public Func<float> OutsideTemperatureC { get; set; }

        /// <summary>
        /// Sets the current state of the ventilation system
        /// </summary>
        public Action<PowerSupplyState> SetCurrentVentilationState { get; set; }
        /// <summary>
        /// Sets the current state of the heating system
        /// </summary>
        public Action<PowerSupplyState> SetCurrentHeatingState { get; set; }
        /// <summary>
        /// Sets the current state of the air conditioning system
        /// </summary>
        public Action<PowerSupplyState> SetCurrentAirConditioningState { get; set; }
        /// <summary>
        /// Sets the current power consumed on the electric power supply line
        /// </summary>
        public Action<float> SetCurrentElectricTrainSupplyPowerW { get; set; }
        /// <summary>
        /// Sets the current thermal power generated by the heating and air conditioning systems
        /// Positive if heating
        /// Negative if air conditioning (cooling)
        /// </summary>
        public Action<float> SetCurrentHeatFlowRateW { get; set; }
    }
}