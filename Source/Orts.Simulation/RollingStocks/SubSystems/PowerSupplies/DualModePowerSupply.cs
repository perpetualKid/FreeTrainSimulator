// COPYRIGHT 2013, 2014, 2015 by the Open Rails project.
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
using FreeTrainSimulator.Common.Calc;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Scripting.Api;
using Orts.Scripting.Api.PowerSupply;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{

    public class ScriptedDualModePowerSupply : ScriptedLocomotivePowerSupply
    {
        public MSTSElectricLocomotive DualModeLocomotive => Locomotive as MSTSElectricLocomotive;
        public Pantographs Pantographs => Locomotive.Pantographs;
        public ScriptedCircuitBreaker CircuitBreaker { get; protected set; }
        public ScriptedTractionCutOffRelay TractionCutOffRelay { get; protected set; }

        public override PowerSupplyType Type => PowerSupplyType.Electric;
        public bool Activated;
        private DualModePowerSupply Script => abstractScript as DualModePowerSupply;

        public float LineVoltageV => (float)Simulator.Route.MaxLineVoltage;
        public float PantographVoltageV { get; set; }
        public float FilterVoltageV { get; set; }

        public ScriptedDualModePowerSupply(MSTSElectricLocomotive locomotive) :
            base(locomotive)
        {
            CircuitBreaker = new ScriptedCircuitBreaker(this);
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

                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        public override void Copy(IPowerSupply source)
        {
            base.Copy(source);

            if (source is ScriptedDualModePowerSupply scriptedOther)
            {
                CircuitBreaker.Copy(scriptedOther.CircuitBreaker);
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
                    abstractScript = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"), scriptName) as DualModePowerSupply;
                }
                if (Script == null)
                {
                    abstractScript = new DefaultDualModePowerSupply();
                }

                AssignScriptFunctions();

                Script.Initialize();
                Activated = true;
            }

            CircuitBreaker.Initialize();
            TractionCutOffRelay.Initialize();
        }


        //================================================================================================//
        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        public override void InitializeMoving()
        {
            base.InitializeMoving();

            CircuitBreaker.InitializeMoving();
            TractionCutOffRelay.InitializeMoving();
        }

        public override async ValueTask<PowerSupplySaveState> Snapshot()
        {
            PowerSupplySaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.CircuitBreakerState = await CircuitBreaker.Snapshot().ConfigureAwait(false);
            saveState.TractionCutOffRelayState = await TractionCutOffRelay.Snapshot().ConfigureAwait(false);
            return saveState;
        }

        public override async ValueTask Restore([NotNull] PowerSupplySaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            await CircuitBreaker.Restore(saveState.CircuitBreakerState).ConfigureAwait(false);
            await TractionCutOffRelay.Restore(saveState.TractionCutOffRelayState).ConfigureAwait(false);
        }

        public override void Update(double elapsedClockSeconds)
        {
            base.Update(elapsedClockSeconds);

            CircuitBreaker.Update(elapsedClockSeconds);
            TractionCutOffRelay.Update(elapsedClockSeconds);

            Script?.Update(elapsedClockSeconds);
        }

        protected override void AssignScriptFunctions()
        {
            base.AssignScriptFunctions();

            // DualModePowerSupply getters
            Script.CurrentDieselEngineState = () => (Locomotive as MSTSDieselLocomotive).DieselEngines.State;
            Script.CurrentTractionCutOffRelayState = () => TractionCutOffRelay.State;
            Script.TractionCutOffRelayDriverClosingOrder = () => TractionCutOffRelay.DriverClosingOrder;
            Script.TractionCutOffRelayDriverOpeningOrder = () => TractionCutOffRelay.DriverOpeningOrder;
            Script.TractionCutOffRelayDriverClosingAuthorization = () => TractionCutOffRelay.DriverClosingAuthorization;

            // DualModePowerSupply setters
            Script.SignalEventToTractionCutOffRelay = (evt) => TractionCutOffRelay.HandleEvent(evt);
        }
    }

    public class DefaultDualModePowerSupply : DualModePowerSupply
    {
        private IIRFilter PantographFilter;
        private IIRFilter VoltageFilter;
        private Timer PowerOnTimer;
        private Timer AuxPowerOnTimer;

        public override void Initialize()
        {
            PantographFilter = new IIRFilter(IIRFilterType.Butterworth, Frequency.Angular.HzToRad(0.7), 0.001);
            VoltageFilter = new IIRFilter(IIRFilterType.Butterworth, Frequency.Angular.HzToRad(0.7), 0.001);

            PowerOnTimer = new Timer(this);
            PowerOnTimer.Setup(PowerOnDelayS());

            AuxPowerOnTimer = new Timer(this);
            AuxPowerOnTimer.Setup(AuxPowerOnDelayS());
        }

        public override void Update(double elapsedClockSeconds)
        {
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

                    SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOff);
                    SetCurrentAuxiliaryPowerSupplyState(PowerSupplyState.PowerOff);
                    SetPantographVoltageV((float)PantographFilter.Filter(0.0f, elapsedClockSeconds));
                    SetFilterVoltageV((float)VoltageFilter.Filter(0.0f, elapsedClockSeconds));
                    break;

                case PantographState.Up:
                    SetPantographVoltageV((float)PantographFilter.Filter(LineVoltageV(), elapsedClockSeconds));

                    switch (CurrentCircuitBreakerState())
                    {
                        case CircuitBreakerState.Open:
                            if (PowerOnTimer.Started)
                                PowerOnTimer.Stop();
                            if (AuxPowerOnTimer.Started)
                                AuxPowerOnTimer.Stop();

                            SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOff);
                            SetCurrentAuxiliaryPowerSupplyState(PowerSupplyState.PowerOff);
                            SetFilterVoltageV((float)VoltageFilter.Filter(0.0f, elapsedClockSeconds));
                            break;

                        case CircuitBreakerState.Closed:
                            if (!PowerOnTimer.Started)
                                PowerOnTimer.Start();
                            if (!AuxPowerOnTimer.Started)
                                AuxPowerOnTimer.Start();

                            SetCurrentMainPowerSupplyState(PowerOnTimer.Triggered ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
                            SetCurrentAuxiliaryPowerSupplyState(AuxPowerOnTimer.Triggered ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
                            SetFilterVoltageV((float)VoltageFilter.Filter(PantographVoltageV(), elapsedClockSeconds));
                            break;
                    }
                    break;
            }
        }
    }
}
