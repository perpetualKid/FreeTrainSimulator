using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [STATestClass]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class UnsavedPathConfirmationDispatchTests
    {
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenRequestCompletesWhileQueuedThenHostDoesNotForwardPrompt()
        {
            ToolboxGameHostControl host = CreateHost();
            UnsavedPathConfirmationEventArgs request = new UnsavedPathConfirmationEventArgs();
            int forwarded = 0;
            host.UnsavedPathConfirmationRequested += (_, _) => forwarded++;
            Forward(host, request);
            DispatcherFrame barrier = new DispatcherFrame();
            _ = host.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => request.Completion.SetResult(false)));
            _ = host.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => barrier.Continue = false));

            Dispatcher.PushFrame(barrier);

            Assert.AreEqual(0, forwarded);
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenCompletedRequestArrivesThenHostDoesNotQueuePrompt()
        {
            ToolboxGameHostControl host = CreateHost();
            UnsavedPathConfirmationEventArgs request = new UnsavedPathConfirmationEventArgs();
            request.Completion.SetResult(false);
            int queued = 0;
            DispatcherHookEventHandler hook = (_, _) => queued++;
            host.Dispatcher.Hooks.OperationPosted += hook;
            try
            {
                Forward(host, request);
            }
            finally
            {
                host.Dispatcher.Hooks.OperationPosted -= hook;
            }

            Assert.AreEqual(0, queued);
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenPendingRequestIsForwardedThenSubscriberCanCompleteIt()
        {
            ToolboxGameHostControl host = CreateHost();
            UnsavedPathConfirmationEventArgs request = new UnsavedPathConfirmationEventArgs();
            host.UnsavedPathConfirmationRequested += (_, args) => args.Completion.SetResult(true);
            Forward(host, request);
            DispatcherFrame barrier = new DispatcherFrame();
            _ = host.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => barrier.Continue = false));

            Dispatcher.PushFrame(barrier);

            Assert.IsTrue(request.Completion.Task.Result);
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenCompletedRequestReachesShellThenItReturnsWithoutOpeningDialog()
        {
            MainWindow shell = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(shell);
#pragma warning restore CA1816
            UnsavedPathConfirmationEventArgs request = new UnsavedPathConfirmationEventArgs();
            request.Completion.SetResult(false);

            typeof(MainWindow).GetMethod("MapHost_UnsavedPathConfirmationRequested", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(shell, new object[] { null, request });

            Assert.IsFalse(request.Completion.Task.Result);
        }

        private static ToolboxGameHostControl CreateHost()
        {
            // Bypass the hardware-backed game startup; only the real WPF forwarding boundary is exercised.
            ToolboxGameHostControl host = (ToolboxGameHostControl)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxGameHostControl));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(host);
#pragma warning restore CA1816
            typeof(DispatcherObject).GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(host, Dispatcher.CurrentDispatcher);
            return host;
        }

        private static void Forward(ToolboxGameHostControl host, UnsavedPathConfirmationEventArgs request)
            => typeof(ToolboxGameHostControl).GetMethod("Game_UnsavedPathConfirmationRequested", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host, new object[] { null, request });
    }
}
