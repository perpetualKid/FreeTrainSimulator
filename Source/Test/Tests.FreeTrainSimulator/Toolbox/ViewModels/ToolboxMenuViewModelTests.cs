using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Toolbox.Hosting;

namespace Tests.FreeTrainSimulator.Toolbox.ViewModels
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class ToolboxMenuViewModelTests
    {
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
        {
            ToolboxMenuEntry folder = new ToolboxMenuEntry(new ToolboxMenuKey(folderId, null, null), "Folder", null);
            ToolboxMenuEntry route = routeId == null ? null : new ToolboxMenuEntry(new ToolboxMenuKey(folderId, routeId, null), "Route", "Description");
            return new ToolboxMenuSnapshot(session, revision, revision, revision, [folder], route == null ? [] : [route], [],
                folder.Key, null, null, route?.Key, null, status, null, true);
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