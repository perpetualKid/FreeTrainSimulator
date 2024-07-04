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

using System;
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Scripting.Api;
using Orts.Scripting.Api.PowerSupply;
using Orts.Simulation.AIs;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{

    public class ScriptedTractionCutOffRelay : ISubSystem<ScriptedTractionCutOffRelay>, ISaveStateApi<CircuitBreakerSaveState>
    {
        public ScriptedLocomotivePowerSupply PowerSupply { get; protected set; }
        public MSTSLocomotive Locomotive => PowerSupply.Locomotive;
        protected static readonly Simulator Simulator = Simulator.Instance;

        public bool Activated;
        string ScriptName = "Automatic";
        TractionCutOffRelay Script;

        private float delayTimer;

        public TractionCutOffRelayState State { get; private set; } = TractionCutOffRelayState.Open;
        public bool DriverClosingOrder { get; private set; }
        public bool DriverOpeningOrder { get; private set; }
        public bool DriverClosingAuthorization { get; private set; }
        public bool TCSClosingAuthorization
        {
            get
            {
                if (Locomotive.Train.LeadLocomotive is MSTSLocomotive locomotive)
                    return locomotive.TrainControlSystem.PowerAuthorization;
                else
                    return false;
            }
        }
        public bool ClosingAuthorization { get; private set; }

        public ScriptedTractionCutOffRelay(ScriptedLocomotivePowerSupply powerSupply)
        {
            PowerSupply = powerSupply;
        }

        public void Copy(ScriptedTractionCutOffRelay source)
        {
            ScriptName = source.ScriptName;
            State = source.State;
            delayTimer = source.delayTimer;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortstractioncutoffrelay":
                    if (Locomotive.Train as AITrain == null)
                    {
                        ScriptName = stf.ReadStringBlock(null);
                    }
                    break;

                case "engine(ortstractioncutoffrelayclosingdelay":
                    delayTimer = stf.ReadFloatBlock(STFReader.Units.Time, null);
                    break;
            }
        }

        public void Initialize()
        {
            if (!Activated)
            {
                if (ScriptName != null)
                {
                    switch(ScriptName)
                    {
                        case "Automatic":
                            Script = new AutomaticTractionCutOffRelay() as TractionCutOffRelay;
                            break;

                        case "Manual":
                            Script = new ManualTractionCutOffRelay() as TractionCutOffRelay;
                            break;

                        default:
                            Script = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"), ScriptName) as TractionCutOffRelay;
                            break;
                    }
                }
                // Fallback to automatic circuit breaker if the above failed.
                if (Script == null)
                {
                    Script = new AutomaticTractionCutOffRelay() as TractionCutOffRelay;
                }

                // AbstractScriptClass
                Script.ClockTime = () => (float)Simulator.ClockTime;
                Script.GameTime = () => (float)Simulator.GameTime;
                Script.PreUpdate = () => Simulator.PreUpdate;
                Script.DistanceM = () => Locomotive.DistanceTravelled;
                Script.Confirm = Simulator.Confirmer.Confirm;
                Script.Message = Simulator.Confirmer.Message;
                Script.SignalEvent = Locomotive.SignalEvent;
                Script.SignalEventToTrain = (evt) =>
                {
                    if (Locomotive.Train != null)
                    {
                        Locomotive.Train.SignalEvent(evt);
                    }
                };

                // TractionCutOffSubsystem getters
                Script.SupplyType = () => PowerSupply.Type;
                Script.CurrentState = () => State;
                Script.CurrentPantographState = () => Locomotive?.Pantographs.State ?? PantographState.Unavailable;
                Script.CurrentDieselEngineState = () => (Locomotive as MSTSDieselLocomotive)?.DieselEngines.State ?? DieselEngineState.Unavailable;
                Script.CurrentPowerSupplyState = () => PowerSupply.MainPowerSupplyState;
                Script.DriverClosingOrder = () => DriverClosingOrder;
                Script.DriverOpeningOrder = () => DriverOpeningOrder;
                Script.DriverClosingAuthorization = () => DriverClosingAuthorization;
                Script.TCSClosingAuthorization = () => TCSClosingAuthorization;
                Script.ClosingAuthorization = () => ClosingAuthorization;
                Script.IsLowVoltagePowerSupplyOn = () => PowerSupply.LowVoltagePowerSupplyOn;
                Script.IsCabPowerSupplyOn = () => PowerSupply.CabPowerSupplyOn;
                Script.ClosingDelayS = () => delayTimer;

                // TractionCutOffSubsystem setters
                Script.SetDriverClosingOrder = (value) => DriverClosingOrder = value;
                Script.SetDriverOpeningOrder = (value) => DriverOpeningOrder = value;
                Script.SetDriverClosingAuthorization = (value) => DriverClosingAuthorization = value;
                Script.SetClosingAuthorization = (value) => ClosingAuthorization = value;

                // TractionCutOffRelay getters
                Script.CurrentState = () => State;

                // TractionCutOffRelay setters
                Script.SetCurrentState = (value) =>
                {
                    State = value;
                    TCSEvent CircuitBreakerEvent = State == TractionCutOffRelayState.Closed ? TCSEvent.TractionCutOffRelayClosed : TCSEvent.TractionCutOffRelayOpen;
                    Locomotive.TrainControlSystem.HandleEvent(CircuitBreakerEvent);
                };

                // DualModeTractionCutOffRelay getters
                Script.CurrentCircuitBreakerState = () => PowerSupply.Type == PowerSupplyType.DualMode ? (PowerSupply as ScriptedDualModePowerSupply).CircuitBreaker.State : CircuitBreakerState.Unavailable;

                Script.Initialize();
                Activated = true;
            }
        }

        public void InitializeMoving()
        {
            Script?.InitializeMoving();

            State = TractionCutOffRelayState.Closed;
        }

        public void Update(double elapsedClockSeconds)
        {
            if (Locomotive.Train.TrainType == TrainType.Ai || Locomotive.Train.TrainType == TrainType.AiAutoGenerated
                || Locomotive.Train.TrainType == TrainType.AiPlayerHosting)
            {
                State = TractionCutOffRelayState.Closed;
            }
            else
            {
                Script?.Update(elapsedClockSeconds);
            }
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            Script?.HandleEvent(evt);
        }

        public ValueTask<CircuitBreakerSaveState> Snapshot()
        {
            return ValueTask.FromResult(new CircuitBreakerSaveState()
            {
                ScriptName = ScriptName,
                DelayTimer = delayTimer,
                TractionCutOffRelayState = State,
                DriverClosingOrder = DriverClosingOrder,
                DriverOpeningOrder = DriverOpeningOrder,
                DriverClosingAuthorization = DriverClosingAuthorization,
                ClosingAuthorization = ClosingAuthorization,
            });
        }

        public ValueTask Restore(CircuitBreakerSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ScriptName = saveState.ScriptName;
            delayTimer = (float)saveState.DelayTimer;
            State = saveState.TractionCutOffRelayState;
            DriverClosingOrder = saveState.DriverClosingOrder;
            DriverOpeningOrder = saveState.DriverOpeningOrder;
            DriverClosingAuthorization = saveState.DriverClosingAuthorization;
            ClosingAuthorization = saveState.ClosingAuthorization;

            return ValueTask.CompletedTask;
        }
    }

    class AutomaticTractionCutOffRelay : TractionCutOffRelay
    {
        private Timer ClosingTimer;
        private TractionCutOffRelayState PreviousState;

        public override void Initialize()
        {
            ClosingTimer = new Timer(this);
            ClosingTimer.Setup(ClosingDelayS());

            SetDriverClosingOrder(false);
            SetDriverOpeningOrder(false);
            SetDriverClosingAuthorization(true);
        }

        public override void Update(double elapsedSeconds)
        {
            UpdateClosingAuthorization();

            switch (CurrentState())
            {
                case TractionCutOffRelayState.Closed:
                    if (!ClosingAuthorization())
                    {
                        SetCurrentState(TractionCutOffRelayState.Open);
                    }
                    break;

                case TractionCutOffRelayState.Closing:
                    if (ClosingAuthorization())
                    {
                        if (!ClosingTimer.Started)
                        {
                            ClosingTimer.Start();
                        }

                        if (ClosingTimer.Triggered)
                        {
                            ClosingTimer.Stop();
                            SetCurrentState(TractionCutOffRelayState.Closed);
                        }
                    }
                    else
                    {
                        ClosingTimer.Stop();
                        SetCurrentState(TractionCutOffRelayState.Open);
                    }
                    break;

                case TractionCutOffRelayState.Open:
                    if (ClosingAuthorization())
                    {
                        SetCurrentState(TractionCutOffRelayState.Closing);
                    }
                    break;
            }

            if (PreviousState != CurrentState())
            {
                switch (CurrentState())
                {
                    case TractionCutOffRelayState.Open:
                        SignalEvent(TrainEvent.TractionCutOffRelayOpen);
                        break;

                    case TractionCutOffRelayState.Closing:
                        SignalEvent(TrainEvent.TractionCutOffRelayClosing);
                        break;

                    case TractionCutOffRelayState.Closed:
                        SignalEvent(TrainEvent.TractionCutOffRelayClosed);
                        break;
                }
            }

            PreviousState = CurrentState();
        }

        public virtual void UpdateClosingAuthorization()
        {
            SetClosingAuthorization(TCSClosingAuthorization() && CurrentDieselEngineState() == DieselEngineState.Running);
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            // Nothing to do since it is automatic
        }
    }

    class AutomaticDualModeTractionCutOffRelay : AutomaticTractionCutOffRelay
    {
        public override void UpdateClosingAuthorization()
        {
            SetClosingAuthorization(
                TCSClosingAuthorization()
                && (
                    (CurrentPantographState() == PantographState.Up && CurrentCircuitBreakerState() == CircuitBreakerState.Closed)
                    || CurrentDieselEngineState() == DieselEngineState.Running
                )
            );
        }
    }

    class ManualTractionCutOffRelay : TractionCutOffRelay
    {
        private Timer ClosingTimer;
        private TractionCutOffRelayState PreviousState;

        public override void Initialize()
        {
            ClosingTimer = new Timer(this);
            ClosingTimer.Setup(ClosingDelayS());

            SetDriverClosingAuthorization(true);
        }

        public override void Update(double elapsedSeconds)
        {
            SetClosingAuthorization(TCSClosingAuthorization() && CurrentDieselEngineState() == DieselEngineState.Running);

            switch (CurrentState())
            {
                case TractionCutOffRelayState.Closed:
                    if (!ClosingAuthorization() || DriverOpeningOrder())
                    {
                        SetCurrentState(TractionCutOffRelayState.Open);
                    }
                    break;

                case TractionCutOffRelayState.Closing:
                    if (ClosingAuthorization() && DriverClosingOrder())
                    {
                        if (!ClosingTimer.Started)
                        {
                            ClosingTimer.Start();
                        }

                        if (ClosingTimer.Triggered)
                        {
                            ClosingTimer.Stop();
                            SetCurrentState(TractionCutOffRelayState.Closed);
                        }
                    }
                    else
                    {
                        ClosingTimer.Stop();
                        SetCurrentState(TractionCutOffRelayState.Open);
                    }
                    break;

                case TractionCutOffRelayState.Open:
                    if (ClosingAuthorization() && DriverClosingOrder())
                    {
                        SetCurrentState(TractionCutOffRelayState.Closing);
                    }
                    break;
            }

            if (PreviousState != CurrentState())
            {
                switch (CurrentState())
                {
                    case TractionCutOffRelayState.Open:
                        SignalEvent(TrainEvent.CircuitBreakerOpen);
                        break;

                    case TractionCutOffRelayState.Closing:
                        SignalEvent(TrainEvent.CircuitBreakerClosing);
                        break;

                    case TractionCutOffRelayState.Closed:
                        SignalEvent(TrainEvent.CircuitBreakerClosed);
                        break;
                }
            }

            PreviousState = CurrentState();
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseTractionCutOffRelay:
                    if (!DriverClosingOrder())
                    {
                        SetDriverClosingOrder(true);
                        SetDriverOpeningOrder(false);
                        SignalEvent(TrainEvent.TractionCutOffRelayClosingOrderOn);

                        Confirm(CabControl.TractionCutOffRelayClosingOrder, CabSetting.On);
                        if (!ClosingAuthorization())
                        {
                            Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Traction cut-off relay closing not authorized"));
                        }
                    }
                    break;

                case PowerSupplyEvent.OpenTractionCutOffRelay:
                    SetDriverClosingOrder(false);
                    SetDriverOpeningOrder(true);
                    SignalEvent(TrainEvent.TractionCutOffRelayClosingOrderOff);

                    Confirm(CabControl.TractionCutOffRelayClosingOrder, CabSetting.Off);
                    break;
            }
        }
    }
}
