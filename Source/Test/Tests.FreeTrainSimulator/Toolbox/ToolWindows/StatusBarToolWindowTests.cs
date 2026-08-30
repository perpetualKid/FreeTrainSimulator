using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.ToolWindows
{
    [TestClass]
    public class StatusBarToolWindowTests
    {
        [TestMethod]
        public void WhenNoContextsRefreshSnapshotThenLocationAndPlaceholderFieldsArePublished()
        {
            StatusBarToolWindow statusBar = new StatusBarToolWindow();

            statusBar.RefreshSnapshot();

            // Three location fields (Tile, LocationX, LocationZ) plus the Track and Item placeholders.
            Assert.HasCount(5, statusBar.CaptureSnapshot().Fields);
        }

        [TestMethod]
        public void WhenNoContextsRefreshSnapshotThenTileFieldIsPresent()
        {
            StatusBarToolWindow statusBar = new StatusBarToolWindow();

            statusBar.RefreshSnapshot();

            Assert.IsTrue(statusBar.CaptureSnapshot().Fields.Any(field => field.Key == "Tile"));
        }

        [TestMethod]
        public void WhenTrackNodeProviderHasNodeIndexThenTrackFieldUsesThatValue()
        {
            TestInformationProvider trackNodeProvider = new TestInformationProvider();
            trackNodeProvider.DetailInfo["Node Index"] = "425";
            StatusBarToolWindow statusBar = new StatusBarToolWindow();
            statusBar.UpdateContexts(null, new TestTrackNodeInfoContext(trackNodeProvider), null);

            statusBar.RefreshSnapshot();

            StatusBarField trackField = statusBar.CaptureSnapshot().Fields.Single(field => field.Key == "TrackNode");
            Assert.AreEqual("425", trackField.Value);
        }

        [TestMethod]
        public void WhenTrackItemProviderHasTypeAndIndexThenItemFieldCombinesThem()
        {
            TestInformationProvider trackItemProvider = new TestInformationProvider();
            trackItemProvider.DetailInfo["Item Type"] = "Siding";
            trackItemProvider.DetailInfo["Item Index"] = "12";
            StatusBarToolWindow statusBar = new StatusBarToolWindow();
            statusBar.UpdateContexts(null, null, new TestTrackItemInfoContext(trackItemProvider));

            statusBar.RefreshSnapshot();

            StatusBarField itemField = statusBar.CaptureSnapshot().Fields.Single(field => field.Key == "TrackItem");
            Assert.AreEqual("Siding 12", itemField.Value);
        }

        [TestMethod]
        public void WhenTrackItemProviderIsEmptyThenItemFieldValueIsNull()
        {
            StatusBarToolWindow statusBar = new StatusBarToolWindow();
            statusBar.UpdateContexts(null, null, new TestTrackItemInfoContext(null));

            statusBar.RefreshSnapshot();

            StatusBarField itemField = statusBar.CaptureSnapshot().Fields.Single(field => field.Key == "TrackItem");
            Assert.IsNull(itemField.Value);
        }

        private sealed class TestTrackNodeInfoContext : ITrackNodeInfoContext
        {
            public TestTrackNodeInfoContext(INameValueInformationProvider provider)
            {
                TrackNodeInfo = provider;
            }

            public INameValueInformationProvider TrackNodeInfo { get; }

            public IMapViewport Viewport => null;

            public IMapHostControl HostControl => null;

            public ToolboxContent Content => null;

            public TrackWorld TrackWorld => null;
        }

        private sealed class TestTrackItemInfoContext : ITrackItemInfoContext
        {
            public TestTrackItemInfoContext(INameValueInformationProvider provider)
            {
                TrackItemInfo = provider;
            }

            public INameValueInformationProvider TrackItemInfo { get; }

            public IMapViewport Viewport => null;

            public TrackWorld TrackWorld => null;
        }

        private sealed class TestInformationProvider : INameValueInformationProvider
        {
            public InformationDictionary DetailInfo { get; } = new InformationDictionary();

            public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();
        }
    }
}
