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
using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Info;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
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
        private List<string> stringStatus = new List<string>();
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
            textPages.Add(TextPageBrakeInfo);
            textPages.Add(TextPageForceInfo);
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

        private static void TableAddLines(TableData table, string lines)
        {
            if (lines == null)
                return;

            foreach (var line in lines.Split('\n'))
            {
                var column = 0;
                foreach (var cell in line.Split('\t'))
                    TableSetCell(table, column++, "{0}", cell);
                table.CurrentRow++;
            }
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
            var bunched = !stretched && playerTrain.Cars.Count > 1 && playerTrain.CouplersPushed == playerTrain.Cars.Count - 1;

            TableSetLabelValueColumns(table, 0, 2);
            TableAddLabelValue(table, Viewer.Catalog.GetString("Version"), VersionInfo.Version);

            // Client and server may have a time difference.
            if (MultiPlayerManager.MultiplayerState == MultiplayerState.Client)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Time"), FormatStrings.FormatTime(Viewer.Simulator.ClockTime + MultiPlayerManager.Instance().ServerTimeDifference));
            else
                TableAddLabelValue(table, Viewer.Catalog.GetString("Time"), FormatStrings.FormatTime(Viewer.Simulator.ClockTime));

            if (Viewer.Simulator.IsReplaying)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Replay"), FormatStrings.FormatTime(Viewer.Log.ReplayEndsAt - Viewer.Simulator.ClockTime));

            TableAddLabelValue(table, Viewer.Catalog.GetString("Speed"), FormatStrings.FormatSpeedDisplay(Viewer.PlayerLocomotive.SpeedMpS, Simulator.Instance.MetricUnits));
            TableAddLabelValue(table, Viewer.Catalog.GetString("Gradient"), "{0:F1}%", Viewer.PlayerLocomotive.CurrentElevationPercent);
            TableAddLabelValue(table, Viewer.Catalog.GetString("Direction"), showMUReverser ? "{1:F0} {0}" : "{0}", Viewer.PlayerLocomotive.Direction.GetLocalizedDescription(), Math.Abs(playerTrain.MUReverserPercent));
            TableAddLabelValue(table, Viewer.PlayerLocomotive is MSTSSteamLocomotive ? Viewer.Catalog.GetString("Regulator") : Viewer.Catalog.GetString("Throttle"), "{0:F0}%",
                Viewer.PlayerLocomotive.ThrottlePercent,
                Viewer.PlayerLocomotive is MSTSDieselLocomotive && Viewer.PlayerLocomotive.Train.DistributedPowerMode == DistributedPowerMode.Traction ? $"({Viewer.PlayerLocomotive.Train.DPThrottlePercent}%)" : "");
            if ((Viewer.PlayerLocomotive as MSTSLocomotive).TrainBrakeFitted)
                TableAddLabelValue(table, Viewer.Catalog.GetString("Train brake"), "{0}", Viewer.PlayerLocomotive.GetTrainBrakeStatus());
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

            if (Viewer.PlayerLocomotive.Train.TrainType == TrainType.AiPlayerHosting)
                TableAddLine(table, Viewer.Catalog.GetString("Autopilot") + "???");

            if (Viewer.PlayerTrain.IsWheelSlip)
                TableAddLine(table, Viewer.Catalog.GetString("Wheel slip") + "!!!");
            else if (Viewer.PlayerTrain.IsWheelSlipWarninq)
                TableAddLine(table, Viewer.Catalog.GetString("Wheel slip warning") + "???");

            if (Viewer.PlayerTrain.IsBrakeSkid)
                TableAddLine(table, Viewer.Catalog.GetString("Wheel skid") + "!!!");

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

        private void TextPageBrakeInfo(TableData table)
        {
            TextPageHeading(table, Viewer.Catalog.GetString("BRAKE INFORMATION"));

            ResetHudScroll(); //Reset Hudscroll.

            var train = Viewer.PlayerLocomotive.Train;
            var mstsLocomotive = Viewer.PlayerLocomotive as MSTSLocomotive;
            var HUDEngineType = mstsLocomotive.EngineType;

            if ((Viewer.PlayerLocomotive as MSTSLocomotive).TrainBrakeFitted) // Only display the following information if a train brake is defined.
            {
                // If vacuum brakes are used then use this display
                if ((Viewer.PlayerLocomotive as MSTSLocomotive).BrakeSystem is VacuumSinglePipe)
                {
                    if ((Viewer.PlayerLocomotive as MSTSLocomotive).VacuumBrakeEQFitted)
                    {
                        TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}\t\t{2}\t{3}\t\t{4}",
                        Viewer.Catalog.GetString("PlayerLoco"),
                        Viewer.Catalog.GetString("Main reservoir"),
                        FormatStrings.FormatPressure(Pressure.Vacuum.FromPressure((Viewer.PlayerLocomotive as MSTSLocomotive).VacuumMainResVacuumPSIAorInHg), Pressure.Unit.InHg, Pressure.Unit.InHg, true),
                        Viewer.Catalog.GetString("Exhauster"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).VacuumExhausterIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off")));
                    }

                    else if ((Viewer.PlayerLocomotive as MSTSLocomotive).VacuumPumpFitted && (Viewer.PlayerLocomotive as MSTSLocomotive).SmallEjectorControllerFitted)
                    {
                        // Display if vacuum pump, large ejector and small ejector fitted
                        TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}\t\t{2}\t{3}\t\t{4}\t{5}\t{6}\t\t{7}\t\t{8}",
                        Viewer.Catalog.GetString("PlayerLoco"),
                        Viewer.Catalog.GetString("Large Ejector"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).LargeSteamEjectorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off"),
                        Viewer.Catalog.GetString("Small Ejector"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).SmallSteamEjectorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off"),
                        Viewer.Catalog.GetString("Pressure"),
                        FormatStrings.FormatPressure((Viewer.PlayerLocomotive as MSTSLocomotive).SteamEjectorSmallPressurePSI, Pressure.Unit.PSI, (Viewer.PlayerLocomotive as MSTSLocomotive).BrakeSystemPressureUnits[BrakeSystemComponent.MainReservoir], true),
                        Viewer.Catalog.GetString("Vacuum Pump"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).VacuumPumpOperating ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off")
                        ));
                    }
                    else if ((Viewer.PlayerLocomotive as MSTSLocomotive).VacuumPumpFitted && !(Viewer.PlayerLocomotive as MSTSLocomotive).SmallEjectorControllerFitted) // Change display so that small ejector is not displayed for vacuum pump operated locomotives
                    {
                        // Display if vacuum pump, and large ejector only fitted
                        TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}\t\t{2}\t{3}\t\t{4}",
                        Viewer.Catalog.GetString("PlayerLoco"),
                        Viewer.Catalog.GetString("Large Ejector"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).LargeSteamEjectorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off"),
                        Viewer.Catalog.GetString("Vacuum Pump"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).VacuumPumpOperating ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off")));
                    }
                    else
                    {
                        // Display if large ejector and small ejector only fitted
                        TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}\t\t{2}\t{3}\t\t{4}\t{5}\t{6}",
                        Viewer.Catalog.GetString("PlayerLoco"),
                        Viewer.Catalog.GetString("Large Ejector"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).LargeSteamEjectorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off"),
                        Viewer.Catalog.GetString("Small Ejector"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).SmallSteamEjectorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off"),
                        Viewer.Catalog.GetString("Pressure"),
                        FormatStrings.FormatPressure((Viewer.PlayerLocomotive as MSTSLocomotive).SteamEjectorSmallPressurePSI, Pressure.Unit.PSI, (Viewer.PlayerLocomotive as MSTSLocomotive).BrakeSystemPressureUnits[BrakeSystemComponent.MainReservoir], true)));
                    }

                    // Lines to show brake system volumes
                    TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}\t\t{2}\t{3}\t\t{4}\t{5}\t{6}",
                    Viewer.Catalog.GetString("Brake Sys Vol"),
                    Viewer.Catalog.GetString("Train Pipe"),
                    FormatStrings.FormatVolume(train.BrakeSystem.TotalTrainBrakePipeVolume, Simulator.Instance.MetricUnits),
                    Viewer.Catalog.GetString("Brake Cyl"),
                    FormatStrings.FormatVolume(train.BrakeSystem.TotalTrainBrakeCylinderVolume, Simulator.Instance.MetricUnits),
                    Viewer.Catalog.GetString("Air Vol"),
                    FormatStrings.FormatVolume(train.BrakeSystem.TotalCurrentTrainBrakeSystemVolume, Simulator.Instance.MetricUnits)
                    ));
                }
                else  // Default to air or electronically braked, use this display
                {
                    if ((Viewer.PlayerLocomotive as MSTSLocomotive).EngineType == EngineType.Control)
                    {
                        // Control cars typically don't have reservoirs
                        TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}",
                            Viewer.Catalog.GetString("PlayerLoco"),
                            Viewer.Catalog.GetString("No compressor or reservoir fitted")
                            ));
                    }
                    else
                    {
                        TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t\t{1}\t\t{2}\t{3}\t\t{4}",
                        Viewer.Catalog.GetString("PlayerLoco"),
                        Viewer.Catalog.GetString("Main reservoir"),
                        FormatStrings.FormatPressure((Viewer.PlayerLocomotive as MSTSLocomotive).MainResPressurePSI, Pressure.Unit.PSI, (Viewer.PlayerLocomotive as MSTSLocomotive).BrakeSystemPressureUnits[BrakeSystemComponent.MainReservoir], true),
                        Viewer.Catalog.GetString("Compressor"),
                        (Viewer.PlayerLocomotive as MSTSLocomotive).CompressorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off")));
                    }
                }

                // Display data for other locomotives
                for (var i = 0; i < train.Cars.Count; i++)
                {
                    var car = train.Cars[i];
                    if (car is MSTSLocomotive && car != Viewer.PlayerLocomotive)
                    {
                        if ((car as MSTSLocomotive).EngineType == EngineType.Control)
                        {
                            // Control cars typically don't have reservoirs
                            TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t{1}",
                                Viewer.Catalog.GetString("Loco"),
                                car.CarID));
                        }
                        else
                        {
                            TableAddLines(table, string.Format(CultureInfo.CurrentCulture, "{0}\t{1}\t{2}\t\t{3}\t{4}\t\t{5}",
                                Viewer.Catalog.GetString("Loco"),
                                car.CarID,
                                Viewer.Catalog.GetString("Main reservoir"),
                                FormatStrings.FormatPressure((car as MSTSLocomotive).MainResPressurePSI, Pressure.Unit.PSI, (car as MSTSLocomotive).BrakeSystemPressureUnits[BrakeSystemComponent.MainReservoir], true),
                                Viewer.Catalog.GetString("Compressor"),
                                (car as MSTSLocomotive).CompressorIsOn ? Viewer.Catalog.GetString("on") : Viewer.Catalog.GetString("off")));
                        }
                    }
                }
                TableAddLine(table);
            }
            //Initialize
            List<string> statusBrake = new List<string>();
            List<string> statusHeader = new List<string>();
            string[] stringStatusToList;//Allow to change data from TableAddLines to TableSetCell
            hudWindowLocoPagesCount = 0;
            int n = train.Cars.Count;

            // Different display depending upon whether vacuum braked, manual braked or air braked
            for (var i = 0; i < n; i++)
            {
                var car = train.Cars[i];
                if (car.BrakeSystem is VacuumSinglePipe)
                {
                    if ((Viewer.PlayerLocomotive as MSTSLocomotive).NonAutoBrakePresent) // Straight brake system
                    {
                        statusHeader.Add(string.Format(CultureInfo.CurrentCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}",
                        Viewer.Catalog.GetString("Car"),
                        Viewer.Catalog.GetString("Type"),
                        Viewer.Catalog.GetString("BrkCyl"),
                        Viewer.Catalog.GetString("BrkPipe"),
                        Viewer.Catalog.GetString(""),
                        Viewer.Catalog.GetString(""),
                        Viewer.Catalog.GetString(""),
                        Viewer.Catalog.GetString(""),
                        Viewer.Catalog.GetString(""),
                        Viewer.Catalog.GetString(""),
                        Viewer.Catalog.GetString("Handbrk"),
                        Viewer.Catalog.GetString("Conn"),
                        Viewer.Catalog.GetString("AnglCock")
                                                                                                    ));
                    }
                    else // automatic vacuum brake system
                    {
                        statusHeader.Add(string.Format(CultureInfo.CurrentCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}",
                        //0
                        Viewer.Catalog.GetString("Car"),
                        //1
                        Viewer.Catalog.GetString("Type"),
                        //2
                        Viewer.Catalog.GetString("BrkCyl"),
                        //3
                        Viewer.Catalog.GetString("BrkPipe"),
                        //4
                        Viewer.Catalog.GetString("VacRes"),
                        //5
                        Viewer.Catalog.GetString(""),
                        //6
                        Viewer.Catalog.GetString(""),
                        //7
                        Viewer.Catalog.GetString(""),
                        //8
                        Viewer.Catalog.GetString(""),
                        //9
                        Viewer.Catalog.GetString(""),
                        //10
                        Viewer.Catalog.GetString("Handbrk"),
                        //11
                        Viewer.Catalog.GetString("Conn"),
                        //12
                        Viewer.Catalog.GetString("AnglCock")

                        //Add new header data here, if addining additional column.

                        ));
                    }
                }
                else if (car.BrakeSystem is ManualBraking)
                {
                    statusHeader.Add(string.Format(CultureInfo.CurrentCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}",
                        //0
                        Viewer.Catalog.GetString("Car"),
                        //1
                        Viewer.Catalog.GetString("Type"),
                        //2
                        Viewer.Catalog.GetString("Brk"),
                        //3
                        Viewer.Catalog.GetString(""),
                        //4
                        Viewer.Catalog.GetString(""),
                        //5
                        Viewer.Catalog.GetString(""),
                        //6
                        Viewer.Catalog.GetString(""),
                        //7
                        Viewer.Catalog.GetString(""),
                        //8
                        Viewer.Catalog.GetString(""),
                        //9
                        Viewer.Catalog.GetString(""),
                        //10
                        Viewer.Catalog.GetString("Handbrk")
                    ));
                }
                else if ((Viewer.PlayerLocomotive as MSTSLocomotive).BrakeSystem is SMEBrakeSystem)
                {
                    statusHeader.Add(string.Format(CultureInfo.CurrentCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}",
                    //0
                    Viewer.Catalog.GetString("Car"),
                    //1
                    Viewer.Catalog.GetString("Type"),
                    //2
                    Viewer.Catalog.GetString("BrkCyl"),
                    //3
                    Viewer.Catalog.GetString("SrvPipe"),
                    //4
                    Viewer.Catalog.GetString("AuxRes"),
                    //5
                    Viewer.Catalog.GetString("ErgRes"),
                    //6
                    Viewer.Catalog.GetString("StrPipe"),
                    //7
                    Viewer.Catalog.GetString("RetValve"),
                    //8
                    Viewer.Catalog.GetString("TripleValve"),
                    //9
                    Viewer.Catalog.GetString(""),
                    //10
                    Viewer.Catalog.GetString("Handbrk"),
                    //11
                    Viewer.Catalog.GetString("Conn"),
                    //12
                    Viewer.Catalog.GetString("AnglCock"),
                    //13
                    Viewer.Catalog.GetString("BleedOff")
                    //Add new header data here, if adding additional column.
                    ));
                }
                else // default air braked
                {
                    statusHeader.Add(string.Format(CultureInfo.CurrentCulture, "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}",
                    //0
                    Viewer.Catalog.GetString("Car"),
                    //1
                    Viewer.Catalog.GetString("Type"),
                    //2
                    Viewer.Catalog.GetString("BrkCyl"),
                    //3
                    Viewer.Catalog.GetString("BrkPipe"),
                    //4
                    Viewer.Catalog.GetString("AuxRes"),
                    //5
                    Viewer.Catalog.GetString("ErgRes"),
                    //6
                    Viewer.Catalog.GetString("MRPipe"),
                    //7
                    Viewer.Catalog.GetString("RetValve"),
                    //8
                    Viewer.Catalog.GetString("TripleValve"),
                    //9
                    Viewer.Catalog.GetString(""),
                    //10
                    Viewer.Catalog.GetString("Handbrk"),
                    //11
                    Viewer.Catalog.GetString("Conn"),
                    //12
                    Viewer.Catalog.GetString("AnglCock"),
                    //13
                    Viewer.Catalog.GetString("BleedOff")

                    //Add new header data here, if addining additional column.

                    ));
                }
            }
            //TableAddLine(table);
            columnsCount = statusHeader[statusHeader.Count - 1].Count(x => x == '\t');
            //The lines that fit by pages.
            TextLineNumber(train.Cars.Count, table.CurrentRow + 1, columnsCount);

            for (var i = 0; i < train.Cars.Count; i++)
            {
                var j = (i == 0) ? 0 : i;
                var car = train.Cars[j];
                var statusString = "";
                foreach (var cell in car.BrakeSystem.GetDebugStatus((Viewer.PlayerLocomotive as MSTSLocomotive).BrakeSystemPressureUnits))
                {
                    statusString = statusString + cell + "\t";
                }
                if (statusString.StartsWith("1V") || statusString.StartsWith("ST"))
                {
                    var indexMatch = statusHeader.FindIndex(x => x.Contains(Viewer.Catalog.GetString("VacRes")));
                    if (!statusBrake.Contains(statusHeader[indexMatch]))//Avoid header duplicity
                        statusBrake.Add(statusHeader[indexMatch]);
                }
                else
                {
                    var indexMatch = statusHeader.FindIndex(x => x.Contains(Viewer.Catalog.GetString("AuxRes")));
                    if (!statusBrake.Contains(statusHeader[indexMatch]))//Avoid header duplicity
                        statusBrake.Add(statusHeader[indexMatch]);
                }
                statusBrake.Add(car.CarID + "\t" + statusString);
            }

            //HudScroll. Pages count from nLinesShow number.
            TextLineNumber(statusBrake.Count, table.CurrentRow, columnsCount);

            //Number of lines to show. HudScroll
            for (var i = (hudWindowLinesActualPage * nLinesShow) - nLinesShow; i < (statusBrake.Count > hudWindowLinesActualPage * nLinesShow ? hudWindowLinesActualPage * nLinesShow : statusBrake.Count); i++)
            {
                if (i > 0 && i < 2 && (statusBrake[i] == stringStatus[i - 1]) || i > 1 && (statusBrake[i - 2] == statusBrake[i]))
                    continue;

                if (statusBrake.Count > i)
                {
                    //Calc col number and take in count 1 left column carID
                    if (i > 0 && i % nLinesShow == 0 && hudWindowColumnsActualPage > 0)
                        TextColNumber(statusBrake[0], 0, false);
                    else
                        TextColNumber(statusBrake[i], 0, false);

                    //Update Brake Information view to show selected car from TrainOperationsWindow (F9)
                    //Once a car clicked CarOperationsWindow remains visible
                    BrakeInfoVisible = false;
                    //TODO 20221006 need some other way to map selected car from Train/Car operations Window
                    int carPosition = 0;
                    if (true)
                    {
                        int indexMatch = statusBrake.FindIndex(x => x.Contains(Viewer.Catalog.GetString((carPosition >= Viewer.PlayerTrain.Cars.Count ? " " : Viewer.PlayerTrain.Cars[carPosition].CarID))));
                        hudWindowLinesActualPage = (int)Math.Ceiling(Convert.ToDouble(indexMatch / nLinesShow) + 0.5);
                        BrakeInfoVisible = true;
                    }

                    //Add yellow color to string when car was selected at CarOperationWindow (F9).
                    stringStatusToList = (statusBrake[i].TrimEnd('\t').Split('\t'));//Convert to string[]

                    var EndText = stringStatusToList[0] == (carPosition >= Viewer.PlayerTrain.Cars.Count ? " " : Viewer.PlayerTrain.Cars[carPosition].CarID) ? "???" : "";
                    var arrow = "";

                    //if (Viewer.CarOperationsWindow.Visible)
                    //{
                    //    int indexMatch = statusBrake.FindIndex(x => x.Contains(Viewer.Catalog.GetString((Viewer.CarOperationsWindow.CarPosition >= Viewer.PlayerTrain.Cars.Count ? " " : Viewer.PlayerTrain.Cars[Viewer.CarOperationsWindow.CarPosition].CarID))));
                    //    hudWindowLinesActualPage = (int)Math.Ceiling(Convert.ToDouble(indexMatch / nLinesShow) + 0.5);
                    //    BrakeInfoVisible = true;
                    //}

                    ////Add yellow color to string when car was selected at CarOperationWindow (F9).
                    //stringStatusToList = (statusBrake[i].TrimEnd('\t').Split('\t'));//Convert to string[]

                    //var EndText = stringStatusToList[0] == (Viewer.CarOperationsWindow.CarPosition >= Viewer.PlayerTrain.Cars.Count ? " " : Viewer.PlayerTrain.Cars[Viewer.CarOperationsWindow.CarPosition].CarID) ? "???" : "";
                    //var arrow = "";

                    //DrawScrollArrows() can't be used because it works with TableAddLines, here we work with TableSetCell.
                    if (hudWindowColumnsActualPage > 0)
                    {
                        if (stringStatus.Count > 1 && stringStatus.Count <= hudWindowColumnsActualPage)
                        {
                            arrow = arrow + "◄";// \u25C0
                        }
                        else if (stringStatus.Count > 1 && hudWindowColumnsActualPage == 1)
                        {
                            arrow = arrow + "►";// \u25B6
                        }

                        if (i > 0 && i % nLinesShow == 0)
                        {//Display header when page was changed
                            TextColNumber(statusBrake[0], 0, false);
                            stringStatusToList = stringStatus[hudWindowColumnsActualPage - 1].TrimEnd('\t').Split('\t');// string[]
                            stringStatusToList[0] = arrow + stringStatusToList[0];
                            BrakeInfoData(table, stringStatusToList, "");
                            TableAddLine(table);
                            TextColNumber(statusBrake[i], 0, false);
                        }

                        if (stringStatus.Count >= hudWindowColumnsActualPage)
                        {//Avoid crash when not same brake system
                            stringStatusToList = stringStatus[hudWindowColumnsActualPage - 1].TrimEnd('\t').Split('\t');// string[]
                            stringStatusToList[0] = arrow + stringStatusToList[0];
                            BrakeInfoData(table, stringStatusToList, EndText);
                        }
                        else if (stringStatus.Count == 1)
                        {
                            stringStatusToList = stringStatus[0].TrimEnd('\t').Split('\t');// string[]
                            stringStatusToList[0] = arrow + stringStatusToList[0];
                            BrakeInfoData(table, stringStatusToList, EndText);
                        }
                    }
                    else
                    {   //hudWindowColumnsActualPage == 0.
                        if (i > 0 && i % nLinesShow == 0)
                        {//Display header when page was changed
                            stringStatusToList = statusBrake[0].TrimEnd('\t').Split('\t');// string[]
                            BrakeInfoData(table, stringStatusToList, "");
                            TableAddLine(table);
                        }

                        stringStatusToList = statusBrake[i].TrimEnd('\t').Split('\t');// string[]
                        BrakeInfoData(table, stringStatusToList, EndText);
                    }
                    TableAddLine(table);
                }
            }
        }

        /// <summary>
        /// Allow to draw brake info data.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="stringToDraw"></param>
        /// <param name="endtext"></param>
        private void BrakeInfoData(TableData table, string[] stringToDraw, string endtext)
        {
            for (int iCell = 0; iCell < stringToDraw.Length; iCell++)
                TableSetCell(table, table.CurrentRow, iCell, stringToDraw[iCell] + endtext);
        }

        private void TextPageForceInfo(TableData table)
        {
            TextPageHeading(table, Viewer.Catalog.GetString("FORCE INFORMATION"));
            bool isUK = Simulator.Instance.Settings.MeasurementUnit == MeasurementUnit.UK;

            var train = Viewer.PlayerLocomotive.Train;
            var mstsLocomotive = Viewer.PlayerLocomotive as MSTSLocomotive;

            ResetHudScroll(); //Reset Hudscroll.
            if (hudWindowFullScreen)
                TableSetLabelValueColumns(table, 0, 2);

            if (mstsLocomotive != null)
            {
                var HUDSteamEngineType = (mstsLocomotive as MSTSSteamLocomotive)?.SteamEngineType;
                var HUDEngineType = mstsLocomotive.EngineType;
                if (HUDEngineType != EngineType.Control) // Don't display adhesion information if it is an unpowered control car.
                {
                    if (mstsLocomotive.AdvancedAdhesionModel)
                    {
                        if (HUDEngineType == EngineType.Steam && (HUDSteamEngineType == SteamEngineType.Compound || HUDSteamEngineType == SteamEngineType.Simple || HUDSteamEngineType == SteamEngineType.Unknown)) // For display of steam locomotive adhesion info
                        {
                            TableAddLine(table, Viewer.Catalog.GetString("(Advanced adhesion model)"));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Loco Adhesion"), "{0:F0}%", mstsLocomotive.LocomotiveCoefficientFrictionHUD * 100.0f);
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Wag Adhesion"), "{0:F0}%", mstsLocomotive.WagonCoefficientFrictionHUD * 100.0f);
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Tang. Force"), "{0:F0}", FormatStrings.FormatForce(Dynamics.Force.FromLbf(mstsLocomotive.SteamTangentialWheelForce), Simulator.Instance.MetricUnits));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Static Force"), "{0:F0}", FormatStrings.FormatForce(Dynamics.Force.FromLbf(mstsLocomotive.SteamStaticWheelForce), Simulator.Instance.MetricUnits));
                            //  TableAddLabelValue(table, Viewer.Catalog.GetString("Axle brake force"), "{0}", FormatStrings.FormatForce(mstsLocomotive.LocomotiveAxle.BrakeForceN, mstsLocomotive.IsMetric));
                        }
                        else  // Advanced adhesion non steam locomotives HUD display
                        {
                            TableAddLine(table, Viewer.Catalog.GetString("(Advanced adhesion model)"));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Wheel slip"), "{0:F0}% ({1:F0}%/{2})", mstsLocomotive.LocomotiveAxle.SlipSpeedPercent, mstsLocomotive.LocomotiveAxle.SlipDerivationPercentpS, FormatStrings.s);
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Conditions"), "{0:F0}%", mstsLocomotive.LocomotiveAxle.AdhesionConditions * 100.0f);
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Axle drive force"), "{0} ({1})", FormatStrings.FormatForce(mstsLocomotive.LocomotiveAxle.DriveForceN, Simulator.Instance.MetricUnits),
                                FormatStrings.FormatPower(mstsLocomotive.LocomotiveAxle.DriveForceN * mstsLocomotive.AbsTractionSpeedMpS, Simulator.Instance.MetricUnits, false, false));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Axle brake force"), "{0}", FormatStrings.FormatForce(mstsLocomotive.LocomotiveAxle.BrakeRetardForceN, Simulator.Instance.MetricUnits));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Number of substeps"), "{0:F0} ({1})", mstsLocomotive.LocomotiveAxle.AxleRevolutionsInt.NumOfSubstepsPS,
                                                      Viewer.Catalog.GetString("filtered by {0:F0}", mstsLocomotive.LocomotiveAxle.FilterMovingAverage.Size));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Solver"), "{0}", mstsLocomotive.LocomotiveAxle.AxleRevolutionsInt.Method.ToString());
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Stability correction"), "{0:F0}", mstsLocomotive.LocomotiveAxle.AdhesionK);
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Axle out force"), "{0} ({1})",
                                FormatStrings.FormatForce(mstsLocomotive.LocomotiveAxle.AxleForceN, Simulator.Instance.MetricUnits),
                                FormatStrings.FormatPower(mstsLocomotive.LocomotiveAxle.AxleForceN * mstsLocomotive.AbsTractionSpeedMpS, Simulator.Instance.MetricUnits, false, false));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Comp Axle out force"), "{0} ({1})",
                                FormatStrings.FormatForce(mstsLocomotive.LocomotiveAxle.CompensatedAxleForceN, Simulator.Instance.MetricUnits),
                                FormatStrings.FormatPower(mstsLocomotive.LocomotiveAxle.CompensatedAxleForceN * mstsLocomotive.AbsTractionSpeedMpS, Simulator.Instance.MetricUnits, false, false));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Wheel Speed"), "{0} ({1})",
                                FormatStrings.FormatSpeedDisplay(mstsLocomotive.AbsWheelSpeedMpS, Simulator.Instance.MetricUnits),
                                FormatStrings.FormatSpeedDisplay(mstsLocomotive.LocomotiveAxle.SlipSpeedMpS, Simulator.Instance.MetricUnits));
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Loco Adhesion"), "{0:F0}%", mstsLocomotive.LocomotiveCoefficientFrictionHUD * 100.0f);
                            TableAddLabelValue(table, Viewer.Catalog.GetString("Wagon Adhesion"), "{0:F0}%", mstsLocomotive.WagonCoefficientFrictionHUD * 100.0f);
                        }
                    }
                    else
                    {
                        TableAddLine(table, Viewer.Catalog.GetString("(Simple adhesion model)"));
                        TableAddLabelValue(table, Viewer.Catalog.GetString("Axle out force"), "{0:F0} N ({1:F0} kW)", mstsLocomotive.MotiveForceN, mstsLocomotive.MotiveForceN * mstsLocomotive.SpeedMpS / 1000.0f);
                        TableAddLabelValue(table, Viewer.Catalog.GetString("Loco Adhesion"), "{0:F0}%", mstsLocomotive.LocomotiveCoefficientFrictionHUD * 100.0f);
                        TableAddLabelValue(table, Viewer.Catalog.GetString("Wagon Adhesion"), "{0:F0}%", mstsLocomotive.WagonCoefficientFrictionHUD * 100.0f);
                    }
                }
                TableAddLine(table);

                var status = new StringBuilder();
                if (hudWindowColumnsActualPage > 0)
                {
                    status.AppendFormat("\n{0}\t{1:N2} mph\t{2}\t{3:N2} mph\n",
                    Viewer.Catalog.GetString("ResWind:"), train.ResultantWindComponentDeg,
                    Viewer.Catalog.GetString("ResSpeed:"), Size.Length.ToMi(Frequency.Periodic.ToHours(train.WindResultantSpeedMpS)));
                }
                else
                {
                    status.AppendFormat("\n{0}\t{1:N2} mph\t{2}\t\t{3:N2} Deg\t{4}\t\t{5:N2} Deg\t{6}\t{7:N2} mph\t{8}\t{9:N2} mph\n",
                    Viewer.Catalog.GetString("Wind Speed:"), Size.Length.ToMi(Frequency.Periodic.ToHours(train.PhysicsWindSpeedMpS)),
                    Viewer.Catalog.GetString("Wind Direction:"), train.PhysicsWindDirectionDeg,
                    Viewer.Catalog.GetString("Train Direction:"), train.PhysicsTrainLocoDirectionDeg,
                    Viewer.Catalog.GetString("ResWind:"), train.ResultantWindComponentDeg,
                    Viewer.Catalog.GetString("ResSpeed:"), Size.Length.ToMi(Frequency.Periodic.ToHours(train.WindResultantSpeedMpS))

                    //Add new header + data here, if required.

                    );
                }
                TableAddLines(table, status.ToString());
            }
            //HudScroll
            if (hudWindowColumnsActualPage > 0)
            {
                //HudScroll
                TableSetCells(table, 0,
                Viewer.Catalog.GetString("Car"),
                Viewer.Catalog.GetString("Coupler"),
                Viewer.Catalog.GetString("Slack"),
                Viewer.Catalog.GetString("Mass"),
                Viewer.Catalog.GetString("Gradient"),
                Viewer.Catalog.GetString("Curve"),
                Viewer.Catalog.GetString("Brk Frict."),
                Viewer.Catalog.GetString("Brk Slide"),
                Viewer.Catalog.GetString("Bear Temp")

                //Add new header data here, if adding additional column.

                );
            }
            else
            {
                //Normal view
                TableSetCells(table, 0,
                Viewer.Catalog.GetString("Car"),
                Viewer.Catalog.GetString("Total"),
                Viewer.Catalog.GetString("Motive"),
                Viewer.Catalog.GetString("Brake"),
                Viewer.Catalog.GetString("Friction"),
                Viewer.Catalog.GetString("Gravity"),
                Viewer.Catalog.GetString("Curve"),
                Viewer.Catalog.GetString("Tunnel"),
                Viewer.Catalog.GetString("Wind"),
                Viewer.Catalog.GetString("Coupler"),
                Viewer.Catalog.GetString("Coupler"),
                Viewer.Catalog.GetString("Slack"),
                Viewer.Catalog.GetString("Mass"),
                Viewer.Catalog.GetString("Gradient"),
                Viewer.Catalog.GetString("Curve"),
                Viewer.Catalog.GetString("Brk Frict."),
                Viewer.Catalog.GetString("Brk Slide"),
                Viewer.Catalog.GetString("Bear Temp"),
                Viewer.Catalog.GetString(" "),
                Viewer.Catalog.GetString("DerailCoeff")

                );
            }
            //Columns. HudScroll
            var columnsCount = ColumnsCount(table, false);

            TableAddLine(table);

            //Pages count from number of nLinesShow.
            TextLineNumber(train.Cars.Count, table.CurrentRow + 1, columnsCount);
            //Number of lines to show
            for (var i = (hudWindowLinesActualPage * nLinesShow) - nLinesShow; i < (train.Cars.Count > hudWindowLinesActualPage * nLinesShow ? hudWindowLinesActualPage * nLinesShow : train.Cars.Count); i++)
            {
                var j = (i == 0) ? 0 : i;
                var car = train.Cars[j];
                TableSetCell(table, 0, "{0}", car.CarID);
                if (hudWindowColumnsActualPage > 0)
                {
                    TableSetCell(table, 10, "{0} : {1}", car.GetCouplerRigidIndication() ? "R" : "F", car.CouplerExceedBreakLimit ? "xxx" + "!!!" : car.CouplerOverloaded ? "O/L" + "???" : car.HUDCouplerForceIndication == 1 ? "Pull" : car.HUDCouplerForceIndication == 2 ? "Push" : "-");
                    TableSetCell(table, 2, "{0}", FormatStrings.FormatVeryShortDistanceDisplay(car.CouplerSlackM, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 3, "{0}", FormatStrings.FormatLargeMass(car.MassKG, Simulator.Instance.MetricUnits, isUK));
                    TableSetCell(table, 4, "{0:F2}%", car.CurrentElevationPercent);
                    TableSetCell(table, 5, "{0}", FormatStrings.FormatDistance(car.CurrentCurveRadius, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 6, "{0:F0}%", car.BrakeShoeCoefficientFriction * 100.0f);
                    TableSetCell(table, 7, car.HUDBrakeSkid ? Viewer.Catalog.GetString("Yes") : Viewer.Catalog.GetString("No"));
                    TableSetCell(table, 8, "{0} {1}", FormatStrings.FormatTemperature(car.WheelBearingTemperatureDegC, Simulator.Instance.MetricUnits), car.DisplayWheelBearingTemperatureStatus);

                    TableSetCell(table, 9, car.Flipped ? Viewer.Catalog.GetString("Flipped") : "");

                    // Possibly needed for buffing forces
                    //                TableSetCell(table, 17, "{0}", FormatStrings.FormatForce(car.WagonVerticalDerailForceN, Simulator.Instance.MetricUnits));
                    //                TableSetCell(table, 18, "{0}", FormatStrings.FormatForce(car.TotalWagonLateralDerailForceN, Simulator.Instance.MetricUnits));
                    //                TableSetCell(table, 19, car.BuffForceExceeded ? Viewer.Catalog.GetString("Yes") : "No");

                    //                TableSetCell(table, 20, "{0:F2}", MathHelper.ToDegrees(car.WagonFrontCouplerAngleRad));
                    TableAddLine(table);
                    TableSetCell(table, 1, "Tot.Slack:");
                    TableSetCell(table, 2, "{0}", FormatStrings.FormatVeryShortDistanceDisplay(train.TotalCouplerSlackM, Simulator.Instance.MetricUnits));
                }
                else
                {
                    TableSetCell(table, 1, "{0}", FormatStrings.FormatForce(car.TotalForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 2, "{0}{1}", FormatStrings.FormatForce(car.MotiveForceN, Simulator.Instance.MetricUnits), car.WheelSlip ? "!!!" : car.WheelSlipWarning ? "???" : "");
                    TableSetCell(table, 3, "{0}", FormatStrings.FormatForce(car.BrakeForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 4, "{0}", FormatStrings.FormatForce(car.FrictionForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 5, "{0}", FormatStrings.FormatForce(car.GravityForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 6, "{0}", FormatStrings.FormatForce(car.CurveForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 7, "{0}", FormatStrings.FormatForce(car.TunnelForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 8, "{0}", FormatStrings.FormatForce(car.WindForceN, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 9, "{0}", FormatStrings.FormatForce(car.CouplerForceU, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 10, "{0} : {1}", car.GetCouplerRigidIndication() ? "R" : "F", car.CouplerExceedBreakLimit ? "xxx" + "!!!" : car.CouplerOverloaded ? "O/L" + "???" : car.HUDCouplerForceIndication == 1 ? "Pull" : car.HUDCouplerForceIndication == 2 ? "Push" : "-");
                    TableSetCell(table, 11, "{0}", FormatStrings.FormatVeryShortDistanceDisplay(car.CouplerSlackM, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 12, "{0}", FormatStrings.FormatLargeMass(car.MassKG, Simulator.Instance.MetricUnits, isUK));
                    TableSetCell(table, 13, "{0:F2}%", car.CurrentElevationPercent);
                    TableSetCell(table, 14, "{0}", FormatStrings.FormatDistance(car.CurrentCurveRadius, Simulator.Instance.MetricUnits));
                    TableSetCell(table, 15, "{0:F0}%", car.BrakeShoeCoefficientFriction * 100.0f);
                    TableSetCell(table, 16, car.HUDBrakeSkid ? Viewer.Catalog.GetString("Yes") : "No");
                    TableSetCell(table, 17, "{0} {1}", FormatStrings.FormatTemperature(car.WheelBearingTemperatureDegC, Simulator.Instance.MetricUnits), car.DisplayWheelBearingTemperatureStatus);

                    TableSetCell(table, 18, car.Flipped ? Viewer.Catalog.GetString("Flipped") : "");
                    TableSetCell(table, 19, "{0:F2}{1}", car.DerailmentCoefficient, car.DerailExpected ? "!!!" : car.DerailPossible ? "???" : "");

                    //TableSetCell(table, 10, "Tot.Slack:");
                    //TableSetCell(table, 11, "{0}", FormatStrings.FormatVeryShortDistanceDisplay(train.TotalCouplerSlackM, mstsLocomotive.IsMetric));
                    TableAddLine(table);
                }
            }
        }

        /// <summary>
        /// Columns count
        /// Used in TextLineNumber(int CarsCount, int CurrentRow, int ColumnCount)
        /// PathColumn == true return 'Path' header column position
        /// </summary>
        /// <param name="table"></param>
        /// <param name="PathColumn"></param>
        /// <returns "nColumnsCount"></returns>
        private int ColumnsCount(TableData table, bool PathColumn)
        {
            //Check columns for not null value. HudScroll
            int nColumnsCount = 0;
            for (int i = 0; i < table.Cells.GetLength(1); i++)
            {
                if (table.Cells[table.CurrentRow, i] != null)
                {
                    //Search Path column position. Dispatcher Information
                    //Avoid conflict with human dispatcher
                    var dato = table.Cells[table.CurrentRow, i].ToString();
                    if (PathColumn && table.Cells[table.CurrentRow, i].ToString() == Viewer.Catalog.GetString("Path"))
                        break;

                    nColumnsCount++;
                }
            }
            return nColumnsCount;
        }

        /// <summary>
        /// Compute the max. lines to show.
        /// </summary>
        /// <param name="CarsCount"></param>
        /// <param name="CurrentRow"></param>
        /// <param name="ColumnCount"></param>
        private void TextLineNumber(int CarsCount, int CurrentRow, int ColumnCount)
        {
            //LinesPages
            nLinesShow = (Viewer.DisplaySize.Y / TextFont.Height) - CurrentRow - 1;
            if (nLinesShow < 1)
                nLinesShow = 1;
            hudWindowLinesPagesCount = (nLinesShow >= CarsCount) ? 1 : (int)Math.Ceiling(Convert.ToDouble(CarsCount / nLinesShow) + 0.5);

            //Character per line
            charFitPerLine = CharFitPerLine("");

            //Columns pages
            int statusPathLenght = ColumnCount * columnsChars;
            hudWindowColumnsPagesCount = (statusPathLenght < charFitPerLine) ? 0 : (int)Math.Ceiling(Convert.ToDouble(statusPathLenght / charFitPerLine) + 0.5);
        }

        /// <summary>
        /// Compute the string width to be displayed.
        /// </summary>
        /// <param name="StringStatus"></param>
        /// <param name="initColumn"></param>
        private void TextColNumber(string StringStatus, int initColumn, bool IsSteamLocomotive)
        {
            char[] stringToChar = StringStatus.TrimEnd('\t').ToCharArray();

            stringStatus.Clear();//Reset
            var tabCount = StringStatus.TrimEnd('\t').Count(x => x == '\t');
            //Character per line
            charFitPerLine = CharFitPerLine("");
            string space = new string('X', columnsChars);

            var StringStatusLength = tabCount * columnsChars;
            var lastText = StringStatus.Substring(StringStatus.LastIndexOf("\t") + 1);
            StringStatusLength = StringStatus.EndsWith("\t") ? StringStatusLength : StringStatusLength + lastText.Length;
            var CurrentPathColumnsPagesCount = (StringStatusLength < charFitPerLine) ? 0 : (int)Math.Ceiling(Convert.ToDouble(StringStatusLength / charFitPerLine) + 0.5);

            //Update columns pages count.
            if (CurrentPathColumnsPagesCount > hudWindowColumnsPagesCount)
                hudWindowColumnsPagesCount = CurrentPathColumnsPagesCount;

            List<string> cellTextList = new List<string>();
            int i = 0;//Counter original status with tab code
                      //87 = character fit per line with minimun 800*600 display size.
                      //Font Monospace.
            if (StringStatus.Contains("\t"))
            {
                i = 0;
                bool lText = false;
                List<string> dataText = new List<string>();
                List<string> dataValue = new List<string>();
                List<string> dataTextString = new List<string>();
                string[] statusSplit = StringStatus.TrimEnd('\t').Split('\t');
                Dictionary<int, string> CumulativeTextStatus = new Dictionary<int, string>();
                Dictionary<int, string> CumulativeTabStatus = new Dictionary<int, string>();
                var cumulativeLenght = 0;
                var cumulativeTextStatus = "";
                var cumulativeTabStatus = "";
                var cellString = "";
                var cellIndex = 0;
                CumulativeTabStatus.Clear();
                CumulativeTextStatus.Clear();
                foreach (var cell in statusSplit)
                {
                    cellIndex++;
                    if (cell.Length == 0)
                    {
                        var cellStringLength = cellString.Length > columnsChars ? 0 : columnsChars;
                        cumulativeLenght += cellStringLength;
                        CumulativeTextStatus.Add(cumulativeLenght, cumulativeTextStatus + (cellString.Length > columnsChars ? "" : space));
                        CumulativeTabStatus.Add(cumulativeLenght, cumulativeTabStatus + "\t");
                        lText = false;
                        //Reset
                        cellString = "";
                        cumulativeTextStatus = "";
                        cumulativeTabStatus = "";
                        continue;
                    }
                    else if (lText && !CumulativeTextStatus.ContainsKey(cumulativeLenght))
                    {
                        CumulativeTextStatus.Add(cumulativeLenght, cumulativeTextStatus);
                        CumulativeTabStatus.Add(cumulativeLenght, cumulativeTabStatus);
                        lText = false;
                    }
                    //Avoid cell > columnsChars
                    cellString = cell.Length > columnsChars ? cell + CellTabSpace(cell.Substring(columnsChars, cell.Length - columnsChars), 1) : cell + CellTabSpace(cell, 1);
                    cumulativeLenght = cumulativeLenght + cellString.Length;
                    cumulativeTextStatus = cellString;
                    cumulativeTabStatus = cell + "\t";
                    lText = true;

                    if (lText && statusSplit.Length == cellIndex)
                    {
                        CumulativeTextStatus.Add(cumulativeLenght, cumulativeTextStatus.TrimEnd('X'));
                        CumulativeTabStatus.Add(cumulativeLenght, cumulativeTabStatus.TrimEnd('\t'));
                    }
                }

                var cumulativeTabString = "";
                var cumulativeTextString = "";
                var offsetFlag = 0;
                cellIndex = 0;
                foreach (var cell in CumulativeTextStatus)
                {
                    cumulativeTabString = cumulativeTabString + CumulativeTabStatus[cell.Key];
                    cumulativeTextString = cumulativeTextString + cell.Value;
                    if (cell.Key - offsetFlag > charFitPerLine || (cell.Key - offsetFlag < charFitPerLine && CumulativeTextStatus.Keys.Last() == cell.Key))
                    {
                        //Place first column data at cumulativeTabString begin
                        cumulativeTabString = cumulativeTabString.Contains(CumulativeTabStatus.Values.First()) ?
                                   cumulativeTabString :
                                   CumulativeTabStatus.Values.First() + cumulativeTabString;

                        cumulativeTextString = cumulativeTextString.Contains(CumulativeTextStatus.Values.First()) ?
                                   cumulativeTextString :
                                   CumulativeTextStatus.Values.First() + cumulativeTextString;

                        if (CumulativeTextStatus.ElementAt(0).Value.Contains(":"))
                        {
                            //cumulativeTextString.TrimEnd('X').Length
                            if (cumulativeTextString.Length <= charFitPerLine)
                            {
                                stringStatus.Add(cumulativeTabString.TrimEnd('\t'));

                                offsetFlag = cell.Key;
                                cumulativeTabString = "";
                                cumulativeTextString = "";
                            }
                            else
                            {
                                //string > charFitPerLine
                                //truncate to charFitPerLine
                                int countDown = cellIndex;
                                while (CumulativeTabStatus.ElementAt(countDown).Key - offsetFlag > charFitPerLine)
                                {
                                    countDown--;
                                }
                                int newCellKey = CumulativeTabStatus.ElementAt(countDown).Key;
                                if (cellIndex - countDown == 1)
                                    stringStatus.Add(cumulativeTabString.Substring(0, cumulativeTabString.Length - (charFitPerLine - newCellKey < columnsChars ? CumulativeTabStatus[cell.Key].Length : CumulativeTabStatus[newCellKey].Length)));
                                else
                                    stringStatus.Add(cumulativeTabString.Substring(0, cumulativeTabString.Length - CumulativeTabStatus[cell.Key].Length));

                                offsetFlag = newCellKey;
                                cumulativeTabString = cellIndex - countDown == 1 && charFitPerLine - newCellKey < columnsChars ? CumulativeTabStatus[cell.Key] : CumulativeTabStatus[cell.Key];
                                cumulativeTextString = cellIndex - countDown == 1 && charFitPerLine - newCellKey < columnsChars ? CumulativeTextStatus[cell.Key] : cell.Value.TrimStart('X');
                            }
                        }
                        else
                        {
                            if (cumulativeTextString.Length <= charFitPerLine)
                            {
                                stringStatus.Add(cumulativeTabString.TrimEnd('\t'));
                                offsetFlag = cell.Key;
                                cumulativeTabString = "";
                                cumulativeTextString = "";
                            }
                            else
                            {
                                //string > charFitPerLine
                                //truncate to charFitPerLine
                                int countDown = cellIndex;
                                while (CumulativeTabStatus.ElementAt(countDown).Key - offsetFlag > charFitPerLine)
                                {
                                    countDown--;
                                }
                                int newCellKey = CumulativeTabStatus.ElementAt(countDown).Key;
                                if (cellIndex - countDown == 1)
                                    stringStatus.Add(cumulativeTabString.Substring(0, cumulativeTabString.Length - CumulativeTabStatus[cell.Key].Length));
                                else
                                    stringStatus.Add(cumulativeTabString.Substring(0, cumulativeTabString.Length - CumulativeTabStatus[cell.Key].Length));

                                offsetFlag = newCellKey;
                                cumulativeTabString = cellIndex - countDown == 1 && charFitPerLine - newCellKey < columnsChars ? CumulativeTabStatus[cell.Key] : CumulativeTabStatus[cell.Key];
                                cumulativeTextString = cellIndex - countDown == 1 && charFitPerLine - newCellKey < columnsChars ? CumulativeTextStatus[cell.Key] : cell.Value.TrimStart('X');
                            }
                        }
                    }
                    cellIndex++;
                }
                if (cumulativeTextString.Length > 0 && cumulativeTextString.TrimEnd('X').Length <= charFitPerLine)
                {
                    stringStatus.Add(cumulativeTabString.Contains(CumulativeTabStatus.Values.First()) ? cumulativeTabString.TrimEnd('\t') : CumulativeTabStatus.Values.First() + cumulativeTabString.TrimEnd('\t'));
                    cumulativeTabString = "";
                    cumulativeTextString = "";
                }

                //Add '\n' to all stringStatus when 'PlayerLoco' Header
                if (stringStatus.Count > 0 && stringStatus[0].Contains(Viewer.Catalog.GetString("PlayerLoco")) && stringStatus[stringStatus.Count - 1].EndsWith("\n"))
                {//TO DO: rewrite this code using LINQ
                    for (int n = 0; n < stringStatus.Count - 1; n++)
                    {
                        if (!stringStatus[n].EndsWith("\n"))
                            stringStatus[n] = stringStatus[n] + "\n";
                    }
                }

                //Update 'page right' and 'page left' labels.
                if (stringStatus.Count > 1 && stringStatus.Count > hudWindowColumnsPagesCount)
                    hudWindowColumnsPagesCount = stringStatus.Count;
            }
            else
            {   //StringStatus without \t.
                //Only Force information and Dispatcher information.
                //Horizontal scroll.
                stringStatus.Clear();//Reset
                var n = 0;
                //Take in count left columns.
                charFitPerLine = initColumn > 0 ? charFitPerLine - (columnsChars * initColumn) - 1 : charFitPerLine;
                for (i = 0; i < StringStatusLength; i += charFitPerLine)
                {
                    if (StringStatusLength - i > charFitPerLine)
                        stringStatus.Add(StringStatus.Substring(i, charFitPerLine) + (StringStatus.EndsWith("???") ? "???" : ""));//Required by human dispacher path data
                    else
                        stringStatus.Add(StringStatus.Substring(i, StringStatus.Length - i));
                    n++;
                    if (n > hudWindowColumnsPagesCount)
                        break;
                }

                //Update 'page right' and 'page left' labels.
                if (stringStatus.Count > hudWindowColumnsPagesCount)
                    hudWindowColumnsPagesCount = stringStatus.Count;
            }

            CurrentPathColumnsPagesCount = (StringStatusLength < charFitPerLine) ? 0 : (int)Math.Ceiling(Convert.ToDouble(StringStatusLength / charFitPerLine) + 0.5);
            //Update columns pages count.
            if (CurrentPathColumnsPagesCount > hudWindowColumnsPagesCount)
                hudWindowColumnsPagesCount = CurrentPathColumnsPagesCount;
        }

        /// <summary>
        /// Count space requiered to fit a column
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="tabCount"></param>
        /// <returns></returns>
        private string CellTabSpace(string cell, int tabCount)
        {
            var cellSpace = "";
            string space = new string('X', columnsChars);
            int cellColumns = (int)Math.Ceiling((decimal)cell.Length / columnsChars);
            cellSpace = cell.Length > 0 && tabCount > 0 ? space.Substring(0, Math.Abs((columnsChars * cellColumns) - cell.Length)) : "";
            cellSpace = tabCount > cellColumns ? cellSpace + space : cellSpace;
            return cellSpace;
        }

        /// <summary>
        /// Compute how many character fit per line.
        /// </summary>
        /// <returns> x
        /// </returns>
        public int CharFitPerLine(string status)
        {
            var stringReference = status.Length > 0 ? status : "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890 \",.-+|!$%&/()=?;:'_[]";
            var textStringPixels = TextFont.MeasureString(stringReference);
            var charWidth = textStringPixels / stringReference.Length;
            columnsChars = (int)Math.Ceiling(Convert.ToDouble(ColumnWidth / charWidth));
            //Minus left and right space
            var x = (Viewer.DisplaySize.X - (TextOffset * 2)) / charWidth;
            return (int)x;
        }

        private bool lResetHudScroll;
        /// <summary>
        /// Reset Scroll Control window
        /// </summary>
        private void ResetHudScroll()
        {
            if (!lResetHudScroll)
            {
                hudWindowLinesActualPage = 1;
                hudWindowLinesPagesCount = 1;
                hudWindowColumnsActualPage = 0;
                hudWindowColumnsPagesCount = 0;
                hudWindowLocoPagesCount = 1;
                hudWindowSteamLocoLead = false;
                lResetHudScroll = true;
                BrakeInfoVisible = false;//Enable PgUp & PgDown (Scroll nav window)
            }
        }

        /// <summary>
        /// Allow to draw arrows.
        /// Consist info data.
        /// Locomotive info headers.
        /// </summary>
        /// <param name="statusConsist"></param>
        /// <param name="table"></param>
        private void DrawScrollArrows(List<string> statusConsist, TableData table, bool IsSteamLocomotive)
        {
            for (int i = 0; i < statusConsist.Count; i++)
            {
                if (i > 0 && i < 2 && (statusConsist[i] == stringStatus[i - 1]) || i > 1 && (statusConsist[i - 2] == statusConsist[i]))
                    continue;

                TextColNumber(statusConsist[i], 0, IsSteamLocomotive);
                if (hudWindowColumnsActualPage > 0)
                {//◄ \u25C0 - ► \u25B6 - ↔ \u2194
                    if (stringStatus.Count > 1 && stringStatus.Count <= hudWindowColumnsActualPage)
                        TableAddLines(table, hudWindowColumnsActualPage > 1 ? "◀" + stringStatus[(stringStatus.Count < hudWindowColumnsActualPage ? stringStatus.Count - 1 : hudWindowColumnsActualPage - 1)] : stringStatus[hudWindowColumnsActualPage - 1]);
                    else if (stringStatus.Count > 1 && hudWindowColumnsActualPage == 1)
                        TableAddLines(table, hudWindowColumnsActualPage > 0 ? "▶" + stringStatus[hudWindowColumnsActualPage - 1] : stringStatus[hudWindowColumnsActualPage - 1]);
                    else if (stringStatus.Count > 1 && hudWindowColumnsActualPage > 1 && stringStatus.Count >= hudWindowColumnsActualPage)
                        TableAddLines(table, hudWindowColumnsActualPage > 0 ? "↔" + stringStatus[hudWindowColumnsActualPage - 1] : stringStatus[hudWindowColumnsActualPage - 1]);
                    else
                    {
                        TableAddLines(table, (stringStatus.Count > 0 ? stringStatus[0] : statusConsist[i]));
                    }
                }
                else
                    TableAddLines(table, statusConsist[i]);

            }
        }

        private static void TextPageHeading(TableData table, string name)
        {
            TableAddLine(table);
            TableAddLine(table, name);
        }
    }
}
