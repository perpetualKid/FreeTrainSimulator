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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{
    /// <summary>
    /// Base class for a controllable power supply for an electric or dual-mode locmotive.
    /// </summary>
    public interface ILocomotivePowerSupply : IPowerSupply, ISaveStateApi<PowerSupplySaveState>
    {
        PowerSupplyType Type { get; }

        MasterKey MasterKey { get; }
        ElectricTrainSupplySwitch ElectricTrainSupplySwitch { get; }

        PowerSupplyState MainPowerSupplyState { get; }
        bool MainPowerSupplyOn { get; }
        bool DynamicBrakeAvailable { get; }

        PowerSupplyState AuxiliaryPowerSupplyState { get; }
        bool AuxiliaryPowerSupplyOn { get; }

        PowerSupplyState CabPowerSupplyState { get; }
        bool CabPowerSupplyOn { get; }

        bool ServiceRetentionButton { get; }
        bool ServiceRetentionCancellationButton { get; }
    }
}
