// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock
{
    public class MSTSSteamLocomotiveViewer : MSTSLocomotiveViewer
    {
        private float throttlepercent;
        private float color_Value;

        private MSTSSteamLocomotive SteamLocomotive => (MSTSSteamLocomotive)Car;

        private List<ParticleEmitterViewer> Cylinders = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders2 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders11 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders12 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders21 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders22 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders31 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders32 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders41 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Cylinders42 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Blowdown = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Drainpipe = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Injectors1 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Injectors2 = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Compressor = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Generator = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> SafetyValves = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Stack = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> Whistle = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> SmallEjector = new List<ParticleEmitterViewer>();
        private List<ParticleEmitterViewer> LargeEjector = new List<ParticleEmitterViewer>();

        public MSTSSteamLocomotiveViewer(Viewer viewer, MSTSSteamLocomotive car)
            : base(viewer, car)
        {
            // Now all the particle drawers have been setup, assign them textures based
            // on what emitters we know about.
            string steamTexture = viewer.Simulator.RouteFolder.ContentFolder.TextureFile("smokemain.ace");

            foreach (KeyValuePair<string, List<ParticleEmitterViewer>> emitter in ParticleDrawers)
            {
                if (emitter.Key.Equals("cylindersfx", StringComparison.OrdinalIgnoreCase)) // This parameter retained as legacy parameters only, ideally they should be removed eventually
                    Cylinders.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders2fx", StringComparison.OrdinalIgnoreCase)) // This parameter retained as legacy parameters only, ideally they should be removed eventually
                {
                    Cylinders2.AddRange(emitter.Value);
                    car.Cylinder2SteamEffects = true;
                }
                else if (emitter.Key.Equals("cylinders11fx", StringComparison.OrdinalIgnoreCase))
                {
                    Cylinders11.AddRange(emitter.Value);
                    car.CylinderAdvancedSteamEffects = true;
                }
                else if (emitter.Key.Equals("cylinders12fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders12.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders21fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders21.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders22fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders22.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders31fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders31.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders32fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders32.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders41fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders41.AddRange(emitter.Value);
                else if (emitter.Key.Equals("cylinders42fx", StringComparison.OrdinalIgnoreCase))
                    Cylinders42.AddRange(emitter.Value);
                else if (emitter.Key.Equals("blowdownfx", StringComparison.OrdinalIgnoreCase))
                    Blowdown.AddRange(emitter.Value);
                else if (emitter.Key.Equals("drainpipefx", StringComparison.OrdinalIgnoreCase))        // Drainpipe was not used in MSTS, and has no control set up for it
                    Drainpipe.AddRange(emitter.Value);
                else if (emitter.Key.Equals("injectors1fx", StringComparison.OrdinalIgnoreCase))
                    Injectors1.AddRange(emitter.Value);
                else if (emitter.Key.Equals("injectors2fx", StringComparison.OrdinalIgnoreCase))
                    Injectors2.AddRange(emitter.Value);
                else if (emitter.Key.Equals("smallejectorfx", StringComparison.OrdinalIgnoreCase))
                    SmallEjector.AddRange(emitter.Value);
                else if (emitter.Key.Equals("largeejectorfx", StringComparison.OrdinalIgnoreCase))
                    LargeEjector.AddRange(emitter.Value);
                else if (emitter.Key.Equals("compressorfx", StringComparison.OrdinalIgnoreCase))
                    Compressor.AddRange(emitter.Value);
                else if (emitter.Key.Equals("generatorfx", StringComparison.OrdinalIgnoreCase))
                {
                    Generator.AddRange(emitter.Value);
                    car.GeneratorSteamEffects = true;
                }
                else if (emitter.Key.Equals("safetyvalvesfx", StringComparison.OrdinalIgnoreCase))
                    SafetyValves.AddRange(emitter.Value);
                else if (emitter.Key.Equals("stackfx", StringComparison.OrdinalIgnoreCase))
                    Stack.AddRange(emitter.Value);
                else if (emitter.Key.Equals("whistlefx", StringComparison.OrdinalIgnoreCase))
                    Whistle.AddRange(emitter.Value);
                foreach (var drawer in emitter.Value)
                    drawer.Initialize(steamTexture);
            }
        }

        /// <summary>
        /// Overrides the base method as steam locomotives have continuous reverser controls and so
        /// lacks the throttle interlock and warning in other locomotives. 
        /// </summary>
        protected override void ReverserControlForwards()
        {
            SteamLocomotive.StartReverseIncrease(null);
        }

        /// <summary>
        /// Overrides the base method as steam locomotives have continuous reverser controls and so
        /// lacks the throttle interlock and warning in other locomotives. 
        /// </summary>
        protected override void ReverserControlBackwards()
        {
            SteamLocomotive.StartReverseDecrease(null);
        }

        /// <summary>
        /// Overrides the base method as steam locomotives have only rudimentary gear boxes. 
        /// </summary>
        protected override void StartGearBoxIncrease()
        {
            SteamLocomotive.SteamStartGearBoxIncrease();
        }

        protected override void StopGearBoxIncrease()
        {
            SteamLocomotive.SteamStopGearBoxIncrease();
        }

        protected override void StartGearBoxDecrease()
        {
            SteamLocomotive.SteamStartGearBoxDecrease();
        }

        protected override void StopGearBoxDecrease()
        {
            SteamLocomotive.SteamStopGearBoxDecrease();
        }

        public override void RegisterUserCommandHandling()
        {
            Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserForward, KeyEventType.KeyPressed, ReverserControlForwards, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyPressed, ReverserControlBackwards, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserForward, KeyEventType.KeyReleased, SteamLocomotive.StopReverseIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyReleased, SteamLocomotive.StopReverseDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFiring, KeyEventType.KeyPressed, ToggleManualFiringCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlAIFireOn, KeyEventType.KeyPressed, AIFireOnCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlAIFireOff, KeyEventType.KeyPressed, AIFireOffCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlAIFireReset, KeyEventType.KeyPressed, AIFireResetCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector1Increase, KeyEventType.KeyPressed, SteamLocomotive.StartInjector1Increase, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector1Increase, KeyEventType.KeyReleased, SteamLocomotive.StopInjector1Increase, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector1Decrease, KeyEventType.KeyPressed, SteamLocomotive.StartInjector1Decrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector1Decrease, KeyEventType.KeyReleased, SteamLocomotive.StopInjector1Decrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector2Increase, KeyEventType.KeyPressed, SteamLocomotive.StartInjector2Increase, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector2Increase, KeyEventType.KeyReleased, SteamLocomotive.StopInjector2Increase, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector2Decrease, KeyEventType.KeyPressed, SteamLocomotive.StartInjector2Decrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector2Decrease, KeyEventType.KeyReleased, SteamLocomotive.StopInjector2Decrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector1, KeyEventType.KeyPressed, ToggleInjector1Command, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlInjector2, KeyEventType.KeyPressed, ToggleInjector2Command, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBlowerIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartBlowerIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBlowerIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopBlowerIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBlowerDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartBlowerDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBlowerDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopBlowerDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDamperIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartDamperIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDamperIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopDamperIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDamperDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartDamperDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlDamperDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopDamperDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFireboxOpen, KeyEventType.KeyPressed, SteamLocomotive.StartFireboxDoorIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFireboxOpen, KeyEventType.KeyReleased, SteamLocomotive.StopFireboxDoorIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFireboxClose, KeyEventType.KeyPressed, SteamLocomotive.StartBlowerDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFireboxClose, KeyEventType.KeyReleased, SteamLocomotive.StartFireboxDoorDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFiringRateIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartFiringRateIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFiringRateIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopFiringRateIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFiringRateDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartFiringRateDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFiringRateDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopFiringRateDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlFireShovelFull, KeyEventType.KeyPressed, FireShovelfullCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlBlowdownValve, KeyEventType.KeyPressed, ToggleBlowdownValveCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCylinderCocks, KeyEventType.KeyPressed, ToggleCylinderCocksCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlCylinderCompound, KeyEventType.KeyPressed, ToggleCylinderCompoundCommand, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSmallEjectorIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartSmallEjectorIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSmallEjectorIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopSmallEjectorIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSmallEjectorDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartSmallEjectorDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlSmallEjectorDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopSmallEjectorDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlLargeEjectorIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartLargeEjectorIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlLargeEjectorIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopLargeEjectorIncrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlLargeEjectorDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartLargeEjectorDecrease, true);
            Viewer.UserCommandController.AddEvent(UserCommand.ControlLargeEjectorDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopLargeEjectorDecrease, true);

            base.RegisterUserCommandHandling();
            // Steam locomotives handle these differently, so we remove the base class handling
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserForward, KeyEventType.KeyPressed, base.ReverserControlForwards);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyPressed, base.ReverserControlBackwards);
        }

        public override void UnregisterUserCommandHandling()
        {
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserForward, KeyEventType.KeyPressed, ReverserControlForwards);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyPressed, ReverserControlBackwards);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserForward, KeyEventType.KeyReleased, SteamLocomotive.StopReverseIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyReleased, SteamLocomotive.StopReverseDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFiring, KeyEventType.KeyPressed, ToggleManualFiringCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAIFireOn, KeyEventType.KeyPressed, AIFireOnCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAIFireOff, KeyEventType.KeyPressed, AIFireOffCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAIFireReset, KeyEventType.KeyPressed, AIFireResetCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector1Increase, KeyEventType.KeyPressed, SteamLocomotive.StartInjector1Increase);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector1Increase, KeyEventType.KeyReleased, SteamLocomotive.StopInjector1Increase);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector1Decrease, KeyEventType.KeyPressed, SteamLocomotive.StartInjector1Decrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector1Decrease, KeyEventType.KeyReleased, SteamLocomotive.StopInjector1Decrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector2Increase, KeyEventType.KeyPressed, SteamLocomotive.StartInjector2Increase);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector2Increase, KeyEventType.KeyReleased, SteamLocomotive.StopInjector2Increase);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector2Decrease, KeyEventType.KeyPressed, SteamLocomotive.StartInjector2Decrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector2Decrease, KeyEventType.KeyReleased, SteamLocomotive.StopInjector2Decrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector1, KeyEventType.KeyPressed, ToggleInjector1Command);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlInjector2, KeyEventType.KeyPressed, ToggleInjector2Command);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBlowerIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartBlowerIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBlowerIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopBlowerIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBlowerDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartBlowerDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBlowerDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopBlowerDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDamperIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartDamperIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDamperIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopDamperIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDamperDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartDamperDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDamperDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopDamperDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFireboxOpen, KeyEventType.KeyPressed, SteamLocomotive.StartFireboxDoorIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFireboxOpen, KeyEventType.KeyReleased, SteamLocomotive.StopFireboxDoorIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFireboxClose, KeyEventType.KeyPressed, SteamLocomotive.StartBlowerDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFireboxClose, KeyEventType.KeyReleased, SteamLocomotive.StartFireboxDoorDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFiringRateIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartFiringRateIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFiringRateIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopFiringRateIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFiringRateDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartFiringRateDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFiringRateDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopFiringRateDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFireShovelFull, KeyEventType.KeyPressed, FireShovelfullCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlBlowdownValve, KeyEventType.KeyPressed, ToggleBlowdownValveCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCylinderCocks, KeyEventType.KeyPressed, ToggleCylinderCocksCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCylinderCompound, KeyEventType.KeyPressed, ToggleCylinderCompoundCommand);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSmallEjectorIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartSmallEjectorIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSmallEjectorIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopSmallEjectorIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSmallEjectorDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartSmallEjectorDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSmallEjectorDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopSmallEjectorDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlLargeEjectorIncrease, KeyEventType.KeyPressed, SteamLocomotive.StartLargeEjectorIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlLargeEjectorIncrease, KeyEventType.KeyReleased, SteamLocomotive.StopLargeEjectorIncrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlLargeEjectorDecrease, KeyEventType.KeyPressed, SteamLocomotive.StartLargeEjectorDecrease);
            Viewer.UserCommandController.RemoveEvent(UserCommand.ControlLargeEjectorDecrease, KeyEventType.KeyReleased, SteamLocomotive.StopLargeEjectorDecrease);

            base.UnregisterUserCommandHandling();
        }

        private void FireShovelfullCommand()
        {
            _ = new FireShovelfullCommand(Viewer.Log);
        }

        private void ToggleBlowdownValveCommand()
        {
            _ = new ToggleBlowdownValveCommand(Viewer.Log);
        }

        private void ToggleCylinderCocksCommand()
        {
            _ = new ToggleCylinderCocksCommand(Viewer.Log);
        }

        private void ToggleCylinderCompoundCommand()
        {
            _ = new ToggleCylinderCompoundCommand(Viewer.Log);
        }
        private void ToggleInjector1Command()
        {
            _ = new ToggleInjectorCommand(Viewer.Log, 1);
        }

        private void ToggleInjector2Command()
        {
            _ = new ToggleInjectorCommand(Viewer.Log, 2);
        }

        private void ToggleManualFiringCommand()
        {
            _ = new ToggleManualFiringCommand(Viewer.Log);
        }

        private void AIFireOnCommand()
        {
            _ = new AIFireOnCommand(Viewer.Log);
        }

        private void AIFireOffCommand()
        {
            _ = new AIFireOffCommand(Viewer.Log);
        }

        private void AIFireResetCommand()
        {
            _ = new AIFireResetCommand(Viewer.Log);
        }

        private protected override void DirectionHandleCommand(UserCommandArgs commandArgs, GameTime gameTime)
        {
            if (commandArgs is UserCommandArgs<float> handleCommandArgs)
            {
                SteamLocomotive.SetCutoffPercent(handleCommandArgs.Value);
            }
            base.DirectionHandleCommand(commandArgs, gameTime);
        }
        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var car = Car as MSTSSteamLocomotive;

            foreach (var drawer in Cylinders)
                drawer.SetOutput(car.Cylinders1SteamVelocityMpS, car.Cylinders1SteamVolumeM3pS, car.Cylinder1ParticleDurationS);

            foreach (var drawer in Cylinders2)
                drawer.SetOutput(car.Cylinders2SteamVelocityMpS, car.Cylinders2SteamVolumeM3pS, car.Cylinder2ParticleDurationS);

            foreach (var drawer in Cylinders11)
                drawer.SetOutput(car.Cylinders1SteamVelocityMpS, car.Cylinders11SteamVolumeM3pS, car.Cylinder1ParticleDurationS);

            foreach (var drawer in Cylinders12)
                drawer.SetOutput(car.Cylinders2SteamVelocityMpS, car.Cylinders12SteamVolumeM3pS, car.Cylinder2ParticleDurationS);

            foreach (var drawer in Cylinders21)
                drawer.SetOutput(car.Cylinders1SteamVelocityMpS, car.Cylinders21SteamVolumeM3pS, car.Cylinder1ParticleDurationS);

            foreach (var drawer in Cylinders22)
                drawer.SetOutput(car.Cylinders2SteamVelocityMpS, car.Cylinders22SteamVolumeM3pS, car.Cylinder2ParticleDurationS);

            foreach (var drawer in Cylinders31)
                drawer.SetOutput(car.Cylinders1SteamVelocityMpS, car.Cylinders31SteamVolumeM3pS, car.Cylinder1ParticleDurationS);

            foreach (var drawer in Cylinders32)
                drawer.SetOutput(car.Cylinders2SteamVelocityMpS, car.Cylinders32SteamVolumeM3pS, car.Cylinder2ParticleDurationS);

            foreach (var drawer in Cylinders41)
                drawer.SetOutput(car.Cylinders1SteamVelocityMpS, car.Cylinders41SteamVolumeM3pS, car.Cylinder1ParticleDurationS);

            foreach (var drawer in Cylinders42)
                drawer.SetOutput(car.Cylinders2SteamVelocityMpS, car.Cylinders42SteamVolumeM3pS, car.Cylinder2ParticleDurationS);

            foreach (var drawer in Blowdown)
                drawer.SetOutput(car.BlowdownSteamVelocityMpS, car.BlowdownSteamVolumeM3pS, car.BlowdownParticleDurationS);

            // TODO: Drainpipe - Not used in either MSTS or OR - currently disabled by zero values set in SteamLocomotive file
            foreach (var drawer in Drainpipe)
                drawer.SetOutput(car.DrainpipeSteamVelocityMpS, car.DrainpipeSteamVolumeM3pS, car.DrainpipeParticleDurationS);

            foreach (var drawer in Injectors1)
                drawer.SetOutput(car.Injector1SteamVelocityMpS, car.Injector1SteamVolumeM3pS, car.Injector1ParticleDurationS);

            foreach (var drawer in Injectors2)
                drawer.SetOutput(car.Injector2SteamVelocityMpS, car.Injector2SteamVolumeM3pS, car.Injector2ParticleDurationS);

            foreach (var drawer in SmallEjector)
                drawer.SetOutput(car.SmallEjectorSteamVelocityMpS, car.SmallEjectorSteamVolumeM3pS, car.SmallEjectorParticleDurationS);

            foreach (var drawer in LargeEjector)
                drawer.SetOutput(car.LargeEjectorSteamVelocityMpS, car.LargeEjectorSteamVolumeM3pS, car.LargeEjectorParticleDurationS);

            foreach (var drawer in Compressor)
                drawer.SetOutput(car.CompressorSteamVelocityMpS, car.CompressorSteamVolumeM3pS, car.CompressorParticleDurationS);

            foreach (var drawer in Generator)
                drawer.SetOutput(car.GeneratorSteamVelocityMpS, car.GeneratorSteamVolumeM3pS, car.GeneratorParticleDurationS);

            foreach (var drawer in SafetyValves)
                drawer.SetOutput(car.SafetyValvesSteamVelocityMpS, car.SafetyValvesSteamVolumeM3pS, car.SafetyValvesParticleDurationS);

            throttlepercent = car.ThrottlePercent > 0 ? car.ThrottlePercent / 10f : 0f;

            foreach (var drawer in Stack)
            {
                color_Value = (float)car.SmokeColor.SmoothedValue;
                drawer.SetOutput((float)car.StackSteamVelocityMpS.SmoothedValue, car.StackSteamVolumeM3pS / Stack.Count + car.FireRatio, throttlepercent + car.FireRatio, new Color(color_Value, color_Value, color_Value));
            }

            foreach (var drawer in Whistle)
                drawer.SetOutput(car.WhistleSteamVelocityMpS, car.WhistleSteamVolumeM3pS, car.WhistleParticleDurationS);

            base.PrepareFrame(frame, elapsedTime);
        }
    }
}
