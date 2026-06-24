using System;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

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
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = new TrainPathListItemViewModel("path-1", "First Path")
                })
                {
                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenSelectedPathClearedThenBridgeSelectPathIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = new TrainPathListItemViewModel("path-1", "First Path")
                })
                {
                    trainPathToolWindowViewModel.SelectedPath = null;
                }
            }

            Assert.AreEqual(2, invocations);
        }

        [TestMethod]
        public void WhenSelectedPathSetToSameInstanceThenBridgeIsNotCalledAgain()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathListItemViewModel path = new TrainPathListItemViewModel("path-1", "First Path");
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = path
                })
                {
                    trainPathToolWindowViewModel.SelectedPath = path;
                }
            }
            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectedPathSetThenStatusMessageIsCleared()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = new TrainPathListItemViewModel("path-1", "First Path")
                })
                {
                    Assert.AreEqual(string.Empty, trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenSelectedNodeSetThenBridgeHighlightNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, "Junction", true)
                })
                {
                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenSelectedNodeClearedThenBridgeHighlightNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, "Junction", true)
                })
                {
                    trainPathToolWindowViewModel.SelectedNode = null;
                }
            }

            Assert.AreEqual(2, invocations);
        }

        [TestMethod]
        public void WhenSearchTextMatchesPathThenPathRemainsVisible()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    TrainPathListItemViewModel match = new TrainPathListItemViewModel("p1", "Northbound");
                    trainPathToolWindowViewModel.Paths.Add(match);

                    trainPathToolWindowViewModel.SearchText = "north";

                    Assert.IsTrue(match.IsVisible);
                }
            }
        }

        [TestMethod]
        public void WhenSearchTextDoesNotMatchPathThenPathIsHidden()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    TrainPathListItemViewModel other = new TrainPathListItemViewModel("p2", "Southbound");
                    trainPathToolWindowViewModel.Paths.Add(other);

                    trainPathToolWindowViewModel.SearchText = "north";

                    Assert.IsFalse(other.IsVisible);
                }
            }
        }

        [TestMethod]
        public void WhenNodeRowUpdatedThenNodeDetailsAreUpdated()
        {
            TrainPathNodeItemViewModel node = new TrainPathNodeItemViewModel(new TrainPathNodeRow(1, "Wait", true, 7, 2, 3, 45, null));

            node.Update(new TrainPathNodeRow(4, "Invalid", false, 9, -1, -1, null, "NotOnTrack"));

            Assert.AreEqual(4, node.Index);
            Assert.AreEqual("Invalid", node.NodeType);
            Assert.IsFalse(node.Valid);
            Assert.AreEqual(9, node.TrackNodeIndex);
            Assert.AreEqual(-1, node.NextMainNode);
            Assert.AreEqual(-1, node.NextSidingNode);
            Assert.IsNull(node.WaitTime);
            Assert.AreEqual("NotOnTrack", node.Validation);
        }
    }
}
