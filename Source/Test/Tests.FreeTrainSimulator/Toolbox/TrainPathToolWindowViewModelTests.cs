using System;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrainPathToolWindowViewModelTests
    {
        private static TrainPathToolWindow CreateBridge(Action<Action> invoker)
        {
            return new TrainPathToolWindow(() => null, () => null, invoker);
        }

        [TestMethod]
        public void WhenSelectedPathSetThenBridgeSelectPathIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);

            sut.SelectedPath = new TrainPathListItemViewModel("path-1", "First Path");

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectedPathClearedThenBridgeSelectPathIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);
            sut.SelectedPath = new TrainPathListItemViewModel("path-1", "First Path");

            sut.SelectedPath = null;

            Assert.AreEqual(2, invocations);
        }

        [TestMethod]
        public void WhenSelectedPathSetToSameInstanceThenBridgeIsNotCalledAgain()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);
            TrainPathListItemViewModel path = new("path-1", "First Path");
            sut.SelectedPath = path;

            sut.SelectedPath = path;

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectedPathSetThenStatusMessageIsCleared()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);

            sut.SelectedPath = new TrainPathListItemViewModel("path-1", "First Path");

            Assert.AreEqual(string.Empty, sut.StatusMessage);
        }

        [TestMethod]
        public void WhenSelectedNodeSetThenBridgeHighlightNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);

            sut.SelectedNode = new TrainPathNodeItemViewModel(2, "Junction", true);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectedNodeClearedThenBridgeHighlightNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);
            sut.SelectedNode = new TrainPathNodeItemViewModel(2, "Junction", true);

            sut.SelectedNode = null;

            Assert.AreEqual(2, invocations);
        }

        [TestMethod]
        public void WhenSearchTextMatchesPathThenPathRemainsVisible()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);
            TrainPathListItemViewModel match = new("p1", "Northbound");
            sut.Paths.Add(match);

            sut.SearchText = "north";

            Assert.IsTrue(match.IsVisible);
        }

        [TestMethod]
        public void WhenSearchTextDoesNotMatchPathThenPathIsHidden()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            TrainPathToolWindowViewModel sut = new(bridge, Dispatcher.CurrentDispatcher);
            TrainPathListItemViewModel other = new("p2", "Southbound");
            sut.Paths.Add(other);

            sut.SearchText = "north";

            Assert.IsFalse(other.IsVisible);
        }
    }
}
