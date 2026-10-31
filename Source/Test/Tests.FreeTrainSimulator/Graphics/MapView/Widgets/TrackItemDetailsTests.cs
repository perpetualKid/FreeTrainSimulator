using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using ModelItem = FreeTrainSimulator.Models.Track.TrackItemBase;
using ModelTrack = FreeTrainSimulator.Models.Track;

namespace Tests.FreeTrainSimulator.Graphics.MapView.Widgets
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class TrackItemDetailsTests
    {
        [TestMethod]
        public async Task ItemZeroPopulatesIndexAndSubtypeOnFirstInspection()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync())
            {
                INameValueInformationProvider widget = fixture.Items[MapContentType.Crossovers]
                    .Cast<INameValueInformationProvider>().Single();

                InformationDictionary details = widget.DetailInfo;

                Assert.AreEqual(("0", "CrossOver"), (details["Item Index"], details["Item Type"]));
            }
        }

        [TestMethod]
        public async Task RepeatedInspectionReturnsSamePopulatedDictionary()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync())
            {
                INameValueInformationProvider widget = fixture.Items[MapContentType.Crossovers]
                    .Cast<INameValueInformationProvider>().Single();
                InformationDictionary details = widget.DetailInfo;
                _ = fixture.Items[MapContentType.LevelCrossings].Cast<INameValueInformationProvider>().Single().DetailInfo;

                Assert.AreSame(details, widget.DetailInfo);
            }
        }

        [TestMethod]
        public async Task InspectingAnotherItemDoesNotMutatePreviouslyReturnedDetails()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync())
            {
                INameValueInformationProvider widget = fixture.Items[MapContentType.Crossovers]
                    .Cast<INameValueInformationProvider>().Single();
                InformationDictionary details = widget.DetailInfo;
                KeyValuePair<string, string>[] expected = details.ToArray();

                _ = fixture.Items[MapContentType.LevelCrossings].Cast<INameValueInformationProvider>().Single().DetailInfo;

                Assert.AreSequenceEqual(expected, details.ToArray());
            }
        }

        [TestMethod]
        public async Task ItemZeroIncludesDerivedSignalFields()
        {
            using (ToolboxInspectionFixture fixture = await CreateSignalFixtureAsync("Route A", true))
            {

                InformationDictionary details = fixture.Items[MapContentType.Signals]
                    .Cast<INameValueInformationProvider>().Single().DetailInfo;

                Assert.AreEqual(("0", "Signal", "Normal", "Route A"),
                    (details["Item Index"], details["Item Type"], details["Signal Type"], details["Signal Name"]));
            }
        }

        [TestMethod]
        public async Task RailAndRoadItemsWithSameIndexKeepIndependentDetails()
        {
            using (ToolboxInspectionFixture fixture = await CreateSignalFixtureAsync("Route A", true))
            {
                InformationDictionary rail = fixture.Items[MapContentType.Signals]
                    .Cast<INameValueInformationProvider>().Single().DetailInfo;

                InformationDictionary road = fixture.Items[MapContentType.CarSpawners]
                    .Cast<INameValueInformationProvider>().Single().DetailInfo;

                Assert.AreEqual(("0", "Signal", "Route A", "0", "Car Spawner", (string)null, false),
                    (rail["Item Index"], rail["Item Type"], rail["Signal Name"], road["Item Index"], road["Item Type"], road["Signal Name"], ReferenceEquals(rail, road)));
            }
        }

        [TestMethod]
        public async Task DifferentItemSubtypesDoNotRetainUnrelatedFields()
        {
            using (ToolboxInspectionFixture fixture = await CreateSignalFixtureAsync("Route A", true))
            {
                _ = fixture.Items[MapContentType.Signals].Cast<INameValueInformationProvider>().Single().DetailInfo;

                InformationDictionary hazard = fixture.Items[MapContentType.Hazards]
                    .Cast<INameValueInformationProvider>().Single().DetailInfo;

                Assert.AreEqual(("1", "Hazard", (string)null, (string)null),
                    (hazard["Item Index"], hazard["Item Type"], hazard["Signal Type"], hazard["Signal Name"]));
            }
        }

        [TestMethod]
        public async Task SameItemIndexInDifferentRoutesKeepsRouteLocalDerivedFields()
        {
            using (ToolboxInspectionFixture first = await CreateSignalFixtureAsync("Route A", true))
            {
                using (ToolboxInspectionFixture second = await CreateSignalFixtureAsync("Route B", false))
                {
                    INameValueInformationProvider firstWidget = first.Items[MapContentType.Signals].Cast<INameValueInformationProvider>().Single();
                    INameValueInformationProvider secondWidget = second.Items[MapContentType.OtherSignals].Cast<INameValueInformationProvider>().Single();
                    InformationDictionary firstDetails = firstWidget.DetailInfo;

                    InformationDictionary secondDetails = secondWidget.DetailInfo;
                    _ = firstWidget.DetailInfo;

                    Assert.AreEqual(("0", "Route A", "Normal", "0", "Route B", "Other", false),
                        (firstDetails["Item Index"], firstDetails["Signal Name"], firstDetails["Signal Type"],
                        secondDetails["Item Index"], secondDetails["Signal Name"], secondDetails["Signal Type"], ReferenceEquals(firstDetails, secondDetails)));
                }
            }
        }

        [TestMethod]
        public async Task RailAndRoadCrossingsWithSameIndexKeepTheirProvenanceAndCaches()
        {
            WorldLocation[] locations = ToolboxInspectionFixture.CrossingLocations();
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateAsync(
                ImmutableArray.Create<ModelItem>(new ModelTrack.LevelCrossingTrackItem(locations[1]) { TrackItemIndex = 0, NodeIndex = 1 }),
                ImmutableArray.Create<ModelItem>(new ModelTrack.RoadLevelCrossingTrackItem(locations[2]) { TrackItemIndex = 0, NodeIndex = 1 })))
            {
                LevelCrossingTrackItem rail = fixture.Items[MapContentType.LevelCrossings].Cast<LevelCrossingTrackItem>().Single();
                LevelCrossingTrackItem road = fixture.Items[MapContentType.RoadCrossings].Cast<LevelCrossingTrackItem>().Single();
                InformationDictionary railDetails = rail.DetailInfo;

                InformationDictionary roadDetails = road.DetailInfo;

                Assert.AreEqual((false, true, "0", "0", "Level Crossing", "Level Crossing", false),
                    (rail.RoadLevelCrossing, road.RoadLevelCrossing, railDetails["Item Index"], roadDetails["Item Index"],
                    railDetails["Item Type"], roadDetails["Item Type"], ReferenceEquals(railDetails, roadDetails)));
            }
        }

        [TestMethod]
        public async Task CopyBeforeInspectionPopulatesItsOwnItemZeroDetails()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync())
            {
                CrossOverTrackItem source = fixture.Items[MapContentType.Crossovers].Cast<CrossOverTrackItem>().Single();
                CrossOverTrackItem copy = source with { };

                InformationDictionary details = copy.DetailInfo;

                Assert.AreEqual(("0", "CrossOver", false),
                    (details["Item Index"], details["Item Type"], ReferenceEquals(details, source.DetailInfo)));
            }
        }

        [TestMethod]
        public async Task CopyAfterInspectionDoesNotShareTheSourcesPopulatedCache()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync())
            {
                CrossOverTrackItem source = fixture.Items[MapContentType.Crossovers].Cast<CrossOverTrackItem>().Single();
                InformationDictionary sourceDetails = source.DetailInfo;

                CrossOverTrackItem copy = source with { };
                InformationDictionary details = copy.DetailInfo;

                Assert.AreEqual(("0", "CrossOver", false),
                    (details["Item Index"], details["Item Type"], ReferenceEquals(details, sourceDetails)));
            }
        }

        private static Task<ToolboxInspectionFixture> CreateSignalFixtureAsync(string signalName, bool normal)
        {
            WorldLocation location = new WorldLocation(1, 0, new Vector3(100, 0, 120));
            return ToolboxInspectionFixture.CreateAsync(
                ImmutableArray.Create<ModelItem>(new ModelTrack.SignalTrackItem(location)
                {
                    TrackItemIndex = 0,
                    NodeIndex = 1,
                    NormalSignal = normal,
                    SignalType = signalName,
                }, new ModelTrack.HazardTrackItem(location) { TrackItemIndex = 1, NodeIndex = 1 }),
                ImmutableArray.Create<ModelItem>(new ModelTrack.CarSpawnerTrackItem(new WorldLocation(2, 0, new Vector3(100, 0, 120)))
                {
                    TrackItemIndex = 0,
                    NodeIndex = 1,
                }));
        }
    }
}
