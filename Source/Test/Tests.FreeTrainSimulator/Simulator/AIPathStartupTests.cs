using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.State;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Simulation.AIs;

using Tests.FreeTrainSimulator.Toolbox.PathEditing;

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
        private static readonly string[] expectedArray3R5R6A8R10R11A = new[] { "3:Reverse, 5:Reverse, 6:Ahead", "8:Reverse, 10:Reverse, 11:Ahead" };
        private static readonly string[] sourceArray = new[]
            {
                "11:Reverse, 9:Ahead, 8:Ahead", "3:Reverse, 4:Reverse, 6:Ahead", "3:Reverse, 5:Reverse, 6:Ahead",
                "6:Reverse, 4:Ahead, 3:Ahead", "8:Reverse, 10:Reverse, 11:Ahead", "8:Reverse, 9:Reverse, 11:Ahead",
            };

        /// <summary>Bounded Toolbox reversal retains its selected track through guarded save, reload, and public startup.</summary>
        [TestMethod]
        [DataRow(4)]
        [DataRow(5)]
        public async Task WhenWholePathIsReversedSavedAndReloadedThenSimulatorStartsOnExactOppositeSelectedTrackAsync(int selectedVector)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = await fixture.GenerateSelectedPath(selectedVector, TestContext.CancellationToken).ConfigureAwait(false);
            using (PathEditor editor = new PathEditor(new ReversePathEditorContext(TrackWorld.Instance)))
            {
                _ = await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false);

                PathEditorCommandResult result = editor.ReversePathCommand();
                Assert.IsTrue(result.Success, result.Message);
                PathModel reversed = editor.TryCaptureCurrentPathModel();
                PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(reversed, fixture.Route, TrackWorld.Instance,
                    TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
                PathModel reloaded = await PathModelHandler.GetExtended(saved.PathModel.Id, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);

                TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, reloaded).ConfigureAwait(false);

                Assert.AreEqual((source.Id, source.Name, source.End, source.Start, 1, 0, 0),
                    (reloaded.Id, reloaded.Name, reloaded.Start, reloaded.End, snapshot.RoutePathSaveState.RoutePaths.Count,
                        snapshot.RoutePathSaveState.AlternativePaths.Count, snapshot.RoutePathSaveState.Waitpoints.Count));
                Assert.AreEqual($"2:Ahead, 6:Reverse, {selectedVector}:Ahead, 3:Ahead, 1:Ahead, 7:Ahead", RouteSignature(snapshot.RoutePathSaveState.RoutePaths.Single()));
            }
        }

        /// <summary>Matching native paths compose without changing either saved source or selected physical route.</summary>
        [TestMethod]
        [DataRow(4, false)]
        [DataRow(5, false)]
        [DataRow(4, true)]
        [DataRow(5, true)]
        public async Task WhenMatchingNativePathsAreComposedThenSaveAsAndStartupPreserveBothSourcesAsync(int selectedVector, bool vectorSeam)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            float seamX = vectorSeam ? 50 : 0;
            float seamZ = vectorSeam && selectedVector == 5 ? 100 : 0;
            int seamTrack = vectorSeam ? selectedVector : 3;
            PathNodeType seamFlag = vectorSeam ? PathNodeType.None : PathNodeType.Junction;
            PathModel current = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "CompositionCurrent" + Guid.NewGuid().ToString("N"), Name = "First leg", Start = "Origin", End = "Join", PlayerPath = true,
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End | seamFlag, seamTrack, seamX, seamZ, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel extension = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "CompositionExtension" + Guid.NewGuid().ToString("N"), Name = "Second leg", Start = "Join", End = "Destination",
                PathNodes = vectorSeam
                    ? ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, seamTrack, seamX, seamZ, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1))
                    : ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start | PathNodeType.Junction, 3, 0, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, selectedVector, 50, selectedVector == 5 ? 100 : 0, 2, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);
            string currentFile = ModelFileResolver<PathModelHeader>.FilePath(current) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            string extensionFile = ModelFileResolver<PathModelHeader>.FilePath(extension) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] currentBytes = await File.ReadAllBytesAsync(currentFile, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] extensionBytes = await File.ReadAllBytesAsync(extensionFile, TestContext.CancellationToken).ConfigureAwait(false);
            ImmutableArray<PathNode> currentNodes = current.PathNodes;
            ImmutableArray<PathNode> extensionNodes = extension.PathNodes;

            PathEditResult composed = PathModelEditor.AppendPathAtMatchingEndpoint(current, extension, TrackWorld.Instance, TestContext.CancellationToken);

            Assert.IsTrue(composed.Success, composed.Message);
            Assert.AreEqual((current.Id, current.Name, current.Start, extension.End, current.PlayerPath),
                (composed.PathModel.Id, composed.PathModel.Name, composed.PathModel.Start, composed.PathModel.End, composed.PathModel.PlayerPath));
            Assert.AreEqual(currentNodes, current.PathNodes);
            Assert.AreEqual(extensionNodes, extension.PathNodes);
            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(composed.PathModel with
            {
                Id = "ComposedCopy" + Guid.NewGuid().ToString("N"),
            }, fixture.Route, TrackWorld.Instance, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
            PathModel reloaded = await fixture.Route.PathModelUncached(saved.PathModel.Id, TestContext.CancellationToken).ConfigureAwait(false);
            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, reloaded).ConfigureAwait(false);

            Assert.AreEqual($"1:Reverse, 3:Reverse, {selectedVector}:Reverse, 6:Ahead, 2:Reverse, 8:Reverse", RouteSignature(snapshot.RoutePathSaveState.RoutePaths.Single()));
            Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
            Assert.AreEqual((current.Name, current.Start, extension.End, true), (reloaded.Name, reloaded.Start, reloaded.End, reloaded.PlayerPath));
            Assert.AreSequenceEqual(currentBytes, await File.ReadAllBytesAsync(currentFile, TestContext.CancellationToken).ConfigureAwait(false));
            Assert.AreSequenceEqual(extensionBytes, await File.ReadAllBytesAsync(extensionFile, TestContext.CancellationToken).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task WhenMatchingJunctionJoinRequiresAnIllegalExitThenCompositionPreservesBothInputsAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel current = new PathModel
            {
                Id = "current",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 4, 50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End | PathNodeType.Junction, 6, 100, 0, -1, -1)),
            };
            PathModel extension = new PathModel
            {
                Id = "extension",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start | PathNodeType.Junction, 6, 100, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 5, 50, 100, -1, -1)),
            };
            Assert.IsTrue(PathRouteResolver.Resolve(current, TrackWorld.Instance, TestContext.CancellationToken).IsValid);
            Assert.IsTrue(PathRouteResolver.Resolve(extension, TrackWorld.Instance, TestContext.CancellationToken).IsValid);
            ImmutableArray<PathNode> originalExtension = extension.PathNodes;

            PathEditResult result = PathModelEditor.AppendPathAtMatchingEndpoint(current, extension, TrackWorld.Instance, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreSame(current, result.PathModel);
            Assert.AreEqual(originalExtension, extension.PathNodes);
        }

        /// <summary>Double reversal retains the original selected route through normal save and public startup.</summary>
        [TestMethod]
        [DataRow(4)]
        [DataRow(5)]
        public async Task WhenWholePathIsReversedTwiceThenSavedStartupRetainsOriginalSelectedTrackAsync(int selectedVector)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = await fixture.GenerateSelectedPath(selectedVector, TestContext.CancellationToken).ConfigureAwait(false);
            using (PathEditor editor = new PathEditor(new ReversePathEditorContext(TrackWorld.Instance)))
            {
                _ = await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsTrue(editor.ReversePathCommand().Success);
                Assert.IsTrue(editor.ReversePathCommand().Success);
                PathModel restored = editor.TryCaptureCurrentPathModel();
                Assert.AreSequenceEqual(source.PathNodes, restored.PathNodes);
                PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(restored, fixture.Route, TrackWorld.Instance,
                    TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
                PathModel reloaded = await PathModelHandler.GetExtended(saved.PathModel.Id, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);

                TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, reloaded).ConfigureAwait(false);

                Assert.AreEqual($"1:Reverse, 3:Reverse, {selectedVector}:Reverse, 6:Ahead, 2:Reverse, 8:Reverse", RouteSignature(snapshot.RoutePathSaveState.RoutePaths.Single()));
            }
        }

        /// <summary>A legal loop can start publicly while the bounded editor refuses its duplicate physical pins.</summary>
        [TestMethod]
        public async Task WhenLoopPathUsesDuplicateJunctionPinsThenReverseRefusesWithoutChangingPublicStartupAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = fixture.Track.TrackDatabase;
            VectorNode original = (VectorNode)database.TrackNodes[4];
            VectorSectionNode Section(float x, float z, float endX, float endZ, float heading)
                => new VectorSectionNode(SyntheticPathFixture.Location(x, z), original.WorldTile, new Vector3(0, heading, 0),
                    SyntheticPathFixture.Location(endX, endZ)) { NodeIndex = 1 };
            VectorNode loop = new VectorNode(original.Location, original.WorldTile, original.Location)
            {
                NodeIndex = 2,
                VectorSections = ImmutableArray.Create(Section(0, 0, 100, 0, MathHelper.PiOver2),
                    Section(100, 0, 100, 100, 0), Section(100, 100, 0, 100, -MathHelper.PiOver2),
                    Section(0, 100, 0, 0, MathHelper.Pi)),
            };
            TrackModel track = fixture.Track with
            {
                TrackDatabase = database with
                {
                    TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, database.TrackNodes[1], loop,
                        database.TrackNodes[3], database.TrackNodes[7] with { NodeIndex = 4 }),
                    TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                        database.TrackNodeConnectors[1] with
                        {
                            TrackNodeConnectors = database.TrackNodeConnectors[1].TrackNodeConnectors
                                .SetItem(0, database.TrackNodeConnectors[1].TrackNodeConnectors[0] with { Link = 4 }),
                        },
                        database.TrackNodeConnectors[4] with
                        {
                            NodeIndex = 2,
                            TrackNodeConnectors = ImmutableArray.Create(
                                new TrackNodeConnector { Link = 3, Direction = TrackDirection.Reverse, ConnectorType = ConnectorType.InPin },
                                new TrackNodeConnector { Link = 3, Direction = TrackDirection.Ahead, ConnectorType = ConnectorType.OutPin }),
                        },
                        database.TrackNodeConnectors[3] with
                        {
                            TrackNodeConnectors = ImmutableArray.Create(
                                new TrackNodeConnector { Link = 2, Direction = TrackDirection.Reverse, ConnectorType = ConnectorType.InPin },
                                new TrackNodeConnector { Link = 1, Direction = TrackDirection.Ahead, ConnectorType = ConnectorType.OutPin },
                                new TrackNodeConnector { Link = 2, Direction = TrackDirection.Ahead, ConnectorType = ConnectorType.OutPin }),
                        },
                        database.TrackNodeConnectors[7] with { NodeIndex = 4 }),
                },
            };
            Simulation simulator = await fixture.CreateSimulator(track, false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = new PathModel
            {
                Id = "LoopReverseStartup",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 100, 50, -1, -1)),
            };
            using PathEditor editor = new PathEditor(new ReversePathEditorContext(TrackWorld.Instance));
            _ = await editor.InitializePathAsync(path, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = editor.TryCaptureCurrentPathModel();

            PathEditorCommandResult result = editor.ReversePathCommand();

            Assert.AreEqual((true, false, false, true, false, false, false),
                (editor.ResolveCurrent(source).IsValid, editor.CanReversePath, result.Success,
                    ReferenceEquals(source, editor.TryCaptureCurrentPathModel()), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, source).ConfigureAwait(false);
            Assert.AreEqual("1:Reverse, 3:Reverse, 2:Reverse", RouteSignature(snapshot.RoutePathSaveState.RoutePaths.Single()));
        }

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

        /// <summary>Guarded Toolbox persistence retains a main wait and the passing route through native reload and real startup in either passing mode.</summary>
        [TestMethod]
        [DataRow(false, 1)]
        [DataRow(true, 0)]
        public async Task StartExploreWhenGuardedPassingPathIsReloadedThenPreservesMainWaitAndPassingRouteAsync(bool locationPassingPaths, int expectedAlternatives)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(locationPassingPaths, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = SyntheticPathFixture.CreatePassingSource(false);
            source = source with
            {
                PathNodes = source.PathNodes.SetItem(1, source.PathNodes[1] with { NextMainNode = source.PathNodes.Length })
                    .Add(SyntheticPathFixture.Node(PathNodeType.Wait, 4, 30, 0, 2, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = 37 } }),
            };

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, TrackWorld.Instance,
                TestContext.CancellationToken).ConfigureAwait(false);
            Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
            PathModel reloaded = await PathModelHandler.GetExtended(saved.PathModel.Id, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreEqual(PathValidationState.Valid, reloaded.ValidationState);
            PathNode wait = reloaded.PathNodes.Single(node => node.NodeType.Includes(PathNodeType.Wait));
            Assert.AreEqual((4, SyntheticPathFixture.Location(30, 0), 37), (wait.NodeIndex, wait.Location, wait.WaitInfo.WaitTime));

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, reloaded).ConfigureAwait(false);

            AssertMainRoute(snapshot, expectedArray134628);
            Assert.HasCount(1, snapshot.RoutePathSaveState.Waitpoints);
            Assert.AreSequenceEqual(new[] { 0, 4, 37, 0, -1, 30 }, snapshot.RoutePathSaveState.Waitpoints.Single());
            Assert.HasCount(expectedAlternatives, snapshot.RoutePathSaveState.AlternativePaths);
            SignalEnvironmentSaveState signals = await simulator.SignalEnvironment.Snapshot().ConfigureAwait(false);
            Assert.AreEqual(locationPassingPaths, signals.LocationPassingPathsEnabled);
            TrackCircuitPartialPathRouteSaveState passing = snapshot.RoutePathSaveState.AlternativePaths
                .Concat(signals.DeadlockDetails.Values.SelectMany(deadlock => deadlock.AvailablePaths).Select(available => available.PathInfo))
                .Single(route => route.RouteElements.Select(element => element.TrackCircuitSectionIndex).SequenceEqual(expectedArray356));
            Assert.AreEqual("3:Reverse, 5:Reverse, 6:Ahead", RouteSignature(passing));
        }

        /// <summary>Guarded Toolbox persistence retains a reversal and its following main wait through native reload and real startup in either passing mode.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task StartExploreWhenGuardedReversalPathIsReloadedThenPreservesDirectionsAndWaitAsync(bool locationPassingPaths)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            Simulation simulator = await fixture.CreateSimulator(locationPassingPaths, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = new PathModel
            {
                Id = "GuardedReversalWait",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -60, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 1, -20, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Reversal, 4, 70, 0, 4, -1),
                    SyntheticPathFixture.Node(PathNodeType.Wait, 4, 30, 0, 5, -1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = 23 } },
                    SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, 6, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 1, -50, 0, -1, -1)),
            };

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, TrackWorld.Instance,
                TestContext.CancellationToken).ConfigureAwait(false);
            Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
            PathModel reloaded = await PathModelHandler.GetExtended(saved.PathModel.Id, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreEqual(PathValidationState.Valid, reloaded.ValidationState);
            PathNode reversal = reloaded.PathNodes.Single(node => node.NodeType.Includes(PathNodeType.Reversal));
            Assert.AreEqual((4, SyntheticPathFixture.Location(70, 0)), (reversal.NodeIndex, reversal.Location));
            PathNode wait = reloaded.PathNodes.Single(node => node.NodeType.Includes(PathNodeType.Wait));
            Assert.AreEqual((4, SyntheticPathFixture.Location(30, 0), 23), (wait.NodeIndex, wait.Location, wait.WaitInfo.WaitTime));

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, reloaded).ConfigureAwait(false);

            Assert.AreSequenceEqual(expectedStrings, snapshot.RoutePathSaveState.RoutePaths.Select(RouteSignature).ToArray());
            TrackCircuitReversalInfoSaveState reversalInfo = snapshot.RoutePathSaveState.ReversalInfoSaveStates[0];
            Assert.IsTrue(reversalInfo.Valid);
            Assert.AreEqual((4, 70f), (reversalInfo.ReversalSectionIndex, reversalInfo.ReverseReversalOffset));
            Assert.HasCount(1, snapshot.RoutePathSaveState.Waitpoints);
            Assert.AreSequenceEqual(new[] { 1, 4, 23, 0, -1, 70 }, snapshot.RoutePathSaveState.Waitpoints.Single());
            Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
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

        /// <summary>EVID-B1 evidence: two main-attached passing bypasses, strictly separated on the global main
        /// route, resolved and generated directly (bypassing the editor's one-branch guard, exactly like the
        /// existing single-branch fixtures), then started through the public explore workflow in train-alternative
        /// mode. Establishes whether both branches actually survive resolution, generation, persistence, and
        /// startup, rather than inferring support from structural compatibility alone.</summary>
        [TestMethod]
        public async Task StartExploreWhenTwoSeparatedBranchesAreGeneratedThenSnapshotContainsBothAlternativeRoutesAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackModel track = fixture.Track with { TrackDatabase = SyntheticPathFixture.CreateTwoSeparatedBranchesDatabase() };
            Simulation simulator = await fixture.CreateSimulator(track, false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateTwoSeparatedBranchesPathAsync(TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            Assert.HasCount(1, snapshot.RoutePathSaveState.RoutePaths);
            Assert.AreEqual("2:Reverse, 3:Reverse, 4:Reverse, 6:Ahead, 7:Reverse, 8:Reverse, 9:Reverse, 11:Ahead, 12:Reverse, 13:Reverse",
                RouteSignature(snapshot.RoutePathSaveState.RoutePaths[0]));
            Assert.HasCount(2, snapshot.RoutePathSaveState.AlternativePaths);
            Assert.AreSequenceEqual(expectedArray3R5R6A8R10R11A, snapshot.RoutePathSaveState.AlternativePaths.Select(RouteSignature).ToArray());

            // Each branch's split/rejoin must anchor to its own, distinct alternative path index, with no cross-branch mixing.
            TrackCircuitRouteElementSaveState[] mainRoute = snapshot.RoutePathSaveState.RoutePaths[0].RouteElements.ToArray();
            TrackCircuitRouteElementSaveState split1 = mainRoute.Single(element => element.TrackCircuitSectionIndex == 3);
            TrackCircuitRouteElementSaveState rejoin1 = mainRoute.Single(element => element.TrackCircuitSectionIndex == 6);
            TrackCircuitRouteElementSaveState split2 = mainRoute.Single(element => element.TrackCircuitSectionIndex == 8);
            TrackCircuitRouteElementSaveState rejoin2 = mainRoute.Single(element => element.TrackCircuitSectionIndex == 11);
            Assert.IsNotNull(split1.AlternativePathStart);
            Assert.IsNotNull(rejoin1.AlternativePathEnd);
            Assert.IsNotNull(split2.AlternativePathStart);
            Assert.IsNotNull(rejoin2.AlternativePathEnd);
            Assert.AreEqual((0, 6, 0, 3), (split1.AlternativePathStart.PathIndex, split1.AlternativePathStart.TrackCircuitSectionIndex, rejoin1.AlternativePathEnd.PathIndex, rejoin1.AlternativePathEnd.TrackCircuitSectionIndex));
            Assert.AreEqual((1, 11, 1, 8), (split2.AlternativePathStart.PathIndex, split2.AlternativePathStart.TrackCircuitSectionIndex, rejoin2.AlternativePathEnd.PathIndex, rejoin2.AlternativePathEnd.TrackCircuitSectionIndex));
        }

        /// <summary>The same strictly-separated two-branch graph in location-based passing (deadlock) mode.</summary>
        [TestMethod]
        public async Task StartExploreWhenTwoSeparatedLocationPassingBranchesAreGeneratedThenSnapshotContainsBothDeadlockRoutesAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackModel track = fixture.Track with { TrackDatabase = SyntheticPathFixture.CreateTwoSeparatedBranchesDatabase() };
            Simulation simulator = await fixture.CreateSimulator(track, true, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel path = await fixture.GenerateTwoSeparatedBranchesPathAsync(TestContext.CancellationToken).ConfigureAwait(false);

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, path).ConfigureAwait(false);

            Assert.IsEmpty(snapshot.RoutePathSaveState.AlternativePaths);
            SignalEnvironmentSaveState signals = await simulator.SignalEnvironment.Snapshot().ConfigureAwait(false);
            Assert.IsTrue(signals.LocationPassingPathsEnabled);
            Assert.HasCount(2, signals.DeadlockDetails.Values);

            // Each independent deadlock zone must remain confined to its own branch's track circuits (no leakage).
            foreach (DeadlockInfoSaveState deadlock in signals.DeadlockDetails.Values)
            {
                int[] sections = deadlock.AvailablePaths.SelectMany(available => available.PathInfo.RouteElements
                    .Select(element => element.TrackCircuitSectionIndex)).Distinct().ToArray();
                bool branch1 = sections.All(section => section is 3 or 4 or 5 or 6);
                bool branch2 = sections.All(section => section is 8 or 9 or 10 or 11);
                Assert.IsTrue(branch1 || branch2, $"Deadlock zone sections [{string.Join(", ", sections)}] must not cross branches.");
            }
            string[] allDeadlockSignatures = signals.DeadlockDetails.Values.SelectMany(deadlock => deadlock.AvailablePaths) .Select(available => RouteSignature(available.PathInfo)).Order().ToArray();
            Assert.AreSequenceEqual(sourceArray.Order().ToArray(), allDeadlockSignatures);
        }

        /// <summary>Editor commands, not synthetic branch generation, deliver both owners to public startup in either passing mode.</summary>
        [TestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(false, true, false, false)]
        [DataRow(true, false, false, false)]
        [DataRow(true, true, false, false)]
        [DataRow(false, false, true, false)]
        [DataRow(false, true, true, false)]
        [DataRow(true, false, true, false)]
        [DataRow(true, true, true, false)]
        [DataRow(false, false, false, true)]
        [DataRow(false, true, false, true)]
        [DataRow(true, false, false, true)]
        [DataRow(true, true, false, true)]
        [DataRow(false, false, true, true)]
        [DataRow(false, true, true, true)]
        [DataRow(true, false, true, true)]
        [DataRow(true, true, true, true)]
        public async Task WhenTwoSeparatedBranchesAreEditorAuthoredThenNativeReloadAndPublicStartupPreserveBothOwnersAsync(
            bool firstGuided, bool secondGuided, bool reverseOrder, bool locationPassingPaths)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackModel track = fixture.Track with { TrackDatabase = SyntheticPathFixture.CreateTwoSeparatedBranchesDatabase() };
            Simulation simulator = await fixture.CreateSimulator(track, locationPassingPaths, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = await fixture.RoundTripPathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false);
            string sourceFile = ModelFileResolver<PathModelHeader>.FilePath(source) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] sourceBytes = await File.ReadAllBytesAsync(sourceFile, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel disk;
            using (SeparatedPassingFixture composition = await SeparatedPassingFixture.CreateAsync(TrackWorld.Instance, fixture.Sections).ConfigureAwait(false))
            {
                using (PathEditor editor = new PathEditor(composition.Content))
                {
                    Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));

                    SeparatedPassingFixture.AuthorBranch(editor, reverseOrder ? 8 : 3, reverseOrder ? 11 : 6, firstGuided);
                    SeparatedPassingFixture.AuthorBranch(editor, reverseOrder ? 3 : 8, reverseOrder ? 6 : 11, secondGuided);
                    PathModel authored = editor.TryCaptureCurrentPathModel();
                    PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(
                        authored with { Id = "EditorTwoOwners" + Guid.NewGuid().ToString("N") }, fixture.Route, composition.World,
                        TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
                    disk = await fixture.Route.PathModelUncached(saved.PathModel.Id, TestContext.CancellationToken).ConfigureAwait(false);
                    PathRouteResolution expected = editor.ResolveCurrent(authored);
                    PathRouteResolution reloaded = PathRouteResolver.Resolve(disk, composition.World, TestContext.CancellationToken);
                    Assert.IsTrue(reloaded.IsValid);
                    Assert.HasCount(2, reloaded.PassingRoutes);
                    Assert.IsTrue(expected.MainRoute.HasSamePhysicalTraversal(reloaded.MainRoute));
                    Assert.IsTrue(expected.PassingRoutes.All(owner => owner.HasSamePhysicalTraversal(reloaded.PassingRoutes.Single(route =>
                        disk.PathNodes[route.StartNodeIndex].NodeIndex == authored.PathNodes[owner.StartNodeIndex].NodeIndex))));
                    Assert.AreSequenceEqual(sourceBytes, await File.ReadAllBytesAsync(sourceFile, TestContext.CancellationToken).ConfigureAwait(false));
                }
            }

            TrainSaveState snapshot = await StartExploreAndSnapshotAsync(fixture, simulator, disk).ConfigureAwait(false);

            Assert.AreEqual("2:Reverse, 3:Reverse, 4:Reverse, 6:Ahead, 7:Reverse, 8:Reverse, 9:Reverse, 11:Ahead, 12:Reverse, 13:Reverse",
                RouteSignature(snapshot.RoutePathSaveState.RoutePaths.Single()));
            SignalEnvironmentSaveState signals = await simulator.SignalEnvironment.Snapshot().ConfigureAwait(false);
            Assert.AreEqual(locationPassingPaths, signals.LocationPassingPathsEnabled);
            string[] passing = snapshot.RoutePathSaveState.AlternativePaths
                .Concat(signals.DeadlockDetails.Values.SelectMany(deadlock => deadlock.AvailablePaths).Select(available => available.PathInfo))
                .Select(RouteSignature).Where(signature => expectedArray3R5R6A8R10R11A.Contains(signature)).Order().ToArray();
            Assert.AreSequenceEqual(expectedArray3R5R6A8R10R11A.Order().ToArray(), passing);
            Assert.AreEqual((locationPassingPaths ? 0 : 2, locationPassingPaths ? 2 : 0),
                (snapshot.RoutePathSaveState.AlternativePaths.Count, signals.DeadlockDetails.Count));
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

        private sealed class ReversePathEditorContext : IPathEditorContext, IPathEditorContextServicesAccessor
        {
            private readonly IPathEditorServices services;

            public IMapRenderer Renderer => null;
            public IMapViewport Viewport => null;
            public ToolboxContentMode ContentMode { get; set; }
            public PathEditorBase PathEditor { get; set; }

            IPathEditorServices IPathEditorContextServicesAccessor.Services => services;

            public ReversePathEditorContext(TrackWorld world)
            {
                services = new PathEditorServices(world);
            }

            public void RequestRedraw() { }
        }
    }
}
