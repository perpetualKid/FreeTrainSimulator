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
    [TestClass]
    public class PathModelRouteGeneratorTests
    {
        [TestMethod]
        public void WhenResolvedRouteHasGeneratedAnchorThenPathModelContainsLinkedIntermediaryNode()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            PathModel sourcePath = CreateSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GenerateMainPath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsTrue(result.Success);
            Assert.HasCount(3, result.PathModel.PathNodes);
            Assert.AreEqual(PathNodeType.Start, result.PathModel.PathNodes[0].NodeType);
            Assert.AreEqual(PathNodeType.Intermediate, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[2].NodeType);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(-1, result.PathModel.PathNodes[2].NextMainNode);
            Assert.AreEqual(3, result.PathModel.PathNodes[1].NodeIndex);
        }

        [TestMethod]
        public void WhenGeneratingPathThenMetadataIsPreserved()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            PathModel sourcePath = CreateSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GenerateMainPath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.AreEqual(sourcePath.Id, result.PathModel.Id);
            Assert.AreEqual(sourcePath.Name, result.PathModel.Name);
            Assert.AreEqual(sourcePath.Start, result.PathModel.Start);
            Assert.AreEqual(sourcePath.End, result.PathModel.End);
            Assert.AreEqual(sourcePath.PlayerPath, result.PathModel.PlayerPath);
        }

        [TestMethod]
        public void WhenRebuildingPathThenAuthoredWaitAndReversalFlagsArePreserved()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            PathNodeWaitInfo waitInfo = new PathNodeWaitInfo { WaitTime = 30 };
            PathModel sourcePath = CreateSourcePath(PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal, waitInfo);
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GenerateMainPath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            PathNode endNode = result.PathModel.PathNodes[^1];
            Assert.IsTrue((endNode.NodeType & PathNodeType.End) == PathNodeType.End);
            Assert.IsTrue((endNode.NodeType & PathNodeType.Wait) == PathNodeType.Wait);
            Assert.IsTrue((endNode.NodeType & PathNodeType.Reversal) == PathNodeType.Reversal);
            Assert.AreEqual(waitInfo, endNode.WaitInfo);
        }

        [TestMethod]
        public void WhenResolvedRouteIsAmbiguousThenGenerationFailsByDefault()
        {
            TrackWorld trackWorld = CreateAmbiguousTrackWorld();
            PathModel sourcePath = CreateSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GenerateMainPath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(sourcePath, result.PathModel);
            Assert.Contains("ambiguous", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void WhenTieBreakingAllowedThenAmbiguousResolvedRouteCanGeneratePath()
        {
            TrackWorld trackWorld = CreateAmbiguousTrackWorld();
            PathModel sourcePath = CreateSourcePath();
            PathRouteResolverOptions options = new PathRouteResolverOptions(5000.0, false, true, true, true);
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, options, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GenerateMainPath(sourcePath, resolution, trackWorld, options);

            Assert.IsTrue(result.Success);
            Assert.HasCount(3, result.PathModel.PathNodes);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute));
        }

        private static PathModel CreateSourcePath()
        {
            return CreateSourcePath(PathNodeType.End, null);
        }

        private static PathModel CreateSourcePath(PathNodeType endNodeType, PathNodeWaitInfo waitInfo)
        {
            return new PathModel
            {
                Id = "path-id",
                Name = "Path Name",
                Start = "Start Location",
                End = "End Location",
                PlayerPath = true,
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(PathNodeType.Start, 1, 1, null),
                    CreatePathNode(endNodeType, -1, 2, waitInfo)),
            };
        }

        private static PathNode CreatePathNode(PathNodeType nodeType, int nextMainNode, int nodeIndex, PathNodeWaitInfo waitInfo)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
                NodeIndex = nodeIndex,
                WaitInfo = waitInfo,
            };
        }

        private static TrackWorld CreateAmbiguousTrackWorld()
        {
            return CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3, 4),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)));
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
    }
}
