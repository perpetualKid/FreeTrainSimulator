// COPYRIGHT 2021 by the Open Rails project.
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

// Define this to log the wheel configurations on cars as they are loaded.
//#define DEBUG_WHEELS

using System;

using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems.ControlSystems
{
    internal class MonitoringDevice
    {
        public float MonitorTimeS = 66; // Time from alerter reset to applying emergency brake
        public float AlarmTimeS = 60; // Time from alerter reset to audible and visible alarm
        public float PenaltyTimeS;
        public float CriticalLevelMpS;
        public float ResetLevelMpS;
        public bool AppliesFullBrake = true;
        public bool AppliesEmergencyBrake;
        public bool EmergencyCutsPower;
        public bool EmergencyShutsDownEngine;
        public float AlarmTimeBeforeOverspeedS = 5;         // OverspeedMonitor only
        public float TriggerOnOverspeedMpS;                 // OverspeedMonitor only
        public bool TriggerOnTrackOverspeed;                // OverspeedMonitor only
        public float TriggerOnTrackOverspeedMarginMpS = 4;  // OverspeedMonitor only
        public bool ResetOnDirectionNeutral;
        public bool ResetOnZeroSpeed = true;
        public bool ResetOnResetButton;                     // OverspeedMonitor only

        public MonitoringDevice(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("monitoringdevicemonitortimelimit", () => { MonitorTimeS = stf.ReadFloatBlock(STFReader.Units.Time, MonitorTimeS); }),
                    new STFReader.TokenProcessor("monitoringdevicealarmtimelimit", () => { AlarmTimeS = stf.ReadFloatBlock(STFReader.Units.Time, AlarmTimeS); }),
                    new STFReader.TokenProcessor("monitoringdevicepenaltytimelimit", () => { PenaltyTimeS = stf.ReadFloatBlock(STFReader.Units.Time, PenaltyTimeS); }),
                    new STFReader.TokenProcessor("monitoringdevicecriticallevel", () => { CriticalLevelMpS = stf.ReadFloatBlock(STFReader.Units.Speed, CriticalLevelMpS); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetlevel", () => { ResetLevelMpS = stf.ReadFloatBlock(STFReader.Units.Speed, ResetLevelMpS); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliesfullbrake", () => { AppliesFullBrake = stf.ReadBoolBlock(AppliesFullBrake); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliesemergencybrake", () => { AppliesEmergencyBrake = stf.ReadBoolBlock(AppliesEmergencyBrake); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliescutspower", () => { EmergencyCutsPower = stf.ReadBoolBlock(EmergencyCutsPower); }),
                    new STFReader.TokenProcessor("monitoringdeviceappliesshutsdownengine", () => { EmergencyShutsDownEngine = stf.ReadBoolBlock(EmergencyShutsDownEngine); }),
                    new STFReader.TokenProcessor("monitoringdevicealarmtimebeforeoverspeed", () => { AlarmTimeBeforeOverspeedS = stf.ReadFloatBlock(STFReader.Units.Time, AlarmTimeBeforeOverspeedS); }),
                    new STFReader.TokenProcessor("monitoringdevicetriggeronoverspeed", () => { TriggerOnOverspeedMpS = stf.ReadFloatBlock(STFReader.Units.Speed, TriggerOnOverspeedMpS); }),
                    new STFReader.TokenProcessor("monitoringdevicetriggerontrackoverspeed", () => { TriggerOnTrackOverspeed = stf.ReadBoolBlock(TriggerOnTrackOverspeed); }),
                    new STFReader.TokenProcessor("monitoringdevicetriggerontrackoverspeedmargin", () => { TriggerOnTrackOverspeedMarginMpS = stf.ReadFloatBlock(STFReader.Units.Speed, TriggerOnTrackOverspeedMarginMpS); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetondirectionneutral", () => { ResetOnDirectionNeutral = stf.ReadBoolBlock(ResetOnDirectionNeutral); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetonresetbutton", () => { ResetOnResetButton = stf.ReadBoolBlock(ResetOnResetButton); }),
                    new STFReader.TokenProcessor("monitoringdeviceresetonzerospeed", () => { ResetOnZeroSpeed = stf.ReadBoolBlock(ResetOnZeroSpeed); }),
                });
        }

        public MonitoringDevice(MonitoringDevice source)
        {
            MonitorTimeS = source?.MonitorTimeS ?? throw new ArgumentNullException(nameof(source));
            AlarmTimeS = source.AlarmTimeS;
            PenaltyTimeS = source.PenaltyTimeS;
            CriticalLevelMpS = source.CriticalLevelMpS;
            ResetLevelMpS = source.ResetLevelMpS;
            AppliesFullBrake = source.AppliesFullBrake;
            AppliesEmergencyBrake = source.AppliesEmergencyBrake;
            EmergencyCutsPower = source.EmergencyCutsPower;
            EmergencyShutsDownEngine = source.EmergencyShutsDownEngine;
            AlarmTimeBeforeOverspeedS = source.AlarmTimeBeforeOverspeedS;
            TriggerOnOverspeedMpS = source.TriggerOnOverspeedMpS;
            TriggerOnTrackOverspeed = source.TriggerOnTrackOverspeed;
            TriggerOnTrackOverspeedMarginMpS = source.TriggerOnTrackOverspeedMarginMpS;
            ResetOnDirectionNeutral = source.ResetOnDirectionNeutral;
            ResetOnZeroSpeed = source.ResetOnZeroSpeed;
            ResetOnResetButton = source.ResetOnResetButton;
        }
    }
}
