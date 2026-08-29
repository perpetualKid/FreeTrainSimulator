using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

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
    /// Unit tests for <see cref="PathRouteResolver"/>.
    /// </summary>
    [TestClass]
    public class PathRouteResolverTests
    {
        /// <summary>
        /// Verifies that an empty path produces a fatal diagnostic.
        /// </summary>
        [TestMethod]
        public void ResolveWhenPathHasNoNodesReturnsEmptyPathDiagnostic()
        {
            PathModel pathModel = new PathModel();

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(PathRouteDiagnosticSeverity.Fatal, result.HighestSeverity);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.EmptyPath));
        }

        /// <summary>
        /// Verifies that missing start and end nodes are reported.
        /// </summary>
        [TestMethod]
        public void ResolveWhenPathHasNoStartOrEndReturnsMissingNodeDiagnostics()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Intermediate, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.MissingStartNode));
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.MissingEndNode));
        }

        /// <summary>
        /// Verifies that invalid main links are fatal diagnostics.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMainLinkIsOutOfRangeReturnsInvalidMainLinkDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 4), CreateNode(PathNodeType.End, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.InvalidMainLink && diagnostic.NodeIndex == 0));
        }

        /// <summary>
        /// Verifies that unreachable authored nodes are reported.
        /// </summary>
        [TestMethod]
        public void ResolveWhenNodeIsUnreachableReturnsUnreachableNodeDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnreachableNode && diagnostic.NodeIndex == 2));
        }

        /// <summary>
        /// Verifies that authored main-link cycles are reported as graph cycles.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMainLinksContainCycleReturnsGraphCycleDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Intermediate, 2),
                    CreateNode(PathNodeType.End, 1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnsupportedGraphCycle && diagnostic.FromNodeIndex == 2 && diagnostic.ToNodeIndex == 1));
        }

        /// <summary>
        /// Verifies that authored siding-link cycles are reported as graph cycles.
        /// </summary>
        [TestMethod]
        public void ResolveWhenSidingLinksContainCycleReturnsGraphCycleDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1, 2), CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1, 3), CreateNode(PathNodeType.Intermediate, -1, 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnsupportedGraphCycle && diagnostic.FromNodeIndex == 3 && diagnostic.ToNodeIndex == 2));
        }

        /// <summary>
        /// Verifies that main route spans follow authored main links.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMainLinksAreValidBuildsMainRouteSpans()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1),
                    CreateNode(PathNodeType.Intermediate, 2),
                    CreateNode(PathNodeType.End, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsNotNull(result.MainRoute);
            Assert.HasCount(2, result.MainRoute.Spans);
            Assert.AreEqual(0, result.MainRoute.Spans[0].FromNodeIndex);
            Assert.AreEqual(1, result.MainRoute.Spans[0].ToNodeIndex);
            Assert.AreEqual(1, result.MainRoute.Spans[1].FromNodeIndex);
            Assert.AreEqual(2, result.MainRoute.Spans[1].ToNodeIndex);
        }

        /// <summary>
        /// Verifies that a main route that stops before the authored end node is fatal.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMainRouteDoesNotReachEndReturnsFatalDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Intermediate, -1), CreateNode(PathNodeType.End, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.MainRouteDoesNotReachEnd && diagnostic.FromNodeIndex == 1 && diagnostic.ToNodeIndex == 2));
        }

        /// <summary>
        /// Verifies that same-track-node spans resolve deterministically when hybrid anchors are available.
        /// </summary>
        [TestMethod]
        public void ResolveWhenLinkedNodesShareTrackNodeResolvesSpan()
        {
            TrackWorld trackWorld = CreateTrackWorldWithSingleVectorNode();
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[0].Status);
            Assert.HasCount(1, result.MainRoute.Spans[0].TrackVectorNodeIndexes);
            Assert.AreEqual(1, result.MainRoute.Spans[0].TrackVectorNodeIndexes[0]);
        }

        /// <summary>
        /// Verifies that two track nodes connected through the same junction resolve as a dense span.
        /// </summary>
        [TestMethod]
        public void ResolveWhenLinkedNodesShareJunctionResolvesDenseSpan()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3),
                    CreateConnectors(2, 3),CreateConnectors(3, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[0].Status);
            Assert.AreSequenceEqual(expectedArray12, result.MainRoute.Spans[0].TrackVectorNodeIndexes.ToArray());
        }

        [TestMethod]
        public void ResolveWhenTargetIsBeyondJunctionThenSelectsTheTargetSideExit()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4), CreateVectorNode(5)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 5), CreateConnectors(5, 4)));
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[0].Status);
            Assert.AreSequenceEqual(expectedArray132, result.MainRoute.Spans[0].Candidates.Single().RouteNodeIndexes.ToArray());
        }

        [TestMethod]
        public void ResolveWhenGeneratedVectorAndJunctionAnchorsShareLocationThenKeepsOnlyJunction()
        {
            VectorNode coLocatedVector = CreateVectorNode(2);
            JunctionNode coLocatedJunction = new JunctionNode(coLocatedVector.Location, new Tile(0, 0), Vector3.Zero)
            {
                NodeIndex = 3,
            };
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), coLocatedVector, coLocatedJunction, CreateVectorNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 2), CreateConnectors(2, 1, 3),
                    CreateConnectors(3, 2, 4), CreateConnectors(4, 3)));
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 4)),
            };

            PathRouteResolution resolution = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);
            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(
                pathModel, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsTrue(generated.Success);
            Assert.AreSequenceEqual(expectedArray134, generated.PathModel.PathNodes.Select(node => node.NodeIndex).ToArray());
        }

        [TestMethod]
        public void ResolveWhenShorterRouteCrossesSameJunctionSideThenKeepsIncomingDirection()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateVectorNode(3), CreateJunctionNode(4), CreateVectorNode(5)),
                ImmutableArray.Create(
                    new TrackNodeConnectorIndex(),
                    CreateTypedConnectors(1, (4, ConnectorType.InPin), (3, ConnectorType.OutPin)),
                    CreateTypedConnectors(2, (4, ConnectorType.InPin), (3, ConnectorType.InPin)),
                    CreateTypedConnectors(3, (1, ConnectorType.InPin), (2, ConnectorType.OutPin)),
                    CreateTypedConnectors(4, (1, ConnectorType.InPin), (2, ConnectorType.InPin), (5, ConnectorType.OutPin)),
                    CreateTypedConnectors(5, (4, ConnectorType.InPin))));
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreSequenceEqual(expectedArray132, result.MainRoute.Spans[0].Candidates.Single().RouteNodeIndexes.ToArray());
            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(pathModel, result, trackWorld, PathRouteResolverOptions.Default);
            Assert.IsTrue(generated.Success);
            Assert.AreSequenceEqual(expectedArray132, generated.PathModel.PathNodes.Select(node => node.NodeIndex).ToArray());
        }

        [TestMethod]
        public void ResolveWhenLoopEndsShareJunctionThenIncomingAnchorOrderSelectsClockwiseEnd()
        {
            TrackWorld trackWorld = CreateInitializedLoopTrackWorld();
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 25))),
                    CreateNode(PathNodeType.Intermediate, 2, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(50, 0, 100))),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(-50, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            PathRouteAnchor directionalAnchor = result.MainRoute.Spans[1].GeneratedIntermediaryAnchors
                .FirstOrDefault(anchor => anchor.TrackNodeIndex == 1 && anchor.TrackVectorSectionIndex == 3);
            Assert.IsNotNull(directionalAnchor);
            PathGenerationResult generated = PathModelRouteGenerator.GeneratePath(pathModel, result, trackWorld, PathRouteResolverOptions.Default);
            Assert.IsTrue(generated.Success);
            Assert.IsTrue(generated.PathModel.PathNodes.Any(node => node.NodeIndex == 1 && node.Location == directionalAnchor.Location));
            PathRouteResolution roundTrip = PathRouteResolver.Resolve(generated.PathModel, trackWorld, TestContext.CancellationToken);
            Assert.IsTrue(roundTrip.MainRoute.Spans.Any(span => span.GeneratedIntermediaryAnchors
                .Any(anchor => anchor.TrackNodeIndex == 1 && anchor.TrackVectorSectionIndex == 3)));
        }

        [TestMethod]
        public void ResolveWhenLoopRoutePointIsReversalThenSelectsOppositeVectorEnd()
        {
            TrackWorld trackWorld = CreateInitializedLoopTrackWorld();
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 25))),
                    CreateNode(PathNodeType.Intermediate | PathNodeType.Reversal, 2, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(50, 0, 100))),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(-50, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            PathRouteAnchor directionalAnchor = result.MainRoute.Spans[1].GeneratedIntermediaryAnchors
                .FirstOrDefault(anchor => anchor.TrackNodeIndex == 1 && anchor.TrackVectorSectionIndex == 0);
            Assert.IsNotNull(directionalAnchor);
        }

        [TestMethod]
        public void ResolveWhenAheadLoopTargetSnapsBehindCurrentPointThenSpanIsUnresolved()
        {
            TrackWorld trackWorld = CreateInitializedLoopTrackWorld();
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 25))),
                    CreateNode(PathNodeType.Intermediate, 2, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(50, 0, 100))),
                    CreateNode(PathNodeType.End, -1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(25, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Unresolved, result.MainRoute.Spans[1].Status);
        }

        [TestMethod]
        public void ResolveWhenLoopPointIsExplicitReversalThenBehindTargetIsResolved()
        {
            TrackWorld trackWorld = CreateInitializedLoopTrackWorld();
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 25))),
                    CreateNode(PathNodeType.Intermediate | PathNodeType.Reversal, 2, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(50, 0, 100))),
                    CreateNode(PathNodeType.End, -1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(25, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[1].Status);
        }

        /// <summary>
        /// Verifies that a single intermediary vector node resolves as a deterministic dense span.
        /// </summary>
        [TestMethod]
        public void ResolveWhenLinkedNodesHaveSingleIntermediaryVectorResolvesDenseSpan()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4), CreateVectorNode(5)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 4),
                    CreateConnectors(3, 1, 5), CreateConnectors(4, 2, 5), CreateConnectors(5, 3, 4)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[0].Status);
            Assert.AreSequenceEqual(expectedArray152, result.MainRoute.Spans[0].TrackVectorNodeIndexes.ToArray());
        }

        private static TrackNodeConnectorIndex CreateTypedConnectors(int nodeIndex, params (int Link, ConnectorType Type)[] connectors)
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

        /// <summary>
        /// Verifies that automatically routed spans expose generated intermediary anchors.
        /// </summary>
        [TestMethod]
        public void ResolveWhenRouteHasIntermediaryNodesThenGeneratedAnchorsArePopulated()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4), CreateVectorNode(5)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 4),
                    CreateConnectors(3, 1, 5), CreateConnectors(4, 2, 5), CreateConnectors(5, 3, 4)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.IsTrue(result.MainRoute.Spans[0].GeneratedIntermediaryAnchors.Any(anchor => anchor.AuthoredNodeIndex == -1 && anchor.TrackNodeIndex == 5));
        }

        /// <summary>
        /// Verifies that generated anchors can be suppressed by resolver options.
        /// </summary>
        [TestMethod]
        public void ResolveWhenGeneratedIntermediaryNodesDisabledThenGeneratedAnchorsAreEmpty()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            PathRouteResolverOptions options = new PathRouteResolverOptions(5000.0, false, false, false, true);

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, options, TestContext.CancellationToken);

            Assert.IsTrue(result.MainRoute.Spans[0].GeneratedIntermediaryAnchors.IsEmpty);
        }

        /// <summary>
        /// Verifies that equal-cost graph routes are surfaced as ambiguous spans by default.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMultipleEqualRoutesExistThenSpanIsAmbiguous()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3, 4),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Ambiguous, result.MainRoute.Spans[0].Status);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute));
        }

        /// <summary>
        /// Verifies that both equal-cost routes are surfaced as selectable candidates on the ambiguous span.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMultipleEqualRoutesExistThenBothCandidatesAreReported()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3, 4),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.HasCount(2, result.MainRoute.Spans[0].Candidates);
        }

        /// <summary>
        /// Verifies that candidates are ordered deterministically, so a selected candidate index stays stable.
        /// </summary>
        [TestMethod]
        public void ResolveWhenMultipleEqualRoutesExistThenCandidatesAreOrderedDeterministically()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3, 4),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreSequenceEqual(expectedArray132, result.MainRoute.Spans[0].Candidates[0].RouteNodeIndexes.ToArray());
            Assert.AreSequenceEqual(expectedArray142, result.MainRoute.Spans[0].Candidates[1].RouteNodeIndexes.ToArray());
        }

        /// <summary>
        /// Verifies that each candidate carries the intermediary anchors needed to author the chosen route.
        /// </summary>
        [TestMethod]
        public void ResolveWhenSpanIsAmbiguousThenCandidatesCarryIntermediaryAnchors()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3, 4),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(4, result.MainRoute.Spans[0].Candidates[1].GeneratedIntermediaryAnchors.Single().TrackNodeIndex);
        }

        /// <summary>
        /// Verifies that an unambiguous span reports exactly one candidate.
        /// </summary>
        [TestMethod]
        public void ResolveWhenSpanIsUnambiguousThenSingleCandidateIsReported()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.HasCount(1, result.MainRoute.Spans[0].Candidates);
        }

        /// <summary>
        /// Verifies that the maximum sparse search distance can reject an otherwise connected route.
        /// </summary>
        [TestMethod]
        public void ResolveWhenRouteExceedsMaximumSearchDistanceThenSpanIsUnresolved()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            PathRouteResolverOptions options = new PathRouteResolverOptions(1.0, false, false, true, true);

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, options, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Unresolved, result.MainRoute.Spans[0].Status);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnresolvedDenseSpan));
        }

        /// <summary>
        /// Verifies that a long-but-simple span across a junction still resolves when the anchors are far apart,
        /// because the configured search distance acts as a floor that is expanded by the anchor separation.
        /// </summary>
        [TestMethod]
        public void ResolveWhenAnchorsAreFarApartThenSearchDistanceFloorIsExpanded()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            // A tiny configured distance would reject the route on its own; the far-apart stored locations expand
            // the effective cap so the span resolves.
            PathRouteResolverOptions options = new PathRouteResolverOptions(1.0, false, false, true, true);

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), Vector3.Zero)),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(300, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, options, TestContext.CancellationToken);

            Assert.AreNotEqual(PathRouteSpanStatus.Unresolved, result.MainRoute.Spans[0].Status);
        }

        /// <summary>
        /// Verifies that a path node marked as a junction must actually be located on a junction.
        /// </summary>
        [TestMethod]
        public void ResolveWhenJunctionNodeIsNotAtJunctionThenReturnsNoJunctionNodeDiagnostic()
        {
            TrackWorld trackWorld = CreateInitializedTrackWorldWithTwoVectorNodes();
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(10, 0, 0))),
                    CreateNode(PathNodeType.Junction, 2, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(20, 0, 0))),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(210, 0, 0))))
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.NoJunctionNode && diagnostic.NodeIndex == 1));
            Assert.AreEqual(PathRouteDiagnosticSeverity.Error, result.HighestSeverity);
        }

        /// <summary>
        /// Verifies that looped track topology is not treated as an authored graph cycle.
        /// </summary>
        [TestMethod]
        public void ResolveWhenTrackTopologyLoopsWithoutAuthoredCycleDoesNotReturnGraphCycleDiagnostic()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateVectorNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 2, 3), CreateConnectors(2, 1, 3),
                    CreateConnectors(3, 1, 2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnsupportedGraphCycle));
        }

        [DataTestMethod]
        [DataRow("Loop")]
        [DataRow("JunctionLadder")]
        [DataRow("Siding")]
        [DataRow("BalloonLoop")]
        [DataRow("DeadEnd")]
        [DataRow("ParallelTrack")]
        [DataRow("AmbiguousSwitch")]
        public void WhenRepresentativeTopologyIsResolvedThenMainRouteConnectsAnchoredEndpoints(string topologyName)
        {
            TrackWorld trackWorld = CreateRepresentativeTrackWorld(topologyName);
            PathModel pathModel = CreateAnchoredEndpointPath(trackWorld);

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreNotEqual(PathRouteSpanStatus.Unresolved, result.MainRoute.Spans.Single().Status,
                string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}:{diagnostic.Code}:{diagnostic.Message}")));
        }

        /// <summary>
        /// Verifies that anchored spans without a deterministic dense connection are reported.
        /// </summary>
        [TestMethod]
        public void ResolveWhenAnchoredSpanHasNoDenseConnectionReturnsDiagnostic()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1), CreateConnectors(2)));

            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 2)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(PathRouteSpanStatus.Unresolved, result.MainRoute.Spans[0].Status);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnresolvedDenseSpan && diagnostic.FromNodeIndex == 0 && diagnostic.ToNodeIndex == 1));
        }

        /// <summary>
        /// Verifies that hybrid anchors with matching stored location and node index do not report a mismatch.
        /// </summary>
        [TestMethod]
        public void ResolveWhenNodeIndexMatchesLocationDoesNotReturnAnchorLocationMismatch()
        {
            TrackWorld trackWorld = CreateInitializedTrackWorldWithTwoVectorNodes();
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(10, 0, 0))),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(210, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AnchorLocationMismatch));
            Assert.AreEqual(1, result.AuthoredNodeAnchors[0].TrackNodeIndex);
        }

        /// <summary>
        /// Verifies that hybrid anchors report a mismatch when the stored node index disagrees with the stored location.
        /// </summary>
        [TestMethod]
        public void ResolveWhenNodeIndexDisagreesWithLocationReturnsAnchorLocationMismatch()
        {
            TrackWorld trackWorld = CreateInitializedTrackWorldWithTwoVectorNodes();
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, new WorldLocation(new Tile(0, 0), new Vector3(210, 0, 0))),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(210, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AnchorLocationMismatch && diagnostic.NodeIndex == 0));
            Assert.AreEqual(-1, result.AuthoredNodeAnchors[0].TrackNodeIndex);
        }

        /// <summary>
        /// Verifies that a hybrid anchor whose stored location cannot be re-anchored remains unresolved instead of
        /// silently continuing to use a stale indexed track node.
        /// </summary>
        [TestMethod]
        public void ResolveWhenHybridAnchorLocationCannotBeReanchoredReturnsAnchorDiagnostic()
        {
            TrackWorld trackWorld = CreateInitializedTrackWorldWithTwoVectorNodes();
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(500, 0, 0))),
                    CreateNode(PathNodeType.End, -1, -1, 2, new WorldLocation(new Tile(0, 0), new Vector3(210, 0, 0)))),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld, TestContext.CancellationToken);

            Assert.AreEqual(-1, result.AuthoredNodeAnchors[0].TrackNodeIndex);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AnchorNotOnTrack && diagnostic.NodeIndex == 0));
        }

        /// <summary>
        /// Verifies that passing links create passing routes when enabled.
        /// </summary>
        [TestMethod]
        public void ResolveWhenPassingLinkExistsBuildsPassingRoute()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, 2),
                    CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.HasCount(1, result.PassingRoutes);
            Assert.AreEqual(PathRouteBranchKind.Passing, result.PassingRoutes[0].BranchKind);
            Assert.AreEqual(0, result.PassingRoutes[0].StartNodeIndex);
            Assert.AreEqual(2, result.PassingRoutes[0].EndNodeIndex);
        }

        /// <summary>
        /// Verifies that a passing branch ending on a later main route node does not report a rejoin warning.
        /// </summary>
        [TestMethod]
        public void ResolveWhenPassingBranchRejoinsMainRouteDoesNotReturnRejoinDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, 2),
                    CreateNode(PathNodeType.Intermediate, 3),
                    CreateNode(PathNodeType.Intermediate, -1, 3),
                    CreateNode(PathNodeType.End, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain));
        }

        /// <summary>
        /// Verifies that a passing branch not ending on the remaining main route is reported.
        /// </summary>
        [TestMethod]
        public void ResolveWhenPassingBranchDoesNotRejoinMainRouteReturnsDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, 2),
                    CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain && diagnostic.FromNodeIndex == 0 && diagnostic.ToNodeIndex == 2));
        }

        /// <summary>
        /// Verifies that a passing branch rejoining an earlier main route node is reported.
        /// </summary>
        [TestMethod]
        public void ResolveWhenPassingBranchRejoinsEarlierMainRouteNodeReturnsDiagnostic()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Intermediate, 2),
                    CreateNode(PathNodeType.Intermediate, 3, 4), CreateNode(PathNodeType.End, -1), CreateNode(PathNodeType.Intermediate, -1, 1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain && diagnostic.FromNodeIndex == 2 && diagnostic.ToNodeIndex == 1));
        }

        /// <summary>
        /// Verifies that an intermediate siding node does not start a separate passing route.
        /// </summary>
        [TestMethod]
        public void ResolveWhenSidingChainHasIntermediateNodeBuildsSinglePassingRoute()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1, 2), CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1, 3), CreateNode(PathNodeType.Intermediate, 1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null, TestContext.CancellationToken);

            Assert.HasCount(1, result.PassingRoutes);
            Assert.AreEqual(0, result.PassingRoutes[0].StartNodeIndex);
        }

        private static PathNode CreateNode(PathNodeType nodeType, int nextMainNode, int nextSidingNode = -1, int nodeIndex = 0)
        {
            return CreateNode(nodeType, nextMainNode, nextSidingNode, nodeIndex, new WorldLocation(new Tile(0, 0), Vector3.Zero));
        }

        private static PathNode CreateNode(PathNodeType nodeType, int nextMainNode, int nextSidingNode, int nodeIndex, WorldLocation location)
        {
            return new PathNode(location)
            {
                NodeType = nodeType,
                NodeIndex = nodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
            };
        }

        private static TrackWorld CreateTrackWorldWithSingleVectorNode()
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), Vector3.Zero);
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 0));
            VectorNode vectorNode = new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = 1,
            };

            return CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, vectorNode),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1)));
        }

        private static TrackWorld CreateTrackWorld(ImmutableArray<TrackNodeBase> trackNodes, ImmutableArray<TrackNodeConnectorIndex> connectors)
        {
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = trackNodes,
                TrackNodeConnectors = connectors,
            };
            TrackModel trackModel = new TrackModel()
            {
                TrackDatabase = trackDatabase,
            };

            return (TrackWorld)Activator.CreateInstance(
                typeof(TrackWorld),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { trackModel },
                null);
        }

        private static TrackWorld CreateRepresentativeTrackWorld(string topologyName)
        {
            return topologyName switch
            {
                "Loop" => TrackWorldTestFixture.CreateLoopTrackWorld(),
                "JunctionLadder" => TrackWorldTestFixture.CreateJunctionLadderTrackWorld(),
                "Siding" => TrackWorldTestFixture.CreateSidingTrackWorld(),
                "BalloonLoop" => TrackWorldTestFixture.CreateBalloonLoopTrackWorld(),
                "DeadEnd" => TrackWorldTestFixture.CreateDeadEndTrackWorld(),
                "ParallelTrack" => TrackWorldTestFixture.CreateParallelTrackWorld(),
                "AmbiguousSwitch" => TrackWorldTestFixture.CreateAmbiguousSwitchTrackWorld(),
                _ => throw new ArgumentException($"Unknown representative topology '{topologyName}'.", nameof(topologyName)),
            };
        }

        private static PathModel CreateAnchoredEndpointPath(TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = "representative-main-path",
                Name = "Representative Main Path",
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, -1, 1, trackWorld.TrackDatabase.TrackNodes[1].Location),
                    CreateNode(PathNodeType.End, -1, -1, 2, trackWorld.TrackDatabase.TrackNodes[2].Location)),
            };
        }

        private static TrackWorld CreateInitializedTrackWorldWithTwoVectorNodes()
        {
            VectorNode firstNode = CreateInitializedVectorNode(1, 0);
            VectorNode secondNode = CreateInitializedVectorNode(2, 200);
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(CreateInitializedVectorNode(0, -200), firstNode, secondNode),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1), CreateConnectors(2)),
            };
            InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel()
            {
                TrackDatabase = trackDatabase,
            };
            TrackSectionModel trackSectionModel = new TrackSectionModel()
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection()
                {
                    SectionIndex = 1,
                    Gauge = 1.435f,
                    Length = 100,
                }),
            };

            return TrackWorld.Initialize(null, trackModel, trackSectionModel);
        }

        private static TrackWorld CreateInitializedLoopTrackWorld()
        {
            WorldLocation junctionLocation = new(new Tile(0, 0), Vector3.Zero);
            VectorNode loop = new(junctionLocation, new Tile(0, 0), junctionLocation)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(
                    CreateSection(0, 0, 100, 0, MathHelper.PiOver2),
                    CreateSection(100, 0, 100, 100, 0),
                    CreateSection(100, 100, 0, 100, -MathHelper.PiOver2),
                    CreateSection(0, 100, 0, 0, MathHelper.Pi)),
            };
            VectorNode exit = new(junctionLocation, new Tile(0, 0), new WorldLocation(new Tile(0, 0), new Vector3(-100, 0, 0)))
            {
                NodeIndex = 2,
                VectorSections = ImmutableArray.Create(CreateSection(0, 0, -100, 0, -MathHelper.PiOver2)),
            };
            JunctionNode junction = CreateJunctionNode(3);
            TrackDatabase trackDatabase = new()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, loop, exit, junction),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex(),
                    CreateDirectionalConnectors(1, (3, TrackDirection.Reverse), (3, TrackDirection.Ahead)),
                    CreateDirectionalConnectors(2, (3, TrackDirection.Reverse)),
                    CreateDirectionalConnectors(3, (1, TrackDirection.Reverse), (1, TrackDirection.Ahead), (2, TrackDirection.Reverse))),
            };
            InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new() { TrackDatabase = trackDatabase };
            TrackSectionModel trackSectionModel = new()
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection
                {
                    SectionIndex = 1,
                    Gauge = 1.435f,
                    Length = 100,
                }),
            };
            return TrackWorld.Initialize(null, trackModel, trackSectionModel);
        }

        private static VectorSectionNode CreateSection(float startX, float startZ, float endX, float endZ, float direction)
        {
            return new VectorSectionNode(
                new WorldLocation(new Tile(0, 0), new Vector3(startX, 0, startZ)),
                new Tile(0, 0),
                new Vector3(0, direction, 0),
                new WorldLocation(new Tile(0, 0), new Vector3(endX, 0, endZ)))
            {
                NodeIndex = 1,
            };
        }

        private static TrackNodeConnectorIndex CreateDirectionalConnectors(int nodeIndex, params (int Link, TrackDirection Direction)[] connectors)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = connectors.Select(connector => new TrackNodeConnector
                {
                    Link = connector.Link,
                    Direction = connector.Direction,
                }).ToImmutableArray(),
            };
        }

        private static void InitializeTrackDatabase(TrackDatabase trackDatabase)
        {
            _ = typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            _ = typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
        }

        private static VectorNode CreateInitializedVectorNode(int nodeIndex, float startX)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(startX, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(startX + 100, 0, 0));
            // Heading along +X (Direction.Y == PiOver2) so geometry and endpoints agree.
            VectorSectionNode section = new VectorSectionNode(start, new Tile(0, 0), new Vector3(0, MathHelper.PiOver2, 0), end)
            {
                NodeIndex = 1,
            };

            return new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = nodeIndex,
                VectorSections = ImmutableArray.Create(section),
            };
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params int[] linkedNodeIndexes)
        {
            return new TrackNodeConnectorIndex()
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = linkedNodeIndexes.Select(link => new TrackNodeConnector() { Link = link }).ToImmutableArray(),
            };
        }

        private static VectorNode CreateVectorNode(int nodeIndex)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3((nodeIndex * 100) + 50, 0, 0));
            return new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = nodeIndex,
            };
        }

        private static JunctionNode CreateJunctionNode(int nodeIndex)
        {
            return new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0)), new Tile(0, 0), Vector3.Zero)
            {
                NodeIndex = nodeIndex,
            };
        }

        public TestContext TestContext { get; set; }

        private static readonly int[] expectedArray12 = new[] { 1, 2 };
        private static readonly int[] expectedArray152 = new[] { 1, 5, 2 };
        private static readonly int[] expectedArray132 = new[] { 1, 3, 2 };
        private static readonly int[] expectedArray142 = new[] { 1, 4, 2 };
        private static readonly int[] expectedArray134 = new[] { 1, 3, 4 };
    }
}
