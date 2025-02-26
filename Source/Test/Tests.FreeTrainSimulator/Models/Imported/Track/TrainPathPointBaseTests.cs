using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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

    }
}
