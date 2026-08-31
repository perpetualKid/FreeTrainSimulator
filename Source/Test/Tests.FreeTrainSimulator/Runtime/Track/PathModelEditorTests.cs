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
        public void WhenSetStartAnchorOnEmptyPathThenStartIsCreated()
        {
            PathModel path = new PathModel();
            PathNode anchor = Anchor(10, 42);

            PathEditResult result = PathModelEditor.SetStartAnchor(path, anchor, true);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Start | PathNodeType.Junction, result.PathModel.PathNodes[0].NodeType);
            Assert.AreEqual(42, result.PathModel.PathNodes[0].NodeIndex);
            Assert.AreEqual(-1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreSequenceEqual(expectedArray0, result.ChangedNodeIndexes.ToArray());
        }

        [TestMethod]
        public void WhenSetStartAnchorReplacesStartThenLinksArePreserved()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start | PathNodeType.Invalid | PathNodeType.Intermediate, 2, 1));

            PathEditResult result = PathModelEditor.SetStartAnchor(path, Anchor(20, 84), false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Start, result.PathModel.PathNodes[0].NodeType);
            Assert.AreEqual(2, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextSidingNode);
            Assert.AreEqual(84, result.PathModel.PathNodes[0].NodeIndex);
        }

        [TestMethod]
        public void WhenSetStartAnchorReplacesAnnotatedStartThenIntentIsPreserved()
        {
            PathNodeWaitInfo waitInfo = new PathNodeWaitInfo { WaitTime = 30 };
            PathNode start = Node(PathNodeType.Start | PathNodeType.Wait | PathNodeType.Reversal, -1) with { WaitInfo = waitInfo };
            PathModel path = CreatePath(start);

            PathEditResult result = PathModelEditor.SetStartAnchor(path, Anchor(20, 84), false);

            Assert.AreEqual(PathNodeType.Start | PathNodeType.Wait | PathNodeType.Reversal, result.PathModel.PathNodes[0].NodeType);
            Assert.AreSame(waitInfo, result.PathModel.PathNodes[0].WaitInfo);
        }

        [TestMethod]
        public void WhenSetStartAnchorPrependsExistingPathThenAllLinksAreReindexed()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Intermediate, 1, 2),
                Node(PathNodeType.Intermediate, -1),
                Node(PathNodeType.Intermediate, 1));

            PathEditResult result = PathModelEditor.SetStartAnchor(path, Anchor(10, 42), false);

            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(3, result.PathModel.PathNodes[1].NextSidingNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[3].NextMainNode);
            Assert.AreSequenceEqual(expectedArray0123, result.ChangedNodeIndexes.ToArray());
        }

        [TestMethod]
        public void WhenSetStartAnchorPrependsExistingPathThenInputModelIsUnchanged()
        {
            PathNode originalNode = Node(PathNodeType.Intermediate, -1);
            PathModel path = CreatePath(originalNode);

            PathEditResult result = PathModelEditor.SetStartAnchor(path, Anchor(10, 42), false);

            Assert.AreNotSame(path, result.PathModel);
            Assert.AreSame(originalNode, path.PathNodes[0]);
            Assert.AreEqual(-1, path.PathNodes[0].NextMainNode);
        }

        [TestMethod]
        public void WhenSetStartAnchorReceivesNullModelThenItThrows()
        {
            Assert.ThrowsExactly<System.ArgumentNullException>(() => PathModelEditor.SetStartAnchor(null, Anchor(10, 42), false));
        }

        [TestMethod]
        public void WhenSetStartAnchorReceivesNullAnchorThenItThrows()
        {
            Assert.ThrowsExactly<System.ArgumentNullException>(() => PathModelEditor.SetStartAnchor(new PathModel(), null, false));
        }

        private static readonly int[] expectedArray01 = new[] { 0, 1 };
        private static readonly int[] expectedArray24 = new[] { 2, 4 };
        private static readonly int[] expectedArray1 = new[] { 1 };
        private static readonly int[] expectedArray0 = new[] { 0 };
        private static readonly int[] expectedArray0123 = new[] { 0, 1, 2, 3 };

        [TestMethod]
        public void WhenSetEndAnchorAfterStartThenEndIsAppended()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, -1));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), true);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(PathNodeType.End | PathNodeType.Junction, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(126, result.PathModel.PathNodes[1].NodeIndex);
            Assert.AreSequenceEqual(expectedArray01, result.ChangedNodeIndexes.ToArray());
        }

        [TestMethod]
        public void WhenSetEndAnchorAppendsToMainTailThenInteriorNodesArePreserved()
        {
            PathNode interior = Node(PathNodeType.Intermediate, 2, 3);
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                interior,
                Node(PathNodeType.Intermediate, -1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.AreSame(interior, result.PathModel.PathNodes[1]);
            Assert.AreEqual(4, result.PathModel.PathNodes[2].NextMainNode);
            Assert.AreEqual(3, result.PathModel.PathNodes[1].NextSidingNode);
            Assert.AreSequenceEqual(expectedArray24, result.ChangedNodeIndexes.ToArray());
        }

        [TestMethod]
        public void WhenSetEndAnchorReplacesEndThenItIsTerminalOnMain()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End | PathNodeType.Invalid | PathNodeType.Intermediate | PathNodeType.Junction, 7));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(-1, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreSequenceEqual(expectedArray1, result.ChangedNodeIndexes.ToArray());
        }

        [TestMethod]
        public void WhenSetEndAnchorReplacesAnnotatedEndThenIntentIsPreserved()
        {
            PathNodeWaitInfo waitInfo = new PathNodeWaitInfo { WaitTime = 45 };
            PathNode end = Node(PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal, -1) with { WaitInfo = waitInfo };
            PathModel path = CreatePath(Node(PathNodeType.Start, 1), end);

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.AreEqual(PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal, result.PathModel.PathNodes[1].NodeType);
            Assert.AreSame(waitInfo, result.PathModel.PathNodes[1].WaitInfo);
        }

        [TestMethod]
        public void WhenSetEndAnchorHasNoStartThenEditFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
            Assert.Contains("start anchor", result.Message);
        }

        [TestMethod]
        public void WhenSetEndAnchorMainLinkIsOutOfRangeThenEditFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, 4));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
            Assert.Contains("out-of-range main link 4", result.Message);
        }

        [TestMethod]
        public void WhenSetEndAnchorMainPathContainsCycleThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 0));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
            Assert.Contains("cycle at node 0", result.Message);
        }

        [TestMethod]
        public void WhenSetEndAnchorAppendsAfterTailWithSidingThenBranchIsPreserved()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1, 2),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextSidingNode);
        }

        [TestMethod]
        public void WhenSetEndAnchorReplacesEndWithSidingThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1, 2),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.SetEndAnchor(path, Anchor(30, 126), false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
            Assert.Contains("siding branch to node 2", result.Message);
        }

        [TestMethod]
        public void WhenSetEndAnchorReceivesNullModelThenItThrows()
        {
            Assert.ThrowsExactly<System.ArgumentNullException>(() => PathModelEditor.SetEndAnchor(null, Anchor(30, 126), false));
        }

        [TestMethod]
        public void WhenSetEndAnchorReceivesNullAnchorThenItThrows()
        {
            Assert.ThrowsExactly<System.ArgumentNullException>(() => PathModelEditor.SetEndAnchor(new PathModel(), null, false));
        }

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
        public void WhenPassingBranchIsCreatedThenItLinksToALaterMainRouteNode()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.CreatePassingBranch(path, 0, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.PathModel.PathNodes[0].NextSidingNode);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
        }

        [TestMethod]
        public void WhenPassingBranchRejoinsBeforeItsStartThenEditFailsWithoutChangingModel()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.CreatePassingBranch(path, 1, 0);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenSidingLinkIsBelowMinusOneThenBranchOperationIsRejected()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1, -2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.CreatePassingBranch(path, 0, 1);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenSupportedBranchRolesAreInspectedThenMainAndBranchNodesAreDistinguished()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1, 3),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1),
                Node(PathNodeType.Intermediate, -1, 2));

            Assert.IsTrue(PathModelEditor.TryGetPassingBranchNodeRole(path, 0, out PassingBranchNodeRole startRole, out _));
            Assert.AreEqual(PassingBranchNodeRole.BranchStart, startRole);
            Assert.IsTrue(PathModelEditor.TryGetPassingBranchNodeRole(path, 1, out PassingBranchNodeRole mainRole, out _));
            Assert.AreEqual(PassingBranchNodeRole.MainRoute, mainRole);
            Assert.IsTrue(PathModelEditor.TryGetPassingBranchNodeRole(path, 2, out PassingBranchNodeRole rejoinRole, out _));
            Assert.AreEqual(PassingBranchNodeRole.BranchRejoin, rejoinRole);
            Assert.IsTrue(PathModelEditor.TryGetPassingBranchNodeRole(path, 3, out PassingBranchNodeRole interiorRole, out _));
            Assert.AreEqual(PassingBranchNodeRole.BranchInterior, interiorRole);
        }

        [TestMethod]
        public void WhenDisconnectedNodeExistsThenPassingBranchCreationIsRejectedWithoutChangingModel()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.CreatePassingBranch(path, 0, 1);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenPassingBranchDoesNotRejoinThenRemovalIsRejectedWithoutChangingModel()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1, 2),
                Node(PathNodeType.End, -1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.RemovePassingBranch(path, 0);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenNestedSidingLinkExistsThenBranchMoveIsRejectedWithoutChangingModel()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1, 2),
                Node(PathNodeType.End, -1),
                Node(PathNodeType.Intermediate, 1, 1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(25, 0, 0))) { NodeIndex = 1 };

            PathEditResult result = PathModelEditor.MovePassingBranchAnchor(path, 2, anchor, false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenPassingBranchAlreadyExistsThenOverlappingBranchIsRejectedWithoutChangingModel()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1, 2),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.CreatePassingBranch(path, 1, 2);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
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
            Assert.IsTrue(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Intermediate));
            Assert.IsFalse(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Junction));
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
            Assert.IsTrue(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Junction));
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
            Assert.IsTrue(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Wait));
            Assert.IsTrue(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Reversal));
            Assert.IsTrue(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Junction));
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

        [TestMethod]
        public void WhenWaitPointIsSetOnIntermediateNodeThenNodeIsMarkedAndWaitTimeStored()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.SetWaitPoint(path, 1, 90);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Intermediate | PathNodeType.Wait, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(90, result.PathModel.PathNodes[1].WaitInfo.WaitTime);
        }

        [TestMethod]
        public void WhenWaitPointIsSetWithNonPositiveWaitTimeThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.SetWaitPoint(path, 1, 0);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenWaitPointIsSetOnStartNodeThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.SetWaitPoint(path, 0, 60);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenWaitPointIsSetOnJunctionNodeThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Junction, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.SetWaitPoint(path, 1, 60);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenWaitPointIsClearedThenMarkerAndWaitInfoAreRemoved()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));
            PathModel withWait = PathModelEditor.SetWaitPoint(path, 1, 30).PathModel;

            PathEditResult result = PathModelEditor.ClearWaitPoint(withWait, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Intermediate, result.PathModel.PathNodes[1].NodeType);
            Assert.IsNull(result.PathModel.PathNodes[1].WaitInfo);
        }

        [TestMethod]
        public void WhenWaitPointIsClearedOnNodeWithoutWaitThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.ClearWaitPoint(path, 1);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenReversalPointIsSetOnIntermediateNodeThenNodeIsMarked()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.SetReversalPoint(path, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Intermediate | PathNodeType.Reversal, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenReversalPointIsSetOnEndNodeThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.SetReversalPoint(path, 1);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenReversalPointIsClearedThenMarkerIsRemoved()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));
            PathModel withReversal = PathModelEditor.SetReversalPoint(path, 1).PathModel;

            PathEditResult result = PathModelEditor.ClearReversalPoint(withReversal, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Intermediate, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenReversalPointIsClearedOnNodeWithoutReversalThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, -1));

            PathEditResult result = PathModelEditor.ClearReversalPoint(path, 1);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenWaitPointIsSetWithOutOfRangeIndexThenEditFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, -1));

            PathEditResult result = PathModelEditor.SetWaitPoint(path, 5, 60);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenViaPointIsInsertedThenItIsLinkedIntoTheMainChain()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(50, 0, 0))) { NodeIndex = 4 };

            PathEditResult result = PathModelEditor.InsertViaPoint(path, 0, anchor, false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.PathModel.PathNodes.Length);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(PathNodeType.Intermediate, result.PathModel.PathNodes[1].NodeType);
            Assert.AreEqual(4, result.PathModel.PathNodes[1].NodeIndex);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[2].NodeType);
        }

        [TestMethod]
        public void WhenViaPointIsInsertedThenFollowingLinksAreShifted()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero));

            PathEditResult result = PathModelEditor.InsertViaPoint(path, 0, anchor, false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.PathModel.PathNodes[2].NextMainNode);
            Assert.AreEqual(-1, result.PathModel.PathNodes[3].NextMainNode);
        }

        [TestMethod]
        public void WhenViaPointIsInsertedAsJunctionThenNodeIsMarkedAsJunction()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero));

            PathEditResult result = PathModelEditor.InsertViaPoint(path, 0, anchor, true);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PathNodeType.Junction, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenViaPointIsInsertedAfterEndNodeThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero));

            PathEditResult result = PathModelEditor.InsertViaPoint(path, 1, anchor, false);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenViaPointIsInsertedWithOutOfRangeIndexThenEditFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, -1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero));

            PathEditResult result = PathModelEditor.InsertViaPoint(path, 3, anchor, false);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenViaPointIsRemovedThenPathIsRelinkedAroundIt()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveViaPoint(path, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.PathModel.PathNodes.Length);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[1].NodeType);
        }

        [TestMethod]
        public void WhenViaPointIsRemovedOnStartNodeThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));

            PathEditResult result = PathModelEditor.RemoveViaPoint(path, 0);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenViaPointIsRemovedWithOutOfRangeIndexThenEditFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, -1));

            PathEditResult result = PathModelEditor.RemoveViaPoint(path, 2);

            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void WhenViaPointIsInsertedAndRemovedThenOriginalLinksAreRestored()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.Intermediate, 2),
                Node(PathNodeType.End, -1));
            PathNode anchor = new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero));

            PathModel withVia = PathModelEditor.InsertViaPoint(path, 1, anchor, false).PathModel;
            PathEditResult result = PathModelEditor.RemoveViaPoint(withVia, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.PathModel.PathNodes.Length);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(-1, result.PathModel.PathNodes[2].NextMainNode);
        }

        [TestMethod]
        public void WhenRouteCandidateIsAppliedThenAnchorsBecomeAuthoredViaPoints()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            ResolvedRouteCandidate candidate = CreateCandidate(7, 8);

            PathEditResult result = PathModelEditor.ApplyRouteCandidate(path, 0, candidate);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(4, result.PathModel.PathNodes.Length);
            Assert.AreEqual(7, result.PathModel.PathNodes[1].NodeIndex);
            Assert.AreEqual(8, result.PathModel.PathNodes[2].NodeIndex);
            Assert.AreEqual(PathNodeType.End, result.PathModel.PathNodes[3].NodeType);
        }

        [TestMethod]
        public void WhenRouteCandidateIsAppliedThenMainChainStaysLinked()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            ResolvedRouteCandidate candidate = CreateCandidate(7, 8);

            PathEditResult result = PathModelEditor.ApplyRouteCandidate(path, 0, candidate);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.PathModel.PathNodes[0].NextMainNode);
            Assert.AreEqual(2, result.PathModel.PathNodes[1].NextMainNode);
            Assert.AreEqual(3, result.PathModel.PathNodes[2].NextMainNode);
            Assert.AreEqual(-1, result.PathModel.PathNodes[3].NextMainNode);
        }

        [TestMethod]
        public void WhenRouteCandidateHasNoIntermediaryAnchorsThenEditFails()
        {
            PathModel path = CreatePath(
                Node(PathNodeType.Start, 1),
                Node(PathNodeType.End, -1));
            ResolvedRouteCandidate candidate = new ResolvedRouteCandidate(ImmutableArray.Create(1, 2), ImmutableArray.Create(1, 2),
                ImmutableArray<PathRouteAnchor>.Empty, 10.0);

            PathEditResult result = PathModelEditor.ApplyRouteCandidate(path, 0, candidate);

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        [TestMethod]
        public void WhenRouteCandidateIsAppliedWithOutOfRangeIndexThenEditFails()
        {
            PathModel path = CreatePath(Node(PathNodeType.Start, -1));

            PathEditResult result = PathModelEditor.ApplyRouteCandidate(path, 4, CreateCandidate(7));

            Assert.IsFalse(result.Success);
            Assert.AreSame(path, result.PathModel);
        }

        private static ResolvedRouteCandidate CreateCandidate(params int[] intermediaryTrackNodeIndexes)
        {
            ImmutableArray<PathRouteAnchor> anchors = intermediaryTrackNodeIndexes
                .Select(trackNodeIndex => new PathRouteAnchor(-1, new WorldLocation(new Tile(0, 0), new Vector3(trackNodeIndex, 0, 0)),
                    PathNodeType.Intermediate, trackNodeIndex, -1))
                .ToImmutableArray();

            return new ResolvedRouteCandidate(ImmutableArray<int>.Empty, ImmutableArray<int>.Empty, anchors, 10.0);
        }

        private static PathNode Anchor(float x, int nodeIndex)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeIndex = nodeIndex,
            };
        }

        private static PathNode Node(PathNodeType nodeType, int nextMainNode)
        {
            return Node(nodeType, nextMainNode, -1);
        }

        private static PathNode Node(PathNodeType nodeType, int nextMainNode, int nextSidingNode)
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
