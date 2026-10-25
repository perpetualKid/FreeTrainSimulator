using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

using PlatformItem = FreeTrainSimulator.Models.Track.PlatformTrackItem;
using PlatformWidget = FreeTrainSimulator.Graphics.MapView.Widgets.PlatformTrackItem;
using SidingItem = FreeTrainSimulator.Models.Track.SidingTrackItem;
using SidingWidget = FreeTrainSimulator.Graphics.MapView.Widgets.SidingTrackItem;

namespace Tests.FreeTrainSimulator.Graphics.MapView.Widgets
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class EditorTrainPathLengthTests
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenStraightPathHasPartialSectionsThenLengthCountsEachSpanOnce(bool reverse)
        {
            TrackWorld world = CreateWorld(0);
            PathNode first = NodeAt(world, reverse ? 90 : 10, PathNodeType.Start, 1);
            PathNode via = NodeAt(world, 50, PathNodeType.Via, 2);
            PathNode last = NodeAt(world, reverse ? 10 : 90, PathNodeType.End, -1);

            EditorTrainPath path = new EditorTrainPath(Model(first, via, last), world);

            Assert.AreEqual(80f, path.Length, 0.001f);
        }

        [TestMethod]
        [DataRow(90, false)]
        [DataRow(90, true)]
        [DataRow(-90, false)]
        [DataRow(-90, true)]
        public void WhenCurvedPathHasPartialSectionsThenLengthUsesArcNotEndpointChord(int angle, bool reverse)
        {
            TrackWorld world = CreateWorld(angle);
            // Radius 100 m, spanning 20% to 80% of a quarter circle, in either direction.
            double quarterCircle = 100 * Math.PI / 2;
            PathNode first = NodeAt(world, quarterCircle * (reverse ? 0.8 : 0.2), PathNodeType.Start, 1);
            PathNode via = NodeAt(world, quarterCircle * 0.5, PathNodeType.Via, 2);
            PathNode last = NodeAt(world, quarterCircle * (reverse ? 0.2 : 0.8), PathNodeType.End, -1);

            EditorTrainPath path = new EditorTrainPath(Model(first, via, last), world);

            Assert.AreEqual((float)(quarterCircle * 0.6), path.Length, 0.001f);
        }

        [TestMethod]
        public void WhenPathRetracesTrackThenLengthCountsBothTraversals()
        {
            TrackWorld world = CreateWorld(0);
            EditorTrainPath path = new EditorTrainPath(Model(
                NodeAt(world, 10, PathNodeType.Start, 1),
                NodeAt(world, 90, PathNodeType.Reversal, 2),
                NodeAt(world, 30, PathNodeType.End, -1)), world);

            Assert.AreEqual(140f, path.Length, 0.001f);
        }

        [TestMethod]
        public void WhenPathHasNoSectionsThenLengthIsZero()
        {
            TrackWorld world = CreateWorld(0);
            Assert.AreEqual(0f, new EditorTrainPath(Model(), world).Length);
            Assert.AreEqual(0f, new EditorTrainPath(Model(NodeAt(world, 10, PathNodeType.Start, -1)), world).Length);
            Assert.AreEqual(0f, new EditorTrainPath(Model(
                NodeAt(world, 10, PathNodeType.Start, -1),
                NodeAt(world, 90, PathNodeType.End, -1)), world).Length);
        }

        [TestMethod]
        public void WhenPartialPathHasDanglingLastNodeThenLengthCountsOnlyConnectedSpan()
        {
            TrackWorld world = CreateWorld(0);
            EditorTrainPath path = new EditorTrainPath(Model(
                NodeAt(world, 10, PathNodeType.Start, 1),
                NodeAt(world, 60, PathNodeType.Via, -1)), world);

            Assert.AreEqual(50f, path.Length, 0.001f);
        }

        [TestMethod]
        public void WhenInvalidSpanIsDrawnAsStraightFallbackThenLengthCountsFallbackOnce()
        {
            TrackWorld world = CreateWorld(0);
            WorldLocation start = new WorldLocation(new Tile(1, 0), new Vector3(30, 0, 30));
            WorldLocation end = new WorldLocation(new Tile(1, 0), new Vector3(60, 0, 70));
            EditorTrainPath path = new EditorTrainPath(Model(
                new PathNode(start) { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                new PathNode(end) { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }), world);

            Assert.AreEqual(50f, path.Length, 0.001f);
        }

        [TestMethod]
        public void WhenEndpointPreviewIsReplacedAndRemovedThenLengthReturnsToCommittedDistance()
        {
            TrackWorld world = CreateWorld(0);
            EditorTrainPath path = new EditorTrainPath(Model(
                NodeAt(world, 10, PathNodeType.Start, 1),
                NodeAt(world, 40, PathNodeType.Via, -1)), world);
            TrackSegmentBase segment = world.SegmentSections[1].SectionSegments[0];

            Assert.AreEqual(30f, path.Length, 0.001f);
            _ = path.UpdatePathEndPoint(PointD.FromWorldLocation(NodeAt(world, 70, PathNodeType.Via, -1).Location), null, segment);
            Assert.AreEqual(60f, path.Length, 0.001f);
            EditorPathPoint preview = path.UpdatePathEndPoint(PointD.FromWorldLocation(NodeAt(world, 90, PathNodeType.Via, -1).Location), null, segment);
            Assert.AreEqual(80f, path.Length, 0.001f);
            _ = path.RemovePathPoint(preview);
            Assert.AreEqual(30f, path.Length, 0.001f);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(90)]
        [DataRow(-90)]
        public void WhenPlatformAndSidingUseDatabaseConstructorThenLengthRemainsSectionBased(int angle)
        {
            TrackWorld world = CreateWorld(angle);
            double fullLength = angle == 0 ? 100 : 100 * Math.PI / 2;
            WorldLocation start = NodeAt(world, fullLength * 0.2, PathNodeType.Start, -1).Location;
            WorldLocation end = NodeAt(world, fullLength * 0.8, PathNodeType.End, -1).Location;
            PlatformPath platform = new PlatformPath(world, 
                new PlatformWidget(new PlatformItem(start) { NodeIndex = 1, PlatformName = "Platform", StationName = "Station" }, world.TrackDatabase),
                new PlatformWidget(new PlatformItem(end) { NodeIndex = 1, PlatformName = "Platform", StationName = "Station" }, world.TrackDatabase));
            SidingPath siding = new SidingPath(world,
                new SidingWidget(new SidingItem(start) { NodeIndex = 1, SidingName = "Siding" }, world.TrackDatabase),
                new SidingWidget(new SidingItem(end) { NodeIndex = 1, SidingName = "Siding" }, world.TrackDatabase));

            Assert.AreEqual((float)(fullLength * 0.6), platform.Length, 0.001f);
            Assert.AreEqual((float)(fullLength * 0.6), siding.Length, 0.001f);
        }

        [TestMethod]
        public void WhenDatabaseSectionCannotResolveEndpointsThenItsExistingZeroLengthFallbackIsPreserved()
        {
            TrackWorld world = CreateWorld(0);
            WorldLocation start = new WorldLocation(new Tile(1, 0), new Vector3(30, 0, 30));
            WorldLocation end = new WorldLocation(new Tile(1, 0), new Vector3(60, 0, 70));
            SidingPath siding = new SidingPath(world,
                new SidingWidget(new SidingItem(start) { NodeIndex = 1, SidingName = "Unresolved" }, world.TrackDatabase),
                new SidingWidget(new SidingItem(end) { NodeIndex = 1, SidingName = "Unresolved" }, world.TrackDatabase));

            // Unlike the explicit invalid-span constructor, the database-backed partial-section
            // constructor draws a fallback but does not attribute a resolved track distance to it.
            Assert.AreEqual(0f, siding.Length);
        }

        private static PathModel Model(params PathNode[] nodes)
            => new PathModel { Id = "length", Name = "Physical distance", PathNodes = nodes.ToImmutableArray() };

        private static PathNode NodeAt(TrackWorld world, double distance, PathNodeType type, int next)
            => new PathNode(world.ComputeSectionLocation(world.TrackDatabase.VectorNodes[1].VectorSections[0], distance))
            { NodeType = type, NodeIndex = 1, NextMainNode = next, NextSidingNode = -1 };

        private static TrackWorld CreateWorld(int angle)
        {
            Tile tile = new Tile(1, 0);
            WorldLocation start = new WorldLocation(tile, Vector3.Zero);
            WorldLocation end = new WorldLocation(tile, angle == 0 ? new Vector3(0, 0, 100) : new Vector3(Math.Sign(angle) * 100, 0, 100));
            VectorSectionNode section = new VectorSectionNode(start, tile, Vector3.Zero, end) { NodeIndex = 1 };
            VectorNode vector = new VectorNode(start, tile, end) { NodeIndex = 1, VectorSections = ImmutableArray.Create(section) };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty },
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return TrackWorld.Create(new TrackModel { TrackDatabase = database }, new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection
                {
                    SectionIndex = 1, Gauge = 1.435f, Curved = angle != 0, Radius = angle == 0 ? 0 : 100,
                    Angle = angle, Length = angle == 0 ? 100 : (float)(100 * Math.PI / 2),
                }),
            });
        }
    }
}
