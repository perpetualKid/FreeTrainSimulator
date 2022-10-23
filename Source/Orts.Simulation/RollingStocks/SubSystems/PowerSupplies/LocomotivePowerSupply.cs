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
using System.Linq;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api.PowerSupply;
using Orts.Simulation.Physics;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerSupplies
{

    public abstract class ScriptedLocomotivePowerSupply : ILocomotivePowerSupply
    {
        public MSTSLocomotive Locomotive { get; }
        protected static readonly Simulator Simulator = Simulator.Instance;
        protected Train Train => Locomotive.Train;
        private int carId;

        public BatterySwitch BatterySwitch { get; protected set; }
        public MasterKey MasterKey { get; protected set; }
        public ElectricTrainSupplySwitch ElectricTrainSupplySwitch { get; protected set; }

        public abstract PowerSupplyType Type { get; }
        private protected string scriptName = "Default";
        private protected LocomotivePowerSupply abstractScript;

        public PowerSupplyState MainPowerSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool MainPowerSupplyOn => MainPowerSupplyState == PowerSupplyState.PowerOn;
        public bool DynamicBrakeAvailable { get; protected set; }

        public PowerSupplyState AuxiliaryPowerSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool AuxiliaryPowerSupplyOn => AuxiliaryPowerSupplyState == PowerSupplyState.PowerOn;

        public PowerSupplyState ElectricTrainSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool ElectricTrainSupplyOn => ElectricTrainSupplyState == PowerSupplyState.PowerOn;
        public bool FrontElectricTrainSupplyCableConnected { get; set; }
        public float ElectricTrainSupplyPowerW
        {
            get
            {
                return Train.Cars.OfType<MSTSWagon>()
                    .Where(wagon => wagon.PassengerCarPowerSupply != null)
                    .Where(wagon => wagon.PassengerCarPowerSupply.ElectricTrainSupplyConnectedLocomotives.Contains(Locomotive))
                    .Select(wagon => wagon.PassengerCarPowerSupply.ElectricTrainSupplyPowerW / wagon.PassengerCarPowerSupply.ElectricTrainSupplyConnectedLocomotives.Count())
                    .Sum();
            }
        }

        public PowerSupplyState LowVoltagePowerSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool LowVoltagePowerSupplyOn => LowVoltagePowerSupplyState == PowerSupplyState.PowerOn;

        public PowerSupplyState BatteryState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool BatteryOn => BatteryState == PowerSupplyState.PowerOn;

        public PowerSupplyState CabPowerSupplyState { get; protected set; } = PowerSupplyState.PowerOff;
        public bool CabPowerSupplyOn => CabPowerSupplyState == PowerSupplyState.PowerOn;

        public float PowerOnDelayS { get; protected set; }
        public float AuxPowerOnDelayS { get; protected set; }

        public bool ServiceRetentionButton { get; protected set; }
        public bool ServiceRetentionCancellationButton { get; protected set; }

        private bool firstUpdate = true;

        protected ScriptedLocomotivePowerSupply(MSTSLocomotive locomotive)
        {
            Locomotive = locomotive;

            BatterySwitch = new BatterySwitch(Locomotive);
            MasterKey = new MasterKey(Locomotive);
            ElectricTrainSupplySwitch = new ElectricTrainSupplySwitch(Locomotive);

            MainPowerSupplyState = PowerSupplyState.PowerOff;
            AuxiliaryPowerSupplyState = PowerSupplyState.PowerOff;
            LowVoltagePowerSupplyState = PowerSupplyState.PowerOff;
            CabPowerSupplyState = PowerSupplyState.PowerOff;
        }

        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortspowersupply":
                    scriptName = stf.ReadStringBlock(null);
                    break;

                case "engine(ortspowerondelay":
                    PowerOnDelayS = stf.ReadFloatBlock(STFReader.Units.Time, null);
                    break;

                case "engine(ortsauxpowerondelay":
                    AuxPowerOnDelayS = stf.ReadFloatBlock(STFReader.Units.Time, null);
                    break;

                case "engine(ortsbattery(mode":
                case "engine(ortsbattery(delay":
                case "engine(ortsbattery(defaulton":
                    BatterySwitch.Parse(lowercasetoken, stf);
                    break;
                case "engine(ortsmasterkey(mode":
                case "engine(ortsmasterkey(delayoff":
                case "engine(ortsmasterkey(headlightcontrol":
                    MasterKey.Parse(lowercasetoken, stf);
                    break;

                case "engine(ortselectrictrainsupply(mode":
                    ElectricTrainSupplySwitch.Parse(lowercasetoken, stf);
                    break;
            }
        }

        public virtual void Copy(IPowerSupply source)
        {
            if (source is ScriptedLocomotivePowerSupply scriptedOther)
            {
                BatterySwitch.Copy(scriptedOther.BatterySwitch);
                MasterKey.Copy(scriptedOther.MasterKey);
                ElectricTrainSupplySwitch.Copy(scriptedOther.ElectricTrainSupplySwitch);

                scriptName = scriptedOther.scriptName;

                PowerOnDelayS = scriptedOther.PowerOnDelayS;
                AuxPowerOnDelayS = scriptedOther.AuxPowerOnDelayS;
            }
        }

        public virtual void Initialize()
        {
            BatterySwitch.Initialize();
            MasterKey.Initialize();
            ElectricTrainSupplySwitch.Initialize();
        }

        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        public virtual void InitializeMoving()
        {
            BatterySwitch.InitializeMoving();
            MasterKey.InitializeMoving();
            ElectricTrainSupplySwitch.InitializeMoving();

            MainPowerSupplyState = PowerSupplyState.PowerOn;
            AuxiliaryPowerSupplyState = PowerSupplyState.PowerOn;
            ElectricTrainSupplyState = PowerSupplyState.PowerOn;
            LowVoltagePowerSupplyState = PowerSupplyState.PowerOn;
            BatteryState = PowerSupplyState.PowerOn;
            if (Locomotive.IsLeadLocomotive())
            {
                CabPowerSupplyState = PowerSupplyState.PowerOn;
            }

            abstractScript?.InitializeMoving();
        }

        public virtual void Save(BinaryWriter outf)
        {
            BatterySwitch.Save(outf);
            MasterKey.Save(outf);
            ElectricTrainSupplySwitch.Save(outf);

            outf.Write(FrontElectricTrainSupplyCableConnected);

            outf.Write(MainPowerSupplyState.ToString());
            outf.Write(AuxiliaryPowerSupplyState.ToString());
            outf.Write(ElectricTrainSupplyState.ToString());
            outf.Write(LowVoltagePowerSupplyState.ToString());
            outf.Write(BatteryState.ToString());
            outf.Write(CabPowerSupplyState.ToString());
        }

        public virtual void Restore(BinaryReader inf)
        {
            BatterySwitch.Restore(inf);
            MasterKey.Restore(inf);
            ElectricTrainSupplySwitch.Restore(inf);

            FrontElectricTrainSupplyCableConnected = inf.ReadBoolean();

            MainPowerSupplyState = (PowerSupplyState)Enum.Parse(typeof(PowerSupplyState), inf.ReadString());
            AuxiliaryPowerSupplyState = (PowerSupplyState)Enum.Parse(typeof(PowerSupplyState), inf.ReadString());
            ElectricTrainSupplyState = (PowerSupplyState)Enum.Parse(typeof(PowerSupplyState), inf.ReadString());
            LowVoltagePowerSupplyState = (PowerSupplyState)Enum.Parse(typeof(PowerSupplyState), inf.ReadString());
            BatteryState = (PowerSupplyState)Enum.Parse(typeof(PowerSupplyState), inf.ReadString());
            CabPowerSupplyState = (PowerSupplyState)Enum.Parse(typeof(PowerSupplyState), inf.ReadString());

            firstUpdate = false;
        }

        public virtual void Update(double elapsedClockSeconds)
        {
            carId = Train?.Cars.IndexOf(Locomotive) ?? 0;

            if (firstUpdate)
            {
                firstUpdate = false;

                TrainCar previousCar = carId > 0 ? Train.Cars[carId - 1] : null;

                // Connect the power supply cable if the previous car is a locomotive or another passenger car
                if (previousCar != null
                    && (previousCar.WagonType == WagonType.Engine
                        || previousCar.WagonType == WagonType.Passenger)
                    )
                {
                    FrontElectricTrainSupplyCableConnected = true;
                }
            }

            BatterySwitch.Update(elapsedClockSeconds);
            MasterKey.Update(elapsedClockSeconds);
            ElectricTrainSupplySwitch.Update(elapsedClockSeconds);
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            abstractScript?.HandleEvent(evt);
        }

        public void HandleEvent(PowerSupplyEvent evt, int id)
        {
            abstractScript?.HandleEvent(evt, id);
        }

        public void HandleEventFromLeadLocomotive(PowerSupplyEvent evt)
        {
            abstractScript?.HandleEventFromLeadLocomotive(evt);
        }

        public void HandleEventFromLeadLocomotive(PowerSupplyEvent evt, int id)
        {
            abstractScript?.HandleEventFromLeadLocomotive(evt, id);
        }

        protected virtual void AssignScriptFunctions()
        {
            // AbstractScriptClass
            abstractScript.ClockTime = () => (float)Simulator.ClockTime;
            abstractScript.GameTime = () => (float)Simulator.GameTime;
            abstractScript.PreUpdate = () => Simulator.PreUpdate;
            abstractScript.DistanceM = () => Locomotive.DistanceTravelled;
            abstractScript.SpeedMpS = () => Math.Abs(Locomotive.SpeedMpS);
            abstractScript.Confirm = Simulator.Confirmer.Confirm;
            abstractScript.Message = Simulator.Confirmer.Message;
            abstractScript.SignalEvent = Locomotive.SignalEvent;
            abstractScript.SignalEventToTrain = (evt) =>
            {
                if (Locomotive.Train != null)
                {
                    Locomotive.Train.SignalEvent(evt);
                }
            };

            // AbstractPowerSupply getters
            abstractScript.CurrentMainPowerSupplyState = () => MainPowerSupplyState;
            abstractScript.CurrentAuxiliaryPowerSupplyState = () => AuxiliaryPowerSupplyState;
            abstractScript.CurrentElectricTrainSupplyState = () => ElectricTrainSupplyState;
            abstractScript.CurrentLowVoltagePowerSupplyState = () => LowVoltagePowerSupplyState;
            abstractScript.CurrentBatteryState = () => BatteryState;
            abstractScript.CurrentCabPowerSupplyState = () => CabPowerSupplyState;
            abstractScript.CurrentHelperEnginesState = () =>
            {
                DieselEngineState state = DieselEngineState.Unavailable;

                foreach (MSTSDieselLocomotive locomotive in Train.Cars.OfType<MSTSDieselLocomotive>().Where(locomotive => locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected))
                {
                    if (locomotive == Simulator.PlayerLocomotive)
                    {
                        foreach (DieselEngine dieselEngine in locomotive.DieselEngines.Skip(1))
                        {
                            if (dieselEngine.State > state)
                                state = dieselEngine.State;
                        }
                    }
                    else
                    {
                        foreach (DieselEngine dieselEngine in locomotive.DieselEngines)
                        {
                            if (dieselEngine.State > state)
                                state = dieselEngine.State;
                        }
                    }
                }

                return state;
            };
            abstractScript.CurrentDynamicBrakeAvailability = () => DynamicBrakeAvailable;
            abstractScript.ThrottlePercent = () => Locomotive.ThrottlePercent;
            abstractScript.PowerOnDelayS = () => PowerOnDelayS;
            abstractScript.AuxPowerOnDelayS = () => AuxPowerOnDelayS;
            abstractScript.BatterySwitchOn = () => BatterySwitch.On;
            abstractScript.MasterKeyOn = () => MasterKey.On;
            abstractScript.ElectricTrainSupplySwitchOn = () => ElectricTrainSupplySwitch.On;
            abstractScript.ElectricTrainSupplyUnfitted = () => ElectricTrainSupplySwitch.Mode == ElectricTrainSupplySwitch.ModeType.Unfitted;

            // AbstractPowerSupply setters
            abstractScript.SetCurrentMainPowerSupplyState = (value) => MainPowerSupplyState = value;
            abstractScript.SetCurrentAuxiliaryPowerSupplyState = (value) => AuxiliaryPowerSupplyState = value;
            abstractScript.SetCurrentElectricTrainSupplyState = (value) => ElectricTrainSupplyState = value;
            abstractScript.SetCurrentLowVoltagePowerSupplyState = (value) => LowVoltagePowerSupplyState = value;
            abstractScript.SetCurrentBatteryState = (value) => BatteryState = value;
            abstractScript.SetCurrentCabPowerSupplyState = (value) => CabPowerSupplyState = value;
            abstractScript.SetCurrentDynamicBrakeAvailability = (value) => DynamicBrakeAvailable = value;
            abstractScript.SignalEventToBatterySwitch = (evt) => BatterySwitch.HandleEvent(evt);
            abstractScript.SignalEventToMasterKey = (evt) => MasterKey.HandleEvent(evt);
            abstractScript.SignalEventToElectricTrainSupplySwitch = (evt) => ElectricTrainSupplySwitch.HandleEvent(evt);
            abstractScript.SignalEventToPantographs = (evt) => Locomotive.Pantographs.HandleEvent(evt);
            abstractScript.SignalEventToPantograph = (evt, id) => Locomotive.Pantographs.HandleEvent(evt, id);
            abstractScript.SignalEventToTcs = (evt) => Locomotive.TrainControlSystem.HandleEvent(evt);
            abstractScript.SignalEventToTcsWithMessage = (evt, message) => Locomotive.TrainControlSystem.HandleEvent(evt, message);
            abstractScript.SignalEventToOtherLocomotives = (evt) =>
            {
                if (Locomotive == Simulator.PlayerLocomotive)
                {
                    foreach (MSTSLocomotive locomotive in Locomotive.Train.Cars.OfType<MSTSLocomotive>())
                    {
                        if (locomotive != Locomotive && locomotive != Locomotive.Train.LeadLocomotive && locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected)
                        {
                            locomotive.LocomotivePowerSupply.HandleEventFromLeadLocomotive(evt);
                        }
                    }
                }
            };
            abstractScript.SignalEventToOtherLocomotivesWithId = (evt, id) =>
            {
                if (Locomotive == Simulator.PlayerLocomotive)
                {
                    foreach (MSTSLocomotive locomotive in Locomotive.Train.Cars.OfType<MSTSLocomotive>())
                    {
                        if (locomotive != Locomotive && locomotive != Locomotive.Train.LeadLocomotive && locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected)
                        {
                            locomotive.LocomotivePowerSupply.HandleEventFromLeadLocomotive(evt, id);
                        }
                    }
                }
            };
            abstractScript.SignalEventToOtherTrainVehicles = (evt) =>
            {
                if (Locomotive == Simulator.PlayerLocomotive)
                {
                    foreach (TrainCar car in Locomotive.Train.Cars)
                    {
                        if (car != Locomotive && car != Locomotive.Train.LeadLocomotive && car.RemoteControlGroup != RemoteControlGroup.Unconnected)
                        {
                            car.PowerSupply?.HandleEventFromLeadLocomotive(evt);
                        }
                    }
                }
            };
            abstractScript.SignalEventToOtherTrainVehiclesWithId = (evt, id) =>
            {
                if (Locomotive == Simulator.PlayerLocomotive)
                {
                    foreach (TrainCar car in Locomotive.Train.Cars)
                    {
                        if (car != Locomotive && car != Locomotive.Train.LeadLocomotive && car.RemoteControlGroup != RemoteControlGroup.Unconnected)
                        {
                            car.PowerSupply?.HandleEventFromLeadLocomotive(evt, id);
                        }
                    }
                }
            };
            abstractScript.SignalEventToHelperEngines = (evt) =>
            {
                bool helperFound = false; //this avoids that locomotive engines toggle in opposite directions

                foreach (MSTSDieselLocomotive locomotive in Train.Cars.OfType<MSTSDieselLocomotive>().Where(locomotive => locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected))
                {
                    if (locomotive == Simulator.PlayerLocomotive)
                    {
                        // Engine number 1 or above are helper engines
                        for (int i = 1; i < locomotive.DieselEngines.Count; i++)
                        {
                            if (!helperFound)
                            {
                                helperFound = true;
                            }

                            locomotive.DieselEngines.HandleEvent(evt, i);
                        }
                    }
                    else
                    {
                        if (!helperFound)
                        {
                            helperFound = true;
                        }

                        locomotive.DieselEngines.HandleEvent(evt);
                    }
                }

                if (helperFound && (evt == PowerSupplyEvent.StartEngine || evt == PowerSupplyEvent.StopEngine))
                {
                    Simulator.Confirmer.Confirm(CabControl.HelperDiesel, evt == PowerSupplyEvent.StartEngine ? CabSetting.On : CabSetting.Off);
                }
            };
        }
    }

}
