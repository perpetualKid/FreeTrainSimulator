// COPYRIGHT 2014 by the Open Rails project.
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
using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public class SingleTransferPipe : AirSinglePipe
    {
        public SingleTransferPipe(TrainCar car)
            : base(car)
        {
            debugBrakeType = "-";
        }

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);
            switch (lowercasetoken)
            {
                // OpenRails specific parameters
                case "wagon(brakepipevolume":
                    BrakePipeVolumeM3 = (float)Size.Volume.FromFt3(stf.ReadFloatBlock(STFReader.Units.VolumeDefaultFT3, null));
                    break;
            }
        }

        public override void Initialize(bool handbrakeOn, float maxPressurePSI, float fullServPressurePSI, bool immediateRelease)
        {
            base.Initialize(handbrakeOn, 0, 0, true);
            auxResPressurePSI = 0;
            emergResPressurePSI = 0;
            (car as MSTSWagon).RetainerPositions = 0;
            (car as MSTSWagon).EmergencyReservoirPresent = false;
            // Calculate brake pipe size depending upon whether vacuum or air braked
            if (car.BrakeSystemType == Formats.Msts.BrakeSystemType.VacuumPiped)
            {
                BrakePipeVolumeM3 = (0.050f * 0.050f * (float)Math.PI / 4f) * Math.Max(5.0f, (1 + car.CarLengthM)); // Using (2") pipe
            }
            else // air braked by default
            {
                BrakePipeVolumeM3 = (0.032f * 0.032f * (float)Math.PI / 4f) * Math.Max(5.0f, (1 + car.CarLengthM)); // Using DN32 (1-1/4") pipe
            }
        }

        public override void InitializeFrom(BrakeSystem source)
        {
            BrakePipeVolumeM3 = (source as SingleTransferPipe)?.BrakePipeVolumeM3 ?? throw new ArgumentNullException(nameof(source));
        }

        public override string GetStatus(EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            ArgumentNullException.ThrowIfNull(units);
            // display differently depending upon whether vacuum or air braked system
            if (car.BrakeSystemType == Formats.Msts.BrakeSystemType.VacuumPiped)
            {
                return Simulator.Catalog.GetString($" BP {FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(BrakeLine1PressurePSI), Pressure.Unit.InHg, Pressure.Unit.InHg, false)}");
            }
            else  // air braked by default
            {
                return Simulator.Catalog.GetString($"BP {FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], true)}");
            }
        }

        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, EnumArray<Pressure.Unit, BrakeSystemComponent> units)
        {
            ArgumentNullException.ThrowIfNull(units);
            // display differently depending upon whether vacuum or air braked system
            if (car.BrakeSystemType == Formats.Msts.BrakeSystemType.VacuumPiped)
            {
                string s = Simulator.Catalog.GetString($" V {FormatStrings.FormatPressure(car.Train.BrakeSystem.EqualReservoirPressurePSIorInHg, Pressure.Unit.InHg, Pressure.Unit.InHg, true)}");
                if (lastCarBrakeSystem != null && lastCarBrakeSystem != this)
                    s += Simulator.Catalog.GetString(" EOT ") + lastCarBrakeSystem.GetStatus(units);
                if (handbrakePercent > 0)
                    s += Simulator.Catalog.GetString($" Handbrake {handbrakePercent:F0}%");
                return s;
            }
            else // air braked by default
            {
                string s = Simulator.Catalog.GetString($"BP {FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], false)}");
                if (lastCarBrakeSystem != null && lastCarBrakeSystem != this)
                    s += Simulator.Catalog.GetString(" EOT ") + lastCarBrakeSystem.GetStatus(units);
                if (handbrakePercent > 0)
                    s += Simulator.Catalog.GetString($" Handbrake {handbrakePercent:F0}%");
                return s;
            }
        }

        public override float GetCylPressurePSI()
        {
            return 0;
        }

        public override float VacResPressurePSI => 0;

        public override void Update(double elapsedClockSeconds)
        {
            BleedOffValveOpen = false;
            car.SetBrakeForce(car.MaxHandbrakeForceN * handbrakePercent / 100);
            brakeInfo.Update(null);
        }

        private protected override void UpdateBrakeStatus()
        {
            base.UpdateBrakeStatus();
            brakeInfo["BrakeType"] = "-";

            // display differently depending upon whether vacuum or air braked system
            brakeInfo["BP"] = car.BrakeSystemType == Formats.Msts.BrakeSystemType.VacuumPiped ?
                FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(BrakeLine1PressurePSI), Pressure.Unit.InHg, Pressure.Unit.InHg, true) :
                // air braked by default
                FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakePipe], true);
            if (car.BrakeSystemType == Formats.Msts.BrakeSystemType.VacuumPiped)
            {
                brakeInfo["V"] = FormatStrings.FormatPressure(car.Train.BrakeSystem.EqualReservoirPressurePSIorInHg, Pressure.Unit.InHg, Pressure.Unit.InHg, true);
            }
            brakeInfo["Status"] = $"BP {brakeInfo["BP"]}";
            brakeInfo["StatusShort"] = car.BrakeSystemType == Formats.Msts.BrakeSystemType.VacuumPiped ?
                $"BP{FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure(BrakeLine1PressurePSI), Pressure.Unit.InHg, Pressure.Unit.InHg, false)}" :
                // air braked by default
                $"BP{FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, Simulator.Instance.PlayerLocomotive.BrakeSystemPressureUnits[BrakeSystemComponent.BrakePipe], false)}";
        }
    }
}
