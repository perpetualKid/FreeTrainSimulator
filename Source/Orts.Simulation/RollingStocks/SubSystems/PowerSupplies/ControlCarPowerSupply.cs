﻿// COPYRIGHT 2020 by the Open Rails project.
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

using Orts.Common;
using Orts.Scripting.Api.PowerSupply;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{

    public class ScriptedControlCarPowerSupply : ScriptedLocomotivePowerSupply, ISubSystem<ScriptedControlCarPowerSupply>
    {
        public MSTSControlTrailerCar ContolTrailer => Locomotive as MSTSControlTrailerCar;

        public override PowerSupplyType Type => PowerSupplyType.ControlCar;

        public bool Activated;
        private ControlCarPowerSupply Script => abstractScript as ControlCarPowerSupply;

        //        public ScriptedTractionCutOffRelay TractionCutOffRelay { get; protected set; }

        public ScriptedControlCarPowerSupply(MSTSControlTrailerCar controlcar) :
        base(controlcar)
        {
            //           ControlTrailer = controlcar;
            //          TractionCutOffRelay = new ScriptedTractionCutOffRelay(this);
        }


        public void Copy(ScriptedControlCarPowerSupply source)
        {
            base.Copy(source);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        //================================================================================================//
        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        public override void InitializeMoving()
        {
            base.InitializeMoving();


        }

        public override void Update(double elapsedClockSeconds)
        {
            base.Update(elapsedClockSeconds);

            // Script?.Update(elapsedClockSeconds);
        }

    }
}
