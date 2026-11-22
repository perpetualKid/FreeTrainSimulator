using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Simulation.AIs;

using Tests.FreeTrainSimulator.Common;
using Tests.FreeTrainSimulator.Simulator;

using EditorTrainPath = FreeTrainSimulator.Graphics.MapView.Widgets.EditorTrainPath;

namespace Tests.FreeTrainSimulator.Toolbox.PathEditing
{
    // Integration coverage for path validation and guarded persistence using the assembly-wide isolated model store.
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathValidationPersistenceTests
    {
        private sealed class TestPathEditorContext : IPathEditorContext, IPathEditorContextServicesAccessor
        {
            private readonly IPathEditorServices services;
            public IMapRenderer Renderer => null;
            public IMapViewport Viewport => null;
            public ToolboxContentMode ContentMode { get; set; }
            public PathEditorBase PathEditor { get; set; }
            IPathEditorServices IPathEditorContextServicesAccessor.Services => services;
            public void RequestRedraw() { }

            public TestPathEditorContext(TrackWorld world)
            {
                services = new PathEditorServices(world);
            }
        }

        /// <summary>The exact Simulator common-suffix wait graph must also be refused by normal editor Save.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(false, PathNodeType.Wait, 30)]
        [DataRow(false, PathNodeType.Wait, 0)]
        [DataRow(true, PathNodeType.Wait, 30)]
        [DataRow(true, PathNodeType.Wait, 0)]
        [DataRow(true, PathNodeType.Reversal, 0)]
        public async Task WhenProjectionChangesMainWaitParticipationThenEditorSavePreservesDraftAndStore(bool prefix, PathNodeType eventType, int waitTime)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            PathModel source = CreateProjectionSource(prefix, eventType, waitTime);
            int eventIndex = prefix ? 1 : 4;
            _ = await fixture.Route.Save(source, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] sourceBytes = MemoryPackSerializer.Serialize(source);
            string nativeFile = ModelFileResolver<PathModelHeader>.FilePath(source) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] storedBytes = await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));

                PathPersistenceValidationResult validation = editor.ValidateCurrentPathForPersistence();
                PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, world,
                    TestContext.CancellationToken).ConfigureAwait(false);

                Assert.IsFalse(validation.PersistenceAllowed, "Projection must not change which trains execute the main wait.");
                Assert.IsFalse(saved.PersistenceAllowed);
                Assert.AreEqual(PathRouteDiagnosticCode.UnsupportedProjectedMainEvent, validation.HighestActionableDiagnostic?.Code);
                Assert.AreEqual(eventIndex, validation.HighestActionableDiagnostic.NodeIndex);
                Assert.AreEqual(eventIndex, saved.HighestActionableDiagnostic.NodeIndex);
                Assert.Contains($"node {eventIndex} ", saved.HighestActionableDiagnostic.Message);
                Assert.AreSame(source, saved.PathModel);
                Assert.AreEqual(eventIndex, editor.GetSimulatorEventDiagnostics().Single().NodeIndex);
                PathPersistenceValidationResult neutral = PathPersistenceValidationPolicy.ResolveAndMaterialize(source, world);
                Assert.IsTrue(neutral.PersistenceAllowed, neutral.FailureMessage);
                foreach (bool timetableMode in new[] { false, true })
                {
                    Assert.IsNull(new AIPath(neutral.PathModel, timetableMode).Nodes);
                    PathEditResult cleared = eventType == PathNodeType.Wait
                        ? PathModelEditor.ClearWaitPoint(source, eventIndex) : PathModelEditor.ClearReversalPoint(source, eventIndex);
                    Assert.IsTrue(cleared.Success, cleared.Message);
                }
                Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(source));
                Assert.AreSequenceEqual(storedBytes, await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        /// <summary>Event authoring uses the same physical projection contract without changing history or the draft.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(false, PathNodeType.Wait)]
        [DataRow(true, PathNodeType.Wait)]
        [DataRow(true, PathNodeType.Reversal)]
        public async Task WhenAuthoringChangesProjectedParticipationThenEventIsRefused(bool prefix, PathNodeType eventType)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel withEvent = CreateProjectionSource(prefix, eventType, 0);
            int eventIndex = prefix ? 1 : 4;
            PathModel source = withEvent with
            {
                PathNodes = withEvent.PathNodes.SetItem(eventIndex,
                withEvent.PathNodes[eventIndex] with { NodeType = PathNodeType.Via, WaitInfo = null })
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(RuntimeDataResolver.Instance.TrackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                PathModel before = editor.TryCaptureCurrentPathModel();
                byte[] sourceBytes = MemoryPackSerializer.Serialize(before);

                bool canSet = eventType == PathNodeType.Wait ? editor.CanSetWaitPoint(eventIndex) : editor.CanSetReversalPoint(eventIndex);
                PathEditResult result = eventType == PathNodeType.Wait ? editor.SetWaitPoint(eventIndex, 30) : editor.SetReversalPoint(eventIndex);

                Assert.IsFalse(canSet);
                Assert.IsFalse(result.Success);
                Assert.Contains($"changes branch participation in main event at node {eventIndex}", result.Message);
                Assert.AreSame(before, editor.TryCaptureCurrentPathModel());
                Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(editor.TryCaptureCurrentPathModel()));
                Assert.IsFalse(editor.CanUndo);
                Assert.IsFalse(editor.HasUnsavedChanges);
            }
        }

        /// <summary>Markerless payloads remain main data, and event-free projection remains supported.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(false, 0)]
        [DataRow(false, 30)]
        [DataRow(true, 0)]
        [DataRow(true, 30)]
        public async Task WhenProjectionHasNoMainMarkerThenSaveAndSimulatorPreservePayload(bool prefix, int waitTime)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = CreateProjectionSource(prefix, PathNodeType.None, waitTime);
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                Assert.IsTrue(editor.ValidateCurrentPathForPersistence().PersistenceAllowed);

                PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, world,
                    TestContext.CancellationToken).ConfigureAwait(false);

                Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
                PathModel reloaded = await fixture.Route.PathModel(source.Id, TestContext.CancellationToken).ConfigureAwait(false);
                PathNode original = source.PathNodes[prefix ? 1 : 4];
                PathNode retained = reloaded.PathNodes.Single(node => node.Location == original.Location);
                Assert.AreEqual(original.WaitInfo, retained.WaitInfo);
                Assert.IsFalse(retained.NodeType.Includes(PathNodeType.Wait));
                foreach (bool timetableMode in new[] { false, true })
                    Assert.IsNotNull(new AIPath(reloaded, timetableMode).Nodes);
            }
        }

        /// <summary>Main-divergent events, terminal events and the authored rejoin retain main-event semantics.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(2, PathNodeType.Wait, false)]
        [DataRow(5, PathNodeType.Wait, false)]
        [DataRow(5, PathNodeType.Reversal, false)]
        [DataRow(5, PathNodeType.Wait, true)]
        public async Task WhenProjectionLeavesMainEventParticipationUnchangedThenNormalSaveAllowsIt(int eventIndex,
            PathNodeType eventType, bool extendAfterRejoin)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = CreateProjectionSource(false, PathNodeType.None, 0);
            source = source with
            {
                PathNodes = source.PathNodes.SetItem(eventIndex, source.PathNodes[eventIndex] with
                {
                    NodeType = (extendAfterRejoin ? PathNodeType.Via : source.PathNodes[eventIndex].NodeType) | eventType,
                    WaitInfo = PathWaitOperationCodec.FromMstsWaitTime(0),
                    NextMainNode = extendAfterRejoin ? 8 : source.PathNodes[eventIndex].NextMainNode,
                })
            };
            if (extendAfterRejoin)
                source = source with { PathNodes = source.PathNodes.Add(SyntheticPathFixture.Node(PathNodeType.End, 2, 180, 0, -1, -1)) };
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                Assert.IsTrue(editor.ValidateCurrentPathForPersistence().PersistenceAllowed);

                PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, world,
                    TestContext.CancellationToken).ConfigureAwait(false);

                Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
                foreach (bool timetableMode in new[] { false, true })
                    Assert.IsNotNull(new AIPath(saved.PathModel, timetableMode).Nodes);
            }
        }

        /// <summary>Ambiguity materialization inserts junctions and reorders nodes before checking projected events.</summary>
        [TestMethod]
        [DoNotParallelize]
        public async Task WhenAmbiguousPassingProjectionInsertsNodesThenSaveChecksGeneratedGraphAndMapsAuthoredEvent()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            // Mirror the 300 m passing vector below the main line so both main choices have equal cost.
            VectorNode mainVector = (VectorNode)fixture.Track.TrackDatabase.TrackNodes[4];
            Tile tile = mainVector.Location.Tile;
            mainVector = mainVector with
            {
                VectorSections = ImmutableArray.Create(
                    new VectorSectionNode(SyntheticPathFixture.Location(0, 0), tile, new Vector3(0, MathHelper.Pi, 0), SyntheticPathFixture.Location(0, -100)) { NodeIndex = 1 },
                    new VectorSectionNode(SyntheticPathFixture.Location(0, -100), tile, new Vector3(0, MathHelper.PiOver2, 0), SyntheticPathFixture.Location(100, -100)) { NodeIndex = 1 },
                    new VectorSectionNode(SyntheticPathFixture.Location(100, -100), tile, Vector3.Zero, SyntheticPathFixture.Location(100, 0)) { NodeIndex = 1 }),
            };
            TrackModel track = fixture.Track with { TrackDatabase = fixture.Track.TrackDatabase with
            { TrackNodes = fixture.Track.TrackDatabase.TrackNodes.SetItem(4, mainVector) } };
            _ = await fixture.CreateSimulator(track, false, TestContext.CancellationToken).ConfigureAwait(false);
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            PathModel source = new PathModel
            {
                Id = "AmbiguousProjectedWait",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Wait, 2, 120, 0, 2, -1) with
                    { WaitInfo = PathWaitOperationCodec.FromMstsWaitTime(30) },
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 0, 3),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 2)),
            };
            _ = await fixture.Route.Save(source, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] sourceBytes = MemoryPackSerializer.Serialize(source);
            string nativeFile = ModelFileResolver<PathModelHeader>.FilePath(source) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] storedBytes = await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false);

            PathPersistenceValidationResult neutral = PathPersistenceValidationPolicy.ResolveAndMaterialize(source, world);
            Assert.IsTrue(neutral.PersistenceAllowed, neutral.FailureMessage);
            Assert.IsTrue(neutral.Resolution.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute));
            int generatedEventIndex = neutral.PathModel.PathNodes.IndexOf(neutral.PathModel.PathNodes.Single(node => node.NodeType.Includes(PathNodeType.Wait)));
            Assert.AreNotEqual(0, generatedEventIndex);
            Assert.IsGreaterThan(source.PathNodes.Length, neutral.PathModel.PathNodes.Length);
            PathRouteDiagnostic generatedDiagnostic = PathPersistenceValidationPolicy.GetSimulatorEventDiagnostics(neutral.PathModel, world).Single();
            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedProjectedMainEvent, generatedEventIndex), (generatedDiagnostic.Code, generatedDiagnostic.NodeIndex));

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, world, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsFalse(saved.PersistenceAllowed);
            Assert.AreSame(source, saved.PathModel);
            Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedProjectedMainEvent, 0), (saved.HighestActionableDiagnostic.Code, saved.HighestActionableDiagnostic.NodeIndex));
            Assert.Contains("node 0 ", saved.HighestActionableDiagnostic.Message);
            Assert.IsTrue(saved.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute));
            foreach (bool timetableMode in new[] { false, true })
                Assert.IsNull(new AIPath(neutral.PathModel, timetableMode).Nodes);
            Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(source));
            Assert.AreSequenceEqual(storedBytes, await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false));
        }

        /// <summary>Co-located events need unique source and generated occurrences; array indexes are not evidence.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenProjectedMainEventsAreColocatedThenSaveMapsOnlyUniquePayloadOccurrences(bool distinctPayloads)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            PathModel source = CreateProjectionSource(false, PathNodeType.Wait, 30);
            source = source with { PathNodes = source.PathNodes.SetItem(4, source.PathNodes[4] with { NextMainNode = 8 })
                .Add(source.PathNodes[4] with { WaitInfo = PathWaitOperationCodec.FromMstsWaitTime(distinctPayloads ? 60 : 30) }) };
            _ = await fixture.Route.Save(source, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] sourceBytes = MemoryPackSerializer.Serialize(source);
            string nativeFile = ModelFileResolver<PathModelHeader>.FilePath(source) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] storedBytes = await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false);

            PathPersistenceValidationResult neutral = PathPersistenceValidationPolicy.ResolveAndMaterialize(source, world);
            Assert.IsTrue(neutral.PersistenceAllowed, neutral.FailureMessage);
            ImmutableArray<PathRouteDiagnostic> generated = PathPersistenceValidationPolicy.GetSimulatorEventDiagnostics(neutral.PathModel, world);
            Assert.HasCount(2, generated);
            Assert.AreSequenceEqual(expectedArray56, generated.Select(diagnostic => diagnostic.NodeIndex).ToArray());
            Assert.IsTrue(generated.All(diagnostic => neutral.PathModel.PathNodes[diagnostic.NodeIndex].NodeType.Includes(PathNodeType.Wait)
                && neutral.PathModel.PathNodes[diagnostic.NodeIndex].Location == source.PathNodes[4].Location));

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, world, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsFalse(saved.PersistenceAllowed);
            Assert.AreSame(source, saved.PathModel);
            PathRouteDiagnostic[] diagnostics = saved.Diagnostics.Where(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnsupportedProjectedMainEvent).ToArray();
            Assert.HasCount(2, diagnostics);
            Assert.AreSequenceEqual(distinctPayloads ? expectedArray48 : new[] { -1, -1 }, diagnostics.Select(diagnostic => diagnostic.NodeIndex).ToArray());
            Assert.IsTrue(diagnostics.All(diagnostic => diagnostic.FromNodeIndex == -1 && diagnostic.ToNodeIndex == -1));
            if (distinctPayloads)
                Assert.Contains("node 8 ", diagnostics[1].Message);

            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                Assert.AreSequenceEqual(diagnostics.Select(diagnostic => diagnostic.NodeIndex).ToArray(),
                    editor.GetSimulatorEventDiagnostics().Select(diagnostic => diagnostic.NodeIndex).ToArray());
                foreach (bool timetableMode in new[] { false, true })
                    Assert.IsNull(new AIPath(neutral.PathModel, timetableMode).Nodes);
            }
            Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(source));
            Assert.AreSequenceEqual(storedBytes, await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false));
        }

        /// <summary>Track identity alone cannot make later or earlier visits part of a common prefix or suffix.</summary>
        [TestMethod]
        [DoNotParallelize]
        public async Task WhenPassingProjectionSpansRepeatedDirectedOccurrencesThenInteriorReversalsRemainSupported()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            PathModel source = new PathModel
            {
                Id = "RepeatedProjectionOccurrences",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -80, 0, 1, 13),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 4, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 2, 150, 0, 5, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 6, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 7, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 8, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 1, -50, 0, 9, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 10, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 11, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 12, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 180, 0, -1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 1, -40, 0, -1, 14),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, -1, 15),
                    SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 16),
                    SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 12)),
            };
            byte[] sourceBytes = MemoryPackSerializer.Serialize(source);

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(source, fixture.Route, world,
                TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
            Assert.IsEmpty(PathPersistenceValidationPolicy.GetSimulatorEventDiagnostics(saved.PathModel, world));
            PathModel reloaded = await fixture.Route.PathModel(source.Id, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.HasCount(2, reloaded.PathNodes.Where(node => node.NodeType.Includes(PathNodeType.Reversal)).ToArray());
            foreach (bool timetableMode in new[] { false, true })
            {
                AIPath runtime = new AIPath(reloaded, timetableMode);
                Assert.IsNotNull(runtime.Nodes);
                Assert.HasCount(2, runtime.Nodes.Where(node => node.Type == TrainPathNodeType.Reverse).ToArray());
            }
            Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(source));
        }

        /// <summary>A mismatched vector arrival is not evidence that a common suffix can be projected.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenPassingRejoinArrivalDiffersThenProjectionDoesNotInventEventRefusal(bool emptyFinalSpan)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            PathModel source = CreateProjectionSource(false, PathNodeType.Reversal, 0);
            source = source with { PathNodes = source.PathNodes
                .SetItem(4, SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Reversal, 2, 170, 0, 5, -1))
                .SetItem(5, SyntheticPathFixture.Node(emptyFinalSpan ? PathNodeType.Via : PathNodeType.End, 2, 130, 0, emptyFinalSpan ? 8 : -1, -1)) };
            if (emptyFinalSpan)
                source = source with { PathNodes = source.PathNodes.SetItem(7, source.PathNodes[7] with { NextSidingNode = 8 }).Add(SyntheticPathFixture.Node(PathNodeType.End, 2, 130, 0, -1, -1)) };
            byte[] sourceBytes = MemoryPackSerializer.Serialize(source);
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);
            Assert.IsTrue(resolution.IsValid);
            Assert.AreNotEqual(resolution.MainRoute.Spans[^1].Candidates[0].Arrival, resolution.PassingRoutes.Single().Spans[^1].Candidates[0].Arrival);
            if (emptyFinalSpan)
                Assert.IsEmpty(resolution.MainRoute.Spans[^1].Candidates[0].PhysicalTraversals);

            Assert.IsEmpty(PathSimulatorEventCompatibility.ValidateProjection(source, resolution, world));

            foreach (bool timetableMode in new[] { false, true })
                Assert.IsNull(new AIPath(source, timetableMode).Nodes, "The separate Simulator arrival guard remains authoritative.");
            Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(source));
        }

        /// <summary>An existing projection refusal cannot hide a new indistinguishable occurrence during authoring.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(4)]
        [DataRow(8)]
        public async Task WhenAuthoringAddsIndistinguishableProjectedWaitThenEachExistingRefusalIsConsumedOnce(int newEventIndex)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = CreateProjectionSource(false, PathNodeType.Wait, 30);
            source = source with { PathNodes = source.PathNodes.SetItem(4, source.PathNodes[4] with { NextMainNode = 8 }).Add(source.PathNodes[4]) };
            source = source with { PathNodes = source.PathNodes.SetItem(newEventIndex,
                source.PathNodes[newEventIndex] with { NodeType = PathNodeType.Via, WaitInfo = null }) };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(RuntimeDataResolver.Instance.TrackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                PathModel before = editor.TryCaptureCurrentPathModel();
                byte[] sourceBytes = MemoryPackSerializer.Serialize(before);
                Assert.AreEqual(newEventIndex == 4 ? 8 : 4, editor.GetSimulatorEventDiagnostics().Single().NodeIndex);
                Assert.IsFalse(editor.CanSetWaitPoint(newEventIndex));
                PathEditResult result = editor.SetWaitPoint(newEventIndex, 30);

                Assert.IsFalse(result.Success);
                Assert.Contains("changes branch participation in main event", result.Message);
                Assert.AreSame(before, editor.TryCaptureCurrentPathModel());
                Assert.AreSequenceEqual(sourceBytes, MemoryPackSerializer.Serialize(editor.TryCaptureCurrentPathModel()));
                Assert.HasCount(1, editor.GetSimulatorEventDiagnostics());
                Assert.IsFalse(editor.CanUndo);
                Assert.IsFalse(editor.HasUnsavedChanges);
            }
        }

        private static PathModel CreateProjectionSource(bool prefix, PathNodeType eventType, int waitTime)
        {
            PathNodeWaitInfo wait = PathWaitOperationCodec.FromMstsWaitTime(waitTime);
            return new PathModel
            {
                Id = prefix ? "BypassedPrefixWait" : "BypassedSuffixWait",
                PathNodes = prefix
                    ? ImmutableArray.Create(
                        SyntheticPathFixture.Node(PathNodeType.Start, 1, eventType == PathNodeType.Reversal ? -50 : -80, 0, 1, 6),
                        SyntheticPathFixture.Node(PathNodeType.Via | eventType, 1, eventType == PathNodeType.Reversal ? -80 : -60, 0, 2, -1) with { WaitInfo = wait },
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 3, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 4, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 5, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 1, -40, 0, -1, 7),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, -1, 8),
                        SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 9),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 5))
                    : ImmutableArray.Create(
                        SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 3, 0, 0, 2, 6),
                        SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, 3, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, 4, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via | eventType, 2, 120, 0, 5, -1) with { WaitInfo = wait },
                        SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                        SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, 7),
                        SyntheticPathFixture.Node(PathNodeType.Via | PathNodeType.Junction, 6, 100, 0, -1, 5)),
            };
        }

        [TestMethod]
        public void WhenNewPathIsPreparedForSaveThenTrimmedNameBecomesIdentity()
        {
            PathModelHeader details = new PathModelHeader { Id = PathEditor.NewPathId, Name = " Morning Run " };

            PathModelHeader prepared = TrainPathSaveRequest.PreparePathDetails(details, PathEditor.NewPathId);

            Assert.AreEqual("Morning Run", prepared.Id);
        }

        [TestMethod]
        public void WhenExistingPathIsPreparedForSaveThenIdentityIsPreserved()
        {
            PathModelHeader details = new PathModelHeader { Id = "path-1", Name = "Renamed Path" };

            PathModelHeader prepared = TrainPathSaveRequest.PreparePathDetails(details, "path-1");

            Assert.AreEqual("path-1", prepared.Id);
        }

        [TestMethod]
        public async Task WhenValidateRoutePathsRunsThenReloadedHeadersCarryPersistedValidationState()
        {
            RouteModelHeader route = await SeedRouteWithPathsAsync(TestContext.CancellationToken).ConfigureAwait(false);

            _ = await PathEditor.ValidateRoutePaths(route, null, true, CancellationToken.None).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> reloaded = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModelHeader emptyPath = reloaded.Single(p => p.Id == "empty-path");
            PathModelHeader linearPath = reloaded.Single(p => p.Id == "linear-path");

            Assert.AreEqual(PathValidationState.Invalid, emptyPath.ValidationState);
            Assert.AreEqual(PathValidationState.Valid, linearPath.ValidationState);
        }

        [TestMethod]
        public async Task WhenValidPathIsSavedThenNormalizedContentIsPersisted()
        {
            RouteModel route = CreateRoute();
            PathModel path = CreateLinearPath("valid-path");

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(path, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(savedPath => savedPath.Id == path.Id);
            PathModel savedPath = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(result.PersistenceAllowed);
            Assert.AreEqual(PathValidationState.Valid, savedPath.ValidationState);
            Assert.AreSequenceEqual(result.PathModel.PathNodes, savedPath.PathNodes);
        }

        [TestMethod]
        [DataRow(30000)]
        [DataRow(40102)]
        [DataRow(50102)]
        [DataRow(60001)]
        [DataRow(60002)]
        [DataRow(60011)]
        [DataRow(60021)]
        public async Task WhenAdvancedWaitOperationIsAuthoredThenNormalSaveAndUncachedReopenPreserveItsRawCode(int waitCode)
        {
            RouteModel route = CreateRoute();
            PathModel source = new PathModel
            {
                Id = "advanced-operation-" + waitCode,
                Name = "Advanced Operation",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(50, PathNodeType.Via, 2),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };
            PathEditResult authored = PathModelEditor.SetWaitOperation(source, 1, PathWaitOperationCodec.FromMstsWaitTime(waitCode));
            Assert.IsTrue(authored.Success, authored.Message);

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(authored.PathModel, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);
            PathModel reopened = await route.PathModelUncached(source.Id, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
            Assert.AreEqual(waitCode, PathWaitOperationCodec.GetMstsWaitCode(
                reopened.PathNodes.Single(node => node.WaitInfo != null).WaitInfo));
        }

        [TestMethod]
        [DataRow(29999)]
        [DataRow(30000)]
        [DataRow(40000)]
        [DataRow(40001)]
        [DataRow(60001)]
        [DataRow(60002)]
        [DataRow(60011)]
        [DataRow(60021)]
        [DataRow(65535)]
        public async Task WhenLegacyWaitIsImportedThenUnchangedNormalSavePreservesRawPayloadAndClearRemainsSupported(int waitTime)
        {
            RouteModel route = CreateRoute() with
            {
                Tags = ImmutableDictionary<string, string>.Empty.Add(RouteModelImportHandler.SourceNameKey, "LegacyWaitRoute"),
            };
            string pathsFolder = Path.Combine(route.Parent.ContentPath, "ROUTES", "LegacyWaitRoute", "PATHS");
            _ = Directory.CreateDirectory(pathsFolder);
            string sourceFile = Path.Combine(pathsFolder, "legacy-wait.pat");
            TestContext.WriteLine(sourceFile);
            await File.WriteAllTextAsync(sourceFile, $"""
                SIMISA@@@@@@@@@@JINX0P0t______
                Serial ( 1 )
                TrackPDPs (
                    TrackPDP ( 1 0 0 0 0 1 1 )
                    TrackPDP ( 1 0 50 0 0 1 1 )
                    TrackPDP ( 1 0 100 0 0 1 1 )
                )
                TrackPath (
                    TrPathName ( legacy-wait )
                    Name ( "Legacy Wait" )
                    TrPathStart ( Start )
                    TrPathEnd ( End )
                    TrPathNodes ( 3
                        TrPathNode ( 00000000 1 4294967295 0 )
                        TrPathNode ( {((uint)waitTime << 16 | 2):X8} 2 4294967295 1 )
                        TrPathNode ( 00000000 4294967295 4294967295 2 )
                    )
                )
                """, Encoding.Unicode, TestContext.CancellationToken).ConfigureAwait(false);
            PathModelHeader importedHeader = (await PathModelImportHandler.ExpandPathModels(route, TestContext.CancellationToken).ConfigureAwait(false)).Single();
            PathModel imported = await importedHeader.GetExtended(TestContext.CancellationToken).ConfigureAwait(false);
            WorldLocation start = new WorldLocation(new Tile(1, 0), Vector3.Zero);
            WorldLocation end = new WorldLocation(new Tile(1, 0), new Vector3(100, 0, 0));
            VectorNode vectorNode = new VectorNode(start, new Tile(1, 0), end)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(new VectorSectionNode(start, new Tile(1, 0), new Vector3(0, MathHelper.PiOver2, 0), end)
                { NodeIndex = 1 }),
            };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vectorNode),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex
                { NodeIndex = 1, TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackWorld world = TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 }),
            });

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(imported, route, world, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel reloaded = await route.PathModel(imported.Id, TestContext.CancellationToken).ConfigureAwait(false);
            PathEditResult cleared = PathModelEditor.ClearWaitPoint(reloaded, 1);
            PathPersistenceValidationResult clearedSave = await PathEditor.SaveValidatedPath(cleared.PathModel, route, world, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel clearedReloaded = await route.PathModel(imported.Id, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual((waitTime, true, waitTime, true, true, false),
                (PathWaitOperationCodec.GetMstsWaitCode(imported.PathNodes[1].WaitInfo), saved.PersistenceAllowed,
                    PathWaitOperationCodec.GetMstsWaitCode(reloaded.PathNodes[1].WaitInfo), cleared.Success,
                    clearedSave.PersistenceAllowed, clearedReloaded.PathNodes.Any(node => node.WaitInfo != null || node.NodeType.Includes(PathNodeType.Wait))),
                $"Save: {saved.FailureMessage}; cleared save: {clearedSave.FailureMessage}");
        }

        /// <summary>Source markers survive import while normal Save and Simulator retain their event restrictions and repair workflow.</summary>
        [TestMethod]
        [DoNotParallelize]
        [DataRow(0, 0x00000001u, 1, PathRouteDiagnosticCode.UnsupportedFirstMainEvent)]
        [DataRow(0, 0x00000002u, 1, PathRouteDiagnosticCode.UnsupportedFirstMainEvent)]
        [DataRow(0, 0x00000003u, 1, PathRouteDiagnosticCode.UnsupportedFirstMainEvent)]
        [DataRow(0, 0x001E0002u, 1, PathRouteDiagnosticCode.UnsupportedFirstMainEvent)]
        [DataRow(1, 0x00000001u, 2, PathRouteDiagnosticCode.UnsupportedJunctionEvent)]
        [DataRow(1, 0x00000002u, 2, PathRouteDiagnosticCode.UnsupportedJunctionEvent)]
        [DataRow(1, 0x00000003u, 2, PathRouteDiagnosticCode.UnsupportedJunctionEvent)]
        [DataRow(1, 0x00000003u, 1, PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal)]
        [DataRow(1, 0x001E0003u, 1, PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal)]
        [DataRow(2, 0x00000003u, 1, PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal)]
        [DataRow(2, 0x00000001u, 1, null)]
        [DataRow(2, 0x00000002u, 1, null)]
        [DataRow(2, 0x001E0001u, 1, null)]
        [DataRow(2, 0x001E0002u, 1, null)]
        public async Task WhenSourceEventsAreImportedThenSaveAndRuntimePreserveCompatibilityAndAllowRepair(int eventIndex,
            uint sourceFlags, int junctionFlag, PathRouteDiagnosticCode? expectedDiagnostic)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await fixture.CreateSimulator(false, TestContext.CancellationToken).ConfigureAwait(false);
            RouteModel route = fixture.Route;
            TrackWorld world = RuntimeDataResolver.Instance.TrackWorld;
            string pathsFolder = Path.Combine(fixture.ContentRoot, "Routes", route.Id, "Paths");
            _ = Directory.CreateDirectory(pathsFolder);
            string sourceFile = Path.Combine(pathsFolder, "source-events.pat");
            bool interiorReturn = eventIndex == 1 && junctionFlag != 2 && (sourceFlags & 1) != 0;
            string nodes = string.Join(Environment.NewLine, Enumerable.Range(0, 3).Select(index =>
                $"        TrPathNode ( {(index == eventIndex ? sourceFlags : index == 1 ? 4u : 0u):X8} {(index == 2 ? uint.MaxValue : (uint)(index + 1))} 4294967295 {index} )"));
            await File.WriteAllTextAsync(sourceFile, $"""
                SIMISA@@@@@@@@@@JINX0P0t______
                Serial ( 1 )
                TrackPDPs (
                    TrackPDP ( 1 1 -80 0 0 1 1 )
                    TrackPDP ( 1 1 {(junctionFlag == 2 ? 0 : -50)} 0 0 {junctionFlag} 0 )
                    TrackPDP ( 1 1 {(junctionFlag == 2 ? 50 : interiorReturn ? -70 : -20)} 0 0 1 1 )
                )
                TrackPath (
                    TrPathName ( source-events )
                    Name ( "Source Events" )
                    TrPathStart ( Start )
                    TrPathEnd ( End )
                    TrPathNodes ( 3
                {nodes}
                    )
                )
                """, Encoding.Unicode, TestContext.CancellationToken).ConfigureAwait(false);
            byte[] sourceBytes = await File.ReadAllBytesAsync(sourceFile, TestContext.CancellationToken).ConfigureAwait(false);
            PathModelHeader header = (await PathModelImportHandler.ExpandPathModels(route, TestContext.CancellationToken).ConfigureAwait(false)).Single();
            PathModel imported = await ContentHandlerBase<PathModelHeader>.FromFile<PathModel, RouteModelHeader>(header.Id, route, TestContext.CancellationToken).ConfigureAwait(false);
            string nativeFile = ModelFileResolver<PathModelHeader>.FilePath(imported) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] nativeBytes = await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false);

            PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(imported, route, world,
                TestContext.CancellationToken).ConfigureAwait(false);

            if (expectedDiagnostic.HasValue)
            {
                Assert.IsFalse(saved.PersistenceAllowed);
                Assert.AreEqual(expectedDiagnostic.Value, saved.HighestActionableDiagnostic?.Code);
                Assert.AreSame(imported, saved.PathModel);
                Assert.AreSequenceEqual(nativeBytes, await File.ReadAllBytesAsync(nativeFile, TestContext.CancellationToken).ConfigureAwait(false));
                foreach (bool timetableMode in new[] { false, true })
                {
                    AIPath rejected = new AIPath(imported, timetableMode);
                    Assert.IsNull(rejected.Nodes);
                    Assert.IsNull(rejected.FirstNode);
                }

                if (imported.PathNodes[eventIndex].NodeType.Includes(PathNodeType.Wait))
                {
                    PathEditResult cleared = PathModelEditor.ClearWaitPoint(imported, eventIndex);
                    Assert.IsTrue(cleared.Success);
                    imported = cleared.PathModel;
                }
                if (imported.PathNodes[eventIndex].NodeType.Includes(PathNodeType.Reversal) && (eventIndex == 0 || junctionFlag == 2))
                {
                    PathEditResult cleared = PathModelEditor.ClearReversalPoint(imported, eventIndex);
                    Assert.IsTrue(cleared.Success);
                    imported = cleared.PathModel;
                }
                saved = await PathEditor.SaveValidatedPath(imported, route, world, TestContext.CancellationToken).ConfigureAwait(false);
            }

            Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
            PathModel reloaded = await ContentHandlerBase<PathModelHeader>.FromFile<PathModel, RouteModelHeader>(header.Id, route, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(saved.PathModel.PathNodes, reloaded.PathNodes);
            foreach (bool timetableMode in new[] { false, true })
            {
                AIPath runtime = new AIPath(reloaded, timetableMode);
                Assert.IsNotNull(runtime.Nodes);
                if (!expectedDiagnostic.HasValue)
                {
                    PathNode terminal = reloaded.PathNodes[^1];
                    PathNodeType eventType = (sourceFlags & 2) != 0 ? PathNodeType.Wait : PathNodeType.Reversal;
                    Assert.AreEqual(PathNodeType.End | eventType, terminal.NodeType);
                    Assert.AreEqual(eventType == PathNodeType.Wait ? TrainPathNodeType.Stop : TrainPathNodeType.Reverse, runtime.Nodes[^1].Type);
                    Assert.AreEqual((int)(sourceFlags >> 16), runtime.Nodes[^1].WaitTimeS);
                    Assert.AreEqual(1, runtime.Nodes[^1].NextMainTVNIndex);
                    Assert.IsNull(runtime.Nodes[^1].NextMainNode);
                }
            }
            Assert.AreSequenceEqual(sourceBytes, await File.ReadAllBytesAsync(sourceFile, TestContext.CancellationToken).ConfigureAwait(false));
        }

        [TestMethod]
        [DataRow(PathNodeType.Wait)]
        [DataRow(PathNodeType.Reversal)]
        public async Task WhenFirstMainEventIsSavedThenOriginalDraftIsReturnedWithoutWriting(PathNodeType eventType)
        {
            RouteModel route = CreateRoute();
            PathModel source = CreateLinearPath("first-event");
            source = source with { PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | eventType }) };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual((false, source, PathRouteDiagnosticCode.UnsupportedFirstMainEvent, 0),
                (result.PersistenceAllowed, result.PathModel, result.HighestActionableDiagnostic.Code, result.HighestActionableDiagnostic.NodeIndex));
            Assert.AreSame(source, result.PathModel);
            Assert.IsFalse((await route.GetPaths(TestContext.CancellationToken).ConfigureAwait(false)).Any(path => path.Id == source.Id));
        }

        [TestMethod]
        public async Task WhenCombinedEventSaveIsBlockedThenExistingStoreContentIsPreserved()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("combined-event");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = persisted with
            {
                PathNodes = ImmutableArray.Create(CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(50, PathNodeType.Via | PathNodeType.Wait | PathNodeType.Reversal, 2) with
                    { WaitInfo = PathWaitOperationCodec.FromMstsWaitTime(15) }, CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader header = (await route.GetPaths(TestContext.CancellationToken).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await header.GetExtended(TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(persisted.PathNodes, reloaded.PathNodes);
            Assert.AreEqual(PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal, result.HighestActionableDiagnostic.Code);
            Assert.Contains(result.HighestActionableDiagnostic.SuggestedAction, result.FailureMessage, StringComparison.Ordinal);
        }

        [TestMethod]
        public async Task WhenWaitSeparatesReversalsWithoutJunctionThenSaveIsBlocked()
        {
            PathModel source = new PathModel
            {
                Id = "repeated-reversal",
                PathNodes = ImmutableArray.Create(CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(20, PathNodeType.Via | PathNodeType.Reversal, 2),
                    CreatePathNode(40, PathNodeType.Via | PathNodeType.Wait, 3) with { WaitInfo = PathWaitOperationCodec.FromMstsWaitTime(10) },
                    CreatePathNode(60, PathNodeType.Via | PathNodeType.Reversal, 4), CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, CreateRoute(),
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual((false, PathRouteDiagnosticCode.MultipleReversalsInContinuousVector, 3),
                (result.PersistenceAllowed, result.HighestActionableDiagnostic.Code, result.HighestActionableDiagnostic.NodeIndex));
        }

        [TestMethod]
        [DataRow(PathNodeType.Wait)]
        [DataRow(PathNodeType.Reversal)]
        public async Task WhenSingleOrdinaryMainEventIsSavedThenItIsPreserved(PathNodeType eventType)
        {
            PathModel source = new PathModel
            {
                Id = "ordinary-event",
                PathNodes = ImmutableArray.Create(CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(50, PathNodeType.Via | eventType, 2), CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, CreateRoute(),
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsTrue(result.PersistenceAllowed && result.PathModel.PathNodes[1].NodeType.Includes(eventType));
        }

        [TestMethod]
        [DataRow(PathNodeType.Wait)]
        [DataRow(PathNodeType.None)]
        public async Task WhenPassingInteriorHasWaitMarkerOrPayloadThenGeneratedTargetMapsBackToSource(PathNodeType eventType)
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateRepresentativePassingPath("passing-event", world);
            source = source with
            {
                PathNodes = source.PathNodes.SetItem(3, source.PathNodes[3] with
                { NodeType = PathNodeType.Via | eventType, WaitInfo = PathWaitOperationCodec.FromMstsWaitTime(30) }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, CreateRoute(), world,
                TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual((false, PathRouteDiagnosticCode.UnsupportedPassingInteriorEvent, 3),
                (result.PersistenceAllowed, result.HighestActionableDiagnostic.Code, result.HighestActionableDiagnostic.NodeIndex));
            Assert.AreSame(source, result.PathModel);
        }

        [TestMethod]
        public void WhenMaterializationAddsJunctionMarkerToWaitThenStrictSaveRejectsTheExactGeneratedModel()
        {
            TrackWorld world = CreateJunctionContinuityWorld(ConnectorType.OutPin);
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredPathNode(world, 1, PathNodeType.Start | PathNodeType.Junction, 1, -1),
                    CreateAnchoredPathNode(world, 3, PathNodeType.Via | PathNodeType.Wait, 2, -1),
                    CreateAnchoredPathNode(world, 2, PathNodeType.End | PathNodeType.Junction, -1, -1)),
            };

            PathPersistenceValidationResult neutral = PathPersistenceValidationPolicy.ResolveAndMaterialize(source, world);
            PathPersistenceValidationResult strict = PathPersistenceValidationPolicy.ValidateForPersistence(source, world);

            Assert.IsTrue(neutral.PersistenceAllowed && neutral.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Junction));
            Assert.AreEqual((false, PathRouteDiagnosticCode.UnsupportedJunctionEvent, 1),
                (strict.PersistenceAllowed, strict.HighestActionableDiagnostic.Code, strict.HighestActionableDiagnostic.NodeIndex));
        }

        [TestMethod]
        public void WhenAmbiguousPathHasUnsupportedEventThenSaveRetainsAmbiguityFeedback()
        {
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreatePathNode(100, PathNodeType.Start | PathNodeType.Wait, 1),
                    CreatePathNode(200, PathNodeType.End, -1) with { NodeIndex = 2 }),
            };

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.ValidateForPersistence(source, CreateAmbiguousRouteTrackWorld());

            Assert.IsTrue(!result.PersistenceAllowed && result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute)
                && result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnsupportedFirstMainEvent));
        }

        [TestMethod]
        public void WhenNoncanonicalSourceIsMaterializedThenEventTargetUsesAuthoredIndex()
        {
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreatePathNode(100, PathNodeType.End, -1),
                    CreatePathNode(0, PathNodeType.Start, 2), CreatePathNode(50, PathNodeType.Via | PathNodeType.Wait | PathNodeType.Reversal, 0)),
            };

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.ValidateForPersistence(source,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());

            Assert.AreEqual(2, result.HighestActionableDiagnostic.NodeIndex);
            Assert.AreEqual(PathSimulatorEventCompatibility.ValidateNode(source.PathNodes[2], 2, false, false).Message,
                result.HighestActionableDiagnostic.Message);
        }

        [TestMethod]
        public void WhenRepeatedEventLocationsAreIndistinguishableThenSaveDiagnosticHasNoAuthoredTarget()
        {
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(50, PathNodeType.Via | PathNodeType.Wait | PathNodeType.Reversal, 2),
                    CreatePathNode(50, PathNodeType.Via | PathNodeType.Wait | PathNodeType.Reversal, 3), CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.ValidateForPersistence(source,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());

            ImmutableArray<PathRouteDiagnostic> eventDiagnostics = result.Diagnostics
                .Where(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal).ToImmutableArray();
            Assert.HasCount(2, eventDiagnostics);
            Assert.IsTrue(!result.PersistenceAllowed && eventDiagnostics
                .All(diagnostic => diagnostic.NodeIndex == -1 && diagnostic.FromNodeIndex == -1 && diagnostic.ToNodeIndex == -1));
        }

        [TestMethod]
        public async Task WhenRawStoreSavesUnsupportedEventThenContentIsNotRestricted()
        {
            RouteModel route = CreateRoute();
            PathModel source = CreateLinearPath("raw-event");
            source = source with { PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait }) };

            _ = await route.Save(source, TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader header = (await route.GetPaths(TestContext.CancellationToken).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await header.GetExtended(TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(source.PathNodes, reloaded.PathNodes);
        }

        [TestMethod]
        public async Task WhenWarningOnlyPathIsSavedThenContentIsPersisted()
        {
            RouteModel route = CreateRoute();
            PathModel path = new PathModel
            {
                Id = "warning-path",
                Name = "Warning Path",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(100, PathNodeType.Start, 1) with { NodeIndex = 1 },
                    CreatePathNode(200, PathNodeType.End, -1) with { NodeIndex = 2 }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(path, route,
                CreateAmbiguousRouteTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(savedPath => savedPath.Id == path.Id);
            PathModel savedPath = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Severity == PathRouteDiagnosticSeverity.Warning));
            Assert.IsTrue(result.Diagnostics.All(diagnostic => diagnostic.Severity < PathRouteDiagnosticSeverity.Error),
                string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}:{diagnostic.Code}:{diagnostic.Message}")));
            Assert.AreSequenceEqual(result.PathModel.PathNodes, savedPath.PathNodes);
            Assert.IsGreaterThan(path.PathNodes.Length, savedPath.PathNodes.Length);
        }

        [TestMethod]
        public async Task WhenErrorPathSaveIsBlockedThenExistingPersistedContentIsUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("protected-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel invalid = persisted with
            {
                Name = "Invalid Replacement",
                PathNodes = persisted.PathNodes.SetItem(0, persisted.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Junction }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader reloadedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == persisted.Id);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(persisted.Name, reloaded.Name);
            Assert.AreSequenceEqual(persisted.PathNodes, reloaded.PathNodes);
        }

        [TestMethod]
        public async Task WhenWarningOnlyUnreachableNodeWouldBeOmittedThenSaveIsBlockedAndPersistedContentIsUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("disconnected-node-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel imported = persisted with
            {
                Name = "Imported Path With Disconnected Node",
                PathNodes = persisted.PathNodes.Add(CreatePathNode(50, PathNodeType.Via, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(imported, route, TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader reloadedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == persisted.Id);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(PathRouteDiagnosticCode.UnreachableNode, result.HighestActionableDiagnostic?.Code);
            Assert.AreEqual(persisted.Name, reloaded.Name);
            Assert.AreSequenceEqual(persisted.PathNodes, reloaded.PathNodes);
        }

        [TestMethod]
        public void WhenDisconnectedNodeWouldBeOmittedThenMaterializationRetainsSourceAndDiagnostics()
        {
            PathModel source = CreateLinearPath("disconnected-materialization") with
            {
                PathNodes = CreateLinearPath("disconnected-materialization").PathNodes.Add(CreatePathNode(50, PathNodeType.Via, -1)),
            };
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, trackWorld, TestContext.CancellationToken);
            PathRouteDiagnostic unreachableNode = resolution.Diagnostics.Single(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnreachableNode);

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.MaterializeResolvedPath(source, resolution, trackWorld);

            Assert.AreEqual((false, source, resolution, unreachableNode, 0),
                (result.PersistenceAllowed, result.PathModel, result.Resolution, result.HighestActionableDiagnostic, result.ChangedNodeIndexes.Length));
        }

        [TestMethod]
        public void WhenDisconnectedNodeBlocksMaterializationThenFailureIncludesSuggestedAction()
        {
            PathModel source = CreateLinearPath("disconnected-materialization-action") with
            {
                PathNodes = CreateLinearPath("disconnected-materialization-action").PathNodes.Add(CreatePathNode(50, PathNodeType.Via, -1)),
            };
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, trackWorld, TestContext.CancellationToken);
            PathRouteDiagnostic unreachableNode = resolution.Diagnostics.Single(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnreachableNode);

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.MaterializeResolvedPath(source, resolution, trackWorld);

            Assert.Contains(unreachableNode.SuggestedAction, result.FailureMessage);
        }

        [TestMethod]
        public void WhenErrorAndDisconnectedNodeExistThenSaveRetainsErrorDiagnosticPrecedence()
        {
            PathModel source = CreateLinearPath("disconnected-error") with
            {
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start | PathNodeType.Junction, 1),
                    CreatePathNode(100, PathNodeType.End, -1),
                    CreatePathNode(50, PathNodeType.Via, -1)),
            };

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.ValidateForPersistence(source,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());

            Assert.AreEqual(PathRouteDiagnosticCode.NoJunctionNode, result.HighestActionableDiagnostic?.Code);
        }

        [TestMethod]
        public async Task WhenAmbiguityAndDisconnectedNodeExistThenSavePreservesExistingBytes()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = new PathModel
            {
                Id = "disconnected-ambiguous-save",
                Name = "Disconnected Ambiguous Save",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(100, PathNodeType.Start, 1),
                    CreatePathNode(200, PathNodeType.End, -1) with { NodeIndex = 2 }),
            };
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] originalBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel source = persisted with
            {
                PathNodes = persisted.PathNodes.Add(CreatePathNode(125, PathNodeType.Via, -1)),
            };

            _ = await PathEditor.SaveValidatedPath(source, route, CreateAmbiguousRouteTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            byte[] savedBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(originalBytes, savedBytes);
        }

        [TestMethod]
        public void WhenAmbiguityAndDisconnectedNodeExistThenPreservationDiagnosticTakesPrecedence()
        {
            PathModel source = new PathModel
            {
                Id = "disconnected-ambiguous-diagnostic",
                Name = "Disconnected Ambiguous Diagnostic",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(100, PathNodeType.Start, 1),
                    CreatePathNode(200, PathNodeType.End, -1) with { NodeIndex = 2 },
                    CreatePathNode(125, PathNodeType.Via, -1)),
            };

            PathPersistenceValidationResult result = PathPersistenceValidationPolicy.ValidateForPersistence(source, CreateAmbiguousRouteTrackWorld());

            Assert.AreEqual(PathRouteDiagnosticCode.UnreachableNode, result.HighestActionableDiagnostic?.Code);
        }

        /// <summary>An authored junction cannot conceal an illegal turn and overwrite existing content.</summary>
        [TestMethod]
        public async Task WhenAuthoredJunctionContinuationIsIllegalThenSavePreservesExistingBytes()
        {
            RouteModel route = CreateRoute();
            TrackWorld world = CreateJunctionContinuityWorld(ConnectorType.InPin);
            PathModel persisted = new PathModel
            {
                Id = "junction-continuity-protected",
                Name = "Protected Junction Path",
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredPathNode(world, 1, PathNodeType.Start | PathNodeType.Junction, 1, -1),
                    CreateAnchoredPathNode(world, 3, PathNodeType.End | PathNodeType.Junction, -1, -1)),
            };
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] originalBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel invalid = persisted with
            {
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredPathNode(world, 1, PathNodeType.Start | PathNodeType.Junction, 1, -1),
                    CreateAnchoredPathNode(world, 3, PathNodeType.Via | PathNodeType.Junction, 2, -1),
                    CreateAnchoredPathNode(world, 2, PathNodeType.End | PathNodeType.Junction, -1, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route, world,
                TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(PathRouteDiagnosticCode.UnresolvedDenseSpan, result.HighestActionableDiagnostic?.Code);
            byte[] savedBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(originalBytes, savedBytes);
        }

        /// <summary>A legal authored junction continuation retains its complete graph after native save/reload.</summary>
        [TestMethod]
        public async Task WhenAuthoredJunctionContinuationIsLegalThenSavedRouteRemainsResolved()
        {
            RouteModel route = CreateRoute();
            TrackWorld world = CreateJunctionContinuityWorld(ConnectorType.OutPin);
            PathModel source = new PathModel
            {
                Id = "junction-continuity-round-trip",
                Name = "Legal Junction Path",
                Start = "Origin",
                End = "Destination",
                PlayerPath = true,
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredPathNode(world, 1, PathNodeType.Start | PathNodeType.Junction, 1, -1),
                    CreateAnchoredPathNode(world, 3, PathNodeType.Via | PathNodeType.Junction, 2, -1),
                    CreateAnchoredPathNode(world, 2, PathNodeType.End | PathNodeType.Junction, -1, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route, world,
                TestContext.CancellationToken).ConfigureAwait(false);
            PathModelHeader header = (await route.GetPaths(TestContext.CancellationToken).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await header.GetExtended(TestContext.CancellationToken).ConfigureAwait(false);
            PathRouteResolution resolution = PathRouteResolver.Resolve(reloaded, world, TestContext.CancellationToken);

            Assert.IsTrue(result.PersistenceAllowed);
            Assert.AreSequenceEqual(source.PathNodes, reloaded.PathNodes);
            Assert.AreEqual((source.Id, source.Name, source.Start, source.End, source.PlayerPath),
                (reloaded.Id, reloaded.Name, reloaded.Start, reloaded.End, reloaded.PlayerPath));
            Assert.IsTrue(resolution.IsValid && resolution.MainRoute.Spans.All(span => span.Status == PathRouteSpanStatus.Resolved));
        }

        [TestMethod]
        public async Task WhenFatalPathSaveIsBlockedThenNoContentIsPersisted()
        {
            RouteModel route = CreateRoute();
            PathModel invalid = CreateLinearPath("fatal-path") with
            {
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 4),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> paths = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.IsFalse(paths.Any(path => path.Id == invalid.Id));
        }

        [TestMethod]
        public async Task WhenPathHasSidingLinkBelowMinusOneThenSaveIsBlocked()
        {
            RouteModel route = CreateRoute();
            PathModel invalid = CreateLinearPath("invalid-siding-link") with
            {
                PathNodes = CreateLinearPath("source").PathNodes.SetItem(0,
                    CreateLinearPath("source").PathNodes[0] with { NextSidingNode = -2 }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsFalse(result.PersistenceAllowed);
            Assert.IsFalse((await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Any(path => path.Id == invalid.Id));
        }

        [TestMethod]
        public async Task WhenPassingBranchMatchesMainRouteThenSaveIsBlocked()
        {
            RouteModel route = CreateRoute();
            PathModel source = CreateLinearPath("passing-round-trip");
            PathModel branch = source with
            {
                PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NextSidingNode = 1 }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(branch, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            Assert.IsFalse(result.PersistenceAllowed);
            Assert.IsFalse((await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Any(path => path.Id == branch.Id));
        }

        [TestMethod]
        public async Task WhenGeneratedPassingPathIsReloadedThenRuntimeConsumerRetainsRejoiningBranch()
        {
            RouteModel route = CreateRoute();
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateRepresentativePassingPath("generated-passing-consumer", trackWorld);

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route, trackWorld,
                TestContext.CancellationToken).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            EditorTrainPath runtimePath = new EditorTrainPath(reloaded, trackWorld);
            TrainPathPointBase branchStart = runtimePath.PathPoints.Single(point => point.NextMainNode > -1 && point.NextSidingNode > -1);

            Assert.IsTrue(result.PersistenceAllowed && PassingBranchRejoinsMain(runtimePath.PathPoints, branchStart));
        }

        [TestMethod]
        public async Task WhenGeneratedMainPathIsReloadedThenResolverRetainsRepresentativeRoute()
        {
            RouteModel route = CreateRoute();
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathModel source = CreateLinearPath("generated-main-round-trip");

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route, trackWorld,
                TestContext.CancellationToken).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            PathRouteResolution resolution = PathRouteResolver.Resolve(reloaded, trackWorld, CancellationToken.None);

            Assert.IsTrue(result.PersistenceAllowed && resolution.IsValid,
                string.Join("; ", resolution.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}:{diagnostic.Code}:{diagnostic.Message}")));
        }

        [TestMethod]
        public async Task WhenGeneratedMainPathIsReloadedThenRuntimeConsumerRetainsMainConnectivity()
        {
            RouteModel route = CreateRoute();
            TrackWorld trackWorld = TrackWorldTestFixture.CreateDeadEndTrackWorld();
            PathModel source = CreateAnchoredEndpointPath("generated-main-consumer", trackWorld);

            _ = await PathEditor.SaveValidatedPath(source, route, trackWorld, TestContext.CancellationToken).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            EditorTrainPath runtimePath = new EditorTrainPath(reloaded, trackWorld);

            Assert.IsTrue(MainChainReachesEnd(runtimePath.PathPoints));
        }

        [TestMethod]
        public async Task WhenChangedPathIdIsSavedThenOriginalAndSaveAsPathsArePersistedSeparately()
        {
            RouteModel route = CreateRoute();
            PathModel original = CreateLinearPath("original-path") with { Name = "Original" };
            _ = await route.Save(original, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel saveAs = original with { Id = "copied-path", Name = "Copy" };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(saveAs, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);
            ImmutableArray<PathModelHeader> paths = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedOriginal = await paths.Single(path => path.Id == original.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedCopy = await paths.Single(path => path.Id == saveAs.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(result.PersistenceAllowed);
            Assert.AreEqual("Original", persistedOriginal.Name);
            Assert.AreEqual("Copy", persistedCopy.Name);
            Assert.AreSequenceEqual(original.PathNodes, persistedOriginal.PathNodes);
            Assert.AreSequenceEqual(saveAs.PathNodes, persistedCopy.PathNodes);
        }

        [TestMethod]
        public async Task WhenConfirmedSaveAsOverwritesExistingIdThenOnlyTheTargetPathIsReplaced()
        {
            RouteModel route = CreateRoute();
            PathModel original = CreateLinearPath("original-path") with { Name = "Original" };
            PathModel existingTarget = CreateLinearPath("target-path") with { Name = "Existing Target" };
            _ = await route.Save(original, TestContext.CancellationToken).ConfigureAwait(false);
            _ = await route.Save(existingTarget, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel replacement = original with { Id = existingTarget.Id, Name = "Replacement Target" };

            _ = await PathEditor.SaveValidatedPath(replacement, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);
            ImmutableArray<PathModelHeader> paths = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedOriginal = await paths.Single(path => path.Id == original.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedTarget = await paths.Single(path => path.Id == existingTarget.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("Original", persistedOriginal.Name);
            Assert.AreEqual("Replacement Target", persistedTarget.Name);
        }

        [TestMethod]
        public async Task WhenSaveNormalizationFailsThenExistingPersistedContentIsUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("normalization-protected-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel nonRejoiningPassingPath = new PathModel(persisted)
            {
                Name = "Unpersisted Replacement",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 1) with { NextSidingNode = 2 },
                    CreatePathNode(50, PathNodeType.Via, 3),
                    CreatePathNode(25, PathNodeType.Via, -1),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(nonRejoiningPassingPath, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), TestContext.CancellationToken).ConfigureAwait(false);

            PathModelHeader reloadedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == persisted.Id);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(persisted.Name, reloaded.Name);
            Assert.AreSequenceEqual(persisted.PathNodes, reloaded.PathNodes);
        }

        [TestMethod]
        public async Task WhenAtomicReplacementFailsThenExistingPersistedBytesAreUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("atomic-replacement-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] originalBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel replacement = persisted with { Name = "Replacement That Must Not Persist" };

            using (FileStream targetLock = new FileStream(targetFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await SaveWhileTargetIsLocked(route, replacement).ConfigureAwait(false);
            }

            byte[] persistedBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(originalBytes, persistedBytes);
        }

        [TestMethod]
        public async Task WhenAtomicReplacementFailsThenExistingPathRemainsReloadable()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("atomic-reload-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            PathModel replacement = persisted with { Name = "Replacement That Must Not Persist" };

            using (FileStream targetLock = new FileStream(targetFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await SaveWhileTargetIsLocked(route, replacement).ConfigureAwait(false);
            }

            PathModelHeader reloadedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == persisted.Id);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(persisted.Name, reloaded.Name);
        }

        [TestMethod]
        public async Task WhenAtomicReplacementFailsThenNoTemporaryFileRemains()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("atomic-cleanup-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            PathModel replacement = persisted with { Name = "Replacement That Must Not Persist" };

            using (FileStream targetLock = new FileStream(targetFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await SaveWhileTargetIsLocked(route, replacement).ConfigureAwait(false);
            }

            string[] temporaryFiles = Directory.GetFiles(Path.GetDirectoryName(targetFileName), $"{Path.GetFileName(targetFileName)}.*.tmp");
            Assert.IsEmpty(temporaryFiles);
        }

        [TestMethod]
        public async Task WhenPathUpdateIsCanceledThenExistingPersistedBytesAreUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("canceled-update-path");
            _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] originalBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            PathModel replacement = persisted with { Name = "Canceled Replacement" };
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

                _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await route.Save(replacement, cancellationTokenSource.Token).ConfigureAwait(false)).ConfigureAwait(false);
            }

            byte[] persistedBytes = await File.ReadAllBytesAsync(targetFileName, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreSequenceEqual(originalBytes, persistedBytes);
        }

        private async Task SaveWhileTargetIsLocked(RouteModel route, PathModel replacement)
        {
            try
            {
                _ = await route.Save(replacement, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.Fail("Saving over a locked target should fail.");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static async Task<RouteModelHeader> SeedRouteWithPathsAsync(CancellationToken cancellationToken)
        {
            RouteModel routeModel = CreateRoute();

            PathModel emptyPath = new PathModel() { Id = "empty-path", Name = "Empty Path" };
            PathModel linearPath = new PathModel()
            {
                Id = "linear-path",
                Name = "Linear Path",
                PathNodes = ImmutableArray.Create(
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero)) { NodeType = PathNodeType.Start, NextMainNode = 1 },
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero)) { NodeType = PathNodeType.End, NextMainNode = -1 }),
            };

            _ = await routeModel.Save(emptyPath, cancellationToken).ConfigureAwait(false);
            _ = await routeModel.Save(linearPath, cancellationToken).ConfigureAwait(false);
            return routeModel;
        }

        private static RouteModel CreateRoute()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);
            contentModel = contentModel with { ContentFolders = ImmutableArray.Create(folderModel) };
            contentModel.Initialize(null);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-{uniqueSuffix}",
                Name = "Validation Test Route",
                RouteKey = "ValidationTestKey",
            };
            routeModel.Initialize(folderModel);
            return routeModel;
        }

        private static PathModel CreateLinearPath(string id)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };
        }

        private static PathModel CreateRepresentativePassingPath(string id, TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredPathNode(trackWorld, 1, PathNodeType.Start, 1, 3),
                    CreateAnchoredPathNode(trackWorld, 4, PathNodeType.Via, 2, -1),
                    CreateAnchoredPathNode(trackWorld, 2, PathNodeType.End, -1, -1),
                    CreateAnchoredPathNode(trackWorld, 5, PathNodeType.Via, -1, 2)),
            };
        }

        private static bool MainChainReachesEnd(List<TrainPathPointBase> pathPoints)
        {
            int currentIndex = pathPoints.Select((point, index) => (point, index))
                .Single(item => item.point.NodeType.Includes(PathNodeType.Start)).index;
            HashSet<int> visited = new HashSet<int>();
            while (currentIndex >= 0 && currentIndex < pathPoints.Count && visited.Add(currentIndex))
            {
                TrainPathPointBase current = pathPoints[currentIndex];
                if (current.NodeType.Includes(PathNodeType.End))
                    return true;

                currentIndex = current.NextMainNode;
            }

            return false;
        }

        private static bool PassingBranchRejoinsMain(List<TrainPathPointBase> pathPoints, TrainPathPointBase branchStart)
        {
            HashSet<int> mainIndexes = new HashSet<int>();
            int mainIndex = pathPoints.Select((point, index) => (point, index)).Single(item => ReferenceEquals(item.point, branchStart)).index;
            while (mainIndex >= 0 && mainIndex < pathPoints.Count && mainIndexes.Add(mainIndex))
                mainIndex = pathPoints[mainIndex].NextMainNode;

            HashSet<int> branchIndexes = new HashSet<int>();
            int branchIndex = branchStart.NextSidingNode;
            while (branchIndex >= 0 && branchIndex < pathPoints.Count && branchIndexes.Add(branchIndex))
            {
                if (mainIndexes.Contains(branchIndex))
                    return true;

                branchIndex = pathPoints[branchIndex].NextSidingNode;
            }

            return false;
        }

        private static PathNode CreateAnchoredPathNode(TrackWorld trackWorld, int trackNodeIndex, PathNodeType nodeType,
            int nextMainNode, int nextSidingNode)
        {
            return new PathNode(trackWorld.TrackDatabase.TrackNodes[trackNodeIndex].Location)
            {
                NodeType = nodeType,
                NodeIndex = trackNodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
            };
        }

        private static PathModel CreateAnchoredEndpointPath(string id, TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                PathNodes = ImmutableArray.Create(
                    new PathNode(trackWorld.TrackDatabase.TrackNodes[1].Location)
                    {
                        NodeType = PathNodeType.Start,
                        NodeIndex = 1,
                        NextMainNode = 1,
                        NextSidingNode = -1,
                    },
                    new PathNode(trackWorld.TrackDatabase.TrackNodes[2].Location)
                    {
                        NodeType = PathNodeType.End,
                        NodeIndex = 2,
                        NextMainNode = -1,
                        NextSidingNode = -1,
                    }),
            };
        }

        private static PathNode CreatePathNode(float x, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeType = nodeType,
                NodeIndex = 1,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static TrackWorld CreateJunctionContinuityWorld(ConnectorType outgoingType)
        {
            TrackNodeBase[] nodes = new TrackNodeBase[] { null }.Concat(Enumerable.Range(1, 4).Select(index =>
                (TrackNodeBase)new JunctionNode(new WorldLocation(new Tile(1, 0), new Vector3(index * 100, 0, 0)),
                    new Tile(1, 0), Vector3.Zero) { NodeIndex = index })).ToArray();
            TrackNodeConnectorIndex junction = new TrackNodeConnectorIndex
            {
                NodeIndex = 3,
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnector { Link = 1, ConnectorType = ConnectorType.InPin },
                    new TrackNodeConnector { Link = 2, ConnectorType = outgoingType },
                    new TrackNodeConnector { Link = 4, ConnectorType = ConnectorType.OutPin }),
            };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = nodes.ToImmutableArray(),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3),
                    CreateConnectors(2, 3), junction, CreateConnectors(4, 3)),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, new TrackSectionModel());
        }

        private static TrackWorld CreateAmbiguousRouteTrackWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1, 100), CreateVectorNode(2, 200),
                    CreateJunctionNode(3), CreateJunctionNode(4)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4),
                    CreateConnectors(2, 3, 4), CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel { TrackDatabase = trackDatabase };

            return TrackWorld.Initialize(null, trackModel, new TrackSectionModel());
        }

        private static VectorNode CreateVectorNode(int nodeIndex, float startX)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(startX, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(startX + 100, 0, 0));
            return new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = nodeIndex,
            };
        }

        private static JunctionNode CreateJunctionNode(int nodeIndex)
        {
            return new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0)),
                new Tile(0, 0), Vector3.Zero)
            { NodeIndex = nodeIndex };
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params int[] linkedNodeIndexes)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = linkedNodeIndexes.Select(link => new TrackNodeConnector { Link = link }).ToImmutableArray(),
            };
        }

        public TestContext TestContext { get; set; }

        private static readonly int[] expectedArray48 = new[] { 4, 8 };
        private static readonly int[] expectedArray56 = new[] { 5, 6 };
    }
}
