using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Graphics.MapView;
using Tests.FreeTrainSimulator.Simulator;

using PathChartStationVisit = FreeTrainSimulator.Runtime.Track.PathChartStationVisit;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenChartInspectsCommittedUnsavedMainThenProfileIsCachedAndHistoryIsUnchanged()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, _) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                PathChartToolWindow chart = null;
                PathChartSnapshot publication = null;
                PathModel source = null;
                PathModel[] history = null;
                int ownerReads = 0;
                ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.IsTrue(editor.SetMetadataCommand("Unsaved chart source", saved.Start, saved.End, saved.PlayerPath).Success);
                    source = editor.TryCaptureCurrentPathModel();
                    history = SavePreflightHistory(editor, "undoHistory");
                    chart = new PathChartToolWindow(() =>
                    {
                        Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                        ownerReads++;
                        return scope.Game.HostedPathEditor;
                    }, () => scope.Game.HostedTrainPathToolingContext, commands.Enqueue) { Active = true };
                    chart.RefreshSnapshot();
                    publication = chart.CaptureSnapshot();
                    Assert.IsTrue(publication.Profile.IsAvailable, publication.Profile.UnavailableReason);
                    Assert.AreEqual(200d, publication.Profile.Length, 0.001);
                    Assert.AreSame(source, publication.Source);
                    chart.RefreshSnapshot();
                    Assert.AreSame(publication, chart.CaptureSnapshot());
                    chart.Active = false;
                    int beforeHiddenRefresh = ownerReads;
                    chart.RefreshSnapshot();
                    Assert.AreEqual(beforeHiddenRefresh, ownerReads);
                }).ConfigureAwait(false);

                StaControlHost shell = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (shell.ConfigureAwait(false))
                {
                    await shell.OnOwnerAsync(() =>
                    {
                        using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                        {
                            using (PathChartToolWindowViewModel viewModel = new PathChartToolWindowViewModel(chart, scheduler))
                            {
                                viewModel.Start();
                                Assert.AreSame(publication.Profile, viewModel.Profile);
                                Assert.AreEqual(string.Join(Environment.NewLine, publication.Profile.Diagnostics), viewModel.StatusMessage,
                                    "Only actual profile warnings belong in the status area; the general interpretation notice belongs in the hint.");
                                viewModel.CursorDistance = 100;
                                Assert.AreSame(publication.Profile, viewModel.Profile);
                                Assert.IsFalse(string.IsNullOrEmpty(viewModel.SelectedValuesText));
                                viewModel.RefreshLocalization();
                                Assert.AreSame(publication.Profile, viewModel.Profile);
                                Assert.IsEmpty(commands);
                                viewModel.Stop();
                                Assert.IsFalse(chart.Active);
                            }
                        }
                    }).ConfigureAwait(false);
                }

                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.AreSequenceEqual(history, SavePreflightHistory(editor, "undoHistory"));
                    Assert.AreSame(publication.Profile, chart.CaptureSnapshot().Profile);
                    chart.UpdateContext(null);
                }).ConfigureAwait(false);
            }
        }

        private static readonly string[] expected = new[] { "Freiburg-Hauptbahnhof", "Freiburg-Hauptbahnhof", "Freiburg-Hauptbahnhof", "Freiburg-Wiehre", "Freiburg-Wiehre", "Freiburg-Wiehre", "Freiburg-Wiehre", "Freiburg-Hauptbahnhof" };
        private static readonly string[] expectedArray = new[] { "Freiburg-Hauptbahnhof", "Freiburg-Hauptbahnhof", "Freiburg-Hauptbahnhof" };
        private static readonly string[] expectedArrayABAB = new[] { "A", "B", "A", "B" };

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenChartStationRunsExpandThenHeadersAreNameOnlyAndAllEncountersRemainSelectable(bool visitSecondStation)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = fixture.Track.TrackDatabase with
            {
                TrackItems = ImmutableArray.Create<TrackItemBase>(
                    new PlatformTrackItem(SyntheticPathFixture.Location(10, 0))
                    { TrackItemIndex = 0, NodeIndex = 4, LinkedPlatformItem = 1, StationName = "Freiburg-Hauptbahnhof", SectionDistance = 10 },
                    new PlatformTrackItem(SyntheticPathFixture.Location(20, 0))
                    { TrackItemIndex = 1, NodeIndex = 4, LinkedPlatformItem = 0, StationName = "Freiburg-Hauptbahnhof", SectionDistance = 20 },
                    new PlatformTrackItem(SyntheticPathFixture.Location(60, 0))
                    { TrackItemIndex = 2, NodeIndex = 4, LinkedPlatformItem = 3, StationName = "Freiburg-Wiehre", SectionDistance = 60 },
                    new PlatformTrackItem(SyntheticPathFixture.Location(70, 0))
                    { TrackItemIndex = 3, NodeIndex = 4, LinkedPlatformItem = 2, StationName = "Freiburg-Wiehre", SectionDistance = 70 }),
                TrackItemSelectors = ImmutableDictionary<int, TrackItemIndex>.Empty.Add(4,
                    new TrackItemIndex { TrackItems = ImmutableArray.Create(0, 1, 2, 3) }),
            };
            _ = await RouteModelHandler.ToFile(fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(fixture.Track with { TrackDatabase = database }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel { Id = fixture.Route.Id, Tags = ImmutableDictionary<string, string>.Empty },
                fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
            Assert.HasCount(4, runtime.TrackWorld.TrackDatabase.TrackItems);
            float[] positions = visitSecondStation ? [5, 30, 5, 80, 50, 80, 5] : [5, 30, 5, 30];
            PathModel path = new PathModel
            {
                Id = "StationShunting" + Guid.NewGuid().ToString("N"),
                Name = "Station shunting",
                PathNodes = positions.Select((position, index) => SyntheticPathFixture.Node(
                    index == 0 ? PathNodeType.Start : index == positions.Length - 1 ? PathNodeType.End : PathNodeType.Via | PathNodeType.Reversal,
                    4, position, 0, index == positions.Length - 1 ? -1 : index + 1, -1)).ToImmutableArray(),
            };
            path.Initialize(fixture.Route);
            _ = await PathModelHandler.ToFile(path, TestContext.CancellationToken).ConfigureAwait(false);

            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, _) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [path]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(path)).ConfigureAwait(false));
                PathChartToolWindow bridge = null;
                PathChartSnapshot publication = null;
                PathModel source = null;
                PathModel[] history = null;
                ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
                int ownerReads = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.IsTrue(editor.SetMetadataCommand("Unsaved station chart", path.Start, path.End, path.PlayerPath).Success);
                    source = editor.TryCaptureCurrentPathModel();
                    history = SavePreflightHistory(editor, "undoHistory");
                    bridge = new PathChartToolWindow(() =>
                    {
                        ownerReads++;
                        return scope.Game.HostedPathEditor;
                    }, () => scope.Game.HostedTrainPathToolingContext, commands.Enqueue) { Active = true };
                    bridge.RefreshSnapshot();
                    publication = bridge.CaptureSnapshot();
                    Assert.IsTrue(publication.Profile.IsAvailable, publication.Profile.UnavailableReason);
                    Assert.AreSequenceEqual(visitSecondStation
                        ? expected : expectedArray,
                        publication.Profile.Stations.Select(station => station.StationName));
                }).ConfigureAwait(false);

                StaControlHost shell = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (shell.ConfigureAwait(false))
                {
                    await shell.OnOwnerAsync(() =>
                    {
                        using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                        {
                            using (PathChartToolWindowViewModel viewModel = new PathChartToolWindowViewModel(bridge, scheduler))
                            {
                                viewModel.Start();
                                PathChartToolView view = new PathChartToolView { DataContext = viewModel };
                                Window window = new Window { Content = view, Width = 1000, Height = 450, ShowInTaskbar = false };
                                try
                                {
                                    window.Show();
                                    window.UpdateLayout();
                                    ListView stations = (ListView)view.FindName("StationList");
                                    PathChartControl chart = (PathChartControl)view.FindName("Chart");
                                    int[] firstOccurrences = visitSecondStation ? [0, 3, 7] : [0];
                                    PathChartStationItemViewModel[] groups = viewModel.Stations.ToArray();
                                    Assert.AreSequenceEqual(firstOccurrences.Select(index => publication.Profile.Stations[index]), groups.Select(row => row.Source));
                                    Assert.AreEqual(firstOccurrences.Length, stations.Items.Count);
                                    Assert.IsNull(view.FindName("FullStationListToggle"));
                                    PathChartStationItemViewModel firstGroup = groups[0];
                                    Assert.AreSame(firstGroup, stations.SelectedItem, "A new chart initially marks the station nearest its zero-distance cursor.");
                                    Assert.AreEqual(0, chart.CursorDistance, "Initial station marking must not snap the marker forward.");
                                    Assert.IsTrue(firstGroup.CanExpand);
                                    Assert.IsFalse(firstGroup.IsExpanded);
                                    Assert.IsFalse(string.IsNullOrEmpty(firstGroup.DistanceText));
                                    Assert.AreEqual(publication.Profile.Stations[0].Distance, firstGroup.Distance);
                                    Assert.AreSequenceEqual(publication.Profile.Stations.Take(3), firstGroup.Encounters.Select(row => row.Source));

                                    Grid CreateStationCell(PathChartStationItemViewModel row)
                                    {
                                        Grid cell = (Grid)((GridView)stations.View).Columns[0].CellTemplate.LoadContent();
                                        cell.DataContext = row;
                                        cell.Measure(new Size(135, double.PositiveInfinity));
                                        cell.Arrange(new Rect(0, 0, 135, cell.DesiredSize.Height));
                                        cell.UpdateLayout();
                                        return cell;
                                    }

                                    Grid firstCell = CreateStationCell(firstGroup);
                                    Expander expander = firstCell.Children.OfType<Expander>().Single();
                                    IExpandCollapseProvider disclosure = (IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(expander)
                                        .GetPattern(PatternInterface.ExpandCollapse);
                                    Assert.AreEqual(ExpandCollapseState.Collapsed, disclosure.ExpandCollapseState);
                                    Assert.AreEqual(firstGroup.StationName, AutomationProperties.GetName(expander));
                                    stations.SetCurrentValue(ListView.SelectedItemProperty, firstGroup);
                                    double firstDistance = viewModel.CursorDistance;
                                    disclosure.Expand();
                                    Assert.IsTrue(firstGroup.IsExpanded);
                                    Assert.IsTrue(firstGroup.IsGroupingAnchor);
                                    Assert.AreEqual(string.Empty, firstGroup.DistanceText);
                                    Assert.AreSame(firstGroup.Encounters[0], viewModel.SelectedStation, "Expansion preserves the selected physical first visit underneath its name-only anchor.");
                                    Assert.AreEqual(firstDistance, viewModel.CursorDistance);
                                    Assert.AreSame(firstGroup, viewModel.Stations[0]);
                                    Assert.AreSequenceEqual(firstGroup.Encounters, viewModel.Stations.Skip(1).Take(3));
                                    Assert.AreEqual(groups.Length + 3, stations.Items.Count);
                                    Assert.IsTrue(firstGroup.Encounters.All(row => row.IsIndented && !row.CanExpand && !string.IsNullOrEmpty(row.DistanceText)));
                                    Assert.AreEqual(Visibility.Collapsed, CreateStationCell(firstGroup.Encounters[0]).Children.OfType<Expander>().Single().Visibility);
                                    disclosure.Collapse();
                                    Assert.AreSame(firstGroup, viewModel.SelectedStation);
                                    Assert.AreEqual(firstDistance, viewModel.CursorDistance);
                                    Assert.IsFalse(string.IsNullOrEmpty(firstGroup.DistanceText));

                                    disclosure.Expand();
                                    stations.SetCurrentValue(ListView.SelectedItemProperty, firstGroup.Encounters[1]);
                                    double inspectedDistance = viewModel.CursorDistance;
                                    Assert.AreEqual(publication.Profile.Stations[1].Distance, inspectedDistance);
                                    disclosure.Collapse();
                                    Assert.IsNull(viewModel.SelectedStation, "Collapsing a later visit must not select the first occurrence instead.");
                                    Assert.AreEqual(inspectedDistance, viewModel.CursorDistance);
                                    Assert.AreSequenceEqual(groups, viewModel.Stations);
                                    disclosure.Expand();
                                    stations.SetCurrentValue(ListView.SelectedItemProperty, firstGroup);
                                    Assert.AreEqual(inspectedDistance, viewModel.CursorDistance, "A name-only anchor is not a new chart position.");
                                    Assert.IsFalse(viewModel.ShowOnMapCommand.CanExecute(null));
                                    viewModel.ShowOnMapCommand.Execute(null);
                                    Assert.IsEmpty(commands, "An expanded grouping anchor must never navigate to its stored first visit.");
                                    disclosure.Collapse();
                                    Assert.IsNull(viewModel.SelectedStation);
                                    Assert.AreEqual(inspectedDistance, viewModel.CursorDistance);

                                    stations.SetCurrentValue(ListView.SelectedItemProperty, firstGroup);
                                    KeyEventArgs expandKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(stations), Environment.TickCount, Key.Right) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                                    stations.RaiseEvent(expandKey);
                                    Assert.IsTrue(expandKey.Handled);
                                    Assert.IsTrue(firstGroup.IsExpanded);
                                    Assert.AreSame(firstGroup.Encounters[0], viewModel.SelectedStation);
                                    KeyEventArgs collapseKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(stations), Environment.TickCount, Key.Left) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                                    stations.RaiseEvent(collapseKey);
                                    Assert.IsTrue(collapseKey.Handled);
                                    Assert.IsFalse(firstGroup.IsExpanded);
                                    Assert.AreSame(firstGroup, viewModel.SelectedStation);
                                    disclosure.Expand();

                                    if (visitSecondStation)
                                    {
                                        PathChartStationItemViewModel secondGroup = groups[1];
                                        Assert.IsFalse(secondGroup.IsExpanded, "Expanding one station run must not expand the next.");
                                        Expander secondExpander = CreateStationCell(secondGroup).Children.OfType<Expander>().Single();
                                        ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(secondExpander).GetPattern(PatternInterface.ExpandCollapse)).Expand();
                                        Assert.IsTrue(firstGroup.IsExpanded);
                                        Assert.IsTrue(secondGroup.IsExpanded);
                                        Assert.AreEqual(string.Empty, secondGroup.DistanceText);
                                        Assert.AreSequenceEqual(publication.Profile.Stations, viewModel.Stations.Where(row => !row.IsGroupingAnchor).Select(row => row.Source));
                                        Assert.AreEqual(publication.Profile.Stations.Length + 2, stations.Items.Count);
                                        Assert.IsFalse(groups[2].CanExpand, "The later return is a separate single encounter, not part of the departure run.");
                                        Assert.AreEqual(groups[0].StationName, groups[2].StationName);
                                        Assert.AreEqual(publication.Profile.Stations[7].Distance, groups[2].Distance);
                                        Assert.AreEqual(Visibility.Collapsed, CreateStationCell(groups[2]).Children.OfType<Expander>().Single().Visibility);
                                    }

                                    foreach (PathChartStationVisit visit in publication.Profile.Stations)
                                    {
                                        double marker = visit.Distance + 0.2;
                                        chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, marker);
                                        Assert.AreEqual(marker, viewModel.CursorDistance, 0.001, "Chart-to-list synchronization must not snap the marker to the station midpoint.");
                                        Assert.AreSame(visit, viewModel.SelectedStation.Source);
                                        Assert.AreSame(viewModel.SelectedStation, stations.SelectedItem);
                                        Assert.AreEqual(viewModel.SelectedValuesText, ((TextBlock)((ScrollViewer)view.FindName("SelectedValuesScroll")).Content).Text,
                                            "The header's live values update through the real cursor/list bindings.");
                                        Assert.IsFalse(viewModel.SelectedStation.IsGroupingAnchor);
                                    }
                                    double halfway = (publication.Profile.Stations[0].Distance + publication.Profile.Stations[1].Distance) / 2;
                                    chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, halfway);
                                    Assert.AreSame(publication.Profile.Stations[0], viewModel.SelectedStation.Source, "An equidistant marker deterministically marks the earlier encounter.");
                                    chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, halfway + 0.1);
                                    Assert.AreSame(publication.Profile.Stations[1], viewModel.SelectedStation.Source);
                                    firstGroup.IsExpanded = false;
                                    double foldedMarker = publication.Profile.Stations[1].Distance + 0.3;
                                    chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, foldedMarker);
                                    Assert.AreSame(firstGroup, stations.SelectedItem, "A folded run represents its later nearest encounter without expanding automatically.");
                                    Assert.IsFalse(firstGroup.IsExpanded);
                                    Assert.AreEqual(foldedMarker, chart.CursorDistance, 0.001);
                                    Assert.AreEqual(publication.Profile.Stations[0].Distance, firstGroup.Distance, "Folding retains the first encounter's distance, even when a later visit is marked.");
                                    firstGroup.IsExpanded = true;
                                    Assert.AreSame(firstGroup.Encounters[1], stations.SelectedItem, "Opening a mouse-marked folded run reveals the actual marked encounter, not its first visit.");
                                    Assert.AreEqual(foldedMarker, chart.CursorDistance, 0.001);

                                    stations.SetCurrentValue(ListView.SelectedItemProperty, firstGroup.Encounters[2]);
                                    Assert.AreEqual(publication.Profile.Stations[2].Distance, chart.CursorDistance, 0.001, "List selection moves the graph marker to the exact physical encounter.");
                                    chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, chart.CursorDistance + 0.2);
                                    window.UpdateLayout();
                                    ListViewItem selectedRow = (ListViewItem)stations.ItemContainerGenerator.ContainerFromItem(stations.SelectedItem);
                                    Assert.IsNotNull(selectedRow, "A chart-marked station is revealed in the list without moving keyboard focus.");
                                    stations.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = selectedRow });
                                    Assert.AreEqual(publication.Profile.Stations[2].Distance, chart.CursorDistance, 0.001, "Clicking an already mouse-marked row still moves the marker to its exact distance.");

                                    chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, publication.Profile.Length / 2);
                                    chart.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                                    { RoutedEvent = Mouse.PreviewMouseDownEvent });
                                    chart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                                    { RoutedEvent = Mouse.PreviewMouseMoveEvent });
                                    PathChartStationVisit nearest = publication.Profile.Stations.OrderBy(visit => Math.Abs(visit.Distance - chart.CursorDistance)).ThenBy(visit => visit.Distance).First();
                                    Assert.AreSame(nearest, viewModel.SelectedStation.Source, "Native pointer inspection updates the same nearest-encounter list selection.");
                                    Assert.AreEqual(chart.CursorDistance, viewModel.CursorDistance, 0.001);
                                    chart.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent });
                                    Assert.IsFalse(chart.IsMouseCaptured);
                                    Assert.IsEmpty(commands, "Marker/list synchronization never requests map movement.");

                                    PathChartStationItemViewModel[] retainedRows = viewModel.Stations.ToArray();
                                    PathChartStationItemViewModel retainedSelection = viewModel.SelectedStation;
                                    CultureInfo previousCulture = CultureInfo.CurrentCulture;
                                    try
                                    {
                                        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                                        viewModel.RefreshLocalization();
                                        Assert.IsTrue(firstGroup.IsExpanded);
                                        Assert.AreEqual(string.Empty, firstGroup.DistanceText);
                                        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, viewModel.UseMetricUnits ? "{0:N2} km" : "{0:N2} mi",
                                            firstGroup.Source.Distance / (viewModel.UseMetricUnits ? 1000 : 1609.344)), firstGroup.Encounters[0].DistanceText);
                                        Assert.AreSequenceEqual(retainedRows, viewModel.Stations);
                                        Assert.AreSame(retainedSelection, viewModel.SelectedStation);
                                    }
                                    finally
                                    {
                                        CultureInfo.CurrentCulture = previousCulture;
                                        viewModel.RefreshLocalization();
                                    }
                                    firstGroup.IsExpanded = true;
                                    Assert.AreSequenceEqual(retainedRows, viewModel.Stations, "Unchanged expansion must reuse existing rows.");
                                    viewModel.Stop();
                                    viewModel.Start();
                                    Assert.AreSequenceEqual(retainedRows, viewModel.Stations, "Hiding and reopening an unchanged chart preserves run expansion.");
                                    Assert.AreSame(publication.Profile, viewModel.Profile);
                                    Assert.AreSame(publication.Profile, chart.Profile);
                                    Assert.AreEqual(1, ownerReads, "Station disclosure must not re-extract owner-thread geometry.");
                                    Assert.IsEmpty(commands, "Disclosure and selection must never request automatic map movement.");
                                }
                                finally
                                {
                                    view.DataContext = null;
                                    window.Close();
                                    viewModel.Stop();
                                }
                            }
                        }
                    }).ConfigureAwait(false);
                }

                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.AreSequenceEqual(history, SavePreflightHistory(editor, "undoHistory"));
                    Assert.AreSame(publication, bridge.CaptureSnapshot());
                    bridge.UpdateContext(null);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenChartStationIntervalsOverlapThenTheBoundCursorFindsTheNearestMidpointWithoutReorderingVisits(bool reverse)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = fixture.Track.TrackDatabase with
            {
                TrackItems = ImmutableArray.Create<TrackItemBase>(
                    new PlatformTrackItem(SyntheticPathFixture.Location(10, 0))
                    { TrackItemIndex = 0, NodeIndex = 4, LinkedPlatformItem = 1, StationName = "A", SectionDistance = 10 },
                    new PlatformTrackItem(SyntheticPathFixture.Location(90, 0))
                    { TrackItemIndex = 1, NodeIndex = 4, LinkedPlatformItem = 0, StationName = "A", SectionDistance = 90 },
                    new PlatformTrackItem(SyntheticPathFixture.Location(20, 0))
                    { TrackItemIndex = 2, NodeIndex = 4, LinkedPlatformItem = 3, StationName = "B", SectionDistance = 20 },
                    new PlatformTrackItem(SyntheticPathFixture.Location(30, 0))
                    { TrackItemIndex = 3, NodeIndex = 4, LinkedPlatformItem = 2, StationName = "B", SectionDistance = 30 }),
                TrackItemSelectors = ImmutableDictionary<int, TrackItemIndex>.Empty.Add(4,
                    new TrackItemIndex { TrackItems = ImmutableArray.Create(0, 1, 2, 3) }),
            };
            _ = await RouteModelHandler.ToFile(fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(fixture.Track with { TrackDatabase = database }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel { Id = fixture.Route.Id, Tags = ImmutableDictionary<string, string>.Empty },
                fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
            float[] positions = reverse ? [95, 5, 95] : [5, 95, 5];
            PathModel path = new PathModel
            {
                Id = "OverlappingStations" + Guid.NewGuid().ToString("N"),
                Name = "Overlapping stations",
                PathNodes = positions.Select((position, index) => SyntheticPathFixture.Node(
                    index == 0 ? PathNodeType.Start : index == 2 ? PathNodeType.End : PathNodeType.Via | PathNodeType.Reversal,
                    4, position, 0, index == 2 ? -1 : index + 1, -1)).ToImmutableArray(),
            };
            path.Initialize(fixture.Route);
            _ = await PathModelHandler.ToFile(path, TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, _) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [path]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(path)).ConfigureAwait(false));
                PathChartToolWindow bridge = null;
                PathChartSnapshot publication = null;
                PathModel source = null;
                PathModel[] history = null;
                ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
                int ownerReads = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    source = editor.TryCaptureCurrentPathModel();
                    history = SavePreflightHistory(editor, "undoHistory");
                    bridge = new PathChartToolWindow(() =>
                    {
                        ownerReads++;
                        return scope.Game.HostedPathEditor;
                    }, () => scope.Game.HostedTrainPathToolingContext, commands.Enqueue) { Active = true };
                    bridge.RefreshSnapshot();
                    publication = bridge.CaptureSnapshot();
                    Assert.IsTrue(publication.Profile.IsAvailable, publication.Profile.UnavailableReason);
                    Assert.HasCount(4, publication.Profile.Stations);
                    Assert.AreSequenceEqual(expectedArrayABAB, publication.Profile.Stations.Select(visit => visit.StationName));
                }).ConfigureAwait(false);
                StaControlHost shell = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (shell.ConfigureAwait(false))
                {
                    await shell.OnOwnerAsync(() =>
                    {
                        using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                        using (PathChartToolWindowViewModel viewModel = new PathChartToolWindowViewModel(bridge, scheduler))
                        {
                            viewModel.Start();
                            PathChartToolView view = new PathChartToolView { DataContext = viewModel };
                            Window window = new Window { Content = view, Width = 1000, Height = 450, ShowInTaskbar = false };
                            try
                            {
                                window.Show();
                                window.UpdateLayout();
                                PathChartControl chart = (PathChartControl)view.FindName("Chart");
                                ListView stations = (ListView)view.FindName("StationList");
                                PathChartStationItemViewModel[] rows = viewModel.Stations.ToArray();
                                Assert.AreSequenceEqual(publication.Profile.Stations, rows.Select(row => row.Source));
                                int overlappingIndex = reverse ? 2 : 0;
                                PathChartStationVisit outer = publication.Profile.Stations[overlappingIndex];
                                PathChartStationVisit inner = publication.Profile.Stations[overlappingIndex + 1];
                                Assert.IsTrue(outer.StartDistance < inner.StartDistance && outer.Distance > inner.Distance,
                                    "The production extractor preserves encounter-start order, not midpoint order, for overlapping intervals.");
                                IInputElement focus = Keyboard.FocusedElement;
                                foreach (PathChartStationVisit visit in publication.Profile.Stations)
                                {
                                    double marker = visit.Distance + 0.2;
                                    chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, marker);
                                    window.UpdateLayout();
                                    Assert.AreEqual(marker, viewModel.CursorDistance, 0.001);
                                    Assert.AreSame(visit, viewModel.SelectedStation.Source);
                                    Assert.AreSame(viewModel.SelectedStation, stations.SelectedItem);
                                    Assert.AreSame(focus, Keyboard.FocusedElement);
                                    Assert.AreSequenceEqual(rows, viewModel.Stations);
                                    Assert.AreSame(publication.Profile, chart.Profile);
                                }
                                double halfway = (outer.Distance + inner.Distance) / 2;
                                chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, halfway);
                                Assert.AreSame(inner, viewModel.SelectedStation.Source, "Equal errors still prefer the earlier midpoint, even when it is later in the encounter list.");
                                chart.SetCurrentValue(PathChartControl.CursorDistanceProperty, inner.Distance);
                                Assert.AreSame(inner, viewModel.SelectedStation.Source, "An exact midpoint selects its own station.");
                                stations.SetCurrentValue(ListView.SelectedItemProperty, rows[overlappingIndex]);
                                Assert.AreEqual(outer.Distance, chart.CursorDistance, 0.001, "Explicit list selection still moves the cursor to its exact visit.");
                                viewModel.RefreshLocalization();
                                Assert.AreSequenceEqual(rows, viewModel.Stations);
                                Assert.AreSame(publication.Profile, viewModel.Profile);
                                Assert.AreEqual(1, ownerReads);
                                Assert.IsEmpty(commands, "Inspection must not navigate the map or re-extract geometry.");
                            }
                            finally
                            {
                                view.DataContext = null;
                                window.Close();
                                viewModel.Stop();
                            }
                        }
                    }).ConfigureAwait(false);
                }
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.AreSame(source, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                    Assert.AreSequenceEqual(history, SavePreflightHistory(scope.Session.ExistingPathEditor, "undoHistory"));
                    Assert.AreSame(publication, bridge.CaptureSnapshot());
                    bridge.UpdateContext(null);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("current")]
        [DataRow("edit-undo")]
        [DataRow("path")]
        [DataRow("context")]
        [DataRow("stop")]
        public async Task WhenChartNavigationIsQueuedThenOnlyTheCurrentSourceCanMoveTheMap(string change)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, PathModel alternate) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent initial, _) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved, alternate]).ConfigureAwait(false);
                using (ToolboxInspectionFixture device = await ToolboxInspectionFixture.CreateDefaultAsync().ConfigureAwait(false))
                {
                    using (JourneyMapSession redraw = new JourneyMapSession())
                    {
                        ChartMapDeviceSession composition = new ChartMapDeviceSession(device, redraw);
                        ToolboxContent content = null;
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime), composition, null, null));
                            SetField(scope.Session, "toolboxContent", content);
                            initial.Session.Dispose();
                        }).ConfigureAwait(false);
                        Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));

                        ConcurrentQueue<Action> commands = new ConcurrentQueue<Action>();
                        PathChartToolWindow chart = null;
                        PathChartSnapshot displayed = null;
                        WorldLocation target = WorldLocation.None;
                        PointD beforeNavigation = default;
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            chart = new PathChartToolWindow(() => scope.Game.HostedPathEditor,
                                () => scope.Game.HostedTrainPathToolingContext, commands.Enqueue)
                            { Active = true };
                            chart.UpdateContext(content);
                            chart.RefreshSnapshot();
                            displayed = chart.CaptureSnapshot();
                            Assert.IsTrue(displayed.Profile.IsAvailable, displayed.Profile.UnavailableReason);
                            target = displayed.Profile.Samples[^1].Location;
                            beforeNavigation = device.Camera.State.CenterPoint;
                            Assert.AreNotEqual(beforeNavigation, PointD.FromWorldLocation(target));
                        }).ConfigureAwait(false);

                        chart.ShowOnMap(displayed, target);
                        Assert.HasCount(1, commands);
                        if (change == "path")
                            Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(alternate)).ConfigureAwait(false));

                        ToolboxContent changedContext = null;
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            PathEditor editor = scope.Session.ExistingPathEditor;
                            if (change == "edit-undo")
                            {
                                Assert.IsTrue(editor.SetMetadataCommand("Intervening edit", saved.Start, saved.End, saved.PlayerPath).Success);
                                Assert.IsTrue(editor.Undo());
                                PathModel restored = editor.TryCaptureCurrentPathModel();
                                Assert.AreEqual(displayed.Source.Name, restored.Name);
                                Assert.AreSequenceEqual(displayed.Source.PathNodes.ToArray(), restored.PathNodes.ToArray());
                                Assert.AreNotEqual(displayed.Revision, editor.CompositionRevision);
                            }
                            if (change == "context")
                            {
                                changedContext = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime), composition, null, null));
                                chart.UpdateContext(changedContext);
                            }
                            if (change == "stop")
                                editor.Dispose();

                            PathModel retained = editor.TryCaptureCurrentPathModel();
                            PathModel[] retainedHistory = SavePreflightHistory(editor, "undoHistory");
                            bool retainedDirty = editor.HasUnsavedChanges;
                            beforeNavigation = device.Camera.State.CenterPoint;
                            Assert.IsTrue(commands.TryDequeue(out Action navigate));
                            navigate();
                            Assert.AreSame(retained, editor.TryCaptureCurrentPathModel());
                            Assert.AreEqual(retainedDirty, editor.HasUnsavedChanges);
                            Assert.AreSequenceEqual(retainedHistory, SavePreflightHistory(editor, "undoHistory"));
                            if (change == "current")
                            {
                                Assert.AreEqual(PointD.FromWorldLocation(target), device.Camera.State.CenterPoint);
                                Assert.AreEqual("Map centered on chart position.", chart.CaptureSnapshot().NavigationMessage);
                            }
                            else
                            {
                                Assert.AreEqual(beforeNavigation, device.Camera.State.CenterPoint);
                                Assert.AreEqual("The route or path has changed. Select a position on the current chart and try again.", chart.CaptureSnapshot().NavigationMessage);
                            }
                            chart.Active = false;
                            changedContext?.Session.Dispose();
                        }).ConfigureAwait(false);
                    }
                }
            }
        }

        private sealed class ChartMapDeviceSession : IMapSessionComposer, IXnaMapShellSession
        {
            private readonly ToolboxInspectionFixture device;
            private readonly JourneyMapSession redraw;

            internal ChartMapDeviceSession(ToolboxInspectionFixture device, JourneyMapSession redraw)
            {
                this.device = device;
                this.redraw = redraw;
            }

            public IMapRenderer Renderer => device.Renderer;
            public IMapViewport Viewport => device.Viewport;
            public IMapHostControl HostControl => null;
            public IXnaMapShellHost ShellHost => redraw.ShellHost;
            IMapShellHost IMapShellSession.ShellHost => ShellHost;
            public IMapSession Compose(MapSessionRequest request) => this;
            public void Dispose() { }
        }
    }
}