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

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Scripting.Api;
using Orts.Scripting.Api.Etcs;

namespace Orts.Simulation.RollingStocks.SubSystems.ControlSystems
{
    public class MSTSTrainControlSystem : TrainControlSystem
    {
        private enum MonitorState
        {
            Disabled,
            StandBy,
            Alarm,
            Emergency
        };

        private Timer vigilanceAlarmTimer;
        private Timer vigilanceEmergencyTimer;
        private Timer vigilancePenaltyTimer;
        private Timer overspeedEmergencyTimer;
        private Timer overspeedPenaltyTimer;
        private MonitorState vigilanceMonitorState;
        private MonitorState overspeedMonitorState;
        private bool externalEmergency;
        private float vigilanceAlarmTimeout;
        private float currentSpeedLimit;
        private float nextSpeedLimit;
        private MonitoringStatus status;

        private readonly MonitoringDevice vigilanceMonitor;
        private readonly MonitoringDevice overspeedMonitor;
        private readonly MonitoringDevice emergencyStopMonitor;
        private readonly MonitoringDevice awsMonitor;
        private readonly bool emergencyCausesThrottleDown;
        private readonly bool emergencyEngagesHorn;

        public bool ResetButtonPressed { get; private set; }

        public bool VigilanceSystemEnabled
        {
            get
            {
                bool enabled = true;

                enabled &= IsAlerterEnabled();

                if (vigilanceMonitor != null)
                {
                    if (vigilanceMonitor.ResetOnDirectionNeutral)
                    {
                        enabled &= CurrentDirection() != MidpointDirection.N;
                    }

                    if (vigilanceMonitor.ResetOnZeroSpeed)
                    {
                        enabled &= SpeedMpS() >= 0.1f;
                    }
                }
                return enabled;
            }
        }

        public bool VigilanceReset
        {
            get
            {
                bool vigilanceReset = true;

                if (vigilanceMonitor != null)
                {
                    if (vigilanceMonitor.ResetOnDirectionNeutral)
                    {
                        vigilanceReset &= CurrentDirection() == MidpointDirection.N;
                    }

                    if (vigilanceMonitor.ResetOnZeroSpeed)
                    {
                        vigilanceReset &= SpeedMpS() < 0.1f;
                    }

                    if (vigilanceMonitor.ResetOnResetButton)
                    {
                        vigilanceReset &= ResetButtonPressed;
                    }
                }

                return vigilanceReset;
            }
        }

        public bool SpeedControlSystemEnabled
        {
            get
            {
                bool enabled = true;
                enabled &= IsSpeedControlEnabled();
                return enabled;
            }
        }

        public bool Overspeed
        {
            get
            {
                bool overspeed = false;

                if (overspeedMonitor != null)
                {
                    if (overspeedMonitor.TriggerOnOverspeedMpS > 0)
                    {
                        overspeed |= SpeedMpS() > overspeedMonitor.TriggerOnOverspeedMpS;
                    }

                    if (overspeedMonitor.CriticalLevelMpS > 0)
                    {
                        overspeed |= SpeedMpS() > overspeedMonitor.CriticalLevelMpS;
                    }

                    if (overspeedMonitor.TriggerOnTrackOverspeed)
                    {
                        overspeed |= SpeedMpS() > currentSpeedLimit + overspeedMonitor.TriggerOnTrackOverspeedMarginMpS;
                    }
                }

                return overspeed;
            }
        }

        public bool OverspeedReset
        {
            get
            {
                bool overspeedReset = true;

                if (overspeedMonitor != null)
                {
                    if (overspeedMonitor.ResetOnDirectionNeutral)
                    {
                        overspeedReset &= CurrentDirection() == MidpointDirection.N;
                    }

                    if (overspeedMonitor.ResetOnZeroSpeed)
                    {
                        overspeedReset &= SpeedMpS() < 0.1f;
                    }

                    if (overspeedMonitor.ResetOnResetButton)
                    {
                        overspeedReset &= ResetButtonPressed;
                    }
                }
                return overspeedReset;
            }
        }

        //#region Save State API sample
        //public class StateSaverSample: TrainControlSystemSaveState
        //{
        //    public int X { get; set; }
        //}

        //private TrainControlSystemStateSaver<StateSaverSample> stateSaveSampleSaver;

        //protected override TrainControlSystemStateSaver StateSaver { get => stateSaveSampleSaver; }

        protected override TrainControlSystemStateSaver StateSaver => null;

        //protected override void OnSnapshot()
        //{
        //    stateSaveSampleSaver.SaveState = new StateSaverSample()
        //    {
        //        X = 2,
        //    };
        //}

