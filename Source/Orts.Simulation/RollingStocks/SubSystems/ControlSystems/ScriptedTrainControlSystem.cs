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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;
using Orts.Scripting.Api.Etcs;
using Orts.Simulation.Activities;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using Orts.Simulation.Signalling;

using static Orts.Scripting.Api.Etcs.ETCSStatus;

namespace Orts.Simulation.RollingStocks.SubSystems.ControlSystems
{
    public class ScriptedTrainControlSystem : ISubSystem<ScriptedTrainControlSystem>
    {

        // Constants
        private const int TCSCabviewControlCount = 48;
        private const int TCSCommandCount = 48;

        private const float GravityMpS2 = 9.80665f;
        private const float GenericItemDistance = 400.0f;

        // Properties
        public bool VigilanceAlarm { get; set; }
        public bool VigilanceEmergency { get; set; }
        public bool OverspeedWarning { get; set; }
        public bool PenaltyApplication { get; set; }
        public float CurrentSpeedLimitMpS { get; set; }
        public float NextSpeedLimitMpS { get; set; }
        public TrackMonitorSignalAspect CabSignalAspect { get; set; }

        private bool activated;
        private bool customTCSScript;
        private readonly MSTSLocomotive Locomotive;
        protected static readonly Simulator Simulator = Simulator.Instance;
        private float ItemSpeedLimit;
        private TrackMonitorSignalAspect ItemAspect;
        private float ItemDistance;
        private string MainHeadSignalTypeName;
        private MonitoringDevice VigilanceMonitor;
        private MonitoringDevice OverspeedMonitor;
        private MonitoringDevice EmergencyStopMonitor;
        private MonitoringDevice AWSMonitor;

        // List of customized control strings;
        private readonly string[] customizedCabviewControlNames = new string[TCSCabviewControlCount];

        private string scriptName;
        private string soundFileName;
        private string parametersFileName;
        private TrainControlSystem script;
        private string trainParametersFileName;

        private bool simulatorEmergencyBraking;
        public bool SimulatorEmergencyBraking
        {
            get
            {
                return simulatorEmergencyBraking;
            }
            protected set
            {
                simulatorEmergencyBraking = value;
                Locomotive.TrainBrakeController.TCSEmergencyBraking = value;
            }
        }
        public bool AlerterButtonPressed { get; private set; }
        public bool PowerAuthorization { get; private set; }
        public bool CircuitBreakerClosingOrder { get; private set; }
        public bool CircuitBreakerOpeningOrder { get; private set; }
        public bool TractionAuthorization { get; private set; }
        public float MaxThrottlePercent { get; private set; } = 100f;
        public bool FullDynamicBrakingOrder { get; private set; }

        public float[] CabDisplayControls { get; } = new float[TCSCabviewControlCount];

        // generic TCS commands
        public bool[] TCSCommandButtonDown { get; } = new bool[TCSCommandCount];
        public bool[] TCSCommandSwitchOn { get; } = new bool[TCSCommandCount];

        public ETCSStatus ETCSStatus { get { return script?.ETCSStatus; } }

        public Dictionary<TrainControlSystem, string> Sounds { get; } = new Dictionary<TrainControlSystem, string>();

        public ScriptedTrainControlSystem() { }

        public ScriptedTrainControlSystem(MSTSLocomotive locomotive)
        {
            Locomotive = locomotive;

            PowerAuthorization = true;
            CircuitBreakerClosingOrder = false;
            CircuitBreakerOpeningOrder = false;
            TractionAuthorization = true;
            FullDynamicBrakingOrder = false;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);

            switch (lowercasetoken)
            {
                case "engine(vigilancemonitor":
                    VigilanceMonitor = new MonitoringDevice(stf);
                    break;
                case "engine(overspeedmonitor":
                    OverspeedMonitor = new MonitoringDevice(stf);
                    break;
                case "engine(emergencystopmonitor":
                    EmergencyStopMonitor = new MonitoringDevice(stf);
                    break;
                case "engine(awsmonitor":
                    AWSMonitor = new MonitoringDevice(stf);
                    break;
                case "engine(ortstraincontrolsystem":
                    scriptName = stf.ReadStringBlock(null);
                    break;
                case "engine(ortstraincontrolsystemsound":
                    soundFileName = stf.ReadStringBlock(null);
                    break;
                case "engine(ortstraincontrolsystemparameters":
                    parametersFileName = stf.ReadStringBlock(null);
                    break;
            }
        }

