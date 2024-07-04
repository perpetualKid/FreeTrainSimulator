// COPYRIGHT 2014 by the Open Rails project.
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
// #define DEBUG_POOLINFO
// #define DEBUG_TURNTABLEINFO
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Parsers;
using Orts.Models.State;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Track;
using Orts.Simulation.World;

namespace Orts.Simulation.Timetables
{

    //================================================================================================//
    //================================================================================================//
    /// <summary>
    /// Class TimetableTurntablePool
    /// Class holding all details for turntables in timetable mode
    /// Child of TimetablePool
    /// </summary>
    public class TimetableTurntablePool : TimetablePool
    {

        public class AccessPathDetails : ISaveStateApi<AccessPathDetailSaveState>
        {
            public TrackCircuitPartialPathRoute AccessPath { get; set; }           // actual access path
            public Traveller AccessTraveller { get; set; }                 // traveler based on access path
            public string AccessPathName { get; set; }                     // access path name
            public int TableVectorIndex { get; set; }                      // index in VectorList of tracknode which is the table
            public int TableExitIndex { get; set; }                        // index in table exit list for this exit
            public float TableApproachOffset { get; set; }                 // offset of holding point in front of turntable (in Inward direction)
            public float TableMiddleEntry { get; set; }                    // offset of middle of table when approaching table
            public float TableMiddleExit { get; set; }                     // offset of middle of table when exiting

            public async ValueTask Restore(AccessPathDetailSaveState saveState)
            {
                ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
                AccessPath = new TrackCircuitPartialPathRoute();
                await AccessPath.Restore(saveState.AccessPath).ConfigureAwait(false);

                AccessTraveller = new Traveller(false);
                await AccessTraveller.Restore(saveState.AccessTraveller).ConfigureAwait(false);

                AccessPathName = saveState.AccessPathName;
                TableExitIndex = saveState.TableExitIndex;
                TableVectorIndex = saveState.TableVectorIndex;
                TableApproachOffset = saveState.TableApproachOffset;
                TableMiddleEntry = saveState.TableMiddleEntry;
                TableMiddleExit = saveState.TableMiddleExit;
            }

            public async ValueTask<AccessPathDetailSaveState> Snapshot()
            {
                return new AccessPathDetailSaveState()
                {
                    AccessPath = await AccessPath.Snapshot().ConfigureAwait(false),
                    AccessTraveller = await AccessTraveller.Snapshot().ConfigureAwait(false),
                    AccessPathName = AccessPathName,
                    TableVectorIndex = TableVectorIndex,
                    TableExitIndex = TableExitIndex,
                    TableApproachOffset = TableApproachOffset,
                    TableMiddleEntry = TableMiddleEntry,
                    TableMiddleExit = TableMiddleExit,
                };
            }
        }

        public class TurntableDetails
        {
            public List<AccessPathDetails> AccessPaths;       // access paths details defined for turntable location
            public int TurntableIndex;                        // index for turntable in list of moving tables
            public float TurntableApproachClearanceM;         // required clearance from front of turntable on approach
            public float TurntableReleaseClearanceM;          // required clearance from front of turntabe for release
            public float? TurntableSpeedMpS;                  // set speed for turntable access
            public int? FrameRate;                            // frame rate for turntable movement
        }

        public TurntableDetails AdditionalTurntableDetails { get; } = new TurntableDetails();
        private static float defaultTurntableApproachClearanceM = 10.0f;  // default approach clearance
        private static float defaultTurntableReleaseClearanceM = 5.0f;    // default release clearance

        public TimetableTurntablePool() 
        { 
        }

        //================================================================================================//
        /// <summary>
        /// constructor for new TimetableTurntablePool
        /// creates TimetableTurntablePool from files .turntable-or
        /// </summary>
        /// <param name="fileContents"></param>
        /// <param name="lineindex"></param>
        /// <param name="simulatorref"></param>
        public TimetableTurntablePool(TimetableReader fileContents, ref int lineindex)
        {

            bool validpool = true;
            bool newName = false;
            bool firstName = false;
            TurnTable thisTurntable = null;

            string Worldfile = string.Empty;
            int UiD = -1;

            forceCreation = Simulator.Instance.Settings.TTCreateTrainOnPoolUnderflow;

            AdditionalTurntableDetails.TurntableApproachClearanceM = defaultTurntableApproachClearanceM;
            AdditionalTurntableDetails.TurntableReleaseClearanceM = defaultTurntableReleaseClearanceM;
            AdditionalTurntableDetails.TurntableSpeedMpS = null;
            AdditionalTurntableDetails.FrameRate = null;

            // loop through definitions
            while (lineindex < fileContents.Strings.Count && !newName)
            {
                string[] inputLine = fileContents.Strings[lineindex];

                // switch through definitions
                switch (inputLine[0].ToLower().Trim())
                {
                    // comment : do not process
                    case "#comment":
                        lineindex++;
                        break;

                    // name : set as name
                    case "#name":
                        newName = firstName;
                        if (!firstName)
                        {
                            lineindex++;
                            firstName = true;
                            PoolName = inputLine[1].ToLower().Trim();
                        }
                        break;

                    // worldfile : read worldfile details
                    case "#worldfile":
                        if (String.IsNullOrEmpty(PoolName))
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : missing pool name \n");
                            validpool = false;
                            lineindex++;
                        }
                        else
                        {
                            Worldfile = inputLine[1].ToLower().Trim();
                            lineindex++;
                        }
                        break;

                    // UiD reference
                    case "#uid":
                        if (String.IsNullOrEmpty(PoolName))
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : missing pool name \n");
                            validpool = false;
                            lineindex++;
                        }
                        else
                        {
                            try
                            {
                                UiD = Convert.ToInt32(inputLine[1].Trim());
                            }
                            catch
                            {
                                Trace.TraceInformation("Pool : " + fileContents.FilePath + " : invalid value for UiD \n");
                            }
                            lineindex++;
                        }
                        break;

                    // access path
                    case "#access":
                        if (String.IsNullOrEmpty(PoolName))
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : missing pool name \n");
                            validpool = false;
                            lineindex++;
                        }
                        else
                        {
                            string accessPath = inputLine[1].ToLower().Trim();
                            bool pathValid = true;
                            TimetableInfo TTInfo = new TimetableInfo();
                            AIPath newPath = TTInfo.LoadPath(accessPath, out pathValid);

                            if (pathValid)
                            {
                                TrackCircuitRoutePath fullRoute = new TrackCircuitRoutePath(newPath, (TrackDirection)(-2), 1, -1);
                                // if first element is end of track, remove it from path (path is defined outbound)
                                TrackCircuitPartialPathRoute usedRoute = fullRoute.TCRouteSubpaths[0];
                                if (TrackCircuitSection.TrackCircuitList[usedRoute.First().TrackCircuitSection.Index].CircuitType == TrackCircuitType.EndOfTrack)
                                {
                                    usedRoute.RemoveAt(0);
                                }
                                // if last element is send of track, remove it from path (if path has no junction it may be in reverse direction)
                                if (TrackCircuitSection.TrackCircuitList[usedRoute.Last().TrackCircuitSection.Index].CircuitType == TrackCircuitType.EndOfTrack)
                                {
                                    usedRoute.RemoveAt(usedRoute.Count - 1);
                                }

                                // create path list if required
                                if (AdditionalTurntableDetails.AccessPaths == null)
                                {
                                    AdditionalTurntableDetails.AccessPaths = new List<AccessPathDetails>();
                                }

                                AccessPathDetails thisAccess = new AccessPathDetails();
                                thisAccess.AccessPath = new TrackCircuitPartialPathRoute(usedRoute);
                                thisAccess.AccessTraveller = new Traveller(newPath.FirstNode.Location, newPath.FirstNode.NextMainNode.Location);
                                thisAccess.AccessPathName = accessPath;
                                AdditionalTurntableDetails.AccessPaths.Add(thisAccess);
                            }
                            else
                            {
                                Trace.TraceInformation("Pool : " + fileContents.FilePath + " : access path not found : " + accessPath);
                                validpool = false;
                            }
                            lineindex++;
                        }
                        break;

                    // case approach clearance : read clearance value
                    case "#approachclearance":
                        try
                        {
                            AdditionalTurntableDetails.TurntableApproachClearanceM = Convert.ToSingle(inputLine[1].Trim());
                        }
                        catch
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : invalid value for approach clearance : " + inputLine[1].Trim());
                        }
                        lineindex++;
                        break;

