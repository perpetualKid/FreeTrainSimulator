// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public class StraightVacuumSinglePipe : VacuumSinglePipe
    {
        public StraightVacuumSinglePipe(TrainCar car)
            : base(car)
        {

        }


        public override void Initialize(bool handbrakeOn, float maxVacuumInHg, float fullServVacuumInHg, bool immediateRelease)
        {
            CylPressurePSIA = BrakeLine1PressurePSI = (float)Pressure.Vacuum.ToPressure(fullServVacuumInHg);
            HandbrakePercent = handbrakeOn & (Car as MSTSWagon).HandBrakePresent ? 100 : 0;
        }

        public override void InitializeMoving() // used when initial speed > 0
        {

            BrakeLine1PressurePSI = (float)Pressure.Vacuum.ToPressure(Car.Train.EqualReservoirPressurePSIorInHg);
            CylPressurePSIA = (float)Pressure.Vacuum.ToPressure(Car.Train.EqualReservoirPressurePSIorInHg);
            HandbrakePercent = 0;
        }


        public override void Update(double elapsedClockSeconds)
        {
            MSTSLocomotive lead = (MSTSLocomotive)Car.Train.LeadLocomotive;

            if (lead != null)
            {

                if (lead.CarBrakeSystemType == "straight_vacuum_single_pipe") // straight braked cars will have separate calculations done  
                {
                    (Car as MSTSWagon).NonAutoBrakePresent = true; // Set flag to indicate that non auto brake is set in train

                    if (BrakeLine1PressurePSI < CylPressurePSIA) // Increase BP pressure, hence vacuum brakes are being released
                    {
                        double dp = elapsedClockSeconds * MaxReleaseRatePSIpS;
                        double vr = NumBrakeCylinders * BrakeCylVolM3 / BrakePipeVolumeM3;
                        if (CylPressurePSIA - dp < BrakeLine1PressurePSI + dp * vr)
                            dp = (CylPressurePSIA - BrakeLine1PressurePSI) / (1 + vr);
                        CylPressurePSIA -= (float)dp;

                    }
                    else if (BrakeLine1PressurePSI > CylPressurePSIA)  // Decrease BP pressure, hence vacuum brakes are being applied
                    {
                        double dp = elapsedClockSeconds * MaxApplicationRatePSIpS;
                        double vr = NumBrakeCylinders * BrakeCylVolM3 / BrakePipeVolumeM3;
                        if (CylPressurePSIA + dp > BrakeLine1PressurePSI - dp * vr)
                            dp = (BrakeLine1PressurePSI - CylPressurePSIA) / (1 + vr);
                        CylPressurePSIA += (float)dp;
                    }


                    float f;
                    if (!Car.BrakesStuck)
                    {

                        float brakecylinderfraction = ((OneAtmospherePSI - CylPressurePSIA) / MaxForcePressurePSI);
                        brakecylinderfraction = MathHelper.Clamp(brakecylinderfraction, 0, 1);

                        f = Car.MaxBrakeForceN * brakecylinderfraction;

                        if (f < Car.MaxHandbrakeForceN * HandbrakePercent / 100)
                            f = Car.MaxHandbrakeForceN * HandbrakePercent / 100;
                    }
                    else
                    {
                        f = Math.Max(Car.MaxBrakeForceN, Car.MaxHandbrakeForceN / 2);
                    }
                    Car.BrakeRetardForceN = f * Car.BrakeShoeRetardCoefficientFrictionAdjFactor; // calculates value of force applied to wheel, independent of wheel skid
                    if (Car.BrakeSkid) // Test to see if wheels are skiding due to excessive brake force
                    {
                        Car.BrakeForceN = f * Car.SkidFriction;   // if excessive brakeforce, wheel skids, and loses adhesion
                    }
                    else
                    {
                        Car.BrakeForceN = f * Car.BrakeShoeCoefficientFrictionAdjFactor; // In advanced adhesion model brake shoe coefficient varies with speed, in simple odel constant force applied as per value in WAG file, will vary with wheel skid.
                    }



                    float MaxVacuumPipeLevelPSI = lead.TrainBrakeController.MaxPressurePSI;
                    // Set value for large ejector to operate - in this instance as there is no small ejector, the whole brake pipe charging rate is used.
                    float LargeEjectorChargingRateInHgpS = lead.BrakePipeChargingRatePSIorInHgpS;

                    // Calculate train pipe pressure at lead locomotive.

                    // Calculate adjustment times for varying lengths of trains
                    float AdjLargeEjectorChargingRateInHgpS;
                    if (lead.LargeSteamEjectorIsOn)
                    {
                        AdjLargeEjectorChargingRateInHgpS = (float)(Size.Volume.FromFt3(200.0f) / Car.Train.TotalTrainBrakeCylinderVolumeM3) * LargeEjectorChargingRateInHgpS;
                    }
                    else
                    {

                        AdjLargeEjectorChargingRateInHgpS = 0;
                    }
                    float AdjBrakeServiceTimeFactorS = (Car.Train.TotalTrainBrakeSystemVolumeM3 / (float)Size.Volume.FromFt3(200.0f)) * lead.BrakeServiceTimeFactorS;
                    float AdjTrainPipeLeakLossPSI = (Car.Train.TotalTrainBrakeSystemVolumeM3 / (float)Size.Volume.FromFt3(200.0f)) * lead.TrainBrakePipeLeakPSIorInHgpS;

                    // Straight brake is opposite of automatic brake, ie vacuum pipe goes from 14.503psi (0 InHg - Release) to 2.24 (25InHg - Apply)

                    // Apply brakes - brakepipe has to have vacuum increased to max vacuum value (ie decrease psi), vacuum is created by large ejector control

                    lead.BrakeSystem.BrakeLine1PressurePSI -= (float)elapsedClockSeconds * AdjLargeEjectorChargingRateInHgpS;
                    if (lead.BrakeSystem.BrakeLine1PressurePSI < (OneAtmospherePSI - MaxVacuumPipeLevelPSI))
                    {
                        lead.BrakeSystem.BrakeLine1PressurePSI = OneAtmospherePSI - MaxVacuumPipeLevelPSI;
                    }

                    // Release brakes - brakepipe has to have brake pipe decreased back to atmospheric pressure to apply brakes (ie psi increases).
                    if (lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.StraightReleaseOn)
                    {

                        lead.BrakeSystem.BrakeLine1PressurePSI *= (float)(1 + elapsedClockSeconds / AdjBrakeServiceTimeFactorS); ;
                        if (lead.BrakeSystem.BrakeLine1PressurePSI > OneAtmospherePSI)
                        {
                            lead.BrakeSystem.BrakeLine1PressurePSI = OneAtmospherePSI;
                        }
                    }

                    // leaks in train pipe will reduce vacuum (increase pressure)
                    lead.BrakeSystem.BrakeLine1PressurePSI += (float)elapsedClockSeconds * AdjTrainPipeLeakLossPSI;

                    // Keep brake line within relevant limits - ie between 21 or 25 InHg and Atmospheric pressure.
                    lead.BrakeSystem.BrakeLine1PressurePSI = MathHelper.Clamp(lead.BrakeSystem.BrakeLine1PressurePSI, OneAtmospherePSI - MaxVacuumPipeLevelPSI, OneAtmospherePSI);

                }

                if (lead.CarBrakeSystemType == "straight_vacuum_single_pipe" || ((lead.CarBrakeSystemType == "vacuum_single_pipe" || lead.CarBrakeSystemType == "vacuum_twin_pipe") && (Car as MSTSWagon).AuxiliaryReservoirPresent))
                {
                    // update non calculated values using vacuum single pipe class
                    base.Update(elapsedClockSeconds);
                }
            }
        }

        // This overides the information for each individual wagon in the extended HUD  
        public override string[] GetDebugStatus(Dictionary<BrakeSystemComponent, Pressure.Unit> units)
        {

            if (!(Car as MSTSWagon).NonAutoBrakePresent)
            {
                // display as a automatic vacuum brake

                return new string[] {
                "1VS",
                FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(CylPressurePSIA), Pressure.Unit.InHg, Pressure.Unit.InHg, true),
                FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(BrakeLine1PressurePSI), Pressure.Unit.InHg, Pressure.Unit.InHg, true),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                HandbrakePercent > 0 ? $"{HandbrakePercent:F0}%" : string.Empty,
                FrontBrakeHoseConnected? "I" : "T",
                $"A{(AngleCockAOpen? "+" : "-")} B{(AngleCockBOpen? "+" : "-")}",
                };
            }
            else
            {
                // display as a straight vacuum brake

                return new string[] {
                "1VS",
                FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(CylPressurePSIA), Pressure.Unit.InHg, Pressure.Unit.InHg, true),
                FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(BrakeLine1PressurePSI), Pressure.Unit.InHg, Pressure.Unit.InHg, true),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                HandbrakePercent > 0 ? $"{HandbrakePercent:F0}%" : string.Empty,
                FrontBrakeHoseConnected? "I" : "T",
                $"A{(AngleCockAOpen? "+" : "-")} B{(AngleCockBOpen? "+" : "-")}",
                };
            }
        }

    }
}