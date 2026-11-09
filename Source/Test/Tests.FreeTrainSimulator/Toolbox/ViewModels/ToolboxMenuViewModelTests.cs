using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using GetText;
using GetText.Wpf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Toolbox.Hosting;
using Tests.FreeTrainSimulator.Toolbox.Views;

namespace Tests.FreeTrainSimulator.Toolbox.ViewModels
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class ToolboxMenuViewModelTests
    {
        [TestMethod]
        public async Task WhenRouteStatusChangesThenActualViewShowsStatusAndRawErrorsAndClearsOnRecovery()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    CultureInfo previous = CultureInfo.CurrentUICulture;
                    try
                    {
                        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                        HostedToolboxMenu menu = CreateMenu();
                        Guid session = Guid.NewGuid();
                        long revision = 1;
                        menu.Publish(Snapshot(session, revision, "folder", "route", ToolboxMenuStatus.Ready));
                        using (ToolboxMenuViewModel viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher))
                        {
                            RouteToolView view = new RouteToolView { DataContext = viewModel, Foreground = SystemColors.ControlTextBrush };
                            TextBlock status = (TextBlock)view.FindName("RouteStatusMessage");
                            TextBlock error = (TextBlock)view.FindName("RouteErrorDetails");
                            Assert.IsNull(view.FindName("UnloadRouteButton"));
                            (ToolboxMenuStatus Status, string Message)[] transitions =
                            [
                                (ToolboxMenuStatus.Ready, string.Empty),
                                (ToolboxMenuStatus.Confirming, "Waiting for confirmation of unsaved path changes."),
                                (ToolboxMenuStatus.DiscoveringRoutes, "Discovering routes..."),
                                (ToolboxMenuStatus.LoadingRoute, "Loading route..."),
                                (ToolboxMenuStatus.Failed, "Unable to load routes or open the selected route."),
                                (ToolboxMenuStatus.Stopped, "The Toolbox session has stopped."),
                                (ToolboxMenuStatus.Ready, string.Empty),
                            ];
                            const string details = "Synthetic failure in C:\\Content\\Route {0}.\nOriginal user-authored details.";
                            foreach ((ToolboxMenuStatus state, string message) in transitions)
                            {
                                menu.Publish(Snapshot(session, ++revision, "folder", "route", state,
                                    state == ToolboxMenuStatus.Failed ? details : null, true));
                                PumpBindings();
                                Assert.AreEqual(message, status.Text);
                                Assert.AreEqual(message.Length == 0 ? Visibility.Collapsed : Visibility.Visible, status.Visibility);
                                Assert.AreEqual(state == ToolboxMenuStatus.Failed ? details : string.Empty, error.Text);
                                Assert.AreEqual(state == ToolboxMenuStatus.Failed ? Visibility.Visible : Visibility.Collapsed, error.Visibility);
                            }
                            Assert.AreEqual(TextWrapping.Wrap, status.TextWrapping);
                            Assert.AreEqual(TextWrapping.Wrap, error.TextWrapping);
                            Assert.AreSame(view.Foreground, status.Foreground);
                            Assert.AreSame(view.Foreground, error.Foreground);
                            Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
                            Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(error));
                            view.DataContext = null;
                        }
                    }
                    finally
                    {
                        CultureInfo.CurrentUICulture = previous;
                    }
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task WhenUnloadIsUnavailableThenActualCloseRouteMenuItemAndDirectExecutionRespectStatusModalStopAndDisposal()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    HostedToolboxMenu menu = CreateMenu();
                    Guid session = Guid.NewGuid();
                    long revision = 1;
                    menu.Publish(Snapshot(session, revision, "folder", "route", ToolboxMenuStatus.Ready));
                    using (ToolboxMenuViewModel viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher))
                    {
                        Menu mainMenu = ProductionMainMenu.Load();
                        DockPanel content = new DockPanel();
                        content.Children.Add(mainMenu);
                        Window window = new Window
                        {
                            DataContext = new MainWindowViewModel { Menu = viewModel },
                            Content = content,
                            Width = 360,
                            Height = 420,
                            ShowInTaskbar = false,
                        };
                        try
                        {
                            window.Show();
                            MenuItem closeRoute = ProductionMainMenu.FindCloseRouteItem(mainMenu);
                            MenuItem file = (MenuItem)closeRoute.Parent;
                            MenuItemAutomationPeer peer = new MenuItemAutomationPeer(closeRoute);
                            PumpBindings();
                            // WPF defers submenu command admission until its parent opens.
                            file.IsSubmenuOpen = true;
                            PumpBindings();
                            Assert.AreSame(mainMenu.Items[0], file);
                            Assert.AreEqual("_File", file.Header);
                            Assert.AreEqual("_Open Route", ((MenuItem)file.Items[0]).Header);
                            Assert.AreSame(closeRoute, file.Items[1]);
                            Assert.AreEqual("_Close Route", closeRoute.Header);
                            Assert.AreEqual(string.Empty, closeRoute.InputGestureText);
                            Assert.AreEqual(0, closeRoute.InputBindings.Count);
                            Assert.AreEqual(0, mainMenu.InputBindings.Count);
                            Assert.AreSame(viewModel, mainMenu.DataContext);
                            Assert.IsTrue(mainMenu.IsEnabled);
                            Assert.IsTrue(closeRoute.IsEnabled);
                            Assert.IsTrue(closeRoute.Focusable && closeRoute.IsTabStop);
                            Assert.AreSame(viewModel.UnloadRouteCommand, closeRoute.Command);
                            Assert.AreEqual("Close Route", peer.GetName().Replace("_", string.Empty, StringComparison.Ordinal));
                            Assert.AreEqual("Close the current route after confirming any unsaved path changes", closeRoute.ToolTip);
                            Assert.AreEqual(closeRoute.ToolTip, peer.GetHelpText());
                            closeRoute.ToolTip = "Updated close-route help";
                            PumpBindings();
                            Assert.AreEqual(closeRoute.ToolTip, peer.GetHelpText());
                            int notifications = 0;
                            viewModel.UnloadRouteCommand.CanExecuteChanged += (_, _) => ++notifications;
                            foreach (ToolboxMenuStatus state in Enum.GetValues<ToolboxMenuStatus>())
                            {
                                menu.Publish(Snapshot(session, ++revision, "folder", "route", state));
                                PumpBindings();
                                bool ready = state == ToolboxMenuStatus.Ready;
                                Assert.AreEqual(ready, viewModel.UnloadRouteCommand.CanExecute(null), state.ToString());
                                Assert.AreEqual(ready, closeRoute.IsEnabled, state.ToString());
                                if (!ready)
                                    viewModel.UnloadRouteCommand.Execute(null);
                            }
                            menu.Publish(Snapshot(session, ++revision, "folder", "route", ToolboxMenuStatus.Ready, null, false));
                            PumpBindings();
                            Assert.IsFalse(mainMenu.IsEnabled);
                            Assert.IsFalse(closeRoute.IsEnabled);
                            Assert.IsFalse(viewModel.UnloadRouteCommand.CanExecute(null));
                            viewModel.UnloadRouteCommand.Execute(null);
                            menu.Publish(Snapshot(session, ++revision, "folder", null, ToolboxMenuStatus.Ready));
                            PumpBindings();
                            file.IsSubmenuOpen = true;
                            PumpBindings();
                            Assert.IsTrue(mainMenu.IsEnabled);
                            Assert.IsFalse(closeRoute.IsEnabled);
                            Assert.IsFalse(viewModel.UnloadRouteCommand.CanExecute(null));
                            viewModel.UnloadRouteCommand.Execute(null);
                            menu.Publish(Snapshot(session, ++revision, "folder", "route", ToolboxMenuStatus.Ready));
                            PumpBindings();
                            Assert.IsTrue(closeRoute.IsEnabled);
                            Assert.AreEqual((int)revision - 1, notifications, "Each applied snapshot must refresh command admission.");
                            viewModel.Dispose();
                            PumpBindings();
                            Assert.IsFalse(closeRoute.IsEnabled);
                            Assert.IsFalse(viewModel.UnloadRouteCommand.CanExecute(null));
                            Assert.AreEqual((int)revision, notifications);
                            viewModel.UnloadRouteCommand.Execute(null);
                            viewModel.Dispose();
                            Assert.AreEqual((int)revision, notifications, "Repeated disposal must not notify again.");
                            // This menu has no game transport: any disabled direct execution would have thrown.
                        }
                        finally
                        {
                            window.Close();
                            window.DataContext = null;
                        }
                    }
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task WhenLanguageIsRefreshedThenBoundStatusNotifiesWithoutRepublishingOrTranslatingErrorDetails()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    HostedToolboxMenu menu = CreateMenu();
                    menu.Publish(Snapshot(Guid.NewGuid(), 1, "folder", null, ToolboxMenuStatus.Failed, "Raw failure {0}", true));
                    ToolboxMenuSnapshot captured = menu.Current;
                    using (ToolboxMenuViewModel viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher))
                    {
                        RouteToolView view = new RouteToolView { DataContext = viewModel };
                        Menu mainMenu = ProductionMainMenu.Load();
                        MenuItem closeRoute = ProductionMainMenu.FindCloseRouteItem(mainMenu);
                        DockPanel content = new DockPanel { DataContext = new MainWindowViewModel { Menu = viewModel } };
                        content.Children.Add(mainMenu);
                        content.Children.Add(view);
                        PumpBindings();
                        int statusNotifications = 0;
                        int otherNotifications = 0;
                        viewModel.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(ToolboxMenuViewModel.StatusMessage))
                                ++statusNotifications;
                            else
                                ++otherNotifications;
                        };
                        ObjectPropertiesStore store = new ObjectPropertiesStore();
                        Localizer.Localize(content, CatalogManager.Catalog, store);
                        Localizer.Revert(content, store);
                        CatalogManager.Reset();
                        Localizer.Localize(content, CatalogManager.Catalog, store);
                        viewModel.RefreshLocalization();
                        PumpBindings();
                        Assert.AreEqual((1, 0), (statusNotifications, otherNotifications));
                        Assert.AreSame(captured, menu.Current);
                        Assert.AreEqual(viewModel.StatusMessage, ((TextBlock)view.FindName("RouteStatusMessage")).Text);
                        Assert.AreEqual("Raw failure {0}", ((TextBlock)view.FindName("RouteErrorDetails")).Text);
                        MenuItemAutomationPeer peer = new MenuItemAutomationPeer(closeRoute);
                        Assert.AreEqual(((string)closeRoute.Header).Replace("_", string.Empty, StringComparison.Ordinal),
                            peer.GetName().Replace("_", string.Empty, StringComparison.Ordinal));
                        Assert.AreEqual(closeRoute.ToolTip, peer.GetHelpText());
                        Assert.AreSame(viewModel.UnloadRouteCommand, closeRoute.Command);
                        content.DataContext = null;
                        view.DataContext = null;
                    }
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task WhenRouteIsExplicitlyClearedThenActualRouteSelectorAndDescriptionClear()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    HostedToolboxMenu menu = CreateMenu();
                    ToolboxMenuSnapshot initial = Snapshot(Guid.NewGuid(), 1, "folder", "route", ToolboxMenuStatus.Ready);
                    menu.Publish(initial);
                    using (ToolboxMenuViewModel viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher))
                    {
                        RouteToolView view = new RouteToolView { DataContext = viewModel };
                        PumpBindings();
                        Assert.IsNotNull(RouteSelector(view).SelectedItem);

                        menu.Publish(Snapshot(initial.SessionId, 2, "folder", null, ToolboxMenuStatus.Ready));
                        PumpBindings();

                        Assert.IsNull(RouteSelector(view).SelectedItem, "An authoritative clear must not be suppressed as a transient load.");
                        Assert.IsTrue(string.IsNullOrEmpty(Description(view).Text));
                        view.DataContext = null;
                    }
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task WhenActualSelectorsReplaceReorderClearAndReattachThenQualifiedSelectionSurvivesWithoutCommands()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    HostedToolboxMenu menu = CreateMenu();
                    Guid session = Guid.NewGuid();
                    ToolboxMenuEntry folder = new ToolboxMenuEntry(new ToolboxMenuKey("folder", null, null), "Duplicate", null);
                    ToolboxMenuEntry another = new ToolboxMenuEntry(new ToolboxMenuKey("other", null, null), "Duplicate", null);
                    ToolboxMenuEntry first = new ToolboxMenuEntry(new ToolboxMenuKey("folder", "first", null), "Duplicate", "First description");
                    ToolboxMenuEntry second = new ToolboxMenuEntry(new ToolboxMenuKey("folder", "second", null), "Duplicate", "Second description");
                    menu.Publish(new ToolboxMenuSnapshot(session, 1, 1, 1, [folder, another], [first, second], [],
                        folder.Key, null, null, second.Key, null, ToolboxMenuStatus.Ready, null, true));
                    using (ToolboxMenuViewModel viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher))
                    {
                        RouteToolView view = new RouteToolView { DataContext = viewModel };
                        PumpBindings();
                        Assert.AreSame(second, RouteSelector(view).SelectedItem);
                        Assert.AreEqual("Second description", Description(view).Text);

                        // Native hierarchy caches are initialized only after binding; selector values own no native data.
                        FolderModel nativeFolder = new FolderModel("folder", "C:\\Unused", new ContentModel());
                        RouteModel nativeRoute = new RouteModel(WorldLocation.None) { Id = "second", Name = "Duplicate" };
                        nativeRoute.Initialize(nativeFolder);
                        int hash = second.GetHashCode();
                        _ = nativeRoute.Hierarchy();
                        nativeRoute.RefreshModel();
                        Assert.AreEqual(hash, second.GetHashCode());
                        Assert.AreEqual(second, new ToolboxMenuEntry(second.Key, "Renamed", "Updated description"));

                        ToolboxMenuEntry replacement = new ToolboxMenuEntry(second.Key, "Renamed", "Updated description");
                        menu.Publish(new ToolboxMenuSnapshot(session, 2, 1, 1, [another, folder], [replacement, first], [],
                            folder.Key, null, null, replacement.Key, null, ToolboxMenuStatus.Ready, null, true));
                        PumpBindings();
                        Assert.AreSame(replacement, RouteSelector(view).SelectedItem);
                        Assert.AreEqual("Updated description", Description(view).Text);
                        view.DataContext = null;
                        PumpBindings();
                        view.DataContext = viewModel;
                        PumpBindings();
                        Assert.AreSame(replacement, RouteSelector(view).SelectedItem);
                        menu.Publish(Snapshot(session, 3, "other", "second", ToolboxMenuStatus.Ready));
                        PumpBindings();
                        Assert.AreNotEqual(second, RouteSelector(view).SelectedItem);
                        Assert.AreEqual("other", ((ToolboxMenuEntry)RouteSelector(view).SelectedItem).Key.FolderId);
                        menu.Publish(Snapshot(session, 4, "other", null, ToolboxMenuStatus.Ready));
                        PumpBindings();
                        Assert.IsNull(RouteSelector(view).SelectedItem);
                        view.DataContext = null;
                        // CreateMenu has no game transport: any accidental selection command would have thrown.
                    }
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public void WhenLocalIdsRepeatAcrossNamespacesThenFolderRouteAndPathKeysCannotAlias()
        {
            Assert.AreNotEqual(new ToolboxMenuKey("a", "route", null), new ToolboxMenuKey("b", "route", null));
            Assert.AreNotEqual(new ToolboxMenuKey("a", "route", "path"), new ToolboxMenuKey("a", "other", "path"));
            Assert.AreNotEqual(new ToolboxMenuKey("a", "route", "path"), new ToolboxMenuKey("b", "route", "path"));
            Assert.AreEqual(new ToolboxMenuKey("A", "ROUTE", "PATH"), new ToolboxMenuKey("a", "route", "path"));
        }

        [TestMethod]
        public async Task WhenCapturedCallbacksArriveLateThenOlderRevisionsOtherSessionsAndDisposedViewsAreIgnored()
        {
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                StaControlHost wpf = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (wpf.ConfigureAwait(false))
                {
                    HostedToolboxMenu menu = CreateMenu();
                    Guid session = Guid.NewGuid();
                    await owner.OnOwnerAsync(() => menu.Publish(Snapshot(session, 1, "a", "one", ToolboxMenuStatus.Ready))).ConfigureAwait(false);
                    ToolboxMenuViewModel viewModel = null;
                    RouteToolView view = null;
                    await wpf.OnOwnerAsync(() =>
                    {
                        viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher);
                        view = new RouteToolView { DataContext = viewModel };
                        PumpBindings();
                    }).ConfigureAwait(false);
                    EventHandler<ToolboxMenuSnapshot> captured = (EventHandler<ToolboxMenuSnapshot>)typeof(HostedToolboxMenu)
                        .GetField("SnapshotChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
                    await owner.OnOwnerAsync(() =>
                    {
                        menu.Publish(Snapshot(session, 3, "b", "three", ToolboxMenuStatus.Ready));
                        captured(menu, Snapshot(session, 2, "a", "two", ToolboxMenuStatus.Ready));
                        captured(menu, Snapshot(Guid.NewGuid(), 999, "old-session", "old", ToolboxMenuStatus.Ready));
                    }).ConfigureAwait(false);
                    await wpf.OnOwnerAsync(() =>
                    {
                        PumpBindings();
                        Assert.AreEqual("b", viewModel.SelectedFolder.Key.FolderId);
                        Assert.AreEqual("three", ((ToolboxMenuEntry)RouteSelector(view).SelectedItem).Key.RouteId);
                        viewModel.Dispose();
                        captured(menu, Snapshot(session, 4, "disposed", "late", ToolboxMenuStatus.Ready));
                        PumpBindings();
                        Assert.AreEqual("b", viewModel.SelectedFolder.Key.FolderId);
                        view.DataContext = null;
                        using ToolboxMenuViewModel reattached = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher);
                        view.DataContext = reattached;
                        PumpBindings();
                        Assert.AreEqual("three", ((ToolboxMenuEntry)RouteSelector(view).SelectedItem).Key.RouteId);
                        view.DataContext = null;
                    }).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        public async Task WhenPublicationOverlapsInitialSubscriptionThenCurrentStateAndCapturedDeliveryCannotLoseUpdate()
        {
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                StaControlHost wpf = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (wpf.ConfigureAwait(false))
                {
                    HostedToolboxMenu menu = CreateMenu();
                    Guid session = Guid.NewGuid();
                    TaskCompletionSource published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    using ManualResetEventSlim deliver = new ManualResetEventSlim();
                    menu.SnapshotChanged += (_, _) => { published.SetResult(); deliver.Wait(TestContext.CancellationToken); };
                    Task publication = owner.OnOwnerAsync(() => menu.Publish(Snapshot(session, 2, "latest", "route", ToolboxMenuStatus.Ready)));
                    ToolboxMenuViewModel viewModel = null;
                    try
                    {
                        await published.Task.ConfigureAwait(false);
                        await wpf.OnOwnerAsync(() =>
                        {
                            viewModel = new ToolboxMenuViewModel(menu, Dispatcher.CurrentDispatcher);
                            Assert.AreEqual("latest", viewModel.SelectedFolder.Key.FolderId);
                        }).ConfigureAwait(false);
                    }
                    finally
                    {
                        deliver.Set();
                        await publication.ConfigureAwait(false);
                    }
                    await wpf.OnOwnerAsync(() =>
                    {
                        PumpBindings();
                        Assert.AreEqual("latest", viewModel.SelectedFolder.Key.FolderId);
                        viewModel.Dispose();
                    }).ConfigureAwait(false);
                }
            }
        }

        private static HostedToolboxMenu CreateMenu()
        {
            GameWindow game = (GameWindow)RuntimeHelpers.GetUninitializedObject(typeof(GameWindow));
#pragma warning disable CA1816 // Deliberately no MonoGame/device construction.
            GC.SuppressFinalize(game);
#pragma warning restore CA1816
            return new HostedToolboxMenu(game);
        }

        private static ToolboxMenuSnapshot Snapshot(Guid session, long revision, string folderId, string routeId, ToolboxMenuStatus status)
            => Snapshot(session, revision, folderId, routeId, status, null, true);

        private static ToolboxMenuSnapshot Snapshot(Guid session, long revision, string folderId, string routeId, ToolboxMenuStatus status, string error, bool enabled)
        {
            ToolboxMenuEntry folder = new ToolboxMenuEntry(new ToolboxMenuKey(folderId, null, null), "Folder", null);
            ToolboxMenuEntry route = routeId == null ? null : new ToolboxMenuEntry(new ToolboxMenuKey(folderId, routeId, null), "Route", "Description");
            return new ToolboxMenuSnapshot(session, revision, revision, revision, [folder], route == null ? [] : [route], [],
                folder.Key, null, null, route?.Key, null, status, error, enabled);
        }

        private static ComboBox RouteSelector(RouteToolView view) => (ComboBox)((StackPanel)((DockPanel)view.Content).Children[0]).Children[3];
        private static TextBox Description(RouteToolView view) => (TextBox)((DockPanel)view.Content).Children[1];

        private static void PumpBindings()
        {
            DispatcherFrame frame = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }

        public TestContext TestContext { get; set; }
    }
}