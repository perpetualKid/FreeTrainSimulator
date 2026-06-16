using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrackItemInfoToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenRowsRemainEmpty()
        {
            TestInformationProvider provider = new();
            provider.DetailInfo["Index"] = "7";
            TrackItemInfoToolWindow trackItemInfoToolWindow = new TrackItemInfoToolWindow(_ => { })
            {
                Active = false,
            };
            trackItemInfoToolWindow.UpdateContext(new TestTrackItemInfoContext(provider));

            trackItemInfoToolWindow.RefreshSnapshot();

            Assert.AreEqual(0, trackItemInfoToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenActiveRefreshSnapshotThenRowsArePublished()
        {
            TestInformationProvider provider = new();
            provider.DetailInfo["Index"] = "7";
            TrackItemInfoToolWindow trackItemInfoToolWindow = new TrackItemInfoToolWindow(_ => { })
            {
                Active = true,
            };
            trackItemInfoToolWindow.UpdateContext(new TestTrackItemInfoContext(provider));

            trackItemInfoToolWindow.RefreshSnapshot();

            Assert.AreEqual(1, trackItemInfoToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenProviderIsNullThenRowsRemainEmpty()
        {
            TrackItemInfoToolWindow trackItemInfoToolWindow = new TrackItemInfoToolWindow(_ => { })
            {
                Active = true,
            };
            trackItemInfoToolWindow.UpdateContext(new TestTrackItemInfoContext(null));

            trackItemInfoToolWindow.RefreshSnapshot();

            Assert.AreEqual(0, trackItemInfoToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenSearchTextIsNotNumericThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            TrackItemInfoToolWindow trackItemInfoToolWindow = new TrackItemInfoToolWindow(_ => invocations++);

            trackItemInfoToolWindow.SearchByIndex("abc");

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenSearchTextIsNumericThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrackItemInfoToolWindow trackItemInfoToolWindow = new TrackItemInfoToolWindow(action =>
            {
                invocations++;
                action();
            });
            trackItemInfoToolWindow.UpdateContext(new TestTrackItemInfoContext(null));

            trackItemInfoToolWindow.SearchByIndex("12");

            Assert.AreEqual(1, invocations);
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
            public InformationDictionary DetailInfo { get; } = new();

            public Dictionary<string, FormatOption> FormattingOptions { get; } = new();
        }
    }
}