        //protected override void OnRestore()
        //{
        //    _ = stateSaveSampleSaver.SaveState.X;
        //}
        //#endregion

        internal MSTSTrainControlSystem(MonitoringDevice vigilanceMonitor, MonitoringDevice overspeedMonitor, MonitoringDevice emergencyStopMonitor, MonitoringDevice awsMonitor,
            bool emergencyCausesThrottleDown, bool emergencyEngagesHorn)
        {
            this.vigilanceMonitor = vigilanceMonitor;
            this.overspeedMonitor = overspeedMonitor;
            this.emergencyStopMonitor = emergencyStopMonitor;
            this.awsMonitor = awsMonitor;
            this.emergencyCausesThrottleDown = emergencyCausesThrottleDown;
            this.emergencyEngagesHorn = emergencyEngagesHorn;
        }

        public override void Initialize()
        {
            vigilanceAlarmTimer = new Timer(this);
            vigilanceEmergencyTimer = new Timer(this);
            vigilancePenaltyTimer = new Timer(this);
            overspeedEmergencyTimer = new Timer(this);
            overspeedPenaltyTimer = new Timer(this);

            if (vigilanceMonitor != null)
            {
                if (vigilanceMonitor.MonitorTimeS > vigilanceMonitor.AlarmTimeS)
                    vigilanceAlarmTimeout = vigilanceMonitor.MonitorTimeS - vigilanceMonitor.AlarmTimeS;
                vigilanceAlarmTimer.Setup(vigilanceMonitor.AlarmTimeS);
                vigilanceEmergencyTimer.Setup(vigilanceAlarmTimeout);
                vigilancePenaltyTimer.Setup(vigilanceMonitor.PenaltyTimeS);
                vigilanceAlarmTimer.Start();
            }
            if (overspeedMonitor != null)
            {
                overspeedEmergencyTimer.Setup(Math.Max(overspeedMonitor.AlarmTimeS, overspeedMonitor.AlarmTimeBeforeOverspeedS));
                overspeedPenaltyTimer.Setup(overspeedMonitor.PenaltyTimeS);
            }

            ETCSStatus.DMIActive = ETCSStatus.PlanningAreaShown = true;

            Activated = true;
        }

        public override void Update()
        {
            UpdateInputs();

            if (IsTrainControlEnabled())
            {
                if (vigilanceMonitor != null)
                    UpdateVigilance();
                if (overspeedMonitor != null)
                    UpdateSpeedControl();

                bool emergencyBrake = false;
                bool fullBrake = false;
                bool powerCut = false;

                if (vigilanceMonitor != null)
                {
                    if (vigilanceMonitor.AppliesEmergencyBrake)
                        emergencyBrake |= vigilanceMonitorState == MonitorState.Emergency;
                    else if (vigilanceMonitor.AppliesFullBrake)
                        fullBrake |= vigilanceMonitorState == MonitorState.Emergency;

                    if (vigilanceMonitor.EmergencyCutsPower)
                        powerCut |= vigilanceMonitorState == MonitorState.Emergency;
                }

                if (overspeedMonitor != null)
                {
                    if (overspeedMonitor.AppliesEmergencyBrake)
                        emergencyBrake |= overspeedMonitorState == MonitorState.Emergency;
                    else if (overspeedMonitor.AppliesFullBrake)
                        fullBrake |= overspeedMonitorState == MonitorState.Emergency;

                    if (overspeedMonitor.EmergencyCutsPower)
                        powerCut |= overspeedMonitorState == MonitorState.Emergency;
                }

                if (emergencyStopMonitor != null)
                {
                    if (emergencyStopMonitor.AppliesEmergencyBrake)
                        emergencyBrake |= externalEmergency;
                    else if (emergencyStopMonitor.AppliesFullBrake)
                        fullBrake |= externalEmergency;

                    if (emergencyStopMonitor.EmergencyCutsPower)
                        powerCut |= externalEmergency;
                }

                SetTractionAuthorization(!DoesBrakeCutPower() || LocomotiveBrakeCylinderPressureBar() < BrakeCutsPowerAtBrakeCylinderPressureBar());

                SetEmergencyBrake(emergencyBrake);
                SetFullBrake(fullBrake);
                SetPowerAuthorization(!powerCut);

                if (emergencyCausesThrottleDown && (IsBrakeEmergency() || IsBrakeFullService()))
                    SetThrottleController(0f);

                if (emergencyEngagesHorn)
                    SetHorn(IsBrakeEmergency() || IsBrakeFullService());

                SetPenaltyApplicationDisplay(IsBrakeEmergency() || IsBrakeFullService());

                UpdateMonitoringStatus();
                UpdateETCSPlanning();
            }
        }

