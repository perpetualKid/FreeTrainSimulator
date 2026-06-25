using System;
using System.Collections.Immutable;
using System.Reflection;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    [TestClass]
    public class TrackWorldTests
    {
        [TestMethod]
        public void NearestTrackDistanceWhenPointProjectsBeyondSectionUsesEndpointDistance()
        {
            VectorSectionNode section = CreateSection(new Vector3(0, 0, 100));
            TrackWorld trackWorld = CreateTrackWorldWithSection(section, new TrackSection() { SectionIndex = 1, Gauge = 1.435f, Length = 100 });

            TrackDistanceDiagnostic diagnostic = trackWorld.NearestTrackDistance(new PointD(Tile.TileSize + 10, 100));

            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(1, diagnostic.TrackNodeIndex);
            Assert.AreEqual(0, diagnostic.TrackVectorSectionIndex);
            Assert.AreEqual(10, diagnostic.DistanceMeters, 0.001);
        }

        [TestMethod]
        public void NearestTrackDistanceWhenPointOnCurvedArcIsNearZero()
        {
            // Heading 0, +90deg arc, radius 100 => tangent end at (100, 100) in (X, Z).
            VectorSectionNode section = CreateSection(new Vector3(100, 0, 100));
            TrackWorld trackWorld = CreateTrackWorldWithSection(section,
                new TrackSection() { SectionIndex = 1, Gauge = 1.435f, Curved = true, Radius = 100, Angle = 90, Length = (float)(100 * Math.PI / 2) });

            // A point exactly on the arc (midpoint along the section).
            WorldLocation onArc = trackWorld.ComputeSectionLocation(section, 100 * Math.PI / 4);

            TrackDistanceDiagnostic diagnostic = trackWorld.NearestTrackDistance(PointD.FromWorldLocation(onArc));

            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(0, diagnostic.DistanceMeters, 0.1);
        }

        [TestMethod]
        public void NearestTrackDistanceWhenPointOnNegativeAngleCurvedArcIsNearZero()
        {
            // Right-hand curve: heading 0, -90deg arc, radius 100 => tangent end at (-100, 100) in (X, Z).
            // Regression: the U/V arc basis always sweeps 0..+absArc, so the in-arc test must not branch on
            // the (negative) ArcAngle sign, otherwise on-arc points are rejected and fall back to endpoint distance.
            VectorSectionNode section = CreateSection(new Vector3(-100, 0, 100));
            TrackWorld trackWorld = CreateTrackWorldWithSection(section,
                new TrackSection() { SectionIndex = 1, Gauge = 1.435f, Curved = true, Radius = 100, Angle = -90, Length = (float)(100 * Math.PI / 2) });

            // A point exactly on the arc (midpoint along the section).
            WorldLocation onArc = trackWorld.ComputeSectionLocation(section, 100 * Math.PI / 4);

            TrackDistanceDiagnostic diagnostic = trackWorld.NearestTrackDistance(PointD.FromWorldLocation(onArc));

            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(0, diagnostic.DistanceMeters, 0.1);
        }

        private static VectorSectionNode CreateSection(Vector3 storedEnd)
        {
            WorldLocation start = new WorldLocation(new Tile(1, 0), Vector3.Zero);
            WorldLocation end = new WorldLocation(new Tile(1, 0), storedEnd);
            // Direction.Y == 0 -> heading toward +Z.
            return new VectorSectionNode(start, new Tile(1, 0), Vector3.Zero, end)
            {
                NodeIndex = 1,
            };
        }

        private static TrackWorld CreateTrackWorldWithSection(VectorSectionNode section, TrackSection trackSection)
        {
            VectorNode vectorNode = new VectorNode(section.Location, new Tile(1, 0), section.EndLocation)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(section),
            };
            VectorNode filler = CreateFillerVectorNode();
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(filler, vectorNode),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel()
            {
                TrackDatabase = trackDatabase,
            };
            TrackSectionModel trackSectionModel = new TrackSectionModel()
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, trackSection),
            };

            return TrackWorld.Initialize(null, trackModel, trackSectionModel);
        }

        private static VectorNode CreateFillerVectorNode()
        {
            WorldLocation start = new WorldLocation(new Tile(1, 0), new Vector3(0, 0, -500));
            WorldLocation end = new WorldLocation(new Tile(1, 0), new Vector3(0, 0, -400));
            VectorSectionNode section = new VectorSectionNode(start, new Tile(1, 0), Vector3.Zero, end)
            {
                NodeIndex = 1,
            };

            return new VectorNode(start, new Tile(1, 0), end)
            {
                NodeIndex = 0,
                VectorSections = ImmutableArray.Create(section),
            };
        }

        private static void InitializeTrackDatabase(TrackDatabase trackDatabase)
        {
            typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
        }
    }
}
