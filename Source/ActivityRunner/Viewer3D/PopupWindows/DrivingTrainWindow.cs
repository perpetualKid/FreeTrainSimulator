using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class DrivingTrainWindow : WindowBase
    {
        private const int monoLeadColumnWidth = 40;
        private const int normalLeadColumnWidth = 64;

        private const int monoColumnWidth = 48;
        private const int normalColumnWidth = 70;

        private enum WindowMode
        {
            Normal,
            NormalMono,
        }

        private enum DetailInfo
        {
            Time,
            Replay,
            Speed,
            Gradient,
            Odometer,
            Direction,
            Throttle,
            CylinderCocks,
            Sander,
            TrainBrake,
            TrainBrakeEQStatus,
            TrainBrakeStatus,
            TrainBrakeFirstCar,
            TrainBrakeLastCar,
            Retainer,
            EngineBrake,
            EngineBC,
            DynamicBrake,
            SteamUsage,
            SteamBoilerPressure,
            SteamBoilerWaterGlass,
            SteamBoilerWaterLevel,
            SteamFireMass,
            SteamFuelLevelCoal,
            SteamFuelLevelWater,
            DieselEngineRunning,
            BatterySwitch,
            MasterKey,
            DieselTractionCutOffRelay,
            ElectricTrainSupply,
            PowerSupply,
            DieselGear,
            Pantographs,
            CircuitBreaker,
            EotDevice,
            DieselDpu,
            AutoPilot,
            SteamAiFireMan,
            SteamGrateLimit,
            WheelSlip,
            DoorOpen,
            Derailment,
            CruiseControl,
            CruiseControlTarget,
            CruiseControlMaxAccel,
        }

        private readonly UserSettings settings;
        private readonly UserCommandController<UserCommand> userCommandController;
        private WindowMode windowMode;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label labelExpandMono;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly EnumArray<ControlLayout, DetailInfo> groupDetails = new EnumArray<ControlLayout, DetailInfo>();
        private readonly EnumArray<bool, DetailInfo> dataAvailable = new EnumArray<bool, DetailInfo>();

        private long wheelSlipTimeout;
        private long doorOpenTimeout;
        private long derailTimeout;

        private int additionalLines;
        private const int constantLines = 11; //need to be updated if additional lines required
        private const int separatorLines = 4;
        private int additonalSeparators;

        private string directionKeyInput;
        private string throttleKeyInput;
        private string cylinderCocksInput;
        private string sanderInput;
        private string trainBrakeInput;
        private string engineBrakeInput;
        private string dynamicBrakeInput;
        private string gearKeyInput;
        private string pantographKeyInput;

        public DrivingTrainWindow(WindowManager owner, Point relativeLocation, UserSettings settings, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Driving Info"), relativeLocation, new Point(200, 220), catalog)
        {
            userCommandController = viewer.UserCommandController;
            this.settings = settings;
            _ = EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DrivingTrainWindow], out windowMode);

            Resize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            MSTSLocomotive playerLocomotive = Simulator.Instance.PlayerLocomotive;
            layout = base.Layout(layout, headerScaling).AddLayoutOffset(0);
            ControlLayout line = layout.AddLayoutHorizontal();
            line.HorizontalChildAlignment = HorizontalAlignment.Right;
            line.VerticalChildAlignment = VerticalAlignment.Top;
            line.Add(labelExpandMono = new Label(this, Owner.TextFontDefault.Height, Owner.TextFontDefault.Height, windowMode == WindowMode.NormalMono ? FormatStrings.Markers.ArrowRight : FormatStrings.Markers.ArrowLeft, HorizontalAlignment.Center, Color.Yellow));
            labelExpandMono.OnClick += LabelExpandMono_OnClick;
            layout = layout.AddLayoutVertical();

            int columnWidth;
            int leadColumnWidth;
            foreach (DetailInfo detailInfo in EnumExtension.GetValues<DetailInfo>())
            {
                groupDetails[detailInfo] = null;
            }

            void AddDetailLine(DetailInfo detail, string caption, System.Drawing.Font font, HorizontalAlignment alignment = HorizontalAlignment.Left)
            {
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, 0, 0, (int)(Owner.DpiScaling * 12), font.Height, null, HorizontalAlignment.Center, font, Color.Yellow));
                line.Add(new Label(this, 0, 0, leadColumnWidth, font.Height, caption, alignment, font, Color.White));
                line.Add(new Label(this, 0, 0, (int)(Owner.DpiScaling * 12), font.Height, null, HorizontalAlignment.Center, font, Color.Yellow));
                line.Add(new Label(this, columnWidth, font.Height, null, font));

                groupDetails[detail] = line;
            }

            System.Drawing.Font font;
            bool shortMode = false;
            if (windowMode == WindowMode.Normal)
            {
                leadColumnWidth = (int)(Owner.DpiScaling * normalLeadColumnWidth);
                columnWidth = (int)(Owner.DpiScaling * normalColumnWidth * 2);
                font = Owner.TextFontDefault;
            }
            else
            {
                leadColumnWidth = (int)(Owner.DpiScaling * monoLeadColumnWidth);
                columnWidth = (int)(Owner.DpiScaling * monoColumnWidth * 2);
                font = Owner.TextFontMonoDefault;
                shortMode = true;
            }

            AddDetailLine(DetailInfo.Time, shortMode ? FourCharAcronym.Time.GetLocalizedDescription() : Catalog.GetString("Time"), font);
            if (dataAvailable[DetailInfo.Replay])
                AddDetailLine(DetailInfo.Replay, shortMode ? FourCharAcronym.Replay.GetLocalizedDescription() : Catalog.GetString("Replay"), font);
            AddDetailLine(DetailInfo.Speed, shortMode ? FourCharAcronym.Speed.GetLocalizedDescription() : Catalog.GetString("Speed"), font);
            AddDetailLine(DetailInfo.Gradient, shortMode ? FourCharAcronym.Gradient.GetLocalizedDescription() : Catalog.GetString("Gradient"), font);
            AddDetailLine(DetailInfo.Odometer, shortMode ? FourCharAcronym.Odometer.GetLocalizedDescription() : Catalog.GetString("Odometer"), font);
            layout.AddHorizontalSeparator(true);
            AddDetailLine(DetailInfo.Direction, playerLocomotive.EngineType == EngineType.Steam ?
                shortMode ? FourCharAcronym.Reverser.GetLocalizedDescription() : Catalog.GetString("Reverser") :
                shortMode ? FourCharAcronym.Direction.GetLocalizedDescription() : Catalog.GetString("Direction"), font);
            AddDetailLine(DetailInfo.Throttle, playerLocomotive.EngineType == EngineType.Steam ?
                shortMode ? FourCharAcronym.Regulator.GetLocalizedDescription() : Catalog.GetString("Regulator") :
                shortMode ? FourCharAcronym.Throttle.GetLocalizedDescription() : Catalog.GetString("Throttle"), font);
            if (playerLocomotive.EngineType == EngineType.Steam)
            {
                AddDetailLine(DetailInfo.CylinderCocks, shortMode ? FourCharAcronym.CylinderCocks.GetLocalizedDescription() : Catalog.GetString("Cyl Cocks"), font);
            }
            AddDetailLine(DetailInfo.Sander, shortMode ? FourCharAcronym.Sander.GetLocalizedDescription() : Catalog.GetString("Sander"), font);
            layout.AddHorizontalSeparator(true);
            AddDetailLine(DetailInfo.TrainBrake, shortMode ? FourCharAcronym.TrainBrake.GetLocalizedDescription() : Catalog.GetString("Train Brk"), font);
            (groupDetails[DetailInfo.TrainBrake].Controls[3] as Label).TextColor = Color.Cyan;
            if (dataAvailable[DetailInfo.TrainBrakeEQStatus])
            {
                AddDetailLine(DetailInfo.TrainBrakeEQStatus, shortMode ? FourCharAcronym.EQReservoir.GetLocalizedDescription() : Catalog.GetString("EQ Res"), font);
                (groupDetails[DetailInfo.TrainBrakeEQStatus].Controls[1] as Label).Alignment = HorizontalAlignment.Right;
                if (dataAvailable[DetailInfo.TrainBrakeFirstCar])
                {
                    AddDetailLine(DetailInfo.TrainBrakeFirstCar, shortMode ? FourCharAcronym.FirstTrainCar.GetLocalizedDescription() : Catalog.GetString("1st car"), font);
                    (groupDetails[DetailInfo.TrainBrakeFirstCar].Controls[1] as Label).Alignment = HorizontalAlignment.Right;
                }
                AddDetailLine(DetailInfo.TrainBrakeLastCar, shortMode ? FourCharAcronym.EndOfTrainCar.GetLocalizedDescription() : Catalog.GetString("EOT car"), font);
                (groupDetails[DetailInfo.TrainBrakeLastCar].Controls[1] as Label).Alignment = HorizontalAlignment.Right;
            }
            else
            {
                groupDetails[DetailInfo.TrainBrakeEQStatus] = null;
                AddDetailLine(DetailInfo.TrainBrakeStatus, string.Empty, font);
            }
            if (dataAvailable[DetailInfo.Retainer])
            {
                AddDetailLine(DetailInfo.Retainer, shortMode ? FourCharAcronym.Retainer.GetLocalizedDescription() : Catalog.GetString("Retainers"), font);
            }
            AddDetailLine(DetailInfo.EngineBrake, shortMode ? FourCharAcronym.EngineBrake.GetLocalizedDescription() : Catalog.GetString("Eng Brk"), font);
            (groupDetails[DetailInfo.EngineBrake].Controls[3] as Label).TextColor = Color.Cyan;
            if (dataAvailable[DetailInfo.EngineBC])
            {
                AddDetailLine(DetailInfo.EngineBC, (shortMode ? FourCharAcronym.BrakeCylinder.GetLocalizedDescription() : Catalog.GetString("Brk Cyl")), font);
                (groupDetails[DetailInfo.EngineBC].Controls[1] as Label).Alignment = HorizontalAlignment.Right;
            }
            if (dataAvailable[DetailInfo.DynamicBrake])
            {
                AddDetailLine(DetailInfo.DynamicBrake, shortMode ? FourCharAcronym.DynamicBrake.GetLocalizedDescription() : Catalog.GetString("Dyn Brk"), font);
                (groupDetails[DetailInfo.DynamicBrake].Controls[3] as Label).TextColor = Color.Cyan;
            }
            layout.AddHorizontalSeparator(true);
            switch (playerLocomotive.EngineType)
            {
                case EngineType.Steam:
                    AddDetailLine(DetailInfo.SteamUsage, shortMode ? FourCharAcronym.SteamUsage.GetLocalizedDescription() : Catalog.GetString("Steam use"), font);
                    AddDetailLine(DetailInfo.SteamBoilerPressure, shortMode ? FourCharAcronym.BoilerPressure.GetLocalizedDescription() : Catalog.GetString("Boil Pres"), font);
                    AddDetailLine(DetailInfo.SteamBoilerWaterGlass, shortMode ? FourCharAcronym.BoilerWaterGlass.GetLocalizedDescription() : Catalog.GetString("Wtr Glass"), font);
                    if (dataAvailable[DetailInfo.SteamBoilerWaterLevel])
                    {
                        AddDetailLine(DetailInfo.SteamBoilerWaterLevel, shortMode ? FourCharAcronym.BoilerWaterLevel.GetLocalizedDescription() : Catalog.GetString("Wtr Level"), font);
                        AddDetailLine(DetailInfo.SteamFireMass, shortMode ? FourCharAcronym.FireMass.GetLocalizedDescription() : Catalog.GetString("Fire Mass"), font);
                    }
                    AddDetailLine(DetailInfo.SteamFuelLevelCoal, shortMode ? FourCharAcronym.FuelLevel.GetLocalizedDescription() : Catalog.GetString("Fuel Lvl"), font);
                    AddDetailLine(DetailInfo.SteamFuelLevelWater, shortMode ? FourCharAcronym.FuelLevel.GetLocalizedDescription() : Catalog.GetString("Fuel Lvl"), font);
                    break;
                case EngineType.Diesel:
                    AddDetailLine(DetailInfo.DieselEngineRunning, shortMode ? FourCharAcronym.Engine.GetLocalizedDescription() : Catalog.GetString("Engine"), font);
                    if (dataAvailable[DetailInfo.DieselGear])
                        AddDetailLine(DetailInfo.DieselGear, shortMode ? FourCharAcronym.Gear.GetLocalizedDescription() : Catalog.GetString("Fix Gear"), font);
                    AddDetailLine(DetailInfo.BatterySwitch, shortMode ? FourCharAcronym.BatterySwitch.GetLocalizedDescription() : Catalog.GetString("Batt Sw"), font);
                    AddDetailLine(DetailInfo.MasterKey, shortMode ? FourCharAcronym.MasterKey.GetLocalizedDescription() : Catalog.GetString("Mstr Key"), font);
                    AddDetailLine(DetailInfo.DieselTractionCutOffRelay, shortMode ? FourCharAcronym.TractionCutOffRelay.GetLocalizedDescription() : Catalog.GetString("Trac Cut"), font);
                    AddDetailLine(DetailInfo.ElectricTrainSupply, shortMode ? FourCharAcronym.ElectricTrainSupply.GetLocalizedDescription() : Catalog.GetString("Elec Sup"), font);
                    AddDetailLine(DetailInfo.PowerSupply, shortMode ? FourCharAcronym.Power.GetLocalizedDescription() : Catalog.GetString("Power"), font);
                    break;
                case EngineType.Electric:
                    AddDetailLine(DetailInfo.Pantographs, shortMode ? FourCharAcronym.Pantographs.GetLocalizedDescription() : Catalog.GetString("Panto"), font);
                    AddDetailLine(DetailInfo.BatterySwitch, shortMode ? FourCharAcronym.BatterySwitch.GetLocalizedDescription() : Catalog.GetString("Batt Sw"), font);
                    AddDetailLine(DetailInfo.MasterKey, shortMode ? FourCharAcronym.MasterKey.GetLocalizedDescription() : Catalog.GetString("Mstr Key"), font);
                    AddDetailLine(DetailInfo.CircuitBreaker, shortMode ? FourCharAcronym.CircuitBreaker.GetLocalizedDescription() : Catalog.GetString("Cir Break"), font);
                    AddDetailLine(DetailInfo.ElectricTrainSupply, shortMode ? FourCharAcronym.ElectricTrainSupply.GetLocalizedDescription() : Catalog.GetString("Elec Sup"), font);
                    AddDetailLine(DetailInfo.PowerSupply, shortMode ? FourCharAcronym.Power.GetLocalizedDescription() : Catalog.GetString("Power"), font);
                    break;
            }
            layout.AddHorizontalSeparator(true);
            if (dataAvailable[DetailInfo.CruiseControl])
            {
                AddDetailLine(DetailInfo.CruiseControl, shortMode ? FourCharAcronym.CruiseControl.GetLocalizedDescription() : Catalog.GetString("CC Stat"), font);
                (groupDetails[DetailInfo.CruiseControl].Controls[3] as Label).TextColor = Color.Cyan;
                if (dataAvailable[DetailInfo.CruiseControlTarget])
                {
                    AddDetailLine(DetailInfo.CruiseControlTarget, shortMode ? FourCharAcronym.CruiseControlTarget.GetLocalizedDescription() : Catalog.GetString("CC Targ"), font);
                    (groupDetails[DetailInfo.CruiseControlTarget].Controls[3] as Label).TextColor = Color.Cyan;
                    AddDetailLine(DetailInfo.CruiseControlMaxAccel, shortMode ? FourCharAcronym.CruiseControlMaxAcceleration.GetLocalizedDescription() : Catalog.GetString("Max Accl"), font);
                    (groupDetails[DetailInfo.CruiseControlMaxAccel].Controls[3] as Label).TextColor = Color.Cyan;
                }
                layout.AddHorizontalSeparator(true);
            }
            if (dataAvailable[DetailInfo.EotDevice])
            {
                AddDetailLine(DetailInfo.EotDevice, shortMode ? FourCharAcronym.EotDevice.GetLocalizedDescription() : Catalog.GetString("EOT Dev"), font);
                layout.AddHorizontalSeparator(true);
            }
            if (dataAvailable[DetailInfo.DieselDpu])
            {
                AddDetailLine(DetailInfo.DieselDpu, shortMode ? FourCharAcronym.Locomotives.GetLocalizedDescription() : Catalog.GetString("Locos"), font);
                layout.AddHorizontalSeparator(true);
            }
            AddDetailLine(DetailInfo.AutoPilot, shortMode ? FourCharAcronym.AutoPilot.GetLocalizedDescription() : Catalog.GetString("AutoPilot"), font);
            if (dataAvailable[DetailInfo.SteamAiFireMan])
                AddDetailLine(DetailInfo.SteamAiFireMan, shortMode ? FourCharAcronym.AiFireman.GetLocalizedDescription() : Catalog.GetString("AI Fire"), font);
            if (dataAvailable[DetailInfo.SteamGrateLimit])
                AddDetailLine(DetailInfo.SteamGrateLimit, shortMode ? FourCharAcronym.GrateLimit.GetLocalizedDescription() : Catalog.GetString("Grate Lim"), font);
            if (dataAvailable[DetailInfo.Derailment])
                AddDetailLine(DetailInfo.Derailment, shortMode ? FourCharAcronym.Derailment.GetLocalizedDescription() : Catalog.GetString("Derail"), font);
            if (dataAvailable[DetailInfo.WheelSlip])
                AddDetailLine(DetailInfo.WheelSlip, shortMode ? FourCharAcronym.Wheel.GetLocalizedDescription() : Catalog.GetString("Wheel Slip"), font);
            if (dataAvailable[DetailInfo.DoorOpen])
                AddDetailLine(DetailInfo.DoorOpen, shortMode ? FourCharAcronym.DoorsOpen.GetLocalizedDescription() : Catalog.GetString("Doors"), font);
            return layout;
        }

        private void LabelExpandMono_OnClick(object sender, MouseClickEventArgs e)
        {
            windowMode = windowMode.Next();
            Resize();
        }

        public override bool Open()
        {
            bool result = base.Open();
            if (result)
            {
                userCommandController.AddEvent(UserCommand.ControlReverserForward, KeyEventType.KeyDown, DirectionCommandForward, true);
                userCommandController.AddEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyDown, DirectionCommandBackward, true);
                userCommandController.AddEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyDown, ThrottleCommandIncrease, true);
                userCommandController.AddEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyDown, ThrottleCommandDecrease, true);
                userCommandController.AddEvent(UserCommand.ControlCylinderCocks, KeyEventType.KeyDown, CylinderCocksCommand, true);
                userCommandController.AddEvent(UserCommand.ControlSander, KeyEventType.KeyDown, SanderCommand, true);
                userCommandController.AddEvent(UserCommand.ControlSanderToggle, KeyEventType.KeyDown, SanderCommand, true);
                userCommandController.AddEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyDown, TrainBrakeCommandIncrease, true);
                userCommandController.AddEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyDown, TrainBrakeCommandDecrease, true);
                userCommandController.AddEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyDown, EngineBrakeCommandIncrease, true);
                userCommandController.AddEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyDown, EngineBrakeCommandDecrease, true);
                userCommandController.AddEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyDown, DynamicBrakeCommandIncrease, true);
                userCommandController.AddEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyDown, DynamicBrakeCommandDecrease, true);
                userCommandController.AddEvent(UserCommand.ControlGearUp, KeyEventType.KeyDown, GearCommandUp, true);
                userCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, GearCommandDown, true);
                userCommandController.AddEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, Pantograph1Command, true);
                userCommandController.AddEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, Pantograph2Command, true);
                userCommandController.AddEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, Pantograph3Command, true);
                userCommandController.AddEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, Pantograph4Command, true);

                userCommandController.AddEvent(UserCommand.DisplayTrainDrivingWindow, KeyEventType.KeyPressed, TabAction, true);
            }
            return result;
        }

        public override bool Close()
        {
            userCommandController.RemoveEvent(UserCommand.ControlReverserForward, KeyEventType.KeyDown, DirectionCommandForward);
            userCommandController.RemoveEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyDown, DirectionCommandBackward);
            userCommandController.RemoveEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyDown, ThrottleCommandIncrease);
            userCommandController.RemoveEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyDown, ThrottleCommandDecrease);
            userCommandController.RemoveEvent(UserCommand.ControlCylinderCocks, KeyEventType.KeyDown, CylinderCocksCommand);
            userCommandController.RemoveEvent(UserCommand.ControlSander, KeyEventType.KeyDown, SanderCommand);
            userCommandController.RemoveEvent(UserCommand.ControlSanderToggle, KeyEventType.KeyDown, SanderCommand);
            userCommandController.RemoveEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyDown, TrainBrakeCommandIncrease);
            userCommandController.RemoveEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyDown, TrainBrakeCommandDecrease);
            userCommandController.RemoveEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyDown, EngineBrakeCommandIncrease);
            userCommandController.RemoveEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyDown, EngineBrakeCommandDecrease);
            userCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyDown, DynamicBrakeCommandIncrease);
            userCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyDown, DynamicBrakeCommandDecrease);
            userCommandController.RemoveEvent(UserCommand.ControlGearUp, KeyEventType.KeyDown, GearCommandUp);
            userCommandController.RemoveEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, GearCommandDown);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, Pantograph1Command);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, Pantograph2Command);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, Pantograph3Command);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, Pantograph4Command);

            userCommandController.RemoveEvent(UserCommand.DisplayTrainDrivingWindow, KeyEventType.KeyPressed, TabAction);
            return base.Close();
        }

        private void TabAction(UserCommandArgs args)
        {
            if (args is ModifiableKeyCommandArgs keyCommandArgs && (keyCommandArgs.AdditionalModifiers & settings.Input.WindowTabCommandModifier) == settings.Input.WindowTabCommandModifier)
            {
                windowMode = windowMode.Next();
                Resize();
            }
        }

        private void Resize()
        {
            // since TextFontDefault.Height is already scaled, need to divide by scaling factor, since base Resize would apply scaling factor again
            int height = 32 + (int)((additionalLines + constantLines) * Owner.TextFontDefault.Height / Owner.DpiScaling) + ((separatorLines + additonalSeparators) * 5) /* #separators * separator height */;
            Point size = windowMode switch
            {
                WindowMode.Normal => new Point(normalLeadColumnWidth + 2 * normalColumnWidth + 36, height),
                WindowMode.NormalMono => new Point(monoLeadColumnWidth + 2 * monoColumnWidth + 36, height),
                _ => throw new InvalidOperationException(),
            };

            Resize(size);

            settings.PopupSettings[ViewerWindowType.DrivingTrainWindow] = windowMode.ToString();
        }

        //we need to keep a delegate reference to be able to unsubscribe, so those are just forwarders
