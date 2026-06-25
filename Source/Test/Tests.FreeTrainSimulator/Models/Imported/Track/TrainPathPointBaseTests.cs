using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Models.Imported.Track
{
    [TestClass]
    public class TrainPathPointBaseTests
    {
        private sealed record TrainPathPoint : TrainPathPointBase
        {
            public TrainPathPoint(in PointD location, PathNodeType nodeType) : base(location, nodeType)
            {
            }

            public TrainPathPoint(PathNode node, TrackWorld trackWorld) : base(node, trackWorld)
            {
            }
        }

        [TestMethod]
        public void NextMainNodeSimplePathTest()
        {
            List<TrainPathPointBase> startEndPath = new List<TrainPathPointBase>
            {
                new TrainPathPoint(PointD.None, PathNodeType.Start)
                {
                    NextMainNode = 1,
                },
                new TrainPathPoint(PointD.None, PathNodeType.End),
            };

            TrainPathPoint endNode = startEndPath.NextPathPoint(startEndPath[0], PathSectionType.MainPath) as TrainPathPoint;
            Assert.AreEqual(endNode, startEndPath[1]);
        }

        [TestMethod]
        public void PreviousMainNodeSimplePathTest()
        {
            List<TrainPathPointBase> startEndPath = new List<TrainPathPointBase>
            {
                new TrainPathPoint(PointD.None, PathNodeType.Start)
                {
                    NextMainNode = 1,
                },
                new TrainPathPoint(PointD.None, PathNodeType.End),
            };

            TrainPathPoint startNode = startEndPath.PreviousPathPoint(startEndPath[1], PathSectionType.MainPath) as TrainPathPoint;
            Assert.AreEqual(startNode, startEndPath[0]);
        }

        [TestMethod]
        public void ConstructorFromPathNodePreservesRoundTripFields()
        {
            PathNodeWaitInfo waitInfo = new PathNodeWaitInfo()
            {
                WaitTime = 45,
            };
            PathNode pathNode = new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = PathNodeType.Wait | PathNodeType.Junction,
                NodeIndex = 7,
                NextMainNode = 2,
                NextSidingNode = 3,
                WaitInfo = waitInfo,
            };

            TrainPathPoint pathPoint = new TrainPathPoint(pathNode, CreateInitializedTrackWorld());

            Assert.AreEqual(7, pathPoint.NodeIndex);
            Assert.AreEqual(2, pathPoint.NextMainNode);
            Assert.AreEqual(3, pathPoint.NextSidingNode);
            Assert.AreSame(waitInfo, pathPoint.WaitInfo);
        }

        private static TrackWorld CreateInitializedTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();

    }
}