                    // case release clearance : read clearance value
                    case "#releaseclearance":
                        try
                        {
                            AdditionalTurntableDetails.TurntableReleaseClearanceM = Convert.ToSingle(inputLine[1].Trim());
                        }
                        catch
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : invalid value for release clearance : " + inputLine[1].Trim());
                        }
                        lineindex++;
                        break;

                    // case turntable speed : read speed (either speedmph or speedkph)
                    case "#speedmph":
                        try
                        {
                            AdditionalTurntableDetails.TurntableSpeedMpS = (float)Speed.MeterPerSecond.FromMpH(Convert.ToSingle(inputLine[1].Trim()));
                        }
                        catch
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : invalid value for speed (mph) : " + inputLine[1].Trim());
                        }
                        lineindex++;
                        break;

                    case "#speedkph":
                        try
                        {
                            AdditionalTurntableDetails.TurntableSpeedMpS = (float)Speed.MeterPerSecond.FromKpH(Convert.ToSingle(inputLine[1].Trim()));
                        }
                        catch
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : invalid value for speed (kph) : " + inputLine[1].Trim());
                        }
                        lineindex++;
                        break;

                    // table frame rate
                    case "#framerate":
                        try
                        {
                            AdditionalTurntableDetails.FrameRate = Convert.ToInt32(inputLine[1].Trim());
                        }
                        catch
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : invalid value for frame rate : " + inputLine[1].Trim());
                        }
                        lineindex++;
                        break;

                    // storage : read path, add to path list
                    case "#storage":
                        if (String.IsNullOrEmpty(PoolName))
                        {
                            Trace.TraceInformation("Pool : " + fileContents.FilePath + " : missing pool name \n");
                            validpool = false;
                            lineindex++;
                        }
                        else
                        {
                            bool validStorage = true;
                            PoolDetails thisPool = ExtractStorage(fileContents, ref lineindex, out validStorage, false);
                            if (validStorage)
                            {
                                StoragePool.Add(thisPool);
                            }
                            else
                            {
                                validpool = false;
                            }
                        }
                        break;

                    default:
                        Trace.TraceInformation("Pool : " + fileContents.FilePath + " : line : " + (lineindex - 1) + " : unexpected line defitinion : " + inputLine[0] + "\n");
                        lineindex++;
                        break;
                }
            }

            // find turntable reference
            AdditionalTurntableDetails.TurntableIndex = FindTurntable(Worldfile, UiD);

            if (AdditionalTurntableDetails.TurntableIndex < 0)
            {
                validpool = false;
                Trace.TraceInformation("Pool : " + fileContents.FilePath + " not included due to errors in pool definition");
            }

            // check validity of access and storage paths
            // paths must start at turntable
            if (validpool)
            {
                thisTurntable = Simulator.Instance.MovingTables[AdditionalTurntableDetails.TurntableIndex] as TurnTable;

                // check validity for all access paths
                for (int iPath = 0; iPath < AdditionalTurntableDetails.AccessPaths.Count; iPath++)
                {
                    int vectorIndex = CheckTurntablePath(AdditionalTurntableDetails.AccessPaths[iPath].AccessPath, thisTurntable.TrackShapeIndex);

                    if (vectorIndex < 0)
                    {
                        Trace.TraceInformation("Pool : " + PoolName + " : access path " + AdditionalTurntableDetails.AccessPaths[iPath].AccessPathName + " does not link to turntable");
                        validpool = false;
                    }
                    else
                    {
                        AccessPathDetails thisPath = AdditionalTurntableDetails.AccessPaths[iPath];
                        thisPath.TableVectorIndex = vectorIndex;
                        AdditionalTurntableDetails.AccessPaths[iPath] = thisPath;
                    }
                }

                // check validity for all storage paths
                for (int iPath = 0; iPath < StoragePool.Count; iPath++)
                {
                    int vectorIndex = CheckTurntablePath(StoragePool[iPath].StoragePath, thisTurntable.TrackShapeIndex);

                    if (vectorIndex < 0)
                    {
                        Trace.TraceInformation("Pool : " + PoolName + " : storage path " + StoragePool[iPath].StorageName + " does not link to turntable");
                        validpool = false;
                    }
                    else
                    {
                        PoolDetails thisPool = StoragePool[iPath];
                        thisPool.TableVectorIndex = vectorIndex;
                        StoragePool[iPath] = thisPool;
                    }
                }
            }

            // calculate offsets for paths

            if (validpool)
            {
                // access path : hold offset, offset for start of turntable, offset for middle of turntable
                for (int ipath = 0; ipath < AdditionalTurntableDetails.AccessPaths.Count; ipath++)
                {
                    CalculateAccessOffsets(ipath, thisTurntable);
                }

                // storage path : actual length, offset for start of turntable, offset for middle of turntable
                for (int ipath = 0; ipath < StoragePool.Count; ipath++)
                {
                    CalculateStorageOffsets(ipath, thisTurntable);
                }
            }

            // reset poolname if not valid
            if (!validpool)
            {
                PoolName = String.Empty;
            }
        }

        public override async ValueTask<TimetablePoolSaveState> Snapshot()
        {
            TimetablePoolSaveState result = await base.Snapshot().ConfigureAwait(false);
            result.PoolType = TimetablePoolType.TimetableTurntablePool;
            result.AccessDetails = new Collection<AccessPathDetailSaveState>(await Task.WhenAll(AdditionalTurntableDetails.AccessPaths.Select(async accessPath => await accessPath.Snapshot().ConfigureAwait(false))).ConfigureAwait(false));
            result.TurntableIndex = AdditionalTurntableDetails.TurntableIndex;
            result.TurntableApproachClearance = AdditionalTurntableDetails.TurntableApproachClearanceM;
            result.TurntableReleaseClearance = AdditionalTurntableDetails.TurntableReleaseClearanceM;
            result.TurntableSpeed = AdditionalTurntableDetails.TurntableSpeedMpS;
            result.TurntableFrameRate = AdditionalTurntableDetails.FrameRate;
            return result;
        }

        public override async ValueTask Restore([NotNull] TimetablePoolSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            AdditionalTurntableDetails.AccessPaths = (await Task.WhenAll(saveState.AccessDetails.Select(async accessDetailState =>
            {
                AccessPathDetails accessDetails = new AccessPathDetails();
                await accessDetails.Restore(accessDetailState).ConfigureAwait(false);
                return accessDetails;
            })).ConfigureAwait(false)).ToList();
            AdditionalTurntableDetails.TurntableIndex = saveState.TurntableIndex;
            AdditionalTurntableDetails.TurntableApproachClearanceM = saveState.TurntableApproachClearance;
            AdditionalTurntableDetails.TurntableReleaseClearanceM = saveState.TurntableReleaseClearance;
            AdditionalTurntableDetails.TurntableSpeedMpS = saveState.TurntableSpeed;
            AdditionalTurntableDetails.FrameRate = saveState.TurntableFrameRate;
        }

        //================================================================================================//
        /// <summary>
        /// Check if path starts at turntable
        /// </summary>

        private int CheckTurntablePath(TrackCircuitPartialPathRoute thisPath, int turntableTrackShape)
        {
            // check if turntable track section is in path - must be in first element (path must start at turntable end)
            int vectorIndex = -1;
            TrackCircuitSection thisSection = thisPath[0].TrackCircuitSection;
            TrackVectorNode thisTDBsection = RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[thisSection.OriginalIndex];

            for (int iVector = 0; iVector < thisTDBsection.TrackVectorSections.Length; iVector++)
            {
                TrackVectorSection thisVector = thisTDBsection.TrackVectorSections[iVector];
                if (thisVector.ShapeIndex == turntableTrackShape)
                {
                    vectorIndex = iVector;
                    break;
                }
            }

            return (vectorIndex);
        }

        //================================================================================================//
        /// <summary>
        /// FindTurntable : find reference to turntable as defined in turntable.dat using worldfile and uid references
        /// </summary>
        private int FindTurntable(string worldfile, int uid)
        {
            if (string.IsNullOrEmpty(worldfile))
                return -1;
            // search through all moving tables
            for (int i = 0; i < Simulator.Instance.MovingTables.Count; i++)
            {
                if (worldfile.Equals(Simulator.Instance.MovingTables[i].WFile, StringComparison.OrdinalIgnoreCase) && Simulator.Instance.MovingTables[i].UID == uid)
                {
                    return i;
                }
            }
            return -1;
        }

        //================================================================================================//
        /// <summary>
        /// Calculate offset of timetable position in access path
        /// </summary>

        private void CalculateAccessOffsets(int ipath, TurnTable thisTurntable)
        {
            AccessPathDetails thisPath = AdditionalTurntableDetails.AccessPaths[ipath];

            // calculate total length of track sections in first section backward upto turntable section
            TrackCircuitSection thisSection = thisPath.AccessPath[0].TrackCircuitSection;
            int trackNodeIndex = thisSection.OriginalIndex;

            TrackVectorSection[] trackVectors = RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[trackNodeIndex].TrackVectorSections;

            // check if path is in front or behind turntable

            int lastVectorIndex = 0;
            float entrySectionLength = 0.0f;
            float exitSectionLength = 0.0f;

            if (thisPath.TableVectorIndex < thisPath.AccessTraveller.TrackVectorSectionIndex)
            {
                // path is behind turntable - calculate length from turntable to end
                // also set direction forward (path is outbound)
                lastVectorIndex = thisPath.TableVectorIndex + 1;
                entrySectionLength = CalculateVectorLength(lastVectorIndex, trackVectors.Length - 1, lastVectorIndex, trackVectors);
                exitSectionLength = CalculateVectorLength(0, lastVectorIndex - 2, lastVectorIndex, trackVectors);
                thisPath.AccessPath[0].Direction = TrackDirection.Reverse;
                thisPath.AccessTraveller.Direction = Direction.Forward;
            }
            else
            {
                // path is in front of turntable - calculate length from start to turntable
                // also set direction backward (path is outbound)
                lastVectorIndex = thisPath.TableVectorIndex - 1;
                entrySectionLength = CalculateVectorLength(0, lastVectorIndex, lastVectorIndex, trackVectors);
                exitSectionLength = CalculateVectorLength(lastVectorIndex + 2, trackVectors.Length - 1, lastVectorIndex, trackVectors);
                thisPath.AccessPath[0].Direction = TrackDirection.Ahead;
                thisPath.AccessTraveller.Direction = Direction.Backward;
            }

            // deduct clearance for turntable
            // if no explicit clearance defined, use length of last vector before turntable

            thisPath.TableApproachOffset = entrySectionLength - AdditionalTurntableDetails.TurntableApproachClearanceM;
            thisPath.TableMiddleEntry = entrySectionLength + (thisTurntable.Length / 2.0f);
            thisPath.TableMiddleExit = exitSectionLength + (thisTurntable.Length / 2.0f);

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("Pool : {0} , calculate access positions for path : {1} :", PoolName, thisPath.AccessPathName);
            Trace.TraceInformation("\n     start offset {0}\n     middle offset [exit] : {1}\n     middle offset [entry] : {2}\n     turntable length : {3}\n     section length : {4}",
                                     thisPath.TableApproachOffset, thisPath.TableMiddleEntry, thisPath.TableMiddleExit, thisTurntable.Length, thisSection.Length);
#endif

            // get turntable exit index
            thisPath.TableExitIndex = thisTurntable.FindExitNode(trackNodeIndex);

            // store updated path
            AdditionalTurntableDetails.AccessPaths[ipath] = thisPath;
        }

        //================================================================================================//
        /// <summary>
        /// Calculate length and turntable offset for storage paths
        /// </summary>

        private void CalculateStorageOffsets(int ipath, TurnTable thisTurntable)
        {
            PoolDetails thisPath = StoragePool[ipath];

            float baseLength = 0;

            // calculate total length of path sections except first section
            for (int isection = 1; isection < thisPath.StoragePath.Count; isection++)
            {
                baseLength += thisPath.StoragePath[isection].TrackCircuitSection.Length;
            }

            // calculate total length of track sections in first section backward upto turntable section
            TrackCircuitSection thisSection = thisPath.StoragePath[0].TrackCircuitSection;
            int trackNodeIndex = thisSection.OriginalIndex;

            TrackVectorSection[] trackVectors = RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[trackNodeIndex].TrackVectorSections;

            // check if path is in front or behind turntable

            int lastVectorIndex = 0;
            float entrySectionLength = 0.0f;
            float exitSectionLength = 0.0f;

            if (thisPath.TableVectorIndex < thisPath.StoragePathTraveller.TrackVectorSectionIndex)
            {
                // path is behind turntable - calculate length from turntable to end
                // also set direction forward (path is outbound)
                lastVectorIndex = thisPath.TableVectorIndex + 1;
                entrySectionLength = CalculateVectorLength(lastVectorIndex, trackVectors.Length - 1, lastVectorIndex, trackVectors);
                exitSectionLength = CalculateVectorLength(0, lastVectorIndex - 2, lastVectorIndex, trackVectors);
                thisPath.StoragePath[0].Direction = TrackDirection.Reverse;
                thisPath.StoragePathTraveller.Direction = Direction.Forward;
                thisPath.StoragePathReverseTraveller.Direction = Direction.Backward;
            }
            else
            {
                // path is in front of turntable - calculate length from start to turntable
                // also set direction backward (path is outbound)
                lastVectorIndex = thisPath.TableVectorIndex - 1;
                entrySectionLength = CalculateVectorLength(0, lastVectorIndex, lastVectorIndex, trackVectors);
                exitSectionLength = CalculateVectorLength(lastVectorIndex + 2, trackVectors.Length - 1, lastVectorIndex, trackVectors);
                thisPath.StoragePath[0].Direction = TrackDirection.Ahead;
                thisPath.StoragePathTraveller.Direction = Direction.Backward;
                thisPath.StoragePathReverseTraveller.Direction = Direction.Forward;
            }

            float totalLength = baseLength + entrySectionLength;

            // deduct clearance for turntable

            thisPath.TableMiddleEntry = totalLength + (thisTurntable.Length / 2.0f);
            thisPath.TableMiddleExit = exitSectionLength + (thisTurntable.Length / 2.0f);

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("Pool : {0} , calculate access positions for path {1}:", PoolName, thisPath.StorageName);
            Trace.TraceInformation("\n     middle offset [exit] : {0}\n     middle offset [entry] : {1}\n     turntable length : {2}\n     section length : {3}",
                                     thisPath.TableMiddleEntry, thisPath.TableMiddleExit, thisTurntable.Length, thisSection.Length);
#endif

            // get turntable exit index
            thisPath.TableExitIndex = thisTurntable.FindExitNode(trackNodeIndex);

            // store updated path
            StoragePool[ipath] = thisPath;
        }

        //================================================================================================//
        /// <summary>
        /// Calculate length of section connected to turntable
        /// </summary>

        private float CalculateVectorLength(int firstIndex, int LastIndex, int connectIndex, TrackVectorSection[] vectors)
        {
            float returnLength = 0.0f;

            for (int iVector = firstIndex; iVector <= LastIndex; iVector++)
            {
                TrackVectorSection thisVector = vectors[iVector];

                if (RuntimeData.Instance.TSectionDat.TrackSections.TryGetValue(thisVector.SectionIndex, out TrackSection ts))
                {
                    returnLength += ts.Length;
                }
            }

            return (returnLength);
        }

        //================================================================================================//
        /// <summary>
        /// Create in pool : create train in pool
        /// </summary>

        override public int CreateInPool(TTTrain train, List<TTTrain> nextTrains)
        {
            train.TCRoute.TCRouteSubpaths[0] = PlaceInPool(train, out int PoolStorageState, false);
            train.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[0]);
            train.TCRoute.ActiveSubPath = 0;

            // if no storage available - abondone train
            if (PoolStorageState < 0)
            {
                return (PoolStorageState);
            }

            train.PoolStorageIndex = PoolStorageState;

            // if no of units is limited to 1, place engine in direction of turntable
            if (StoragePool[PoolStorageState].MaxStoredUnits.HasValue && StoragePool[PoolStorageState].MaxStoredUnits == 1)
            {
                // use stored traveller
                train.RearTDBTraveller = new Traveller(StoragePool[PoolStorageState].StoragePathTraveller);
            }

            else
            {
                // use reverse path
                train.TCRoute.TCRouteSubpaths[0] = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[0].ReversePath());
                train.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[0]);

                train.RearTDBTraveller = new Traveller(StoragePool[PoolStorageState].StoragePathReverseTraveller);

                // if storage available check for other engines on storage track
                if (StoragePool[PoolStorageState].StoredUnits.Count > 0)
                {
                    int lastTrainNumber = StoragePool[PoolStorageState].StoredUnits[StoragePool[PoolStorageState].StoredUnits.Count - 1];
                    TTTrain lastTrain = train.GetOtherTTTrainByNumber(lastTrainNumber);
                    lastTrain ??= Simulator.Instance.GetAutoGenTTTrainByNumber(lastTrainNumber);
                    if (lastTrain != null)
                    {
                        train.CreateAhead = lastTrain.Name;
                    }
                }
            }

            bool validPosition = false;
            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTTTrainPosition(ref validPosition, nextTrains);

            if (validPosition)
            {
                train.SetInitialTrainRoute(tempRoute);
                train.CalculatePositionOfCars();
                for (int i = 0; i < train.Cars.Count; i++)
                {
                    Microsoft.Xna.Framework.Vector3 position = train.Cars[i].WorldPosition.XNAMatrix.Translation;
                    position.Y -= 1000;
                    train.Cars[i].UpdateWorldPosition(train.Cars[i].WorldPosition.SetTranslation(position));
                }
                train.ResetInitialTrainRoute(tempRoute);

                // set train route and position so proper position in pool can be calculated
                train.UpdateTrainPosition();

                // add unit to pool
                AddUnit(train, false);
                validPosition = train.PostInit(false); // post init train but do not activate
            }

            return (PoolStorageState);
        }

        //================================================================================================//
        /// <summary>
        /// Place in pool : place train in pool, for this type of pool train is created directly on storage path
        /// </summary>

        public override TrackCircuitPartialPathRoute PlaceInPool(TTTrain train, out int poolStorageIndex, bool checkAccessPath)
        {
            TrackCircuitPartialPathRoute newRoute = null;
            int storageIndex = -1;

            // check if train fits on turntable - if not, reject
            TurnTable thisTurntable = Simulator.Instance.MovingTables[AdditionalTurntableDetails.TurntableIndex] as TurnTable;

            if (train.Length > thisTurntable.Length)
            {
                Trace.TraceWarning($"Train : {train.Name} too long ({train.Length}) for turntable (length {thisTurntable.Length}) in pool {PoolName}\n");
            }
            else
            {
                storageIndex = GetPoolExitIndex(train);
                if (storageIndex < 0)
                {
                    Trace.TraceWarning("Pool : " + PoolName + " : overflow : cannot place train : " + train.Name + "\n");
                }
                else
                {
                    newRoute = new TrackCircuitPartialPathRoute(StoragePool[storageIndex].StoragePath);
                }
            }

            poolStorageIndex = storageIndex;
            return (newRoute);
        }

        //================================================================================================//
        /// <summary>
        /// TestPoolExit : find access path linked to trains path
        /// </summary>

        public override bool TestPoolExit(TTTrain train)
        {
            int testAccess;
            bool validPath = TestPoolAccess(train, out testAccess);
            return (validPath);
        }

        public bool TestPoolAccess(TTTrain train, out int accessIndex)
        {
            bool validPool = false;
            int reqPath = -1;
            int reqPathIndex = -1;

            // set dispose states
            train.FormsStatic = true;
            train.Closeup = true;

            // find relevant access path

            int lastValidSectionIndex = train.TCRoute.TCRouteSubpaths.Last().Count - 1;
            for (int iSection = train.TCRoute.TCRouteSubpaths.Last().Count - 1; iSection >= 0 && reqPath == -1; iSection--)
            {
                int lastSectionIndex = train.TCRoute.TCRouteSubpaths.Last()[iSection].TrackCircuitSection.Index;
                TrackDirection lastSectionDirection = train.TCRoute.TCRouteSubpaths.Last()[iSection].Direction;

                if (TrackCircuitSection.TrackCircuitList[lastSectionIndex].CircuitType == TrackCircuitType.Normal)
                {
                    for (int iPath = 0; iPath < AdditionalTurntableDetails.AccessPaths.Count && reqPath < 0; iPath++)
                    {
                        TrackCircuitPartialPathRoute accessPath = AdditionalTurntableDetails.AccessPaths[iPath].AccessPath;
                        reqPathIndex = accessPath.GetRouteIndex(lastSectionIndex, 0);

                        // path is defined outbound, so directions must be opposite
                        if (reqPathIndex >= 0 && accessPath[reqPathIndex].Direction != lastSectionDirection)
                        {
                            reqPath = iPath;
                            lastValidSectionIndex = iSection;
                        }
                    }
                }
            }

            // remove sections from train route if required
            if (reqPath >= 0 && lastValidSectionIndex < train.TCRoute.TCRouteSubpaths.Last().Count - 1)
            {
                for (int iSection = train.TCRoute.TCRouteSubpaths.Last().Count - 1; iSection > lastValidSectionIndex; iSection--)
                {
                    train.TCRoute.TCRouteSubpaths.Last().RemoveAt(iSection);
                }
            }

            // none found
            if (reqPath < 0)
            {
                Trace.TraceWarning("Train : " + train.Name + " : no valid path found to access pool storage " + PoolName + "\n");
                train.FormsStatic = false;
                train.Closeup = false;
            }
            // path found
            else
            {
                validPool = true;
            }

            accessIndex = reqPath;
            return (validPool);
        }

        //================================================================================================//
        /// <summary>
        /// Extract train from pool
        /// </summary>

        public override TrainFromPool ExtractTrain(ref TTTrain train, int presentTime)
        {
#if DEBUG_POOLINFO
            var sob = new StringBuilder();
            sob.AppendFormat("Pool {0} : request for train {1} ({2})", PoolName, train.Number, train.Name);
            File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
            // check if any engines available
            int selectedTrainNumber = -1;
            int selectedStorage = -1;
            bool claimActive = false;

            for (int iStorage = 0; iStorage < StoragePool.Count; iStorage++)
            {
                PoolDetails thisStorage = StoragePool[iStorage];
                // engine has claimed access - this storage cannot be used for exit right now
                if (thisStorage.ClaimUnits.Count > 0)
                {
                    claimActive = true;
                }
                else if (thisStorage.StoredUnits.Count > 0)
                {
                    selectedTrainNumber = thisStorage.StoredUnits.Last();
                    selectedStorage = iStorage;
                    break;
                }
            }

            // pool underflow : create engine from scratch
            if (selectedTrainNumber < 0)
            {
                // no train found but claim is active - create engine is delayed
                if (claimActive)
                {
                    return (TrainFromPool.Delayed);
                }

                DateTime baseDTA = new DateTime();
                DateTime moveTimeA = baseDTA.AddSeconds(train.AI.ClockTime);

                if (forceCreation)
                {
                    Trace.TraceInformation("Train request : " + train.Name + " from pool " + PoolName +
                        " : no engines available in pool, engine is created, at " + moveTimeA.ToString("HH:mm:ss") + "\n");
#if DEBUG_POOLINFO
                    sob = new StringBuilder();
                    sob.AppendFormat("Pool {0} : train {1} ({2}) : no units available, engine force created", PoolName, train.Number, train.Name);
                    File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
                    return (TrainFromPool.ForceCreated);
                }
                else
                {
                    Trace.TraceInformation("Train request : " + train.Name + " from pool " + PoolName +
                        " : no engines available in pool, engine is not created , at " + moveTimeA.ToString("HH:mm:ss") + "\n");
#if DEBUG_POOLINFO
                    sob = new StringBuilder();
                    sob.AppendFormat("Pool {0} : train {1} ({2}) : no units available, enigne not created", PoolName, train.Number, train.Name);
                    File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
                    return (TrainFromPool.NotCreated);
                }
            }

            // find required access path
            int firstSectionIndex = train.TCRoute.TCRouteSubpaths[0][0].TrackCircuitSection.Index;

            int reqAccessPath = -1;
            for (int iPath = 0; iPath < AdditionalTurntableDetails.AccessPaths.Count; iPath++)
            {
                TrackCircuitPartialPathRoute thisPath = AdditionalTurntableDetails.AccessPaths[iPath].AccessPath;
                int pathSectionIndex = thisPath.GetRouteIndex(firstSectionIndex, 0);
                if (pathSectionIndex >= 0 && AdditionalTurntableDetails.AccessPaths[iPath].AccessPath[pathSectionIndex].Direction == train.TCRoute.TCRouteSubpaths[0][0].Direction)
                {
                    reqAccessPath = iPath;
                    break;
                }
            }

            // no valid path found
            if (reqAccessPath < 0)
            {
                Trace.TraceInformation("Train request : " + train.Name + " from pool " + PoolName + " : no valid access path found \n");
                return (TrainFromPool.Failed);
            }

            // valid train found - start train
            TTTrain selectedTrain = train.GetOtherTTTrainByNumber(selectedTrainNumber);
            if (selectedTrain == null)
            {
#if DEBUG_POOLINFO
                sob = new StringBuilder();
                sob.AppendFormat("Pool {0} : cannot find train {1} for {2} ({3}) \n", PoolName, selectedTrainNumber, train.Number, train.Name);
                sob.AppendFormat("           stored units : {0}", StoragePool[selectedStorage].StoredUnits.Count);
                File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
                return (TrainFromPool.Delayed);
            }

            TrackCircuitSection[] occupiedSections = new TrackCircuitSection[selectedTrain.OccupiedTrack.Count];
            selectedTrain.OccupiedTrack.CopyTo(occupiedSections);

            selectedTrain.Forms = -1;
            selectedTrain.RemoveTrain();
            train.FormedOfType = TimetableFormationCommand.TerminationFormed;

#if DEBUG_POOLINFO
            sob = new StringBuilder();
            sob.AppendFormat("Pool {0} : train {1} ({2}) extracted as {3} ({4}) \n", PoolName, selectedTrain.Number, selectedTrain.Name, train.Number, train.Name);
            sob.AppendFormat("           stored units : {0}", StoragePool[selectedStorage].StoredUnits.Count);
            File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
            // add access path from turntable to train path (path is defined outbound)
            AccessPathDetails reqPath = AdditionalTurntableDetails.AccessPaths[reqAccessPath];
            train.TCRoute.AddSectionsAtStart(reqPath.AccessPath, train, false);

            // set path of train upto turntable (reverse of storage path)
            PoolDetails reqStorage = StoragePool[selectedStorage];
            TrackCircuitPartialPathRoute reversePath = reqStorage.StoragePath.ReversePath();
            train.TCRoute.AddSubrouteAtStart(reversePath, train);
            train.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[0]);

            // set details for new train from existing train
            bool validFormed = train.StartFromAITrain(selectedTrain, presentTime, occupiedSections);

            if (validFormed)
            {
                train.InitializeSignals(true);

                // start new train
                if (Simulator.Instance.StartReference.Contains(train.Number))
                {
                    Simulator.Instance.StartReference.Remove(train.Number);
                }

                // existing train is player, so continue as player
                if (selectedTrain.TrainType == TrainType.Player)
                {
                    train.AI.TrainsToRemoveFromAI.Add(train);

                    // set proper details for new formed train
                    train.OrgAINumber = train.Number;
                    train.Number = 0;
                    train.LeadLocomotiveIndex = selectedTrain.LeadLocomotiveIndex;
                    for (int carid = 0; carid < train.Cars.Count; carid++)
                    {
                        train.Cars[carid].CarID = selectedTrain.Cars[carid].CarID;
                    }
                    train.AI.TrainsToAdd.Add(train);
                    Simulator.Instance.Trains.Add(train);

                    train.SetFormedOccupied();
                    train.TrainType = TrainType.Player;
                    train.ControlMode = TrainControlMode.Inactive;
                    train.MovementState = AiMovementState.Static;

                    // inform viewer about player train switch
                    Simulator.Instance.PlayerLocomotive = train.LeadLocomotive;
                    Simulator.Instance.OnPlayerLocomotiveChanged();

                    Simulator.Instance.OnPlayerTrainChanged(selectedTrain, train);
                    Simulator.Instance.PlayerLocomotive.Train = train;

                    train.SetupStationStopHandling();

                    // clear replay commands
                    Simulator.Instance.Log.CommandList.Clear();

                    // display messages
                    Simulator.Instance.Confirmer?.Information("Player switched to train : " + train.Name); // As Confirmer may not be created until after a restore.
                }

                // new train is intended as player
                else if (train.TrainType == TrainType.Player || train.TrainType == TrainType.PlayerIntended)
                {
                    train.TrainType = TrainType.Player;
                    train.ControlMode = TrainControlMode.Inactive;
                    train.MovementState = AiMovementState.Static;

                    train.AI.TrainsToAdd.Add(train);

                    // set player locomotive
                    // first test first and last cars - if either is drivable, use it as player locomotive
                    Simulator.Instance.PlayerLocomotive = train.LeadLocomotive = train.Cars.First() as MSTSLocomotive ?? train.Cars.Last() as MSTSLocomotive ?? train.Cars.OfType<MSTSLocomotive>().FirstOrDefault();

                    train.InitializeBrakes();

                    if (Simulator.Instance.PlayerLocomotive == null)
                    {
                        throw new InvalidDataException("Can't find player locomotive in " + train.Name);
                    }
                    else
                    {
                        foreach (TrainCar car in train.Cars)
                        {
                            if (car.WagonType == WagonType.Engine)
                            {
                                MSTSLocomotive loco = car as MSTSLocomotive;
                                loco.AntiSlip = train.leadLocoAntiSlip;
                            }
                        }
                    }
                }

                // normal AI train
                else
                {
                    // set delay
                    train.RestdelayS = train.DelayedStartSettings[DelayedStartType.NewStart].RemainingDelay();
                    train.DelayStart = true;
                    train.DelayedStartState = AiStartMovement.NewTrain;

                    train.TrainType = TrainType.Ai;
                    train.AI.TrainsToAdd.Add(train);
                }

                train.SetFormedOccupied();

                // update any outstanding required actions adding the added length
                train.ResetActions(true);

                // set forced consist name if required
                if (!String.IsNullOrEmpty(train.ForcedConsistName))
                {
                    foreach (var car in train.Cars)
                    {
                        car.OriginalConsist = train.ForcedConsistName;
                    }
                }

                // set timetable turntable class
                train.ActiveTurntable = new TimetableTurntableControl(this, PoolName, AdditionalTurntableDetails.TurntableIndex, train);
                train.ActiveTurntable.MovingTableState = MovingTableState.WaitingMovingTableAvailability;
                train.ActiveTurntable.MovingTableAction = MovingTableAction.FromStorage;
                train.ActiveTurntable.AccessPathIndex = reqAccessPath;
                train.ActiveTurntable.StoragePathIndex = selectedStorage;
                train.MovementState = AiMovementState.Static;
                train.ActivateTime = presentTime - 1; // train is immediately activated
            }
            else
            {
                return (TrainFromPool.Failed);
            }

            // update pool data
            reqStorage.StoredUnits.Remove(selectedTrainNumber);

            // get last train in storage
            TTTrain storedTrain = null;

            if (reqStorage.StoredUnits.Count > 0)
            {
                int trainNumber = reqStorage.StoredUnits.Last();
                storedTrain = train.GetOtherTTTrainByNumber(trainNumber);

                if (storedTrain != null)
                {
                    reqStorage.RemainingLength = CalculateStorageLength(reqStorage, storedTrain);
                }
                else
                {
                    Trace.TraceWarning("Error in pool {0} : stored units : {1} : train no. {2} not found\n", PoolName, reqStorage.StoredUnits.Count, trainNumber);
                    reqStorage.StoredUnits.RemoveAt(reqStorage.StoredUnits.Count - 1);

                    trainNumber = reqStorage.StoredUnits.Last();
                    storedTrain = train.GetOtherTTTrainByNumber(trainNumber);

                    if (storedTrain != null)
                    {
                        reqStorage.RemainingLength = CalculateStorageLength(reqStorage, storedTrain);
                    }
                }
            }
            else
            {
                reqStorage.RemainingLength = reqStorage.StorageLength;
            }

            StoragePool[selectedStorage] = reqStorage;

            return (TrainFromPool.Formed);
        }

        //================================================================================================//
        /// <summary>
        /// SetPoolExit : adjust train dispose details and path to required pool exit
        /// Returned poolStorageState : <0 : state (enum TTTrain.PoolAccessState); >0 : poolIndex
        /// </summary>

        public override TrackCircuitPartialPathRoute SetPoolExit(TTTrain train, out int poolStorageState, bool checkAccessPath)
        {
            // new route
            TrackCircuitPartialPathRoute newRoute = null;
            poolStorageState = -1;

            // set dispose states
            train.FormsStatic = true;
            train.Closeup = true;

            // find storage path with enough space to store train

            int reqPool = GetPoolExitIndex(train);
            int reqPath = 0;

            // no storage space found : pool overflow
            if (reqPool == (int)PoolAccessState.PoolOverflow)
            {
                Trace.TraceWarning("Pool : " + PoolName + " : overflow : cannot place train : " + train.Name + "\n");

                // train will be abandoned when reaching end of path
                train.FormsStatic = false;
                train.Closeup = false;
            }

            // no valid pool found
            else if (reqPool == (int)PoolAccessState.PoolInvalid)
            {
                // pool invalid
                Trace.TraceWarning("Pool : " + PoolName + " : no valid pool found : " + train.Name + "\n");

                // train will be abandoned when reaching end of path
                train.FormsStatic = false;
                train.Closeup = false;
            }

            // no action if state is poolClaimed - state will resolve as train ahead is stabled in pool

            // valid pool
            else if (reqPool >= 0)
            {
                // train approaches from exit path - train is moving toward turntable and is stored after turntable movement
                if (checkAccessPath)
                {
                    bool validPath = TestPoolAccess(train, out reqPath);

                    // none found
                    if (!validPath)
                    {
                        Trace.TraceWarning("Train : " + train.Name + " : no valid path found to access pool storage " + PoolName + "\n");
                        train.FormsStatic = false;
                        train.Closeup = false;
                        poolStorageState = -1;
                    }
                    // path found : extend train path with access paths
                    else
                    {
                        TrackCircuitPartialPathRoute accessPath = AdditionalTurntableDetails.AccessPaths[reqPath].AccessPath;
                        newRoute = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths.Last());

                        // add elements from access route except those allready on the path
                        // add in reverse order and reverse direction as path is defined outbound
                        for (int iElement = accessPath.Count - 1; iElement >= 0; iElement--)
                        {
                            if (newRoute.GetRouteIndex(accessPath[iElement].TrackCircuitSection.Index, 0) < 0)
                            {
                                TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(accessPath[iElement]);
                                newElement.Direction = newElement.Direction.Reverse();
                                newRoute.Add(newElement);
                            }
                        }

                        train.PoolStorageIndex = reqPool;

                        // set approach to turntable
                        // also add unit to storage as claim
                        newRoute.Last().MovingTableApproachPath = reqPath;
                        AddUnit(train, true);
                        StoragePool[reqPool].ClaimUnits.Add(train.Number);

                    }
                }
                // create new route from access track only
                // use first defined access track
                // if only one unit allowed on storage, reverse path so train stands at allocated position
                // if multiple units allowed on storage, do not reverse path so train moves to end of storage location
                else
                {
                    if (StoragePool[reqPool].MaxStoredUnits == 1)
                    {
                        newRoute = new TrackCircuitPartialPathRoute(AdditionalTurntableDetails.AccessPaths[0].AccessPath.ReversePath());
                    }
                    else
                    {
                        newRoute = new TrackCircuitPartialPathRoute(AdditionalTurntableDetails.AccessPaths[0].AccessPath);
                    }
                    train.PoolStorageIndex = reqPool;
                }
            }
            return (newRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Get end of route distance on approach to turntable
        /// </summary>

        internal override float GetEndOfRouteDistance(TrackCircuitPartialPathRoute thisRoute, TrackCircuitPosition frontPosition, int pathIndex)
        {
            // get distance to approach point from present position of train
            int turntableSectionIndex = thisRoute.GetRouteIndex(AdditionalTurntableDetails.AccessPaths[pathIndex].AccessPath[0].TrackCircuitSection.Index, 0);
            float startoffset = TrackCircuitSection.TrackCircuitList[frontPosition.TrackCircuitSectionIndex].Length - frontPosition.Offset;
            if (frontPosition.RouteListIndex < 0 && turntableSectionIndex < 0)
            {
                Trace.TraceInformation("Invalid check on turntable approach : present position and turntable index both < 0; for pool : " + PoolName);
                return (-1);
            }
            float distanceToTurntable = thisRoute.GetDistanceAlongRoute(frontPosition.RouteListIndex, startoffset,
                turntableSectionIndex, AdditionalTurntableDetails.AccessPaths[pathIndex].TableApproachOffset, true);

            return (distanceToTurntable);
        }
    }

    /// <summary>
    /// Class to hold additional info and methods for use of turntable in timetable mode
    /// </summary>
    public class TimetableTurntableControl : ISaveStateApi<TimetableTurntableControlSaveState>
    {
        private TurnTable parentTurntable;                          // parent turntable
        private int parentIndex;                                    // index of parent turntable in moving table list

        private TimetableTurntablePool parentPool;                  // parent pool
        private string poolName;                                    // parent pool name

        private TTTrain parentTrain;                         // train linked to turntable actions

        private TrainOnMovingTable trainOnTable;            // class for train on table information
        private int reqTurntableExit;                              // index of required exit
        private bool reqReverseFormation;                          // train exits table in reverse formation
        private float clearingDistanceM;                           // distance for train to move to clear turntable
        private float originalTrainMaxSpeedMpS;                    // original allowed train max speed
        private float originalSpeedSignalMpS;                      // original signal speed limit
        private float originalSpeedLimitMpS;                       // original speedpost speed limit
        private float stopPositionOnTurntableM;                    // actual stop position on turntable

        public MovingTableState MovingTableState { get; set; } = MovingTableState.Inactive;     // state of this turntable
        public MovingTableAction MovingTableAction { get; set; } = MovingTableAction.Undefined; // type of action 
        public int StoragePathIndex { get; set; }                       // index of selected storage path
        public int AccessPathIndex { get; set; }                        // index of selected access path

        public TimetableTurntableControl(TTTrain parentTrain)
        {
            this.parentTrain = parentTrain;
        }

        // constructor from new
        public TimetableTurntableControl(TimetableTurntablePool parentPool, string poolName, int turntableIndex, TTTrain train)
        {
            this.parentPool = parentPool;
            this.poolName = poolName;
            parentIndex = turntableIndex;
            parentTrain = train;
            parentTurntable = Simulator.Instance.MovingTables[parentIndex] as TurnTable;

            // set defined framerate if defined and not yet set for turntable
            if (this.parentPool.AdditionalTurntableDetails.FrameRate.HasValue && !parentTurntable.TurntableFrameRate.HasValue)
            {
                parentTurntable.TurntableFrameRate = this.parentPool.AdditionalTurntableDetails.FrameRate.Value;
            }
        }

        public ValueTask<TimetableTurntableControlSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TimetableTurntableControlSaveState()
            {
                ParentIndex = parentIndex,
                PoolName = poolName,
                MovingTableAction = MovingTableAction,
                MovingTableState = MovingTableState,
                StoragePathIndex = StoragePathIndex,
                AccessPathIndex = AccessPathIndex,
                ReverseFormation = reqReverseFormation,
                TurnTableExit = reqTurntableExit,
                ClearingDistance = clearingDistanceM,
                TrainSpeedMax = originalTrainMaxSpeedMpS,
                TrainSpeedSignal = originalSpeedSignalMpS,
                TrainSpeedLimit = originalSpeedLimitMpS,
                StopPositionOnTurntable = stopPositionOnTurntableM,
                TrainNumber = trainOnTable?.Train.Number,
                FrontOnBoard = trainOnTable?.FrontOnBoard ?? false,
                RearOnBoard = trainOnTable?.BackOnBoard ?? false,
            });
        }

        public ValueTask Restore(TimetableTurntableControlSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            parentIndex = saveState.ParentIndex;
            parentTurntable = null;  // cannot be restored as turntables are restored later

            poolName = saveState.PoolName;
            parentPool = Simulator.Instance.PoolHolder.Pools[poolName] as TimetableTurntablePool;

            MovingTableState = saveState.MovingTableState;
            MovingTableAction = saveState.MovingTableAction;
            StoragePathIndex = saveState.StoragePathIndex;
            AccessPathIndex = saveState.AccessPathIndex;
            reqReverseFormation = saveState.ReverseFormation;
            reqTurntableExit = saveState.TurnTableExit;

            clearingDistanceM = saveState.ClearingDistance;
            originalTrainMaxSpeedMpS = saveState.TrainSpeedMax;
            originalSpeedSignalMpS = saveState.TrainSpeedSignal;
            originalSpeedLimitMpS = saveState.TrainSpeedLimit;
            stopPositionOnTurntableM = saveState.StopPositionOnTurntable;

            if (saveState.TrainNumber.HasValue)
            {
                trainOnTable = new TrainOnMovingTable(saveState.TrainNumber.Value, saveState.FrontOnBoard, saveState.RearOnBoard, parentTrain);
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Check if turntable is available for this train
        /// </summary>
        public bool CheckTurntableAvailable()
        {
            parentTurntable ??= Simulator.Instance.MovingTables[parentIndex] as TurnTable;

            bool available = true;
            // check if waiting for turntable availability
            if (MovingTableState == MovingTableState.WaitingMovingTableAvailability)
            {
                available = false;

                // turntable is not available
                if (parentTurntable.InUse)
                {
                    if (!parentTurntable.WaitingTrains.Contains(parentTrain.Number))
                    {
                        parentTurntable.WaitingTrains.Enqueue(parentTrain.Number);
                    }
                }
                else
                {
                    if (parentTurntable.WaitingTrains.Count < 1 || parentTurntable.WaitingTrains.Peek() == parentTrain.Number)
                    {
                        if (parentTurntable.WaitingTrains.Count >= 1)
                            parentTurntable.WaitingTrains.Dequeue();
                        available = true;
                        parentTurntable.InUse = true;
                        switch (MovingTableAction)
                        {
                            case MovingTableAction.FromAccess:
                                MovingTableState = MovingTableState.WaitingAccessToMovingTable;
                                AccessPathIndex = GetAccessPathIndex();
                                StoragePathIndex = parentTrain.PoolStorageIndex;
                                break;

                            case MovingTableAction.FromStorage:
                                MovingTableState = MovingTableState.WaitingStorageToMovingTable;
                                break;

                            default:
                                MovingTableState = MovingTableState.Inactive;
                                break;
                        }
                    }
                }
            }
            return (available);
        }

        /// <summary>
        /// Perform update for train and turntable depending on action state (for AI trains)
        /// </summary>
        /// return : true if turntable actions for this train have terminated
        /// Instance of class will then be removed by train
        public void UpdateTurntableStateAI(double elapsedClockSeconds, int presentTime)
        {
            if (parentTurntable == null)
                parentTurntable = Simulator.Instance.MovingTables[parentIndex] as TurnTable;

            int reqTurntableExit = -1;
            int reqTurntableEntry = -1;

            Direction reqEntryDirection = Direction.Forward;
            Direction reqExitDirection = Direction.Forward;

            // switch on turntable action state

            switch (MovingTableState)
            {

                // state : waiting for table (from Access)
                case MovingTableState.WaitingAccessToMovingTable:

                    reqTurntableEntry = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    reqTurntableExit = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqExitDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    if (AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds))
                    {
                        parentTrain.DelayedStartMoving(AiStartMovement.Turntable);
                        MovingTableState = MovingTableState.AccessToMovingTable;
                        parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;

                        // calculate end position
                        parentTrain.EndAuthorities[Direction.Forward].Distance = CalculateDistanceToTurntable();

                        // set reduced speed
                        float reqTrainSpeed = parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.HasValue ?
                            Math.Min(parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.Value, parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value) :
                            parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value;

                        originalTrainMaxSpeedMpS = parentTrain.TrainMaxSpeedMpS;
                        originalSpeedSignalMpS = parentTrain.AllowedMaxSpeedSignalMpS;
                        originalSpeedLimitMpS = parentTrain.AllowedMaxSpeedLimitMpS;
                        parentTrain.TrainMaxSpeedMpS = reqTrainSpeed;
                        parentTrain.AllowedMaxSpeedMpS = Math.Min(parentTrain.AllowedMaxSpeedMpS, parentTrain.TrainMaxSpeedMpS);

                        // add storage path
                        parentTrain.TCRoute.AddSubrouteAtEnd(parentPool.StoragePool[parentTrain.PoolStorageIndex].StoragePath);
                    }
                    break;

                // state : waiting for table (from storage)
                case MovingTableState.WaitingStorageToMovingTable:

                    reqTurntableEntry = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    reqTurntableExit = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqExitDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    if (AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds))
                    {
                        parentTrain.DelayedStartMoving(AiStartMovement.Turntable);
                        MovingTableState = MovingTableState.StorageToMovingTable;
                        parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;

                        // calculate end position
                        parentTrain.EndAuthorities[Direction.Forward].Distance = CalculateDistanceToTurntable();

                        // set reduced speed
                        float reqTrainSpeed = parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.HasValue ?
                            Math.Min(parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.Value, parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value) :
                            parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value;

                        originalTrainMaxSpeedMpS = parentTrain.TrainMaxSpeedMpS;
                        originalSpeedSignalMpS = parentTrain.AllowedMaxSpeedSignalMpS;
                        originalSpeedLimitMpS = parentTrain.AllowedMaxSpeedLimitMpS;
                        parentTrain.TrainMaxSpeedMpS = reqTrainSpeed;
                        parentTrain.AllowedMaxSpeedMpS = Math.Min(parentTrain.AllowedMaxSpeedMpS, parentTrain.TrainMaxSpeedMpS);
                    }
                    break;

                // state : moving onto turntable (from Access)
                // exit from this state is through UpdateBrakingState and SetNextStageOnStopped
                case MovingTableState.AccessToMovingTable:

                    parentTrain.EndAuthorities[Direction.Forward].Distance = CalculateDistanceToTurntable();
                    parentTrain.UpdateBrakingState(elapsedClockSeconds, presentTime);
                    break;


                // state : moving onto turntable (from Storage)
                // exit from this state is through UpdateBrakingState and SetNextStageOnStopped
                case MovingTableState.StorageToMovingTable:

                    parentTrain.EndAuthorities[Direction.Forward].Distance = CalculateDistanceToTurntable();
                    parentTrain.UpdateBrakingState(elapsedClockSeconds, presentTime);
                    break;

                // state : turning on turntable (from Access)
                // get required exit
                // exit from this state is through PrepareMoveOffTable and SetNextStageOnStopped 
                case MovingTableState.AccessOnMovingTable:

                    reqTurntableEntry = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    reqTurntableExit = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqExitDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds);
                    break;

                // state : turning on turntable (from Storage)
                // get required exit
                // exit from this state is through PrepareMoveOffTable and SetNextStageOnStopped 
                case MovingTableState.StorageOnMovingTable:

                    reqTurntableEntry = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    reqTurntableExit = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqExitDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds);
                    break;

                default:
                    break;
            }

            return;
        }

        /// <summary>
        /// Perform update for train and turntable depending on action state (for player train)
        /// </summary>
        /// return : true if turntable actions for this train have terminated
        /// Instance of class will then be removed by train
        public bool UpdateTurntableStatePlayer(double elapsedClockSeconds)
        {
            if (parentTurntable == null)
                parentTurntable = Simulator.Instance.MovingTables[parentIndex] as TurnTable;

            bool terminated = false;
            int reqTurntableExit = -1;
            int reqTurntableEntry = -1;

            Direction reqEntryDirection = Direction.Forward;
            Direction reqExitDirection = Direction.Forward;

            // switch on turntable action state

            switch (MovingTableState)
            {
                // state : waiting for table to become available
                case MovingTableState.WaitingMovingTableAvailability:
                    if (parentTurntable.InUse)
                    {
                        if (!parentTurntable.WaitingTrains.Contains(parentTrain.Number))
                        {
                            parentTurntable.WaitingTrains.Enqueue(parentTrain.Number);
                        }
                    }
                    else
                    {
                        if (parentTurntable.WaitingTrains.Count < 1 || parentTurntable.WaitingTrains.Peek() == parentTrain.Number)
                        {
                            if (parentTurntable.WaitingTrains.Count >= 1)
                                parentTurntable.WaitingTrains.Dequeue();
                            parentTurntable.InUse = true;

                            if (MovingTableAction == MovingTableAction.FromAccess)
                            {
                                MovingTableState = MovingTableState.WaitingAccessToMovingTable;
                                AccessPathIndex = GetAccessPathIndex();
                                StoragePathIndex = parentTrain.PoolStorageIndex;
                            }
                            else
                            {
                                MovingTableState = MovingTableState.WaitingStorageToMovingTable;
                            }
                        }
                    }
                    break;

                // state : waiting for table (from Access)
                case MovingTableState.WaitingAccessToMovingTable:

                    reqTurntableEntry = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    reqTurntableExit = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqExitDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    if (AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds))
                    {
                        MovingTableState = MovingTableState.AccessToMovingTable;

                        // calculate end position - place in front of timetable incl. clearance
                        parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;
                        parentTrain.EndAuthorities[Direction.Forward].Distance =
                            CalculateDistanceToTurntable() - (parentTurntable.Length / 2.0f) - parentPool.AdditionalTurntableDetails.TurntableApproachClearanceM;

                        // set reduced speed
                        float reqTrainSpeed = parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.HasValue ?
                            Math.Min(parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.Value, parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value) :
                            parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value;

                        originalTrainMaxSpeedMpS = parentTrain.TrainMaxSpeedMpS;
                        originalSpeedSignalMpS = parentTrain.AllowedMaxSpeedSignalMpS;
                        originalSpeedLimitMpS = parentTrain.AllowedMaxSpeedLimitMpS;
                        parentTrain.TrainMaxSpeedMpS = reqTrainSpeed;
                        parentTrain.AllowedMaxSpeedMpS = Math.Min(parentTrain.AllowedMaxSpeedMpS, parentTrain.TrainMaxSpeedMpS);

                        // add storage path
                        parentTrain.TCRoute.AddSubrouteAtEnd(parentPool.StoragePool[parentTrain.PoolStorageIndex].StoragePath);

                        // send message
                        var message = Simulator.Catalog.GetString("Turntable is ready for access - allowed speed set to {0}", FormatStrings.FormatSpeedDisplay(parentTrain.AllowedMaxSpeedMpS, RuntimeData.Instance.UseMetricUnits));
                        Simulator.Instance.Confirmer.Information(message);

                        // create train-on-table class
                        trainOnTable = new TrainOnMovingTable(parentTrain);
                        trainOnTable.SetFrontState(false);
                        trainOnTable.SetBackState(false);
                    }
                    break;

                // state : waiting for table (from storage)
                case MovingTableState.WaitingStorageToMovingTable:

                    reqTurntableEntry = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    reqTurntableExit = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqExitDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    if (AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds))
                    {
                        MovingTableState = MovingTableState.StorageToMovingTable;

                        // calculate end position - place in front of timetable incl. clearance
                        parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;
                        parentTrain.EndAuthorities[Direction.Forward].Distance =
                            CalculateDistanceToTurntable() - (parentTurntable.Length / 2.0f) - parentPool.AdditionalTurntableDetails.TurntableApproachClearanceM;

                        // set reduced speed
                        float reqTrainSpeed = parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.HasValue ?
                            Math.Min(parentPool.AdditionalTurntableDetails.TurntableSpeedMpS.Value, parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value) :
                            parentTrain.SpeedSettings[SpeedValueType.MovingtableSpeed].Value;

                        originalTrainMaxSpeedMpS = parentTrain.TrainMaxSpeedMpS;
                        originalSpeedSignalMpS = parentTrain.AllowedMaxSpeedSignalMpS;
                        originalSpeedLimitMpS = parentTrain.AllowedMaxSpeedLimitMpS;
                        parentTrain.TrainMaxSpeedMpS = reqTrainSpeed;
                        parentTrain.AllowedMaxSpeedMpS = Math.Min(parentTrain.AllowedMaxSpeedMpS, parentTrain.TrainMaxSpeedMpS);

                        // send message
                        var message = Simulator.Catalog.GetString("Turntable is ready for access - allowed speed set to {0}", FormatStrings.FormatSpeedDisplay(parentTrain.AllowedMaxSpeedMpS, RuntimeData.Instance.UseMetricUnits));
                        Simulator.Instance.Confirmer.Information(message);

                        // create train-on-table class
                        trainOnTable = new TrainOnMovingTable(parentTrain);
                        trainOnTable.SetFrontState(false);
                        trainOnTable.SetBackState(false);
                    }
                    break;

                // state : moving onto turntable (from Access)
                case MovingTableState.AccessToMovingTable:

                    // set end of authority beyond turntable
                    parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;
                    parentTrain.EndAuthorities[Direction.Forward].Distance = CalculateDistanceToTurntable() + (parentTurntable.Length / 2.0f);

                    // check if train position on turntable
                    if (!trainOnTable.FrontOnBoard)
                    {
                        if (parentTurntable.CheckOnSection(parentTrain.FrontTDBTraveller))
                        {
                            trainOnTable.SetFrontState(true);
                            Simulator.Instance.Confirmer.Information("Front of train is on table");
                        }
                    }
                    else if (!trainOnTable.BackOnBoard)
                    {
                        if (parentTurntable.CheckOnSection(parentTrain.RearTDBTraveller))
                        {
                            trainOnTable.SetBackState(true);
                            Simulator.Instance.Confirmer.Information("Rear of train is on table");
                            Simulator.Instance.Confirmer.Information("Stop train, set throttle to zero, set reverser to neutral");
                        }
                    }
                    else if (parentTrain.SpeedMpS < 0.05f)
                    {
                        parentTrain.ControlMode = TrainControlMode.TurnTable;

                        var loco = parentTrain.LeadLocomotive;
                        if (loco.ThrottlePercent < 1 && Math.Abs(loco.SpeedMpS) < 0.05 && (loco.Direction == MidpointDirection.N || Math.Abs(parentTrain.MUReverserPercent) <= 1))
                        {
                            // check if train still on turntable
                            if (!parentTurntable.CheckOnSection(parentTrain.FrontTDBTraveller))
                            {
                                trainOnTable.SetFrontState(false);
                                Simulator.Instance.Confirmer.Information("Front of train slipped off table");
                            }
                            if (!parentTurntable.CheckOnSection(parentTrain.RearTDBTraveller))
                            {
                                trainOnTable.SetBackState(false);
                                Simulator.Instance.Confirmer.Information("Rear of train slipped off table");
                            }

                            if (trainOnTable.FrontOnBoard && trainOnTable.BackOnBoard)
                            {
                                parentTrain.ClearActiveSectionItems();   // release all track sections
                                MovingTableState = MovingTableState.AccessOnMovingTable;
                                parentTurntable.TrainsOnMovingTable.Add(trainOnTable);
                                parentTurntable.ComputeTrainPosition(parentTrain);
                            }
                        }
                    }

                    break;

                // state : moving onto turntable (from Storage)
                case MovingTableState.StorageToMovingTable:

                    // set end of authority beyond turntable
                    parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.EndOfPath;
                    parentTrain.EndAuthorities[Direction.Forward].Distance = CalculateDistanceToTurntable() + (parentTurntable.Length / 2.0f);

                    // check if train position on turntable
                    if (!trainOnTable.FrontOnBoard)
                    {
                        if (parentTurntable.CheckOnSection(parentTrain.FrontTDBTraveller))
                        {
                            trainOnTable.SetFrontState(true);
                            Simulator.Instance.Confirmer.Information("Front of train is on table");

                        }
                    }
                    else if (!trainOnTable.BackOnBoard)
                    {
                        if (parentTurntable.CheckOnSection(parentTrain.RearTDBTraveller))
                        {
                            trainOnTable.SetBackState(true);
                            Simulator.Instance.Confirmer.Information("Rear of train is on table");
                            Simulator.Instance.Confirmer.Information("Stop train, set throttle to zero, set reverser to neutral");
                        }
                    }
                    else if (parentTrain.SpeedMpS < 0.05f)
                    {
                        parentTrain.ControlMode = TrainControlMode.TurnTable;

                        var loco = parentTrain.LeadLocomotive;
                        if (loco.ThrottlePercent < 1 && Math.Abs(loco.SpeedMpS) < 0.05 && (loco.Direction == MidpointDirection.N || Math.Abs(parentTrain.MUReverserPercent) <= 1))
                        {
                            // check if train still on turntable
                            if (!parentTurntable.CheckOnSection(parentTrain.FrontTDBTraveller))
                            {
                                trainOnTable.SetFrontState(false);
                                Simulator.Instance.Confirmer.Information("Front of train slipped off table");
                            }
                            if (!parentTurntable.CheckOnSection(parentTrain.RearTDBTraveller))
                            {
                                trainOnTable.SetBackState(false);
                                Simulator.Instance.Confirmer.Information("Rear of train slipped off table");
                            }

                            if (trainOnTable.FrontOnBoard && trainOnTable.BackOnBoard)
                            {
                                parentTrain.ClearActiveSectionItems();   // release all track sections
                                MovingTableState = MovingTableState.StorageOnMovingTable;
                                parentTurntable.TrainsOnMovingTable.Add(trainOnTable);
                                parentTurntable.ComputeTrainPosition(parentTrain);
                            }
                        }
                    }

                    break;

                // state : turning on turntable (from Access)
                // get required exit
                // exit from this state is through PrepareMoveOffTable 
                case MovingTableState.AccessOnMovingTable:

                    reqTurntableEntry = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    reqTurntableExit = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqExitDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds);
                    break;

                // state : turning on turntable (from Storage)
                // get required exit
                // exit from this state is through PrepareMoveOffTable
                case MovingTableState.StorageOnMovingTable:

                    reqTurntableEntry = parentPool.StoragePool[StoragePathIndex].TableExitIndex;
                    reqEntryDirection = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction;

                    reqTurntableExit = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableExitIndex;
                    reqExitDirection = parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction;

                    AutoRequestExit(reqTurntableExit, reqEntryDirection, reqExitDirection, elapsedClockSeconds);
                    break;

                default:
                    break;
            }

            return (terminated);
        }

        /// <summary>
        /// Get access path index from present position of train
        /// </summary>
        /// <returns></returns>
        public int GetAccessPathIndex()
        {
            int presentSection = parentTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            TrackDirection presentDirection = parentTrain.PresentPosition[Direction.Forward].Direction;

            // search if section in access path
            // direction must be reverse as access path is defined outbound
            int reqPath = -1;
            for (int iPath = 0; iPath <= parentPool.AdditionalTurntableDetails.AccessPaths.Count - 1 && reqPath < 0; iPath++)
            {
                int routeIndex = parentPool.AdditionalTurntableDetails.AccessPaths[iPath].AccessPath.GetRouteIndex(presentSection, 0);
                if (routeIndex >= 0 && parentPool.AdditionalTurntableDetails.AccessPaths[iPath].AccessPath[routeIndex].Direction != presentDirection)
                {
                    reqPath = iPath;
                }
            }

            return (reqPath);
        }

        /// <summary>
        /// Turn turntable to required exit position
        /// </summary>
        public bool AutoRequestExit(int reqExit, Direction entryPathDirection, Direction exitPathDirection,
                        double elapsedClockSeconds)
        {
            Simulator simulator = Simulator.Instance;
            // if turntable is moving, always return false
            if (parentTurntable.AutoRotationDirection != Rotation.None)
            {
                parentTurntable.AutoRotateTable(elapsedClockSeconds);

                // in prerun, also perform turntable update as simulation is not yet running
                // also perform turntable update if this is not the active moving table
                bool performUpdate = simulator.PreUpdate;
                if (!performUpdate)
                {
                    if (simulator.ActiveMovingTable == null)
                    {
                        performUpdate = true;
                    }
                    else
                    {
                        performUpdate = simulator.ActiveMovingTable.WFile != parentTurntable.WFile ||
                                        simulator.ActiveMovingTable.UID != parentTurntable.UID;
                    }
                }

                if (performUpdate)
                {
                    parentTurntable.Update();
                }
                return (false);
            }

            // if connected and connection is required exit, return true
            if ((parentTurntable.ForwardConnected || parentTurntable.RearConnected) && parentTurntable.ConnectedTrackEnd == reqExit)
            {
                // if train on table, prepare to move off table
                if (parentTurntable.TrainsOnMovingTable.Count > 0)
                {
                    PrepareMoveOffTable();
                }
                return (true);
            }

            // if not moving and not connected, start movement
#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("Pool {0} - Train {1} [{2}]",parentPool.PoolName, parentTrain.Name, parentTrain.Number.ToString());
#endif

            parentTurntable.SendNotifications = false;
            reqTurntableExit = reqExit;

            // find out direction to move
            (float startAngle, float endAngle) = parentTurntable.FindConnectingDirections(reqExit);
            float angleToMove = (endAngle - startAngle) % (float)Math.PI;
            float halfPi = MathHelper.PiOver2;
            bool exitForward = parentTurntable.TrackNodeOrientation(reqExit);
            bool entryForward = parentTurntable.TrackNodeOrientation(parentTurntable.ConnectedTrackEnd);

            bool reqChangeEnd = false;

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("   Exit information : start Exit - Angle {0} - {1} (front : {2} ), end Exit - angle {3} - {4}, angle to move : {5}",
                parentTurntable.ConnectedTrackEnd, startAngle, parentTurntable.ForwardConnected.ToString(), reqExit, endAngle, angleToMove.ToString());
            Trace.TraceInformation("             entry orientation forward : {0} , exit orientation forward : {1}", entryForward.ToString(), exitForward.ToString());
#endif

            // empty turntable : rotate required exit over smallest angle
            if (parentTurntable.TrainsOnMovingTable.Count <= 0)
            {
#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   Empty turntable");
#endif

                if (angleToMove <= -halfPi)
                {
                    parentTurntable.AutoRotationDirection = Rotation.CounterClockwise;
                    reqChangeEnd = true;
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("   angle <= - halfPi");
#endif
                }
                else if (angleToMove < 0)
                {
                    parentTurntable.AutoRotationDirection = Rotation.CounterClockwise;
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("   -halfPi < angle < 0");
#endif
                }

                else if (angleToMove < halfPi)
                {
                    parentTurntable.AutoRotationDirection = Rotation.Clockwise;
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("   0 < angle < halfPi");
#endif
                }
                else
                {
                    parentTurntable.AutoRotationDirection = Rotation.Clockwise;
                    reqChangeEnd = true;
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("   halfPi <= angle");
#endif
                }

#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   Angle calculation : Rotation Clockwise : {0}, Counterclockwise : {1}, Change end required : {2}",
                    parentTurntable.AutoClockwise.ToString(), parentTurntable.AutoCounterclockwise.ToString(), reqChangeEnd.ToString());
#endif

#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   Exit orientation and path direction : exit forward : {0} , path direction : {1}",
                                  exitForward.ToString(), exitPathDirection.ToString());
