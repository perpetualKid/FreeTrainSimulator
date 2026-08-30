using System.Collections.Generic;
using System.Collections.Immutable;

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
    public class PathEndpointNameResolverTests
    {
        [TestMethod]
        [DataRow(TrackDirection.Ahead, 100f, 200f, "Ahead Station")]
        [DataRow(TrackDirection.Reverse, 300f, 200f, "Reverse Station")]
        public void WhenPlatformsExistInBothDirectionsThenClosestInPathDirectionIsSelected(TrackDirection direction, float startPosition, float expectedPosition, string expectedStation)
        {
            PlatformTrackItem ahead = Platform(1, 200, "Ahead Station");
            PlatformTrackItem reverse = Platform(1, 200, "Reverse Station");
            PlatformTrackItem expected = direction == TrackDirection.Ahead ? ahead : reverse;
            expected = expected with { SectionDistance = expectedPosition };

            string station = PathEndpointNameResolver.NearestStation(
                new[] { (1, (double)startPosition, direction, 0d) }, new[] { expected }, 1000);

            Assert.AreEqual(expectedStation, station);
        }

        [TestMethod]
        public void WhenPlatformIsBeyondMaximumDistanceThenNoStationIsSelected()
        {
            string station = PathEndpointNameResolver.NearestStation(
                new[] { (1, 0d, TrackDirection.Ahead, 900d) },
                new[] { Platform(1, 200, "Too Far") }, 1000);

            Assert.IsNull(station);
        }

        [TestMethod]
        public void WhenRouteHasNoPlatformThenEndpointFallbackIsReturned()
        {
            PathModel path = new()
            {
                PathNodes = ImmutableArray.Create(
                    new PathNode(WorldLocation.None) { NodeType = PathNodeType.Start, NextMainNode = 1 },
                    new PathNode(WorldLocation.None) { NodeType = PathNodeType.End, NextMainNode = -1 }),
            };

            string station = PathEndpointNameResolver.Resolve(path, null, null, true,
                PathEndpointNameResolver.DefaultMaximumDistance);

            Assert.AreEqual("Start", station);
        }

        private static PlatformTrackItem Platform(int nodeIndex, float sectionDistance, string stationName)
        {
            return new PlatformTrackItem(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeIndex = nodeIndex,
                SectionDistance = sectionDistance,
                StationName = stationName,
            };
        }
    }
}
