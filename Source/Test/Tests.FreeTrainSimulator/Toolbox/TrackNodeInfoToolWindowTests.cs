using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrackNodeInfoToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenRowsRemainEmpty()
        {
            TrackNodeInfoToolWindow trackNodeInfoToolWindow = new TrackNodeInfoToolWindow(_ => { })
            {
                Active = false,
            };

            trackNodeInfoToolWindow.RefreshSnapshot();

            Assert.IsEmpty(trackNodeInfoToolWindow.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenActiveWithNoContextRefreshSnapshotThenRowsRemainEmpty()
        {
            TrackNodeInfoToolWindow trackNodeInfoToolWindow = new TrackNodeInfoToolWindow(_ => { })
            {
                Active = true,
            };

            trackNodeInfoToolWindow.RefreshSnapshot();

            Assert.IsEmpty(trackNodeInfoToolWindow.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenSearchTextIsNotNumericThenGameThreadInvokerIsNotCalled()
        {
            int invocations = 0;
            TrackNodeInfoToolWindow trackNodeInfoToolWindow = new TrackNodeInfoToolWindow(_ => invocations++);

            trackNodeInfoToolWindow.SearchByIndex("abc", searchRoads: false);

            Assert.AreEqual(0, invocations);
        }

        [TestMethod]
        public void WhenSearchTextIsNumericThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrackNodeInfoToolWindow trackNodeInfoToolWindow = new TrackNodeInfoToolWindow(action =>
            {
                invocations++;
                action();
            });

            trackNodeInfoToolWindow.SearchByIndex("12", searchRoads: true);

            Assert.AreEqual(1, invocations);
        }
    }
}