#pragma warning disable IDE0022 // Use block body for methods
        private void DirectionCommandForward() => DirectionCommand(Direction.Forward);
        private void DirectionCommandBackward() => DirectionCommand(Direction.Backward);
        private void ThrottleCommandIncrease() => ThrottleCommand(true);
        private void ThrottleCommandDecrease() => ThrottleCommand(false);
        private void TrainBrakeCommandIncrease(UserCommandArgs userCommandArgs) => TrainBrakeCommand(true);
        private void TrainBrakeCommandDecrease(UserCommandArgs userCommandArgs) => TrainBrakeCommand(false);
        private void EngineBrakeCommandIncrease(UserCommandArgs userCommandArgs) => EngineBrakeCommand(true);
        private void EngineBrakeCommandDecrease(UserCommandArgs userCommandArgs) => EngineBrakeCommand(false);
        private void DynamicBrakeCommandIncrease(UserCommandArgs userCommandArgs) => DynamicBrakeCommand(true);
        private void DynamicBrakeCommandDecrease(UserCommandArgs userCommandArgs) => DynamicBrakeCommand(false);
        private void GearCommandDown(UserCommandArgs userCommandArgs) => GearCommand(true);
        private void GearCommandUp(UserCommandArgs userCommandArgs) => GearCommand(false);
        private void Pantograph1Command(UserCommandArgs userCommandArgs) => PantographCommand(1);
        private void Pantograph2Command(UserCommandArgs userCommandArgs) => PantographCommand(2);
        private void Pantograph3Command(UserCommandArgs userCommandArgs) => PantographCommand(3);
        private void Pantograph4Command(UserCommandArgs userCommandArgs) => PantographCommand(4);
