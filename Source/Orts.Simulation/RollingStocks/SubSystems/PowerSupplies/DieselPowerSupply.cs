// COPYRIGHT 2020 by the Open Rails project.
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

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;
using Orts.Scripting.Api.PowerSupply;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{

    public class ScriptedDieselPowerSupply : ScriptedLocomotivePowerSupply
    {
        public MSTSDieselLocomotive DieselLocomotive => Locomotive as MSTSDieselLocomotive;
        public ScriptedTractionCutOffRelay TractionCutOffRelay { get; protected set; }
        protected DieselEngines DieselEngines => DieselLocomotive.DieselEngines;

        public override PowerSupplyType Type => PowerSupplyType.DieselElectric;
        public bool Activated;
        private DieselPowerSupply script => abstractScript as DieselPowerSupply;

        public float DieselEngineMinRpmForElectricTrainSupply { get; protected set; }
        public float DieselEngineMinRpm => ElectricTrainSupplyOn ? DieselEngineMinRpmForElectricTrainSupply : 0f;

        public ScriptedDieselPowerSupply(MSTSDieselLocomotive locomotive) :
            base(locomotive)
        {
            TractionCutOffRelay = new ScriptedTractionCutOffRelay(this);
        }

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortstractioncutoffrelay":
                case "engine(ortstractioncutoffrelayclosingdelay":
                    TractionCutOffRelay.Parse(lowercasetoken, stf);
                    break;

                case "engine(ortselectrictrainsupply(dieselengineminrpm":
                    DieselEngineMinRpmForElectricTrainSupply = stf.ReadFloatBlock(STFReader.Units.None, 0f);
                    break;

                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        public override void Copy(IPowerSupply source)
        {
            base.Copy(source);

            if (source is ScriptedDieselPowerSupply scriptedOther)
            {
                TractionCutOffRelay.Copy(scriptedOther.TractionCutOffRelay);
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            if (!Activated)
            {
                if (scriptName != null && scriptName != "Default")
                {
                    abstractScript = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"), scriptName) as DieselPowerSupply;
                }
                if (script == null)
                {
                    abstractScript = new DefaultDieselPowerSupply();
                }

                AssignScriptFunctions();

                script.Initialize();
                Activated = true;
            }

            TractionCutOffRelay.Initialize();
        }

        public override async ValueTask<PowerSupplySaveState> Snapshot()
        {
            PowerSupplySaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.TractionCutOffRelayState = await TractionCutOffRelay.Snapshot().ConfigureAwait(false);
            return saveState;
        }

        public override async ValueTask Restore([NotNull] PowerSupplySaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            await TractionCutOffRelay.Restore(saveState.TractionCutOffRelayState).ConfigureAwait(false);
        }

        //================================================================================================//
        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        public override void InitializeMoving()
        {
            base.InitializeMoving();

            TractionCutOffRelay.InitializeMoving();
        }
        public override void Update(double elapsedClockSeconds)
        {
            base.Update(elapsedClockSeconds);

            TractionCutOffRelay.Update(elapsedClockSeconds);

            script?.Update(elapsedClockSeconds);
        }

        protected override void AssignScriptFunctions()
        {
            base.AssignScriptFunctions();

            // DieselPowerSupply getters
            script.CurrentDieselEnginesState = () => DieselLocomotive.DieselEngines.State;
            script.CurrentDieselEngineState = (id) =>
            {
                if (id >= 0 && id < DieselEngines.Count)
                {
                    return DieselEngines[id].State;
                }
                else
                {
                    return DieselEngineState.Unavailable;
                }
            };
            script.CurrentTractionCutOffRelayState = () => TractionCutOffRelay.State;
            script.TractionCutOffRelayDriverClosingOrder = () => TractionCutOffRelay.DriverClosingOrder;
            script.TractionCutOffRelayDriverOpeningOrder = () => TractionCutOffRelay.DriverOpeningOrder;
            script.TractionCutOffRelayDriverClosingAuthorization = () => TractionCutOffRelay.DriverClosingAuthorization;

            // DieselPowerSupply setters
            script.SignalEventToDieselEngines = (evt) => DieselEngines.HandleEvent(evt);
            script.SignalEventToDieselEngine = (evt, id) => DieselEngines.HandleEvent(evt, id);
            script.SignalEventToTractionCutOffRelay = (evt) => TractionCutOffRelay.HandleEvent(evt);
        }
    }

    public class DefaultDieselPowerSupply : DieselPowerSupply
    {
        private Timer PowerOnTimer;
        private Timer AuxPowerOnTimer;

        /// <remarks>
        /// Used for the corresponding first engine on/off sound triggers.
        /// </remarks>
        private DieselEngineState PreviousFirstEngineState;
        /// <remarks>
        /// Used for the corresponding second engine on/off sound triggers.
        /// </remarks>
        private DieselEngineState PreviousSecondEngineState;

        private bool QuickPowerOn;

        public override void Initialize()
        {
            PowerOnTimer = new Timer(this);
            PowerOnTimer.Setup(PowerOnDelayS());

            AuxPowerOnTimer = new Timer(this);
            AuxPowerOnTimer.Setup(AuxPowerOnDelayS());

            PreviousFirstEngineState = CurrentDieselEngineState(0);
            PreviousSecondEngineState = CurrentDieselEngineState(1);
        }

        public override void Update(double elapsedClockSeconds)
        {
            SetCurrentBatteryState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentLowVoltagePowerSupplyState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentCabPowerSupplyState(BatterySwitchOn() && MasterKeyOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);

            switch (CurrentDieselEnginesState())
            {
                case DieselEngineState.Stopped:
                case DieselEngineState.Stopping:
                case DieselEngineState.Starting:
                    if (PowerOnTimer.Started)
                        PowerOnTimer.Stop();
                    if (AuxPowerOnTimer.Started)
                        AuxPowerOnTimer.Stop();

                    if (CurrentMainPowerSupplyState() == PowerSupplyState.PowerOn)
                    {
                        SignalEvent(TrainEvent.EnginePowerOff);
                        SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOff);
                    }
                    SetCurrentAuxiliaryPowerSupplyState(PowerSupplyState.PowerOff);
                    break;

                case DieselEngineState.Running:
                    switch (CurrentTractionCutOffRelayState())
                    {
                        case TractionCutOffRelayState.Open:
                            // If traction cut-off relay is open, then it must be closed to finish the quick power-on sequence
                            if (QuickPowerOn)
                            {
                                QuickPowerOn = false;
                                SignalEventToTractionCutOffRelay(PowerSupplyEvent.CloseTractionCutOffRelay);
                            }

                            if (PowerOnTimer.Started)
                                PowerOnTimer.Stop();

                            if (CurrentMainPowerSupplyState() == PowerSupplyState.PowerOn)
                            {
                                SignalEvent(TrainEvent.EnginePowerOff);
                                SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOff);
                            }
                            break;

                        case TractionCutOffRelayState.Closed:
                            // If traction cut-off relay is closed, quick power-on sequence has finished
                            QuickPowerOn = false;

                            if (!PowerOnTimer.Started)
                                PowerOnTimer.Start();

                            if (PowerOnTimer.Triggered && CurrentMainPowerSupplyState() == PowerSupplyState.PowerOff)
                            {
                                SignalEvent(TrainEvent.EnginePowerOn);
                                SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOn);
                            }
                            break;
                    }

                    if (!AuxPowerOnTimer.Started)
                        AuxPowerOnTimer.Start();

                    SetCurrentAuxiliaryPowerSupplyState(AuxPowerOnTimer.Triggered ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
                    break;
            }

            // By default, on diesel locomotives, dynamic brake is available only if main power is available.
            SetCurrentDynamicBrakeAvailability(CurrentMainPowerSupplyState() == PowerSupplyState.PowerOn);

            if (ElectricTrainSupplyUnfitted())
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.Unavailable);
            }
            else if (CurrentAuxiliaryPowerSupplyState() == PowerSupplyState.PowerOn
                    && ElectricTrainSupplySwitchOn())
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOn);
            }
            else
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOff);
            }

            UpdateSounds();
        }

        protected void UpdateSounds()
        {
            // First engine
            if ((PreviousFirstEngineState == DieselEngineState.Stopped
                || PreviousFirstEngineState == DieselEngineState.Stopping)
                && (CurrentDieselEngineState(0) == DieselEngineState.Starting
                || CurrentDieselEngineState(0) == DieselEngineState.Running))
            {
                SignalEvent(TrainEvent.EnginePowerOn);
            }
            else if ((PreviousFirstEngineState == DieselEngineState.Starting
                || PreviousFirstEngineState == DieselEngineState.Running)
                && (CurrentDieselEngineState(0) == DieselEngineState.Stopping
                || CurrentDieselEngineState(0) == DieselEngineState.Stopped))
            {
                SignalEvent(TrainEvent.EnginePowerOff);
            }
            PreviousFirstEngineState = CurrentDieselEngineState(0);

            // Second engine
            if ((PreviousSecondEngineState == DieselEngineState.Stopped
                || PreviousSecondEngineState == DieselEngineState.Stopping)
                && (CurrentDieselEngineState(1) == DieselEngineState.Starting
                || CurrentDieselEngineState(1) == DieselEngineState.Running))
            {
                SignalEvent(TrainEvent.SecondEnginePowerOn);
            }
            else if ((PreviousSecondEngineState == DieselEngineState.Starting
                || PreviousSecondEngineState == DieselEngineState.Running)
                && (CurrentDieselEngineState(1) == DieselEngineState.Stopping
                || CurrentDieselEngineState(1) == DieselEngineState.Stopped))
            {
                SignalEvent(TrainEvent.SecondEnginePowerOff);
            }
            PreviousSecondEngineState = CurrentDieselEngineState(1);
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.QuickPowerOn:
                    QuickPowerOn = true;
                    SignalEventToBatterySwitch(PowerSupplyEvent.CloseBatterySwitch);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOnMasterKey);
                    SignalEventToDieselEngines(PowerSupplyEvent.StartEngine);
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOnElectricTrainSupply);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    QuickPowerOn = false;
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOffElectricTrainSupply);
                    SignalEventToTractionCutOffRelay(PowerSupplyEvent.OpenTractionCutOffRelay);
                    SignalEventToDieselEngines(PowerSupplyEvent.StopEngine);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOffMasterKey);
                    SignalEventToBatterySwitch(PowerSupplyEvent.OpenBatterySwitch);
                    break;

                case PowerSupplyEvent.TogglePlayerEngine:
                    switch (CurrentDieselEngineState(0))
                    {
                        case DieselEngineState.Stopped:
                        case DieselEngineState.Stopping:
                            SignalEventToDieselEngine(PowerSupplyEvent.StartEngine, 0);
                            Confirm(CabControl.PlayerDiesel, CabSetting.On);
                            break;

                        case DieselEngineState.Starting:
                            SignalEventToDieselEngine(PowerSupplyEvent.StopEngine, 0);
                            Confirm(CabControl.PlayerDiesel, CabSetting.Off);
                            break;

                        case DieselEngineState.Running:
                            if (ThrottlePercent() < 1)
                            {
                                SignalEventToDieselEngine(PowerSupplyEvent.StopEngine, 0);
                                Confirm(CabControl.PlayerDiesel, CabSetting.Off);
                            }
                            else
                            {
                                Confirm(CabControl.PlayerDiesel, CabSetting.Warn1);
                            }
                            break;
                    }
                    break;

                default:
                    base.HandleEvent(evt);
                    break;
            }
        }
    }
}
