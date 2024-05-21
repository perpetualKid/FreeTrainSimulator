// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

/* ELECTRIC LOCOMOTIVE CLASSES
 * 
 * The locomotive is represented by two classes:
 *  ...Simulator - defines the behaviour, ie physics, motion, power generated etc
 *  ...Viewer - defines the appearance in a 3D viewer
 * 
 * The ElectricLocomotive classes add to the basic behaviour provided by:
 *  LocomotiveSimulator - provides for movement, throttle controls, direction controls etc
 *  LocomotiveViewer - provides basic animation for running gear, wipers, etc
 * 
 */

using System;
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.Simulation.RollingStocks
{
    ///////////////////////////////////////////////////
    ///   SIMULATION BEHAVIOUR
    ///////////////////////////////////////////////////


    /// <summary>
    /// Adds pantograph control to the basic LocomotiveSimulator functionality
    /// </summary>
    public class MSTSElectricLocomotive : MSTSLocomotive
    {
        public ScriptedElectricPowerSupply ElectricPowerSupply => PowerSupply as ScriptedElectricPowerSupply;

        public MSTSElectricLocomotive(string wagFile) :
            base(wagFile)
        {
            PowerSupply = new ScriptedElectricPowerSupply(this);
        }

        /// <summary>
        /// Parse the wag file parameters required for the simulator and viewer classes
        /// </summary>
        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortspowerondelay":
                case "engine(ortsauxpowerondelay":
                case "engine(ortspowersupply":
                case "engine(ortscircuitbreaker":
                case "engine(ortscircuitbreakerclosingdelay":
                case "engine(ortsbattery(mode":
                case "engine(ortsbattery(delay":
                case "engine(ortsbattery(defaulton":
                case "engine(ortsmasterkey(mode":
                case "engine(ortsmasterkey(delayoff":
                case "engine(ortsmasterkey(headlightcontrol":
                case "engine(ortselectrictrainsupply(mode":
                    LocomotivePowerSupply.Parse(lowercasetoken, stf);
                    break;

                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        /// <summary>
        /// We are saving the game.  Save anything that we'll need to restore the 
        /// status later.
        /// </summary>
        public override void Save(BinaryWriter outf)
        {
            outf.Write(CurrentLocomotiveSteamHeatBoilerWaterCapacityL);
            base.Save(outf);
        }

        /// <summary>
        /// We are restoring a saved game.  The TrainCar class has already
        /// been initialized.   Restore the game state.
        /// </summary>
        public override void Restore(BinaryReader inf)
        {
            CurrentLocomotiveSteamHeatBoilerWaterCapacityL = inf.ReadDouble();
            base.Restore(inf);
        }

        public override void Initialize()
        {
            base.Initialize();

            // If DrvWheelWeight is not in ENG file, then calculate drivewheel weight freom FoA

            if (DrvWheelWeightKg == 0) // if DrvWheelWeightKg not in ENG file.
            {
                DrvWheelWeightKg = MassKG; // set Drive wheel weight to total wagon mass if not in ENG file
            }

            // Initialise water level in steam heat boiler
            if (CurrentLocomotiveSteamHeatBoilerWaterCapacityL == 0 && IsSteamHeatFitted)
            {
                if (maximumSteamHeatBoilerWaterTankCapacityL != 0)
                {
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL = (float)maximumSteamHeatBoilerWaterTankCapacityL;
                }
                else
                {
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL = (float)Size.LiquidVolume.FromGallonUK(800.0f);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Initialization when simulation starts with moving train
        /// <\summary>
        /// 
        public override void InitializeMoving()
        {
            base.InitializeMoving();
            WheelSpeedMpS = SpeedMpS;
            DynamicBrakePercent = -1;
            ThrottleController.SetValue(Train.MUThrottlePercent / 100);

            Pantographs.InitializeMoving();
        }

        /// <summary>
        /// This function updates periodically the wagon heating.
        /// </summary>
        protected override void UpdateCarSteamHeat(double elapsedClockSeconds)
        {
            // Update Steam Heating System

            // TO DO - Add test to see if cars are coupled, if Light Engine, disable steam heating.


            if (IsSteamHeatFitted && this.IsLeadLocomotive())  // Only Update steam heating if train and locomotive fitted with steam heating
            {

                // Update water controller for steam boiler heating tank
                WaterController.Update(elapsedClockSeconds);
                if (WaterController.UpdateValue > 0.0)
                    simulator.Confirmer.UpdateWithPerCent(CabControl.SteamHeatBoilerWater, CabSetting.Increase, WaterController.CurrentValue * 100);


                CurrentSteamHeatPressurePSI = SteamHeatController.CurrentValue * MaxSteamHeatPressurePSI;

                // Calculate steam boiler usage values
                // Don't turn steam heat on until pressure valve has been opened, water and fuel capacity also needs to be present, and steam boiler is not locked out
                if (CurrentSteamHeatPressurePSI > 0.1 && CurrentLocomotiveSteamHeatBoilerWaterCapacityL > 0 && currentSteamHeatBoilerFuelCapacityL > 0 && !steamHeatBoilerLockedOut)
                {
                    // Set values for visible exhaust based upon setting of steam controller
                    HeatingSteamBoilerVolumeM3pS = 1.5f * SteamHeatController.CurrentValue;
                    HeatingSteamBoilerDurationS = 1.0f * SteamHeatController.CurrentValue;
                    Train.CarSteamHeatOn = true; // turn on steam effects on wagons

                    // Calculate fuel usage for steam heat boiler
                    float FuelUsageLpS = (float)Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(TrainHeatBoilerFuelUsageGalukpH[Frequency.Periodic.ToHours(CalculatedCarHeaterSteamUsageLBpS)]));
                    currentSteamHeatBoilerFuelCapacityL -= (float)(FuelUsageLpS * elapsedClockSeconds); // Reduce Tank capacity as fuel used.

                    // Calculate water usage for steam heat boiler
                    float WaterUsageLpS = (float)Size.LiquidVolume.FromGallonUK(Frequency.Periodic.FromHours(TrainHeatBoilerWaterUsageGalukpH[Frequency.Periodic.ToHours(CalculatedCarHeaterSteamUsageLBpS)]));
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL -= (float)(WaterUsageLpS * elapsedClockSeconds); // Reduce Tank capacity as water used.Weight of locomotive is reduced in Wagon.cs
                }
                else
                {
                    Train.CarSteamHeatOn = false; // turn on steam effects on wagons
                }


            }
        }


        /// <summary>
        /// This function updates periodically the locomotive's sound variables.
        /// </summary>
        protected override void UpdateSoundVariables(double elapsedClockSeconds)
        {
            Variable1 = ThrottlePercent;
            if (ThrottlePercent == 0f)
                Variable2 = 0;
            else
            {
                float dV2;
                dV2 = Math.Abs(TractiveForceN) / MaxForceN * 100f - Variable2;
                float max = 2f;
                if (dV2 > max)
                    dV2 = max;
                else if (dV2 < -max)
                    dV2 = -max;
                Variable2 += dV2;
            }
            if (DynamicBrakePercent > 0)
                Variable3 = MaxDynamicBrakeForceN == 0 ? DynamicBrakePercent / 100f : DynamicBrakeForceN / MaxDynamicBrakeForceN;
            else
                Variable3 = 0;
        }

        public override float GetDataOf(CabViewControl cvc)
        {
            float data = 0;

            switch (cvc.ControlType.CabViewControlType)
            {
                case CabViewControlType.Line_Voltage:
                    data = ElectricPowerSupply.PantographVoltageV;
                    if (cvc.ControlUnit == CabViewControlUnit.KiloVolts)
                        data /= 1000;
                    break;

                case CabViewControlType.Panto_Display:
                    data = Pantographs.State == PantographState.Up ? 1 : 0;
                    break;

                case CabViewControlType.Pantograph:
                    data = Pantographs[1].CommandUp ? 1 : 0;
                    break;

                case CabViewControlType.Pantograph2:
                    data = Pantographs[2].CommandUp ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Pantograph3:
                    data = Pantographs.Count > 2 && Pantographs[3].CommandUp ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Pantograph4:
                    data = Pantographs.Count > 3 && Pantographs[4].CommandUp ? 1 : 0;
                    break;

                case CabViewControlType.Pantographs_4:
                case CabViewControlType.Pantographs_4C:
                    if (Pantographs[1].CommandUp && Pantographs[2].CommandUp)
                        data = 2;
                    else if (Pantographs[1].CommandUp)
                        data = 1;
                    else if (Pantographs[2].CommandUp)
                        data = 3;
                    else
                        data = 0;
                    break;

                case CabViewControlType.Pantographs_5:
                    if (Pantographs[1].CommandUp && Pantographs[2].CommandUp)
                        data = 0; // TODO: Should be 0 if the previous state was Pan2Up, and 4 if that was Pan1Up
                    else if (Pantographs[2].CommandUp)
                        data = 1;
                    else if (Pantographs[1].CommandUp)
                        data = 3;
                    else
                        data = 2;
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Driver_Closing_Order:
                    data = ElectricPowerSupply.CircuitBreaker.DriverClosingOrder ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Driver_Opening_Order:
                    data = ElectricPowerSupply.CircuitBreaker.DriverOpeningOrder ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Driver_Closing_Authorization:
                    data = ElectricPowerSupply.CircuitBreaker.DriverClosingAuthorization ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_State:
                    switch (ElectricPowerSupply.CircuitBreaker.State)
                    {
                        case CircuitBreakerState.Open:
                            data = 0;
                            break;
                        case CircuitBreakerState.Closing:
                            data = 1;
                            break;
                        case CircuitBreakerState.Closed:
                            data = 2;
                            break;
                    }
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Closed:
                    switch (ElectricPowerSupply.CircuitBreaker.State)
                    {
                        case CircuitBreakerState.Open:
                        case CircuitBreakerState.Closing:
                            data = 0;
                            break;
                        case CircuitBreakerState.Closed:
                            data = 1;
                            break;
                    }
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Open:
                    switch (ElectricPowerSupply.CircuitBreaker.State)
                    {
                        case CircuitBreakerState.Open:
                        case CircuitBreakerState.Closing:
                            data = 1;
                            break;
                        case CircuitBreakerState.Closed:
                            data = 0;
                            break;
                    }
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Authorized:
                    data = ElectricPowerSupply.CircuitBreaker.ClosingAuthorization ? 1 : 0;
                    break;

                case CabViewControlType.Orts_Circuit_Breaker_Open_And_Authorized:
                    data = (ElectricPowerSupply.CircuitBreaker.State < CircuitBreakerState.Closed && ElectricPowerSupply.CircuitBreaker.ClosingAuthorization) ? 1 : 0;
                    break;

                default:
                    data = base.GetDataOf(cvc);
                    break;
            }

            return data;
        }

        public override void SwitchToAutopilotControl()
        {
            SetDirection(MidpointDirection.Forward);
            base.SwitchToAutopilotControl();
        }

        /// <summary>
        /// Returns the controller which refills from the matching pickup point.
        /// </summary>
        /// <param name="type">Pickup type</param>
        /// <returns>Matching controller or null</returns>
        public override MSTSNotchController GetRefillController(PickupType type)
        {
            return (type == PickupType.FuelWater) ? WaterController : null;
        }

        /// <summary>
        /// Sets step size for the fuel controller basing on pickup feed rate and engine fuel capacity
        /// </summary>
        /// <param name="type">Pickup</param>

        public override void SetStepSize(PickupObject matchPickup)
        {
            ArgumentNullException.ThrowIfNull(matchPickup);
            if (maximumSteamHeatBoilerWaterTankCapacityL != 0)
                WaterController.SetStepSize(matchPickup.Capacity.FeedRateKGpS / MSTSNotchController.StandardBoost / (float)maximumSteamHeatBoilerWaterTankCapacityL);
        }

        /// <summary>
        /// Sets coal and water supplies to full immediately.
        /// Provided in case route lacks pickup points for diesel oil.
        /// </summary>
        public override void RefillImmediately()
        {
            WaterController.CurrentValue = 1.0f;
        }

        /// <summary>
        /// Returns the fraction of diesel oil already in tank.
        /// </summary>
        /// <param name="pickupType">Pickup type</param>
        /// <returns>0.0 to 1.0. If type is unknown, returns 0.0</returns>
        public override float GetFilledFraction(PickupType pickupType)
        {
            return (pickupType == PickupType.FuelWater) ? WaterController.CurrentValue : 0f;
        }

        protected internal override void UpdateRemotePosition(double elapsedClockSeconds, float speed, float targetSpeed)
        {
            base.UpdateRemotePosition(elapsedClockSeconds, speed, targetSpeed);
            if (AbsSpeedMpS > 0.5f)
            {
                Variable1 = 70;
                Variable2 = 70;
            }
            else
            {
                Variable1 = 0;
                Variable2 = 0;
            }
        }

        private protected override void UpdateCarStatus()
        {
            base.UpdateCarStatus();
            carInfo["Pantographs"] = string.Join(' ', Pantographs.Select(p => p.State.GetLocalizedDescription()));
            carInfo["BatterySwitch"] = LocomotivePowerSupply.BatterySwitch.On ? Simulator.Catalog.GetString("On") : Simulator.Catalog.GetString("Off");
            carInfo["MasterKey"]= LocomotivePowerSupply.MasterKey.On ? Simulator.Catalog.GetString("On") : Simulator.Catalog.GetString("Off");
            carInfo["CircuitBreaker"] = ElectricPowerSupply.CircuitBreaker.State.GetLocalizedDescription();
            carInfo["ElectricTrainSupply"] = LocomotivePowerSupply.ElectricTrainSupplySwitch.On ? Simulator.Catalog.GetString("On") : Simulator.Catalog.GetString("Off");
            carInfo["PowerSupply"] = LocomotivePowerSupply.MainPowerSupplyState.GetLocalizedDescription();

            carInfo["TCS Autorization"] = ElectricPowerSupply.CircuitBreaker.TCSClosingAuthorization ? Simulator.Catalog.GetString("OK") : Simulator.Catalog.GetString("NOT OK");
            carInfo["Driver Autorization"] = ElectricPowerSupply.CircuitBreaker.DriverClosingAuthorization ? Simulator.Catalog.GetString("OK") : Simulator.Catalog.GetString("NOT OK");
            carInfo["Auxiliar power"] = LocomotivePowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
        }
    } // class ElectricLocomotive
}
