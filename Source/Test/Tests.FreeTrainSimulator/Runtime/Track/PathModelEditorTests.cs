using System.Collections.Generic;
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
    public class PathModelEditorTests
    {
        [TestMethod]
        public void WhenAddEndOnLinearPathThenLastNodeBecomesEnd()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.AddEnd(path);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenAddEndOnLinearPathThenTrailingLinkIsCleared()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 5));

            PathEditResult result = PathModelEditor.AddEnd(path);

            Assert.AreEqual(-1, result.PathModel.PathNodes[1].NextMainNode);
        }

        [TestMethod]
        public void WhenAddEndButPathAlreadyEndsThenResultFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.AddEnd(path);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenAddEndButNoStartExistsThenResultFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.AddEnd(path);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenAddEndOnEmptyPathThenResultFails()
        {
            PathModel path = new PathModel();

            PathEditResult result = PathModelEditor.AddEnd(path);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenRemoveEndThenEndNodeBecomesIntermediate()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveEnd(path);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Intermediate, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenRemoveEndOnJunctionEndThenJunctionFlagSurvives()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End | PathNodeType.Junction, -1));

            PathEditResult result = PathModelEditor.RemoveEnd(path);

            Assert.AreEqual(PathNodeType.Junction, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenRemoveEndButNoEndExistsThenResultFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.RemoveEnd(path);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenAddStartThenFirstNodeBecomesStart()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Intermediate, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.AddStart(path);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Start, result.PathModel.PathNodes[0].NodeType);
        }

        [TestMethod]
        public void WhenAddStartButStartAlreadyExistsThenResultFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.AddStart(path);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenRemoveStartThenStartNodeIsDropped()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveStart(path);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.PathModel.PathNodes.Length);
        }

        [TestMethod]
        public void WhenRemoveStartThenForwardLinksAreReindexed()
        {
            // 0:Start->1, 1:Intermediate->2, 2:End. After removing node 0, the survivors shift down by one
            // and their links must decrement: old node 1 (now 0) linked to 2 -> now links to 1.
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveStart(path);

            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
        }

        [TestMethod]
        public void WhenRemoveStartThenLinkToRemovedNodeIsBroken()
        {
            // Node 1 has a passing link back to node 0 (the start). Removing node 0 must break that link to -1.
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 2),
                Node(PathNodeType.Intermediate, 2, 0),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveStart(path);

            Assert.AreEqual(-1, result.PathModel.PathNodes[0].NextSidingNode);
        }

        [TestMethod]
        public void WhenRemoveStartButNoStartExistsThenResultFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Intermediate, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveStart(path);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenRemoveRestOfPathThenLaterNodesAreRemoved()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.Intermediate, 3),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveRestOfPath(path, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.PathModel.PathNodes.Length);
        }

        [TestMethod]
        public void WhenRemoveRestOfPathThenTruncationNodeBecomesEnd()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveRestOfPath(path, 1);

            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(-1, result.PathModel.PathNodes[1].NextMainNode);
        }

        [TestMethod]
        public void WhenRemoveRestOfPathThenLinkIntoRemovedTailIsBroken()
        {
            // Node 0 links to node 2 (in the removed tail) via a passing link; truncating after node 1 must
            // break that link to -1.
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1, 2),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveRestOfPath(path, 1);

            Assert.AreEqual(-1, result.PathModel.PathNodes[0].NextSidingNode);
        }

        [TestMethod]
        public void WhenRemoveRestOfPathWithOutOfRangeIndexThenResultFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveRestOfPath(path, 5);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenRemoveRestOfPathAtLastNodeThenResultFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveRestOfPath(path, 1);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenOperationFailsThenOriginalModelIsReturnedUnchanged()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.AddEnd(path);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenRepairJunctionNodeOnSingleTrackSectionThenNodeBecomesTrackPoint()
        {
            TrackWorld trackWorld = CreateInitializedTrackWorldWithSingleVectorNode();
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Junction, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RepairNode(path, 1, trackWorld);

            Assert.IsTrue(result.Success);
            Assert.IsTrue((result.PathModel.PathNodes[1].NodeType & PathNodeType.Intermediate) == PathNodeType.Intermediate);
            Assert.IsFalse((result.PathModel.PathNodes[1].NodeType & PathNodeType.Junction) == PathNodeType.Junction);
            Assert.AreEqual(1, result.PathModel.PathNodes[1].NodeIndex);
        }

        [TestMethod]
        public void WhenRepairNodeHasSingleNearbyJunctionThenNodeSnapsToJunction()
        {
            TrackWorld trackWorld = CreateTrackWorldWithJunctions(
                new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(3, 0, 0)), new Tile(0, 0), Vector3.Zero) { NodeIndex = 2 });
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RepairNode(path, 1, trackWorld);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NodeIndex);
            Assert.AreEqual(new WorldLocation(new Tile(0, 0), new Vector3(3, 0, 0)), result.PathModel.PathNodes[1].Location);
            Assert.IsTrue((result.PathModel.PathNodes[1].NodeType & PathNodeType.Junction) == PathNodeType.Junction);
        }

        [TestMethod]
        public void WhenRepairNodeHasMultipleNearbyJunctionsThenResultFails()
        {
            TrackWorld trackWorld = CreateTrackWorldWithJunctions(
                new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(3, 0, 0)), new Tile(0, 0), Vector3.Zero) { NodeIndex = 2 },
                new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(-3, 0, 0)), new Tile(0, 0), Vector3.Zero) { NodeIndex = 3 });
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RepairNode(path, 1, trackWorld);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenRepairNodeWithoutTrackWorldThenResultFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Junction, -1));

            PathEditResult result = PathModelEditor.RepairNode(path, 0, null);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenMoveNodeThenLocationAndAnchorAreUpdated()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            PathNode replacement = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(10, 0, 20)))
            {
                NodeIndex = 42,
            };

            PathEditResult result = PathModelEditor.MoveNode(path, 1, replacement, false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(42, result.PathModel.PathNodes[1].NodeIndex);
            Assert.AreEqual(replacement.Location, result.PathModel.PathNodes[1].Location);
        }

        [TestMethod]
        public void WhenMoveNodeThenLinksAndWaitInfoArePreserved()
        {
            PathNodeWaitInfo waitInfo = new PathNodeWaitInfo { WaitTime = 25 };
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Wait | PathNodeType.Reversal, 2, -1) with { WaitInfo = waitInfo },
                Node(PathNodeType.End, -1));
            PathNode replacement = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(10, 0, 20)))
            {
                NodeIndex = 42,
            };

            PathEditResult result = PathModelEditor.MoveNode(path, 1, replacement, true);

            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(waitInfo, result.PathModel.PathNodes[1].WaitInfo);
            Assert.IsTrue((result.PathModel.PathNodes[1].NodeType & PathNodeType.Wait) == PathNodeType.Wait);
            Assert.IsTrue((result.PathModel.PathNodes[1].NodeType & PathNodeType.Reversal) == PathNodeType.Reversal);
            Assert.IsTrue((result.PathModel.PathNodes[1].NodeType & PathNodeType.Junction) == PathNodeType.Junction);
        }

        [TestMethod]
        public void WhenMoveNodeWithOutOfRangeIndexThenResultFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, -1));
            PathNode replacement = Node(PathNodeType.Intermediate, -1);

            PathEditResult result = PathModelEditor.MoveNode(path, 4, replacement, false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenSuccessfulMutationIsUndoneViaSnapshotThenPriorModelIsRecovered()
        {
            // Mirrors PathEditor.ApplyMutation + Undo: push the current model, apply the mutation, then
            // restore the pushed snapshot. The mutation must not alter the original (immutable) model.
            PathModel original = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));
            Stack<PathModel> undoHistory = new Stack<PathModel>();

            undoHistory.Push(original);
            PathEditResult result = PathModelEditor.AddEnd(original);
            PathModel afterMutation = result.PathModel;
            PathModel afterUndo = undoHistory.Pop();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.End, afterMutation.PathNodes[1].NodeType);
            Assert.AreEqual(PathNodeType.Intermediate, afterUndo.PathNodes[1].NodeType);
            Assert.AreSame(original, afterUndo);
        }

        private static PathNode Node(PathNodeType nodeType, int nextMainNode, int nextSidingNode = -1)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
            };
        }

        private static PathModel CreatePath(params PathNode[] nodes)
        {
            return new PathModel()
            {
                Id = "test-path",
                Name = "Test Path",
                PathNodes = ImmutableArray.Create(nodes),
            };
        }

        private static TrackWorld CreateInitializedTrackWorldWithSingleVectorNode()
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), Vector3.Zero);
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 0));
            VectorSectionNode section = new VectorSectionNode(start, new Tile(0, 0), new Vector3(0, MathHelper.PiOver2, 0), end)
            {
                NodeIndex = 1,
            };
            VectorNode vectorNode = new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(section),
            };
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vectorNode),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);

            TrackModel trackModel = new TrackModel()
            {
                TrackDatabase = trackDatabase,
            };
            TrackSectionModel trackSectionModel = new TrackSectionModel()
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

        private static TrackWorld CreateTrackWorldWithJunctions(params JunctionNode[] junctionNodes)
        {
            TrackNodeBase[] nodes = new TrackNodeBase[junctionNodes.Max(junction => junction.NodeIndex) + 1];
            foreach (JunctionNode junctionNode in junctionNodes)
                nodes[junctionNode.NodeIndex] = junctionNode;

            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create(nodes),
                TrackNodeConnectors = ImmutableArray.CreateRange(Enumerable.Range(0, nodes.Length).Select(index => new TrackNodeConnectorIndex { NodeIndex = index })),
            };
            typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);

            TrackModel trackModel = new TrackModel()
            {
                TrackDatabase = trackDatabase,
            };
            return TrackWorld.Initialize(null, trackModel, new TrackSectionModel());
        }
    }
}
