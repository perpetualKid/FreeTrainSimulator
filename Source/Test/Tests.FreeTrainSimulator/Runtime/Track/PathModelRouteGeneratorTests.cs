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

using MemoryPack;

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
        public void WhenGeneratedPathIsResolvedAgainThenMaterializationIsNearIdempotent()
        {
            TrackWorld trackWorld = CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3), CreateConnectors(2, 3), CreateConnectors(3, 1, 2)));
            PathModel sourcePath = CreateSourcePath();
            PathRouteResolution firstResolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);
            PathGenerationResult firstGeneration = PathModelRouteGenerator.GenerateMainPath(sourcePath, firstResolution, trackWorld, PathRouteResolverOptions.Default);

            PathRouteResolution secondResolution = PathRouteResolver.Resolve(firstGeneration.PathModel, trackWorld, TestContext.CancellationToken);
            PathGenerationResult secondGeneration = PathModelRouteGenerator.GenerateMainPath(firstGeneration.PathModel, secondResolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsTrue(secondGeneration.Success);
            Assert.AreSequenceEqual(firstGeneration.PathModel.PathNodes, secondGeneration.PathModel.PathNodes);
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
            Assert.IsTrue(endNode.NodeType.Includes(PathNodeType.End));
            Assert.IsTrue(endNode.NodeType.Includes(PathNodeType.Wait));
            Assert.IsTrue(endNode.NodeType.Includes(PathNodeType.Reversal));
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

        [TestMethod]
        public void WhenGeneratePathHasPassingBranchThenSidingNodeIsWeavedAndRejoinsMain()
        {
            TrackWorld trackWorld = CreatePassingTrackWorld();
            PathModel sourcePath = CreatePassingSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GeneratePath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsTrue(result.Success);
            Assert.HasCount(4, result.PathModel.PathNodes);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(3, result.PathModel.PathNodes[0].NextSidingNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(-1, result.PathModel.PathNodes[2].NextMainNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[3].NextSidingNode);
        }

        [TestMethod]
        public void WhenGeneratePathHasPassingBranchThenSidingNodeKeepsTrackAnchor()
        {
            TrackWorld trackWorld = CreatePassingTrackWorld();
            PathModel sourcePath = CreatePassingSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GeneratePath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            PathNode sidingNode = result.PathModel.PathNodes[3];
            Assert.AreEqual(3, sidingNode.NodeIndex);
            Assert.IsTrue(sidingNode.NodeType.Includes(PathNodeType.Intermediate));
        }

        [TestMethod]
        public void WhenGenerateMainPathHasPassingBranchThenSidingLinksAreDropped()
        {
            TrackWorld trackWorld = CreatePassingTrackWorld();
            PathModel sourcePath = CreatePassingSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GenerateMainPath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsTrue(result.Success);
            Assert.HasCount(3, result.PathModel.PathNodes);
            Assert.IsTrue(result.PathModel.PathNodes.All(node => node.NextSidingNode == -1));
        }

        [TestMethod]
        public void WhenGeneratePathPassingBranchDoesNotRejoinMainThenGenerationIsRefused()
        {
            TrackWorld trackWorld = CreatePassingTrackWorld();
            PathModel sourcePath = CreateNonRejoiningPassingSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);

            PathGenerationResult result = PathModelRouteGenerator.GeneratePath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(sourcePath, result.PathModel);
            Assert.Contains("rejoin", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void WhenGeneratedPassingPathIsResolvedAgainThenPassingBranchRejoinsWithoutDiagnostic()
        {
            TrackWorld trackWorld = CreatePassingTrackWorld();
            PathModel sourcePath = CreatePassingSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);
            PathGenerationResult result = PathModelRouteGenerator.GeneratePath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            PathRouteResolution regenerated = PathRouteResolver.Resolve(result.PathModel, trackWorld, TestContext.CancellationToken);

            Assert.HasCount(1, regenerated.PassingRoutes);
            Assert.IsFalse(regenerated.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain));
        }

        [TestMethod]
        public void WhenGeneratedPassingPathRoundTripsThenSidingChainIsPreserved()
        {
            TrackWorld trackWorld = CreatePassingTrackWorld();
            PathModel sourcePath = CreatePassingSourcePath();
            PathRouteResolution resolution = PathRouteResolver.Resolve(sourcePath, trackWorld, TestContext.CancellationToken);
            PathGenerationResult result = PathModelRouteGenerator.GeneratePath(sourcePath, resolution, trackWorld, PathRouteResolverOptions.Default);

            byte[] serialized = MemoryPackSerializer.Serialize(result.PathModel);
            PathModel restored = MemoryPackSerializer.Deserialize<PathModel>(serialized);

            Assert.AreEqual(3, restored.PathNodes[0].NextSidingNode);
            Assert.AreEqual(2, restored.PathNodes[3].NextSidingNode);
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

        // Authored path with a single passing branch that rejoins the main route:
        // node0 (start) --main--> node1 --main--> node3 (end)
        //               \--siding--> node2 --siding--> node3 (rejoin)
        // Track nodes 1..4 are directly connected so the resolved spans carry no generated intermediaries.
        private static PathModel CreatePassingSourcePath()
        {
            return new PathModel
            {
                Id = "passing-path-id",
                Name = "Passing Path",
                Start = "Start Location",
                End = "End Location",
                PlayerPath = true,
                PathNodes = ImmutableArray.Create(
                    CreatePassingNode(PathNodeType.Start, 1, 2, 1),
                    CreatePassingNode(PathNodeType.Intermediate, 3, -1, 2),
                    CreatePassingNode(PathNodeType.Intermediate, -1, 3, 3),
                    CreatePassingNode(PathNodeType.End, -1, -1, 4)),
            };
        }

        // Same topology as CreatePassingSourcePath but the siding node does not rejoin the main route
        // (its NextSidingNode is -1), so passing-branch generation must refuse the path.
        private static PathModel CreateNonRejoiningPassingSourcePath()
        {
            return new PathModel
            {
                Id = "broken-passing-path-id",
                Name = "Broken Passing Path",
                Start = "Start Location",
                End = "End Location",
                PlayerPath = true,
                PathNodes = ImmutableArray.Create(
                    CreatePassingNode(PathNodeType.Start, 1, 2, 1),
                    CreatePassingNode(PathNodeType.Intermediate, 3, -1, 2),
                    CreatePassingNode(PathNodeType.Intermediate, -1, -1, 3),
                    CreatePassingNode(PathNodeType.End, -1, -1, 4)),
            };
        }

        private static PathNode CreatePassingNode(PathNodeType nodeType, int nextMainNode, int nextSidingNode, int nodeIndex)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
                NodeIndex = nodeIndex,
            };
        }

        private static TrackWorld CreatePassingTrackWorld()
        {
            return CreateTrackWorld(
                ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateVectorNode(3), CreateVectorNode(4)),
                ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 2, 3), CreateConnectors(2, 1, 4),
                    CreateConnectors(3, 1, 4), CreateConnectors(4, 2, 3)));
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
