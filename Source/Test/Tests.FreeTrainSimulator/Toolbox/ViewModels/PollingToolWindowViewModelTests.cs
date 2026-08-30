using System;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.ViewModels
{
    [TestClass]
    public class PollingToolWindowViewModelTests
    {
        [TestMethod]
        public void WhenDisposedAfterStartThenStoppedOnce()
        {
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                TestPollingToolWindowViewModel viewModel = new TestPollingToolWindowViewModel(scheduler);

                viewModel.Start();
                viewModel.Dispose();
                viewModel.Dispose();

                Assert.AreEqual(1, viewModel.StoppedCount);
            }
        }

        [TestMethod]
        public void WhenStartAfterDisposeThenObjectDisposedExceptionThrown()
        {
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                TestPollingToolWindowViewModel viewModel = new TestPollingToolWindowViewModel(scheduler);
                viewModel.Dispose();

                Assert.ThrowsExactly<ObjectDisposedException>(() => viewModel.Start());
            }
        }

        private sealed class TestPollingToolWindowViewModel : PollingToolWindowViewModel
        {
            public TestPollingToolWindowViewModel(ToolWindowRefreshScheduler scheduler)
                : base(scheduler, TimeSpan.FromMilliseconds(50))
            {
            }

            public int StoppedCount { get; private set; }

            protected override void Refresh()
            {
            }

            protected override void OnStopped()
            {
                StoppedCount++;
            }
        }
    }
}
