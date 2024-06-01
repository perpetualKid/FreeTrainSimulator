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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Scripting.Api;
using Orts.Scripting.Api.PowerSupply;
using Orts.Simulation.Physics;

using SharpDX.Direct2D1;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{
    public class ScriptedPassengerCarPowerSupply : IPassengerCarPowerSupply, ISubSystem<ScriptedPassengerCarPowerSupply>, ISaveStateApi<PowerSupplySaveState>
    {
        public readonly MSTSWagon Wagon;
        protected static readonly Simulator Simulator = Simulator.Instance;
        protected Train Train => Wagon.Train;
        protected Pantographs Pantographs => Wagon.Pantographs;
        protected int CarId;

        public BatterySwitch BatterySwitch { get; protected set; }

        protected bool Activated;
        protected string ScriptName = "Default";
        protected PassengerCarPowerSupply Script;

        // Variables
        public IEnumerable<MSTSLocomotive> ElectricTrainSupplyConnectedLocomotives = new List<MSTSLocomotive>();
        public PowerSupplyState ElectricTrainSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool ElectricTrainSupplyOn => ElectricTrainSupplyState == PowerSupplyState.PowerOn;
        public bool FrontElectricTrainSupplyCableConnected { get; set; }
        public float ElectricTrainSupplyPowerW { get; protected set; }

        public PowerSupplyState LowVoltagePowerSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool LowVoltagePowerSupplyOn => LowVoltagePowerSupplyState == PowerSupplyState.PowerOn;

        public PowerSupplyState BatteryState { get; protected set; }
        public bool BatteryOn => BatteryState == PowerSupplyState.PowerOn;

        public PowerSupplyState VentilationState { get; protected set; }
        public PowerSupplyState HeatingState { get; protected set; }
        public PowerSupplyState AirConditioningState { get; protected set; }
        public float HeatFlowRateW { get; protected set; }

        // Parameters
        public float PowerOnDelayS { get; protected set; }
        public float ContinuousPowerW { get; protected set; }
        public float HeatingPowerW { get; protected set; }
        public float AirConditioningPowerW { get; protected set; }
        public float AirConditioningYield { get; protected set; } = 0.9f;

        private bool firstUpdate = true;

        public ScriptedPassengerCarPowerSupply(MSTSWagon wagon)
        {
            Wagon = wagon;

            BatterySwitch = new BatterySwitch(Wagon);
        }

        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(ortspowersupply":
                    ScriptName = stf.ReadStringBlock(null);
                    break;

                case "wagon(ortspowerondelay":
                    PowerOnDelayS = stf.ReadFloatBlock(STFReader.Units.Time, null);
                    break;

                case "wagon(ortsbattery(mode":
                case "wagon(ortsbattery(delay":
                case "wagon(ortsbattery(defaulton":
                    BatterySwitch.Parse(lowercasetoken, stf);
                    break;

                case "wagon(ortspowersupplycontinuouspower":
                    ContinuousPowerW = stf.ReadFloatBlock(STFReader.Units.Power, 0f);
                    break;

                case "wagon(ortspowersupplyheatingpower":
                    HeatingPowerW = stf.ReadFloatBlock(STFReader.Units.Power, 0f);
                    break;

                case "wagon(ortspowersupplyairconditioningpower":
                    AirConditioningPowerW = stf.ReadFloatBlock(STFReader.Units.Power, 0f);
                    break;

                case "wagon(ortspowersupplyairconditioningyield":
                    AirConditioningYield = stf.ReadFloatBlock(STFReader.Units.Power, 0.9f);
                    break;
            }
        }

        public void Copy(IPowerSupply source)
        {
            if (source is ScriptedPassengerCarPowerSupply scriptedOther)
            {
                Copy(scriptedOther);
            }
        }

        public void Copy(ScriptedPassengerCarPowerSupply source)
        {
            BatterySwitch.Copy(source.BatterySwitch);

            ScriptName = source.ScriptName;

            PowerOnDelayS = source.PowerOnDelayS;
            ContinuousPowerW = source.ContinuousPowerW;
            HeatingPowerW = source.HeatingPowerW;
            AirConditioningPowerW = source.AirConditioningPowerW;
            AirConditioningYield = source.AirConditioningYield;
        }

        public virtual void Initialize()
        {
            if (!Activated)
            {
                if (ScriptName != null && ScriptName != "Default")
                {
                    Script = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(Wagon.WagFilePath), "Script"), ScriptName) as PassengerCarPowerSupply;
                }
                if (Script == null)
                {
                    Script = new DefaultPassengerCarPowerSupply();
                }

                AssignScriptFunctions();

                Script.Initialize();
                Activated = true;
            }

            BatterySwitch.Initialize();
        }

        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        public virtual void InitializeMoving()
        {
            BatterySwitch.InitializeMoving();

            ElectricTrainSupplyState = PowerSupplyState.PowerOn;
            BatteryState = PowerSupplyState.PowerOn;

            Script?.InitializeMoving();
        }

        public async ValueTask<PowerSupplySaveState> Snapshot()
        {
            return new PowerSupplySaveState()
            {
                BatterySwitchState = await BatterySwitch.Snapshot().ConfigureAwait(false),
                FrontElectricTrainSupplyCableConnected = FrontElectricTrainSupplyCableConnected,
                ElectricTrainSupplyState = ElectricTrainSupplyState,
                LowVoltagePowerSupplyState = LowVoltagePowerSupplyState,
                BatteryState = BatteryState,
                VentilationState = VentilationState,
                HeatingState = HeatingState,
                AirConditioningState = AirConditioningState,
                HeatFlowRate = HeatFlowRateW,
            };
        }

        public async ValueTask Restore(PowerSupplySaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            await BatterySwitch.Restore(saveState.BatterySwitchState).ConfigureAwait(false);
            FrontElectricTrainSupplyCableConnected = saveState.FrontElectricTrainSupplyCableConnected;
            ElectricTrainSupplyState = saveState.ElectricTrainSupplyState;
            LowVoltagePowerSupplyState = saveState.LowVoltagePowerSupplyState;
            BatteryState = saveState.BatteryState;
            VentilationState = saveState.VentilationState;
            HeatingState = saveState.HeatingState;
            AirConditioningState = saveState.AirConditioningState;
            HeatFlowRateW = saveState.HeatFlowRate;

            firstUpdate = false;
        }

        public virtual void Update(double elapsedClockSeconds)
        {
            CarId = Train?.Cars.IndexOf(Wagon) ?? 0;

            if (firstUpdate)
            {
                firstUpdate = false;

                TrainCar previousCar = CarId > 0 ? Train.Cars[CarId - 1] : null;

                // Connect the power supply cable if the previous car is a locomotive or another passenger car
                if (previousCar != null
                    && (previousCar is MSTSLocomotive locomotive && locomotive.LocomotivePowerSupply.ElectricTrainSupplyState != PowerSupplyState.Unavailable
                        || previousCar.WagonSpecialType == WagonSpecialType.PowerVan
                        || previousCar.WagonType == WagonType.Passenger && previousCar.PowerSupply is ScriptedPassengerCarPowerSupply)
                    )
                {
                    FrontElectricTrainSupplyCableConnected = true;
                }
            }

            ElectricTrainSupplyConnectedLocomotives = Train.Cars.OfType<MSTSLocomotive>().Where((locomotive) =>
            {
                int locomotiveId = Train.Cars.IndexOf(locomotive);
                bool locomotiveInFront = locomotiveId < CarId;

                bool connectedToLocomotive = true;
                if (locomotiveInFront)
                {
                    for (int i = locomotiveId; i < CarId; i++)
                    {
                        if (Train.Cars[i + 1].PowerSupply == null)
                        {
                            connectedToLocomotive = false;
                            break;
                        }
                        if (!Train.Cars[i + 1].PowerSupply.FrontElectricTrainSupplyCableConnected)
                        {
                            connectedToLocomotive = false;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = locomotiveId; i > CarId; i--)
                    {
                        if (Train.Cars[i].PowerSupply == null)
                        {
                            connectedToLocomotive = false;
                            break;
                        }
                        if (!Train.Cars[i].PowerSupply.FrontElectricTrainSupplyCableConnected)
                        {
                            connectedToLocomotive = false;
                            break;
                        }
                    }
                }

                return connectedToLocomotive;
            });

            if (ElectricTrainSupplyConnectedLocomotives.Any())
            {
                ElectricTrainSupplyState = ElectricTrainSupplyConnectedLocomotives.Select(locomotive => locomotive.LocomotivePowerSupply.ElectricTrainSupplyState).Max();
            }
            else
            {
                ElectricTrainSupplyState = PowerSupplyState.PowerOff;
            }

            BatterySwitch.Update(elapsedClockSeconds);
            Script?.Update(elapsedClockSeconds);
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            Script?.HandleEvent(evt);
        }

        public void HandleEvent(PowerSupplyEvent evt, int id)
        {
            Script?.HandleEvent(evt, id);
        }

        public void HandleEventFromLeadLocomotive(PowerSupplyEvent evt)
        {
            Script?.HandleEventFromLeadLocomotive(evt);
        }

        public void HandleEventFromLeadLocomotive(PowerSupplyEvent evt, int id)
        {
            Script?.HandleEventFromLeadLocomotive(evt, id);
        }

        protected virtual void AssignScriptFunctions()
        {
            // AbstractScriptClass
            Script.ClockTime = () => (float)Simulator.ClockTime;
            Script.GameTime = () => (float)Simulator.GameTime;
            Script.PreUpdate = () => Simulator.PreUpdate;
            Script.DistanceM = () => Wagon.DistanceTravelled;
            Script.SpeedMpS = () => Math.Abs(Wagon.SpeedMpS);
            Script.Confirm = Simulator.Confirmer.Confirm;
            Script.Message = Simulator.Confirmer.Message;
            Script.SignalEvent = Wagon.SignalEvent;
            Script.SignalEventToTrain = (evt) => Train?.SignalEvent(evt);

            // AbstractPowerSupply getters
            Script.CurrentElectricTrainSupplyState = () => ElectricTrainSupplyState;
            Script.CurrentLowVoltagePowerSupplyState = () => LowVoltagePowerSupplyState;
            Script.CurrentBatteryState = () => BatteryState;
            Script.BatterySwitchOn = () => BatterySwitch.On;

            // PassengerCarPowerSupply getters
            Script.CurrentVentilationState = () => VentilationState;
            Script.CurrentHeatingState = () => HeatingState;
            Script.CurrentAirConditioningState = () => AirConditioningState;
            Script.CurrentElectricTrainSupplyPowerW = () => ElectricTrainSupplyPowerW;
            Script.CurrentHeatFlowRateW = () => HeatFlowRateW;
            Script.ContinuousPowerW = () => ContinuousPowerW;
            Script.HeatingPowerW = () => HeatingPowerW;
            Script.AirConditioningPowerW = () => AirConditioningPowerW;
            Script.AirConditioningYield = () => AirConditioningYield;
            Script.PowerOnDelayS = () => PowerOnDelayS;
            Script.DesiredTemperatureC = () => (float)Wagon.DesiredCompartmentTempSetpointC;
            Script.InsideTemperatureC = () => (float)Wagon.CarInsideTempC;
            Script.OutsideTemperatureC = () => Wagon.CarOutsideTempC;

            // AbstractPowerSupply setters
            Script.SetCurrentLowVoltagePowerSupplyState = (value) => LowVoltagePowerSupplyState = value;
            Script.SetCurrentBatteryState = (value) => BatteryState = value;
            Script.SignalEventToBatterySwitch = (evt) => BatterySwitch.HandleEvent(evt);
            Script.SignalEventToPantographs = (evt) => Wagon.Pantographs.HandleEvent(evt);
            Script.SignalEventToPantograph = (evt, id) => Wagon.Pantographs.HandleEvent(evt, id);

            // PassengerCarPowerSupply setters
            Script.SetCurrentVentilationState = (value) => VentilationState = value;
            Script.SetCurrentHeatingState = (value) => HeatingState = value;
            Script.SetCurrentAirConditioningState = (value) => AirConditioningState = value;
            Script.SetCurrentElectricTrainSupplyPowerW = (value) => {
                if (value >= 0f)
                {
                    ElectricTrainSupplyPowerW = value;
                }
            };
            Script.SetCurrentHeatFlowRateW = (value) => HeatFlowRateW = value;
        }
    }

    public class DefaultPassengerCarPowerSupply : PassengerCarPowerSupply
    {
        private Timer PowerOnTimer;

        public override void Initialize()
        {
            PowerOnTimer = new Timer(this);
            PowerOnTimer.Setup(PowerOnDelayS());

            SetCurrentVentilationState(PowerSupplyState.PowerOff);
            SetCurrentHeatingState(PowerSupplyState.PowerOff);
            SetCurrentAirConditioningState(PowerSupplyState.PowerOff);
        }

        public override void InitializeMoving()
        {
        }

        public override void Update(double elapsedClockSeconds)
        {
            SetCurrentBatteryState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentLowVoltagePowerSupplyState(BatterySwitchOn() || CurrentElectricTrainSupplyState() == PowerSupplyState.PowerOn ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);

            switch (CurrentElectricTrainSupplyState())
            {
                case PowerSupplyState.PowerOff:
                    if (PowerOnTimer.Started)
                        PowerOnTimer.Stop();

                    if (CurrentVentilationState() == PowerSupplyState.PowerOn)
                    {
                        SetCurrentVentilationState(PowerSupplyState.PowerOff);
                        SignalEvent(TrainEvent.VentilationOff);
                    }

                    if (CurrentHeatingState() == PowerSupplyState.PowerOn)
                    {
                        SetCurrentHeatingState(PowerSupplyState.PowerOff);
                        SignalEvent(TrainEvent.HeatingOff);
                    }

                    if (CurrentAirConditioningState() == PowerSupplyState.PowerOn)
                    {
                        SetCurrentAirConditioningState(PowerSupplyState.PowerOff);
                        SignalEvent(TrainEvent.AirConditioningOff);
                    }

                    SetCurrentElectricTrainSupplyPowerW(0f);
                    SetCurrentHeatFlowRateW(0f);
                    break;

                case PowerSupplyState.PowerOn:
                    if (!PowerOnTimer.Started)
                        PowerOnTimer.Start();

                    if (CurrentVentilationState() == PowerSupplyState.PowerOff)
                    {
                        SetCurrentVentilationState(PowerSupplyState.PowerOn);
                        SignalEvent(TrainEvent.VentilationLow);
                    }

                    if (CurrentHeatingState() == PowerSupplyState.PowerOff
                        && InsideTemperatureC() < DesiredTemperatureC() - 2.5f)
                    {
                        SetCurrentHeatingState(PowerSupplyState.PowerOn);
                        SignalEvent(TrainEvent.HeatingOn);
                    }
                    else if (CurrentHeatingState() == PowerSupplyState.PowerOn
                        && InsideTemperatureC() >= DesiredTemperatureC())
                    {
                        SetCurrentHeatingState(PowerSupplyState.PowerOff);
                        SignalEvent(TrainEvent.HeatingOff);
                    }

                    float heatingPowerW = CurrentHeatingState() == PowerSupplyState.PowerOn ? HeatingPowerW() : 0f;

                    if (CurrentAirConditioningState() == PowerSupplyState.PowerOff
                        && InsideTemperatureC() > DesiredTemperatureC() + 2.5f)
                    {
                        SetCurrentAirConditioningState(PowerSupplyState.PowerOn);
                        SignalEvent(TrainEvent.AirConditioningOn);
                    }
                    else if (CurrentAirConditioningState() == PowerSupplyState.PowerOn
                        && InsideTemperatureC() <= DesiredTemperatureC())
                    {
                        SetCurrentAirConditioningState(PowerSupplyState.PowerOff);
                        SignalEvent(TrainEvent.AirConditioningOff);
                    }

                    float airConditioningElectricPowerW = CurrentAirConditioningState() == PowerSupplyState.PowerOn ? AirConditioningPowerW() : 0f;
                    float airConditioningThermalPowerW = CurrentAirConditioningState() == PowerSupplyState.PowerOn ? - AirConditioningPowerW() * AirConditioningYield() : 0f;

                    SetCurrentElectricTrainSupplyPowerW(ContinuousPowerW() + heatingPowerW + airConditioningElectricPowerW);
                    SetCurrentHeatFlowRateW(heatingPowerW + airConditioningThermalPowerW);
                    break;
            }
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            SignalEventToPantographs(evt);
            SignalEventToBatterySwitch(evt);
        }

        public override void HandleEvent(PowerSupplyEvent evt, int id)
        {
            SignalEventToPantograph(evt, id);
        }
    }

}
