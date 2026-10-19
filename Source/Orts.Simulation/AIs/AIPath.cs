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
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

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
            try
            {
                if (pathModel.PathNodes.IsDefaultOrEmpty)
                    throw new InvalidDataException("Path has no nodes.");

                foreach (PathNode pathNode in pathModel.PathNodes)
                {
                    if (pathNode == null || pathNode.NodeType.Includes(PathNodeType.Invalid))
                        throw new InvalidDataException($"Invalid path node {Nodes.Count}.");
                    
                    AIPathNode node = new AIPathNode(pathNode, timetableMode) { Index = Nodes.Count };
                    
                    if (node.Index == 0 && node.Type is TrainPathNodeType.Stop or TrainPathNodeType.Reverse)
                        throw new InvalidDataException("Unsupported wait/reverse event at first main node; the circuit consumer skips this event.");
                    
                    if (pathNode.NodeType.Includes(PathNodeType.Junction) && node.JunctionIndex < 0)
                        throw new InvalidDataException($"Cannot resolve junction at node {node.Index}.");
                    
                    // The runtime cannot process an event at a junction, or two events at one node.
                    if ((node.JunctionIndex >= 0 && node.Type != TrainPathNodeType.Other) || pathNode.NodeType.Includes(PathNodeType.Wait | PathNodeType.Reversal))
                        throw new InvalidDataException($"Unsupported junction or combined wait/reverse event at node {node.Index}.");
                    Nodes.Add(node);
                }
                FirstNode = Nodes[0];

                // Link the entire graph before using any predecessor or track context.
                for (int i = 0; i < Nodes.Count; i++)
                {
                    PathNode source = pathModel.PathNodes[i];

                    if (source.NextMainNode < -1 || source.NextMainNode >= Nodes.Count || source.NextSidingNode < -1 || source.NextSidingNode >= Nodes.Count)
                        throw new InvalidDataException($"Out-of-range path link at node {i}.");
                    
                    Nodes[i].NextMainNode = source.NextMainNode < 0 ? null : Nodes[source.NextMainNode];
                    Nodes[i].NextSidingNode = source.NextSidingNode < 0 ? null : Nodes[source.NextSidingNode];
                }

                List<AIPathNode> main = ReadMainPath();
                List<List<AIPathNode>> sidings = ReadSidings(main);
                ResolveTracks(main, false, -1);
                foreach (List<AIPathNode> siding in sidings)
                {
                    int start = main.IndexOf(siding[0]);
                    ResolveTracks(siding, true, start == 0 ? -1 : main[start - 1].NextMainTVNIndex);
                }
                ProjectSidings(main, sidings);
            }
            catch (InvalidDataException error)
            {
                Trace.TraceWarning("Cannot use path {0}: {1}", pathModel.Hierarchy(), error.Message);
                Nodes = null;
                FirstNode = null;
            }
        }

        /// <summary>
        /// Places a traveller using the resolved first edge, without reselecting nearby parallel track.
        /// </summary>
        internal TrackTraveller CreateInitialTraveller()
        {
            return Nodes == null || FirstNode?.NextMainNode?.IncomingTrack is not { } initial ||
                initial.VectorIndex != FirstNode.NextMainTVNIndex
                ? throw new InvalidDataException($"Path {PathName} has no resolved initial traversal.")
                : TrackTraveller.InitializeTraveller(FirstNode.Location, initial.VectorIndex, initial.Direction,
                TrackDataBaseType.Rail) ?? throw new InvalidDataException($"Cannot place path {PathName} on resolved vector {initial.VectorIndex}.");
        }

        private List<AIPathNode> ReadMainPath()
        {
            List<AIPathNode> main = new List<AIPathNode>();
            HashSet<AIPathNode> visited = new HashSet<AIPathNode>();
            bool reversed = false;
            for (AIPathNode node = FirstNode; node != null; node = node.NextMainNode)
            {
                if (!visited.Add(node))
                    throw new InvalidDataException($"Main path cycle at node {node.Index}.");

                if (node.JunctionIndex >= 0)
                    reversed = false;
                else if (node.Type == TrainPathNodeType.Reverse)
                {
                    if (reversed)
                        throw new InvalidDataException($"Multiple reversals in one continuous vector occurrence at node {node.Index}; the circuit consumer cannot preserve every leg.");
                    reversed = true;
                }
                main.Add(node);
            }

            return main.Count < 2 ? throw new InvalidDataException("Path needs at least two main nodes.") : main;
        }

        private List<List<AIPathNode>> ReadSidings(List<AIPathNode> main)
        {
            Dictionary<AIPathNode, int> positions = main.Select((node, index) => (node, index)) .ToDictionary(item => item.node, item => item.index);
            HashSet<AIPathNode> visited = new HashSet<AIPathNode>(main);
            List<List<AIPathNode>> sidings = new List<List<AIPathNode>>();
            int previousEnd = -1;

            for (int i = 0; i < main.Count; i++)
            {
                if (main[i].NextSidingNode == null)
                    continue;
            
                if (i <= previousEnd)
                    throw new InvalidDataException($"Nested or overlapping siding at node {main[i].Index}.");
                
                List<AIPathNode> siding = new List<AIPathNode> { main[i] };
                AIPathNode node = main[i].NextSidingNode;
                
                while (node != null && !positions.ContainsKey(node))
                {
                    if (!visited.Add(node) || node.NextMainNode != null)
                        throw new InvalidDataException($"Cyclic, shared or nested siding at node {node.Index}.");
                
                    if (node.Type != TrainPathNodeType.Other || node.WaitTimeS != 0)
                        throw new InvalidDataException($"Unsupported siding event at node {node.Index}; branch waits/reversals cannot be preserved.");
                    siding.Add(node);
                    node = node.NextSidingNode;
                }

                if (node == null || positions[node] <= i)
                    throw new InvalidDataException($"Siding at node {main[i].Index} does not rejoin the main path downstream.");

                previousEnd = positions[node];
                siding.Add(node);
                sidings.Add(siding);
            }

            return visited.Count != Nodes.Count ? throw new InvalidDataException("Path contains unreachable nodes.") : sidings;
        }

        private static void ResolveTracks(List<AIPathNode> traversal, bool siding, int incoming)
        {
            AIPathNode authoredLast = traversal[traversal.Count - 1];
            (int VectorIndex, TrackDirection Direction)? mainArrival = authoredLast.IncomingTrack;
            // Bind the alternative arrival independently of the shared main node.
            AIPathNode last = siding ? new AIPathNode(authoredLast) : authoredLast;

            for (int i = 0; i < traversal.Count - 1; i++)
            {
                AIPathNode node = traversal[i];
                AIPathNode next = i == traversal.Count - 2 ? last : traversal[i + 1];
                int vector = node.FindTVNIndex(next, incoming);
            
                if (vector < 0)
                    throw new InvalidDataException($"Cannot unambiguously connect {(siding ? "siding" : "main")} nodes {node.Index} and {traversal[i + 1].Index} (incoming vector {incoming}).");
                
                if (siding)
                    node.NextSidingTVNIndex = vector;
                else
                    node.NextMainTVNIndex = vector;
                
                incoming = vector;
            }
            // Terminal events still need the vector containing their offset; they have no outgoing edge.
            if (!siding && last.JunctionIndex < 0 && last.Type is TrainPathNodeType.Stop or TrainPathNodeType.Reverse)
                last.NextMainTVNIndex = incoming;
            
            if (siding)
            {
                if (last.JunctionIndex < 0 && last.IncomingTrack != mainArrival)
                    throw new InvalidDataException($"Incompatible siding arrival vector or direction at rejoin node {last.Index}.");
            
                if (authoredLast.NextMainNode != null)
                {
                    AIPathNode continuation = new AIPathNode(authoredLast.NextMainNode);
                
                    if (last.FindTVNIndex(continuation, incoming) != authoredLast.NextMainTVNIndex || continuation.IncomingTrack != authoredLast.NextMainNode.IncomingTrack)
                        throw new InvalidDataException($"Siding cannot continue along the main path at rejoin node {last.Index} with a compatible arrival vector and direction.");
                }
            }
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
        /// Authored passing endpoints can lie outside the physical switches. Project only the
        /// transient links onto matching occurrences already present in the materialized graph.
        /// </summary>
        private void ProjectSidings(List<AIPathNode> main, List<List<AIPathNode>> sidings)
        {
            HashSet<int> physicalSplits = new HashSet<int>();
            foreach (List<AIPathNode> siding in sidings)
            {
                int start = main.IndexOf(siding[0]);
                int end = main.IndexOf(siding[siding.Count - 1]);
                int incoming = start == 0 ? -1 : main[start - 1].NextMainTVNIndex;
                List<AIPathNode> mainSection = main.GetRange(start, end - start + 1);
                List<(AIPathNode Node, int Position, int Incoming, int Outgoing)> mainJunctions = ReadJunctionTraversal(mainSection, false, incoming);
                List<(AIPathNode Node, int Position, int Incoming, int Outgoing)> sideJunctions = ReadJunctionTraversal(siding, true, incoming);

                int split = 0;
                while (split < mainJunctions.Count && split < sideJunctions.Count &&
                    mainJunctions[split].Node.JunctionIndex == sideJunctions[split].Node.JunctionIndex &&
                    mainJunctions[split].Incoming == sideJunctions[split].Incoming &&
                    mainJunctions[split].Outgoing == sideJunctions[split].Outgoing)
                    split++;
                if (split >= mainJunctions.Count || split >= sideJunctions.Count ||
                    mainJunctions[split].Node.JunctionIndex != sideJunctions[split].Node.JunctionIndex ||
                    mainJunctions[split].Incoming != sideJunctions[split].Incoming)
                    throw new InvalidDataException($"Siding at node {siding[0].Index} has no unambiguous physical split in the generated graph.");

                int mainRejoin = mainJunctions.Count - 1;
                int sideRejoin = sideJunctions.Count - 1;
                while (mainRejoin > split && sideRejoin > split &&
                    mainJunctions[mainRejoin].Node.JunctionIndex == sideJunctions[sideRejoin].Node.JunctionIndex &&
                    mainJunctions[mainRejoin].Incoming == sideJunctions[sideRejoin].Incoming &&
                    mainJunctions[mainRejoin].Outgoing == sideJunctions[sideRejoin].Outgoing)
                {
                    mainRejoin--;
                    sideRejoin--;
                }
                if (mainRejoin <= split || sideRejoin <= split ||
                    mainJunctions[mainRejoin].Node.JunctionIndex != sideJunctions[sideRejoin].Node.JunctionIndex ||
                    mainJunctions[mainRejoin].Outgoing != sideJunctions[sideRejoin].Outgoing ||
                    mainJunctions[mainRejoin].Incoming == sideJunctions[sideRejoin].Incoming)
                    throw new InvalidDataException($"Siding at node {siding[0].Index} has no unambiguous physical rejoin in the generated graph.");

                (AIPathNode Node, int Position, int Incoming, int Outgoing) mainSplit = mainJunctions[split];
                (AIPathNode Node, int Position, int Incoming, int Outgoing) sideSplit = sideJunctions[split];
                (AIPathNode Node, int Position, int Incoming, int Outgoing) mainEnd = mainJunctions[mainRejoin];
                (AIPathNode Node, int Position, int Incoming, int Outgoing) sideEnd = sideJunctions[sideRejoin];

                for (int i = 0; i < mainSection.Count - 1; i++)
                {
                    if ((i < mainSplit.Position || i > mainEnd.Position) &&
                        mainSection[i].Type is TrainPathNodeType.Stop or TrainPathNodeType.Reverse)
                        throw new InvalidDataException($"Siding projection changes branch participation in main event at node {mainSection[i].Index} in the common prefix or suffix.");
                }
                
                if (!physicalSplits.Add(mainSplit.Node.JunctionIndex))
                    throw new InvalidDataException($"Repeated physical split junction {mainSplit.Node.JunctionIndex} across separate passing branches.");
                
                if (!TestFacingPoint(mainSplit.Node.JunctionIndex, mainSplit.Outgoing) || !TestFacingPoint(sideSplit.Node.JunctionIndex, sideSplit.Outgoing) ||
                    TestFacingPoint(mainEnd.Node.JunctionIndex, mainEnd.Outgoing) || mainSplit.Node.Type != TrainPathNodeType.Other || mainEnd.Node.Type != TrainPathNodeType.Other)
                    throw new InvalidDataException($"Unsupported passing switch orientation or boundary event at node {siding[0].Index}.");

                // A single alternative must not cross the main route between its split and rejoin,
                // or visit a switch twice. Such a graph needs more than one runtime alternative.
                HashSet<int> mainSwitches = mainSection.Where(node => node.JunctionIndex >= 0)
                    .Select(node => node.JunctionIndex).ToHashSet();
                HashSet<int> sideSwitches = new HashSet<int>();

                for (int i = 0; i < sideJunctions.Count; i++)
                {
                    int junction = sideJunctions[i].Node.JunctionIndex;
                    if (!sideSwitches.Add(junction) || (i > split && i < sideRejoin && mainSwitches.Contains(junction)))
                        throw new InvalidDataException($"Cyclic or multiply rejoining physical siding at node {siding[0].Index}.");
                }

                AIPathNode first = siding[sideSplit.Position + 1];
                AIPathNode last = siding[sideEnd.Position - 1];
                siding[0].NextSidingNode = null;
                siding[0].NextSidingTVNIndex = -1;
                mainSplit.Node.NextSidingNode = first == sideEnd.Node ? mainEnd.Node : first;
                mainSplit.Node.NextSidingTVNIndex = sideSplit.Outgoing;
                if (last != sideSplit.Node)
                    last.NextSidingNode = mainEnd.Node;
                mainSplit.Node.Type = TrainPathNodeType.SidingStart;
                mainEnd.Node.Type = TrainPathNodeType.SidingEnd;
            }

            // Remove only detached, event-free duplicate prefix/suffix nodes, retaining the main
            // chain and its wait/reverse nodes verbatim. Keep collection indices usable by consumers.
            HashSet<AIPathNode> mainNodes = new HashSet<AIPathNode>(main);
            HashSet<AIPathNode> retained = new HashSet<AIPathNode>(mainNodes);
            foreach (AIPathNode node in main)
            {
                for (AIPathNode branch = node.NextSidingNode; branch != null && !mainNodes.Contains(branch); branch = branch.NextSidingNode)
                {
                    if (!retained.Add(branch))
                        throw new InvalidDataException($"Projected siding cycle at node {branch.Index}.");
                }
            }

            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                if (!retained.Contains(Nodes[i]))
                    Nodes.RemoveAt(i);
            }

            for (int i = 0; i < Nodes.Count; i++)
                Nodes[i].Index = i;

            // A production self-check: projection must leave a complete, acyclic, rejoining graph.
            ReadSidings(main);
        }

        private static List<(AIPathNode Node, int Position, int Incoming, int Outgoing)> ReadJunctionTraversal(List<AIPathNode> traversal, bool siding, int incoming)
        {
            List<(AIPathNode Node, int Position, int Incoming, int Outgoing)> junctions = new List<(AIPathNode, int, int, int)>();

            for (int i = 0; i < traversal.Count; i++)
            {
                AIPathNode node = traversal[i];
                int outgoing = siding && i < traversal.Count - 1 ? node.NextSidingTVNIndex : node.NextMainTVNIndex;
            
                if (node.JunctionIndex >= 0)
                    junctions.Add((node, i, incoming, outgoing));
                incoming = outgoing;
            }
            return junctions;
        }

        /// <summary>
        /// returns true if the specified vector node is at the facing point end of
        /// the specified juction node, else false.
        /// </summary>
        private static bool TestFacingPoint(int junctionIndex, int vectorIndex)
        {
            if (junctionIndex < 0 || vectorIndex < 0)
                return false;
            TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;

            if (trackDatabase.TrackNodes[junctionIndex] is not JunctionNode)
                return false;

            TrackNodeConnectorIndex connectors = trackDatabase.TrackNodeConnectors[junctionIndex];
            int stemPin = connectors.InboundCount == 1 ? 0 : connectors.InboundCount;
            return connectors.TrackNodeConnectors[stemPin].Link != vectorIndex;
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
        private readonly int trackNodeIndexHint = -1;
        private (int VectorIndex, TrackDirection Direction)? incomingTrack;

        internal (int VectorIndex, TrackDirection Direction)? IncomingTrack => incomingTrack;

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

            trackNodeIndexHint = pathNode.NodeIndex;
            WaitTimeS = pathNode.WaitInfo?.WaitTime ?? 0;
            Location = pathNode.Location;
            if (!double.IsFinite(Location.Location.X) || !double.IsFinite(Location.Location.Y) || !double.IsFinite(Location.Location.Z))
                throw new InvalidDataException("Path node has non-finite coordinates.");

            if (pathNode.NodeType.Includes(PathNodeType.Junction))
            {
                TrackDatabase database = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
                JunctionIndex = trackNodeIndexHint > 0 && trackNodeIndexHint < database.TrackNodes.Length &&
                    database.TrackNodes[trackNodeIndexHint] is JunctionNode junction && IsNearby(Location, junction.Location)
                    ? trackNodeIndexHint : FindJunctionOrEndIndex(Location, true);
            }

            switch (pathNode.NodeType)
            {
                case PathNodeType _ when pathNode.NodeType.Includes(PathNodeType.Reversal):
                    Type = TrainPathNodeType.Reverse;
                    break;
                case PathNodeType _ when pathNode.NodeType.Includes(PathNodeType.Wait):
                    Type = TrainPathNodeType.Stop;
                    break;
                case PathNodeType _ when pathNode.NodeType.Includes(PathNodeType.Invalid) && timetableMode:
                    Type = TrainPathNodeType.Invalid;
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
            trackNodeIndexHint = otherNode.trackNodeIndexHint;
            incomingTrack = otherNode.incomingTrack;
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
            TrackDatabase database = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
            int candidate = -1, preferred = -1, count = 0, preferredCount = 0;
            TrackDirection candidateDirection = TrackDirection.Ahead, preferredDirection = TrackDirection.Ahead;
            double closest = double.PositiveInfinity;
            TrackDirection? arrivalDirection = incomingTrack is { } arrival && arrival.VectorIndex == previousNextMainTVNIndex
                ? arrival.Direction : null;
            foreach (VectorNode vector in database.TrackNodes.OfType<VectorNode>())
            {
                if (!HasValidConnectors(database, vector.NodeIndex))
                    continue;
                TrackTraveller? here = JunctionIndex < 0
                    ? TrackTraveller.InitializeTraveller(Location, vector, TrackDirection.Ahead, TrackDataBaseType.Rail) : null;
                TrackTraveller? there = nextNode.JunctionIndex < 0
                    ? TrackTraveller.InitializeTraveller(nextNode.Location, vector, TrackDirection.Ahead, TrackDataBaseType.Rail) : null;
                if ((JunctionIndex < 0 && !here.HasValue) || (nextNode.JunctionIndex < 0 && !there.HasValue))
                    continue;
                double distance = ProjectionDistanceSquared(here) + nextNode.ProjectionDistanceSquared(there);
                TrackNodeConnectorIndex connectors = database.TrackNodeConnectors[vector.NodeIndex];
                for (int startEnd = 0; startEnd < 2; startEnd++)
                {
                    TrackDirection direction = startEnd == 0 ? TrackDirection.Ahead : TrackDirection.Reverse;
                    if ((JunctionIndex >= 0 && connectors.TrackNodeConnectors[startEnd].Link != JunctionIndex) ||
                        (nextNode.JunctionIndex >= 0 && connectors.TrackNodeConnectors[1 - startEnd].Link != nextNode.JunctionIndex))
                        continue;
                    if (here.HasValue && there.HasValue && (direction == TrackDirection.Ahead
                        ? there.Value.VectorNodeOffset < here.Value.VectorNodeOffset
                        : there.Value.VectorNodeOffset > here.Value.VectorNodeOffset))
                        continue;
                    if (previousNextMainTVNIndex >= 0)
                    {
                        if (JunctionIndex < 0 && (vector.NodeIndex != previousNextMainTVNIndex ||
                            (arrivalDirection.HasValue && direction != (Type == TrainPathNodeType.Reverse
                                ? arrivalDirection.Value.Reverse() : arrivalDirection.Value))))
                            continue;
                        if (JunctionIndex >= 0 && !CanTraverseJunction(database, JunctionIndex, previousNextMainTVNIndex,
                            arrivalDirection, vector.NodeIndex, direction))
                            continue;
                    }
                    // Compare squared projection errors, ignoring only sub-millimetre floating-point noise.
                    if (distance > closest + 1e-8)
                        continue;
                    if (distance < closest - 1e-8)
                    {
                        closest = distance;
                        count = preferredCount = 0;
                    }
                    candidate = vector.NodeIndex;
                    candidateDirection = direction;
                    count++;
                    if ((JunctionIndex < 0 && vector.NodeIndex == trackNodeIndexHint) ||
                        (nextNode.JunctionIndex < 0 && vector.NodeIndex == nextNode.trackNodeIndexHint))
                    {
                        preferred = vector.NodeIndex;
                        preferredDirection = direction;
                        preferredCount++;
                    }
                }
            }
            int result = preferredCount == 1 ? preferred : preferredCount == 0 && count == 1 ? candidate : -1;
            if (result >= 0)
            {
                TrackDirection direction = preferredCount == 1 ? preferredDirection : candidateDirection;
                nextNode.incomingTrack = (result, direction);
                if (JunctionIndex >= 0)
                {
                    TrackNodeConnectorIndex connectors = database.TrackNodeConnectors[JunctionIndex];
                    int stemPin = connectors.InboundCount == 1 ? 0 : connectors.InboundCount;
                    IsFacingPoint = FindPin(database, JunctionIndex, result, direction.Reverse()) != stemPin;
                }
            }
            return result;
        }

        private double ProjectionDistanceSquared(TrackTraveller? projection)
        {
            return !projection.HasValue ? 0 : Location.Location.Y == 0
                ? WorldLocation.GetDistanceSquared2D(Location, projection.Value.Location)
                : WorldLocation.GetDistanceSquared(Location, projection.Value.Location);
        }

        private static bool HasValidConnectors(TrackDatabase database, int vectorIndex)
        {
            if (vectorIndex <= 0 || vectorIndex >= database.TrackNodeConnectors.Length ||
                database.TrackNodeConnectors[vectorIndex] is not TrackNodeConnectorIndex connectors ||
                connectors.TrackNodeConnectors.IsDefault || connectors.TrackNodeConnectors.Length != 2)
                return false;
            for (int i = 0; i < connectors.TrackNodeConnectors.Length; i++)
            {
                TrackNodeConnector pin = connectors.TrackNodeConnectors[i];
                if (pin == null || pin.Link <= 0 || pin.Link >= database.TrackNodes.Length ||
                    database.TrackNodes[pin.Link] is not (JunctionNode or EndNode) ||
                    FindPin(database, pin.Link, vectorIndex, i == 0 ? TrackDirection.Reverse : TrackDirection.Ahead) < 0)
                    return false;
                TrackNodeConnectorIndex endpoint = database.TrackNodeConnectors[pin.Link];
                if (database.TrackNodes[pin.Link] is JunctionNode &&
                    (endpoint.TrackNodeConnectors.Length != 3 || endpoint.InboundCount is not (1 or 2)))
                    return false;
            }
            return true;
        }

        private static int FindPin(TrackDatabase database, int nodeIndex, int link, TrackDirection? direction)
        {
            if (nodeIndex < 0 || nodeIndex >= database.TrackNodeConnectors.Length ||
                database.TrackNodeConnectors[nodeIndex] is not TrackNodeConnectorIndex connectors || connectors.TrackNodeConnectors.IsDefault)
                return -1;
            int result = -1;
            for (int i = 0; i < connectors.TrackNodeConnectors.Length; i++)
            {
                if (connectors.TrackNodeConnectors[i]?.Link != link ||
                    (direction.HasValue && connectors.TrackNodeConnectors[i].Direction != direction.Value))
                    continue;
                if (result >= 0)
                    return -1;
                result = i;
            }
            return result;
        }

        private static bool CanTraverseJunction(TrackDatabase database, int junctionIndex, int incoming,
            TrackDirection? incomingDirection, int outgoing, TrackDirection outgoingDirection)
        {
            int inPin = FindPin(database, junctionIndex, incoming, incomingDirection);
            int outPin = FindPin(database, junctionIndex, outgoing, outgoingDirection.Reverse());
            int inboundCount = database.TrackNodeConnectors[junctionIndex].InboundCount;
            return inPin >= 0 && outPin >= 0 && (inPin < inboundCount) != (outPin < inboundCount);
        }

        private static bool IsNearby(in WorldLocation location, in WorldLocation other)
        {
            double distance = location.Location.Y == 0
                ? WorldLocation.GetDistanceSquared2D(location, other) : WorldLocation.GetDistanceSquared(location, other);
            return distance < WorldLocation.ProximityTolerance * WorldLocation.ProximityTolerance;
        }

        /// <summary>
        /// Find a unique junctionNode or endNode within the track matching tolerance.
        /// </summary>
        /// <param name="location">Location for which we want to find the node</param>
        /// <param name="wantJunctionNode">true if a junctionNode is wanted, false for a endNode</param>
        /// <returns>The unique matching track node index, or -1 if missing or ambiguous.</returns>
        public static int FindJunctionOrEndIndex(in WorldLocation location, bool wantJunctionNode)
        {
            TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
            int result = -1;
            for (int j = 0; j < trackDatabase.TrackNodes.Length; j++)
            {
                TrackNodeBase tn = trackDatabase.TrackNodes[j];
                if (tn == null)
                    continue;
                if (wantJunctionNode && tn is not JunctionNode)
                    continue;
                if (!wantJunctionNode && tn is not EndNode)
                    continue;
                if (!IsNearby(location, tn.Location))
                    continue;
                if (result >= 0)
                    return -1;
                result = j;
            }
            return result;
        }
    }
}
