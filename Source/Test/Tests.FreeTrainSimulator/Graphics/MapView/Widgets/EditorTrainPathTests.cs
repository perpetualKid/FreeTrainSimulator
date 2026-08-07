using System.Collections.Immutable;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Graphics.MapView.Widgets
{
    // Regression coverage for the path-editor hardening: EditorTrainPath must construct and edit incomplete
    // (partial) paths without throwing. These exercise the reconstruction path that undo/redo and mutations
    // use, which previously crashed on dangling end nodes, null anchors, and removing the last point.
    [TestClass]
    public class EditorTrainPathTests
    {
        [TestMethod]
        public void WhenPathHasStartAndDanglingNodeWithoutEndThenConstructionSucceeds()
        {
            // Start -> Intermediate with no End and no outgoing link on the last node: the dangling node must
            // be skipped during section building rather than dereferencing a null endpoint.
            PathModel partialPath = CreatePath(
                Node(PathNodeType.Start, nextMainNode: 1),
                Node(PathNodeType.Intermediate, nextMainNode: -1));

            EditorTrainPath trainPath = new EditorTrainPath(partialPath, CreateTrackWorld());

            Assert.IsNotNull(trainPath);
            Assert.AreEqual(2, trainPath.PathPoints.Count);
        }

        [TestMethod]
        public void WhenSingleNodePathThenConstructionSucceeds()
        {
            PathModel singleNode = CreatePath(Node(PathNodeType.Start, nextMainNode: -1));

            EditorTrainPath trainPath = new EditorTrainPath(singleNode, CreateTrackWorld());

            Assert.IsNotNull(trainPath);
            Assert.AreEqual(1, trainPath.PathPoints.Count);
        }

        [TestMethod]
        public void WhenRemovingLastPointDownToEmptyThenNoException()
        {
            PathModel singleNode = CreatePath(Node(PathNodeType.Start, nextMainNode: -1));
            EditorTrainPath trainPath = new EditorTrainPath(singleNode, CreateTrackWorld());
            EditorPathPoint candidate = new EditorPathPoint(PointD.None, PointD.None, PathNodeType.None);

            // Removing the only point empties the path; the anchor seeding must not index an empty list.
            _ = trainPath.RemovePathPoint(candidate);

            Assert.AreEqual(0, trainPath.PathPoints.Count);
        }

        [TestMethod]
        public void WhenUpdatingEndPointAfterReconstructionThenNoException()
        {
            // After reconstruction the active anchor is seeded from the last point; a subsequent pointer move
            // (UpdatePathEndPoint) must not dereference a null anchor.
            PathModel path = CreatePath(
                Node(PathNodeType.Start, nextMainNode: 1),
                Node(PathNodeType.Intermediate, nextMainNode: -1));
            EditorTrainPath trainPath = new EditorTrainPath(path, CreateTrackWorld());

            EditorPathPoint result = trainPath.UpdatePathEndPoint(PointD.None, null, null);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void WhenUpdatingEndPointOnEmptyPathThenTreatedAsStart()
        {
            PathModel empty = new PathModel() { Id = "empty", Name = "Empty" };
            EditorTrainPath trainPath = new EditorTrainPath(empty, CreateTrackWorld());

            EditorPathPoint result = trainPath.UpdatePathEndPoint(PointD.None, null, null);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void WhenAddingFirstPointToEmptyPathThenItBecomesStartNode()
        {
            // The editor, not the caller, decides the node type: the first point added is always the Start,
            // regardless of the candidate's incoming node type.
            EditorTrainPath trainPath = CreateEmptyEditorPath();

            _ = trainPath.AddPathPoint(CreateEditableCandidate());

            Assert.AreEqual(PathNodeType.Start, trainPath.PathPoints[0].NodeType);
        }

        [TestMethod]
        public void WhenAddingFirstPointToEmptyPathThenNextMainNodeIsOne()
        {
            EditorTrainPath trainPath = CreateEmptyEditorPath();

            _ = trainPath.AddPathPoint(CreateEditableCandidate());

            Assert.AreEqual(1, trainPath.PathPoints[0].NextMainNode);
        }

        [TestMethod]
        public void WhenAddingTwoPointsThenCountBecomesTwo()
        {
            EditorTrainPath trainPath = CreateEmptyEditorPath();

            _ = trainPath.AddPathPoint(CreateEditableCandidate());
            _ = trainPath.AddPathPoint(CreateEditableCandidate());

            Assert.AreEqual(2, trainPath.PathPoints.Count);
        }

        [TestMethod]
        public void WhenAddingSecondPointThenNextMainNodeIncrements()
        {
            // The interactive append assigns each new point a forward link to the slot that will follow it.
            EditorTrainPath trainPath = CreateEmptyEditorPath();

            _ = trainPath.AddPathPoint(CreateEditableCandidate());
            _ = trainPath.AddPathPoint(CreateEditableCandidate());

            Assert.AreEqual(2, trainPath.PathPoints[1].NextMainNode);
        }

        [TestMethod]
        public void WhenLocationIsBesideRenderedMainSectionThenSpanHitReturnsSourceNode()
        {
            PathModel path = CreatePath(
                NodeAt(0, PathNodeType.Start, nextMainNode: 1),
                NodeAt(100, PathNodeType.Intermediate, nextMainNode: 2),
                NodeAt(200, PathNodeType.End, nextMainNode: -1));
            EditorTrainPath trainPath = new EditorTrainPath(path, CreateTrackWorld());

            bool found = trainPath.TryGetMainPathSpanAt(new PointD(150, 4), 10, out int fromNodeIndex);

            Assert.IsTrue(found);
            Assert.AreEqual(1, fromNodeIndex);
        }

        [TestMethod]
        public void WhenAddingPointAfterPreviewIntermediaryThenIntermediaryLinksToCommittedPoint()
        {
            // UpdatePathEndPoint temporarily inserts an intermediary junction preview when a segment crosses a
            // junction. Committing the candidate must link that preview node to the committed endpoint;
            // otherwise resolver validation sees the main path stop at the intermediary and marks later nodes
            // unreachable.
            EditorTrainPath trainPath = CreateEmptyEditorPath();
            _ = trainPath.AddPathPoint(CreateEditableCandidate());
            trainPath.PathPoints.Add(CreateEditableCandidate() with { NodeType = PathNodeType.Junction });
            typeof(EditorTrainPath).GetField("editorUseIntermediaryPathPoint", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(trainPath, true);

            _ = trainPath.AddPathPoint(CreateEditableCandidate());

            Assert.AreEqual(2, trainPath.PathPoints[1].NextMainNode);
        }

        [TestMethod]
        public void WhenActiveAnchorIsInvalidThenAddingPointIsRejected()
        {
            // Reconstructing an off-track path seeds an invalid (NotOnTrack) active anchor; AddPathPoint must
            // refuse to extend from an invalid anchor, leaving the path unchanged.
            PathModel offTrackStart = CreatePath(Node(PathNodeType.Start, nextMainNode: -1));
            EditorTrainPath trainPath = new EditorTrainPath(offTrackStart, CreateTrackWorld());

            _ = trainPath.AddPathPoint(CreateEditableCandidate());

            Assert.AreEqual(1, trainPath.PathPoints.Count);
        }

        [TestMethod]
        public void WhenAddingNullPointThenReturnsNull()
        {
            EditorTrainPath trainPath = CreateEmptyEditorPath();

            EditorPathPoint result = trainPath.AddPathPoint(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WhenRemovingNullPointThenReturnsNull()
        {
            EditorTrainPath trainPath = CreateEmptyEditorPath();

            EditorPathPoint result = trainPath.RemovePathPoint(null);

            Assert.IsNull(result);
        }

        private static EditorTrainPath CreateEmptyEditorPath()
        {
            PathModel empty = new PathModel() { Id = "empty", Name = "Empty" };
            return new EditorTrainPath(empty, CreateTrackWorld());
        }

        private static EditorPathPoint CreateEditableCandidate()
        {
            // Built via the (location, vector, nodeType) constructor so ValidationResult stays None, i.e. a
            // valid anchor the editor is allowed to extend from.
            return new EditorPathPoint(PointD.None, PointD.None, PathNodeType.Intermediate);
        }

        private static PathNode Node(PathNodeType nodeType, int nextMainNode)
        {
            return NodeAt(0, nodeType, nextMainNode);
        }

        private static PathNode NodeAt(float x, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
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

        private static TrackWorld CreateTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
    }
}
