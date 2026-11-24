using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

using AvalonDock;
using AvalonDock.Layout;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [STATestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenChartIsUndockedThenPathChangesContinueRefreshingAndHideStopsRefresh()
        {
            (MainWindow shell, MainWindowViewModel model, LayoutAnchorable chart, _) = CreateChartShell();
            DockingManager manager = DetachedTestManager(shell);
            Window owner = new Window { Content = manager, Width = 800, Height = 600, ShowInTaskbar = false };
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                PathChartToolWindow bridge = new PathChartToolWindow(() => null, () => null, _ => Assert.Fail("No map navigation expected."));
                model.PathChartTool = new PathChartToolWindowViewModel(bridge, scheduler);
                PathChartControl drawing = new PathChartControl { DataContext = model.PathChartTool };
                drawing.SetBinding(PathChartControl.ProfileProperty, new Binding(nameof(PathChartToolWindowViewModel.Profile)));
                chart.Content = drawing;
                try
                {
                    HookDetachedTestShell(shell);
                    owner.Show();
                    model.ShowPathChartToolCommand.Execute(null);
                    manager.DetachAnchorableToWindow(chart);
                    PumpMenuBindings();
                    Assert.IsTrue(chart.IsDetached);
                    Assert.IsFalse(chart.IsVisible, "The docked placeholder is hidden during standalone hosting.");
                    Assert.IsTrue(bridge.Active, "A visible undocked chart must keep requesting owner snapshots.");
                    Assert.IsTrue(model.IsPathChartToolVisible);
                    PathChartProfile first = new PathChartProfile(true, string.Empty, [], [], [], 100, []);
                    PathChartProfile second = new PathChartProfile(true, string.Empty, [], [], [], 250, []);
                    PublishDetachedChart(bridge, first, "First path");
                    TickDetachedScheduler(scheduler);
                    Assert.AreSame(first, drawing.Profile);
                    PublishDetachedChart(bridge, second, "Second path");
                    TickDetachedScheduler(scheduler);
                    Assert.AreSame(second, drawing.Profile);
                    StringAssert.Contains(model.PathChartTool.TitleText, "Second path", StringComparison.Ordinal);
                    model.ShowPathChartToolCommand.Execute(null);
                    Assert.IsTrue(chart.IsDetached, "Showing a detached pane must not create a second docked view.");
                    model.TogglePathChartToolCommand.Execute(null);
                    Assert.IsFalse(chart.IsDetached);
                    Assert.IsFalse(chart.IsVisible);
                    Assert.IsFalse(bridge.Active);
                    model.ShowPathChartToolCommand.Execute(null);
                    Assert.IsTrue(chart.IsVisible);
                    Assert.IsTrue(bridge.Active);
                }
                finally
                {
                    manager.ReattachAllDetachedAnchorables();
                    owner.Content = null;
                    owner.Close();
                    ShutdownChartShell(shell);
                }
            }
        }

        [STATestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenPathEditorIsUndockedThenRoutePathListsContinueUpdatingAfterReattachment()
        {
            (MainWindow shell, MainWindowViewModel model, _, _) = CreateChartShell();
            DockingManager manager = DetachedTestManager(shell);
            LayoutAnchorable editor = manager.Layout.Descendents().OfType<LayoutAnchorable>()
                .Single(window => window.ContentId == "TrainPathToolWindow");
            Window owner = new Window { Content = manager, Width = 800, Height = 600, ShowInTaskbar = false };
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, action => action(),
                    () => { }, () => { }, _ => { }, () => { }, () => { });
                model.TrainPathTool = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.PathsTabIndex);
                ListBox list = new ListBox { DataContext = model.TrainPathTool };
                list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(TrainPathToolWindowViewModel.Paths)));
                editor.Content = list;
                try
                {
                    HookDetachedTestShell(shell);
                    owner.Show();
                    manager.DetachAnchorableToWindow(editor);
                    PumpMenuBindings();
                    Assert.IsTrue(editor.IsDetached);
                    Assert.IsTrue(bridge.Active);
                    bridge.UpdatePaths([new PathModel { Id = "first-route-path", Name = "First route path" }]);
                    bridge.RefreshSnapshot();
                    TickDetachedScheduler(scheduler);
                    Assert.AreEqual("first-route-path", ((TrainPathListItemViewModel)list.Items[0]).Id);
                    bridge.UpdatePaths([]);
                    bridge.RefreshSnapshot();
                    TickDetachedScheduler(scheduler);
                    Assert.AreEqual(0, list.Items.Count, "Unloading the route clears the detached editor list.");
                    bridge.UpdatePaths([new PathModel { Id = "second-route-path", Name = "Second route path" }]);
                    bridge.RefreshSnapshot();
                    TickDetachedScheduler(scheduler);
                    Assert.AreEqual(1, list.Items.Count);
                    Assert.AreEqual("second-route-path", ((TrainPathListItemViewModel)list.Items[0]).Id);
                    manager.ReattachAnchorable(editor);
                    Assert.IsTrue(editor.IsVisible);
                    Assert.IsTrue(bridge.Active);
                    model.ToggleTrainPathToolCommand.Execute(null);
                    Assert.IsFalse(bridge.Active);
                }
                finally
                {
                    manager.ReattachAllDetachedAnchorables();
                    owner.Content = null;
                    owner.Close();
                    ShutdownChartShell(shell);
                }
            }
        }

        private static DockingManager DetachedTestManager(MainWindow shell)
        {
            DockingManager manager = (DockingManager)typeof(MainWindow)
                .GetField("DockingManager", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(shell);
            manager.AllowDetachedWindows = true;
            return manager;
        }

        private static void HookDetachedTestShell(MainWindow shell)
            => typeof(MainWindow).GetMethod("HookToolWindowAnchorables", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);

        private static void PublishDetachedChart(PathChartToolWindow bridge, PathChartProfile profile, string name)
            => SetField(bridge, "snapshot", new PathChartSnapshot(null, new PathModel { Name = name }, null, null, 0, 0, profile, true, null));

        private static void TickDetachedScheduler(ToolWindowRefreshScheduler scheduler)
        {
            MethodInfo tick = typeof(ToolWindowRefreshScheduler).GetMethod("Timer_Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 10; i++)
                tick.Invoke(scheduler, [null, EventArgs.Empty]);
            PumpMenuBindings();
        }
    }
}