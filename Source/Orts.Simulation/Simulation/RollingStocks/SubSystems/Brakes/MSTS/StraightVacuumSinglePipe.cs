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
            HandbrakePercent = handbrakeOn & (Car as MSTSWagon).HandBrakePresent? 100 : 0;
            (Car as MSTSWagon).NonAutoBrakePresent = true; // Set flag to indicate that non auto brake is set in train
        }

        public override void InitializeMoving() // used when initial speed > 0
        {

            BrakeLine1PressurePSI = (float)Pressure.Vacuum.ToPressure(Car.Train.EqualReservoirPressurePSIorInHg);
            CylPressurePSIA = (float)Pressure.Vacuum.ToPressure(Car.Train.EqualReservoirPressurePSIorInHg);
            HandbrakePercent = 0;
        }


        public override void Update(double elapsedClockSeconds)
        {


            if (BrakeLine1PressurePSI<CylPressurePSIA) // Increase BP pressure, hence vacuum brakes are being released
            {
                double dp = elapsedClockSeconds * MaxReleaseRatePSIpS;
                double vr = NumBrakeCylinders * BrakeCylVolM3 / BrakePipeVolumeM3;
                if (CylPressurePSIA - dp<BrakeLine1PressurePSI + dp* vr)
                    dp = (CylPressurePSIA - BrakeLine1PressurePSI) / (1 + vr);
                CylPressurePSIA -= (float)dp;

                //                if (LeadLoco == false)
                //                {
                //                    BrakeLine1PressurePSI += dp * vr;
                //                }
            }
            else if (BrakeLine1PressurePSI > CylPressurePSIA)  // Decrease BP pressure, hence vacuum brakes are being applied
            {
                double dp = elapsedClockSeconds * MaxApplicationRatePSIpS;
                double vr = NumBrakeCylinders * BrakeCylVolM3 / BrakePipeVolumeM3;
                if (CylPressurePSIA + dp > BrakeLine1PressurePSI - dp* vr)
                    dp = (BrakeLine1PressurePSI - CylPressurePSIA) / (1 + vr);
                CylPressurePSIA += (float)dp;
            }


            float f;
            if (!Car.BrakesStuck)
            {

                float brakecylinderfraction = ((OneAtmospherePSI - CylPressurePSIA) / MaxForcePressurePSI);
                brakecylinderfraction = MathHelper.Clamp(brakecylinderfraction, 0, 1);

                f = Car.MaxBrakeForceN* brakecylinderfraction;

                if (f<Car.MaxHandbrakeForceN* HandbrakePercent / 100)
                    f = Car.MaxHandbrakeForceN* HandbrakePercent / 100;
            }
            else
            {
                f = Math.Max(Car.MaxBrakeForceN, Car.MaxHandbrakeForceN / 2);
            }
            Car.BrakeRetardForceN = f* Car.BrakeShoeRetardCoefficientFrictionAdjFactor; // calculates value of force applied to wheel, independent of wheel skid
            if (Car.BrakeSkid) // Test to see if wheels are skiding due to excessive brake force
            {
                Car.BrakeForceN = f* Car.SkidFriction;   // if excessive brakeforce, wheel skids, and loses adhesion
            }
            else
            {
                Car.BrakeForceN = f* Car.BrakeShoeCoefficientFrictionAdjFactor; // In advanced adhesion model brake shoe coefficient varies with speed, in simple odel constant force applied as per value in WAG file, will vary with wheel skid.
            }
            //          Trace.TraceInformation("Straight Brake Force - CarID {0} BP1 {1} Cyl {2} MaxForce {3}", Car.CarID, BrakeLine1PressurePSI, (OneAtmospherePSI - CylPressuePSIA), MaxForcePressurePSI);
            //  Trace.TraceInformation("Straight Brake Force - CarID {0} BP1 {1} BrakeForce {2}", Car.CarID, BrakeLine1PressurePSI, Car.BrakeForceN);

            MSTSLocomotive lead = Car as MSTSLocomotive;

            // Calculate train pipe pressure at lead locomotive.
            if (lead != null)
            {
                float MaxVacuumPipeLevelPSI = lead.TrainBrakeController.MaxPressurePSI;
                float LargeEjectorChargingRateInHgpS = lead.LargeEjectorBrakePipeChargingRatePSIorInHgpS; // Set value for large ejector to operate - fraction set in steam locomotive
                //var brakePipeTimeFactorS = lead.BrakePipeTimeFactorS;

                //   float AdjLargeEjectorChargingRateInHgpS = (Me3.FromFt3(200.0f) / TrainCar.train  train.TotalTrainBrakeSystemVolumeM3) * lead.l  .LargeEjectorChargingRateInHgpS;

                // Straight brake is opposite of automatic brake, ie vacuum pipe goes from 14.503psi (0 InHg - Release) to 2.24 (25InHg - Apply)
                                    

                // Apply brakes - brakepipe has to have vacuum increased to max vacuum value (ie decrease psi), vacuum is created by large ejector control
                lead.BrakeSystem.BrakeLine1PressurePSI -= (float)elapsedClockSeconds* LargeEjectorChargingRateInHgpS;
                if (lead.BrakeSystem.BrakeLine1PressurePSI<(OneAtmospherePSI - MaxVacuumPipeLevelPSI))
                {
                    //                            Trace.TraceInformation("Apply - BP1 {0} LgEj {1}", lead.BrakeSystem.BrakeLine1PressurePSI, AdjLargeEjectorChargingRateInHgpS);
                    lead.BrakeSystem.BrakeLine1PressurePSI = OneAtmospherePSI - MaxVacuumPipeLevelPSI;

                }


                // Release brakes - brakepipe has to have brake pipe decreased back to atmospheric pressure to apply brakes (ie psi increases).
                if (lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.StraightApplyOn)
                {
                    //                            Trace.TraceInformation("Loop ### - BP1 {0} PipeVariation {1} ServiceTime {2}", lead.BrakeSystem.BrakeLine1PressurePSI, TrainPipeTimeVariationS, AdjBrakeServiceTimeFactorS);
                    lead.BrakeSystem.BrakeLine1PressurePSI *= (float)(1 + elapsedClockSeconds / lead.BrakeServiceTimeFactorS); ;
                    if (lead.BrakeSystem.BrakeLine1PressurePSI > OneAtmospherePSI)
                    {
                        lead.BrakeSystem.BrakeLine1PressurePSI = OneAtmospherePSI;
                        //                                Trace.TraceInformation("Release - BP1 {0}", lead.BrakeSystem.BrakeLine1PressurePSI);
                    }
                }

                // update values using vacuum single pipe class
                base.Update(elapsedClockSeconds);

            }
        }

        // This overides the information for each individual wagon in the extended HUD  
        public override string[] GetDebugStatus(Dictionary<BrakeSystemComponent, Pressure.Unit> units)
        {
            // display differently as a straight vacuum brake

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