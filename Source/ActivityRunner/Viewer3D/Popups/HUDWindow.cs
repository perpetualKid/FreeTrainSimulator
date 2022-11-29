// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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
#define SHOW_PHYSICS_GRAPHS     //Matej Pacha - if commented, the physics graphs are not ready for public release

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Info;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class HUDWindow : LayeredWindow
    {
        // Set this to the width of each column in font-height units.
        private readonly int ColumnWidth = 5;

        // Set to distance from top-left corner to place text.
        private const int TextOffset = 10;
        private readonly Viewer Viewer;
        private readonly Action<TableData>[] TextPages;
        private readonly WindowTextFont TextFont;

        //Set lines rows HUDScroll.
        public int nLinesShow;
        public int charFitPerLine;
        public int columnsCount;
        public int headerToRestore;
        public int PathHeaderColumn;
        public static int columnsChars;
        public int[] lineOffsetLocoInfo = { 0, 0, 0, 0, 0, 0 };
        public static int hudWindowLinesActualPage = 1;
        public static int hudWindowLinesPagesCount = 1;
        public static int hudWindowColumnsActualPage;
        public static int hudWindowColumnsPagesCount;
        public static int hudWindowLocoPagesCount = 1;
        public static bool hudWindowFullScreen;
        public static bool hudWindowHorizontalScroll;
        public static bool hudWindowSteamLocoLead;
        public static bool BrakeInfoVisible;

        private int TextPage;
        private TableData TextTable = new TableData() { Cells = new string[0, 0] };

        public HUDWindow(WindowManager owner)
            : base(owner, TextOffset, TextOffset, "HUD")
        {
            Viewer = owner.Viewer;

            var textPages = new List<Action<TableData>>();
            textPages.Add(TextPageCommon);
            textPages.Add(TextPagePowerSupplyInfo);
            TextPages = textPages.ToArray();

            TextFont = owner.TextFontMonoSpacedOutlined;

            ColumnWidth *= TextFont.Height;
        }

        public override void TabAction()
        {
            TextPage = (TextPage + 1) % TextPages.Length;
            if (TextPage != 0)
            {
                lResetHudScroll = false;
            }
        }

        public override void PrepareFrame(in ElapsedTime elapsedTime, bool updateFull)
        {
            base.PrepareFrame(elapsedTime, updateFull);

            if (updateFull)
            {
                var table = new TableData() { Cells = new string[TextTable.Cells.GetLength(0), TextTable.Cells.GetLength(1)] };
                //Normal screen or full screen
                if (!hudWindowFullScreen)
                    TextPages[0](table);

                if (TextPage > 0)
                    TextPages[TextPage](table);
                TextTable = table;
            }
        }

        // ==========================================================================================================================================
        //      Method to construct the various Heads Up Display pages for use by the WebServer 
        //      Replaces the Prepare Frame Method
        //      djr - 20171221
        // ==========================================================================================================================================
        public TableData PrepareTable(int PageNo)
        {
            var table = new TableData() { Cells = new string[1, 1] };
            PageNo = MathHelper.Clamp(PageNo, 0, TextPages.Length - 1);
            TextPages[PageNo](table);
            return (table);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // Completely customise the rendering of the HUD - don't call base.Draw(spriteBatch).
            for (var row = 0; row < TextTable.Cells.GetLength(0); row++)
            {
                for (var column = 0; column < TextTable.Cells.GetLength(1); column++)
                {
                    if (TextTable.Cells[row, column] != null)
                    {
                        var text = TextTable.Cells[row, column];
                        var align = text.StartsWith(" ") ? LabelAlignment.Right : LabelAlignment.Left;
                        var color = Color.White;
                        if (text.Contains("!!!"))
                        {//Change to red color, an example: overspeed.
                            color = Color.OrangeRed;
                            text = text.Contains("!!!") && text.Contains("???") ? text.Substring(0, text.Length - 6) : text.Substring(0, text.Length - 3);
                        }
                        else if (text.EndsWith("!!!") || text.EndsWith("???"))
                        {
                            color = text.EndsWith("!!!") ? Color.OrangeRed : Color.Yellow;
                            text = text.Substring(0, text.Length - 3);
                        }
                        else if (text.EndsWith("%%%"))
                        {
                            color = Color.Cyan;
                            text = text.Substring(0, text.Length - 3);
                        }
                        else if (text.EndsWith("$$$"))
                        {
                            color = Color.Pink;
                            text = text.Substring(0, text.Length - 3);
                        }
                        TextFont.Draw(spriteBatch, new Rectangle(TextOffset + column * ColumnWidth, TextOffset + row * TextFont.Height, ColumnWidth, TextFont.Height), Point.Zero, text, align, color);
                    }
                }
            }
        }

        #region Table handling
        public sealed class TableData
        {
            public string[,] Cells;
            public int CurrentRow;
            public int CurrentLabelColumn;
            public int CurrentValueColumn;
        }

        private static void TableSetCell(TableData table, int cellColumn, string format, params object[] args)
        {
            TableSetCell(table, table.CurrentRow, cellColumn, format, args);
        }

        private static void TableSetCell(TableData table, int cellRow, int cellColumn, string format, params object[] args)
        {
            if (cellRow > table.Cells.GetUpperBound(0) || cellColumn > table.Cells.GetUpperBound(1))
            {
                var newCells = new string[Math.Max(cellRow + 1, table.Cells.GetLength(0)), Math.Max(cellColumn + 1, table.Cells.GetLength(1))];
                for (var row = 0; row < table.Cells.GetLength(0); row++)
                    for (var column = 0; column < table.Cells.GetLength(1); column++)
                        newCells[row, column] = table.Cells[row, column];
                table.Cells = newCells;
            }
            Debug.Assert(!format.Contains('\n'), "HUD table cells must not contain newlines. Use the table positioning instead.");
            table.Cells[cellRow, cellColumn] = args.Length > 0 ? string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args) : format;
        }

        private static void TableSetCells(TableData table, int startColumn, params string[] columns)
        {
            for (var i = 0; i < columns.Length; i++)
                TableSetCell(table, startColumn + i, columns[i]);
        }

        private static void TableAddLine(TableData table)
        {
            table.CurrentRow++;
        }

        private static void TableAddLine(TableData table, string format, params object[] args)
        {
            TableSetCell(table, table.CurrentRow, 0, format, args);
            table.CurrentRow++;
        }

        private static void TableSetLabelValueColumns(TableData table, int labelColumn, int valueColumn)
        {
            table.CurrentLabelColumn = labelColumn;
            table.CurrentValueColumn = valueColumn;
        }

        private static void TableAddLabelValue(TableData table, string label, string format, params object[] args)
        {
            TableSetCell(table, table.CurrentRow, table.CurrentLabelColumn, label);
            TableSetCell(table, table.CurrentRow, table.CurrentValueColumn, format, args);
            table.CurrentRow++;
        }
        #endregion

        private void TextPageCommon(TableData table)
        {
            var playerTrain = Viewer.PlayerLocomotive.Train;
            var showMUReverser = Math.Abs(playerTrain.MUReverserPercent) != 100;
            var showRetainers = playerTrain.BrakeSystem.RetainerSetting != RetainerSetting.Exhaust;
            var engineBrakeStatus = Viewer.PlayerLocomotive.GetEngineBrakeStatus();
            var brakemanBrakeStatus = Viewer.PlayerLocomotive.GetBrakemanBrakeStatus();
            var dynamicBrakeStatus = Viewer.PlayerLocomotive.GetDistributedPowerDynamicBrakeStatus();
            var multipleUnitsConfiguration = (Viewer.PlayerLocomotive as MSTSDieselLocomotive)?.GetMultipleUnitsConfiguration();
            var stretched = playerTrain.Cars.Count > 1 && playerTrain.CouplersPulled == playerTrain.Cars.Count - 1;

            TableSetLabelValueColumns(table, 0, 2);
            TableAddLabelValue(table, Viewer.Catalog.GetString("Version"), VersionInfo.Version);


            TableAddLabelValue(table, Viewer.PlayerLocomotive is MSTSSteamLocomotive ? Viewer.Catalog.GetString("Regulator") : Viewer.Catalog.GetString("Throttle"), "{0:F0}%",
                Viewer.PlayerLocomotive.ThrottlePercent,
                Viewer.PlayerLocomotive is MSTSDieselLocomotive && Viewer.PlayerLocomotive.Train.DistributedPowerMode == DistributedPowerMode.Traction ? $"({Viewer.PlayerLocomotive.Train.DPThrottlePercent}%)" : "");
            if (showRetainers)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Retainers"), "{0}% {1}", playerTrain.BrakeSystem.RetainerPercent, playerTrain.BrakeSystem.RetainerSetting.GetLocalizedDescription());
            if ((Viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeFitted) // ideally this test should be using "engineBrakeStatus != null", but this currently does not work, as a controller is defined by default
                TableAddLabelValue(table, Viewer.Catalog.GetString("Engine brake"), "{0}", engineBrakeStatus);
            if ((Viewer.PlayerLocomotive as MSTSLocomotive).BrakemanBrakeFitted)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Brakemen brake"), "{0}", brakemanBrakeStatus);
            if (dynamicBrakeStatus != null)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Dynamic brake"), "{0}", dynamicBrakeStatus);

            if (multipleUnitsConfiguration != null)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Multiple Units"), "{0}", multipleUnitsConfiguration);
            TableAddLine(table);
            TableAddLabelValue(table, Viewer.Catalog.GetString("FPS"), "{0:F0}", 0);
            TableAddLine(table);


            if (Viewer.PlayerLocomotive.Sander)
            {
                var sanderBlocked = Viewer.PlayerLocomotive is MSTSLocomotive && Math.Abs(playerTrain.SpeedMpS) > ((MSTSLocomotive)Viewer.PlayerLocomotive).SanderSpeedOfMpS;
                if (sanderBlocked)
                    TableAddLine(table, Viewer.Catalog.GetString("Sander blocked") + "!!!");
                else
                    TableAddLine(table, Viewer.Catalog.GetString("Sander on") + "???");
            }

            if ((Viewer.PlayerLocomotive as MSTSWagon).DoorLeftOpen || (Viewer.PlayerLocomotive as MSTSWagon).DoorRightOpen)
            {
                var color = Math.Abs(Viewer.PlayerLocomotive.SpeedMpS) > 0.1f ? "!!!" : "???";
                var status = "";
                if ((Viewer.PlayerLocomotive as MSTSWagon).DoorLeftOpen)
                    status += (Viewer.PlayerLocomotive as MSTSLocomotive).GetCabFlipped() ? Viewer.Catalog.GetString("Right") : Viewer.Catalog.GetString("Left");
                if ((Viewer.PlayerLocomotive as MSTSWagon).DoorRightOpen)
                {
                    if (status.Length > 0)
                        status += " ";
                    status += $"{((Viewer.PlayerLocomotive as MSTSLocomotive).GetCabFlipped() ? Viewer.Catalog.GetString("Left") : Viewer.Catalog.GetString("Right"))}";
                }
                status += color;

                TableAddLabelValue(table, Viewer.Catalog.GetString("Doors open") + color, status);
            }
            if (MultiPlayerManager.MultiplayerState != MultiplayerState.None)
            {
                var text = MultiPlayerManager.Instance().GetOnlineUsersInfo();

                TableAddLabelValue(table, Viewer.Catalog.GetString("MultiPlayerStatus: "), "{0}", MultiPlayerManager.Instance().GetMultiPlayerStatus());
                TableAddLine(table);
                foreach (var t in text.Split('\t'))
                    TableAddLine(table, "{0}", t);
            }
        }

        private void TextPagePowerSupplyInfo(TableData table)
        {
            TextPageHeading(table, Viewer.Catalog.GetString("POWER SUPPLY INFORMATION"));

            Train train = Viewer.PlayerLocomotive.Train;

            TableAddLine(table);
            TableSetCells(table, 0,
                    Viewer.Catalog.GetString("Wagon"),
                    Viewer.Catalog.GetString("Type"),
                    Viewer.Catalog.GetParticularString("Pantograph", "Panto"),
                    Viewer.Catalog.GetParticularString("Engine", "Eng"),
                    Viewer.Catalog.GetParticularString("CircuitBreaker", "CB"),
                    Viewer.Catalog.GetParticularString("TractionCutOffRelay", "TCOR"),
                    Viewer.Catalog.GetString("MainPS"),
                    Viewer.Catalog.GetString("AuxPS"),
                    Viewer.Catalog.GetString("Battery"),
                    Viewer.Catalog.GetString("LowVoltPS"),
                    Viewer.Catalog.GetString("CabPS"),
                    Viewer.Catalog.GetString("ETS"),
                    Viewer.Catalog.GetString("ETSCable"),
                    Viewer.Catalog.GetString("Power")
                );
            foreach (TrainCar car in train.Cars.Where(car => car.PowerSupply != null))
            {
                IPowerSupply powerSupply = car.PowerSupply;
                ILocomotivePowerSupply locomotivePowerSupply = powerSupply as ILocomotivePowerSupply;

                string pantographState = string.Empty;
                string dieselEngineState = string.Empty;
                string circuitBreakerState = string.Empty;
                string tractionCutOffRelayState = string.Empty;
                string mainPowerSupplyState = string.Empty;
                string auxiliaryPowerSupplyState = string.Empty;
                string electricTrainSupplyState = string.Empty;
                string electricTrainSupplyCableState = string.Empty;
                string electricTrainSupplyPower = string.Empty;

                if (powerSupply is ScriptedElectricPowerSupply electricPowerSupply)
                {
                    pantographState = (car as MSTSWagon).Pantographs.State.GetLocalizedDescription();
                    circuitBreakerState = electricPowerSupply.CircuitBreaker.State.GetLocalizedDescription();
                    mainPowerSupplyState = locomotivePowerSupply.MainPowerSupplyState.GetLocalizedDescription();
                    auxiliaryPowerSupplyState = locomotivePowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
                    if (locomotivePowerSupply.ElectricTrainSupplyState != PowerSupplyState.Unavailable)
                    {
                        electricTrainSupplyState = car.PowerSupply.ElectricTrainSupplyState.GetLocalizedDescription();
                        electricTrainSupplyCableState = car.PowerSupply.FrontElectricTrainSupplyCableConnected ? Viewer.Catalog.GetString("connected") : Viewer.Catalog.GetString("disconnected");
                        electricTrainSupplyPower = FormatStrings.FormatPower(locomotivePowerSupply.ElectricTrainSupplyPowerW, true, false, false);
                    }
                }
                else if (powerSupply is ScriptedDieselPowerSupply dieselPowerSupply)
                {
                    dieselEngineState = (car as MSTSDieselLocomotive).DieselEngines.State.GetLocalizedDescription();
                    tractionCutOffRelayState = dieselPowerSupply.TractionCutOffRelay.State.GetLocalizedDescription();
                    mainPowerSupplyState = locomotivePowerSupply.MainPowerSupplyState.GetLocalizedDescription();
                    auxiliaryPowerSupplyState = locomotivePowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
                    if (locomotivePowerSupply.ElectricTrainSupplyState != PowerSupplyState.Unavailable)
                    {
                        electricTrainSupplyState = car.PowerSupply.ElectricTrainSupplyState.GetLocalizedDescription();
                        electricTrainSupplyCableState = car.PowerSupply.FrontElectricTrainSupplyCableConnected ? Viewer.Catalog.GetString("connected") : Viewer.Catalog.GetString("disconnected");
                        electricTrainSupplyPower = FormatStrings.FormatPower(locomotivePowerSupply.ElectricTrainSupplyPowerW, true, false, false);
                    }
                }
                else if (powerSupply is ScriptedDualModePowerSupply dualModePowerSupply)
                {
                    pantographState = (car as MSTSWagon).Pantographs.State.GetLocalizedDescription();
                    // TODO with DualModeLocomotive : dieselEngineState = Viewer.Catalog.GetParticularString("Engine", GetStringAttribute.GetPrettyName((car as MSTSDualModeLocomotive).DieselEngines.State));
                    circuitBreakerState = dualModePowerSupply.CircuitBreaker.State.GetLocalizedDescription();
                    tractionCutOffRelayState = dualModePowerSupply.TractionCutOffRelay.State.GetLocalizedDescription();
                    mainPowerSupplyState = locomotivePowerSupply.MainPowerSupplyState.GetLocalizedDescription();
                    auxiliaryPowerSupplyState = locomotivePowerSupply.AuxiliaryPowerSupplyState.GetLocalizedDescription();
                    if (locomotivePowerSupply.ElectricTrainSupplyState != PowerSupplyState.Unavailable)
                    {
                        electricTrainSupplyState = car.PowerSupply.ElectricTrainSupplyState.GetLocalizedDescription();
                        electricTrainSupplyCableState = car.PowerSupply.FrontElectricTrainSupplyCableConnected ? Viewer.Catalog.GetString("connected") : Viewer.Catalog.GetString("disconnected");
                        electricTrainSupplyPower = FormatStrings.FormatPower(locomotivePowerSupply.ElectricTrainSupplyPowerW, true, false, false);
                    }
                }
                else if (powerSupply is IPassengerCarPowerSupply passengerCarPowerSupply)
                {
                    if (passengerCarPowerSupply.ElectricTrainSupplyState != PowerSupplyState.Unavailable)
                    {
                        electricTrainSupplyState = car.PowerSupply.ElectricTrainSupplyState.GetLocalizedDescription();
                        electricTrainSupplyCableState = car.PowerSupply.FrontElectricTrainSupplyCableConnected ? Viewer.Catalog.GetString("connected") : Viewer.Catalog.GetString("disconnected");
                        electricTrainSupplyPower = FormatStrings.FormatPower(passengerCarPowerSupply.ElectricTrainSupplyPowerW, true, false, false);
                    }
                }
                // If power supply is steam power supply, do nothing.

                TableAddLine(table);
                TableSetCells(table, 0,
                    car.CarID,
                    car.WagonType.ToString(),
                    pantographState,
                    dieselEngineState,
                    circuitBreakerState,
                    tractionCutOffRelayState,
                    mainPowerSupplyState,
                    auxiliaryPowerSupplyState,
                    car.PowerSupply.BatteryState.GetLocalizedDescription(),
                    car.PowerSupply.LowVoltagePowerSupplyState.GetLocalizedDescription(),
                    locomotivePowerSupply != null ? locomotivePowerSupply.CabPowerSupplyState.GetLocalizedDescription() : string.Empty,
                    electricTrainSupplyState,
                    electricTrainSupplyCableState,
                    electricTrainSupplyPower
                    );
            }
        }

        private bool lResetHudScroll;

        private static void TextPageHeading(TableData table, string name)
        {
            TableAddLine(table);
            TableAddLine(table, name);
        }
    }
}
