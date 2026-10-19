using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Simulation.AIs;

namespace Tests.FreeTrainSimulator.Simulator
{
    /// <summary>Exercises generated native path graphs through public runtime APIs.</summary>
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class AIPathTests
    {
        public TestContext TestContext { get; set; }

        private static readonly bool[] sourceArray = new bool[] { false, true };

        /// <summary>Parallel routes retain a geometric witness rather than only their shared switches.</summary>
        [TestMethod]
        [DataRow(4)]
        public async Task GenerateSelectedPathWhenParallelVectorThenRetainsInteriorWitness(int selectedVector)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateSelectedPath(selectedVector, TestContext.CancellationToken).ConfigureAwait(false);
            TestContext.WriteLine(string.Join("; ", path.PathNodes.Select((node, index) =>
                $"{index}: {node.NodeType} track={node.NodeIndex} location={node.Location} main={node.NextMainNode} siding={node.NextSidingNode}")));

            Assert.HasCount(1, path.PathNodes.Where(node => node.NodeIndex == selectedVector &&
                node.Location != SyntheticPathFixture.Location(0, 0) &&
                node.Location != SyntheticPathFixture.Location(100, 0)).ToArray());
        }

        /// <summary>Generated switch-bound branches retain their distinct physical vector edges.</summary>
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task ConstructWhenGeneratedPassingGraphThenKeepsDistinctRoutes(bool vectorBound, bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GeneratePassingPath(vectorBound, TestContext.CancellationToken).ConfigureAwait(false);

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes);
            AIPathNode split = runtime.Nodes.Single(node => node.Type == TrainPathNodeType.SidingStart);
            AIPathNode rejoin = runtime.Nodes.Single(node => node.Type == TrainPathNodeType.SidingEnd);
            Assert.AreEqual((3, 6, 4, 5, 2), (split.JunctionIndex, rejoin.JunctionIndex, split.NextMainTVNIndex, split.NextSidingTVNIndex, rejoin.NextMainTVNIndex));
        }

        /// <summary>The public bridge uses the selected vector on both sides of its interior witness.</summary>
        [TestMethod]
        [DataRow(4)]
        public async Task ConstructWhenSelectedParallelRouteThenPreservesExactMainVectorEdges(int selectedVector)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateSelectedPath(selectedVector, TestContext.CancellationToken).ConfigureAwait(false);

            AIPath runtime = new AIPath(path, false);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreSequenceEqual(new[] { 1, selectedVector, selectedVector, 2, -1 }, Walk(runtime.FirstNode, node => node.NextMainNode).Select(node => node.NextMainTVNIndex).ToArray());
        }

        /// <summary>Vector-bound authoring projects only transient branch links to physical switches.</summary>
        [TestMethod]
        public async Task ConstructWhenVectorBoundPassingGraphThenProjectsCompleteRejoiningGraph()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GeneratePassingPath(true, TestContext.CancellationToken).ConfigureAwait(false);

            AIPath runtime = new AIPath(path, false);

            Assert.IsNotNull(runtime.Nodes);
            AIPathNode[] main = Walk(runtime.FirstNode, node => node.NextMainNode).ToArray();
            AIPathNode split = main.Single(node => node.Type == TrainPathNodeType.SidingStart);
            AIPathNode rejoin = main.Single(node => node.Type == TrainPathNodeType.SidingEnd);
            AIPathNode[] branch = Walk(split.NextSidingNode, node => node.NextSidingNode).ToArray();
            Assert.IsNull(runtime.FirstNode.NextSidingNode);
            Assert.AreSame(rejoin, branch[^1]);
            Assert.AreSequenceEqual(Enumerable.Repeat(5, branch.Length - 1).Append(-1).ToArray(), branch.Select(node => node.NextSidingTVNIndex).ToArray());
            Assert.AreSequenceEqual(Enumerable.Range(0, runtime.Nodes.Count).ToArray(), main.Concat(branch).Distinct().OrderBy(node => node.Index).Select(node => node.Index).ToArray());
        }

        /// <summary>Graph edges, not the position of branch nodes in storage, determine the route.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenPassingStorageReorderedThenPreservesProjectedGraph(bool vectorBound)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GeneratePassingPath(vectorBound, TestContext.CancellationToken).ConfigureAwait(false);
            int[] order = Enumerable.Range(1, path.PathNodes.Length - 1).Reverse().Prepend(0).ToArray();
            Dictionary<int, int> indexes = order.Select((oldIndex, newIndex) => (oldIndex, newIndex))
                .ToDictionary(item => item.oldIndex, item => item.newIndex);
            indexes.Add(-1, -1);
            PathModel reordered = new PathModel(path)
            {
                PathNodes = order.Select(index => path.PathNodes[index] with
                {
                    NextMainNode = indexes[path.PathNodes[index].NextMainNode],
                    NextSidingNode = indexes[path.PathNodes[index].NextSidingNode],
                }).ToImmutableArray(),
            };

            AIPath original = new AIPath(path, false);
            AIPath runtime = new AIPath(reordered, false);

            Assert.IsNotNull(original.Nodes);
            Assert.IsNotNull(runtime.Nodes);
            Assert.AreSequenceEqual(GraphSignature(original), GraphSignature(runtime));
        }

        /// <summary>Projection must never rewrite the persisted generated path graph.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenGeneratedPassingGraphThenPreservesSourceBytes(bool vectorBound)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GeneratePassingPath(vectorBound, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] before = MemoryPackSerializer.Serialize(path);

            AIPath runtime = new AIPath(path, false);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Generated witnesses do not shift any authored locations or rewrite the source model.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GeneratePassingGraphThenPreservesAuthoredAnchorsAndSourceBytes(bool vectorBound)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = SyntheticPathFixture.CreatePassingSource(vectorBound);
            byte[] before = MemoryPackSerializer.Serialize(source);
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, TrackWorld.Instance, TestContext.CancellationToken);

            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(source, resolution, TrackWorld.Instance, PathRouteResolverOptions.Default);

            Assert.IsTrue(generated.Success, generated.Message);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(source));
            Assert.AreSequenceEqual(source.PathNodes.Select(node => node.Location).ToArray(), source.PathNodes
                .Select(node => generated.PathModel.PathNodes.Single(candidate => candidate.Location == node.Location).Location).ToArray());
        }

        /// <summary>Absent, wrong-type, stale, and out-of-range index hints cannot override generated geometry.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(5)]
        [DataRow(999)]
        public async Task ConstructWhenStaleNodeIndexThenUsesGeometricWitness(int staleIndex)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel generated = await fixture.GenerateSelectedPath(4, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel stale = new PathModel(generated)
            {
                PathNodes = generated.PathNodes.Select(node => node with { NodeIndex = staleIndex }).ToImmutableArray(),
            };

            AIPath runtime = new AIPath(stale, false);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreSequenceEqual(new[] { 1, 4, 4, 2, -1 },
                Walk(runtime.FirstNode, node => node.NextMainNode).Select(node => node.NextMainTVNIndex).ToArray());
        }

        /// <summary>Legacy approach points select the closest geometry, not every endpoint within tolerance.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(4)]
        [DataRow(5)]
        public async Task ConstructWhenNearSwitchApproachThenUsesClosestVector(int hint)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "NearSwitchApproach",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, hint, -.8f, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, hint, -.4f, 0, -1, -1)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            AIPath runtime = new AIPath(path, false);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreEqual(1, runtime.FirstNode.NextMainTVNIndex);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Equal parallel geometry stays ambiguous unless a valid track hint selects it.</summary>
        [TestMethod]
        [DataRow(0, false)]
        [DataRow(4, true)]
        [DataRow(5, true)]
        public async Task ConstructWhenIndistinguishableParallelGeometryThenRequiresValidHint(int hint, bool accepted)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = fixture.Track.TrackDatabase;
            VectorNode main = (VectorNode)database.TrackNodes[4];
            await InitializeTrackAsync(fixture, database with
            {
                TrackNodes = database.TrackNodes.SetItem(5, main with { NodeIndex = 5 }),
            }).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "CoincidentParallel",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, hint, 20, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, hint, 80, 0, -1, -1)),
            };

            AIPath runtime = new AIPath(path, false);

            if (accepted)
            {
                Assert.IsNotNull(runtime.Nodes);
                Assert.AreEqual(hint, runtime.FirstNode.NextMainTVNIndex);
            }
            else
            {
                Assert.IsNull(runtime.Nodes);
                Assert.IsNull(runtime.FirstNode);
            }
        }

        /// <summary>A three-pin switch can own both ends of a legal vector; interior travel needs no switch exit.</summary>
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task ConstructWhenSameJunctionLoopThenPreservesInteriorTravelAndReversal(bool reverse, bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            await InitializeTrackAsync(fixture, CreateLoopDatabase(fixture)).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "LoopInterior",
                PathNodes = reverse
                    ? ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 0, 50, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 0, 100, 50, 2, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 0, 25, 0, -1, -1))
                    : ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 0, 50, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 0, 100, 50, -1, -1)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreSequenceEqual(reverse ? new[] { 4, 4, -1 } : new[] { 4, -1 },
                Walk(runtime.FirstNode, node => node.NextMainNode).Select(node => node.NextMainTVNIndex).ToArray());
            if (reverse)
                Assert.AreEqual(TrainPathNodeType.Reverse, runtime.Nodes[1].Type);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Loop exits use the actual arrival endpoint, including an explicit change of direction.</summary>
        [TestMethod]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public async Task ConstructWhenLoopReachesJunctionThenBindsDirectionalPins(bool reverse, bool accepted)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            await InitializeTrackAsync(fixture, CreateLoopDatabase(fixture)).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "LoopExit",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 0, 50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | (reverse ? PathNodeType.Reversal : 0), 0, 100, 50, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 1, -50, 0, -1, -1)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            AIPath runtime = new AIPath(path, false);

            if (accepted)
            {
                Assert.IsNotNull(runtime.Nodes);
                Assert.AreSequenceEqual(new[] { 4, 4, 1, -1 },
                    Walk(runtime.FirstNode, node => node.NextMainNode).Select(node => node.NextMainTVNIndex).ToArray());
                Assert.IsTrue(runtime.Nodes[2].IsFacingPoint);
            }
            else
            {
                Assert.IsNull(runtime.Nodes, "Reversing reaches the branch end, which cannot exit to the other branch.");
                Assert.IsNull(runtime.FirstNode);
            }
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Two loop endpoints on the same switch side must not select an arbitrary exit.</summary>
        [TestMethod]
        public async Task ConstructWhenLoopHasAmbiguousDepartureThenRefuses()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = CreateLoopDatabase(fixture);
            TrackNodeConnectorIndex junction = database.TrackNodeConnectors[3];
            await InitializeTrackAsync(fixture, database with
            {
                TrackNodeConnectors = database.TrackNodeConnectors.SetItem(3, junction with
                {
                    TrackNodeConnectors = ImmutableArray.Create(junction.TrackNodeConnectors[1] with { ConnectorType = ConnectorType.InPin },
                        junction.TrackNodeConnectors[0] with { ConnectorType = ConnectorType.OutPin }, junction.TrackNodeConnectors[2]),
                }),
            }).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "AmbiguousLoopExit",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 4, 100, 50, -1, -1)),
            };

            AIPath runtime = new AIPath(path, false);

            Assert.IsNull(runtime.Nodes);
            Assert.IsNull(runtime.FirstNode);
        }

        /// <summary>The circuit consumer cannot represent separate alternatives keyed by the same physical split.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenSeparateBranchesSharePhysicalSplitThenRefusesWithDiagnostic(bool duplicateBranch)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            ImmutableArray<PathNode> nodes = ImmutableArray.Create(
                SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, 13),
                SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 4, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 2, 150, 0, 5, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 6, -1),
                SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 7, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 8, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 1, -50, 0, 9, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 10, duplicateBranch ? 14 : -1),
                SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 11, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 12, -1),
                SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 3));
            if (duplicateBranch)
                nodes = nodes.Add(SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 11));
            PathModel path = new PathModel { Id = "RepeatedPhysicalSplit", PathNodes = nodes };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, false);

            if (duplicateBranch)
            {
                Assert.IsNull(runtime.Nodes);
                Assert.IsNull(runtime.FirstNode);
                Assert.Contains("physical split junction 3", diagnostic);
            }
            else
            {
                Assert.IsNotNull(runtime.Nodes, diagnostic);
                Assert.HasCount(1, runtime.Nodes.Where(node => node.Type == TrainPathNodeType.SidingStart).ToArray());
            }
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>A siding cannot inherit a main-only reversal at a terminal or continuing vector rejoin.</summary>
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task ConstructWhenSidingRejoinArrivalDirectionDiffersThenRefusesWithDiagnostic(bool terminal, bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            ImmutableArray<PathNode> nodes = ImmutableArray.Create(
                SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, 6),
                SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 4, -1),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 2, 170, 0, 5, -1),
                SyntheticPathFixture.Node(terminal ? PathNodeType.End : PathNodeType.Via, 2, 130, 0, terminal ? -1 : 8, -1),
                SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 7),
                SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 5));
            PathModel path = new PathModel
            {
                Id = "IncompatibleRejoinArrival",
                PathNodes = terminal ? nodes : nodes.Add(SyntheticPathFixture.Node(PathNodeType.End, 2, 110, 0, -1, -1)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, timetableMode);

            Assert.IsNull(runtime.Nodes);
            Assert.IsNull(runtime.FirstNode);
            Assert.Contains("Incompatible siding arrival vector or direction at rejoin node 5", diagnostic);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Matching terminal arrivals still project to the physical rejoin without changing source bytes.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenTerminalSidingArrivalMatchesThenPreservesProjectedRejoin(bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "CompatibleTerminalRejoin",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, 5),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 4, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 130, 0, -1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 6),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 4)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes, diagnostic);
            Assert.AreEqual(6, runtime.Nodes.Single(node => node.Type == TrainPathNodeType.SidingEnd).JunctionIndex);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Moving the rejoin upstream must not make a branch execute a previously bypassed main wait.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenCommonSuffixBypassesMainWaitThenRefusesWithDiagnostic(bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "BypassedSuffixWait",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, 6),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 4, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Wait, 2, 120, 0, 5, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = 30 } },
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 7),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 5)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, timetableMode);

            Assert.IsNull(runtime.Nodes);
            Assert.IsNull(runtime.FirstNode);
            Assert.Contains("changes branch participation in main event at node 4", diagnostic);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Moving the split downstream must not make a branch execute a previously bypassed main wait.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenCommonPrefixBypassesMainWaitThenRefusesWithDiagnostic(bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "BypassedPrefixWait",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -80, 0, 1, 6),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Wait, 1, -60, 0, 2, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = 30 } },
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 4, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 5, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 1, -40, 0, -1, 7),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, -1, 8),
                    SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 9),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 5)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, timetableMode);

            Assert.IsNull(runtime.Nodes);
            Assert.IsNull(runtime.FirstNode);
            Assert.Contains("changes branch participation in main event at node 1", diagnostic);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>Waits and vias cannot separate two reversals that the circuit consumer batches on one vector.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Wait, 30, false)]
        [DataRow(PathNodeType.Wait, 30, true)]
        [DataRow(PathNodeType.Via, 0, false)]
        [DataRow(PathNodeType.Via, 0, true)]
        public async Task ConstructWhenContinuousVectorHasMultipleReversalsThenRefusesWithDiagnostic(PathNodeType between, int waitTime, bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "RepeatedVectorReversal",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 4, 70, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | between, 4, 60, 0, 4, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = waitTime } },
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 4, 30, 0, 5, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 6, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, timetableMode);

            Assert.IsNull(runtime.Nodes);
            Assert.IsNull(runtime.FirstNode);
            Assert.Contains("Multiple reversals in one continuous vector occurrence at node 4", diagnostic);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>First-node events must not be silently skipped by the circuit consumer.</summary>
        [TestMethod]
        [DataRow(PathNodeType.Wait, false)]
        [DataRow(PathNodeType.Wait, true)]
        [DataRow(PathNodeType.Reversal, false)]
        [DataRow(PathNodeType.Reversal, true)]
        public async Task ConstructWhenFirstMainNodeHasEventThenRefusesWithDiagnostic(PathNodeType eventType, bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "FirstEvent",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start | eventType, 1, -80, 0, 1, -1) with
                    { WaitInfo = eventType == PathNodeType.Wait ? new PathNodeWaitInfo { WaitTime = 30 } : null },
                    SyntheticPathFixture.Node(PathNodeType.End, 1, -20, 0, -1, -1)),
            };
            byte[] before = MemoryPackSerializer.Serialize(path);

            (AIPath runtime, string diagnostic) = ConstructWithDiagnostics(path, timetableMode);

            Assert.IsNull(runtime.Nodes);
            Assert.IsNull(runtime.FirstNode);
            Assert.Contains("first main node", diagnostic);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path));
        }

        /// <summary>A native main-line wait retains its location and duration in either runtime mode.</summary>
        [TestMethod]
        [DataRow(false, 0)]
        [DataRow(false, 30)]
        [DataRow(true, 0)]
        [DataRow(true, 30)]
        public async Task ConstructWhenMainWaitRoundTripsThenPreservesEventAsync(bool timetableMode, int waitTime)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = new PathModel
            {
                Id = "Wait" + Guid.NewGuid().ToString("N"),
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -80, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Wait, 1, -60, 0, 2, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = waitTime } },
                    SyntheticPathFixture.Node(PathNodeType.End, 1, -20, 0, -1, -1)),
            };
            PathModel path = await fixture.RoundTripPathAsync(source, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(MemoryPackSerializer.Serialize(source.PathNodes), MemoryPackSerializer.Serialize(path.PathNodes));

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreEqual((TrainPathNodeType.Stop, waitTime, 1, SyntheticPathFixture.Location(-60, 0)),
                (runtime.Nodes[1].Type, runtime.Nodes[1].WaitTimeS, runtime.Nodes[1].NextMainTVNIndex, runtime.Nodes[1].Location));
        }

        /// <summary>A native terminal reversal retains the incoming vector needed for its offset.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenTerminalReversalRoundTripsThenKeepsIncomingVectorAsync(bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = new PathModel
            {
                Id = "TerminalReversal" + Guid.NewGuid().ToString("N"),
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -80, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End | PathNodeType.Reversal, 1, -20, 0, -1, -1)),
            };
            PathModel path = await fixture.RoundTripPathAsync(source, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(MemoryPackSerializer.Serialize(source.PathNodes), MemoryPackSerializer.Serialize(path.PathNodes));

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreEqual((TrainPathNodeType.Reverse, 1, SyntheticPathFixture.Location(-20, 0)),
                (runtime.Nodes[1].Type, runtime.Nodes[1].NextMainTVNIndex, runtime.Nodes[1].Location));
        }

        /// <summary>A native terminal wait retains its incoming vector, duration, and terminal links.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenTerminalWaitRoundTripsThenKeepsIncomingVectorAsync(bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = new PathModel
            {
                Id = "TerminalWait" + Guid.NewGuid().ToString("N"),
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -80, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End | PathNodeType.Wait, 1, -20, 0, -1, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = 30 } }),
            };
            PathModel path = await fixture.RoundTripPathAsync(source, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(MemoryPackSerializer.Serialize(source.PathNodes), MemoryPackSerializer.Serialize(path.PathNodes));

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreEqual((TrainPathNodeType.Stop, 30, 1, SyntheticPathFixture.Location(-20, 0)),
                (runtime.Nodes[1].Type, runtime.Nodes[1].WaitTimeS, runtime.Nodes[1].NextMainTVNIndex, runtime.Nodes[1].Location));
            Assert.IsNull(runtime.Nodes[1].NextMainNode);
        }

        /// <summary>An interior native reversal preserves the event and its outgoing return traversal.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConstructWhenInteriorReversalRoundTripsThenKeepsReturnTraversalAsync(bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = new PathModel
            {
                Id = "InteriorReversal" + Guid.NewGuid().ToString("N"),
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -80, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 1, -20, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 1, -70, 0, -1, -1)),
            };
            PathModel path = await fixture.RoundTripPathAsync(source, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(MemoryPackSerializer.Serialize(source.PathNodes), MemoryPackSerializer.Serialize(path.PathNodes));

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNotNull(runtime.Nodes);
            Assert.AreEqual((TrainPathNodeType.Reverse, 1, SyntheticPathFixture.Location(-20, 0)),
                (runtime.Nodes[1].Type, runtime.Nodes[1].NextMainTVNIndex, runtime.Nodes[1].Location));
            Assert.AreSame(runtime.Nodes[2], runtime.Nodes[1].NextMainNode);
            Assert.AreSequenceEqual(new[] { 1, 1, -1 }, Walk(runtime.FirstNode, node => node.NextMainNode)
                .Select(node => node.NextMainTVNIndex).ToArray());
        }

        /// <summary>Malformed or unsupported graphs are refused atomically without changing source bytes.</summary>
        [TestMethod]
        [DynamicData(nameof(GetRefusedPaths))]
        public async Task ConstructWhenUnsupportedGraphThenRefusesWithoutMutation(string reason, PathModel path, bool timetableMode)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] before = MemoryPackSerializer.Serialize(path);

            AIPath runtime = new AIPath(path, timetableMode);

            Assert.IsNull(runtime.Nodes, reason);
            Assert.IsNull(runtime.FirstNode, reason);
            Assert.AreSequenceEqual(before, MemoryPackSerializer.Serialize(path), reason);
        }

        /// <summary>Supplies independent malformed graphs in both runtime modes.</summary>
        public static IEnumerable<object[]> GetRefusedPaths()
        {
            return RefusalCases().SelectMany(item => sourceArray.Select(timetableMode =>
                new object[] { item.Reason, new PathModel { Id = item.Reason, PathNodes = item.Nodes }, timetableMode }));
        }

        private static IEnumerable<(string Reason, ImmutableArray<PathNode> Nodes)> RefusalCases()
        {
            ImmutableArray<PathNode> nodes = ImmutableArray.Create(
                SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                SyntheticPathFixture.Node(PathNodeType.Junction | PathNodeType.Via, 3, 0, 0, 2, 5),
                SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                SyntheticPathFixture.Node(PathNodeType.Junction | PathNodeType.Via, 6, 100, 0, 4, -1),
                SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 3));
            yield return ("Empty", ImmutableArray<PathNode>.Empty);
            yield return ("Single", ImmutableArray.Create(nodes[0] with { NextMainNode = -1 }));
            yield return ("NullNode", nodes.SetItem(2, null));
            yield return ("InvalidNode", nodes.SetItem(2, nodes[2] with { NodeType = PathNodeType.Invalid }));
            yield return ("LinkBeyondStorage", nodes.SetItem(2, nodes[2] with { NextMainNode = nodes.Length }));
            yield return ("NegativeLink", nodes.SetItem(2, nodes[2] with { NextMainNode = -2 }));
            yield return ("MainCycle", nodes.SetItem(4, nodes[4] with { NextMainNode = 1 }));
            yield return ("Orphan", nodes.Add(SyntheticPathFixture.Node(PathNodeType.Via, 1, -70, 0, -1, -1)));
            yield return ("MissingRejoin", nodes.SetItem(5, nodes[5] with { NextSidingNode = -1 }));
            yield return ("BackwardRejoin", nodes.SetItem(5, nodes[5] with { NextSidingNode = 0 }));
            yield return ("SidingCycle", nodes.SetItem(5, nodes[5] with { NextSidingNode = 5 }));
            yield return ("SidingMainLink", nodes.SetItem(5, nodes[5] with { NextMainNode = 3 }));
            yield return ("OverlappingSiding", nodes.SetItem(0, nodes[0] with { NextSidingNode = 5 }));
            yield return ("SidingWait", nodes.SetItem(5, nodes[5] with { NodeType = PathNodeType.Via | PathNodeType.Wait }));
            yield return ("SidingReverse", nodes.SetItem(5, nodes[5] with { NodeType = PathNodeType.Via | PathNodeType.Reversal }));
            yield return ("SidingWaitInfo", nodes.SetItem(5, nodes[5] with { WaitInfo = new PathNodeWaitInfo { WaitTime = 30 } }));
            yield return ("CombinedEvent", nodes.SetItem(2, nodes[2] with { NodeType = PathNodeType.Via | PathNodeType.Wait | PathNodeType.Reversal }));
            yield return ("JunctionWait", nodes.SetItem(1, nodes[1] with { NodeType = PathNodeType.Junction | PathNodeType.Wait }));
            yield return ("NonFinite", nodes.SetItem(2, SyntheticPathFixture.Node(PathNodeType.Via, 4, float.NaN, 0, 3, -1)));
            yield return ("Disconnected", nodes.SetItem(2, SyntheticPathFixture.Node(PathNodeType.Via, 4, 500, 500, 3, -1)));
            yield return ("ParallelWithoutWitness", ImmutableArray.Create(nodes[0], nodes[1] with { NextMainNode = 2, NextSidingNode = -1 },
                nodes[3] with { NextMainNode = 3 }, nodes[4]));
        }

        private async Task InitializeTrackAsync(SyntheticPathFixture fixture, TrackDatabase database)
        {
            await TrackModelHandler.Create(fixture.Track with { TrackDatabase = database }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await RuntimeDataResolver.Initialize(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
        }

        private static TrackDatabase CreateLoopDatabase(SyntheticPathFixture fixture)
        {
            TrackDatabase database = fixture.Track.TrackDatabase;
            VectorNode original = (VectorNode)database.TrackNodes[4];
            VectorSectionNode Section(float x, float z, float endX, float endZ, float heading)
                => new VectorSectionNode(SyntheticPathFixture.Location(x, z), original.WorldTile, new Vector3(0, heading, 0),
                    SyntheticPathFixture.Location(endX, endZ)) { NodeIndex = 1 };
            VectorNode loop = new VectorNode(original.Location, original.WorldTile, original.Location)
            {
                NodeIndex = 4,
                VectorSections = ImmutableArray.Create(Section(0, 0, 100, 0, MathHelper.PiOver2),
                    Section(100, 0, 100, 100, 0), Section(100, 100, 0, 100, -MathHelper.PiOver2),
                    Section(0, 100, 0, 0, MathHelper.Pi)),
            };
            // Stem = loop finish; branches = approach and loop start. All three pins are physical.
            return database with
            {
                TrackNodes = database.TrackNodes.SetItem(4, loop),
                TrackNodeConnectors = database.TrackNodeConnectors
                    .SetItem(3, database.TrackNodeConnectors[3] with
                    {
                        TrackNodeConnectors = ImmutableArray.Create(
                            new TrackNodeConnector { Link = 4, Direction = TrackDirection.Ahead, ConnectorType = ConnectorType.InPin },
                            new TrackNodeConnector { Link = 1, Direction = TrackDirection.Ahead, ConnectorType = ConnectorType.OutPin },
                            new TrackNodeConnector { Link = 4, Direction = TrackDirection.Reverse, ConnectorType = ConnectorType.OutPin }),
                    })
                    .SetItem(4, database.TrackNodeConnectors[4] with
                    {
                        TrackNodeConnectors = ImmutableArray.Create(
                            new TrackNodeConnector { Link = 3, Direction = TrackDirection.Reverse, ConnectorType = ConnectorType.InPin },
                            new TrackNodeConnector { Link = 3, Direction = TrackDirection.Ahead, ConnectorType = ConnectorType.OutPin }),
                    }),
            };
        }

        private static (AIPath Runtime, string Diagnostic) ConstructWithDiagnostics(PathModel path, bool timetableMode)
        {
            using StringWriter messages = new StringWriter();
            using TextWriterTraceListener listener = new TextWriterTraceListener(messages);
            Trace.Listeners.Add(listener);
            try
            {
                AIPath runtime = new AIPath(path, timetableMode);
                listener.Flush();
                return (runtime, messages.ToString());
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }

        private static IEnumerable<AIPathNode> Walk(AIPathNode first, Func<AIPathNode, AIPathNode> next)
        {
            HashSet<AIPathNode> visited = new HashSet<AIPathNode>();
            for (AIPathNode node = first; node != null && visited.Add(node); node = next(node))
                yield return node;
        }

        private static (TrainPathNodeType Type, int Junction, int MainVector, int SidingVector, string Location)[] GraphSignature(AIPath path)
        {
            AIPathNode[] main = Walk(path.FirstNode, node => node.NextMainNode).ToArray();
            AIPathNode split = main.Single(node => node.Type == TrainPathNodeType.SidingStart);
            return main.Concat(Walk(split.NextSidingNode, node => node.NextSidingNode))
                .Select(node => (node.Type, node.JunctionIndex, node.NextMainTVNIndex, node.NextSidingTVNIndex, node.Location.ToString())).ToArray();
        }
    }
}