#endif

                // reverse rotation if required
                if (reqChangeEnd)
                {
                    parentTurntable.AutoRotationDirection = parentTurntable.AutoRotationDirection.Reverse();
                }

                // set required end
                if ((parentTurntable.ForwardConnected && !reqChangeEnd) || (parentTurntable.RearConnected && reqChangeEnd))
                {
                    parentTurntable.ForwardConnectedTarget = reqExit;
                    parentTurntable.RearConnectedTarget = -1;
                }
                else
                {
                    parentTurntable.RearConnectedTarget = reqExit;
                    parentTurntable.ForwardConnectedTarget = -1;
                }

#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   Final setting empty table : Forward connected : {0}, Rear connected {1} , Clockwise : {2}, Counterclockwise : {3}\n",
                            parentTurntable.ForwardConnectedTarget, parentTurntable.RearConnectedTarget, 
                            parentTurntable.AutoClockwise.ToString(), parentTurntable.AutoCounterclockwise.ToString());
#endif
                return (false);
            }

            // turntable has train
#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("   Train on turntable");
#endif

            // find out if train needs to reverse
            reqReverseFormation = TestTrainFormation(parentTrain);

            // if reverse formation not required, reverse connection
            if (!reqReverseFormation)
            {
                reqChangeEnd = !reqChangeEnd;
#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("    reverse formation not required, Change end required {0}", reqChangeEnd.ToString());
#endif
            }

            // rotate clockwise or counterclockwise depending on angle
            if (angleToMove < 0)
            {
                parentTurntable.AutoRotationDirection = Rotation.CounterClockwise;
#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   angle < 0");
#endif
            }
            else if (angleToMove > 0)
            {
                parentTurntable.AutoRotationDirection = Rotation.Clockwise;
#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   angle > 0");
#endif
            }

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("   Angle calculation : Rotation Clockwise : {0}, Counterclockwise : {1}, Change end required : {2}",
                parentTurntable.AutoClockwise.ToString(), parentTurntable.AutoCounterclockwise.ToString(), reqChangeEnd.ToString());
