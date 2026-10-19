using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.State;
using FreeTrainSimulator.Models.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Simulation.AIs;

using Simulation = Orts.Simulation.Simulator;

namespace Tests.FreeTrainSimulator.Simulator
{
    /// <summary>Tests the generated native path bridge through public explore startup and the train save-state API.</summary>
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class AIPathStartupTests
    {
        public TestContext TestContext { get; set; }

        private static readonly int[] expectedArray134628 = new int[] { 1, 3, 4, 6, 2, 8 };
        private static readonly int[] expectedArray34628 = new int[] { 3, 4, 6, 2, 8 };
        private static readonly string[] expectedStrings = new string[] { "1:Reverse, 3:Reverse, 4:Reverse", "4:Ahead, 3:Ahead, 1:Ahead, 7:Ahead" };
        private static readonly int[] exptectedArray1346 = new int[] { 1, 3, 4, 6 };
        private static readonly int[] expectedArray346 = new int[] { 3, 4, 6 };
        private static readonly int[] expectedArray356 = new int[] { 3, 5, 6 };

        /// <summary>A selected geometry-backed route survives generation, persistence, and real rolling-stock startup.</summary>
        [TestMethod]
        [DataRow(4)]
        [DataRow(5)]
        public async Task StartExploreWhenGeneratedMainPathThenSnapshotContainsExactCircuitRoute(int selectedVector)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateSelectedPath(selectedVector, TestContext.CancellationToken).ConfigureAwait(false);
            simulator.SetExplore(path, fixture.ConsistFile, TimeSpan.FromHours(12), SeasonType.Summer, WeatherType.Clear);

            simulator.Start(TestContext.CancellationToken);

            Assert.IsNotNull(simulator.OriginalPlayerTrain);
            Assert.HasCount(1, simulator.OriginalPlayerTrain.Cars, "The generated engine must load, not merely an empty train.");
            TrainSaveState snapshot = await simulator.OriginalPlayerTrain.Snapshot().ConfigureAwait(false);
            Assert.IsNotNull(snapshot.RoutePathSaveState);
            Assert.HasCount(1, snapshot.RoutePathSaveState.RoutePaths);
            TrackCircuitRouteElementSaveState[] route = snapshot.RoutePathSaveState.RoutePaths[0].RouteElements.ToArray();
            Assert.AreSequenceEqual(new[] { 1, 3, selectedVector, 6, 2, 8 }, route.Select(element => element.TrackCircuitSectionIndex).ToArray());
            Assert.AreSequenceEqual(new[] { TrackDirection.Reverse, TrackDirection.Reverse, TrackDirection.Reverse,
                TrackDirection.Ahead, TrackDirection.Reverse, TrackDirection.Reverse }, route.Select(element => element.Direction).ToArray());
            Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
        }

        /// <summary>Coincident vectors retain the explicitly selected first-edge vector and heading through real startup.</summary>
        [TestMethod]
        [DataRow(4, false)]
        [DataRow(4, true)]
        [DataRow(5, false)]
        [DataRow(5, true)]
        public async Task StartExploreWhenCoincidentVectorsThenRetainsSelectedInitialBindingAsync(int selectedVector, bool opposite)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = fixture.Track.TrackDatabase;
            VectorNode main = (VectorNode)database.TrackNodes[4];
            TrackModel track = fixture.Track with
            {
                TrackDatabase = database with { TrackNodes = database.TrackNodes.SetItem(5, main with { NodeIndex = 5 }) },
            };
            Simulation simulator = await fixture.CreateSimulator(track, false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "CoincidentStartup",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, selectedVector, opposite ? 80 : 20, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, selectedVector, opposite ? 20 : 80, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            Assert.AreEqual(selectedVector, simulator.OriginalPlayerTrain.RearTrackTraveller.TrackNodeIndex);
            Assert.AreEqual(opposite ? TrackDirection.Reverse : TrackDirection.Ahead, simulator.OriginalPlayerTrain.RearTrackTraveller.Direction);
            Assert.AreEqual(opposite ? -MathHelper.PiOver2 : MathHelper.PiOver2, simulator.OriginalPlayerTrain.RearTrackTraveller.Heading, .0001f);
            Assert.HasCount(1, snapshot.RoutePathSaveState.RoutePaths);
            Assert.AreEqual($"{selectedVector}:{(opposite ? TrackDirection.Ahead : TrackDirection.Reverse)}",
                RouteSignature(snapshot.RoutePathSaveState.RoutePaths[0]));
            Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
        }

