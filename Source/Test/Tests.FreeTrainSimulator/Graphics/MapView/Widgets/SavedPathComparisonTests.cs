using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;
using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Graphics.MapView.Widgets
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class SavedPathComparisonTests
    {
        private TrackWorld previousWorld;

        public TestContext TestContext { get; set; }

        private static readonly int[] expected23134 = new[] { 2, 3, 1, 3, 4 };

        [TestInitialize]
        public void CaptureWorld() => previousWorld = TrackWorld.Instance;

        [TestCleanup]
        public void RestoreWorld() => GameService<TrackWorld>.Set(null, previousWorld);

        [TestMethod]
        [DataRow(4, 200f)]
        [DataRow(5, 400f)]
        public async Task WhenSavedParallelTraversalIsComparedThenSelectedGeometryAndClippedLengthArePreserved(int vector, float length)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackWorld world = TrackWorld.Initialize(null, fixture.Track, fixture.Sections);
            PathModel saved = await fixture.GenerateSelectedPath(vector, TestContext.CancellationToken).ConfigureAwait(false);
            RecordingRenderer renderer = new RecordingRenderer();

            SavedPathComparison comparison = SavedPathComparison.Create(saved, world, Color.Orange, TestContext.CancellationToken);
            comparison.Draw(renderer);

            Assert.AreEqual(length, renderer.Length, 0.01f);
            Assert.AreEqual((saved.Id, Color.Orange), (comparison.Id, comparison.Color));
            Assert.IsTrue(renderer.Lines.All(line => line.Color == Color.Orange));
            Vector2 bend = renderer.WorldToScreenCoordinates(PointD.FromWorldLocation(SyntheticPathFixture.Location(0, 100)));
            Assert.AreEqual(vector == 5, renderer.Lines.Any(line => Vector2.Distance(line.Point, bend) < 0.01f));
            Vector2 start = renderer.WorldToScreenCoordinates(PointD.FromWorldLocation(SyntheticPathFixture.Location(-50, 0)));
            Vector2 end = renderer.WorldToScreenCoordinates(PointD.FromWorldLocation(SyntheticPathFixture.Location(150, 0)));
            Assert.AreEqual(start.X, renderer.Lines.Min(line => line.Point.X), 0.01f);
            Assert.AreEqual(end.X, renderer.Lines.Max(line => line.Point.X + line.Length * (float)Math.Cos(line.Angle)), 0.01f);
        }

        [TestMethod]
        [DataRow(false, 500f)]
        [DataRow(true, 600f)]
        public async Task WhenSavedPassingGraphIsComparedThenWholeMainAndPassingRoutesAreClippedOnce(bool vectorBound, float length)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackWorld world = TrackWorld.Initialize(null, fixture.Track, fixture.Sections);
            PathModel saved = await fixture.GeneratePassingPath(vectorBound, TestContext.CancellationToken).ConfigureAwait(false);
            RecordingRenderer renderer = new RecordingRenderer();

            SavedPathComparison.Create(saved, world, Color.CornflowerBlue, TestContext.CancellationToken).Draw(renderer);

            // Vector-bound branches include the clipped approach and departure in their own traversal.
            Assert.AreEqual(length, renderer.Length, 0.01f);
            Assert.IsTrue(renderer.Lines.All(line => line.Color == Color.CornflowerBlue));
        }

        [TestMethod]
        [DataRow(90, false)]
        [DataRow(90, true)]
        [DataRow(-90, false)]
        [DataRow(-90, true)]
        public void WhenSavedCurvedSpanIsComparedThenArcCoordinatesAndClippingAreRetained(int angle, bool reverse)
        {
            Tile tile = new Tile(1, 0);
            WorldLocation origin = new WorldLocation(tile, Vector3.Zero);
            WorldLocation end = new WorldLocation(tile, new Vector3(Math.Sign(angle) * 100, 0, 100));
            VectorSectionNode section = new VectorSectionNode(origin, tile, Vector3.Zero, end) { NodeIndex = 1 };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new VectorNode(origin, tile, end) { NodeIndex = 1, VectorSections = ImmutableArray.Create(section) }),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty },
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackWorld world = TrackWorld.Create(new TrackModel { TrackDatabase = database }, new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection
                { SectionIndex = 1, Gauge = 1.435f, Curved = true, Radius = 100, Angle = angle, Length = (float)(100 * Math.PI / 2) }),
            });
            double fullLength = 100 * Math.PI / 2;
            WorldLocation first = world.ComputeSectionLocation(section, fullLength * (reverse ? 0.8 : 0.2));
            WorldLocation last = world.ComputeSectionLocation(section, fullLength * (reverse ? 0.2 : 0.8));
            PathModel model = new PathModel
            {
                Id = "arc",
                PathNodes = ImmutableArray.Create(
                    new PathNode(first) { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    new PathNode(last) { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };
            RecordingRenderer renderer = new RecordingRenderer();

            SavedPathComparison.Create(model, world, Color.Magenta, CancellationToken.None).Draw(renderer);

            Assert.IsEmpty(renderer.Lines);
            Assert.HasCount(1, renderer.Arcs);
            Assert.AreEqual((float)(fullLength * 0.6), renderer.Length, 0.01f);
            Assert.AreEqual((Color.Magenta, 100f), (renderer.Arcs[0].Color, renderer.Arcs[0].Radius));
            // Clipped track sections retain their physical orientation, even for a reverse traversal.
            WorldLocation physicalStart = world.ComputeSectionLocation(section, fullLength * 0.2);
            Assert.AreEqual(renderer.WorldToScreenCoordinates(PointD.FromWorldLocation(physicalStart)), renderer.Arcs[0].Point);
            Assert.AreEqual(Math.PI * 0.3, Math.Abs(renderer.Arcs[0].ArcSize), 0.001);
        }

        [TestMethod]
        public void WhenSavedPathIsUnresolvedThenComparisonRefusesInsteadOfDrawingFallback()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackWorld world = TrackWorld.Initialize(null, fixture.Track, fixture.Sections);
            PathModel model = new PathModel { Id = "unresolved", PathNodes = ImmutableArray<PathNode>.Empty };

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                SavedPathComparison.Create(model, world, Color.Orange, CancellationToken.None));

            Assert.AreEqual(SavedPathComparison.UnresolvedMessage, error.Message);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void WhenSavedPathContainsNullNodeThenComparisonReportsUnresolvedInsteadOfEscaping(int nullIndex)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackWorld world = TrackWorld.Initialize(null, fixture.Track, fixture.Sections);
            PathModel model = new PathModel
            {
                Id = "null-node",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1),
                    SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, -1, -1)).SetItem(nullIndex, null),
            };

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                SavedPathComparison.Create(model, world, Color.Orange, CancellationToken.None));

            Assert.AreEqual(SavedPathComparison.UnresolvedMessage, error.Message);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenSavedVectorSelfIntersectsThenResolvedOccurrencesAndIntermediateSectionsArePreserved(bool reverse)
        {
            ImmutableArray<VectorSectionNode> sections = ImmutableArray.Create(
                ComparisonVectorSection(0, 0, 100, 0, 1),
                ComparisonVectorSection(100, 0, 0, 100, 2),
                ComparisonVectorSection(0, 100, 0, -100, 3));
            TrackWorld world = CreateComparisonWorld(sections);
            WorldLocation crossing = SyntheticPathFixture.Location(0, 0);
            WorldLocation end = SyntheticPathFixture.Location(0, -50);
            PathModel model = new PathModel
            {
                Id = "self-intersection",
                PathNodes = ImmutableArray.Create(
                    new PathNode(reverse ? end : crossing) { NodeType = PathNodeType.Start, NodeIndex = 1, NextMainNode = 1, NextSidingNode = -1 },
                    new PathNode(reverse ? crossing : end) { NodeType = PathNodeType.End, NodeIndex = 1, NextMainNode = -1, NextSidingNode = -1 }),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, CancellationToken.None);
            RecordingRenderer renderer = new RecordingRenderer();

            SavedPathComparison.Create(model, world, Color.Orange, CancellationToken.None).Draw(renderer);

            Assert.AreEqual(reverse ? (2, 0) : (0, 2),
                (resolution.AuthoredNodeAnchors[0].TrackVectorSectionIndex, resolution.AuthoredNodeAnchors[1].TrackVectorSectionIndex));
            Assert.HasCount(3, renderer.Lines);
            Assert.AreEqual(250f + (float)Math.Sqrt(20000), renderer.Length, 0.01f);
            Assert.AreEqual(renderer.WorldToScreenCoordinates(PointD.FromWorldLocation(sections[1].Location)), renderer.Lines[1].Point);
            Assert.AreEqual(reverse ? 150f : 100f, renderer.Lines[0].Length, 0.01f);
        }

        [TestMethod]
        public void WhenSelectedLoopRepeatsJunctionPinsThenComparisonRefusesInsteadOfDrawingAnEmptyLoop()
        {
            ImmutableArray<VectorSectionNode> sections = ImmutableArray.Create(
                ComparisonVectorSection(0, 0, 100, 0, 1), ComparisonVectorSection(100, 0, 100, 100, 1),
                ComparisonVectorSection(100, 100, 0, 100, 1), ComparisonVectorSection(0, 100, 0, 0, 1));
            VectorNode loop = new VectorNode(sections[0].Location, sections[0].Tile, sections[^1].EndLocation)
            { NodeIndex = 1, VectorSections = sections };
            VectorSectionNode approach = ComparisonVectorSection(-100, 0, 0, 0, 1);
            VectorSectionNode exit = ComparisonVectorSection(0, 0, 0, -100, 1);
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, loop,
                    new VectorNode(approach.Location, approach.Tile, approach.EndLocation) { NodeIndex = 2, VectorSections = ImmutableArray.Create(approach) },
                    new global::FreeTrainSimulator.Models.Track.JunctionNode(sections[0].Location, sections[0].Tile, Vector3.Zero) { NodeIndex = 3 },
                    new VectorNode(exit.Location, exit.Tile, exit.EndLocation) { NodeIndex = 4, VectorSections = ImmutableArray.Create(exit) }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    ComparisonPins(1, 1, (3, ConnectorType.InPin, TrackDirection.Reverse), (3, ConnectorType.InPin, TrackDirection.Ahead)),
                    ComparisonPins(2, 1, (3, ConnectorType.InPin, TrackDirection.Reverse)),
                    ComparisonPins(3, 1, (2, ConnectorType.InPin, TrackDirection.Reverse), (1, ConnectorType.OutPin, TrackDirection.Reverse),
                        (1, ConnectorType.OutPin, TrackDirection.Ahead), (4, ConnectorType.InPin, TrackDirection.Reverse)),
                    ComparisonPins(4, 1, (3, ConnectorType.InPin, TrackDirection.Reverse))),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackWorld world = TrackWorld.Create(new TrackModel { TrackDatabase = database }, new TrackSectionModel
            { TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 }) });
            PathModel model = new PathModel
            {
                Id = "loop",
                PathNodes = ImmutableArray.Create(
                    SyntheticPathFixture.Node(PathNodeType.Start, 2, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 4, 0, -50, -1, -1)),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, CancellationToken.None);

            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                SavedPathComparison.Create(model, world, Color.Orange, CancellationToken.None));

            Assert.IsTrue(resolution.IsValid);
            Assert.AreSequenceEqual(expected23134, resolution.MainRoute.Spans[0].Candidates[0].RouteNodeIndexes);
            Assert.AreEqual(SavedPathComparison.MissingGeometryMessage, error.Message);
        }

        private static VectorSectionNode ComparisonVectorSection(float x, float z, float endX, float endZ, int sectionId)
            => new VectorSectionNode(SyntheticPathFixture.Location(x, z), new Tile(1, 1),
                new Vector3(0, (float)Math.Atan2(endX - x, endZ - z), 0), SyntheticPathFixture.Location(endX, endZ)) { NodeIndex = sectionId };

        private static TrackWorld CreateComparisonWorld(ImmutableArray<VectorSectionNode> sections)
        {
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new VectorNode(sections[0].Location, new Tile(1, 1), sections[^1].EndLocation) { NodeIndex = 1, VectorSections = sections }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return TrackWorld.Create(new TrackModel { TrackDatabase = database }, new TrackSectionModel
            {
                TrackSections = sections.ToImmutableDictionary(section => section.NodeIndex, section => new TrackSection
                { SectionIndex = section.NodeIndex, Gauge = 1.435f, Length = (float)Math.Sqrt(WorldLocation.GetDistanceSquared2D(section.Location, section.EndLocation)) }),
            });
        }

        private static TrackNodeConnectorIndex ComparisonPins(int node, int inboundCount, params (int Link, ConnectorType Type, TrackDirection Direction)[] pins)
            => new TrackNodeConnectorIndex
            {
                NodeIndex = node,
                InboundCount = inboundCount,
                TrackNodeConnectors = pins.Select(pin => new TrackNodeConnector { Link = pin.Link, ConnectorType = pin.Type, Direction = pin.Direction }).ToImmutableArray(),
            };

        [TestMethod]
        public async Task WhenComparisonPreparationIsCanceledThenItDoesNotReturnAnOverlay()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackWorld world = TrackWorld.Initialize(null, fixture.Track, fixture.Sections);
            PathModel saved = await fixture.GenerateSelectedPath(4, TestContext.CancellationToken).ConfigureAwait(false);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                await cancellation.CancelAsync().ConfigureAwait(false);

                _ = Assert.ThrowsExactly<OperationCanceledException>(() =>
                    SavedPathComparison.Create(saved, world, Color.Orange, cancellation.Token));
            }
        }

        internal sealed class RecordingRenderer : IMapRenderer
        {
            internal List<(float Width, Color Color, Vector2 Point, float Length, double Angle)> Lines { get; } = new();
            internal List<(float Width, Color Color, Vector2 Point, float Radius, double Angle, double ArcSize)> Arcs { get; } = new();
            internal float Length => Lines.Sum(line => line.Length) + Arcs.Sum(arc => arc.Radius * (float)Math.Abs(arc.ArcSize));
            public double Scale => 1;
            public System.Drawing.Font CurrentFont => null;
            public System.Drawing.Font ConstantSizeFont => null;
            public Vector2 WorldToScreenCoordinates(in WorldLocation location) => WorldToScreenCoordinates(PointD.FromWorldLocation(location));
            public Vector2 WorldToScreenCoordinates(in PointD location) => new Vector2((float)location.X, (float)location.Y);
            public float WorldToScreenSize(double size, int minScreenSize) => (float)size;
            public void DrawLine(float width, Color color, Vector2 point, float length, double angle) => Lines.Add((width, color, point, length, angle));
            public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize) => Arcs.Add((width, color, point, radius, angle, arcSize));
            public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2) => throw new AssertFailedException("Comparison must retain track sections.");
            public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2) => throw new AssertFailedException("Comparison must not draw editor previews.");
            public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight) => throw new AssertFailedException("Comparison must not draw editor handles.");
            public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, bool flipHorizontal, bool flipVertical) => throw new AssertFailedException("Comparison must not draw editor handles.");
            public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color) => throw new AssertFailedException("Comparison must not draw editor handles.");
            public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions) => throw new AssertFailedException("Comparison must not draw editor labels.");
        }
    }
}