        public void UpdateInputs()
        {
            SetNextSignalAspect(NextSignalAspect(0));

            currentSpeedLimit = CurrentSignalSpeedLimitMpS();
            if (currentSpeedLimit < 0 || currentSpeedLimit > TrainSpeedLimitMpS())
                currentSpeedLimit = TrainSpeedLimitMpS();

            // TODO: NextSignalSpeedLimitMpS(0) should return 0 if the signal is at stop; cause seems to be updateSpeedInfo() within Train.cs
            nextSpeedLimit = NextSignalAspect(0) != TrackMonitorSignalAspect.Stop ? NextSignalSpeedLimitMpS(0) > 0 && NextSignalSpeedLimitMpS(0) < TrainSpeedLimitMpS() ? NextSignalSpeedLimitMpS(0) : TrainSpeedLimitMpS() : 0;

            SetCurrentSpeedLimitMpS(currentSpeedLimit);
            SetNextSpeedLimitMpS(nextSpeedLimit);
        }

        private void UpdateMonitoringStatus()
        {
            if (SpeedMpS() > currentSpeedLimit)
            {
                status = overspeedMonitor != null && (overspeedMonitor.AppliesEmergencyBrake || overspeedMonitor.AppliesFullBrake)
                    ? MonitoringStatus.Intervention
                    : MonitoringStatus.Warning;
            }
            else if (nextSpeedLimit < currentSpeedLimit && SpeedMpS() > nextSpeedLimit)
            {
                status = Deceleration(SpeedMpS(), nextSpeedLimit, NextSignalDistanceM(0)) > 0.7f
                    ? MonitoringStatus.Overspeed
                    : MonitoringStatus.Indication;
            }
            else
                status = MonitoringStatus.Normal;
            SetMonitoringStatus(status);
        }

        // Provide basic functionality for ETCS DMI planning area
        private void UpdateETCSPlanning()
        {
            float maxDistanceAheadM = 0;
            ETCSStatus.SpeedTargets.Clear();
            ETCSStatus.SpeedTargets.Add(new PlanningTarget(0, currentSpeedLimit));
            for (int i = 0; i < 5; i++)
            {
                maxDistanceAheadM = NextSignalDistanceM(i);
                if (NextSignalAspect(i) is TrackMonitorSignalAspect.Stop or TrackMonitorSignalAspect.None)
                    break;
                float speedLimMpS = NextSignalSpeedLimitMpS(i);
                if (speedLimMpS >= 0)
                    ETCSStatus.SpeedTargets.Add(new PlanningTarget(maxDistanceAheadM, speedLimMpS));
            }
            float prevDist = 0;
            float prevSpeed = 0;
            for (int i = 0; i < 10; i++)
            {
                float distanceM = NextPostDistanceM(i);
                if (distanceM >= maxDistanceAheadM)
                    break;
                float speed = NextPostSpeedLimitMpS(i);
                if (speed == prevSpeed || distanceM - prevDist < 10)
                    continue;
                ETCSStatus.SpeedTargets.Add(new PlanningTarget(distanceM, speed));
                prevDist = distanceM;
                prevSpeed = speed;
            }
            ETCSStatus.SpeedTargets.Sort((x, y) => x.DistanceToTrainM.CompareTo(y.DistanceToTrainM));
            ETCSStatus.SpeedTargets.Add(new PlanningTarget(maxDistanceAheadM, 0));
            ETCSStatus.GradientProfile.Clear();
            ETCSStatus.GradientProfile.Add(new GradientProfileElement(0, (int)(CurrentGradientPercent() * 10)));
            ETCSStatus.GradientProfile.Add(new GradientProfileElement(maxDistanceAheadM, 0)); // End of profile
        }

        public override void HandleEvent(TCSEvent evt, string message)
        {
            switch (evt)
            {
                case TCSEvent.AlerterPressed:
                case TCSEvent.AlerterReleased:
                case TCSEvent.AlerterReset:
                    if (Activated)
                    {
                        switch (vigilanceMonitorState)
                        {
                            // case VigilanceState.Disabled: do nothing

                            case MonitorState.StandBy:
                                vigilanceAlarmTimer.Stop();
                                break;

                            case MonitorState.Alarm:
                                vigilanceEmergencyTimer.Stop();
                                vigilanceMonitorState = MonitorState.StandBy;
                                break;

                                // case VigilanceState.Emergency: do nothing
                        }
                    }
                    break;
            }

            switch (evt)
            {
                case TCSEvent.AlerterPressed:
                    ResetButtonPressed = true;
                    break;

                case TCSEvent.AlerterReleased:
                    ResetButtonPressed = false;
                    break;

                case TCSEvent.EmergencyBrakingRequestedBySimulator:
                    externalEmergency = true;
                    break;

                case TCSEvent.EmergencyBrakingReleasedBySimulator:
                    externalEmergency = false;
                    break;
            }
        }