        /// <summary>A reversal on coincident geometry retains its selected vector and directional offset.</summary>
        [TestMethod]
        [DataRow(4, false)]
        [DataRow(4, true)]
        [DataRow(5, false)]
        [DataRow(5, true)]
        public async Task StartExploreWhenCoincidentVectorReversalThenRetainsResolvedOffsetAsync(int selectedVector, bool opposite)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = fixture.Track.TrackDatabase;
            VectorNode main = (VectorNode)database.TrackNodes[4];
            TrackModel track = fixture.Track with
            {
                TrackDatabase = database with { TrackNodes = database.TrackNodes.SetItem(5, main with { NodeIndex = 5 }) },
            };
            Simulation simulator = await fixture.CreateSimulator(track, false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "CoincidentReversalStartup",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, selectedVector, opposite ? 80 : 20, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Reversal, selectedVector, opposite ? 20 : 80, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, selectedVector, opposite ? 60 : 40, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            Assert.AreEqual(selectedVector, simulator.OriginalPlayerTrain.RearTrackTraveller.TrackNodeIndex);
            Assert.AreSequenceEqual(new[]
            {
                $"{selectedVector}:{(opposite ? TrackDirection.Ahead : TrackDirection.Reverse)}",
                $"{selectedVector}:{(opposite ? TrackDirection.Reverse : TrackDirection.Ahead)}",
            }, snapshot.RoutePathSaveState.RoutePaths.Select(RouteSignature).ToArray());
            TrackCircuitReversalInfoSaveState reversal = snapshot.RoutePathSaveState.ReversalInfoSaveStates[0];
            Assert.IsTrue(reversal.Valid);
            Assert.AreEqual(selectedVector, reversal.ReversalSectionIndex);
            Assert.AreEqual(80f, reversal.ReverseReversalOffset, .0001f);
        }

