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

using System.IO;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;
using Orts.Scripting.Api.PowerSupply;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{

    public class ScriptedElectricPowerSupply : ScriptedLocomotivePowerSupply
    {
        public MSTSElectricLocomotive ElectricLocomotive => Locomotive as MSTSElectricLocomotive;
        public Pantographs Pantographs => Locomotive.Pantographs;
        public ScriptedCircuitBreaker CircuitBreaker { get; protected set; }

        public override PowerSupplyType Type => PowerSupplyType.Electric;
        public bool Activated;
        private ElectricPowerSupply Script => abstractScript as ElectricPowerSupply;

        public float LineVoltageV => (float)Simulator.Route.MaxLineVoltage;
        public float PantographVoltageV { get; set; }
        public float FilterVoltageV { get; set; }

        public ScriptedElectricPowerSupply(MSTSLocomotive locomotive) :
            base(locomotive)
        {
            CircuitBreaker = new ScriptedCircuitBreaker(this);
        }

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortscircuitbreaker":
                case "engine(ortscircuitbreakerclosingdelay":
                    CircuitBreaker.Parse(lowercasetoken, stf);
                    break;

                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        public override void Copy(IPowerSupply source)
        {
            base.Copy(source);

            if (source is ScriptedElectricPowerSupply scriptedOther)
            {
                CircuitBreaker.Copy(scriptedOther.CircuitBreaker);
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            if (!Activated)
            {
                if (scriptName != null && scriptName != "Default")
                {
                    abstractScript = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(ElectricLocomotive.WagFilePath), "Script"), scriptName) as ElectricPowerSupply;
                }
                if (Script == null)
                {
                    abstractScript = new DefaultElectricPowerSupply();
                }

                AssignScriptFunctions();

                Script.Initialize();
                Activated = true;
            }

            CircuitBreaker.Initialize();
        }


        //================================================================================================//
        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        public override void InitializeMoving()
        {
            base.InitializeMoving();

            CircuitBreaker.InitializeMoving();
        }

        public override void Save(BinaryWriter outf)
        {
            outf.Write(scriptName);

            base.Save(outf);
            CircuitBreaker.Save(outf);
        }

        public override void Restore(BinaryReader inf)
        {
            scriptName = inf.ReadString();

            base.Restore(inf);
            CircuitBreaker.Restore(inf);
        }

        public override void Update(double elapsedClockSeconds)
        {
            base.Update(elapsedClockSeconds);

            CircuitBreaker.Update(elapsedClockSeconds);

            Script?.Update(elapsedClockSeconds);
        }

        protected override void AssignScriptFunctions()
        {
            base.AssignScriptFunctions();

            // ElectricPowerSupply getters
            Script.CurrentPantographState = () => Pantographs.State;
            Script.CurrentCircuitBreakerState = () => CircuitBreaker.State;
            Script.CircuitBreakerDriverClosingOrder = () => CircuitBreaker.DriverClosingOrder;
            Script.CircuitBreakerDriverOpeningOrder = () => CircuitBreaker.DriverOpeningOrder;
            Script.CircuitBreakerDriverClosingAuthorization = () => CircuitBreaker.DriverClosingAuthorization;
            Script.PantographVoltageV = () => PantographVoltageV;
            Script.FilterVoltageV = () => FilterVoltageV;
            Script.LineVoltageV = () => LineVoltageV;

            // ElectricPowerSupply setters
            Script.SetPantographVoltageV = (value) => PantographVoltageV = value;
            Script.SetFilterVoltageV = (value) => FilterVoltageV = value;
            Script.SignalEventToCircuitBreaker = (evt) => CircuitBreaker.HandleEvent(evt);
        }
    }

    public class DefaultElectricPowerSupply : ElectricPowerSupply
    {
        private IIRFilter PantographFilter;
        private IIRFilter VoltageFilter;
        private Timer PowerOnTimer;
        private Timer AuxPowerOnTimer;

        private bool QuickPowerOn;

        public override void Initialize()
        {
            PantographFilter = new IIRFilter(IIRFilterType.Butterworth, 1, Frequency.Angular.HzToRad(0.7f), 0.001f);
            VoltageFilter = new IIRFilter(IIRFilterType.Butterworth, 1, Frequency.Angular.HzToRad(0.7f), 0.001f);
            
            PowerOnTimer = new Timer(this);
            PowerOnTimer.Setup(PowerOnDelayS());

            AuxPowerOnTimer = new Timer(this);
            AuxPowerOnTimer.Setup(AuxPowerOnDelayS());
        }

        public override void Update(double elapsedClockSeconds)
        {
            SetCurrentBatteryState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentLowVoltagePowerSupplyState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentCabPowerSupplyState(BatterySwitchOn() && MasterKeyOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);

            switch (CurrentPantographState())
            {
                case PantographState.Down:
                case PantographState.Lowering:
                case PantographState.Raising:
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
                    SetPantographVoltageV((float)PantographFilter.Filter(0.0, elapsedClockSeconds));
                    SetFilterVoltageV((float)VoltageFilter.Filter(0.0, elapsedClockSeconds));
                    break;

                case PantographState.Up:
                    SetPantographVoltageV((float)PantographFilter.Filter(LineVoltageV(), elapsedClockSeconds));

                    switch (CurrentCircuitBreakerState())
                    {
                        case CircuitBreakerState.Open:
                            // If circuit breaker is open, then it must be closed to finish the quick power-on sequence
                            if (QuickPowerOn)
                            {
                                QuickPowerOn = false;
                                SignalEventToCircuitBreaker(PowerSupplyEvent.CloseCircuitBreaker);
                            }

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
                            SetFilterVoltageV((float)VoltageFilter.Filter(0.0f, elapsedClockSeconds));
                            break;

                        case CircuitBreakerState.Closed:
                            // If circuit breaker is closed, quick power-on sequence has finished
                            QuickPowerOn = false;

                            if (!PowerOnTimer.Started)
                                PowerOnTimer.Start();
                            if (!AuxPowerOnTimer.Started)
                                AuxPowerOnTimer.Start();

                            if (PowerOnTimer.Triggered && CurrentMainPowerSupplyState() == PowerSupplyState.PowerOff)
                            {
                                SignalEvent(TrainEvent.EnginePowerOn);
                                SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOn);
                            }
                            SetCurrentAuxiliaryPowerSupplyState(AuxPowerOnTimer.Triggered ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
                            SetFilterVoltageV((float)VoltageFilter.Filter(PantographVoltageV(), elapsedClockSeconds));
                            break;
                    }
                    break;
            }

            // By default, on electric locomotives, dynamic brake is always available (rheostatic brake is always available).
            SetCurrentDynamicBrakeAvailability(true);

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
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.QuickPowerOn:
                    QuickPowerOn = true;
                    SignalEventToBatterySwitch(PowerSupplyEvent.CloseBatterySwitch);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOnMasterKey);
                    SignalEventToPantograph(PowerSupplyEvent.RaisePantograph, 1);
                    SignalEventToOtherTrainVehiclesWithId(PowerSupplyEvent.RaisePantograph, 1);
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOnElectricTrainSupply);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    QuickPowerOn = false;
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOffElectricTrainSupply);
                    SignalEventToCircuitBreaker(PowerSupplyEvent.OpenCircuitBreaker);
                    SignalEventToPantographs(PowerSupplyEvent.LowerPantograph);
                    SignalEventToOtherTrainVehicles(PowerSupplyEvent.LowerPantograph);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOffMasterKey);
                    SignalEventToBatterySwitch(PowerSupplyEvent.OpenBatterySwitch);
                    break;

                default:
                    base.HandleEvent(evt);
                    break;
            }
        }
    }
}
