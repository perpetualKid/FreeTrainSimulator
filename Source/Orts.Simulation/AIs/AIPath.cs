// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

/* AIPath
 * 
 * Contains a processed version of the MSTS PAT file.
 * The processing saves information needed for AI train dispatching and to align switches.
 * Could this be used for player trains also?
 * 
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Simulation.AIs
{
    public class AIPath : ISaveStateApi<AiPathSaveState>
    {
        public AIPathNode FirstNode { get; }    // path starting node
        //public AIPathNode LastVisitedNode; not used anymore
        public Collection<AIPathNode> Nodes { get; } = new Collection<AIPathNode>();
        public string PathName { get; } //name of the path to be able to print it.

        public AIPath() { }

        /// <summary>
        /// Creates an AIPath from path model information.
        /// First creates all the nodes and then links them together into a main list
        /// with optional parallel siding list.
        /// </summary>
        public AIPath(PathModel pathModel, bool timetableMode)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            PathName = pathModel.Name;
            bool fatalerror = false;
            if (pathModel.PathNodes.Length <= 0)
            {
                Nodes = null;
                return;
            }

            foreach (FreeTrainSimulator.Models.Content.PathNode pathNode in pathModel.PathNodes)
                Nodes.Add(new AIPathNode(pathNode, timetableMode));
            FirstNode = Nodes[0];
            //LastVisitedNode = FirstNode;            

            // Connect the various nodes to each other
            for (int i = 0; i < Nodes.Count; i++)
            {
                AIPathNode node = Nodes[i];
                node.Index = i;
                FreeTrainSimulator.Models.Content.PathNode tpn = pathModel.PathNodes[i];

                // find TVNindex to next main node.
                if (tpn.NextMainNode > -1)
                {
                    node.NextMainNode = Nodes[tpn.NextMainNode];
                    node.NextMainTVNIndex = node.FindTVNIndex(node.NextMainNode, i == 0 ? -1 : Nodes[i - 1].NextMainTVNIndex);
                    if (node.JunctionIndex >= 0)
                        node.IsFacingPoint = TestFacingPoint(node.JunctionIndex, node.NextMainTVNIndex);
                    if (node.NextMainTVNIndex < 0)
                    {
                        node.NextMainNode = null;
                        Trace.TraceWarning("Cannot find main track for node {1} in path {0}", pathModel.Hierarchy(), i);
                        fatalerror = true;
                    }
                }

                // find TVNindex to next siding node
                if (tpn.NextSidingNode > -1)
                {
                    node.NextSidingNode = Nodes[tpn.NextSidingNode];
                    node.NextSidingTVNIndex = node.FindTVNIndex(node.NextSidingNode, i == 0 ? -1 : Nodes[i - 1].NextMainTVNIndex);
                    if (node.JunctionIndex >= 0)
                        node.IsFacingPoint = TestFacingPoint(node.JunctionIndex, node.NextSidingTVNIndex);
                    if (node.NextSidingTVNIndex < 0)
                    {
                        node.NextSidingNode = null;
                        Trace.TraceWarning("Cannot find siding track for node {1} in path {0}", pathModel.Hierarchy(), i);
                        fatalerror = true;
                    }
                }

                if (node.NextMainNode != null && node.NextSidingNode != null)
                    node.Type = TrainPathNodeType.SidingStart;
            }

            FindSidingEnds();

            if (fatalerror)
                Nodes = null; // invalid path - do not return any nodes
        }

        /// <summary>
        /// constructor out of other path
        /// </summary>
        /// <param name="otherPath"></param>

        public AIPath(AIPath otherPath)
        {
            ArgumentNullException.ThrowIfNull(otherPath);
            FirstNode = new AIPathNode(otherPath.FirstNode);
            foreach (AIPathNode otherNode in otherPath.Nodes)
            {
                Nodes.Add(new AIPathNode(otherNode));
            }

            // set correct node references

            for (int iNode = 0; iNode <= otherPath.Nodes.Count - 1; iNode++)
            {
                AIPathNode otherNode = otherPath.Nodes[iNode];
                if (otherNode.NextMainNode != null)
                {
                    Nodes[iNode].NextMainNode = Nodes[otherNode.NextMainNode.Index];
                }

                if (otherNode.NextSidingNode != null)
                {
                    Nodes[iNode].NextSidingNode = Nodes[otherNode.NextSidingNode.Index];
                }
            }

            if (otherPath.FirstNode.NextMainNode != null)
            {
                FirstNode.NextMainNode = Nodes[otherPath.FirstNode.NextMainNode.Index];
            }
            if (otherPath.FirstNode.NextSidingNode != null)
            {
                FirstNode.NextSidingNode = Nodes[otherPath.FirstNode.NextSidingNode.Index];
            }

            PathName = otherPath.PathName;
        }

        /// <summary>
        /// Find all nodes that are the end of a siding (so where main path and siding path come together again)
        /// </summary>
        private void FindSidingEnds()
        {
            Dictionary<int, AIPathNode> lastUse = new Dictionary<int, AIPathNode>();
            for (AIPathNode node1 = FirstNode; node1 != null; node1 = node1.NextMainNode)
            {
                if (node1.JunctionIndex >= 0)
                    lastUse[node1.JunctionIndex] = node1;
                AIPathNode node2 = node1.NextSidingNode;
                while (node2 != null && node2.NextSidingNode != null)
                {
                    if (node2.JunctionIndex >= 0)
                        lastUse[node2.JunctionIndex] = node2;
                    node2 = node2.NextSidingNode;
                }
                if (node2 != null)
                    node2.Type = TrainPathNodeType.SidingEnd;
            }
            //foreach (KeyValuePair<int, AIPathNode> kvp in lastUse)
            //    kvp.Value.IsLastSwitchUse = true;
        }

        /// <summary>
        /// returns true if the specified vector node is at the facing point end of
        /// the specified juction node, else false.
        /// </summary>
        private static bool TestFacingPoint(int junctionIndex, int vectorIndex)
        {
            if (junctionIndex < 0 || vectorIndex < 0)
                return false;
            TrackJunctionNode tn = RuntimeData.Instance.TrackDB.TrackNodes.JunctionNodes[junctionIndex];
            if (tn == null || tn.TrackPins[0].Link == vectorIndex)
                return false;
            return true;
        }

        public async ValueTask<AiPathSaveState> Snapshot()
        {
            return new AiPathSaveState()
            {
                AiPathNodeSaveStates = await Nodes.SnapshotCollection<AiPathNodeSaveState, AIPathNode>().ConfigureAwait(false),
            };
        }

        public async ValueTask Restore(AiPathSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            await Nodes.RestoreCollectionCreateNewItems(saveState.AiPathNodeSaveStates).ConfigureAwait(false);

            foreach (AiPathNodeSaveState saveNode in saveState.AiPathNodeSaveStates)
            {
                AIPathNode result = Nodes.Where(node => node.Index == saveNode.Index).FirstOrDefault();
                result.NextMainNode = Nodes[saveNode.NextMainNodeIndex];
                result.NextSidingNode = Nodes[saveNode.NextSidingNodeIndex];
            }
        }
    }

    public class AIPathNode : ISaveStateApi<AiPathNodeSaveState>
    {
        public int Index { get; set; }
        public FreeTrainSimulator.Common.TrainPathNodeType Type { get; set; } = FreeTrainSimulator.Common.TrainPathNodeType.Other;
        public int WaitTimeS { get; private set; }               // number of seconds to wait after stopping at this node
        public int WaitUntil { get; private set; }               // clock time to wait until if not zero
        public AIPathNode NextMainNode { get; set; }     // next path node on main path
        public AIPathNode NextSidingNode { get; set; }   // next path node on siding path
        public int NextMainTVNIndex { get; set; } = -1;   // index of main vector node leaving this path node
        public int NextSidingTVNIndex { get; set; } = -1; // index of siding vector node leaving this path node
        public WorldLocation Location { get; set; }      // coordinates for this path node
        public int JunctionIndex { get; set; } = -1;      // index of junction node, -1 if none
        public bool IsFacingPoint { get; set; }          // true if this node entered from the facing point end

        public AIPathNode() { }

        /// <summary>
        /// Creates a single AIPathNode and initializes everything that do not depend on other nodes.
        /// The AIPath constructor will initialize the rest.
        /// </summary>
        public AIPathNode(FreeTrainSimulator.Models.Content.PathNode pathNode, bool timetableMode)
        {
            ArgumentNullException.ThrowIfNull(pathNode);

            WaitTimeS = pathNode.WaitInfo?.WaitTime ?? 0;
            Location = pathNode.Location;

            switch (pathNode.NodeType)
            {
                case PathNodeType _ when (pathNode.NodeType & PathNodeType.Reversal) == PathNodeType.Reversal:
                    Type = TrainPathNodeType.Reverse;
                    break;
                case PathNodeType _ when (pathNode.NodeType & PathNodeType.Wait) == PathNodeType.Wait:
                    Type = TrainPathNodeType.Stop;
                    break;
                case PathNodeType _ when (pathNode.NodeType & PathNodeType.Invalid) == PathNodeType.Invalid && timetableMode:
                    Type = TrainPathNodeType.Invalid;
                    break;
                case PathNodeType _ when (pathNode.NodeType & PathNodeType.Junction) == PathNodeType.Junction:
                    JunctionIndex = FindJunctionOrEndIndex(Location, true);
                    break;
            }
        }

        /// <summary>
        /// Constructor from other AIPathNode
        /// </summary>
        /// <param name="otherNode"></param>

        public AIPathNode(AIPathNode otherNode)
        {
            ArgumentNullException.ThrowIfNull(otherNode);
            Index = otherNode.Index;
            Type = otherNode.Type;
            WaitTimeS = otherNode.WaitTimeS;
            WaitUntil = otherNode.WaitUntil;
            NextMainNode = null; // set after completion of copying to get correct reference
            NextSidingNode = null; // set after completion of copying to get correct reference
            NextMainTVNIndex = otherNode.NextMainTVNIndex;
            NextSidingTVNIndex = otherNode.NextSidingTVNIndex;
            Location = otherNode.Location;
            JunctionIndex = otherNode.JunctionIndex;
            IsFacingPoint = otherNode.IsFacingPoint;
        }


        public ValueTask<AiPathNodeSaveState> Snapshot()
        {
            return ValueTask.FromResult(new AiPathNodeSaveState()
            {
                Index = Index,
                NodeType = Type,
                WaitTime = WaitTimeS,
                WaitUntil = WaitUntil,
                NextMainNodeIndex = NextMainNode == null ? -1 : NextMainNode.Index,
                NextMainTrackVectorNodeIndex = NextMainTVNIndex,
                NextSidingNodeIndex = NextSidingNode == null ? -1 : NextSidingNode.Index,
                NextSidingTrackVectorNodeIndex = NextSidingTVNIndex,
                JunctionIndex = JunctionIndex,
                FacingJunction = IsFacingPoint,
                Location = Location,
            });
        }

        public ValueTask Restore(AiPathNodeSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            Index = saveState.Index;
            Type = saveState.NodeType;
            WaitTimeS = saveState.WaitTime;
            WaitUntil = saveState.WaitUntil;
            NextMainTVNIndex = saveState.NextMainTrackVectorNodeIndex;
            NextSidingTVNIndex = saveState.NextSidingTrackVectorNodeIndex;
            JunctionIndex = saveState.JunctionIndex;
            IsFacingPoint = saveState.FacingJunction;
            Location = saveState.Location;

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Returns the index of the vector node connection this path node to the (given) nextNode.
        /// </summary>
        public int FindTVNIndex(AIPathNode nextNode, int previousNextMainTVNIndex)
        {
            ArgumentNullException.ThrowIfNull(nextNode);

            int junctionIndexThis = JunctionIndex;
            int junctionIndexNext = nextNode.JunctionIndex;

            // if this is no junction, try to find the TVN index 
            if (junctionIndexThis < 0)
            {
                try
                {
                    return FindTrackNodeIndex(this);
                }
                catch (InvalidDataException)
                {
                    junctionIndexThis = FindJunctionOrEndIndex(this.Location, false);
                }
            }

            // this is a junction; if the next node is no junction, try that one.
            if (junctionIndexNext < 0)
            {
                try
                {
                    return FindTrackNodeIndex(nextNode);
                }
                catch (InvalidDataException)
                {
                    junctionIndexNext = FindJunctionOrEndIndex(nextNode.Location, false);
                }
            }

            //both this node and the next node are junctions: find the vector node connecting them.
            var iCand = -1;
            foreach (TrackVectorNode vectorNode in RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes)
            {
                if (vectorNode.TrackPins[0].Link == junctionIndexThis && vectorNode.TrackPins[1].Link == junctionIndexNext)
                {
                    iCand = vectorNode.Index;
                    if (iCand != previousNextMainTVNIndex)
                        break;
                    Trace.TraceInformation("Managing rocket loop at trackNode {0}", iCand);
                }
                else if (vectorNode.TrackPins[1].Link == junctionIndexThis && vectorNode.TrackPins[0].Link == junctionIndexNext)
                {
                    iCand = vectorNode.Index;
                    if (iCand != previousNextMainTVNIndex)
                        break;
                    Trace.TraceInformation("Managing rocket loop at trackNode {0}", iCand);
                }
            }
            return iCand;
        }

        /// <summary>
        /// Try to find the tracknode corresponding to the given node's location.
        /// This will raise an exception if it cannot be found
        /// </summary>
        /// <param name="TDB"></param>
        /// <param name="tsectiondat"></param>
        /// <param name="node"></param>
        /// <returns>The track node index that has been found (or an exception)</returns>
        private static int FindTrackNodeIndex(AIPathNode node)
        {
            Traveller traveller = new Traveller(node.Location);
            return traveller.TrackNode?.Index ?? -1;
        }

        /// <summary>
        /// Find the junctionNode or endNode closest to the given location
        /// </summary>
        /// <param name="location">Location for which we want to find the node</param>
        /// <param name="trackDB">track database containing the trackNodes</param>
        /// <param name="wantJunctionNode">true if a junctionNode is wanted, false for a endNode</param>
        /// <returns>tracknode index of the closes node</returns>
        public static int FindJunctionOrEndIndex(in WorldLocation location, bool wantJunctionNode)
        {
            int bestIndex = -1;
            float bestDistance2 = 1e10f;
            for (int j = 0; j < RuntimeData.Instance.TrackDB.TrackNodes.Count; j++)
            {
                TrackNode tn = RuntimeData.Instance.TrackDB.TrackNodes[j];
                if (tn == null)
                    continue;
                if (wantJunctionNode && !(tn is TrackJunctionNode))
                    continue;
                if (!wantJunctionNode && !(tn is TrackEndNode))
                    continue;
                if (tn.UiD.Location.Tile != location.Tile)
                    continue;

                float dx = tn.UiD.Location.Location.X - location.Location.X;
                dx += (tn.UiD.Location.TileX - location.TileX) * 2048;
                float dz = tn.UiD.Location.Location.Z - location.Location.Z;
                dz += (tn.UiD.Location.TileZ - location.TileZ) * 2048;
                float dy = tn.UiD.Location.Location.Y - location.Location.Y;
                float d = dx * dx + dy * dy + dz * dz;
                if (bestDistance2 > d)
                {
                    bestIndex = j;
                    bestDistance2 = d;
                }

            }
            return bestIndex;
        }
    }
}
