using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Orts.Common;
using Orts.Simulation.Signalling;

using static Orts.Simulation.Physics.Train;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// Subpath list : list of TCRouteElements building a subpath
    /// </summary>
    public class TCSubpathRoute : List<TrackCircuitRouteElement>
    {

        // Base constructor
        public TCSubpathRoute()
        {
        }


        // Constructor from existing subpath
        public TCSubpathRoute(TCSubpathRoute source):
            base(source ?? Enumerable.Empty<TrackCircuitRouteElement>())
        {
        }

        // Constructor from part of existing subpath
        // if either value is < 0, start from start or stop at end
        public TCSubpathRoute(TCSubpathRoute source, int startIndex, int endIndex)
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
        public TCSubpathRoute(BinaryReader inf)
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
            for (int iNode = startIndex; iNode >= 0 && iNode < this.Count; iNode++)
            {
                TrackCircuitRouteElement thisElement = this[iNode];
                if (thisElement.TrackCircuitSectionIndex == sectionIndex)
                {
                    return (iNode);
                }
            }

            return (-1);
        }

        //================================================================================================//
        /// <summary>
        /// Get sectionindex in subpath
        /// <\summary>

        public int GetRouteIndexBackward(int thisSectionIndex, int startIndex)
        {
            for (int iNode = startIndex - 1; iNode >= 0 && iNode < this.Count; iNode--)
            {
                TrackCircuitRouteElement thisElement = this[iNode];
                if (thisElement.TrackCircuitSectionIndex == thisSectionIndex)
                {
                    return (iNode);
                }
            }

            return (-1);
        }

        //================================================================================================//
        /// <summary>
        /// returns if signal is ahead of train
        /// <\summary>

        public bool SignalIsAheadOfTrain(Signal thisSignal, TCPosition trainPosition)
        {
            int signalSection = thisSignal.TrackCircuitIndex;
            int signalRouteIndex = GetRouteIndexBackward(signalSection, trainPosition.RouteListIndex);
            if (signalRouteIndex >= 0)
                return (false);  // signal section passed earlier in route
            signalRouteIndex = GetRouteIndex(signalSection, trainPosition.RouteListIndex);
            if (signalRouteIndex >= 0)
                return (true); // signal section still ahead

            if (trainPosition.TCSectionIndex == thisSignal.TrackCircuitNextIndex)
                return (false); // if train in section following signal, assume we passed

            // signal is not on route - assume we did not pass
            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// returns distance along route
        /// <\summary>

        public float GetDistanceAlongRoute(int startSectionIndex, float startOffset,
           int endSectionIndex, float endOffset, bool forward, SignalEnvironment signals)

        // startSectionIndex and endSectionIndex are indices in route list
        // startOffset is remaining length of startSection in required direction
        // endOffset is length along endSection in required direction
        {
            float totalLength = startOffset;

            if (startSectionIndex == endSectionIndex)
            {
                TrackCircuitSection thisSection = signals.TrackCircuitList[this[startSectionIndex].TrackCircuitSectionIndex];
                totalLength = startOffset - (thisSection.Length - endOffset);
                return (totalLength);
            }

            if (forward)
            {
                if (startSectionIndex > endSectionIndex)
                    return (-1);

                for (int iIndex = startSectionIndex + 1; iIndex < endSectionIndex; iIndex++)
                {
                    TrackCircuitSection thisSection = signals.TrackCircuitList[this[iIndex].TrackCircuitSectionIndex];
                    totalLength += thisSection.Length;
                }
            }
            else
            {
                if (startSectionIndex < endSectionIndex)
                    return (-1);

                for (int iIndex = startSectionIndex - 1; iIndex > endSectionIndex; iIndex--)
                {
                    TrackCircuitSection thisSection = signals.TrackCircuitList[this[iIndex].TrackCircuitSectionIndex];
                    totalLength += thisSection.Length;
                }
            }

            totalLength += endOffset;

            return (totalLength);
        }

        //================================================================================================//
        /// <summary>
        /// returns if position is ahead of train
        /// <\summary>

        // without offset
        public static bool IsAheadOfTrain(TrackCircuitSection thisSection, TCPosition trainPosition)
        {
            float distanceAhead = TrackCircuitSection.GetDistanceBetweenObjects(
                trainPosition.TCSectionIndex, trainPosition.TCOffset, (TrackDirection)trainPosition.TCDirection,
                    thisSection.Index, 0.0f);
            return (distanceAhead > 0.0f);
        }

        // with offset
        public static bool IsAheadOfTrain(TrackCircuitSection thisSection, float offset, TCPosition trainPosition)
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
                if (!thisDict.ContainsKey(thisElement.TrackCircuitSectionIndex))
                {
                    thisDict.Add(thisElement.TrackCircuitSectionIndex, (int)thisElement.Direction);
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
            return (thisRoute.ContainsKey(thisElement.TrackCircuitSectionIndex));
        }


        //================================================================================================//
        /// <summary>
        /// Find actual diverging path from alternative path definition
        /// Returns : [0,*] = Main Route, [1,*] = Alt Route, [*,0] = Start Index, [*,1] = End Index
        /// <\summary>

        public int[,] FindActualDivergePath(TCSubpathRoute altRoute, int startIndex, int endIndex)
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
                if (altRoute[iIndex].TrackCircuitSectionIndex != this[mainIndex].TrackCircuitSectionIndex)
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
                if (altRoute[altIndex].TrackCircuitSectionIndex != this[mainIndex].TrackCircuitSectionIndex)
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
            TrackCircuitSection firstSection = TrackCircuitSection.TrackCircuitList[this[usedStartIndex].TrackCircuitSectionIndex];
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
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisElement.TrackCircuitSectionIndex];
                actLength += thisSection.Length;

                // if section has end signal, set usefull length upto this point
                if (thisSection.EndSignals[(TrackDirection)thisElement.Direction] != null)
                {
                    useLength = actLength - (2 * defaultSignalClearingDistance);
                    endSignal = true;
                    lastUsedIndex = iSection - usedStartIndex;
                }
            }

            // last section if no signal found

            if (!endSignal)
            {
                TrackCircuitSection lastSection = TrackCircuitSection.TrackCircuitList[this[usedEndIndex].TrackCircuitSectionIndex];
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

        public bool EqualsPath(TCSubpathRoute otherRoute)
        {
            // check common route parts

            if (Count != otherRoute.Count) return (false);  // if path lengths are unequal they cannot be the same

            bool equalPath = true;

            for (int iIndex = 0; iIndex < Count - 1; iIndex++)
            {
                if (this[iIndex].TrackCircuitSectionIndex != otherRoute[iIndex].TrackCircuitSectionIndex)
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

        public bool EqualsReversePath(TCSubpathRoute otherRoute)
        {
            // check common route parts

            if (Count != otherRoute.Count) return (false);  // if path lengths are unequal they cannot be the same

            bool equalPath = true;

            for (int iIndex = 0; iIndex < Count - 1; iIndex++)
            {
                if (this[iIndex].TrackCircuitSectionIndex != otherRoute[otherRoute.Count - 1 - iIndex].TrackCircuitSectionIndex)
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

        public TCSubpathRoute ReversePath()
        {
            TCSubpathRoute reversePath = new TCSubpathRoute();
            int lastSectionIndex = -1;

            for (int iIndex = Count - 1; iIndex >= 0; iIndex--)
            {
                TrackCircuitRouteElement thisElement = this[iIndex];
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisElement.TrackCircuitSectionIndex];

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
                            newElement.OutPin[Location.FarEnd] = (thisSection.Pins[TrackDirection.Reverse, Location.NearEnd].Link == this[iIndex - 1].TrackCircuitSectionIndex) ? TrackDirection.Ahead : TrackDirection.Reverse;
                        }
                    }
                    else
                    {
                        newElement.OutPin[Location.NearEnd] = TrackDirection.Ahead;
                        newElement.OutPin[Location.FarEnd] = TrackDirection.Ahead;
                    }
                }

                reversePath.Add(newElement);
                lastSectionIndex = thisElement.TrackCircuitSectionIndex;
            }

            return (reversePath);
        }
    }// end class TCSubpathRoute
}
