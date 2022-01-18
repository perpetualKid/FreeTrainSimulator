// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.IO;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;
using Orts.Simulation.AIs;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public class ScriptedBrakeController : IController, INameValueInformationProvider
    {
        private protected readonly DetailInfoBase brakeInfo = new DetailInfoBase();
        private bool updateBrakeStatus;

        private protected readonly MSTSLocomotive locomotive;
        private readonly Simulator simulator;

        private string scriptName = "MSTS";
        private protected BrakeController script;
        private readonly List<IControllerNotch> notches = new List<IControllerNotch>();

        private bool emergencyBrakingPushButton;
        private bool tcsEmergencyBraking;
        private bool tcsFullServiceBraking;
        private bool overchargeButtonPressed;
        private bool quickReleaseButtonPressed;

        public bool EmergencyBraking => emergencyBrakingPushButton || tcsEmergencyBraking || BrakeController.IsEmergencyState(script.State);

        public bool EmergencyBrakingPushButton
        {
            get => emergencyBrakingPushButton;
            set
            {
                if (simulator.Confirmer != null)
                {
                    if (value && !emergencyBrakingPushButton && !tcsEmergencyBraking)
                        simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.On);
                    else if (!value && emergencyBrakingPushButton && !tcsEmergencyBraking)
                        simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.Off);
                }

                emergencyBrakingPushButton = value;
            }
        }

        public bool TCSEmergencyBraking
        {
            get => tcsEmergencyBraking;
            set
            {
                if (simulator.Confirmer != null)
                {
                    if (value && !emergencyBrakingPushButton && !tcsEmergencyBraking)
                        simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.On);
                    else if (!value && !emergencyBrakingPushButton && tcsEmergencyBraking)
                        simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.Off);
                }

                tcsEmergencyBraking = value;
            }
        }

        public bool TCSFullServiceBraking
        {
            get => tcsFullServiceBraking;
            set
            {
                if (simulator.Confirmer != null)
                {
                    if (value && !tcsFullServiceBraking)
                        simulator.Confirmer.Confirm(CabControl.TrainBrake, CabSetting.On);
                }

                tcsFullServiceBraking = value;
            }
        }

        public bool QuickReleaseButtonPressed
        {
            get => quickReleaseButtonPressed;
            set
            {
                if (simulator.Confirmer != null)
                {
                    if (value && !quickReleaseButtonPressed)
                        simulator.Confirmer.Confirm(CabControl.QuickRelease, CabSetting.On);
                    else if (!value && quickReleaseButtonPressed)
                        simulator.Confirmer.Confirm(CabControl.QuickRelease, CabSetting.Off);
                }

                quickReleaseButtonPressed = value;
            }
        }

        public bool OverchargeButtonPressed
        {
            get => overchargeButtonPressed;
            set
            {
                if (simulator.Confirmer != null)
                {
                    if (value && !overchargeButtonPressed)
                        simulator.Confirmer.Confirm(CabControl.Overcharge, CabSetting.On);
                    else if (!value && overchargeButtonPressed)
                        simulator.Confirmer.Confirm(CabControl.Overcharge, CabSetting.Off);
                }

                overchargeButtonPressed = value;
            }
        }

        public float MaxPressurePSI { get; set; }
        public float MaxOverchargePressurePSI { get; private set; }
        public float ReleaseRatePSIpS { get; private set; }
        public float QuickReleaseRatePSIpS { get; private set; }
        public float OverchargeEliminationRatePSIpS { get; private set; }
        public float SlowApplicationRatePSIpS { get; private set; }
        public float ApplyRatePSIpS { get; private set; }
        public float EmergencyRatePSIpS { get; private set; }
        public float FullServReductionPSI { get; private set; }
        public float MinReductionPSI { get; private set; }

        /// <summary>
        /// Needed for proper mouse operation in the cabview
        /// </summary>
        public float IntermediateValue { get { return script is MSTSBrakeController ? (script as MSTSBrakeController).NotchController.IntermediateValue : CurrentValue; } }

        /// <summary>
        /// Knowing actual notch and its change is needed for proper repeatability of mouse and RailDriver operation
        /// </summary>
        public int NotchIndex { get { return script is MSTSBrakeController ? (script as MSTSBrakeController).NotchController.NotchIndex : 0; } set { } }

        public ControllerState TrainBrakeControllerState
        {
            get
            {
                return script is MSTSBrakeController
                    ? notches.Count > 0 ? notches[NotchIndex].NotchStateType : ControllerState.Dummy
                    : script.State;
            }
        }

        private float oldValue;

        public float CurrentValue { get; set; }
        public float MinimumValue { get; set; }
        public float MaximumValue { get; set; }
        public float StepSize { get; set; }
        public float UpdateValue { get; set; }
        public double CommandStartTime { get; set; }

        public ScriptedBrakeController(MSTSLocomotive locomotive)
        {
            simulator = Simulator.Instance;
            this.locomotive = locomotive;

            MaxPressurePSI = 90;
            MaxOverchargePressurePSI = 95;
            ReleaseRatePSIpS = 5;
            QuickReleaseRatePSIpS = 10;
            OverchargeEliminationRatePSIpS = 0.036f;
            ApplyRatePSIpS = 2;
            SlowApplicationRatePSIpS = 1;
            EmergencyRatePSIpS = 10;
            FullServReductionPSI = 26;
            MinReductionPSI = 6;
        }

        protected ScriptedBrakeController(ScriptedBrakeController source, MSTSLocomotive locomotive)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));

            simulator = Simulator.Instance;
            this.locomotive = locomotive;

            scriptName = source.scriptName;
            MaxPressurePSI = source.MaxPressurePSI;
            MaxOverchargePressurePSI = source.MaxOverchargePressurePSI;
            ReleaseRatePSIpS = source.ReleaseRatePSIpS;
            QuickReleaseRatePSIpS = source.QuickReleaseRatePSIpS;
            OverchargeEliminationRatePSIpS = source.OverchargeEliminationRatePSIpS;
            ApplyRatePSIpS = source.ApplyRatePSIpS;
            SlowApplicationRatePSIpS = source.SlowApplicationRatePSIpS;
            EmergencyRatePSIpS = source.EmergencyRatePSIpS;
            FullServReductionPSI = source.FullServReductionPSI;
            MinReductionPSI = source.MinReductionPSI;

            CurrentValue = source.CurrentValue;
            MinimumValue = source.MinimumValue;
            MaximumValue = source.MaximumValue;
            StepSize = source.StepSize;

            source.notches.ForEach((item) => { notches.Add(new MSTSNotch(item)); });

            Initialize();
        }

        public static ScriptedBrakeController From(ScriptedBrakeController source, MSTSLocomotive locomotive)
        {
            return source switch
            {
                ScriptedEngineBrakeController => new ScriptedEngineBrakeController(source, locomotive),
                ScriptedTrainBrakeController => new ScriptedTrainBrakeController(source, locomotive),
                ScriptedBrakeController => new ScriptedBrakeController(source, locomotive),
                _ => null,
            };
        }

        public void Parse(STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);
            Parse(stf.Tree.ToLower(), stf);
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            ArgumentNullException.ThrowIfNull(stf);

            switch (lowercasetoken)
            {
                case "engine(trainbrakescontrollermaxsystempressure":
                case "engine(enginebrakescontrollermaxsystempressure":
                    MaxPressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(ortstrainbrakescontrollermaxoverchargepressure":
                    MaxOverchargePressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(trainbrakescontrollermaxreleaserate":
                case "engine(enginebrakescontrollermaxreleaserate":
                    ReleaseRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrollermaxquickreleaserate":
                case "engine(enginebrakescontrollermaxquickreleaserate":
                    QuickReleaseRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(ortstrainbrakescontrolleroverchargeeliminationrate":
                    OverchargeEliminationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrollermaxapplicationrate":
                case "engine(enginebrakescontrollermaxapplicationrate":
                    ApplyRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrolleremergencyapplicationrate":
                case "engine(enginebrakescontrolleremergencyapplicationrate":
                    EmergencyRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrollerfullservicepressuredrop":
                case "engine(enginebrakescontrollerfullservicepressuredrop":
                    FullServReductionPSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(trainbrakescontrollerminpressurereduction":
                case "engine(enginebrakescontrollerminpressurereduction":
                    MinReductionPSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(ortstrainbrakescontrollerslowapplicationrate":
                case "engine(ortsenginebrakescontrollerslowapplicationrate":
                    SlowApplicationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(enginecontrollers(brake_train":
                case "engine(enginecontrollers(brake_engine":
                case "engine(enginecontrollers(brake_brakeman":
                    stf.MustMatch("(");
                    MinimumValue = stf.ReadFloat(STFReader.Units.None, null);
                    MaximumValue = stf.ReadFloat(STFReader.Units.None, null);
                    StepSize = stf.ReadFloat(STFReader.Units.None, null);
                    CurrentValue = stf.ReadFloat(STFReader.Units.None, null);
                    string token = stf.ReadItem(); // s/b numnotches
                    if (!string.Equals(token, "NumNotches", StringComparison.OrdinalIgnoreCase)) // handle error in gp38.eng where extra parameter provided before NumNotches statement 
                        stf.ReadItem();
                    stf.MustMatch("(");
                    stf.ReadInt(null);
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("notch", ()=>{
                            stf.MustMatch("(");
                            float value = stf.ReadFloat(STFReader.Units.None, null);
                            int smooth = stf.ReadInt(null);
                            string type = stf.ReadString();
                            notches.Add(new MSTSNotch(value, smooth, type, stf));
                            if (type != ")") stf.SkipRestOfBlock();
                        }),
                    });
                    break;

                case "engine(ortstrainbrakecontroller":
                case "engine(ortsenginebrakecontroller":
                    scriptName = stf.ReadStringBlock(null);
                    break;
            }
        }

        public void Initialize()
        {
            if (scriptName != null && scriptName != "MSTS")
            {
                script = simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(locomotive.WagFilePath), "Script"), scriptName) as BrakeController;
            }
            if (script == null)
            {
                script = new MSTSBrakeController();
                (script as MSTSBrakeController).ForceControllerReleaseGraduated = simulator.Settings.GraduatedRelease;
            }

            // AbstractScriptClass
            script.ClockTime = () => (float)simulator.ClockTime;
            script.GameTime = () => (float)simulator.GameTime;
            script.PreUpdate = () => simulator.PreUpdate;
            script.DistanceM = () => locomotive.DistanceTravelled;
            script.SpeedMpS = () => Math.Abs(locomotive.SpeedMpS);
            script.Confirm = simulator.Confirmer.Confirm;
            script.Message = simulator.Confirmer.Message;
            script.SignalEvent = locomotive.SignalEvent;
            script.SignalEventToTrain = (evt) =>
            {
                if (locomotive.Train != null)
                {
                    locomotive.Train.SignalEvent(evt);
                }
            };

            // BrakeController
            script.EmergencyBrakingPushButton = () => EmergencyBrakingPushButton;
            script.TCSEmergencyBraking = () => TCSEmergencyBraking;
            script.TCSFullServiceBraking = () => TCSFullServiceBraking;
            script.QuickReleaseButtonPressed = () => QuickReleaseButtonPressed;
            script.OverchargeButtonPressed = () => OverchargeButtonPressed;
            script.IsLowVoltagePowerSupplyOn = () => locomotive.LocomotivePowerSupply.LowVoltagePowerSupplyOn;
            script.IsCabPowerSupplyOn = () => locomotive.LocomotivePowerSupply.CabPowerSupplyOn;

            script.MainReservoirPressureBar = () =>
            {
                return locomotive.Train != null ? (float)Pressure.Atmospheric.FromPSI(locomotive.MainResPressurePSI) : float.MaxValue;
            };
            script.MaxPressureBar = () => (float)Pressure.Atmospheric.FromPSI(MaxPressurePSI);
            script.MaxOverchargePressureBar = () => (float)Pressure.Atmospheric.FromPSI(MaxOverchargePressurePSI);
            script.ReleaseRateBarpS = () => (float)Rate.Pressure.FromPSIpS(ReleaseRatePSIpS);
            script.QuickReleaseRateBarpS = () => (float)Rate.Pressure.FromPSIpS(QuickReleaseRatePSIpS);
            script.OverchargeEliminationRateBarpS = () => (float)Rate.Pressure.FromPSIpS(OverchargeEliminationRatePSIpS);
            script.SlowApplicationRateBarpS = () => (float)Rate.Pressure.FromPSIpS(SlowApplicationRatePSIpS);
            script.ApplyRateBarpS = () => (float)Rate.Pressure.FromPSIpS(ApplyRatePSIpS);
            script.EmergencyRateBarpS = () => (float)Rate.Pressure.FromPSIpS(EmergencyRatePSIpS);
            script.FullServReductionBar = () => (float)Pressure.Atmospheric.FromPSI(FullServReductionPSI);
            script.MinReductionBar = () => (float)Pressure.Atmospheric.FromPSI(MinReductionPSI);
            script.CurrentValue = () => CurrentValue;
            script.MinimumValue = () => MinimumValue;
            script.MaximumValue = () => MaximumValue;
            script.StepSize = () => StepSize;
            script.UpdateValue = () => UpdateValue;
            script.Notches = () => notches;
            script.CruiseControlBrakeDemand = () => locomotive.CruiseControl != null ? locomotive.CruiseControl.TrainBrakePercent / 100 : 0;

            script.SetCurrentValue = (value) => CurrentValue = value;
            script.SetUpdateValue = (value) => UpdateValue = value;

            script.SetDynamicBrakeIntervention = (value) =>
            {
                // TODO: Set dynamic brake intervention instead of controller position
                // There are some issues that need to be identified and fixed before setting the intervention directly
                if (locomotive.DynamicBrakeController == null)
                    return;
                locomotive.DynamicBrakeChangeActiveState(value > 0);
                locomotive.DynamicBrakeController.SetValue(value);
            };

            script.Initialize();
        }

        public void InitializeMoving()
        {
            script.InitializeMoving();
        }

        public float Update(double elapsedSeconds)
        {
            float result = script?.Update(elapsedSeconds) ?? 0;
            if (updateBrakeStatus)
            {
                UpdateBrakeStatus();
                updateBrakeStatus = false;
            }
            return result;
        }

        public (double pressurePSI, double epPressureBar) UpdatePressure(double pressurePSI, double epPressureBar, double elapsedClockSeconds)
        {
            if (script != null)
            {
                // Conversion is needed until the pressures of the brake system are converted to Pressure.Atmospheric.
                double pressureBar = Pressure.Atmospheric.FromPSI(pressurePSI);
                var result = script.UpdatePressure(pressureBar, epPressureBar, elapsedClockSeconds);
                pressurePSI = Pressure.Atmospheric.ToPSI(result.Item1);
                epPressureBar = result.Item2;
            }
            return (pressurePSI, epPressureBar);
        }

        public double UpdateEngineBrakePressure(double pressurePSI, double elapsedClockSeconds)
        {
            if (script != null)
            {
                // Conversion is needed until the pressures of the brake system are converted to Pressure.Atmospheric.
                double pressureBar = Pressure.Atmospheric.FromPSI(pressurePSI);
                pressureBar = script.UpdateEngineBrakePressure(pressureBar, elapsedClockSeconds);
                pressurePSI = Pressure.Atmospheric.ToPSI(pressureBar);
            }
            return pressurePSI;
        }

        public void SignalEvent(BrakeControllerEvent evt)
        {
            if (script != null)
                script.HandleEvent(evt);
        }

        public void SignalEvent(BrakeControllerEvent evt, float? value)
        {
            if (script != null)
                script.HandleEvent(evt, value);
            else
            {
                if (evt == BrakeControllerEvent.SetCurrentValue && value.HasValue)
                {
                    CurrentValue = value.Value;
                }
            }
        }

        public void StartIncrease()
        {
            SignalEvent(BrakeControllerEvent.StartIncrease);
        }

        public void StopIncrease()
        {
            SignalEvent(BrakeControllerEvent.StopIncrease);
        }

        public void StartDecrease()
        {
            SignalEvent(BrakeControllerEvent.StartDecrease);
        }

        public void StopDecrease()
        {
            SignalEvent(BrakeControllerEvent.StopDecrease);
        }

        public void StartIncrease(float? target)
        {
            SignalEvent(BrakeControllerEvent.StartIncrease, target);
        }

        public void StartDecrease(float? target, bool toZero = false)
        {
            if (toZero)
                SignalEvent(BrakeControllerEvent.StartDecreaseToZero, target);
            else
                SignalEvent(BrakeControllerEvent.StartDecrease, target);
        }

        public float SetPercent(float percent)
        {
            SignalEvent(BrakeControllerEvent.SetCurrentPercent, percent);
            return CurrentValue;
        }

        public int SetValue(float value)
        {
            int oldNotch = NotchIndex;
            SignalEvent(BrakeControllerEvent.SetCurrentValue, value);

            int change = NotchIndex > oldNotch || CurrentValue > oldValue + 0.1f || CurrentValue == 1 && oldValue < 1
                ? 1 : NotchIndex < oldNotch || CurrentValue < oldValue - 0.1f || CurrentValue == 0 && oldValue > 0 ? -1 : 0;
            if (change != 0)
                oldValue = CurrentValue;
            return change;
        }

        public bool IsValid()
        {
            return script == null || script.IsValid();
        }

        public ControllerState State => script?.State ?? ControllerState.Dummy;

        public float ControllerValue => script?.StateFraction ?? float.NaN;

        public string GetStatus()
        {
            string status = script?.State.GetLocalizedDescription();
            string percentage = script?.StateFraction > -1 ? $"{script.StateFraction * 100:N0}%" : null;
            return FormatStrings.JoinIfNotEmpty(' ', status, percentage);
        }

        public InformationDictionary DetailInfo => GetBrakeStatus();

        public Dictionary<string, FormatOption> FormattingOptions => brakeInfo.FormattingOptions;

        public void Save(BinaryWriter outf)
        {
            outf.Write((int)ControllerTypes.BrakeController);

            outf.Write(CurrentValue);

            outf.Write(EmergencyBrakingPushButton);
            outf.Write(TCSEmergencyBraking);
            outf.Write(TCSFullServiceBraking);
        }

        public void Restore(BinaryReader inf)
        {
            SignalEvent(BrakeControllerEvent.SetCurrentValue, inf.ReadSingle());

            EmergencyBrakingPushButton = inf.ReadBoolean();
            TCSEmergencyBraking = inf.ReadBoolean();
            TCSFullServiceBraking = inf.ReadBoolean();
        }

        private protected virtual void UpdateBrakeStatus()
        {
            brakeInfo["State"] = script?.State.GetLocalizedDescription();
            float fraction = script?.StateFraction ?? float.NaN;
            brakeInfo["Value"] = float.IsNaN(fraction) ? null : $"{fraction * 100:N0}%";
            brakeInfo["Status"] = FormatStrings.JoinIfNotEmpty(' ', brakeInfo["State"], brakeInfo["Value"]);
            brakeInfo["StatusShort"] = FormatStrings.JoinIfNotEmpty(' ', brakeInfo["State"].Max(string.IsNullOrEmpty(brakeInfo["Value"]) ? 20 : 5), brakeInfo["Value"]);
        }

        private InformationDictionary GetBrakeStatus()
        {
            updateBrakeStatus = true;
            return brakeInfo;
        }
    }

    public class ScriptedTrainBrakeController : ScriptedBrakeController
    {
        public ScriptedTrainBrakeController(MSTSLocomotive locomotive) :
            base(locomotive)
        { }

        internal ScriptedTrainBrakeController(ScriptedBrakeController source, MSTSLocomotive locomotive) :
            base(source, locomotive)
        { }
    }

    public class ScriptedEngineBrakeController : ScriptedBrakeController
    {
        public ScriptedEngineBrakeController(MSTSLocomotive locomotive) :
            base(locomotive)
        { }

        internal ScriptedEngineBrakeController(ScriptedBrakeController source, MSTSLocomotive locomotive) :
            base(source, locomotive)
        { }

        private protected override void UpdateBrakeStatus()
        {
            base.UpdateBrakeStatus();
            // If brake type is only a state, and no numerical fraction application is displayed in the HUD, then display Brake Cylinder (BC) pressure
            if (script == null || float.IsNaN(script.StateFraction)) // Test to see if a brake state only is present without a fraction of application, if no value, display BC pressure
            {
                if (locomotive.BrakeSystem is VacuumSinglePipe)
                {
                    if (locomotive.SteamEngineBrakeFitted)
                    {
                        brakeInfo["Value"] = $"{(int)(CurrentValue * 100):N0}%";
                        brakeInfo["Status"] = FormatStrings.JoinIfNotEmpty(' ', brakeInfo["State"], brakeInfo["Value"]);
                        brakeInfo["StatusShort"] = FormatStrings.JoinIfNotEmpty(' ', brakeInfo["State"].Max(5), brakeInfo["Value"]);
                    }
                    else
                    {
                        brakeInfo["BC"] = locomotive.BrakeSystem.BrakeInfo.DetailInfo["BC"];
                    }
                }
                else
                {
                    brakeInfo["BC"] = locomotive.BrakeSystem.BrakeInfo.DetailInfo["BC"];
                    brakeInfo["BailOff"] = locomotive.BailOff ? Simulator.Catalog.GetString("BailOff") : null;
                }
                // Fraction not found so display BC                
            }
            else
            {
                brakeInfo["BailOff"] = locomotive.BailOff ? Simulator.Catalog.GetString("BailOff") : null;
            }
        }
    }
}