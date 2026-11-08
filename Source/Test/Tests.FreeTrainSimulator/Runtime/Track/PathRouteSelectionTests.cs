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
    /// <summary>Validates the public main and passing span selection contracts independently of editor eligibility checks.</summary>
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
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
            ResolvedRouteCandidate candidate = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken).MainRoute.Spans[0].Candidates.Single();
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
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty .Add(0, CreateCandidate(1, 2));

            PathRouteResolution result = PathRouteResolver.ResolveSelectedRoutes(CreatePath(), world, PathRouteResolverOptions.Default, selections, CancellationToken.None);

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

        /// <summary>Passing selections reject main-only, terminal, and out-of-range span sources.</summary>
        [TestMethod]
        [DataRow(-1)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(4)]
        public void ResolveSelectedPassingRoutesWhenKeyIsNotBranchSpanSourceThenThrows(int sourceIndex)
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();
            ImmutableDictionary<int, ResolvedRouteCandidate> selections = ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(sourceIndex, CreateCandidate(1));

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathRouteResolver.ResolveSelectedPassingRoutes( CreatePassingPath(world), world, PathRouteResolverOptions.Default, 0, selections, CancellationToken.None));
        }

        /// <summary>Only the structural branch start can own exact passing selections.</summary>
        [TestMethod]
        [DataRow(-1)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void ResolveSelectedPassingRoutesWhenOwnerIsNotBranchStartThenThrows(int branchStart)
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathRouteResolver.ResolveSelectedPassingRoutes(CreatePassingPath(world), world, PathRouteResolverOptions.Default, branchStart, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty, CancellationToken.None));
        }

        /// <summary>Passing selection collections are required, not implicit default choices.</summary>
        [TestMethod]
        public void ResolveSelectedPassingRoutesWhenSelectionsAreNullThenThrows()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();

            _ = Assert.ThrowsExactly<ArgumentNullException>(() => PathRouteResolver.ResolveSelectedPassingRoutes(CreatePassingPath(world), world, PathRouteResolverOptions.Default, 0, null, CancellationToken.None));
        }

        /// <summary>A null passing candidate cannot silently fall back to an arbitrary route.</summary>
        [TestMethod]
        public void ResolveSelectedPassingRoutesWhenCandidateIsNullThenThrows()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();

            _ = Assert.ThrowsExactly<ArgumentNullException>(() => PathRouteResolver.ResolveSelectedPassingRoutes(CreatePassingPath(world), world, PathRouteResolverOptions.Default, 0, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(0, null), CancellationToken.None));
        }

        /// <summary>Exact passing choices resolve by sequence identity without changing the main traversal.</summary>
        [TestMethod]
        public void ResolveSelectedPassingRoutesWhenEquivalentChoiceIsSelectedThenMainAndPassingTraversalArePreserved()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreatePassingPath(world);
            PathRouteResolution baseline = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);
            ResolvedRouteCandidate candidate = baseline.PassingRoutes.Single().Spans[0].Candidates.Single();
            ResolvedRouteCandidate equivalent = candidate with { RouteNodeIndexes = candidate.RouteNodeIndexes.ToArray().ToImmutableArray() };

            PathRouteResolution result = PathRouteResolver.ResolveSelectedPassingRoutes(source, world, PathRouteResolverOptions.Default, 0, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(0, equivalent), CancellationToken.None);

            Assert.AreEqual((true, true, true), (result.IsValid, baseline.MainRoute.HasSamePhysicalTraversal(result.MainRoute), baseline.PassingRoutes.Single().HasSamePhysicalTraversal(result.PassingRoutes.Single())));
        }

        /// <summary>A main-route choice cannot substitute for a siding span or alter the main route.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(3)]
        public void ResolveSelectedPassingRoutesWhenMainChoiceIsUsedForBranchThenBranchIsUnresolvedAndMainUntouched(int sourceIndex)
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreatePassingPath(world);
            PathRouteResolution baseline = PathRouteResolver.Resolve(source, world, TestContext.CancellationToken);
            ResolvedRouteCandidate wrong = baseline.MainRoute.Spans[0].Candidates.Single();

            PathRouteResolution result = PathRouteResolver.ResolveSelectedPassingRoutes(source, world, PathRouteResolverOptions.Default, 0, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(sourceIndex, wrong), CancellationToken.None);

            Assert.AreEqual((PathRouteSpanStatus.Unresolved, true),
                (result.PassingRoutes.Single().Spans.Single(span => span.FromNodeIndex == sourceIndex).Status,
                baseline.MainRoute.HasSamePhysicalTraversal(result.MainRoute)));
        }

        /// <summary>Passing resolution observes cancellation before resolving any explicit choices.</summary>
        [TestMethod]
        public void ResolveSelectedPassingRoutesWhenCanceledThenThrows()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSidingTrackWorld();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            _ = Assert.ThrowsExactly<OperationCanceledException>(() => PathRouteResolver.ResolveSelectedPassingRoutes(
                CreatePassingPath(world), world, PathRouteResolverOptions.Default, 0,
                ImmutableDictionary<int, ResolvedRouteCandidate>.Empty, cancellation.Token));
        }

        private static PathModel CreatePassingPath(TrackWorld world)
        {
            PathNode Anchor(int trackIndex, PathNodeType type, int nextMain, int nextSiding) => new PathNode(world.TrackDatabase.TrackNodes[trackIndex].Location)
            {
                NodeType = type,
                NodeIndex = trackIndex,
                NextMainNode = nextMain,
                NextSidingNode = nextSiding,
            };

            return new PathModel
            {
                PathNodes = [Anchor(1, PathNodeType.Start, 1, 3), Anchor(4, PathNodeType.Via, 2, -1), Anchor(2, PathNodeType.End, -1, -1), Anchor(5, PathNodeType.Via, -1, 2)],
            };
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

        public TestContext TestContext { get; set; }
    }
}