        public void Copy(ScriptedTrainControlSystem source)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));
            scriptName = source.scriptName;
            soundFileName = source.soundFileName;
            parametersFileName = source.parametersFileName;
            trainParametersFileName = source.trainParametersFileName;
            if (source.VigilanceMonitor != null)
                VigilanceMonitor = new MonitoringDevice(source.VigilanceMonitor);
            if (source.OverspeedMonitor != null)
                OverspeedMonitor = new MonitoringDevice(source.OverspeedMonitor);
            if (source.EmergencyStopMonitor != null)
                EmergencyStopMonitor = new MonitoringDevice(source.EmergencyStopMonitor);
            if (source.AWSMonitor != null)
                AWSMonitor = new MonitoringDevice(source.AWSMonitor);
        }

        //Debrief Eval
        private bool ldbfevalfullbrakeabove16kmh;

        public void Initialize()
        {
            if (!activated)
            {
                if (!Simulator.Settings.DisableTCSScripts && !string.IsNullOrEmpty(scriptName) && !scriptName.Equals("MSTS", StringComparison.OrdinalIgnoreCase))
                {
                    script = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"), scriptName) as TrainControlSystem;
                    customTCSScript = true;
                }

                if (parametersFileName != null)
                {
                    parametersFileName = Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script", parametersFileName);
                }

                if (Locomotive.Train.TcsParametersFileName != null)
                {
                    trainParametersFileName = Path.Combine(Simulator.Instance.RouteFolder.ContentFolder.ConsistsFolder, "Script", Locomotive.Train.TcsParametersFileName);
                }

                script ??= new MSTSTrainControlSystem(VigilanceMonitor, OverspeedMonitor, EmergencyStopMonitor, AWSMonitor, Locomotive.EmergencyCausesThrottleDown, Locomotive.EmergencyEngagesHorn);

                if (soundFileName != null)
                {
                    string[] soundPathArray = new[] {
                    Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "SOUND"),
                    Path.Combine(Simulator.RouteFolder.ContentFolder.SoundFolder),
                };
                    var soundPath = FolderStructure.FindFileFromFolders(soundPathArray, soundFileName);
                    if (File.Exists(soundPath))
                        Sounds.Add(script, soundPath);
                }

                // AbstractScriptClass
                script.ClockTime = () => (float)Simulator.ClockTime;
                script.GameTime = () => (float)Simulator.GameTime;
                script.PreUpdate = () => Simulator.PreUpdate;
                script.DistanceM = () => Locomotive.DistanceTravelled;
                script.Confirm = Simulator.Confirmer.Confirm;
                script.Message = Simulator.Confirmer.Message;
                script.SignalEvent = Locomotive.SignalEvent;
                script.SignalEventToTrain = (evt) =>
                {
                    if (Locomotive.Train != null)
                    {
                        Locomotive.Train.SignalEvent(evt);
                    }
                };

                // TrainControlSystem getters
                script.IsTrainControlEnabled = () => Locomotive == Locomotive.Train.LeadLocomotive && Locomotive.Train.TrainType != TrainType.AiPlayerHosting;
                script.IsAutopiloted = () => Locomotive == Simulator.PlayerLocomotive && Locomotive.Train.TrainType == TrainType.AiPlayerHosting;
                script.IsAlerterEnabled = () =>
                {
                    return Simulator.Settings.Alerter
                        && !(Simulator.Settings.AlerterDisableExternal
                            && !Simulator.PlayerIsInCab
                        );
                };
                script.IsSpeedControlEnabled = () => Simulator.Settings.SpeedControl;
                script.IsLowVoltagePowerSupplyOn = () => Locomotive.LocomotivePowerSupply.LowVoltagePowerSupplyOn;
                script.IsCabPowerSupplyOn = () => Locomotive.LocomotivePowerSupply.CabPowerSupplyOn;
                script.AlerterSound = () => Locomotive.AlerterSnd;
                script.TrainSpeedLimitMpS = () => Math.Min(Locomotive.Train.AllowedMaxSpeedMpS, Locomotive.Train.TrainMaxSpeedMpS);
                script.TrainMaxSpeedMpS = () => Locomotive.Train.TrainMaxSpeedMpS; // max speed for train in a specific section, independently from speedpost and signal limits
                script.CurrentSignalSpeedLimitMpS = () => Locomotive.Train.AllowedMaxSpeedSignalMpS;
                script.NextSignalSpeedLimitMpS = (value) => NextGenericSignalItem(value, ref ItemSpeedLimit, float.MaxValue, TrainPathItemType.Signal, "NORMAL");
                script.NextSignalAspect = (value) => NextGenericSignalItem(value, ref ItemAspect, float.MaxValue, TrainPathItemType.Signal, "NORMAL");
                script.NextSignalDistanceM = (value) => NextGenericSignalItem(value, ref ItemDistance, float.MaxValue, TrainPathItemType.Signal, "NORMAL");
                script.NextNormalSignalDistanceHeadsAspect = () => NextNormalSignalDistanceHeadsAspect();
                script.DoesNextNormalSignalHaveTwoAspects = () => DoesNextNormalSignalHaveTwoAspects();
                script.NextDistanceSignalAspect = () =>
                    NextGenericSignalItem(0, ref ItemAspect, GenericItemDistance, TrainPathItemType.Signal, "DISTANCE");
                script.NextDistanceSignalDistanceM = () =>
                    NextGenericSignalItem(0, ref ItemDistance, GenericItemDistance, TrainPathItemType.Signal, "DISTANCE");
                script.NextGenericSignalMainHeadSignalType = (type) =>
                    NextGenericSignalItem(0, ref MainHeadSignalTypeName, GenericItemDistance, TrainPathItemType.Signal, type);
                script.NextGenericSignalAspect = (type) =>
                    NextGenericSignalItem(0, ref ItemAspect, GenericItemDistance, TrainPathItemType.Signal, type);
                script.NextGenericSignalDistanceM = (type) =>
                    NextGenericSignalItem(0, ref ItemDistance, GenericItemDistance, TrainPathItemType.Signal, type);
                script.NextGenericSignalFeatures = (arg1, arg2, arg3) => NextGenericSignalFeatures(arg1, arg2, arg3, TrainPathItemType.Signal);
                script.NextSpeedPostFeatures = (arg1, arg2) => NextSpeedPostFeatures(arg1, arg2);
                script.DoesNextNormalSignalHaveRepeaterHead = () => DoesNextNormalSignalHaveRepeaterHead();
                script.CurrentPostSpeedLimitMpS = () => Locomotive.Train.AllowedMaxSpeedLimitMpS;
                script.NextPostSpeedLimitMpS = (value) => NextGenericSignalItem(value, ref ItemSpeedLimit, float.MaxValue, TrainPathItemType.Speedpost);
                script.NextPostDistanceM = (value) => NextGenericSignalItem(value, ref ItemDistance, float.MaxValue, TrainPathItemType.Speedpost);
                script.NextTunnel = (value) =>
                {
                    List<TrainPathItem> list = Locomotive.Train.PlayerTrainTunnels[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward];
                    return value >= list?.Count
                        ? new TunnelInfo(float.MaxValue, -1)
                        : new TunnelInfo(list[value].DistanceToTrainM, list[value].StationPlatformLength);
                };
                script.NextMilepost = (value) =>
                {
                    List<TrainPathItem> list = Locomotive.Train.PlayerTrainMileposts[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward];
                    return value >= list?.Count
                        ? new MilepostInfo(float.MaxValue, -1)
                        : new MilepostInfo(list[value].DistanceToTrainM, list[value].Miles);
                };
                script.EOADistanceM = (value) => Locomotive.Train.DistanceToEndNodeAuthorityM[value];
                script.TrainLengthM = () => Locomotive.Train != null ? Locomotive.Train.Length : 0f;
                script.SpeedMpS = () => Math.Abs(Locomotive.SpeedMpS);
                script.CurrentDirection = () => Locomotive.Direction; // Direction of locomotive, may be different from direction of train
                script.IsDirectionForward = () => Locomotive.Direction == MidpointDirection.Forward;
                script.IsDirectionNeutral = () => Locomotive.Direction == MidpointDirection.N;
                script.IsDirectionReverse = () => Locomotive.Direction == MidpointDirection.Reverse;
                script.CurrentTrainMUDirection = () => Locomotive.Train.MUDirection; // Direction of train
                script.IsFlipped = () => Locomotive.Flipped;
                script.IsRearCab = () => Locomotive.UsingRearCab;
                script.IsBrakeEmergency = () => Locomotive.TrainBrakeController.EmergencyBraking;
                script.IsBrakeFullService = () => Locomotive.TrainBrakeController.TCSFullServiceBraking;
                script.PowerAuthorization = () => PowerAuthorization;
                script.CircuitBreakerClosingOrder = () => CircuitBreakerClosingOrder;
                script.CircuitBreakerOpeningOrder = () => CircuitBreakerOpeningOrder;
                script.PantographCount = () => Locomotive.Pantographs.Count;
                script.GetPantographState = (pantoID) =>
                {
                    if (pantoID >= Pantographs.MinPantoID && pantoID <= Pantographs.MaxPantoID)
                    {
                        return Locomotive.Pantographs[pantoID].State;
                    }
                    else
                    {
                        Trace.TraceError($"TCS script used bad pantograph ID {pantoID}");
                        return PantographState.Down;
                    }
                };
                script.ArePantographsDown = () => Locomotive.Pantographs.State == PantographState.Down;
                script.ThrottlePercent = () => Locomotive.ThrottleController.CurrentValue * 100;
                script.MaxThrottlePercent = () => MaxThrottlePercent;
                script.DynamicBrakePercent = () => Locomotive.DynamicBrakeController == null ? 0 : Locomotive.DynamicBrakeController.CurrentValue * 100;
                script.TractionAuthorization = () => TractionAuthorization;
                script.BrakePipePressureBar = () => Locomotive.BrakeSystem != null ? (float)Pressure.Atmospheric.FromPSI(Locomotive.BrakeSystem.BrakeLine1PressurePSI) : float.MaxValue;
                script.LocomotiveBrakeCylinderPressureBar = () => Locomotive.BrakeSystem != null ? (float)Pressure.Atmospheric.FromPSI(Locomotive.BrakeSystem.GetCylPressurePSI()) : float.MaxValue;
                script.DoesBrakeCutPower = () => Locomotive.DoesBrakeCutPower;
                script.BrakeCutsPowerAtBrakeCylinderPressureBar = () => (float)Pressure.Atmospheric.FromPSI(Locomotive.BrakeCutsPowerAtBrakeCylinderPressurePSI);
                script.TrainBrakeControllerState = () => Locomotive.TrainBrakeController.TrainBrakeControllerState;
                script.AccelerationMpSS = () => Locomotive.AccelerationMpSS;
                script.AltitudeM = () => Locomotive.WorldPosition.Location.Y;
                script.CurrentGradientPercent = () => Locomotive.CurrentElevationPercent;
                script.LineSpeedMpS = () => (float)Simulator.Route.SpeedLimit;
                script.SignedDistanceM = () => Locomotive.Train.DistanceTravelledM;
                script.DoesStartFromTerminalStation = () => DoesStartFromTerminalStation();
                script.IsColdStart = () => Locomotive.Train.ColdStart;
                script.GetTrackNodeOffset = () => Locomotive.Train.FrontTDBTraveller.TrackNodeLength - Locomotive.Train.FrontTDBTraveller.TrackNodeOffset;
                script.NextDivergingSwitchDistanceM = (value) =>
                {
                    List<TrainPathItem> list = Locomotive.Train.PlayerTrainDivergingSwitches[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward, SwitchDirection.Facing];
                    return list == null || list.Count == 0 || list[0].DistanceToTrainM > value ? float.MaxValue : list[0].DistanceToTrainM;
                };
                script.NextTrailingDivergingSwitchDistanceM = (value) =>
                {
                    List<TrainPathItem> list = Locomotive.Train.PlayerTrainDivergingSwitches[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward, SwitchDirection.Trailing];
                    return list == null || list.Count == 0 || list[0].DistanceToTrainM > value ? float.MaxValue : list[0].DistanceToTrainM;
                };
                script.GetControlMode = () => (TrainControlMode)(int)Locomotive.Train.ControlMode;
                script.NextStationName = () => Locomotive.Train.StationStops != null && Locomotive.Train.StationStops.Count > 0 ? Locomotive.Train.StationStops[0].PlatformItem.Name : "";
                script.NextStationDistanceM = () => Locomotive.Train.StationStops != null && Locomotive.Train.StationStops.Count > 0 ? Locomotive.Train.StationStops[0].DistanceToTrainM : float.MaxValue;

                // TrainControlSystem functions
                script.SpeedCurve = (arg1, arg2, arg3, arg4, arg5) => SpeedCurve(arg1, arg2, arg3, arg4, arg5);
                script.DistanceCurve = (arg1, arg2, arg3, arg4, arg5) => DistanceCurve(arg1, arg2, arg3, arg4, arg5);
                script.Deceleration = (arg1, arg2, arg3) => Deceleration(arg1, arg2, arg3);

                // TrainControlSystem setters
                script.SetFullBrake = (value) =>
                {
                    if (Locomotive.TrainBrakeController.TCSFullServiceBraking != value)
                    {
                        Locomotive.TrainBrakeController.TCSFullServiceBraking = value;

                        //Debrief Eval
                        if (value && Locomotive.IsPlayerTrain && !ldbfevalfullbrakeabove16kmh && Math.Abs(Locomotive.SpeedMpS) > 4.44444)
                        {
                            ActivityEvaluation.Instance.FullBrakeAbove16kmh++;
                            ldbfevalfullbrakeabove16kmh = true;
                        }
                        if (!value)
                            ldbfevalfullbrakeabove16kmh = false;
                    }
                };
                script.SetEmergencyBrake = (value) =>
                {
                    if (Locomotive.TrainBrakeController.TCSEmergencyBraking != value)
                        Locomotive.TrainBrakeController.TCSEmergencyBraking = value;
                };
                script.SetFullDynamicBrake = (value) => FullDynamicBrakingOrder = value;
                script.SetThrottleController = (value) => Locomotive.ThrottleController.SetValue(value);
                script.SetDynamicBrakeController = (value) =>
                {
                    if (Locomotive.DynamicBrakeController == null)
                        return;
                    Locomotive.DynamicBrakeChangeActiveState(value > 0);
                    Locomotive.DynamicBrakeController.SetValue(value);
                };
                script.SetPantographsDown = () =>
                {
                    if (Locomotive.Pantographs.State == PantographState.Up)
                    {
                        Locomotive.Train.SignalEvent(PowerSupplyEvent.LowerPantograph);
                    }
                };
                script.SetPantographUp = (pantoID) =>
                {
                    if (pantoID < Pantographs.MinPantoID || pantoID > Pantographs.MaxPantoID)
                    {
                        Trace.TraceError($"TCS script used bad pantograph ID {pantoID}");
                        return;
                    }
                    Locomotive.Train.SignalEvent(PowerSupplyEvent.RaisePantograph, pantoID);
                };
                script.SetPantographDown = (pantoID) =>
                {
                    if (pantoID < Pantographs.MinPantoID || pantoID > Pantographs.MaxPantoID)
                    {
                        Trace.TraceError($"TCS script used bad pantograph ID {pantoID}");
                        return;
                    }
                    Locomotive.Train.SignalEvent(PowerSupplyEvent.LowerPantograph, pantoID);
                };
                script.SetPowerAuthorization = (value) => PowerAuthorization = value;
                script.SetCircuitBreakerClosingOrder = (value) => CircuitBreakerClosingOrder = value;
                script.SetCircuitBreakerOpeningOrder = (value) => CircuitBreakerOpeningOrder = value;
                script.SetTractionAuthorization = (value) => TractionAuthorization = value;
                script.SetMaxThrottlePercent = (value) =>
                {
                    if (value is >= 0 and <= 100f)
                    {
                        MaxThrottlePercent = value;
                    }
                };
                script.SetVigilanceAlarm = (value) => Locomotive.SignalEvent(value ? TrainEvent.VigilanceAlarmOn : TrainEvent.VigilanceAlarmOff);
                script.SetHorn = (value) => Locomotive.TCSHorn = value;
                script.TriggerSoundAlert1 = () => SignalEvent(TrainEvent.TrainControlSystemAlert1, script);
                script.TriggerSoundAlert2 = () => SignalEvent(TrainEvent.TrainControlSystemAlert2, script);
                script.TriggerSoundInfo1 = () => SignalEvent(TrainEvent.TrainControlSystemInfo1, script);
                script.TriggerSoundInfo2 = () => SignalEvent(TrainEvent.TrainControlSystemInfo2, script);
                script.TriggerSoundPenalty1 = () => SignalEvent(TrainEvent.TrainControlSystemPenalty1, script);
                script.TriggerSoundPenalty2 = () => SignalEvent(TrainEvent.TrainControlSystemPenalty2, script);
                script.TriggerSoundWarning1 = () => SignalEvent(TrainEvent.TrainControlSystemWarning1, script);
                script.TriggerSoundWarning2 = () => SignalEvent(TrainEvent.TrainControlSystemWarning2, script);
                script.TriggerSoundSystemActivate = () => SignalEvent(TrainEvent.TrainControlSystemActivate, script);
                script.TriggerSoundSystemDeactivate = () => SignalEvent(TrainEvent.TrainControlSystemDeactivate, script);
                script.TriggerGenericSound = (value) => SignalEvent(value, script);
                script.SetVigilanceAlarmDisplay = (value) => VigilanceAlarm = value;
                script.SetVigilanceEmergencyDisplay = (value) => VigilanceEmergency = value;
                script.SetOverspeedWarningDisplay = (value) => OverspeedWarning = value;
                script.SetPenaltyApplicationDisplay = (value) => PenaltyApplication = value;
                script.SetMonitoringStatus = (value) =>
                {
                    switch (value)
                    {
                        case MonitoringStatus.Normal:
                        case MonitoringStatus.Indication:
                            ETCSStatus.CurrentMonitor = Monitor.CeilingSpeed;
                            ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Normal;
                            break;
                        case MonitoringStatus.Overspeed:
                            ETCSStatus.CurrentMonitor = Monitor.TargetSpeed;
                            ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Indication;
                            break;
                        case MonitoringStatus.Warning:
                            ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Overspeed;
                            break;
                        case MonitoringStatus.Intervention:
                            ETCSStatus.CurrentSupervisionStatus = SupervisionStatus.Intervention;
                            break;
                    }
                };
                script.SetCurrentSpeedLimitMpS = (value) =>
                {
                    CurrentSpeedLimitMpS = value;
                    ETCSStatus.AllowedSpeedMpS = value;
                };
                script.SetNextSpeedLimitMpS = (value) =>
                {
                    NextSpeedLimitMpS = value;
                    ETCSStatus.TargetSpeedMpS = value;
                };
                script.SetInterventionSpeedLimitMpS = (value) => script.ETCSStatus.InterventionSpeedMpS = value;
                script.SetNextSignalAspect = (value) => CabSignalAspect = value;
                script.SetCabDisplayControl = (arg1, arg2) => CabDisplayControls[arg1] = arg2;
                script.SetCustomizedCabviewControlName = (id, value) =>
                {
                    if (id >= 0 && id < TCSCabviewControlCount)
                    {
                        customizedCabviewControlNames[id] = value;
                    }
                };
                script.RequestToggleManualMode = () => Locomotive.Train.RequestToggleManualMode();

                // TrainControlSystem INI configuration file
                script.GetBoolParameter = (arg1, arg2, arg3) => LoadParameter(arg1, arg2, arg3);
                script.GetIntParameter = (arg1, arg2, arg3) => LoadParameter(arg1, arg2, arg3);
                script.GetFloatParameter = (arg1, arg2, arg3) => LoadParameter(arg1, arg2, arg3);
                script.GetStringParameter = (arg1, arg2, arg3) => LoadParameter(arg1, arg2, arg3);

                script.Initialize();
                activated = true;
            }
        }

        public void InitializeMoving()
        {
            script?.InitializeMoving();
        }



        private TrackMonitorSignalAspect NextNormalSignalDistanceHeadsAspect()
        {
            var signal = Locomotive.Train.NextSignalObject[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? 1 : 0];
            if (signal != null)
            {
                foreach (var signalHead in signal.SignalHeads)
                {
                    if (signalHead.SignalType.FunctionType == SignalFunction.Distance)
                    {
                        return SignalEnvironment.TranslateToTCSAspect(signal.SignalLR(SignalFunction.Distance));
                    }
                }
            }
            return TrackMonitorSignalAspect.None;
            ;
        }

        private bool DoesNextNormalSignalHaveTwoAspects()
        // ...and the two aspects of each head are STOP and ( CLEAR_2 or CLEAR_1 or RESTRICTING)
        {
            var signal = Locomotive.Train.NextSignalObject[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? 1 : 0];
            if (signal != null)
            {
                if (signal.SignalHeads[0].SignalType.Aspects.Count > 2)
                    return false;
                else
                {
                    foreach (var signalHead in signal.SignalHeads)
                    {
                        if (signalHead.SignalType.FunctionType != SignalFunction.Distance &&
                            signalHead.SignalType.Aspects.Count == 2 &&
                            signalHead.SignalType.Aspects[0].Aspect == 0 &&
                                ((int)signalHead.SignalType.Aspects[1].Aspect == 7 ||
                                (int)signalHead.SignalType.Aspects[1].Aspect == 6 ||
                                (int)signalHead.SignalType.Aspects[1].Aspect == 2))
                            continue;
                        else
                            return false;
                    }
                    return true;
                }
            }
            return true;
        }

        private T NextGenericSignalItem<T>(int itemSequenceIndex, ref T retval, float maxDistanceM, TrainPathItemType type, string signalTypeName = "UNKNOWN")
        {
            SignalFeatures item = NextGenericSignalFeatures(signalTypeName, itemSequenceIndex, maxDistanceM, type);
            MainHeadSignalTypeName = item.MainHeadSignalTypeName;
            ItemAspect = item.Aspect;
            ItemDistance = item.Distance;
            ItemSpeedLimit = item.SpeedLimit;
            return retval;
        }

        private SignalFeatures NextGenericSignalFeatures(string signalFunctionTypeName, int itemSequenceIndex, float maxDistanceM, TrainPathItemType type)
        {
            string mainHeadSignalTypeName = string.Empty;
            string signalTypeName = string.Empty;
            TrackMonitorSignalAspect aspect = TrackMonitorSignalAspect.None;
            string drawStateName = string.Empty;
            float distanceM = float.MaxValue;
            float speedLimitMpS = -1f;
            float altitudeOrLengthM = float.MinValue;
            string textAspect = "";

            Direction dir = Locomotive.Train.MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward;

            if (Locomotive.Train.ValidRoute[(int)dir] == null || dir == Direction.Backward && Locomotive.Train.PresentPosition[dir].TrackCircuitSectionIndex < 0)
                return SignalFeatures.None;

            int index = dir == Direction.Forward ? Locomotive.Train.PresentPosition[dir].RouteListIndex :
                Locomotive.Train.ValidRoute[(int)dir].GetRouteIndex(Locomotive.Train.PresentPosition[dir].TrackCircuitSectionIndex, 0);
            if (index < 0)
                return SignalFeatures.None;
            int fn_type = OrSignalTypes.Instance.FunctionTypes.FindIndex(i => StringComparer.OrdinalIgnoreCase.Equals(i, signalFunctionTypeName));
            if (fn_type == -1) // check for not existing signal type
                return SignalFeatures.None;

            TrainPathItem trainpathItem;
            if (itemSequenceIndex > Locomotive.Train.PlayerTrainSpeedposts[dir].Count - 1 || (trainpathItem = Locomotive.Train.PlayerTrainSpeedposts[dir][itemSequenceIndex]).DistanceToTrainM > maxDistanceM)
                return SignalFeatures.None; // no n-th speedpost available or the requested speedpost is too distant

            if (type == TrainPathItemType.Signal)
            {
                // All OK, we can retrieve the data for the required signal;
                distanceM = trainpathItem.DistanceToTrainM;
                mainHeadSignalTypeName = trainpathItem.Signal.SignalHeads[0].SignalType.Name;
                if (signalFunctionTypeName.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                {
                    aspect = trainpathItem.SignalState;
                    speedLimitMpS = trainpathItem.AllowedSpeedMpS;
                    altitudeOrLengthM = trainpathItem.Signal.TdbTraveller.Y;
                }
                else
                {
                    aspect = SignalEnvironment.TranslateToTCSAspect(trainpathItem.Signal.SignalLR(fn_type));
                }
                foreach (SignalHead functionHead in trainpathItem.Signal.SignalHeads)
                {
                    if (functionHead.OrtsSignalFunctionIndex == fn_type)
                    {
                        textAspect = functionHead.TextSignalAspect;
                        signalTypeName = functionHead.SignalType.Name;
                        if (functionHead.SignalType.DrawStates.Any(d => d.Value.Index == functionHead.DrawState))
                        {
                            drawStateName = functionHead.SignalType.DrawStates.First(d => d.Value.Index == functionHead.DrawState).Value.Name;
                        }
                        break;
                    }
                }
            }
            else if (type == TrainPathItemType.Speedpost)
            {
                var playerTrainSpeedpostList = Locomotive.Train.PlayerTrainSpeedposts[dir].Where(x => !x.IsWarning).ToList();
                if (itemSequenceIndex > playerTrainSpeedpostList.Count - 1)
                    return SignalFeatures.None;
                var trainSpeedpost = playerTrainSpeedpostList[itemSequenceIndex];
                if (trainSpeedpost.DistanceToTrainM > maxDistanceM)
                    return SignalFeatures.None;

                // All OK, we can retrieve the data for the required speedpost;
                distanceM = trainpathItem.DistanceToTrainM;
                speedLimitMpS = trainpathItem.AllowedSpeedMpS;
            }
            return new SignalFeatures(mainHeadSignalTypeName, signalTypeName, aspect, drawStateName, distanceM, speedLimitMpS, altitudeOrLengthM, textAspect);
        }

        private SpeedPostFeatures NextSpeedPostFeatures(int itemSequenceIndex, float maxDistanceM)
        {
            Direction dir = Locomotive.Train.MUDirection == MidpointDirection.Reverse ? Direction.Backward : Direction.Forward;

            if (Locomotive.Train.ValidRoute[(int)dir] == null || dir == Direction.Backward && Locomotive.Train.PresentPosition[dir].TrackCircuitSectionIndex < 0)
                return SpeedPostFeatures.None;

            int index = dir == 0 ? Locomotive.Train.PresentPosition[dir].RouteListIndex :
                Locomotive.Train.ValidRoute[(int)dir].GetRouteIndex(Locomotive.Train.PresentPosition[dir].TrackCircuitSectionIndex, 0);
            if (index < 0)
                return SpeedPostFeatures.None;

            var playerTrainSpeedpostList = Locomotive.Train.PlayerTrainSpeedposts[dir];
            if (itemSequenceIndex > playerTrainSpeedpostList.Count - 1)
                return SpeedPostFeatures.None; // no n-th speedpost available
            var trainSpeedpost = playerTrainSpeedpostList[itemSequenceIndex];
            if (trainSpeedpost.DistanceToTrainM > maxDistanceM)
                return SpeedPostFeatures.None; // the requested speedpost is too distant

            // All OK, we can retrieve the data for the required speedpost;
            string speedPostTypeName = Path.GetFileNameWithoutExtension(trainSpeedpost.Signal.SpeedPostWorldObject?.SpeedPostFileName);
            bool isWarning = trainSpeedpost.IsWarning;
            float distanceM = trainSpeedpost.DistanceToTrainM;
            float speedLimitMpS = trainSpeedpost.AllowedSpeedMpS;
            float altitudeM = trainSpeedpost.Signal.TdbTraveller.Y;
            return new SpeedPostFeatures(speedPostTypeName: speedPostTypeName, isWarning: isWarning, distanceM: distanceM, speedLimitMpS: speedLimitMpS,
                altitudeM: altitudeM);
        }

        private bool DoesNextNormalSignalHaveRepeaterHead()
        {
            Signal signal = Locomotive.Train.NextSignalObject[Locomotive.Train.MUDirection == MidpointDirection.Reverse ? 1 : 0];
            if (signal != null)
            {
                foreach (SignalHead signalHead in signal.SignalHeads)
                {
                    if (signalHead.SignalType.FunctionType == SignalFunction.Repeater)
                        return true;
                }
                return false;
            }
            return false;
        }

        private bool DoesStartFromTerminalStation()
        {
            var tempTraveller = new Traveller(Locomotive.Train.RearTDBTraveller);
            tempTraveller.ReverseDirection();
            return tempTraveller.NextTrackNode() && tempTraveller.TrackNodeType == TrackNodeType.End;
        }


        public void SignalEvent(TrainEvent evt, TrainControlSystem script)
        {
            Locomotive.TriggerWagonSoundEvent(evt, script);
        }

        private static float SpeedCurve(float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        {
            if (targetSpeedMpS < 0)
                targetSpeedMpS = 0;

            decelerationMpS2 -= GravityMpS2 * slope;

            float squareSpeedComponent = targetSpeedMpS * targetSpeedMpS
                + delayS * delayS * decelerationMpS2 * decelerationMpS2
                + 2f * targetDistanceM * decelerationMpS2;

            float speedComponent = delayS * decelerationMpS2;

            return (float)Math.Sqrt(squareSpeedComponent) - speedComponent;
        }

        private static float DistanceCurve(float currentSpeedMpS, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        {
            if (targetSpeedMpS < 0)
                targetSpeedMpS = 0;

            float brakingDistanceM = (currentSpeedMpS * currentSpeedMpS - targetSpeedMpS * targetSpeedMpS)
                / (2 * (decelerationMpS2 - GravityMpS2 * slope));

            float delayDistanceM = delayS * currentSpeedMpS;

            return brakingDistanceM + delayDistanceM;
        }

        private static float Deceleration(float currentSpeedMpS, float targetSpeedMpS, float distanceM)
        {
            return (currentSpeedMpS - targetSpeedMpS) * (currentSpeedMpS + targetSpeedMpS) / (2 * distanceM);
        }

        public void Update(double elapsedClockSeconds)
        {
            switch (Locomotive.Train.TrainType)
            {
                case TrainType.Static:
                case TrainType.Ai:
                case TrainType.AiNotStarted:
                case TrainType.AiAutoGenerated:
                case TrainType.Remote:
                case TrainType.AiIncorporated:
                    DisableRestrictions();
                    break;

                default:
                    if (Locomotive == Simulator.PlayerLocomotive || Locomotive.Train.PlayerTrainSignals == null)
                        Locomotive.Train.UpdatePlayerTrainData();
                    if (script == null)
                    {
                        DisableRestrictions();
                    }
                    else
                    {
                        ClearParams();
                        script.Update();
                    }
                    break;
            }
        }

        public void DisableRestrictions()
        {
            PowerAuthorization = true;
            if (Locomotive.TrainBrakeController != null)
            {
                Locomotive.TrainBrakeController.TCSFullServiceBraking = false;
                Locomotive.TrainBrakeController.TCSEmergencyBraking = false;
            }
        }

        public void ClearParams()
        {
            _ = activated; //just to satisfy the code analyzer
        }

        public void AlerterPressed(bool pressed)
        {
            AlerterButtonPressed = pressed;
            HandleEvent(pressed ? TCSEvent.AlerterPressed : TCSEvent.AlerterReleased);
        }

        public void AlerterReset()
        {
            HandleEvent(TCSEvent.AlerterReset);
        }

        public void HandleEvent(TCSEvent evt)
        {
            HandleEvent(evt, string.Empty);
        }

        public void HandleEvent(TCSEvent evt, string message)
        {
            script?.HandleEvent(evt, message);

            switch (evt)
            {
                case TCSEvent.EmergencyBrakingRequestedBySimulator:
                    SimulatorEmergencyBraking = true;
                    break;

                case TCSEvent.EmergencyBrakingReleasedBySimulator:
                    SimulatorEmergencyBraking = false;
                    break;
            }
        }

        public void HandleEvent(TCSEvent evt, int eventIndex)
        {
            HandleEvent(evt, $"{eventIndex}");
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            HandleEvent(evt, string.Empty);
        }

        public void HandleEvent(PowerSupplyEvent evt, string message)
        {
            script?.HandleEvent(evt, message);
        }

        private T LoadParameter<T>(string sectionName, string keyName, T defaultValue)
        {
            string buffer;
            int length;

            if (File.Exists(trainParametersFileName))
            {
                buffer = new string('\0', 256);
                length = Common.Native.NativeMethods.GetPrivateProfileString(sectionName, keyName, null, buffer, buffer.Length, trainParametersFileName);
                if (length > 0)
                {
                    buffer = buffer.Trim();
                    return (T)Convert.ChangeType(buffer, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (File.Exists(parametersFileName))
            {
                buffer = new string('\0', 256);
                length = Common.Native.NativeMethods.GetPrivateProfileString(sectionName, keyName, null, buffer, buffer.Length, parametersFileName);

                if (length > 0)
                {
                    buffer = buffer.Trim();
                    return (T)Convert.ChangeType(buffer, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return defaultValue;
        }

        // Converts the generic string (e.g. ORTS_TCS5) shown when browsing with the mouse on a TCS control
        // to a customized string defined in the script
        public string GetDisplayString(string source)
        {
            if (string.IsNullOrEmpty(source) || source.Length < 9 || !source[..8].Equals("ORTS_TCS", StringComparison.OrdinalIgnoreCase))
                return source;

            string result = null;
            if (int.TryParse(source[8..], out int commandIndex) && commandIndex <= TCSCabviewControlCount)
                result = customizedCabviewControlNames[commandIndex - 1];
            return string.IsNullOrEmpty(result) ? source : result;
        }

        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));
            outf.Write(scriptName ?? "");
            if (!string.IsNullOrEmpty(scriptName))
                script.Save(outf);
        }

        public void Restore(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));
            scriptName = inf.ReadString();
            if (!string.IsNullOrEmpty(scriptName))
            {
                Initialize();
                script.Restore(inf);
            }
        }
    }
}
