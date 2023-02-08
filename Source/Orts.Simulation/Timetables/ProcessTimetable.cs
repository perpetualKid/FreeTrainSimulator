﻿// COPYRIGHT 2014 by the Open Rails project.
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

// This code processes the Timetable definition and converts it into playable train information
//

// Set debug flag to extract additional info
// Info is printed to C:\temp\timetableproc.txt
// #define DEBUG_TIMETABLE
//

using Orts.Simulation.RollingStocks.SubSystems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Parsers;
using Orts.Simulation.AIs;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

using static Orts.Formats.Msts.FolderStructure.ContentFolder;

namespace Orts.Simulation.Timetables
{
    public class TimetableInfo
    {
        private Simulator simulator;

        private enum columnType
        {
            stationInfo,
            addStationInfo,
            comment,
            trainDefinition,
            trainAddInfo,
            invalid,
        }

        private enum rowType
        {
            trainInfo,
            stationInfo,
            addStationInfo,
            consistInfo,
            pathInfo,
            startInfo,
            disposeInfo,
            directionInfo,
            trainNotesInfo,
            restartDelayInfo,
            speedInfo,
            comment,
            briefing,
            invalid,
        }

        private Dictionary<string, AIPath> Paths = new Dictionary<string, AIPath>();                                  // original path referenced by path name
        private List<string> reportedPaths = new List<string>();                                                      // reported path fails
        private Dictionary<int, string> TrainRouteXRef = new Dictionary<int, string>();                               // path name referenced from train index    

        public bool BinaryPaths;

        public static int? PlayerTrainOriginalStartTime; // Set by TimetableInfo.ProcessTimetable() and read by AI.PrerunAI()

        //================================================================================================//
        /// <summary>
        ///  Constructor - empty constructor
        /// </summary>
        public TimetableInfo(Simulator simulatorref)
        {
            simulator = simulatorref;
        }

        //================================================================================================//
        /// <summary>
        /// Process timetable file
        /// Convert info into list of trains
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>List of extracted Trains</returns>
        public List<TTTrain> ProcessTimetable(string fileName, string train, CancellationToken cancellationToken)
        {
            TTTrain reqPlayerTrain;

            bool loadPathNoFailure = true;
            List<TTTrain> trainList = new List<TTTrain>();
            List<TTTrainInfo> trainInfoList = new List<TTTrainInfo>();
            TTTrainInfo playerTrain = null;
            List<string> filenames;
            int indexcount = 0;

            // get filenames to process
            filenames = GetFilenames(fileName);

            // get file contents as strings
            Trace.Write("\n");
            foreach (string filePath in filenames)
            {
                // get contents as strings
                Trace.Write("TT File : " + filePath + "\n");
                var fileContents = new TimetableReader(filePath);

#if DEBUG_TIMETABLE
                File.AppendAllText(@"C:\temp\timetableproc.txt", "\nProcessing file : " + filePath + "\n");
#endif

                // convert to train info
                indexcount = ConvertFileContents(fileContents, simulator.SignalEnvironment, ref trainInfoList, indexcount, filePath);
            }

            // read and pre-process routes

            Trace.Write($" TTROUTES:{Paths.Count} ");

            loadPathNoFailure = PreProcessRoutes(cancellationToken);

            Trace.Write($" TTTRAINS:{trainInfoList.Count} ");

            // get startinfo for player train
            playerTrain = GetPlayerTrain(ref trainInfoList, train);

            // pre-init player train to abstract alternative paths if set
            if (playerTrain != null)
            {
                PreInitPlayerTrain(playerTrain);
            }

            // reduce trainlist using player train info and parameters
            bool addPathNoLoadFailure;
            trainList = BuildAITrains(trainInfoList, playerTrain, out addPathNoLoadFailure);
            if (!addPathNoLoadFailure) loadPathNoFailure = false;

            // set references (required to process commands)
            foreach (Physics.Train thisTrain in trainList)
            {
                if (simulator.NameDictionary.ContainsKey(thisTrain.Name))
                {
                    Trace.TraceWarning("Train : " + thisTrain.Name + " : duplicate name");
                }
                else
                {
                    simulator.TrainDictionary.Add(thisTrain.Number, thisTrain);
                    simulator.NameDictionary.Add(thisTrain.Name, thisTrain);
                }
            }

            // set player train
            reqPlayerTrain = null;
            if (playerTrain != null)
            {
                if (playerTrain.DisposeDetails != null)
                {
                    addPathNoLoadFailure = playerTrain.ProcessDisposeInfo(ref trainList, null, simulator);
                    if (!addPathNoLoadFailure) loadPathNoFailure = false;
                }
                PlayerTrainOriginalStartTime = playerTrain.StartTime; // Saved here for use after playerTrain.StartTime gets changed.
                reqPlayerTrain = InitializePlayerTrain(playerTrain, ref Paths, ref trainList);
                simulator.TrainDictionary.Add(reqPlayerTrain.Number, reqPlayerTrain);
                simulator.NameDictionary.Add(reqPlayerTrain.Name, reqPlayerTrain);
            }

            // process additional commands for all extracted trains
            reqPlayerTrain.FinalizeTimetableCommands();
            reqPlayerTrain.StationStops.Sort();

            foreach (TTTrain thisTrain in trainList)
            {
                thisTrain.FinalizeTimetableCommands();
                thisTrain.StationStops.Sort();

                // finalize attach details
                if (thisTrain.AttachDetails != null && thisTrain.AttachDetails.Valid)
                {
                    thisTrain.AttachDetails.FinalizeAttachDetails(thisTrain, trainList, playerTrain.TTTrain);
                }

                // finalize pickup details
                if (thisTrain.PickUpDetails != null && thisTrain.PickUpDetails.Count > 0)
                {
                    foreach (PickUpInfo thisPickUp in thisTrain.PickUpDetails)
                    {
                        thisPickUp.FinalizePickUpDetails(thisTrain, trainList, playerTrain.TTTrain);
                    }
                    thisTrain.PickUpDetails.Clear();
                }

                // finalize transfer details
                if (thisTrain.TransferStationDetails != null && thisTrain.TransferStationDetails.Count > 0)
                {
                    foreach (KeyValuePair<int, TransferInfo> thisTransferStation in thisTrain.TransferStationDetails)
                    {
                        TransferInfo thisTransfer = thisTransferStation.Value;
                        thisTransfer.SetTransferXRef(thisTrain, trainList, playerTrain.TTTrain, true, false);
                    }
                }

                if (thisTrain.TransferTrainDetails != null && thisTrain.TransferTrainDetails.ContainsKey(-1))
                {
                    foreach (TransferInfo thisTransfer in thisTrain.TransferTrainDetails[-1])
                    {
                        thisTransfer.SetTransferXRef(thisTrain, trainList, playerTrain.TTTrain, false, true);
                        if (thisTransfer.Valid)
                        {
                            if (thisTrain.TransferTrainDetails.ContainsKey(thisTransfer.TransferTrain))
                            {
                                Trace.TraceInformation("Train {0} : transfer command : cannot transfer to same train twice : {1}", thisTrain.Name, thisTransfer.TransferTrainName);
                            }
                            else
                            {
                                List<TransferInfo> thisTransferList = new List<TransferInfo>();
                                thisTransferList.Add(thisTransfer);
                                thisTrain.TransferTrainDetails.Add(thisTransfer.TransferTrain, thisTransferList);
                            }
                        }
                    }

                    thisTrain.TransferTrainDetails.Remove(-1);
                }
            }

            // process activation commands for all trains
            FinalizeActivationCommands(ref trainList, ref reqPlayerTrain);

            // set timetable identification for simulator for saves etc.
            simulator.TimetableFileName = Path.GetFileNameWithoutExtension(fileName);

            if (!loadPathNoFailure)
            {
                Trace.TraceError("Load path failures");
            }

            // check on engine in player train
            if (simulator.PlayerLocomotive == null)
            {
                if (reqPlayerTrain.NeedAttach != null && reqPlayerTrain.NeedAttach.Count > 0)
                {
                    Trace.TraceInformation("Player trains " + reqPlayerTrain.Name + " defined without engine, engine assumed to be attached later");
                }
                else
                {
                    throw new InvalidDataException("Can't find player locomotive in " + reqPlayerTrain.Name);
                }
            }

            trainList.Insert(0, reqPlayerTrain);
            return (trainList);
        }

        //================================================================================================//
        /// <summary>
        /// Get filenames of TTfiles to process
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private List<string> GetFilenames(string filePath)
        {
            List<string> filenames = new List<string>();

            // check type of timetable file - list or single
            string fileExtension = Path.GetExtension(filePath);
            string fileDirectory = Path.GetDirectoryName(filePath);

            switch (fileExtension)
            {
                case ".timetable_or":
                case ".timetable-or":
                    filenames.Add(filePath);
                    break;

                case ".timetablelist_or":
                case ".timetablelist-or":
                    filenames = TimetableGroupFile.GetTimeTableList(filePath);
                    break;

                default:
                    break;
            }

#if DEBUG_TIMETABLE
            if (File.Exists(@"C:\temp\timetableproc.txt"))
            {
                File.Delete(@"C:\temp\timetableproc.txt");
            }

            File.AppendAllText(@"C:\temp\timetableproc.txt", "Files : \n");
            foreach (string ttfile in filenames)
            {
                File.AppendAllText(@"C:\temp\timetableproc.txt", ttfile + "\n");
            }
#endif
            return (filenames);
        }

