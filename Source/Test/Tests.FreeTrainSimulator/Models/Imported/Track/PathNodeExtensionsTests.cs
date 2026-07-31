using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Models.Imported.Track
{
    [TestClass]
    public class PathNodeExtensionsTests
    {
        private sealed record TrainPathPoint : TrainPathPointBase
        {
            public TrainPathPoint(in PointD location, PathNodeType nodeType) : base(location, nodeType)
            {
            }
        }

        // 0 (start) -> 1 -> 3 (end) on main path, 0 -> 2 -> 3 on passing path
        private static List<TrainPathPointBase> CreateMainPathWithPassingPath()
        {
            return new List<TrainPathPointBase>
            {
                new TrainPathPoint(PointD.None, PathNodeType.Start) { NextMainNode = 1, NextSidingNode = 2 },
                new TrainPathPoint(PointD.None, PathNodeType.Intermediate) { NextMainNode = 3, NextSidingNode = -1 },
                new TrainPathPoint(PointD.None, PathNodeType.Intermediate) { NextMainNode = -1, NextSidingNode = 3 },
                new TrainPathPoint(PointD.None, PathNodeType.End) { NextMainNode = -1, NextSidingNode = -1 },
            };
        }

        private static PathNode CreatePathNode(PathNodeType nodeType)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
            };
        }

        [TestMethod]
        public void NodeOfTypeReturnsFirstMatchForNonEndType()
        {
            List<PathNode> nodes = new List<PathNode>
            {
                CreatePathNode(PathNodeType.Start),
                CreatePathNode(PathNodeType.Intermediate),
                CreatePathNode(PathNodeType.Intermediate),
                CreatePathNode(PathNodeType.End),
            };

            Assert.AreSame(nodes[1], nodes.NodeOfType(PathNodeType.Intermediate));
        }

        [TestMethod]
        public void NodeOfTypeReturnsLastMatchForEndType()
        {
            List<PathNode> nodes = new List<PathNode>
            {
                CreatePathNode(PathNodeType.Start),
                CreatePathNode(PathNodeType.End),
                CreatePathNode(PathNodeType.End),
            };

            Assert.AreSame(nodes[2], nodes.NodeOfType(PathNodeType.End));
        }

        [TestMethod]
        public void NodeOfTypeOnPathPointsReturnsLastEndNode()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.AreSame(path[3], path.NodeOfType(PathNodeType.End));
        }

        [TestMethod]
        public void NodeOfTypeReturnsNullWhenNoMatch()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.IsNull(path.NodeOfType(PathNodeType.Reversal));
        }

        [TestMethod]
        public void NodeOfTypeOnNullPathNodeListThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => ((IList<PathNode>)null).NodeOfType(PathNodeType.Start));
        }

        [TestMethod]
        public void NodeOfTypeOnNullPathPointListThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => ((IList<TrainPathPointBase>)null).NodeOfType(PathNodeType.Start));
        }

        [TestMethod]
        public void NextPathPointFollowsMainPath()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.AreSame(path[1], path.NextPathPoint(path[0], PathSectionType.MainPath));
        }

        [TestMethod]
        public void NextPathPointFollowsPassingPath()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.AreSame(path[2], path.NextPathPoint(path[0], PathSectionType.PassingPath));
        }

        [TestMethod]
        public void NextPathPointOfEndNodeReturnsNull()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.IsNull(path.NextPathPoint(path[3], PathSectionType.MainPath));
        }

        [TestMethod]
        public void NextPathPointWithOutOfRangeIndexReturnsNull()
        {
            List<TrainPathPointBase> path = new List<TrainPathPointBase>
            {
                new TrainPathPoint(PointD.None, PathNodeType.Start) { NextMainNode = 99 },
            };

            Assert.IsNull(path.NextPathPoint(path[0], PathSectionType.MainPath));
        }

        [TestMethod]
        public void PreviousPathPointFollowsMainPath()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.AreSame(path[1], path.PreviousPathPoint(path[3], PathSectionType.MainPath));
        }

        [TestMethod]
        public void PreviousPathPointFollowsPassingPath()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.AreSame(path[2], path.PreviousPathPoint(path[3], PathSectionType.PassingPath));
        }

        [TestMethod]
        public void PreviousPathPointOfStartNodeReturnsNull()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            // regression: nodes carrying -1 next-indices must not be reported as predecessor
            Assert.IsNull(path.PreviousPathPoint(path[0], PathSectionType.MainPath));
        }

        [TestMethod]
        public void PreviousPathPointForUnknownPointReturnsNull()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();
            TrainPathPointBase foreignPoint = new TrainPathPoint(PointD.None, PathNodeType.Intermediate);

            Assert.IsNull(path.PreviousPathPoint(foreignPoint, PathSectionType.MainPath));
        }

        [TestMethod]
        public void NextPathPointOnNullListThrows()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.ThrowsExactly<ArgumentNullException>(() => ((IList<TrainPathPointBase>)null).NextPathPoint(path[0], PathSectionType.MainPath));
        }

        [TestMethod]
        public void NextPathPointOnNullPointThrows()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.ThrowsExactly<ArgumentNullException>(() => path.NextPathPoint(null, PathSectionType.MainPath));
        }

        [TestMethod]
        public void PreviousPathPointOnNullListThrows()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.ThrowsExactly<ArgumentNullException>(() => ((IList<TrainPathPointBase>)null).PreviousPathPoint(path[0], PathSectionType.MainPath));
        }

        [TestMethod]
        public void PreviousPathPointOnNullPointThrows()
        {
            List<TrainPathPointBase> path = CreateMainPathWithPassingPath();

            Assert.ThrowsExactly<ArgumentNullException>(() => path.PreviousPathPoint(null, PathSectionType.MainPath));
        }
    }
}
