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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class TrainDpuWindow : Window
    {
        private bool ResizeWindow;
        private bool UpdateDataEnded;
        private int FirstColLenght;
        private int FirstColOverFlow;
        private int LastColLenght;
        private int LastColOverFlow;
        private int LastPlayerTrainCars;
        private int dpiOffset;

        public bool NormalTextMode { get; private set; } = true;// Standard text
        public bool NormalVerticalMode { get; private set; } = true;// vertical window size
        public bool TrainDpuUpdating { get; private set; }

        private int dieselLocomotivesCount;
        private int maxFirstColWidth;
        private int maxLastColWidth;
        private int windowHeightMin;
        private int windowHeightMax;
        private int windowWidthMin;
        private int windowWidthMax;

        private const int TextSize = 15;
        public int KeyPresLength { get; }
        public int OffSetX { get; }

        private Label ExpandWindow;
        private Label VerticalWindow;
        private Label indicator;
        private LabelMono indicatorMono;
        private Label LabelFontToBold;
        public static bool FontChanged { get; private set; }
        public static bool FontToBold { get; private set; }
        public static bool MonoFont { get; private set; }

        /// <summary>
        /// A Train Dpu row with data fields.
        /// </summary>
        public struct ListLabel
        {
            public string FirstCol;
            public int FirstColWidth;
            public List<string> LastCol;
            public List<int> LastColWidth;
            public List<string> SymbolCol;
            public bool ChangeColWidth;
            public string KeyPressed;
        }

        public List<ListLabel> Labels { get; private set; } = new List<ListLabel>();

        /// <summary>
        /// Table of Colors to client-side color codes.
        /// </summary>
        /// <remarks>
        /// Compare codes with index.css.
        /// </remarks>
        private static readonly Dictionary<Color, string> ColorCode = new Dictionary<Color, string>
        {
            { Color.Yellow, "???" },
            { Color.Green, "??!" },
            { Color.Black, "?!?" },
            { Color.PaleGreen, "?!!" },
            { Color.White, "!??" },
            { Color.Orange, "!!?" },
            { Color.OrangeRed, "!!!" },
            { Color.Cyan, "%%%" },
            { Color.Brown, "%$$" },
            { Color.LightGreen, "%%$" },
            { Color.Blue, "$%$" },
            { Color.LightSkyBlue, "$$$" },
        };

        // Change text color
        readonly Dictionary<string, Color> ColorCodeCtrl = new Dictionary<string, Color>
        {
            { "!!!", Color.OrangeRed },
            { "!!?", Color.Orange },
            { "!??", Color.White },
            { "?!?", Color.Black },
            { "???", Color.Yellow },
            { "??!", Color.Green },
            { "?!!", Color.PaleGreen },
            { "$$$", Color.LightSkyBlue},
            { "%%%", Color.Cyan}
        };

        private static class Symbols
        {
            public const string Fence = "\u2590";
            public const string ArrowUp = "▲";
            public const string ArrowDown = "▼";
            public const string ArrowToRight = "\u25BA";
            public const string ArrowToLeft = "\u25C4";
        }

        readonly Dictionary<string, string> FirstColToAbbreviated = new Dictionary<string, string>()
        {
            [Viewer.Catalog.GetString("Flow")] = Viewer.Catalog.GetString("FLOW"),//
            [Viewer.Catalog.GetString("Fuel")] = Viewer.Catalog.GetString("FUEL"),//
            [Viewer.Catalog.GetString("Load")] = Viewer.Catalog.GetString("LOAD"),//
            [Viewer.Catalog.GetString("Loco Groups")] = Viewer.Catalog.GetString("GRUP"),
            [Viewer.Catalog.GetString("Oil Pressure")] = Viewer.Catalog.GetString("OIL"),//
            [Viewer.Catalog.GetString("Power")] = Viewer.Catalog.GetString("POWR"),//
            [Viewer.Catalog.GetString("Remote")] = Viewer.Catalog.GetString("RMT"),//
            [Viewer.Catalog.GetString("RPM")] = Viewer.Catalog.GetString("RPM"),//
            [Viewer.Catalog.GetString("Reverser")] = Viewer.Catalog.GetString("REVR"),//
            [Viewer.Catalog.GetString("Status")] = Viewer.Catalog.GetString("STAT"),//
            [Viewer.Catalog.GetString("Temperature")] = Viewer.Catalog.GetString("TEMP"),//
            [Viewer.Catalog.GetString("Throttle")] = Viewer.Catalog.GetString("THRO"),//
            [Viewer.Catalog.GetString("Time")] = Viewer.Catalog.GetString("TIME"),//
            [Viewer.Catalog.GetString("Tractive Effort")] = Viewer.Catalog.GetString("TRACT")//
        };

        readonly Dictionary<string, string> LastColToAbbreviated = new Dictionary<string, string>()
        {
            [Viewer.Catalog.GetString("Forward")] = Viewer.Catalog.GetString("Forw."),
            [Viewer.Catalog.GetString("Idle")] = Viewer.Catalog.GetString("Idle"),
            [Viewer.Catalog.GetString("Running")] = Viewer.Catalog.GetString("Runn")
        };

        public TrainDpuWindow(WindowManager owner)
            : base(owner, Window.DecorationSize.X + owner.TextFontDefault.Height * 10, Window.DecorationSize.Y + owner.TextFontDefault.Height * 10, Viewer.Catalog.GetString("Train Dpu Info"))
        {
            windowHeightMin = Location.Height / 2;
            windowHeightMax = Location.Height + owner.TextFontDefault.Height * 20;
            windowWidthMin = Location.Width;
            windowWidthMax = Location.Width + owner.TextFontDefault.Height * 20;
        }

        protected internal override void Save(BinaryWriter outf)
        {
            base.Save(outf);
            outf.Write(NormalTextMode);
            outf.Write(NormalVerticalMode);
            outf.Write(Location.X);
            outf.Write(Location.Y);
            outf.Write(Location.Width);
            outf.Write(Location.Height);
        }

        protected internal override void Restore(BinaryReader inf)
        {
            base.Restore(inf);
            Rectangle LocationRestore;
            NormalTextMode = inf.ReadBoolean();
            NormalVerticalMode = inf.ReadBoolean();
            LocationRestore.X = inf.ReadInt32();
            LocationRestore.Y = inf.ReadInt32();
            LocationRestore.Width = inf.ReadInt32();
            LocationRestore.Height = inf.ReadInt32();

            // Display window
            SizeTo(LocationRestore.Width, LocationRestore.Height);
            MoveTo(LocationRestore.X, LocationRestore.Y);
        }

        protected internal override void Initialize()
        {
            base.Initialize();
            if (Visible)
            {   // Reset window size
                UpdateWindowSize();
            }
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            // Display main DUP data
            var vbox = base.Layout(layout).AddLayoutVertical();
            if (Labels.Count > 0)
            {
                var colWidth = Labels.Max(x => x.FirstColWidth) + TextSize;// right space

                var lastColLenght = 0;
                var LastColLenght = 0;
                foreach (var data in Labels.Where(x => x.LastColWidth != null && x.LastColWidth.Count > 0))
                {
                    lastColLenght = data.LastColWidth.Max(x => x);
                    LastColLenght = lastColLenght > LastColLenght ? lastColLenght : LastColLenght;
                }

                var lastWidth = LastColLenght + TextSize / 2;
                var TimeHboxPositionY = 0;

                foreach (var data in Labels.ToList())
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
                        var FirstCol = " " + data.FirstCol;
                        var LastCol = data.LastCol;
                        var SymbolCol = data.SymbolCol;
                        var locoGroups = new[] { Viewer.Catalog.GetString("Loco Groups"), Viewer.Catalog.GetString("GRUP") }.Any(s => FirstCol.Contains(s, StringComparison.OrdinalIgnoreCase));

                        if (LastCol != null && LastCol[0] != null)
                        {
                            //Avoids troubles when the Main Scale (Windows DPI settings) is not set to 100%
                            if (locoGroups)
                                TimeHboxPositionY = hbox.Position.Y;

                            if (NormalTextMode)
                            {
                                hbox.Add(indicator = new Label(colWidth, hbox.RemainingHeight, FirstCol));
                                indicator.Color = Color.White; // Default color
                            }
                            else
                            {
                                hbox.Add(indicatorMono = new LabelMono(colWidth, hbox.RemainingHeight, FirstCol));
                                indicatorMono.Color = Color.White; // Default color
                            }

                            for (int i = 0; i < data.LastCol.Count; i++)
                            {
                                var colorFirstColEndsWith = ColorCodeCtrl.Keys.Any(FirstCol.EndsWith) ? ColorCodeCtrl[FirstCol[^3..]] : Color.White;
                                var colorLastColEndsWith = ColorCodeCtrl.Keys.Any(LastCol[i].EndsWith) ? ColorCodeCtrl[LastCol[i][^3..]] : Color.White;
                                var colorSymbolCol = ColorCodeCtrl.Keys.Any(data.SymbolCol[i].EndsWith) ? ColorCodeCtrl[data.SymbolCol[i][^3..]] : Color.White;

                                // Erase the color code at the string end
                                SymbolCol[i] = ColorCodeCtrl.Keys.Any(data.SymbolCol[i].EndsWith) ? data.SymbolCol[i][0..^3] : data.SymbolCol[i];
                                LastCol[i] = ColorCodeCtrl.Keys.Any(LastCol[i].EndsWith) ? LastCol[i][0..^3] : LastCol[i];

                                if (SymbolCol[i].Contains(Symbols.Fence, StringComparison.OrdinalIgnoreCase))
                                {
                                    hbox.Add(indicator = new Label(-(TextSize / 2), 0, TextSize, hbox.RemainingHeight, Symbols.Fence, LabelAlignment.Left));
                                    indicator.Color = Color.Green;

                                    // Apply color to LastCol
                                    var lastCol = LastCol[i].Replace("|", " ", StringComparison.OrdinalIgnoreCase);
                                    hbox.Add(indicator = new Label(lastWidth, hbox.RemainingHeight, lastCol, locoGroups ? LabelAlignment.Center : LabelAlignment.Left));//center
                                    indicator.Color = colorFirstColEndsWith == Color.White ? colorLastColEndsWith : colorFirstColEndsWith;
                                }
                                else
                                {
                                    // Font to bold, clickable label
                                    if (hbox.Position.Y == TimeHboxPositionY && i == 0)
                                    {
                                        hbox.Add(LabelFontToBold = new Label(lastWidth, hbox.RemainingHeight, LastCol[i], locoGroups ? LabelAlignment.Center : LabelAlignment.Left));
                                        LabelFontToBold.Click += new Action<Control, Point>(FontToBold_Click);
                                    }
                                    else
                                    {
                                        if (i > 0)
                                        {
                                            hbox.Add(indicator = new Label(-(TextSize / 2), 0, TextSize, hbox.RemainingHeight, SymbolCol[i], LabelAlignment.Left));
                                            indicator.Color = colorSymbolCol;
                                        }
                                        hbox.Add(indicator = new Label(lastWidth, hbox.RemainingHeight, LastCol[i], locoGroups ? LabelAlignment.Center : LabelAlignment.Left));
                                        indicator.Color = colorLastColEndsWith;
                                    }
                                }
                            }
                        }

                        // Clickable symbol
                        if (hbox.Position.Y == TimeHboxPositionY)
                        {
                            var verticalWindow = NormalVerticalMode ? Symbols.ArrowDown : Symbols.ArrowUp;// ▲ : ▶
                            hbox.Add(VerticalWindow = new Label(hbox.RemainingWidth - (TextSize * 2), 0, TextSize, hbox.RemainingHeight, verticalWindow.ToString(), LabelAlignment.Right));
                            VerticalWindow.Color = Color.Yellow;
                            VerticalWindow.Click += new Action<Control, Point>(VerticalWindow_Click);

                            var expandWindow = NormalTextMode ? Symbols.ArrowToLeft : Symbols.ArrowToRight;// ◀ : ▶
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

        void FontToBold_Click(Control arg1, Point arg2)
        {
            FontChanged = true;
            FontToBold = !FontToBold;
            UpdateWindowSize();
        }

        void ExpandWindow_Click(Control arg1, Point arg2)
        {
            NormalTextMode = !NormalTextMode;
            UpdateWindowSize();
        }

        void VerticalWindow_Click(Control arg1, Point arg2)
        {
            NormalVerticalMode = !NormalVerticalMode;
            UpdateWindowSize();
        }

        public override void TabAction() => CycleMode();

        /// <summary>
        /// Change between full and abbreviated text mode.
        /// </summary>
        public void CycleMode()
        {
            NormalTextMode = !NormalTextMode;
            UpdateWindowSize();
        }

        private void UpdateWindowSize()
        {
            Labels = TrainDPUWindowList(Owner.Viewer, NormalTextMode).ToList();
            ModifyWindowSize();
        }

        /// <summary>
        /// Modify window size
        /// </summary>
        private void ModifyWindowSize()
        {
            if (Labels.Count > 0)
            {
                var textwidth = Owner.TextFontDefault.Height;
                FirstColLenght = Labels.Max(x => x.FirstColWidth);

                var lastColLenght = 0;
                foreach (var data in Labels.Where(x => x.LastColWidth != null && x.LastColWidth.Count > 0))
                {
                    lastColLenght = data.LastColWidth.Max(x => x) + TextSize / 2;
                    LastColLenght = lastColLenght > LastColLenght ? lastColLenght : LastColLenght;
                }

                // Validates rows with windows DPI settings
                dpiOffset = (System.Drawing.Graphics.FromHwnd(IntPtr.Zero).DpiY / 96) > 1.00f ? 1 : 0;// values from testing
                var rowCount = Labels.Where(x => !string.IsNullOrEmpty(x.FirstCol)).Count() - dpiOffset;

                var desiredHeight = FontToBold ? (Owner.TextFontDefaultBold.Height + 2) * (rowCount + 1)
                    : (Owner.TextFontDefault.Height + 2) * (rowCount + 1);
                var desiredWidth = FirstColLenght + (LastColLenght * (dieselLocomotivesCount + 1));// interval between firstcol and lastcol
                var normalMode = NormalTextMode && NormalVerticalMode;
                var newHeight = desiredHeight < windowHeightMin ? desiredHeight + Owner.TextFontDefault.Height * 2
                    : (int)MathHelper.Clamp(desiredHeight, (normalMode ? windowHeightMin : 100), windowHeightMax);

                var newWidth = (int)MathHelper.Clamp(desiredWidth, (NormalTextMode ? windowWidthMin : 100), windowWidthMax + (Owner.Viewer.DisplaySize.X / 2));

                // Move the dialog up if we're expanding it, or down if not; this keeps the center in the same place.
                var newTop = Location.Y + (Location.Height - newHeight) / 2;

                // Display window
                SizeTo(newWidth, newHeight);
                MoveTo(Location.X, newTop);
            }
        }

        /// <summary>
        /// Sanitize the fields of a <see cref="ListLabel"/> in-place.
        /// </summary>
        /// <param name="label">A reference to the <see cref="ListLabel"/> to check.</param>
        private void CheckLabel(ref ListLabel label, bool normalMode)
        {
            static void CheckString(ref string s) => s ??= "";

            CheckString(ref label.FirstCol);

            if (label.LastCol != null)
            {
                for (int i = 0; i < label.LastCol.Count; i++)
                {
                    var LastCol = label.LastCol[i];
                    CheckString(ref LastCol);
                    label.LastCol[i] = LastCol;
                }
            }

            if (label.SymbolCol != null)
            {
                for (int i = 0; i < label.SymbolCol.Count; i++)
                {
                    var symbolCol = label.SymbolCol[i];
                    CheckString(ref symbolCol);
                    label.SymbolCol[i] = symbolCol;
                }
            }
            CheckString(ref label.KeyPressed);

            UpdateColsWidth(label);
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

        private void UpdateColsWidth(ListLabel label)
        {
            if (!UpdateDataEnded)
            {
                if (!NormalTextMode)
                {
                    foreach (KeyValuePair<string, string> mapping in FirstColToAbbreviated)
                        label.FirstCol = label.FirstCol.Replace(mapping.Key, mapping.Value, StringComparison.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> mapping in LastColToAbbreviated)
                    {
                        if (label.LastCol != null)
                        {
                            for (int i = 0; i < label.LastCol.Count; i++)
                            {
                                label.LastCol[i] = label.LastCol[i].Replace(mapping.Key, mapping.Value, StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }
                }
                var firstCol = label.FirstCol;
                var firstColWidth = 0;
                var lastCol = label.LastCol;
                List<int> lastColWidth = new List<int>();
                var symbolCol = label.SymbolCol;
                var keyPressed = label.KeyPressed;
                var changeColwidth = label.ChangeColWidth;

                if (!firstCol.Contains("Sprtr", StringComparison.OrdinalIgnoreCase))
                {
                    if (ColorCodeCtrl.Keys.Any(firstCol.EndsWith))
                    {
                        var tempFirstCol = firstCol.Substring(0, firstCol.Length - 3);
                        firstColWidth = FontToBold ? Owner.TextFontDefaultBold.MeasureString(tempFirstCol.TrimEnd())
                            : !NormalTextMode ? Owner.TextFontMonoSpacedBold.MeasureString(tempFirstCol.TrimEnd())
                            : Owner.TextFontDefault.MeasureString(tempFirstCol.TrimEnd());
                    }
                    else
                    {
                        firstColWidth = FontToBold ? Owner.TextFontDefaultBold.MeasureString(firstCol.TrimEnd())
                            : !NormalTextMode ? Owner.TextFontMonoSpacedBold.MeasureString(firstCol.TrimEnd())
                            : Owner.TextFontDefault.MeasureString(firstCol.TrimEnd());
                    }

                    if (label.LastCol != null)
                    {
                        foreach (string data in label.LastCol)
                        {
                            if (data != null)
                            {
                                data.Replace("|", "", StringComparison.OrdinalIgnoreCase);
                                if (ColorCodeCtrl.Keys.Any(data.EndsWith))
                                {
                                    var tempLastCol = data.Substring(0, data.Length - 3);
                                    lastColWidth.Add(FontToBold ? Owner.TextFontDefaultBold.MeasureString(tempLastCol.TrimEnd())
                                        : Owner.TextFontDefault.MeasureString(tempLastCol.TrimEnd()));
                                }
                                else
                                {
                                    lastColWidth.Add(FontToBold ? Owner.TextFontDefaultBold.MeasureString(data.TrimEnd())
: Owner.TextFontDefault.MeasureString(data.TrimEnd()));
                                }
                            }
                        }
                    }
                }

                //Set a minimum value for LastColWidth to avoid overlap between time value and clickable symbol
                if (Labels.Count == 1)//&& lastColWidth.Count > 0)
                {
                    lastColWidth.Add(Labels[0].LastColWidth[0] + (TextSize * 3) + dpiOffset * 10);// time value + clickable symbol
                }

                Labels.Add(new ListLabel
                {
                    FirstCol = firstCol,
                    FirstColWidth = firstColWidth,
                    LastCol = lastCol,
                    LastColWidth = lastColWidth,
                    SymbolCol = symbolCol,
                    ChangeColWidth = changeColwidth,
                    KeyPressed = keyPressed
                });

                //ResizeWindow, when the string spans over the right boundary of the window
                if (!ResizeWindow)
                {
                    if (maxFirstColWidth < firstColWidth)
                        FirstColOverFlow = maxFirstColWidth;

                    if (label.LastColWidth != null)
                    {
                        for (int i = 0; i < label.LastColWidth.Count; i++)
                        {
                            if (maxLastColWidth < lastColWidth[i])
                                LastColOverFlow = maxLastColWidth;
                        }
                    }
                    ResizeWindow = true;
                }
            }
            else
            {
                if (Visible)
                {
                    // Detect Autopilot is on to avoid flickering when slim window is displayed
                    bool AutopilotOn = Owner.Viewer.PlayerLocomotive.Train.TrainType == TrainType.AiPlayerHosting;

                    //ResizeWindow, when the string spans over the right boundary of the window
                    maxFirstColWidth = Labels.Max(x => x.FirstColWidth);
                    maxLastColWidth = Labels.Max(x => x.LastColWidth[0]);

                    if (!ResizeWindow & (FirstColOverFlow != maxFirstColWidth || (!AutopilotOn && LastColOverFlow != maxLastColWidth)))
                    {
                        LastColOverFlow = maxLastColWidth;
                        FirstColOverFlow = maxFirstColWidth;
                        ResizeWindow = true;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve a formatted list <see cref="ListLabel"/>s to be displayed as an in-browser Track Monitor.
        /// </summary>
        /// <param name="viewer">The Viewer to read train data from.</param>
        /// <returns>A list of <see cref="ListLabel"/>s, one per row of the popup.</returns>
        public IEnumerable<ListLabel> TrainDPUWindowList(Viewer viewer, bool normalTextMode)
        {
            bool useMetric = viewer.MilepostUnitsMetric;
            Labels = new List<ListLabel>();
            void AddLabel(ListLabel label)
            {
                CheckLabel(ref label, normalTextMode);
            }
            void AddSeparator() => AddLabel(new ListLabel
            {
                FirstCol = "Sprtr",
            });

            TrainCar trainCar = viewer.PlayerLocomotive;
            Train train = trainCar.Train;
            MSTSLocomotive locomotive = (MSTSLocomotive)trainCar;
            var multipleUnitsConfiguration = locomotive.GetMultipleUnitsConfiguration();
            var lastCol = new List<string>();
            var symbolCol = new List<string>();
            var notDpuTrain = false;

            Labels.Clear();
            UpdateDataEnded = false;

            // Distributed Power
            if (multipleUnitsConfiguration != null)
            {
                lastCol = new List<string>();
                symbolCol = new List<string>();
                char[] multipleUnits = multipleUnitsConfiguration.Replace(" ", "", StringComparison.OrdinalIgnoreCase).ToCharArray();
                symbolCol.Add("");//first symbol empty
                foreach (char ch in multipleUnits)
                {
                    if (ch.ToString() != " ")
                    {
                        if (Char.IsDigit(ch))
                        {
                            lastCol.Add(ch.ToString());
                            continue;
                        }
                        else
                            symbolCol.Add(ch == '|' ? Symbols.Fence + ColorCode[Color.Green] : ch == '–' ? ch.ToString() : "");
                    }
                }

                // allows to draw the second fence
                lastCol.Add("");
                symbolCol.Add("");
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Loco Groups"),
                    SymbolCol = symbolCol,
                    LastCol = lastCol
                });
                AddSeparator();
            }
            else
            {
                lastCol = new List<string>();
                symbolCol = new List<string>();
                lastCol.Add("");
                symbolCol.Add("");
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString(" Distributed power management not available with this player train. "),
                    SymbolCol = symbolCol,
                    LastCol = lastCol
                });
                notDpuTrain = true;
            }

            if (locomotive != null && !notDpuTrain)
            {
                int numberOfDieselLocomotives = 0;
                int maxNumberOfEngines = 0;
                for (var i = 0; i < train.Cars.Count; i++)
                {
                    if (train.Cars[i] is MSTSDieselLocomotive)
                    {
                        numberOfDieselLocomotives++;
                        maxNumberOfEngines = Math.Max(maxNumberOfEngines, (train.Cars[i] as MSTSDieselLocomotive).DieselEngines.Count);
                    }
                }
                if (numberOfDieselLocomotives > 0)
                {
                    var dieselLoco = MSTSDieselLocomotive.GetDpuHeader(NormalVerticalMode, numberOfDieselLocomotives, maxNumberOfEngines).Replace("\t", "", StringComparison.OrdinalIgnoreCase);
                    string[] dieselLocoHeader = dieselLoco.Split('\n');
                    string[,] tempStatus = new string[numberOfDieselLocomotives, dieselLocoHeader.Length];
                    var k = 0;
                    RemoteControlGroup dpUnitId = RemoteControlGroup.FrontGroupSync;
                    var dpUId = -1;
                    for (var i = 0; i < train.Cars.Count; i++)
                    {
                        if (train.Cars[i] is MSTSDieselLocomotive)
                        {
                            if (dpUId != (train.Cars[i] as MSTSLocomotive).DistributedPowerUnitId)
                            {
                                var status = (train.Cars[i] as MSTSDieselLocomotive).GetDpuStatus(NormalVerticalMode).Split('\t');
                                var fence = ((dpUnitId != (dpUnitId = train.Cars[i].RemoteControlGroup)) ? "|" : " ");
                                for (var j = 0; j < status.Length; j++)
                                {
                                    // fence
                                    tempStatus[k, j] = fence + status[j];
                                }
                                dpUId = (train.Cars[i] as MSTSLocomotive).DistributedPowerUnitId;
                                k++;
                            }
                        }
                    }

                    dieselLocomotivesCount = k;// only leaders loco group
                    for (var j = 0; j < dieselLocoHeader.Length; j++)
                    {
                        lastCol = new List<string>();
                        symbolCol = new List<string>();

                        for (int i = 0; i < dieselLocomotivesCount; i++)
                        {
                            symbolCol.Add(tempStatus[i, j] != null && tempStatus[i, j].Contains('|', StringComparison.OrdinalIgnoreCase) ? Symbols.Fence + ColorCode[Color.Green] : " ");
                            lastCol.Add(tempStatus[i, j]);
                        }

                        // allows to draw the second fence
                        lastCol.Add("");
                        symbolCol.Add(" ");

                        AddLabel(new ListLabel
                        {
                            FirstCol = dieselLocoHeader[j],
                            SymbolCol = symbolCol,
                            LastCol = lastCol
                        });
                    }
                }
                AddLabel(new ListLabel());
            }

            AddLabel(new ListLabel());
            UpdateDataEnded = true;
            return Labels;
        }


        public override void PrepareFrame(in ElapsedTime elapsedTime, bool updateFull)
        {
            base.PrepareFrame(elapsedTime, updateFull);

            // Avoid to updateFull when the window is moving
            if (!dragged && !TrainDpuUpdating && updateFull)
            {
                TrainDpuUpdating = true;
                Labels = TrainDPUWindowList(Owner.Viewer, NormalTextMode).ToList();
                TrainDpuUpdating = false;

                //Resize this window when the cars count has been changed
                if (Owner.Viewer.PlayerTrain.Cars.Count != LastPlayerTrainCars)
                {
                    LastPlayerTrainCars = Owner.Viewer.PlayerTrain.Cars.Count;
                    UpdateWindowSize();
                }
                //Update Layout
                Layout();
            }
        }
    }
}