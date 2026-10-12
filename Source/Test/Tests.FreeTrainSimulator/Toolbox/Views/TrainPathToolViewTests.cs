using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Views
{
    [STATestClass]
    [DoNotParallelize]
    public class TrainPathToolViewTests
    {
        [TestMethod]
        [Timeout(15000)]
        [DataRow(false, Visibility.Collapsed)]
        [DataRow(true, Visibility.Visible)]
        public void WhenRepairModeBindingIsUnavailableThenTheBannerDoesNotFlash(bool repairMode, Visibility expected)
        {
            Window window = null;
            try
            {
                TrainPathToolView view = new();
                window = new Window { Content = view, Width = 520, Height = 520, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);
                TextBlock banner = Descendants(view).OfType<TextBlock>().Single(element => element.Text.StartsWith("Repair mode:", StringComparison.Ordinal));
                Assert.AreEqual(Visibility.Collapsed, banner.Visibility);
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge,
                    TrainPathSnapshot.Empty with { IsRepairMode = repairMode });
                using (ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new(bridge, scheduler))
                    {
                        viewModel.Start();
                        view.DataContext = viewModel;
                        SettleLayout(window);

                        Assert.AreEqual(expected, banner.Visibility);

                        view.DataContext = null;
                        SettleLayout(window);

                        Assert.AreEqual(Visibility.Collapsed, banner.Visibility);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        public void WhenMetadataIsEditedAgainAfterEnterThenLeavingTheEditorCommitsTheLatestText()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new(bridge, scheduler))
                    {
                        TrainPathToolView view = new() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        window.UpdateLayout();
                        TextBox editor = Descendants(view).OfType<TextBox>().Single(element => AutomationProperties.GetName(element) == "Path name");
                        TextBox nextEditor = Descendants(view).OfType<TextBox>().Single(element => AutomationProperties.GetName(element) == "Path start");
                        Assert.IsTrue(editor.Focus());
                        editor.Text = "First name";
                        editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), Environment.TickCount, Key.Enter)
                        {
                            RoutedEvent = Keyboard.PreviewKeyDownEvent,
                        });
                        Assert.AreEqual("First name", viewModel.PathName);
                        Assert.IsTrue(editor.IsKeyboardFocusWithin);

                        editor.Text = "Latest name";
                        Assert.IsTrue(nextEditor.Focus());

                        Assert.AreEqual("Latest name", viewModel.PathName);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        [DataRow(220)]
        [DataRow(520)]
        public void WhenPathHeaderFilterIsUsedThenTheLowerRowRetainsSearchUntilClearedAndToggled(int width)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new(bridge, scheduler))
                    {
                        typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge,
                            TrainPathSnapshot.Empty with
                            {
                                Paths = [
                                    new TrainPathListRow("path", "Morning service", global::FreeTrainSimulator.Models.Content.PathValidationState.NotValidated),
                                    new TrainPathListRow("other", "Evening service", global::FreeTrainSimulator.Models.Content.PathValidationState.NotValidated)],
                                SelectedPathId = "path",
                            });
                        viewModel.Start();
                        TrainPathListItemViewModel path = viewModel.Paths.Single(item => item.Id == "path");
                        TrainPathListItemViewModel otherPath = viewModel.Paths.Single(item => item.Id == "other");
                        TrainPathToolView view = new() { DataContext = viewModel };
                        window = new Window { Content = view, Width = width, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ColumnFilterHeader header = Descendants(view).OfType<ColumnFilterHeader>().Single();
                        TextBox filter = Descendants(header).OfType<TextBox>().Single();
                        Button lens = Descendants(header).OfType<Button>().Single(element => AutomationProperties.GetName(element) == "Toggle column filter");
                        Button clear = Descendants(header).OfType<Button>().Single(element => AutomationProperties.GetName(element) == "Clear column filter");
                        TextBlock caption = Descendants(header).OfType<TextBlock>().Single(element => element.Text == "Name");
                        Assert.IsFalse(filter.IsVisible);
                        InvokeButton(lens, window);
                        Assert.IsTrue(filter.IsVisible);
                        Assert.IsTrue(filter.IsKeyboardFocusWithin);
                        Assert.IsTrue(filter.TranslatePoint(new Point(), header).Y >= caption.TranslatePoint(new Point(0, caption.ActualHeight), header).Y);
                        Assert.IsTrue(caption.IsVisible);

                        filter.Text = "mOrNiNg";
                        SettleLayout(window);
                        Assert.AreEqual("mOrNiNg", viewModel.SearchText);
                        Assert.IsTrue(path.IsVisible);
                        Assert.IsFalse(otherPath.IsVisible);
                        TextBox metadata = Descendants(view).OfType<TextBox>().Single(element => AutomationProperties.GetName(element) == "Path name");
                        Assert.IsTrue(metadata.Focus());
                        Assert.IsTrue(filter.IsVisible);
                        Assert.AreEqual("Name", caption.Text);
                        InvokeButton(lens, window);
                        Assert.IsTrue(filter.IsVisible);
                        viewModel.Start();
                        view.DataContext = null;
                        SettleLayout(window);
                        view.DataContext = viewModel;
                        window.Width = 180;
                        SettleLayout(window);
                        window.Width = width;
                        SettleLayout(window);

                        Assert.AreEqual("mOrNiNg", filter.Text);
                        Assert.IsTrue(filter.IsVisible);
                        Assert.AreEqual("Name", caption.Text);
                        Assert.AreSame(path, viewModel.SelectedPath);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Paths));
                        Assert.AreSame(path, list.SelectedItem);
                        ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(path);
                        Assert.AreEqual(FontWeights.Bold, row.FontWeight);
                        ListViewItem otherRow = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(otherPath);
                        Assert.AreEqual(Visibility.Collapsed, otherRow.Visibility);

                        InvokeButton(clear, window);
                        Assert.AreEqual(string.Empty, viewModel.SearchText);
                        Assert.IsTrue(otherPath.IsVisible);
                        Assert.AreEqual(Visibility.Visible, otherRow.Visibility);
                        Assert.IsTrue(filter.IsVisible);
                        InvokeButton(lens, window);
                        Assert.IsFalse(filter.IsVisible);
                        Assert.AreEqual("Name", caption.Text);
                        Assert.IsTrue(caption.IsVisible);
                        Assert.AreSame(path, viewModel.SelectedPath);
                        Assert.AreSame(path, list.SelectedItem);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        public void WhenAColumnIsNotSearchableThenItReservesTheSharedFilterRowWithoutAnEditor()
        {
            Window window = null;
            try
            {
                ColumnFilterHeader searchable = new() { Caption = "Name", IsSearchable = true, IsFilterRowVisible = true };
                ColumnFilterHeader reserved = new() { Caption = "Status", IsSearchable = false, IsFilterRowVisible = true };
                StackPanel panel = new();
                panel.Children.Add(searchable);
                panel.Children.Add(reserved);
                window = new Window { Content = panel, Width = 220, Height = 160, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);

                Assert.AreEqual(searchable.ActualHeight, reserved.ActualHeight);
                Assert.IsFalse(Descendants(reserved).OfType<TextBox>().Single().IsVisible);
                Assert.IsFalse(Descendants(reserved).OfType<Button>().Any(button => button.IsVisible));
                double expandedHeight = reserved.ActualHeight;
                reserved.IsFilterRowVisible = false;
                SettleLayout(window);
                Assert.IsTrue(reserved.ActualHeight < expandedHeight);
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        public void WhenAnUnselectedPathHasUnsavedChangesThenOnlyTheAsteriskMarksIt()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new(bridge, scheduler))
                    {
                        TrainPathListItemViewModel path = new("dirty", "Unsaved path",
                            global::FreeTrainSimulator.Models.Content.PathValidationState.NotValidated, true);
                        viewModel.Paths.Add(path);
                        TrainPathToolView view = new() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Paths));
                        ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(path);
                        TextBlock name = Descendants(row).OfType<TextBlock>().Single(element => element.Text == "Unsaved path");
                        TextBlock marker = Descendants(row).OfType<TextBlock>().Single(element => element.Text == " *");

                        Assert.AreEqual(FontWeights.Normal, name.FontWeight);
                        Assert.IsTrue(marker.IsVisible);

                        viewModel.UserSelectPath(path);
                        SettleLayout(window);

                        Assert.AreEqual(FontWeights.Bold, name.FontWeight);
                        Assert.IsTrue(marker.IsVisible);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        [DataRow(true, "✓", "Valid path node")]
        [DataRow(false, "⚠", "Invalid path node")]
        public void WhenPathNodesAreDisplayedThenValidityHasItsOwnColumn(bool valid, string expectedSymbol, string expectedName)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new(bridge, scheduler))
                    {
                        viewModel.Nodes.Add(new TrainPathNodeItemViewModel(new TrainPathNodeRow(0,
                            global::FreeTrainSimulator.Common.PathNodeType.Via, valid, 1, -1, -1, null, valid ? null : "Off track")));
                        viewModel.SelectedTabIndex = TrainPathToolWindowViewModel.EditorTabIndex;
                        TrainPathToolView view = new() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 320, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Nodes));
                        GridView grid = (GridView)list.View;
                        ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                        TextBlock status = Descendants(row).OfType<TextBlock>().Single(element => AutomationProperties.GetName(element) == expectedName);

                        Assert.AreEqual(3, grid.Columns.Count);
                        Assert.AreEqual("Type", grid.Columns[2].Header);
                        Assert.AreEqual(expectedSymbol, status.Text);
                        Assert.IsTrue(status.IsVisible);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        [DataRow(320)]
        [DataRow(520)]
        public void WhenIssuesAreExpandedThenTheListAndSelectedMessageRemainVisible(int width)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new(bridge, scheduler))
                    {
                        TrainPathDiagnosticItemViewModel issue = new(new TrainPathDiagnosticRow(
                            global::FreeTrainSimulator.Runtime.Track.PathRouteDiagnosticSeverity.Error,
                            global::FreeTrainSimulator.Runtime.Track.PathRouteDiagnosticCode.AnchorNotOnTrack,
                            "The selected node is off track.", 0, -1, -1, "Move the node onto track.", true));
                        viewModel.Diagnostics.Add(issue);
                        viewModel.SelectedTabIndex = TrainPathToolWindowViewModel.EditorTabIndex;
                        viewModel.IsIssuesExpanded = true;
                        TrainPathToolView view = new() { DataContext = viewModel };
                        window = new Window { Content = view, Width = width, Height = 900, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        Expander details = Descendants(view).OfType<Expander>().Single(element => Equals(element.Header, "Details"));
                        details.IsExpanded = false;
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.VisibleDiagnostics));
                        TextBlock message = Descendants(view).OfType<TextBlock>().Single(element => element.Text == "Message");
                        Assert.IsFalse(message.IsVisible);

                        list.SelectedItem = issue;
                        SettleLayout(window);

                        Assert.AreSame(issue, viewModel.SelectedDiagnostic);
                        Assert.IsTrue(list.ActualHeight >= 40);
                        Assert.IsTrue(message.IsVisible);
                        Assert.IsTrue(message.TranslatePoint(new Point(), view).Y >= list.TranslatePoint(new Point(0, list.ActualHeight), view).Y);
                        Assert.IsTrue(Descendants(view).OfType<TextBlock>().Any(element => element.Text == issue.Message && element.IsVisible));
                        Assert.IsTrue(Descendants(view).OfType<Button>().Any(element => Equals(element.Content, "Repair node") && element.IsVisible));
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000)]
        public void WhenNodeRowsAreSelectedScrolledAndRefreshedThenRenderedIndexesMatchTheirPositions()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using ToolWindowRefreshScheduler scheduler = new(Dispatcher.CurrentDispatcher);
                using TrainPathToolWindowViewModel viewModel = new(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex);
                TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
                {
                    Nodes = System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(0, 112).Select(index =>
                        new TrainPathNodeRow(index, global::FreeTrainSimulator.Common.PathNodeType.Via, true, 10, index + 1, -1, null, null))),
                };
                typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, snapshot);
                viewModel.Start();
                TrainPathToolView view = new() { DataContext = viewModel };
                window = new Window { Content = view, Width = 320, Height = 520, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);
                ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Nodes));

                foreach (int selectedIndex in new[] { 10, 100, 101, 109, 110, 111, 51, 0 })
                {
                    list.ScrollIntoView(viewModel.Nodes[selectedIndex]);
                    SettleLayout(window);
                    list.SelectedItem = viewModel.Nodes[selectedIndex];
                    snapshot = snapshot with { SelectedNodeIndex = selectedIndex };
                    typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, snapshot);
                    viewModel.Start();
                    window.Width = selectedIndex % 2 == 0 ? 520 : 320;
                    SettleLayout(window);

                    CollectionAssert.AreEqual(Enumerable.Range(0, 112).ToArray(), viewModel.Nodes.Select(node => node.Index).ToArray());
                    foreach (ListViewItem row in Descendants(list).OfType<ListViewItem>())
                    {
                        TrainPathNodeItemViewModel node = (TrainPathNodeItemViewModel)row.Content;
                        TextBlock indexCell = Descendants(row).OfType<TextBlock>().Single(text => text.Name == "NodeIndexText");
                        Assert.AreEqual(node.Index.ToString(System.Globalization.CultureInfo.InvariantCulture), indexCell.Text,
                            $"Rendered row {node.Index} after selecting {selectedIndex}");
                        Assert.AreEqual(TextTrimming.CharacterEllipsis, indexCell.TextTrimming);
                        Assert.AreEqual(node.Index, indexCell.ToolTip);
                        TextBlock measured = new()
                        {
                            Text = indexCell.Text,
                            FontFamily = indexCell.FontFamily,
                            FontSize = indexCell.FontSize,
                            FontWeight = indexCell.FontWeight,
                        };
                        measured.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Assert.IsTrue(indexCell.ActualWidth + 0.5 >= measured.DesiredSize.Width,
                            $"Index {node.Index} needs {measured.DesiredSize.Width} units but has {indexCell.ActualWidth}.");
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        private static void InvokeButton(Button button, Window window)
        {
            IInvokeProvider provider = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
            provider.Invoke();
            SettleLayout(window);
        }

        private static void SettleLayout(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (DependencyObject descendant in Descendants(child))
                    yield return descendant;
            }
        }
    }
}
