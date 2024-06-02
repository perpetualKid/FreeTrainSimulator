// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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
using Orts.Common.Calc;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{

    // Detailed description of the operation of a SME brake system can be found in:  "Air brakes, an up-to-date treatise on the Westinghouse air brake as designed for passenger and 
    // freight service and for electric cars" by Ludy, Llewellyn V., 1875- [from old catalog]; American Technical Society
    // https://archive.org/details/airbrakesuptodat00ludy/page/174/mode/2up?q=%22SME+brake%22

    public class SMEBrakeSystem : AirTwinPipe
    {
        public SMEBrakeSystem(TrainCar car)
            : base(car)
        {
            debugBrakeType = "SME";
        }

        public override void Update(double elapsedClockSeconds)
        {
            MSTSLocomotive lead = (MSTSLocomotive)car.Train.LeadLocomotive;
            double demandedAutoCylPressurePSI = 0;

            // Only allow SME brake tokens to operate if car is connected to an SME system
            if (lead == null || lead.BrakeSystem is not SMEBrakeSystem)
            {
                holdingValve = ValveState.Release;
                base.Update(elapsedClockSeconds);
                return;
            }

            // process valid SME brake tokens

            if (BrakeLine3PressurePSI >= 1000f || car.Train.BrakeSystem.BrakeLine4Pressure < 0)
            {
                holdingValve = ValveState.Release;
            }
            else if (car.Train.BrakeSystem.BrakeLine4Pressure == 0)
            {
                holdingValve = ValveState.Lap;
            }
            else
            {
                demandedAutoCylPressurePSI = Math.Min(Math.Max(car.Train.BrakeSystem.BrakeLine4Pressure, 0), 1) * maxCylPressurePSI;
                holdingValve = autoCylPressurePSI <= demandedAutoCylPressurePSI ? ValveState.Lap : ValveState.Release;
            }

            base.Update(elapsedClockSeconds); // Allow processing of other valid tokens

            if (autoCylPressurePSI < demandedAutoCylPressurePSI && !car.WheelBrakeSlideProtectionActive)
            {
                double dp = (float)elapsedClockSeconds * maxApplicationRatePSIpS;
                if (BrakeLine2PressurePSI - dp * auxBrakeLineVolumeRatio / auxCylVolumeRatio < autoCylPressurePSI + dp)
                    dp = (BrakeLine2PressurePSI - autoCylPressurePSI) / (1 + auxBrakeLineVolumeRatio / auxCylVolumeRatio);
                if (dp > demandedAutoCylPressurePSI - autoCylPressurePSI)
                    dp = demandedAutoCylPressurePSI - autoCylPressurePSI;
                BrakeLine2PressurePSI -= (float)dp * auxBrakeLineVolumeRatio / auxCylVolumeRatio;
                autoCylPressurePSI += (float)dp;
            }
            brakeInformation.Update(null);
        }

        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            var s = $" {Simulator.Catalog.GetString("BC")} {FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true)}";
            if (handbrakePercent > 0)
                s += $" {Simulator.Catalog.GetString("Handbrake")} {handbrakePercent:F0}%";
            return s;
        }

        private protected override void UpdateBrakeStatus()
        {
            base.UpdateBrakeStatus();
            brakeInformation["BrakeType"] = "SME";

            brakeInformation["BC"] = FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakeCylinder], true);
            brakeInformation["Handbrake"] = handbrakePercent > 0 ? $"{handbrakePercent:F0}%" : null;
            brakeInformation["Status"] = $"BC {brakeInformation["BC"]}";
            brakeInformation["StatusShort"] = $"BP{FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakePipe], false)}";
        }
    }
}
