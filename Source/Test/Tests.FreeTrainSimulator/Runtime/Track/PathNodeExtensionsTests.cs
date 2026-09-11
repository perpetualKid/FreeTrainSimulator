using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Runtime.Track
{
    [TestClass]
    public class PathNodeExtensionsTests
    {
        // EditorTrainPath relies on NextPathPoint returning null for a dangling last node (no outgoing link)
        // so it can skip building a section for an incomplete/partial path. These tests pin that contract.

        [TestMethod]
        public void WhenMainLinkIsPresentThenNextPathPointReturnsLinkedNode()
        {
            List<TrainPathPointBase> points = new List<TrainPathPointBase>
            {
                Point(PathNodeType.Start, nextMainNode: 1),
                Point(PathNodeType.End, nextMainNode: -1),
            };

            TrainPathPointBase next = points.NextPathPoint(points[0], PathSectionType.MainPath);

            Assert.AreSame(points[1], next);
        }

        [TestMethod]
        public void WhenMainLinkIsAbsentThenNextPathPointReturnsNull()
        {
            // Dangling last node of a partial path: not flagged End, no outgoing main link.
            List<TrainPathPointBase> points = new List<TrainPathPointBase>
            {
                Point(PathNodeType.Start, nextMainNode: 1),
                Point(PathNodeType.Via, nextMainNode: -1),
            };

            TrainPathPointBase next = points.NextPathPoint(points[1], PathSectionType.MainPath);

            Assert.IsNull(next);
        }

        [TestMethod]
        public void WhenSidingLinkIsAbsentThenNextPathPointReturnsNull()
        {
            List<TrainPathPointBase> points = new List<TrainPathPointBase>
            {
                Point(PathNodeType.Start, nextMainNode: 1),
                Point(PathNodeType.Via, nextMainNode: -1),
            };

            TrainPathPointBase next = points.NextPathPoint(points[0], PathSectionType.PassingPath);

            Assert.IsNull(next);
        }

        [TestMethod]
        public void WhenMainLinkIsOutOfRangeThenNextPathPointReturnsNull()
        {
            List<TrainPathPointBase> points = new List<TrainPathPointBase>
            {
                Point(PathNodeType.Start, nextMainNode: 5),
            };

            TrainPathPointBase next = points.NextPathPoint(points[0], PathSectionType.MainPath);

            Assert.IsNull(next);
        }

        private static TestTrainPathPoint Point(PathNodeType nodeType, int nextMainNode)
        {
            return new TestTrainPathPoint(nodeType) { NextMainNode = nextMainNode };
        }

        private sealed record TestTrainPathPoint : TrainPathPointBase
        {
            public TestTrainPathPoint(PathNodeType nodeType)
                : base(PointD.None, nodeType)
            {
            }
        }
    }
}
