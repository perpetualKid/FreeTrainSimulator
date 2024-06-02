using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common;
using Orts.Models.State;
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


    }
}