#endif

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("   Entry orientation and path direction : entry forward : {0} , path direction : {1}",
                              entryForward.ToString(), entryPathDirection.ToString());
#endif

            // if entry orientation does not match tracknode direction, entry direction must be reversed
            // orientation is true : tracknode direction is away from turntable so traveller direction must be forward
            // orientation is false : tracknode direction is toward turntable so traveller direction must be backward
            if ((!entryForward && entryPathDirection == Direction.Backward) || (entryForward && entryPathDirection == Direction.Forward))
            {
                reqChangeEnd = !reqChangeEnd;
#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   Entry has reversed orientation : change end required : {0}", reqChangeEnd.ToString());
#endif
            }

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("   Exit orientation and path direction : exit forward : {0} , path direction : {1}",
                              exitForward.ToString(), exitPathDirection.ToString());
#endif

            // if exit orientation does not match tracknode direction, exit direction must be reversed
            // orientation is true : tracknode direction is away from turntable so traveller direction must be forward
            // orientation is false : tracknode direction is toward turntable so traveller direction must be backward
            if ((!exitForward && exitPathDirection == Direction.Backward) || (exitForward && exitPathDirection == Direction.Forward))
            {
                reqChangeEnd = !reqChangeEnd;
#if DEBUG_TURNTABLEINFO
                Trace.TraceInformation("   Exit has reversed orientation : change end required : {0}", reqChangeEnd.ToString());
#endif
            }

            // reverse rotation if required
            if (reqChangeEnd)
            {
                parentTurntable.AutoRotationDirection = parentTurntable.AutoRotationDirection.Reverse();
            }

            // set required end
            if ((parentTurntable.ForwardConnected && !reqChangeEnd) || (parentTurntable.RearConnected && reqChangeEnd))
            {
                parentTurntable.ForwardConnectedTarget = reqExit;
                parentTurntable.RearConnectedTarget = -1;
            }
            else
            {
                parentTurntable.RearConnectedTarget = reqExit;
                parentTurntable.ForwardConnectedTarget = -1;
            }

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("   Final setting loaded table : Forward connected : {0}, Rear connected {1} , Clockwise : {2}, Counterclockwise : {3}\n",
                        parentTurntable.ForwardConnectedTarget, parentTurntable.RearConnectedTarget,
                        parentTurntable.AutoClockwise.ToString(), parentTurntable.AutoCounterclockwise.ToString());