        private void UpdateVigilance()
        {
            switch (vigilanceMonitorState)
            {
                case MonitorState.Disabled:
                    if (VigilanceSystemEnabled)
                    {
                        vigilanceMonitorState = MonitorState.StandBy;
                    }
                    break;

                case MonitorState.StandBy:
                    if (!VigilanceSystemEnabled)
                    {
                        vigilanceAlarmTimer.Stop();
                        vigilanceMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (!vigilanceAlarmTimer.Started)
                        {
                            vigilanceAlarmTimer.Start();
                        }

                        if (vigilanceAlarmTimer.Triggered)
                        {
                            vigilanceAlarmTimer.Stop();
                            vigilanceMonitorState = MonitorState.Alarm;
                        }
                    }
                    break;

                case MonitorState.Alarm:
                    if (!VigilanceSystemEnabled)
                    {
                        vigilanceEmergencyTimer.Stop();
                        vigilanceMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (!vigilanceEmergencyTimer.Started)
                        {
                            vigilanceEmergencyTimer.Start();
                        }

                        if (vigilanceEmergencyTimer.Triggered)
                        {
                            vigilanceEmergencyTimer.Stop();
                            vigilanceMonitorState = MonitorState.Emergency;
                        }
                    }
                    break;

                case MonitorState.Emergency:
                    if (!vigilancePenaltyTimer.Started)
                    {
                        vigilancePenaltyTimer.Start();
                    }

                    if (vigilancePenaltyTimer.Triggered && VigilanceReset)
                    {
                        vigilanceEmergencyTimer.Stop();
                        vigilanceMonitorState = VigilanceSystemEnabled ? MonitorState.StandBy : MonitorState.Disabled;
                    }
                    break;
            }

            if (vigilanceMonitorState >= MonitorState.Alarm)
            {
                if (!AlerterSound())
                {
                    SetVigilanceAlarm(true);
                }
            }
            else
            {
                if (AlerterSound())
                {
                    SetVigilanceAlarm(false);
                }
            }

            SetVigilanceAlarmDisplay(vigilanceMonitorState == MonitorState.Alarm);
            SetVigilanceEmergencyDisplay(vigilanceMonitorState == MonitorState.Emergency);
        }

        private void UpdateSpeedControl()
        {
            var interventionSpeedMpS = currentSpeedLimit + Speed.MeterPerSecond.FromKpH(5.0f); // Default margin : 5 km/h

            if (overspeedMonitor.TriggerOnTrackOverspeed)
            {
                interventionSpeedMpS = currentSpeedLimit + overspeedMonitor.TriggerOnTrackOverspeedMarginMpS;
            }

            SetInterventionSpeedLimitMpS((float)interventionSpeedMpS);

            switch (overspeedMonitorState)
            {
                case MonitorState.Disabled:
                    if (SpeedControlSystemEnabled)
                    {
                        overspeedMonitorState = MonitorState.StandBy;
                    }
                    break;

                case MonitorState.StandBy:
                    if (!SpeedControlSystemEnabled)
                    {
                        overspeedMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (Overspeed)
                        {
                            overspeedMonitorState = MonitorState.Alarm;
                        }
                    }
                    break;

                case MonitorState.Alarm:
                    if (!SpeedControlSystemEnabled)
                    {
                        overspeedMonitorState = MonitorState.Disabled;
                    }
                    else
                    {
                        if (!overspeedEmergencyTimer.Started)
                        {
                            overspeedEmergencyTimer.Start();
                        }

                        if (!Overspeed)
                        {
                            overspeedEmergencyTimer.Stop();
                            overspeedMonitorState = MonitorState.StandBy;
                        }
                        else if (overspeedEmergencyTimer.Triggered)
                        {
                            overspeedEmergencyTimer.Stop();
                            overspeedMonitorState = MonitorState.Emergency;
                        }
                    }
                    break;

                case MonitorState.Emergency:
                    if (!overspeedPenaltyTimer.Started)
                    {
                        overspeedPenaltyTimer.Start();
                    }

                    if (overspeedPenaltyTimer.Triggered && OverspeedReset)
                    {
                        overspeedPenaltyTimer.Stop();
                        overspeedMonitorState = MonitorState.StandBy;
                    }
                    break;
            }

            SetOverspeedWarningDisplay(overspeedMonitorState >= MonitorState.Alarm);
        }
    }
}
