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
            CollectionAssert.AreEqual(expectedArray12, result.MainRoute.Spans[0].TrackVectorNodeIndexes.ToArray());
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
            CollectionAssert.AreEqual(expectedArray152, result.MainRoute.Spans[0].TrackVectorNodeIndexes.ToArray());
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
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
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
    }
}
