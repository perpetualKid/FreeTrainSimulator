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
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public class ManualBraking : MSTSBrakeSystem
    {
        private string brakeType;

        public ManualBraking(TrainCar car) : base(car)
        {
        }

        private float ManualMaxBrakeValue = 100.0f;
        private float ManualReleaseRateValuepS;
        private float ManualMaxApplicationRateValuepS;
        private float ManualBrakingDesiredFraction;
        private float EngineBrakeDesiredFraction;
        private float ManualBrakingCurrentFraction;
        private float EngineBrakingCurrentFraction;
        private float SteamBrakeCompensation;
        private bool LocomotiveSteamBrakeFitted;
        private float SteamBrakePressurePSI;
        private float SteamBrakeCylinderPressurePSI;
        private float BrakeForceFraction;

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(maxreleaserate":
                    ManualReleaseRateValuepS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
                case "wagon(maxapplicationrate":
                    ManualMaxApplicationRateValuepS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;
            }
        }

        public override void InitializeFrom(BrakeSystem source)
        {
            if (source is not ManualBraking manualBraking)
                throw new ArgumentNullException(nameof(source));
            ManualMaxApplicationRateValuepS = manualBraking.ManualMaxApplicationRateValuepS;
            ManualReleaseRateValuepS = manualBraking.ManualReleaseRateValuepS;

        }
        public override ValueTask<BrakeSystemSaveState> Snapshot()
        {
            return ValueTask.FromResult(new BrakeSystemSaveState()
            {
                ManualBraking = ManualBrakingCurrentFraction,
            });
        }

        public override ValueTask Restore(BrakeSystemSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ManualBrakingCurrentFraction = saveState.ManualBraking;

            return ValueTask.CompletedTask;
        }

        public override void Initialize(bool handbrakeOn, float maxPressurePSI, float fullServPressurePSI, bool immediateRelease)
        {
            brakeType = (car as MSTSWagon).ManualBrakePresent ? "M" : "-";

            // Changes brake type if locomotive fitted with steam brakes
            if (car is MSTSSteamLocomotive steamLocomotive && steamLocomotive.SteamEngineBrakeFitted)
            {
                brakeType = "S";
            }
            else if (car.WagonType == WagonType.Tender)// Changes brake type if tender fitted with steam brakes
            {
                MSTSWagon wagonid = car as MSTSWagon;
                // Find the associated steam locomotive for this tender
                if (wagonid.TendersSteamLocomotive == null)
                    wagonid.FindTendersSteamLocomotive();

                if (wagonid.TendersSteamLocomotive != null && wagonid.TendersSteamLocomotive.SteamEngineBrakeFitted) // if steam brakes are fitted to the associated locomotive, then add steam brakes here.
                {
                    brakeType = "S";
                }
            }
        }

        public override void Update(double elapsedClockSeconds)
        {
            MSTSLocomotive lead = car.Train.LeadLocomotive;
            float BrakemanBrakeSettingValue = 0;
            ManualBrakingDesiredFraction = 0;

            SteamBrakeCompensation = 1.0f;

            // Process manual braking on all cars
            if (lead != null)
            {
                BrakemanBrakeSettingValue = lead.BrakemanBrakeController.CurrentValue;
            }

            ManualBrakingDesiredFraction = BrakemanBrakeSettingValue * ManualMaxBrakeValue;

            if (ManualBrakingCurrentFraction < ManualBrakingDesiredFraction)
            {
                ManualBrakingCurrentFraction += ManualMaxApplicationRateValuepS;
                if (ManualBrakingCurrentFraction > ManualBrakingDesiredFraction)
                {
                    ManualBrakingCurrentFraction = ManualBrakingDesiredFraction;
                }

            }
            else if (ManualBrakingCurrentFraction > ManualBrakingDesiredFraction)
            {
                ManualBrakingCurrentFraction -= ManualReleaseRateValuepS;
                if (ManualBrakingCurrentFraction < 0)
                {
                    ManualBrakingCurrentFraction = 0;
                }

            }

            BrakeForceFraction = ManualBrakingCurrentFraction / ManualMaxBrakeValue;

            // If car is a locomotive or tender, then process engine brake
            if (car.WagonType == WagonType.Engine || car.WagonType == WagonType.Tender) // Engine brake
            {
                if (lead != null)
                {
                    float EngineBrakeSettingValue = lead.EngineBrakeController.CurrentValue;
                    if (lead.SteamEngineBrakeFitted)
                    {
                        LocomotiveSteamBrakeFitted = true;
                        EngineBrakeDesiredFraction = EngineBrakeSettingValue * lead.MaxBoilerPressurePSI;
                    }
                    else
                    {
                        EngineBrakeDesiredFraction = EngineBrakeSettingValue * ManualMaxBrakeValue;
                    }


                    if (EngineBrakingCurrentFraction < EngineBrakeDesiredFraction)
                    {

                        EngineBrakingCurrentFraction += (float)(elapsedClockSeconds * lead.EngineBrakeController.ApplyRatePSIpS);
                        if (EngineBrakingCurrentFraction > EngineBrakeDesiredFraction)
                        {
                            EngineBrakingCurrentFraction = EngineBrakeDesiredFraction;
                        }

                    }
                    else if (EngineBrakingCurrentFraction > EngineBrakeDesiredFraction)
                    {
                        EngineBrakingCurrentFraction -= (float)(elapsedClockSeconds * lead.EngineBrakeController.ReleaseRatePSIpS);
                        if (EngineBrakingCurrentFraction < 0)
                        {
                            EngineBrakingCurrentFraction = 0;
                        }
                    }

                    if (lead.SteamEngineBrakeFitted)
                    {
                        SteamBrakeCompensation = lead.BoilerPressurePSI / lead.MaxBoilerPressurePSI;
                        SteamBrakePressurePSI = EngineBrakeSettingValue * SteamBrakeCompensation * lead.MaxBoilerPressurePSI;
                        SteamBrakeCylinderPressurePSI = EngineBrakingCurrentFraction * SteamBrakeCompensation; // For display purposes
                        BrakeForceFraction = EngineBrakingCurrentFraction / lead.MaxBoilerPressurePSI; // Manual braking value overwritten by engine calculated value
                    }
                    else
                    {
                        BrakeForceFraction = EngineBrakingCurrentFraction / ManualMaxBrakeValue;
                    }
                }
            }

            float f;
            if (!car.BrakesStuck)
            {
                f = car.MaxBrakeForceN * Math.Min(BrakeForceFraction, 1);
                if (f < car.MaxHandbrakeForceN * handbrakePercent / 100)
                    f = car.MaxHandbrakeForceN * handbrakePercent / 100;
            }
            else
                f = Math.Max(car.MaxBrakeForceN, car.MaxHandbrakeForceN / 2);
            car.SetBrakeForce(f);
            brakeInformation.Update(null);
        }

        // Get the brake BC & BP for EOT conditions
        public override string GetStatus(EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            string s = Simulator.Catalog.GetString("Manual Brake");
            return s;
        }

        // Get Brake information for train
        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            string s = Simulator.Catalog.GetString("Manual Brake");
            return s;
        }

        // Required to override BrakeSystem
        public override void AISetPercent(float percent)
        {
            if (percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            //  car.Train.EqualReservoirPressurePSIorInHg = Vac.FromPress(Const.OneAtmospherePSI - MaxForcePressurePSI * (1 - percent / 100));
        }

        public override float GetCylPressurePSI()
        {
            if (LocomotiveSteamBrakeFitted)
            {
                return SteamBrakeCylinderPressurePSI;
            }
            else
            {
                return ManualBrakingCurrentFraction;
            }
        }

        public override float GetCylVolumeM3()
        {
            return 0;
        }

        public override float VacResVolume => 0;

        public override float VacBrakeCylNumber => 0;


        public override float VacResPressurePSI => 0;

        public override bool IsBraking()
        {
            return false;
        }

        public override void CorrectMaxCylPressurePSI(MSTSLocomotive loco)
        {

        }

        public override void SetRetainer(RetainerSetting setting)
        {
        }

        public override float InternalPressure(float realPressure)
        {
            return (float)Pressure.Vacuum.ToPressure(realPressure);
        }

        public override void PropagateBrakePressure(double elapsedClockSeconds)
        {

        }

        public override void InitializeMoving() // used when initial speed > 0
        {

        }

        public override void LocoInitializeMoving() // starting conditions when starting speed > 0
        {

        }

        private protected override void UpdateBrakeStatus()
        {
            brakeInformation["Car"] = car.CarID;
            brakeInformation["BrakeType"] = brakeType;
            brakeInformation["Handbrake"] = handbrakePercent > 0 ? $"{handbrakePercent:F0}%" : null;

            brakeInformation["BP"] = (car as MSTSWagon).ManualBrakePresent && LocomotiveSteamBrakeFitted ?
                FormatStrings.FormatPressure(SteamBrakeCylinderPressurePSI, Pressure.Unit.PSI, Pressure.Unit.PSI, true) :
                (car as MSTSWagon).ManualBrakePresent ? $"{ManualBrakingCurrentFraction:F0} %" : null;

            brakeInformation["Manual Brake"] = Simulator.Catalog.GetString("Manual Brake");
            brakeInformation["Status"] = $"{brakeInformation["Manual Brake"]}";
            brakeInformation["StatusShort"] = Simulator.Catalog.GetParticularString("Braking", "Manual");
        }
    }
}