        //================================================================================================//
        /// <summary>
        /// Extract information and convert to traininfo
        /// </summary>
        /// <param name="fileContents"></param>
        /// <param name="signalRef"></param>
        /// <param name="TDB"></param>
        /// <param name="trainInfoList"></param>
        private int ConvertFileContents(TimetableReader fileContents, SignalEnvironment signalRef, ref List<TTTrainInfo> trainInfoList, int indexcount, string filePath)
        {
            int consistRow = -1;
            int pathRow = -1;
            int startRow = -1;
            int disposeRow = -1;
            int briefingRow = -1;

            int firstCommentRow = -1;
            int firstCommentColumn = -1;

            Dictionary<int, string> trainHeaders = new Dictionary<int, string>();          // key int = column no, value string = train header
            Dictionary<int, TTTrainInfo> trainInfo = new Dictionary<int, TTTrainInfo>();   // key int = column no, value = train info class
            Dictionary<int, int> addTrainInfo = new Dictionary<int, int>();                // key int = column no, value int = main train column
            Dictionary<int, List<int>> addTrainColumns = new Dictionary<int, List<int>>(); // key int = main train column, value = add columns
            Dictionary<int, StationInfo> stationNames = new Dictionary<int, StationInfo>();          // key int = row no, value string = station name

            float actSpeedConv = 1.0f;                                                     // actual set speedconversion

            rowType[] RowInfo = new rowType[fileContents.Strings.Count];
            columnType[] ColInfo = new columnType[fileContents.Strings[0].Length];

            // process first row separately

            ColInfo[0] = columnType.stationInfo;

            for (int iColumn = 1; iColumn <= fileContents.Strings[0].Length - 1; iColumn++)
            {
                string columnDef = fileContents.Strings[0][iColumn];

                // empty : continuation column
                if (String.IsNullOrEmpty(columnDef))
                {
                    switch (ColInfo[iColumn - 1])
                    {
                        case columnType.stationInfo:
                        case columnType.addStationInfo:
                            ColInfo[iColumn] = columnType.addStationInfo;
                            break;

                        case columnType.comment:
                            ColInfo[iColumn] = columnType.comment;
                            break;

                        case columnType.trainDefinition:
                            ColInfo[iColumn] = columnType.trainAddInfo;
                            addTrainInfo.Add(iColumn, iColumn - 1);
                            break;

                        case columnType.trainAddInfo:
                            ColInfo[iColumn] = columnType.trainAddInfo;
                            addTrainInfo.Add(iColumn, addTrainInfo[iColumn - 1]);
                            break;
                    }
                }

                // comment
                else if (string.Equals(columnDef, "#comment", StringComparison.OrdinalIgnoreCase))
                {
                    ColInfo[iColumn] = columnType.comment;
                    if (firstCommentColumn < 0) firstCommentColumn = iColumn;
                }

                // check for invalid command definition
                else if (columnDef[0] == '#')
                {
                    Trace.TraceWarning("Invalid column definition in {0} : column {1} : {2}", filePath, iColumn, columnDef);
                    ColInfo[iColumn] = columnType.invalid;
                }

                // otherwise it is a train definition
                else
                {
                    ColInfo[iColumn] = columnType.trainDefinition;
                    trainHeaders.Add(iColumn, columnDef);
                    trainInfo.Add(iColumn, new TTTrainInfo(iColumn, columnDef, simulator, indexcount, this));
                    indexcount++;
                }
            }

            // get row information
            RowInfo[0] = rowType.trainInfo;

            for (int iRow = 1; iRow <= fileContents.Strings.Count - 1; iRow++)
            {

                string rowDef = fileContents.Strings[iRow][0];

                string[] rowCommands = null;
                if (rowDef.Contains('/', StringComparison.OrdinalIgnoreCase))
                {
                    rowCommands = rowDef.Split('/');
                    rowDef = rowCommands[0];
                }

                // emtpy : continuation
                if (string.IsNullOrEmpty(rowDef))
                {
                    switch (RowInfo[iRow - 1])
                    {
                        case rowType.stationInfo:
                            RowInfo[iRow] = rowType.addStationInfo;
                            break;

                        default:  // continuation of other types not allowed, treat line as comment
                            RowInfo[iRow] = rowType.comment;
                            break;
                    }
                }

                // switch on actual string

                else
                {
                    switch (rowDef)
                    {
                        case string consist when consist.Equals("#consist", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.consistInfo;
                            consistRow = iRow;
                            break;

                        case string path when path.Equals("#path", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.pathInfo;
                            pathRow = iRow;
                            if (rowCommands != null && rowCommands.Length >= 2 && string.Equals(rowCommands[1], "binary", StringComparison.OrdinalIgnoreCase)) 
                                BinaryPaths = true;
                            break;

                        case string start when start.Equals("#start", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.startInfo;
                            startRow = iRow;
                            break;

                        case string dispose when dispose.Equals("#dispose", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.disposeInfo;
                            disposeRow = iRow;
                            break;

                        case string direction when direction.Equals("#direction", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.directionInfo;
                            break;

                        case string note when note.Equals("#note", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.trainNotesInfo;
                            break;

                        case string restartDelay when restartDelay.Equals("#restartdelay", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.restartDelayInfo;
                            break;

                        case string speed when speed.Equals("#speed", StringComparison.OrdinalIgnoreCase):
                            bool speeddef = false;
                            for (int rowIndex = 0; rowIndex < iRow; rowIndex++)
                            {
                                if (RowInfo[rowIndex] == rowType.speedInfo)
                                {
                                    Trace.TraceInformation("Multiple speed row definition - second entry ignored \n");
                                    speeddef = true;
                                    break;
                                }
                            }
                            if (!speeddef)
                            {
                                RowInfo[iRow] = rowType.speedInfo;
                                actSpeedConv = 1.0f;
                            }
                            break;
                        case string speedmph when speedmph.Equals("#speedmph", StringComparison.OrdinalIgnoreCase):
                            speeddef = false;
                            for (int rowIndex = 0; rowIndex < iRow; rowIndex++)
                            {
                                if (RowInfo[rowIndex] == rowType.speedInfo)
                                {
                                    Trace.TraceInformation("Multiple speed row definition - second entry ignored \n");
                                    speeddef = true;
                                    break;
                                }
                            }
                            if (!speeddef)
                            {
                                RowInfo[iRow] = rowType.speedInfo;
                                actSpeedConv = (float)Speed.MeterPerSecond.FromMpH(1.0f);
                            }
                            break;
                        case string speedkph when speedkph.Equals("#speedkph", StringComparison.OrdinalIgnoreCase):
                            speeddef = false;
                            for (int rowIndex = 0; rowIndex < iRow; rowIndex++)
                            {
                                if (RowInfo[rowIndex] == rowType.speedInfo)
                                {
                                    Trace.TraceInformation("Multiple speed row definition - second entry ignored \n");
                                    speeddef = true;
                                    break;
                                }
                            }
                            if (!speeddef)
                            {
                                RowInfo[iRow] = rowType.speedInfo;
                                actSpeedConv = (float)Speed.MeterPerSecond.FromKpH(1.0f);
                            }
                            break;

                        case string comment when comment.Equals("#comment", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.comment;
                            if (firstCommentRow < 0) firstCommentRow = iRow;
                            break;

                        case string briefing when briefing.Equals("#briefing", StringComparison.OrdinalIgnoreCase):
                            RowInfo[iRow] = rowType.briefing;
                            briefingRow = iRow;
                            break;

                        default:  // default is station definition
                            if (rowDef[0] == '#')
                            {
                                Trace.TraceWarning("Invalid row definition in {0} : {1}", filePath, rowDef);
                                RowInfo[iRow] = rowType.invalid;
                            }
                            else
                            {
                                RowInfo[iRow] = rowType.stationInfo;
                                stationNames.Add(iRow, new StationInfo(rowDef));
                            }
                            break;
                    }
                }
            }

#if DEBUG_TIMETABLE
            File.AppendAllText(@"C:\temp\timetableproc.txt", "\n Row and Column details : \n");

            File.AppendAllText(@"C:\temp\timetableproc.txt", "\n Columns : \n");
            for (int iColumn = 0; iColumn <= ColInfo.Length - 1; iColumn++)
            {
                columnType ctype = ColInfo[iColumn];

                var stbuild = new StringBuilder();
                stbuild.AppendFormat("Column : {0} = {1}", iColumn, ctype.ToString());
                if (ctype == columnType.trainDefinition)
                {
                    stbuild.AppendFormat(" = train : {0}", trainHeaders[iColumn]);
                }
                stbuild.Append("\n");
                File.AppendAllText(@"C:\temp\timetableproc.txt", stbuild.ToString());
            }

            File.AppendAllText(@"C:\temp\timetableproc.txt", "\n Rows : \n");
            for (int iRow = 0; iRow <= RowInfo.Length - 1; iRow++)
            {
                rowType rtype = RowInfo[iRow];

                var stbuild = new StringBuilder();
                stbuild.AppendFormat("Row    : {0} = {1}", iRow, rtype.ToString());
                if (rtype == rowType.stationInfo)
                {
                    stbuild.AppendFormat(" = station {0}", stationNames[iRow]);
                }
                stbuild.Append("\n");
                File.AppendAllText(@"C:\temp\timetableproc.txt", stbuild.ToString());
            }
#endif

            bool validFile = true;

            // check if all required row definitions are available
            if (consistRow < 0)
            {
                Trace.TraceWarning("File : {0} - Consist definition row missing, file cannot be processed", filePath);
                validFile = false;
            }

            if (pathRow < 0)
            {
                Trace.TraceWarning("File : {0} - Path definition row missing, file cannot be processed", filePath);
                validFile = false;
            }

            if (startRow < 0)
            {
                Trace.TraceWarning("File : {0} - Start definition row missing, file cannot be processed", filePath);
                validFile = false;
            }

            if (!validFile) return (indexcount); // abandone processing

            // extract description

            string description = (firstCommentRow >= 0 && firstCommentColumn >= 0) ?
                fileContents.Strings[firstCommentRow][firstCommentColumn] : Path.GetFileNameWithoutExtension(fileContents.FilePath);

            // extract additional station info

            for (int iRow = 1; iRow <= fileContents.Strings.Count - 1; iRow++)
            {
                if (RowInfo[iRow] == rowType.stationInfo)
                {
                    string[] columnStrings = fileContents.Strings[iRow];
                    for (int iColumn = 1; iColumn <= ColInfo.Length - 1; iColumn++)
                    {
                        if (ColInfo[iColumn] == columnType.addStationInfo)
                        {
                            string[] stationCommands = columnStrings[iColumn].Split('$');
                            stationNames[iRow].ProcessStationCommands(stationCommands);
                        }
                    }
                }
            }

            // build list of additional train columns

            foreach (KeyValuePair<int, int> addColumn in addTrainInfo)
            {
                if (addTrainColumns.ContainsKey(addColumn.Value))
                {
                    addTrainColumns[addColumn.Value].Add(addColumn.Key);
                }
                else
                {
                    List<int> addTrainColumn = new List<int>();
                    addTrainColumn.Add(addColumn.Key);
                    addTrainColumns.Add(addColumn.Value, addTrainColumn);
                }
            }

            // build actual trains

            bool allCorrectBuild = true;

            for (int iColumn = 1; iColumn <= ColInfo.Length - 1; iColumn++)
            {
                if (ColInfo[iColumn] == columnType.trainDefinition)
                {
                    List<int> addColumns = null;
                    addTrainColumns.TryGetValue(iColumn, out addColumns);

                    if (addColumns != null)
                    {
                        ConcatTrainStrings(fileContents.Strings, iColumn, addColumns);
                    }

                    if (!trainInfo[iColumn].BuildTrain(fileContents.Strings, RowInfo, pathRow, consistRow, startRow, disposeRow, briefingRow, description, stationNames, actSpeedConv, this))
                    {
                        allCorrectBuild = false;
                    }
                }
            }

            if (!allCorrectBuild)
            {
                Trace.TraceError("Failed to build trains");
            }

            // extract valid trains
            foreach (KeyValuePair<int, TTTrainInfo> train in trainInfo)
            {
                if (train.Value.validTrain)
                {
                    trainInfoList.Add(train.Value);
                }
            }

            return (indexcount);
        }

        //================================================================================================//
        /// <summary>
        /// Concatinate train string with info from additional columns
        /// </summary>
        /// <param name="fileStrings"></param>
        /// <param name="iColumn"></param>
        /// <param name="addColumns"></param>
        private void ConcatTrainStrings(List<string[]> fileStrings, int iColumn, List<int> addColumns)
        {
            for (int iRow = 1; iRow < fileStrings.Count - 1; iRow++)
            {
                string[] columnStrings = fileStrings[iRow];
                foreach (int addCol in addColumns)
                {
                    string addCols = columnStrings[addCol];
                    if (!String.IsNullOrEmpty(addCols))
                    {
                        columnStrings[iColumn] = String.Concat(columnStrings[iColumn], " ", addCols);
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// GetPlayerTrain : extract player train from list of all available trains
        /// </summary>
        /// <param name="allTrains"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private TTTrainInfo GetPlayerTrain(ref List<TTTrainInfo> allTrains, string train)
        {
            TTTrainInfo reqTrain = null;

            string[] playerTrainDetails = train.Split(':');

            // loop through all trains to find player train
            int playerIndex = -1;

            for (int iTrain = 0; iTrain <= allTrains.Count - 1 && playerIndex < 0; iTrain++)
            {
                if (string.Equals(allTrains[iTrain].Name, playerTrainDetails[1], StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(allTrains[iTrain].TTDescription, playerTrainDetails[0], StringComparison.OrdinalIgnoreCase))
                {
                    playerIndex = iTrain;
                }
            }

            if (playerIndex >= 0)
            {
                reqTrain = allTrains[playerIndex];
                allTrains.RemoveAt(playerIndex);
            }
            else
            {
                throw new InvalidDataException($"Player train : {train} not found in timetables");
            }

            return (reqTrain);
        }

        //================================================================================================//
        /// <summary>
        /// Build AI trains
        /// </summary>
        /// <param name="allTrains"></param>
        /// <param name="playerTrain"></param>
        /// <param name="arguments"></param>
        private List<TTTrain> BuildAITrains(List<TTTrainInfo> allTrains, TTTrainInfo playerTrain, out bool allPathsLoaded)
        {
            allPathsLoaded = true;
            List<TTTrain> trainList = new List<TTTrain>();

            foreach (TTTrainInfo reqTrain in allTrains)
            {
                // create train route
                if (TrainRouteXRef.ContainsKey(reqTrain.Index) && Paths.ContainsKey(TrainRouteXRef[reqTrain.Index]))
                {
                    AIPath usedPath = new AIPath(Paths[TrainRouteXRef[reqTrain.Index]]);
                    reqTrain.TTTrain.RearTDBTraveller = new Traveller(usedPath.FirstNode.Location, usedPath.FirstNode.NextMainNode.Location);
                    reqTrain.TTTrain.Path = usedPath;
                    reqTrain.TTTrain.CreateRoute(false);  // create route without use of FrontTDBtraveller
                    reqTrain.TTTrain.EndRouteAtLastSignal();
                    reqTrain.TTTrain.ValidRoute[0] = new TrackCircuitPartialPathRoute(reqTrain.TTTrain.TCRoute.TCRouteSubpaths[0]);
                    reqTrain.TTTrain.AITrainDirectionForward = true;

                    // process stops
                    reqTrain.ConvertStops(simulator, reqTrain.TTTrain, reqTrain.Name);

                    // process commands
                    if (reqTrain.TrainCommands.Count > 0)
                    {
                        reqTrain.ProcessCommands(simulator, reqTrain.TTTrain);
                    }

                    // add AI train to output list
                    trainList.Add(reqTrain.TTTrain);
                }
            }

            // process dispose commands
            foreach (TTTrainInfo reqTrain in allTrains)
            {
                if (reqTrain.DisposeDetails != null)
                {
                    bool pathsNoLoadFailure = reqTrain.ProcessDisposeInfo(ref trainList, playerTrain, simulator);
                    if (!pathsNoLoadFailure)
                    {
                        allPathsLoaded = false;
                        return (trainList);
                    }
                }

                // build detach cross references
                if (reqTrain.TTTrain.DetachDetails != null)
                {
                    int detachCount = 0;

                    foreach (KeyValuePair<int, List<DetachInfo>> thisDetachInfo in reqTrain.TTTrain.DetachDetails)
                    {
                        List<DetachInfo> detachList = thisDetachInfo.Value;

                        foreach (DetachInfo thisDetach in detachList)
                        {
                            if (!thisDetach.DetachFormedStatic)
                            {
                                if (thisDetach.DetachFormedTrain < 0)
                                {
                                    thisDetach.SetDetachXRef(reqTrain.TTTrain, trainList, playerTrain.TTTrain);
                                }
                            }
                            else
                            {
                                int lastSectionIndex = reqTrain.TTTrain.TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
                                thisDetach.DetachFormedTrain = reqTrain.TTTrain.CreateStaticTrainRef(reqTrain.TTTrain, ref trainList, thisDetach.DetachFormedTrainName, lastSectionIndex, detachCount);
                                detachCount++;
                            }
                        }
                    }
                }
            }

            return (trainList);
        }

        //================================================================================================//
        /// <summary>
        /// Pre-Initialize player train : set all default details
        /// </summary>
        private void PreInitPlayerTrain(TTTrainInfo reqTrain)
        {
            // set player train idents
            TTTrain playerTrain = reqTrain.TTTrain;
            reqTrain.playerTrain = true;

            playerTrain.TrainType = TrainType.PlayerIntended;
            playerTrain.OrgAINumber = playerTrain.Number;
            playerTrain.Number = 0;
            playerTrain.ControlMode = TrainControlMode.Inactive;
            playerTrain.MovementState = AiMovementState.Static;

            // create traveller
            AIPath usedPath = Paths[TrainRouteXRef[reqTrain.Index]];
            playerTrain.RearTDBTraveller = new Traveller(usedPath.FirstNode.Location, usedPath.FirstNode.NextMainNode.Location);

            // extract train path
            playerTrain.SetRoutePath(usedPath, false);
            playerTrain.EndRouteAtLastSignal();
            playerTrain.ValidRoute[0] = new TrackCircuitPartialPathRoute(playerTrain.TCRoute.TCRouteSubpaths[0]);
        }

        //================================================================================================//
        /// <summary>
        /// Extract and initialize player train
        /// contains extracted train plus additional info for identification and selection
        /// </summary>
        private TTTrain InitializePlayerTrain(TTTrainInfo reqTrain, ref Dictionary<string, AIPath> paths, ref List<TTTrain> trainList)
        {
            // set player train idents
            TTTrain playerTrain = reqTrain.TTTrain;

            simulator.Trains.Add(playerTrain);

            // reset train for each car

            int icar = 1;
            foreach (TrainCar car in playerTrain.Cars)
            {
                car.Train = playerTrain;
                car.CarID = $"{playerTrain.Number:0000}_{icar:000}";
                icar++;
            }

            // set player locomotive
            // first test first and last cars - if either is drivable, use it as player locomotive
            simulator.PlayerLocomotive = playerTrain.LeadLocomotive = playerTrain.Cars[0] as MSTSLocomotive ?? playerTrain.Cars[^1] as MSTSLocomotive ?? playerTrain.Cars.OfType<MSTSLocomotive>().FirstOrDefault();

            // initialize brakes
            playerTrain.AITrainBrakePercent = 100;
            playerTrain.InitializeBrakes();

            // set stops
            reqTrain.ConvertStops(simulator, playerTrain, reqTrain.Name);

            // process commands
            if (reqTrain.TrainCommands.Count > 0)
            {
                reqTrain.ProcessCommands(simulator, reqTrain.TTTrain);
            }


            // set detach cross-references
            foreach (KeyValuePair<int, List<DetachInfo>> thisDetachInfo in reqTrain.TTTrain.DetachDetails)
            {
                int detachCount = 0;

                List<DetachInfo> detachList = thisDetachInfo.Value;

                foreach (DetachInfo thisDetach in detachList)
                {
                    if (thisDetach.DetachFormedTrain < 0)
                    {
                        if (!thisDetach.DetachFormedStatic)
                        {
                            if (thisDetach.DetachFormedTrain < 0)
                            {
                                thisDetach.SetDetachXRef(reqTrain.TTTrain, trainList, null);
                            }
                        }
                        else
                        {
                            int lastSectionIndex = reqTrain.TTTrain.TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
                            thisDetach.DetachFormedTrain = reqTrain.TTTrain.CreateStaticTrainRef(reqTrain.TTTrain, ref trainList, thisDetach.DetachFormedTrainName, lastSectionIndex, detachCount);
                            detachCount++;
                        }
                    }
                }
            }


            // finalize attach details
            if (reqTrain.TTTrain.AttachDetails != null && reqTrain.TTTrain.AttachDetails.Valid)
            {
                reqTrain.TTTrain.AttachDetails.FinalizeAttachDetails(reqTrain.TTTrain, trainList, null);
            }

            // finalize pickup details
            if (reqTrain.TTTrain.PickUpDetails != null && reqTrain.TTTrain.PickUpDetails.Count > 0)
            {
                foreach (PickUpInfo thisPickUp in reqTrain.TTTrain.PickUpDetails)
                {
                    thisPickUp.FinalizePickUpDetails(reqTrain.TTTrain, trainList, null);
                }
                reqTrain.TTTrain.PickUpDetails.Clear();
            }

            // finalize transfer details
            if (reqTrain.TTTrain.TransferStationDetails != null && reqTrain.TTTrain.TransferStationDetails.Count > 0)
            {
                foreach (KeyValuePair<int, TransferInfo> thisTransferStation in reqTrain.TTTrain.TransferStationDetails)
                {
                    TransferInfo thisTransfer = thisTransferStation.Value;
                    thisTransfer.SetTransferXRef(reqTrain.TTTrain, trainList, null, true, false);
                }
            }

            if (reqTrain.TTTrain.TransferTrainDetails != null && reqTrain.TTTrain.TransferTrainDetails.ContainsKey(-1))
            {
                foreach (TransferInfo thisTransfer in reqTrain.TTTrain.TransferTrainDetails[-1])
                {
                    thisTransfer.SetTransferXRef(reqTrain.TTTrain, trainList, null, false, true);
                    if (thisTransfer.Valid)
                    {
                        if (reqTrain.TTTrain.TransferTrainDetails.ContainsKey(thisTransfer.TransferTrain))
                        {
                            Trace.TraceInformation("Train {0} : transfer command : cannot transfer to same train twice : {1}", reqTrain.TTTrain.Name, thisTransfer.TransferTrainName);
                        }
                        else
                        {
                            List<TransferInfo> thisTransferList = new List<TransferInfo>();
                            thisTransferList.Add(thisTransfer);
                            reqTrain.TTTrain.TransferTrainDetails.Add(thisTransfer.TransferTrain, thisTransferList);
                        }
                    }
                }
                reqTrain.TTTrain.TransferTrainDetails.Remove(-1);
            }

            // set activity details
            simulator.ClockTime = reqTrain.StartTime;
            simulator.ActivityFileName = reqTrain.TTDescription + "_" + reqTrain.Name;

            // if train is created before start time, create train as intended player train
            if (playerTrain.StartTime != playerTrain.ActivateTime)
            {
                playerTrain.TrainType = TrainType.PlayerIntended;
                playerTrain.FormedOf = -1;
                playerTrain.FormedOfType = TTTrain.FormCommand.Created;
            }

            return (playerTrain);
        }

        //================================================================================================//
        /// <summary>
        /// Finalize activation commands
        /// </summary>

        public void FinalizeActivationCommands(ref List<TTTrain> trainList, ref TTTrain reqPlayerTrain)
        {
            List<int> activatedTrains = new List<int>();

            // build list of trains to be activated
            // set original AI number for player train
            foreach (TTTrain thisTrain in trainList)
            {
                if (thisTrain.TriggeredActivationRequired)
                {
                    if (thisTrain.Number == 0)
                    {
                        activatedTrains.Add(thisTrain.OrgAINumber);
                    }
                    else
                    {
                        activatedTrains.Add(thisTrain.Number);
                    }
                }
            }

            if (reqPlayerTrain.TriggeredActivationRequired)
            {
                activatedTrains.Add(reqPlayerTrain.OrgAINumber);
            }

            // process all activation commands
            foreach (TTTrain thisTrain in trainList)
            {
                if (thisTrain.activatedTrainTriggers != null && thisTrain.activatedTrainTriggers.Count > 0)
                {
                    for (int itrigger = thisTrain.activatedTrainTriggers.Count - 1; itrigger >= 0; itrigger--)
                    {
                        TTTrain.TriggerActivation thisTrigger = thisTrain.activatedTrainTriggers[itrigger];
                        thisTrain.activatedTrainTriggers.RemoveAt(itrigger);
                        string otherTrainName = thisTrigger.activatedName;

                        if (!otherTrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                        {
                            int seppos = thisTrain.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                            otherTrainName = $"{otherTrainName}:{thisTrain.Name.Substring(seppos + 1)}";
                        }

                        TTTrain otherTrain = thisTrain.GetOtherTTTrainByName(otherTrainName);
                        if (otherTrain == null)
                        {
                            Trace.TraceInformation("Invalid train activation command: train {0} not found, for train {1}", otherTrainName, thisTrain.Name);
                        }
                        else
                        {
                            if (activatedTrains.Contains(otherTrain.Number))
                            {
                                activatedTrains.Remove(otherTrain.Number);
                                thisTrigger.activatedTrain = otherTrain.Number;
                                thisTrain.activatedTrainTriggers.Insert(itrigger, thisTrigger);
                            }
                            else if (activatedTrains.Contains(otherTrain.OrgAINumber))
                            {
                                activatedTrains.Remove(otherTrain.OrgAINumber);
                                thisTrigger.activatedTrain = otherTrain.Number;
                                thisTrain.activatedTrainTriggers.Insert(itrigger, thisTrigger);
                            }
                            else
                            {
                                Trace.TraceInformation("Invalid train activation command: train {0} not waiting for activation, for train {1}", otherTrainName, thisTrain.Name);
                            }
                        }
                    }
                }
            }

            // process activation request for player train
            if (reqPlayerTrain.activatedTrainTriggers != null && reqPlayerTrain.activatedTrainTriggers.Count > 0)
            {
                for (int itrigger = reqPlayerTrain.activatedTrainTriggers.Count - 1; itrigger >= 0; itrigger--)
                {
                    TTTrain.TriggerActivation thisTrigger = reqPlayerTrain.activatedTrainTriggers[itrigger];
                    reqPlayerTrain.activatedTrainTriggers.RemoveAt(itrigger);
                    string otherTrainName = thisTrigger.activatedName;

                    if (!otherTrainName.Contains(':', StringComparison.OrdinalIgnoreCase))
                    {
                        int seppos = reqPlayerTrain.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                        otherTrainName = $"{otherTrainName}:{reqPlayerTrain.Name.Substring(seppos + 1)}";
                    }

                    TTTrain otherTrain = reqPlayerTrain.GetOtherTTTrainByName(otherTrainName);
                    if (otherTrain == null)
                    {
                        Trace.TraceInformation("Invalid train activation command: train {0} not found, for train {1}", otherTrainName, reqPlayerTrain.Name);
                    }
                    else
                    {
                        if (activatedTrains.Contains(otherTrain.Number))
                        {
                            activatedTrains.Remove(otherTrain.Number);
                            thisTrigger.activatedTrain = otherTrain.Number;
                            reqPlayerTrain.activatedTrainTriggers.Insert(itrigger, thisTrigger);
                        }
                        else if (activatedTrains.Contains(otherTrain.OrgAINumber))
                        {
                            activatedTrains.Remove(otherTrain.OrgAINumber);
                            thisTrigger.activatedTrain = otherTrain.Number;
                            reqPlayerTrain.activatedTrainTriggers.Insert(itrigger, thisTrigger);
                        }
                        else
                        {
                            Trace.TraceInformation("Invalid train activation command: train {0} not waiting for activation, for train {1}", otherTrainName, reqPlayerTrain.Name);
                        }
                    }
                }
            }

            // check if any activated trains remain untriggered
            foreach (int inumber in activatedTrains)
            {
                TTTrain otherTrain = trainList[0].GetOtherTTTrainByNumber(inumber);
                Trace.TraceInformation("Train activation required but no activation command set for train {0}", otherTrain.Name);
                otherTrain.TriggeredActivationRequired = false;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Pre-process all routes : read routes and convert to AIPath structure
        /// </summary>
        public bool PreProcessRoutes(CancellationToken cancellation)
        {

            // extract names
            List<string> routeNames = new List<string>();

            foreach (KeyValuePair<string, AIPath> thisRoute in Paths)
            {
                routeNames.Add(thisRoute.Key);
            }

            // clear routes - will be refilled
            Paths.Clear();
            bool allPathsLoaded = true;

            // create routes
            foreach (string thisRoute in routeNames)
            {
                // read route
                bool pathValid = true;
                LoadPath(thisRoute, out pathValid);
                if (!pathValid) allPathsLoaded = false;
                if (cancellation.IsCancellationRequested)
                    return (false);
            }

            return (allPathsLoaded);
        }

        //================================================================================================//
        /// <summary>
        /// Load path
        /// </summary>
        /// <param name="pathstring"></param>
        /// <param name="validPath"></param>
        /// <returns></returns>
        public AIPath LoadPath(string pathstring, out bool validPath)
        {
            validPath = true;

            string formedpathFilefull = Path.Combine(simulator.RouteFolder.PathsFolder, pathstring);
            string pathExtension = Path.GetExtension(formedpathFilefull);

            if (String.IsNullOrEmpty(pathExtension))
                formedpathFilefull = Path.ChangeExtension(formedpathFilefull, "pat");

            if (!Paths.TryGetValue(formedpathFilefull, out var outPath))
            {
                // try to load binary path if required
                bool binaryloaded = false;
                string formedpathFilefullBinary = Path.Combine(Path.GetDirectoryName(formedpathFilefull), "OpenRails");
                formedpathFilefullBinary = Path.Combine(formedpathFilefullBinary, Path.GetFileNameWithoutExtension(formedpathFilefull));
                formedpathFilefullBinary = Path.ChangeExtension(formedpathFilefullBinary, "or-binpat");

                if (BinaryPaths && File.Exists(formedpathFilefullBinary))
                {
                    var binaryLastWriteTime = File.GetLastWriteTime(formedpathFilefullBinary);
                    if (binaryLastWriteTime < File.GetLastWriteTime(simulator.RouteFolder.TrackDatabaseFile(simulator.Route.FileName)) ||
                        File.Exists(formedpathFilefull) && binaryLastWriteTime < File.GetLastWriteTime(formedpathFilefull))
                    {
                        File.Delete(formedpathFilefullBinary);
                    }
                    else
                    {
                        try
                        {
                            var infpath = new BinaryReader(new FileStream(formedpathFilefullBinary, FileMode.Open, FileAccess.Read));
                            outPath = new AIPath(infpath);
                            infpath.Close();

                            if (outPath.Nodes != null)
                            {
                                Paths.Add(formedpathFilefull, outPath);
                                binaryloaded = true;
                            }
                        }
                        catch
                        {
                            binaryloaded = false;
                        }
                    }
                }

                if (!binaryloaded)
                {
                    try
                    {
                        outPath = new AIPath(formedpathFilefull, true);
                        validPath = outPath.Nodes != null;

                        if (validPath)
                        {
                            try
                            {
                                Paths.Add(formedpathFilefull, outPath);
                            }
                            catch (Exception e)
                            {
                                validPath = false;
                                if (!reportedPaths.Contains(formedpathFilefull))
                                {
                                    Trace.TraceInformation(new FileLoadException(formedpathFilefull, e).ToString());
                                    reportedPaths.Add(formedpathFilefull);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        validPath = false;
                        if (!reportedPaths.Contains(formedpathFilefull))
                        {
                            Trace.TraceInformation(new FileLoadException(formedpathFilefull, e).ToString());
                            reportedPaths.Add(formedpathFilefull);
                        }
                    }

                    if (validPath)
                    {
                        if (!binaryloaded && BinaryPaths)
                        {
                            try
                            {
                                var outfpath = new BinaryWriter(new FileStream(formedpathFilefullBinary, FileMode.Create));
                                outPath.Save(outfpath);
                                outfpath.Close();
                            }
                            // dummy catch to avoid error
                            catch
                            { }
                        }
                    }
                    // report path load failure
                }
            }

            return (outPath);
        }

        //================================================================================================//
        /// <summary>
        /// class TTTrainInfo
        /// contains extracted train plus additional info for identification and selection
        /// </summary>

        private class TTTrainInfo
        {
            public TTTrain TTTrain;
            public string Name;
            public int StartTime;
            public string TTDescription;
            public int columnIndex;
            public bool validTrain = true;
            public bool playerTrain;
            public Dictionary<string, StopInfo> Stops = new Dictionary<string, StopInfo>();
            public List<string> reportedConsistFailures = new List<string>();
            public int Index;
            public List<TTTrainCommands> TrainCommands = new List<TTTrainCommands>();
            public DisposeInfo DisposeDetails;

            public readonly TimetableInfo parentInfo;

            private readonly struct ConsistInfo
            {
                public readonly string ConsistFile;
                public readonly bool Reversed;

                public ConsistInfo(string consistFile, bool reversed)
                {
                    ConsistFile = consistFile;
                    Reversed = reversed;
                }
            }


            //================================================================================================//
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="trainName"></param>
            /// <param name="simulator"></param>
            /// <param name="ttfilename"></param>
            public TTTrainInfo(int icolumn, string trainName, Simulator simulator, int index, TimetableInfo thisParent)
            {
                parentInfo = thisParent;
                Name = trainName.Trim();
                TTTrain = new TTTrain();
                columnIndex = icolumn;
                Index = index;
            }

            //================================================================================================//
            /// <summary>
            /// Build train from info in single column
            /// </summary>
            /// <param name="fileStrings"></param>
            /// <param name="RowInfo"></param>
            /// <param name="pathRow"></param>
            /// <param name="consistRow"></param>
            /// <param name="startRow"></param>
            /// <param name="description"></param>
            /// <param name="stationNames"></param>
            /// <param name="ttInfo"></param>
            public bool BuildTrain(List<string[]> fileStrings, rowType[] RowInfo, int pathRow, int consistRow, int startRow, int disposeRow, int briefingRow, string description,
                Dictionary<int, StationInfo> stationNames, float actSpeedConv, TimetableInfo ttInfo)
            {
                TTDescription = description;

                // set name

                // if $static, set starttime row to $static and create unique name
                if (Name.Contains("$static", StringComparison.OrdinalIgnoreCase))
                {
                    fileStrings[startRow][columnIndex] = "$static";

                    if (Name.Trim()[0] == '$')
                    {
                        TTTrain.Name = $"S{columnIndex}:{TTDescription}";
                    }
                    else
                    {
                        string[] nameParts = Name.Split('$');
                        TTTrain.Name = nameParts[0].Trim();
                    }
                }
                else
                {
                    TTTrain.Name = Name + ":" + TTDescription;
                }

                TTTrain.MovementState = AiMovementState.Static;

                // no path defined : exit
                if (string.IsNullOrEmpty(fileStrings[pathRow][columnIndex]))
                {
                    Trace.TraceInformation("Error for train {0} : no path defined", TTTrain.Name);
                    return (false);
                }

                string pathFilefull = ExtractPathString(ttInfo.simulator.RouteFolder.PathsFolder, fileStrings[pathRow][columnIndex], ref TTTrain);

                string consistDirectory = ttInfo.simulator.RouteFolder.ContentFolder.ConsistsFolder;

                string consistdef = fileStrings[consistRow][columnIndex];

                // no consist defined : exit
                if (String.IsNullOrEmpty(consistdef))
                {
                    Trace.TraceInformation("Error for train {0} : no consist defined", TTTrain.Name);
                    return (false);
                }

                List<ConsistInfo> consistdetails = ProcessConsistInfo(consistdef);
                string trainsetDirectory = ttInfo.simulator.RouteFolder.ContentFolder.TrainSetsFolder;

                // extract path
                string pathExtension = Path.GetExtension(pathFilefull);
                if (String.IsNullOrEmpty(pathExtension))
                    pathFilefull = Path.ChangeExtension(pathFilefull, "pat");
                ttInfo.TrainRouteXRef.Add(Index, pathFilefull);    // set reference to path

                if (!ttInfo.Paths.ContainsKey(pathFilefull))
                {
                    ttInfo.Paths.Add(pathFilefull, null);  // insert name in dictionary, path will be loaded later
                }

                // build consist
                bool returnValue = true;
                returnValue = BuildConsist(consistdetails, trainsetDirectory, consistDirectory, ttInfo.simulator);

                // return if consist could not be loaded
                if (!returnValue) return (returnValue);

                // derive starttime

                if (validTrain)
                {
                    ExtractStartTime(fileStrings[startRow][columnIndex], consistdetails[0].ConsistFile, ttInfo.simulator);
                }

                // process dispose info
                if (disposeRow > 0)
                {
                    string disposeString = fileStrings[disposeRow][columnIndex];

                    if (!string.IsNullOrEmpty(disposeString))
                    {
                        string[] disposeCommandString = disposeString.Split('$');

                        foreach (string thisDisposeString in disposeCommandString)
                        {
                            if (!string.IsNullOrEmpty(thisDisposeString))
                            {
                                TTTrainCommands disposeCommands = new TTTrainCommands(thisDisposeString);
                                switch (disposeCommands.CommandToken)
                                {
                                    case string forms when forms.Equals("forms", StringComparison.OrdinalIgnoreCase):
                                        DisposeDetails = new DisposeInfo(DisposeInfo.DisposeType.Forms, disposeCommands, TTTrain.FormCommand.TerminationFormed, TTTrain.Name);
                                        break;

                                    case string triggers when triggers.Equals("triggers", StringComparison.OrdinalIgnoreCase):
                                        DisposeDetails = new DisposeInfo(DisposeInfo.DisposeType.Triggers, disposeCommands, TTTrain.FormCommand.TerminationTriggered, TTTrain.Name);
                                        break;

                                    case string staticTrain when staticTrain.Equals("static", StringComparison.OrdinalIgnoreCase):
                                        DisposeDetails = new DisposeInfo(DisposeInfo.DisposeType.Static, disposeCommands, TTTrain.FormCommand.TerminationFormed, TTTrain.Name);
                                        break;

                                    case string stable when stable.Equals("stable", StringComparison.OrdinalIgnoreCase):
                                        DisposeDetails = new DisposeInfo(DisposeInfo.DisposeType.Stable, disposeCommands, TTTrain.FormCommand.TerminationFormed, TTTrain.Name);
                                        break;

                                    case string pool when pool.Equals("pool", StringComparison.OrdinalIgnoreCase):
                                        DisposeDetails = new DisposeInfo(DisposeInfo.DisposeType.Pool, disposeCommands, TTTrain.FormCommand.None, TTTrain.Name);
                                        break;

                                    case string attach when attach.Equals("attach", StringComparison.OrdinalIgnoreCase):
                                        TTTrain.AttachDetails = new AttachInfo(-1, disposeCommands, TTTrain);
                                        break;

                                    case string detach when detach.Equals("detach", StringComparison.OrdinalIgnoreCase):
                                        DetachInfo thisDetach = new DetachInfo(TTTrain, disposeCommands, false, false, true, -1, null);
                                        if (TTTrain.DetachDetails.ContainsKey(-1))
                                        {
                                            List<DetachInfo> tempList = TTTrain.DetachDetails[-1];
                                            tempList.Add(thisDetach);
                                        }
                                        else
                                        {
                                            List<DetachInfo> tempList = new List<DetachInfo>();
                                            tempList.Add(thisDetach);
                                            TTTrain.DetachDetails.Add(-1, tempList);
                                        }
                                        break;

                                    case string pickup when pickup.Equals("pickup", StringComparison.OrdinalIgnoreCase):
                                        if (!DisposeDetails.FormTrain)
                                        {
                                            Trace.TraceInformation("Train : {0} : $pickup in dispose command is only allowed if preceded by a $forms command", TTTrain.Name);
                                        }
                                        else
                                        {
                                            PickUpInfo thisPickup = new PickUpInfo(-1, disposeCommands, TTTrain);
                                            TTTrain.PickUpDetails.Add(thisPickup);
                                        }
                                        break;

                                    case string transfer when transfer.Equals("transfer", StringComparison.OrdinalIgnoreCase):
                                        if (!DisposeDetails.FormTrain)
                                        {
                                            Trace.TraceInformation("Train : {0} : $transfer in dispose command is only allowed if preceded by a $forms command", TTTrain.Name);
                                        }
                                        else if (TTTrain.TransferTrainDetails.ContainsKey(-1))
                                        {
                                            Trace.TraceInformation("Train : {0} : cannot define multiple transfer on static consists", TTTrain.Name);
                                        }
                                        else
                                        {
                                            TransferInfo thisTransfer = new TransferInfo(-1, disposeCommands, TTTrain);
                                            List<TransferInfo> newList = new List<TransferInfo>();
                                            newList.Add(thisTransfer);

                                            if (thisTransfer.TransferTrain == -99)
                                            {
                                                TTTrain.TransferTrainDetails.Add(-99, newList); //set key to -99 as reference
                                            }
                                            else
                                            {
                                                TTTrain.TransferTrainDetails.Add(-1, newList); // set key to -1 to work out reference later
                                            }
                                        } 
                                        break;

                                    case string activate when activate.Equals("activate", StringComparison.OrdinalIgnoreCase):
                                        if (disposeCommands.CommandValues == null || disposeCommands.CommandValues.Count < 1)
                                        {
                                            Trace.TraceInformation("No train reference set for train activation, train {0}", Name);
                                            break;
                                        }

                                        TTTrain.TriggerActivation thisTrigger = new TTTrain.TriggerActivation();

                                        thisTrigger.activationType = TTTrain.TriggerActivationType.Dispose;
                                        thisTrigger.activatedName = disposeCommands.CommandValues[0];
                                        TTTrain.activatedTrainTriggers.Add(thisTrigger);

                                        break;

                                    default:
                                        Trace.TraceWarning("Invalid dispose string defined for train {0} : {1}",
                                            TTTrain.Name, disposeCommands.CommandToken);
                                        break;
                                }
                            }
                        }
                    }
                }

                // derive station stops and other info

                for (int iRow = 1; iRow <= fileStrings.Count - 1; iRow++)
                {
                    switch (RowInfo[iRow])
                    {
                        case rowType.directionInfo:   // no longer used, maintained for compatibility with existing timetables
                            break;

                        case rowType.stationInfo:
                            StationInfo stationDetails = stationNames[iRow];
                            if (!string.IsNullOrEmpty(fileStrings[iRow][columnIndex]))
                            {
                                if (Stops.ContainsKey(stationDetails.StationName))
                                {
                                    Trace.TraceInformation("Double station reference : train " + Name + " ; station : " + stationDetails.StationName);
                                }
                                else if (fileStrings[iRow][columnIndex].StartsWith("P", StringComparison.OrdinalIgnoreCase))
                                {
                                    // allowed in timetable but not yet implemented
                                }
                                else
                                {
                                    Stops.Add(stationDetails.StationName, ProcessStopInfo(fileStrings[iRow][columnIndex], stationDetails));
                                }
                            }
                            break;

                        case rowType.restartDelayInfo:
                            ProcessRestartDelay(fileStrings[iRow][columnIndex]);
                            break;

                        case rowType.speedInfo:
                            ProcessSpeedInfo(fileStrings[iRow][columnIndex], actSpeedConv);
                            break;

                        case rowType.trainNotesInfo:
                            if (!string.IsNullOrEmpty(fileStrings[iRow][columnIndex]))
                            {
                                string[] commandStrings = fileStrings[iRow][columnIndex].Split('$');
                                foreach (string thisCommand in commandStrings)
                                {
                                    if (!string.IsNullOrEmpty(thisCommand))
                                    {
                                        TrainCommands.Add(new TTTrainCommands(thisCommand));
                                    }
                                }
                            }
                            break;

                        default:
                            break;
                    }
                }

                // set speed details based on route, config and input
                TTTrain.ProcessSpeedSettings();

                if (briefingRow >= 0)
                    TTTrain.Briefing = fileStrings[briefingRow][columnIndex].Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);

                return (true);
            }

            //================================================================================================//
            /// <summary>
            /// Extract path string from train details, add it to list of paths if not yet added
            /// </summary>
            /// <param name="pathDirectory"></param>
            /// <param name="pathString"></param>
            /// <param name="thisTrain"></param>
            /// <returns></returns>
            public string ExtractPathString(string pathDirectory, string pathString, ref TTTrain thisTrain)
            {
                string fullstring = string.Empty;

                // process strings
                string procPathString = pathString;
                List<TTTrainCommands> pathCommands = new List<TTTrainCommands>();

                if (!string.IsNullOrEmpty(procPathString))
                {
                    string[] pathCommandString = procPathString.Split('$');
                    foreach (string thisCommand in pathCommandString)
                    {
                        if (!string.IsNullOrEmpty(thisCommand))
                        {
                            pathCommands.Add(new TTTrainCommands(thisCommand));
                        }
                    }

                    // actual path is string [0]
                    fullstring = Path.Combine(pathDirectory, pathCommandString[0].Trim());
                    pathCommands.RemoveAt(0);

                    // process qualifiers
                    foreach (TTTrainCommands thisCommand in pathCommands)
                    {
                        if (!string.IsNullOrEmpty(thisCommand.CommandToken))
                        {
                            switch (thisCommand.CommandToken)
                            {
                                case string endSignal when endSignal.Equals("endatlastsignal", StringComparison.OrdinalIgnoreCase):
                                    bool reverse = false;

                                    if (thisCommand.CommandQualifiers?.Count > 0)
                                    {
                                        if (string.Equals(thisCommand.CommandQualifiers[0].QualifierName, "reverse", StringComparison.OrdinalIgnoreCase))
                                        {
                                            reverse = true;
                                        }
                                    }

                                    thisTrain.ReqLastSignalStop = reverse ? TTTrain.LastSignalStop.Reverse : TTTrain.LastSignalStop.Last;
                                    break;

                                default:
                                    Trace.TraceInformation("Train {0} : invalid qualifier for path field : {1} \n", TTTrain.Name, thisCommand.CommandToken);
                                    break;
                            }
                        }
                    }
                }

                return (fullstring);
            }

            //================================================================================================//
            /// <summary>
            /// Extract start time info from train details
            /// </summary>
            /// <param name="startString"></param>
            /// <param name="consistInfo"></param>
            /// <param name="simulator"></param>
            public void ExtractStartTime(string startString, string consistInfo, Simulator simulator)
            {
                string[] startparts = new string[1];
                string startTimeString = string.Empty;
                string activateTimeString = string.Empty;
                bool created = false;
                bool createStatic = false;
                string createAhead = string.Empty;
                string createInPool = string.Empty;
                bool startNextNight = false;
                string createFromPool = string.Empty;
                string createPoolDirection = string.Empty;
                bool setConsistName = false;
                bool activationRequired = false;

                // process qualifier if set

                List<TTTrainCommands> StartCommands = new List<TTTrainCommands>();

                if (startString.Contains('$', StringComparison.OrdinalIgnoreCase))
                {
                    string[] commandStrings = startString.Split('$');
                    foreach (string thisCommand in commandStrings)
                    {
                        if (!string.IsNullOrEmpty(thisCommand))
                        {
                            StartCommands.Add(new TTTrainCommands(thisCommand));
                        }
                    }

                    // first command is start time except for static
                    if (!string.Equals(StartCommands[0].CommandToken, "static", StringComparison.OrdinalIgnoreCase))
                    {
                        startTimeString = StartCommands[0].CommandToken;
                        activateTimeString = StartCommands[0].CommandToken;

                        StartCommands.RemoveAt(0);
                    }

                    foreach (TTTrainCommands thisCommand in StartCommands)
                    {
                        switch (thisCommand.CommandToken)
                        {
                            // check for create - syntax : $create [=starttime] [/ahead = train] [/pool = pool]
                            case string create when create.Equals("create", StringComparison.OrdinalIgnoreCase):
                                created = true;

                                // process starttime
                                if (thisCommand.CommandValues != null && thisCommand.CommandValues.Count > 0)
                                {
                                    startTimeString = thisCommand.CommandValues[0];
                                }
                                else
                                // if not set, start at 1 second (same start time as for static so /ahead will work for both create and static)
                                {
                                    startTimeString = "00:00:01";
                                }

                                // check additional qualifiers
                                if (thisCommand.CommandQualifiers != null && thisCommand.CommandQualifiers.Count > 0)
                                {
                                    foreach (TTTrainCommands.TTTrainComQualifiers thisQualifier in thisCommand.CommandQualifiers)
                                    {
                                        switch (thisQualifier.QualifierName)
                                        {
                                            case "ahead":
                                                if (thisQualifier.QualifierValues != null && thisQualifier.QualifierValues.Count > 0)
                                                {
                                                    createAhead = thisQualifier.QualifierValues[0];
                                                }
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                }
                                break;

                            // pool : created from pool - syntax : $pool = pool [/direction = backward | forward]
                            case string pool when pool.Equals("pool", StringComparison.OrdinalIgnoreCase):
                                if (thisCommand.CommandValues == null || thisCommand.CommandValues.Count < 1)
                                {
                                    Trace.TraceInformation("Missing poolname for train {0}, train not included", TTTrain.Name + "\n");
                                }
                                else
                                {
                                    createFromPool = thisCommand.CommandValues[0];
                                    if (thisCommand.CommandQualifiers != null)
                                    {
                                        foreach (TTTrainCommands.TTTrainComQualifiers thisQualifier in thisCommand.CommandQualifiers)
                                        {
                                            switch (thisQualifier.QualifierName)
                                            {
                                                case string setName when setName.Equals("set_consist_name", StringComparison.OrdinalIgnoreCase):
                                                    setConsistName = true;
                                                    break;

                                                case string direction when direction.Equals("direction", StringComparison.OrdinalIgnoreCase):
                                                    createPoolDirection = thisQualifier.QualifierValues[0];
                                                    break;

                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                }
                                break;

                            // check for $next : set special flag to start after midnight
                            case string next when next.Equals("next", StringComparison.OrdinalIgnoreCase):
                                startNextNight = true;
                                break;

                            // static : syntax : $static [/ahead = train]
                            case string staticTrain when staticTrain.Equals("static", StringComparison.OrdinalIgnoreCase):
                                createStatic = true;

                                // check additional qualifiers
                                if (thisCommand.CommandQualifiers != null && thisCommand.CommandQualifiers.Count > 0)
                                {
                                    foreach (TTTrainCommands.TTTrainComQualifiers thisQualifier in thisCommand.CommandQualifiers)
                                    {
                                        switch (thisQualifier.QualifierName)
                                        {
                                            case string ahead when ahead.Equals("ahead", StringComparison.OrdinalIgnoreCase):
                                                if (thisQualifier.QualifierValues != null && thisQualifier.QualifierValues.Count > 0)
                                                {
                                                    createAhead = thisQualifier.QualifierValues[0];
                                                }
                                                break;

                                            case string pool when pool.Equals("pool", StringComparison.OrdinalIgnoreCase):
                                                if (thisQualifier.QualifierValues != null && thisQualifier.QualifierValues.Count > 0)
                                                {
                                                    createInPool = thisQualifier.QualifierValues[0];
                                                    if (!simulator.PoolHolder.Pools.ContainsKey(createInPool))
                                                    {
                                                        Trace.TraceInformation("Train : " + TTTrain.Name + " : no such pool : " + createInPool + " ; train not created");
                                                        createInPool = String.Empty;
                                                    }
                                                }
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                }
                                break;

                            // activated : set activated flag
                            case string activated when activated.Equals("activated", StringComparison.OrdinalIgnoreCase):
                                activationRequired = true;
                                break;

                            // invalid command
                            default:
                                Trace.TraceInformation("Train : " + Name + " invalid command for start value : " + thisCommand.CommandToken + "\n");
                                break;
                        }
                    }
                }
                else
                {
                    startTimeString = startString;
                    activateTimeString = startString;
                }

                TimeSpan startingTime;
                bool validSTime = TimeSpan.TryParse(startTimeString, out startingTime);
                TimeSpan activateTime;
                bool validATime = TimeSpan.TryParse(activateTimeString, out activateTime);

                if (validSTime && validATime)
                {
                    TTTrain.StartTime = Math.Max(Convert.ToInt32(startingTime.TotalSeconds), 1);
                    TTTrain.ActivateTime = Math.Max(Convert.ToInt32(activateTime.TotalSeconds), 1);
                    TTTrain.Created = created;
                    TTTrain.TriggeredActivationRequired = activationRequired;

                    // trains starting after midnight
                    if (startNextNight && TTTrain.StartTime.HasValue)
                    {
                        TTTrain.StartTime = TTTrain.StartTime.Value + (24 * 3600);
                        TTTrain.ActivateTime = TTTrain.ActivateTime.Value + (24 * 3600);
                    }

                    if (created && !string.IsNullOrEmpty(createAhead))
                    {
                        if (!createAhead.Contains(':', StringComparison.OrdinalIgnoreCase))
                        {
                            TTTrain.CreateAhead = createAhead + ":" + TTDescription;
                        }
                        else
                        {
                            TTTrain.CreateAhead = createAhead;
                        }
                    }

                    if (!string.IsNullOrEmpty(createFromPool))
                    {
                        TTTrain.CreateFromPool = createFromPool;
                        TTTrain.ForcedConsistName = string.Empty;

                        if (setConsistName)
                        {
                            TTTrain.ForcedConsistName = consistInfo;
                        }

                        switch (createPoolDirection)
                        {
                            case "backward":
                                TTTrain.CreatePoolDirection = TimetablePool.PoolExitDirectionEnum.Backward;
                                break;

                            case "forward":
                                TTTrain.CreatePoolDirection = TimetablePool.PoolExitDirectionEnum.Forward;
                                break;

                            default:
                                TTTrain.CreatePoolDirection = TimetablePool.PoolExitDirectionEnum.Undefined;
                                break;
                        }
                    }

                    StartTime = TTTrain.ActivateTime.Value;
                }
                else if (!string.IsNullOrEmpty(createInPool))
                {
                    TTTrain.StartTime = 1;
                    TTTrain.ActivateTime = null;
                    TTTrain.CreateInPool = createInPool;
                }
                else if (createStatic)
                {
                    TTTrain.StartTime = 1;
                    TTTrain.ActivateTime = null;

                    if (!string.IsNullOrEmpty(createAhead))
                    {
                        if (!createAhead.Contains(':', StringComparison.OrdinalIgnoreCase))
                        {
                            TTTrain.CreateAhead = createAhead + ":" + TTDescription;
                        }
                        else
                        {
                            TTTrain.CreateAhead = createAhead;
                        }
                    }
                }
                else
                {
                    Trace.TraceInformation("Invalid starttime {0} for train {1}, train not included", startString, TTTrain.Name);
                    validTrain = false;
                }

                // activation is not allowed if started from pool
                if (activationRequired && !string.IsNullOrEmpty(createFromPool))
                {
                    activationRequired = false;
                    Trace.TraceInformation("Trigger activation not allowed when starting from pool, trigger activation reset for train {0}", TTTrain.Name);                  
                }

            }

            //================================================================================================//
            /// <summary>
            /// Extract restart delay info from train details
            /// </summary>
            /// <param name="restartDelayInfo"></param>
            public void ProcessRestartDelay(string restartDelayInfo)
            {
                // build list of commands
                List<TTTrainCommands> RestartDelayCommands = new List<TTTrainCommands>();
                
                if (!string.IsNullOrEmpty(restartDelayInfo))
                {
                    string[] commandStrings = restartDelayInfo.Split('$');
                    foreach (string thisCommand in commandStrings)
                    {
                        if (!string.IsNullOrEmpty(thisCommand))
                        {
                            RestartDelayCommands.Add(new TTTrainCommands(thisCommand));
                        }
                    }
                }

                // process list of commands

                foreach (TTTrainCommands thisCommand in RestartDelayCommands)
                {
                    switch (thisCommand.CommandToken)
                    {
                        // delay when new
                        case string newTrain when newTrain.Equals("new", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.newStart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay when restarting from signal or other path action
                        case string path when path.Equals("path", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.pathRestart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay when restarting from station stop
                        case string station when station.Equals("station", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.stationRestart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay when restarting when following stopped train
                        case string follow when follow.Equals("follow", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.followRestart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay after attaching
                        case string attach when attach.Equals("attach", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.attachRestart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay on detaching
                        case string detach when detach.Equals("detach", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.detachRestart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay for train and moving table
                        case string movingTable when movingTable.Equals("movingtable", StringComparison.OrdinalIgnoreCase):
                            TTTrain.DelayedStartSettings.movingtableRestart = ProcessRestartDelayValues(TTTrain.Name, thisCommand.CommandQualifiers, thisCommand.CommandToken);
                            break;

                        // delay when restarting at reversal
                        case string reverse when reverse.Equals("reverse", StringComparison.OrdinalIgnoreCase):
                            // process additional reversal delay
                            for (int iIndex = thisCommand.CommandQualifiers.Count - 1; iIndex >= 0; iIndex--)
                            {
                                TTTrainCommands.TTTrainComQualifiers thisQual = thisCommand.CommandQualifiers[iIndex];
                                switch (thisQual.QualifierName)
                                {
                                    case string additional when additional.Equals("additional", StringComparison.OrdinalIgnoreCase):
                                        try
                                        {
                                            TTTrain.DelayedStartSettings.reverseAddedDelaySperM = Convert.ToSingle(thisQual.QualifierValues[0], CultureInfo.CurrentCulture);
                                        }
                                        catch
                                        {
                                            Trace.TraceInformation("Train {0} : invalid value for '$reverse /additional' delay value : {1} \n", TTTrain.Name, thisQual.QualifierValues[0]);
                                        }
                                        break;

                                    default:
                                        Trace.TraceInformation("Invalid qualifier in restartDelay value for reversal : {0} for train : {1}", thisQual.QualifierName, TTTrain.Name);
                                        break;
                                }
                            }
                            break;

                        default:
                            Trace.TraceInformation("Invalid command in restartDelay value : {0} for train : {1}", thisCommand.CommandToken, TTTrain.Name);
                            break;
                    }
                }
            }

            //================================================================================================//
            /// <summary>
            /// Read and convert input of restart delay values
            /// </summary>
            /// <param name="trainName"></param>
            /// <param name="commandQualifiers"></param>
            /// <param name="delayType"></param>
            /// <returns></returns>
            public TTTrain.DelayedStartBase ProcessRestartDelayValues(string trainName, List<TTTrainCommands.TTTrainComQualifiers> commandQualifiers, string delayType)
            {
                // preset values
                TTTrain.DelayedStartBase newDelayInfo = new TTTrain.DelayedStartBase();

                // process command qualifiers
                foreach (TTTrainCommands.TTTrainComQualifiers thisQual in commandQualifiers)
                {
                    switch (thisQual.QualifierName)
                    {
                        case string fix when fix.Equals("fix", StringComparison.OrdinalIgnoreCase):
                            try
                            {
                                newDelayInfo.fixedPartS = Convert.ToInt32(thisQual.QualifierValues[0], CultureInfo.CurrentCulture);
                            }
                            catch
                            {
                                Trace.TraceInformation("Train {0} : invalid value for fixed part for '{1}' restart delay : {2} \n",
                                    trainName, delayType, thisQual.QualifierValues[0]);
                            }
                            break;

                        case string var when var.Equals("var", StringComparison.OrdinalIgnoreCase):
                            try
                            {
                                newDelayInfo.randomPartS = Convert.ToInt32(thisQual.QualifierValues[0], CultureInfo.CurrentCulture);
                            }
                            catch
                            {
                                Trace.TraceInformation("Train {0} : invalid value for variable part for '{1}' restart delay : {2} \n",
                                    trainName, delayType, thisQual.QualifierValues[0]);
                            }
                            break;

                        default:
                            Trace.TraceInformation("Invalid qualifier in restartDelay value : {0} for train : {1}", thisQual.QualifierName, trainName);
                            break;
                    }
                }

                return (newDelayInfo);
            }

            //================================================================================================//
            /// <summary>
            /// Extract speed info from train details
            /// </summary>
            /// <param name="speedInfo"></param>
            /// <param name="actSpeedConv"></param>
            public void ProcessSpeedInfo(string speedInfo, float actSpeedConv)
            {
                // build list of commands
                List<TTTrainCommands> SpeedCommands = new List<TTTrainCommands>();

                if (!string.IsNullOrEmpty(speedInfo))
                {
                    string[] commandStrings = speedInfo.Split('$');
                    foreach (string thisCommand in commandStrings)
                    {
                        if (!string.IsNullOrEmpty(thisCommand))
                        {
                            SpeedCommands.Add(new TTTrainCommands(thisCommand));
                        }
                    }

                    foreach (TTTrainCommands thisCommand in SpeedCommands)
                    {
                        if (thisCommand.CommandValues == null || thisCommand.CommandValues.Count < 1)
                        {
                            Trace.TraceInformation("Value missing in speed command : {0} for train : {1}", thisCommand.CommandToken, TTTrain.Name);
                            break;
                        }

                        switch (thisCommand.CommandToken)
                        {
                            case string max when max.Equals("max", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.maxSpeedMpS = Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * actSpeedConv;
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' speed setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            case string cruise when cruise.Equals("cruise", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.cruiseSpeedMpS = Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * actSpeedConv;
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' speed setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            case string maxDelay when maxDelay.Equals("maxdelay", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.cruiseMaxDelayS = Convert.ToInt32(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * 60; // defined in minutes
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            case string creep when creep.Equals("creep", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.creepSpeedMpS = Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * actSpeedConv;
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' speed setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            case string attach when attach.Equals("attach", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.attachSpeedMpS = Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * actSpeedConv;
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' speed setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            case string detach when detach.Equals("detach", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.detachSpeedMpS = Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * actSpeedConv;
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' speed setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            case string movingTable when movingTable.Equals("movingtable", StringComparison.OrdinalIgnoreCase):
                                try
                                {
                                    TTTrain.SpeedSettings.movingtableSpeedMpS = Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture) * actSpeedConv;
                                }
                                catch
                                {
                                    Trace.TraceInformation("Train {0} : invalid value for '{1}' speed setting : {2} \n",
                                        TTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                                }
                                break;

                            default:
                                Trace.TraceInformation("Invalid token in speed command : {0} for train : {1}", thisCommand.CommandToken, TTTrain.Name);
                                break;
                        }
                    }
                }
            }

            //================================================================================================//
            /// <summary>
            /// Extract and process consist info from train details
            /// </summary>
            /// <param name="consistDef"></param>
            /// <returns></returns>
            private List<ConsistInfo> ProcessConsistInfo(string consistDef)
            {
                List<ConsistInfo> consistDetails = new List<ConsistInfo>();
                string consistProc = consistDef.Trim();

                while (!string.IsNullOrEmpty(consistProc))
                {
                    if (consistProc[0] == '<')
                    {
                        int endIndex = consistProc.IndexOf('>', StringComparison.OrdinalIgnoreCase);
                        if (endIndex < 0)
                        {
                            Trace.TraceWarning("Incomplete consist definition : \">\" character missing : {0}", consistProc);
                            ConsistInfo thisConsist = new ConsistInfo(consistProc.Substring(1), false);
                            consistDetails.Add(thisConsist);
                            consistProc = String.Empty;
                        }
                        else
                        {
                            ConsistInfo thisConsist = new ConsistInfo(consistProc.Substring(1, endIndex - 1), false);
                            consistDetails.Add(thisConsist);
                            consistProc = consistProc.Substring(endIndex + 1).Trim();
                        }
                    }
                    else if (consistProc[0] == '$')
                    {
                        if (consistProc.Length >= 8 && consistProc.Substring(1, 7).Equals("reverse", StringComparison.OrdinalIgnoreCase))
                        {
                            if (consistDetails.Count > 0)
                            {
                                ConsistInfo thisConsist = consistDetails[consistDetails.Count - 1];
                                consistDetails.RemoveAt(consistDetails.Count - 1);
                                thisConsist = new ConsistInfo(thisConsist.ConsistFile, true);
                                consistDetails.Add(thisConsist);
                            }
                            else
                            {
                                Trace.TraceInformation("Invalid conmand at start of consist string {0}, command ingored", consistProc);
                            }
                            consistProc = consistProc.Substring(8).Trim();
                        }
                        else
                        {
                            Trace.TraceWarning("Invalid command in consist string : {0}", consistProc);
                            consistProc = String.Empty;
                        }
                    }
                    else
                    {
                        int plusIndex = consistProc.IndexOf('+', StringComparison.OrdinalIgnoreCase);
                        if (plusIndex == 0)
                        {
                            consistProc = consistProc.Substring(1).Trim();
                        }
                        else if (plusIndex > 0)
                        {
                            string consistFile = consistProc.Substring(0, plusIndex).Trim();

                            int sepIndex = consistFile.IndexOf('$', StringComparison.OrdinalIgnoreCase);
                            if (sepIndex > 0)
                            {
                                consistProc = string.Concat(consistFile.Substring(sepIndex).Trim(), consistProc.Substring(plusIndex).Trim());
                                consistFile = consistFile.Substring(0, sepIndex).Trim();
                            }
                            else
                            {
                                consistProc = consistProc.Substring(plusIndex + 1).Trim();
                            }
                            ConsistInfo thisConsist = new ConsistInfo(consistFile, false);
                            consistDetails.Add(thisConsist);
                        }
                        else
                        {
                            string consistFile = consistProc;

                            int sepIndex = consistProc.IndexOf('$', StringComparison.OrdinalIgnoreCase);
                            if (sepIndex > 0)
                            {
                                consistFile = consistProc.Substring(0, sepIndex).Trim();
                                consistProc = consistProc.Substring(sepIndex).Trim();
                            }
                            else
                            {
                                consistProc = string.Empty;
                            }
                            ConsistInfo thisConsist = new ConsistInfo(consistFile, false);
                            consistDetails.Add(thisConsist);
                        }
                    }
                }

                return (consistDetails);
            }

            //================================================================================================//
            /// <summary>
            /// Build train consist
            /// </summary>
            /// <param name="consistFile">Defined consist file</param>
            /// <param name="trainsetDirectory">Consist directory</param>
            /// <param name="simulator">Simulator</param>
            private bool BuildConsist(List<ConsistInfo> consistSets, string trainsetDirectory, string consistDirectory, Simulator simulator)
            {
                TTTrain.IsTilting = true;

                float? confMaxSpeed = null;
                TTTrain.Length = 0.0f;

                foreach (ConsistInfo consistDetails in consistSets)
                {
                    string consistFile = Path.Combine(consistDirectory, consistDetails.ConsistFile);

                    string pathExtension = Path.GetExtension(consistFile);
                    if (string.IsNullOrEmpty(pathExtension))
                        consistFile = Path.ChangeExtension(consistFile, "con");

                    if (!consistFile.Contains("tilted", StringComparison.OrdinalIgnoreCase))
                    {
                        TTTrain.IsTilting = false;
                    }

                    ConsistFile conFile = null;

                    // try to load config file, exit if failed
                    try
                    {
                        conFile = new ConsistFile(consistFile);
                    }
                    catch (Exception e)
                    {
                        if (!reportedConsistFailures.Contains(consistFile.ToString()))
                        {
                            Trace.TraceInformation("Reading " + consistFile.ToString() + " : " + e.ToString());
                            reportedConsistFailures.Add(consistFile.ToString());
                            return false;
                        }
                    }

                    TTTrain.TcsParametersFileName = conFile.Train.TcsParametersFileName;

                    AddWagons(conFile, consistDetails, trainsetDirectory, simulator);

                    // derive speed
                    if (conFile.Train.MaxVelocity?.A > 0)
                    {
                        if (confMaxSpeed.HasValue)
                        {
                            confMaxSpeed = Math.Min(confMaxSpeed.Value, conFile.Train.MaxVelocity.A);
                        }
                        else
                        {
                            confMaxSpeed = Math.Min((float)simulator.Route.SpeedLimit, conFile.Train.MaxVelocity.A);
                        }
                    }
                }

                if (TTTrain.Cars.Count <= 0)
                {
                    Trace.TraceInformation("Empty consists for train " + TTTrain.Name + " : train removed");
                    validTrain = false;
                }

                // set train details
                TTTrain.CheckFreight();
                TTTrain.SetDistributedPowerUnitIds();
                TTTrain.ReinitializeEOT();
                TTTrain.SpeedSettings.routeSpeedMpS = (float)simulator.Route.SpeedLimit;

                if (!confMaxSpeed.HasValue || confMaxSpeed.Value <= 0f)
                {
                    float tempMaxSpeedMpS = TTTrain.TrainMaxSpeedMpS;

                    foreach (TrainCar car in TTTrain.Cars)
                    {
                        float engineMaxSpeedMpS = 0;
                        if (car is MSTSLocomotive locomotive)
                            engineMaxSpeedMpS = locomotive.MaxSpeedMpS;
                        if (car is MSTSElectricLocomotive electricLocomotive)
                            engineMaxSpeedMpS = electricLocomotive.MaxSpeedMpS;
                        if (car is MSTSDieselLocomotive dieselLocomotive)
                            engineMaxSpeedMpS = dieselLocomotive.MaxSpeedMpS;
                        if (car is MSTSSteamLocomotive steamLocomotive)
                            engineMaxSpeedMpS = steamLocomotive.MaxSpeedMpS;

                        if (engineMaxSpeedMpS > 0)
                        {
                            tempMaxSpeedMpS = Math.Min(tempMaxSpeedMpS, engineMaxSpeedMpS);
                        }
                    }

                    TTTrain.SpeedSettings.consistSpeedMpS = tempMaxSpeedMpS;
                }
                else
                {
                    TTTrain.SpeedSettings.consistSpeedMpS = confMaxSpeed.Value;
                }

                return true;
            }

            /// <summary>
            /// Add wagons from consist file to traincar list
            /// </summary>
            /// <param name="consistFile">Processed consist File</param>
            /// <param name="trainsDirectory">Consist Directory</param>
            /// <param name="simulator">Simulator</param>
            private void AddWagons(ConsistFile consistFile, ConsistInfo consistDetails, string trainsDirectory, Simulator simulator)
            {
                int carId = 0;

                IEnumerable<Wagon> wagonList = consistDetails.Reversed ? 
                    consistFile.Train.Wagons.AsEnumerable().Reverse() : consistFile.Train.Wagons;

                // add wagons
                foreach (Wagon wagon in wagonList)
                {
                    string wagonFolder = Path.Combine(trainsDirectory, wagon.Folder);
                    string wagonFilePath = Path.Combine(wagonFolder, wagon.Name + ".wag");

                    TrainCar car = null;

                    if (wagon.IsEngine)
                        wagonFilePath = Path.ChangeExtension(wagonFilePath, ".eng");
                    else if (wagon.IsEOT)
                    {
                        wagonFolder = Path.Combine(simulator.RouteFolder.ContentFolder.Folder, "trains\\orts_eot", wagon.Folder);
                        wagonFilePath = wagonFolder + @"\" + wagon.Name + ".eot";
                    }

                    if (!File.Exists(wagonFilePath))
                    {
                        Trace.TraceWarning($"Ignored missing {(wagon.IsEngine ? "engine" : "wagon")} {wagonFilePath} in consist {consistFile}");
                        continue;
                    }

                    car = RollingStock.Load(TTTrain, wagonFilePath);
                    car.UiD = wagon.UiD;
                    car.Flipped = consistDetails.Reversed ? !wagon.Flip : wagon.Flip;
                    car.CarID = $"{TTTrain.Number:0###}_{carId:0##}";
                    carId++;
                    car.OriginalConsist = consistDetails.ConsistFile.ToLowerInvariant();

                    car.SignalEvent(TrainEvent.Pantograph1Up);

                    TTTrain.Length += car.CarLengthM;
                    if (car is EndOfTrainDevice)
                        TTTrain.EndOfTrainDevice = car as EndOfTrainDevice;
                }
            }

            //================================================================================================//
            /// <summary>
            /// Process station stop info cell including possible commands
            /// Info may consist of :
            /// one or two time values (arr / dep time or pass time)
            /// commands
            /// time values and commands
            /// </summary>
            /// <param name="stationInfo">Reference to station string</param>
            /// <param name="stationName">Station Details class</param>
            /// <returns> StopInfo structure</returns>
            public StopInfo ProcessStopInfo(string stationInfo, StationInfo stationDetails)
            {
                string[] arr_dep = new string[2] { string.Empty, string.Empty };
                string fullCommandString = string.Empty;

                if (stationInfo.Contains('$', StringComparison.OrdinalIgnoreCase))
                {
                    int commandseparator = stationInfo.IndexOf('$', StringComparison.OrdinalIgnoreCase);
                    fullCommandString = stationInfo.Substring(commandseparator + 1);
                    stationInfo = stationInfo.Substring(0, commandseparator);
                }

                if (!string.IsNullOrEmpty(stationInfo))
                {
                    if (stationInfo.Contains('-', StringComparison.OrdinalIgnoreCase))
                    {
                        arr_dep = stationInfo.Split(new char[1] { '-' }, 2);
                    }
                    else
                    {
                        arr_dep[0] = stationInfo;
                        arr_dep[1] = stationInfo;
                    }
                }

                StopInfo newStop = new StopInfo(stationDetails.StationName, arr_dep[0], arr_dep[1], parentInfo);
                newStop.holdState = stationDetails.HoldState == StationInfo.HoldInfo.Hold ? StopInfo.SignalHoldType.Normal : StopInfo.SignalHoldType.None;
                newStop.noWaitSignal = stationDetails.NoWaitSignal;

                if (!string.IsNullOrEmpty(fullCommandString))
                {
                    newStop.Commands = new List<TTTrainCommands>();
                    string[] commandStrings = fullCommandString.Split('$');
                    foreach (string thisCommand in commandStrings)
                    {
                        newStop.Commands.Add(new TTTrainCommands(thisCommand));
                    }
                }

                // process forced stop through station commands
                if (stationDetails.HoldState == StationInfo.HoldInfo.ForceHold)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands("forcehold"));
                }

                // process forced wait signal command
                if (stationDetails.ForceWaitSignal)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands("forcewait"));
                }

                // process terminal through station commands
                if (stationDetails.IsTerminal)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands("terminal"));
                }

                // process closeupsignal through station commands
                if (stationDetails.CloseupSignal)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands("closeupsignal"));
                }

                // process actual min stop time
                if (stationDetails.actMinStopTime.HasValue)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands($"stoptime={stationDetails.actMinStopTime.Value}"));
                }

                // process restrict to signal
                if (stationDetails.RestrictPlatformToSignal)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands("restrictplatformtosignal"));
                }

                // process restrict to signal
                if (stationDetails.ExtendPlatformToSignal)
                {
                    if (newStop.Commands == null)
                    {
                        newStop.Commands = new List<TTTrainCommands>();
                    }

                    newStop.Commands.Add(new TTTrainCommands("extendplatformtosignal"));
                }

                return (newStop);
            }

            //================================================================================================//
            /// <summary>
            /// Convert station stops to train stationStop info
            /// </summary>
            /// <param name="simulator"></param>
            /// <param name="actTrain"></param>
            /// <param name="name"></param>
            public void ConvertStops(Simulator simulator, TTTrain actTrain, string name)
            {
                foreach (KeyValuePair<string, StopInfo> stationStop in Stops)
                {
                    if (actTrain.TCRoute.StationCrossReferences.ContainsKey(stationStop.Key))
                    {
                        StopInfo stationInfo = stationStop.Value;
                        int[] platformInfo = actTrain.TCRoute.StationCrossReferences[stationStop.Key];
                        bool ValidStop = stationInfo.BuildStopInfo(actTrain, platformInfo[2], simulator.SignalEnvironment);
                        if (!ValidStop)
                        {
                            Trace.TraceInformation("Station {0} not found for train {1}:{2} ", stationStop.Key, Name, TTDescription);
                        }
                        actTrain.TCRoute.StationCrossReferences.Remove(stationStop.Key);
                    }
                    else
                    {
                        Trace.TraceInformation("Station {0} not found for train {1}:{2} ", stationStop.Key, Name, TTDescription);
                    }
                }
                actTrain.TCRoute.StationCrossReferences.Clear();  // info no longer required
            }

            //================================================================================================//
            /// <summary>
            /// Process Timetable commands entered as general notes
            /// All commands are valid from start of route
            /// </summary>
            /// <param name="simulator"></param>
            /// <param name="actTrain"></param>
            public void ProcessCommands(Simulator simulator, TTTrain actTTTrain)
            {
                foreach (TTTrainCommands thisCommand in TrainCommands)
                {
                    switch (thisCommand.CommandToken)
                    {
                        case "acc":
                            try
                            {
                                actTTTrain.MaxAccelMpSSP = actTTTrain.DefMaxAccelMpSSP * Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture);
                                actTTTrain.MaxAccelMpSSF = actTTTrain.DefMaxAccelMpSSF * Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture);
                            }
                            catch
                            {
                                Trace.TraceInformation("Train {0} : invalid value for '{1}' setting : {2} \n",
                                    actTTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                            }
                            break;

                        case "dec":
                            try
                            {
                                actTTTrain.MaxDecelMpSSP = actTTTrain.DefMaxDecelMpSSP * Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture);
                                actTTTrain.MaxDecelMpSSF = actTTTrain.DefMaxDecelMpSSF * Convert.ToSingle(thisCommand.CommandValues[0], CultureInfo.CurrentCulture);
                            }
                            catch
                            {
                                Trace.TraceInformation("Train {0} : invalid value for '{1}' setting : {2} \n",
                                    actTTTrain.Name, thisCommand.CommandToken, thisCommand.CommandValues[0]);
                            }
                            break;

                        case "doo":
                            actTTTrain.DriverOnlyOperation = true;
                            break;

                        case "forcereversal":
                            actTTTrain.ForceReversal = true;
                            break;

                        default:
                            actTTTrain.ProcessTimetableStopCommands(thisCommand, 0, -1, -1, -1, parentInfo);
                            break;
                    }
                }
            }

            //================================================================================================//
            /// <summary>
            /// Extract and process dispose info
            /// </summary>
            /// <param name="trainList"></param>
            /// <param name="playerTrain"></param>
            /// <param name="simulator"></param>
            /// <returns></returns>
            public bool ProcessDisposeInfo(ref List<TTTrain> trainList, TTTrainInfo playerTrain, Simulator simulator)
            {
                bool loadPathNoFailure = true;
                TTTrain formedTrain = null;
                TTTrain.FormCommand formtype = TTTrain.FormCommand.None;
                bool trainFound = false;

                // set closeup if required
                TTTrain.Closeup = DisposeDetails.Closeup;

                // train forms other train
                if (DisposeDetails.FormType == TTTrain.FormCommand.TerminationFormed || DisposeDetails.FormType == TTTrain.FormCommand.TerminationTriggered)
                {
                    formtype = DisposeDetails.FormType;
                    string[] otherTrainName = null;

                    if (DisposeDetails.FormedTrain == null)
                    {
                        Trace.TraceInformation("Error in dispose details for train : " + Name + " : no formed train defined");
                        return (true);
                    }

                    // extract name
                    if (DisposeDetails.FormedTrain.Contains('=', StringComparison.OrdinalIgnoreCase))
                    {
                        otherTrainName = DisposeDetails.FormedTrain.Split('='); // extract train name
                    }
                    else
                    {
                        otherTrainName = new string[2];
                        otherTrainName[1] = DisposeDetails.FormedTrain;
                    }

                    if (otherTrainName[1].Contains('/', StringComparison.OrdinalIgnoreCase))
                    {
                        int splitPosition = otherTrainName[1].IndexOf('/', StringComparison.OrdinalIgnoreCase);
                        otherTrainName[1] = otherTrainName[1][..splitPosition];
                    }

                    if (!otherTrainName[1].Contains(':', StringComparison.OrdinalIgnoreCase))
                    {
                        string[] timetableName = TTTrain.Name.Split(':');
                        otherTrainName[1] = $"{otherTrainName[1]}:{timetableName[1]}";
                    }

                    // search train
                    foreach (TTTrain otherTrain in trainList)
                    {
                        if (string.Equals(otherTrain.Name, otherTrainName[1], StringComparison.OrdinalIgnoreCase))
                        {
                            if (otherTrain.FormedOf >= 0)
                            {
                                Trace.TraceWarning("Train : {0} : dispose details : formed train {1} already formed out of another train",
                                    TTTrain.Name, otherTrain.Name);
                                break;
                            }

                            TTTrain.Forms = otherTrain.Number;
                            TTTrain.SetStop = DisposeDetails.SetStop;
                            TTTrain.FormsAtStation = DisposeDetails.FormsAtStation;
                            otherTrain.FormedOf = TTTrain.Number;
                            otherTrain.FormedOfType = DisposeDetails.FormType;
                            trainFound = true;
                            formedTrain = otherTrain;
                            break;
                        }
                    }

                    // if not found, try player train
                    if (!trainFound)
                    {
                        if (playerTrain != null && string.Equals(playerTrain.TTTrain.Name, otherTrainName[1], StringComparison.OrdinalIgnoreCase))
                        {
                            if (playerTrain.TTTrain.FormedOf >= 0)
                            {
                                Trace.TraceWarning("Train : {0} : dispose details : formed train {1} already formed out of another train",
                                    TTTrain.Name, playerTrain.Name);
                            }

                            TTTrain.Forms = playerTrain.TTTrain.Number;
                            TTTrain.SetStop = DisposeDetails.SetStop;
                            TTTrain.FormsAtStation = DisposeDetails.FormsAtStation;
                            playerTrain.TTTrain.FormedOf = TTTrain.Number;
                            playerTrain.TTTrain.FormedOfType = DisposeDetails.FormType;
                            trainFound = true;
                            formedTrain = playerTrain.TTTrain;
                        }
                    }

                    if (!trainFound)
                    {
                        Trace.TraceWarning("Train :  {0} : Dispose details : formed train {1} not found",
                            TTTrain.Name, otherTrainName[1]);
                    }
                }

                TTTrain outTrain = null;
                TTTrain inTrain = null;

                // check if train must be stabled
                if (DisposeDetails.Stable && (trainFound || DisposeDetails.FormStatic))
                {
                    // save final train
                    int finalForms = TTTrain.Forms;

                    // create outbound train (note : train is defined WITHOUT consist as it is formed of incoming train)
                    outTrain = new TTTrain(TTTrain);

                    bool addPathNoLoadFailure;
                    AIPath outPath = parentInfo.LoadPath(DisposeDetails.StableInfo.Stable_outpath, out addPathNoLoadFailure);
                    if (!addPathNoLoadFailure)
                    {
                        loadPathNoFailure = false;
                    }
                    else
                    {
                        outTrain.RearTDBTraveller = new Traveller(outPath.FirstNode.Location, outPath.FirstNode.NextMainNode.Location);
                        outTrain.Path = outPath;
                        outTrain.CreateRoute(false);
                        outTrain.ValidRoute[0] = new TrackCircuitPartialPathRoute(outTrain.TCRoute.TCRouteSubpaths[0]);
                        outTrain.AITrainDirectionForward = true;
                        outTrain.StartTime = DisposeDetails.StableInfo.Stable_outtime;
                        outTrain.ActivateTime = DisposeDetails.StableInfo.Stable_outtime;
                        if (string.IsNullOrEmpty(DisposeDetails.StableInfo.Stable_name))
                        {
                            outTrain.Name = $"SO_{TTTrain.Number:0000}";
                        }
                        else
                        {
                            outTrain.Name = DisposeDetails.StableInfo.Stable_name.ToLowerInvariant();
                            if (!outTrain.Name.Contains(':', StringComparison.OrdinalIgnoreCase))
                            {
                                int seppos = TTTrain.Name.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                                outTrain.Name = $"{outTrain.Name}:{TTTrain.Name.Substring(seppos + 1)}";
                            }
                        }
                        outTrain.FormedOf = TTTrain.Number;
                        outTrain.FormedOfType = TTTrain.FormCommand.TerminationFormed;
                        outTrain.TrainType = TrainType.AiAutoGenerated;
                        if (DisposeDetails.DisposeSpeed != null)
                        {
                            outTrain.SpeedSettings.maxSpeedMpS = DisposeDetails.DisposeSpeed.Value;
                            outTrain.SpeedSettings.restrictedSet = true;
                            outTrain.ProcessSpeedSettings();
                        }
                        trainList.Add(outTrain);

                        TTTrain.Forms = outTrain.Number;
                    }

                    // if stable to static
                    if (DisposeDetails.FormStatic)
                    {
                        outTrain.FormsStatic = true;
                    }
                    else
                    {
                        outTrain.FormsStatic = false;

                        // create inbound train
                        inTrain = new TTTrain(TTTrain);

                        AIPath inPath = parentInfo.LoadPath(DisposeDetails.StableInfo.Stable_inpath, out addPathNoLoadFailure);
                        if (!addPathNoLoadFailure)
                        {
                            loadPathNoFailure = false;
                        }
                        else
                        {
                            inTrain.RearTDBTraveller = new Traveller(inPath.FirstNode.Location, inPath.FirstNode.NextMainNode.Location);
                            inTrain.Path = inPath;
                            inTrain.CreateRoute(false);
                            inTrain.ValidRoute[0] = new TrackCircuitPartialPathRoute(inTrain.TCRoute.TCRouteSubpaths[0]);
                            inTrain.AITrainDirectionForward = true;
                            inTrain.StartTime = DisposeDetails.StableInfo.Stable_intime;
                            inTrain.ActivateTime = DisposeDetails.StableInfo.Stable_intime;
                            inTrain.Name = $"SI_{finalForms:0000}";
                            inTrain.FormedOf = outTrain.Number;
                            inTrain.FormedOfType = DisposeDetails.FormType; // set forms or triggered as defined in stable
                            inTrain.TrainType = TrainType.AiAutoGenerated;
                            inTrain.Forms = finalForms;
                            inTrain.SetStop = DisposeDetails.SetStop;
                            inTrain.FormsStatic = false;
                            inTrain.Stable_CallOn = DisposeDetails.CallOn;
                            if (DisposeDetails.DisposeSpeed != null)
                            {
                                inTrain.SpeedSettings.maxSpeedMpS = DisposeDetails.DisposeSpeed.Value;
                                inTrain.SpeedSettings.restrictedSet = true;
                                inTrain.ProcessSpeedSettings();
                            }

                            trainList.Add(inTrain);

                            outTrain.Forms = inTrain.Number;

                            formtype = inTrain.FormedOfType;

                            // set back reference from final train

                            formedTrain.FormedOf = inTrain.Number;
                            formedTrain.FormedOfType = TTTrain.FormCommand.TerminationFormed;

                            TrackCircuitPartialPathRoute lastSubpath = inTrain.TCRoute.TCRouteSubpaths[inTrain.TCRoute.TCRouteSubpaths.Count - 1];
                            if (inTrain.FormedOfType == TTTrain.FormCommand.TerminationTriggered && formedTrain.Number != 0) // no need to set consist for player train
                            {
                                bool reverseTrain = CheckFormedReverse(lastSubpath, formedTrain.TCRoute.TCRouteSubpaths[0]);
                                BuildStabledConsist(ref inTrain, formedTrain.Cars, formedTrain.TCRoute.TCRouteSubpaths[0], reverseTrain);
                            }
                        }
                    }
                }
                // if run round required, build runround

                if (formtype == TTTrain.FormCommand.TerminationFormed && trainFound && DisposeDetails.RunRound)
                {
                    TTTrain usedTrain;
                    bool atStart = false;  // indicates if run-round is to be performed before start of move or forms, or at end of move

                    if (DisposeDetails.Stable)
                    {
                        switch (DisposeDetails.RunRoundPos)
                        {
                            case DisposeInfo.RunRoundPosition.outposition:
                                usedTrain = outTrain;
                                atStart = true;
                                break;

                            case DisposeInfo.RunRoundPosition.inposition:
                                usedTrain = inTrain;
                                atStart = false;
                                break;

                            default:
                                usedTrain = inTrain;
                                atStart = true;
                                break;
                        }
                    }
                    else
                    {
                        usedTrain = formedTrain;
                        atStart = true;
                    }

                    bool addPathNoLoadFailure = BuildRunRound(ref usedTrain, atStart, DisposeDetails, simulator, ref trainList);
                    if (!addPathNoLoadFailure) loadPathNoFailure = false;
                }

                // static
                if (DisposeDetails.FormStatic)
                {
                    TTTrain.FormsStatic = true;
                }

                // pool
                if (DisposeDetails.Pool)
                {
                    // check pool name
                    if (!simulator.PoolHolder.Pools.ContainsKey(DisposeDetails.PoolName))
                    {
                        Trace.TraceInformation("Train : " + TTTrain.Name + " : reference to unkown pool in dispose command : " + DisposeDetails.PoolName + "\n");
                    }
                    else
                    {
                        TTTrain.ExitPool = DisposeDetails.PoolName;

                        switch (DisposeDetails.PoolExitDirection)
                        {
                            case "backward":
                                TTTrain.PoolExitDirection = TimetablePool.PoolExitDirectionEnum.Backward;
                                break;

                            case "forward":
                                TTTrain.PoolExitDirection = TimetablePool.PoolExitDirectionEnum.Forward;
                                break;

                            default:
                                TTTrain.PoolExitDirection = TimetablePool.PoolExitDirectionEnum.Undefined;
                                break;
                        }
                    }
                }
                return (loadPathNoFailure);
            }

            //================================================================================================//
            /// <summary>
            /// Build run round details and train
            /// </summary>
            /// <param name="rrtrain"></param>
            /// <param name="atStart"></param>
            /// <param name="disposeDetails"></param>
            /// <param name="simulator"></param>
            /// <param name="trainList"></param>
            /// <param name="paths"></param>
            public bool BuildRunRound(ref TTTrain rrtrain, bool atStart, DisposeInfo disposeDetails, Simulator simulator, ref List<TTTrain> trainList)
            {
                bool loadPathNoFailure = true;
                TTTrain formedTrain = new TTTrain(TTTrain);

                string formedpathFilefull = Path.Combine(simulator.RouteFolder.PathsFolder, DisposeDetails.RunRoundPath);
                string pathExtension = Path.GetExtension(formedpathFilefull);
                if (String.IsNullOrEmpty(pathExtension))
                    formedpathFilefull = Path.ChangeExtension(formedpathFilefull, "pat");

                bool addPathNoLoadFailure;
                AIPath formedPath = parentInfo.LoadPath(formedpathFilefull, out addPathNoLoadFailure);
                if (!addPathNoLoadFailure)
                {
                    loadPathNoFailure = false;
                }
                else
                {
                    formedTrain.RearTDBTraveller = new Traveller(formedPath.FirstNode.Location, formedPath.FirstNode.NextMainNode.Location);
                    formedTrain.Path = formedPath;
                    formedTrain.CreateRoute(false);
                    formedTrain.ValidRoute[0] = new TrackCircuitPartialPathRoute(formedTrain.TCRoute.TCRouteSubpaths[0]);
                    formedTrain.AITrainDirectionForward = true;
                    formedTrain.Name = $"RR_{rrtrain.Number:0000}";
                    formedTrain.FormedOf = rrtrain.Number;
                    formedTrain.FormedOfType = TTTrain.FormCommand.Detached;
                    formedTrain.TrainType = TrainType.AiAutoGenerated;
                    if (disposeDetails.DisposeSpeed != null)
                    {
                        formedTrain.SpeedSettings.maxSpeedMpS = disposeDetails.DisposeSpeed.Value;
                        formedTrain.SpeedSettings.restrictedSet = true;
                        formedTrain.ProcessSpeedSettings();
                    }

                    formedTrain.AttachDetails = new AttachInfo(rrtrain);
                    trainList.Add(formedTrain);

                    TrackCircuitPartialPathRoute lastSubpath = rrtrain.TCRoute.TCRouteSubpaths[rrtrain.TCRoute.TCRouteSubpaths.Count - 1];
                    if (atStart) lastSubpath = rrtrain.TCRoute.TCRouteSubpaths[0]; // if runround at start use first subpath

                    bool reverseTrain = CheckFormedReverse(lastSubpath, formedTrain.TCRoute.TCRouteSubpaths[0]);

                    if (atStart)
                    {
                        int? rrtime = disposeDetails.RunRoundTime;
                        DetachInfo detachDetails = new DetachInfo(true, false, false, 0, false, false, false, false, true, false, -1, rrtime, formedTrain.Number, reverseTrain);
                        if (rrtrain.DetachDetails.ContainsKey(-1))
                        {
                            List<DetachInfo> thisDetachList = rrtrain.DetachDetails[-1];
                            thisDetachList.Add(detachDetails);
                        }
                        else
                        {
                            List<DetachInfo> thisDetachList = new List<DetachInfo>();
                            thisDetachList.Add(detachDetails);
                            rrtrain.DetachDetails.Add(-1, thisDetachList);
                        }
                        formedTrain.ActivateTime = rrtime.HasValue ? (rrtime.Value + 30) : 0;
                    }
                    else
                    {
                        DetachInfo detachDetails = new DetachInfo(false, true, false, 0, false, false, false, false, true, false, -1, null, formedTrain.Number, reverseTrain);
                        if (rrtrain.DetachDetails.ContainsKey(-1))
                        {
                            List<DetachInfo> thisDetachList = rrtrain.DetachDetails[-1];
                            thisDetachList.Add(detachDetails);
                        }
                        else
                        {
                            List<DetachInfo> thisDetachList = new List<DetachInfo>();
                            thisDetachList.Add(detachDetails);
                            rrtrain.DetachDetails.Add(-1, thisDetachList);
                        }
                        formedTrain.ActivateTime = 0;
                    }
                }

                return (loadPathNoFailure);
            }

            //================================================================================================//
            /// <summary>
            /// Build consist for stabled train from final train
            /// </summary>
            /// <param name="stabledTrain"></param>
            /// <param name="cars"></param>
            /// <param name="trainRoute"></param>
            private void BuildStabledConsist(ref TTTrain stabledTrain, List<TrainCar> cars, TrackCircuitPartialPathRoute trainRoute, bool reverseTrain)
            {
                int totalreverse = 0;

                // check no. of reversals
                foreach (TrackCircuitReversalInfo reversalInfo in stabledTrain.TCRoute.ReversalInfo)
                {
                    if (reversalInfo.Valid) totalreverse++;
                }

                if (reverseTrain) totalreverse++;

                // copy consist in same or reverse direction
                if ((totalreverse % 2) == 0) // even number, so same direction
                {
                    int carId = 0;
                    foreach (TrainCar car in cars)
                    {
                        car.Train = stabledTrain;
                        car.CarID = $"{stabledTrain.Number:0000}_{carId:000}";
                        carId++;
                        stabledTrain.Cars.Add(car);
                    }
                }
                else
                {
                    int carId = 0;
                    foreach (TrainCar car in cars)
                    {
                        car.Train = stabledTrain;
                        car.CarID = $"{stabledTrain.Number:0000}_{carId:000}";
                        carId++;
                        car.Flipped = !car.Flipped;
                        stabledTrain.Cars.Insert(0, car);
                    }
                }
            }

            //================================================================================================//
            /// <summary>
            /// Check if formed train is reversed of present train
            /// </summary>
            /// <param name="thisTrainRoute"></param>
            /// <param name="formedTrainRoute"></param>
            /// <returns></returns>
            public bool CheckFormedReverse(TrackCircuitPartialPathRoute thisTrainRoute, TrackCircuitPartialPathRoute formedTrainRoute)
            {
                // get matching route sections to check on direction
                int lastElementIndex = thisTrainRoute.Count - 1;
                TrackCircuitRouteElement lastElement = thisTrainRoute[lastElementIndex];

                int firstElementIndex = formedTrainRoute.GetRouteIndex(lastElement.TrackCircuitSection.Index, 0);

                while (firstElementIndex < 0 && lastElementIndex > 0)
                {
                    lastElementIndex--;
                    lastElement = thisTrainRoute[lastElementIndex];
                    firstElementIndex = formedTrainRoute.GetRouteIndex(lastElement.TrackCircuitSection.Index, 0);
                }

                // if no matching sections found leave train without consist
                if (firstElementIndex < 0)
                {
                    return false;
                }

                TrackCircuitRouteElement firstElement = formedTrainRoute[firstElementIndex];

                // reverse required
                return (firstElement.Direction != lastElement.Direction);
            }
        }

        //================================================================================================//
        //================================================================================================//
        /// <summary>
        /// Class to hold stop info
        /// Class is used during extraction process only
        /// </summary>
        private class StopInfo
        {
            public enum SignalHoldType
            {
                None,
                Normal,
                Forced,
            }

            public string StopName;
            public int arrivalTime;
            public int departureTime;
            public DateTime arrivalDT;
            public DateTime departureDT;
            public bool arrdepvalid;
            public SignalHoldType holdState;
            public bool noWaitSignal;
            //          public int passageTime;   // not yet implemented
            //          public bool passvalid;    // not yet implemented
            public List<TTTrainCommands> Commands;

            public TimetableInfo refTTInfo;

            //================================================================================================//
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="name"></param>
            /// <param name="arrTime"></param>
            /// <param name="depTime"></param>
            public StopInfo(string name, string arrTime, string depTime, TimetableInfo ttinfo)
            {
                refTTInfo = ttinfo;
                arrivalTime = -1;
                departureTime = -1;
                Commands = null;

                TimeSpan atime;
                bool validArrTime = false;
                bool validDepTime = false;

                validArrTime = TimeSpan.TryParse(arrTime, out atime);
                if (validArrTime)
                {
                    arrivalTime = Convert.ToInt32(atime.TotalSeconds);
                    arrivalDT = new DateTime(atime.Ticks);
                }

                validDepTime = TimeSpan.TryParse(depTime, out atime);
                if (validDepTime)
                {
                    departureTime = Convert.ToInt32(atime.TotalSeconds);
                    departureDT = new DateTime(atime.Ticks);
                }

                arrdepvalid = (validArrTime || validDepTime);

                StopName = name;
            }

            //================================================================================================//
            /// <summary>
            /// Build station stop info
            /// </summary>
            /// <param name="actTrain"></param>
            /// <param name="TDB"></param>
            /// <param name="signalRef"></param>
            /// <returns>bool (indicating stop is found on route)</returns>
            public bool BuildStopInfo(TTTrain actTrain, int actPlatformID, SignalEnvironment signalRef)
            {
                bool validStop = false;

                // valid stop
                if (arrdepvalid)
                {
                    // check for station flags
                    bool terminal = false;
                    int? actMinStopTime = null;
                    float? keepClearFront = null;
                    float? keepClearRear = null;
                    bool forcePosition = false;
                    bool closeupSignal = false;
                    bool closeup = false;
                    bool restrictPlatformToSignal = false;
                    bool extendPlatformToSignal = false;
                    bool endStop = false;

                    if (Commands != null)
                    {
                        foreach (TTTrainCommands thisCommand in Commands)
                        {
                            switch (thisCommand.CommandToken)
                            {
                                case "terminal":
                                    terminal = true;
                                    break;

                                case "closeupsignal":
                                    closeupSignal = true;
                                    break;

                                case "closeup":
                                    closeup = true;
                                    break;

                                case "restrictplatformtosignal":
                                    restrictPlatformToSignal = true;
                                    break;

                                case "extendplatformtosignal":
                                    extendPlatformToSignal = true;
                                    break;

                                case "keepclear":
                                    if (thisCommand.CommandQualifiers == null || thisCommand.CommandQualifiers.Count <= 0)
                                    {
                                        Trace.TraceInformation("Train {0} : station stop : keepclear command : missing value", actTrain.Name);
                                    }
                                    else
                                    {
                                        bool setfront = false;
                                        bool setrear = false;

                                        foreach (TTTrainCommands.TTTrainComQualifiers thisQualifier in thisCommand.CommandQualifiers)
                                        {
                                            bool getPosition = false;

                                            switch (thisQualifier.QualifierName)
                                            {
                                                case string front when front.Equals("front", StringComparison.OrdinalIgnoreCase):
                                                    if (setrear)
                                                    {
                                                        Trace.TraceInformation($"Train {actTrain.Name} : station stop : keepclear command : spurious value : {thisQualifier.QualifierName}");
                                                    }
                                                    else
                                                    {
                                                        setfront = true;
                                                        getPosition = true;
                                                    }
                                                    break;

                                                case string rear  when rear.Equals("rear", StringComparison.OrdinalIgnoreCase):
                                                    if (setfront)
                                                    {
                                                        Trace.TraceInformation($"Train {actTrain.Name} : station stop : keepclear command : spurious value : {thisQualifier.QualifierName}");
                                                    }
                                                    else
                                                    {
                                                        setrear = true;
                                                        getPosition = true;
                                                    }
                                                    break;

                                                case string force when force.Equals("force", StringComparison.OrdinalIgnoreCase):
                                                    forcePosition = true;
                                                    break;

                                                default:
                                                    Trace.TraceInformation("Train {0} : station stop : keepclear command : unknown value", actTrain.Name);
                                                    break;
                                            }

                                            if (getPosition)
                                            {
                                                if (thisQualifier.QualifierValues == null || thisQualifier.QualifierValues.Count <= 0)
                                                {
                                                    Trace.TraceInformation("Train {0} : station stop : keepclear command : missing value", actTrain.Name);
                                                }
                                                else
                                                {
                                                    if (float.TryParse(thisCommand.CommandQualifiers[0].QualifierValues[0], out float clearValue))
                                                    {
                                                        if (setfront)
                                                        {
                                                            keepClearFront = clearValue;
                                                        }
                                                        else if (setrear)
                                                        {
                                                            keepClearRear = clearValue;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Trace.TraceInformation("Train {0} : station stop : keepclear command : invalid value", actTrain.Name);
                                                    }
                                                }
                                            }
                                        }

                                        if (!setfront && !setrear)
                                        {
                                            Trace.TraceInformation("Train {0} : station stop : keepclear command : missing position definition", actTrain.Name);
                                        }
                                    }
                                    break;

                                // train terminates at station
                                case "endstop":
                                    endStop = true;
                                    break;

                                // required minimal stop time
                                case "stoptime":
                                    if (thisCommand.CommandValues?.Count > 0)
                                    {
                                        if (int.TryParse(thisCommand.CommandValues[0], out int minStopTime))
                                        {
                                            actMinStopTime = minStopTime;
                                        }
                                        else
                                        {
                                            Trace.TraceInformation("Train {0} : station stop : invalid value for stop time", actTrain.Name);
                                        }
                                    }
                                    else
                                    {
                                        Trace.TraceInformation("Train {0} : station stop : missing value for station stop time", actTrain.Name);
                                    }
                                    break;

                                // other commands processed in station stop handling
                                default:
                                    break;
                            }
                        }
                    }

                    // create station stop info
                    validStop = actTrain.CreateStationStop(actPlatformID, arrivalTime, departureTime, AITrain.clearingDistanceM,
                        AITrain.minStopDistanceM, terminal, actMinStopTime,keepClearFront, keepClearRear, forcePosition, closeupSignal, closeup, restrictPlatformToSignal, extendPlatformToSignal, endStop);

                    // override holdstate using stop info - but only if exit signal is defined

                    int exitSignal = actTrain.StationStops[actTrain.StationStops.Count - 1].ExitSignal;
                    bool holdSignal = holdState != SignalHoldType.None && (exitSignal >= 0);
                    actTrain.StationStops[actTrain.StationStops.Count - 1].HoldSignal = holdSignal;

                    // override nosignalwait using stop info

                    actTrain.StationStops[actTrain.StationStops.Count - 1].NoWaitSignal = noWaitSignal;

                    // process additional commands
                    if (Commands != null && validStop)
                    {
                        int sectionIndex = actTrain.StationStops[actTrain.StationStops.Count - 1].TrackCircuitSectionIndex;
                        int subrouteIndex = actTrain.StationStops[actTrain.StationStops.Count - 1].SubrouteIndex;

                        foreach (TTTrainCommands thisCommand in Commands)
                        {
                            actTrain.ProcessTimetableStopCommands(thisCommand, subrouteIndex, sectionIndex, (actTrain.StationStops.Count - 1), actPlatformID, refTTInfo);
                        }

                        holdSignal = actTrain.StationStops[actTrain.StationStops.Count - 1].HoldSignal;
                    }

                    // check holdsignal list

                    if (holdSignal)
                    {
                        if (!actTrain.HoldingSignals.Contains(exitSignal))
                        {
                            actTrain.HoldingSignals.Add(exitSignal);
                        }
                    }
                    else
                    {
                        if (actTrain.HoldingSignals.Contains(exitSignal))
                        {
                            actTrain.HoldingSignals.Remove(exitSignal);
                        }
                    }
                }

                // stop used to define command only - find related section in route
                else if (Commands != null)
                {
                    // get platform details
                    int platformIndex;
                    int actSubpath = 0;

                    if (signalRef.PlatformXRefList.TryGetValue(actPlatformID, out platformIndex))
                    {
                        PlatformDetails thisPlatform = signalRef.PlatformDetailsList[platformIndex];
                        int sectionIndex = thisPlatform.TCSectionIndex[0];
                        int routeIndex = actTrain.TCRoute.TCRouteSubpaths[actSubpath].GetRouteIndex(sectionIndex, 0);

                        // if first section not found in route, try last

                        if (routeIndex < 0)
                        {
                            sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                            routeIndex = actTrain.TCRoute.TCRouteSubpaths[actSubpath].GetRouteIndex(sectionIndex, 0);
                        }

                        // if neither section found - try next subroute - keep trying till found or out of subroutes

                        while (routeIndex < 0 && actSubpath < (actTrain.TCRoute.TCRouteSubpaths.Count - 1))
                        {
                            actSubpath++;
                            TrackCircuitPartialPathRoute thisRoute = actTrain.TCRoute.TCRouteSubpaths[actSubpath];
                            routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);

                            // if first section not found in route, try last

                            if (routeIndex < 0)
                            {
                                sectionIndex = thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1];
                                routeIndex = thisRoute.GetRouteIndex(sectionIndex, 0);
                            }
                        }

                        // if section found : process stop
                        if (routeIndex >= 0)
                        {
                            validStop = true;

                            sectionIndex = actTrain.TCRoute.TCRouteSubpaths[actSubpath][routeIndex].TrackCircuitSection.Index;
                            foreach (TTTrainCommands thisCommand in Commands)
                            {
                                actTrain.ProcessTimetableStopCommands(thisCommand, actSubpath, sectionIndex, -1, actPlatformID, refTTInfo);
                            }
                        }
                    }
                }

                return (validStop);
            } // end buildStopInfo

        } // end class stopInfo

        //================================================================================================//
        //================================================================================================//
        /// <summary>
        /// Class to hold station information
        /// Class is used during extraction process only
        /// </summary>
        private class StationInfo
        {
            public enum HoldInfo
            {
                Hold,
                NoHold,
                ForceHold,
                HoldConditional_DwellTime,
            }

            public string StationName;       // Station Name
            public HoldInfo HoldState;       // Hold State
            public bool NoWaitSignal;        // Train will run up to signal and not wait in platform
            public bool ForceWaitSignal;     // force to wait for signal even if not exit signal for platform
            public int? actMinStopTime;      // Min Dwell time for Conditional Holdstate
            public bool IsTerminal;          // Station is terminal
            public bool CloseupSignal;       // Train may close up to signal
            public bool RestrictPlatformToSignal;   // restrict platform end to signal position
            public bool ExtendPlatformToSignal;     // extend platform end to next signal position

            //================================================================================================//
            /// <summary>
            /// Constructor from String
            /// </summary>
            /// <param name="stationName"></param>
            public StationInfo(string stationString)
            {
                // default settings
                HoldState = HoldInfo.NoHold;
                NoWaitSignal = false;
                ForceWaitSignal = false;
                actMinStopTime = null;
                IsTerminal = false;
                CloseupSignal = false;
                RestrictPlatformToSignal = false;
                ExtendPlatformToSignal = false;

                // if string contains commands : split name and commands
                if (stationString.Contains('$', StringComparison.OrdinalIgnoreCase))
                {
                    string[] stationDetails = stationString.Split('$');
                    StationName = stationDetails[0].ToLowerInvariant().Trim();
                    ProcessStationCommands(stationDetails);
                }
                else
                // string contains name only
                {
                    StationName = stationString.ToLowerInvariant().Trim();
                }
            }

            //================================================================================================//
            /// <summary>
            /// Process Station Commands : add command info to stationInfo class
            /// </summary>
            /// <param name="commands"></param>
            public void ProcessStationCommands(string[] commands)
            {
                // start at 1 as 0 is station name
                for (int iString = 1; iString <= commands.Length - 1; iString++)
                {
                    string commandFull = commands[iString];
                    TTTrainCommands thisCommand = new TTTrainCommands(commandFull);

                    switch (thisCommand.CommandToken)
                    {
                        case "hold":
                            HoldState = HoldInfo.Hold;
                            break;

                        case "nohold":
                            HoldState = HoldInfo.NoHold;
                            break;

                        case "forcehold":
                            HoldState = HoldInfo.ForceHold;
                            break;

                        case "forcewait":
                            ForceWaitSignal = true;
                            break;

                        case "nowaitsignal":
                            NoWaitSignal = true;
                            break;

                        case "terminal":
                            IsTerminal = true;
                            break;

                        case "closeupsignal":
                            CloseupSignal = true;
                            break;

                        case "extendplatformtosignal":
                            ExtendPlatformToSignal = true;
                            break;

                        case "restrictplatformtosignal":
                            RestrictPlatformToSignal = true;
                            break;

                        // required minimal stop time
                        case "stoptime":
                            if (thisCommand.CommandValues?.Count > 0)
                            {
                                if (int.TryParse(thisCommand.CommandValues[0], out int minStopTime))
                                {
                                    actMinStopTime = minStopTime;
                                }
                                else
                                {
                                    Trace.TraceInformation("Station stop {0} : invalid value for stop time", commands[0]);
                                }
                            }
                            else
                            {
                                Trace.TraceInformation("Station stop {0} : missing value for station stop time", commands[0]);
                            }
                            break;


                        // other commands not yet implemented
                        default:
                            break;
                    }
                }
            }
        }

        //================================================================================================//
        //================================================================================================//
        /// <summary>
        /// Class to hold dispose info
        /// Class is used during extraction process only
        /// </summary>
        private class DisposeInfo
        {
            public enum DisposeType
            {
                Forms,
                Triggers,
                Static,
                Stable,
                Pool,
                None,
            }

            public string FormedTrain;
            public TTTrain.FormCommand FormType;
            public bool FormTrain;
            public bool FormStatic;
            public bool SetStop;
            public bool FormsAtStation;
            public bool Closeup;

            public struct StableDetails
            {
                public string Stable_outpath;
                public int? Stable_outtime;
                public string Stable_inpath;
                public int? Stable_intime;
                public string Stable_name;
            }

            public bool Stable;
            public StableDetails StableInfo;

            public bool Pool;
            public string PoolName;
            public string PoolExitDirection;

            public float? DisposeSpeed;
            public bool RunRound;
            public string RunRoundPath;
            public int? RunRoundTime;

            public enum RunRoundPosition
            {
                inposition,
                stableposition,
                outposition,
            }

            public RunRoundPosition RunRoundPos;

            public bool CallOn;

            //================================================================================================//
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="typeOfDispose></param>
            /// <param name="trainCommands"></param>
            /// <param name="formType"></param>
            public DisposeInfo(DisposeType typeOfDispose, TTTrainCommands trainCommands, TTTrain.FormCommand formType, string trainName)
            {
                FormTrain = false;
                FormStatic = false;
                Closeup = false;
                Stable = false;
                Pool = false;
                RunRound = false;
                SetStop = false;
                FormsAtStation = false;
                DisposeSpeed = null;

                switch (typeOfDispose)
                {
                    case DisposeType.Forms:
                    case DisposeType.Triggers:
                        FormedTrain = trainCommands.CommandValues[0];
                        FormType = formType;
                        FormTrain = true;

                        if (trainCommands.CommandQualifiers != null && (formType == TTTrain.FormCommand.TerminationFormed || formType == TTTrain.FormCommand.TerminationTriggered))
                        {
                            foreach (TTTrainCommands.TTTrainComQualifiers formedTrainQualifiers in trainCommands.CommandQualifiers)
                            {
                                if (string.Equals(formedTrainQualifiers.QualifierName, "runround", StringComparison.OrdinalIgnoreCase))
                                {
                                    RunRound = true;
                                    RunRoundPath = formedTrainQualifiers.QualifierValues[0];
                                    RunRoundTime = -1;
                                }

                                if (string.Equals(formedTrainQualifiers.QualifierName, "rrtime", StringComparison.OrdinalIgnoreCase))
                                {
                                    TimeSpan RRSpan;
                                    TimeSpan.TryParse(formedTrainQualifiers.QualifierValues[0], out RRSpan);
                                    RunRoundTime = Convert.ToInt32(RRSpan.TotalSeconds);
                                }

                                if (string.Equals(formedTrainQualifiers.QualifierName, "setstop", StringComparison.OrdinalIgnoreCase))
                                {
                                    SetStop = true;
                                }

                                if (string.Equals(formedTrainQualifiers.QualifierName, "atstation", StringComparison.OrdinalIgnoreCase))
                                {
                                    FormsAtStation = true;
                                }

                                if (string.Equals(formedTrainQualifiers.QualifierName, "closeup", StringComparison.OrdinalIgnoreCase))
                                {
                                    Closeup = true;
                                }

                                if (string.Equals(formedTrainQualifiers.QualifierName, "speed", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (float.TryParse(formedTrainQualifiers.QualifierValues[0], out float disposeSpeed))
                                    {
                                        DisposeSpeed = disposeSpeed;
                                    }
                                    else
                                    {
                                        Trace.TraceInformation("Train : {0} : invalid value for runround speed : {1} \n", trainName, formedTrainQualifiers.QualifierValues[0]);
                                    }
                                }
                            }
                        }

                        // reset speed if runround is not set
                        if (!RunRound && DisposeSpeed != null)
                        {
                            DisposeSpeed = null;
                        }
                        break;
                    // end of Forms and Triggers

                    case DisposeType.Static:
                        List<TTTrainCommands.TTTrainComQualifiers> staticQualifiers = trainCommands.CommandQualifiers;
                        FormStatic = true;
                        FormType = TTTrain.FormCommand.None;

                        if (staticQualifiers != null)
                        {
                            foreach (TTTrainCommands.TTTrainComQualifiers staticQualifier in staticQualifiers)
                            {
                                switch (staticQualifier.QualifierName)
                                {
                                    case "closeup":
                                        Closeup = true;
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                        break;
                    // end of static

                    case DisposeType.Stable:
                        Stable = true;
                        RunRound = false;
                        SetStop = true;
                        StableInfo.Stable_name = String.Empty;

                        foreach (TTTrainCommands.TTTrainComQualifiers stableQualifier in trainCommands.CommandQualifiers)
                        {
                            switch (stableQualifier.QualifierName)
                            {
                                case "out_path":
                                    StableInfo.Stable_outpath = stableQualifier.QualifierValues[0];
                                    break;

                                case "out_time":
                                    TimeSpan outtime;
                                    TimeSpan.TryParse(stableQualifier.QualifierValues[0], out outtime);
                                    StableInfo.Stable_outtime = Convert.ToInt32(outtime.TotalSeconds);
                                    break;

                                case "in_path":
                                    StableInfo.Stable_inpath = stableQualifier.QualifierValues[0];
                                    break;

                                case "in_time":
                                    TimeSpan intime;
                                    TimeSpan.TryParse(stableQualifier.QualifierValues[0], out intime);
                                    StableInfo.Stable_intime = Convert.ToInt32(intime.TotalSeconds);
                                    break;

                                case "forms":
                                    FormTrain = true;
                                    FormedTrain = stableQualifier.QualifierValues[0];
                                    FormStatic = false;
                                    FormType = TTTrain.FormCommand.TerminationFormed;
                                    break;

                                case "triggers":
                                    FormTrain = true;
                                    FormedTrain = stableQualifier.QualifierValues[0];
                                    FormStatic = false;
                                    FormType = TTTrain.FormCommand.TerminationTriggered;
                                    break;

                                case "static":
                                    FormTrain = false;
                                    FormStatic = true;
                                    FormType = TTTrain.FormCommand.None;
                                    break;

                                case "closeup":
                                    Closeup = true;
                                    break;

                                case "runround":
                                    RunRound = true;
                                    RunRoundPath = stableQualifier.QualifierValues[0];
                                    RunRoundTime = -1;
                                    RunRoundPos = RunRoundPosition.stableposition;
                                    break;

                                case "rrtime":
                                    TimeSpan RRSpan;
                                    TimeSpan.TryParse(stableQualifier.QualifierValues[0], out RRSpan);
                                    RunRoundTime = Convert.ToInt32(RRSpan.TotalSeconds);
                                    break;

                                case "callon":
                                    CallOn = true;
                                    break;

                                case "rrpos":
                                    switch (stableQualifier.QualifierValues[0])
                                    {
                                        case "in":
                                            RunRoundPos = RunRoundPosition.inposition;
                                            break;

                                        case "out":
                                            RunRoundPos = RunRoundPosition.outposition;
                                            break;

                                        case "stable":
                                            RunRoundPos = RunRoundPosition.stableposition;
                                            break;

                                        default:
                                            break;
                                    }
                                    break;

                                case "speed":
                                    if (float.TryParse(stableQualifier.QualifierValues[0], out float disposeSpeed))
                                    {
                                        DisposeSpeed = disposeSpeed;
                                    }
                                    else
                                    {
                                        Trace.TraceInformation("Train : {0} : invalid value for stable speed : {1} \n", trainName, stableQualifier.QualifierValues[0]);
                                    }
                                    break;

                                case "name":
                                    StableInfo.Stable_name = stableQualifier.QualifierValues[0];
                                    break;

                                default:
                                    break;
                            }
                        }
                        break;
                    // end of stable

                    // process pool
                    case DisposeType.Pool:
                        Pool = true;
                        FormType = formType;
                        PoolName = trainCommands.CommandValues[0].ToLowerInvariant().Trim();
                        PoolExitDirection = string.Empty;

                        if (trainCommands.CommandQualifiers != null)
                        {
                            foreach (TTTrainCommands.TTTrainComQualifiers poolQualifiers in trainCommands.CommandQualifiers)
                            {
                                switch (poolQualifiers.QualifierName)
                                {
                                    case "direction":
                                        PoolExitDirection = poolQualifiers.QualifierValues[0];
                                        break;

                                    default:
                                        Trace.TraceInformation("Train : {0} : invalid qualifier for dispose to pool : {1} : {2}\n", trainName, PoolName, poolQualifiers.QualifierName);
                                        break;
                                }
                            }
                        }

                        break;

                    // end of pool

                    // unknow type
                    default:
                        Trace.TraceInformation("Train : {0} : invalid qualifier for dispose {1}\n", trainName, typeOfDispose);
                        break;
                }
            }

        }// end class DisposeInfo

    } // end class TimetableInfo

    //================================================================================================//
    //================================================================================================//
    /// <summary>
    /// Class to hold all additional commands in unprocessed form
    /// </summary>
    /// 
    public class TTTrainCommands
    {
        public string CommandToken;
        public List<string> CommandValues;
        public List<TTTrainComQualifiers> CommandQualifiers;

        //================================================================================================//
        /// <summary>
        /// Constructor from string (excludes leading '$')
        /// </summary>
        /// <param name="CommandString"></param>
        public TTTrainCommands(string CommandString)
        {
            string workString = CommandString.ToLowerInvariant().Trim();
            string commandValueString = string.Empty;

            string restString;
            // check for qualifiers

            if (workString.Contains('/', StringComparison.OrdinalIgnoreCase))
            {
                string[] tempStrings = workString.Split('/');  // first string is token plus value, rest is qualifiers
                restString = tempStrings[0];

                if (CommandQualifiers == null) CommandQualifiers = new List<TTTrainComQualifiers>();

                for (int iQual = 1; iQual < tempStrings.Length; iQual++)
                {
                    CommandQualifiers.Add(new TTTrainComQualifiers(tempStrings[iQual]));
                }
            }
            else
            {
                restString = workString;
            }

            // extract command token and values
            if (restString.Contains('=', StringComparison.OrdinalIgnoreCase))
            {
                int splitPosition = restString.IndexOf('=', StringComparison.OrdinalIgnoreCase);
                CommandToken = restString.Substring(0, splitPosition);
                commandValueString = restString.Substring(splitPosition + 1);
            }
            else
            {
                CommandToken = restString.Trim();
            }

            // process values
            // split on "+" sign (multiple values)
            string[] valueStrings = null;

            if (String.IsNullOrEmpty(commandValueString))
            {
                CommandValues = null;
            }
            else
            {
                CommandValues = new List<string>();

                if (commandValueString.Contains('+', StringComparison.OrdinalIgnoreCase))
                {
                    valueStrings = commandValueString.Split('+');
                }
                else
                {
                    valueStrings = new string[1] { commandValueString };
                }

                foreach (string thisValue in valueStrings)
                {
                    CommandValues.Add(thisValue.Trim());
                }
            }
        }

        //================================================================================================//
        //================================================================================================//
        /// <summary>
        /// Class for command qualifiers
        /// </summary>
        public class TTTrainComQualifiers
        {
            public string QualifierName;
            public List<string> QualifierValues = new List<string>();

            //================================================================================================//
            /// <summary>
            /// Constructor (string is without leading '/')
            /// </summary>
            /// <param name="qualifier"></param>
            public TTTrainComQualifiers(string qualifier)
            {
                string[] qualparts = null;
                if (qualifier.Contains('=', StringComparison.OrdinalIgnoreCase))
                {
                    qualparts = qualifier.Split('=');
                }
                else
                {
                    qualparts = new string[1] { qualifier };
                }

                QualifierName = qualparts[0].Trim();

                string[] valueStrings;

                if (qualparts.Length > 1)
                {
                    if (qualparts[1].Contains('+', StringComparison.OrdinalIgnoreCase))
                    {
                        valueStrings = qualparts[1].Trim().Split('+');
                    }
                    else
                    {
                        valueStrings = new string[1] { qualparts[1].Trim() };
                    }

                    foreach (string thisValue in valueStrings)
                    {
                        QualifierValues.Add(thisValue.Trim());
                    }
                }
            }

        } // end class TTTrainComQualifiers
    } // end class TTTrainCommands
}
