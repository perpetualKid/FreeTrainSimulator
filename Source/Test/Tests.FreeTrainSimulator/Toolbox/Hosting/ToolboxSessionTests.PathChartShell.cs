using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using AvalonDock;
using AvalonDock.Layout;

using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [STATestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenChartShellDescriptorsAreInitializedThenHostedViewModelsCanArriveLater()
        {
            (MainWindow shell, MainWindowViewModel viewModel, LayoutAnchorable chart, _) = CreateChartShell();
            try
            {
                Assert.IsFalse(chart.IsVisible);
                Assert.IsNull(viewModel.PathChartTool);
                Assert.IsFalse(viewModel.ShowPathChartToolCommand.CanExecute(null));
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    PathChartToolWindow bridge = new PathChartToolWindow(() => null, () => null, _ => Assert.Fail("No map command is expected."));
                    viewModel.PathChartTool = new PathChartToolWindowViewModel(bridge, scheduler);
                    Assert.IsTrue(viewModel.ShowPathChartToolCommand.CanExecute(null));
                    viewModel.ShowPathChartToolCommand.Execute(null);
                    Assert.IsTrue(bridge.Active, "The callback must resolve the newly arrived view model, not a null constructor-time target.");
                    viewModel.TogglePathChartToolCommand.Execute(null);
                    Assert.IsFalse(bridge.Active);
                }
            }
            finally
            {
                ShutdownChartShell(shell);
            }
        }

        [STATestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        [DataRow(160d, 400d)]
        [DataRow(520d, 520d)]
        public void WhenChartShellOpensThenOnlyAnInsufficientLoggingPaneGrows(double initialHeight, double expectedHeight)
        {
            (MainWindow shell, MainWindowViewModel viewModel, LayoutAnchorable chart, LayoutAnchorablePane pane) = CreateChartShell();
            try
            {
                pane.DockHeight = new GridLength(initialHeight);
                viewModel.ShowPathChartToolCommand.Execute(null);
                Assert.IsTrue(chart.IsVisible);
                Assert.AreSame(pane, chart.Parent);
                Assert.AreEqual(expectedHeight, pane.DockHeight.Value, 0.001);
                pane.DockHeight = new GridLength(310);
                viewModel.TogglePathChartToolCommand.Execute(null);
                Assert.IsFalse(chart.IsVisible);
                Assert.AreEqual(310d, pane.DockHeight.Value, 0.001);
            }
            finally
            {
                ShutdownChartShell(shell);
            }
        }

        private static (MainWindow Shell, MainWindowViewModel ViewModel, LayoutAnchorable Chart, LayoutAnchorablePane Pane) CreateChartShell()
        {
            TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource terminated = Completion();
            ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
            ready.SetResult(null);
            terminated.SetResult();
            SetField(host, "ownerResourcesReleased", true);
            SetField(host, "hostPanel", Activator.CreateInstance(typeof(ToolboxGameHostControl).GetNestedType("HostPanel", BindingFlags.NonPublic), new object[] { (Action)(() => { }) }));
            MainWindow shell = CreateShutdownShell(host);
            MainWindowViewModel viewModel = (MainWindowViewModel)typeof(MainWindow).GetField("viewModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
            DockingManager manager = (DockingManager)typeof(MainWindow).GetField("DockingManager", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(shell);
            LayoutAnchorablePane pane = new LayoutAnchorablePane() { DockHeight = new GridLength(160) };
            manager.Layout = new LayoutRoot() { RootPanel = new LayoutPanel() { Orientation = Orientation.Vertical } };
            manager.Layout.RootPanel.Children.Add(pane);
            (string Field, string ContentId)[] tools = new[]
            {
                ("RouteToolAnchorable", "RouteToolWindow"), ("LocationToolAnchorable", "LocationToolWindow"),
                ("DebugToolAnchorable", "DebugToolWindow"), ("LogToolAnchorable", "LogToolWindow"),
                ("RouteNavigationToolAnchorable", "RouteNavigationToolWindow"), ("HelpToolAnchorable", "HelpToolWindow"),
                ("SettingsToolAnchorable", "SettingsToolWindow"), ("TrainPathToolAnchorable", "TrainPathToolWindow"),
                ("PathChartToolAnchorable", "PathChartToolWindow"),
            };
            LayoutAnchorable chart = null;
            foreach ((string field, string contentId) in tools)
            {
                LayoutAnchorable anchorable = new LayoutAnchorable() { ContentId = contentId, Content = new FrameworkElement(), CanHide = true };
                pane.Children.Add(anchorable);
                SetField(shell, field, anchorable);
                if (contentId == "PathChartToolWindow")
                {
                    chart = anchorable;
                    chart.Hide();
                }
            }
            typeof(MainWindow).GetMethod("InitializeToolWindowDescriptors", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
            typeof(MainWindow).GetMethod("InitializeToolWindowCommands", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
            return (shell, viewModel, chart, pane);
        }

        private static void ShutdownChartShell(MainWindow shell)
        {
            Task shutdown = (Task)typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
            shutdown.GetAwaiter().GetResult();
            shell.Dispose();
        }
    }
}