#endif

            return (false);
        }

        /// <summary>
        /// Calculate distance to position in middle of turntable
        /// </summary>
        public float CalculateDistanceToTurntable()
        {
            float remDistance = 0.0f;

            // check present position is in last section of route
            if (parentTrain.PresentPosition[Direction.Forward].RouteListIndex < parentTrain.ValidRoutes[Direction.Forward].Count - 1)
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[parentTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex];
                remDistance = thisSection.Length - parentTrain.PresentPosition[Direction.Forward].Offset;

                for (int iIndex = parentTrain.PresentPosition[Direction.Forward].RouteListIndex + 1; iIndex < parentTrain.ValidRoutes[Direction.Forward].Count - 1; iIndex++)
                {
                    remDistance += parentTrain.ValidRoutes[Direction.Forward][iIndex].TrackCircuitSection.Length;
                }

                if (MovingTableState == MovingTableState.StorageToMovingTable)
                {
                    remDistance += parentPool.StoragePool[StoragePathIndex].TableMiddleEntry + parentTrain.Length / 2.0f;
                }
                else if (MovingTableState == MovingTableState.AccessToMovingTable)
                {
                    remDistance += parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleEntry + parentTrain.Length / 2.0f;
                }
            }
            // train in same section as turntable
            else
            {
                if (MovingTableState == MovingTableState.StorageToMovingTable)
                {
                    remDistance += parentPool.StoragePool[StoragePathIndex].TableMiddleEntry - parentTrain.PresentPosition[Direction.Forward].Offset + parentTrain.Length / 2.0f;
                }
                else if (MovingTableState == MovingTableState.AccessToMovingTable)
                {
                    remDistance += parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleEntry - parentTrain.PresentPosition[Direction.Forward].Offset + parentTrain.Length / 2.0f;
                }
            }

            return (remDistance);
        }

        /// <summary>
        /// Set next stage in process when train is stopped after moving
        /// </summary>
        public void SetNextStageOnStopped()
        {
            bool trainOnTable = false;

            switch (MovingTableState)
            {
                case MovingTableState.AccessToMovingTable:
                    MovingTableState = MovingTableState.AccessOnMovingTable;
                    trainOnTable = true;
                    break;

                case MovingTableState.StorageToMovingTable:
                    MovingTableState = MovingTableState.StorageOnMovingTable;
                    trainOnTable = true;
                    break;

                default:
                    break;
            }

            if (trainOnTable)
            {
                SetTrainOnTable();
            }
        }

        /// <summary>
        /// Place train on turntable
        /// </summary>
        public void SetTrainOnTable()
        {
            // ensure train is not moving
            if (parentTrain.SpeedMpS > 0 || parentTrain.SpeedMpS < 0)   // if train still running force it to stop
            {
                parentTrain.SpeedMpS = 0;
                foreach (var Car in parentTrain.Cars)
                {
                    Car.SpeedMpS = 0;
                }

                parentTrain.Update(0);   // stop the wheels from moving etc
                parentTrain.AITrainThrottlePercent = 0;
                parentTrain.AITrainBrakePercent = 100;
            }

            // get actual stop position
            if (MovingTableAction == MovingTableAction.FromAccess)
            {
                float trainOffset = parentTrain.RearTDBTraveller.TrackNodeOffset;
                stopPositionOnTurntableM = trainOffset + (parentTrain.Length / 2.0f) - parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleEntry;
            }
            else if (MovingTableAction == MovingTableAction.FromStorage)
            {
                float trainOffset = parentTrain.RearTDBTraveller.TrackNodeOffset;
                stopPositionOnTurntableM = trainOffset + (parentTrain.Length / 2.0f) - parentPool.StoragePool[StoragePathIndex].TableMiddleEntry;
            }

            // clear approach route
            parentTrain.RemoveFromTrack();
            foreach (DistanceTravelledItem thisAction in parentTrain.RequiredActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearSectionItem thisItem = thisAction as ClearSectionItem;
                    TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisItem.TrackSectionIndex];
                    thisSection.ClearOccupied(parentTrain, true);
                }
            }

            parentTrain.ResetActions(false);
            parentTrain.DelayedStartMoving(AiStartMovement.Turntable);

            // set train on table
            parentTrain.ControlMode = TrainControlMode.TurnTable;

            TrainOnMovingTable trainOnTable = new TrainOnMovingTable(parentTrain);
            trainOnTable.SetFrontState(true);
            trainOnTable.SetBackState(true);

            parentTurntable.TrainsOnMovingTable.Add(trainOnTable);
            parentTurntable.ComputeTrainPosition(parentTrain);
        }

        /// <summary>
        /// Prepare train to move off turntable
        /// </summary>
        public void PrepareMoveOffTable()
        {

            // set next active path for train
            parentTrain.TCRoute.ActiveSubPath++;
            parentTrain.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(parentTrain.TCRoute.TCRouteSubpaths[parentTrain.TCRoute.ActiveSubPath]);

            // check if formation reverse is required
            bool reverseFormation = reqReverseFormation;

            // position reverse is required if path off turntable is backward
            bool reversePosition = false;
            if (parentTrain.ValidRoutes[Direction.Forward][0].Direction == 0)
            {
                reversePosition = true;
                reverseFormation = !reverseFormation;  // inverse reverse formation as this is also done in reverseposition
            }

            // reverse formation if required
            if (reverseFormation)
                parentTrain.ReverseCars();

            // get traveller at start of path tracknode
            TrackCircuitSection thisSection = parentTrain.ValidRoutes[Direction.Forward][0].TrackCircuitSection;
            Traveller middlePosition = new Traveller(RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[thisSection.OriginalIndex]);

#if DEBUG_TURNTABLEINFO
            Trace.TraceInformation("Pool {0} - Train {1} [{2}] : calculating middle position for state : {3} , orientation : {4}",
                parentPool.PoolName, parentTrain.Name, parentTrain.Number, MovingTableState.ToString(),
                parentTurntable.MyTrackNodesOrientation[reqTurntableExit].ToString());
#endif

            // get position of front and rear of train in present tracknode
            if (MovingTableState == MovingTableState.StorageOnMovingTable)
            {
                if (parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].AccessTraveller.Direction == Direction.Forward)
                {
                    middlePosition.Move(parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleExit);
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("    used correction : {0} , resulting middle offset : TN : {1} , offset : {2} (off length : {3} ), Vector : {4}",
                        parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleExit,
                        middlePosition.TrackNodeIndex, middlePosition.TrackNodeOffset, middlePosition.TrackNodeLength, middlePosition.TrackVectorSectionIndex);
#endif
                }
                else
                {
                    // if tracknode direction through turntable is backward, use opposite position
                    middlePosition.Move(parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleEntry);
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("    used correction : {0} , resulting middle offset : TN : {1} , offset : {2} (off length : {3} ), Vector : {4}",
                        parentPool.AdditionalTurntableDetails.AccessPaths[AccessPathIndex].TableMiddleExit,
                        middlePosition.TrackNodeIndex, middlePosition.TrackNodeOffset, middlePosition.TrackNodeLength, middlePosition.TrackVectorSectionIndex);
#endif
                }
            }
            else if (MovingTableState == MovingTableState.AccessOnMovingTable)
            {
                if (parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.Direction == Direction.Forward)
                {
                    middlePosition.Move(parentPool.StoragePool[StoragePathIndex].TableMiddleExit);
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("    used correction : {0} , resulting middle offset : TN : {1} , offset : {2} (off length : {3} ), Vector : {4}",
                        parentPool.StoragePool[StoragePathIndex].TableMiddleExit,
                        middlePosition.TrackNodeIndex, middlePosition.TrackNodeOffset, middlePosition.TrackNodeLength, middlePosition.TrackVectorSectionIndex);
#endif
                }
                else
                {
                    // if tracknode direction through turntable is backward, use opposite position
                    middlePosition.Move(parentPool.StoragePool[StoragePathIndex].TableMiddleEntry);
#if DEBUG_TURNTABLEINFO
                    Trace.TraceInformation("    used correction : {0} , resulting middle offset : TN : {1} , offset : {2} (off length : {3} ), Vector : {4}",
                        parentPool.StoragePool[StoragePathIndex].TableMiddleEntry,
                        middlePosition.TrackNodeIndex, middlePosition.TrackNodeOffset, middlePosition.TrackNodeLength, middlePosition.TrackVectorSectionIndex);
#endif
                }
            }

            parentTrain.RearTDBTraveller = new Traveller(middlePosition);
            float offsetPosition = reverseFormation ? (-parentTrain.Length / 2.0f) - stopPositionOnTurntableM : (-parentTrain.Length / 2.0f) + stopPositionOnTurntableM;
            parentTrain.RearTDBTraveller.MoveInSection(offsetPosition);
            parentTrain.FrontTDBTraveller = new Traveller(parentTrain.RearTDBTraveller);
            parentTrain.FrontTDBTraveller.MoveInSection(parentTrain.Length);

            // place train
            parentTrain.InitialTrainPlacement();
            if (reversePosition)
            {
                parentTrain.ReverseFormation(false);
            }

            // reinitiate train
            MovingTableState = MovingTableState.Completed;
            parentTrain.MovementState = AiMovementState.Static;
            parentTrain.ControlMode = TrainControlMode.AutoNode;
            parentTrain.DistanceTravelledM = 0;
            parentTrain.DelayedStartMoving(AiStartMovement.PathAction);
            parentTrain.EndAuthorities[Direction.Forward].EndAuthorityType = EndAuthorityType.NoPathReserved;
            parentTrain.EndAuthorities[Direction.Backward].EndAuthorityType = EndAuthorityType.NoPathReserved;

            // actions for mode access (train going into storage)
            if (MovingTableAction == MovingTableAction.FromAccess)
            {
                // set terminate
                parentTrain.Closeup = true;
                parentTrain.FormsStatic = true;

                // calculate stop position
                float endOffset = parentPool.StoragePool[StoragePathIndex].StoragePathTraveller.TrackNodeOffset + parentTrain.Length;
                if (endOffset < parentTrain.EndAuthorities[Direction.Forward].Distance)
                {
                    parentTrain.EndAuthorities[Direction.Forward].Distance = parentTrain.NextStopDistanceM = endOffset;
                }
            }

            // actions for mode storage (train going out of storage)
            else
            {
                // create end of route action
                parentTrain.SetEndOfRouteAction();
            }

            // set distance for train to clear table
            clearingDistanceM = (parentTrain.Length / 2f) + (parentTurntable.Length / 2f) + parentPool.AdditionalTurntableDetails.TurntableReleaseClearanceM;

            // create action for clearing turntable
            ClearMovingTableAction newAction = new ClearMovingTableAction(clearingDistanceM, originalTrainMaxSpeedMpS);
            parentTrain.RequiredActions.InsertAction(newAction);
        }

        /// <summary>
        /// Remove train from turntable, return train to normal state
        /// </summary>
        public void RemoveTrainFromTurntable()
        {
            // clear table
            parentTurntable ??= Simulator.Instance.MovingTables[parentIndex] as TurnTable;

            parentTurntable.TrainsOnMovingTable.Clear();
            parentTurntable.InUse = false;
            parentTurntable.GoToAutoTarget = false;
            trainOnTable = null;

            // reset train speed
            parentTrain.TrainMaxSpeedMpS = originalTrainMaxSpeedMpS;
            ActivateSpeedLimit activeSpeeds = new ActivateSpeedLimit(0.0f, originalSpeedLimitMpS, originalSpeedSignalMpS, originalSpeedLimitMpS);

            if (parentTrain.TrainType == TrainType.Player)
            {
                parentTrain.SetPendingSpeedLimit(activeSpeeds);
            }
            else
            {
                parentTrain.SetAIPendingSpeedLimit(activeSpeeds);
            }
        }

        public bool TestTrainFormation(TTTrain parentTrain)
        {
            bool reqReverse = true;

            // get present train direction
            bool nowBackward = parentTrain.Cars[0].Flipped;

            if (MovingTableAction == MovingTableAction.FromAccess)
            {
                if (nowBackward && parentTrain.PoolExitDirection == PoolExitDirection.Backward)
                {
                    reqReverse = false;
                }
                else if (!nowBackward && parentTrain.PoolExitDirection == PoolExitDirection.Forward)
                {
                    reqReverse = false;
                }
            }
            else
            {
                if (nowBackward && parentTrain.CreatePoolDirection == PoolExitDirection.Backward)
                {
                    reqReverse = false;
                }
                else if (!nowBackward && parentTrain.CreatePoolDirection == PoolExitDirection.Forward)
                {
                    reqReverse = false;
                }
            }

            return (reqReverse);
        }
    }
}
