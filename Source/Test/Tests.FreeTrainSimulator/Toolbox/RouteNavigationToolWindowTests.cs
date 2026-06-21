using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class RouteNavigationToolWindowTests
    {
        [TestMethod]
        public void WhenTrackItemSearchTextIsNotNumericThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => invocations++);

            routeNavigationToolWindow.SearchTrackItemByIndex("abc");

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenTrackItemSearchTextIsNumericThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(action =>
            {
                invocations++;
                action();
            });
            routeNavigationToolWindow.UpdateContext(new TestTrackNodeInfoContext());

            routeNavigationToolWindow.SearchTrackItemByIndex("12");

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenTrackNodeSearchTextIsNotNumericThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => invocations++);

            routeNavigationToolWindow.SearchTrackNodeByIndex("abc", false);

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenTrackNodeSearchTextIsNumericThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(action =>
            {
                invocations++;
                action();
            });
            routeNavigationToolWindow.UpdateContext(new TestTrackNodeInfoContext());

            routeNavigationToolWindow.SearchTrackNodeByIndex("5", true);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenNavigateToStationWithNegativeIndexThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => invocations++);

            routeNavigationToolWindow.NavigateToStation(-1);

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenNavigateToPlatformWithNegativeIndexThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => invocations++);

            routeNavigationToolWindow.NavigateToPlatform(-1);

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenNavigateToSidingWithNegativeIndexThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => invocations++);

            routeNavigationToolWindow.NavigateToSiding(-1);

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenNavigateToStationWithValidIndexThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(action =>
            {
                invocations++;
                action();
            });
            routeNavigationToolWindow.UpdateContext(new TestTrackNodeInfoContext());

            routeNavigationToolWindow.NavigateToStation(0);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenSnapshotRemainsEmpty()
        {
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => { })
            {
                Active = false,
            };
            routeNavigationToolWindow.UpdateContext(new TestTrackNodeInfoContext());

            routeNavigationToolWindow.RefreshSnapshot();

            Assert.AreSame(RouteNavigationSnapshot.Empty, routeNavigationToolWindow.CaptureRouteNavigationSnapshot());
        }

        [TestMethod]
        public void WhenSnapshotIsEmptyThenDetailRowsAreEmpty()
        {
            RouteNavigationToolWindow routeNavigationToolWindow = new RouteNavigationToolWindow(_ => { })
            {
                Active = false,
            };

            Assert.IsEmpty(routeNavigationToolWindow.CaptureRouteNavigationSnapshot().DetailRows);
        }

        private sealed class TestTrackNodeInfoContext : ITrackNodeInfoContext
        {
            public INameValueInformationProvider TrackNodeInfo => null;

            public IMapViewport Viewport => null;

            public IMapHostControl HostControl => null;

            public ToolboxContent Content => null;

            public TrackWorld TrackWorld => null;
        }
    }
}
