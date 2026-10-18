using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

using EditorTrainPath = FreeTrainSimulator.Graphics.MapView.Widgets.EditorTrainPath;
using JunctionWidget = FreeTrainSimulator.Graphics.MapView.Widgets.JunctionNode;
using TrackSegment = FreeTrainSimulator.Graphics.MapView.Widgets.TrackSegment;

namespace Tests.FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>Exercises correlated main-route candidates through the production editor interaction flow.</summary>
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class PathEditorCorrelatedCandidateTests : IDisposable
    {
        private readonly TrackWorld previousTrackWorld = TrackWorld.Instance;
        private readonly RuntimeDataResolver previousResolver = RuntimeDataResolver.Instance;

        /// <summary>Provides cancellation for asynchronous path initialization.</summary>
        public TestContext TestContext { get; set; }

        private static readonly int[] expectedArray124 = new int[] { 1, 2, 4 };
        private static readonly int[] expectedArray134 = new int[] { 1, 3, 4 };
        private static readonly int[] expectedArray468 = new int[] { 4, 6, 8 };
        private static readonly int[] expectedArray458 = new int[] { 4, 5, 8 };

        /// <summary>Restores process-wide services after each case, including failed fixture setup.</summary>
        public void Dispose()
        {
            GameService<RuntimeDataResolver>.Set(null, previousResolver);
            GameService<TrackWorld>.Set(null, previousTrackWorld);
            GC.SuppressFinalize(this);
        }

        /// <summary>Previewing either upstream choice lets the resolver choose its compatible, unselected suffix.</summary>
        [TestMethod]
        [DataRow(0, new int[] { 1, 2, 4, 6, 8 })]
        [DataRow(1, new int[] { 1, 3, 4, 5, 8 })]
        public async Task WhenUpstreamCandidateIsPreviewedThenUnselectedSuffixIsCoherent(int candidateIndex, int[] expectedRoute)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            TestPathEditorContext context = new TestPathEditorContext(trackWorld);
            using (PathEditor editor = new PathEditor(context))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);

                PathEditResult preview = editor.PreviewPendingRouteCandidate(0, candidateIndex);

                Assert.IsTrue(preview.Success, preview.Message);
                AssertJunctionRoute(expectedRoute, context.LastRenderedPathModel.PathNodes.Select(node => node.NodeIndex));
                Assert.AreEqual(0, editor.PreviewedRouteCandidateFromNodeIndex);
                Assert.AreEqual(candidateIndex, editor.PreviewedRouteCandidateIndex);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                AssertSourceUnchanged(editor, source);
                editor.CancelPendingRouteCandidate();
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>A coherent automatic suffix preview does not count as an explicit downstream selection.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public async Task WhenOnlyUpstreamCandidateIsSelectedThenAcceptanceRequiresDownstreamSelection(int candidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult preview = editor.PreviewPendingRouteCandidate(0, candidateIndex);
                Assert.IsTrue(preview.Success, preview.Message);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(0, candidateIndex);

                Assert.IsFalse(acceptance.Success);
                Assert.Contains("Select a route candidate for the span starting at node 1.", acceptance.Message);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                AssertSourceUnchanged(editor, source);
                editor.CancelPendingRouteCandidate();
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Explicitly compatible upstream and downstream choices commit exactly the selected chain.</summary>
        [TestMethod]
        [DataRow(0, 0, new int[] { 1, 2, 4, 6, 8 })]
        [DataRow(1, 1, new int[] { 1, 3, 4, 5, 8 })]
        public async Task WhenCompatibleCandidatesAreSelectedThenAcceptanceCommitsExactRoute(int upstreamCandidateIndex,
            int downstreamCandidateIndex, int[] expectedRoute)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult upstream = editor.PreviewPendingRouteCandidate(0, upstreamCandidateIndex);
                Assert.IsTrue(upstream.Success, upstream.Message);
                PathEditResult downstream = editor.PreviewPendingRouteCandidate(1, downstreamCandidateIndex);
                Assert.IsTrue(downstream.Success, downstream.Message);
                AssertSourceUnchanged(editor, source);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, downstreamCandidateIndex);

                Assert.IsTrue(acceptance.Success, acceptance.Message);
                AssertJunctionRoute(expectedRoute, editor.TryCaptureCurrentPathModel().PathNodes.Select(node => node.NodeIndex));
                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsTrue(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.CanUndo);
                Assert.IsFalse(editor.CanRedo);
            }
        }

        /// <summary>Accepting both correlated selections records only one undo snapshot of the original sparse path.</summary>
        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(1, 1)]
        public async Task WhenCompatibleCandidatesAreAcceptedThenOneUndoRestoresSource(int upstreamCandidateIndex,
            int downstreamCandidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult upstream = editor.PreviewPendingRouteCandidate(0, upstreamCandidateIndex);
                Assert.IsTrue(upstream.Success, upstream.Message);
                PathEditResult downstream = editor.PreviewPendingRouteCandidate(1, downstreamCandidateIndex);
                Assert.IsTrue(downstream.Success, downstream.Message);
                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, downstreamCandidateIndex);
                Assert.IsTrue(acceptance.Success, acceptance.Message);

                Assert.IsTrue(editor.Undo());

                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual(source.Id, editor.TryCaptureCurrentPathModel().Id);
                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsTrue(editor.CanRedo);
            }
        }

        /// <summary>Incompatible explicit choices are refused without changing the committed model or history.</summary>
        [TestMethod]
        [DataRow(0, 1)]
        [DataRow(1, 0)]
        public async Task WhenExplicitCandidatesAreIncompatibleThenAcceptanceLeavesSourceUnchanged(int upstreamCandidateIndex,
            int downstreamCandidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult upstream = editor.PreviewPendingRouteCandidate(0, upstreamCandidateIndex);
                Assert.IsTrue(upstream.Success, upstream.Message);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, downstreamCandidateIndex);

                Assert.IsFalse(acceptance.Success, $"{acceptance.Message} Committed junction route: " +
                    string.Join("->", editor.TryCaptureCurrentPathModel().PathNodes
                        .Where(node => node.NodeIndex is >= 1 and <= 8).Select(node => node.NodeIndex)));
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                AssertSourceUnchanged(editor, source);
                editor.CancelPendingRouteCandidate();
                AssertSourceUnchanged(editor, source);
            }
        }

        private static void BeginPendingCandidates(PathEditor editor, TrackWorld trackWorld)
        {
            Assert.IsFalse(editor.IsRepairMode);
            PathEditorCommandResult insertion = editor.AddViaPointHereCommand(0,
                CreateAnchor(trackWorld, 4, PathNodeType.Via, -1), true);

            Assert.IsFalse(insertion.Success);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit, insertion.Message);
            Assert.HasCount(2, editor.PendingAmbiguousSpans);
            ResolvedPathSpan upstream = editor.PendingAmbiguousSpans[0];
            ResolvedPathSpan downstream = editor.PendingAmbiguousSpans[1];
            Assert.AreEqual(0, upstream.FromNodeIndex);
            Assert.AreEqual(1, downstream.FromNodeIndex);
            Assert.AreEqual(PathRouteSpanStatus.Ambiguous, upstream.Status);
            Assert.AreEqual(PathRouteSpanStatus.Ambiguous, downstream.Status);
            Assert.HasCount(2, upstream.Candidates);
            Assert.HasCount(2, downstream.Candidates);
            AssertJunctionRoute(expectedArray124, upstream.Candidates[0].RouteNodeIndexes);
            AssertJunctionRoute(expectedArray134, upstream.Candidates[1].RouteNodeIndexes);
            AssertJunctionRoute(expectedArray468, downstream.Candidates[0].RouteNodeIndexes);
            AssertJunctionRoute(expectedArray458, downstream.Candidates[1].RouteNodeIndexes);
        }

        private static void AssertJunctionRoute(int[] expectedRoute, IEnumerable<int> route)
        {
            // Only IDs 1..8 are junctions; inserted vectors and generated direction witnesses are not detours.
            Assert.AreSequenceEqual(expectedRoute, route.Where(index => index is >= 1 and <= 8).ToArray());
        }

        private static void AssertSourceUnchanged(PathEditor editor, PathModel source)
        {
            Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
            Assert.IsFalse(editor.HasUnsavedChanges);
            Assert.IsFalse(editor.CanUndo);
            Assert.IsFalse(editor.CanRedo);
        }

        private static PathModel CreateSourcePath(TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = "correlated-candidates",
                Name = "Correlated candidates",
                PathNodes = ImmutableArray.Create(
                    CreateAnchor(trackWorld, 1, PathNodeType.Start, 1),
                    CreateAnchor(trackWorld, 8, PathNodeType.End, -1)),
            };
        }

        private static PathNode CreateAnchor(TrackWorld trackWorld, int trackNodeIndex, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(trackWorld.TrackDatabase.TrackNodes[trackNodeIndex].Location)
            {
                NodeIndex = trackNodeIndex,
                NodeType = nodeType | PathNodeType.Junction,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static TrackWorld CreateCorrelatedTrackWorld()
        {
            TrackNodeConnectorIndex[] connectors = new TrackNodeConnectorIndex[]
            {
                new TrackNodeConnectorIndex(),
                CreateConnectors(1, (2, ConnectorType.OutPin), (3, ConnectorType.OutPin), (7, ConnectorType.InPin)),
                CreateConnectors(2, (1, ConnectorType.InPin), (4, ConnectorType.OutPin)),
                CreateConnectors(3, (1, ConnectorType.InPin), (4, ConnectorType.OutPin)),
                CreateConnectors(4, (2, ConnectorType.InPin), (3, ConnectorType.OutPin), (5, ConnectorType.InPin), (6, ConnectorType.OutPin)),
                CreateConnectors(5, (4, ConnectorType.OutPin), (8, ConnectorType.InPin)),
                CreateConnectors(6, (4, ConnectorType.InPin), (8, ConnectorType.OutPin)),
                CreateConnectors(7, (1, ConnectorType.OutPin)),
                CreateConnectors(8, (5, ConnectorType.OutPin), (6, ConnectorType.InPin)),
            };
            Tile tile = new Tile(1, 0);
            List<TrackNodeBase> nodes = new TrackNodeBase[] { null }.Concat(Enumerable.Range(1, 8).Select(index =>
                (TrackNodeBase)new JunctionNode(new WorldLocation(tile, new Vector3(index * 100, 0, 0)),
                    tile, Vector3.Zero) { NodeIndex = index })).ToList();
            List<TrackNodeConnectorIndex> vectorConnectors = connectors.ToList();
            ImmutableDictionary<int, TrackSection>.Builder sectionDefinitions = ImmutableDictionary.CreateBuilder<int, TrackSection>();

            // Keep every original junction pin side, inserting one real vector per undirected edge.
            // ponytail: Collinear, overlapping tracks suffice for candidate rendering, not pointer/snap disambiguation;
            // use spatially separated branches if this fixture grows to cover map interaction.
            for (int startIndex = 1; startIndex <= 8; startIndex++)
            {
                foreach (TrackNodeConnector connector in connectors[startIndex].TrackNodeConnectors.Where(pin => pin.Link > startIndex))
                {
                    int endIndex = connector.Link;
                    int vectorIndex = nodes.Count;
                    WorldLocation start = nodes[startIndex].Location;
                    WorldLocation end = nodes[endIndex].Location;
                    VectorSectionNode section = new VectorSectionNode(start, tile, new Vector3(0, MathHelper.PiOver2, 0), end)
                    {
                        NodeIndex = vectorIndex,
                    };
                    nodes.Add(new VectorNode(start, tile, end)
                    {
                        NodeIndex = vectorIndex,
                        VectorSections = ImmutableArray.Create(section),
                    });
                    sectionDefinitions.Add(vectorIndex, new TrackSection
                    {
                        SectionIndex = vectorIndex,
                        Gauge = 1.435f,
                        Length = (endIndex - startIndex) * 100,
                    });
                    // Vector pins are ordered start/finish. Their directions describe the linked junctions.
                    vectorConnectors.Add(CreateConnectors(vectorIndex, (startIndex, ConnectorType.InPin), (endIndex, ConnectorType.OutPin)));
                    vectorConnectors[startIndex] = vectorConnectors[startIndex] with
                    {
                        TrackNodeConnectors = vectorConnectors[startIndex].TrackNodeConnectors.Select(pin => pin.Link == endIndex
                            ? pin with { Link = vectorIndex, Direction = TrackDirection.Reverse } : pin).ToImmutableArray(),
                    };
                    vectorConnectors[endIndex] = vectorConnectors[endIndex] with
                    {
                        TrackNodeConnectors = vectorConnectors[endIndex].TrackNodeConnectors.Select(pin => pin.Link == startIndex
                            ? pin with { Link = vectorIndex, Direction = TrackDirection.Ahead } : pin).ToImmutableArray(),
                    };
                }
            }

            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = nodes.ToImmutableArray(),
                TrackNodeConnectors = vectorConnectors.ToImmutableArray(),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackSectionModel sections = new TrackSectionModel { TrackSections = sectionDefinitions.ToImmutable() };
            TrackWorld world = TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, sections);
            GameService<RuntimeDataResolver>.Set(null, new TestRuntimeDataResolver(sections, world));
            world.SetSegmentSections(database.VectorNodes.Select(node => new TrackSegmentSection(node.NodeIndex,
                node.VectorSections.Select((section, index) => new TrackSegment(section, node.NodeIndex, index)))));
            world.SetJunctions(database.JunctionNodes.Select(node => new JunctionWidget(node,
                database.TrackNodeConnectors[node.NodeIndex].OutConnectors[node.MainRoute].Link)));
            return world;
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params (int Link, ConnectorType Type)[] connectors)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = connectors.Select(connector => new TrackNodeConnector
                {
                    Link = connector.Link,
                    ConnectorType = connector.Type,
                }).ToImmutableArray(),
            };
        }

        private sealed class TestRuntimeDataResolver : RuntimeDataResolver
        {
            public TestRuntimeDataResolver(TrackSectionModel sections, TrackWorld world)
                : base(null, sections, null, world, true, null)
            {
            }
        }

        private sealed class TestPathEditorContext : IPathEditorContext, IPathEditorContextServicesAccessor, IPathEditorServices
        {
            private readonly PathEditorServices services;

            /// <summary>Captures the latest model passed to the production editor rendering service, including previews.</summary>
            public PathModel LastRenderedPathModel { get; private set; }

            /// <summary>No map renderer is needed to exercise candidate state.</summary>
            public IMapRenderer Renderer => null;

            /// <summary>No viewport is needed to exercise candidate state.</summary>
            public IMapViewport Viewport => null;

            /// <summary>Retains the content mode requested by the editor.</summary>
            public ToolboxContentMode ContentMode { get; set; }

            /// <summary>Retains the active editor registered by its base constructor.</summary>
            public PathEditorBase PathEditor { get; set; }

            IPathEditorServices IPathEditorContextServicesAccessor.Services => this;

            TrackWorld IPathEditorServices.TrackWorld => services.TrackWorld;

            /// <summary>Uses the production rendering service with the synthetic initialized track world.</summary>
            public TestPathEditorContext(TrackWorld trackWorld)
            {
                services = new PathEditorServices(trackWorld);
            }

            EditorTrainPath IPathEditorServices.CreateEditorTrainPath(PathModel pathModel)
            {
                LastRenderedPathModel = pathModel;
                return services.CreateEditorTrainPath(pathModel);
            }

            /// <summary>Candidate-state tests do not need to draw the map.</summary>
            public void RequestRedraw()
            {
            }
        }
    }
}
