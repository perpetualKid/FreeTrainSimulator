using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    /// <summary>Validates the public main-span selection contract independently of editor eligibility checks.</summary>
    [TestClass]
    public class PathRouteSelectionTests
    {
        /// <summary>Rejects negative, terminal, and out-of-range keys rather than ignoring explicit choices.</summary>
        [TestMethod]
        [DataRow(-1)]
        [DataRow(1)]
        [DataRow(2)]
        public void ResolveSelectedRoutesWhenKeyIsNotMainSpanSourceThenThrows(int sourceIndex)
        {
            PathModel source = CreatePath();
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(sourceIndex, CreateCandidate(1));

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathRouteResolver.ResolveSelectedRoutes(source, null, PathRouteResolverOptions.Default, selections, CancellationToken.None));
        }

        /// <summary>A reachable siding-only node is not a source of a main-route span.</summary>
        [TestMethod]
        public void ResolveSelectedRoutesWhenKeyIsSidingOnlyThenThrows()
        {
            PathModel source = CreatePath();
            source = source with
            {
                PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NextSidingNode = 2 })
                    .Add(CreateNode(PathNodeType.Via, -1, 50) with { NextSidingNode = 1 }),
            };
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(2, CreateCandidate(1));

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathRouteResolver.ResolveSelectedRoutes(source, null, PathRouteResolverOptions.Default, selections, CancellationToken.None));
        }

        /// <summary>The selection collection is required even when it contains no choices.</summary>
        [TestMethod]
        public void ResolveSelectedRoutesWhenSelectionsAreNullThenThrows()
        {
            _ = Assert.ThrowsExactly<ArgumentNullException>(() => PathRouteResolver.ResolveSelectedRoutes(CreatePath(), null, PathRouteResolverOptions.Default, null, CancellationToken.None));
        }

        /// <summary>A null candidate is not an implicit default selection.</summary>
        [TestMethod]
        public void ResolveSelectedRoutesWhenCandidateIsNullThenThrows()
        {
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(0, null);

            _ = Assert.ThrowsExactly<ArgumentNullException>(() => PathRouteResolver.ResolveSelectedRoutes(CreatePath(), null, PathRouteResolverOptions.Default, selections, CancellationToken.None));
        }

        /// <summary>Candidate identity is its node sequence, not the instance or immutable-array allocation.</summary>
        [TestMethod]
        public void ResolveSelectedRoutesWhenCandidateSequenceIsSeparatelyAllocatedThenResolves()
        {
            PathModel source = CreatePath();
            TrackWorld world = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            ResolvedRouteCandidate candidate = PathRouteResolver.Resolve(source, world).MainRoute.Spans[0].Candidates.Single();
            ResolvedRouteCandidate equivalent = candidate with
            {
                RouteNodeIndexes = ImmutableArray.CreateRange(candidate.RouteNodeIndexes.ToArray()),
            };
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(0, equivalent);

            PathRouteResolution result = PathRouteResolver.ResolveSelectedRoutes(source, world, PathRouteResolverOptions.Default, selections, CancellationToken.None);

            Assert.AreEqual(PathRouteSpanStatus.Resolved, result.MainRoute.Spans[0].Status);
        }

        /// <summary>A sequence with different endpoint nodes cannot be silently replaced by the default route.</summary>
        [TestMethod]
        public void ResolveSelectedRoutesWhenCandidateEndpointsDoNotMatchThenRemainsUnresolved()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty
                .Add(0, CreateCandidate(1, 2));

            PathRouteResolution result = PathRouteResolver.ResolveSelectedRoutes(CreatePath(), world, PathRouteResolverOptions.Default,
                selections, CancellationToken.None);

            Assert.AreEqual(PathRouteSpanStatus.Unresolved, result.MainRoute.Spans[0].Status);
        }

        /// <summary>Cancellation applies to selected-route resolution as well as ordinary resolution.</summary>
        [TestMethod]
        public void ResolveSelectedRoutesWhenCanceledThenThrows()
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            _ = Assert.ThrowsExactly<OperationCanceledException>(() => PathRouteResolver.ResolveSelectedRoutes(CreatePath(), null, PathRouteResolverOptions.Default, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty, cancellation.Token));
        }

        private static PathModel CreatePath()
        {
            return new PathModel
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1, 0), CreateNode(PathNodeType.End, -1, 100)),
            };
        }

        private static PathNode CreateNode(PathNodeType nodeType, int nextMainNode, float x)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeType = nodeType,
                NodeIndex = 1,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static ResolvedRouteCandidate CreateCandidate(params int[] route)
        {
            return new ResolvedRouteCandidate(route.ToImmutableArray(), route.ToImmutableArray(), ImmutableArray<PathRouteAnchor>.Empty, 0);
        }
    }
}
