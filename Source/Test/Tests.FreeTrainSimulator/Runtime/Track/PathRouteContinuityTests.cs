using System.Collections.Immutable;
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
    /// <summary>
    /// Geometry-backed public resolver regressions for FTS-native continuity and generated path witnesses.
    /// All coordinates are synthetic; no imported content or model-store writes are required.
    /// </summary>
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathRouteContinuityTests
    {
        /// <summary>Provides the cancellation token for each public resolver call.</summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Arrival heading follows the entered vector endpoint, not the reciprocal pin's direction
        /// describing the linked junction. Only an explicit reversal permits continuing against that heading.
        /// </summary>
        [TestMethod]
        [DataRow(true, TrackDirection.Ahead, true, false, PathRouteSpanStatus.Resolved)]
        [DataRow(true, TrackDirection.Ahead, false, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(true, TrackDirection.Ahead, true, true, PathRouteSpanStatus.Unresolved)]
        [DataRow(true, TrackDirection.Ahead, false, true, PathRouteSpanStatus.Resolved)]
        [DataRow(true, TrackDirection.Reverse, true, false, PathRouteSpanStatus.Resolved)]
        [DataRow(true, TrackDirection.Reverse, false, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(true, TrackDirection.Reverse, true, true, PathRouteSpanStatus.Unresolved)]
        [DataRow(true, TrackDirection.Reverse, false, true, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Ahead, true, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(false, TrackDirection.Ahead, false, false, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Ahead, true, true, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Ahead, false, true, PathRouteSpanStatus.Unresolved)]
        [DataRow(false, TrackDirection.Reverse, true, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(false, TrackDirection.Reverse, false, false, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Reverse, true, true, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Reverse, false, true, PathRouteSpanStatus.Unresolved)]
        public void ResolveWhenEnteringVectorThenDirectContinuationUsesEnteredEndpoint(bool enterAtStart,
            TrackDirection reciprocalDirection, bool continueAhead, bool reversal, PathRouteSpanStatus expectedStatus)
        {
            TrackWorld world = CreateContinuityWorld(reciprocalDirection);
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start | PathNodeType.Junction, 1, enterAtStart ? 2 : 3, enterAtStart ? 0 : 100),
                    CreateNode(PathNodeType.Via | (reversal ? PathNodeType.Reversal : PathNodeType.None), 2, 1, 50),
                    CreateNode(PathNodeType.End, -1, 1, continueAhead ? 75 : 25)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);

            AssertSpanStatuses(result, PathRouteSpanStatus.Resolved, expectedStatus);
            Assert.AreEqual(1, result.AuthoredNodeAnchors[1].TrackNodeIndex);
            Assert.AreEqual(0, result.AuthoredNodeAnchors[1].TrackVectorSectionIndex);
            Assert.AreSequenceEqual(new[] { enterAtStart ? 2 : 3, 1 }, result.MainRoute.Spans[0].Candidates.Single().RouteNodeIndexes.ToArray());
            Assert.IsEmpty(result.MainRoute.Spans[1].GeneratedIntermediaryAnchors,
                "A direct same-vector span must not gain a witness beyond its target.");
        }

        /// <summary>
        /// A co-located via carries the incoming heading, applying the preceding reversal exactly once.
        /// The final point is toward the entry junction and therefore requires that reversal.
        /// </summary>
        [TestMethod]
        [DataRow(true, TrackDirection.Ahead, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(true, TrackDirection.Ahead, true, PathRouteSpanStatus.Resolved)]
        [DataRow(true, TrackDirection.Reverse, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(true, TrackDirection.Reverse, true, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Ahead, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(false, TrackDirection.Ahead, true, PathRouteSpanStatus.Resolved)]
        [DataRow(false, TrackDirection.Reverse, false, PathRouteSpanStatus.Unresolved)]
        [DataRow(false, TrackDirection.Reverse, true, PathRouteSpanStatus.Resolved)]
        public void ResolveWhenColocatedViaFollowsVectorArrivalThenCarriesHeadingAndReversalOnce(bool enterAtStart,
            TrackDirection reciprocalDirection, bool reversal, PathRouteSpanStatus expectedStatus)
        {
            TrackWorld world = CreateContinuityWorld(reciprocalDirection);
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start | PathNodeType.Junction, 1, enterAtStart ? 2 : 3, enterAtStart ? 0 : 100),
                    CreateNode(PathNodeType.Via | (reversal ? PathNodeType.Reversal : PathNodeType.None), 2, 1, 50),
                    CreateNode(PathNodeType.Via, 3, 1, 50),
                    CreateNode(PathNodeType.End, -1, 1, enterAtStart ? 25 : 75)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);

            AssertSpanStatuses(result, PathRouteSpanStatus.Resolved, PathRouteSpanStatus.Resolved, expectedStatus);
            Assert.IsEmpty(result.MainRoute.Spans[1].GeneratedIntermediaryAnchors);
            Assert.IsEmpty(result.MainRoute.Spans[2].GeneratedIntermediaryAnchors);
        }

        /// <summary>
        /// A witness emitted when leaving at 99 m ahead or 1 m reverse must stay between the source via
        /// and its departure endpoint. Omitting an unnecessary witness is also valid.
        /// </summary>
        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public void GeneratePathWhenViaIsNearDepartureBoundaryThenWitnessDoesNotBacktrack(bool ahead, bool endAtJunction)
        {
            PathModel generated = GenerateBoundaryPath(ahead, endAtJunction, out TrackWorld world);
            VectorNode vector = (VectorNode)world.TrackDatabase.TrackNodes[1];
            SectionGeometry geometry = world.SectionGeometry[vector.VectorSections[0]];
            double sourcePosition = ahead ? 99 : 1;

            foreach (PathNode witness in generated.PathNodes.Skip(2).Where(node => node.NodeIndex == 1))
            {
                double witnessPosition = geometry.DistanceOnSection(witness.Location);
                Assert.IsTrue(ahead
                    ? witnessPosition >= sourcePosition && witnessPosition <= 100
                    : witnessPosition <= sourcePosition && witnessPosition >= 0,
                    $"Witness at {witnessPosition} m backtracks from {sourcePosition} m ({(ahead ? "ahead" : "reverse")}).");
            }
        }

        /// <summary>
        /// Materializing a route with inherited near-boundary heading must not make any regenerated span unresolved.
        /// </summary>
        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public void ResolveWhenGeneratedNearBoundaryPathIsReloadedThenEverySpanRemainsResolved(bool ahead, bool endAtJunction)
        {
            PathModel generated = GenerateBoundaryPath(ahead, endAtJunction, out TrackWorld world);

            PathRouteResolution roundTrip = PathRouteResolver.Resolve(generated, world, TestContext.CancellationToken);

            AssertSpanStatuses(roundTrip, Enumerable.Repeat(PathRouteSpanStatus.Resolved, generated.PathNodes.Length - 1).ToArray());
            PathGenerationResult regenerated = PathModelRouteGenerator.GeneratePath(generated, roundTrip, world, PathRouteResolverOptions.Default);
            Assert.IsTrue(regenerated.Success, regenerated.Message);
        }

        /// <summary>
        /// Valid on-vector waits and optional reversals retain their authored location, flags, and duration on generation.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GeneratePathWhenVectorViaHasWaitAndOptionalReversalThenPreservesIntent(bool reversal)
        {
            TrackWorld world = CreateContinuityWorld(TrackDirection.Ahead);
            PathNodeWaitInfo waitInfo = new PathNodeWaitInfo { WaitTime = 30 };
            PathNode via = CreateNode(PathNodeType.Via | PathNodeType.Wait | (reversal ? PathNodeType.Reversal : PathNodeType.None), 2, 1, 50) with { WaitInfo = waitInfo };
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, 1, 25), via,
                    CreateNode(PathNodeType.End, -1, 1, reversal ? 25 : 75)),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);
            AssertSpanStatuses(resolution, PathRouteSpanStatus.Resolved, PathRouteSpanStatus.Resolved);

            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(source, resolution, world, PathRouteResolverOptions.Default);

            Assert.IsTrue(generated.Success, generated.Message);
            Assert.HasCount(3, generated.PathModel.PathNodes);
            Assert.AreEqual(via, generated.PathModel.PathNodes[1]);
            Assert.AreEqual(via, source.PathNodes[1], "Generation must not rewrite the authored wait/reversal.");
            AssertSpanStatuses(PathRouteResolver.Resolve(generated.PathModel, world, TestContext.CancellationToken),
                PathRouteSpanStatus.Resolved, PathRouteSpanStatus.Resolved);
        }

        private PathModel GenerateBoundaryPath(bool ahead, bool endAtJunction, out TrackWorld world)
        {
            world = CreateContinuityWorld(TrackDirection.Ahead);
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, 1, 50),
                    CreateNode(PathNodeType.Via, 2, 1, ahead ? 99 : 1),
                    CreateNode(PathNodeType.End | (endAtJunction ? PathNodeType.Junction : PathNodeType.None), -1,
                        endAtJunction ? (ahead ? 3 : 2) : (ahead ? 7 : 5),
                        endAtJunction ? (ahead ? 100 : 0) : (ahead ? 200 : -100))),
            };
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);
            AssertSpanStatuses(resolution, PathRouteSpanStatus.Resolved, PathRouteSpanStatus.Resolved);

            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(source, resolution, world, PathRouteResolverOptions.Default);

            Assert.IsTrue(generated.Success, generated.Message);
            Assert.AreEqual(source.PathNodes[0], generated.PathModel.PathNodes[0]);
            Assert.AreEqual(source.PathNodes[1], generated.PathModel.PathNodes[1]);
            return generated.PathModel;
        }

        private static void AssertSpanStatuses(PathRouteResolution result, params PathRouteSpanStatus[] expectedStatuses)
        {
            string diagnostics = string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            // Adjacent real sections can produce a proximity-based AmbiguousAnchor warning at 1 m/99 m.
            // Missing or mismatched anchors and fictitious junctions must never explain a continuity failure.
            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AnchorNotOnTrack ||
                diagnostic.Code == PathRouteDiagnosticCode.AnchorLocationMismatch || diagnostic.Code == PathRouteDiagnosticCode.NoJunctionNode), diagnostics);
            Assert.IsTrue(result.IsValid, diagnostics);
            Assert.IsNotNull(result.MainRoute);
            Assert.HasCount(expectedStatuses.Length, result.MainRoute.Spans, diagnostics);

            for (int index = 0; index < expectedStatuses.Length; index++)
            {
                ResolvedPathSpan span = result.MainRoute.Spans[index];
                Assert.AreEqual(index, span.FromNodeIndex);
                Assert.AreEqual(index + 1, span.ToNodeIndex);
                Assert.AreEqual(expectedStatuses[index], span.Status, $"Span {index}: {diagnostics}");
            }
            Assert.AreEqual(expectedStatuses.Count(status => status == PathRouteSpanStatus.Unresolved),
                result.Diagnostics.Count(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnresolvedDenseSpan), diagnostics);
        }

        private static PathNode CreateNode(PathNodeType nodeType, int nextMainNode, int trackNodeIndex, float x)
        {
            return new PathNode(AtX(x))
            {
                NodeType = nodeType,
                NodeIndex = trackNodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static WorldLocation AtX(float x)
        {
            return new WorldLocation(new Tile(1, 0), new Vector3(x, 0, 0));
        }

        private static TrackWorld CreateContinuityWorld(TrackDirection reciprocalDirection)
        {
            // End(5) -- vector(4) -- junction(2) -- vector(1) -- junction(3) -- vector(6) -- End(7).
            // Every vector has exactly two physical pins: index 0 at its start, index 1 at its end.
            // Directions on vector(1)'s pins describe the linked junction, NOT vector(1)'s entry end.
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    CreateVector(1, 0, 100),
                    new JunctionNode(AtX(0), new Tile(1, 0), Vector3.Zero) { NodeIndex = 2 },
                    new JunctionNode(AtX(100), new Tile(1, 0), Vector3.Zero) { NodeIndex = 3 },
                    CreateVector(4, -100, 0),
                    new EndNode(AtX(-100), new Tile(1, 0), Vector3.Zero) { NodeIndex = 5 },
                    CreateVector(6, 100, 200),
                    new EndNode(AtX(200), new Tile(1, 0), Vector3.Zero) { NodeIndex = 7 }),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex(),
                    CreateConnectors(1, (2, ConnectorType.InPin, reciprocalDirection), (3, ConnectorType.OutPin, reciprocalDirection)),
                    // Junction-to-vector directions remain fixed: Reverse reaches the start; Ahead reaches the end.
                    CreateConnectors(2, (4, ConnectorType.InPin, TrackDirection.Ahead), (1, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(3, (1, ConnectorType.InPin, TrackDirection.Ahead), (6, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(4, (5, ConnectorType.InPin, TrackDirection.Ahead), (2, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(5, (4, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(6, (3, ConnectorType.InPin, TrackDirection.Reverse), (7, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(7, (6, ConnectorType.InPin, TrackDirection.Ahead))),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackSectionModel sections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection
                {
                    SectionIndex = 1,
                    Gauge = 1.435f,
                    Length = 100,
                }),
            };
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, sections);
        }

        private static VectorNode CreateVector(int nodeIndex, float startX, float endX)
        {
            VectorSectionNode section = new VectorSectionNode(AtX(startX), new Tile(1, 0),
                new Vector3(0, MathHelper.PiOver2, 0), AtX(endX)) { NodeIndex = 1 };
            return new VectorNode(AtX(startX), new Tile(1, 0), AtX(endX))
            {
                NodeIndex = nodeIndex,
                VectorSections = ImmutableArray.Create(section),
            };
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params (int Link, ConnectorType Type, TrackDirection Direction)[] pins)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = pins.Select(pin => new TrackNodeConnector
                {
                    Link = pin.Link,
                    ConnectorType = pin.Type,
                    Direction = pin.Direction,
                }).ToImmutableArray(),
            };
        }
    }
}
