using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static FreeTrainSimulator.Toolbox.MapContextMenuActionBuilder;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class MapContextMenuActionBuilderTests
    {
        [TestMethod]
        public void WhenNodeMoveIsInProgressThenNodeMenuOnlyOffersCancelMove()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 2, true, new MapContextMenuState { IsPlacementActive = true });

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(MapContextMenuAction.CancelPlacement, items[0].Action);
        }

        [TestMethod]
        public void WhenNodeMoveIsInProgressThenMapMenuOnlyOffersCancelMove()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForMap(
                new MapContextMenuState { IsPlacementActive = true, CanExtendPath = true, CanUndo = true });

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(MapContextMenuAction.CancelPlacement, items[0].Action);
        }

        [TestMethod]
        public void WhenStartAnchorCanBePlacedThenMapMenuOffersSetStartHere()
        {
            PathNode anchor = PlacementAnchor();
            ImmutableArray<MapContextMenuItem> items = BuildForMap(new MapContextMenuState { CanSetStartAnchor = true }, anchor);

            Assert.Contains(MapContextMenuAction.SetStartHere, Actions(items));
            Assert.AreSame(anchor, items.Single(item => item.Action == MapContextMenuAction.SetStartHere).PlacementAnchor);
        }

        [TestMethod]
        public void WhenEndAnchorCannotBePlacedThenMapMenuOmitsSetEndHere()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForMap(new MapContextMenuState { CanSetStartAnchor = true }, PlacementAnchor());

            Assert.DoesNotContain(MapContextMenuAction.SetEndHere, Actions(items));
        }

        [TestMethod]
        public void WhenNoEditablePathExistsThenMapMenuOffersStartNewPathHere()
        {
            PathNode anchor = PlacementAnchor();
            ImmutableArray<MapContextMenuItem> items = BuildForMap(new MapContextMenuState { CanStartNewPath = true }, anchor);

            Assert.Contains(MapContextMenuAction.StartNewPathHere, Actions(items));
            Assert.AreSame(anchor, items.Single(item => item.Action == MapContextMenuAction.StartNewPathHere).PlacementAnchor);
        }

        [TestMethod]
        public void WhenEditablePathExistsThenMapMenuStillOffersAnchoredStartNewPathHere()
        {
            PathNode anchor = PlacementAnchor();
            ImmutableArray<MapContextMenuItem> items = BuildForMap(
                new MapContextMenuState { CanStartNewPath = true, CanSetStartAnchor = true }, anchor);

            MapContextMenuItem startNewPath = items.Single(item => item.Action == MapContextMenuAction.StartNewPathHere);
            Assert.AreSame(anchor, startNewPath.PlacementAnchor);
            Assert.DoesNotContain(MapContextMenuAction.StartNewPath, Actions(items));
        }

        [TestMethod]
        public void WhenNoTrackAnchorExistsThenMapMenuOmitsEndpointHereActions()
        {            ImmutableArray<MapContextMenuItem> items = BuildForMap(new MapContextMenuState
            {
                CanStartNewPath = true,
                CanSetStartAnchor = true,
                CanSetEndAnchor = true,
            });

            Assert.DoesNotContain(MapContextMenuAction.StartNewPathHere, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.SetStartHere, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.SetEndHere, Actions(items));
        }

        [TestMethod]
        public void WhenPlacementIsActiveThenSpanMenuOnlyOffersCancelPlacement()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForSpan(0, PlacementAnchor(),
                ImmutableArray<ResolvedRouteCandidate>.Empty, new MapContextMenuState { IsPlacementActive = true });

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(MapContextMenuAction.CancelPlacement, items[0].Action);
        }

        [TestMethod]
        public void WhenNodeIsNullThenNoNodeActionsAreOffered()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(null, 0, true, default);

            Assert.IsTrue(items.IsEmpty);
        }

        [TestMethod]
        public void WhenNodeCannotBeMovedThenMoveNodeIsNotOffered()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 1, false, default);

            Assert.DoesNotContain(MapContextMenuAction.MoveNode, Actions(items));
        }

        [TestMethod]
        public void WhenNodeIsEndThenRemoveViaPointIsNotOffered()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.End), 3, true, default);

            Assert.DoesNotContain(MapContextMenuAction.RemoveViaPoint, Actions(items));
        }

        [TestMethod]
        public void WhenNodeIsIntermediateThenRemoveViaPointIsOffered()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 1, true, default);

            Assert.Contains(MapContextMenuAction.RemoveViaPoint, Actions(items));
        }

        [TestMethod]
        public void WhenNodeHasWaitPointThenClearWaitPointIsOffered()
        {
            TestPathPoint node = new TestPathPoint(PathNodeType.Wait)
            {
                WaitInfo = new PathNodeWaitInfo { WaitTime = 30 },
            };

            ImmutableArray<MapContextMenuItem> items = BuildForNode(node, 1, true, default);

            Assert.Contains(MapContextMenuAction.ClearWaitPoint, Actions(items));
        }

        [TestMethod]
        public void WhenNodeHasReversalPointThenClearReversalPointIsOffered()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Reversal), 1, true, default);

            Assert.Contains(MapContextMenuAction.ClearReversalPoint, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.SetReversalPoint, Actions(items));
        }

        [TestMethod]
        public void WhenNodeIsValidThenRepairNodeIsNotOffered()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 1, true, default);

            Assert.DoesNotContain(MapContextMenuAction.RepairNode, Actions(items));
        }

        [TestMethod]
        public void WhenNodeIsInvalidThenRepairNodeIsOffered()
        {
            TestPathPoint node = new TestPathPoint(PathNodeType.Intermediate)
            {
                ValidationResult = PathNodeInvalidReasons.NotOnTrack,
            };

            ImmutableArray<MapContextMenuItem> items = BuildForNode(node, 1, true, default);

            Assert.Contains(MapContextMenuAction.RepairNode, Actions(items));
        }

        [TestMethod]
        public void WhenNodeActionsAreBuiltThenTheyCarryTheNodeIndex()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 4, true, default);

            Assert.IsTrue(items.All(item => item.IsSeparator
                || item.Action == MapContextMenuAction.Undo
                || item.Action == MapContextMenuAction.Redo
                || item.NodeIndex == 4));
        }

        [TestMethod]
        public void WhenMenuHasMultipleSectionsThenSeparatorsAreInserted()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 1, true, new MapContextMenuState { CanUndo = true });

            Assert.AreEqual(2, items.Count(item => item.IsSeparator));
        }

        [TestMethod]
        public void WhenMenuEndsWithASectionBreakThenNoTrailingSeparatorRemains()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForMap(new MapContextMenuState { CanExtendPath = true });

            Assert.IsFalse(items[^1].IsSeparator);
        }

        [TestMethod]
        public void WhenMenuStartsWithASectionThenNoLeadingSeparatorIsAdded()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForMap(new MapContextMenuState { CanUndo = true, CanRedo = true });

            Assert.IsFalse(items[0].IsSeparator);
            Assert.AreEqual(MapContextMenuAction.Undo, items[0].Action);
        }

        [TestMethod]
        public void WhenSpanIsUnambiguousThenOnlySpanEditsAreOffered()
        {
            PathNode placementAnchor = PlacementAnchor();
            ImmutableArray<MapContextMenuItem> items = BuildForSpan(2, placementAnchor, ImmutableArray<ResolvedRouteCandidate>.Empty, default);

            Assert.Contains(MapContextMenuAction.AddViaPoint, Actions(items));
            Assert.Contains(MapContextMenuAction.RemoveRestOfPath, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.SelectRouteCandidate, Actions(items));
            Assert.AreSame(placementAnchor, items.Single(item => item.Action == MapContextMenuAction.AddViaPoint).PlacementAnchor);
        }

        [TestMethod]
        public void WhenSpanIsAmbiguousThenEachCandidateIsOffered()
        {
            ImmutableArray<ResolvedRouteCandidate> candidates = ImmutableArray.Create(
                Candidate(1, 2),
                Candidate(1, 3));

            ImmutableArray<MapContextMenuItem> items = BuildForSpan(1, PlacementAnchor(), candidates, default);

            MapContextMenuItem[] candidateItems = items.Where(item => item.Action == MapContextMenuAction.SelectRouteCandidate).ToArray();
            Assert.AreEqual(2, candidateItems.Length);
            Assert.AreEqual(0, candidateItems[0].CandidateIndex);
            Assert.AreEqual(1, candidateItems[1].CandidateIndex);
            Assert.AreEqual(1, candidateItems[0].NodeIndex);
        }

        [TestMethod]
        public void WhenMapMenuIsBuiltThenOnlyAvailablePathActionsAreOffered()
        {
            MapContextMenuState state = new MapContextMenuState
            {
                CanExtendPath = true,
                CanStartNewPath = true,
            };

            ImmutableArray<MapContextMenuItem> items = BuildForMap(state, PlacementAnchor());

            Assert.Contains(MapContextMenuAction.ExtendPath, Actions(items));
            Assert.Contains(MapContextMenuAction.StartNewPathHere, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.SavePath, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.ReResolvePath, Actions(items));
        }

        [TestMethod]
        public void WhenUndoIsAvailableThenNodeMenuOffersUndo()
        {
            ImmutableArray<MapContextMenuItem> items = BuildForNode(
                new TestPathPoint(PathNodeType.Intermediate), 1, true, new MapContextMenuState { CanUndo = true });

            Assert.Contains(MapContextMenuAction.Undo, Actions(items));
            Assert.DoesNotContain(MapContextMenuAction.Redo, Actions(items));
        }

        private static ImmutableArray<MapContextMenuAction> Actions(ImmutableArray<MapContextMenuItem> items)
            => items.Select(item => item.Action).ToImmutableArray();

        private static ResolvedRouteCandidate Candidate(params int[] routeNodeIndexes)
            => new ResolvedRouteCandidate(ImmutableArray.Create(routeNodeIndexes), ImmutableArray<int>.Empty, ImmutableArray<PathRouteAnchor>.Empty, 1);

        private static PathNode PlacementAnchor()
            => new PathNode(PointD.ToWorldLocation(new PointD(10, 0)));

        private sealed record TestPathPoint : TrainPathPointBase
        {
            public TestPathPoint(PathNodeType nodeType)
                : base(PointD.None, nodeType)
            {
            }
        }
    }
}
