using System.Collections.Immutable;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrainPathToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow sut = new(() => null, () => null, _ => { })
            {
                Active = false,
            };

            sut.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, sut.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenActiveWithNoEditorRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow sut = new(() => null, () => null, _ => { })
            {
                Active = true,
            };

            sut.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, sut.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenSelectPathThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow sut = new(() => null, () => null, _ => invocations++);

            sut.SelectPath("path-1");

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenHighlightNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow sut = new(() => null, () => null, _ => invocations++);

            sut.HighlightNode(3);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectPathInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow sut = new(() => null, () => null, action => action());

            sut.SelectPath("path-1");

            Assert.AreEqual(TrainPathSnapshot.Empty, sut.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenHighlightNodeInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow sut = new(() => null, () => null, action => action());

            sut.HighlightNode(0);

            Assert.AreEqual(TrainPathSnapshot.Empty, sut.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenPathEditorIsNotQueried()
        {
            int editorQueries = 0;
            TrainPathToolWindow sut = new(() => { editorQueries++; return null; }, () => null, action => action())
            {
                Active = false,
            };

            sut.RefreshSnapshot();

            Assert.AreEqual(0, editorQueries);
        }
    }
}
