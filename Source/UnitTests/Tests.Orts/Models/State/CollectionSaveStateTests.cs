using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common;
using Orts.Models.State;
using Orts.Simulation.Timetables;
using Orts.Simulation.Track;

namespace Tests.Orts.Models.Track
{
    [TestClass]
    public class CollectionSaveStateTests
    {
        [TestMethod]
        public async Task EnumArrayRoundTripTestNewInstance()
        {
            EnumArray<TrackCircuitPartialPathRoute, Direction> trackCircuitRouteElements = new EnumArray<TrackCircuitPartialPathRoute, Direction>();

            trackCircuitRouteElements[Direction.Backward] = new TrackCircuitPartialPathRoute();

            Collection<TrackCircuitPartialPathRouteSaveState> partialRoutes = await trackCircuitRouteElements.SnapshotCollection<TrackCircuitPartialPathRouteSaveState, TrackCircuitPartialPathRoute>().ConfigureAwait(false);

            List<TrackCircuitPartialPathRoute> restoreItems = new List<TrackCircuitPartialPathRoute>();
            await restoreItems.RestoreCollectionCreateNewInstances(partialRoutes).ConfigureAwait(false);
            EnumArray<TrackCircuitPartialPathRoute, Direction> results = new EnumArray<TrackCircuitPartialPathRoute, Direction>(restoreItems);
        }

        [TestMethod]
        public async Task EnumArrayRoundTripTestExistingInstance()
        {
            EnumArray<TrackCircuitPartialPathRoute, Direction> trackCircuitRouteElements = new EnumArray<TrackCircuitPartialPathRoute, Direction>();

            trackCircuitRouteElements[Direction.Backward] = new TrackCircuitPartialPathRoute();

            Collection<TrackCircuitPartialPathRouteSaveState> partialRoutes = await trackCircuitRouteElements.SnapshotCollection<TrackCircuitPartialPathRouteSaveState, TrackCircuitPartialPathRoute>().ConfigureAwait(false);

            await trackCircuitRouteElements.RestoreCollectionOnExistingInstances(partialRoutes).ConfigureAwait(false);
        }


        [TestMethod]
        public async Task DictionaryRoundTripNewInstanceTest()
        {
            Dictionary<int, WaitInfo> source = new Dictionary<int, WaitInfo>
            {
                { 7, new WaitInfo() { PathDirection = PathCheckDirection.Both, CheckPath = new TrackCircuitPartialPathRoute() } },
                { 13, null },
                { 21, new WaitInfo() { WaitType = WaitInfoType.Follow } }
            };

            Dictionary<int, WaitInfoSaveState> saveStates = await source.SnapshotDictionary<WaitInfoSaveState, WaitInfo, int>().ConfigureAwait(false);

            Dictionary<int, WaitInfo> target = new Dictionary<int, WaitInfo>();
            await target.RestoreDictionaryCreateNewInstances(saveStates).ConfigureAwait(false);

            Assert.IsTrue(saveStates.ContainsKey(7));
            Assert.IsTrue(saveStates.ContainsKey(13));
            Assert.IsTrue(saveStates.ContainsKey(21));
            Assert.IsNull(saveStates[13]);
            Assert.AreEqual(PathCheckDirection.Both, target[7].PathDirection);
            Assert.IsNull(target[13]);
            Assert.AreEqual(WaitInfoType.Follow, target[21].WaitType);
        }

    }
}
