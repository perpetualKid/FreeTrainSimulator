using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrainPathToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow trainPathToolWindow= new TrainPathToolWindow(() => null, () => null, _ => { })
            {
                Active = false,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenActiveWithNoEditorRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, _ => { })
            {
                Active = true,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenSelectPathThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, _ => invocations++);

            trainPathToolWindow.SelectPath("path-1");

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenHighlightNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, _ => invocations++);

            trainPathToolWindow.HighlightNode(3);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectPathInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, action => action());

            trainPathToolWindow.SelectPath("path-1");

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenHighlightNodeInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, action => action());

            trainPathToolWindow.HighlightNode(0);

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenPathEditorIsNotQueried()
        {
            int editorQueries = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => { editorQueries++; return null; }, () => null, action => action())
            {
                Active = false,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(0, editorQueries);
        }
    }
}
