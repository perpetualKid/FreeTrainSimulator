// COPYRIGHT 2013 by the Open Rails project.
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

// This module covers all classes and code for signal, speed post, track occupation and track reservation control

// Debug flags :
// #define DEBUG_PRINT
// print details of train behaviour
// #define DEBUG_DEADLOCK
// print details of deadlock processing

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Simulation.Physics;
using Orts.Simulation.Track;

namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// DeadlockInfo Object
    ///
    /// </summary>
    //================================================================================================//

    internal class DeadlockInfo
    {
        public enum DeadlockTrainState                                    // state of train wrt this deadlock                     
        {
            KeepClearThisDirection,
            KeepClearReverseDirection,
            Approaching,
            StoppedAheadLoop,
            InLoop,
            StoppedInLoop,
        }

        private readonly int deadlockIndex;                                          // this deadlock unique index reference
        private int nextTrainSubpathIndex;                                 // counter for train/subpath index

        public List<DeadlockPathInfo> AvailablePathList { get; }                   // list of available paths
        public Dictionary<int, List<int>> PathReferences { get; }                  // list of paths per boundary section
        public Dictionary<int, List<int>> TrainReferences { get; }                 // list of paths as allowed per train/subpath index
        public Dictionary<int, Dictionary<int, bool>> TrainLengthFit { get; }      // list of length fit per train/subpath and per path
        public Dictionary<int, int> TrainOwnPath { get; }                          // train's own path per train/subpath
        public Dictionary<int, int> InverseInfo { get; }                           // list of paths which are each others inverse
        public Dictionary<int, Dictionary<int, int>> TrainSubpathIndex { get; }    // unique index per train and subpath

        //================================================================================================//
        /// <summary>
        /// Constructor
        /// </summary>

        public DeadlockInfo()
        {
            AvailablePathList = new List<DeadlockPathInfo>();
            PathReferences = new Dictionary<int, List<int>>();
            TrainReferences = new Dictionary<int, List<int>>();
            TrainLengthFit = new Dictionary<int, Dictionary<int, bool>>();
            TrainOwnPath = new Dictionary<int, int>();
            InverseInfo = new Dictionary<int, int>();
            TrainSubpathIndex = new Dictionary<int, Dictionary<int, int>>();
            nextTrainSubpathIndex = 0;

            Simulator.Instance.SignalEnvironment.DeadlockInfoList.Add(deadlockIndex = Simulator.Instance.SignalEnvironment.deadlockIndex++, this);
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for restore
        /// </summary>

        public DeadlockInfo(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);

            deadlockIndex = inf.ReadInt32();
            AvailablePathList = new List<DeadlockPathInfo>();

            int totalPaths = inf.ReadInt32();
            for (int iPath = 0; iPath <= totalPaths - 1; iPath++)
            {
                DeadlockPathInfo thisPath = new DeadlockPathInfo(inf);
                AvailablePathList.Add(thisPath);
            }

            PathReferences = new Dictionary<int, List<int>>();

            int totalReferences = inf.ReadInt32();
            for (int iReference = 0; iReference <= totalReferences - 1; iReference++)
            {
                int thisReference = inf.ReadInt32();
                List<int> thisList = new List<int>();

                int totalItems = inf.ReadInt32();
                for (int iItem = 0; iItem <= totalItems - 1; iItem++)
                {
                    int thisItem = inf.ReadInt32();
                    thisList.Add(thisItem);
                }
                PathReferences.Add(thisReference, thisList);
            }

            TrainReferences = new Dictionary<int, List<int>>();

            totalReferences = inf.ReadInt32();
            for (int iReference = 0; iReference <= totalReferences - 1; iReference++)
            {
                int thisReference = inf.ReadInt32();
                List<int> thisList = new List<int>();

                int totalItems = inf.ReadInt32();
                for (int iItem = 0; iItem <= totalItems - 1; iItem++)
                {
                    int thisItem = inf.ReadInt32();
                    thisList.Add(thisItem);
                }
                TrainReferences.Add(thisReference, thisList);
            }

            TrainLengthFit = new Dictionary<int, Dictionary<int, bool>>();

            int totalFits = inf.ReadInt32();
            for (int iFits = 0; iFits <= totalFits - 1; iFits++)
            {
                int thisTrain = inf.ReadInt32();
                Dictionary<int, bool> thisLengthFit = new Dictionary<int, bool>();

                int totalItems = inf.ReadInt32();
                for (int iItem = 0; iItem <= totalItems - 1; iItem++)
                {
                    int itemRef = inf.ReadInt32();
                    bool itemValue = inf.ReadBoolean();

                    thisLengthFit.Add(itemRef, itemValue);
                }
                TrainLengthFit.Add(thisTrain, thisLengthFit);
            }

            TrainOwnPath = new Dictionary<int, int>();

            int totalOwnPath = inf.ReadInt32();
            for (int iOwnPath = 0; iOwnPath <= totalOwnPath - 1; iOwnPath++)
            {
                int trainIndex = inf.ReadInt32();
                int pathIndex = inf.ReadInt32();
                TrainOwnPath.Add(trainIndex, pathIndex);
            }

            InverseInfo = new Dictionary<int, int>();
            int totalInverseInfo = inf.ReadInt32();

            for (int iInfo = 0; iInfo <= totalInverseInfo - 1; iInfo++)
            {
                int infoKey = inf.ReadInt32();
                int infoValue = inf.ReadInt32();
                InverseInfo.Add(infoKey, infoValue);
            }

            TrainSubpathIndex = new Dictionary<int, Dictionary<int, int>>();
            int totalTrain = inf.ReadInt32();

            for (int iTrain = 0; iTrain <= totalTrain - 1; iTrain++)
            {
                int trainValue = inf.ReadInt32();
                Dictionary<int, int> subpathList = new Dictionary<int, int>();

                int totalSubpaths = inf.ReadInt32();
                for (int iSubpath = 0; iSubpath <= totalSubpaths - 1; iSubpath++)
                {
                    int subpathValue = inf.ReadInt32();
                    int indexValue = inf.ReadInt32();
                    subpathList.Add(subpathValue, indexValue);
                }
                TrainSubpathIndex.Add(trainValue, subpathList);
            }

            nextTrainSubpathIndex = inf.ReadInt32();
        }

        //================================================================================================//
        /// <summary>
        /// save
        /// </summary>

        public void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);

            outf.Write(deadlockIndex);
            outf.Write(AvailablePathList.Count);

            foreach (DeadlockPathInfo thisPathInfo in AvailablePathList)
            {
                thisPathInfo.Save(outf);
            }

            outf.Write(PathReferences.Count);
            foreach (KeyValuePair<int, List<int>> thisReference in PathReferences)
            {
                outf.Write(thisReference.Key);
                outf.Write(thisReference.Value.Count);

                foreach (int thisRefValue in thisReference.Value)
                {
                    outf.Write(thisRefValue);
                }
            }

            outf.Write(TrainReferences.Count);
            foreach (KeyValuePair<int, List<int>> thisReference in TrainReferences)
            {
                outf.Write(thisReference.Key);
                outf.Write(thisReference.Value.Count);

                foreach (int thisRefValue in thisReference.Value)
                {
                    outf.Write(thisRefValue);
                }
            }

            outf.Write(TrainLengthFit.Count);
            foreach (KeyValuePair<int, Dictionary<int, bool>> thisLengthFit in TrainLengthFit)
            {
                outf.Write(thisLengthFit.Key);
                outf.Write(thisLengthFit.Value.Count);

                foreach (KeyValuePair<int, bool> thisAvailValue in thisLengthFit.Value)
                {
                    outf.Write(thisAvailValue.Key);
                    outf.Write(thisAvailValue.Value);
                }
            }

            outf.Write(TrainOwnPath.Count);
            foreach (KeyValuePair<int, int> ownTrainInfo in TrainOwnPath)
            {
                outf.Write(ownTrainInfo.Key);
                outf.Write(ownTrainInfo.Value);
            }

            outf.Write(InverseInfo.Count);
            foreach (KeyValuePair<int, int> thisInfo in InverseInfo)
            {
                outf.Write(thisInfo.Key);
                outf.Write(thisInfo.Value);
            }

            outf.Write(TrainSubpathIndex.Count);
            foreach (KeyValuePair<int, Dictionary<int, int>> trainInfo in TrainSubpathIndex)
            {
                outf.Write(trainInfo.Key);
                outf.Write(trainInfo.Value.Count);

                foreach (KeyValuePair<int, int> subpathInfo in trainInfo.Value)
                {
                    outf.Write(subpathInfo.Key);
                    outf.Write(subpathInfo.Value);
                }
            }

            outf.Write(nextTrainSubpathIndex);
        }

        //================================================================================================//
        /// <summary>
        /// Create deadlock info from alternative path or find related info
        /// </summary>

        internal static DeadlockInfo FindDeadlockInfo(TrackCircuitPartialPathRoute partPath, TrackCircuitPartialPathRoute mainPath, int startSectionIndex, int endSectionIndex)
        {
            TrackCircuitSection startSection = TrackCircuitSection.TrackCircuitList[startSectionIndex];
            TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];

            int usedStartSectionRouteIndex = mainPath.GetRouteIndex(startSectionIndex, 0);
            int usedEndSectionRouteIndex = mainPath.GetRouteIndex(endSectionIndex, usedStartSectionRouteIndex);

            // check if there is a deadlock info defined with these as boundaries
            int startSectionDLReference = startSection.DeadlockReference;
            int endSectionDLReference = endSection.DeadlockReference;

            DeadlockInfo newDeadlockInfo = null;

            // if either end is within a deadlock, try if end of deadlock matches train path

            if (startSection.DeadlockBoundaries != null && startSection.DeadlockBoundaries.Count > 0)
            {
                int newStartSectionRouteIndex = -1;
                foreach (KeyValuePair<int, int> startSectionInfo in startSection.DeadlockBoundaries)
                {
                    DeadlockInfo existDeadlockInfo = Simulator.Instance.SignalEnvironment.DeadlockInfoList[startSectionInfo.Key];
                    TrackCircuitPartialPathRoute existPath = existDeadlockInfo.AvailablePathList[startSectionInfo.Value].Path;
                    newStartSectionRouteIndex = mainPath.GetRouteIndexBackward(existPath[0].TrackCircuitSection.Index, usedStartSectionRouteIndex);
                    if (newStartSectionRouteIndex < 0) // may be wrong direction - try end section
                    {
                        newStartSectionRouteIndex =
                            mainPath.GetRouteIndexBackward(existDeadlockInfo.AvailablePathList[startSectionInfo.Value].EndSectionIndex, usedStartSectionRouteIndex);
                    }

                    if (newStartSectionRouteIndex >= 0)
                    {
                        newDeadlockInfo = existDeadlockInfo;
                        break; // match found, stop searching
                    }
                }

                // no match found - train path is not on existing deadlock - do not accept

                if (newStartSectionRouteIndex < 0)
                {
                    return (null);
                }
                else
                {
                    // add sections to start of temp path
                    for (int iIndex = usedStartSectionRouteIndex - 1; iIndex >= newStartSectionRouteIndex; iIndex--)
                    {
                        TrackCircuitRouteElement newElement = mainPath[iIndex];
                        partPath.Insert(0, newElement);
                    }
                }
            }

            if (endSection.DeadlockBoundaries != null && endSection.DeadlockBoundaries.Count > 0)
            {
                int newEndSectionRouteIndex = -1;
                foreach (KeyValuePair<int, int> endSectionInfo in endSection.DeadlockBoundaries)
                {
                    DeadlockInfo existDeadlockInfo = Simulator.Instance.SignalEnvironment.DeadlockInfoList[endSectionInfo.Key];
                    TrackCircuitPartialPathRoute existPath = existDeadlockInfo.AvailablePathList[endSectionInfo.Value].Path;
                    newEndSectionRouteIndex = mainPath.GetRouteIndex(existPath[0].TrackCircuitSection.Index, usedEndSectionRouteIndex);
                    if (newEndSectionRouteIndex < 0) // may be wrong direction - try end section
                    {
                        newEndSectionRouteIndex =
                            mainPath.GetRouteIndex(existDeadlockInfo.AvailablePathList[endSectionInfo.Value].EndSectionIndex, usedEndSectionRouteIndex);
                    }

                    if (newEndSectionRouteIndex >= 0)
                    {
                        newDeadlockInfo = existDeadlockInfo;
                        break; // match found, stop searching
                    }
                }

                // no match found - train path is not on existing deadlock - do not accept

                if (newEndSectionRouteIndex < 0)
                {
                    return (null);
                }
                else
                {
                    // add sections to end of temp path
                    for (int iIndex = usedEndSectionRouteIndex + 1; iIndex <= newEndSectionRouteIndex; iIndex++)
                    {
                        TrackCircuitRouteElement newElement = mainPath[iIndex];
                        partPath.Add(newElement);
                    }
                }
            }

            // if no deadlock yet found

            if (newDeadlockInfo == null)
            {
                // if both references are equal, use existing information
                if (startSectionDLReference > 0 && startSectionDLReference == endSectionDLReference)
                {
                    newDeadlockInfo = Simulator.Instance.SignalEnvironment.DeadlockInfoList[startSectionDLReference];
                }

                // if both references are null, check for existing references along route
                else if (startSectionDLReference < 0 && endSectionDLReference < 0)
                {
                    if (CheckNoOverlapDeadlockPaths(partPath))
                    {
                        newDeadlockInfo = new DeadlockInfo();
                        Simulator.Instance.SignalEnvironment.DeadlockReference.Add(startSectionIndex, newDeadlockInfo.deadlockIndex);
                        Simulator.Instance.SignalEnvironment.DeadlockReference.Add(endSectionIndex, newDeadlockInfo.deadlockIndex);

                        startSection.DeadlockReference = newDeadlockInfo.deadlockIndex;
                        endSection.DeadlockReference = newDeadlockInfo.deadlockIndex;
                    }
                    // else : overlaps existing deadlocks - will sort that out later //TODO DEADLOCK
                }
            }

            return (newDeadlockInfo);
        }

        //================================================================================================//
        /// <summary>
        /// add unnamed path to deadlock info
        /// return : [0] index to path
        ///          [1] > 0 : existing, < 0 : new
        /// </summary>
        internal (int PathIndex, bool Exists) AddPath(TrackCircuitPartialPathRoute path, int startSectionIndex)
        {
            ArgumentNullException.ThrowIfNull(path);

            // check if equal to existing path
            for (int i = 0; i <= AvailablePathList.Count - 1; i++)
            {
                DeadlockPathInfo existPathInfo = AvailablePathList[i];
                if (path.EqualsPath(existPathInfo.Path))
                {
                    // check if path referenced from correct start position, else add reference
                    if (PathReferences.ContainsKey(startSectionIndex))
                    {
                        if (!PathReferences[startSectionIndex].Contains(i))
                        {
                            PathReferences[startSectionIndex].Add(i);
                        }
                    }
                    else
                    {
                        List<int> refSectionPaths = new List<int>
                        {
                            i
                        };
                        PathReferences.Add(startSectionIndex, refSectionPaths);
                    }

                    // return path
                    return (i, true);
                }
            }

            // new path
            int newPathIndex = AvailablePathList.Count;
            DeadlockPathInfo newPathInfo = new DeadlockPathInfo(path, newPathIndex);
            AvailablePathList.Add(newPathInfo);

            // add path to list of paths from this section
            List<int> thisSectionPaths;

            if (PathReferences.ContainsKey(startSectionIndex))
            {
                thisSectionPaths = PathReferences[startSectionIndex];
            }
            else
            {
                thisSectionPaths = new List<int>();
                PathReferences.Add(startSectionIndex, thisSectionPaths);
            }

            thisSectionPaths.Add(newPathIndex);

            // set references for intermediate sections
            SetIntermediateReferences(path, newPathIndex);

            if (AvailablePathList.Count == 1) // if only one entry, set name to MAIN (first path is MAIN path)
            {
                newPathInfo.Name = "MAIN";
            }
            else
            {
                newPathInfo.Name = $"PASS{AvailablePathList.Count:00}";
            }

            // check for reverse path (through existing paths only)

            for (int iPath = 0; iPath <= AvailablePathList.Count - 2; iPath++)
            {
                if (path.EqualsReversePath(AvailablePathList[iPath].Path))
                {
                    InverseInfo.Add(newPathIndex, iPath);
                    InverseInfo.Add(iPath, newPathIndex);
                }
            }

            return (newPathIndex, false); // set new path found
        }

        //================================================================================================//
        /// <summary>
        /// add named path to deadlock info
        /// return : [0] index to path
        ///          [1] > 0 : existing, < 0 : new
        /// </summary>
        internal (int PathIndex, bool Exists) AddPath(TrackCircuitPartialPathRoute path, int startSectionIndex, string name, string groupName)
        {
            ArgumentNullException.ThrowIfNull(path);

            // check if equal to existing path and has same name
            for (int i = 0; i <= AvailablePathList.Count - 1; i++)
            {
                DeadlockPathInfo existPathInfo = AvailablePathList[i];
                if (path.EqualsPath(existPathInfo.Path) && existPathInfo.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        bool groupfound = false;
                        foreach (string otherGroupName in existPathInfo.Groups)
                        {
                            if (otherGroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                            {
                                groupfound = true;
                                break;
                            }
                        }

                        if (!groupfound)
                            existPathInfo.Groups.Add(groupName);
                    }

                    // check if path referenced from correct start position, else add reference
                    if (PathReferences.ContainsKey(startSectionIndex))
                    {
                        if (!PathReferences[startSectionIndex].Contains(i))
                        {
                            PathReferences[startSectionIndex].Add(i);
                        }
                    }
                    else
                    {
                        List<int> refSectionPaths = new List<int>
                        {
                            i
                        };
                        PathReferences.Add(startSectionIndex, refSectionPaths);
                    }

                    // return path
                    return (i, true);
                }
            }

            // new path

            int newPathIndex = AvailablePathList.Count;
            DeadlockPathInfo newPathInfo = new DeadlockPathInfo(path, newPathIndex)
            {
                Name = name
            };
            if (!string.IsNullOrEmpty(groupName))
                newPathInfo.Groups.Add(groupName);

            AvailablePathList.Add(newPathInfo);

            // add path to list of path from this section
            List<int> thisSectionPaths;

            if (PathReferences.ContainsKey(startSectionIndex))
            {
                thisSectionPaths = PathReferences[startSectionIndex];
            }
            else
            {
                thisSectionPaths = new List<int>();
                PathReferences.Add(startSectionIndex, thisSectionPaths);
            }

            thisSectionPaths.Add(newPathIndex);

            // set references for intermediate sections
            SetIntermediateReferences(path, newPathIndex);

            // check for reverse path (through existing paths only)

            for (int iPath = 0; iPath <= AvailablePathList.Count - 2; iPath++)
            {
                if (path.EqualsReversePath(AvailablePathList[iPath].Path))
                {
                    InverseInfo.Add(newPathIndex, iPath);
                    InverseInfo.Add(iPath, newPathIndex);
                }
            }

            return (newPathIndex, false); // return negative index to indicate new path
        }

        //================================================================================================//
        /// <summary>
        /// check if path has no conflict with overlapping deadlock paths
        /// returns false if there is an overlap
        /// </summary>
        private static bool CheckNoOverlapDeadlockPaths(TrackCircuitPartialPathRoute path)
        {
            foreach (TrackCircuitRouteElement element in path)
            {
                TrackCircuitSection thisSection = element.TrackCircuitSection;
                if (thisSection.DeadlockReference >= 0)
                {
                    return (false);
                }
            }
            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// check if at least one valid path is available into a deadlock area
        /// returns indices of available paths
        /// </summary>
        internal List<int> CheckDeadlockPathAvailability(TrackCircuitSection startSection, Train train)
        {
            ArgumentNullException.ThrowIfNull(train);
            ArgumentNullException.ThrowIfNull(startSection);

            List<int> useablePaths = new List<int>();

            // get end section for this train
            int endSectionIndex = GetEndSection(train);
            TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];

            // get list of paths which are available
            List<int> freePaths = GetFreePaths(train);

            // get all possible paths from train(s) in opposite direction
            List<int> usedRoutes = new List<int>();    // all routes allowed for any train
            List<int> commonRoutes = new List<int>();  // routes common to all trains
            List<int> singleRoutes = new List<int>();  // routes which are the single available route for trains which have one route only

            bool firstTrain = true;

            // loop through other trains
            foreach (int otherTrainNumber in endSection.DeadlockActives)
            {
                Train otherTrain = Train.GetOtherTrainByNumber(otherTrainNumber);

                // TODO : find proper most matching path
                if (HasTrainAndSubpathIndex(otherTrain.Number, otherTrain.TCRoute.ActiveSubPath))
                {
                    List<int> otherFreePaths = GetFreePaths(otherTrain);
                    foreach (int iPath in otherFreePaths)
                    {
                        if (!usedRoutes.Contains(iPath)) usedRoutes.Add(iPath);
                        if (firstTrain)
                        {
                            commonRoutes.Add(iPath);
                        }
                    }

                    if (otherFreePaths.Count == 1)
                    {
                        singleRoutes.Add(otherFreePaths[0]);
                    }

                    for (int cPathIndex = commonRoutes.Count - 1; cPathIndex >= 0 && !firstTrain; cPathIndex--)
                    {
                        if (!otherFreePaths.Contains(commonRoutes[cPathIndex]))
                        {
                            commonRoutes.RemoveAt(cPathIndex);
                        }
                    }
                }
                else
                {
                    // for now : set all possible routes to used and single
                    foreach (int iroute in freePaths)
                    {
                        singleRoutes.Add(iroute);
                        usedRoutes.Add(iroute);
                    }
                }

                firstTrain = false;
            }

            // get inverse path indices to compare with this train's paths

            List<int> inverseUsedRoutes = new List<int>();
            List<int> inverseCommonRoutes = new List<int>();
            List<int> inverseSingleRoutes = new List<int>();

            foreach (int iPath in usedRoutes)
            {
                if (InverseInfo.ContainsKey(iPath))
                    inverseUsedRoutes.Add(InverseInfo[iPath]);
            }
            foreach (int iPath in commonRoutes)
            {
                if (InverseInfo.ContainsKey(iPath))
                    inverseCommonRoutes.Add(InverseInfo[iPath]);
            }
            foreach (int iPath in singleRoutes)
            {
                if (InverseInfo.ContainsKey(iPath))
                    inverseSingleRoutes.Add(InverseInfo[iPath]);
            }

            // if deadlock is awaited at other end : remove paths which would cause conflict
            if (endSection.CheckDeadlockAwaited(train.Number))
            {
                // check if this train has any route not used by trains from other end

                foreach (int iPath in freePaths)
                {
                    if (!inverseUsedRoutes.Contains(iPath)) useablePaths.Add(iPath);
                }

                if (useablePaths.Count > 0) return (useablePaths); // unused paths available

                // check if any path remains if common paths are excluded

                if (inverseCommonRoutes.Count >= 1) // there are common routes, so other routes may be used
                {
                    foreach (int iPath in freePaths)
                    {
                        if (!inverseCommonRoutes.Contains(iPath)) useablePaths.Add(iPath);
                    }

                    if (useablePaths.Count > 0)
                    {
                        return (useablePaths);
                    }
                }

                // check if any path remains if all required single paths are excluded

                if (inverseSingleRoutes.Count >= 1) // there are single paths
                {
                    foreach (int iPath in freePaths)
                    {
                        if (!inverseSingleRoutes.Contains(iPath)) useablePaths.Add(iPath);
                    }

                    if (useablePaths.Count > 0)
                    {
                        return (useablePaths);
                    }
                }

                // no path available without conflict - but if deadlock also awaited on this end, proceed anyway (otherwise everything gets stuck)

                if (startSection.DeadlockAwaited.Count >= 1)
                {
                    return (freePaths); // may use any path in this situation
                }

                // no path available - return empty list
                return (useablePaths);
            }

            // no deadlock awaited at other end : check if there is any single path set, if so exclude those to avoid conflict
            else
            {
                // check if any path remains if all required single paths are excluded

                if (inverseSingleRoutes.Count >= 1) // there are single paths
                {
                    foreach (int iPath in freePaths)
                    {
                        if (!inverseSingleRoutes.Contains(iPath)) useablePaths.Add(iPath);
                    }

                    if (useablePaths.Count > 0)
                    {
                        return (useablePaths);
                    }
                }

                // no single path conflicts - so all free paths are available
                return (freePaths);
            }
        }

        //================================================================================================//
        /// <summary>
        /// get valid list of indices related available for specific train / subpath index
        /// </summary>
        internal List<int> GetValidPassingPaths(int trainNumber, int sublistRef, bool allowPublic)
        {
            List<int> foundIndices = new List<int>();

            for (int iPath = 0; iPath <= AvailablePathList.Count - 1; iPath++)
            {
                DeadlockPathInfo thisPathInfo = AvailablePathList[iPath];
                int trainSubpathIndex = GetTrainAndSubpathIndex(trainNumber, sublistRef);
                if (thisPathInfo.AllowedTrains.Contains(trainSubpathIndex) || (thisPathInfo.AllowedTrains.Contains(-1) && allowPublic))
                {
                    foundIndices.Add(iPath);
                }
            }

            return (foundIndices);
        }

        //================================================================================================//
        /// <summary>
        /// check availability of passing paths
        /// return list of paths which are free
        /// </summary>
        internal List<int> GetFreePaths(Train train)
        {
            ArgumentNullException.ThrowIfNull(train);

            List<int> freePaths = new List<int>();

            int thisTrainAndSubpathIndex = GetTrainAndSubpathIndex(train.Number, train.TCRoute.ActiveSubPath);
            for (int iPath = 0; iPath <= TrainReferences[thisTrainAndSubpathIndex].Count - 1; iPath++)
            {
                int pathIndex = TrainReferences[thisTrainAndSubpathIndex][iPath];
                DeadlockPathInfo altPathInfo = AvailablePathList[pathIndex];
                TrackCircuitPartialPathRoute altPath = altPathInfo.Path;

                // check all sections upto and including last used index, but do not check first junction section

                bool pathAvail = true;
                for (int iElement = 1; iElement <= altPathInfo.LastUsefulSectionIndex; iElement++)
                {
                    TrackCircuitSection thisSection = altPath[iElement].TrackCircuitSection;
                    if (!thisSection.IsAvailable(train.RoutedForward))
                    {
                        pathAvail = false;
                        break;
                    }
                }

                if (pathAvail) freePaths.Add(pathIndex);
            }

            return (freePaths);
        }

        //================================================================================================//
        /// <summary>
        /// set deadlock info references for intermediate sections
        /// </summary>
        internal int SelectPath(List<int> availableRoutes, Train train, ref int endSectionIndex)
        {
            ArgumentNullException.ThrowIfNull(train);

            int selectedPathNofit = -1;
            int selectedPathFit = -1;
            bool checkedMain = false;
            bool checkedOwn = false;

            endSectionIndex = GetEndSection(train);
            TrackCircuitSection endSection = TrackCircuitSection.TrackCircuitList[endSectionIndex];

            bool preferMain = true;
            // if deadlock actives : main least preferred
            if (endSection.DeadlockActives.Count > 0)
            {
                preferMain = false;
                checkedMain = true; // consider main as checked
            }

            // check if own path is also main path - if so, do not check it separately

            int indexTrainAndSubroute = GetTrainAndSubpathIndex(train.Number, train.TCRoute.ActiveSubPath);
            int ownPathIndex = TrainOwnPath[indexTrainAndSubroute];
            int defaultPath = ownPathIndex;
            if (AvailablePathList[ownPathIndex].Name.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
            {
                checkedOwn = true; // do not check own path separately
            }

            // get train fit list
            Dictionary<int, bool> trainFitInfo = TrainLengthFit[indexTrainAndSubroute];

            // loop through all available paths

            for (int iPath = 0; iPath <= availableRoutes.Count - 1; iPath++)
            {
                int pathIndex = availableRoutes[iPath];
                DeadlockPathInfo pathInfo = AvailablePathList[pathIndex];
                bool trainFitsInSection = trainFitInfo[pathIndex];

                // check for OWN
                if (!checkedOwn && pathIndex == ownPathIndex)
                {
                    checkedOwn = true;
                    if (trainFitsInSection)
                    {
                        selectedPathFit = pathIndex;
                        break; // if train fits in own path, break
                    }

                    selectedPathNofit = pathIndex;
                    if (checkedMain && selectedPathFit > 0) break;  // if doesnt fit but main has been checked and train fits somewhere, break
                }

                // check for MAIN
                if (pathInfo.Name.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
                {
                    checkedMain = true;
                    if (trainFitsInSection)
                    {
                        selectedPathFit = pathIndex;
                        if (checkedOwn && preferMain) break;  // if fits and own has been checked and main prefered - break
                    }
                    else
                    {
                        if (!checkedOwn || selectedPathNofit < 0 || preferMain)  // if own has not been checked
                        {
                            selectedPathNofit = pathIndex;
                        }
                    }
                }

                // check for others
                else
                {
                    if (trainFitsInSection) // if train fits
                    {
                        selectedPathFit = pathIndex;
                        if (checkedMain || checkedOwn)
                        {
                            break;  // main and own allready checked so no need to look further
                        }
                    }
                    else
                    {
                        if ((!checkedOwn && !checkedMain) || !preferMain) // set as option if own and main both not checked or main not prefered
                        {
                            selectedPathNofit = pathIndex;
                        }
                    }
                }
            }

            // Sometimes selectedPathFit nor selectedPathNofit gets new value, which is wrong and will induce an
            // IndexOutOfRangeException, but I can't find out why that happens, so here is a warning message when it
            // happens, to at least find out which train, and passing path that triggers this bug.
            if (selectedPathFit < 0 && selectedPathNofit < 0 && defaultPath < 0)
                Trace.TraceWarning("Path can't be selected for train {0} at end-section index {1}", train.Name, endSectionIndex);
            return (selectedPathFit >= 0 ? selectedPathFit : selectedPathNofit >= 0 ? selectedPathNofit : defaultPath); // return fit path if set else no-fit path if set else default path
        }

        //================================================================================================//
        /// <summary>
        /// get end section index for deadlock area for a particular train
        /// </summary>
        private int GetEndSection(Train train)
        {
            int thisTrainAndSubpathIndex = GetTrainAndSubpathIndex(train.Number, train.TCRoute.ActiveSubPath);
            if (!TrainReferences.ContainsKey(thisTrainAndSubpathIndex))
            {
                Trace.TraceWarning("Multiple passing paths at the same location, without common branch out, or return switch. Check the passing paths for Train name: {0} (number: {1}), and other train's paths, which have passing paths at the same locations", train.Name, train.Number);
            }
            int pathIndex = TrainReferences[thisTrainAndSubpathIndex][0];
            DeadlockPathInfo pathInfo = AvailablePathList[pathIndex];
            return (pathInfo.EndSectionIndex);
        }

        //================================================================================================//
        /// <summary>
        /// set deadlock info references for intermediate sections
        /// </summary>
        private void SetIntermediateReferences(TrackCircuitPartialPathRoute path, int pathIndex)
        {
            for (int i = 1; i <= path.Count - 2; i++) // loop through path excluding first and last section
            {
                TrackCircuitRouteElement thisElement = path[i];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                if (thisSection.DeadlockBoundaries == null)
                {
                    thisSection.DeadlockBoundaries = new Dictionary<int, int>();
                }

                thisSection.DeadlockBoundaries.TryAdd(deadlockIndex, pathIndex);
            }
        }

        //================================================================================================//
        /// <summary>
        /// get index value for specific train/subpath combination
        /// if set, return value
        /// if not set, generate value, set value and return value
        /// </summary>
        internal int GetTrainAndSubpathIndex(int trainNumber, int subpathIndex)
        {
            if (TrainSubpathIndex.ContainsKey(trainNumber))
            {
                Dictionary<int, int> subpathList = TrainSubpathIndex[trainNumber];
                if (subpathList.ContainsKey(subpathIndex))
                {
                    return (subpathList[subpathIndex]);
                }
            }

            int newIndex = ++nextTrainSubpathIndex;
            Dictionary<int, int> newSubpathList;
            if (TrainSubpathIndex.ContainsKey(trainNumber))
            {
                newSubpathList = TrainSubpathIndex[trainNumber];
            }
            else
            {
                newSubpathList = new Dictionary<int, int>();
                TrainSubpathIndex.Add(trainNumber, newSubpathList);
            }

            newSubpathList.Add(subpathIndex, newIndex);

            return (newIndex);
        }

        //================================================================================================//
        /// <summary>
        /// check index value for specific train/subpath combination
        /// if set, return value
        /// if not set, generate value, set value and return value
        /// </summary>
        internal bool HasTrainAndSubpathIndex(int trainNumber, int subpathIndex)
        {
            if (TrainSubpathIndex.ContainsKey(trainNumber))
            {
                Dictionary<int, int> subpathList = TrainSubpathIndex[trainNumber];
                if (subpathList.ContainsKey(subpathIndex))
                {
                    return (true);
                }
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// check index value for specific train/subpath combination
        /// if set, return value
        /// if not set, generate value, set value and return value
        /// </summary>
        internal bool RemoveTrainAndSubpathIndex(int trainNumber, int subpathIndex)
        {
            if (TrainSubpathIndex.ContainsKey(trainNumber))
            {
                Dictionary<int, int> subpathList = TrainSubpathIndex[trainNumber];
                if (subpathList.ContainsKey(subpathIndex))
                {
                    subpathList.Remove(subpathIndex);
                }
                if (subpathList.Count <= 0)
                {
                    TrainSubpathIndex.Remove(trainNumber);
                }
            }
            return (false);
        }

        //================================================================================================//
        /// <summary>
        /// Insert train reference details
        /// </summary>
        internal int SetTrainDetails(int trainNumber, int subpathRef, float trainLength, TrackCircuitPartialPathRoute subpath, int elementRouteIndex)
        {

            // search if trains path has valid equivalent

            if (elementRouteIndex <= 0 || elementRouteIndex >= subpath.Count)
            {
                Trace.TraceWarning("Invalid route element in SetTrainDetails : value =  {0}, max. is {1}", elementRouteIndex, subpath.Count);
                return (-1);
            }

            int trainSubpathIndex = GetTrainAndSubpathIndex(trainNumber, subpathRef);
            int sectionIndex = subpath[elementRouteIndex].TrackCircuitSection.Index;
            (int Result, int PathIndex) matchingPath = SearchMatchingFullPath(subpath, sectionIndex, elementRouteIndex);
            TrackCircuitPartialPathRoute partPath;

            switch (matchingPath.Result)
            {
                // matchingPath[0] == 1 : path runs short of all available paths - train ends within area - no alternative path available
                case 1:
                    {
                        // if no other paths for this reference, remove train/subpath reference from table
                        if (!TrainReferences.ContainsKey(trainSubpathIndex))
                        {
                            RemoveTrainAndSubpathIndex(trainNumber, subpathRef);
                        }
                        return (-1);
                    }
                // matchingPath[0] == 2 : path runs through area but has no match - insert path for this train only (no inverse inserted)
                // matchingPath[1] = end section index in route
                case 2:
                    {
                        partPath = new TrackCircuitPartialPathRoute(subpath, elementRouteIndex, matchingPath.PathIndex);
                        (int PathIndex, _) = AddPath(partPath, sectionIndex);
                        DeadlockPathInfo thisPathInfo = AvailablePathList[PathIndex];

                        (thisPathInfo.LastUsefulSectionIndex, thisPathInfo.UsefulLength) = partPath.GetUsefullLength(0.0f, -1, -1);
                        thisPathInfo.EndSectionIndex = subpath[matchingPath.PathIndex].TrackCircuitSection.Index;
                        thisPathInfo.Name = string.Empty;  // path has no name

                        thisPathInfo.AllowedTrains.Add(trainSubpathIndex);
                        TrainOwnPath.Add(trainSubpathIndex, PathIndex);
                        break;
                    }
                // matchingPath[0] == 3 : path runs through area but no valid path available or possible - remove train index as train has no alternative paths at this location
                case 3:
                    {
                        RemoveTrainAndSubpathIndex(trainNumber, subpathRef);
                        return (matchingPath.PathIndex);
                    }
                // otherwise matchingPath [1] is matching path - add track details if not yet set
                default:
                    {
                        DeadlockPathInfo thisPathInfo = AvailablePathList[matchingPath.PathIndex];
                        if (!thisPathInfo.AllowedTrains.Contains(trainSubpathIndex))
                        {
                            thisPathInfo.AllowedTrains.Add(trainSubpathIndex);
                        }
                        TrainOwnPath.Add(trainSubpathIndex, matchingPath.PathIndex);
                        break;
                    }
            }
            // set cross-references to allowed track entries for easy reference

            List<int> availPathList;

            if (TrainReferences.ContainsKey(trainSubpathIndex))
            {
                availPathList = TrainReferences[trainSubpathIndex];
            }
            else
            {
                availPathList = new List<int>();
                TrainReferences.Add(trainSubpathIndex, availPathList);
            }

            Dictionary<int, bool> thisTrainFitList;
            if (TrainLengthFit.ContainsKey(trainSubpathIndex))
            {
                thisTrainFitList = TrainLengthFit[trainSubpathIndex];
            }
            else
            {
                thisTrainFitList = new Dictionary<int, bool>();
                TrainLengthFit.Add(trainSubpathIndex, thisTrainFitList);
            }

            for (int iPath = 0; iPath <= AvailablePathList.Count - 1; iPath++)
            {
                DeadlockPathInfo thisPathInfo = AvailablePathList[iPath];

                if (thisPathInfo.AllowedTrains.Contains(-1) || thisPathInfo.AllowedTrains.Contains(trainSubpathIndex))
                {
                    if (PathReferences[sectionIndex].Contains(iPath)) // path from correct end
                    {
                        availPathList.Add(iPath);

                        bool trainFit = (trainLength < thisPathInfo.UsefulLength);
                        thisTrainFitList.Add(iPath, trainFit);
                    }
                }
            }

            // get end section from first valid path

            partPath = new TrackCircuitPartialPathRoute(AvailablePathList[availPathList[0]].Path);
            int lastSection = partPath[partPath.Count - 1].TrackCircuitSection.Index;
            int returnIndex = subpath.GetRouteIndex(lastSection, elementRouteIndex);
            return (returnIndex);

        }

        //================================================================================================//
        /// <summary>
        /// Search matching path from full route path
        ///
        /// return : [0] = 0 : matching path, [1] = matching path index
        ///          [0] = 1 : no matching path and route does not contain any of the end sections (route ends within area)
        ///          [0] = 2 : no matching path but route does run through area, [1] contains end section index
        ///          [0] = 3 : no matching path in required direction but route does run through area, [1] contains end section index
        /// </summary>
        private (int Result, int PathIndex) SearchMatchingFullPath(TrackCircuitPartialPathRoute fullPath, int startSectionIndex, int startSectionRouteIndex)
        {
            int foundMatchingEndRouteIndex = -1;
            int matchingPath = -1;

            // paths available from start section
            if (PathReferences.ContainsKey(startSectionIndex))
            {
                List<int> availablePaths = PathReferences[startSectionIndex];

                // search through paths from this section

                for (int iPath = 0; iPath <= availablePaths.Count - 1; iPath++)
                {
                    // extract path, get indices in train path
                    TrackCircuitPartialPathRoute testPath = AvailablePathList[availablePaths[iPath]].Path;
                    int endSectionIndex = AvailablePathList[availablePaths[iPath]].EndSectionIndex;
                    int endSectionRouteIndex = fullPath.GetRouteIndex(endSectionIndex, startSectionRouteIndex);

                    // can only be matching path if endindex > 0 and endindex != startindex (if wrong way path, endindex = startindex)
                    if (endSectionRouteIndex > 0 && endSectionRouteIndex != startSectionRouteIndex)
                    {
                        TrackCircuitPartialPathRoute partPath = new TrackCircuitPartialPathRoute(fullPath, startSectionRouteIndex, endSectionRouteIndex);

                        // test route
                        if (partPath.EqualsPath(testPath))
                        {
                            matchingPath = availablePaths[iPath];
                            break;
                        }

                        // set end index (if not yet found)
                        if (foundMatchingEndRouteIndex < 0)
                        {
                            foundMatchingEndRouteIndex = endSectionRouteIndex;
                        }
                    }

                    // no matching end index - check train direction
                    else
                    {
                        // check direction
                        TrackDirection areadirection = AvailablePathList[availablePaths[0]].Path[0].Direction;
                        TrackDirection traindirection = fullPath[startSectionRouteIndex].Direction;

                        // train has same direction - check if end of path is really within the path
                        if (areadirection == traindirection)
                        {
                            int pathEndSection = fullPath[fullPath.Count - 1].TrackCircuitSection.Index;
                            if (testPath.GetRouteIndex(pathEndSection, 0) >= 0) // end point is within section
                            {
                                return (1, 0);
                            }
                        }
                        else  //if wrong direction, train exits area at this location//
                        {
                            return (3, startSectionRouteIndex + 1);
                        }
                    }
                }
            }

            // no paths available from start section, check if end section of paths matches start section
            else
            {
                if (startSectionIndex == AvailablePathList[0].EndSectionIndex)
                {
                    int matchingEndIndex = fullPath.GetRouteIndex(AvailablePathList[0].Path[0].TrackCircuitSection.Index, startSectionRouteIndex);
                    if (matchingEndIndex > 0)
                    {
                        return (2, matchingEndIndex);
                    }
                    else
                    {
                        return (3, startSectionRouteIndex + 1);
                    }
                }
            }

            if (matchingPath >= 0)
            {
                return (0, matchingPath);
            }
            else if (foundMatchingEndRouteIndex >= 0)
            {
                return (2, foundMatchingEndRouteIndex);
            }
            else
            {
                return (3, startSectionRouteIndex + 1);
            }
        }

    } // end DeadlockInfo class

}
