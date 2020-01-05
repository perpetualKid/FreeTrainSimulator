// COPYRIGHT 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

using Microsoft.Xna.Framework.Graphics;
using Orts.Simulation.Physics;
using ORTS.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks;
using System.Text;
using ORTS.Common.Input;
using Microsoft.Xna.Framework;
using System.Threading;
using System.Globalization;

namespace Orts.Viewer3D.Popups
{
    public class TrainDrivingWindow : Window
    {
        int FirstColLenght = 0;
        int LastColLenght = 0;
        int LinesCount;
        bool ResizeWindow;                
        public bool StandardHUD = true;// Standard text

        int WindowHeightMin = 0;
        int WindowHeightMax = 0;
        int WindowWidthMin = 0;
        int WindowWidthMax = 0;

        char expandWindow;
        string keyPressed;// display a symbol when a control key is pressed.
        string Gradient;
        public int OffSetX = 0;
        const int TextSize = 15;
        public int keyPresLenght;

        Label indicator;
        LabelMono indicatorMono;
        Label ExpandWindow;
        Label LabelFontToBold;
        public static bool MonoFont;
        public static bool FontToBold;

        public struct ListLabel
        {
            public string FirstCol { get; set; }
            public string LastCol { get; set; }
            public string SymbolCol { get; set; }
            public bool ChangeColWidth { get; set; }
            public string keyPressed { get; set; }
        }
        List<ListLabel> ListToLabel = new List<ListLabel>();

        // Change text color
        readonly Dictionary<string, Color> ColorCode = new Dictionary<string, Color>
        {
            { "!!!", Color.OrangeRed },
            { "!!?", Color.Orange },
            { "!??", Color.White },
            { "?!?", Color.Black },
            { "???", Color.Yellow },
            { "??!", Color.Green },
            { "?!!", Color.PaleGreen },
            { "$$$", Color.LightSkyBlue},
            { "$??", Color.Cyan}
        };

        readonly Dictionary<string, string> FirstColToAbbreviated = new Dictionary<string, string>()
        {
            {Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("AUTO")},
            {Viewer.Catalog.GetString("Boiler pressure"), Viewer.Catalog.GetString("PRES")},
            {Viewer.Catalog.GetString("Boiler water glass"), Viewer.Catalog.GetString("WATR")},
            {Viewer.Catalog.GetString("Boiler water level"), Viewer.Catalog.GetString("LEVL")},
            {Viewer.Catalog.GetString("Circuit breaker"), Viewer.Catalog.GetString("CIRC")},
            {Viewer.Catalog.GetString("Cylinder cocks"), Viewer.Catalog.GetString("CCOK")},
            {Viewer.Catalog.GetString("Direction"), Viewer.Catalog.GetString("DIRC")},
            {Viewer.Catalog.GetString("Doors open"), Viewer.Catalog.GetString("DOOR")},
            {Viewer.Catalog.GetString("Dynamic brake"), Viewer.Catalog.GetString("BDYN")},
            {Viewer.Catalog.GetString("Engine brake"), Viewer.Catalog.GetString("BLOC")},
            {Viewer.Catalog.GetString("Engine"), Viewer.Catalog.GetString("ENGN")},
            {Viewer.Catalog.GetString("Fire mass"), Viewer.Catalog.GetString("FIRE")},
            {Viewer.Catalog.GetString("Fixed gear"), Viewer.Catalog.GetString("GEAR")},
            {Viewer.Catalog.GetString("Fuel levels"), Viewer.Catalog.GetString("FUEL")},
            {Viewer.Catalog.GetString("Gear"), Viewer.Catalog.GetString("GEAR")},
            {Viewer.Catalog.GetString("Gradient"), Viewer.Catalog.GetString("GRAD")},
            {Viewer.Catalog.GetString("Grate limit"), Viewer.Catalog.GetString("GRAT")},
            {Viewer.Catalog.GetString("Pantographs"), Viewer.Catalog.GetString("PANT")},
            {Viewer.Catalog.GetString("Power"), Viewer.Catalog.GetString("POWR")},
            {Viewer.Catalog.GetString("Regulator"), Viewer.Catalog.GetString("REGL")},
            {Viewer.Catalog.GetString("Replay"), Viewer.Catalog.GetString("RPLY")},
            {Viewer.Catalog.GetString("Retainers"), Viewer.Catalog.GetString("RETN")},
            {Viewer.Catalog.GetString("Reverser"), Viewer.Catalog.GetString("REVR")},
            {Viewer.Catalog.GetString("Sander"), Viewer.Catalog.GetString("SAND")},
            {Viewer.Catalog.GetString("Speed"), Viewer.Catalog.GetString("SPED")},
            {Viewer.Catalog.GetString("Steam usage"), Viewer.Catalog.GetString("STEM")},
            {Viewer.Catalog.GetString("Throttle"), Viewer.Catalog.GetString("THRO")},
            {Viewer.Catalog.GetString("Time"), Viewer.Catalog.GetString("TIME")},
            {Viewer.Catalog.GetString("Train brake"), Viewer.Catalog.GetString("BTRN")},
            {Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("WHEL")},    
        };

