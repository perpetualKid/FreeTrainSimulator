using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Input;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.RollingStock;
using Orts.Scripting.Api;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;

using Simulation = Orts.Simulation.Simulator;

namespace Tests.FreeTrainSimulator.Common.Input
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class RailDriverTests
    {
        [TestMethod]
        public void DuplicateMappingsReportOriginalCommandNamesAfterUnassignedEntries()
        {
            EnumArray<byte, UserCommand> commands = new EnumArray<byte, UserCommand>(byte.MaxValue);
            commands[UserCommand.GameSave] = 7;
            commands[UserCommand.ControlHorn] = 7;

            string errors = commands.ToArray().CheckForErrors();

            Assert.AreEqual($"Button 7 is assigned to \r\n\t\"{UserCommand.GameSave.GetLocalizedDescription()}\" and \"{UserCommand.ControlHorn.GetLocalizedDescription()}\"{Environment.NewLine}", errors);
        }

        [TestMethod]
        public void UnassignedEntriesAndDistinctButtonsDoNotReportDuplicates()
        {
            EnumArray<byte, UserCommand> commands = new EnumArray<byte, UserCommand>(byte.MaxValue);
            commands[UserCommand.GamePauseMenu] = 0;
            commands[UserCommand.ControlHorn] = 47;

            Assert.AreEqual(string.Empty, commands.ToArray().CheckForErrors());
        }

        [TestMethod]
        [DataRow(42)]
        [DataRow(43)]
        public void HornReleaseIsForwardedAfterRunStopDeactivatesInput(int hornButton)
        {
            RailDriverInputGameComponent component = (RailDriverInputGameComponent)RuntimeHelpers.GetUninitializedObject(typeof(RailDriverInputGameComponent));
            UserCommandController<UserCommand> controller = Bind(component);
            bool hornHeld = false;
            int releases = 0;
            controller.AddEvent(UserCommand.ControlHorn, KeyEventType.KeyPressed, () => hornHeld = true);
            controller.AddEvent(UserCommand.ControlHorn, KeyEventType.KeyReleased, () =>
            {
                hornHeld = false;
                releases++;
            });
            controller.AddEvent(UserCommand.GameExternalCabController, KeyEventType.KeyPressed, () =>
                typeof(RailDriverInputGameComponent).GetProperty(nameof(RailDriverInputGameComponent.Active)).SetValue(component, false));
            typeof(RailDriverInputGameComponent).GetProperty(nameof(RailDriverInputGameComponent.Active)).SetValue(component, true);
            Action<int, GameTime, KeyEventType> input = (Action<int, GameTime, KeyEventType>)typeof(RailDriverInputGameComponent)
                .GetField("keyActionHandler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);

            input(RailDriverInputGameComponent.KeyEventCode((byte)hornButton, KeyEventType.KeyPressed), new GameTime(), KeyEventType.KeyPressed);
            Assert.IsTrue(hornHeld);
            input(RailDriverInputGameComponent.KeyEventCode(14, KeyEventType.KeyPressed), new GameTime(), KeyEventType.KeyPressed);
            Assert.IsFalse(component.Active);
            input(RailDriverInputGameComponent.KeyEventCode((byte)hornButton, KeyEventType.KeyReleased), new GameTime(), KeyEventType.KeyReleased);

            Assert.IsFalse(hornHeld);
            Assert.AreEqual(1, releases);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void EmergencyHandleAppliesPressureAcrossUpdatesAndReturnsToServiceAndRelease(bool pbl2)
        {
            ScriptedBrakeController brake = CreateBrakeController(pbl2);
            Action<float> demand = BindTrainBrake(brake);
            demand(200);
            brake.Update(0.5);
            double pressure = UpdatePressure(brake, 5, 0.5);
            Assert.AreEqual(4.5, pressure, 0.000001);
            brake.Update(0.5);
            pressure = UpdatePressure(brake, pressure, 0.5);
            Assert.AreEqual(4, pressure, 0.000001);
            Assert.AreEqual(1f, brake.CurrentValue);
            Assert.AreEqual(ControllerState.Emergency, brake.State);

            demand(100);
            brake.Update(1);
            pressure = UpdatePressure(brake, pressure, 1);
            Assert.AreEqual(3.75, pressure, 0.000001);
            Assert.AreEqual(0.7f, brake.CurrentValue);
            Assert.AreEqual(pbl2 ? ControllerState.Apply : ControllerState.FullServ, brake.State);

            demand(0);
            brake.Update(1);
            pressure = UpdatePressure(brake, pressure, 1);
            Assert.AreEqual(4.25, pressure, 0.000001);
            Assert.AreEqual(0f, brake.CurrentValue);
            Assert.AreEqual(ControllerState.Release, brake.State);
            Assert.IsFalse(brake.EmergencyBrakingPushButton);
            Assert.IsFalse(brake.TCSEmergencyBraking);
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(false, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        [DataRow(true, false, true)]
        [DataRow(true, true, true)]
        public void MovingHandleOutOfEmergencyDoesNotClearOtherEmergencySources(bool pbl2, bool pushbutton, bool tcs)
        {
            ScriptedBrakeController brake = CreateBrakeController(pbl2);
            brake.EmergencyBrakingPushButton = pushbutton;
            brake.TCSEmergencyBraking = tcs;
            Action<float> demand = BindTrainBrake(brake);
            demand(200);
            brake.Update(1);
            double pressure = UpdatePressure(brake, 5, 1);
            Assert.AreEqual(4, pressure, 0.000001);

            demand(0);
            brake.Update(0.5);
            pressure = UpdatePressure(brake, pressure, 0.5);

            Assert.AreEqual(pushbutton || tcs ? 3.5 : 4.25, pressure, 0.000001);
            Assert.AreEqual(pushbutton, brake.EmergencyBrakingPushButton);
            Assert.AreEqual(tcs, brake.TCSEmergencyBraking);
            Assert.AreEqual(pushbutton || tcs, brake.EmergencyBraking);
            Assert.AreEqual(0f, brake.CurrentValue);
        }

        [TestMethod]
        [DataRow(0f, 0f, 4.5)]
        [DataRow(39f, 0f, 4.5)]
        [DataRow(40f, 0.5f, 4.0)]
        [DataRow(50f, 0.5f, 4.0)]
        [DataRow(60f, 0.5f, 4.0)]
        [DataRow(61f, 0.7f, 3.75)]
        [DataRow(100f, 0.7f, 3.75)]
        public void Pbl2ServicePercentagesUseReleaseLapAndApplyWithoutEmergency(float percent, float position, double expectedPressure)
        {
            ScriptedBrakeController brake = CreateBrakeController(true);
            brake.SetPercent(percent);
            brake.Update(1);

            Assert.AreEqual(expectedPressure, UpdatePressure(brake, 4, 1), 0.000001);
            Assert.AreEqual(position, brake.CurrentValue);
            Assert.IsFalse(brake.EmergencyBraking);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void Pbl2KeyboardMovementExitsHandleEmergency(bool increase, bool targeted)
        {
            ScriptedBrakeController brake = CreateBrakeController(true);
            brake.SetPercent(200);
            brake.Update(1);
            double pressure = UpdatePressure(brake, 5, 1);
            Assert.AreEqual(4, pressure, 0.000001);
            Assert.AreEqual(ControllerState.Emergency, brake.State);

            if (targeted)
            {
                if (increase)
                    brake.StartIncrease(0.7f);
                else
                    brake.StartDecrease(0f);
            }
            else if (increase)
                brake.StartIncrease();
            else
                brake.StartDecrease();
            brake.Update(1);
            pressure = UpdatePressure(brake, pressure, 1);

            Assert.AreEqual(increase ? 3.75 : 4.5, pressure, 0.000001);
            Assert.AreEqual(increase ? 0.7f : 0f, brake.CurrentValue);
            Assert.AreEqual(increase ? ControllerState.Apply : ControllerState.Release, brake.State);
            Assert.IsFalse(brake.EmergencyBraking);

            if (increase)
                brake.StopIncrease();
            else
                brake.StopDecrease();
            brake.Update(1);
            Assert.AreEqual(pressure, UpdatePressure(brake, pressure, 1), 0.000001);
            Assert.AreEqual(0.5f, brake.CurrentValue);
            Assert.AreEqual(ControllerState.Lap, brake.State);
        }

        [TestMethod]
        public void Pbl2WithoutEmergencyNotchDoesNotTreatReleaseAsEmergency()
        {
            ScriptedBrakeController brake = CreateBrakeController(true, false);
            brake.SetPercent(0);
            brake.Update(1);

            Assert.AreEqual(4.5, UpdatePressure(brake, 4, 1), 0.000001);
            Assert.AreEqual(ControllerState.Release, brake.State);
            Assert.IsFalse(brake.EmergencyBraking);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task EmergencyHandleStateSurvivesSaveRestoreAndContinuesReducingPressure(bool pbl2)
        {
            ScriptedBrakeController original = CreateBrakeController(pbl2);
            original.SetPercent(200);
            original.Update(1);
            double pressure = UpdatePressure(original, 5, 1);
            ScriptedBrakeController restored = CreateBrakeController(pbl2);
            await restored.Restore(await original.Snapshot().ConfigureAwait(false)).ConfigureAwait(false);
            restored.Update(1);

            Assert.AreEqual(3, UpdatePressure(restored, pressure, 1), 0.000001);
            Assert.AreEqual(1f, restored.CurrentValue);
            Assert.AreEqual(ControllerState.Emergency, restored.State);
            Assert.IsFalse(restored.EmergencyBrakingPushButton);
            Assert.IsFalse(restored.TCSEmergencyBraking);
        }

        [TestMethod]
        public void BellToggleChangesManualStateWhileAutomaticBellIsRinging()
        {
            MSTSLocomotive locomotive = (MSTSLocomotive)RuntimeHelpers.GetUninitializedObject(typeof(MSTSDieselLocomotive));
            locomotive.Bell = true;
            locomotive.ManualBell = false;
            MSTSLocomotiveViewer locomotiveViewer = (MSTSLocomotiveViewer)RuntimeHelpers.GetUninitializedObject(typeof(MSTSLocomotiveViewer));
            SetField(locomotiveViewer, "locomotive", locomotive);
            Simulation simulator = (Simulation)RuntimeHelpers.GetUninitializedObject(typeof(Simulation));
            simulator.Log = new CommandLog(simulator);
            Viewer viewer = (Viewer)RuntimeHelpers.GetUninitializedObject(typeof(Viewer));
            SetField(viewer, "<Simulator>k__BackingField", simulator);
            FieldInfo viewerField = typeof(TrainCarViewer).GetField("Viewer", BindingFlags.Static | BindingFlags.NonPublic);
            object previousViewer = viewerField.GetValue(null);
            MSTSLocomotive previousReceiver = BellCommand.Receiver;
            try
            {
                viewerField.SetValue(null, viewer);
                BellCommand.Receiver = locomotive;

                _ = typeof(MSTSLocomotiveViewer).GetMethod("BellToggleCommand", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(locomotiveViewer, null);
                Assert.IsTrue(locomotive.ManualBell);
                Assert.IsTrue(locomotive.Bell);
                typeof(MSTSLocomotiveViewer).GetMethod("BellToggleCommand", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(locomotiveViewer, null);
                Assert.IsFalse(locomotive.ManualBell);
                Assert.HasCount(2, simulator.Log.CommandList);
            }
            finally
            {
                viewerField.SetValue(null, previousViewer);
                BellCommand.Receiver = previousReceiver;
            }
        }

        private static UserCommandController<UserCommand> Bind(RailDriverInputGameComponent component)
        {
            EnumArray<byte, UserCommand> mappings = new EnumArray<byte, UserCommand>(byte.MaxValue);
            mappings[UserCommand.GameExternalCabController] = 14;
            mappings[UserCommand.ControlHorn] = 42;
            UserCommandController<UserCommand> controller = new UserCommandController<UserCommand>();
            new RailDriverInputHandler<UserCommand>().Initialize(mappings, component, controller);
            return controller;
        }

        private static Action<float> BindTrainBrake(ScriptedBrakeController brake)
        {
            RailDriverInputGameComponent component = (RailDriverInputGameComponent)RuntimeHelpers.GetUninitializedObject(typeof(RailDriverInputGameComponent));
            UserCommandController<UserCommand> controller = Bind(component);
            MSTSLocomotive locomotive = (MSTSLocomotive)RuntimeHelpers.GetUninitializedObject(typeof(MSTSDieselLocomotive));
            SetField(locomotive, "<TrainBrakeController>k__BackingField", brake, typeof(MSTSLocomotive));
            MSTSLocomotiveViewer viewer = (MSTSLocomotiveViewer)RuntimeHelpers.GetUninitializedObject(typeof(MSTSLocomotiveViewer));
            SetField(viewer, "locomotive", locomotive);
            Action<UserCommandArgs, GameTime> command = typeof(MSTSLocomotiveViewer)
                .GetMethod("TrainBrakeHandleCommand", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate<Action<UserCommandArgs, GameTime>>(viewer);
            controller.AddEvent(AnalogUserCommand.TrainBrake, command);
            Action<RailDriverHandleEventType, GameTime, UserCommandArgs> input = (Action<RailDriverHandleEventType, GameTime, UserCommandArgs>)typeof(RailDriverInputGameComponent)
                .GetField("handleActionHandler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
            return percent =>
            {
                UserCommandArgs<float> args = new UserCommandArgs<float>();
                typeof(UserCommandArgs<float>).GetProperty(nameof(UserCommandArgs<float>.Value)).SetValue(args, percent);
                input(RailDriverHandleEventType.TrainBrake, new GameTime(), args);
            };
        }

        private static ScriptedBrakeController CreateBrakeController(bool pbl2, bool includeEmergency = true)
        {
            ScriptedBrakeController controller = new ScriptedBrakeController(null) { MaximumValue = 1, StepSize = 0.1f };
            SetField(controller, "simulator", RuntimeHelpers.GetUninitializedObject(typeof(Simulation)));
            List<IControllerNotch> notches = new List<IControllerNotch>
            {
                new MSTSNotch(0, false, (int)ControllerState.Release),
                new MSTSNotch(0.5f, false, (int)ControllerState.Lap),
                new MSTSNotch(0.7f, false, (int)(pbl2 ? ControllerState.Apply : ControllerState.FullServ)),
            };
            if (includeEmergency)
                notches.Add(new MSTSNotch(1, false, (int)ControllerState.Emergency));
            BrakeController script = pbl2 ? new PBL2BrakeController() : new MSTSBrakeController();
            script.CurrentValue = () => controller.CurrentValue;
            script.MinimumValue = () => controller.MinimumValue;
            script.MaximumValue = () => controller.MaximumValue;
            script.StepSize = () => controller.StepSize;
            script.SetCurrentValue = value => controller.CurrentValue = value;
            script.SetUpdateValue = value => controller.UpdateValue = value;
            script.Notches = () => notches;
            script.EmergencyBrakingPushButton = () => controller.EmergencyBrakingPushButton;
            script.TCSEmergencyBraking = () => controller.TCSEmergencyBraking;
            script.TCSFullServiceBraking = () => false;
            script.QuickReleaseButtonPressed = () => false;
            script.OverchargeButtonPressed = () => false;
            script.MainReservoirPressureBar = () => 8;
            script.MaxPressureBar = () => 5;
            script.MaxOverchargePressureBar = () => 5.4f;
            script.ReleaseRateBarpS = () => 0.5f;
            script.QuickReleaseRateBarpS = () => 1;
            script.OverchargeEliminationRateBarpS = () => 0.0025f;
            script.SlowApplicationRateBarpS = () => 0.125f;
            script.ApplyRateBarpS = () => 0.25f;
            script.EmergencyRateBarpS = () => 1;
            script.FullServReductionBar = () => 1.5f;
            script.MinReductionBar = () => 0.5f;
            script.CruiseControlBrakeDemand = () => 0;
            script.Initialize();
            SetField(controller, "script", script);
            SetField(controller, "notches", notches);
            return controller;
        }

        private static double UpdatePressure(ScriptedBrakeController controller, double pressureBar, double elapsedSeconds)
        {
            return Pressure.Atmospheric.FromPSI(controller.UpdatePressure(Pressure.Atmospheric.ToPSI(pressureBar), 0, elapsedSeconds).pressurePSI);
        }

        private static void SetField(object target, string name, object value, Type declaringType = null)
        {
            (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}