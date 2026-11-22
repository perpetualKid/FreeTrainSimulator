using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    /// <summary>Public-resolver, geometry-backed chart extraction regressions without persistence.</summary>
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathChartBuilderTests
    {
        /// <summary>Provides cancellation and focused timing output.</summary>
        public TestContext TestContext { get; set; }

        private static readonly string[] expectedArrayABA = new[] { "A", "B", "A" };
        private static readonly string[] expectedArrayAA = new[] { "A", "A" };
        private static readonly int[] expectedArray134 = new[] { 1, 3, 4 };
        private static readonly int[] expectedArray421 = new[] { 4, 2, 1 };

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildStraightPartialTraversalUsesPlacedElevationAndTravelGrade(bool reverse)
        {
            TrackWorld world = CreateSingleWorld(new TrackSection { SectionIndex = 1, Length = 100 }, At(0, 10, 0), At((float)Math.Sqrt(9975), 15, 0), ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            SectionGeometry geometry = world.SectionGeometry[((VectorNode)world.TrackDatabase.TrackNodes[1]).VectorSections[0]];
            PathModel model = CreatePath(world, reverse ? 80 : 20, reverse ? 20 : 80, PathNodeType.End);

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(60, profile.Length, 0.0001);
            Assert.AreEqual(0, profile.Samples[0].Distance, 0.0001);
            Assert.AreEqual(60, profile.Samples[^1].Distance, 0.0001);
            AssertLocation(geometry.LocationAt(reverse ? 80 : 20), profile.Samples[0].Location);
            AssertLocation(geometry.LocationAt(reverse ? 20 : 80), profile.Samples[^1].Location);
            double grade = (reverse ? -1 : 1) * 500 / Math.Sqrt(9975);
            foreach (PathChartSample sample in profile.Samples)
            {
                Assert.AreEqual(sample.Location.Location.Y, sample.Elevation, 0.000001);
                Assert.AreEqual(grade, sample.GradePercent, 0.00001);
                Assert.AreEqual(0, sample.CurvaturePerKilometre, 0.000001);
            }
            AssertSpacing(profile, 25);
        }

        [TestMethod]
        [DataRow(false, 90f)]
        [DataRow(true, 90f)]
        [DataRow(false, -90f)]
        [DataRow(true, -90f)]
        public void BuildCurvedPartialTraversalUsesArcOffsetsAndSignedReciprocalRadius(bool reverse, float angle)
        {
            TrackWorld world = CreateSingleWorld(new TrackSection
            {
                SectionIndex = 1, Length = MathHelper.PiOver2 * 100, Curved = true, Radius = 100, Angle = angle,
            }, At(0, 0, 0), At(100, 0, 100), ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            SectionGeometry geometry = world.SectionGeometry[((VectorNode)world.TrackDatabase.TrackNodes[1]).VectorSections[0]];
            PathModel model = CreatePath(world, reverse ? 120 : 20, reverse ? 20 : 120, PathNodeType.End);

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(100, profile.Length, 0.0001);
            Assert.IsLessThan(profile.Length, Math.Sqrt(WorldLocation.GetDistanceSquared2D(model.PathNodes[0].Location, model.PathNodes[1].Location)));
            AssertLocation(geometry.LocationAt(reverse ? 120 : 20), profile.Samples[0].Location);
            AssertLocation(geometry.LocationAt(reverse ? 20 : 120), profile.Samples[^1].Location);
            foreach (PathChartSample sample in profile.Samples)
            {
                Assert.AreEqual(Math.Sign(angle) * (reverse ? -10 : 10), sample.CurvaturePerKilometre, 0.000001);
                Assert.AreEqual(0, sample.GradePercent, 0.000001);
                double offset = (reverse ? 120 : 20) + (reverse ? -sample.Distance : sample.Distance);
                AssertLocation(geometry.LocationAt(offset), sample.Location);
            }
            AssertSpacing(profile, 100 * Math.PI / 180);
        }

        [TestMethod]
        public void BuildTiltedCurveGradeFollowsEvaluatedGeometryRatherThanStoredPitch()
        {
            TrackWorld world = CreateSingleWorld(new TrackSection
            {
                SectionIndex = 1, Length = MathHelper.PiOver2 * 100, Curved = true, Radius = 100, Angle = 90,
            }, At(0, 0, 0), At(100, 10, (float)Math.Sqrt(9900)), ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            SectionGeometry geometry = world.SectionGeometry[((VectorNode)world.TrackDatabase.TrackNodes[1]).VectorSections[0]];

            PathChartProfile profile = Build(CreatePath(world, 20, 120, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            foreach (PathChartSample sample in profile.Samples)
            {
                double offset = 20 + sample.Distance;
                WorldLocation before = geometry.LocationAt(offset - 0.5);
                WorldLocation after = geometry.LocationAt(offset + 0.5);
                Vector3 delta = WorldLocation.GetDistanceVector(before, after);
                double expected = 100 * delta.Y / Math.Sqrt((delta.X * delta.X) + (delta.Z * delta.Z));
                Assert.AreEqual(expected, sample.GradePercent, 0.0003);
            }
            Assert.IsTrue(profile.Samples.Any(sample => sample.GradePercent > 1));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildCrossesSectionBoundaryWithExactPartialOffsetsAndOneSidedCurvature(bool reverse)
        {
            VectorSectionNode first = Section(1, At(0, 0, 0), At(50, 0, 0));
            VectorSectionNode second = Section(2, At(50, 0, 0), At(100, 0, (float)(100 * (Math.Cos(Math.PI / 6) - 1))));
            VectorNode vector = new VectorNode(first.Location, first.Tile, second.EndLocation)
            { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, second) };
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 50 },
                new TrackSection { SectionIndex = 2, Length = 100 * MathHelper.Pi / 6, Curved = true, Radius = 100, Angle = 30 }));
            WorldLocation straightPoint = world.SectionGeometry[first].LocationAt(20);
            WorldLocation curvePoint = world.SectionGeometry[second].LocationAt(30);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, reverse ? curvePoint : straightPoint),
                    Node(PathNodeType.End, -1, 1, reverse ? straightPoint : curvePoint)),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(60, profile.Length, 0.0001);
            AssertLocation(reverse ? curvePoint : straightPoint, profile.Samples[0].Location);
            AssertLocation(reverse ? straightPoint : curvePoint, profile.Samples[^1].Location);
            PathChartSample[] boundary = profile.Samples.Where(sample => Math.Abs(sample.Distance - 30) < 0.0001).ToArray();
            Assert.HasCount(2, boundary);
            Assert.AreEqual(reverse ? -10 : 0, boundary[0].CurvaturePerKilometre, 0.000001);
            Assert.AreEqual(reverse ? 0 : 10, boundary[1].CurvaturePerKilometre, 0.000001);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildImportedFlatEndpointsRetainPitchAcrossSlopingSectionBoundaries(bool reverse)
        {
            Vector3 direction = new Vector3((float)-Math.Atan(0.05), MathHelper.PiOver2, 0);
            VectorSectionNode first = new VectorSectionNode(At(0, 10, 0), new Tile(1, 0), direction, At(100, 10, 0)) { NodeIndex = 1 };
            VectorSectionNode second = new VectorSectionNode(At(100, 15, 0), new Tile(1, 0), direction, At(200, 15, 0)) { NodeIndex = 1 };
            VectorNode vector = new VectorNode(first.Location, first.Tile, second.EndLocation)
            { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, second) };
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 100 }));
            WorldLocation start = At(20, 11, 0);
            WorldLocation end = At(180, 19, 0);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, reverse ? end : start),
                    Node(PathNodeType.End, -1, 1, reverse ? start : end)),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(160, profile.Length, 0.0001);
            AssertLocation(reverse ? end : start, profile.Samples[0].Location);
            AssertLocation(reverse ? start : end, profile.Samples[^1].Location);
            foreach (PathChartSample sample in profile.Samples)
            {
                Assert.AreEqual(10 + 0.05 * sample.Location.Location.X, sample.Elevation, 0.0001);
                Assert.AreEqual(reverse ? -5 : 5, sample.GradePercent, 0.00001);
            }
            Assert.AreEqual(10, world.SectionGeometry[first].LocationAt(100).Location.Y);
            Assert.AreEqual(15, world.SectionGeometry[second].LocationAt(100).Location.Y);
            Assert.AreEqual(10, first.EndLocation.Location.Y);
            Assert.AreEqual(15, second.EndLocation.Location.Y);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildImportedCurvedPitchKeepsElevationAndLocalGradeConsistent(bool reverse)
        {
            VectorSectionNode section = new VectorSectionNode(At(0, 10, 0), new Tile(1, 0), new Vector3((float)-Math.Atan(0.1), 0, 0), At(100, 10, 100)) { NodeIndex = 1 };
            VectorNode vector = new VectorNode(section.Location, section.Tile, section.EndLocation)
            { NodeIndex = 1, VectorSections = ImmutableArray.Create(section) };
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection
            { SectionIndex = 1, Length = 100 * MathHelper.PiOver2, Radius = 100, Angle = 90, Curved = true }));
            SectionGeometry geometry = world.SectionGeometry[section];
            PathModel model = CreatePath(world, reverse ? 120 : 20, reverse ? 20 : 120, PathNodeType.End);

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(100, profile.Length, 0.0001);
            foreach (PathChartSample sample in profile.Samples)
            {
                double offset = (reverse ? 120 : 20) + (reverse ? -sample.Distance : sample.Distance);
                Assert.AreEqual(10 + 10 * Math.Sin(offset / 100), sample.Elevation, 0.0001);
                Assert.AreEqual((reverse ? -10 : 10) * Math.Cos(offset / 100), sample.GradePercent, 0.0001);
                Assert.AreEqual(reverse ? -10 : 10, sample.CurvaturePerKilometre, 0.000001);
                Assert.AreEqual(geometry.LocationAt(offset).Location.X, sample.Location.Location.X, 0.0001);
                Assert.AreEqual(geometry.LocationAt(offset).Location.Z, sample.Location.Location.Z, 0.0001);
            }
            Assert.AreEqual(10, geometry.LocationAt(120).Location.Y);
        }

        [TestMethod]
        public void BuildRetainedPitchDoesNotHideAnUnexplainedVerticalGeometryGap()
        {
            Vector3 direction = new Vector3((float)-Math.Atan(0.05), MathHelper.PiOver2, 0);
            VectorSectionNode first = new VectorSectionNode(At(0, 10, 0), new Tile(1, 0), direction, At(100, 10, 0)) { NodeIndex = 1 };
            VectorSectionNode second = new VectorSectionNode(At(100, 30, 0), new Tile(1, 0), direction, At(200, 30, 0)) { NodeIndex = 1 };
            VectorNode vector = new VectorNode(first.Location, first.Tile, second.EndLocation)
            { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, second) };
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 100 }));
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(20, 11, 0)),
                    Node(PathNodeType.End, -1, 1, At(180, 34, 0))),
            };

            AssertUnavailable(Build(model, world), "The main path contains missing or inconsistent track geometry.");
        }

        [TestMethod]
        public void BuildGroupedStationMidpointStaysOnCurveRatherThanAveragingCoordinates()
        {
            TrackSection template = new TrackSection
            { SectionIndex = 1, Length = MathHelper.PiOver2 * 100, Curved = true, Radius = 100, Angle = 90 };
            TrackWorld initial = CreateSingleWorld(template, At(0, 0, 0), At(100, 0, 100), ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            SectionGeometry geometry = initial.SectionGeometry[((VectorNode)initial.TrackDatabase.TrackNodes[1]).VectorSections[0]];
            ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items = ImmutableArray.Create<global::FreeTrainSimulator.Models.Track.TrackItemBase>(
                new PlatformTrackItem(geometry.LocationAt(10)) { TrackItemIndex = 0, NodeIndex = 1, LinkedPlatformItem = 1, StationName = "A" },
                new PlatformTrackItem(geometry.LocationAt(40)) { TrackItemIndex = 1, NodeIndex = 1, LinkedPlatformItem = 0, StationName = "A" },
                new PlatformTrackItem(geometry.LocationAt(70)) { TrackItemIndex = 2, NodeIndex = 1, LinkedPlatformItem = 3, StationName = "A" },
                new PlatformTrackItem(geometry.LocationAt(110)) { TrackItemIndex = 3, NodeIndex = 1, LinkedPlatformItem = 2, StationName = "A" });
            TrackWorld world = CreateSingleWorld(template, At(0, 0, 0), At(100, 0, 100), items);

            PathChartProfile profile = Build(CreatePath(world, 5, 120, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.HasCount(1, profile.Stations);
            Assert.HasCount(2, profile.Stations[0].Platforms);
            Assert.AreEqual(55, profile.Stations[0].Distance, 0.0001);
            AssertLocation(geometry.LocationAt(60), profile.Stations[0].Location);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildDraftEndsAtLastResolvedAuthoredExtentWithoutRequiringEndMarker(bool reverse)
        {
            TrackWorld world = CreateStraightWorld(ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            PathModel model = CreatePath(world, reverse ? 80 : 20, reverse ? 20 : 80, PathNodeType.Via);
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            Assert.IsFalse(resolution.IsValid);
            Assert.IsTrue(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.MissingEndNode));

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(60, profile.Length, 0.0001);
            Assert.AreEqual(reverse ? 20 : 80, profile.Samples[^1].Location.Location.X, 0.0001);
        }

        [TestMethod]
        public void BuildGroupsConsecutiveSameNamePlatformsWithoutGlobalDeduplication()
        {
            ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items = Platforms(("A", 5f, 15f), ("A", 20f, 30f),
                ("B", 40f, 50f), ("A", 70f, 90f));
            TrackWorld world = CreateStraightWorld(items);
            PathModel model = CreatePath(world, 10, 80, PathNodeType.End);

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreSequenceEqual(expectedArrayABA, profile.Stations.Select(station => station.StationName).ToArray());
            Assert.HasCount(2, profile.Stations[0].Platforms);
            Assert.AreEqual(0, profile.Stations[0].StartDistance, 0.0001);
            Assert.AreEqual(20, profile.Stations[0].EndDistance, 0.0001);
            Assert.AreEqual(10, profile.Stations[0].Distance, 0.0001);
            Assert.AreEqual(20, profile.Stations[0].Location.Location.X, 0.0001);
            Assert.AreEqual(60, profile.Stations[2].StartDistance, 0.0001);
            Assert.AreEqual(70, profile.Stations[2].EndDistance, 0.0001);
            Assert.AreEqual(75, profile.Stations[2].Location.Location.X, 0.0001);
            Assert.AreEqual(0, profile.Stations[0].Platforms[0].FirstPlatformItemIndex);
            Assert.AreEqual(1, profile.Stations[0].Platforms[0].SecondPlatformItemIndex);
            foreach (PathChartStationVisit station in profile.Stations)
                Assert.IsTrue(profile.Samples.Any(sample => Math.Abs(sample.Distance - station.Distance) < 0.0001));
        }

        [TestMethod]
        [DataRow(false, 49.75f)]
        [DataRow(true, 49.75f)]
        [DataRow(false, 49.74998f)]
        [DataRow(true, 49.75002f)]
        public void BuildStartingStationNearASectionJoinUsesItsStoredTrackDistance(bool reverse, float storedDistance)
        {
            TrackWorld world = CreatePlatformSectionJoinWorld(storedDistance);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(reverse ? 55 : 10, 0, 0)),
                    Node(PathNodeType.End, -1, 1, At(reverse ? 10 : 55, 0, 0))),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.HasCount(1, profile.Stations);
            PathChartStationVisit station = profile.Stations[0];
            Assert.AreEqual("Starting Station", station.StationName);
            Assert.HasCount(1, station.Platforms);
            Assert.AreEqual(reverse ? 5.75 : 10, station.StartDistance, 0.0001);
            Assert.AreEqual(reverse ? 35.5 : 39.75, station.EndDistance, 0.0001);
            Assert.AreEqual(reverse ? 20.625 : 24.875, station.Distance, 0.0001);
            Assert.AreEqual(34.875, station.Location.Location.X, 0.0001);
            Assert.IsTrue(profile.Samples.Any(sample => Math.Abs(sample.Distance - station.Distance) < 0.0001));
            Assert.IsEmpty(profile.Diagnostics);
        }

        [TestMethod]
        [DataRow(50f)]
        [DataRow(float.NaN)]
        [DataRow(999f)]
        public void BuildDoesNotGuessAStationWhenSectionJoinDistancesTieOrConflict(float storedDistance)
        {
            TrackWorld world = CreatePlatformSectionJoinWorld(storedDistance);
            PathModel model = new PathModel
            {
                Name = "Starting Station",
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(55, 0, 0)),
                    Node(PathNodeType.End, -1, 1, At(10, 0, 0))),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.IsEmpty(profile.Stations);
            Assert.Contains("A platform pair cannot be established on the traversed track.", profile.Diagnostics);
            Assert.Contains("A chart item cannot be located unambiguously on its owning track.", profile.Diagnostics);
        }

        private static TrackWorld CreatePlatformSectionJoinWorld(float storedDistance)
        {
            VectorSectionNode first = Section(1, At(0, 0, 0), At(50, 0, 0));
            VectorSectionNode second = Section(1, At(49.5f, 0, 0), At(99.5f, 0, 0));
            VectorNode vector = new VectorNode(first.Location, first.Tile, second.EndLocation)
            { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, second) };
            return InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
                TrackItems = ImmutableArray.Create<global::FreeTrainSimulator.Models.Track.TrackItemBase>(
                    new PlatformTrackItem(At(20, 0, 0)) { TrackItemIndex = 0, NodeIndex = 1, LinkedPlatformItem = 1, StationName = "Starting Station", SectionDistance = 20 },
                    new PlatformTrackItem(At(49.75f, 0, 0)) { TrackItemIndex = 1, NodeIndex = 1, LinkedPlatformItem = 0, StationName = "Starting Station", SectionDistance = storedDistance }),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 50 }));
        }

        [TestMethod]
        public void BuildPreservesReturnVisitsAndDoesNotSplitAPlatformAtAuthoredVia()
        {
            TrackWorld world = CreateStraightWorld(Platforms(("A", 20f, 70f)));
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(10, 0, 0)),
                    Node(PathNodeType.Via, 2, 1, At(50, 0, 0)),
                    Node(PathNodeType.Via | PathNodeType.Reversal, 3, 1, At(80, 0, 0)),
                    Node(PathNodeType.End, -1, 1, At(10, 0, 0))),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(140, profile.Length, 0.0001);
            Assert.HasCount(2, profile.Stations);
            Assert.HasCount(1, profile.Stations[0].Platforms);
            Assert.HasCount(1, profile.Stations[1].Platforms);
            Assert.AreEqual(10, profile.Stations[0].StartDistance, 0.0001);
            Assert.AreEqual(60, profile.Stations[0].EndDistance, 0.0001);
            Assert.AreEqual(80, profile.Stations[1].StartDistance, 0.0001);
            Assert.AreEqual(130, profile.Stations[1].EndDistance, 0.0001);
            Assert.AreEqual(profile.Stations[0].Platforms[0].FirstPlatformItemIndex, profile.Stations[1].Platforms[0].FirstPlatformItemIndex);
            AssertLocation(profile.Stations[0].Location, profile.Stations[1].Location);
        }

        [TestMethod]
        public void BuildSameDirectionLoopReturnThroughDifferentPlatformPairsPreservesSeparateStationVisits()
        {
            WorldLocation start = At(0, 0, 0);
            WorldLocation end = At(100, 0, 0);
            VectorSectionNode main = Section(1, start, end);
            VectorSectionNode loop = Section(2, end, start);
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new VectorNode(start, start.Tile, end) { NodeIndex = 1, VectorSections = ImmutableArray.Create(main) },
                    new JunctionNode(start, start.Tile, Vector3.Zero) { NodeIndex = 2 },
                    new JunctionNode(end, end.Tile, Vector3.Zero) { NodeIndex = 3 },
                    new VectorNode(end, end.Tile, start) { NodeIndex = 4, VectorSections = ImmutableArray.Create(loop) }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    Pins(1, (2, ConnectorType.InPin), (3, ConnectorType.OutPin)),
                    Pins(2, (4, ConnectorType.InPin), (1, ConnectorType.OutPin)),
                    Pins(3, (1, ConnectorType.InPin), (4, ConnectorType.OutPin)),
                    Pins(4, (3, ConnectorType.InPin), (2, ConnectorType.OutPin))),
                TrackItems = Platforms(("A", 70f, 90f), ("A", 10f, 30f)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 100 }, new TrackSection { SectionIndex = 2, Length = 100 * MathHelper.Pi / 3, Curved = true, Radius = 100, Angle = 60 }));
            SectionGeometry loopGeometry = world.SectionGeometry[loop];
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(50, 0, 0)),
                    Node(PathNodeType.Via, 2, 1, At(60, 0, 0)),
                    Node(PathNodeType.Via, 3, 4, loopGeometry.LocationAt(loopGeometry.Length / 2)),
                    Node(PathNodeType.End, -1, 1, At(40, 0, 0))),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            Assert.IsTrue(resolution.MainRoute.Spans.All(span => span.Status == PathRouteSpanStatus.Resolved));
            Assert.AreSequenceEqual(expectedArray134, resolution.MainRoute.Spans[1].Candidates.Single().RouteNodeIndexes.ToArray());
            Assert.AreSequenceEqual(expectedArray421, resolution.MainRoute.Spans[2].Candidates.Single().RouteNodeIndexes.ToArray());

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.HasCount(2, profile.Stations);
            Assert.AreSequenceEqual(expectedArrayAA, profile.Stations.Select(visit => visit.StationName).ToArray());
            Assert.HasCount(1, profile.Stations[0].Platforms);
            Assert.HasCount(1, profile.Stations[1].Platforms);
            Assert.AreEqual(0, profile.Stations[0].Platforms[0].FirstPlatformItemIndex);
            Assert.AreEqual(2, profile.Stations[1].Platforms[0].FirstPlatformItemIndex);
            Assert.AreEqual(30, profile.Stations[0].Distance, 0.0001);
            Assert.AreEqual(50 + loopGeometry.Length + 20, profile.Stations[1].Distance, 0.0001);
            AssertLocation(At(80, 0, 0), profile.Stations[0].Location);
            AssertLocation(At(20, 0, 0), profile.Stations[1].Location);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildClipsEnclosingPlatformEvenWhenNeitherEndpointIsOnAuthoredExtent(bool reverse)
        {
            TrackWorld world = CreateStraightWorld(Platforms(("A", 5f, 95f)));

            PathChartProfile profile = Build(CreatePath(world, reverse ? 80 : 20, reverse ? 20 : 80, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.HasCount(1, profile.Stations);
            Assert.AreEqual(0, profile.Stations[0].StartDistance, 0.0001);
            Assert.AreEqual(60, profile.Stations[0].EndDistance, 0.0001);
            Assert.AreEqual(30, profile.Stations[0].Distance, 0.0001);
            Assert.AreEqual(50, profile.Stations[0].Location.Location.X, 0.0001);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildMalformedPlatformPairOmitsStationAndReportsStableDiagnostic(bool invalidLocation)
        {
            ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items = Platforms(("A", 20f, 80f));
            items = invalidLocation
                ? items.SetItem(1, new PlatformTrackItem(At(80, 0, 20))
                { TrackItemIndex = 1, NodeIndex = 1, LinkedPlatformItem = 0, StationName = "A" })
                : items.SetItem(1, ((PlatformTrackItem)items[1]) with { LinkedPlatformItem = 1 });
            TrackWorld world = CreateStraightWorld(items);

            PathChartProfile profile = Build(CreatePath(world, 10, 90, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.IsEmpty(profile.Stations);
            Assert.Contains("A platform pair cannot be established on the traversed track.", profile.Diagnostics);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildSourceSpeedMarkersRespectFacingUnitsExactDistanceAndClipping(bool reverse)
        {
            ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items = ImmutableArray.Create<global::FreeTrainSimulator.Models.Track.TrackItemBase>(
                Speedpost(0, 35, 0, 60, true), Speedpost(1, 65, MathHelper.Pi, 45, false),
                Speedpost(2, 5, 0, 100, true), Speedpost(3, 95, MathHelper.Pi, 100, true));
            TrackWorld world = CreateStraightWorld(items);

            PathChartProfile profile = Build(CreatePath(world, reverse ? 80 : 20, reverse ? 20 : 80, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.HasCount(1, profile.Markers);
            PathChartMarker marker = profile.Markers[0];
            Assert.AreEqual(15, marker.Distance, 0.0001);
            Assert.AreEqual(reverse ? 45 : 60, marker.Value, 0.0001);
            Assert.AreEqual(reverse, marker.IsMiles);
            Assert.AreEqual(PathChartMarkerKind.Speedpost, marker.Kind);
            Assert.AreEqual(reverse ? 65 : 35, marker.Location.Location.X, 0.0001);
            Assert.IsTrue(profile.Samples.Any(sample => Math.Abs(sample.Distance - marker.Distance) < 0.0001));
        }

        [TestMethod]
        public void BuildMilepostWithoutPreservedMetadataDoesNotInventUnitsOrFacing()
        {
            TrackWorld world = CreateStraightWorld(ImmutableArray.Create<global::FreeTrainSimulator.Models.Track.TrackItemBase>(
                new MilepostTrackItem(At(35, 0, 0)) { TrackItemIndex = 0, NodeIndex = 1, DistanceValue = 120 }));

            PathChartProfile profile = Build(CreatePath(world, 20, 80, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.IsEmpty(profile.Markers);
            Assert.Contains("Milepost units and direction are not retained in this track model.", profile.Diagnostics);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildCurvedSpeedMarkersUseActualPlacedTangentInBothTravelDirections(bool reverse)
        {
            TrackSection template = new TrackSection
            { SectionIndex = 1, Length = MathHelper.PiOver2 * 100, Curved = true, Radius = 100, Angle = 90 };
            TrackWorld initial = CreateSingleWorld(template, At(0, 0, 0), At(100, 0, 100),
                ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            SectionGeometry geometry = initial.SectionGeometry[((VectorNode)initial.TrackDatabase.TrackNodes[1]).VectorSections[0]];
            ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items = ImmutableArray.Create<global::FreeTrainSimulator.Models.Track.TrackItemBase>(
                new SpeedpostTrackItem(geometry.LocationAt(35)) { TrackItemIndex = 0, NodeIndex = 1, Angle = MathHelper.PiOver2 - 0.35f, SpeedValue = 60, SpeedpostType = SpeedpostType.Limit | SpeedpostType.Metric },
                new SpeedpostTrackItem(geometry.LocationAt(65)) { TrackItemIndex = 1, NodeIndex = 1, Angle = MathHelper.PiOver2 - 0.65f + MathHelper.Pi, SpeedValue = 45, SpeedpostType = SpeedpostType.Limit });
            TrackWorld world = CreateSingleWorld(template, At(0, 0, 0), At(100, 0, 100), items);

            PathChartProfile profile = Build(CreatePath(world, reverse ? 80 : 20, reverse ? 20 : 80, PathNodeType.End), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.HasCount(1, profile.Markers);
            Assert.AreEqual(15, profile.Markers[0].Distance, 0.0001);
            Assert.AreEqual(reverse ? 45 : 60, profile.Markers[0].Value, 0.0001);
            AssertLocation(geometry.LocationAt(reverse ? 65 : 35), profile.Markers[0].Location);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildUsesExactSelectedParallelPhysicalRouteRatherThanSharedEndpointChord(bool reverse)
        {
            TrackWorld world = CreateParallelWorld();
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start | PathNodeType.Junction, 1, reverse ? 2 : 1, At(reverse ? 100 : 0, 0, 0)),
                    Node(PathNodeType.End | PathNodeType.Junction, -1, reverse ? 1 : 2, At(reverse ? 0 : 100, 0, 0))),
            };
            PathRouteResolution choices = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            ResolvedRouteCandidate choice = choices.MainRoute.Spans[0].Candidates.Single(candidate => candidate.RouteNodeIndexes.Contains(4));
            PathRouteResolution selected = PathRouteResolver.ResolveSelectedRoutes(model, world, PathRouteResolverOptions.Default, new Dictionary<int, ResolvedRouteCandidate> { { 0, choice } }, TestContext.CancellationToken);

            PathChartProfile profile = PathChartBuilder.Build(model, selected, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(100 * Math.PI / 3, profile.Length, 0.0001);
            Assert.IsTrue(profile.Samples.Any(sample => sample.Location.Location.Z < -10));
            Assert.IsFalse(profile.Samples.Any(sample => sample.Location.Location.Z > 1));
            Assert.IsTrue(profile.Samples.All(sample => Math.Abs(sample.CurvaturePerKilometre - (reverse ? 10 : -10)) < 0.000001));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildAmbiguousMainRemainsUnavailableEvenWithWarningTieBreaking(bool tieBreaking)
        {
            TrackWorld world = CreateParallelWorld();
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start | PathNodeType.Junction, 1, 1, At(0, 0, 0)),
                    Node(PathNodeType.End | PathNodeType.Junction, -1, 2, At(100, 0, 0))),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, new PathRouteResolverOptions(5000, false, tieBreaking, true, true), TestContext.CancellationToken);

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            AssertUnavailable(profile, "The main path contains an unresolved or ambiguous span.");
        }

        [TestMethod]
        public void BuildRetainsExactParallelConnectorAndRejectsItsRemovalDespiteSameNodeSequence()
        {
            WorldLocation first = At(0, 0, 0);
            WorldLocation second = At(0.0001f, 0, 0);
            WorldLocation end = At(100.0001f, 0, 0);
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new JunctionNode(first, first.Tile, Vector3.Zero) { NodeIndex = 1 },
                    new JunctionNode(second, second.Tile, Vector3.Zero) { NodeIndex = 2 },
                    new VectorNode(second, second.Tile, end) { NodeIndex = 3, VectorSections = ImmutableArray.Create(Section(1, second, end)) },
                    new EndNode(end, end.Tile, Vector3.Zero) { NodeIndex = 4 }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    Pins(1, (2, ConnectorType.OutPin), (2, ConnectorType.OutPin)),
                    Pins(2, (1, ConnectorType.InPin), (1, ConnectorType.OutPin), (3, ConnectorType.InPin)),
                    Pins(3, (2, ConnectorType.InPin), (4, ConnectorType.OutPin)),
                    Pins(4, (3, ConnectorType.InPin))),
            };
            ImmutableArray<TrackSection> templates = ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 100 });
            TrackWorld world = InitializeWorld(database, templates);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start | PathNodeType.Junction, 1, 1, first),
                    Node(PathNodeType.Via | PathNodeType.Junction, 2, 2, second), Node(PathNodeType.End, -1, 3, At(50.0001f, 0, 0))),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            Assert.AreEqual(1, resolution.MainRoute.Spans[0].Candidates.Single().PhysicalTraversals.Single().FromConnectorIndex);

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(50, profile.Length, 0.0001);
            TrackNodeConnectorIndex changedPins = Pins(1, (2, ConnectorType.OutPin), (4, ConnectorType.OutPin));
            TrackWorld changedWorld = InitializeWorld(database with { TrackNodeConnectors = database.TrackNodeConnectors.SetItem(1, changedPins) }, templates);
            AssertUnavailable(PathChartBuilder.Build(model, resolution, changedWorld), "The selected main path connector cannot be established.");
        }

        [TestMethod]
        public void BuildIndependentBrokenPassingBranchDoesNotSuppressProvenMainGeometry()
        {
            TrackWorld world = CreateStraightWorld(ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            PathModel model = CreatePath(world, 20, 80, PathNodeType.End);
            model = model with { PathNodes = model.PathNodes.SetItem(0, model.PathNodes[0] with { NextSidingNode = 99 }) };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            Assert.IsFalse(resolution.IsValid);

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(60, profile.Length, 0.0001);
            Assert.Contains("Passing branch profiles are not included in the main path chart.", profile.Diagnostics);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildInvalidMainNeverReturnsAResolvedPrefix(bool cycle)
        {
            TrackWorld world = CreateStraightWorld(ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(20, 0, 0)),
                    Node(PathNodeType.Via, cycle ? 0 : 2, 1, At(50, 0, 0)),
                    Node(PathNodeType.End, -1, 1, At(200, 0, 0))),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsFalse(profile.IsAvailable);
            Assert.AreNotEqual(string.Empty, profile.UnavailableReason);
            Assert.IsEmpty(profile.Samples);
            Assert.IsEmpty(profile.Stations);
            Assert.IsEmpty(profile.Markers);
            Assert.AreEqual(0, profile.Length);
        }

        [TestMethod]
        public void BuildStaleAuthoredResolutionIsUnavailable()
        {
            TrackWorld world = CreateStraightWorld(ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            PathModel model = CreatePath(world, 20, 80, PathNodeType.End);
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            model = model with { PathNodes = model.PathNodes.SetItem(1, Node(PathNodeType.End, -1, 1, At(70, 0, 0))) };

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            AssertUnavailable(profile, "The path resolution does not match the authored main path.");
        }

        [TestMethod]
        public void BuildUsesSuppliedWorldAfterAnotherWorldBecomesTheGlobalGameService()
        {
            TrackWorld first = CreateStraightWorld(Platforms(("First", 20f, 70f)));
            PathModel model = CreatePath(first, 10, 90, PathNodeType.End);
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, first, TestContext.CancellationToken);
            TrackWorld second = CreateSingleWorld(new TrackSection { SectionIndex = 1, Length = 100 }, At(0, 50, 0), At(100, 50, 0), Platforms(("Second", 20f, 70f)));
            Assert.AreSame(second, TrackWorld.Instance);

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, first);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(0, profile.Samples[0].Elevation, 0.000001);
            Assert.AreEqual("First", profile.Stations.Single().StationName);
        }

        [TestMethod]
        public void BuildRejectsGeometryGapsBetweenPlacedSections()
        {
            VectorSectionNode first = Section(1, At(0, 0, 0), At(50, 0, 0));
            VectorSectionNode second = Section(1, At(70, 0, 0), At(120, 0, 0));
            VectorNode vector = new VectorNode(first.Location, new Tile(1, 0), second.EndLocation) { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, second) };
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 50 }));
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(10, 0, 0)), Node(PathNodeType.End, -1, 1, At(110, 0, 0))),
            };

            PathChartProfile profile = Build(model, world);

            AssertUnavailable(profile, "The main path contains missing or inconsistent track geometry.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildDraftDoesNotRequireGeometryBeyondItsLastAuthoredExtent(bool reverse)
        {
            VectorSectionNode first = Section(1, At(0, 0, 0), At(100, 0, 0));
            VectorSectionNode future = Section(2, At(100, 0, 0), At(200, 0, 0));
            VectorNode vector = new VectorNode(first.Location, first.Tile, future.EndLocation) { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, future) };
            TrackWorld world = InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 100 }));

            PathChartProfile profile = Build(CreatePath(world, reverse ? 80 : 20, reverse ? 20 : 80, PathNodeType.Via), world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(60, profile.Length, 0.0001);
            Assert.AreEqual(reverse ? 20 : 80, profile.Samples[^1].Location.Location.X, 0.0001);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void BuildTraversalInsideLaterSectionDoesNotRequireTheUnusedPrecedingJoin(bool reverse, bool atBoundary)
        {
            TrackWorld world = CreateWorldWithSectionGap();
            SectionGeometry geometry = world.SectionGeometry[((VectorNode)world.TrackDatabase.TrackNodes[1]).VectorSections[1]];
            double lower = atBoundary ? 0 : 20;
            WorldLocation start = geometry.LocationAt(reverse ? 80 : lower);
            WorldLocation end = geometry.LocationAt(reverse ? lower : 80);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, start), Node(PathNodeType.End, -1, 1, end)),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            Assert.IsTrue(resolution.IsValid);

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(80 - lower, profile.Length, 0.0001);
            AssertLocation(start, profile.Samples[0].Location);
            AssertLocation(end, profile.Samples[^1].Location);
            Assert.IsTrue(profile.Samples.All(sample => sample.Location.Location.X >= 120 && sample.Location.Location.X <= 200));
            AssertSpacing(profile, 25);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildReversalAtLaterSectionStartKeepsItsBoundaryOnTheTraversedSection(bool reverse)
        {
            TrackWorld world = CreateWorldWithSectionGap();
            SectionGeometry geometry = world.SectionGeometry[((VectorNode)world.TrackDatabase.TrackNodes[1]).VectorSections[1]];
            WorldLocation start = geometry.LocationAt(reverse ? 40 : 80);
            WorldLocation end = geometry.LocationAt(reverse ? 80 : 40);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, start),
                    Node(PathNodeType.Via | PathNodeType.Reversal, 2, 1, geometry.LocationAt(0)), Node(PathNodeType.End, -1, 1, end)),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(120, profile.Length, 0.0001);
            AssertLocation(start, profile.Samples[0].Location);
            AssertLocation(end, profile.Samples[^1].Location);
            AssertLocation(geometry.LocationAt(0), profile.Samples.First(sample => Math.Abs(sample.Distance - (reverse ? 40 : 80)) < 0.0001).Location);
            Assert.IsTrue(profile.Samples.All(sample => sample.Location.Location.X >= 120));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildTraversalCrossingTheSectionGapStillRefusesTheCompleteChart(bool reverse)
        {
            TrackWorld world = CreateWorldWithSectionGap();
            VectorNode vector = (VectorNode)world.TrackDatabase.TrackNodes[1];
            WorldLocation first = world.SectionGeometry[vector.VectorSections[0]].LocationAt(80);
            WorldLocation second = world.SectionGeometry[vector.VectorSections[1]].LocationAt(20);
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, reverse ? second : first),
                    Node(PathNodeType.End, -1, 1, reverse ? first : second)),
            };

            AssertUnavailable(Build(model, world), "The main path contains missing or inconsistent track geometry.");
        }

        [TestMethod]
        public void BuildMissingGeometryIsUnavailableInsteadOfInventingAnAnchorBridge()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathModel model = new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, At(20, 0, 0)), Node(PathNodeType.End, -1, 1, At(80, 0, 0))),
            };

            PathChartProfile profile = Build(model, world);

            Assert.IsFalse(profile.IsAvailable);
            Assert.AreNotEqual(string.Empty, profile.UnavailableReason);
            Assert.IsEmpty(profile.Samples);
        }

        [TestMethod]
        public void BuildLargeStraightHasBoundedSamplesAndReportsFocusedTiming()
        {
            TrackWorld world = CreateSingleWorld(new TrackSection { SectionIndex = 1, Length = 100000 }, At(0, 0, 0), At(100000, 0, 0), ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);
            PathModel model = CreatePath(world, 10, 99990, PathNodeType.End);
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, TestContext.CancellationToken);
            Stopwatch timer = Stopwatch.StartNew();

            PathChartProfile profile = PathChartBuilder.Build(model, resolution, world);

            timer.Stop();
            TestContext.WriteLine($"100 km chart: {profile.Samples.Length} samples in {timer.Elapsed.TotalMilliseconds:F2} ms.");
            Assert.IsTrue(profile.IsAvailable, profile.UnavailableReason);
            Assert.AreEqual(99980, profile.Length, 0.1);
            Assert.IsLessThanOrEqualTo(4001, profile.Samples.Length);
            AssertSpacing(profile, 25.001);
        }

        [TestMethod]
        public void BuildRejectsExcessiveSampleDensityBeforeMaterializingAProfile()
        {
            TrackWorld world = CreateSingleWorld(new TrackSection { SectionIndex = 1, Length = 10000000 }, At(0, 0, 0), At(10000000, 0, 0), ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Empty);

            PathChartProfile profile = Build(CreatePath(world, 10, 9999990, PathNodeType.End), world);

            AssertUnavailable(profile, "The path chart exceeds the supported sample limit.");
        }

        private PathChartProfile Build(PathModel model, TrackWorld world)
        {
            return PathChartBuilder.Build(model, PathRouteResolver.Resolve(model, world, TestContext.CancellationToken), world);
        }

        private static PathModel CreatePath(TrackWorld world, double start, double end, PathNodeType terminalType)
        {
            VectorNode node = (VectorNode)world.TrackDatabase.TrackNodes[1];
            SectionGeometry geometry = world.SectionGeometry[node.VectorSections[0]];
            return new PathModel
            {
                PathNodes = ImmutableArray.Create(Node(PathNodeType.Start, 1, 1, geometry.LocationAt(start)),
                    Node(terminalType, -1, 1, geometry.LocationAt(end))),
            };
        }

        private static PathNode Node(PathNodeType type, int next, int nodeIndex, WorldLocation location)
        {
            return new PathNode(location) { NodeType = type, NodeIndex = nodeIndex, NextMainNode = next, NextSidingNode = -1 };
        }

        private static WorldLocation At(float x, float y, float z)
        {
            return new WorldLocation(new Tile(1, 0), new Vector3(x, y, z), true);
        }

        private static VectorSectionNode Section(int templateIndex, WorldLocation start, WorldLocation end)
        {
            return new VectorSectionNode(start, start.Tile, new Vector3(0, MathHelper.PiOver2, 0), end) { NodeIndex = templateIndex };
        }

        private static TrackWorld CreateWorldWithSectionGap()
        {
            VectorSectionNode first = Section(1, At(0, 0, 0), At(100, 0, 0));
            VectorSectionNode second = Section(1, At(120, 0, 0), At(220, 0, 0));
            VectorNode vector = new VectorNode(first.Location, first.Tile, second.EndLocation)
            { NodeIndex = 1, VectorSections = ImmutableArray.Create(first, second) };
            return InitializeWorld(new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vector),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
            }, ImmutableArray.Create(new TrackSection { SectionIndex = 1, Length = 100 }));
        }

        private static TrackWorld CreateStraightWorld(ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items)
        {
            return CreateSingleWorld(new TrackSection { SectionIndex = 1, Length = 100 }, At(0, 0, 0), At(100, 0, 0), items);
        }

        private static TrackWorld CreateSingleWorld(TrackSection template, WorldLocation start, WorldLocation end, ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> items)
        {
            VectorNode node = new VectorNode(start, start.Tile, end) { NodeIndex = 1, VectorSections = ImmutableArray.Create(Section(1, start, end)) };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, node),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), Pins(1)),
                TrackItems = items,
            };
            return InitializeWorld(database, ImmutableArray.Create(template));
        }

        private static TrackWorld InitializeWorld(TrackDatabase database, ImmutableArray<TrackSection> templates)
        {
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, new TrackSectionModel { TrackSections = templates.ToImmutableDictionary(template => template.SectionIndex) });
        }

        private static ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase> Platforms(params (string Station, float Start, float End)[] pairs)
        {
            ImmutableArray<global::FreeTrainSimulator.Models.Track.TrackItemBase>.Builder items = ImmutableArray.CreateBuilder<global::FreeTrainSimulator.Models.Track.TrackItemBase>();
            foreach ((string station, float start, float end) in pairs)
            {
                int index = items.Count;
                items.Add(new PlatformTrackItem(At(start, 0, 0)) { TrackItemIndex = index, NodeIndex = 1, LinkedPlatformItem = index + 1, StationName = station, PlatformName = "Platform" });
                items.Add(new PlatformTrackItem(At(end, 0, 0)) { TrackItemIndex = index + 1, NodeIndex = 1, LinkedPlatformItem = index, StationName = station, PlatformName = "Platform" });
            }
            return items.ToImmutable();
        }

        private static SpeedpostTrackItem Speedpost(int index, float position, float angle, float value, bool metric)
        {
            return new SpeedpostTrackItem(At(position, 0, 0))
            {
                TrackItemIndex = index, NodeIndex = 1, Angle = angle, SpeedValue = value,
                SpeedpostType = SpeedpostType.Limit | (metric ? SpeedpostType.Metric : SpeedpostType.None),
            };
        }

        private static TrackWorld CreateParallelWorld()
        {
            WorldLocation start = At(0, 0, 0);
            WorldLocation end = At(100, 0, 0);
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new JunctionNode(start, start.Tile, Vector3.Zero) { NodeIndex = 1 },
                    new JunctionNode(end, end.Tile, Vector3.Zero) { NodeIndex = 2 },
                    new VectorNode(start, start.Tile, end) { NodeIndex = 3, VectorSections = ImmutableArray.Create(Section(1, start, end)) },
                    new VectorNode(start, start.Tile, end) { NodeIndex = 4, VectorSections = ImmutableArray.Create(Section(2, start, end)) }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    Pins(1, (3, ConnectorType.OutPin), (4, ConnectorType.OutPin)),
                    Pins(2, (3, ConnectorType.InPin), (4, ConnectorType.InPin)),
                    Pins(3, (1, ConnectorType.InPin), (2, ConnectorType.OutPin)),
                    Pins(4, (1, ConnectorType.InPin), (2, ConnectorType.OutPin))),
            };
            return InitializeWorld(database, ImmutableArray.Create(
                new TrackSection { SectionIndex = 1, Length = 100 * MathHelper.Pi / 3, Curved = true, Radius = 100, Angle = 60 },
                new TrackSection { SectionIndex = 2, Length = 100 * MathHelper.Pi / 3, Curved = true, Radius = 100, Angle = -60 }));
        }

        private static TrackNodeConnectorIndex Pins(int node, params (int Link, ConnectorType Type)[] pins)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = node,
                TrackNodeConnectors = pins.Select(pin => new TrackNodeConnector
                { Link = pin.Link, ConnectorType = pin.Type, Direction = TrackDirection.Reverse }).ToImmutableArray(),
            };
        }

        private static void AssertLocation(WorldLocation expected, WorldLocation actual)
        {
            Assert.IsLessThan(0.000001, WorldLocation.GetDistanceSquared(expected, actual));
        }

        private static void AssertSpacing(PathChartProfile profile, double maximum)
        {
            for (int index = 1; index < profile.Samples.Length; index++)
            {
                double spacing = profile.Samples[index].Distance - profile.Samples[index - 1].Distance;
                Assert.IsGreaterThanOrEqualTo(0, spacing);
                Assert.IsLessThanOrEqualTo(maximum + 0.0001, spacing);
            }
        }

        private static void AssertUnavailable(PathChartProfile profile, string reason)
        {
            Assert.IsFalse(profile.IsAvailable);
            Assert.AreEqual(reason, profile.UnavailableReason);
            Assert.AreEqual(0, profile.Length);
            Assert.IsEmpty(profile.Samples);
            Assert.IsEmpty(profile.Stations);
            Assert.IsEmpty(profile.Markers);
        }
    }
}