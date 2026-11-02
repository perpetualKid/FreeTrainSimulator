using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;

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
    /// Covers <see cref="PathModelEditor.CombinePaths(PathModel, PathModel, TrackWorld, CancellationToken)"/>:
    /// admission refusals, the single-viable-orientation success path, zero-viable refusal, and the
    /// multiple-viable-orientations refusal that requires a loop topology to reach.
    /// </summary>
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathModelEditorCombineTests
    {
        public TestContext TestContext { get; set; }

        private static readonly float[] expectedArrayf10406090 = new[] { 10f, 40f, 60f, 90f };
        private static readonly int[] expectedArray135462 = new[] { 1, 3, 4, 6, 2 };
        private static readonly int[] expectedArray13562 = new[] { 1, 3, 5, 6, 2 };
        private static readonly int[] expectedArray2463 = new[] { 2, 4, 6, 3 };
        private static readonly int[] expectedArray2513 = new[] { 2, 5, 1, 3 };
        private static readonly int[] expectedArray27138 = new[] { 2, 7, 1, 3, 8 };

        [TestMethod]
        public void WhenCombiningTheSameInstanceThenItIsRefused()
        {
            TrackWorld world = CreateSingleVectorNodeWorld();
            PathModel first = CreateCombinePath("first", 10, 40);

            PathEditResult result = PathModelEditor.CombinePaths(first, first, world, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PathEditorMessageKind.CombineChooseDifferentPaths, result.MessageKind);
            Assert.AreSame(first, result.PathModel);
        }

        [TestMethod]
        public void WhenOnlyTheConnectingSpanIsAmbiguousThenCombineRetainsBothCandidates()
        {
            TrackWorld world = CreateTiedConnectingWorld();
            PathModel first = CreateCombinePath("first", -90, -70);
            PathModel second = CreateCombinePath("second", 120, 170) with
            {
                PathNodes = ImmutableArray.Create(
                    Anchor(120, 2) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(170, 2) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };
            PathEditResult combination = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);
            Assert.IsTrue(combination.Success, combination.Message);
            PathRouteResolution resolution = PathRouteResolver.Resolve(combination.PathModel, world, TestContext.CancellationToken);
            ResolvedPathSpan join = resolution.MainRoute.Spans.Single(span => span.Status == PathRouteSpanStatus.Ambiguous);
            Assert.AreEqual(1, join.FromNodeIndex);
            Assert.HasCount(2, join.Candidates);
            Assert.AreSequenceEqual(expectedArray135462, join.Candidates[0].RouteNodeIndexes);
            Assert.AreSequenceEqual(expectedArray13562, join.Candidates[1].RouteNodeIndexes);
            Assert.HasCount(2, first.PathNodes);
            Assert.HasCount(2, second.PathNodes);
        }

        [TestMethod]
        public void WhenACombineSourceRevisitsTrackThenItIsRefused()
        {
            TrackWorld world = CreateLoopWorld();
            PathModel first = CreateCombinePath("first", 20, 80) with
            {
                PathNodes = ImmutableArray.Create(
                    Anchor(20, 1) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(80, 4) with { NodeType = PathNodeType.Via, NextMainNode = 2, NextSidingNode = -1 },
                    Anchor(80, 1) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };
            PathEditResult result = PathModelEditor.CombinePaths(first, CreateCombinePath("second", 85, 95), world, TestContext.CancellationToken);
            Assert.IsFalse(result.Success);
            Assert.AreSame(first, result.PathModel);
        }

        [TestMethod]
        public void WhenASecondaryConnectingCandidateRevisitsTheSourceThenThatOrientationIsNotViable()
        {
            int[][] links = [[], [7, 5, 3], [7, 4, 5], [1, 6, 8], [2, 6], [2, 1], [4, 3], [1, 2], [3]];
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = Enumerable.Range(0, links.Length).Select(index => index == 0 ? null
                    : (TrackNodeBase)new JunctionNode(AtX(index * 10), new Tile(0, 0), Vector3.Zero) { NodeIndex = index }).ToImmutableArray(),
                TrackNodeConnectors = Enumerable.Range(0, links.Length).Select(index => new TrackNodeConnectorIndex
                {
                    NodeIndex = index,
                    TrackNodeConnectors = links[index].Select(link => new TrackNodeConnector { Link = link }).ToImmutableArray(),
                }).ToImmutableArray(),
            };
            InitializeTrackDatabase(database);
            TrackWorld world = TrackWorld.Create(new TrackModel { TrackDatabase = database }, CreateTrackSectionModel());
            PathModel first = new PathModel
            {
                Id = "first",
                PathNodes = [Anchor(10, 1) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(70, 7) with { NodeType = PathNodeType.Via, NextMainNode = 2, NextSidingNode = -1 },
                    Anchor(20, 2) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }],
            };
            PathModel second = new PathModel
            {
                Id = "second",
                PathNodes = [Anchor(30, 3) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(80, 8) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }],
            };
            PathModel normal = first with
            {
                PathNodes = [first.PathNodes[0], first.PathNodes[1], first.PathNodes[2] with { NodeType = PathNodeType.Via, NextMainNode = 3 },
                    second.PathNodes[0] with { NodeType = PathNodeType.Via, NextMainNode = 4 }, second.PathNodes[1]],
            };
            PathRouteResolution normalResolution = PathRouteResolver.Resolve(normal, world, TestContext.CancellationToken);
            Assert.IsTrue(normalResolution.IsValid, string.Join(Environment.NewLine, normalResolution.Diagnostics.Select(diagnostic => diagnostic.Message)));
            ResolvedPathSpan join = normalResolution.MainRoute.Spans.Single(span => span.FromNodeIndex == 2);
            Assert.HasCount(2, join.Candidates);
            Assert.AreSequenceEqual(expectedArray2463, join.Candidates[0].RouteNodeIndexes);
            Assert.AreSequenceEqual(expectedArray2513, join.Candidates[1].RouteNodeIndexes);

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreSequenceEqual(expectedArray27138, result.PathModel.PathNodes.Select(node => node.NodeIndex));
        }

        [TestMethod]
        public void WhenCombiningPathsWithTheSameIdThenItIsRefused()
        {
            TrackWorld world = CreateSingleVectorNodeWorld();
            PathModel first = CreateCombinePath("shared-id", 10, 40);
            PathModel second = CreateCombinePath("SHARED-ID", 60, 90);

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PathEditorMessageKind.CombineChooseDifferentPaths, result.MessageKind);
            Assert.AreSame(first, result.PathModel);
        }

        [TestMethod]
        public void WhenCombiningPathsFromDifferentRoutesThenTheyAreRefused()
        {
            TrackWorld world = CreateSingleVectorNodeWorld();
            PathModel first = CreateCombinePath("first", 10, 40);
            PathModel second = CreateCombinePath("second", 60, 90);
            first.Initialize(new RouteModel(first.PathNodes[0].Location) { Id = "route-a" });
            second.Initialize(new RouteModel(second.PathNodes[0].Location) { Id = "route-b" });

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PathEditorMessageKind.CombineRouteMismatch, result.MessageKind);
            Assert.AreSame(first, result.PathModel);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenEitherCombinePathHasAWaitPayloadThenItIsRefused(bool changeSecond)
        {
            TrackWorld world = CreateSingleVectorNodeWorld();
            PathModel first = CreateCombinePath("first", 10, 40);
            PathModel second = CreateCombinePath("second", 60, 90);
            PathModel target = changeSecond ? second : first;
            target = target with { PathNodes = target.PathNodes.SetItem(0, target.PathNodes[0] with { WaitInfo = new PathNodeWaitInfo { WaitTime = 0 } }) };
            first = changeSecond ? first : target;
            second = changeSecond ? target : second;

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PathEditorMessageKind.CombineChainRequired, result.MessageKind);
            Assert.AreSame(first, result.PathModel);
        }

        [TestMethod]
        public void WhenCombineHasNoTrackWorldThenItIsRefused()
        {
            PathModel first = CreateCombinePath("first", 10, 40);

            PathEditResult result = PathModelEditor.CombinePaths(first, CreateCombinePath("second", 60, 90), null, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreSame(first, result.PathModel);
        }

        [TestMethod]
        public void WhenCombineIsCanceledThenItThrowsWithoutChangingEitherModel()
        {
            TrackWorld world = CreateSingleVectorNodeWorld();
            PathModel first = CreateCombinePath("first", 10, 40);
            PathModel second = CreateCombinePath("second", 60, 90);

            Assert.ThrowsExactly<OperationCanceledException>(() => PathModelEditor.CombinePaths(first, second, world, new CancellationToken(true)));
            Assert.HasCount(2, first.PathNodes);
            Assert.HasCount(2, second.PathNodes);
        }

        /// <summary>
        /// On a single vector node, joining first's end to second's start is the only orientation whose
        /// onward direction matches both inputs' own authored direction; the other three each require an
        /// unsupported reversal at some point in the joined chain.
        /// </summary>
        [TestMethod]
        public void WhenExactlyOneOrientationConnectsThenCombineSucceedsWithThatOrientation()
        {
            TrackWorld world = CreateSingleVectorNodeWorld();
            PathModel first = CreateCombinePath("first", 10, 40);
            PathModel second = CreateCombinePath("second", 60, 90);

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(PathEditorMessageKind.CombineCombined, result.MessageKind);
            Assert.AreEqual(first.Id, result.PathModel.Id);
            Assert.AreEqual(second.End, result.PathModel.End);
            Assert.AreEqual(PathValidationState.NotValidated, result.PathModel.ValidationState);
            Assert.HasCount(4, result.PathModel.PathNodes);
            Assert.AreSequenceEqual(expectedArrayf10406090, result.PathModel.PathNodes.Select(node => node.Location.Location.X).ToArray());
            Assert.AreEqual(PathNodeType.Start, result.PathModel.PathNodes[0].NodeType);
            Assert.AreEqual(PathNodeType.Via, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(PathNodeType.Via, result.PathModel.PathNodes[2].NodeType);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[3].NodeType);
            Assert.AreSequenceEqual(new[] { 1, 2, 3, -1 }, result.PathModel.PathNodes.Select(node => node.NextMainNode).ToArray());
            Assert.HasCount(4, result.ChangedNodeIndexes);
            // Inputs are never mutated by a successful combine.
            Assert.HasCount(2, first.PathNodes);
            Assert.HasCount(2, second.PathNodes);
        }

        [TestMethod]
        public void WhenNoOrientationConnectsThenCombineIsRefused()
        {
            TrackWorld world = CreateTwoDisconnectedVectorNodesWorld();
            PathModel first = CreateCombinePath("first", 10, 40);
            PathModel second = CreateCombinePath("second", 60, 90) with
            {
                PathNodes = ImmutableArray.Create(
                    Anchor(60, 2) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(90, 2) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PathEditorMessageKind.CombineUnconnectable, result.MessageKind);
            Assert.AreSame(first, result.PathModel);
        }

        /// <summary>
        /// A two-vector loop between the same two junctions lets both the "forward" and the fully-reversed
        /// orientation connect without any internal reversal, so neither is uniquely preferred.
        /// </summary>
        [TestMethod]
        public void WhenMultipleOrientationsConnectThenCombineIsRefused()
        {
            TrackWorld world = CreateLoopWorld();
            PathModel first = CreateCombinePath("first", 20, 80) with
            {
                PathNodes = ImmutableArray.Create(
                    Anchor(20, 1) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(80, 1) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };
            PathModel second = CreateCombinePath("second", 80, 20) with
            {
                PathNodes = ImmutableArray.Create(
                    Anchor(80, 4) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(20, 4) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };

            PathEditResult result = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PathEditorMessageKind.CombineOrientationAmbiguous, result.MessageKind);
            Assert.AreSame(first, result.PathModel);
        }

        private static PathModel CreateCombinePath(string id, float start, float end)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                Start = "Start " + id,
                End = "End " + id,
                PathNodes = ImmutableArray.Create(Anchor(start, 1) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 },
                    Anchor(end, 1) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }),
            };
        }

        private static PathNode Anchor(float x, int nodeIndex)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeIndex = nodeIndex,
            };
        }

        private static TrackWorld CreateSingleVectorNodeWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, CreateVector(1, 0, 100)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            InitializeTrackDatabase(trackDatabase);
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = trackDatabase }, CreateTrackSectionModel());
        }

        /// <summary>Equal-cost connecting alternatives with unique approach and departure vectors.</summary>
        internal static TrackWorld CreateTiedConnectingWorld()
        {
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    CreateVector(1, -100, 0), CreateVector(2, 100, 200),
                    new JunctionNode(AtX(0), new Tile(0, 0), Vector3.Zero) { NodeIndex = 3 },
                    CreateVector(4, 0, 100), CreateVector(5, 0, 100),
                    new JunctionNode(AtX(100), new Tile(0, 0), Vector3.Zero) { NodeIndex = 6 },
                    new EndNode(AtX(-100), new Tile(0, 0), Vector3.Zero) { NodeIndex = 7 },
                    new EndNode(AtX(200), new Tile(0, 0), Vector3.Zero) { NodeIndex = 8 }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    CreateConnectors(1, (7, ConnectorType.InPin, TrackDirection.Ahead), (3, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(2, (6, ConnectorType.InPin, TrackDirection.Reverse), (8, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(3, (1, ConnectorType.InPin, TrackDirection.Ahead), (4, ConnectorType.OutPin, TrackDirection.Reverse), (5, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(4, (3, ConnectorType.InPin, TrackDirection.Ahead), (6, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(5, (3, ConnectorType.InPin, TrackDirection.Ahead), (6, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(6, (2, ConnectorType.InPin, TrackDirection.Reverse), (4, ConnectorType.OutPin, TrackDirection.Ahead), (5, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(7, (1, ConnectorType.OutPin, TrackDirection.Reverse)),
                    CreateConnectors(8, (2, ConnectorType.InPin, TrackDirection.Ahead))),
            };
            InitializeTrackDatabase(database);
            return TrackWorld.Create(new TrackModel { TrackDatabase = database }, CreateTrackSectionModel());
        }

        private static TrackWorld CreateTwoDisconnectedVectorNodesWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, CreateVector(1, 0, 100), CreateVector(2, 0, 100)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            InitializeTrackDatabase(trackDatabase);
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = trackDatabase }, CreateTrackSectionModel());
        }

        // A loop of two parallel vectors (1 and 4) between the same two junctions (2 and 3): vector(1)'s end and
        // vector(4)'s end both sit at junction(3); vector(1)'s start and vector(4)'s start both sit at junction(2).
        // Either rotational direction around the loop is a valid, unobstructed route between the two vectors.
        private static TrackWorld CreateLoopWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    CreateVector(1, 0, 100),
                    new JunctionNode(AtX(0), new Tile(0, 0), Vector3.Zero) { NodeIndex = 2 },
                    new JunctionNode(AtX(100), new Tile(0, 0), Vector3.Zero) { NodeIndex = 3 },
                    CreateVector(4, 0, 100)),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex(),
                    CreateConnectors(1, (2, ConnectorType.InPin, TrackDirection.Ahead), (3, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(2, (1, ConnectorType.OutPin, TrackDirection.Reverse), (4, ConnectorType.InPin, TrackDirection.Reverse)),
                    CreateConnectors(3, (1, ConnectorType.InPin, TrackDirection.Ahead), (4, ConnectorType.OutPin, TrackDirection.Ahead)),
                    CreateConnectors(4, (2, ConnectorType.InPin, TrackDirection.Ahead), (3, ConnectorType.OutPin, TrackDirection.Ahead))),
            };
            InitializeTrackDatabase(trackDatabase);
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = trackDatabase }, CreateTrackSectionModel());
        }

        private static VectorNode CreateVector(int nodeIndex, float startX, float endX)
        {
            WorldLocation start = AtX(startX);
            WorldLocation end = AtX(endX);
            VectorSectionNode section = new VectorSectionNode(start, new Tile(0, 0), new Vector3(0, MathHelper.PiOver2, 0), end) { NodeIndex = 1 };
            return new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = nodeIndex,
                VectorSections = ImmutableArray.Create(section),
            };
        }

        private static WorldLocation AtX(float x)
        {
            return new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0));
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

        private static TrackSectionModel CreateTrackSectionModel()
        {
            return new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection
                {
                    SectionIndex = 1,
                    Gauge = 1.435f,
                    Length = 100,
                }),
            };
        }

        private static void InitializeTrackDatabase(TrackDatabase trackDatabase)
        {
            typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
        }
    }
}
