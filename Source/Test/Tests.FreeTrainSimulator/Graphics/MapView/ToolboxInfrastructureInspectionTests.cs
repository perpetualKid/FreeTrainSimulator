using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

using ModelItem = FreeTrainSimulator.Models.Track.TrackItemBase;
using ModelEndNode = FreeTrainSimulator.Models.Track.EndNode;
using ModelJunctionNode = FreeTrainSimulator.Models.Track.JunctionNode;
using FreeTrainSimulator.Common.DebugInfo;

namespace Tests.FreeTrainSimulator.Graphics.MapView
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class ToolboxInfrastructureInspectionTests
    {
        [TestMethod]
        [DataRow(MapContentType.Crossovers, 0)]
        [DataRow(MapContentType.LevelCrossings, 1)]
        [DataRow(MapContentType.RoadCrossings, 2)]
        public async Task CrossingLayersContainOnlyTheirSourceWidgets(MapContentType layer, int locationIndex)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                WorldLocation[] locations = ToolboxInspectionFixture.CrossingLocations();

                Assert.AreSequenceEqual(new[] { PointD.FromWorldLocation(locations[locationIndex]) }, fixture.Items[layer].Cast<PointPrimitive>().Select(item => item.Location).ToArray());
            }
        }

        [TestMethod]
        [DataRow(MapContentType.Crossovers, 0)]
        [DataRow(MapContentType.LevelCrossings, 1)]
        [DataRow(MapContentType.RoadCrossings, 2)]
        public async Task VisibilityProjectsOnlyTheEnabledCrossingLayer(MapContentType layer, int locationIndex)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                WorldLocation[] locations = ToolboxInspectionFixture.CrossingLocations();
                fixture.Content.UpdateItemVisiblity(layer, true);

                fixture.Draw();

                Assert.AreSequenceEqual(new[] { fixture.Renderer.WorldToScreenCoordinates(locations[locationIndex]) }, fixture.Drawing.TexturePositions.ToArray());
            }
        }

        [TestMethod]
        [DataRow(MapContentType.Crossovers)]
        [DataRow(MapContentType.LevelCrossings)]
        [DataRow(MapContentType.RoadCrossings)]
        public async Task DisablingCrossingLayerRemovesItsProjection(MapContentType layer)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                fixture.Content.UpdateItemVisiblity(layer, true);
                fixture.Draw();
                fixture.Drawing.TexturePositions.Clear();

                fixture.Content.UpdateItemVisiblity(layer, false);
                fixture.Draw();

                Assert.IsEmpty(fixture.Drawing.TexturePositions);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task VectorNavigationCentersAndFitsChosenDatabase(bool road)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                TrackSegmentSection section = fixture.GetSegmentSection(1, road);

                fixture.Content.NavigateToTrackNode(1, road);

                Assert.AreEqual((section.MidPoint, section.TopLeftBound, section.BottomRightBound, 1),
                    (fixture.Camera.State.CenterPoint, fixture.Camera.FitTopLeft, fixture.Camera.FitBottomRight, fixture.Camera.FitCount));
            }
        }

        [TestMethod]
        [DataRow(1, false, MapContentType.Tracks)]
        [DataRow(1, true, MapContentType.Roads)]
        [DataRow(2, false, MapContentType.JunctionNodes)]
        [DataRow(3, false, MapContentType.EndNodes)]
        [DataRow(2, true, MapContentType.RoadEndNodes)]
        [DataRow(6, false, MapContentType.EndNodes)]
        [DataRow(6, true, MapContentType.RoadEndNodes)]
        public async Task NavigationHighlightsActualWidgetInChosenLayer(int index, bool road, MapContentType layer)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                ITileCoordinate widget = fixture.Items[layer].Single(item => NodeIndex(item) == index);

                fixture.Content.NavigateToTrackNode(index, road);

                Assert.AreSame(widget, fixture.Highlights[layer]);
            }
        }

        [TestMethod]
        [DataRow(2, false)]
        [DataRow(3, false)]
        [DataRow(2, true)]
        [DataRow(3, true)]
        [DataRow(6, false)]
        [DataRow(6, true)]
        public async Task PointNodeNavigationCentersWithoutVectorGeometry(int index, bool road)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                TrackNodeBase node = fixture.GetTrackNode(index, road);

                fixture.Content.NavigateToTrackNode(index, road);

                Assert.AreEqual((PointD.FromWorldLocation(node.Location), 0), (fixture.Camera.State.CenterPoint, fixture.Camera.FitCount));
            }
        }

        [TestMethod]
        [DataRow(1, false, "Track Node", "Vector Section", "1", null, null)]
        [DataRow(1, true, "Road Node", "Vector Section", "1", null, null)]
        [DataRow(2, false, "Track Node", "Junction", null, "1", "17")]
        [DataRow(3, true, "Road Node", "Junction", null, "1", "29")]
        [DataRow(3, false, "Track Node", "End Node", null, null, null)]
        [DataRow(2, true, "Road Node", "End Node", null, null, null)]
        [DataRow(6, false, "Track Node", "End Node", null, null, null)]
        [DataRow(6, true, "Road Node", "End Node", null, null, null)]
        public async Task NavigationPinsDatabaseAndSubtypeDetails(int index, bool road, string itemType, string nodeType, string segments, string mainRoute, string shape)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                TrackNodeBase node = fixture.GetTrackNode(index, road);

                fixture.Content.NavigateToTrackNode(index, road);
                InformationDictionary details = fixture.Content.PinnedNavigationInfo.DetailInfo;

                Assert.AreEqual((itemType, nodeType, index.ToString(System.Globalization.CultureInfo.InvariantCulture), node.Location.ToString(), segments, mainRoute, shape),
                    (details["Item Type"], details["Node Type"], details["Node Index"], details["Location"], details["Segments"], details["Main Route"], details["Shape Index"]));
            }
        }

        [TestMethod]
        public async Task RoadJunctionDoesNotCreateARailHighlight()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                fixture.Content.NavigateToTrackNode(3, true);

                Assert.IsTrue(fixture.Highlights.All(item => item == null));
            }
        }

        [TestMethod]
        public async Task PinnedNodeDetailsRemainStableAfterAnotherSelection()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                fixture.Content.NavigateToTrackNode(2, false);
                var previous = fixture.Content.PinnedNavigationInfo.DetailInfo;
                KeyValuePair<string, string>[] expected = previous.ToArray();

                fixture.Content.NavigateToTrackNode(2, true);

                Assert.AreSequenceEqual(expected, previous.ToArray());
            }
        }

        [TestMethod]
        [DataRow(-1, false)]
        [DataRow(-1, true)]
        [DataRow(0, false)]
        [DataRow(0, true)]
        [DataRow(4, false)]
        [DataRow(4, true)]
        [DataRow(5, false)]
        [DataRow(5, true)]
        [DataRow(7, false)]
        [DataRow(7, true)]
        [DataRow(int.MaxValue, false)]
        [DataRow(int.MaxValue, true)]
        public async Task InvalidOrEmptyNodeLeavesNavigationUnchanged(int index, bool road)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                fixture.Content.NavigateToTrackNode(1, false);
                var previous = fixture.Content.PinnedNavigationInfo.DetailInfo;
                PointD center = fixture.Camera.State.CenterPoint;
                ITileCoordinate selected = fixture.Highlights[MapContentType.Tracks];

                fixture.Content.NavigateToTrackNode(index, road);

                Assert.AreEqual((center, 1, true, true), (fixture.Camera.State.CenterPoint, fixture.Camera.FitCount,
                    ReferenceEquals(previous, fixture.Content.PinnedNavigationInfo.DetailInfo), ReferenceEquals(selected, fixture.Highlights[MapContentType.Tracks])));
            }
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(0, true)]
        [DataRow(1, false)]
        public async Task TrackItemNavigationCentersOnItemLocationWithoutFitting(int index, bool road)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                ModelItem item = fixture.GetTrackItem(index, road);

                fixture.Content.NavigateToTrackItem(index, road);

                Assert.AreEqual((PointD.FromWorldLocation(item.Location), 0), (fixture.Camera.State.CenterPoint, fixture.Camera.FitCount));
            }
        }

        [TestMethod]
        [DataRow(0, false, "Track Item")]
        [DataRow(0, true, "Road Item")]
        [DataRow(1, false, "Track Item")]
        public async Task TrackItemNavigationPinsDatabaseAndIndexDetails(int index, bool road, string itemType)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                ModelItem item = fixture.GetTrackItem(index, road);

                fixture.Content.NavigateToTrackItem(index, road);
                InformationDictionary details = fixture.Content.PinnedNavigationInfo.DetailInfo;

                Assert.AreEqual((itemType, index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.NodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), item.Location.ToString()),
                    (details["Item Type"], details["Item Index"], details["Node Index"], details["Location"]));
            }
        }

        [TestMethod]
        public async Task TrackItemSearchDistinguishesOverlappingIndexByDatabase()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                fixture.Content.NavigateToTrackItem(0, false);
                KeyValuePair<string, string>[] rail = fixture.Content.PinnedNavigationInfo.DetailInfo.ToArray();

                fixture.Content.NavigateToTrackItem(0, true);
                KeyValuePair<string, string>[] roadItem = fixture.Content.PinnedNavigationInfo.DetailInfo.ToArray();

                Assert.AreEqual(("Track Item", "Road Item"),
                    (rail.Single(entry => entry.Key == "Item Type").Value, roadItem.Single(entry => entry.Key == "Item Type").Value));
                Assert.AreNotEqual(rail.Single(entry => entry.Key == "Location").Value, roadItem.Single(entry => entry.Key == "Location").Value);
            }
        }

        [TestMethod]
        [DataRow(-1, false)]
        [DataRow(-1, true)]
        [DataRow(1, true)]
        [DataRow(2, false)]
        [DataRow(int.MaxValue, false)]
        [DataRow(int.MaxValue, true)]
        public async Task InvalidOrMissingTrackItemLeavesNavigationUnchanged(int index, bool road)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                fixture.Content.NavigateToTrackItem(0, false);
                var previous = fixture.Content.PinnedNavigationInfo.DetailInfo;
                PointD center = fixture.Camera.State.CenterPoint;

                fixture.Content.NavigateToTrackItem(index, road);

                Assert.AreEqual((center, true), (fixture.Camera.State.CenterPoint,
                    ReferenceEquals(previous, fixture.Content.PinnedNavigationInfo.DetailInfo)));
            }
        }

        private static int NodeIndex(ITileCoordinate item) => item switch
        {
            TrackSegmentBase segment => segment.TrackNodeIndex,
            JunctionNodeBase junction => junction.TrackNodeIndex,
            EndNodeBase end => end.TrackNodeIndex,
            _ => throw new ArgumentException("Expected a track node widget.", nameof(item)),
        };
    }

    // Only device composition is substituted. Runtime topology and Toolbox conversion/navigation are production code.
    internal sealed class ToolboxInspectionFixture : IMapSessionComposer, IMapSession
    {
        internal ToolboxContent Content { get; private set; }
        internal TrackWorld World { get; private set; }
        internal InspectionViewport Camera { get; } = new InspectionViewport();
        internal InspectionRenderer Drawing { get; } = new InspectionRenderer();
        public IMapViewport Viewport => Camera;
        public IMapRenderer Renderer => Drawing;
        public IMapHostControl HostControl => null;

        // Read-only state inspection follows MapRuntimeReadinessTests; no production state is injected.
        internal EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> Items =>
            (EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType>)typeof(ContentBase)
                .GetProperty("ContentByTile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Content);
        internal EnumArray<ITileCoordinate, MapContentType> Highlights =>
            (EnumArray<ITileCoordinate, MapContentType>)typeof(ContentBase)
                .GetField("nearestItems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Content);

        internal static WorldLocation[] CrossingLocations() =>
        [
            new WorldLocation(1, 0, new Vector3(10, 0, 20)),
            new WorldLocation(1, 0, new Vector3(30, 0, 40)),
            new WorldLocation(2, 0, new Vector3(50, 0, 60)),
        ];

        internal static Task<ToolboxInspectionFixture> CreateDefaultAsync()
        {
            WorldLocation[] locations = CrossingLocations();
            return CreateAsync(
                ImmutableArray.Create<ModelItem>(new CrossoverTrackItem(locations[0]) { TrackItemIndex = 0, NodeIndex = 1 },
                    new LevelCrossingTrackItem(locations[1]) { TrackItemIndex = 1, NodeIndex = 1 }),
                ImmutableArray.Create<ModelItem>(new RoadLevelCrossingTrackItem(locations[2]) { TrackItemIndex = 0, NodeIndex = 1 }));
        }

        internal static async Task<ToolboxInspectionFixture> CreateAsync(ImmutableArray<ModelItem> railItems, ImmutableArray<ModelItem> roadItems)
        {
            TrackSectionModel sections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty
                    .Add(1, new TrackSection { SectionIndex = 1, Length = 100, Gauge = 1.435f })
                    .Add(2, new TrackSection { SectionIndex = 2, Length = 200, Gauge = 3 }),
            };
            TrackWorld world = TrackWorld.Create(new TrackModel
            {
                TrackDatabase = CreateDatabase(false, railItems),
                RoadDatabase = CreateDatabase(true, roadItems),
            }, sections);
            ToolboxInspectionFixture fixture = new ToolboxInspectionFixture { World = world };
            fixture.Content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(new InspectionRuntime(world, sections)), fixture, null, null));
            // Initialization's host completion owns GPU resources; the CPU conversion is still executed normally.
            await fixture.Content.InitializeAsync(_ => Task.CompletedTask).ConfigureAwait(false);
            fixture.Content.InitializeItemVisiblity(new EnumArray<bool, MapContentType>(false));
            return fixture;
        }

        private static TrackDatabase CreateDatabase(bool road, ImmutableArray<ModelItem> items)
        {
            Tile tile = new Tile(road ? 2 : 1, 0);
            WorldLocation start = new WorldLocation(tile, new Vector3(100, 0, 100));
            WorldLocation end = new WorldLocation(tile, new Vector3(100, 0, road ? 300 : 200));
            int junctionIndex = road ? 3 : 2;
            ModelJunctionNode junction = new ModelJunctionNode(new WorldLocation(tile, new Vector3(200, 0, 250)), tile, Vector3.Zero)
            {
                NodeIndex = junctionIndex,
                MainRoute = 1,
                ShapeIndex = road ? 29 : 17,
            };
            ModelEndNode endNode = new ModelEndNode(end, tile, Vector3.Zero) { NodeIndex = road ? 2 : 3 };
            TrackDatabase database = new TrackDatabase
            {
                TrackDataBaseType = road ? TrackDataBaseType.Road : TrackDataBaseType.Rail,
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new VectorNode(start, tile, end)
                    {
                        NodeIndex = 1,
                        VectorSections = ImmutableArray.Create(new VectorSectionNode(start, tile, Vector3.Zero, end) { NodeIndex = road ? 2 : 1 }),
                    },
                    road ? endNode : junction,
                    road ? junction : endNode,
                    new VectorNode(start, tile, end) { NodeIndex = 4, VectorSections = ImmutableArray<VectorSectionNode>.Empty },
                    null,
                    new ModelEndNode(new WorldLocation(tile, new Vector3(400, 0, 500)), tile, Vector3.Zero) { NodeIndex = 6 }),
                TrackNodeConnectors = Enumerable.Range(0, 7).Select(index => new TrackNodeConnectorIndex
                {
                    NodeIndex = index,
                    InboundCount = 1,
                    TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnector { Link = 1 },
                        new TrackNodeConnector { Link = 1 }, new TrackNodeConnector { Link = 1 }),
                }).ToImmutableArray(),
                TrackItems = items,
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return database;
        }

        internal TrackNodeBase GetTrackNode(int index, bool road) => World.TrackNodeByIndex(index, road ? TrackDataBaseType.Road : TrackDataBaseType.Rail);
        internal ModelItem GetTrackItem(int index, bool road) => World.TrackItemByIndex(index, road ? TrackDataBaseType.Road : TrackDataBaseType.Rail);
        internal TrackSegmentSection GetSegmentSection(int index, bool road) => (road ? World.RoadSegmentSections : World.SegmentSections)[index];
        internal void Draw() => Content.Draw(new Tile(0, -1), new Tile(3, 1));
        public IMapSession Compose(MapSessionRequest request) => this;
        public void Dispose() { }

        private sealed class InspectionRuntime : RuntimeDataResolver
        {
            internal InspectionRuntime(TrackWorld world, TrackSectionModel sections) :
                base(new RouteModel(WorldLocation.None) { Name = "Inspection fixture" }, sections, new SignalConfigurationModel(), world, true, null)
            {
            }
        }

        internal sealed class InspectionViewport : IMapViewport
        {
            internal MapViewportState State { get; } = new MapViewportState(new MapViewportBounds(0, 10000, 10000, 0));
            internal PointD FitTopLeft { get; private set; }
            internal PointD FitBottomRight { get; private set; }
            internal int FitCount { get; private set; }

            internal InspectionViewport() => State.ResetSize(new MapViewportSize(800, 600), 0);
            public bool InsideScreenArea(PointPrimitive pointPrimitive) => true;
            public bool InsideScreenArea(VectorPrimitive vectorPrimitive) => true;
            public void SetTrackingPosition(in WorldLocation location) => State.SetTrackingPosition(PointD.FromWorldLocation(location));
            public void SetTrackingPosition(in PointD location) => State.SetTrackingPosition(location);
            public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
            {
                FitTopLeft = topLeft;
                FitBottomRight = bottomRight;
                FitCount++;
                State.UpdateScaleToFit(topLeft, bottomRight, 100);
            }
        }

        internal sealed class InspectionRenderer : IMapRenderer
        {
            internal List<Vector2> TexturePositions { get; } = new List<Vector2>();
            public double Scale => 5;
            public System.Drawing.Font CurrentFont => null;
            public System.Drawing.Font ConstantSizeFont => null;
            public Vector2 WorldToScreenCoordinates(in WorldLocation location) => WorldToScreenCoordinates(PointD.FromWorldLocation(location));
            public Vector2 WorldToScreenCoordinates(in PointD location) => new Vector2((float)location.X, (float)location.Y);
            public float WorldToScreenSize(double worldSize, int minScreenSize) => (float)worldSize;
            public void DrawLine(float width, Color color, Vector2 point, float length, double angle) { }
            public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2) { }
            public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2) { }
            public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize) { }
            public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight)
                => TexturePositions.Add(point);
            public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, bool flipHorizontal, bool flipVertical)
                => TexturePositions.Add(point);
            public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color)
                => TexturePositions.Add(point);
            public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
                HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
            { }
        }
    }
}