        /// <summary>Both authored boundary forms yield a distinct circuit alternative after real startup.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StartExploreWhenGeneratedPassingPathThenSnapshotContainsAlternativeRouteAsync(bool vectorBound)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GeneratePassingPath(vectorBound, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            AssertMainRoute(snapshot, expectedArray134628);
            AssertAlternativeRoute(snapshot);
        }

        /// <summary>Location-based passing paths are stored as deadlock routes, not train alternatives.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StartExploreWhenGeneratedLocationPassingPathThenSnapshotContainsDeadlockRoutesAsync(bool vectorBound)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(true, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GeneratePassingPath(vectorBound, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            AssertMainRoute(snapshot, expectedArray134628);
            await AssertLocationPassingRoutesAsync(simulator, snapshot).ConfigureAwait(false);
        }

        /// <summary>A physical split at node zero must not lose its siding-start event in the consumer.</summary>
        [TestMethod]
        public async Task StartExploreWhenPhysicalSplitIsFirstNodeThenSnapshotContainsAlternativeRouteAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateSplitStartPassingPathAsync(TestContext.CancellationToken).ConfigureAwait(false);
            AssertPhysicalSplitStart(path);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            AssertMainRoute(snapshot, expectedArray34628);
            AssertAlternativeRoute(snapshot);
        }

        /// <summary>The first-node split also remains usable in location-based passing mode.</summary>
        [TestMethod]
        public async Task StartExploreWhenPhysicalSplitIsFirstNodeThenSnapshotContainsDeadlockRoutesAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(true, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateSplitStartPassingPathAsync(TestContext.CancellationToken).ConfigureAwait(false);
            AssertPhysicalSplitStart(path);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            AssertMainRoute(snapshot, expectedArray34628);
            await AssertLocationPassingRoutesAsync(simulator, snapshot).ConfigureAwait(false);
        }

        /// <summary>Ordinary approach anchors do not duplicate a vector; an interior wait keeps its circuit-relative offset in either orientation.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StartExploreWhenApproachHasOrdinaryPrefixThenWaitAndExactRouteSurviveAsync(bool opposite)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "PrefixWait",
                PathNodes = opposite
                    ? ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 2, 160, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 2, 140, 0, 2, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 2, 120, 0, 3, -1),
                        SyntheticPathFixture.Node(PathNodeType.Junction, 6, 100, 0, 4, -1),
                        SyntheticPathFixture.Node(PathNodeType.Wait, 4, 30, 0, 5, -1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = 37 } },
                        SyntheticPathFixture.Node(PathNodeType.Via, 4, 10, 0, 6, -1),
                        SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 7, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 1, -50, 0, -1, -1))
                    : ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -60, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 1, -40, 0, 2, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 1, -20, 0, 3, -1),
                        SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 4, -1),
                        SyntheticPathFixture.Node(PathNodeType.Wait, 4, 30, 0, 5, -1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = 37 } },
                        SyntheticPathFixture.Node(PathNodeType.Via, 4, 70, 0, 6, -1),
                        SyntheticPathFixture.Node(PathNodeType.Junction, 6, 100, 0, 7, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            Assert.HasCount(1, snapshot.RoutePathSaveState.RoutePaths);
            Assert.AreEqual(opposite ? "2:Ahead, 6:Reverse, 4:Ahead, 3:Ahead, 1:Ahead, 7:Ahead"
                : "1:Reverse, 3:Reverse, 4:Reverse, 6:Ahead, 2:Reverse, 8:Reverse", RouteSignature(snapshot.RoutePathSaveState.RoutePaths[0]));
            Assert.HasCount(1, snapshot.RoutePathSaveState.Waitpoints);
            int[] wait = snapshot.RoutePathSaveState.Waitpoints.Single();
            Assert.AreSequenceEqual(new[] { 0, 4, 37, 0, -1, opposite ? 70 : 30 }, wait);
        }

        /// <summary>Skipping ordinary prefix nodes must not remove an interior reversal or the wait after it.</summary>
        [TestMethod]
        public async Task StartExploreWhenInteriorReversalThenSnapshotRetainsBothDirectionsAndWaitAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "ReversalWait",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -60, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 1, -20, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Reversal, 4, 70, 0, 4, -1),
                    SyntheticPathFixture.Node(PathNodeType.Wait, 4, 30, 0, 5, -1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = 23 } },
                    SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 6, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 1, -50, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            Assert.AreSequenceEqual(expectedStrings, snapshot.RoutePathSaveState.RoutePaths.Select(RouteSignature).ToArray());
            Assert.IsTrue(snapshot.RoutePathSaveState.ReversalInfoSaveStates[0].Valid);
            Assert.HasCount(1, snapshot.RoutePathSaveState.Waitpoints);
            Assert.AreSequenceEqual(new[] { 1, 4, 23, 0, -1, 70 }, snapshot.RoutePathSaveState.Waitpoints.Single());
        }

        /// <summary>A junction endpoint includes the approach and junction, never a fictitious departure vector.</summary>
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task StartExploreWhenTerminalJunctionThenRouteEndsAtJunctionAsync(bool passing, bool locationPassingPaths)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(locationPassingPaths, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "TerminalJunction",
                PathNodes = passing
                    ? ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 2, 4),
                        SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                        SyntheticPathFixture.Node(PathNodeType.End | PathNodeType.Junction, 6, 100, 0, -1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 3))
                    : ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 2, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                        SyntheticPathFixture.Node(PathNodeType.End | PathNodeType.Junction, 6, 100, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            AssertMainRoute(snapshot, exptectedArray1346);
            TrackCircuitReversalInfoSaveState end = snapshot.RoutePathSaveState.ReversalInfoSaveStates.Single();
            Assert.AreEqual(6, end.ReversalSectionIndex);
            Assert.AreEqual(0f, end.ReverseReversalOffset);
            if (passing && locationPassingPaths)
                await AssertLocationPassingRoutesAsync(simulator, snapshot).ConfigureAwait(false);
            else if (passing)
                AssertAlternativeRoute(snapshot);
            else
                Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
        }

        /// <summary>Real sequential binding replaces, rather than appends, the circuit index domain.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StartExploreTwiceThenCircuitIndexesRemainExactAsync(bool locationPassingPaths)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            for (int start = 0; start < 2; start++)
            {
                Simulation simulator = await fixture.CreateSimulator(locationPassingPaths, TestContext.CancellationToken).ConfigureAwait(false);
                PathModel path = await fixture.GeneratePassingPath(false, TestContext.CancellationToken).ConfigureAwait(false);

                TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

                AssertMainRoute(snapshot, expectedArray134628);
                SignalEnvironmentSaveState signals = await simulator.SignalEnvironment.Snapshot().ConfigureAwait(false);
                Assert.AreEqual(9, signals.TrackCircuitSectionsCount, $"Startup {start + 1} must bind exactly one circuit domain.");
                Assert.AreSequenceEqual(Enumerable.Range(0, 9).ToArray(), signals.TrackCircuitSections.Select(section => section.Index).ToArray());
                if (locationPassingPaths)
                    await AssertLocationPassingRoutesAsync(simulator, snapshot).ConfigureAwait(false);
                else
                    AssertAlternativeRoute(snapshot);
            }
        }

        private async Task<TrainSaveState> StartExploreAndSnapshotAsync(SyntheticPathFixture fixture, Simulation simulator, PathModel path)
        {
            TestContext.WriteLine("Persisted graph: {0}", string.Join("; ", path.PathNodes.Select((node, index) =>
                $"{index}: {node.NodeType} track={node.NodeIndex} location={node.Location} main={node.NextMainNode} siding={node.NextSidingNode}")));
            AIPath bridge = new AIPath(path, false);
            Assert.IsNotNull(bridge.Nodes, "The persisted fixture must form an accepted public bridge before startup.");
            TestContext.WriteLine("Bridge graph: {0}", string.Join("; ", bridge.Nodes.Select(node =>
                $"{node.Index}: {node.Type} junction={node.JunctionIndex} location={node.Location} main={node.NextMainNode?.Index} mainVector={node.NextMainTVNIndex} siding={node.NextSidingNode?.Index} sidingVector={node.NextSidingTVNIndex}")));
            simulator.SetExplore(path, fixture.ConsistFile, TimeSpan.FromHours(12), SeasonType.Summer, WeatherType.Clear);

            simulator.Start(TestContext.CancellationToken);

            Assert.IsNotNull(simulator.OriginalPlayerTrain);
            Assert.HasCount(1, simulator.OriginalPlayerTrain.Cars, "The generated engine must load, not merely an empty train.");
            TrainSaveState snapshot = await simulator.OriginalPlayerTrain.Snapshot().ConfigureAwait(false);
            Assert.IsNotNull(snapshot.RoutePathSaveState);
            TestContext.WriteLine("Main routes: {0}", string.Join(" | ", snapshot.RoutePathSaveState.RoutePaths.Select(RouteSignature)));
            TestContext.WriteLine("Alternative routes: {0}", string.Join(" | ", snapshot.RoutePathSaveState.AlternativePaths.Select(RouteSignature)));
            SignalEnvironmentSaveState signals = await simulator.SignalEnvironment.Snapshot().ConfigureAwait(false);
            TestContext.WriteLine("Location passing: {0}; deadlock routes: {1}", signals.LocationPassingPathsEnabled,
                string.Join(" | ", signals.DeadlockDetails.Values.SelectMany(deadlock => deadlock.AvailablePaths)
                    .Select(available => RouteSignature(available.PathInfo))));
            return snapshot;
        }

        private static void AssertPhysicalSplitStart(PathModel path)
        {
            Assert.AreEqual((3, SyntheticPathFixture.Location(0, 0)), (path.PathNodes[0].NodeIndex, path.PathNodes[0].Location));
            AIPath bridge = new AIPath(path, false);
            Assert.IsNotNull(bridge.Nodes);
            Assert.AreEqual((0, TrainPathNodeType.SidingStart, 3, 4, 5), (bridge.FirstNode.Index, bridge.FirstNode.Type,
                bridge.FirstNode.JunctionIndex, bridge.FirstNode.NextMainTVNIndex, bridge.FirstNode.NextSidingTVNIndex));
        }

        private static void AssertMainRoute(TrainSaveState snapshot, int[] sections)
        {
            Assert.HasCount(1, snapshot.RoutePathSaveState.RoutePaths);
            TrackCircuitRouteElementSaveState[] route = snapshot.RoutePathSaveState.RoutePaths[0].RouteElements.ToArray();
            Assert.AreSequenceEqual(sections, route.Select(element => element.TrackCircuitSectionIndex).ToArray());
            Assert.AreSequenceEqual(sections.Select(section => section == 6 ? TrackDirection.Ahead : TrackDirection.Reverse).ToArray(),
                route.Select(element => element.Direction).ToArray());
        }

        private static void AssertAlternativeRoute(TrainSaveState snapshot)
        {
            Assert.HasCount(1, snapshot.RoutePathSaveState.AlternativePaths);
            TrackCircuitRouteElementSaveState[] route = snapshot.RoutePathSaveState.AlternativePaths[0].RouteElements.ToArray();
            Assert.AreSequenceEqual(expectedArray356, route.Select(element => element.TrackCircuitSectionIndex).ToArray());
            Assert.AreSequenceEqual(new[] { TrackDirection.Reverse, TrackDirection.Reverse, TrackDirection.Ahead },
                route.Select(element => element.Direction).ToArray());
            TrackCircuitRouteElementSaveState split = snapshot.RoutePathSaveState.RoutePaths[0].RouteElements.Single(element => element.TrackCircuitSectionIndex == 3);
            TrackCircuitRouteElementSaveState rejoin = snapshot.RoutePathSaveState.RoutePaths[0].RouteElements.Single(element => element.TrackCircuitSectionIndex == 6);
            Assert.IsNotNull(split.AlternativePathStart);
            Assert.IsNotNull(rejoin.AlternativePathEnd);
            Assert.AreEqual((0, 6, 0, 3), (split.AlternativePathStart.PathIndex, split.AlternativePathStart.TrackCircuitSectionIndex,
                rejoin.AlternativePathEnd.PathIndex, rejoin.AlternativePathEnd.TrackCircuitSectionIndex));
        }

        private static async Task AssertLocationPassingRoutesAsync(Simulation simulator, TrainSaveState snapshot)
        {
            Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
            SignalEnvironmentSaveState signals = await simulator.SignalEnvironment.Snapshot().ConfigureAwait(false);
            Assert.IsTrue(signals.LocationPassingPathsEnabled);
            Assert.HasCount(1, signals.DeadlockDetails.Values);
            DeadlockInfoSaveState deadlock = signals.DeadlockDetails.Values.Single();
            Assert.HasCount(1, deadlock.AvailablePaths.Where(available => available.PathInfo.RouteElements.Select(element => element.TrackCircuitSectionIndex).SequenceEqual(expectedArray346)).ToArray());
            Assert.HasCount(1, deadlock.AvailablePaths.Where(available => available.PathInfo.RouteElements.Select(element => element.TrackCircuitSectionIndex).SequenceEqual(expectedArray356)).ToArray());

            int trainSubpathIndex = deadlock.TrainSubpathIndex[snapshot.TrainNumber][snapshot.RoutePathSaveState.ActivePath];
            Assert.HasCount(2, deadlock.TrainReferences[trainSubpathIndex]);
            Assert.AreSequenceEqual(deadlock.PathReferences[3].Order().ToArray(), deadlock.TrainReferences[trainSubpathIndex].Order().ToArray());
            Assert.AreSequenceEqual(deadlock.TrainReferences[trainSubpathIndex].Order().ToArray(), deadlock.TrainLengthFit[trainSubpathIndex].Keys.Order().ToArray());
            Assert.AreSequenceEqual(expectedArray346, deadlock.AvailablePaths[deadlock.TrainOwnPath[trainSubpathIndex]].PathInfo.RouteElements.Select(element => element.TrackCircuitSectionIndex).ToArray());
        }

        private static string RouteSignature(TrackCircuitPartialPathRouteSaveState route)
            => string.Join(", ", route.RouteElements.Select(element => $"{element.TrackCircuitSectionIndex}:{element.Direction}"));
    }
}