        readonly Dictionary<string, string> LastColToAbbreviated = new Dictionary<string, string>()
        {
            { Viewer.Catalog.GetString("apply Service"), Viewer.Catalog.GetString("Apply")},
            {Viewer.Catalog.GetString("Apply Quick"), Viewer.Catalog.GetString("ApplQ")},
            {Viewer.Catalog.GetString("Apply Slow"), Viewer.Catalog.GetString("ApplS")},
            {Viewer.Catalog.GetString("coal"), Viewer.Catalog.GetString("c")},
            {Viewer.Catalog.GetString("Emergency Braking Push Button"), Viewer.Catalog.GetString("EmerBPB")},
            {Viewer.Catalog.GetString("Lap Self"), Viewer.Catalog.GetString("LapS")},
            {Viewer.Catalog.GetString("Minimum Reduction"), Viewer.Catalog.GetString("MRedc")},
            {Viewer.Catalog.GetString("safe range"), Viewer.Catalog.GetString("safe")},
            {Viewer.Catalog.GetString("skid"), Viewer.Catalog.GetString("Skid")},
            {Viewer.Catalog.GetString("slip warning"), Viewer.Catalog.GetString("Warning")},
            {Viewer.Catalog.GetString("slip"), Viewer.Catalog.GetString("Slip")},
            {Viewer.Catalog.GetString("water"), Viewer.Catalog.GetString("w")},
        };

        public TrainDrivingWindow(WindowManager owner)
            : base(owner, Window.DecorationSize.X + (owner.TextFontDefault.Height * ( Orts.MultiPlayer.MPManager.IsMultiPlayer() ? 15 : 9)), Window.DecorationSize.Y + owner.TextFontDefault.Height * (Orts.MultiPlayer.MPManager.IsMultiPlayer() ? 30 : 15), Viewer.Catalog.GetString("Train Driving Info"))
        {
            WindowHeightMin = Location.Height;
            WindowHeightMax = Location.Height + owner.TextFontDefault.Height * 10; // 10 lines
            WindowWidthMin = Location.Width;
            WindowWidthMax = Location.Width + owner.TextFontDefault.Height * 20; ; // 20 char
        }

