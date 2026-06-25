using System.Collections.Generic;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
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
    }
}
