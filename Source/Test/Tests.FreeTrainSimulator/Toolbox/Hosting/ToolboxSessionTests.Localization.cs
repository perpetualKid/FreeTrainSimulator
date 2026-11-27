using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

using AvalonDock;
using AvalonDock.Layout;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Dialogs;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using GetText;
using GetText.Wpf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [STATestMethod]
        [DoNotParallelize]
        public void WhenPathToolbarsRefreshThenAccessibleLabelsAndTooltipsFollowTheCatalog()
        {
            using (LocalizationCatalogScope catalogScope = new LocalizationCatalogScope())
            {
                using (TrainPathToolView editor = new TrainPathToolView())
                {
                    PathChartToolView chart = new PathChartToolView();
                    (Button Button, string Label)[] commands =
                    [
                        ((Button)editor.FindName("NewPathButton"), "New path"),
                        ((Button)editor.FindName("SavePathButton"), "Save path"),
                        ((Button)editor.FindName("UndoPathButton"), "Undo"),
                        ((Button)editor.FindName("RedoPathButton"), "Redo"),
                        ((Button)chart.FindName("ShowOnMapButton"), "Show on Map"),
                        ((Button)chart.FindName("ResetChartViewButton"), "Reset View"),
                ];
                    foreach ((_, string label) in commands)
                        catalogScope.Catalog.Translations[label] = ["Translated " + label];
                    catalogScope.Catalog.Translations["Path actions"] = ["Translated actions"];

                    editor.RefreshLocalization();
                    chart.RefreshLocalization();
                    PumpMenuBindings();

                    foreach ((Button button, string label) in commands)
                    {
                        Assert.AreEqual("Translated " + label, AutomationProperties.GetName(button));
                        Assert.AreEqual("Translated " + label, button.ToolTip);
                    }
                    MenuItem pathActions = (MenuItem)editor.FindName("PathActionsMenuItem");
                    Assert.AreEqual("Translated actions", AutomationProperties.GetName(pathActions));
                    Assert.StartsWith("Translated actions", (string)pathActions.Header, StringComparison.Ordinal);
                }
            }
        }

        [STATestMethod]
        [DoNotParallelize]
        public void WhenPathWorkflowLabelsExpandThenActionsWrapInANarrowPane()
        {
            using (LocalizationCatalogScope catalogScope = new LocalizationCatalogScope())
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                FieldInfo snapshot = typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                snapshot.SetValue(bridge, TrainPathSnapshot.Empty with { PassingBranchPhase = PassingBranchAuthoringPhase.SelectingRejoin });
                using (TrainPathToolWindowViewModel model = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex))
                {
                    using (TrainPathToolView editor = new TrainPathToolView { DataContext = model })
                    {
                        model.Start();
                        Window window = new Window { Content = editor, Width = 220, Height = 700, ShowInTaskbar = false };
                        try
                        {
                            window.Show();
                            PumpMenuBindings();
                            Button rejoin = (Button)editor.FindName("RejoinWithPassingViaButton");
                            Button cancel = (Button)editor.FindName("CancelRejoinButton");
                            double originalHeight = rejoin.ActualHeight;
                            const string expanded = "[Řéĵôîñ ţĥé þåššîñĝ ƀřåñçĥ ŵîţĥ ôñé åððîţîôñåļ þåššîñĝ vîå !!!]";
                            catalogScope.Catalog.Translations["Rejoin with one passing via"] = [expanded];
                            catalogScope.Catalog.Translations["Cancel"] = ["[Çåñçéļ !!!]"];
                            catalogScope.Catalog.Translations["Paths"] = ["[Þåţĥš !!!]"];
                            catalogScope.Catalog.Translations["Editor"] = ["[Éðîţôř !!!]"];
                            editor.RefreshLocalization();
                            PumpMenuBindings();
                            window.UpdateLayout();

                            Assert.AreEqual("[Þåţĥš !!!]", ((TabItem)editor.FindName("PathsTab")).Header);
                            Assert.AreEqual("[Éðîţôř !!!]", ((TabItem)editor.FindName("EditorTab")).Header);
                            Assert.AreEqual(expanded, rejoin.Content);
                            Assert.AreEqual(expanded, AutomationProperties.GetName(rejoin));
                            Assert.AreEqual("[Çåñçéļ !!!]", cancel.Content);
                            Assert.AreSame(model.CancelPassingBranchCommand, cancel.Command);
                            Assert.IsTrue(rejoin.IsVisible);
                            Assert.IsTrue(cancel.IsVisible);
                            Assert.IsFalse(((Button)editor.FindName("SetStartButton")).IsVisible);
                            Separator nodeActionsSeparator = (Separator)editor.FindName("NodeActionsSeparator");
                            Assert.AreEqual(Visibility.Collapsed, nodeActionsSeparator.Visibility);
                            WrapPanel row = (WrapPanel)rejoin.Parent;
                            Point rejoinOrigin = rejoin.TranslatePoint(new Point(), row);
                            Point cancelOrigin = cancel.TranslatePoint(new Point(), row);
                            Assert.IsGreaterThan(originalHeight, rejoin.ActualHeight, "The long translated label must wrap inside its button.");
                            Assert.IsLessThanOrEqualTo(row.ActualWidth + 0.5, rejoinOrigin.X + rejoin.ActualWidth);
                            Assert.IsGreaterThan(rejoinOrigin.Y, cancelOrigin.Y, "Cancel must wrap to the next line rather than clip.");

                            snapshot.SetValue(bridge, TrainPathSnapshot.Empty with
                            {
                                Nodes = [new TrainPathNodeRow(0, PathNodeType.Via | PathNodeType.Wait, true, 1, -1, -1, 31725, null)],
                                SelectedNodeIndex = 0,
                                CanSetSelectedWaitPoint = true,
                                CanClearSelectedWaitPoint = true,
                            });
                            model.Start();
                            PumpMenuBindings();
                            ToolBar toolbar = (ToolBar)editor.FindName("EditorActionToolbar");
                            Button start = (Button)editor.FindName("SetStartButton");
                            Button end = (Button)editor.FindName("SetEndButton");
                            Button continuePath = (Button)editor.FindName("ContinuePathButton");
                            MenuItem nodeActions = (MenuItem)editor.FindName("NodeActionsMenuItem");
                            Assert.AreSame(model.SetStartHereCommand, start.Command);
                            Assert.AreSame(model.SetEndHereCommand, end.Command);
                            Assert.AreSame(model.ContinuePathCommand, continuePath.Command);
                            Assert.IsTrue(toolbar.HasOverflowItems);
                            Assert.IsTrue(nodeActions.IsVisible);
                            Assert.IsFalse(ToolBar.GetIsOverflowItem((Menu)nodeActions.Parent));
                            Assert.IsTrue(nodeActionsSeparator.IsVisible);
                            Assert.IsFalse(ToolBar.GetIsOverflowItem(nodeActionsSeparator));
                            Assert.IsGreaterThan(0, nodeActionsSeparator.ActualWidth);
                            Assert.IsGreaterThanOrEqualTo(
                                nodeActionsSeparator.TranslatePoint(new Point(nodeActionsSeparator.ActualWidth, 0), toolbar).X,
                                nodeActions.TranslatePoint(new Point(), toolbar).X + 0.5);
                            Button[] overflow = new[] { start, end, continuePath }.Where(ToolBar.GetIsOverflowItem).ToArray();
                            Assert.IsNotEmpty(overflow);
                            toolbar.IsOverflowOpen = true;
                            PumpMenuBindings();
                            Assert.IsTrue(toolbar.IsOverflowOpen);
                            Assert.IsTrue(overflow.All(button => button.IsVisible));
                            toolbar.IsOverflowOpen = false;

                            window.Width = 700;
                            PumpMenuBindings();
                            Assert.IsFalse(toolbar.HasOverflowItems);
                            Assert.IsTrue(start.IsVisible && end.IsVisible && continuePath.IsVisible && nodeActions.IsVisible);
                            window.Width = 220;
                            PumpMenuBindings();

                            Button apply = (Button)editor.FindName("ApplyWaitOperationButton");
                            Button clear = (Button)editor.FindName("ClearWaitPointButton");
                            double originalApplyHeight = apply.ActualHeight;
                            catalogScope.Catalog.Translations["Apply Operation"] = ["[Åþþļý ţĥé šéļéçţéð ôþéřåţîôñ ţô ţĥîš ŵåîţ þôîñţ !!!]"];
                            editor.RefreshLocalization();
                            PumpMenuBindings();
                            window.UpdateLayout();
                            WrapPanel waitRow = (WrapPanel)apply.Parent;
                            Point applyOrigin = apply.TranslatePoint(new Point(), waitRow);
                            Assert.IsTrue(apply.IsVisible);
                            Assert.IsTrue(clear.IsVisible);
                            Assert.IsGreaterThan(originalApplyHeight, apply.ActualHeight);
                            Assert.IsLessThanOrEqualTo(waitRow.ActualWidth + 0.5, applyOrigin.X + apply.ActualWidth);
                            Assert.IsGreaterThan(applyOrigin.Y, clear.TranslatePoint(new Point(), waitRow).Y);
                        }
                        finally
                        {
                            window.Close();
                        }
                    }
                }
            }
        }

        [STATestMethod]
        [DoNotParallelize]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenDockTitlesAreRefreshedThenRestoredHiddenAndMapTitlesFollowTheCatalog()
        {
            using (LocalizationCatalogScope catalogScope = new LocalizationCatalogScope())
            {
                (MainWindow shell, _, LayoutAnchorable chart, LayoutAnchorablePane pane) = CreateChartShell();
                try
                {
                    DockingManager manager = DetachedTestManager(shell);
                    LayoutAnchorable routeTemplate = pane.Children.Single(item => item.ContentId == "RouteToolWindow");
                    pane.Children.Remove(routeTemplate);
                    LayoutAnchorable restoredRoute = new LayoutAnchorable { ContentId = "RouteToolWindow", Title = "Old persisted title" };
                    pane.Children.Add(restoredRoute);
                    LayoutDocumentPane documents = new LayoutDocumentPane();
                    manager.Layout.RootPanel.Children.Add(documents);
                    LayoutDocument map = new LayoutDocument { ContentId = "MapViewDocument", Title = "Old map title" };
                    documents.Children.Add(map);
                    (string ContentId, string Source)[] titles =
                    [
                        ("RouteToolWindow", "Routes"), ("LocationToolWindow", "Location"),
                        ("DebugToolWindow", "Debug Information"), ("LogToolWindow", "Logging"),
                        ("RouteNavigationToolWindow", "Route Navigation"), ("HelpToolWindow", "Help"),
                        ("SettingsToolWindow", "Settings"), ("TrainPathToolWindow", "Path Editor"),
                        ("PathChartToolWindow", "Path Chart"),
                    ];
                    foreach ((_, string source) in titles)
                        catalogScope.Catalog.Translations[source] = ["Translated " + source];
                    catalogScope.Catalog.Translations["Map View"] = ["Translated map"];
                    MethodInfo refresh = typeof(MainWindow).GetMethod("RefreshDockingTitles", BindingFlags.Instance | BindingFlags.NonPublic);
                    refresh.Invoke(shell, [catalogScope.Catalog]);
                    foreach ((string contentId, string source) in titles)
                    {
                        LayoutAnchorable current = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(item => item.ContentId == contentId);
                        Assert.AreEqual("Translated " + source, current.Title, contentId);
                    }
                    Assert.AreEqual("Translated Routes", routeTemplate.Title);
                    Assert.AreEqual("Translated map", map.Title);
                    Assert.IsFalse(chart.IsVisible, "Refreshing hidden titles must not show panes.");
                    Assert.AreSame(pane, restoredRoute.Parent);

                    catalogScope.Catalog.Translations.Clear();
                    refresh.Invoke(shell, [catalogScope.Catalog]);
                    foreach ((string contentId, string source) in titles)
                        Assert.AreEqual(source, manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(item => item.ContentId == contentId).Title);
                    Assert.AreEqual("Routes", routeTemplate.Title);
                    Assert.AreEqual("Map View", map.Title);
                    restoredRoute.Title = "Old persisted title";
                    MethodInfo find = typeof(MainWindow).GetMethod("FindToolWindowDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
                    object descriptor = find.Invoke(shell, ["RouteToolWindow"]);
                    typeof(MainWindow).GetMethod("EnsureToolWindowAnchorable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, [descriptor]);
                    Assert.AreEqual("Routes", restoredRoute.Title, "Restored panes must not retain a persisted translated title.");
                }
                finally
                {
                    ShutdownChartShell(shell);
                }
            }
        }

        [STATestMethod]
        [DoNotParallelize]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenExitDialogIsCreatedThenPromptAndUnsavedWarningAreTranslated(bool hasUnsavedChanges)
        {
            using (LocalizationCatalogScope catalogScope = new LocalizationCatalogScope())
            {
                catalogScope.Catalog.Translations["Exit {0}?"] = ["Leave {0} now?"];
                catalogScope.Catalog.Translations["Exit?"] = ["Leave now?"];
                catalogScope.Catalog.Translations["There are unsaved path changes. If you exit now, those changes will be lost."] = ["Unsaved edits will be lost."];
                catalogScope.Catalog.Translations["Exit"] = ["Leave"];
                catalogScope.Catalog.Translations["Cancel"] = ["Stay"];
                QuitDialog dialog = new QuitDialog(hasUnsavedChanges);
                try
                {
                    dialog.Show();
                    PumpMenuBindings();
                    string expected = RuntimeInfo.ApplicationName is null ? "Leave now?" : $"Leave {RuntimeInfo.ApplicationName} now?";
                    if (hasUnsavedChanges)
                        expected += Environment.NewLine + Environment.NewLine + "Unsaved edits will be lost.";
                    Assert.AreEqual(expected, dialog.Message);
                    Grid content = (Grid)dialog.Content;
                    Assert.AreEqual(expected, content.Children.OfType<TextBlock>().Single().Text);
                    Assert.AreEqual("Leave", dialog.Title);
                    Assert.AreSequenceEqual(expectedArrayLeaveStay, content.Children.OfType<StackPanel>().Single().Children.OfType<Button>().Select(button => button.Content));
                }
                finally
                {
                    dialog.Close();
                }
            }
        }

        private static readonly string[] expectedArrayLeaveStay = new[] { "Leave", "Stay" };

        private sealed class LocalizationCatalogScope : IDisposable
        {
            private readonly CatalogDomainPattern previousPatternType;
            private readonly string previousPattern;
            private readonly string previousFolder;
            private readonly ICatalog previousMarkupCatalog;

            public LocalizationCatalogScope()
            {
                previousPatternType = (CatalogDomainPattern)typeof(CatalogManager).GetField("catalogDomainPattern", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                previousPattern = (string)typeof(CatalogManager).GetField("pattern", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                previousFolder = (string)typeof(CatalogManager).GetField("folder", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                previousMarkupCatalog = GettextExtension.DefaultCatalog;
                CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.Static, "ToolboxLocalizationRegression");
                Catalog = CatalogManager.Catalog;
                GettextExtension.DefaultCatalog = Catalog;
            }

            public Catalog Catalog { get; }

            public void Dispose()
            {
                GettextExtension.DefaultCatalog = previousMarkupCatalog;
                CatalogManager.SetCatalogDomainPattern(previousPatternType, previousPattern, previousFolder);
            }
        }
    }
}
