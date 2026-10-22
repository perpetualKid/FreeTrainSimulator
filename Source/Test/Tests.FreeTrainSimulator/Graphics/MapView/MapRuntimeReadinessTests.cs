using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Graphics.MapView
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class MapRuntimeReadinessTests
    {
        [TestMethod]
        public void CandidateFactoryRejectsNullGameBeforeCreatingResources()
        {
            ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() =>
                XnaMapContentFactory.CreateToolboxContent(null, null, null, null, null));

            Assert.AreEqual("game", error.ParamName);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void CandidateFactoryRejectsNullInputBeforeCreatingMapResources()
        {
            using (Game game = new Game())
            {
                ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() =>
                    XnaMapContentFactory.CreateToolboxContent(game, null, null, null, null));

                Assert.AreEqual("mouseInputGameComponent", error.ParamName);
            }
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void CandidateFactoryRejectsNullRuntimeBeforeCreatingMapResources()
        {
            using (Game game = new Game())
            {
                MouseInputGameComponent input = (MouseInputGameComponent)RuntimeHelpers.GetUninitializedObject(typeof(MouseInputGameComponent));
                ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() =>
                    XnaMapContentFactory.CreateToolboxContent(game, input, null, null, null));

                Assert.AreEqual("runtimeData", error.ParamName);
            }
        }

        [TestMethod]
        [DataRow(false, 0, 100f, 0f, 0f)]
        [DataRow(false, 1, 157.07964f, 90f, 100f)]
        [DataRow(false, 2, 157.07964f, -90f, 100f)]
        [DataRow(true, 0, 200f, 0f, 0f)]
        [DataRow(true, 1, 125.663704f, 90f, 80f)]
        [DataRow(true, 2, 125.663704f, -90f, 80f)]
        public void PreparedTopologyContainsRouteLocalStraightAndCurvedGeometry(bool road, int sectionIndex,
            float expectedLength, float expectedAngle, float expectedRadius)
        {
            RuntimeDataResolver published = RuntimeDataResolver.Instance;
            TrackWorld publishedWorld = TrackWorld.Instance;
            PreparedResolver candidate = CreateRuntime();
            ImmutableArray<TrackSegmentSection> sections = road ? candidate.TrackWorld.RoadSegmentSections : candidate.TrackWorld.SegmentSections;
            TrackSegmentBase segment = sections[2].SectionSegments[sectionIndex];

            Assert.AreEqual((2, sectionIndex, expectedLength, MathHelper.ToRadians(expectedAngle), expectedRadius, true, true),
                (segment.TrackNodeIndex, segment.TrackVectorSectionIndex, segment.Length, segment.Angle, segment.Radius,
                ReferenceEquals(published, RuntimeDataResolver.Instance), ReferenceEquals(publishedWorld, TrackWorld.Instance)));
        }

        [TestMethod]
        public void PreparedJunctionUsesSecondOutboundPinForMainRouteOne()
        {
            Tile tile = new Tile(1, 0);
            WorldLocation location = new WorldLocation(tile, Vector3.Zero);
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new JunctionNode(location, tile, Vector3.Zero) { NodeIndex = 1, MainRoute = 1 }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex
                {
                    NodeIndex = 1,
                    InboundCount = 1,
                    TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnector { Link = 4 },
                        new TrackNodeConnector { Link = 2 }, new TrackNodeConnector { Link = 3 }),
                }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);

            TrackWorld world = TrackWorld.Create(new TrackModel { TrackDatabase = database }, new TrackSectionModel());

            Assert.AreEqual((1, 3, 1), (world.Junctions[1].TrackNodeIndex, world.Junctions[1].MainRoute, world.SwitchStates[1]));
        }

        [TestMethod]
        [DataRow(typeof(ToolboxContent))]
        [DataRow(typeof(DispatcherContent))]
        public void GraphicsConversionSkipsInternalAndTrailingEmptyVectorsWithoutChangingPreparedTopology(Type contentType)
        {
            ArgumentNullException.ThrowIfNull(contentType);
            PreparedResolver runtime = CreateRuntime();
            TrackSegmentSection rail = runtime.TrackWorld.SegmentSections[2];
            TrackSegmentSection road = runtime.TrackWorld.RoadSegmentSections[2];
            RuntimeDataResolver published = RuntimeDataResolver.Instance;
            TrackWorld publishedWorld = TrackWorld.Instance;

            EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> tiles = ConvertSegments(contentType, runtime);

            Assert.AreEqual((3, 2, true, true, true, true),
                (tiles[MapContentType.Tracks].ItemCount,
                ((TrackSegmentBase)tiles[MapContentType.Tracks].First()).TrackNodeIndex,
                ReferenceEquals(rail, runtime.TrackWorld.SegmentSections[2]),
                ReferenceEquals(road, runtime.TrackWorld.RoadSegmentSections[2]),
                ReferenceEquals(published, RuntimeDataResolver.Instance), ReferenceEquals(publishedWorld, TrackWorld.Instance)));
        }

        [TestMethod]
        public void ToolboxConversionKeepsRoadAndRailGeometrySeparateAtTheSameNodeIndex()
        {
            PreparedResolver runtime = CreateRuntime();

            EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> tiles = ConvertSegments(typeof(ToolboxContent), runtime);
            TrackSegmentBase rail = tiles[MapContentType.Tracks].Cast<TrackSegmentBase>().Single(segment => segment.TrackVectorSectionIndex == 0);
            TrackSegmentBase road = tiles[MapContentType.Roads].Cast<TrackSegmentBase>().Single(segment => segment.TrackVectorSectionIndex == 0);

            Assert.AreEqual((3, 3, 2, 2, 100f, 200f),
                (tiles[MapContentType.Tracks].ItemCount, tiles[MapContentType.Roads].ItemCount,
                rail.TrackNodeIndex, road.TrackNodeIndex, rail.Length, road.Length));
        }

        private static EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> ConvertSegments(Type contentType, RuntimeDataResolver runtime)
        {
            // Exercise the actual CPU-to-widget conversion, without allocating a device-backed shell.
            ContentBase content = (ContentBase)RuntimeHelpers.GetUninitializedObject(contentType);
            EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> tiles = new EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType>();
            typeof(ContentBase).GetField("runtimeServices", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(content, new MapRuntimeServices(runtime));
            typeof(ContentBase).GetField("trackWorld", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(content, runtime.TrackWorld);
            typeof(ContentBase).GetField("<ContentByTile>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(content, tiles);
            contentType.GetMethod("AddTrackSegments", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(content, null);
            return tiles;
        }

        private static PreparedResolver CreateRuntime()
        {
            TrackSectionModel sections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty
                    .Add(1, new TrackSection { SectionIndex = 1, Length = 100, Gauge = 1.435f })
                    .Add(2, new TrackSection { SectionIndex = 2, Length = 157.07964f, Gauge = 1.435f, Curved = true, Radius = 100, Angle = 90 })
                    .Add(3, new TrackSection { SectionIndex = 3, Length = 157.07964f, Gauge = 1.435f, Curved = true, Radius = 100, Angle = -90 })
                    .Add(4, new TrackSection { SectionIndex = 4, Length = 200, Gauge = 3 })
                    .Add(5, new TrackSection { SectionIndex = 5, Length = 125.663704f, Gauge = 3, Curved = true, Radius = 80, Angle = 90 })
                    .Add(6, new TrackSection { SectionIndex = 6, Length = 125.663704f, Gauge = 3, Curved = true, Radius = 80, Angle = -90 }),
            };
            TrackWorld world = TrackWorld.Create(new TrackModel
            {
                TrackDatabase = CreateDatabase(1),
                RoadDatabase = CreateDatabase(4),
            }, sections);
            return new PreparedResolver(world, sections);
        }

        private static TrackDatabase CreateDatabase(int firstSection)
        {
            Tile tile = new Tile(1, 0);
            WorldLocation start = new WorldLocation(tile, Vector3.Zero);
            WorldLocation end = new WorldLocation(tile, new Vector3(0, 0, 100));
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new VectorNode(start, tile, end) { NodeIndex = 1, VectorSections = ImmutableArray<VectorSectionNode>.Empty },
                    new VectorNode(start, tile, end)
                    {
                        NodeIndex = 2,
                        VectorSections = ImmutableArray.Create(
                            new VectorSectionNode(start, tile, Vector3.Zero, end) { NodeIndex = firstSection },
                            new VectorSectionNode(start, tile, Vector3.Zero, end) { NodeIndex = firstSection + 1 },
                            new VectorSectionNode(start, tile, Vector3.Zero, end) { NodeIndex = firstSection + 2 }),
                    },
                    new VectorNode(start, tile, end) { NodeIndex = 3, VectorSections = ImmutableArray<VectorSectionNode>.Empty }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex(),
                    new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return database;
        }

        private sealed class PreparedResolver : RuntimeDataResolver
        {
            internal PreparedResolver(TrackWorld world, TrackSectionModel sections) :
                base(new RouteModel(WorldLocation.None) { Name = "Private candidate" }, sections, new SignalConfigurationModel(), world, true, null)
            {
            }
        }
    }
}
