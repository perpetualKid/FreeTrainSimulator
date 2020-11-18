using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// Subpath list : list of TCRouteElements building a subpath
    /// </summary>
    public class TrackCircuitPartialPathRoute : List<TrackCircuitRouteElement>
    {

        // Base constructor
        public TrackCircuitPartialPathRoute()
        {
        }


        // Constructor from existing subpath
        public TrackCircuitPartialPathRoute(TrackCircuitPartialPathRoute source):
            base(source ?? Enumerable.Empty<TrackCircuitRouteElement>())
        {
        }

        // Constructor from part of existing subpath
        // if either value is < 0, start from start or stop at end
        public TrackCircuitPartialPathRoute(TrackCircuitPartialPathRoute source, int startIndex, int endIndex)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));

            startIndex = Math.Max(startIndex, 0);
            endIndex = Math.Min(source.Count - 1, endIndex);
            endIndex = Math.Max(startIndex, endIndex);

            for (int i = startIndex; i <= endIndex; i++)
            {
                Add(new TrackCircuitRouteElement(source[i]));
            }
        }

        // Restore
        public TrackCircuitPartialPathRoute(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            int totalElements = inf.ReadInt32();

            for (int i = 0; i < totalElements; i++)
            {
                Add(new TrackCircuitRouteElement(inf));
            }
        }

        // Save
        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));
            outf.Write(Count);
            foreach (TrackCircuitRouteElement element in this)
            {
                element.Save(outf);
            }
        }

        /// <summary>
        /// Get sectionindex in subpath
        /// <\summary>
        public int GetRouteIndex(int sectionIndex, int startIndex)
        {
            for (int i = startIndex; i >= 0 && i < Count; i++)
            {
                TrackCircuitRouteElement thisElement = this[i];
                if (thisElement.TrackCircuitSection.Index == sectionIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        //================================================================================================//
        /// <summary>
        /// Get sectionindex in subpath
        /// <\summary>

        public int GetRouteIndexBackward(int thisSectionIndex, int startIndex)
        {
            for (int iNode = startIndex - 1; iNode >= 0 && iNode < Count; iNode--)
            {
                TrackCircuitRouteElement thisElement = this[iNode];
                if (thisElement.TrackCircuitSection.Index == thisSectionIndex)
                {
                    return iNode;
                }
            }

            return -1;
        }

        //================================================================================================//
        /// <summary>
        /// returns if signal is ahead of train
        /// <\summary>
        public bool SignalIsAheadOfTrain(Signal signal, Train.TCPosition trainPosition)
        {
            if (null == signal)
                throw new ArgumentNullException(nameof(signal));
            if (null == trainPosition)
                throw new ArgumentNullException(nameof(trainPosition));

            int signalSection = signal.TrackCircuitIndex;
            int signalRouteIndex = GetRouteIndexBackward(signalSection, trainPosition.RouteListIndex);
            if (signalRouteIndex >= 0)
                return false;  // signal section passed earlier in route

            signalRouteIndex = GetRouteIndex(signalSection, trainPosition.RouteListIndex);
            if (signalRouteIndex >= 0)
                return true; // signal section still ahead

            if (trainPosition.TCSectionIndex == signal.TrackCircuitNextIndex)
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

            if (startIndex == endIndex)
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
        /// <summary>
        /// returns if position is ahead of train
        /// <\summary>

        // without offset
        public static bool IsAheadOfTrain(TrackCircuitSection thisSection, Train.TCPosition trainPosition)
        {
            float distanceAhead = TrackCircuitSection.GetDistanceBetweenObjects(
                trainPosition.TCSectionIndex, trainPosition.TCOffset, (TrackDirection)trainPosition.TCDirection,
                    thisSection.Index, 0.0f);
            return (distanceAhead > 0.0f);
        }

        // with offset
        public static bool IsAheadOfTrain(TrackCircuitSection thisSection, float offset, Train.TCPosition trainPosition)
        {
            float distanceAhead = TrackCircuitSection.GetDistanceBetweenObjects(
                trainPosition.TCSectionIndex, trainPosition.TCOffset, (TrackDirection)trainPosition.TCDirection,
                    thisSection.Index, offset);
            return (distanceAhead > 0.0f);
        }

        //================================================================================================//
        //
        // Converts list of elements to dictionary
        //

        public Dictionary<int, int> ConvertRoute()
        {
            Dictionary<int, int> thisDict = new Dictionary<int, int>();

            foreach (TrackCircuitRouteElement thisElement in this)
            {
                if (!thisDict.ContainsKey(thisElement.TrackCircuitSection.Index))
                {
                    thisDict.Add(thisElement.TrackCircuitSection.Index, (int)thisElement.Direction);
                }
            }

            return (thisDict);
        }

        //================================================================================================//
        /// <summary>
        /// check if subroute contains section
        /// <\summary>

        internal bool ContainsSection(TrackCircuitRouteElement thisElement)
        {
            // convert route to dictionary

            Dictionary<int, int> thisRoute = ConvertRoute();
            return (thisRoute.ContainsKey(thisElement.TrackCircuitSection.Index));
        }


        //================================================================================================//
        /// <summary>
        /// Find actual diverging path from alternative path definition
        /// Returns : [0,*] = Main Route, [1,*] = Alt Route, [*,0] = Start Index, [*,1] = End Index
        /// <\summary>

        public int[,] FindActualDivergePath(TrackCircuitPartialPathRoute altRoute, int startIndex, int endIndex)
        {
            int[,] returnValue = new int[2, 2] { { -1, -1 }, { -1, -1 } };

            bool firstfound = false;
            bool lastfound = false;

            int MainPathActualStartRouteIndex = -1;
            int MainPathActualEndRouteIndex = -1;
            int AltPathActualStartRouteIndex = -1;
            int AltPathActualEndRouteIndex = -1;

            for (int iIndex = 0; iIndex < altRoute.Count && !firstfound; iIndex++)
            {
                int mainIndex = iIndex + startIndex;
                if (altRoute[iIndex].TrackCircuitSection.Index != this[mainIndex].TrackCircuitSection.Index)
                {
                    firstfound = true;
                    MainPathActualStartRouteIndex = mainIndex;
                    AltPathActualStartRouteIndex = iIndex;
                }
            }

            for (int iIndex = 0; iIndex < altRoute.Count && firstfound && !lastfound; iIndex++)
            {
                int altIndex = altRoute.Count - 1 - iIndex;
                int mainIndex = endIndex - iIndex;
                if (altRoute[altIndex].TrackCircuitSection.Index != this[mainIndex].TrackCircuitSection.Index)
                {
                    lastfound = true;
                    MainPathActualEndRouteIndex = mainIndex;
                    AltPathActualEndRouteIndex = altIndex;
                }
            }

            if (lastfound)
            {
                returnValue[0, 0] = MainPathActualStartRouteIndex;
                returnValue[0, 1] = MainPathActualEndRouteIndex;
                returnValue[1, 0] = AltPathActualStartRouteIndex;
                returnValue[1, 1] = AltPathActualEndRouteIndex;
            }

            return (returnValue);
        }

        //================================================================================================//
        /// <summary>
        /// Get usefull length
        /// Returns : dictionary with : 
        ///    key is last section to be used in path (before signal or node)
        ///    value is usefull length
        /// <\summary>

        public Dictionary<int, float> GetUsefullLength(float defaultSignalClearingDistance, int startIndex, int endIndex)
        {
            float actLength = 0.0f;
            float useLength = 0.0f;
            bool endSignal = false;

            int usedStartIndex = (startIndex >= 0) ? startIndex : 0;
            int usedEndIndex = (endIndex > 0 && endIndex <= Count - 1) ? endIndex : Count - 1;
            int lastUsedIndex = startIndex;

            // first junction
            TrackCircuitSection firstSection = this[usedStartIndex].TrackCircuitSection;
            if (firstSection.CircuitType == TrackCircuitType.Junction)
            {
                actLength = firstSection.Length - (float)(2 * firstSection.Overlap);
            }
            else
            {
                actLength = firstSection.Length;
            }

            useLength = actLength;

            // intermediate sections

            for (int iSection = usedStartIndex + 1; iSection < usedEndIndex - 1; iSection++)
            {
                TrackCircuitRouteElement thisElement = this[iSection];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;
                actLength += thisSection.Length;

                // if section has end signal, set usefull length upto this point
                if (thisSection.EndSignals[thisElement.Direction] != null)
                {
                    useLength = actLength - (2 * defaultSignalClearingDistance);
                    endSignal = true;
                    lastUsedIndex = iSection - usedStartIndex;
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

            return (new Dictionary<int, float>() { { lastUsedIndex, useLength } });
        }

        //================================================================================================//
        /// <summary>
        /// compares if equal to other path
        /// paths must be exactly equal (no part check)
        /// <\summary>

        public bool EqualsPath(TrackCircuitPartialPathRoute otherRoute)
        {
            // check common route parts

            if (Count != otherRoute.Count) return (false);  // if path lengths are unequal they cannot be the same

            bool equalPath = true;

            for (int iIndex = 0; iIndex < Count - 1; iIndex++)
            {
                if (this[iIndex].TrackCircuitSection.Index != otherRoute[iIndex].TrackCircuitSection.Index)
                {
                    equalPath = false;
                    break;
                }
            }

            return (equalPath);
        }

        //================================================================================================//
        /// <summary>
        /// compares if equal to other path in reverse
        /// paths must be exactly equal (no part check)
        /// <\summary>

        public bool EqualsReversePath(TrackCircuitPartialPathRoute otherRoute)
        {
            // check common route parts

            if (Count != otherRoute.Count) return (false);  // if path lengths are unequal they cannot be the same

            bool equalPath = true;

            for (int iIndex = 0; iIndex < Count - 1; iIndex++)
            {
                if (this[iIndex].TrackCircuitSection.Index != otherRoute[otherRoute.Count - 1 - iIndex].TrackCircuitSection.Index)
                {
                    equalPath = false;
                    break;
                }
            }

            return (equalPath);
        }

        //================================================================================================//
        /// <summary>
        /// reverses existing path
        /// <\summary>

        public TrackCircuitPartialPathRoute ReversePath()
        {
            TrackCircuitPartialPathRoute reversePath = new TrackCircuitPartialPathRoute();
            int lastSectionIndex = -1;

            for (int iIndex = Count - 1; iIndex >= 0; iIndex--)
            {
                TrackCircuitRouteElement thisElement = this[iIndex];
                TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(thisSection, thisElement.Direction.Next(), lastSectionIndex);

                // reset outpin for JUNCTION
                // if trailing, pin[0] = 0, pin[1] = 0
                // if facing, pin[0] = 1, check next element for pin[1]

                if (thisSection.CircuitType == TrackCircuitType.Junction)
                {
                    if (newElement.FacingPoint)
                    {
                        if (iIndex >= 1)
                        {
                            newElement.OutPin[Location.NearEnd] = TrackDirection.Reverse;
                            newElement.OutPin[Location.FarEnd] = (thisSection.Pins[TrackDirection.Reverse, Location.NearEnd].Link == this[iIndex - 1].TrackCircuitSection.Index) ? TrackDirection.Ahead : TrackDirection.Reverse;
                        }
                    }
                    else
                    {
                        newElement.OutPin[Location.NearEnd] = TrackDirection.Ahead;
                        newElement.OutPin[Location.FarEnd] = TrackDirection.Ahead;
                    }
                }

                reversePath.Add(newElement);
                lastSectionIndex = thisElement.TrackCircuitSection.Index;
            }

            return (reversePath);
        }
    }// end class TCSubpathRoute
}
