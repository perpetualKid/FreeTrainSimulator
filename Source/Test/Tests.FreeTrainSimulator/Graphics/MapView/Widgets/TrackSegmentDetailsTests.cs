using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Graphics.MapView.Widgets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Graphics.MapView.Widgets
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class TrackSegmentDetailsTests
    {
        [TestMethod]
        public async Task EqualIndexRailAndRoadSegmentsKeepTheirOwnGeometryDetails()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                INameValueInformationProvider rail = fixture.Items[MapContentType.Tracks].Cast<INameValueInformationProvider>().Single();
                INameValueInformationProvider road = fixture.Items[MapContentType.Roads].Cast<INameValueInformationProvider>().Single();
                _ = rail.DetailInfo;

                InformationDictionary details = road.DetailInfo;

                Assert.AreEqual(("Road", "1", "0", $"{200f:F1}m", "False"),
                    (details["Segment Type"], details["Node Index"], details["Section Index"], details["Length"], details["Curved"]));
            }
        }

        [TestMethod]
        public async Task InspectingRoadThenRailRestoresRailTypeAndGeometry()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                INameValueInformationProvider rail = fixture.Items[MapContentType.Tracks].Cast<INameValueInformationProvider>().Single();
                INameValueInformationProvider road = fixture.Items[MapContentType.Roads].Cast<INameValueInformationProvider>().Single();
                _ = rail.DetailInfo;
                _ = road.DetailInfo;

                InformationDictionary details = rail.DetailInfo;

                Assert.AreEqual(("Rail Track", $"{100f:F1}m"), (details["Segment Type"], details["Length"]));
            }
        }

        [TestMethod]
        public async Task InspectingAnotherSegmentDoesNotMutatePreviouslyReturnedDetails()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                INameValueInformationProvider rail = fixture.Items[MapContentType.Tracks].Cast<INameValueInformationProvider>().Single();
                InformationDictionary details = rail.DetailInfo;
                KeyValuePair<string, string>[] expected = details.ToArray();

                _ = fixture.Items[MapContentType.Roads].Cast<INameValueInformationProvider>().Single().DetailInfo;

                Assert.AreSequenceEqual(expected, details.ToArray());
            }
        }

        [TestMethod]
        [DataRow(MapContentType.Tracks)]
        [DataRow(MapContentType.Roads)]
        public async Task RepeatedSegmentInspectionReusesItsPopulatedDictionary(MapContentType layer)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                INameValueInformationProvider segment = fixture.Items[layer].Cast<INameValueInformationProvider>().Single();
                InformationDictionary details = segment.DetailInfo;

                InformationDictionary repeated = segment.DetailInfo;

                Assert.AreSame(details, repeated);
            }
        }

        [TestMethod]
        public async Task SameSegmentIndexesInReplacementContentKeepIndependentCaches()
        {
            using (ToolboxInspectionFixture first = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            using (ToolboxInspectionFixture second = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                INameValueInformationProvider firstSegment = first.Items[MapContentType.Tracks].Cast<INameValueInformationProvider>().Single();
                INameValueInformationProvider secondSegment = second.Items[MapContentType.Tracks].Cast<INameValueInformationProvider>().Single();
                InformationDictionary firstDetails = firstSegment.DetailInfo;

                InformationDictionary secondDetails = secondSegment.DetailInfo;

                Assert.AreNotSame(firstDetails, secondDetails);
            }
        }

        [TestMethod]
        public async Task SegmentCopiedFromDifferentGeometryDoesNotInheritSameIndexDetails()
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                _ = fixture.Items[MapContentType.Tracks].Cast<INameValueInformationProvider>().Single().DetailInfo;
                TrackSegment replacement = new TrackSegment(fixture.GetSegmentSection(1, true).SectionSegments.Single());

                InformationDictionary details = replacement.DetailInfo;

                Assert.AreEqual(("Rail Track", $"{200f:F1}m"), (details["Segment Type"], details["Length"]));
            }
        }

        [TestMethod]
        [DataRow(MapContentType.Tracks, false)]
        [DataRow(MapContentType.Tracks, true)]
        [DataRow(MapContentType.Roads, false)]
        [DataRow(MapContentType.Roads, true)]
        public async Task RecordCopyOwnsItsCacheBeforeOrAfterSourceInspection(MapContentType layer, bool inspectSource)
        {
            using (ToolboxInspectionFixture fixture = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
            {
                TrackSegment source = fixture.Items[layer].Cast<TrackSegment>().Single();
                _ = inspectSource ? source.DetailInfo : null;

                TrackSegment copy = source with { };
                InformationDictionary details = copy.DetailInfo;

                Assert.AreEqual((false, true, source.DetailInfo["Segment Type"], source.DetailInfo["Length"]),
                    (ReferenceEquals(source.DetailInfo, details), ReferenceEquals(details, copy.DetailInfo), details["Segment Type"], details["Length"]));
            }
        }
    }
}
