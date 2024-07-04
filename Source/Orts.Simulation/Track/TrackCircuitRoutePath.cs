using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Models.State;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{

    /// <summary>
    /// Track Circuit Route Path
    /// </summary>
    internal class TrackCircuitRoutePath : ISaveStateApi<TrackCircuitRoutePathSaveState>
    {
        public List<TrackCircuitPartialPathRoute> TCRouteSubpaths { get; } = new List<TrackCircuitPartialPathRoute>();
        public List<TrackCircuitPartialPathRoute> TCAlternativePaths { get; } = new List<TrackCircuitPartialPathRoute>();
        public int ActiveSubPath { get; set; }
        public int ActiveAlternativePath { get; set; }
        public List<WaitingPointDetail> WaitingPoints { get; } = new List<WaitingPointDetail>(); // [0] = sublist in which WP is placed; 
                                                                                                 // [1] = WP section; [2] = WP wait time (delta); [3] = WP depart time;
                                                                                                 // [4] = hold signal
        public List<TrackCircuitReversalInfo> ReversalInfo { get; } = new List<TrackCircuitReversalInfo>();
        public List<RoughReversalInfo> RoughReversalInfos { get; } = new List<RoughReversalInfo>();
        public List<int> LoopEnd { get; } = new List<int>();
        public Dictionary<string, int[]> StationCrossReferences { get; } = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        // int[0] = subpath index, int[1] = element index, int[2] = platform ID
        public int OriginalSubpath { get; set; } = -1; // reminds original subpath when train manually rerouted

        public TrackCircuitRoutePath() { }

        /// <summary>
        /// Constructor (from AIPath)
        /// </summary>
        public TrackCircuitRoutePath(AIPath aiPath, TrackDirection direction, float thisTrainLength, int trainNumber)
        {
            ActiveSubPath = 0;
            ActiveAlternativePath = -1;
            float offset = 0;

            //
            // collect all TC Elements
            //
            // get tracknode from first path node
            //
            int sublist = 0;

            Dictionary<int, int[]> alternativeRoutes = new Dictionary<int, int[]>();
            Queue<int> ActiveAlternativeRoutes = new Queue<int>();

            //  Create the first TCSubpath into the TCRoute
            TrackCircuitPartialPathRoute thisSubpath = new TrackCircuitPartialPathRoute();
            TCRouteSubpaths.Add(thisSubpath);

            TrackDirection currentDir = direction;
            TrackDirection newDir = direction;

            List<float> reversalOffset = new List<float>();
            List<int> reversalIndex = new List<int>();

            //
            // if original direction not set, determine it through first switch
            //
            if ((int)direction < -1)
            {
                bool firstSwitch = false;
                int prevTNode = 0;
                TrackDirection jnDir = TrackDirection.Ahead;

                for (int i = 0; i < aiPath.Nodes.Count - 1 && !firstSwitch; i++)
                {
                    AIPathNode pNode = aiPath.Nodes[i];
                    if (pNode.JunctionIndex > 0)
                    {
                        TrackJunctionNode jn = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[pNode.JunctionIndex];
                        firstSwitch = true;
                        for (int iPin = 0; iPin < jn.TrackPins.Length; iPin++)
                        {
                            if (jn.TrackPins[iPin].Link == prevTNode)
                            {
                                jnDir = jn.TrackPins[iPin].Direction.Reverse();
                            }
                        }
                    }
                    else
                    {
                        if (pNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.Other)
                            prevTNode = pNode.NextMainTVNIndex;
                    }
                }

                currentDir = jnDir;
            }

            //
            // loop through path nodes
            //

            AIPathNode currentPathNode = aiPath.Nodes[0];
            AIPathNode nextPathNode = null;
            AIPathNode lastPathNode = null;

            int trackNodeIndex = currentPathNode.NextMainTVNIndex;
            TrackNode thisNode = null;

            currentPathNode = currentPathNode.NextMainNode;
            int reversal = 0;

            while (currentPathNode != null)
            {
                lastPathNode = currentPathNode;

                // process siding items

                if (currentPathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.SidingStart)
                {
                    TrackNode sidingNode = RuntimeData.Instance.TrackDB.TrackNodes[currentPathNode.JunctionIndex];
                    int startTCSectionIndex = sidingNode.TrackCircuitCrossReferences[0].Index;
                    int[] altRouteReference = new int[3];
                    altRouteReference[0] = sublist;
                    altRouteReference[1] = currentPathNode.Index;
                    altRouteReference[2] = -1;
                    alternativeRoutes.Add(startTCSectionIndex, altRouteReference);
                    ActiveAlternativeRoutes.Enqueue(startTCSectionIndex);

                    currentPathNode.Type = FreeTrainSimulator.Common.TrainPathNodeType.Other;
                }
                else if (currentPathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.SidingEnd)
                {
                    TrackNode sidingNode = RuntimeData.Instance.TrackDB.TrackNodes[currentPathNode.JunctionIndex];
                    int endTCSectionIndex = sidingNode.TrackCircuitCrossReferences[0].Index;

                    int refStartIndex = ActiveAlternativeRoutes.Dequeue();
                    int[] altRouteReference = alternativeRoutes[refStartIndex];
                    altRouteReference[2] = endTCSectionIndex;

                    currentPathNode.Type = FreeTrainSimulator.Common.TrainPathNodeType.Other;
                }

                //
                // process last non-junction section
                //

                if (currentPathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.Other)
                {
                    thisNode = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];

                    //  SPA:    Subpath:    Add TCRouteElement for each TrackCircuitsection in node
                    if (currentDir == 0)
                    {
                        for (int iTC = 0; iTC < thisNode.TrackCircuitCrossReferences.Count; iTC++)
                        {
                            TrackCircuitRouteElement element = new TrackCircuitRouteElement(thisNode, iTC, currentDir);
                            thisSubpath.Add(element);
                            SetStationReference(TCRouteSubpaths, element.TrackCircuitSection);
                        }
                        newDir = thisNode.TrackPins[(int)currentDir].Direction;

                    }
                    else
                    {
                        for (int iTC = thisNode.TrackCircuitCrossReferences.Count - 1; iTC >= 0; iTC--)
                        {
                            TrackCircuitRouteElement element = new TrackCircuitRouteElement(thisNode, iTC, currentDir);
                            thisSubpath.Add(element);
                            SetStationReference(TCRouteSubpaths, element.TrackCircuitSection);
                        }
                        newDir = thisNode.TrackPins[(int)currentDir].Direction;
                    }

                    if (reversal > 0)
                    {
                        while (reversal > 0)
                        {
                            //<CSComment> following block can be uncommented if it is preferred to leave in the path the double reverse points
                            //                                if (!Simulator.TimetableMode && Simulator.Settings.EnhancedActCompatibility && sublist > 0 &&
                            //                                    TCRouteSubpaths[sublist].Count <= 0)
                            //                                {
                            //                                    // check if preceding subpath has no sections, and in such case insert the one it should have,
                            //                                    // taking the last section from the preceding subpath
                            //                                    thisNode = aiPath.TrackDB.TrackNodes[trackNodeIndex];
                            //                                    if (currentDir == 0)
                            //                                    {
                            //                                        for (int iTC = 0; iTC < thisNode.TCCrossReference.Count; iTC++)
                            //                                        {
                            //                                            if (thisNode.TCCrossReference[iTC].Index == RoughReversalInfos[sublist].ReversalSectionIndex)
                            //                                            {
                            //                                                TCRouteElement thisElement =
                            //                                                     new TCRouteElement(thisNode, iTC, currentDir, orgSignals);
                            //                                                thisSubpath.Add(thisElement);
                            //                                                //  SPA:    Station:    A adapter, 
                            //                                                SetStationReference(TCRouteSubpaths, thisElement.TCSectionIndex, orgSignals);
                            //                                                break;
                            //                                            }
                            //                                        }
                            //                                        newDir = thisNode.TrPins[currentDir].Direction;
                            //
                            //                                    }
                            //                                    else
                            //                                    {
                            //                                        for (int iTC = thisNode.TCCrossReference.Count - 1; iTC >= 0; iTC--)
                            //                                        {
                            //                                            if (thisNode.TCCrossReference[iTC].Index == RoughReversalInfos[sublist].ReversalSectionIndex)
                            //                                            {
                            //                                                TCRouteElement thisElement =
                            //                                                   new TCRouteElement(thisNode, iTC, currentDir, orgSignals);
                            //                                                thisSubpath.Add(thisElement);
                            //                                                SetStationReference(TCRouteSubpaths, thisElement.TCSectionIndex, orgSignals);
                            //                                                break;
                            //                                            }
                            //                                        }
                            //                                        newDir = thisNode.TrPins[currentDir].Direction;
                            //                                    }
                            //                                }

                            sublist++;
                            thisSubpath = new TrackCircuitPartialPathRoute();
                            TCRouteSubpaths.Add(thisSubpath);
                            currentDir = currentDir.Reverse();
                            reversal--;        // reset reverse point
                        }
                        continue;          // process this node again in reverse direction
                    }
                    //  SPA:    WP: New forms 

                    //
                    // process junction section
                    //

                    if (currentPathNode.JunctionIndex > 0)
                    {
                        TrackNode junctionNode = RuntimeData.Instance.TrackDB.TrackNodes[currentPathNode.JunctionIndex];
                        TrackCircuitRouteElement thisElement =
                            new TrackCircuitRouteElement(junctionNode, 0, newDir);
                        thisSubpath.Add(thisElement);

                        trackNodeIndex = currentPathNode.NextMainTVNIndex;

                        if (currentPathNode.IsFacingPoint)   // exit is one of two switch paths //
                        {
                            int firstpin = (junctionNode.InPins > 1) ? 0 : junctionNode.InPins;
                            if (junctionNode.TrackPins[firstpin].Link == trackNodeIndex)
                            {
                                newDir = junctionNode.TrackPins[firstpin].Direction;
                                thisElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
                            }
                            else
                            {
                                firstpin++;
                                newDir = junctionNode.TrackPins[firstpin].Direction;
                                thisElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Reverse;
                            }
                        }
                        else  // exit is single path //
                        {
                            int firstpin = (junctionNode.InPins > 1) ? junctionNode.InPins : 0;
                            newDir = junctionNode.TrackPins[firstpin].Direction;
                        }
                    }
                    //
                    // find next junction path node
                    //
                    nextPathNode = currentPathNode.NextMainNode;

                    // if we were on last main node, direction was already set
                    if (nextPathNode != null)
                        currentDir = newDir;

                }
                else
                {
                    nextPathNode = currentPathNode;
                }

                while (nextPathNode != null && nextPathNode.JunctionIndex < 0)
                {
                    lastPathNode = nextPathNode;

                    if (nextPathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.Reverse)
                    {
                        TrackVectorNode reversalNode = RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[nextPathNode.NextMainTVNIndex];
                        TrackVectorSection firstSection = reversalNode.TrackVectorSections[0];
                        Traveller TDBTrav = new Traveller(reversalNode, firstSection.Location, Direction.Forward);
                        offset = TDBTrav.DistanceTo(reversalNode, nextPathNode.Location);
                        float reverseOffset = 0;
                        int sectionIndex = -1;
                        TrackDirection validDir = currentDir;
                        if (reversal % 2 == 1)
                            validDir = validDir.Reverse();
                        if (validDir == TrackDirection.Ahead)
                        {
                            reverseOffset = -offset;
                            for (int i = reversalNode.TrackCircuitCrossReferences.Count - 1; i >= 0 && reverseOffset <= 0; i--)
                            {
                                reverseOffset += reversalNode.TrackCircuitCrossReferences[i].Length;
                                sectionIndex = reversalNode.TrackCircuitCrossReferences[i].Index;

                            }
                        }
                        else
                        {
                            int exti = 0;
                            reverseOffset = offset;
                            for (int i = reversalNode.TrackCircuitCrossReferences.Count - 1; i >= 0 && reverseOffset >= 0; i--)
                            {
                                reverseOffset -= reversalNode.TrackCircuitCrossReferences[i].Length;
                                sectionIndex = reversalNode.TrackCircuitCrossReferences[i].Index;
                                exti = i;
                            }
                            reverseOffset += reversalNode.TrackCircuitCrossReferences[exti].Length;
                        }
                        RoughReversalInfo roughReversalInfo = new RoughReversalInfo(sublist + reversal, reverseOffset, sectionIndex);
                        RoughReversalInfos.Add(roughReversalInfo);
                        reversalOffset.Add(offset);
                        reversalIndex.Add(sublist);
                        reversal++;
                    }
                    else if (nextPathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.Stop)
                    {
                        TrackDirection validDir = currentDir;
                        if (reversal % 2 == 1)
                            validDir = validDir.Reverse();
                        offset = GetOffsetToPathNode(aiPath, validDir, nextPathNode);
                        WaitingPoints.Add(new WaitingPointDetail()
                        {
                            SubListIndex = sublist + reversal,
                            WaitingPointSection = ConvertWaitingPoint(nextPathNode),
                            WaitTime = nextPathNode.WaitTimeS,
                            DepartTime = nextPathNode.WaitUntil,
                            HoldSignal = -1, // hold signal set later
                            Offset = (int)offset,
                        });
                    }

                    // other type of path need not be processed

                    // go to next node
                    nextPathNode = nextPathNode.NextMainNode;
                }
                currentPathNode = nextPathNode;
            }

            if (!Simulator.Instance.TimetableMode)
            {
                // insert reversals when they are in last section
                while (reversal > 0)
                {
                    thisNode = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];
                    if (currentDir == 0)
                    {
                        for (int iTC = 0; iTC < thisNode.TrackCircuitCrossReferences.Count; iTC++)
                        {
                            TrackCircuitRouteElement element = new TrackCircuitRouteElement(thisNode, iTC, currentDir);
                            thisSubpath.Add(element);
                            //  SPA:    Station:    A adapter, 
                            SetStationReference(TCRouteSubpaths, element.TrackCircuitSection);
                            if (thisNode.TrackCircuitCrossReferences[iTC].Index == RoughReversalInfos[sublist].ReversalSectionIndex)
                            {
                                break;
                            }
                        }
                        newDir = thisNode.TrackPins[(int)currentDir].Direction;

                    }
                    else
                    {
                        for (int iTC = thisNode.TrackCircuitCrossReferences.Count - 1; iTC >= 0; iTC--)
                        {
                            TrackCircuitRouteElement element = new TrackCircuitRouteElement(thisNode, iTC, currentDir);
                            thisSubpath.Add(element);
                            SetStationReference(TCRouteSubpaths, element.TrackCircuitSection);
                            if (thisNode.TrackCircuitCrossReferences[iTC].Index == RoughReversalInfos[sublist].ReversalSectionIndex)
                            {
                                break;
                            }
                        }
                        newDir = thisNode.TrackPins[(int)currentDir].Direction;
                    }
                    sublist++;
                    thisSubpath = new TrackCircuitPartialPathRoute();
                    TCRouteSubpaths.Add(thisSubpath);
                    currentDir = currentDir.Reverse();
                    reversal--;        // reset reverse point
                }
            }
            //
            // add last section
            //

            thisNode = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];
            TrackVectorSection endFirstSection = (thisNode as TrackVectorNode).TrackVectorSections[0];
            Traveller TDBEndTrav = new Traveller(thisNode as TrackVectorNode, endFirstSection.Location, Direction.Forward);
            float endOffset = TDBEndTrav.DistanceTo(thisNode, lastPathNode.Location);

            // Prepare info about route end point
            float reverseEndOffset = 0;
            int endNodeSectionIndex = -1;
            if (currentDir == 0)
            {
                reverseEndOffset = -endOffset;
                for (int i = thisNode.TrackCircuitCrossReferences.Count - 1; i >= 0 && reverseEndOffset <= 0; i--)
                {
                    reverseEndOffset += thisNode.TrackCircuitCrossReferences[i].Length;
                    endNodeSectionIndex = thisNode.TrackCircuitCrossReferences[i].Index;

                }
            }
            else
            {
                int exti = 0;
                reverseEndOffset = endOffset;
                for (int i = thisNode.TrackCircuitCrossReferences.Count - 1; i >= 0 && reverseEndOffset >= 0; i--)
                {
                    reverseEndOffset -= thisNode.TrackCircuitCrossReferences[i].Length;
                    endNodeSectionIndex = thisNode.TrackCircuitCrossReferences[i].Index;
                    exti = i;
                }
                reverseEndOffset += thisNode.TrackCircuitCrossReferences[exti].Length;
            }
            RoughReversalInfo lastReversalInfo = new RoughReversalInfo(sublist, reverseEndOffset, endNodeSectionIndex);
            RoughReversalInfos.Add(lastReversalInfo);

            // only add last section if end point is in different tracknode as last added item
            if (thisSubpath.Count <= 0 ||
                thisNode.Index != thisSubpath[thisSubpath.Count - 1].TrackCircuitSection.OriginalIndex)
            {
                if (currentDir == 0)
                {
                    for (int iTC = 0; iTC < thisNode.TrackCircuitCrossReferences.Count; iTC++)
                    {
                        if ((thisNode.TrackCircuitCrossReferences[iTC].OffsetLength[TrackDirection.Reverse] + thisNode.TrackCircuitCrossReferences[iTC].Length) > endOffset)
                        //                      if (thisNode.TCCrossReference[iTC].Position[0] < endOffset)
                        {
                            TrackCircuitRouteElement element = new TrackCircuitRouteElement(thisNode, iTC, currentDir);
                            if (thisSubpath.Count <= 0 || thisSubpath[thisSubpath.Count - 1].TrackCircuitSection.Index != element.TrackCircuitSection.Index)
                            {
                                thisSubpath.Add(element); // only add if not yet set
                                SetStationReference(TCRouteSubpaths, element.TrackCircuitSection);
                            }
                        }
                    }
                }
                else
                {
                    for (int iTC = thisNode.TrackCircuitCrossReferences.Count - 1; iTC >= 0; iTC--)
                    {
                        if (thisNode.TrackCircuitCrossReferences[iTC].OffsetLength[TrackDirection.Reverse] < endOffset)
                        {
                            TrackCircuitRouteElement element = new TrackCircuitRouteElement(thisNode, iTC, currentDir);
                            if (thisSubpath.Count <= 0 || thisSubpath[thisSubpath.Count - 1].TrackCircuitSection.Index != element.TrackCircuitSection.Index)
                            {
                                thisSubpath.Add(element); // only add if not yet set
                                SetStationReference(TCRouteSubpaths, element.TrackCircuitSection);
                            }
                        }
                    }
                }
            }

            // check if section extends to end of track

            TrackCircuitRouteElement lastElement = thisSubpath[thisSubpath.Count - 1];
            TrackCircuitSection lastEndSection = lastElement.TrackCircuitSection;
            TrackDirection lastDirection = lastElement.Direction;

            List<TrackCircuitRouteElement> addedElements = new List<TrackCircuitRouteElement>();
            if (lastEndSection.CircuitType != TrackCircuitType.EndOfTrack && lastEndSection.EndSignals[lastDirection] == null)
            {
                TrackDirection thisDirection = lastDirection;
                lastDirection = lastEndSection.Pins[thisDirection, SignalLocation.NearEnd].Direction;
                lastEndSection = TrackCircuitSection.TrackCircuitList[lastEndSection.Pins[thisDirection, SignalLocation.NearEnd].Link];

                while (lastEndSection.CircuitType == TrackCircuitType.Normal && lastEndSection.EndSignals[lastDirection] == null)
                {
                    addedElements.Add(new TrackCircuitRouteElement(lastEndSection.Index, lastDirection));
                    thisDirection = lastDirection;
                    lastDirection = lastEndSection.Pins[thisDirection, SignalLocation.NearEnd].Direction;
                    lastEndSection = TrackCircuitSection.TrackCircuitList[lastEndSection.Pins[thisDirection, SignalLocation.NearEnd].Link];
                }

                if (lastEndSection.CircuitType == TrackCircuitType.EndOfTrack)
                {
                    foreach (TrackCircuitRouteElement addedElement in addedElements)
                    {
                        thisSubpath.Add(addedElement);
                        SetStationReference(TCRouteSubpaths, addedElement.TrackCircuitSection);
                    }
                    thisSubpath.Add(new TrackCircuitRouteElement(lastEndSection.Index, lastDirection));
                }
            }

            // remove sections beyond reversal points

            for (int iSub = 0; iSub < reversalOffset.Count; iSub++)  // no reversal for final path
            {
                TrackCircuitPartialPathRoute revSubPath = TCRouteSubpaths[reversalIndex[iSub]];
                offset = reversalOffset[iSub];
                if (revSubPath.Count <= 0)
                    continue;

                TrackDirection reversalDirection = revSubPath[revSubPath.Count - 1].Direction;

                bool withinOffset = true;
                List<int> removeSections = new List<int>();
                int lastSectionIndex = revSubPath.Count - 1;

                // create list of sections beyond reversal point 

                if (reversalDirection == TrackDirection.Ahead)
                {
                    for (int iSection = revSubPath.Count - 1; iSection > 0 && withinOffset; iSection--)
                    {
                        TrackCircuitSection thisSection = revSubPath[iSection].TrackCircuitSection;
                        if (thisSection.CircuitType == TrackCircuitType.Junction)
                        {
                            withinOffset = false;    // always end on junction (next node)
                        }
                        else if (thisSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            removeSections.Add(iSection);        // always remove crossover if last section was removed
                            lastSectionIndex = iSection - 1;
                        }
                        else if (thisSection.OffsetLength[SignalLocation.FarEnd] + thisSection.Length < offset) // always use offsetLength[1] as offset is wrt begin of original section
                        {
                            removeSections.Add(iSection);
                            lastSectionIndex = iSection - 1;
                        }
                        else
                        {
                            withinOffset = false;
                        }
                    }
                }
                else
                {
                    for (int iSection = revSubPath.Count - 1; iSection > 0 && withinOffset; iSection--)
                    {
                        TrackCircuitSection thisSection = revSubPath[iSection].TrackCircuitSection;
                        if (thisSection.CircuitType == TrackCircuitType.Junction)
                        {
                            withinOffset = false;     // always end on junction (next node)
                        }
                        else if (thisSection.CircuitType == TrackCircuitType.Crossover)
                        {
                            removeSections.Add(iSection);        // always remove crossover if last section was removed
                            lastSectionIndex = iSection - 1;
                        }
                        else if (thisSection.OffsetLength[SignalLocation.FarEnd] > offset)
                        {
                            removeSections.Add(iSection);
                            lastSectionIndex = iSection - 1;
                        }
                        else
                        {
                            withinOffset = false;
                        }
                    }
                }

                // extend route to first signal or first node

                bool signalFound = false;

                for (int iSection = lastSectionIndex; iSection < revSubPath.Count - 1 && !signalFound; iSection++)
                {
                    TrackCircuitSection thisSection = revSubPath[iSection].TrackCircuitSection;
                    removeSections.Remove(iSection);
                    if (thisSection.EndSignals[reversalDirection] != null)
                    {
                        signalFound = true;
                    }
                }

                // remove sections beyond first signal or first node from reversal point

                for (int iSection = 0; iSection < removeSections.Count; iSection++)
                {
                    revSubPath.RemoveAt(removeSections[iSection]);
                }
            }

            // remove dummy subpaths (from double reversion)

            List<int> subRemoved = new List<int>();
            int orgCount = TCRouteSubpaths.Count;
            int removed = 0;
            Dictionary<int, int> newIndices = new Dictionary<int, int>();

            for (int iSub = TCRouteSubpaths.Count - 1; iSub >= 0; iSub--)
            {
                if (TCRouteSubpaths[iSub].Count <= 0)
                {
                    TCRouteSubpaths.RemoveAt(iSub);
                    subRemoved.Add(iSub);
                    int itemToRemove = RoughReversalInfos.FindIndex(r => r.SubPathIndex >= iSub);
                    if (itemToRemove != -1)
                    {
                        if (RoughReversalInfos[itemToRemove].SubPathIndex == iSub)
                            RoughReversalInfos.RemoveAt(itemToRemove);
                        for (int i = itemToRemove; i < RoughReversalInfos.Count; i++)
                        {
                            RoughReversalInfos[i].SubPathIndex--;
                        }
                    }
                }
            }

            // calculate new indices
            for (int iSub = 0; iSub <= orgCount - 1; iSub++) //<CSComment> maybe comparison only with less than?
            {
                newIndices.Add(iSub, iSub - removed);
                if (subRemoved.Contains(iSub))
                {
                    removed++;
                }
            }

            // if removed, update indices of waiting points
            if (removed > 0)
            {
                foreach (WaitingPointDetail waitPoint in WaitingPoints)
                {
                    waitPoint.SubListIndex = newIndices[waitPoint.SubListIndex];
                }

                // if remove, update indices of alternative paths
                Dictionary<int, int[]> copyAltRoutes = alternativeRoutes;
                alternativeRoutes.Clear();
                foreach (KeyValuePair<int, int[]> thisAltPath in copyAltRoutes)
                {
                    int[] pathDetails = thisAltPath.Value;
                    pathDetails[0] = newIndices[pathDetails[0]];
                    alternativeRoutes.Add(thisAltPath.Key, pathDetails);
                }

                // if remove, update indices in station xref

                Dictionary<string, int[]> copyXRef = StationCrossReferences;
                StationCrossReferences.Clear();

                foreach (KeyValuePair<string, int[]> actXRef in copyXRef)
                {
                    int[] oldValue = actXRef.Value;
                    int[] newValue = new int[3] { newIndices[oldValue[0]], oldValue[1], oldValue[2] };
                    StationCrossReferences.Add(actXRef.Key, newValue);
                }
            }

            // find if last stretch is dummy track

            // first, find last signal - there may not be a junction between last signal and end
            // last end must be end-of-track

            foreach (TrackCircuitPartialPathRoute endSubPath in TCRouteSubpaths)
            {
                int lastIndex = endSubPath.Count - 1;
                TrackCircuitRouteElement thisElement = endSubPath[lastIndex];
                TrackCircuitSection lastSection = thisElement.TrackCircuitSection;

                // build additional route from end of last section but not further than train length

                int nextSectionIndex = lastSection.ActivePins[thisElement.OutPin[SignalLocation.NearEnd], (SignalLocation)thisElement.OutPin[SignalLocation.FarEnd]].Link;
                TrackDirection nextDirection = lastSection.ActivePins[thisElement.OutPin[SignalLocation.NearEnd], (SignalLocation)thisElement.OutPin[SignalLocation.FarEnd]].Direction;
                int lastUseIndex = lastIndex - 1;  // do not use final element if this is end of track

                List<int> addSections = new List<int>();

                if (nextSectionIndex > 0)
                {
                    lastUseIndex = lastIndex;  // last element is not end of track
                    addSections = SignalEnvironment.ScanRoute(null, nextSectionIndex, 0.0f, nextDirection,
                       true, thisTrainLength, false, true, true, false, true, false, false, false, false, false);

                    if (addSections.Count > 0)
                    {
                        lastSection = TrackCircuitSection.TrackCircuitList[Math.Abs(addSections[addSections.Count - 1])];
                    }
                }

                if (lastSection.CircuitType == TrackCircuitType.EndOfTrack)
                {

                    // first length of added sections

                    float totalLength = 0.0f;
                    bool juncfound = false;

                    for (int iSection = 0; iSection < addSections.Count - 2; iSection++)  // exclude end of track
                    {
                        TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[Math.Abs(addSections[iSection])];
                        totalLength += thisSection.Length;
                        if (thisSection.CircuitType != TrackCircuitType.Normal)
                        {
                            juncfound = true;
                        }
                    }

                    // next length of sections back to last signal
                    // stop loop : when junction found, when signal found, when length exceeds train length

                    int sigIndex = -1;

                    for (int iSection = lastUseIndex;
                            iSection >= 0 && sigIndex < 0 && !juncfound && totalLength < 0.5 * thisTrainLength;
                            iSection--)
                    {
                        thisElement = endSubPath[iSection];
                        TrackCircuitSection thisSection = thisElement.TrackCircuitSection;

                        if (thisSection.EndSignals[thisElement.Direction] != null)
                        {
                            sigIndex = iSection;
                        }
                        else if (thisSection.CircuitType != TrackCircuitType.Normal)
                        {
                            juncfound = true;
                        }
                        else
                        {
                            totalLength += thisSection.Length;
                        }
                    }

                    // remove dummy ends

                    if (sigIndex > 0 && totalLength < 0.5f * thisTrainLength)
                    {
                        for (int iSection = endSubPath.Count - 1; iSection > sigIndex; iSection--)
                        {
                            if (endSubPath == TCRouteSubpaths[TCRouteSubpaths.Count - 1] &&
                                endSubPath[iSection].TrackCircuitSection.Index == RoughReversalInfos[RoughReversalInfos.Count - 1].ReversalSectionIndex)
                            {
                                RoughReversalInfos[RoughReversalInfos.Count - 1].ReversalSectionIndex = endSubPath[sigIndex].TrackCircuitSection.Index;
                                RoughReversalInfos[RoughReversalInfos.Count - 1].ReverseReversalOffset =
                                    endSubPath[sigIndex].TrackCircuitSection.Length;
                            }
                            endSubPath.RemoveAt(iSection);
                        }
                    }
                }
            }

            // for reversals, find actual diverging section

            int prevDivergeSectorIndex = -1;
            int iReversalLists = 0;
            TrackCircuitReversalInfo reversalInfo;
            for (int iSubpath = 1; iSubpath < TCRouteSubpaths.Count; iSubpath++)
            {
                while (RoughReversalInfos.Count > 0 && RoughReversalInfos[iReversalLists].SubPathIndex < iSubpath - 1 && iReversalLists < RoughReversalInfos.Count - 2)
                {
                    iReversalLists++;
                }

                if (RoughReversalInfos.Count > 0 && RoughReversalInfos[iReversalLists].SubPathIndex == iSubpath - 1)
                {
                    reversalInfo = new TrackCircuitReversalInfo(TCRouteSubpaths[iSubpath - 1], prevDivergeSectorIndex,
                        TCRouteSubpaths[iSubpath], RoughReversalInfos[iReversalLists].ReverseReversalOffset, RoughReversalInfos[iReversalLists].SubPathIndex, RoughReversalInfos[iReversalLists].ReversalSectionIndex);
                }
                else
                {
                    reversalInfo = new TrackCircuitReversalInfo(TCRouteSubpaths[iSubpath - 1], prevDivergeSectorIndex,
                        TCRouteSubpaths[iSubpath], -1, -1, -1);
                }

                ReversalInfo.Add(reversalInfo);
                prevDivergeSectorIndex = reversalInfo.Valid ? reversalInfo.FirstDivergeIndex : -1;
            }
            ReversalInfo.Add(new TrackCircuitReversalInfo());  // add invalid item to make up the numbers (equals no. subpaths)
                                                               // Insert data for end route offset
            ReversalInfo[ReversalInfo.Count - 1].ReverseReversalOffset = RoughReversalInfos[RoughReversalInfos.Count - 1].ReverseReversalOffset;
            ReversalInfo[ReversalInfo.Count - 1].ReversalIndex = RoughReversalInfos[RoughReversalInfos.Count - 1].SubPathIndex;
            ReversalInfo[ReversalInfo.Count - 1].ReversalSectionIndex = RoughReversalInfos[RoughReversalInfos.Count - 1].ReversalSectionIndex;

            RoughReversalInfos.Clear(); // no more used


            // process alternative paths - MSTS style

            if (Simulator.Instance.Settings.UseLocationPassingPaths)
            {
                ProcessAlternativePathLocationDef(alternativeRoutes, aiPath, trainNumber);
                if (trainNumber >= 0)
                    SearchPassingPaths(trainNumber, thisTrainLength);
            }
            else
            {
                ProcessAlternativePathPathDef(alternativeRoutes, aiPath);
            }

            // search for loops

            LoopSearch();
        }

        //
        // Constructor from single subpath
        //
        public TrackCircuitRoutePath(TrackCircuitPartialPathRoute subPath)
        {
            ActiveSubPath = 0;
            ActiveAlternativePath = -1;

            TCRouteSubpaths.Add(subPath);
        }

        //
        // Constructor from existing path
        //
        public TrackCircuitRoutePath(TrackCircuitRoutePath source)
        {
            ArgumentNullException.ThrowIfNull(source);

            ActiveSubPath = source.ActiveSubPath;
            ActiveAlternativePath = source.ActiveAlternativePath;

            TCRouteSubpaths.AddRange(source.TCRouteSubpaths);
            TCAlternativePaths.AddRange(source.TCAlternativePaths);

            for (int iWaitingPoint = 0; iWaitingPoint < source.WaitingPoints.Count; iWaitingPoint++)
            {
                WaitingPointDetail newWaitingPoint = new WaitingPointDetail(source.WaitingPoints[iWaitingPoint]);
                WaitingPoints.Add(newWaitingPoint);
            }

            for (int iReversalPoint = 0; iReversalPoint < source.ReversalInfo.Count; iReversalPoint++)
            {
                if (source.ReversalInfo[iReversalPoint] == null)
                {
                    ReversalInfo.Add(null);
                }
                else
                {
                    TrackCircuitReversalInfo reversalInfo = new TrackCircuitReversalInfo(source.ReversalInfo[iReversalPoint]);
                    ReversalInfo.Add(reversalInfo);
                }
            }

            LoopEnd.AddRange(source.LoopEnd);

            foreach (KeyValuePair<string, int[]> actStation in source.StationCrossReferences)
            {
                StationCrossReferences.Add(actStation.Key, actStation.Value);
            }
        }

        public async ValueTask<TrackCircuitRoutePathSaveState> Snapshot()
        {
            return new TrackCircuitRoutePathSaveState()
            {
                ActivePath = ActiveSubPath,
                ActiveAlternativePath = ActiveAlternativePath,
                RoutePaths = await TCRouteSubpaths.SnapshotCollection<TrackCircuitPartialPathRouteSaveState, TrackCircuitPartialPathRoute>().ConfigureAwait(false),
                AlternativePaths = await TCAlternativePaths.SnapshotCollection<TrackCircuitPartialPathRouteSaveState, TrackCircuitPartialPathRoute>().ConfigureAwait(false),
                Waitpoints = new Collection<int[]>(WaitingPoints.Select(item => item.Values).ToList()),
                LoopEnd = new Collection<int>(LoopEnd),
                OriginalSubPath = OriginalSubpath,
                ReversalInfoSaveStates = await ReversalInfo.SnapshotCollection<TrackCircuitReversalInfoSaveState, TrackCircuitReversalInfo>().ConfigureAwait(false),
            };
        }

        public async ValueTask Restore(TrackCircuitRoutePathSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ActiveSubPath = saveState.ActivePath;
            ActiveAlternativePath = saveState.ActiveAlternativePath;

            await TCRouteSubpaths.RestoreCollectionCreateNewItems(saveState.RoutePaths).ConfigureAwait(false);
            await TCAlternativePaths.RestoreCollectionCreateNewItems(saveState.AlternativePaths).ConfigureAwait(false);
            WaitingPoints.AddRange(saveState.Waitpoints.Select(waitPoint => new WaitingPointDetail(waitPoint))); 
            await ReversalInfo.RestoreCollectionCreateNewItems(saveState.ReversalInfoSaveStates).ConfigureAwait(false);
            LoopEnd.AddRange(saveState.LoopEnd);
            OriginalSubpath = saveState.OriginalSubPath;

            // note : stationXRef only used on init, not saved
        }

        //  SPA: Used with enhanced MSTS Mode, please don't change
        private static float GetOffsetToPathNode(AIPath aiPath, TrackDirection direction, AIPathNode pathNode)
        {
            //TrackVectorNode waitPointNode;
            //TrackVectorSection firstSection;
            ////int nextNodeIdx = 0;
            //TrackDirection nodeDirection = direction;

            //waitPointNode = aiPath.TrackDB.TrackNodes[pathNode.NextMainTVNIndex] as TrackVectorNode;
            //int idxSectionWP = ConvertWaitingPoint(pathNode, aiPath.TrackDB, aiPath.TSectionDat);
            //firstSection = waitPointNode.TrackVectorSections[0];
            //Traveller tdbTraveller = new Traveller(aiPath.TSectionDat, aiPath.TrackDB.TrackNodes, waitPointNode, firstSection.Location, (Traveller.TravellerDirection)nodeDirection);

            //float offset;
            //if (tdbTraveller.Direction == Direction.Backward)
            //{
            //    nodeDirection = 1 - direction;
            //    tdbTraveller = new Traveller(aiPath.TSectionDat, aiPath.TrackDB.TrackNodes, waitPointNode, firstSection.Location, (Traveller.TravellerDirection)nodeDirection);
            //    offset = tdbTraveller.DistanceTo(waitPointNode, pathNode.Location);
            //    for (int i = 0; i < waitPointNode.TrackCircuitCrossReferences.Count; i++)
            //    {
            //        if (waitPointNode.TrackCircuitCrossReferences[i].Index == idxSectionWP)
            //        {
            //            float sectionOffset = offset - waitPointNode.TrackCircuitCrossReferences[i].OffsetLength[(int)nodeDirection];
            //            offset = waitPointNode.TrackCircuitCrossReferences[i].Length - sectionOffset;
            //            break;
            //        }
            //    }
            //}
            //else
            //{
            //    //Trace.TraceInformation("no reverse");
            //    offset = tdbTraveller.DistanceTo(waitPointNode, pathNode.Location);
            //    for (int i = 0; i < waitPointNode.TrackCircuitCrossReferences.Count; i++)
            //    {
            //        if (waitPointNode.TrackCircuitCrossReferences[i].Index == idxSectionWP)
            //        {
            //            offset -= waitPointNode.TrackCircuitCrossReferences[i].OffsetLength[(int)nodeDirection];
            //            break;
            //        }
            //    }
            //}
            //return offset;
            return 0;
        }

        // process alternative paths - MSTS style Path definition
        private void ProcessAlternativePathPathDef(Dictionary<int, int[]> alternativeRoutes, AIPath aiPath)
        {
            int altlist = 0;

            foreach (KeyValuePair<int, int[]> alternativePath in alternativeRoutes)
            {
                TrackCircuitPartialPathRoute alternativePathRoute = new TrackCircuitPartialPathRoute();

                int startSection = alternativePath.Key;
                int[] pathDetails = alternativePath.Value;
                int sublistRef = pathDetails[0];

                int startSectionRouteIndex = TCRouteSubpaths[sublistRef].GetRouteIndex(startSection, 0);
                int endSectionRouteIndex = -1;

                int endSection = pathDetails[2];
                if (endSection < 0)
                {
                    Trace.TraceInformation($"No end-index found for alternative path starting at {startSection}");
                }
                else
                {
                    endSectionRouteIndex = TCRouteSubpaths[sublistRef].GetRouteIndex(endSection, 0);
                }

                if (startSectionRouteIndex < 0 || endSectionRouteIndex < 0)
                {
                    Trace.TraceInformation($"Start section {startSection} or end section {endSection} for alternative path not in subroute {sublistRef}");
                }
                else
                {
                    TrackCircuitRouteElement startElement = TCRouteSubpaths[sublistRef][startSectionRouteIndex];
                    TrackCircuitRouteElement endElement = TCRouteSubpaths[sublistRef][endSectionRouteIndex];

                    startElement.StartAlternativePath = new TrackCircuitRouteElement.AlternativePath(altlist, TrackCircuitSection.TrackCircuitList[endSection]);
                    endElement.EndAlternativePath = new TrackCircuitRouteElement.AlternativePath(altlist, TrackCircuitSection.TrackCircuitList[startSection]);

                    TrackDirection currentDir = startElement.Direction;
                    TrackDirection newDir = currentDir;

                    //
                    // loop through path nodes
                    //

                    AIPathNode currentPathNode = aiPath.Nodes[pathDetails[1]];
                    AIPathNode nextPathNode = null;
                    AIPathNode lastPathNode = null;

                    // process junction node

                    TrackJunctionNode firstJunctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[currentPathNode.JunctionIndex];
                    TrackCircuitRouteElement junctionElement = new TrackCircuitRouteElement(firstJunctionNode, 0, currentDir);
                    alternativePathRoute.Add(junctionElement);

                    int trackNodeIndex = currentPathNode.NextSidingTVNIndex;

                    int firstJunctionPin = (firstJunctionNode.InPins > 1) ? 0 : firstJunctionNode.InPins;
                    if (firstJunctionNode.TrackPins[firstJunctionPin].Link == trackNodeIndex)
                    {
                        currentDir = firstJunctionNode.TrackPins[firstJunctionPin].Direction;
                        junctionElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
                    }
                    else
                    {
                        firstJunctionPin++;
                        currentDir = firstJunctionNode.TrackPins[firstJunctionPin].Direction;
                        junctionElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Reverse;
                    }

                    // process alternative path
                    TrackNode node = null;
                    currentPathNode = currentPathNode.NextSidingNode;

                    while (currentPathNode != null)
                    {
                        // process last non-junction section
                        if (currentPathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.Other)
                        {
                            if (trackNodeIndex > 0)
                            {
                                node = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];

                                if (currentDir == TrackDirection.Ahead)
                                {
                                    for (int i = 0; i < node.TrackCircuitCrossReferences.Count; i++)
                                    {
                                        TrackCircuitRouteElement element = new TrackCircuitRouteElement(node, i, currentDir);
                                        alternativePathRoute.Add(element);
                                    }
                                    newDir = node.TrackPins[(int)currentDir].Direction;

                                }
                                else
                                {
                                    for (int i = node.TrackCircuitCrossReferences.Count - 1; i >= 0; i--)
                                    {
                                        TrackCircuitRouteElement element = new TrackCircuitRouteElement(node, i, currentDir);
                                        alternativePathRoute.Add(element);
                                    }
                                    newDir = node.TrackPins[(int)currentDir].Direction;
                                }
                                trackNodeIndex = -1;
                            }

                            //
                            // process junction section
                            //

                            if (currentPathNode.JunctionIndex > 0)
                            {
                                TrackJunctionNode junctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[currentPathNode.JunctionIndex];
                                TrackCircuitRouteElement element = new TrackCircuitRouteElement(junctionNode, 0, newDir);
                                alternativePathRoute.Add(element);

                                trackNodeIndex = currentPathNode.NextSidingTVNIndex;

                                if (currentPathNode.IsFacingPoint)   // exit is one of two switch paths //
                                {
                                    int firstpin = (junctionNode.InPins > 1) ? 0 : junctionNode.InPins;
                                    if (junctionNode.TrackPins[firstpin].Link == trackNodeIndex)
                                    {
                                        newDir = junctionNode.TrackPins[firstpin].Direction;
                                        element.OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
                                    }
                                    else
                                    {
                                        firstpin++;
                                        newDir = junctionNode.TrackPins[firstpin].Direction;
                                        element.OutPin[SignalLocation.FarEnd] = TrackDirection.Reverse;
                                    }
                                }
                                else  // exit is single path //
                                {
                                    int firstpin = (junctionNode.InPins > 1) ? junctionNode.InPins : 0;
                                    newDir = junctionNode.TrackPins[firstpin].Direction;
                                }
                            }

                            currentDir = newDir;
                            // find next junction path node
                            nextPathNode = currentPathNode.NextSidingNode;
                        }
                        else
                        {
                            nextPathNode = currentPathNode;
                        }

                        while (nextPathNode != null && nextPathNode.JunctionIndex < 0)
                        {
                            nextPathNode = nextPathNode.NextSidingNode;
                        }

                        lastPathNode = currentPathNode;
                        currentPathNode = nextPathNode;
                    }

                    // add last section
                    if (trackNodeIndex > 0)
                    {
                        node = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];

                        if (currentDir == TrackDirection.Ahead)
                        {
                            for (int i = 0; i < node.TrackCircuitCrossReferences.Count; i++)
                            {
                                TrackCircuitRouteElement element = new TrackCircuitRouteElement(node, i, currentDir);
                                alternativePathRoute.Add(element);
                            }
                        }
                        else
                        {
                            for (int i = node.TrackCircuitCrossReferences.Count - 1; i >= 0; i--)
                            {
                                TrackCircuitRouteElement element = new TrackCircuitRouteElement(node, i, currentDir);
                                alternativePathRoute.Add(element);
                            }
                        }
                    }

                    TCAlternativePaths.Add(alternativePathRoute);
                    altlist++;
                }
            }
        }

        // process alternative paths - location definition
        private void ProcessAlternativePathLocationDef(Dictionary<int, int[]> alternativeRoutes, AIPath aiPath, int trainNumber)
        {
            foreach (KeyValuePair<int, int[]> alternativePath in alternativeRoutes)
            {
                TrackCircuitPartialPathRoute alternativePathRoute = new TrackCircuitPartialPathRoute();

                int startSectionIndex = alternativePath.Key;
                int[] pathDetails = alternativePath.Value;
                int sublistRef = pathDetails[0];

                int startSectionRouteIndex = TCRouteSubpaths[sublistRef].GetRouteIndex(startSectionIndex, 0);
                int endSectionRouteIndex = -1;

                int endSectionIndex = pathDetails[2];
                if (endSectionIndex < 0)
                {
                    Trace.TraceInformation($"No end-index found for passing path for train {trainNumber} starting at {startSectionIndex}");
                }
                else
                {
                    endSectionRouteIndex = TCRouteSubpaths[sublistRef].GetRouteIndex(endSectionIndex, 0);
                }

                if (startSectionRouteIndex < 0 || endSectionRouteIndex < 0)
                {
                    Trace.TraceInformation($"Start section {startSectionIndex} or end section {endSectionIndex} for passing path not in subroute {sublistRef}");
                }
                else
                {
                    TrackCircuitRouteElement startElement = TCRouteSubpaths[sublistRef][startSectionRouteIndex];
                    TrackDirection currentDir = startElement.Direction;
                    TrackDirection newDir = currentDir;

                    // loop through path nodes
                    AIPathNode pathNode = aiPath.Nodes[pathDetails[1]];

                    // process junction node
                    TrackJunctionNode firstJunctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[pathNode.JunctionIndex];
                    TrackCircuitRouteElement junctionElement = new TrackCircuitRouteElement(firstJunctionNode, 0, currentDir);
                    alternativePathRoute.Add(junctionElement);

                    int trackNodeIndex = pathNode.NextSidingTVNIndex;

                    int firstJunctionPin = (firstJunctionNode.InPins > 1) ? 0 : firstJunctionNode.InPins;
                    if (firstJunctionNode.TrackPins[firstJunctionPin].Link == trackNodeIndex)
                    {
                        currentDir = firstJunctionNode.TrackPins[firstJunctionPin].Direction;
                        junctionElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
                    }
                    else
                    {
                        firstJunctionPin++;
                        currentDir = firstJunctionNode.TrackPins[firstJunctionPin].Direction;
                        junctionElement.OutPin[SignalLocation.FarEnd] = TrackDirection.Reverse;
                    }

                    pathNode = pathNode.NextSidingNode;
                    // process alternative path
                    TrackNode trackNode;
                    while (pathNode != null)
                    {

                        AIPathNode nextPathNode;

                        // process last non-junction section
                        if (pathNode.Type == FreeTrainSimulator.Common.TrainPathNodeType.Other)
                        {
                            if (trackNodeIndex > 0)
                            {
                                trackNode = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];

                                if (currentDir == TrackDirection.Ahead)
                                {
                                    for (int i = 0; i < trackNode.TrackCircuitCrossReferences.Count; i++)
                                    {
                                        TrackCircuitRouteElement element = new TrackCircuitRouteElement(trackNode, i, currentDir);
                                        alternativePathRoute.Add(element);
                                    }
                                    newDir = trackNode.TrackPins[(int)currentDir].Direction;

                                }
                                else
                                {
                                    for (int i = trackNode.TrackCircuitCrossReferences.Count - 1; i >= 0; i--)
                                    {
                                        TrackCircuitRouteElement element = new TrackCircuitRouteElement(trackNode, i, currentDir);
                                        alternativePathRoute.Add(element);
                                    }
                                    newDir = trackNode.TrackPins[(int)currentDir].Direction;
                                }
                                trackNodeIndex = -1;
                            }

                            // process junction section
                            if (pathNode.JunctionIndex > 0)
                            {
                                TrackJunctionNode junctionNode = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[pathNode.JunctionIndex];
                                TrackCircuitRouteElement element = new TrackCircuitRouteElement(junctionNode, 0, newDir);
                                alternativePathRoute.Add(element);

                                trackNodeIndex = pathNode.NextSidingTVNIndex;

                                if (pathNode.IsFacingPoint)   // exit is one of two switch paths //
                                {
                                    int firstpin = (junctionNode.InPins > 1) ? 0 : junctionNode.InPins;
                                    if (junctionNode.TrackPins[firstpin].Link == trackNodeIndex)
                                    {
                                        newDir = junctionNode.TrackPins[firstpin].Direction;
                                        element.OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
                                    }
                                    else
                                    {
                                        firstpin++;
                                        newDir = junctionNode.TrackPins[firstpin].Direction;
                                        element.OutPin[SignalLocation.FarEnd] = TrackDirection.Reverse;
                                    }
                                }
                                else  // exit is single path //
                                {
                                    int firstpin = (junctionNode.InPins > 1) ? junctionNode.InPins : 0;
                                    newDir = junctionNode.TrackPins[firstpin].Direction;
                                }
                            }

                            currentDir = newDir;

                            // find next junction path node
                            nextPathNode = pathNode.NextSidingNode;
                        }
                        else
                        {
                            nextPathNode = pathNode;
                        }

                        while (nextPathNode != null && nextPathNode.JunctionIndex < 0)
                        {
                            nextPathNode = nextPathNode.NextSidingNode;
                        }

                        pathNode = nextPathNode;
                    }

                    // add last section
                    if (trackNodeIndex > 0)
                    {
                        trackNode = RuntimeData.Instance.TrackDB.TrackNodes[trackNodeIndex];

                        if (currentDir == TrackDirection.Ahead)
                        {
                            for (int i = 0; i < trackNode.TrackCircuitCrossReferences.Count; i++)
                            {
                                TrackCircuitRouteElement element = new TrackCircuitRouteElement(trackNode, i, currentDir);
                                alternativePathRoute.Add(element);
                            }
                        }
                        else
                        {
                            for (int i = trackNode.TrackCircuitCrossReferences.Count - 1; i >= 0; i--)
                            {
                                TrackCircuitRouteElement element = new TrackCircuitRouteElement(trackNode, i, currentDir);
                                alternativePathRoute.Add(element);
                            }
                        }
                    }

                    InsertPassingPath(TCRouteSubpaths[sublistRef], alternativePathRoute, startSectionIndex, endSectionIndex, trainNumber, sublistRef);
                }
            }
        }
        // check if path is valid diverge path

        // process alternative paths - location definition
        // main path may be NULL if private path is to be set for fixed deadlocks
        private static void InsertPassingPath(TrackCircuitPartialPathRoute mainPath, TrackCircuitPartialPathRoute passPath, int startSectionIndex, int endSectionIndex,
                              int trainNumber, int sublistRef)
        {
            // if main set, check if path is valid diverge path - otherwise assume it is indeed
            if (mainPath != null && !mainPath.HasActualDivergePath(passPath, 0))
            {
                Trace.TraceInformation($"Invalid passing path defined for train {trainNumber} at section {startSectionIndex} : path does not diverge from main path");
                return;
            }

            // find related deadlock definition - note that path may be extended to match other deadlock paths
            DeadlockInfo deadlock = DeadlockInfo.FindDeadlockInfo(passPath, mainPath, startSectionIndex, endSectionIndex);

            if (deadlock == null) // path is not valid in relation to other deadlocks
            {
                Trace.TraceInformation($"Invalid passing path defined for train {trainNumber} at section {startSectionIndex} : overlaps with other passing path");
                return;
            }

            // insert main path

            int usedStartSectionIndex = passPath[0].TrackCircuitSection.Index;
            int usedEndSectionIndex = passPath[passPath.Count - 1].TrackCircuitSection.Index;
            int usedStartSectionRouteIndex = mainPath.GetRouteIndex(usedStartSectionIndex, 0);
            int usedEndSectionRouteIndex = mainPath.GetRouteIndex(usedEndSectionIndex, usedStartSectionRouteIndex);

            TrackCircuitPartialPathRoute mainPathPart = new TrackCircuitPartialPathRoute(mainPath, usedStartSectionRouteIndex, usedEndSectionRouteIndex);
            if (mainPathPart != null)
            {
                (int PathIndex, bool Exists) mainIndex = deadlock.AddPath(mainPathPart, usedStartSectionIndex);  // [0] is Index, [1] > 0 is existing

                if (!mainIndex.Exists)
                {
                    // calculate usefull lenght and usefull end section for main path
                    DeadlockPathInfo deadlockPathInfo = deadlock.AvailablePathList[mainIndex.PathIndex];
                    deadlockPathInfo.EndSectionIndex = usedEndSectionIndex;
                    (deadlockPathInfo.LastUsefulSectionIndex, deadlockPathInfo.UsefulLength) = mainPathPart.GetUsefullLength(0.0f, -1, -1);

                    // only allow as public path if not in timetable mode
                    if (Simulator.Instance.TimetableMode)
                    {
                        deadlockPathInfo.AllowedTrains.Add(deadlock.GetTrainAndSubpathIndex(trainNumber, sublistRef));
                    }
                    else
                    {
                        deadlockPathInfo.AllowedTrains.Add(-1); // set as public path
                    }

                    // if name is main insert inverse path also as MAIN to ensure reverse path is available

                    if ("MAIN".Equals(deadlockPathInfo.Name, StringComparison.OrdinalIgnoreCase) && !Simulator.Instance.TimetableMode)
                    {
                        TrackCircuitPartialPathRoute inverseMainPath = mainPathPart.ReversePath();
                        (int pathIndex, _) = deadlock.AddPath(inverseMainPath, endSectionIndex, "MAIN", string.Empty);
                        DeadlockPathInfo deadlockInverseInfo = deadlock.AvailablePathList[pathIndex];

                        deadlockInverseInfo.EndSectionIndex = startSectionIndex;
                        (deadlockInverseInfo.LastUsefulSectionIndex, deadlockInverseInfo.UsefulLength) = inverseMainPath.GetUsefullLength(0.0f, -1, -1);
                        deadlockInverseInfo.AllowedTrains.Add(-1);
                    }
                }
                // if existing path, add trainnumber if set and path is not public
                else if (trainNumber >= 0)
                {
                    DeadlockPathInfo deadlockPathInfo = deadlock.AvailablePathList[mainIndex.PathIndex];
                    if (!deadlockPathInfo.AllowedTrains.Contains(-1))
                    {
                        deadlockPathInfo.AllowedTrains.Add(deadlock.GetTrainAndSubpathIndex(trainNumber, sublistRef));
                    }
                }
            }

            // add passing path
            (int PathIndex, bool Exists) = deadlock.AddPath(passPath, startSectionIndex);

            if (!Exists)
            {
                // calculate usefull lenght and usefull end section for passing path
                DeadlockPathInfo deadlockPathInfo = deadlock.AvailablePathList[PathIndex];
                deadlockPathInfo.EndSectionIndex = endSectionIndex;
                (deadlockPathInfo.LastUsefulSectionIndex, deadlockPathInfo.UsefulLength) = passPath.GetUsefullLength(0.0f, -1, -1);

                if (trainNumber > 0)
                {
                    deadlockPathInfo.AllowedTrains.Add(deadlock.GetTrainAndSubpathIndex(trainNumber, sublistRef));
                }
                else
                {
                    deadlockPathInfo.AllowedTrains.Add(-1);
                }

                // insert inverse path only if public

                if (trainNumber < 0)
                {
                    TrackCircuitPartialPathRoute inversePassPath = passPath.ReversePath();
                    (int PathIndex, bool Exists) inverseIndex = deadlock.AddPath(inversePassPath, endSectionIndex, deadlockPathInfo.Name, string.Empty);
                    DeadlockPathInfo deadlockInverseInfo = deadlock.AvailablePathList[inverseIndex.PathIndex];
                    deadlockInverseInfo.EndSectionIndex = startSectionIndex;
                    (deadlockInverseInfo.LastUsefulSectionIndex, deadlockInverseInfo.UsefulLength) = inversePassPath.GetUsefullLength(0.0f, -1, -1);
                    deadlockInverseInfo.AllowedTrains.Add(-1);
                }
            }
            // if existing path, add trainnumber if set and path is not public
            else if (trainNumber >= 0)
            {
                DeadlockPathInfo deadlockPathInfo = deadlock.AvailablePathList[PathIndex];
                if (!deadlockPathInfo.AllowedTrains.Contains(-1))
                {
                    deadlockPathInfo.AllowedTrains.Add(deadlock.GetTrainAndSubpathIndex(trainNumber, sublistRef));
                }
            }
        }

        // search for valid passing paths
        // includes public paths
        private void SearchPassingPaths(int trainNumber, float trainLength)
        {
            for (int i = 0; i <= TCRouteSubpaths.Count - 1; i++)
            {
                TrackCircuitPartialPathRoute subPath = TCRouteSubpaths[i];

                for (int j = 0; j <= subPath.Count - 1; j++)
                {
                    TrackCircuitRouteElement routeElement = subPath[j];
                    TrackCircuitSection section = routeElement.TrackCircuitSection;

                    // if section is a deadlock boundary determine available paths
                    if (section.DeadlockReference > 0)
                    {
                        DeadlockInfo thisDeadlockInfo = Simulator.Instance.SignalEnvironment.DeadlockInfoList[section.DeadlockReference];
                        int nextElement = thisDeadlockInfo.SetTrainDetails(trainNumber, i, trainLength, subPath, j);

                        if (nextElement < 0) // end of path reached
                        {
                            break;
                        }
                        else // skip deadlock area
                        {
                            j = nextElement;
                        }
                    }
                }
            }
        }

        // search for loops
        //
        private void LoopSearch()
        {
            List<List<int[]>> loopList = new List<List<int[]>>();

            foreach (TrackCircuitPartialPathRoute partialRoute in TCRouteSubpaths)
            {
                Dictionary<int, int> sections = new Dictionary<int, int>();
                List<int[]> loopInfo = new List<int[]>();
                loopList.Add(loopInfo);

                bool loopset = false;

                for (int i = 0; i < partialRoute.Count; i++)
                {
                    TrackCircuitRouteElement thisElement = partialRoute[i];

                    if (sections.TryGetValue(thisElement.TrackCircuitSection.Index, out int loopindex) && !loopset)
                    {
                        int[] loopDetails = [loopindex, i];
                        loopInfo.Add(loopDetails);
                        loopset = true;

                        // check if loop reverses or continues
                    }
                    else if (sections.TryGetValue(thisElement.TrackCircuitSection.Index, out int preloopindex) && loopset)
                    {
                        if (thisElement.Direction == partialRoute[preloopindex].Direction)
                        {
                            loopindex++;
                        }
                        else
                        {
                            loopindex--;
                        }

                        if (loopindex >= 0 && loopindex <= (partialRoute.Count - 1))
                        {
                            loopset = (thisElement.TrackCircuitSection.Index == partialRoute[loopindex].TrackCircuitSection.Index);
                        }
                    }
                    else
                    {
                        loopset = false;
                    }

                    if (!loopset && !sections.ContainsKey(thisElement.TrackCircuitSection.Index))
                    {
                        sections.Add(thisElement.TrackCircuitSection.Index, i);
                    }
                }
            }

            // check for inner loops within outer loops
            // if found, remove outer loops
            for (int i = 0; i <= TCRouteSubpaths.Count - 1; i++)
            {
                List<int> invalids = new List<int>();
                for (int j = loopList[i].Count - 1; j >= 1; j--)
                {
                    if (loopList[i][j][1] > loopList[i][j - 1][0] && loopList[i][j][0] < loopList[i][j - 1][1])
                    {
                        invalids.Add(j);
                    }
                }
                foreach (int j in invalids)
                {
                    loopList[i].RemoveAt(j);
                }
            }

            // preset loop ends to invalid
            for (int i = 0; i <= TCRouteSubpaths.Count - 1; i++)
            {
                LoopEnd.Add(-1);
            }

            // split loops with overlap - search backward as subroutes may be added
            int orgTotalRoutes = TCRouteSubpaths.Count;
            for (int i = orgTotalRoutes - 1; i >= 0; i--)
            {
                TrackCircuitPartialPathRoute partialRoute = TCRouteSubpaths[i];

                List<int[]> loopInfo = loopList[i];

                // loop through looppoints backward as well
                for (int j = loopInfo.Count - 1; j >= 0; j--)
                {
                    int[] loopDetails = loopInfo[j];

                    // copy route and add after existing route
                    // remove points from loop-end in first route
                    // remove points upto loop-start in second route
                    TrackCircuitPartialPathRoute newRoute = new TrackCircuitPartialPathRoute(partialRoute);
                    partialRoute.RemoveRange(loopDetails[1], partialRoute.Count - loopDetails[1]);
                    newRoute.RemoveRange(0, loopDetails[0] + 1);

                    // add new route to list
                    TCRouteSubpaths.Insert(i + 1, newRoute);

                    // set loop end
                    LoopEnd.Insert(i, partialRoute[loopDetails[0]].TrackCircuitSection.Index);

                    // create dummy reversal lists
                    // shift waiting points and reversal lists
                    TrackCircuitReversalInfo dummyReversal = new TrackCircuitReversalInfo()
                    {
                        ReversalSectionIndex = partialRoute[partialRoute.Count - 1].TrackCircuitSection.Index,
                        ReversalIndex = partialRoute.Count - 1,
                        ReverseReversalOffset = partialRoute[partialRoute.Count - 1].TrackCircuitSection.Length
                    };

                    ReversalInfo.Insert(i, dummyReversal);

                    foreach (WaitingPointDetail waitingPoint in WaitingPoints)
                    {
                        if (waitingPoint.SubListIndex >= i)
                            waitingPoint.SubListIndex++;
                    }
                }
            }
        }

        // Convert waiting point to section no.
        private static int ConvertWaitingPoint(AIPathNode stopPathNode)
        {
            TrackVectorNode waitingNode = RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes[stopPathNode.NextMainTVNIndex];
            TrackVectorSection firstSection = waitingNode.TrackVectorSections[0];
            Traveller tdbTraveller = new Traveller(waitingNode, firstSection.Location, Direction.Forward);
            float offset = tdbTraveller.DistanceTo(waitingNode, stopPathNode.Location);

            int sectionIndex = -1;

            foreach (TrackCircuitSectionCrossReference crossReference in waitingNode.TrackCircuitCrossReferences)
            {
                if (offset < (crossReference.OffsetLength[TrackDirection.Reverse] + crossReference.Length))
                {
                    sectionIndex = crossReference.Index;
                    break;
                }
            }

            if (sectionIndex < 0)
            {
                sectionIndex = waitingNode.TrackCircuitCrossReferences[0].Index;
            }

            return sectionIndex;
        }

        // Check for reversal offset margin
        public void SetReversalOffset(float trainLength, bool timetableMode)
        {
            TrackCircuitReversalInfo reversal = ReversalInfo[ActiveSubPath];
            reversal.SignalUsed = reversal.Valid && reversal.SignalAvailable && timetableMode && trainLength < reversal.SignalOffset;
        }

        // build station xref list
        private void SetStationReference(List<TrackCircuitPartialPathRoute> subpaths, TrackCircuitSection section)
        {
            foreach (int platformRef in section.PlatformIndices)
            {
                PlatformDetails platform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[platformRef];

                if (!StationCrossReferences.ContainsKey(platform.Name))
                {
                    int[] platformInfo = new int[3] { subpaths.Count - 1, subpaths[subpaths.Count - 1].Count - 1, platform.PlatformReference[0] };
                    StationCrossReferences.Add(platform.Name, platformInfo);
                }
            }
        }

        // add sections from other path at front
        public void AddSectionsAtStart(TrackCircuitPartialPathRoute otherRoute, Train train, bool reverse)
        {
            ArgumentNullException.ThrowIfNull(otherRoute);
            ArgumentNullException.ThrowIfNull(train);

            int addedSections = 0;

            // add sections from other path at front
            // as sections are inserted at index 0, insertion must take place in reverse sequence to preserve original sequence

            // add in reverse sequence - also reverse direction
            if (reverse)
            {
                bool startAdding = false;
                foreach (TrackCircuitRouteElement routeElement in otherRoute)
                {
                    if (startAdding)
                    {
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        newElement.Direction = newElement.Direction.Reverse();
                        TCRouteSubpaths[0].Insert(0, newElement);
                        addedSections++;
                    }
                    else if (TCRouteSubpaths[0].GetRouteIndex(routeElement.TrackCircuitSection.Index, 0) < 0)
                    {
                        startAdding = true;
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        newElement.Direction = newElement.Direction.Reverse();
                        TCRouteSubpaths[0].Insert(0, newElement);
                        addedSections++;
                    }
                }
            }
            // add in forward sequence
            else
            {
                bool startAdding = false;
                for (int i = otherRoute.Count - 1; i >= 0; i--)
                {
                    TrackCircuitRouteElement routeElement = otherRoute[i];
                    if (startAdding)
                    {
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        TCRouteSubpaths[0].Insert(0, newElement);
                        addedSections++;
                    }
                    else if (TCRouteSubpaths[0].GetRouteIndex(routeElement.TrackCircuitSection.Index, 0) < 0)
                    {
                        startAdding = true;
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        TCRouteSubpaths[0].Insert(0, newElement);
                        addedSections++;
                    }
                }
            }

            // add number of added sections to reversal info
            if (ReversalInfo[0].Valid)
            {
                ReversalInfo[0].FirstDivergeIndex += addedSections;
                ReversalInfo[0].FirstSignalIndex += addedSections;
                ReversalInfo[0].LastDivergeIndex += addedSections;
                ReversalInfo[0].LastSignalIndex += addedSections;
            }

            // add number of sections to station stops
            foreach (StationStop stationStop in train.StationStops ?? Enumerable.Empty<StationStop>())
            {
                if (stationStop.SubrouteIndex == 0)
                {
                    stationStop.RouteIndex += addedSections;
                }
                else
                {
                    break;
                }
            }
        }

        // Add subroute from other path at front
        public void AddSubrouteAtStart(TrackCircuitPartialPathRoute otherRoute, Train train)
        {
            ArgumentNullException.ThrowIfNull(otherRoute);
            ArgumentNullException.ThrowIfNull(train);

            TCRouteSubpaths.Insert(0, new TrackCircuitPartialPathRoute(otherRoute));

            // add additional reversal info
            ReversalInfo.Insert(0, new TrackCircuitReversalInfo());
            RoughReversalInfos.Insert(0, null);

            // add additional loop end info
            LoopEnd.Insert(0, -1);

            // adjust waiting point indices
            foreach (WaitingPointDetail waitingPoint in WaitingPoints)
            {
                waitingPoint.SubListIndex++;
            }

            // shift subroute index for station stops
            // add number of sections to station stops
            foreach (StationStop stationStop in train.StationStops ?? Enumerable.Empty<StationStop>())
            {
                stationStop.SubrouteIndex++;
            }
        }

        // Add sections from other path at end
        public void AddSectionsAtEnd(TrackCircuitPartialPathRoute otherRoute, bool reverse)
        {
            ArgumentNullException.ThrowIfNull(otherRoute);

            int addedSections = 0;

            // add sections from other path at end
            // add in reverse sequence
            if (reverse)
            {
                bool startAdding = false;
                for (int i = otherRoute.Count - 1; i >= 0; i--)
                {
                    TrackCircuitRouteElement routeElement = otherRoute[i];

                    if (startAdding)
                    {
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        newElement.Direction = newElement.Direction.Reverse();
                        TCRouteSubpaths[TCRouteSubpaths.Count - 1].Add(newElement);
                        addedSections++;
                    }
                    else if (TCRouteSubpaths[TCRouteSubpaths.Count - 1].GetRouteIndex(routeElement.TrackCircuitSection.Index, 0) < 0)
                    {
                        startAdding = true;
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        newElement.Direction = newElement.Direction.Reverse();
                        TCRouteSubpaths[TCRouteSubpaths.Count - 1].Add(newElement);
                        addedSections++;
                    }
                }
            }
            // add in forward sequence
            else
            {
                bool startAdding = false;
                for (int i = 0; i < otherRoute.Count; i++)
                {
                    TrackCircuitRouteElement routeElement = otherRoute[i];

                    if (startAdding)
                    {
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        TCRouteSubpaths[TCRouteSubpaths.Count - 1].Add(newElement);
                        addedSections++;
                    }
                    else if (TCRouteSubpaths[TCRouteSubpaths.Count - 1].GetRouteIndex(routeElement.TrackCircuitSection.Index, 0) < 0)
                    {
                        startAdding = true;
                        TrackCircuitRouteElement newElement = new TrackCircuitRouteElement(routeElement);
                        TCRouteSubpaths[TCRouteSubpaths.Count - 1].Add(newElement);
                        addedSections++;
                    }
                }
            }
        }

        // Add subroute from other path at end
        public void AddSubrouteAtEnd(TrackCircuitPartialPathRoute otherRoute)
        {
            ArgumentNullException.ThrowIfNull(otherRoute);

            TCRouteSubpaths.Add(new TrackCircuitPartialPathRoute(otherRoute));

            // add additional reversal info
            ReversalInfo.Add(new TrackCircuitReversalInfo());
            RoughReversalInfos.Add(null);

            // add additional loop end info
            LoopEnd.Add(-1);
        }
    }
}