        protected internal override void Initialize()
        {
            base.Initialize();
            // Reset window size
            UpdateWindowSize();
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            // Display main HUD data
            var vbox = base.Layout(layout).AddLayoutVertical();
            var colWidth =(int) Math.Round((double) vbox.RemainingWidth / TextSize, 1);
            var OffestColWidth = FirstColLenght / colWidth;

            foreach (var data in ListToLabel)
            {
                {
                    {   // open
                        if (data.FirstCol.Contains(Viewer.Catalog.GetString("NwLn")))
                        {
                            var hbox = vbox.AddLayoutHorizontalLineOfText();
                            hbox.Add(new Label(colWidth * 2, hbox.RemainingHeight, " "));
                        }
                        else if (data.FirstCol.Contains("Sprtr"))
                        {
                            vbox.AddHorizontalSeparator();
                        }
                        else
                        {
                            var hbox = vbox.AddLayoutHorizontalLineOfText();
                            var FirstCol = data.FirstCol;
                            var LastCol = data.LastCol;
                            var SymbolCol = data.SymbolCol;

                            if (ColorCode.Keys.Any(FirstCol.EndsWith) || ColorCode.Keys.Any(LastCol.EndsWith) || ColorCode.Keys.Any(data.keyPressed.EndsWith) || ColorCode.Keys.Any(data.SymbolCol.EndsWith))
                            {
                                var colorFirstColEndsWith = ColorCode.Keys.Any(FirstCol.EndsWith) ? ColorCode[FirstCol.Substring(FirstCol.Length - 3)] : Color.White;
                                var colorLastColEndsWith = ColorCode.Keys.Any(LastCol.EndsWith) ? ColorCode[LastCol.Substring(LastCol.Length - 3)] : Color.White;
                                var colorKeyPressed = ColorCode.Keys.Any(data.keyPressed.EndsWith) ? ColorCode[data.keyPressed.Substring(data.keyPressed.Length - 3)] : Color.White;
                                var colorSymbolCol = ColorCode.Keys.Any(data.SymbolCol.EndsWith) ? ColorCode[data.SymbolCol.Substring(data.SymbolCol.Length - 3)] : Color.White;

                                // Erase the color code at the string end
                                FirstCol = ColorCode.Keys.Any(FirstCol.EndsWith) ? FirstCol.Substring(0, FirstCol.Length - 3) : FirstCol;
                                LastCol = ColorCode.Keys.Any(LastCol.EndsWith) ? LastCol.Substring(0, LastCol.Length - 3) : LastCol;
                                keyPressed = ColorCode.Keys.Any(data.keyPressed.EndsWith) ? data.keyPressed.Substring(0, data.keyPressed.Length - 3) : data.keyPressed;
                                SymbolCol = ColorCode.Keys.Any(data.SymbolCol.EndsWith) ? data.SymbolCol.Substring(0, data.SymbolCol.Length - 3) : data.SymbolCol;

                                // Apply color to FirstCol
                                if (StandardHUD || FirstCol.StartsWith(Viewer.Catalog.GetString("Multi")))
                                {   // Apply color to FirstCol
                                    hbox.Add(indicator = new Label(TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Center));
                                    indicator.Color = colorKeyPressed;
                                    hbox.Add(indicator = new Label(colWidth * OffestColWidth, hbox.RemainingHeight, FirstCol));
                                    indicator.Color = colorFirstColEndsWith;
                                }
                                else
                                {   // Use constant width font
                                    hbox.Add(indicator = new Label(TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Center));
                                    indicator.Color = colorKeyPressed;
                                    hbox.Add(indicatorMono = new LabelMono(colWidth * OffestColWidth, hbox.RemainingHeight, FirstCol));
                                    indicatorMono.Color = colorFirstColEndsWith;
                                }

                                if (data.keyPressed != null && data.keyPressed != "")
                                {
                                    hbox.Add(indicator = new Label(-TextSize, 0, TextSize, hbox.RemainingHeight, keyPressed, LabelAlignment.Right));
                                    indicator.Color = colorKeyPressed;
                                }

                                if (data.SymbolCol != null && data.SymbolCol != "")
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

                                if (StandardHUD || FirstCol.StartsWith(Viewer.Catalog.GetString("Multi")) || FirstCol.ToUpper().Contains(Viewer.Catalog.GetString("PLAYER")))
                                {
                                    hbox.Add(indicator = new Label(colWidth * OffestColWidth, hbox.RemainingHeight, FirstCol));
                                    indicator.Color = Color.White; // Default color
                                }
                                else
                                {
                                    hbox.Add(indicatorMono = new LabelMono(colWidth * OffestColWidth, hbox.RemainingHeight, FirstCol));
                                    indicatorMono.Color = Color.White; // Default color
                                }
                                // Font to bold
                                if (hbox.Position.Y == 24 && LastCol.Contains(':')) // Time line.
                                {
                                    hbox.Add(LabelFontToBold = new Label(colWidth * (StandardHUD? 3: 5), hbox.RemainingHeight, LastCol));
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
                            if (hbox.Position.Y == 24)
                            {
                                hbox.Add(ExpandWindow = new Label(hbox.RemainingWidth - TextSize,0 , TextSize, hbox.RemainingHeight, expandWindow.ToString(), LabelAlignment.Right));
                                ExpandWindow.Color = Color.Yellow;
                                ExpandWindow.Click += new Action<Control, Point>(ExpandWindow_Click);
                            }
                            // Separator line
                            if (data.FirstCol.Contains("Sprtr"))
                            {
                                hbox.AddHorizontalSeparator();
                            }
                        }
                    }
                }
            }  // close
            return vbox;
        }
                
        void FontToBold_Click(Control arg1, Point arg2)
        {
            FontToBold = FontToBold ? false : true;
        }

        void ExpandWindow_Click(Control arg1, Point arg2)
        {
            StandardHUD = StandardHUD ? false : true;
            UpdateWindowSize();
        }

        private void UpdateWindowSize()
        {
            UpdateData();
            if (ListToLabel.Count > 0)
            {
                var newData = new List<string>();
                foreach (var data in ListToLabel)
                {
                    if (!data.FirstCol.Contains("Sprtr"))
                    {
                        if (data.FirstCol.Contains("?") || data.FirstCol.Contains("!") || data.FirstCol.Contains("$"))
                        {
                            newData.Add(data.FirstCol.Replace("?", "").Replace("!", "").Replace("$", ""));
                        }
                        else
                            newData.Add(data.FirstCol);
                    }
                }
                var FirstColLongest = newData.OrderByDescending(s => s.Length).First().ToString();

                newData.Clear();
                foreach (var data in ListToLabel)
                {
                    if (data.LastCol.Contains("?") || data.LastCol.Contains("!") || data.LastCol.Contains("$"))
                    {
                        newData.Add(data.LastCol.Replace("?", "").Replace("!", "").Replace("$", ""));
                    }
                    else
                        newData.Add(data.LastCol);
                }

                var LastColLongest = (newData.OrderByDescending(s => s.Length).First()).ToString();
                var textWidht = Owner.TextFontDefault;// StandardHUD ? Owner.TextFontDefault : Owner.TextFontMonoSpacedBold;
                FirstColLenght = textWidht.MeasureString('\u2589'.ToString() + FirstColLongest) + (StandardHUD?(int)(TextSize * 1.2): TextSize);
                LastColLenght = Owner.TextFontDefault.MeasureString(LastColLongest + "NNN");

                var desiredHeight = Owner.TextFontDefault.Height * (ListToLabel.Count(x => x.FirstCol != "Sprtr") + (StandardHUD ? 4 : 4));
                var desiredWidth = FirstColLenght + LastColLenght;

                var newHeight = (int)MathHelper.Clamp(desiredHeight, (StandardHUD? WindowHeightMin: 100), WindowHeightMax);
                var newWidth = (int)MathHelper.Clamp(desiredWidth, (StandardHUD? WindowWidthMin: 100), WindowWidthMax);

                // Move the dialog up if we're expanding it, or down if not; this keeps the center in the same place.
                var newTop = Location.Y + (Location.Height - newHeight) / 2;

                SizeTo(newWidth, newHeight);
                MoveTo(Location.X, newTop);
            }
        }

        /// <summary>
        /// Display info according to the full text window or the condensed text window
        /// </summary>
        /// <param name="firstkeyactivated"></param>
        /// <param name="firstcol"></param>
        /// <param name="lastcol"></param>
        /// <param name="symbolcol"></param>
        /// <param name="changecolwidth"></param>
        /// <param name="lastkeyactivated"></param>
        private void InfoToLabel(string firstkeyactivated, string firstcol, string lastcol, string symbolcol, bool changecolwidth, string lastkeyactivated)
        {
            if (!StandardHUD)
            {
                foreach (var code in FirstColToAbbreviated)
                {
                    if (firstcol.Contains(code.Key))
                    {
                        firstcol = firstcol.Replace(code.Key, code.Value);
                    }
                }
                foreach (var code in LastColToAbbreviated)
                {
                    if (lastcol.Contains(code.Key))
                    {
                        lastcol = lastcol.Replace(code.Key, code.Value);
                    }
                }
            }

            ListToLabel.Add(new ListLabel
            {
                FirstCol = firstcol,
                LastCol = lastcol,
                SymbolCol = symbolcol,
                ChangeColWidth = changecolwidth,
                keyPressed = keyPressed
            });
        }

        private void UpdateData()
        {   //Update data
            var arrowUp = '\u25B2';  // ▲
            var smallArrowUp = '\u25B3';  // △
            var arrowDown = '\u25BC';// ▼
            var smallArrowDown = '\u25BD';// ▽
            var end = '\u2589';// block
            var arrowToRight = '\u25BA'; // ►
            var smallDiamond = '\u25C6'; // ●

            var playerTrain = Owner.Viewer.PlayerLocomotive.Train;
            var showMUReverser = Math.Abs(playerTrain.MUReverserPercent) != 100;
            var showRetainers = playerTrain.RetainerSetting != RetainerSetting.Exhaust;
            var engineBrakeStatus = Owner.Viewer.PlayerLocomotive.GetEngineBrakeStatus();
            var dynamicBrakeStatus = Owner.Viewer.PlayerLocomotive.GetDynamicBrakeStatus();
            var locomotiveStatus = Owner.Viewer.PlayerLocomotive.GetStatus();
            var locomotiveDebugStatus = Owner.Viewer.PlayerLocomotive.GetDebugStatus();
            var stretched = playerTrain.Cars.Count > 1 && playerTrain.NPull == playerTrain.Cars.Count - 1;
            var bunched = !stretched && playerTrain.Cars.Count > 1 && playerTrain.NPush == playerTrain.Cars.Count - 1;
            var thisInfo = Owner.Viewer.PlayerTrain.GetTrainInfo();
            var brakeStatus = Owner.Viewer.PlayerLocomotive.GetTrainBrakeStatus();
            expandWindow = '\u23FA';// ⏺ toggle window

            keyPressed = "";
            ListToLabel.Clear();
            if (!StandardHUD)
            {
                var newBrakeStatus = new StringBuilder(brakeStatus);
                brakeStatus = newBrakeStatus
                      .Replace("bar", string.Empty)
                      .Replace("inHg", string.Empty)
                      .Replace("kgf/cm²", string.Empty)
                      .Replace("kPa", string.Empty)
                      .Replace("lib./pal.", string.Empty)
                      .Replace("psi", string.Empty)
                      .ToString();
            }

            //if (StandardHUD)
            //    InfoToLabel(Viewer.Catalog.GetString("Version"), VersionInfo.VersionOrBuild.ToString(), false);

            // First Block
            // Client and server may have a time difference.
            keyPressed = "";
            if (Orts.MultiPlayer.MPManager.IsClient())
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Time"), FormatStrings.FormatTime(Owner.Viewer.Simulator.ClockTime + Orts.MultiPlayer.MPManager.Instance().serverTimeDifference), "", false, keyPressed);
            else
            {
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Time"), FormatStrings.FormatTime(Owner.Viewer.Simulator.ClockTime), "", false, keyPressed);
            }
            if (Owner.Viewer.Simulator.IsReplaying)
            {
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Replay"), FormatStrings.FormatTime(Owner.Viewer.Log.ReplayEndsAt - Owner.Viewer.Simulator.ClockTime), "", false, keyPressed);
                keyPressed = "";
            }

            InfoToLabel(keyPressed, Viewer.Catalog.GetString("Speed"),
                FormatStrings.FormatSpeedDisplay(Owner.Viewer.PlayerLocomotive.SpeedMpS, Owner.Viewer.PlayerLocomotive.IsMetric) +
                (thisInfo.speedMpS < thisInfo.allowedSpeedMpS - 1.0f ? "!??" :        // White
                thisInfo.speedMpS < thisInfo.allowedSpeedMpS + 0.0f ? "?!!" :         // PaleGreen
                thisInfo.speedMpS < thisInfo.allowedSpeedMpS + 5.0f ? "!!?" : "!!!"), "", false, keyPressed);// Orange : Red
            keyPressed = "";

            // Gradient info
            if (StandardHUD)
            {
                if (-thisInfo.currentElevationPercent < -0.00015)
                {
                    var c = '\u2198';
                    Gradient = String.Format("{0:F1}%{1}", -thisInfo.currentElevationPercent, c) + "$$$";
                }
                else if (-thisInfo.currentElevationPercent > 0.00015)
                {
                    var c = '\u2197';
                    Gradient = String.Format("{0:F1}%{1}", -thisInfo.currentElevationPercent, c) + "???";
                }
                else Gradient = String.Format("{0:F1}%", -thisInfo.currentElevationPercent);

                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Gradient"), Gradient, "", false, keyPressed);
                keyPressed = "";
            }

            // Separator
            InfoToLabel(keyPressed, "Sprtr", "", "", false, keyPressed);
            keyPressed = "";

            // Second block
            // Direction
            if (UserInput.IsDown(UserCommand.ControlBackwards) || UserInput.IsDown(UserCommand.ControlForwards))
            {
                if (Owner.Viewer.PlayerLocomotive.EngineType != TrainCar.EngineTypes.Steam &&
                    (Owner.Viewer.PlayerLocomotive.Direction != Direction.Forward
                    || Owner.Viewer.PlayerLocomotive.Direction != Direction.Reverse)
                    && (Owner.Viewer.PlayerLocomotive.ThrottlePercent >= 1
                    || Math.Abs(Owner.Viewer.PlayerLocomotive.SpeedMpS) > 1))
                {
                    keyPressed = end.ToString() + "???";
                }
                else
                {
                    keyPressed = (UserInput.IsDown(UserCommand.ControlBackwards) ? arrowDown.ToString() + "???" : UserInput.IsDown(UserCommand.ControlForwards) ? arrowUp.ToString() + "???" : " ");
                }
            }
            InfoToLabel(keyPressed, (Owner.Viewer.PlayerLocomotive.EngineType == TrainCar.EngineTypes.Steam ? Viewer.Catalog.GetString("Reverser") : Viewer.Catalog.GetString("Direction")),
                (showMUReverser ? Math.Abs(playerTrain.MUReverserPercent).ToString("0") + "% " : "") + FormatStrings.Catalog.GetParticularString("Reverser", GetStringAttribute.GetPrettyName(Owner.Viewer.PlayerLocomotive.Direction)), "", false, keyPressed);
            keyPressed = "";

            // Throttle
            if ((UserInput.IsDown(UserCommand.ControlThrottleIncrease) && Owner.Viewer.PlayerLocomotive.ThrottlePercent == 100)
               || (UserInput.IsDown(UserCommand.ControlThrottleDecrease) && Owner.Viewer.PlayerLocomotive.ThrottlePercent == 0))
            {
                keyPressed = end.ToString() + "???";
            }
            else
            {
                keyPressed = UserInput.IsDown(UserCommand.ControlThrottleIncrease) ? arrowUp.ToString() + "???"
                    : UserInput.IsDown(UserCommand.ControlThrottleDecrease) ? arrowDown.ToString() + "???"
                    : "";
            }
            InfoToLabel(keyPressed, (Owner.Viewer.PlayerLocomotive is MSTSSteamLocomotive ? Viewer.Catalog.GetString("Regulator") : Viewer.Catalog.GetString("Throttle")), Owner.Viewer.PlayerLocomotive.ThrottlePercent.ToString("0") + "%", "", false, keyPressed);
            keyPressed = "";

            // Cylinder Cocks
            if (Owner.Viewer.PlayerLocomotive is MSTSSteamLocomotive)
            {
                keyPressed = (UserInput.IsDown(UserCommand.ControlCylinderCocks) || (Owner.Viewer.PlayerLocomotive as MSTSSteamLocomotive).CylinderCocksAreOpen) ? arrowToRight.ToString() + "???" : "";
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Cylinder cocks"), (Owner.Viewer.PlayerLocomotive as MSTSSteamLocomotive).CylinderCocksAreOpen ? Viewer.Catalog.GetString("Open") + "!!?" : Viewer.Catalog.GetString("Closed") + "!??", "", false, keyPressed);
            }

            // Sander
            keyPressed = UserInput.IsDown(UserCommand.ControlSander) || UserInput.IsDown(UserCommand.ControlSanderToggle) ? arrowDown.ToString() + "???" : " ";
            if (Owner.Viewer.PlayerLocomotive.GetSanderOn())
            {
                var sanderBlocked = Owner.Viewer.PlayerLocomotive is MSTSLocomotive && Math.Abs(playerTrain.SpeedMpS) > ((MSTSLocomotive)Owner.Viewer.PlayerLocomotive).SanderSpeedOfMpS;
                if (sanderBlocked)
                {
                    InfoToLabel(keyPressed, Viewer.Catalog.GetString("Sander"), Viewer.Catalog.GetString("Blocked") + "!!!", "", StandardHUD ? true : false, keyPressed);
                    keyPressed = "";
                }
                else
                {
                    keyPressed = arrowToRight.ToString() + "???";
                    InfoToLabel(keyPressed, Viewer.Catalog.GetString("Sander"), Viewer.Catalog.GetString("On") + "!!?", "", StandardHUD ? true : false, keyPressed);
                    keyPressed = "";
                }
            }
            else
            {
                keyPressed = "";
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Sander"), "Off", "", false, keyPressed);
            }

            InfoToLabel("", "Sprtr", "", "", false, keyPressed);

            // Train Brake multi-lines
            // TO DO: A better algorithm
            //var brakeStatus = Owner.Viewer.PlayerLocomotive.GetTrainBrakeStatus();
            //steam loco
            keyPressed = UserInput.IsDown(UserCommand.ControlTrainBrakeDecrease) ? arrowDown.ToString() + "???" : UserInput.IsDown(UserCommand.ControlTrainBrakeIncrease) ? arrowUp.ToString() + "???" : "";

            var brakeInfoValue = "";
            var index = 0;

            if (brakeStatus.Contains(Viewer.Catalog.GetString("EQ")))
            {
                brakeInfoValue = brakeStatus.Substring(0, brakeStatus.IndexOf(Viewer.Catalog.GetString("EQ"))).TrimEnd();
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Train brake"), brakeInfoValue + "$??", "", false, keyPressed);
                keyPressed = "";
                index = brakeStatus.IndexOf(Viewer.Catalog.GetString("EQ"));
                brakeInfoValue = brakeStatus.Substring(index, brakeStatus.IndexOf(Viewer.Catalog.GetString("BC")) - index).TrimEnd();

                InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                keyPressed = "";
                if (brakeStatus.Contains(Viewer.Catalog.GetString("EOT")))
                {
                    var IndexOffset = Viewer.Catalog.GetString("EOT").Length + 1;
                    index = brakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                    brakeInfoValue = brakeStatus.Substring(index, brakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) - index).TrimEnd();
                    keyPressed = "";
                    InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                    keyPressed = "";
                    index = brakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) + IndexOffset;
                    brakeInfoValue = brakeStatus.Substring(index, brakeStatus.Length - index).TrimStart();
                    keyPressed = "";
                    InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                    keyPressed = "";
                }
                else
                {
                    index = brakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                    brakeInfoValue = brakeStatus.Substring(index, brakeStatus.Length - index).TrimEnd();
                    keyPressed = "";
                    InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                    keyPressed = "";
                }
            }
            else if (brakeStatus.Contains(Viewer.Catalog.GetString("Lead")))
            {
                var IndexOffset  = Viewer.Catalog.GetString("Lead").Length + 1;
                brakeInfoValue = brakeStatus.Substring(0, brakeStatus.IndexOf(Viewer.Catalog.GetString("Lead"))).TrimEnd();
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Train brake"), brakeInfoValue + "$??", "", false, keyPressed);

                keyPressed = "";
                index = brakeStatus.IndexOf(Viewer.Catalog.GetString("Lead")) + IndexOffset;
                if (brakeStatus.Contains(Viewer.Catalog.GetString("EOT")))
                {
                    brakeInfoValue = brakeStatus.Substring(index, brakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) - index).TrimEnd();
                    InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);

                    keyPressed = "";
                    index = brakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) + IndexOffset;
                    brakeInfoValue = brakeStatus.Substring(index, brakeStatus.Length - index).TrimEnd();
                    InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                }
                else
                {
                    brakeInfoValue = brakeStatus.Substring(index, brakeStatus.Length - index).TrimEnd();
                    InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                }
            }
            else if (brakeStatus.Contains(Viewer.Catalog.GetString("BC")))
            {
                brakeInfoValue = brakeStatus.Substring(0, brakeStatus.IndexOf(Viewer.Catalog.GetString("BC"))).TrimEnd();
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Train brake"), brakeInfoValue + "$??", "", false, keyPressed);

                keyPressed = "";
                index = brakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                brakeInfoValue = brakeStatus.Substring(index, brakeStatus.Length - index).TrimEnd();

                keyPressed = "";
                InfoToLabel(keyPressed, "", brakeInfoValue, "", false, keyPressed);
                keyPressed = "";
            }

            keyPressed = "";
            if (showRetainers)
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Retainers"), (playerTrain.RetainerPercent + " " + Viewer.Catalog.GetString(GetStringAttribute.GetPrettyName(playerTrain.RetainerSetting))), "", false, keyPressed);

            keyPressed = "";
            if ((Owner.Viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeFitted) // ideally this test should be using "engineBrakeStatus != null", but this currently does not work, as a controller is defined by default
            {
            }
            keyPressed = UserInput.IsDown(UserCommand.ControlEngineBrakeDecrease) ? arrowDown.ToString() + "???" : UserInput.IsDown(UserCommand.ControlEngineBrakeIncrease) ? arrowUp.ToString() + "???" : "";
            if (engineBrakeStatus.Contains(Viewer.Catalog.GetString("BC")))
            {
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Engine brake"), engineBrakeStatus.Substring(0, engineBrakeStatus.IndexOf("BC")) + "$??", "", false, keyPressed);
                keyPressed = "";
                index = engineBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                brakeInfoValue = engineBrakeStatus.Substring(index, engineBrakeStatus.Length - index).TrimEnd();
                InfoToLabel(keyPressed, Viewer.Catalog.GetString(""), brakeInfoValue + "!??", "", false, keyPressed);
            }
            else
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Engine brake"), engineBrakeStatus + "$??", "", false, keyPressed);

            keyPressed = "";
            if (dynamicBrakeStatus != null)
            {
                keyPressed = UserInput.IsDown(UserCommand.ControlDynamicBrakeDecrease) ? arrowDown.ToString() + "???"
                    : UserInput.IsDown(UserCommand.ControlDynamicBrakeIncrease) ? arrowUp.ToString() + "???" : "";
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("Dynamic brake"), dynamicBrakeStatus + "$??", "", false, keyPressed);
            }
            keyPressed = "";
            InfoToLabel(keyPressed, "Sprtr", "", "", false, keyPressed);

            if (locomotiveStatus != null)
            {
                var lines = locomotiveStatus.Split('\n');
                foreach (var data in lines)
                {
                    if (data.Length > 0)
                    {
                        var parts = data.Split(new[] { " = " }, 2, StringSplitOptions.None);
                        var HeatColor = "!??"; // Color.White
                        keyPressed = "";
                        if (!StandardHUD && Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Steam usage")))
                        {
                        }
                        else if (Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Boiler pressure")))
                        {
                            MSTSSteamLocomotive steamloco = (MSTSSteamLocomotive)Owner.Viewer.PlayerLocomotive;
                            var bandUpper = steamloco.PreviousBoilerHeatOutBTUpS * 1.025f; // find upper bandwidth point
                            var bandLower = steamloco.PreviousBoilerHeatOutBTUpS * 0.975f; // find lower bandwidth point - gives a total 5% bandwidth

                            if (steamloco.BoilerHeatInBTUpS > bandLower && steamloco.BoilerHeatInBTUpS < bandUpper) HeatColor = smallDiamond.ToString() + "!??";
                            else if (steamloco.BoilerHeatInBTUpS < bandLower) HeatColor = smallArrowDown.ToString() + "$??"; // Color.Cyan
                            else if (steamloco.BoilerHeatInBTUpS > bandUpper) HeatColor = smallArrowUp.ToString() + "!!!"; // Color.OrangeRed

                            keyPressed = "";
                            InfoToLabel(keyPressed, Viewer.Catalog.GetString("Boiler pressure"), Viewer.Catalog.GetString(parts[1]), HeatColor, false, keyPressed);
                        }
                        else if (!StandardHUD && Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Fuel levels")))
                        {
                            keyPressed = "";
                            InfoToLabel(keyPressed, parts[0].EndsWith("?") || parts[0].EndsWith("!") ? Viewer.Catalog.GetString(parts[0].Substring(0, parts[0].Length - 3)) : Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1].Replace(" ", string.Empty)) : ""), "", false, keyPressed);
                        }
                        else if (parts[0].StartsWith(Viewer.Catalog.GetString("Gear")))
                        {
                            keyPressed = UserInput.IsDown(UserCommand.ControlGearDown) ? arrowDown.ToString() + "???" : UserInput.IsDown(UserCommand.ControlGearUp) ? arrowUp.ToString() + "???" : "";
                            InfoToLabel(keyPressed, Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) : ""), "", false, keyPressed);
                            keyPressed = "";
                        }
                        else if (parts.Contains(Viewer.Catalog.GetString("Pantographs")))
                        {
                            keyPressed = UserInput.IsDown(UserCommand.ControlPantograph1) ? parts[1].StartsWith(Viewer.Catalog.GetString("Up")) ? arrowUp.ToString() + "???" : arrowDown.ToString() + "???" : "";
                            InfoToLabel(keyPressed, Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) : ""), "", false, keyPressed);
                            keyPressed = "";
                        }
                        else if (parts.Contains(Viewer.Catalog.GetString("Engine")))
                        {
                            keyPressed = "";
                            InfoToLabel(keyPressed, Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) + "!??" : ""), "", false, keyPressed);
                            keyPressed = "";
                        }
                        else
                        {
                            InfoToLabel("", parts[0].EndsWith("?") || parts[0].EndsWith("!") ? Viewer.Catalog.GetString( parts[0].Substring(0, parts[0].Length - 3)) : Viewer.Catalog.GetString(parts[0]), (parts.Length > 1 ? Viewer.Catalog.GetString(parts[1]) : ""), "", false, keyPressed);
                        }
                    }
                }
            }

            keyPressed = "";
            InfoToLabel(keyPressed, "Sprtr", "", "", true, keyPressed);

            keyPressed = "";
            if (StandardHUD)
                InfoToLabel(keyPressed, Viewer.Catalog.GetString("FPS"), Owner.Viewer.RenderProcess.FrameRate.SmoothedValue.ToString("F0"), "", false, keyPressed);

            // Messages
			// Autopilot
            keyPressed = "";
            if (Owner.Viewer.Settings.Autopilot)
            {
                if (Owner.Viewer.PlayerLocomotive.Train.TrainType == Train.TRAINTYPE.AI_PLAYERHOSTING)
                {
                    keyPressed = UserInput.IsDown(UserCommand.GameAutopilotMode) ? arrowUp.ToString() + "???" : "";
                    InfoToLabel(keyPressed, Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("On") + "???", "", false, keyPressed);
                }
                else if (Owner.Viewer.PlayerLocomotive.Train.TrainType != Train.TRAINTYPE.AI_PLAYERHOSTING)
                {
                    keyPressed = UserInput.IsDown(UserCommand.GameAutopilotMode) ? arrowDown.ToString() + "???" : "";
                    InfoToLabel(keyPressed, Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("Off"), "", false, keyPressed);
                }
                else
                    InfoToLabel("", Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("Off"), "", false, keyPressed);
            }
            else
                InfoToLabel("", Viewer.Catalog.GetString("Autopilot" + "?!?"), Viewer.Catalog.GetString("Off"), "", false, keyPressed);

            // Grate limit
            keyPressed = "";
            if (Owner.Viewer.PlayerLocomotive.GetType() == typeof(MSTSSteamLocomotive))
            {
                MSTSSteamLocomotive steamloco = (MSTSSteamLocomotive)Owner.Viewer.PlayerLocomotive;
                if (steamloco.GrateCombustionRateLBpFt2 > steamloco.GrateLimitLBpFt2)
                {
                    if (steamloco.IsGrateLimit)
                        InfoToLabel("", Viewer.Catalog.GetString("Grate limit"), Viewer.Catalog.GetString("Exceeded") + "!!!", "", false, keyPressed);
                }
                else
                    InfoToLabel("", Viewer.Catalog.GetString("Grate limit") + "?!?", Viewer.Catalog.GetString("Normal") + "?!?", "", false, keyPressed);
            }
            else
                InfoToLabel("", Viewer.Catalog.GetString("Grate limit") + "?!?", Viewer.Catalog.GetString("-") + "?!?", "", false, keyPressed);

			// Whell
            keyPressed = "";
            if (Owner.Viewer.PlayerTrain.IsWheelSlip)
                InfoToLabel("", Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("slip") + "!!!", "", false, keyPressed);
            else if (Owner.Viewer.PlayerTrain.IsWheelSlipWarninq)
                InfoToLabel("", Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("slip warning") + "???", "", false, keyPressed);
            else if (Owner.Viewer.PlayerTrain.IsBrakeSkid)
                InfoToLabel("", Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("skid") + "!!!", "", false, keyPressed);
            else
                InfoToLabel("", Viewer.Catalog.GetString("Wheel") + "?!?", Viewer.Catalog.GetString("Normal") + "?!?", "", false, keyPressed);

			// Door
            keyPressed = "";
            if ((Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorLeftOpen || (Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorRightOpen)
            {
                var color = Math.Abs(Owner.Viewer.PlayerLocomotive.SpeedMpS) > 0.1f ? "!!!" : "???";
                var status = "";
                if ((Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorLeftOpen)
                    status += Viewer.Catalog.GetString((Owner.Viewer.PlayerLocomotive as MSTSLocomotive).GetCabFlipped() ? Viewer.Catalog.GetString("Right") : Viewer.Catalog.GetString("Left"));
                if ((Owner.Viewer.PlayerLocomotive as MSTSWagon).DoorRightOpen)
                    status += string.Format(status == "" ? "{0}" : " {0}", Viewer.Catalog.GetString((Owner.Viewer.PlayerLocomotive as MSTSLocomotive).GetCabFlipped() ? Viewer.Catalog.GetString("Left") : Viewer.Catalog.GetString("Right")));
                status += color;

                InfoToLabel(" ", Viewer.Catalog.GetString("Doors open"), status, "", false, keyPressed);
            }
            else
                InfoToLabel(" ", Viewer.Catalog.GetString("Doors open") + "?!?", Viewer.Catalog.GetString("Closed"), "", false, keyPressed);

            // MultiPlayer
            if (StandardHUD && Orts.MultiPlayer.MPManager.IsMultiPlayer())
            {
                InfoToLabel("", "Sprtr", "", "", false, keyPressed);
                var text = Orts.MultiPlayer.MPManager.Instance().GetOnlineUsersInfo();

                InfoToLabel(" ", Viewer.Catalog.GetString("MultiPlayerStatus: "), (Orts.MultiPlayer.MPManager.IsServer()
                    ? Viewer.Catalog.GetString("Dispatcher") : Orts.MultiPlayer.MPManager.Instance().AmAider
                    ? Viewer.Catalog.GetString("Helper") : Orts.MultiPlayer.MPManager.IsClient()
                    ? Viewer.Catalog.GetString("Client") : ""), "", true, keyPressed);
                InfoToLabel("", "NwLn", "", "", false, keyPressed);
                foreach (var t in text.Split('\t'))
                    InfoToLabel(" ", (t), "", "", true, keyPressed);
            }

            // Ctrl + F Firing to manual
            if (UserInput.IsDown(UserCommand.ControlFiring))
            {
                ResizeWindow = true;
            }
        }

        public override void PrepareFrame(ElapsedTime elapsedTime, bool updateFull)
        {
            base.PrepareFrame(elapsedTime, updateFull);

            if (updateFull)
            {
                UpdateData();

                // Ctrl + F (FiringIsManual)                
                if (ResizeWindow || LinesCount != ListToLabel.Count())
                {
                    ResizeWindow = false;
                    UpdateWindowSize();
                    LinesCount = ListToLabel.Count();
                }
                
                //Update Layout
                Layout();
            }
        }
    }
}