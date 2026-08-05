using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class MapContextMenuActionBuilderTests
    {
        [TestMethod]
        public void WhenNodeMoveIsInProgressThenOnlyCancelMoveIsOffered()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(
                new TestPathPoint(PathNodeType.Intermediate), true, true);

            Assert.AreEqual(1, actions.Length);
            Assert.AreEqual(MapContextMenuAction.CancelMoveNode, actions[0]);
        }

        [TestMethod]
        public void WhenNodeIsNullThenNoActionsAreOffered()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(null, true, false);

            Assert.IsTrue(actions.IsEmpty);
        }

        [TestMethod]
        public void WhenNodeCannotBeMovedThenMoveNodeIsNotOffered()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(
                new TestPathPoint(PathNodeType.Intermediate), false, false);

            Assert.DoesNotContain(MapContextMenuAction.MoveNode, actions);
        }

        [TestMethod]
        public void WhenNodeHasNoWaitPointThenSetWaitPointIsOffered()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(
                new TestPathPoint(PathNodeType.Intermediate), true, false);

            Assert.Contains(MapContextMenuAction.SetWaitPoint, actions);
            Assert.DoesNotContain(MapContextMenuAction.ClearWaitPoint, actions);
        }

        [TestMethod]
        public void WhenNodeHasWaitPointThenClearWaitPointIsOffered()
        {
            TestPathPoint node = new TestPathPoint(PathNodeType.Wait)
            {
                WaitInfo = new PathNodeWaitInfo { WaitTime = 30 },
            };

            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(node, true, false);

            Assert.Contains(MapContextMenuAction.ClearWaitPoint, actions);
            Assert.DoesNotContain(MapContextMenuAction.SetWaitPoint, actions);
        }

        [TestMethod]
        public void WhenNodeHasReversalPointThenClearReversalPointIsOffered()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(
                new TestPathPoint(PathNodeType.Reversal), true, false);

            Assert.Contains(MapContextMenuAction.ClearReversalPoint, actions);
            Assert.DoesNotContain(MapContextMenuAction.SetReversalPoint, actions);
        }

        [TestMethod]
        public void WhenNodeIsValidThenRepairNodeIsNotOffered()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(
                new TestPathPoint(PathNodeType.Intermediate), true, false);

            Assert.DoesNotContain(MapContextMenuAction.RepairNode, actions);
        }

        [TestMethod]
        public void WhenNodeIsInvalidThenRepairNodeIsOffered()
        {
            TestPathPoint node = new TestPathPoint(PathNodeType.Intermediate)
            {
                ValidationResult = PathNodeInvalidReasons.NotOnTrack,
            };

            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(node, true, false);

            Assert.Contains(MapContextMenuAction.RepairNode, actions);
        }

        [TestMethod]
        public void WhenNodeIsMovableThenMoveNodeIsTheFirstAction()
        {
            ImmutableArray<MapContextMenuAction> actions = MapContextMenuActionBuilder.Build(
                new TestPathPoint(PathNodeType.Intermediate), true, false);

            Assert.AreEqual(MapContextMenuAction.MoveNode, actions[0]);
        }

        private sealed record TestPathPoint : TrainPathPointBase
        {
            public TestPathPoint(PathNodeType nodeType)
                : base(PointD.None, nodeType)
            {
            }
        }
    }
}
