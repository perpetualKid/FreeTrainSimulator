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

/* DIESEL LOCOMOTIVE CLASSES
 * 
 * The Locomotive is represented by two classes:
 *  MSTSDieselLocomotiveSimulator - defines the behaviour, ie physics, motion, power generated etc
 *  MSTSDieselLocomotiveViewer - defines the appearance in a 3D viewer.  The viewer doesn't
 *  get attached to the car until it comes into viewing range.
 *  
 * Both these classes derive from corresponding classes for a basic locomotive
 *  LocomotiveSimulator - provides for movement, basic controls etc
 *  LocomotiveViewer - provides basic animation for running gear, wipers, etc
 * 
 */

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Orts.Common;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.Simulation.RollingStocks
{
    public class MSTSControlTrailerCar : MSTSLocomotive
    {
        private bool hasGearController;
        private bool controlGearUp;
        private bool controlGearDown;
        private int controlGearIndex;
        private int controlGearIndication;
        private bool controlGearBoxTypeC;

        public int ControllerNumberOfGears { get; private set; } = 1;

        public MSTSControlTrailerCar(string wagFile) :
            base(wagFile)

        {

            PowerSupply = new ScriptedControlCarPowerSupply(this);

        }

        public override void LoadFromWagFile(string wagFilePath)
        {
            base.LoadFromWagFile(wagFilePath);

            Trace.TraceInformation("Control Trailer");
        }

        public override void Initialize()
        {
            // Initialise gearbox controller
            if (ControllerNumberOfGears > 0)
            {
                GearBoxController = new MSTSNotchController(ControllerNumberOfGears + 1);
                if (Simulator.Instance.Settings.VerboseConfigurationMessages)
                    hasGearController = true;
                //                Trace.TraceInformation("Control Car Gear Controller created");
                controlGearIndex = 0;
                Train.HasControlCarWithGear = true;
                base.Initialize();
            }
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
                case "engine(ortstractioncutoffrelay":
                case "engine(ortstractioncutoffrelayclosingdelay":
                case "engine(ortsbattery(mode":
                case "engine(ortsbattery(delay":
                case "engine(ortsbattery(defaulton":
                case "engine(ortsmasterkey(mode":
                case "engine(ortsmasterkey(delayoff":
                case "engine(ortsmasterkey(headlightcontrol":
                case "engine(ortselectrictrainsupply(mode":
                case "engine(ortselectrictrainsupply(dieselengineminrpm":
                    LocomotivePowerSupply.Parse(lowercasetoken, stf);
                    break;

                // to setup gearbox controller
                case "engine(gearboxcontrollernumberofgears":
                    ControllerNumberOfGears = stf.ReadIntBlock(null);
                    break;
                default:
                    base.Parse(lowercasetoken, stf);
                    break;
            }
        }

        /// <summary>
        /// This initializer is called when we are making a new copy of a locomotive already
        /// loaded in memory.  We use this one to speed up loading by eliminating the
        /// need to parse the wag file multiple times.
        /// NOTE:  you must initialize all the same variables as you parsed above
        /// </summary>
        public override void Copy(MSTSWagon source)
        {
            base.Copy(source);  // each derived level initializes its own variables

            if (source is not MSTSControlTrailerCar controlTrailerCar)
                throw new System.InvalidCastException();

            ControllerNumberOfGears = controlTrailerCar.ControllerNumberOfGears;
        }

        public override async ValueTask<TrainCarSaveState> Snapshot()
        {
            TrainCarSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.LocomotiveSaveState.ControlTrailerSaveState = new ControlTrailerSaveState()
            {
                GearboxControllerSaveState = await GearBoxController.Snapshot().ConfigureAwait(false),
                GearBoxIndication = controlGearIndication,
                GearIndex = controlGearIndex,
            };
            return saveState;
        }

        public override async ValueTask Restore([NotNull] TrainCarSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(saveState.LocomotiveSaveState.ControlTrailerSaveState, nameof(saveState.LocomotiveSaveState.ControlTrailerSaveState));
            ControlTrailerSaveState controlTrailerSaveState = saveState.LocomotiveSaveState.ControlTrailerSaveState;

            GearBoxController ??= new MSTSNotchController();
            await GearBoxController.Restore(controlTrailerSaveState.GearboxControllerSaveState).ConfigureAwait(false);
            controlGearIndication = controlTrailerSaveState.GearBoxIndication;
            controlGearIndex = controlTrailerSaveState.GearIndex;
        }

        /// <summary>
        /// Set starting conditions  when initial speed > 0 
        /// 
        public override void InitializeMoving()
        {
            base.InitializeMoving();
            WheelSpeedMpS = SpeedMpS;

            ThrottleController.SetValue(Train.MUThrottlePercent / 100);
        }

        /// <summary>
        /// This function updates periodically the states and physical variables of the locomotive's subsystems.
        /// </summary>
        public override void Update(double elapsedClockSeconds)
        {
            base.Update(elapsedClockSeconds);
            WheelSpeedMpS = SpeedMpS; // Set wheel speed for control car, required to make wheels go around.

            if (ControllerNumberOfGears > 0 && IsLeadLocomotive() && GearBoxController != null)
            {
                // Pass gearbox command key to other locomotives in train, don't treat the player locomotive in this fashion.
                // This assumes that Contol cars have been "matched" with motor cars. Also return values will be on the basis of the last motor car in the train.         
                foreach (TrainCar car in Train.Cars)
                {
                    if (car is MSTSDieselLocomotive dieselLocomotive && car != this && !dieselLocomotive.IsLeadLocomotive() && (controlGearDown || controlGearUp))
                    {

                        if (controlGearUp)
                        {

                            dieselLocomotive.GearBoxController.NotchIndex = GearBoxController.NotchIndex;
                            dieselLocomotive.GearBoxController.SetValue(dieselLocomotive.GearBoxController.NotchIndex);

                            dieselLocomotive.ChangeGearUp();
                        }

                        if (controlGearDown)
                        {

                            dieselLocomotive.GearBoxController.NotchIndex = GearBoxController.NotchIndex;
                            dieselLocomotive.GearBoxController.SetValue(dieselLocomotive.GearBoxController.NotchIndex);

                            dieselLocomotive.ChangeGearDown();
                        }

                        // Read values for the HuD, will be based upon the last motorcar
                        controlGearIndex = dieselLocomotive.DieselEngines[0].GearBox.CurrentGearIndex;
                        controlGearIndication = dieselLocomotive.DieselEngines[0].GearBox.GearIndication;
                        if (dieselLocomotive.DieselEngines[0].GearBox.GearBoxType == SubSystems.PowerTransmissions.GearBoxType.C)
                        {
                            controlGearBoxTypeC = true;
                        }
                    }
                }

                // Rest gear flags once all the cars have been processed
                controlGearUp = false;
                controlGearDown = false;
            }
        }

        private protected override void UpdateCarStatus()
        {
            base.UpdateCarStatus();
            if (hasGearController)
                carInfo["Gear"] = controlGearIndex < 0 ? Simulator.Catalog.GetParticularString("Gear", "N") : $"{controlGearIndication}";
        }

        /// <summary>
        /// This function updates periodically the locomotive's motive force.
        /// </summary>
        protected override void UpdateTractiveForce(double elapsedClockSeconds, float t, float AbsSpeedMpS, float AbsWheelSpeedMpS)
        {

        }

        /// <summary>
        /// This function updates periodically the locomotive's sound variables.
        /// </summary>
        protected override void UpdateSoundVariables(double elapsedClockSeconds)
        {

        }


        public override void ChangeGearUp()
        {

            if (controlGearBoxTypeC)
            {
                if (ThrottlePercent == 0)
                {
                    GearBoxController.NotchIndex += 1;
                }
                else
                {
                    Simulator.Instance.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Throttle must be reduced to Idle before gear change can happen."));
                }
            }
            else
            {
                GearBoxController.NotchIndex += 1;
            }

            if (GearBoxController.NotchIndex > ControllerNumberOfGears)
            {
                GearBoxController.NotchIndex = ControllerNumberOfGears;
            }

            controlGearUp = true;
            controlGearDown = false;

        }

        public override void ChangeGearDown()
        {
            if (controlGearBoxTypeC)
            {
                if (ThrottlePercent == 0)
                {
                    GearBoxController.NotchIndex -= 1;
                }
                else
                {
                    Simulator.Instance.Confirmer.Message(ConfirmLevel.Warning, Simulator.Catalog.GetString("Throttle must be reduced to Idle before gear change can happen."));
                }
            }
            else
            {
                GearBoxController.NotchIndex -= 1;
            }

            if (GearBoxController.NotchIndex < 0)
            {
                GearBoxController.NotchIndex = 0;
            }

            controlGearUp = false;
            controlGearDown = true;
        }
    }
}
