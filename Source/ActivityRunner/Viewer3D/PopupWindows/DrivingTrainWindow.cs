using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Formats.Msts;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class DrivingTrainWindow : WindowBase
    {
        private const int monoColumnWidth = 48;
        private const int normalColumnWidth = 64;

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
            Direction,
            Throttle,
        }

        private readonly UserSettings settings;
        private readonly UserCommandController<UserCommand> userCommandController;
        private WindowMode windowMode;
        private Label labelExpandMono;
        private readonly EnumArray<ControlLayout, DetailInfo> groupDetails = new EnumArray<ControlLayout, DetailInfo>();

        private string directionKeyInput;
        private string throttleKeyInput;
        private string cylinderCocksInput;
        private string sanderInput;
        private string trainBrakeInput;
        private string engineBrakeInput;
        private bool throttleIncreaseDown;
        private bool throttleDecreaseDown;
        private bool dynamicBrakeIncreaseDown;
        private bool dynamicBrakeDecreaseDown;
        private string gearKeyInput;
        private bool pantographKeyDown;
        private bool autoPilotKeyDown;
        private bool firingKeyDown;
        private bool aiFireOnKeyDown;
        private bool aiFireOffKeyDown;
        private bool aiFireResetKeyDown;

        public DrivingTrainWindow(WindowManager owner, Point relativeLocation, UserSettings settings, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Train Driving Info"), relativeLocation, new Point(200, 300), catalog)
        {
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
            this.settings = settings;
            _ = EnumExtension.GetValue(settings.PopupSettings[ViewerWindowType.DistributedPowerWindow], out windowMode);

            Resize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling).AddLayoutOffset(0);
            ControlLayout line = layout.AddLayoutHorizontal();
            line.HorizontalChildAlignment = HorizontalAlignment.Right;
            line.VerticalChildAlignment = VerticalAlignment.Top;
            line.Add(labelExpandMono = new Label(this, Owner.TextFontDefault.Height, Owner.TextFontDefault.Height, windowMode == WindowMode.NormalMono ? Markers.ArrowRight : Markers.ArrowLeft, HorizontalAlignment.Center, Color.Yellow));
            labelExpandMono.OnClick += LabelExpandMono_OnClick;
            layout = layout.AddLayoutVertical();

            void AddDetailLine(DetailInfo detail, int width, string caption, System.Drawing.Font font)
            {
                line = layout.AddLayoutHorizontalLineOfText();
                line.Add(new Label(this, 14, font.Height, null, font) { TextColor = Color.Yellow });
                line.Add(new Label(this, width, font.Height, caption, font));
                line.Add(new Label(this, 14, font.Height, null, font) { TextColor = Color.Yellow });
                line.Add(new Label(this, 80, font.Height, null, font));

                groupDetails[detail] = line;
            }

            if (windowMode == WindowMode.Normal)
            {
                int columnWidth = (int)(Owner.DpiScaling * normalColumnWidth);
                AddDetailLine(DetailInfo.Time, columnWidth, Catalog.GetString("Time"), Owner.TextFontDefault);
                if (Simulator.Instance.IsReplaying)
                {
                    AddDetailLine(DetailInfo.Replay, columnWidth, Catalog.GetString("Replay"), Owner.TextFontDefault);
                }
                AddDetailLine(DetailInfo.Speed, columnWidth, Catalog.GetString("Speed"), Owner.TextFontDefault);
                AddDetailLine(DetailInfo.Gradient, columnWidth, Catalog.GetString("Gradient"), Owner.TextFontDefault);
                layout.AddHorizontalSeparator(true);
                AddDetailLine(DetailInfo.Direction, columnWidth, Simulator.Instance.PlayerLocomotive.EngineType == EngineType.Steam ? Catalog.GetString("Reverser") : Catalog.GetString("Direction"), Owner.TextFontDefault);
                AddDetailLine(DetailInfo.Throttle, columnWidth, Simulator.Instance.PlayerLocomotive.EngineType == EngineType.Steam ? Catalog.GetString("Regulator") : Catalog.GetString("Throttle"), Owner.TextFontDefault);
            }
            else
            {
                int columnWidth = (int)(Owner.DpiScaling * monoColumnWidth);
                AddDetailLine(DetailInfo.Time, columnWidth, FourCharAcronym.Time.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                if (Simulator.Instance.IsReplaying)
                {
                    AddDetailLine(DetailInfo.Replay, columnWidth, FourCharAcronym.Replay.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                }
                AddDetailLine(DetailInfo.Speed, columnWidth, FourCharAcronym.Speed.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                AddDetailLine(DetailInfo.Gradient, columnWidth, FourCharAcronym.Gradient.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                layout.AddHorizontalSeparator(true);
                AddDetailLine(DetailInfo.Direction, columnWidth, Simulator.Instance.PlayerLocomotive.EngineType == EngineType.Steam ? FourCharAcronym.Reverser.GetLocalizedDescription() : FourCharAcronym.Direction.GetLocalizedDescription(), Owner.TextFontMonoDefault);
                AddDetailLine(DetailInfo.Throttle, columnWidth, Simulator.Instance.PlayerLocomotive.EngineType == EngineType.Steam ? FourCharAcronym.Regulator.GetLocalizedDescription() : FourCharAcronym.Throttle.GetLocalizedDescription(), Owner.TextFontMonoDefault);
            }

            return layout;
        }

        private void LabelExpandMono_OnClick(object sender, MouseClickEventArgs e)
        {
            windowMode = windowMode.Next();
            Resize();
        }

        public override bool Open()
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
            userCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, GearCommandDown, true);
            userCommandController.AddEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, PantographCommand, true);
            userCommandController.AddEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, PantographCommand, true);
            userCommandController.AddEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, PantographCommand, true);
            userCommandController.AddEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, PantographCommand, true);
            userCommandController.AddEvent(UserCommand.GameAutopilotMode, KeyEventType.KeyDown, AutoPilotCommand, true);
            userCommandController.AddEvent(UserCommand.ControlFiring, KeyEventType.KeyDown, FiringCommand, true);
            userCommandController.AddEvent(UserCommand.ControlAIFireOn, KeyEventType.KeyDown, AIFiringOnCommand, true);
            userCommandController.AddEvent(UserCommand.ControlAIFireOff, KeyEventType.KeyDown, AIFiringOffCommand, true);
            userCommandController.AddEvent(UserCommand.ControlAIFireReset, KeyEventType.KeyDown, AIFiringResetCommand, true);

            userCommandController.AddEvent(UserCommand.DisplayTrainDrivingWindow, KeyEventType.KeyPressed, TabAction, true);

            return base.Open();
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
            userCommandController.RemoveEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, PantographCommand);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, PantographCommand);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, PantographCommand);
            userCommandController.RemoveEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, PantographCommand);
            userCommandController.RemoveEvent(UserCommand.GameAutopilotMode, KeyEventType.KeyDown, AutoPilotCommand);
            userCommandController.RemoveEvent(UserCommand.ControlFiring, KeyEventType.KeyDown, FiringCommand);
            userCommandController.RemoveEvent(UserCommand.ControlAIFireOn, KeyEventType.KeyDown, AIFiringOnCommand);
            userCommandController.RemoveEvent(UserCommand.ControlAIFireOff, KeyEventType.KeyDown, AIFiringOffCommand);
            userCommandController.RemoveEvent(UserCommand.ControlAIFireReset, KeyEventType.KeyDown, AIFiringResetCommand);

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
            Point size = windowMode switch
            {
                WindowMode.Normal => new Point(normalColumnWidth + 36 + 120, 300),
                WindowMode.NormalMono => new Point(monoColumnWidth + 36 + 80, 300),
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
#pragma warning restore IDE0022 // Use block body for methods

        private void DirectionCommand(Direction direction)
        {
            TrainCar locomotive = Simulator.Instance.PlayerLocomotive;
            directionKeyInput = (locomotive.EngineType != EngineType.Steam &&
                (locomotive.Direction == MidpointDirection.N) && (locomotive.ThrottlePercent >= 1 || Math.Abs(locomotive.SpeedMpS) > 1))
                || (locomotive.EngineType == EngineType.Steam && locomotive is MSTSSteamLocomotive mstsSteamLocomotive && mstsSteamLocomotive.CutoffController.MaximumValue == Math.Abs(locomotive.Train.MUReverserPercent / 100))
                ? Markers.Block
                : direction == Direction.Forward ? Markers.ArrowUp : Markers.ArrowDown;
        }

        private void ThrottleCommand(bool increase)
        {
            throttleIncreaseDown = increase;
            throttleDecreaseDown = !increase;
            TrainCar locomotive = Simulator.Instance.PlayerLocomotive;
            throttleKeyInput = locomotive.DynamicBrakePercent < 1 &&
                (increase && (locomotive as MSTSLocomotive).ThrottleController.MaximumValue == locomotive.ThrottlePercent / 100)
                || (!increase && locomotive.ThrottlePercent == 0)
                ? Markers.Block
                : locomotive.DynamicBrakePercent > -1
                    ? Markers.BlockLowerHalf
                    : increase ? Markers.ArrowUp : Markers.ArrowDown;
        }

        private void CylinderCocksCommand()
        {
            if (Simulator.Instance.PlayerLocomotive is MSTSSteamLocomotive)
                cylinderCocksInput = Markers.ArrowRight;
        }

        private void SanderCommand()
        {
            sanderInput = Markers.ArrowDown;
        }

        private void TrainBrakeCommand(bool increase)
        {
            trainBrakeInput = increase ? Markers.ArrowDown : Markers.ArrowUp;
        }

        private void EngineBrakeCommand(bool increase)
        {
            engineBrakeInput = increase ? Markers.ArrowDown : Markers.ArrowUp;
        }

        private void DynamicBrakeCommand(bool increase)
        {
            dynamicBrakeIncreaseDown = increase;
            dynamicBrakeDecreaseDown = !increase;
        }

        private void GearCommand(bool down)
        {
            gearKeyInput = down ? Markers.ArrowDown : Markers.ArrowUp;
        }

        private void PantographCommand()
        {
            pantographKeyDown = true;
        }

        private void AutoPilotCommand()
        {
            autoPilotKeyDown = true;
        }

        private void FiringCommand()
        {
            firingKeyDown = true;
        }

        private void AIFiringOnCommand()
        {
            aiFireOnKeyDown = true;
        }

        private void AIFiringOffCommand()
        {
            aiFireOffKeyDown = true;
        }

        private void AIFiringResetCommand()
        {
            aiFireResetKeyDown = true;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
                UpdateDrivingInformation();
        }

        private void UpdateDrivingInformation()
        {
            TrainCar playerLocomotive = Simulator.Instance.PlayerLocomotive;
            if (groupDetails[DetailInfo.Time]?.Controls[3] is Label timeLabel)
                timeLabel.Text = $"{FormatStrings.FormatTime(Simulator.Instance.ClockTime)}";
            if (groupDetails[DetailInfo.Replay]?.Controls[3] is Label replayLabel)
                replayLabel.Text = $"{FormatStrings.FormatTime(Simulator.Instance.Log.ReplayEndsAt - Simulator.Instance.ClockTime)}";
            if (groupDetails[DetailInfo.Speed]?.Controls[3] is Label speedLabel)
            {
                speedLabel.Text = $"{FormatStrings.FormatSpeedDisplay(playerLocomotive.SpeedMpS, Simulator.Instance.MetricUnits)}";
                speedLabel.TextColor = ColorCoding.SpeedingColor(playerLocomotive.AbsSpeedMpS, playerLocomotive.Train.MaxTrainSpeedAllowed);
            }
            if (groupDetails[DetailInfo.Gradient]?.Controls[3] is Label gradientLabel)
            {
                double gradient = Math.Round(playerLocomotive.CurrentElevationPercent, 1);
                if (gradient == 0) // to avoid negative zero string output if gradient after rounding is -0.0
                    gradient = 0;
                gradientLabel.Text = $"{gradient:F1}% {(gradient > 0 ? Markers.Ascent : gradient < 0 ? Markers.Descent : string.Empty)}";
                gradientLabel.TextColor = (gradient > 0 ? Color.Yellow : gradient < 0 ? Color.LightSkyBlue : Color.White);
            }
            if (groupDetails[DetailInfo.Direction]?.Controls[3] is Label directionLabel)
            {
                float reverserPercent = playerLocomotive.Train.MUReverserPercent;
                directionLabel.Text = $"{(Math.Abs(reverserPercent) != 100 ? $"{reverserPercent}% " : string.Empty)}{playerLocomotive.Direction.GetLocalizedDescription()}";
                (groupDetails[DetailInfo.Direction].Controls[0] as Label).Text = directionKeyInput;
                (groupDetails[DetailInfo.Direction].Controls[2] as Label).Text = directionKeyInput;
                directionKeyInput = string.Empty;
            }
            if (groupDetails[DetailInfo.Throttle]?.Controls[3] is Label throttleLabel)
            {
                throttleLabel.Text = $"{Math.Round(playerLocomotive.ThrottlePercent):F0}% {(playerLocomotive is MSTSDieselLocomotive && playerLocomotive.Train.DistributedPowerMode == DistributedPowerMode.Traction ? $"({Math.Round(playerLocomotive.Train.DPThrottlePercent):F0}%)" : string.Empty)}";
                (groupDetails[DetailInfo.Throttle].Controls[0] as Label).Text = throttleKeyInput;
                (groupDetails[DetailInfo.Throttle].Controls[2] as Label).Text = throttleKeyInput;
                throttleKeyInput = string.Empty;
            }

        }
    }
}
