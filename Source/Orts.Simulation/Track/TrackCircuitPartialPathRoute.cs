using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Models.State;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// Subpath list : list of TCRouteElements building a subpath
    /// </summary>
    public class TrackCircuitPartialPathRoute : IList<TrackCircuitRouteElement>, ISaveStateApi<TrackCircuitPartialPathRouteSaveState>
    {
        private readonly List<TrackCircuitRouteElement> list;
        private ILookup<int, int> items;
        private ILookup<int, TrackDirection> routeDirections;

        #region interface implementation
        public int Count => list.Count;

        public bool IsReadOnly => false;

        public TrackCircuitRouteElement this[int index] { get => list[index]; set => list[index] = value; }

        public int IndexOf(TrackCircuitRouteElement item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, TrackCircuitRouteElement item)
        {
            list.Insert(index, item);
            items = null;
            routeDirections = null;
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
            items = null;
            routeDirections = null;
        }

        public void Add(TrackCircuitRouteElement item)
        {
            list.Add(item);
            items = null;
            routeDirections = null;
        }

        public void Clear()
        {
            list.Clear();
            items = null;
            routeDirections = null;
        }

        public bool Contains(TrackCircuitRouteElement item)
        {
            return list.Contains(item);
        }

        public void CopyTo(TrackCircuitRouteElement[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TrackCircuitRouteElement item)
        {
            items = null;
            routeDirections = null;
            return list.Remove(item);
        }

        public IEnumerator<TrackCircuitRouteElement> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public void RemoveRange(int index, int count)
        {
            list.RemoveRange(index, count);
            items = null;
            routeDirections = null;
        }
        #endregion

        // Base constructor
        public TrackCircuitPartialPathRoute()
        {
            list = new List<TrackCircuitRouteElement>();
        }


        // Constructor from existing subpath
        public TrackCircuitPartialPathRoute(TrackCircuitPartialPathRoute source)
        {
            list = (source?.list == null) ? new List<TrackCircuitRouteElement>() : new List<TrackCircuitRouteElement>(source.list);
        }

        // Constructor from part of existing subpath
        // if either value is < 0, start from start or stop at end
        public TrackCircuitPartialPathRoute(TrackCircuitPartialPathRoute source, int startIndex, int endIndex)
        {
            ArgumentNullException.ThrowIfNull(source);

            list = new List<TrackCircuitRouteElement>();
            startIndex = Math.Max(startIndex, 0);
            endIndex = Math.Min(source.Count - 1, endIndex);
            endIndex = Math.Max(startIndex, endIndex);

            for (int i = startIndex; i <= endIndex; i++)
            {
                Add(new TrackCircuitRouteElement(source[i]));
            }
        }

        public async ValueTask<TrackCircuitPartialPathRouteSaveState> Snapshot()
        {
            ConcurrentBag<TrackCircuitRouteElementSaveState> routeElementSaveStates = new ConcurrentBag<TrackCircuitRouteElementSaveState>();
            await Parallel.ForEachAsync(this, async (element, cancellationToken) =>
            {
                routeElementSaveStates.Add(await element.Snapshot().ConfigureAwait(false));
            });

            return new TrackCircuitPartialPathRouteSaveState()
            {
                RouteElements = new Collection<TrackCircuitRouteElementSaveState>(routeElementSaveStates.ToList()),
            };
        }

        public async ValueTask Restore(TrackCircuitPartialPathRouteSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            list.Clear();
            items = null;
            routeDirections = null;

            ConcurrentBag<TrackCircuitRouteElement> routeElements = new ConcurrentBag<TrackCircuitRouteElement>();
            if (saveState.RouteElements != null)
            {
                await Parallel.ForEachAsync(saveState.RouteElements, async (trackCircuitRouteElementSaveState, cancellationToken) =>
                {
                    TrackCircuitRouteElement routeElement = new TrackCircuitRouteElement();
                    await routeElement.Restore(trackCircuitRouteElementSaveState).ConfigureAwait(false);
                    routeElements.Add(routeElement);
                });
            }
            list.AddRange(routeElements);
        }

        // Restore
        public TrackCircuitPartialPathRoute(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);
            list = new List<TrackCircuitRouteElement>();

            int totalElements = inf.ReadInt32();

            for (int i = 0; i < totalElements; i++)
            {
                Add(new TrackCircuitRouteElement(inf));
            }
        }

        // Save
        public void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);
            outf.Write(Count);
            foreach (TrackCircuitRouteElement element in this)
            {
                element.Save(outf);
            }
        }

        private void ReIndex()
        {
            items = list.Select((i, Index) => (i.TrackCircuitSection.Index, Index)).ToLookup(pair => pair.Item1, pair => pair.Item2);
        }

        /// <summary>
        /// Get sectionindex in subpath
        /// <\summary>
        public int GetRouteIndex(int sectionIndex, int startIndex)
        {
            if (Count == 1 && startIndex == 0 && this[0].TrackCircuitSection.Index == sectionIndex)
            {
                return 0;
            }
            else if (Count > 1 && startIndex < Count)
            {
                if (items == null)
                {
                    ReIndex();
                }
                if (items.Contains(sectionIndex))
                {
                    foreach (int item in items[sectionIndex])
                        if (item >= startIndex)
                            return item;
                }
            }
            return -1;
        }

        //================================================================================================//
        /// <summary>
        /// Get sectionindex in subpath
        /// <\summary>

        public int GetRouteIndexBackward(int sectionIndex, int startIndex)
        {
            if (Count == 1 && startIndex == 1 && this[0].TrackCircuitSection.Index == sectionIndex)
            {
                return 0;
            }
            else if (Count > 1 && startIndex < Count)
            {
                if (items == null)
                {
                    ReIndex();
                }
                if (items.Contains(sectionIndex))
                {
                    foreach (int item in items[sectionIndex])
                        if (item < startIndex)
                            return item;
                }
            }
            return -1;
        }

        //================================================================================================//
        /// <summary>
        /// returns if signal is ahead of train
        /// <\summary>
        internal bool SignalIsAheadOfTrain(Signal signal, TrackCircuitPosition trainPosition)
        {
            ArgumentNullException.ThrowIfNull(signal);
            ArgumentNullException.ThrowIfNull(trainPosition);

            int signalSection = signal.TrackCircuitIndex;
            int signalRouteIndex = GetRouteIndexBackward(signalSection, trainPosition.RouteListIndex);
            if (signalRouteIndex >= 0)
                return false;  // signal section passed earlier in route

            signalRouteIndex = GetRouteIndex(signalSection, trainPosition.RouteListIndex);
            if (signalRouteIndex >= 0)
                return true; // signal section still ahead

            if (trainPosition.TrackCircuitSectionIndex == signal.TrackCircuitNextIndex)
                return false; // if train in section following signal, assume we passed

            // signal is not on route - assume we did not pass
            return true;
        }

        //================================================================================================//
        /// <summary>
        /// returns distance along route
        /// <\summary>
        public float GetDistanceAlongRoute(int startIndex, float startOffset, int endIndex, float endOffset, bool forward)
        {
            // startSectionIndex and endSectionIndex are indices in route list
            // startOffset is remaining length of startSection in required direction
            // endOffset is length along endSection in required direction

            if (startIndex == endIndex && startIndex > -1)
            {
                return startOffset - (this[startIndex].TrackCircuitSection.Length - endOffset);
            }

            if (forward)
            {
                if (startIndex > endIndex)
                    return -1;

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    startOffset += this[i].TrackCircuitSection.Length;
                }
            }
            else
            {
                if (startIndex < endIndex)
                    return -1;

                for (int i = startIndex - 1; i > endIndex; i--)
                {
                    startOffset += this[i].TrackCircuitSection.Length;
                }
            }

            startOffset += endOffset;
            return startOffset;
        }

        //================================================================================================//
        //
        // Converts list of elements to dictionary
        //
        public ILookup<int, TrackDirection> ConvertRoute()
        {
            if (routeDirections == null)
            {
                routeDirections = list.ToLookup(item => item.TrackCircuitSection.Index, item => item.Direction);
            }
            return routeDirections;
        }

        //================================================================================================//
        /// <summary>
        /// check if subroute contains section
        /// <\summary>
        internal bool ContainsSection(TrackCircuitRouteElement routeElement)
        {
            if (Count == 1 && this[0].TrackCircuitSection.Index == routeElement.TrackCircuitSection.Index)
            {
                return true;
            }
            else if (Count > 1)
            {
                if (items == null)
                    ReIndex();
                return items.Contains(routeElement.TrackCircuitSection.Index);
            }
            return false;
        }

        //================================================================================================//
        /// <summary>
        /// Find actual diverging path from alternative path definition
        /// Returns : [0,*] = Main Route, [1,*] = Alt Route, [*,0] = Start Index, [*,1] = End Index
        /// <\summary>
        internal bool HasActualDivergePath(TrackCircuitPartialPathRoute altRoute, int startIndex)
        {
            for (int i = 0; i < altRoute.Count; i++)
            {
                int mainIndex = i + startIndex;
                if (altRoute[i].TrackCircuitSection.Index != this[mainIndex].TrackCircuitSection.Index)
                {
                    return true;
                }
            }
            return false;
        }

        ////================================================================================================//
        ///// <summary>
        ///// Find actual diverging path from alternative path definition
        ///// Returns : [0,*] = Main Route, [1,*] = Alt Route, [*,0] = Start Index, [*,1] = End Index
        ///// <\summary>

        //public int[,] FindActualDivergePath(TrackCircuitPartialPathRoute altRoute, int startIndex, int endIndex)
        //{
        //    int[,] returnValue = new int[2, 2] { { -1, -1 }, { -1, -1 } };

        //    bool firstfound = false;
        //    bool lastfound = false;

        //    int MainPathActualStartRouteIndex = -1;
        //    int MainPathActualEndRouteIndex = -1;
        //    int AltPathActualStartRouteIndex = -1;
        //    int AltPathActualEndRouteIndex = -1;

        //    for (int iIndex = 0; iIndex < altRoute.Count && !firstfound; iIndex++)
        //    {
        //        int mainIndex = iIndex + startIndex;
        //        if (altRoute[iIndex].TrackCircuitSection.Index != this[mainIndex].TrackCircuitSection.Index)
        //        {
        //            firstfound = true;
        //            MainPathActualStartRouteIndex = mainIndex;
        //            AltPathActualStartRouteIndex = iIndex;
        //        }
        //    }

        //    for (int iIndex = 0; iIndex < altRoute.Count && firstfound && !lastfound; iIndex++)
        //    {
        //        int altIndex = altRoute.Count - 1 - iIndex;
        //        int mainIndex = endIndex - iIndex;
        //        if (altRoute[altIndex].TrackCircuitSection.Index != this[mainIndex].TrackCircuitSection.Index)
        //        {
        //            lastfound = true;
        //            MainPathActualEndRouteIndex = mainIndex;
        //            AltPathActualEndRouteIndex = altIndex;
        //        }
        //    }

        //    if (lastfound)
        //    {
        //        returnValue[0, 0] = MainPathActualStartRouteIndex;
        //        returnValue[0, 1] = MainPathActualEndRouteIndex;
        //        returnValue[1, 0] = AltPathActualStartRouteIndex;
        //        returnValue[1, 1] = AltPathActualEndRouteIndex;
        //    }

        //    return (returnValue);
        //}

        //================================================================================================//
        /// <summary>
        /// Get usefull length
        /// Returns : dictionary with : 
        ///    key is last section to be used in path (before signal or node)
        ///    value is usefull length
        /// <\summary>
        public (int sectionIndex, float length) GetUsefullLength(float defaultSignalClearingDistance, int startIndex, int endIndex)
        {
            bool endSignal = false;

            int usedStartIndex = (startIndex >= 0) ? startIndex : 0;
            int usedEndIndex = (endIndex > 0 && endIndex <= Count - 1) ? endIndex : Count - 1;
            int lastUsedIndex = startIndex;

            // first junction
            TrackCircuitSection firstSection = this[usedStartIndex].TrackCircuitSection;
            float actLength;
            if (firstSection.CircuitType == TrackCircuitType.Junction)
            {
                actLength = firstSection.Length - (float)(2 * firstSection.Overlap);
            }
            else
            {
                actLength = firstSection.Length;
            }

            float useLength = actLength;

            // intermediate sections

            for (int i = usedStartIndex + 1; i < usedEndIndex - 1; i++)
            {
                TrackCircuitRouteElement routeElement = this[i];
                TrackCircuitSection thisSection = routeElement.TrackCircuitSection;
                actLength += thisSection.Length;

                // if section has end signal, set usefull length upto this point
                if (thisSection.EndSignals[routeElement.Direction] != null)
                {
                    useLength = actLength - (2 * defaultSignalClearingDistance);
                    endSignal = true;
                    lastUsedIndex = i - usedStartIndex;
                }
            }

            // last section if no signal found

            if (!endSignal)
            {
                TrackCircuitSection lastSection = this[usedEndIndex].TrackCircuitSection;
                if (lastSection.CircuitType == TrackCircuitType.Junction)
                {
                    actLength += (lastSection.Length - (float)(2 * lastSection.Overlap));
                    lastUsedIndex = usedEndIndex - usedStartIndex - 1;
                }
                else
                {
                    actLength += lastSection.Length;
                    lastUsedIndex = usedEndIndex - usedStartIndex;
                }

                useLength = actLength;
            }

            return (lastUsedIndex, useLength);
        }

        //================================================================================================//
        /// <summary>
        /// compares if equal to other path
        /// paths must be exactly equal (no part check)
        /// <\summary>
        public bool EqualsPath(TrackCircuitPartialPathRoute otherRoute)
        {
            // check common route parts
            if (otherRoute == null || Count != otherRoute.Count)
                return false;  // if path lengths are unequal they cannot be the same

            for (int i = 0; i < Count - 1; i++)
            {
                if (this[i].TrackCircuitSection.Index != otherRoute[i].TrackCircuitSection.Index)
                {
                    return false;
                }
            }

            return true;
        }

        //================================================================================================//
        /// <summary>
        /// compares if equal to other path in reverse
        /// paths must be exactly equal (no part check)
        /// <\summary>

        public bool EqualsReversePath(TrackCircuitPartialPathRoute otherRoute)
        {
            // check common route parts
            if (otherRoute == null || Count != otherRoute.Count)
                return false;  // if path lengths are unequal they cannot be the same

            for (int i = 0; i < Count - 1; i++)
            {
                if (this[i].TrackCircuitSection.Index != otherRoute[otherRoute.Count - 1 - i].TrackCircuitSection.Index)
                {
                    return false;
                }
            }

            return true;
        }

        //================================================================================================//
        /// <summary>
        /// reverses existing path
        /// <\summary>
        public TrackCircuitPartialPathRoute ReversePath()
        {
            TrackCircuitPartialPathRoute reversePath = new TrackCircuitPartialPathRoute();
            int lastSectionIndex = -1;

            for (int i = Count - 1; i >= 0; i--)
            {
                TrackCircuitRouteElement routeElement = this[i];
                TrackCircuitSection section = routeElement.TrackCircuitSection;

                TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(section, routeElement.Direction.Reverse(), lastSectionIndex);

                // reset outpin for JUNCTION
                // if trailing, pin[0] = 0, pin[1] = 0
                // if facing, pin[0] = 1, check next element for pin[1]

                if (section.CircuitType == TrackCircuitType.Junction)
                {
                    if (newElement.FacingPoint)
                    {
                        if (i >= 1)
                        {
                            newElement.OutPin[SignalLocation.NearEnd] = TrackDirection.Reverse;
                            newElement.OutPin[SignalLocation.FarEnd] = (section.Pins[TrackDirection.Reverse, SignalLocation.NearEnd].Link == this[i - 1].TrackCircuitSection.Index) ? TrackDirection.Ahead : TrackDirection.Reverse;
                        }
                    }
                    else
                    {
                        newElement.OutPin[SignalLocation.NearEnd] = TrackDirection.Ahead;
                        newElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
                    }
                }

                reversePath.Add(newElement);
                lastSectionIndex = routeElement.TrackCircuitSection.Index;
            }

            return reversePath;
        }

        /// <summary>
        /// Check if a train is waiting for a stationary (stopped) train or a train in manual mode 
        /// </summary>
        public bool CheckStoppedTrains()
        {
            foreach (TrackCircuitRouteElement routeElement in this)
            {
                TrackCircuitSection section = routeElement.TrackCircuitSection;
                foreach (KeyValuePair<Train.TrainRouted, Direction> item in section.CircuitState.OccupationState)
                {
                    if (item.Key.Train.SpeedMpS == 0.0f || item.Key.Train.ControlMode == TrainControlMode.Manual)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }// end class TCSubpathRoute
}
