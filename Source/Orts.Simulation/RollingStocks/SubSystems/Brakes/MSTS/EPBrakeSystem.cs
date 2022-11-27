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

using Orts.Common;
using Orts.Common.Calc;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{

    public class EPBrakeSystem : AirTwinPipe
    {
        public EPBrakeSystem(TrainCar car)
            : base(car)
        {
            debugBrakeType = "EP";
        }

        public override void Update(double elapsedClockSeconds)
        {
            MSTSLocomotive lead = car.Train.LeadLocomotive;
            float demandedAutoCylPressurePSI = 0;

            // Only allow EP brake tokens to operate if car is connected to an EP system
            if (lead == null || lead.BrakeSystem is not EPBrakeSystem)
            {
                holdingValve = ValveState.Release;
                base.Update(elapsedClockSeconds);
                return;
            }

            // process valid EP brake tokens

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
                float dp = (float)elapsedClockSeconds * maxApplicationRatePSIpS;
                if (BrakeLine2PressurePSI - dp * auxBrakeLineVolumeRatio / auxCylVolumeRatio < autoCylPressurePSI + dp)
                    dp = (BrakeLine2PressurePSI - autoCylPressurePSI) / (1 + auxBrakeLineVolumeRatio / auxCylVolumeRatio);
                if (dp > demandedAutoCylPressurePSI - autoCylPressurePSI)
                    dp = demandedAutoCylPressurePSI - autoCylPressurePSI;
                BrakeLine2PressurePSI -= dp * auxBrakeLineVolumeRatio / auxCylVolumeRatio;
                autoCylPressurePSI += dp;
            }
            brakeInfo.Update(null);
        }

        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            string s = Simulator.Catalog.GetString($" BC {FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true)}");
            if (handbrakePercent > 0)
                s += Simulator.Catalog.GetString($" Handbrake {handbrakePercent:F0}%");
            return s;
        }

        public override void Initialize(bool handbrakeOn, float maxPressurePSI, float fullServPressurePSI, bool immediateRelease)
        {
            base.Initialize(handbrakeOn, maxPressurePSI, fullServPressurePSI, immediateRelease);
            autoCylPressurePSI = Math.Max(autoCylPressurePSI, Math.Min(Math.Max(car.Train.BrakeSystem.BrakeLine4Pressure, 0), 1) * maxCylPressurePSI);
        }

        private protected override void UpdateBrakeStatus()
        {
            brakeInfo["BC"] = FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakeCylinder], true);
            brakeInfo["Handbrake"] = handbrakePercent > 0 ? $"{handbrakePercent:F0}%" : null;
            brakeInfo["Status"] = $"BC {brakeInfo["BC"]}";
            brakeInfo["StatusShort"] = $"BC{FormatStrings.FormatPressure(cylPressurePSI, Pressure.Unit.PSI, Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakeCylinder], false)}";
        }

    }
}
