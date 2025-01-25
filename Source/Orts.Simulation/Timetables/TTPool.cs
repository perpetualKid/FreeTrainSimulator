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
// #DEBUG_POOLINFO
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts;
using Orts.Formats.OpenRails.Parsers;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// class Poolholder
    /// Interface class for access from Simulator
    /// </summary>
    public class Poolholder
        : ISaveStateRestoreApi<TimetablePoolSaveState, TimetablePool>
    {
        public Dictionary<string, TimetablePool> Pools { get; }

        /// <summary>
        /// loader for timetable mode
        /// </summary>
        public Poolholder(string fileName, CancellationToken cancellationToken): this()
        {
            // process pools
            PoolInfo.ProcessPools(fileName, Pools, cancellationToken);

            // process turntables
            Dictionary<string, TimetableTurntablePool> TTTurntables = TurntableInfo.ProcessTurntables(fileName, cancellationToken);

            // add turntables to poolholder
            foreach (KeyValuePair<string, TimetableTurntablePool> turntable in TTTurntables)
            {
                Pools.Add(turntable.Key, turntable.Value);
            }
        }

        //================================================================================================//
        /// <summary>
        /// loader for activity mode (dummy)
        /// </summary>
        public Poolholder()
        {
            Pools = new Dictionary<string, TimetablePool>();
        }

        TimetablePool ISaveStateRestoreApi<TimetablePoolSaveState, TimetablePool>.CreateRuntimeTarget(TimetablePoolSaveState saveState)
        {
            return saveState.PoolType switch
            {
                TimetablePoolType.TimetablePool => new TimetablePool(),
                TimetablePoolType.TimetableTurntablePool => new TimetableTurntablePool(),
                _ => throw new NotImplementedException(),
            };
        }
    }

    /// <summary>
    /// Class TimetablePool
    /// Class holding all pool details
    /// </summary>
    public class TimetablePool : ISaveStateApi<TimetablePoolSaveState>
    {
        public enum TrainFromPool
        {
            NotCreated,
            Delayed,
            Formed,
            ForceCreated,
            Failed,
        }

        public string PoolName { get; private protected set; } = string.Empty;
        private protected bool forceCreation;

        public List<PoolDetails> StoragePool { get; private set; } = new List<PoolDetails>();

        //================================================================================================//
        /// <summary>
        /// Empty constructor for use by children
        /// </summary>
        /// <param name="filePath"></param>
        public TimetablePool()
        {
        }

        //================================================================================================//
        /// <summary>
        /// Constructor to read pool info from csv file
        /// </summary>
        /// <param name="filePath"></param>
        public TimetablePool(TimetableReader fileContents, ref int lineindex)
        {
            bool validpool = true;
            bool newName = false;
            bool firstName = false;

            forceCreation = true; // Simulator.Instance.Settings.TTCreateTrainOnPoolUnderflow;

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
                            PoolDetails thisPool = ExtractStorage(fileContents, ref lineindex, out validStorage, true);
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

            // reset poolname if not valid
            if (!validpool)
            {
                PoolName = string.Empty;
            }
        }

        public virtual async ValueTask<TimetablePoolSaveState> Snapshot()
        {
            return new TimetablePoolSaveState()
            {
                PoolType = TimetablePoolType.TimetablePool,
                PoolName = PoolName,
                ForceCreation = forceCreation,
                PoolDetails = new Collection<TimetablePoolDetailSaveState>(await Task.WhenAll(StoragePool.Select(async storagePool => await storagePool.Snapshot().ConfigureAwait(false)).ToList()).ConfigureAwait(false)),
            };
        }

        public virtual async ValueTask Restore(TimetablePoolSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            PoolName = saveState.PoolName;
            forceCreation = saveState.ForceCreation;

            StoragePool = (await Task.WhenAll(saveState.PoolDetails.Select(async poolDetailState =>
            {
                PoolDetails poolDetails = new PoolDetails();
                await poolDetails.Restore(poolDetailState).ConfigureAwait(false);
                return poolDetails;
            })).ConfigureAwait(false)).ToList();
        }

        //================================================================================================//
        /// <summary>
        /// Extract details for storage area
        /// </summary>
        /// <param name="fileContents"></param>
        /// <param name="lineindex"></param>
        /// <param name="simulatorref"></param>
        /// <param name="validStorage"></param>
        /// <returns></returns>
        public PoolDetails ExtractStorage(TimetableReader fileContents, ref int lineindex, out bool validStorage, bool reqAccess)
        {
            PoolDetails newPool = new PoolDetails();
            List<string> accessPathNames = new List<string>();

            string[] inputLine = fileContents.Strings[lineindex];
            string storagePathName = inputLine[1];

            int? maxStoredUnits = null;

            lineindex++;
            inputLine = fileContents.Strings[lineindex];

            bool endOfStorage = false;
            validStorage = true;

            // extract access paths
            while (lineindex < fileContents.Strings.Count && !endOfStorage)
            {
                inputLine = fileContents.Strings[lineindex];
                switch (inputLine[0].ToLower().Trim())
                {
                    // skip comment
                    case "#comment":
                        lineindex++;
                        break;

                    // exit on next name
                    case "#name":
                        endOfStorage = true;
                        break;

                    // storage : next storage area
                    case "#storage":
                        endOfStorage = true;
                        break;

                    // maxstorage : set max storage for this storage track
                    case "#maxunits":
                        try
                        {
                            maxStoredUnits = Convert.ToInt32(inputLine[1]);
                        }
                        catch
                        {
                            Trace.TraceInformation("Invalid value for maxunits : {0} for storage {1} in pool {2} ; definition ignored", inputLine, storagePathName, PoolName);
                            maxStoredUnits = null;
                        }
                        lineindex++;
                        break;

                    // access paths : process
                    case "#access":
                        int nextfield = 1;

                        while (nextfield < inputLine.Length && !String.IsNullOrEmpty(inputLine[nextfield]))
                        {
                            accessPathNames.Add(inputLine[nextfield]);
                            nextfield++;
                        }

                        lineindex++;
                        break;

                    // settings : check setting
                    case "#settings":
                        nextfield = 1;
                        while (nextfield < inputLine.Length)
                        {
                            if (!String.IsNullOrEmpty(inputLine[nextfield]))
                            {
                                switch (inputLine[nextfield].ToLower().Trim())
                                {
                                    default:
                                        break;
                                }
                            }
                            nextfield++;
                        }
                        lineindex++;
                        break;

                    default:
                        Trace.TraceInformation("Pool : " + fileContents.FilePath + " : line : " + lineindex + " : unknown definition : " + inputLine[0] + " ; line ignored \n");
                        lineindex++;
                        break;
                }
            }

            // check if access paths defined
            if (reqAccess && accessPathNames.Count <= 0)
            {
                Trace.TraceInformation("Pool : " + fileContents.FilePath + " : storage : " + storagePathName + " : no access paths defined \n");
                validStorage = false;
                return (newPool);
            }

            // process storage paths
            newPool.AccessPaths = new List<TrackCircuitPartialPathRoute>();
            newPool.StoredUnits = new List<int>();
            newPool.ClaimUnits = new List<int>();
            newPool.StorageLength = 0.0f;
            newPool.RemainingLength = 0.0f;
            newPool.MaxStoredUnits = maxStoredUnits;

            bool pathValid = true;
            TimetableInfo TTInfo = new TimetableInfo();
            AIPath newPath = TTInfo.LoadPath(storagePathName, out pathValid);

            if (pathValid)
            {
                TrackCircuitRoutePath fullRoute = new TrackCircuitRoutePath(newPath, (TrackDirection)(-2), 1, -1);

                // front traveller
                newPool.StoragePath = new TrackCircuitPartialPathRoute(fullRoute.TCRouteSubpaths[0]);
                newPool.StoragePathTraveller = new Traveller(newPath.FirstNode.Location, newPath.FirstNode.NextMainNode.Location);
                // rear traveller (for moving tables)
                AIPathNode lastNode = newPath.Nodes.Last();
                newPool.StoragePathReverseTraveller = new Traveller(lastNode.Location, newPool.StoragePathTraveller.Direction.Reverse());

                Traveller dummy = new Traveller(newPool.StoragePathTraveller);
                dummy.Move(newPool.StoragePath[0].TrackCircuitSection.Length - newPool.StoragePathTraveller.TrackNodeOffset - 1.0f);
                newPool.StorageName = storagePathName;

                // if last element is end of track, remove it from path
                int lastSectionIndex = newPool.StoragePath[newPool.StoragePath.Count - 1].TrackCircuitSection.Index;
                if (TrackCircuitSection.TrackCircuitList[lastSectionIndex].CircuitType == TrackCircuitType.EndOfTrack)
                {
                    newPool.StoragePath.RemoveAt(newPool.StoragePath.Count - 1);
                }

                // check for multiple subpaths - not allowed for storage area
                if (fullRoute.TCRouteSubpaths.Count > 1)
                {
                    Trace.TraceInformation("Pool : " + fileContents.FilePath + " : storage area : " + storagePathName + " : storage path may not contain multiple subpaths\n");
                }
            }
            else
            {
                Trace.TraceWarning("Pool : " + fileContents.FilePath + " : error while processing storege area path : " + storagePathName + "\n");
                validStorage = false;
                return (newPool);
            }

            // process access paths
            foreach (string accessPath in accessPathNames)
            {
                pathValid = true;
                newPath = TTInfo.LoadPath(accessPath, out pathValid);

                if (pathValid)
                {
                    TrackCircuitRoutePath fullRoute = new TrackCircuitRoutePath(newPath, (TrackDirection)(-2), 1, -1);
                    // if last element is end of track, remove it from path
                    TrackCircuitPartialPathRoute usedRoute = fullRoute.TCRouteSubpaths[0];
                    int lastIndex = usedRoute.Count - 1;
                    if (usedRoute[lastIndex].TrackCircuitSection.CircuitType == TrackCircuitType.EndOfTrack)
                    {
                        lastIndex = usedRoute.Count - 2;
                    }
                    newPool.AccessPaths.Add(new TrackCircuitPartialPathRoute(usedRoute, 0, lastIndex));

                    // check for multiple subpaths - not allowed for storage area
                    if (fullRoute.TCRouteSubpaths.Count > 1)
                    {
                        Trace.TraceInformation("Pool : " + fileContents.FilePath + " : storage area : " + accessPath + " : access path may not contain multiple subpaths\n");
                    }
                }
                else
                {
                    Trace.TraceWarning("Pool : " + fileContents.FilePath + " : error while processing access path : " + accessPath + "\n");
                    validStorage = false;
                }
            }

            // verify proper access route definition

            if (!validStorage)
            {
                return (newPool);
            }

            for (int iPath = 0; iPath < newPool.AccessPaths.Count; iPath++)
            {
                TrackCircuitPartialPathRoute accessPath = newPool.AccessPaths[iPath];
                int firstAccessSection = accessPath[0].TrackCircuitSection.Index;
                TrackDirection firstAccessDirection = accessPath[0].Direction;
                string accessName = accessPathNames[iPath];

                int reqElementIndex = newPool.StoragePath.GetRouteIndex(firstAccessSection, 0);

                if (reqElementIndex < 0)
                {
                    Trace.TraceInformation("Pool : " + fileContents.FilePath + " : storage area : " + newPool.StorageName +
                        " : access path : " + accessName + " does not start within storage area\n");
                    validStorage = false;
                }
                else
                {
                    // check storage path direction, reverse path if required
                    // path may be in wrong direction due to path conversion problems
                    if (firstAccessDirection != newPool.StoragePath[reqElementIndex].Direction)
                    {
                        TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute();
                        for (int iElement = newPool.StoragePath.Count - 1; iElement >= 0; iElement--)
                        {
                            TrackCircuitRouteElement thisElement = newPool.StoragePath[iElement];
                            thisElement.Direction = thisElement.Direction.Reverse();
                            newRoute.Add(thisElement);
                        }
                        newPool.StoragePath = new TrackCircuitPartialPathRoute(newRoute);
                    }

                    // remove elements from access path which are part of storage path
                    int lastReqElement = accessPath.Count - 1;
                    int storageRouteIndex = newPool.StoragePath.GetRouteIndex(accessPath[lastReqElement].TrackCircuitSection.Index, 0);

                    while (storageRouteIndex >= 0 && lastReqElement > 0)
                    {
                        lastReqElement--;
                        storageRouteIndex = newPool.StoragePath.GetRouteIndex(accessPath[lastReqElement].TrackCircuitSection.Index, 0);
                    }

                    newPool.AccessPaths[iPath] = new TrackCircuitPartialPathRoute(accessPath, 0, lastReqElement);
                }
            }

            // calculate storage length
            if (!validStorage)
            {
                return (newPool);
            }
            float storeLength = 0;
            foreach (TrackCircuitRouteElement thisElement in newPool.StoragePath)
            {
                storeLength += thisElement.TrackCircuitSection.Length;
            }

            // if storage ends at switch, deduct switch safety distance

            float addedLength = 0;

            foreach (TrackCircuitRouteElement thisElement in newPool.StoragePath)
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    addedLength -= (float)thisSection.Overlap;
                    break;
                }
                else
                {
                    // count length only if not part of storage path itself
                    if (newPool.StoragePath.GetRouteIndex(thisSection.Index, 0) < 0)
                    {
                        addedLength += thisSection.Length;
                    }
                }
            }

            // if switch overlap exceeds distance between end of storage and switch, deduct from storage length
            if (addedLength < 0)
            {
                storeLength += addedLength;
            }

            newPool.StorageLength = storeLength;
            newPool.StorageCorrection = addedLength;
            newPool.RemainingLength = storeLength;

            return (newPool);
        }

        //================================================================================================//
        /// <summary>
        /// TestPoolExit : test if end of route is access to required pool
        /// </summary>
        /// <param name="train"></param>
        public virtual bool TestPoolExit(TTTrain train)
        {

            bool validPool = false;

            // set dispose states
            train.FormsStatic = true;
            train.Closeup = true;

            // find relevant access path
            int lastSectionIndex = train.TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
            TrackDirection lastSectionDirection = train.TCRoute.TCRouteSubpaths.Last().Last().Direction;

            // use first storage path to get pool access path

            PoolDetails thisStorage = StoragePool[0];
            int reqPath = -1;
            int reqPathIndex = -1;

            // find relevant access path
            for (int iPath = 0; iPath < thisStorage.AccessPaths.Count && reqPath < 0; iPath++)
            {
                TrackCircuitPartialPathRoute accessPath = thisStorage.AccessPaths[iPath];
                reqPathIndex = accessPath.GetRouteIndex(lastSectionIndex, 0);

                // path is defined outbound, so directions must be opposite
                if (reqPathIndex >= 0 && accessPath[reqPathIndex].Direction != lastSectionDirection)
                {
                    reqPath = iPath;
                }
            }

            // none found
            if (reqPath < 0)
            {
                Trace.TraceWarning("Train : " + train.Name + " : no valid path found to access pool storage " + PoolName + "\n");
                train.FormsStatic = false;
                train.Closeup = false;
            }
            // path found : extend train path with access and storage paths
            else
            {
                train.PoolAccessSection = lastSectionIndex;
                validPool = true;
            }

            return (validPool);
        }

        //================================================================================================//
        /// <summary>
        /// Create in pool : create train in pool
        /// </summary>
        /// <param name="train"></param>
        virtual public int CreateInPool(TTTrain train, List<TTTrain> nextTrains)
        {
            int PoolStorageState = (int)PoolAccessState.PoolInvalid;
            train.TCRoute.TCRouteSubpaths[0] = PlaceInPool(train, out PoolStorageState, false);
            train.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[0]);
            train.TCRoute.ActiveSubPath = 0;

            // if no storage available - abondone train
            if (PoolStorageState < 0)
            {
                return (PoolStorageState);
            }

            // use stored traveller
            train.PoolStorageIndex = PoolStorageState;
            train.RearTDBTraveller = new Traveller(StoragePool[train.PoolStorageIndex].StoragePathTraveller);

            // if storage available check for other engines on storage track
            if (StoragePool[train.PoolStorageIndex].StoredUnits.Count > 0)
            {
                int lastTrainNumber = StoragePool[train.PoolStorageIndex].StoredUnits[^1];
                TTTrain lastTrain = train.GetOtherTTTrainByNumber(lastTrainNumber);
                lastTrain ??= Simulator.Instance.GetAutoGenTTTrainByNumber(lastTrainNumber);
                if (lastTrain != null)
                {
                    train.CreateAhead = lastTrain.Name;
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
        /// Place in pool : place train in pool
        /// </summary>
        /// <param name="train"></param>
        virtual public TrackCircuitPartialPathRoute PlaceInPool(TTTrain train, out int poolStorageIndex, bool checkAccessPath)
        {
            TrackCircuitPartialPathRoute newRoute = SetPoolExit(train, out int tempIndex, checkAccessPath);
            poolStorageIndex = tempIndex;
            return newRoute;
        }

        //================================================================================================//
        /// <summary>
        /// SetPoolExit : adjust train dispose details and path to required pool exit
        /// Returned poolStorageState : <0 : state (enum TTTrain.PoolAccessState); >0 : poolIndex
        /// </summary>
        /// <param name="train"></param>
        public virtual TrackCircuitPartialPathRoute SetPoolExit(TTTrain train, out int poolStorageState, bool checkAccessPath)
        {
            // new route
            TrackCircuitPartialPathRoute newRoute = null;
            poolStorageState = (int)PoolAccessState.PoolInvalid;

            // set dispose states
            train.FormsStatic = true;
            train.Closeup = true;

            // find relevant access path
            int lastSectionIndex = train.TCRoute.TCRouteSubpaths.Last().Last().TrackCircuitSection.Index;
            TrackDirection lastSectionDirection = train.TCRoute.TCRouteSubpaths.Last().Last().Direction;

            // find storage path with enough space to store train

            poolStorageState = GetPoolExitIndex(train);

            // pool overflow
            if (poolStorageState == (int)PoolAccessState.PoolOverflow)
            {
                Trace.TraceWarning("Pool : " + PoolName + " : overflow : cannot place train : " + train.Name + "\n");

                // train will be abandoned when reaching end of path
                train.FormsStatic = false;
                train.Closeup = false;
            }

            // pool invalid
            else if (poolStorageState == (int)PoolAccessState.PoolInvalid)
            {
                Trace.TraceWarning("Pool : " + PoolName + " : no valid pool found : " + train.Name + "\n");

                // train will be abandoned when reaching end of path
                train.FormsStatic = false;
                train.Closeup = false;
            }

            // no action if state is poolClaimed - state will resolve as train ahead is stabled in pool


            // valid pool
            else if (poolStorageState >= 0)
            {
                PoolDetails thisStorage = StoragePool[poolStorageState];
                train.PoolStorageIndex = poolStorageState;

                if (checkAccessPath)
                {
                    int reqPath = -1;
                    int reqPathIndex = -1;

                    // find relevant access path
                    for (int iPath = 0; iPath < thisStorage.AccessPaths.Count && reqPath < 0; iPath++)
                    {
                        TrackCircuitPartialPathRoute accessPath = thisStorage.AccessPaths[iPath];
                        reqPathIndex = accessPath.GetRouteIndex(lastSectionIndex, 0);

                        // path is defined outbound, so directions must be opposite
                        if (reqPathIndex >= 0 && accessPath[reqPathIndex].Direction != lastSectionDirection)
                        {
                            reqPath = iPath;
                        }
                    }

                    // none found
                    if (reqPath < 0)
                    {
                        Trace.TraceWarning("Train : " + train.Name + " : no valid path found to access pool storage " + PoolName + "\n");
                        train.FormsStatic = false;
                        train.Closeup = false;
                        poolStorageState = -1;
                    }
                    // path found : extend train path with access and storage paths
                    else
                    {
                        TrackCircuitPartialPathRoute accessPath = thisStorage.AccessPaths[reqPath];
                        newRoute = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths.Last());

                        // add elements from access route except those allready on the path
                        // add in reverse order and reverse direction as path is defined outbound
                        for (int iElement = reqPathIndex; iElement >= 0; iElement--)
                        {
                            if (newRoute.GetRouteIndex(accessPath[iElement].TrackCircuitSection.Index, 0) < 0)
                            {
                                TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(accessPath[iElement]);
                                newElement.Direction = newElement.Direction.Reverse();
                                newRoute.Add(newElement);
                            }
                        }
                        // add elements from storage
                        for (int iElement = thisStorage.StoragePath.Count - 1; iElement >= 0; iElement--)
                        {
                            if (newRoute.GetRouteIndex(thisStorage.StoragePath[iElement].TrackCircuitSection.Index, 0) < 0)
                            {
                                TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(thisStorage.StoragePath[iElement]);
                                newElement.Direction = newElement.Direction.Reverse();
                                newRoute.Add(newElement);
                            }
                        }
                        // set pool claim
                        AddUnit(train, true);
                        thisStorage.ClaimUnits.Add(train.Number);
                    }
                }
                // create new route from storage and access track only
                else
                {
                    newRoute = new TrackCircuitPartialPathRoute(thisStorage.AccessPaths[0]);

                    foreach (TrackCircuitRouteElement thisElement in thisStorage.StoragePath)
                    {
                        if (newRoute.GetRouteIndex(thisElement.TrackCircuitSection.Index, 0) < 0)
                        {
                            TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(thisElement);
                            newRoute.Add(newElement);
                        }
                    }
                }
            }

            return (newRoute);
        }

        //================================================================================================//
        /// <summary>
        /// Base class to allow override for moving table classes
        /// </summary>

        internal virtual float GetEndOfRouteDistance(TrackCircuitPartialPathRoute thisRoute, TrackCircuitPosition frontPosition, int pathIndex)
        {
            return (0);
        }

        //================================================================================================//
        /// <summary>
        /// GetPoolExitIndex : get pool index for train exiting to pool
        /// Returned poolStorageState : <0 : state (enum TTTrain.PoolAccessState); >0 : poolIndex
        /// </summary>
        /// <param name="train"></param>
        public int GetPoolExitIndex(TTTrain train)
        {
            // find storage path with enough space to store train

            int reqPool = (int)PoolAccessState.PoolInvalid;
            for (int iPool = 0; iPool < StoragePool.Count && reqPool < 0; iPool++)
            {
                PoolDetails thisStorage = StoragePool[iPool];

                // check on max units on storage track
                bool maxUnitsReached = false;
                if (thisStorage.MaxStoredUnits.HasValue)
                {
                    maxUnitsReached = thisStorage.StoredUnits.Count >= thisStorage.MaxStoredUnits.Value;
                }

                // train already has claimed space
                if (thisStorage.ClaimUnits.Contains(train.Number))
                {
                    reqPool = iPool;
                }

                else if (thisStorage.StoredUnits.Contains(train.Number))
                {

#if DEBUG_POOLINFO
                    var sob = new StringBuilder();
                    sob.AppendFormat("Pool {0} : error : train {1} ({2}) allready stored in pool \n", PoolName, train.Number, train.Name);
                    sob.AppendFormat("           stored units : {0}", thisStorage.StoredUnits.Count);
                    File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
                }
                else if (thisStorage.RemainingLength > train.Length && !maxUnitsReached)
                {
                    reqPool = iPool;
                }
            }

            // if no valid pool found, check if any paths have a claimed train
            // else state is pool overflow
            if (reqPool < 0)
            {
                reqPool = (int)PoolAccessState.PoolOverflow;

                foreach (PoolDetails thisPool in StoragePool)
                {
                    if (thisPool.ClaimUnits.Count > 0)
                    {
                        reqPool = (int)PoolAccessState.PoolClaimed;
                        break;
                    }
                }
            }

            return (reqPool);
        }

        //================================================================================================//
        /// <summary>
        /// Test if route leads to pool
        /// </summary>

        public bool TestRouteLeadingToPool(TrackCircuitPartialPathRoute testedRoute, int poolIndex, string trainName)
        {
            TrackCircuitPartialPathRoute poolStorage = StoragePool[poolIndex].StoragePath;

            // check if signal route leads to pool
            foreach (TrackCircuitRouteElement routeElement in poolStorage)
            {
                if (testedRoute.GetRouteIndex(routeElement.TrackCircuitSection.Index, 0) > 0)
                {
                    return (true);
                }
            }

            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// AddUnit : add unit to pool, update remaining length
        /// </summary>
        /// <param name="train"></param>
        public void AddUnit(TTTrain train, bool claimOnly)
        {
            PoolDetails thisPool = StoragePool[train.PoolStorageIndex];

            // if train has already claimed position, remove claim
            if (thisPool.ClaimUnits.Contains(train.Number))
            {
                thisPool.ClaimUnits.Remove(train.Number);
                thisPool.RemainingLength = CalculateStorageLength(thisPool, train);
            }

            else
            {
                // add train to pool
                thisPool.StoredUnits.Add(train.Number);

                thisPool.RemainingLength = CalculateStorageLength(thisPool, train);
                StoragePool[train.PoolStorageIndex] = thisPool;

#if DEBUG_POOLINFO
                var sob = new StringBuilder();
                sob.AppendFormat("Pool {0} : train {1} ({2}) added\n", PoolName, train.Number, train.Name);
                sob.AppendFormat("           stored units : {0}\n", thisPool.StoredUnits.Count);
                File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
            }

            // update altered pool
            StoragePool[train.PoolStorageIndex] = thisPool;

            // if claim only, do not reset track section states
            if (claimOnly)
            {
                return;
            }

            // clear track behind engine, only keep actual occupied sections
            TrackCircuitPartialPathRoute tempRoute = SignalEnvironment.BuildTempRoute(train, train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex, train.PresentPosition[Direction.Backward].Offset,
                train.PresentPosition[Direction.Backward].Direction, train.Length, true, true, false);
            train.OccupiedTrack.Clear();

            foreach (TrackCircuitRouteElement thisElement in tempRoute)
            {
                train.OccupiedTrack.Add(thisElement.TrackCircuitSection);
            }

            train.ClearActiveSectionItems();
        }

        //================================================================================================//
        /// <summary>
        /// Calculate remaining storage length
        /// </summary>
        /// <param name="reqStorage"></param>
        /// <param name="train"></param> is last train in storage (one just added, or one remaining as previous last stored unit), = null if storage is empty
        /// <returns></returns>
        public float CalculateStorageLength(PoolDetails reqStorage, TTTrain train)
        {
            // no trains in storage
            if (reqStorage.StoredUnits.Count <= 0)
            {
                return (reqStorage.StorageLength);
            }

            // calculate remaining length
            int occSectionIndex = train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
            TrackDirection occSectionDirection = train.PresentPosition[Direction.Forward].Direction;
            int storageSectionIndex = reqStorage.StoragePath.GetRouteIndex(occSectionIndex, 0);

            // if train not stopped in pool, return remaining length = 0
            if (storageSectionIndex < 0)
            {
                return (0);
            }

            TrackDirection storageSectionDirection = reqStorage.StoragePath[storageSectionIndex].Direction;
            // if directions of paths are equal, use front section, section.length - position.offset, and use front of train position

            float remLength = 0;

            // same direction : use rear of train position
            // for turntable pools, path is defined in opposite direction

            if (this is TimetableTurntablePool)
            {
                // use rear of train position
                if (occSectionDirection == storageSectionDirection)
                {
                    occSectionIndex = train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                    TrackCircuitSection occSection = TrackCircuitSection.TrackCircuitList[occSectionIndex];
                    remLength = train.PresentPosition[Direction.Backward].Offset;
                }
                else
                // use front of train position
                {
                    TrackCircuitSection occSection = TrackCircuitSection.TrackCircuitList[occSectionIndex];
                    remLength = occSection.Length - train.PresentPosition[Direction.Forward].Offset;
                }
            }
            else
            {
                // use rear of train position
                if (occSectionDirection == storageSectionDirection)
                {
                    occSectionIndex = train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                    TrackCircuitSection occSection = TrackCircuitSection.TrackCircuitList[occSectionIndex];
                    remLength = occSection.Length - train.PresentPosition[Direction.Backward].Offset;
                }
                else
                // use front of train position
                {
                    remLength = train.PresentPosition[Direction.Forward].Offset;
                }
            }

            for (int iSection = reqStorage.StoragePath.Count - 1; iSection >= 0 && reqStorage.StoragePath[iSection].TrackCircuitSection.Index != occSectionIndex; iSection--)
            {
                remLength += reqStorage.StoragePath[iSection].TrackCircuitSection.Length;
            }

            // position was furthest down the storage area, so take off train length
            remLength -= train.Length;

            // correct for overlap etc.
            remLength += reqStorage.StorageCorrection;  // storage correction is negative!

            return (remLength);
        }

        //================================================================================================//
        /// <summary>
        /// Extract train from pool
        /// </summary>
        /// <param name="train"></param>
        /// <returns></returns>
        public virtual TrainFromPool ExtractTrain(ref TTTrain train, int presentTime)
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
                    selectedTrainNumber = thisStorage.StoredUnits[thisStorage.StoredUnits.Count - 1];
                    selectedStorage = iStorage;
                    break;
                }
            }

            if (selectedTrainNumber < 0)
            {
                // no train found but claim is active - create engine is delayed
                if (claimActive)
                {
                    return (TrainFromPool.Delayed);
                }

                // pool underflow : create engine from scratch
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
            PoolDetails reqStorage = StoragePool[selectedStorage];

            int reqAccessPath = -1;
            for (int iPath = 0; iPath < reqStorage.AccessPaths.Count; iPath++)
            {
                TrackCircuitPartialPathRoute thisPath = reqStorage.AccessPaths[iPath];
                if (thisPath.GetRouteIndex(firstSectionIndex, 0) >= 0)
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

            // if valid path found : build new path from storage area

            train.TCRoute.AddSectionsAtStart(reqStorage.AccessPaths[reqAccessPath], train, false);
            train.TCRoute.AddSectionsAtStart(reqStorage.StoragePath, train, false);

            // check all sections in route for engine heading for pool
            // if found, do not create engine as this may result in deadlock

            bool incomingEngine = false;
            foreach (TrackCircuitRouteElement thisElement in train.TCRoute.TCRouteSubpaths[0])
            {
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                // check reserved
                if (thisSection.CircuitState.TrainReserved != null)
                {
                    TTTrain otherTTTrain = thisSection.CircuitState.TrainReserved.Train as TTTrain;
                    if (string.Equals(otherTTTrain.ExitPool, PoolName, StringComparison.OrdinalIgnoreCase))
                    {
                        incomingEngine = true;
                        break;
                    }
                }

                // check claimed
                if (thisSection.CircuitState.TrainClaimed.Count > 0)
                {
                    foreach (Train.TrainRouted otherTrain in thisSection.CircuitState.TrainClaimed)
                    {
                        TTTrain otherTTTrain = otherTrain.Train as TTTrain;
                        if (string.Equals(otherTTTrain.ExitPool, PoolName, StringComparison.OrdinalIgnoreCase))
                        {
                            incomingEngine = true;
                            break;
                        }
                    }
                }
                if (incomingEngine)
                    break;

                // check occupied
                List<Train.TrainRouted> otherTrains = thisSection.CircuitState.TrainsOccupying();
                foreach (Train.TrainRouted otherTrain in otherTrains)
                {
                    TTTrain otherTTTrain = otherTrain.Train as TTTrain;
                    if (string.Equals(otherTTTrain.ExitPool, PoolName, StringComparison.OrdinalIgnoreCase) && otherTTTrain.MovementState != AiMovementState.Static)
                    {
                        incomingEngine = true;
#if DEBUG_POOLINFO
                        sob = new StringBuilder();
                        sob.AppendFormat("Pool {0} : train {1} ({2}) waiting for incoming train {3} ({4})\n", PoolName, train.Number, train.Name, otherTTTrain.Number, otherTTTrain.Name);
                        sob.AppendFormat("           stored units : {0}", reqStorage.StoredUnits.Count);
                        File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
                        break;
                    }
                }
                if (incomingEngine)
                    break;
            }

            // if incoming engine is approach, do not create train
            if (incomingEngine)
            {
                return (TrainFromPool.Delayed);
            }

            // valid engine found - start train from found engine

            TTTrain selectedTrain = train.GetOtherTTTrainByNumber(selectedTrainNumber);
            if (selectedTrain == null)
            {
#if DEBUG_POOLINFO
                sob = new StringBuilder();
                sob.AppendFormat("Pool {0} : cannot find train {1} for {2} ({3}) \n", PoolName, selectedTrainNumber, train.Number, train.Name);
                sob.AppendFormat("           stored units : {0}", reqStorage.StoredUnits.Count);
                File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
                return (TrainFromPool.Delayed);
            }

            TrackCircuitSection[] occupiedSections = new TrackCircuitSection[selectedTrain.OccupiedTrack.Count];
            selectedTrain.OccupiedTrack.CopyTo(occupiedSections);

            selectedTrain.Forms = -1;
            selectedTrain.RemoveTrain();
            train.FormedOfType = TimetableFormationCommand.TerminationFormed;
            train.ValidRoutes[Direction.Forward] = new TrackCircuitPartialPathRoute(train.TCRoute.TCRouteSubpaths[0]);

#if DEBUG_POOLINFO
            sob = new StringBuilder();
            sob.AppendFormat("Pool {0} : train {1} ({2}) extracted as {3} ({4}) \n", PoolName, selectedTrain.Number, selectedTrain.Name, train.Number, train.Name);
            sob.AppendFormat("           stored units : {0}", reqStorage.StoredUnits.Count);
            File.AppendAllText(@"C:\temp\PoolAnal.csv", sob.ToString() + "\n");
#endif
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
                    Simulator.Instance.Confirmer?.Information("Player switched to train : " + train.Name);// As Confirmer may not be created until after a restore.
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
                    Simulator.Instance.PlayerLocomotive = train.LeadLocomotive = train.Cars[0] as MSTSLocomotive ?? train.Cars[^1] as MSTSLocomotive ?? train.Cars.OfType<MSTSLocomotive>().FirstOrDefault();

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
                                loco.AntiSlip = train.LeadLocoAntiSlip;
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

                train.MovementState = AiMovementState.Static;
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
    }
}
