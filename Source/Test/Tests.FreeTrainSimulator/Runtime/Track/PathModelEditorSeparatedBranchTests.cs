using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class PathModelEditorSeparatedBranchTests
    {
        [TestMethod]
        [DataRow(0, false)]
        [DataRow(0, true)]
        [DataRow(1, false)]
        [DataRow(1, true)]
        [DataRow(2, false)]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(3, true)]
        public void WhenStructuralCapabilitiesAreCollectedThenTheyMatchAuthoringAndRemovalForEveryPermutedNode(int branchCount, bool includeEnd)
        {
            PathModel source = CreateBranches(branchCount);
            source = Permute(source with { PathNodes = source.PathNodes.SetItem(9, source.PathNodes[9] with { NodeType = includeEnd ? PathNodeType.End : PathNodeType.Via }) });
            int[] indexes = Enumerable.Range(-1, source.PathNodes.Length + 2).ToArray();
            (bool CanBegin, bool CanRemove)[] expected = indexes.Select(index => (
                Enumerable.Range(0, source.PathNodes.Length).Any(rejoin => PathModelEditor.CreatePassingBranch(source, index, rejoin).Success),
                PathModelEditor.RemovePassingBranch(source, index).Success)).ToArray();

            ImmutableDictionary<int, (bool CanBegin, bool CanRemove)> capabilities = PathModelEditor.GetPassingBranchCapabilities(source);

            Assert.AreSequenceEqual(expected, indexes.Select(index => capabilities.GetValueOrDefault(index)));
        }

        [TestMethod]
        [DataRow("null")]
        [DataRow("badMain")]
        [DataRow("badSiding")]
        [DataRow("negativeMain")]
        [DataRow("negativeSiding")]
        [DataRow("mainCycle")]
        [DataRow("sidingCycle")]
        [DataRow("disconnected")]
        [DataRow("interiorMain")]
        [DataRow("shared")]
        [DataRow("backward")]
        [DataRow("touch")]
        [DataRow("enclosed")]
        [DataRow("crossing")]
        [DataRow("duplicateStart")]
        public void WhenTopologyIsMalformedThenStructuralCapabilitiesAreEmpty(string defect)
        {
            PathModel source = Permute(Corrupt(CreateBranches(2), defect));

            ImmutableDictionary<int, (bool CanBegin, bool CanRemove)> capabilities = PathModelEditor.GetPassingBranchCapabilities(source);

            Assert.IsTrue(capabilities.IsEmpty);
        }

        [TestMethod]
        public void WhenCapabilityModelIsNullThenArgumentIsRejected()
        {
            ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => PathModelEditor.GetPassingBranchCapabilities(null));

            Assert.AreEqual("pathModel", exception.ParamName);
        }

        [TestMethod]
        public void WhenCapabilityModelHasNoNodesThenCapabilitiesAreEmpty()
        {
            ImmutableDictionary<int, (bool CanBegin, bool CanRemove)> capabilities = PathModelEditor.GetPassingBranchCapabilities(new PathModel());

            Assert.IsTrue(capabilities.IsEmpty);
        }

        [TestMethod]
        [DataRow(1, 2, 4, 5)]
        [DataRow(4, 5, 1, 2)]
        public void WhenSecondBranchIsSeparatedThenLinkedOrderAllowsEitherAuthoringOrder(int firstStart, int firstRejoin, int secondStart, int secondRejoin)
        {
            PathModel source = Permute(CreateMain());
            PathEditResult first = PathModelEditor.CreatePassingBranch(source, Index(source, firstStart), Index(source, firstRejoin));

            PathEditResult second = PathModelEditor.CreatePassingBranch(first.PathModel, Index(source, secondStart), Index(source, secondRejoin));

            Assert.AreEqual((true, true), (first.Success, second.Success));
        }

        [TestMethod]
        [DataRow(2, 4)]
        [DataRow(0, 1)]
        [DataRow(0, 3)]
        [DataRow(1, 3)]
        [DataRow(1, 2)]
        [DataRow(0, 2)]
        public void WhenSecondBranchTouchesOrOverlapsThenAuthoringReturnsOriginal(int start, int rejoin)
        {
            PathModel source = PathModelEditor.CreatePassingBranch(Permute(CreateMain()), 8, 7).PathModel;

            PathEditResult result = PathModelEditor.CreatePassingBranch(source, Index(source, start), Index(source, rejoin));

            Assert.AreEqual((false, true, 0), (result.Success, ReferenceEquals(source, result.PathModel), result.ChangedNodeIndexes.Length));
        }

        [TestMethod]
        public void WhenTwoBranchesExistThenThirdAuthoringReturnsOriginal()
        {
            PathModel source = Permute(CreateBranches(2));

            PathEditResult result = PathModelEditor.CreatePassingBranch(source, Index(source, 7), Index(source, 8));

            Assert.AreEqual((false, true), (result.Success, ReferenceEquals(source, result.PathModel)));
        }

        [TestMethod]
        [DataRow(1, PassingBranchNodeRole.BranchStart)]
        [DataRow(2, PassingBranchNodeRole.BranchRejoin)]
        [DataRow(4, PassingBranchNodeRole.BranchStart)]
        [DataRow(5, PassingBranchNodeRole.BranchRejoin)]
        [DataRow(10, PassingBranchNodeRole.BranchInterior)]
        [DataRow(11, PassingBranchNodeRole.BranchInterior)]
        [DataRow(3, PassingBranchNodeRole.MainRoute)]
        public void WhenStorageIsPermutedThenEveryOwnerRoleIsClassified(int identity, PassingBranchNodeRole expected)
        {
            PathModel source = Permute(CreateBranches(2));

            bool success = PathModelEditor.TryGetPassingBranchNodeRole(source, Index(source, identity), out PassingBranchNodeRole role, out _);

            Assert.AreEqual((true, expected), (success, role));
        }

        [TestMethod]
        public void WhenImportedModelHasThreeSeparatedBranchesThenClassificationHasNoAuthoringCeiling()
        {
            PathModel source = Permute(CreateBranches(3));

            bool success = PathModelEditor.TryGetPassingBranchNodeRole(source, Index(source, 12), out PassingBranchNodeRole role, out _);

            Assert.AreEqual((true, PassingBranchNodeRole.BranchInterior), (success, role));
        }

        [TestMethod]
        public void WhenMainHasNoEndThenSecondInteriorEventCanStillBeClearedAndClassified()
        {
            PathModel source = CreateBranches(2);
            source = source with { PathNodes = source.PathNodes.SetItem(9, source.PathNodes[9] with { NodeType = PathNodeType.Via })
                .SetItem(11, source.PathNodes[11] with { NodeType = PathNodeType.Wait | PathNodeType.Via, WaitInfo = new PathNodeWaitInfo { WaitTime = 30 } }) };

            PathEditResult result = PathModelEditor.ClearWaitPoint(source, 11);
            bool classified = PathModelEditor.TryGetPassingBranchNodeRole(result.PathModel, 11, out PassingBranchNodeRole role, out _);

            Assert.AreEqual((true, true, PassingBranchNodeRole.BranchInterior), (result.Success, classified, role));
        }

        [TestMethod]
        [DataRow("null")]
        [DataRow("badMain")]
        [DataRow("badSiding")]
        [DataRow("negativeMain")]
        [DataRow("negativeSiding")]
        [DataRow("mainCycle")]
        [DataRow("sidingCycle")]
        [DataRow("disconnected")]
        [DataRow("interiorMain")]
        [DataRow("shared")]
        [DataRow("backward")]
        [DataRow("touch")]
        [DataRow("enclosed")]
        [DataRow("crossing")]
        [DataRow("duplicateStart")]
        public void WhenAnyOwnerTopologyIsMalformedThenClassificationRefusesWholeGraph(string defect)
        {
            PathModel source = Corrupt(CreateBranches(2), defect);

            bool success = PathModelEditor.TryGetPassingBranchNodeRole(source, 1, out PassingBranchNodeRole role, out _);

            Assert.AreEqual((false, PassingBranchNodeRole.None), (success, role));
        }

        [TestMethod]
        [DataRow("null")]
        [DataRow("badMain")]
        [DataRow("badSiding")]
        [DataRow("negativeMain")]
        [DataRow("negativeSiding")]
        [DataRow("mainCycle")]
        [DataRow("sidingCycle")]
        [DataRow("disconnected")]
        [DataRow("interiorMain")]
        [DataRow("shared")]
        [DataRow("backward")]
        [DataRow("touch")]
        [DataRow("enclosed")]
        [DataRow("crossing")]
        [DataRow("duplicateStart")]
        public void WhenMalformedOwnerStorageIsPermutedThenClassificationStillRefusesWholeGraph(string defect)
        {
            PathModel source = Permute(Corrupt(CreateBranches(2), defect));

            bool success = PathModelEditor.TryGetPassingBranchNodeRole(source, source.PathNodes.Length - 2, out PassingBranchNodeRole role, out _);

            Assert.AreEqual((false, PassingBranchNodeRole.None), (success, role));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(4)]
        [DataRow(5)]
        public void WhenRemovingAnyBranchEndpointAsViaThenOriginalIsReturned(int endpoint)
        {
            PathModel source = Permute(CreateBranches(2));

            PathEditResult result = PathModelEditor.RemoveViaPoint(source, Index(source, endpoint));

            Assert.AreEqual((false, true), (result.Success, ReferenceEquals(source, result.PathModel)));
        }

        private static readonly int[] expectedArray4115 = new[] { 4, 11, 5 };
        private static readonly int[] expectedArray1102 = new[] { 1, 10, 2 };

        [TestMethod]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(10)]
        [DataRow(11)]
        public void WhenInsertingSidingViaThenOtherOwnerRetainsItsExactNodes(int identity)
        {
            PathModel source = Permute(CreateBranches(2));
            PathNode[] expected = Identities(source, identity < 4 || identity == 10 ? expectedArray4115 : expectedArray1102);

            PathEditResult result = PathModelEditor.InsertPassingBranchViaPoint(source, Index(source, identity), Node(99, PathNodeType.Via, -1, -1), false);

            Assert.AreSequenceEqual(expected, Identities(result.PathModel, expected.Select(node => node.NodeIndex).ToArray()));
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(11)]
        public void WhenMovingAnInteriorThenOnlyRequestedOwnerLocationChanges(int identity)
        {
            PathModel source = Permute(CreateBranches(2));
            int index = Index(source, identity);

            PathEditResult result = PathModelEditor.MovePassingBranchAnchor(source, index, Node(99, PathNodeType.Via, -1, -1), false);

            Assert.AreSequenceEqual(source.PathNodes.SetItem(index, new PathNode(Node(99, PathNodeType.Via, -1, -1).Location)
            {
                NodeType = source.PathNodes[index].NodeType, NodeIndex = 99, NextMainNode = source.PathNodes[index].NextMainNode,
                NextSidingNode = source.PathNodes[index].NextSidingNode,
            }), result.PathModel.PathNodes);
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(11)]
        [DataRow(3)]
        public void WhenRemovingViaThenBothSurvivingOwnersRemainClassifiable(int identity)
        {
            PathModel source = Permute(CreateBranches(2));

            PathEditResult result = PathModelEditor.RemoveViaPoint(source, Index(source, identity));
            bool first = PathModelEditor.TryGetPassingBranchNodeRole(result.PathModel, Index(result.PathModel, 1), out PassingBranchNodeRole firstRole, out _);
            bool second = PathModelEditor.TryGetPassingBranchNodeRole(result.PathModel, Index(result.PathModel, 4), out PassingBranchNodeRole secondRole, out _);

            Assert.AreEqual((true, true, true, PassingBranchNodeRole.BranchStart, PassingBranchNodeRole.BranchStart), (result.Success, first, second, firstRole, secondRole));
        }

        [TestMethod]
        [DataRow(1, 4)]
        [DataRow(4, 1)]
        public void WhenRemovingOneBranchThenChangedIndexSelectsRemappedStartAndOtherOwnerSurvives(int start, int remainingStart)
        {
            PathModel source = Permute(CreateBranches(2));

            PathEditResult result = PathModelEditor.RemovePassingBranch(source, Index(source, start));
            bool classified = PathModelEditor.TryGetPassingBranchNodeRole(result.PathModel, Index(result.PathModel, remainingStart), out PassingBranchNodeRole role, out _);

            Assert.AreEqual((true, true, PassingBranchNodeRole.BranchStart, Index(result.PathModel, start)), (result.Success, classified, role, result.ChangedNodeIndexes[0]));
        }

        [TestMethod]
        public void WhenMaterializingPopulatedOwnerThenExistingInteriorIsNotOrphaned()
        {
            PathModel source = CreateBranches(2);
            ImmutableArray<PathRouteAnchor> anchors = ImmutableArray.Create(new PathRouteAnchor(-1, Node(99, PathNodeType.Via, -1, -1).Location, PathNodeType.Via, 99, 0));

            PathEditResult result = PathModelEditor.AddPassingBranchAnchors(source, 4, anchors);

            Assert.AreEqual((false, true), (result.Success, ReferenceEquals(source, result.PathModel)));
        }

        [TestMethod]
        [DataRow(1, 0)]
        [DataRow(2, 1)]
        [DataRow(4, 1)]
        [DataRow(5, 2)]
        [DataRow(8, 3)]
        public void WhenTruncatingPermutedImportedGraphThenAllCompleteOwnersAreRetained(int cutoff, int branchCount)
        {
            PathModel source = Permute(CreateBranches(3));

            PathEditResult result = PathModelEditor.RemoveRestOfPath(source, Index(source, cutoff));

            Assert.AreEqual((true, branchCount, cutoff), (result.Success, result.PathModel.PathNodes.Count(node => node.NextMainNode >= 0 && node.NextSidingNode >= 0), result.PathModel.PathNodes[result.ChangedNodeIndexes[0]].NodeIndex));
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(11)]
        public void WhenSettingWaitOnEitherInteriorThenOriginalIsReturned(int identity)
        {
            PathModel source = Permute(CreateBranches(2));

            PathEditResult result = PathModelEditor.SetWaitPoint(source, Index(source, identity), 30);

            Assert.AreEqual((false, true), (result.Success, ReferenceEquals(source, result.PathModel)));
        }

        private static PathNode[] Identities(PathModel path, int[] identities)
        {
            return identities.Select(identity => path.PathNodes[Index(path, identity)] with { NextMainNode = -1, NextSidingNode = -1 }).ToArray();
        }

        private static int Index(PathModel path, int identity)
        {
            return Enumerable.Range(0, path.PathNodes.Length).Single(index => path.PathNodes[index].NodeIndex == identity);
        }

        private static PathModel CreateMain()
        {
            return new PathModel { PathNodes = Enumerable.Range(0, 10).Select(index => Node(index,
                index == 0 ? PathNodeType.Start : index == 9 ? PathNodeType.End : PathNodeType.Via,
                index == 9 ? -1 : index + 1, -1)).ToImmutableArray() };
        }

        private static PathModel CreateBranches(int count)
        {
            ImmutableArray<PathNode>.Builder nodes = CreateMain().PathNodes.ToBuilder();
            for (int branch = 0; branch < count; branch++)
            {
                int start = 1 + branch * 3;
                nodes[start] = nodes[start] with { NextSidingNode = nodes.Count };
                nodes.Add(Node(10 + branch, PathNodeType.Via, -1, start + 1));
            }
            return new PathModel { PathNodes = nodes.ToImmutable() };
        }

        private static PathNode Node(int identity, PathNodeType type, int main, int siding)
        {
            return new PathNode(new WorldLocation(new Tile(1, 1), new Vector3(identity * 10, 0, 0))) { NodeIndex = identity, NodeType = type, NextMainNode = main, NextSidingNode = siding };
        }

        private static PathModel Permute(PathModel source)
        {
            int length = source.PathNodes.Length;
            return source with { PathNodes = source.PathNodes.Reverse().Select(node => node == null ? null : node with
            {
                NextMainNode = node.NextMainNode < 0 || node.NextMainNode >= length ? node.NextMainNode : length - 1 - node.NextMainNode,
                NextSidingNode = node.NextSidingNode < 0 || node.NextSidingNode >= length ? node.NextSidingNode : length - 1 - node.NextSidingNode,
            }).ToImmutableArray() };
        }

        private static PathModel Corrupt(PathModel source, string defect)
        {
            ImmutableArray<PathNode> nodes = source.PathNodes;
            nodes = defect switch
            {
                "null" => nodes.SetItem(11, null),
                "badMain" => nodes.SetItem(11, nodes[11] with { NextMainNode = 99 }),
                "badSiding" => nodes.SetItem(11, nodes[11] with { NextSidingNode = 99 }),
                "negativeMain" => nodes.SetItem(11, nodes[11] with { NextMainNode = -2 }),
                "negativeSiding" => nodes.SetItem(11, nodes[11] with { NextSidingNode = -2 }),
                "mainCycle" => nodes.SetItem(9, nodes[9] with { NextMainNode = 0 }),
                "sidingCycle" => nodes.SetItem(11, nodes[11] with { NextSidingNode = 11 }),
                "disconnected" => nodes.Add(Node(99, PathNodeType.Via, -1, -1)),
                "interiorMain" => nodes.SetItem(11, nodes[11] with { NextMainNode = 5 }),
                "shared" => nodes.SetItem(4, nodes[4] with { NextSidingNode = 10 }),
                "backward" => nodes.SetItem(11, nodes[11] with { NextSidingNode = 3 }),
                "touch" => nodes.SetItem(10, nodes[10] with { NextSidingNode = 4 }),
                "enclosed" => nodes.SetItem(10, nodes[10] with { NextSidingNode = 6 }),
                "crossing" => nodes.SetItem(10, nodes[10] with { NextSidingNode = 5 }),
                "duplicateStart" => nodes.SetItem(3, nodes[3] with { NodeType = PathNodeType.Start }),
                _ => throw new ArgumentOutOfRangeException(nameof(defect)),
            };
            return source with { PathNodes = nodes };
        }
    }
}
