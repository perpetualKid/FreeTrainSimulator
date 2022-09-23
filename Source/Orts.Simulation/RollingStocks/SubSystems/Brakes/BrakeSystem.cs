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

using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes
{
    public enum BrakeSystemComponent
    {
        MainReservoir,
        EqualizingReservoir,
        AuxiliaryReservoir,
        EmergencyReservoir,
        MainPipe,
        BrakePipe,
        BrakeCylinder
    }

    public abstract class BrakeSystem : INameValueInformationProvider
    {
        private protected readonly TrainCar car;
        private protected readonly DebugInfoBase brakeInfo = new DebugInfoBase();
        private protected bool updateBrakeStatus;

        private protected abstract void UpdateBrakeStatus();

        /// <summary>
        /// Main trainline pressure at this car in PSI
        /// </summary>
        public float BrakeLine1PressurePSI { get; set; } = 90;

        /// <summary>
        /// Main reservoir equalization pipe pressure in PSI
        /// </summary>
        public float BrakeLine2PressurePSI { get; set; }

        /// <summary>
        /// Engine brake cylinder equalization pipe pressure
        /// </summary>
        public float BrakeLine3PressurePSI { get; set; }

        /// <summary>
        /// Volume of a single brake line
        /// </summary>
        public float BrakePipeVolumeM3 { get; set; } = 1.4e-2f;

        /// <summary>
        /// Stops Running controller from becoming active until BP = EQ Res, used in EQ vacuum brakes
        /// </summary>
        public bool ControllerRunningLock { get; set; }

        public float BrakeCylFraction { get; set; }

        /// <summary>
        /// Front brake hoses connection status
        /// </summary>
        public bool FrontBrakeHoseConnected { get; set; }
        /// <summary>
        /// Front angle cock opened/closed status
        /// </summary>
        public bool AngleCockAOpen { get; set; } = true;
        /// <summary>
        /// Rear angle cock opened/closed status
        /// </summary>
        public bool AngleCockBOpen { get; set; } = true;
        /// <summary>
        /// Auxiliary brake reservoir vent valve open/closed status
        /// </summary>
        public bool BleedOffValveOpen { get; set; }
        /// <summary>
        /// Indicates whether the main reservoir pipe is available
        /// </summary>
        public bool TwoPipes { get; protected set; }

        public abstract void AISetPercent(float percent);

        public abstract string GetStatus(EnumArray<Pressure.Unit, BrakeSystemComponent> units);
        public abstract string GetFullStatus(BrakeSystem lastCarBrakeSystem, EnumArray<Pressure.Unit, BrakeSystemComponent> units);
        public abstract string[] GetDebugStatus(EnumArray<Pressure.Unit, BrakeSystemComponent> units);
        public abstract float GetCylPressurePSI();
        public abstract float GetCylVolumeM3();
        public abstract float GetVacResPressurePSI();
        public abstract float GetVacResVolume();
        public abstract float GetVacBrakeCylNumber();
        public bool CarBPIntact { get; set; }

        public NameValueCollection DebugInfo => GetBrakeStatus();

        public Dictionary<string, FormatOption> FormattingOptions => brakeInfo.FormattingOptions;

        public abstract void Save(BinaryWriter outf);

        public abstract void Restore(BinaryReader inf);

        public abstract void PropagateBrakePressure(double elapsedClockSeconds);

        /// <summary>
        /// Convert real pressure to a system specific internal pressure.
        /// For pressured brakes it is a straight 1:1 noop conversion,
        /// but for vacuum brakes it is a conversion to an internally used equivalent pressure.
        /// </summary>
        public abstract float InternalPressure(float realPressure);

        public abstract void Initialize(bool handbrakeOn, float maxPressurePSI, float fullServPressurePSI, bool immediateRelease);
        public abstract void SetHandbrakePercent(float percent);
        public abstract bool GetHandbrakeStatus();
        public abstract void SetRetainer(RetainerSetting setting);
        public abstract void InitializeMoving(); // starting conditions when starting speed > 0
        public abstract void LocoInitializeMoving(); // starting conditions when starting speed > 0
        public abstract bool IsBraking(); // return true if the wagon is braking above a certain threshold
        public abstract void CorrectMaxCylPressurePSI(MSTSLocomotive loco); // corrects max cyl pressure when too high

        protected BrakeSystem(TrainCar car)
        {
            this.car = car;
        }

        private NameValueCollection GetBrakeStatus()
        {
            updateBrakeStatus = true;
            return brakeInfo;
        }
    }

    public enum RetainerSetting
    {
        [Description("Exhaust")] Exhaust,
        [Description("High Pressure")] HighPressure,
        [Description("Low Pressure")] LowPressure,
        [Description("Slow Direct")] SlowDirect
    };
}
