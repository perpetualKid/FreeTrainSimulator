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

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

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

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

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
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 4),
                    CreateNode(PathNodeType.End, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

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
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1),
                    CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnreachableNode && diagnostic.NodeIndex == 2));
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

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

            Assert.IsNotNull(result.MainRoute);
            Assert.AreEqual(2, result.MainRoute.Spans.Length);
            Assert.AreEqual(0, result.MainRoute.Spans[0].FromNodeIndex);
            Assert.AreEqual(1, result.MainRoute.Spans[0].ToNodeIndex);
            Assert.AreEqual(1, result.MainRoute.Spans[1].FromNodeIndex);
            Assert.AreEqual(2, result.MainRoute.Spans[1].ToNodeIndex);
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
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, nodeIndex: 1),
                    CreateNode(PathNodeType.End, -1, nodeIndex: 1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, trackWorld);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[0].Status);
            Assert.AreEqual(1, result.MainRoute.Spans[0].TrackVectorNodeIndexes.Length);
            Assert.AreEqual(1, result.MainRoute.Spans[0].TrackVectorNodeIndexes[0]);
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

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

            Assert.AreEqual(1, result.PassingRoutes.Length);
            Assert.AreEqual(PathRouteBranchKind.Passing, result.PassingRoutes[0].BranchKind);
            Assert.AreEqual(0, result.PassingRoutes[0].StartNodeIndex);
            Assert.AreEqual(2, result.PassingRoutes[0].EndNodeIndex);
        }

        /// <summary>
        /// Verifies that an intermediate siding node does not start a separate passing route.
        /// </summary>
        [TestMethod]
        public void ResolveWhenSidingChainHasIntermediateNodeBuildsSinglePassingRoute()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNode(PathNodeType.Start, 1, 2),
                    CreateNode(PathNodeType.End, -1),
                    CreateNode(PathNodeType.Intermediate, -1, 3),
                    CreateNode(PathNodeType.Intermediate, 1)),
            };

            PathRouteResolution result = PathRouteResolver.Resolve(pathModel, null);

            Assert.AreEqual(1, result.PassingRoutes.Length);
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

            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vectorNode),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex(),
                    new TrackNodeConnectorIndex() { NodeIndex = 1, TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty }),
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
    }
}