#pragma warning restore IDE0022 // Use block body for methods

        private void DirectionCommand(Direction direction)
        {
            TrainCar locomotive = Simulator.Instance.PlayerLocomotive;
            directionKeyInput = (locomotive.EngineType != EngineType.Steam && (locomotive.ThrottlePercent >= 1 || locomotive.AbsSpeedMpS > 1))
                || (locomotive.EngineType == EngineType.Steam && locomotive is MSTSSteamLocomotive mstsSteamLocomotive && mstsSteamLocomotive.CutoffController.MaximumValue == Math.Abs(locomotive.Train.MUReverserPercent / 100))
                ? FormatStrings.Markers.Block
                : direction == Direction.Forward ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown;
        }

        private void ThrottleCommand(bool increase)
        {
            TrainCar locomotive = Simulator.Instance.PlayerLocomotive;
            throttleKeyInput = locomotive.DynamicBrakePercent < 1 && (increase && (locomotive as MSTSLocomotive).ThrottleController.MaximumValue == locomotive.ThrottlePercent / 100)
                || (!increase && locomotive.ThrottlePercent == 0)
                ? FormatStrings.Markers.Block
                : locomotive.DynamicBrakePercent > -1
                    ? FormatStrings.Markers.BlockHorizontal
                    : increase ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown;
        }

        private void CylinderCocksCommand()
        {
            if (Simulator.Instance.PlayerLocomotive is MSTSSteamLocomotive)
                cylinderCocksInput = FormatStrings.Markers.ArrowRight;
        }

        private void SanderCommand()
        {
            sanderInput = FormatStrings.Markers.ArrowDown;
        }

        private void TrainBrakeCommand(bool increase)
        {
            trainBrakeInput = increase ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown;
        }

        private void EngineBrakeCommand(bool increase)
        {
            engineBrakeInput = increase ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown;
        }

        private void DynamicBrakeCommand(bool increase)
        {
            dynamicBrakeInput = increase ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown;
        }

        private void GearCommand(bool down)
        {
            gearKeyInput = down ? FormatStrings.Markers.ArrowDown : FormatStrings.Markers.ArrowUp;
        }

        private void PantographCommand(int pantograph)
        {
            MSTSLocomotive locomotive = Simulator.Instance.PlayerLocomotive;
            pantographKeyInput = locomotive.Pantographs[pantograph].State is PantographState.Up or PantographState.Raising ? FormatStrings.Markers.ArrowUp : FormatStrings.Markers.ArrowDown;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
                if (UpdateDrivingInformation())
                    Resize();
        }

        private bool UpdateDrivingInformation()
        {
            bool result = false;
            int linesAdded = 0;
            int separatorsAdded = 0;
            // Client and server may have a time difference.
            MSTSLocomotive playerLocomotive = Simulator.Instance.PlayerLocomotive;
            if (groupDetails[DetailInfo.Time]?.Controls[3] is Label timeLabel)
            {
                timeLabel.Text = MultiPlayerManager.MultiplayerState == MultiplayerState.Client ? $"{FormatStrings.FormatTime(Simulator.Instance.ClockTime + MultiPlayerManager.Instance().ServerTimeDifference)}" : $"{FormatStrings.FormatTime(Simulator.Instance.ClockTime)}";
            }
            // Replay info
            result |= dataAvailable[DetailInfo.Replay] != (dataAvailable[DetailInfo.Replay] = Simulator.Instance.IsReplaying);
            linesAdded += dataAvailable[DetailInfo.Replay] ? 1 : 0;
            if (dataAvailable[DetailInfo.Replay] && groupDetails[DetailInfo.Replay]?.Controls[3] is Label replayLabel)
                replayLabel.Text = $"{FormatStrings.FormatTime(Simulator.Instance.Log.ReplayEndsAt - Simulator.Instance.ClockTime)}";
            // Speed info
            if (groupDetails[DetailInfo.Speed]?.Controls[3] is Label speedLabel)
            {
                speedLabel.Text = $"{FormatStrings.FormatSpeedDisplay(playerLocomotive.SpeedMpS, Simulator.Instance.MetricUnits)}";
                speedLabel.TextColor = ColorCoding.SpeedingColor(playerLocomotive.AbsSpeedMpS, playerLocomotive.Train.MaxTrainSpeedAllowed);
            }
            // Gradient info
            if (groupDetails[DetailInfo.Gradient]?.Controls[3] is Label gradientLabel)
            {
                double gradient = Math.Round(playerLocomotive.CurrentElevationPercent, 1);
                if (gradient == 0) // to avoid negative zero string output if gradient after rounding is -0.0
                    gradient = 0;
                gradientLabel.Text = $"{gradient:F1}% {(gradient > 0 ? FormatStrings.Markers.Ascent : gradient < 0 ? FormatStrings.Markers.Descent : string.Empty)}";
                gradientLabel.TextColor = (gradient > 0 ? Color.Yellow : gradient < 0 ? Color.LightSkyBlue : Color.White);
            }
            // Odometer
            if (groupDetails[DetailInfo.Odometer]?.Controls[3] is Label odometerLabel)
            {
                odometerLabel.Text = Simulator.Instance.Settings.OdometerShortDistanceMode ? 
                    FormatStrings.FormatShortDistanceDisplay(playerLocomotive.OdometerM, Simulator.Instance.MetricUnits) : 
                    FormatStrings.FormatDistanceDisplay(playerLocomotive.OdometerM, Simulator.Instance.MetricUnits);
            }
            // Direction
            if (groupDetails[DetailInfo.Direction]?.Controls[3] is Label directionLabel)
            {
                float reverserPercent = Math.Abs(playerLocomotive.Train.MUReverserPercent);
                directionLabel.Text = $"{(reverserPercent != 100 ? $"{reverserPercent:F0}% " : string.Empty)}{playerLocomotive.Direction.GetLocalizedDescription()}";
                (groupDetails[DetailInfo.Direction].Controls[0] as Label).Text = directionKeyInput;
                (groupDetails[DetailInfo.Direction].Controls[2] as Label).Text = directionKeyInput;
                directionKeyInput = null;
            }
            // Throttle
            if (groupDetails[DetailInfo.Throttle]?.Controls[3] is Label throttleLabel)
            {
                throttleLabel.Text = $"{Math.Round(playerLocomotive.ThrottlePercent):F0}% {(playerLocomotive is MSTSDieselLocomotive && playerLocomotive.Train.DistributedPowerMode == DistributedPowerMode.Traction ? $"({Math.Round(playerLocomotive.Train.DPThrottlePercent):F0}%)" : string.Empty)}";
                (groupDetails[DetailInfo.Throttle].Controls[0] as Label).Text = throttleKeyInput;
                (groupDetails[DetailInfo.Throttle].Controls[2] as Label).Text = throttleKeyInput;
                throttleKeyInput = null;
            }
            // Cylinder Cocks
            if (groupDetails[DetailInfo.CylinderCocks]?.Controls[3] is Label cocksLabel && playerLocomotive is MSTSSteamLocomotive mstsSteamLocomotive)
            {
                cocksLabel.Text = $"{(mstsSteamLocomotive.CylinderCocksAreOpen ? Catalog.GetString("Open") : Catalog.GetString("Closed"))}";
                cocksLabel.TextColor = mstsSteamLocomotive.CylinderCocksAreOpen ? Color.Orange : Color.White;
                (groupDetails[DetailInfo.CylinderCocks].Controls[0] as Label).Text = cylinderCocksInput;
                (groupDetails[DetailInfo.CylinderCocks].Controls[2] as Label).Text = cylinderCocksInput;
                cylinderCocksInput = null;
            }
            linesAdded += playerLocomotive is MSTSSteamLocomotive ? 1 : 0;
            // Sander
            if (groupDetails[DetailInfo.Sander]?.Controls[3] is Label sanderLabel)
            {
                bool sanderBlocked = playerLocomotive.AbsSpeedMpS > playerLocomotive.SanderSpeedOfMpS;
                sanderLabel.Text = $"{(playerLocomotive.Sander ? sanderBlocked ? Catalog.GetString("Blocked") : Catalog.GetString("On") : Catalog.GetString("Off"))}";
                sanderLabel.TextColor = playerLocomotive.Sander ? sanderBlocked ? Color.OrangeRed : Color.Orange : Color.White;
                (groupDetails[DetailInfo.Sander].Controls[0] as Label).Text = sanderInput;
                (groupDetails[DetailInfo.Sander].Controls[2] as Label).Text = sanderInput;
                sanderInput = null;
            }
            // Train Brake
            if (groupDetails[DetailInfo.TrainBrake]?.Controls[3] is Label trainBrakeStatusLabel)
            {
                trainBrakeStatusLabel.Text = (playerLocomotive.TrainBrakeController as INameValueInformationProvider).DetailInfo[windowMode != WindowMode.Normal ? "StatusShort" : "Status"];
                (groupDetails[DetailInfo.TrainBrake].Controls[0] as Label).Text = trainBrakeInput;
                (groupDetails[DetailInfo.TrainBrake].Controls[2] as Label).Text = trainBrakeInput;
                trainBrakeInput = null;
            }
            // Train Brake Equalizer Reservoir
            result |= dataAvailable[DetailInfo.TrainBrakeEQStatus] != (dataAvailable[DetailInfo.TrainBrakeEQStatus] = !string.IsNullOrEmpty(playerLocomotive.BrakeSystem.BrakeInfo.DetailInfo["EQ"]));
            linesAdded += dataAvailable[DetailInfo.TrainBrakeEQStatus] ? 1 : 0;
            if (dataAvailable[DetailInfo.TrainBrakeEQStatus] && groupDetails[DetailInfo.TrainBrakeEQStatus]?.Controls[3] is Label trainBrakeEQLabel)
            {
                string eqReservoir = playerLocomotive.BrakeSystem.BrakeInfo.DetailInfo["EQ"];
                if (windowMode != WindowMode.Normal)
                    eqReservoir = eqReservoir?.Split(' ')[0];
                trainBrakeEQLabel.Text = eqReservoir;
                result |= dataAvailable[DetailInfo.TrainBrakeFirstCar] != (dataAvailable[DetailInfo.TrainBrakeFirstCar] = playerLocomotive.Train.FirstWagonCar != null);
                linesAdded += dataAvailable[DetailInfo.TrainBrakeFirstCar] ? 1 : 0;
                if (dataAvailable[DetailInfo.TrainBrakeFirstCar] && groupDetails[DetailInfo.TrainBrakeFirstCar]?.Controls[3] is Label firstWagonBrakeLabel)
                {
                    firstWagonBrakeLabel.Text = (windowMode != WindowMode.Normal) ?
                    playerLocomotive.Train.FirstWagonCar?.BrakeSystem.BrakeInfo.DetailInfo["StatusShort"] :
                    playerLocomotive.Train.FirstWagonCar?.BrakeSystem.BrakeInfo.DetailInfo["Status"];
                }
                (groupDetails[DetailInfo.TrainBrakeLastCar]?.Controls[3] as Label).Text = (windowMode != WindowMode.Normal) ?
                    playerLocomotive.Train.EndOfTrainCar?.BrakeSystem.BrakeInfo.DetailInfo["StatusShort"] :
                    playerLocomotive.Train.EndOfTrainCar?.BrakeSystem.BrakeInfo.DetailInfo["Status"];
            }
            else if (groupDetails[DetailInfo.TrainBrakeStatus]?.Controls[3] is Label trainBrakeLabel)
            {
                trainBrakeLabel.Text = (windowMode != WindowMode.Normal) ?
                    playerLocomotive.BrakeSystem.BrakeInfo.DetailInfo["StatusShort"] :
                    playerLocomotive.BrakeSystem.BrakeInfo.DetailInfo["Status"];
            }
            result |= dataAvailable[DetailInfo.Retainer] != (dataAvailable[DetailInfo.Retainer] = playerLocomotive.Train.BrakeSystem.RetainerSetting != RetainerSetting.Exhaust);
            linesAdded += dataAvailable[DetailInfo.Retainer] ? 1 : 0;
            if (dataAvailable[DetailInfo.Retainer] && groupDetails[DetailInfo.Retainer]?.Controls[3] is Label retainerLabel)
            {
                retainerLabel.Text = $"{playerLocomotive.Train.BrakeSystem.RetainerPercent}% {playerLocomotive.Train.BrakeSystem.RetainerSetting.GetLocalizedDescription()}";
            }
            if (groupDetails[DetailInfo.EngineBrake]?.Controls[3] is Label engineBrakeLabel)
            {
                engineBrakeLabel.Text = (playerLocomotive.EngineBrakeController as INameValueInformationProvider).DetailInfo["Status"];
                (groupDetails[DetailInfo.EngineBrake].Controls[0] as Label).Text = engineBrakeInput ?? (((playerLocomotive.EngineBrakeController as INameValueInformationProvider).DetailInfo["BailOff"] != null) ? FormatStrings.Markers.Block : null);
                (groupDetails[DetailInfo.EngineBrake].Controls[2] as Label).Text = engineBrakeInput ?? (((playerLocomotive.EngineBrakeController as INameValueInformationProvider).DetailInfo["BailOff"] != null) ? FormatStrings.Markers.Block : null);
                result |= dataAvailable[DetailInfo.EngineBC] != (dataAvailable[DetailInfo.EngineBC] = !string.IsNullOrEmpty((playerLocomotive.EngineBrakeController as INameValueInformationProvider).DetailInfo["BC"]));
                linesAdded += dataAvailable[DetailInfo.EngineBC] ? 1 : 0;
                if (dataAvailable[DetailInfo.EngineBC] && groupDetails[DetailInfo.EngineBC]?.Controls[3] is Label engineBCLabel)
                {
                    engineBCLabel.Text = (playerLocomotive.EngineBrakeController as INameValueInformationProvider).DetailInfo["BC"];
                }
                engineBrakeInput = null;
            }
            result |= dataAvailable[DetailInfo.DynamicBrake] != (dataAvailable[DetailInfo.DynamicBrake] = playerLocomotive.IsLeadLocomotive() && playerLocomotive.DynamicBrakeController != null);
            linesAdded += dataAvailable[DetailInfo.DynamicBrake] ? 1 : 0;
            if (dataAvailable[DetailInfo.DynamicBrake] && groupDetails[DetailInfo.DynamicBrake]?.Controls[3] is Label dynamicBrakeLabel)
            {
                dynamicBrakeLabel.Text = (playerLocomotive.DynamicBrakePercent >= 0) ?
                        playerLocomotive.DynamicBrake ?
                        (windowMode != WindowMode.Normal) ?
                    (playerLocomotive.DynamicBrakeController as INameValueInformationProvider).DetailInfo["StatusShort"] :
                    (playerLocomotive.DynamicBrakeController as INameValueInformationProvider).DetailInfo["Status"] :
                    Catalog.GetParticularString("DynamicBrake", "Setup") :
                    Catalog.GetParticularString("DynamicBrake", "Off");
                (groupDetails[DetailInfo.DynamicBrake].Controls[0] as Label).Text = dynamicBrakeInput;
                (groupDetails[DetailInfo.DynamicBrake].Controls[2] as Label).Text = dynamicBrakeInput;
                dynamicBrakeInput = null;
            }
            switch (playerLocomotive.EngineType)
            {
                case EngineType.Steam:
                    linesAdded += 5;
                    if (groupDetails[DetailInfo.SteamUsage]?.Controls[3] is Label steamUsageLabel)
                    {
                        steamUsageLabel.Text = playerLocomotive.CarInfo.DetailInfo["SteamUsage"];
                        if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("SteamUsage", out FormatOption option) && option != null)
                        {
                            steamUsageLabel.TextColor = option.TextColor ?? Color.White;
                        }
                    }
                    if (groupDetails[DetailInfo.SteamBoilerPressure]?.Controls[3] is Label boilerPressureLabel)
                    {
                        boilerPressureLabel.Text = playerLocomotive.CarInfo.DetailInfo["BoilerPressure"];
                        if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("BoilerPressure", out FormatOption option) && option != null)
                        {
                            boilerPressureLabel.TextColor = option.TextColor ?? Color.White;
                        }
                        (groupDetails[DetailInfo.SteamBoilerPressure].Controls[0] as Label).Text = playerLocomotive.CarInfo.DetailInfo["HeatingStatus"];
                        (groupDetails[DetailInfo.SteamBoilerPressure].Controls[0] as Label).TextColor = (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("HeatingStatus", out option) && option?.TextColor != null ? option.TextColor.Value : Color.White);
                        (groupDetails[DetailInfo.SteamBoilerPressure].Controls[2] as Label).Text = playerLocomotive.CarInfo.DetailInfo["HeatingStatus"];
                        (groupDetails[DetailInfo.SteamBoilerPressure].Controls[2] as Label).TextColor = (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("HeatingStatus", out option) && option?.TextColor != null ? option.TextColor.Value : Color.White);
                    }
                    if (groupDetails[DetailInfo.SteamBoilerWaterGlass]?.Controls[3] is Label boilerGlassLabel)
                    {
                        boilerGlassLabel.Text = playerLocomotive.CarInfo.DetailInfo["BoilerWaterGlass"];
                        if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("BoilerWaterGlass", out FormatOption option) && option != null)
                        {
                            boilerGlassLabel.TextColor = option.TextColor ?? Color.White;
                        }
                    }
                    result |= dataAvailable[DetailInfo.SteamBoilerWaterLevel] != (dataAvailable[DetailInfo.SteamBoilerWaterLevel] = !string.IsNullOrEmpty(playerLocomotive.CarInfo.DetailInfo["BoilerWaterLevel"]));
                    linesAdded += dataAvailable[DetailInfo.SteamBoilerWaterLevel] ? 2 : 0;
                    if (dataAvailable[DetailInfo.SteamBoilerWaterLevel] && groupDetails[DetailInfo.SteamBoilerWaterLevel]?.Controls[3] is Label boilerLevelLabel)
                    {
                        boilerLevelLabel.Text = playerLocomotive.CarInfo.DetailInfo["BoilerWaterLevel"];
                        if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("BoilerWaterLevel", out FormatOption option) && option != null)
                        {
                            boilerLevelLabel.TextColor = option.TextColor ?? Color.White;
                        }
                        if (groupDetails[DetailInfo.SteamFireMass]?.Controls[3] is Label fireMassLabel)
                        {
                            fireMassLabel.Text = playerLocomotive.CarInfo.DetailInfo["FireMass"];
                            if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("FireMass", out option) && option != null)
                            {
                                fireMassLabel.TextColor = option.TextColor ?? Color.White;
                            }
                        }
                    }
                    if (groupDetails[DetailInfo.SteamFuelLevelCoal]?.Controls[3] is Label coalLabel)
                    {
                        coalLabel.Text = $"{playerLocomotive.CarInfo.DetailInfo["FuelLevelCoal"]} {Catalog.GetString("coal")}";
                        if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("FuelLevelCoal", out FormatOption option) && option != null)
                            coalLabel.TextColor = option.TextColor ?? Color.White;
                    }
                    if (groupDetails[DetailInfo.SteamFuelLevelWater]?.Controls[3] is Label waterLabel)
                    {
                        waterLabel.Text = $"{playerLocomotive.CarInfo.DetailInfo["FuelLevelWater"]} {Catalog.GetString("water")}";
                        if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("FuelLevelWater", out FormatOption option) && option != null)
                            waterLabel.TextColor = option.TextColor ?? Color.White;
                    }
                    break;
                case EngineType.Diesel:
                case EngineType.Electric:
                    linesAdded += 6;
                    if (groupDetails[DetailInfo.DieselEngineRunning]?.Controls[3] is Label engineLabel)
                    {
                        engineLabel.Text = playerLocomotive.CarInfo.DetailInfo["Engine"];
                    }
                    result |= dataAvailable[DetailInfo.DieselGear] != (dataAvailable[DetailInfo.DieselGear] = !string.IsNullOrEmpty(playerLocomotive.CarInfo.DetailInfo["Gear"]));
                    linesAdded += dataAvailable[DetailInfo.DieselGear] ? 1 : 0;
                    if (dataAvailable[DetailInfo.DieselGear] && groupDetails[DetailInfo.DieselGear]?.Controls[3] is Label gearLabel)
                    {
                        gearLabel.Text = playerLocomotive.CarInfo.DetailInfo["Gear"];
                        (groupDetails[DetailInfo.DieselGear].Controls[0] as Label).Text = gearKeyInput;
                        (groupDetails[DetailInfo.DieselGear].Controls[2] as Label).Text = gearKeyInput;
                        gearKeyInput = null;
                    }
                    if (groupDetails[DetailInfo.Pantographs]?.Controls[3] is Label pantographLabel)
                    {
                        pantographLabel.Text = playerLocomotive.CarInfo.DetailInfo["Pantographs"];
                        (groupDetails[DetailInfo.Pantographs].Controls[0] as Label).Text = pantographKeyInput;
                        (groupDetails[DetailInfo.Pantographs].Controls[2] as Label).Text = pantographKeyInput;
                        pantographKeyInput = null;
                    }
                    if (groupDetails[DetailInfo.BatterySwitch]?.Controls[3] is Label batteryLabel)
                    {
                        batteryLabel.Text = playerLocomotive.CarInfo.DetailInfo["BatterySwitch"];
                    }
                    if (groupDetails[DetailInfo.MasterKey]?.Controls[3] is Label masterKeyLabel)
                    {
                        masterKeyLabel.Text = playerLocomotive.CarInfo.DetailInfo["MasterKey"];
                    }
                    if (groupDetails[DetailInfo.DieselTractionCutOffRelay]?.Controls[3] is Label tractionCutLabel)
                    {
                        tractionCutLabel.Text = playerLocomotive.CarInfo.DetailInfo["TractionCutOffRelay"];
                    }
                    if (groupDetails[DetailInfo.CircuitBreaker]?.Controls[3] is Label circuitBreakerLabel)
                    {
                        circuitBreakerLabel.Text = playerLocomotive.CarInfo.DetailInfo["CircuitBreaker"];
                    }
                    if (groupDetails[DetailInfo.ElectricTrainSupply]?.Controls[3] is Label electricSupplyLabel)
                    {
                        electricSupplyLabel.Text = playerLocomotive.CarInfo.DetailInfo["ElectricTrainSupply"];
                    }
                    if (groupDetails[DetailInfo.PowerSupply]?.Controls[3] is Label powerSupplyLabel)
                    {
                        powerSupplyLabel.Text = playerLocomotive.CarInfo.DetailInfo["PowerSupply"];
                    }
                    break;
            }

            // Cruise Control
            result |= dataAvailable[DetailInfo.CruiseControl] != (dataAvailable[DetailInfo.CruiseControl] = playerLocomotive.CruiseControl != null);
            linesAdded += dataAvailable[DetailInfo.CruiseControl] ? 1 : 0;
            if (dataAvailable[DetailInfo.CruiseControl] && groupDetails[DetailInfo.CruiseControl]?.Controls[3] is Label ccLabel && playerLocomotive.CruiseControl is CruiseControl cruiseControl)
            {
                ccLabel.Text = playerLocomotive.CruiseControl.SpeedRegulatorMode.GetLocalizedDescription();

                if ((dataAvailable[DetailInfo.CruiseControlTarget] = cruiseControl.SpeedRegulatorMode == SpeedRegulatorMode.Auto) && 
                    groupDetails[DetailInfo.CruiseControlTarget]?.Controls[3] is Label ccTargetLabel)
                {
                    ccTargetLabel.Text = $"{FormatStrings.FormatSpeedDisplay(cruiseControl.SelectedSpeedMpS, Simulator.Instance.MetricUnits)}";                    
                    (groupDetails[DetailInfo.CruiseControlMaxAccel]?.Controls[3] as Label).Text = $"{Math.Round(cruiseControl.SelectedMaxAccelerationPercent):0}%";
                    linesAdded += 2;
                }
            }
            separatorsAdded += dataAvailable[DetailInfo.EotDevice] ? 1 : 0;

            //EOT
            result |= dataAvailable[DetailInfo.EotDevice] != (dataAvailable[DetailInfo.EotDevice] = playerLocomotive.Train.EndOfTrainDevice != null);
            linesAdded += dataAvailable[DetailInfo.EotDevice] ? 1 : 0;
            separatorsAdded += dataAvailable[DetailInfo.EotDevice] ? 1 : 0;
            if (dataAvailable[DetailInfo.EotDevice] && groupDetails[DetailInfo.EotDevice]?.Controls[3] is Label eotLabel)
            {
                eotLabel.Text = playerLocomotive.Train.EndOfTrainDevice.State.GetLocalizedDescription();
            }

            IEnumerable<IGrouping<int, MSTSDieselLocomotive>> distributedLocomotives = playerLocomotive.Train.Cars.OfType<MSTSDieselLocomotive>().GroupBy((dieselLocomotive) => dieselLocomotive.DistributedPowerUnitId);
            result |= dataAvailable[DetailInfo.DieselDpu] != (dataAvailable[DetailInfo.DieselDpu] = distributedLocomotives.Count() > 1);
            linesAdded += dataAvailable[DetailInfo.DieselDpu] ? 1 : 0;
            separatorsAdded += dataAvailable[DetailInfo.DieselDpu] ? 1 : 0;
            if (dataAvailable[DetailInfo.DieselDpu] && groupDetails[DetailInfo.DieselDpu]?.Controls[3] is Label dpuLabel)
            {
                string dpuConfig = string.Empty;
                RemoteControlGroup remoteControlGroup = RemoteControlGroup.FrontGroupSync;
                foreach (IGrouping<int, MSTSDieselLocomotive> item in distributedLocomotives)
                {
                    if (!string.IsNullOrEmpty(dpuConfig))
                        dpuConfig += remoteControlGroup != (remoteControlGroup = item.First().RemoteControlGroup) ? FormatStrings.Markers.Fence : FormatStrings.Markers.Dash;
                    dpuConfig += item.Count();
                }
                dpuLabel.Text = dpuConfig;
            }

            if (groupDetails[DetailInfo.AutoPilot]?.Controls[3] is Label autopilotLabel)
            {
                autopilotLabel.Text = playerLocomotive.Train.TrainType == TrainType.AiPlayerHosting ? "On" : "Off";
                autopilotLabel.TextColor = playerLocomotive.Train.TrainType == TrainType.AiPlayerHosting ? Color.Yellow : Color.White;
            }

            result |= dataAvailable[DetailInfo.SteamAiFireMan] != (dataAvailable[DetailInfo.SteamAiFireMan] = !(string.IsNullOrEmpty(playerLocomotive.CarInfo.DetailInfo["AIFireMan"])));
            linesAdded += dataAvailable[DetailInfo.SteamAiFireMan] ? 1 : 0;
            if (dataAvailable[DetailInfo.SteamAiFireMan] && groupDetails[DetailInfo.SteamAiFireMan]?.Controls[3] is Label aiFireLabel)
            {
                aiFireLabel.Text = playerLocomotive.CarInfo.DetailInfo["AIFireMan"];
                if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("AIFireMan", out FormatOption option) && option != null)
                    aiFireLabel.TextColor = option.TextColor ?? Color.White;
            }

            //Grate Limit
            result |= dataAvailable[DetailInfo.SteamGrateLimit] != (dataAvailable[DetailInfo.SteamGrateLimit] = !(string.IsNullOrEmpty(playerLocomotive.CarInfo.DetailInfo["GrateLimit"])));
            linesAdded += dataAvailable[DetailInfo.SteamGrateLimit] ? 1 : 0;
            if (dataAvailable[DetailInfo.SteamGrateLimit] && groupDetails[DetailInfo.SteamGrateLimit]?.Controls[3] is Label grateLimitLabel)
            {
                grateLimitLabel.Text = playerLocomotive.CarInfo.DetailInfo["GrateLimit"];
                if (playerLocomotive.CarInfo.FormattingOptions.TryGetValue("GrateLimit", out FormatOption option) && option != null)
                    grateLimitLabel.TextColor = option.TextColor ?? Color.White;
            }

            //derail
            result |= dataAvailable[DetailInfo.Derailment] != (dataAvailable[DetailInfo.Derailment] = playerLocomotive.Train.Cars.Where(c => c.DerailExpected || c.DerailPossible).Any() || System.Environment.TickCount64 < derailTimeout);
            linesAdded += dataAvailable[DetailInfo.Derailment] ? 1 : 0;
            if (dataAvailable[DetailInfo.Derailment] && groupDetails[DetailInfo.Derailment]?.Controls[3] is Label derailmentLabel)
            {
                string derailedCar = playerLocomotive.Train.Cars.Where(c => c.DerailExpected).FirstOrDefault()?.CarID;
                if (!string.IsNullOrEmpty(derailedCar))
                {
                    derailTimeout = System.Environment.TickCount64 + (2 * settings.NotificationsTimeout);
                    derailmentLabel.Text = Catalog.GetString($"Derailed {derailedCar})");
                    derailmentLabel.TextColor = Color.OrangeRed;
                }
                else if (!string.IsNullOrEmpty(derailedCar = playerLocomotive.Train.Cars.Where(c => c.DerailPossible).FirstOrDefault()?.CarID))
                {
                    derailTimeout = System.Environment.TickCount64 + (2 * settings.NotificationsTimeout);
                    derailmentLabel.Text = Catalog.GetString($"Warning {derailedCar})");
                    derailmentLabel.TextColor = Color.Yellow;
                }
                else
                {
                    derailmentLabel.Text = Catalog.GetString("Normal");
                    derailmentLabel.TextColor = Color.White;
                }
            }

            // Wheel
            result |= dataAvailable[DetailInfo.WheelSlip] != (dataAvailable[DetailInfo.WheelSlip] = playerLocomotive.Train.IsWheelSlip || playerLocomotive.Train.IsWheelSlipWarninq || playerLocomotive.Train.IsBrakeSkid || System.Environment.TickCount64 < wheelSlipTimeout);
            linesAdded += dataAvailable[DetailInfo.WheelSlip] ? 1 : 0;
            if (dataAvailable[DetailInfo.WheelSlip] && groupDetails[DetailInfo.WheelSlip]?.Controls[3] is Label wheelSlipLabel)
            {
                if (playerLocomotive.Train.IsWheelSlip)
                {
                    wheelSlipLabel.Text = Viewer.Catalog.GetString("slip");
                    wheelSlipLabel.TextColor = Color.OrangeRed;
                    wheelSlipTimeout = System.Environment.TickCount64 + (settings.NotificationsTimeout * 2);
                }
                else if (playerLocomotive.Train.IsWheelSlipWarninq)
                {
                    wheelSlipLabel.Text = Viewer.Catalog.GetString("slip warning");
                    wheelSlipLabel.TextColor = Color.Yellow;
                    wheelSlipTimeout = System.Environment.TickCount64 + (settings.NotificationsTimeout * 2);
                }
                else if (playerLocomotive.Train.IsBrakeSkid)
                {
                    wheelSlipLabel.Text = Viewer.Catalog.GetString("skid");
                    wheelSlipLabel.TextColor = Color.OrangeRed;
                    wheelSlipTimeout = System.Environment.TickCount64 + (settings.NotificationsTimeout * 2);
                }
                else
                {
                    wheelSlipLabel.Text = Viewer.Catalog.GetString("Normal");
                    wheelSlipLabel.TextColor = Color.White;
                }
            }


            bool flipped = playerLocomotive.Flipped ^ playerLocomotive.GetCabFlipped();
            bool doorLeftOpen = playerLocomotive.Train.DoorState(flipped ? DoorSide.Right : DoorSide.Left) != DoorState.Closed;
            bool doorRightOpen = playerLocomotive.Train.DoorState(flipped ? DoorSide.Left : DoorSide.Right) != DoorState.Closed;

            result |= dataAvailable[DetailInfo.DoorOpen] != (dataAvailable[DetailInfo.DoorOpen] = doorLeftOpen || doorRightOpen || System.Environment.TickCount64 < doorOpenTimeout);
            linesAdded += dataAvailable[DetailInfo.DoorOpen] ? 1 : 0;
            if (dataAvailable[DetailInfo.DoorOpen] && groupDetails[DetailInfo.DoorOpen]?.Controls[3] is Label doorLabel)
            {
                if (doorLeftOpen || doorRightOpen)
                {
                    doorOpenTimeout = System.Environment.TickCount64 + (settings.NotificationsTimeout * 2);

                    doorLabel.Text = FormatStrings.JoinIfNotEmpty(FormatStrings.Markers.Fence[0], 
                        doorLeftOpen ? Catalog.GetString("Left") : null,
                        doorRightOpen ? Catalog.GetString("Right") : null);
                    doorLabel.TextColor = playerLocomotive.AbsSpeedMpS > 0 ? Color.OrangeRed : Color.Yellow;
                }
                else
                {
                    doorLabel.Text = Catalog.GetString("Closed");
                    doorLabel.TextColor = Color.White;
                }
            }

            result |= additionalLines != linesAdded;
            additionalLines = linesAdded;
            additonalSeparators = separatorsAdded;
            return result;
        }
    }
}
