// COPYRIGHT 2019, 2020 by the Open Rails project.
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
using System.Text;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Input;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class TrainDrivingWindow : Window
    {

        private const string arrowUp = "\u25B2???";  // ▲
        private const char smallArrowUp = '\u25B3';  // △
        private const string arrowDown = "\u25BC???";// ▼
        private const char smallArrowDown = '\u25BD';// ▽
        private const string end = "\u2589???";// block ▉
        private const string endLower = "\u2596???";// block ▖
        private const string arrowToRight = "\u25BA???"; // ►
        private const char smallDiamond = '\u25C6'; // ●
        private const string marker = "???";
        private bool DynBrakeSetup;
        private bool ResizeWindow;
        private bool UpdateDataEnded;
        private double StartTime;
        private int FirstColIndex;//first string that does not fit
        private int FirstColLenght;
        private int FirstColOverFlow;
        private int LastColLenght;
        private int LastColOverFlow;
        private int LinesCount;
        private int maxFirstColWidth;
        private int maxLastColWidth;

        private bool ctrlAIFiremanOn; //AIFireman On
        private bool ctrlAIFiremanOff;//AIFireman Off
        private bool ctrlAIFiremanReset;//AIFireman Reset
        private double clockAIFireTime; //AIFireman reset timing

        private bool grateLabelVisible;// Grate label visible
        private double clockGrateTime; // Grate hide timing

        private bool wheelLabelVisible;// Wheel label visible
        private double clockWheelTime; // Wheel hide timing

        private bool doorsLabelVisible; // Doors label visible
        private double clockDoorsTime; // Doors hide timing

        bool derailLabelVisible;// DerailCoeff label visible
        double clockDerailTime; //  DerailCoeff label visible

        private bool standardHUDMode = true;// Standard text

        private int WindowHeightMin;
        private int WindowHeightMax;
        private int WindowWidthMin;
        private int WindowWidthMax;
        private char expandWindow;
        private string Gradient;
        private const int TextSize = 15;
        private Label indicator;
        private LabelMono indicatorMono;
        private Label ExpandWindow;
        private Label LabelFontToBold;
        public static bool MonoFont;
        public static bool FontToBold;
        public static bool FontChanged;

        private readonly struct ListLabel
        {
            public readonly string FirstCol;
            public readonly int FirstColWidth;
            public readonly string LastCol;
            public readonly int LastColWidth;
            public readonly string SymbolCol;
            public readonly bool ChangeColWidth;
            public readonly string KeyPressed;

            public ListLabel(string firstCol, int firstColWidth, string lastCol, int lastColWidth, string symbolCol, bool updateSymbolColWidth, string keyPressed)
            {
                FirstCol = firstCol;
                FirstColWidth = firstColWidth;
                LastCol = lastCol;
                LastColWidth = lastColWidth;
                SymbolCol = symbolCol;
                ChangeColWidth = updateSymbolColWidth;
                KeyPressed = keyPressed;
            }
        }

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

        private readonly List<ListLabel> listToLabel = new List<ListLabel>();

        // Change text color
        private readonly Dictionary<string, Color> ColorCode = new Dictionary<string, Color>
        {
            { "!!!", Color.OrangeRed },
            { "!!?", Color.Orange },
            { "!??", Color.White },
            { "?!?", Color.Black },
            { "???", Color.Yellow },
            { "??!", Color.Green },
            { "?!!", Color.PaleGreen },
            { "$$$", Color.LightSkyBlue},
            { "$??", Color.Cyan},
            { "%$$", Color.Brown },
            { "%%$", Color.LightGreen },
            { "$%$", Color.Blue },
        };

        private readonly Dictionary<string, string> FirstColToAbbreviated = new Dictionary<string, string>()
        {
            [Viewer.Catalog.GetString("AI Fireman")] = Viewer.Catalog.GetString("AIFR"),
            [Viewer.Catalog.GetString("Autopilot")] = Viewer.Catalog.GetString("AUTO"),
            [Viewer.Catalog.GetString("Boiler pressure")] = Viewer.Catalog.GetString("PRES"),
            [Viewer.Catalog.GetString("Boiler water glass")] = Viewer.Catalog.GetString("WATR"),
            [Viewer.Catalog.GetString("Boiler water level")] = Viewer.Catalog.GetString("LEVL"),
            [Viewer.Catalog.GetString("Circuit breaker")] = Viewer.Catalog.GetString("CIRC"),
            [Viewer.Catalog.GetString("Cylinder cocks")] = Viewer.Catalog.GetString("CCOK"),
            [Viewer.Catalog.GetString("Direction")] = Viewer.Catalog.GetString("DIRC"),
            [Viewer.Catalog.GetString("DerailCoeff")] = Viewer.Catalog.GetString("DRLC"),
            [Viewer.Catalog.GetString("Doors open")] = Viewer.Catalog.GetString("DOOR"),
            [Viewer.Catalog.GetString("Dynamic brake")] = Viewer.Catalog.GetString("BDYN"),
            [Viewer.Catalog.GetString("Engine brake")] = Viewer.Catalog.GetString("BLOC"),
            [Viewer.Catalog.GetString("Engine")] = Viewer.Catalog.GetString("ENGN"),
            [Viewer.Catalog.GetString("Fire mass")] = Viewer.Catalog.GetString("FIRE"),
            [Viewer.Catalog.GetString("Fixed gear")] = Viewer.Catalog.GetString("GEAR"),
            [Viewer.Catalog.GetString("Fuel levels")] = Viewer.Catalog.GetString("FUEL"),
            [Viewer.Catalog.GetString("Gear")] = Viewer.Catalog.GetString("GEAR"),
            [Viewer.Catalog.GetString("Gradient")] = Viewer.Catalog.GetString("GRAD"),
            [Viewer.Catalog.GetString("Grate limit")] = Viewer.Catalog.GetString("GRAT"),
            [Viewer.Catalog.GetString("Pantographs")] = Viewer.Catalog.GetString("PANT"),
            [Viewer.Catalog.GetString("Power")] = Viewer.Catalog.GetString("POWR"),
            [Viewer.Catalog.GetString("Regulator")] = Viewer.Catalog.GetString("REGL"),
            [Viewer.Catalog.GetString("Replay")] = Viewer.Catalog.GetString("RPLY"),
            [Viewer.Catalog.GetString("Retainers")] = Viewer.Catalog.GetString("RETN"),
            [Viewer.Catalog.GetString("Reverser")] = Viewer.Catalog.GetString("REVR"),
            [Viewer.Catalog.GetString("Sander")] = Viewer.Catalog.GetString("SAND"),
            [Viewer.Catalog.GetString("Speed")] = Viewer.Catalog.GetString("SPED"),
            [Viewer.Catalog.GetString("Steam usage")] = Viewer.Catalog.GetString("STEM"),
            [Viewer.Catalog.GetString("Throttle")] = Viewer.Catalog.GetString("THRO"),
            [Viewer.Catalog.GetString("Time")] = Viewer.Catalog.GetString("TIME"),
            [Viewer.Catalog.GetString("Train brake")] = Viewer.Catalog.GetString("BTRN"),
            [Viewer.Catalog.GetString("Wheel")] = Viewer.Catalog.GetString("WHEL"),
        };

        private readonly Dictionary<string, string> LastColToAbbreviated = new Dictionary<string, string>()
        {
            [Viewer.Catalog.GetString("(absolute)")] = Viewer.Catalog.GetString("(Abs.)"),
            [Viewer.Catalog.GetString("apply Service")] = Viewer.Catalog.GetString("Apply"),
            [Viewer.Catalog.GetString("Apply Quick")] = Viewer.Catalog.GetString("ApplQ"),
            [Viewer.Catalog.GetString("Apply Slow")] = Viewer.Catalog.GetString("ApplS"),
            [Viewer.Catalog.GetString("coal")] = Viewer.Catalog.GetString("c"),
            [Viewer.Catalog.GetString("Emergency Braking Push Button")] = Viewer.Catalog.GetString("EmerBPB"),
            [Viewer.Catalog.GetString("Lap Self")] = Viewer.Catalog.GetString("LapS"),
            [Viewer.Catalog.GetString("Minimum Reduction")] = Viewer.Catalog.GetString("MRedc"),
            [Viewer.Catalog.GetString("safe range")] = Viewer.Catalog.GetString("safe"),
            [Viewer.Catalog.GetString("skid")] = Viewer.Catalog.GetString("Skid"),
            [Viewer.Catalog.GetString("slip warning")] = Viewer.Catalog.GetString("Warning"),
            [Viewer.Catalog.GetString("slip")] = Viewer.Catalog.GetString("Slip"),
            [Viewer.Catalog.GetString("water")] = Viewer.Catalog.GetString("w"),
        };

        public TrainDrivingWindow(WindowManager owner)
            : base(owner, Window.DecorationSize.X + owner.TextFontDefault.Height * 10, Window.DecorationSize.Y + owner.TextFontDefault.Height * 10, Viewer.Catalog.GetString("Train Driving Info"))
        {
            WindowHeightMin = Location.Height;
            WindowHeightMax = Location.Height + owner.TextFontDefault.Height * 20;
            WindowWidthMin = Location.Width;
            WindowWidthMax = Location.Width + owner.TextFontDefault.Height * 20;
        }

        protected override void VisibilityChanged()
        {
            base.VisibilityChanged();
            if (Visible)
            {
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserForward, KeyEventType.KeyDown, DirectionCommandForward, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyDown, DirectionCommandBackward, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyDown, ThrottleCommandIncrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyDown, ThrottleCommandDecrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlCylinderCocks, KeyEventType.KeyDown, CylinderCocksCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlSander, KeyEventType.KeyDown, SanderCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlSanderToggle, KeyEventType.KeyDown, SanderCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyDown, TrainBrakeCommandIncrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyDown, TrainBrakeCommandDecrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyDown, EngineBrakeCommandIncrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyDown, EngineBrakeCommandDecrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyDown, DynamicBrakeCommandIncrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyDown, DynamicBrakeCommandDecrease, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlGearUp, KeyEventType.KeyDown, GearCommandUp, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, GearCommandDown, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlGearUp, KeyEventType.KeyDown, GearCommandUp, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, GearCommandDown, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, PantographCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, PantographCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, PantographCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, PantographCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.GameAutopilotMode, KeyEventType.KeyDown, AutoPilotCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlFiring, KeyEventType.KeyDown, FiringCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlAIFireOn, KeyEventType.KeyDown, AIFiringOnCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlAIFireOff, KeyEventType.KeyDown, AIFiringOffCommand, true);
                Owner.Viewer.UserCommandController.AddEvent(UserCommand.ControlAIFireReset, KeyEventType.KeyDown, AIFiringResetCommand, true);
            }
            else
            {
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserForward, KeyEventType.KeyDown, DirectionCommandForward);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyDown, DirectionCommandBackward);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyDown, ThrottleCommandIncrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyDown, ThrottleCommandDecrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlCylinderCocks, KeyEventType.KeyDown, CylinderCocksCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSander, KeyEventType.KeyDown, SanderCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlSanderToggle, KeyEventType.KeyDown, SanderCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyDown, TrainBrakeCommandIncrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyDown, TrainBrakeCommandDecrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEngineBrakeIncrease, KeyEventType.KeyDown, EngineBrakeCommandIncrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlEngineBrakeDecrease, KeyEventType.KeyDown, EngineBrakeCommandDecrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeIncrease, KeyEventType.KeyDown, DynamicBrakeCommandIncrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlDynamicBrakeDecrease, KeyEventType.KeyDown, DynamicBrakeCommandDecrease);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGearUp, KeyEventType.KeyDown, GearCommandUp);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, GearCommandDown);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, PantographCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, PantographCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, PantographCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, PantographCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.GameAutopilotMode, KeyEventType.KeyDown, AutoPilotCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlFiring, KeyEventType.KeyDown, FiringCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAIFireOn, KeyEventType.KeyDown, AIFiringOnCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAIFireOff, KeyEventType.KeyDown, AIFiringOffCommand);
                Owner.Viewer.UserCommandController.RemoveEvent(UserCommand.ControlAIFireReset, KeyEventType.KeyDown, AIFiringResetCommand);
            }
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
            if ((Owner.Viewer.PlayerLocomotive.EngineType != TrainCar.EngineTypes.Steam &&
                (Owner.Viewer.PlayerLocomotive.Direction != MidpointDirection.Forward
                || Owner.Viewer.PlayerLocomotive.Direction != MidpointDirection.Reverse)
                && (Owner.Viewer.PlayerLocomotive.ThrottlePercent >= 1
                || Math.Abs(Owner.Viewer.PlayerLocomotive.SpeedMpS) > 1))
                || (Owner.Viewer.PlayerLocomotive.EngineType == TrainCar.EngineTypes.Steam &&
                Owner.Viewer.PlayerLocomotive is MSTSSteamLocomotive mstsSteamLocomotive && mstsSteamLocomotive.CutoffController.MaximumValue == Math.Abs(Owner.Viewer.PlayerLocomotive.Train.MUReverserPercent / 100))
                )
            {
                directionKeyInput = end;
            }
            else
            {
                directionKeyInput = direction == Direction.Forward ? arrowUp : arrowDown;
            }
        }

        private void ThrottleCommand(bool increase)
        {
            throttleIncreaseDown = increase;
            throttleDecreaseDown = !increase;
            if (Owner.Viewer.PlayerLocomotive.DynamicBrakePercent < 1 &&
                (increase && (Owner.Viewer.PlayerLocomotive as MSTSLocomotive).ThrottleController.MaximumValue == Owner.Viewer.PlayerLocomotive.ThrottlePercent / 100)
                || (!increase && Owner.Viewer.PlayerLocomotive.ThrottlePercent == 0))
            {
                throttleKeyInput = end;
            }
            else if (Owner.Viewer.PlayerLocomotive.DynamicBrakePercent > -1)
            {
                throttleKeyInput = endLower;
            }
            else
            {
                throttleKeyInput = increase ? arrowUp : arrowDown;
            }
        }

        private void CylinderCocksCommand()
        {
            if (Owner.Viewer.PlayerLocomotive is MSTSSteamLocomotive)
                cylinderCocksInput = arrowToRight;
        }

        private void SanderCommand()
        {
            sanderInput = arrowDown;
        }

        private void TrainBrakeCommand(bool increase)
        {
            trainBrakeInput = increase ? arrowUp : arrowDown;
        }

        private void EngineBrakeCommand(bool increase)
        {
            engineBrakeInput = increase ? arrowUp : arrowDown;
        }

        private void DynamicBrakeCommand(bool increase)
        {
            dynamicBrakeIncreaseDown = increase;
            dynamicBrakeDecreaseDown = !increase;
        }

        private void GearCommand(bool down)
        {
            gearKeyInput = down ? arrowDown : arrowUp;
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

        internal protected override void Save(BinaryWriter outf)
        {
            base.Save(outf);
            outf.Write(standardHUDMode);
            outf.Write(Location.X);
            outf.Write(Location.Y);
            outf.Write(Location.Width);
            outf.Write(Location.Height);
            outf.Write(clockAIFireTime);
            outf.Write(ctrlAIFiremanOn);
            outf.Write(ctrlAIFiremanOff);
            outf.Write(ctrlAIFiremanReset);
            outf.Write(clockWheelTime);
            outf.Write(wheelLabelVisible);
            outf.Write(clockDerailTime);
            outf.Write(derailLabelVisible);
            outf.Write(clockDoorsTime);
            outf.Write(doorsLabelVisible);
        }

        internal protected override void Restore(BinaryReader inf)
        {
            base.Restore(inf);
            Rectangle LocationRestore;
            standardHUDMode = inf.ReadBoolean();
            LocationRestore.X = inf.ReadInt32();
            LocationRestore.Y = inf.ReadInt32();
            LocationRestore.Width = inf.ReadInt32();
            LocationRestore.Height = inf.ReadInt32();
            clockAIFireTime = inf.ReadDouble();
            ctrlAIFiremanOn = inf.ReadBoolean();
            ctrlAIFiremanOff = inf.ReadBoolean();
            ctrlAIFiremanReset = inf.ReadBoolean();
            clockWheelTime = inf.ReadDouble();
            wheelLabelVisible = inf.ReadBoolean();
            clockDerailTime = inf.ReadDouble();
            derailLabelVisible = inf.ReadBoolean();
            clockDoorsTime = inf.ReadDouble();
            doorsLabelVisible = inf.ReadBoolean();

            // Display window
            SizeTo(LocationRestore.Width, LocationRestore.Height);
            MoveTo(LocationRestore.X, LocationRestore.Y);
        }

        internal protected override void Initialize()
        {
            base.Initialize();
            // Reset window size
            UpdateWindowSize();

        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            string keyPressed;
            // Display main HUD data
            var vbox = base.Layout(layout).AddLayoutVertical();
            if (listToLabel.Count > 0)
            {
                int colWidth = listToLabel.Max(x => x.FirstColWidth) + (standardHUDMode ? 15 : 20);
                int TimeHboxPositionY = 0;
                foreach (var data in listToLabel)
                {
                    if (data.FirstCol.Contains("NwLn", StringComparison.OrdinalIgnoreCase))
                    {
                        var hbox = vbox.AddLayoutHorizontalLineOfText();
                        hbox.Add(new Label(colWidth * 2, hbox.RemainingHeight, " "));
                    }
                    else if (data.FirstCol.Contains("Sprtr", StringComparison.OrdinalIgnoreCase))
                    {
                        vbox.AddHorizontalSeparator();
                    }
                    else
                    {
                        var hbox = vbox.AddLayoutHorizontalLineOfText();
                        var FirstCol = data.FirstCol;
                        var LastCol = data.LastCol;
                        var SymbolCol = data.SymbolCol;

                        if (ColorCode.Keys.Any(FirstCol.EndsWith) || ColorCode.Keys.Any(LastCol.EndsWith) || ColorCode.Keys.Any(data.KeyPressed.EndsWith) || ColorCode.Keys.Any(data.SymbolCol.EndsWith))
                        {
                            var colorFirstColEndsWith = ColorCode.Keys.Any(FirstCol.EndsWith) ? ColorCode[FirstCol.Substring(FirstCol.Length - 3)] : Color.White;
                            var colorLastColEndsWith = ColorCode.Keys.Any(LastCol.EndsWith) ? ColorCode[LastCol.Substring(LastCol.Length - 3)] : Color.White;
                            var colorKeyPressed = ColorCode.Keys.Any(data.KeyPressed.EndsWith) ? ColorCode[data.KeyPressed.Substring(data.KeyPressed.Length - 3)] : Color.White;
                            var colorSymbolCol = ColorCode.Keys.Any(data.SymbolCol.EndsWith) ? ColorCode[data.SymbolCol.Substring(data.SymbolCol.Length - 3)] : Color.White;

                            // Erase the color code at the string end
                            FirstCol = ColorCode.Keys.Any(FirstCol.EndsWith) ? FirstCol.Substring(0, FirstCol.Length - 3) : FirstCol;
                            LastCol = ColorCode.Keys.Any(LastCol.EndsWith) ? LastCol.Substring(0, LastCol.Length - 3) : LastCol;
                            keyPressed = ColorCode.Keys.Any(data.KeyPressed.EndsWith) ? data.KeyPressed.Substring(0, data.KeyPressed.Length - 3) : data.KeyPressed;
                            SymbolCol = ColorCode.Keys.Any(data.SymbolCol.EndsWith) ? data.SymbolCol.Substring(0, data.SymbolCol.Length - 3) : data.SymbolCol;

                            // Apply color to FirstCol
                            if (standardHUDMode)
                            {   // Apply color to FirstCol
                                hbox.Add(indicator = new Label(TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Center));
                                indicator.Color = colorKeyPressed;
                                hbox.Add(indicator = new Label(colWidth, hbox.RemainingHeight, FirstCol));
                                indicator.Color = colorFirstColEndsWith;
                            }
                            else
                            {   // Use constant width font
                                hbox.Add(indicator = new Label(TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Center));
                                indicator.Color = colorKeyPressed;
                                hbox.Add(indicatorMono = new LabelMono(colWidth, hbox.RemainingHeight, FirstCol));
                                indicatorMono.Color = colorFirstColEndsWith;
                            }

                            if (!string.IsNullOrEmpty(data.KeyPressed))
                            {
                                hbox.Add(indicator = new Label(-TextSize, 0, TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Right));
                                indicator.Color = colorKeyPressed;
                            }

                            if (!string.IsNullOrEmpty(data.SymbolCol))
                            {
                                hbox.Add(indicator = new Label(-(TextSize + 3), 0, TextSize, hbox.RemainingHeight, SymbolCol, LabelAlignment.Right));
                                indicator.Color = colorSymbolCol;
                            }

                            // Apply color to LastCol
                            hbox.Add(indicator = new Label(colWidth, hbox.RemainingHeight, LastCol));
                            indicator.Color = colorFirstColEndsWith == Color.White ? colorLastColEndsWith : colorFirstColEndsWith;
                        }
                        else
                        {   // blanck space
                            keyPressed = "";
                            hbox.Add(indicator = new Label(TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Center));
                            indicator.Color = Color.White; // Default color

                            //Avoids troubles when the Main Scale (Windows DPI settings) is not set to 100%
                            if (LastCol.Contains(':', StringComparison.OrdinalIgnoreCase)) TimeHboxPositionY = hbox.Position.Y;

                            if (standardHUDMode)
                            {
                                hbox.Add(indicator = new Label(colWidth, hbox.RemainingHeight, FirstCol));
                                indicator.Color = Color.White; // Default color
                            }
                            else
                            {
                                hbox.Add(indicatorMono = new LabelMono(colWidth, hbox.RemainingHeight, FirstCol));
                                indicatorMono.Color = Color.White; // Default color
                            }

                            // Font to bold, clickable label
                            if (hbox.Position.Y == TimeHboxPositionY && LastCol.Contains(':', StringComparison.OrdinalIgnoreCase)) // Time line.
                            {
                                hbox.Add(LabelFontToBold = new Label(Owner.TextFontDefault.MeasureString(LastCol) - (standardHUDMode ? 5 : 3), hbox.RemainingHeight, LastCol));
                                LabelFontToBold.Color = Color.White;
                                LabelFontToBold.Click += new Action<Control, Point>(FontToBold_Click);
                            }
                            else
                            {
                                hbox.Add(indicator = new Label(colWidth, hbox.RemainingHeight, LastCol));
                                indicator.Color = Color.White; // Default color
                            }
                        }

                        // Clickable symbol
                        if (hbox.Position.Y == TimeHboxPositionY)
                        {
                            char expandWindow = !standardHUDMode ? '\u25C4' : '\u25BA';// ◀ : ▶
                            hbox.Add(ExpandWindow = new Label(hbox.RemainingWidth - TextSize, 0, TextSize, hbox.RemainingHeight, expandWindow.ToString(), LabelAlignment.Right));
                            ExpandWindow.Color = Color.Yellow;
                            ExpandWindow.Click += new Action<Control, Point>(ExpandWindow_Click);
                        }
                        // Separator line
                        if (data.FirstCol.Contains("Sprtr", StringComparison.OrdinalIgnoreCase))
                        {
                            hbox.AddHorizontalSeparator();
                        }
                    }
                }
            }// close
            return vbox;
        }

        private void FontToBold_Click(Control arg1, Point arg2)
        {
            FontChanged = true;
            FontToBold = !FontToBold;
            UpdateWindowSize();
        }

        private void ExpandWindow_Click(Control arg1, Point arg2)
        {
            CycleMode();
        }

        public override void TabAction()
        {
            CycleMode();
        }

        /// <summary>
        /// Change between full and abbreviated text mode.
        /// </summary>
        public void CycleMode()
        {
            standardHUDMode = !standardHUDMode;
            UpdateWindowSize();
        }

        private void UpdateWindowSize()
        {
            UpdateData();
            ModifyWindowSize();
        }

        /// <summary>
        /// Modify window size
        /// </summary>
        private void ModifyWindowSize()
        {
            if (listToLabel.Count > 0)
            {
                var textwidth = Owner.TextFontDefault.Height;
                FirstColLenght = listToLabel.Max(x => x.FirstColWidth);
                LastColLenght = listToLabel.Max(x => x.LastColWidth);

                // Valid rows
                // Validates rows with windows DPI settings
                var dpiOffset = (System.Drawing.Graphics.FromHwnd(IntPtr.Zero).DpiY / 96) > 1.00f ? 1 : 0;// values from testing
                var rowCount = listToLabel.Where(x => !string.IsNullOrWhiteSpace(x.FirstCol.ToString()) || !string.IsNullOrWhiteSpace(x.LastCol.ToString())).Count() - dpiOffset + 1;
                var desiredHeight = FontToBold ? Owner.TextFontDefaultBold.Height * rowCount
                    : Owner.TextFontDefault.Height * rowCount;

                var desiredWidth = FirstColLenght + LastColLenght + 45;// interval between firstcol and lastcol

                var newHeight = (int)MathHelper.Clamp(desiredHeight, (standardHUDMode ? WindowHeightMin : 100), WindowHeightMax);
                var newWidth = (int)MathHelper.Clamp(desiredWidth, (standardHUDMode ? WindowWidthMin : 100), WindowWidthMax);

                // Move the dialog up if we're expanding it, or down if not; this keeps the center in the same place.
                var newTop = Location.Y + (Location.Height - newHeight) / 2;

                // Display window
                SizeTo(newWidth, newHeight);
                MoveTo(Location.X, newTop);
            }
        }

        /// <summary>
        /// Display info according to the full text window or the slim text window
        /// </summary>
        /// <param name="firstkeyactivated"></param>
        /// <param name="firstcol"></param>
        /// <param name="lastcol"></param>
        /// <param name="symbolcol"></param>
        /// <param name="changecolwidth"></param>
        /// <param name="lastkeyactivated"></param>
        private void InfoToLabel(string keyPressed, string firstcol, string lastcol, string symbolcol, bool changecolwidth)
        {
            if (!UpdateDataEnded)
            {
                if (!standardHUDMode)
                {
                    foreach (var code in FirstColToAbbreviated)
                    {
                        if (firstcol.Contains(code.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            firstcol = firstcol.Replace(code.Key, code.Value, StringComparison.OrdinalIgnoreCase).TrimEnd();
                        }
                    }
                    foreach (var code in LastColToAbbreviated)
                    {
                        if (lastcol.Contains(code.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            lastcol = lastcol.Replace(code.Key, code.Value, StringComparison.OrdinalIgnoreCase).TrimEnd();
                        }
                    }
                }

                var firstColWidth = 0;
                var lastColWidth = 0;

                if (!firstcol.Contains("Sprtr", StringComparison.OrdinalIgnoreCase))
                {

                    if (ColorCode.Keys.Any(firstcol.EndsWith))
                    {
                        var tempFirstCol = firstcol.Substring(0, firstcol.Length - 3);
                        firstColWidth = FontToBold ? Owner.TextFontDefaultBold.MeasureString(tempFirstCol.TrimEnd())
                            : !standardHUDMode ? Owner.TextFontMonoSpacedBold.MeasureString(tempFirstCol.TrimEnd())
                            : Owner.TextFontDefault.MeasureString(tempFirstCol.TrimEnd());
                    }
                    else
                    {
                        firstColWidth = FontToBold ? Owner.TextFontDefaultBold.MeasureString(firstcol.TrimEnd())
                            : !standardHUDMode ? Owner.TextFontMonoSpacedBold.MeasureString(firstcol.TrimEnd())
                            : Owner.TextFontDefault.MeasureString(firstcol.TrimEnd());
                    }

                    if (ColorCode.Keys.Any(lastcol.EndsWith))
                    {
                        var tempLastCol = lastcol.Substring(0, lastcol.Length - 3);
                        lastColWidth = FontToBold ? Owner.TextFontDefaultBold.MeasureString(tempLastCol.TrimEnd())
                            : Owner.TextFontDefault.MeasureString(tempLastCol.TrimEnd());
                    }
                    else
                    {
                        lastColWidth = FontToBold ? Owner.TextFontDefaultBold.MeasureString(lastcol.TrimEnd())
                            : Owner.TextFontDefault.MeasureString(lastcol.TrimEnd());
                    }

                    //Set a minimum value for LastColWidth to avoid overlap between time value and clickable symbol
                    if (listToLabel.Count == 1)
                    {
                        lastColWidth = listToLabel.First().LastColWidth + 15;// time value + clickable symbol
                    }
                }

                listToLabel.Add(new ListLabel(firstcol, firstColWidth, lastcol, lastColWidth, symbolcol, changecolwidth, keyPressed ?? string.Empty));// avoids the symbol/keypressed from overlapping with the text,

                //ResizeWindow, when the string spans over the right boundary of the window
                if (!ResizeWindow)
                {
                    if (maxFirstColWidth < firstColWidth)
                        FirstColOverFlow = maxFirstColWidth;
                    if (maxLastColWidth < lastColWidth)
                        LastColOverFlow = maxLastColWidth;
                    ResizeWindow = true;
                }
            }
            else
            {
                // Detect Autopilot is on to avoid flickering when slim window is displayed
                var AutopilotOn = Owner.Viewer.PlayerLocomotive.Train.TrainType == TrainType.AiPlayerHosting ? true : false;

                //ResizeWindow, when the string spans over the right boundary of the window
                maxFirstColWidth = listToLabel.Max(x => x.FirstColWidth);
                maxLastColWidth = listToLabel.Max(x => x.LastColWidth);

                if (!ResizeWindow & (FirstColOverFlow != maxFirstColWidth || (!AutopilotOn && LastColOverFlow != maxLastColWidth)))
                {
                    LastColOverFlow = maxLastColWidth;
                    FirstColOverFlow = maxFirstColWidth;
                    ResizeWindow = true;
                }
            }
        }

        private void UpdateData()
        {   //Update data

            var PlayerTrain = Owner.Viewer.PlayerLocomotive.Train;
            var BrakeStatus = Owner.Viewer.PlayerLocomotive.GetTrainBrakeStatus();
            var DynamicBrakePercent = Owner.Viewer.PlayerLocomotive.DynamicBrakePercent;
            var DynamicBrakeStatus = Owner.Viewer.PlayerLocomotive.GetDynamicBrakeStatus();
            var EngineBrakeStatus = Owner.Viewer.PlayerLocomotive.GetEngineBrakeStatus();
            var Locomotive = Owner.Viewer.PlayerLocomotive as MSTSLocomotive;
            var LocomotiveDebugStatus = Owner.Viewer.PlayerLocomotive.GetDebugStatus();
            var LocomotiveStatus = Owner.Viewer.PlayerLocomotive.GetStatus();
            var LocomotiveSteam = Owner.Viewer.PlayerLocomotive as MSTSSteamLocomotive;
            var CombinedCT = Locomotive.CombinedControlType == MSTSLocomotive.CombinedControl.ThrottleDynamic;
            var ShowMUReverser = Math.Abs(PlayerTrain.MUReverserPercent) != 100;
            var ShowRetainers = PlayerTrain.RetainerSetting != RetainerSetting.Exhaust;
            var Stretched = PlayerTrain.Cars.Count > 1 && PlayerTrain.CouplersPulled == PlayerTrain.Cars.Count - 1;
            var Bunched = !Stretched && PlayerTrain.Cars.Count > 1 && PlayerTrain.CouplersPushed == PlayerTrain.Cars.Count - 1;
            var ThisInfo = Owner.Viewer.PlayerTrain.GetTrainInfo();
            expandWindow = '\u23FA';// ⏺ toggle window

            listToLabel.Clear();
            UpdateDataEnded = false;

            if (!standardHUDMode)
            {
                var newBrakeStatus = new StringBuilder(BrakeStatus);
                BrakeStatus = newBrakeStatus
                      .Replace(Viewer.Catalog.GetString("bar"), string.Empty)
                      .Replace(Viewer.Catalog.GetString("inHg"), string.Empty)
                      .Replace(Viewer.Catalog.GetString("kgf/cm²"), string.Empty)
                      .Replace(Viewer.Catalog.GetString("kPa"), string.Empty)
                      .Replace(Viewer.Catalog.GetString("psi"), string.Empty)
                      .Replace(Viewer.Catalog.GetString("lib./pal."), string.Empty)//cs locales
                      .Replace(Viewer.Catalog.GetString("pal.rtuti"), string.Empty)
                      .ToString();
            }

            // First Block
            // Client and server may have a time difference.
            if (MultiPlayerManager.MultiplayerState == MultiplayerState.Client)
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Time"), FormatStrings.FormatTime(Owner.Viewer.Simulator.ClockTime + MultiPlayerManager.Instance().serverTimeDifference), "", false);
            else
            {
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Time"), FormatStrings.FormatTime(Owner.Viewer.Simulator.ClockTime), "", false);
            }
            if (Owner.Viewer.Simulator.IsReplaying)
            {
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Replay"), FormatStrings.FormatTime(Owner.Viewer.Log.ReplayEndsAt - Owner.Viewer.Simulator.ClockTime), "", false);
            }

            InfoToLabel(string.Empty, Viewer.Catalog.GetString("Speed"),
                FormatStrings.FormatSpeedDisplay(Owner.Viewer.PlayerLocomotive.SpeedMpS, Owner.Viewer.PlayerLocomotive.IsMetric) +
                (ThisInfo.Speed < ThisInfo.AllowedSpeed - 1.0f ? "!??" :        // White
                ThisInfo.Speed < ThisInfo.AllowedSpeed + 0.0f ? "?!!" :         // PaleGreen
                ThisInfo.Speed < ThisInfo.AllowedSpeed + 5.0f ? "!!?" : "!!!"), "", false);// Orange : Red

            // Gradient info
            if (standardHUDMode)
            {
                if (-ThisInfo.Gradient < -0.00015)
                {
                    var c = '\u2198';
                    Gradient = $"{-ThisInfo.Gradient:F1}%{c}$$$";
                }
                else if (-ThisInfo.Gradient > 0.00015)
                {
                    var c = '\u2197';
                    Gradient = $"{-ThisInfo.Gradient:F1}%{c}???";
                }
                else Gradient = $"{-ThisInfo.Gradient:F1}%";

                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Gradient"), Gradient, "", false);
            }

            // Separator
            InfoToLabel(string.Empty, "Sprtr", "", "", false);

            // Second block
            // Direction
            if (string.Equals(directionKeyInput, arrowUp, StringComparison.OrdinalIgnoreCase) || string.Equals(directionKeyInput, arrowDown, StringComparison.OrdinalIgnoreCase))
            {
                InfoToLabel(directionKeyInput, Owner.Viewer.PlayerLocomotive.EngineType == TrainCar.EngineTypes.Steam ? Viewer.Catalog.GetString("Reverser") : Viewer.Catalog.GetString("Direction"),
                    (ShowMUReverser ? $"{Math.Abs(PlayerTrain.MUReverserPercent):0}% " : string.Empty) + Owner.Viewer.PlayerLocomotive.Direction.GetLocalizedDescription(), "", false);
            }
            // Throttle
            InfoToLabel(throttleKeyInput, Owner.Viewer.PlayerLocomotive is MSTSSteamLocomotive ? Viewer.Catalog.GetString("Regulator") : Viewer.Catalog.GetString("Throttle"),
                $"{Owner.Viewer.PlayerLocomotive.ThrottlePercent:0}%", "", false);

            // Cylinder Cocks
            if (Owner.Viewer.PlayerLocomotive is MSTSSteamLocomotive mstsSteamLocomotive)
            {
                if (mstsSteamLocomotive.CylinderCocksAreOpen)
                    cylinderCocksInput = arrowToRight;
                InfoToLabel(cylinderCocksInput, Viewer.Catalog.GetString("Cylinder cocks"), mstsSteamLocomotive.CylinderCocksAreOpen ? Viewer.Catalog.GetString("Open") + "!!?" : Viewer.Catalog.GetString("Closed") + "!??", "", false);
            }

            // Sander
            if (Owner.Viewer.PlayerLocomotive.GetSanderOn())
            {
                bool sanderBlocked = Owner.Viewer.PlayerLocomotive is MSTSLocomotive locomotive && Math.Abs(PlayerTrain.SpeedMpS) > locomotive.SanderSpeedOfMpS;
                if (sanderBlocked)
                {
                    InfoToLabel(sanderInput, Viewer.Catalog.GetString("Sander"), Viewer.Catalog.GetString("Blocked") + "!!!", "", standardHUDMode);
                }
                else
                {
                    sanderInput = arrowToRight;
                    InfoToLabel(sanderInput, Viewer.Catalog.GetString("Sander"), Viewer.Catalog.GetString("On") + "!!?", "", standardHUDMode);
                }
            }
            else
            {
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Sander"), "Off", "", false);
            }

            InfoToLabel(string.Empty, "Sprtr", "", "", false);

            // Train Brake multi-lines
            // TO DO: A better algorithm
            //var brakeStatus = Owner.Viewer.PlayerLocomotive.GetTrainBrakeStatus();
            //steam loco

            string brakeInfoValue;
            int index;

            if (BrakeStatus.Contains(Viewer.Catalog.GetString("EQ"), StringComparison.OrdinalIgnoreCase))
            {
                brakeInfoValue = BrakeStatus.Substring(0, BrakeStatus.IndexOf(Viewer.Catalog.GetString("EQ"), StringComparison.OrdinalIgnoreCase)).TrimEnd();
                InfoToLabel(trainBrakeInput, Viewer.Catalog.GetString("Train brake"), brakeInfoValue + "$??", "", false);
                trainBrakeInput = "";
                index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("EQ"), StringComparison.OrdinalIgnoreCase);
                brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase) - index).TrimEnd();

                InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);

                if (BrakeStatus.Contains(Viewer.Catalog.GetString("EOT"), StringComparison.OrdinalIgnoreCase))
                {
                    var IndexOffset = Viewer.Catalog.GetString("EOT").Length + 1;
                    index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase);
                    brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT"), StringComparison.OrdinalIgnoreCase) - index).TrimEnd();

                    InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);

                    index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT"), StringComparison.OrdinalIgnoreCase) + IndexOffset;
                    brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.Length - index).TrimStart();

                    InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);
                }
                else
                {
                    index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase);
                    brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.Length - index).TrimEnd();
                    trainBrakeInput = "";
                    InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);
                }
            }
            else if (BrakeStatus.Contains(Viewer.Catalog.GetString("Lead"), StringComparison.OrdinalIgnoreCase))
            {
                var IndexOffset = Viewer.Catalog.GetString("Lead").Length + 1;
                brakeInfoValue = BrakeStatus.Substring(0, BrakeStatus.IndexOf(Viewer.Catalog.GetString("Lead"), StringComparison.OrdinalIgnoreCase)).TrimEnd();
                InfoToLabel(trainBrakeInput, Viewer.Catalog.GetString("Train brake"), brakeInfoValue + "$??", "", false);

                trainBrakeInput = "";
                index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("Lead"), StringComparison.OrdinalIgnoreCase) + IndexOffset;
                if (BrakeStatus.Contains(Viewer.Catalog.GetString("EOT"), StringComparison.OrdinalIgnoreCase))
                {
                    brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT"), StringComparison.OrdinalIgnoreCase) - index).TrimEnd();
                    InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);

                    index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT"), StringComparison.OrdinalIgnoreCase) + IndexOffset;
                    brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.Length - index).TrimEnd();
                    InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);
                }
                else
                {
                    brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.Length - index).TrimEnd();
                    InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);
                }
            }
            else if (BrakeStatus.Contains(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase))
            {
                brakeInfoValue = BrakeStatus.Substring(0, BrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase)).TrimEnd();
                InfoToLabel(trainBrakeInput, Viewer.Catalog.GetString("Train brake"), brakeInfoValue + "$??", "", false);

                trainBrakeInput = "";
                index = BrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase);
                brakeInfoValue = BrakeStatus.Substring(index, BrakeStatus.Length - index).TrimEnd();

                InfoToLabel(trainBrakeInput, "", brakeInfoValue, "", false);
                trainBrakeInput = "";
            }

            if (ShowRetainers)
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Retainers"), (PlayerTrain.RetainerPercent + " " + PlayerTrain.RetainerSetting.GetLocalizedDescription()), "", false);

            if ((Owner.Viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeFitted) // ideally this test should be using "engineBrakeStatus != null", but this currently does not work, as a controller is defined by default
            {
            }

            if (EngineBrakeStatus.Contains(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase))
            {
                InfoToLabel(engineBrakeInput, Viewer.Catalog.GetString("Engine brake"), string.Concat(EngineBrakeStatus.AsSpan(0, EngineBrakeStatus.IndexOf("BC", StringComparison.OrdinalIgnoreCase)), "$??"), "", false);
                index = EngineBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"), StringComparison.OrdinalIgnoreCase);
                brakeInfoValue = EngineBrakeStatus.Substring(index, EngineBrakeStatus.Length - index).TrimEnd();
                InfoToLabel(string.Empty, Viewer.Catalog.GetString(""), brakeInfoValue + "!??", "", false);
            }
            else
                InfoToLabel(engineBrakeInput, Viewer.Catalog.GetString("Engine brake"), EngineBrakeStatus + "$??", "", false);

            if (DynamicBrakeStatus != null && Locomotive.IsLeadLocomotive())
            {
                if (!DynBrakeSetup && (dynamicBrakeIncreaseDown && DynamicBrakePercent == 0)
                    || (CombinedCT && throttleDecreaseDown && Owner.Viewer.PlayerLocomotive.ThrottlePercent == 0 && DynamicBrakeStatus == "0%"))
                {
                    StartTime = Locomotive.DynamicBrakeCommandStartTime + Locomotive.DynamicBrakeDelayS;
                    DynBrakeSetup = true;
                    InfoToLabel(arrowToRight, Viewer.Catalog.GetString("Dynamic brake"), Viewer.Catalog.GetString("Setup") + "$??", "", false);
                }
                else if (DynBrakeSetup && StartTime < Owner.Viewer.Simulator.ClockTime)
                {
                    DynBrakeSetup = false;
                    InfoToLabel(string.Empty, Viewer.Catalog.GetString("Dynamic brake"), DynamicBrakePercent + "% " + "$??", "", false);
                }
                else if (DynBrakeSetup && StartTime > Owner.Viewer.Simulator.ClockTime)
                {
                    InfoToLabel(arrowToRight, Viewer.Catalog.GetString("Dynamic brake"), Viewer.Catalog.GetString("Setup") + "$??", "", false);
                }
                else if (!DynBrakeSetup && DynamicBrakePercent > -1)
                {
                    string dynamicBrake;
                    if (CombinedCT)
                    {
                        dynamicBrake = throttleIncreaseDown || dynamicBrakeDecreaseDown ? arrowDown :
                            throttleDecreaseDown || dynamicBrakeIncreaseDown ? arrowUp :
                            string.Empty;
                    }
                    else
                    {
                        dynamicBrake = dynamicBrakeDecreaseDown ? arrowDown : dynamicBrakeIncreaseDown ? arrowUp : string.Empty;
                    }
                    InfoToLabel(dynamicBrake, Viewer.Catalog.GetString("Dynamic brake"), DynamicBrakeStatus + "$??", "", false);
                }
                else if (DynamicBrakeStatus.Length == 0 && DynamicBrakePercent < 0)
                {
                    InfoToLabel(string.Empty, Viewer.Catalog.GetString("Dynamic brake"), Viewer.Catalog.GetString("Off"), "", false);
                }
            }

            InfoToLabel(string.Empty, "Sprtr", "", "", false);

            if (LocomotiveStatus != null)
            {
                var lines = LocomotiveStatus.Split('\n');
                foreach (var data in lines)
                {
                    if (data.Length > 0)
                    {
                        var parts = data.Split(new[] { " = " }, 2, StringSplitOptions.None);
                        var HeatColor = "!??"; // Color.White
                        if (!standardHUDMode && Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Steam usage"), StringComparison.OrdinalIgnoreCase))
                        {
                        }
                        else if (Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Boiler pressure"), StringComparison.OrdinalIgnoreCase))
                        {
                            MSTSSteamLocomotive steamloco = (MSTSSteamLocomotive)Owner.Viewer.PlayerLocomotive;
                            var bandUpper = steamloco.PreviousBoilerHeatOutBTUpS * 1.025f; // find upper bandwidth point
                            var bandLower = steamloco.PreviousBoilerHeatOutBTUpS * 0.975f; // find lower bandwidth point - gives a total 5% bandwidth

                            if (steamloco.BoilerHeatInBTUpS > bandLower && steamloco.BoilerHeatInBTUpS < bandUpper)
                                HeatColor = smallDiamond.ToString() + "!??";
                            else if (steamloco.BoilerHeatInBTUpS < bandLower)
                                HeatColor = smallArrowDown.ToString() + "$??"; // Color.Cyan
                            else if (steamloco.BoilerHeatInBTUpS > bandUpper)
                                HeatColor = smallArrowUp.ToString() + "!!?"; // Color.Orange

                            InfoToLabel(string.Empty, Viewer.Catalog.GetString("Boiler pressure"), Viewer.Catalog.GetString(parts[1]), HeatColor, false);
                        }
                        else if (!standardHUDMode && Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Fuel levels"), StringComparison.OrdinalIgnoreCase))
                        {
                            InfoToLabel(string.Empty, parts[0].EndsWith("?", StringComparison.OrdinalIgnoreCase) || parts[0].EndsWith("!", StringComparison.OrdinalIgnoreCase) ? Viewer.Catalog.GetString(parts[0].Substring(0, parts[0].Length - 3)) : Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1].Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)) : ""), "", false);
                        }
                        else if (parts[0].StartsWith(Viewer.Catalog.GetString("Gear"), StringComparison.OrdinalIgnoreCase))
                        {
                            InfoToLabel(gearKeyInput, Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) : ""), "", false);
                        }
                        else if (parts.Contains(Viewer.Catalog.GetString("Pantographs")))
                        {
                            string pantograph = pantographKeyDown ? parts[1].StartsWith(Viewer.Catalog.GetString("Up"), StringComparison.OrdinalIgnoreCase) ? arrowUp : arrowDown : "";
                            InfoToLabel(pantograph, Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) : ""), "", false);
                        }
                        else if (parts.Contains(Viewer.Catalog.GetString("Engine")))
                        {
                            InfoToLabel(string.Empty, Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) + "!??" : ""), "", false);
                        }
                        else
                        {
                            InfoToLabel(string.Empty, parts[0].EndsWith("?", StringComparison.OrdinalIgnoreCase) || parts[0].EndsWith("!", StringComparison.OrdinalIgnoreCase) ? Viewer.Catalog.GetString(parts[0].Substring(0, parts[0].Length - 3)) : Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) : ""), "", false);
                        }
                    }
                }
            }

            InfoToLabel(string.Empty, "Sprtr", "", "", true);

            if (standardHUDMode)
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("FPS"), $"{Owner.Viewer.RenderProcess.FrameRate.SmoothedValue:F0}", string.Empty, false);

            // Messages
            // Autopilot
            if (Owner.Viewer.PlayerLocomotive.Train.TrainType == TrainType.AiPlayerHosting)
            {
                InfoToLabel(autoPilotKeyDown ? arrowUp : "", Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("On") + "???", "", false);
            }
            else if (Owner.Viewer.PlayerLocomotive.Train.TrainType != TrainType.AiPlayerHosting)
            {
                InfoToLabel(autoPilotKeyDown ? arrowDown : "", Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("Off"), "", false);
            }
            else
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("Off"), "", false);

            //AI Fireman
            if (Locomotive is MSTSSteamLocomotive steamLocomotive)
            {
                string aiFireInput = arrowToRight;
                if (aiFireOnKeyDown)
                {
                    ctrlAIFiremanReset = ctrlAIFiremanOff = false;
                    ctrlAIFiremanOn = true;
                }
                else if (aiFireOffKeyDown)
                {
                    ctrlAIFiremanReset = ctrlAIFiremanOn = false;
                    ctrlAIFiremanOff = true;
                }
                else if (aiFireResetKeyDown)
                {
                    ctrlAIFiremanOn = ctrlAIFiremanOff = false;
                    ctrlAIFiremanReset = true;
                    clockAIFireTime = Owner.Viewer.Simulator.ClockTime;
                }
                else
                    aiFireInput = string.Empty;

                if (ctrlAIFiremanOn || ctrlAIFiremanOff || ctrlAIFiremanReset)
                {
                    InfoToLabel(aiFireInput, Viewer.Catalog.GetString("AI Fireman") + "!??",
                        ctrlAIFiremanOn ? Viewer.Catalog.GetString("On") + "!??" :
                        ctrlAIFiremanOff ? Viewer.Catalog.GetString("Off") + "!??" :
                        ctrlAIFiremanReset ? Viewer.Catalog.GetString("Reset") + "%%%" : "-", "", false);
                }

                // Delay to hide the Reset label
                if (ctrlAIFiremanReset && clockAIFireTime + 3 < Owner.Viewer.Simulator.ClockTime)
                    ctrlAIFiremanReset = false;

                // Grate limit
                if (steamLocomotive.GrateCombustionRateLBpFt2 > steamLocomotive.GrateLimitLBpFt2)
                {
                    if (steamLocomotive.IsGrateLimit)
                    {
                        grateLabelVisible = true;
                        clockGrateTime = Owner.Viewer.Simulator.ClockTime;
                        InfoToLabel(string.Empty, Viewer.Catalog.GetString("Grate limit"), Viewer.Catalog.GetString("Exceeded") + "!!!", "", false);
                    }
                }
                else
                {
                    // delay to hide the grate label
                    if (grateLabelVisible && clockGrateTime + 3 < Owner.Viewer.Simulator.ClockTime)
                        grateLabelVisible = false;
                    if (grateLabelVisible)
                    {

                        InfoToLabel(string.Empty, Viewer.Catalog.GetString("Grate limit") + "!??", Viewer.Catalog.GetString("Normal") + "!??", "", false);
                    }
                }
            }

            // Wheel
            if (PlayerTrain.IsWheelSlip || PlayerTrain.IsWheelSlipWarninq || PlayerTrain.IsBrakeSkid)
            {
                wheelLabelVisible = true;
                clockWheelTime = Owner.Viewer.Simulator.ClockTime;
            }
            if (Owner.Viewer.PlayerTrain.IsWheelSlip)
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("slip") + "!!!", "", false);
            else if (Owner.Viewer.PlayerTrain.IsWheelSlipWarninq)
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("slip warning") + "???", "", false);
            else if (Owner.Viewer.PlayerTrain.IsBrakeSkid)
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("skid") + "!!!", "", false);
            else
            {
                // delay to hide the wheel label
                if (wheelLabelVisible && clockWheelTime + 3 < Owner.Viewer.Simulator.ClockTime)
                    wheelLabelVisible = false;

                if (wheelLabelVisible)
                {
                    InfoToLabel(string.Empty, Viewer.Catalog.GetString("Wheel") + "?!?", Viewer.Catalog.GetString("Normal") + "!??", "", false);
                }
            }

            //Derailment Coefficient. Changed the float value output by a text label.
            var carIDerailCoeff = "";
            var carDerailPossible = false;
            var carDerailExpected = false;

            for (var i = 0; i < PlayerTrain.Cars.Count; i++)
            {
                var carDerailCoeff = PlayerTrain.Cars[i].DerailmentCoefficient;
                carDerailCoeff = float.IsInfinity(carDerailCoeff) || float.IsNaN(carDerailCoeff) ? 0 : carDerailCoeff;

                carIDerailCoeff = PlayerTrain.Cars[i].CarID;

                // Only record the first car that has derailed, stop looking for other derailed cars
                carDerailExpected = PlayerTrain.Cars[i].DerailExpected;
                if (carDerailExpected)
                {
                    break;
                }

                // Only record first instance of a possible car derailment (warning)
                if (PlayerTrain.Cars[i].DerailPossible && !carDerailPossible)
                {
                    carDerailPossible = PlayerTrain.Cars[i].DerailPossible;
                }
            }

            if (carDerailPossible || carDerailExpected)
            {
                derailLabelVisible = true;
                clockDerailTime = Owner.Viewer.Simulator.ClockTime;
            }

            // The most extreme instance of the derail coefficient will only be displayed in the TDW
            if (carDerailExpected)
            {
                InfoToLabel(string.Empty, Viewer.Catalog.GetString("DerailCoeff"), $"{Viewer.Catalog.GetString("Derailed")} {carIDerailCoeff}" + "!!!", "", false);
            }
            else if (carDerailPossible)
            {
                    InfoToLabel(string.Empty, Viewer.Catalog.GetString("DerailCoeff"), $"{Viewer.Catalog.GetString("Warning")} {carIDerailCoeff}" + "???", "", false);
            }
            else
            {
                // delay to hide the derailcoeff label if normal
                if (derailLabelVisible && clockDerailTime + 3 < Owner.Viewer.Simulator.ClockTime)
                    derailLabelVisible = false;

                if (derailLabelVisible)
                {
                    InfoToLabel(string.Empty, Viewer.Catalog.GetString("DerailCoeff"), $"{Viewer.Catalog.GetString("Normal")} {carIDerailCoeff}" + "!??", "", false);
                }
            }

            // Doors
            if ((Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorLeftOpen || (Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorRightOpen)
            {
                var color = Math.Abs(Owner.Viewer.PlayerLocomotive.SpeedMpS) > 0.1f ? "!!!" : "???";
                var status = "";
                doorsLabelVisible = true;
                clockDoorsTime = Owner.Viewer.Simulator.ClockTime;

                if ((Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorLeftOpen)
                    status += (Owner.Viewer.PlayerLocomotive as MSTSLocomotive).GetCabFlipped() ? Viewer.Catalog.GetString("Right") : Viewer.Catalog.GetString("Left");
                if ((Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorRightOpen)
                {
                    if (status.Length > 0)
                        status += " ";
                    status += (Owner.Viewer.PlayerLocomotive as MSTSLocomotive).GetCabFlipped() ? Viewer.Catalog.GetString("Left") : Viewer.Catalog.GetString("Right");
                    status += color;
                }
                InfoToLabel(" ", Viewer.Catalog.GetString("Doors open"), status, "", false);
            }
            else
            {
                // delay to hide the doors label
                if (doorsLabelVisible && clockDoorsTime + 3 < Owner.Viewer.Simulator.ClockTime)
                    doorsLabelVisible = false;

                if (doorsLabelVisible)
                {
                    InfoToLabel(" ", Viewer.Catalog.GetString("Doors open") + "!??", Viewer.Catalog.GetString("Closed"), "", false);
                }
            }

            // Ctrl + F Firing to manual
            if (firingKeyDown)
            {
                ResizeWindow = true;
            }

            UpdateDataEnded = true;
            InfoToLabel(string.Empty, "", "", "", true);

            throttleKeyInput = string.Empty;
            throttleDecreaseDown = false;
            throttleIncreaseDown = false;
            dynamicBrakeDecreaseDown = false;
            dynamicBrakeIncreaseDown = false;
            gearKeyInput = string.Empty;
            pantographKeyDown = false;
            autoPilotKeyDown = false;
            firingKeyDown = false;
            cylinderCocksInput = string.Empty;
            sanderInput = " ";
            engineBrakeInput = string.Empty;
            directionKeyInput = string.Empty;
            trainBrakeInput = string.Empty;
            aiFireOnKeyDown = false;
            aiFireOffKeyDown = false;
            aiFireResetKeyDown = false;
        }

        public override void PrepareFrame(in ElapsedTime elapsedTime, bool updateFull)
        {
            base.PrepareFrame(elapsedTime, updateFull);

            // Avoid to updateFull when the window is moving
            if (!dragged & updateFull)
            {
                UpdateData();

                // Ctrl + F (FiringIsManual)
                if (ResizeWindow || LinesCount != listToLabel.Count)
                {
                    ResizeWindow = false;
                    UpdateWindowSize();
                    LinesCount = listToLabel.Count;
                }
                //Resize this window after the font has been changed externally
                if (MultiPlayerWindow.FontChanged)
                {
                    MultiPlayerWindow.FontChanged = false;
                    FontToBold = !FontToBold;
                    UpdateWindowSize();
                }
                //Update Layout
                Layout();
            }
        }
    }
}