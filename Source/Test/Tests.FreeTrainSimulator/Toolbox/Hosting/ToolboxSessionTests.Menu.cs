using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Simulator;
using Tests.FreeTrainSimulator.Toolbox.Views;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(20000, CooperativeCancellation = true)]
        public async Task WhenActualCloseRouteMenuItemIsInvokedThenDirtyCancelAcceptAndQueuedStaleLifetimeRespectSession()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                StaControlHost wpf = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (wpf.ConfigureAwait(false))
                {
                    FolderModel folder = MenuFolder("Folder");
                    RouteModelHeader route = MenuRoute(folder, "route", "Route");
                    TaskCompletionSource<UnsavedPathConfirmationEventArgs>[] prompts =
                    [
                        new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously),
                        new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously),
                    ];
                    TaskCompletionSource canceled = Completion();
                    TaskCompletionSource unloaded = Completion();
                    int promptCount = 0;
                    PathEditor editor = null;
                    PathModel authored = null;
                    RouteToolView view = null;
                    MenuItem closeRoute = null;
                    ToolboxMenuViewModel viewModel = null;
                    Window window = null;
                    try
                    {
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            SetMenuState(scope, folder, [folder], [route], route);
                            editor = CreateEditor();
                            editor.InitializeNewPath();
                            authored = editor.TryCaptureCurrentPathModel();
                            SetField(scope.Session, "pathEditor", editor);
                            scope.Session.UnsavedPathConfirmationRequested += (_, args) =>
                            {
                                ++promptCount;
                                if (promptCount <= prompts.Length)
                                    prompts[promptCount - 1].SetResult(args);
                                else
                                    args.Completion.TrySetResult(false);
                            };
                            scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                            {
                                if (state.Status != ToolboxMenuStatus.Ready)
                                    return;
                                if (state.CommittedRoute == null)
                                    unloaded.TrySetResult();
                                else if (promptCount == 1)
                                    canceled.TrySetResult();
                            };
                        }).ConfigureAwait(false);
                        ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                        await wpf.OnOwnerAsync(() =>
                        {
                            viewModel = new ToolboxMenuViewModel(scope.Game.HostedMenu, Dispatcher.CurrentDispatcher);
                            view = new RouteToolView { DataContext = viewModel };
                            Menu mainMenu = ProductionMainMenu.Load();
                            closeRoute = ProductionMainMenu.FindCloseRouteItem(mainMenu);
                            DockPanel content = new DockPanel();
                            content.Children.Add(mainMenu);
                            content.Children.Add(view);
                            window = new Window
                            {
                                DataContext = new MainWindowViewModel { Menu = viewModel },
                                Content = content,
                                Width = 360,
                                Height = 420,
                                ShowInTaskbar = false,
                            };
                            window.Show();
                            OpenFileMenu(closeRoute);
                            Assert.AreSame(viewModel.UnloadRouteCommand, closeRoute.Command);
                            Assert.IsNull(view.FindName("UnloadRouteButton"));
                            Assert.IsTrue(closeRoute.IsEnabled);
                            InvokeCloseRouteMenuItem(closeRoute);
                        }).ConfigureAwait(false);
                        UnsavedPathConfirmationEventArgs cancel = await prompts[0].Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await wpf.OnOwnerAsync(() =>
                        {
                            OpenFileMenu(closeRoute);
                            Assert.AreEqual(ToolboxMenuStatus.Confirming, viewModel.Status);
                            Assert.IsFalse(closeRoute.IsEnabled);
                            Assert.IsFalse(string.IsNullOrEmpty(((TextBlock)view.FindName("RouteStatusMessage")).Text));
                            Assert.AreEqual(route.Id, ((ToolboxMenuEntry)MenuRouteSelector(view).SelectedItem).Key.RouteId);
                            // Direct Execute must not bypass the disabled menu item while confirmation is pending.
                            viewModel.UnloadRouteCommand.Execute(null);
                        }).ConfigureAwait(false);
                        cancel.Completion.SetResult(false);
                        await canceled.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            Assert.AreEqual(1, promptCount);
                            Assert.AreSame(route, scope.Session.SelectedRoute);
                            Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                            Assert.AreSame(authored, editor.TryCaptureCurrentPathModel());
                            Assert.IsTrue(editor.HasUnsavedChanges);
                            Assert.AreEqual(initial.RouteContext, scope.Game.HostedMenu.Current.RouteContext);
                        }).ConfigureAwait(false);
                        await wpf.OnOwnerAsync(() =>
                        {
                            OpenFileMenu(closeRoute);
                            Assert.IsTrue(closeRoute.IsEnabled);
                            Assert.AreEqual(route.Description, ((TextBox)((DockPanel)view.Content).Children[1]).Text);
                            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)view.FindName("RouteStatusMessage")).Visibility);
                            InvokeCloseRouteMenuItem(closeRoute);
                        }).ConfigureAwait(false);
                        UnsavedPathConfirmationEventArgs accept = await prompts[1].Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        accept.Completion.SetResult(true);
                        await unloaded.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            Assert.AreEqual(2, promptCount);
                            Assert.IsNull(scope.Session.SelectedRoute);
                            Assert.IsNull(scope.Session.ExistingPathEditor);
                        }).ConfigureAwait(false);
                        await wpf.OnOwnerAsync(() =>
                        {
                            OpenFileMenu(closeRoute);
                            Assert.IsNull(MenuRouteSelector(view).SelectedItem);
                            Assert.IsTrue(string.IsNullOrEmpty(((TextBox)((DockPanel)view.Content).Children[1]).Text));
                            Assert.IsFalse(closeRoute.IsEnabled);
                            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)view.FindName("RouteStatusMessage")).Visibility);
                            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)view.FindName("RouteErrorDetails")).Visibility);
                        }).ConfigureAwait(false);

                        // Queue an actual WPF click behind an owner-side replacement of the same route key.
                        // The click must retain its old lifetime, not reread Current when transport runs.
                        await scope.Host.OnOwnerAsync(() => SetMenuState(scope, folder, [folder], [route], route)).ConfigureAwait(false);
                        await wpf.OnOwnerAsync(() => OpenFileMenu(closeRoute)).ConfigureAwait(false);
                        ToolboxMenuSnapshot beforeReplacement = scope.Game.HostedMenu.Current;
                        ToolboxMenuSnapshot replacementState = null;
                        PathEditor replacementEditor = null;
                        TaskCompletionSource ownerBlocked = Completion();
                        using (ManualResetEventSlim releaseOwner = new ManualResetEventSlim())
                        {
                            Task replacement = scope.Host.OnOwnerAsync(() =>
                            {
                                ownerBlocked.SetResult();
                                releaseOwner.Wait(TestContext.CancellationToken);
                                typeof(ToolboxSession).GetMethod("ClearRoute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scope.Session, null);
                                SetMenuState(scope, folder, [folder], [route], route);
                                replacementEditor = CreateEditor();
                                replacementEditor.InitializeNewPath();
                                SetField(scope.Session, "pathEditor", replacementEditor);
                                replacementState = scope.Game.HostedMenu.Current;
                            });
                            try
                            {
                                await ownerBlocked.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                                await wpf.OnOwnerAsync(() =>
                                {
                                    Assert.IsTrue(closeRoute.IsEnabled);
                                    InvokeCloseRouteMenuItem(closeRoute);
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                releaseOwner.Set();
                                await replacement.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                            }
                        }
                        await scope.Game.InvokeOnGameThreadAsync(() => Task.CompletedTask).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            Assert.IsGreaterThan(beforeReplacement.RouteContext, replacementState.RouteContext);
                            Assert.AreEqual(beforeReplacement.CommittedRoute, replacementState.CommittedRoute);
                            Assert.AreSame(replacementState, scope.Game.HostedMenu.Current);
                            Assert.AreEqual(2, promptCount, "A queued stale unload must not prompt for the replacement's dirty path.");
                            Assert.AreSame(route, scope.Session.SelectedRoute);
                            Assert.AreSame(replacementEditor, scope.Session.ExistingPathEditor);
                            Assert.IsTrue(replacementEditor.HasUnsavedChanges);
                        }).ConfigureAwait(false);
                        await wpf.OnOwnerAsync(() =>
                        {
                            OpenFileMenu(closeRoute);
                            Assert.IsTrue(closeRoute.IsEnabled);
                            Assert.AreEqual(route.Id, viewModel.SelectedRoute.Key.RouteId);
                        }).ConfigureAwait(false);
                    }
                    finally
                    {
                        foreach (TaskCompletionSource<UnsavedPathConfirmationEventArgs> prompt in prompts)
                            if (prompt.Task.IsCompletedSuccessfully)
                                prompt.Task.Result.Completion.TrySetResult(false);
                        await wpf.OnOwnerAsync(() =>
                        {
                            window?.Close();
                            view?.DataContext = null;
                            viewModel?.Dispose();
                        }).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenActualRouteViewBindsNativeProjectionThenHierarchyReplacementAndReattachmentEmitOnlyUserIntent()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                StaControlHost wpf = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (wpf.ConfigureAwait(false))
                {
                    FolderModel folder = MenuFolder("Duplicate folder");
                    FolderModel target = MenuFolder("Duplicate folder");
                    RouteModelHeader first = MenuRoute(folder, "first", "Duplicate route");
                    RouteModelHeader second = MenuRoute(folder, "second", "Duplicate route");
                    int intents = 0;
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        SetMenuState(scope, folder, [folder, target], [first, second], first);
                        PathEditor editor = CreateEditor();
                        SetField(editor, "unsavedChanges", true);
                        SetField(scope.Session, "pathEditor", editor);
                        scope.Session.UnsavedPathConfirmationRequested += (_, args) =>
                        {
                            ++intents;
                            args.Completion.SetResult(false);
                        };
                    }).ConfigureAwait(false);
                    RouteToolView view = null;
                    ToolboxMenuViewModel viewModel = null;
                    await wpf.OnOwnerAsync(() =>
                    {
                        viewModel = new ToolboxMenuViewModel(scope.Game.HostedMenu, Dispatcher.CurrentDispatcher);
                        view = new RouteToolView { DataContext = viewModel };
                        PumpMenuBindings();
                        Assert.AreEqual("first", ((ToolboxMenuEntry)MenuRouteSelector(view).SelectedItem).Key.RouteId);
                    }).ConfigureAwait(false);
                    ToolboxMenuEntry before = scope.Game.HostedMenu.Current.Routes[0];
                    int hash = before.GetHashCode();
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        _ = first.Hierarchy();
                        _ = second.Hierarchy();
                        first.RefreshModel();
                        PublishMenu(scope);
                    }).ConfigureAwait(false);
                    await wpf.OnOwnerAsync(() =>
                    {
                        PumpMenuBindings();
                        Assert.AreEqual(hash, before.GetHashCode());
                        Assert.AreEqual(2, MenuRouteSelector(view).Items.Count);
                        view.DataContext = null;
                        PumpMenuBindings();
                        view.DataContext = viewModel;
                        PumpMenuBindings();
                        MenuRouteSelector(view).SelectedIndex = 1;
                        PumpMenuBindings();
                    }).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() => Assert.AreEqual(1, intents)).ConfigureAwait(false);
                    await wpf.OnOwnerAsync(() =>
                    {
                        PumpMenuBindings();
                        Assert.AreEqual("first", ((ToolboxMenuEntry)MenuRouteSelector(view).SelectedItem).Key.RouteId);
                        ComboBox folders = MenuFolderSelector(view);
                        folders.SelectedItem = viewModel.ContentFolders[0].Key.FolderId == target.Id ? viewModel.ContentFolders[0] : viewModel.ContentFolders[1];
                        PumpMenuBindings();
                    }).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() => Assert.AreEqual(2, intents)).ConfigureAwait(false);
                    await wpf.OnOwnerAsync(() =>
                    {
                        PumpMenuBindings();
                        Assert.AreEqual(folder.Id, viewModel.SelectedFolder.Key.FolderId);
                        Assert.AreEqual("first", viewModel.SelectedRoute.Key.RouteId);
                        view.DataContext = null;
                        viewModel.Dispose();
                    }).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenFolderConfirmationIsPendingAndDeclinedThenAcceptedContentRemainsCoherent()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Accepted");
                FolderModel target = MenuFolder("Pending");
                RouteModelHeader route = MenuRoute(folder, "accepted-route", "Accepted route");
                TaskCompletionSource<UnsavedPathConfirmationEventArgs> prompt = new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder, target], [route], route);
                    PathEditor editor = CreateEditor();
                    SetField(editor, "unsavedChanges", true);
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => prompt.SetResult(args);
                }).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                Task selection = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(initial.Command(new ToolboxMenuKey(target.Id, null, null))));
                UnsavedPathConfirmationEventArgs confirmation = await prompt.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                ToolboxMenuSnapshot pending = scope.Game.HostedMenu.Current;
                Assert.AreEqual((folder.Id, target.Id, route.Id, 1, ToolboxMenuStatus.Confirming),
                    (pending.AcceptedFolder.FolderId, pending.PendingFolder.FolderId, pending.CommittedRoute.RouteId, pending.Routes.Length, pending.Status));
                confirmation.Completion.SetResult(false);
                await selection.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                ToolboxMenuSnapshot declined = scope.Game.HostedMenu.Current;
                Assert.AreEqual((folder.Id, route.Id, 1, ToolboxMenuStatus.Ready),
                    (declined.AcceptedFolder.FolderId, declined.CommittedRoute.RouteId, declined.Routes.Length, declined.Status));
                Assert.IsNull(declined.PendingFolder);
                Assert.IsTrue(scope.Session.ExistingPathEditor.HasUnsavedChanges);
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenDiscardConfirmationIsPendingThenCapturedEditAndSaveCommandsProtectAuthoredPath(bool selectFolder, bool edit)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Accepted");
                FolderModel target = MenuFolder("Pending");
                RouteModelHeader route = MenuRoute(folder, "accepted", "Accepted route");
                RouteModelHeader other = MenuRoute(folder, "pending", "Pending route");
                TaskCompletionSource<UnsavedPathConfirmationEventArgs> prompt = new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathEditor editor = null;
                PathModel authored = null;
                int saves = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder, target], [route, other], route);
                    editor = CreateEditor();
                    SetField(scope.Session, "pathEditor", editor);
                    editor.InitializeNewPath();
                    PathModel source = editor.TryCaptureCurrentPathModel();
                    Assert.IsTrue(editor.SetMetadataCommand("Protected unsaved path", source.Start, source.End, source.PlayerPath).Success);
                    authored = editor.TryCaptureCurrentPathModel();
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => prompt.SetResult(args);
                    scope.Session.SaveTrainPathRequested += (_, _) => ++saves;
                }).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                ToolboxMenuCommand captured = initial.Command(initial.CommittedRoute);
                Task selection = scope.Game.InvokeOnGameThreadAsync(() => selectFolder
                    ? scope.Session.SelectFolderAsync(initial.Command(new ToolboxMenuKey(target.Id, null, null)))
                    : scope.Session.ToggleRouteAsync(initial.Command(new ToolboxMenuKey(folder.Id, other.Id, null))));
                UnsavedPathConfirmationEventArgs confirmation = await prompt.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                try
                {
                    Assert.AreEqual(ToolboxMenuStatus.Confirming, scope.Game.HostedMenu.Current.Status);
                    Assert.IsFalse(scope.Game.HostedMenu.Current.CanEditPath);
                    try
                    {
                        await scope.Game.InvokeOnGameThreadAsync(() => edit
                            ? scope.Session.EditMenuPathAsync(captured)
                            : scope.Session.SaveMenuPathAsync(captured)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Check preservation even if a wrongly admitted edit reaches platform focus in this device-free host.
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                            Assert.AreSame(authored, editor.TryCaptureCurrentPathModel());
                            Assert.IsTrue(editor.HasUnsavedChanges);
                            Assert.AreEqual(0, saves);
                        }).ConfigureAwait(false);
                    }
                }
                finally
                {
                    confirmation.Completion.TrySetResult(false);
                    await selection.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                }
                Assert.IsTrue(scope.Game.HostedMenu.Current.CanEditPath);
                // Save has no platform-focus side effect, so exercise re-admission without activating the map.
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SaveMenuPathAsync(captured)).ConfigureAwait(false);
                Assert.AreEqual(1, saves);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenEditorMetadataIsRenamedAndUndoneThenMenuUpdatesWithoutPublishingUnchangedPreviewMetadata()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder], [route], route);
                    PathEditor editor = CreateEditor();
                    SetField(scope.Session, "pathEditor", editor);
                    editor.OnPathChanged += typeof(ToolboxSession).GetMethod("PathEditor_OnEditorPathChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                        .CreateDelegate<EventHandler<PathEditorChangedEventArgs>>(scope.Session);
                    editor.OnPathUpdated += typeof(ToolboxSession).GetMethod("PathEditor_OnEditorPathUpdated", BindingFlags.Instance | BindingFlags.NonPublic)
                        .CreateDelegate<EventHandler<PathEditorChangedEventArgs>>(scope.Session);
                    editor.InitializeNewPath();
                    PathModel original = editor.TryCaptureCurrentPathModel();
                    ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                    Assert.AreEqual(original.Name, initial.SelectedPath.Name);
                    PathEditorCommandResult rename = editor.SetMetadataCommand("Unsaved rename", original.Start, original.End, original.PlayerPath);
                    Assert.IsTrue(rename.Success);
                    PathModel renamed = editor.TryCaptureCurrentPathModel();
                    Assert.AreEqual((original.Id, original.Start, original.End, original.PlayerPath, original.PathNodes),
                        (renamed.Id, renamed.Start, renamed.End, renamed.PlayerPath, renamed.PathNodes));
                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual("Unsaved rename", state.SelectedPath.Name);
                    Assert.AreEqual(initial.Revision + 1, state.Revision);
                    Assert.AreEqual(initial.SelectedPath.Key, state.SelectedPath.Key);
                    Assert.AreEqual(original.Name, initial.SelectedPath.Name, "Previously captured metadata remains immutable.");
                    Assert.AreEqual((initial.FolderContext, initial.RouteContext, initial.AcceptedFolder, initial.CommittedRoute, initial.Paths),
                        (state.FolderContext, state.RouteContext, state.AcceptedFolder, state.CommittedRoute, state.Paths));
                    editor.ClearRouteCandidatePreview();
                    Assert.AreSame(renamed, editor.TryCaptureCurrentPathModel());
                    Assert.AreSame(state, scope.Game.HostedMenu.Current, "Preview-only updates must not reset the menu lists.");
                    Assert.IsTrue(editor.Undo());
                    state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual(original.Name, state.SelectedPath.Name);
                    Assert.AreEqual(initial.Revision + 2, state.Revision);
                    Assert.IsTrue(editor.SetMetadataCommand(original.Name, "Changed start", original.End, original.PlayerPath).Success);
                    editor.ClearRouteCandidatePreview();
                    Assert.AreSame(state, scope.Game.HostedMenu.Current, "Metadata absent from the menu must not republish it.");
                    Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                    Assert.AreSame(route, scope.Session.SelectedRoute);
                    Assert.IsTrue(editor.HasUnsavedChanges);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenFolderIsAcceptedThenDiscoveryPublishesOnlyMatchingEmptyAndDiscoveredState()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Original");
                FolderModel target = MenuFolder("Target");
                RouteModelHeader oldRoute = MenuRoute(folder, "old", "Old");
                RouteModelHeader first = MenuRoute(target, "first", "Same");
                RouteModelHeader second = MenuRoute(target, "second", "Same");
                TaskCompletionSource<ImmutableArray<RouteModelHeader>> discovery = new TaskCompletionSource<ImmutableArray<RouteModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
                MenuRouteCache.SetRoutes(target, discovery.Task);
                TaskCompletionSource accepted = Completion();
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder, target], [oldRoute], oldRoute);
                    scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                    {
                        if (state.Status == ToolboxMenuStatus.DiscoveringRoutes)
                            accepted.TrySetResult();
                    };
                }).ConfigureAwait(false);
                Task select = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(scope.Game.HostedMenu.Current.Command(new ToolboxMenuKey(target.Id, null, null))));
                try
                {
                    await accepted.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((target.Id, 0, 0, ToolboxMenuStatus.DiscoveringRoutes), (state.AcceptedFolder.FolderId, state.Routes.Length, state.Paths.Length, state.Status));
                    Assert.IsNull(state.CommittedRoute);
                    Assert.IsNull(state.SelectedPath);
                    discovery.SetResult([second, first]);
                    await select.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((target.Id, 2, ToolboxMenuStatus.Ready), (state.AcceptedFolder.FolderId, state.Routes.Length, state.Status));
                    Assert.IsTrue(state.Routes.All(entry => entry.Key.FolderId == target.Id));
                }
                finally
                {
                    discovery.TrySetResult([]);
                    await select.ConfigureAwait(false);
                    MenuRouteCache.RemoveRoutes(target);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSingleRouteAutoLoadFailsSyntheticallyThenActualViewKeepsAcceptedFolderAndClearsRoute()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                StaControlHost wpf = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (wpf.ConfigureAwait(false))
                {
                    FolderModel folder = MenuFolder("Original");
                    FolderModel target = MenuFolder("Accepted target");
                    RouteModelHeader oldRoute = MenuRoute(folder, "old", "Old");
                    RouteModelHeader route = MenuRoute(target, "failure", "Synthetic failure");
                    TaskCompletionSource<RouteModelHeader> read = new TaskCompletionSource<RouteModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                    MenuRouteCache.SetRoutes(target, Task.FromResult(ImmutableArray.Create(route)));
                    MenuRouteCache.SetRead(route, read.Task);
                    TaskCompletionSource loading = Completion();
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        SetMenuState(scope, folder, [folder, target], [oldRoute], oldRoute);
                        scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                        {
                            if (state.Status == ToolboxMenuStatus.LoadingRoute)
                                loading.TrySetResult();
                        };
                    }).ConfigureAwait(false);
                    RouteToolView view = null;
                    ToolboxMenuViewModel viewModel = null;
                    await wpf.OnOwnerAsync(() =>
                    {
                        viewModel = new ToolboxMenuViewModel(scope.Game.HostedMenu, Dispatcher.CurrentDispatcher);
                        view = new RouteToolView { DataContext = viewModel };
                        PumpMenuBindings();
                    }).ConfigureAwait(false);
                    Task select = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(scope.Game.HostedMenu.Current.Command(new ToolboxMenuKey(target.Id, null, null))));
                    try
                    {
                        await loading.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                        Assert.AreEqual((target.Id, route.Id, 1, ToolboxMenuStatus.LoadingRoute),
                            (state.AcceptedFolder.FolderId, state.PendingRoute.RouteId, state.Routes.Length, state.Status));
                        Assert.IsNull(state.CommittedRoute);
                        // Loading publication precedes native cache admission. Let the owner reach its pending
                        // read before faulting it; pre-faulted cache tasks are correctly retried by native handlers.
                        await scope.Host.OnOwnerAsync(() => { }).ConfigureAwait(false);
                        read.SetException(new IOException("Synthetic route read failure."));
                        _ = await Assert.ThrowsExactlyAsync<IOException>(() => select.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                        state = scope.Game.HostedMenu.Current;
                        Assert.AreEqual((target.Id, 1, 0, ToolboxMenuStatus.Failed, "Synthetic route read failure."),
                            (state.AcceptedFolder.FolderId, state.Routes.Length, state.Paths.Length, state.Status, state.Error));
                        Assert.IsNull(state.CommittedRoute);
                        Assert.IsNull(state.PendingRoute);
                        Assert.IsNull(state.SelectedPath);
                        Assert.IsFalse(state.CanEditPath);
                        await wpf.OnOwnerAsync(() =>
                        {
                            PumpMenuBindings();
                            Assert.AreEqual(target.Id, viewModel.SelectedFolder.Key.FolderId);
                            Assert.AreEqual(1, MenuRouteSelector(view).Items.Count);
                            Assert.IsNull(MenuRouteSelector(view).SelectedItem);
                            Assert.IsTrue(string.IsNullOrEmpty(((TextBox)((DockPanel)view.Content).Children[1]).Text));
                        }).ConfigureAwait(false);
                    }
                    finally
                    {
                        read.TrySetException(new IOException("Synthetic cleanup."));
                        try
                        { await select.ConfigureAwait(false); }
                        catch (IOException) { }
                        MenuRouteCache.RemoveRoutes(target);
                        MenuRouteCache.RemoveRead(route);
                        await wpf.OnOwnerAsync(() => { view.DataContext = null; viewModel.Dispose(); }).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenOldAutoLoadFailureUnwindsAfterSameFolderRetryThenItsErrorCannotReappear()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Retry folder");
                RouteModelHeader route = MenuRoute(folder, "retry", "Retry route");
                TaskCompletionSource<RouteModelHeader> oldRead = new TaskCompletionSource<RouteModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<RouteModelHeader> retryRead = new TaskCompletionSource<RouteModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource loading = Completion();
                TaskCompletionSource failedPublication = Completion();
                TaskCompletionSource releaseUnwind = Completion();
                const string oldError = "Earlier auto-load failed.";
                const string retryError = "Current retry failed.";
                MenuRouteCache.SetRoutes(folder, Task.FromResult(ImmutableArray.Create(route)));
                MenuRouteCache.SetRead(route, oldRead.Task);
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, null, [folder], [], null);
                    scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                    {
                        if (state.Status == ToolboxMenuStatus.LoadingRoute)
                            loading.TrySetResult();
                    };
                    RouteLoadCoordinator loads = (RouteLoadCoordinator)typeof(ToolboxSession).GetField("routeLoads", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session);
                    bool held = false;
                    // Keep the owner pumping, but hold the old load's continuation after its real failure commit.
                    SetField(loads, "dispatchAsync", new Func<Func<Task>, Task>(async action =>
                    {
                        bool hold = false;
                        await scope.Game.InvokeOnGameThreadAsync(async () =>
                        {
                            await action().ConfigureAwait(true);
                            if (!held && scope.Game.HostedMenu.Current.Error == oldError)
                                hold = held = true;
                        }).ConfigureAwait(false);
                        if (hold)
                        {
                            failedPublication.SetResult();
                            await releaseUnwind.Task.ConfigureAwait(false);
                        }
                    }));
                }).ConfigureAwait(false);
                Task original = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(scope.Game.HostedMenu.Current.Command(new ToolboxMenuKey(folder.Id, null, null))));
                Task retry = null;
                try
                {
                    await loading.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() => oldRead.SetException(new IOException(oldError))).ConfigureAwait(false);
                    await failedPublication.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.AreEqual((ToolboxMenuStatus.Failed, oldError), (scope.Game.HostedMenu.Current.Status, scope.Game.HostedMenu.Current.Error));
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        MenuRouteCache.SetRead(route, retryRead.Task);
                        ToolboxMenuSnapshot failed = scope.Game.HostedMenu.Current;
                        retry = scope.Session.ToggleRouteAsync(failed.Command(failed.Routes.Single().Key));
                    }).ConfigureAwait(false);
                    ToolboxMenuSnapshot current = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((folder.Id, route.Id, ToolboxMenuStatus.LoadingRoute), (current.AcceptedFolder.FolderId, current.PendingRoute.RouteId, current.Status));
                    Assert.IsNull(current.Error);
                    releaseUnwind.SetResult();
                    IOException oldFailure = await Assert.ThrowsExactlyAsync<IOException>(() => original.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                    Assert.AreEqual(oldError, oldFailure.Message);
                    Assert.AreSame(current, scope.Game.HostedMenu.Current, "The older unwind must not republish into the retry's folder context.");
                    retryRead.SetException(new IOException(retryError));
                    IOException retryFailure = await Assert.ThrowsExactlyAsync<IOException>(() => retry.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                    Assert.AreEqual(retryError, retryFailure.Message);
                    Assert.AreEqual((folder.Id, ToolboxMenuStatus.Failed, retryError),
                        (scope.Game.HostedMenu.Current.AcceptedFolder.FolderId, scope.Game.HostedMenu.Current.Status, scope.Game.HostedMenu.Current.Error));
                }
                finally
                {
                    releaseUnwind.TrySetResult();
                    oldRead.TrySetException(new IOException(oldError));
                    retryRead.TrySetException(new IOException(retryError));
                    try
                    { await original.ConfigureAwait(false); }
                    catch (IOException) { }
                    if (retry != null)
                        try
                        { await retry.ConfigureAwait(false); }
                        catch (IOException) { }
                    MenuRouteCache.RemoveRoutes(folder);
                    MenuRouteCache.RemoveRead(route);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenCommandsAreStaleThenOwnerRejectsThemButRapidSameFolderIntentRemainsValid()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Accepted");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                RouteModelHeader other = MenuRoute(folder, "other", "Other");
                List<UnsavedPathConfirmationEventArgs> prompts = new List<UnsavedPathConfirmationEventArgs>();
                await scope.Host.OnOwnerAsync(() =>
            {
                SetMenuState(scope, folder, [folder], [route, other], route);
                PathEditor editor = CreateEditor();
                SetField(editor, "unsavedChanges", true);
                SetField(scope.Session, "pathEditor", editor);
                scope.Session.UnsavedPathConfirmationRequested += (_, args) => prompts.Add(args);
            }).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                ToolboxMenuKey key = new ToolboxMenuKey(folder.Id, other.Id, null);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ToggleRouteAsync(new ToolboxMenuCommand(Guid.NewGuid(), initial.FolderContext, initial.RouteContext, key))).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ToggleRouteAsync(new ToolboxMenuCommand(initial.SessionId, initial.FolderContext + 1, initial.RouteContext, key))).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ToggleRouteAsync(initial.Command(new ToolboxMenuKey("other-folder", other.Id, null)))).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.TogglePathAsync(new ToolboxMenuCommand(initial.SessionId, initial.FolderContext, initial.RouteContext + 1, new ToolboxMenuKey(folder.Id, route.Id, "path")))).ConfigureAwait(false);
                Assert.IsEmpty(prompts);
                Task first = null;
                Task second = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    first = scope.Session.ToggleRouteAsync(initial.Command(key));
                    // A new revision and even unrelated route-lifetime value must not reject another route pick.
                    PublishMenu(scope);
                    second = scope.Session.ToggleRouteAsync(new ToolboxMenuCommand(initial.SessionId, initial.FolderContext, initial.RouteContext - 1, key));
                    Assert.HasCount(2, prompts);
                    prompts[1].Completion.SetResult(false);
                    prompts[0].Completion.SetResult(true);
                }).ConfigureAwait(false);
                await Task.WhenAll(first, second).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Assert.AreSame(route, scope.Session.SelectedRoute);
                Assert.AreEqual(route.Id, scope.Game.HostedMenu.Current.CommittedRoute.RouteId);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenCommittedRouteIsUnloadedThenMenuClearsRoutePathsDescriptionAndCommandsTogether()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                await scope.Host.OnOwnerAsync(() => SetMenuState(scope, folder, [folder], [route], route)).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ToggleRouteAsync(initial.Command(initial.CommittedRoute))).ConfigureAwait(false);
                ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                Assert.AreEqual((folder.Id, 1, 0, ToolboxMenuStatus.Ready, false), (state.AcceptedFolder.FolderId, state.Routes.Length, state.Paths.Length, state.Status, state.CanEditPath));
                Assert.IsNull(state.CommittedRoute);
                Assert.IsNull(state.PendingRoute);
                Assert.IsNull(state.SelectedPath);
                Assert.IsGreaterThan(initial.RouteContext, state.RouteContext);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenExplicitRouteUnloadSucceedsThenOwnerReleasesEditorComparisonsAndPathState(bool dirty)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                PathEditor editor = null;
                RouteLoadCoordinator loads = null;
                RouteLoadCoordinator.Request request = null;
                int prompts = 0;
                int releases = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    editor = scope.Session.ExistingPathEditor;
                    if (dirty)
                        Assert.IsTrue(editor.SetMetadataCommand("Unsaved unload", saved.Start, saved.End, saved.PlayerPath).Success);
                    Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                    Assert.IsNotNull(scope.Session.CaptureTrainPathSaveState());
                    Assert.HasCount(1, content.SavedPathComparisons);
                    loads = (RouteLoadCoordinator)typeof(ToolboxSession).GetField("routeLoads", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session);
                    request = loads.BeginRequest(loads.BeginSelection());
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) =>
                    {
                        Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                        ++prompts;
                        args.Completion.SetResult(true);
                    };
                    scope.Session.OnPathEditorChanged += (_, args) =>
                    {
                        Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                        Assert.IsNull(args.PathEditor);
                        ++releases;
                    };
                    scope.Game.HostedMenu.SnapshotChanged += (_, _) => Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                }).ConfigureAwait(false);
                using (request)
                {
                    ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                    Assert.HasCount(1, initial.Paths);
                    Assert.IsNotNull(initial.SelectedPath);
                    await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.UnloadRouteAsync(initial.Command(initial.CommittedRoute)))
                        .WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                        Assert.AreEqual((initial.AcceptedFolder, initial.FolderContext, ToolboxMenuStatus.Ready), (state.AcceptedFolder, state.FolderContext, state.Status));
                        Assert.AreSequenceEqual(initial.Routes, state.Routes);
                        Assert.IsGreaterThan(initial.RouteContext, state.RouteContext);
                        Assert.IsNull(state.CommittedRoute);
                        Assert.IsNull(state.PendingRoute);
                        Assert.IsNull(state.SelectedPath);
                        Assert.IsNull(state.Error);
                        Assert.IsEmpty(state.Paths);
                        Assert.IsFalse(state.CanEditPath);
                        Assert.IsNull(scope.Session.SelectedRoute);
                        Assert.IsNull(scope.Session.ExistingPathEditor);
                        Assert.IsNull(scope.Session.TrainPathToolingContext);
                        Assert.IsNull(loads.Current);
                        Assert.IsTrue(request.Token.IsCancellationRequested);
                        Assert.IsTrue((bool)typeof(PathEditor).GetField("stopped", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                        Assert.IsEmpty(content.SavedPathComparisons);
                        bridge.RefreshSnapshot();
                        Assert.IsEmpty(bridge.CaptureTrainPathSnapshot().Paths);
                        Assert.IsEmpty(bridge.CaptureTrainPathSnapshot().Comparisons);
                        foreach (string field in new[] { "toolboxContent", "saveDialogEditor", "saveDialogSource" })
                            Assert.IsNull(typeof(ToolboxSession).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session), field);
                        Assert.AreEqual((dirty ? 1 : 0, 1), (prompts, releases));
                    }).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenExplicitRouteUnloadIsCanceledThenDirtyEditorAndAcceptedContentRemain()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                PathEditor editor = null;
                PathModel authored = null;
                TaskCompletionSource<UnsavedPathConfirmationEventArgs> prompt = new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder], [route], route);
                    editor = CreateEditor();
                    editor.InitializeNewPath();
                    authored = editor.TryCaptureCurrentPathModel();
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => prompt.SetResult(args);
                }).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                Task unload = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.UnloadRouteAsync(initial.Command(initial.CommittedRoute)));
                UnsavedPathConfirmationEventArgs confirmation = await prompt.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                try
                {
                    ToolboxMenuSnapshot pending = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((initial.CommittedRoute, initial.CommittedRoute, ToolboxMenuStatus.Confirming, false),
                        (pending.CommittedRoute, pending.PendingRoute, pending.Status, pending.CanEditPath));
                }
                finally
                {
                    confirmation.Completion.TrySetResult(false);
                    await unload.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                }
                await scope.Host.OnOwnerAsync(() =>
                {
                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((initial.AcceptedFolder, initial.CommittedRoute, initial.RouteContext, ToolboxMenuStatus.Ready), (state.AcceptedFolder, state.CommittedRoute, state.RouteContext, state.Status));
                    Assert.AreSequenceEqual(initial.Routes, state.Routes);
                    Assert.IsNull(state.PendingRoute);
                    Assert.IsTrue(state.CanEditPath);
                    Assert.AreSame(route, scope.Session.SelectedRoute);
                    Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                    Assert.AreSame(authored, editor.TryCaptureCurrentPathModel());
                    Assert.IsTrue(editor.HasUnsavedChanges);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow("session")]
        [DataRow("folder-context")]
        [DataRow("route-context")]
        [DataRow("folder-key")]
        [DataRow("route-key")]
        [DataRow("path-key")]
        [DataRow("folder-only-key")]
        [DataRow("null-key")]
        [DataRow("null-command")]
        [DataRow("confirming-route")]
        [DataRow("confirming-folder")]
        [DataRow("discovering")]
        [DataRow("loading")]
        [DataRow("failed")]
        [DataRow("disabled")]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenExplicitRouteUnloadIsNotAdmissibleThenOwnerRejectsItWithoutPromptOrPublication(string invalid)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                PathEditor editor = null;
                int prompts = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder], [route], route);
                    editor = CreateEditor();
                    editor.InitializeNewPath();
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => { ++prompts; args.Completion.SetResult(false); };
                    switch (invalid)
                    {
                        case "confirming-route": SetField(scope.Session, "pendingRoute", new ToolboxMenuKey(folder.Id, route.Id, null)); break;
                        case "confirming-folder": SetField(scope.Session, "pendingFolder", new ToolboxMenuKey(folder.Id, null, null)); break;
                        case "discovering": SetField(scope.Session, "discoveringRoutes", true); break;
                        case "loading": SetField(scope.Session, "loadingRoute", new ToolboxMenuKey(folder.Id, route.Id, null)); break;
                        case "failed": SetField(scope.Session, "menuError", "Existing failure"); break;
                        case "disabled": scope.Session.SetMenuEnabled(false); break;
                    }
                    PublishMenu(scope);
                }).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                ToolboxMenuCommand command = invalid switch
                {
                    "session" => new ToolboxMenuCommand(Guid.NewGuid(), initial.FolderContext, initial.RouteContext, initial.CommittedRoute),
                    "folder-context" => new ToolboxMenuCommand(initial.SessionId, initial.FolderContext + 1, initial.RouteContext, initial.CommittedRoute),
                    "route-context" => new ToolboxMenuCommand(initial.SessionId, initial.FolderContext, initial.RouteContext + 1, initial.CommittedRoute),
                    "folder-key" => initial.Command(new ToolboxMenuKey("other-folder", route.Id, null)),
                    "route-key" => initial.Command(new ToolboxMenuKey(folder.Id, "other-route", null)),
                    "path-key" => initial.Command(new ToolboxMenuKey(folder.Id, route.Id, "path")),
                    "folder-only-key" => initial.Command(new ToolboxMenuKey(folder.Id, null, null)),
                    "null-key" => initial.Command(null),
                    "null-command" => null,
                    _ => initial.Command(initial.CommittedRoute),
                };
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.UnloadRouteAsync(command)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.AreEqual(0, prompts);
                    Assert.AreSame(initial, scope.Game.HostedMenu.Current);
                    Assert.AreSame(route, scope.Session.SelectedRoute);
                    Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                    Assert.IsTrue(editor.HasUnsavedChanges);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSameRouteKeyIsReusedThenExplicitUnloadRejectsOldLifetimeBeforeOrAfterConfirmation(bool duringConfirmation)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                List<UnsavedPathConfirmationEventArgs> prompts = new List<UnsavedPathConfirmationEventArgs>();
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder], [route], route);
                    PathEditor editor = CreateEditor();
                    editor.InitializeNewPath();
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => prompts.Add(args);
                }).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                ToolboxMenuCommand captured = initial.Command(initial.CommittedRoute);
                Task unload = null;
                PathEditor replacement = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    if (duringConfirmation)
                    {
                        unload = scope.Session.UnloadRouteAsync(captured);
                        Assert.HasCount(1, prompts);
                    }
                    // ClearRoute advances the lifetime, not selection intent. Reuse even the same route object.
                    typeof(ToolboxSession).GetMethod("ClearRoute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scope.Session, null);
                    SetField(scope.Session, "pendingRoute", null);
                    SetMenuState(scope, folder, [folder], [route], route);
                    replacement = CreateEditor();
                    replacement.InitializeNewPath();
                    SetField(scope.Session, "pathEditor", replacement);
                }).ConfigureAwait(false);
                ToolboxMenuSnapshot current = scope.Game.HostedMenu.Current;
                Assert.AreEqual(initial.CommittedRoute, current.CommittedRoute);
                Assert.IsGreaterThan(initial.RouteContext, current.RouteContext);
                if (duringConfirmation)
                    prompts[0].Completion.SetResult(true);
                else
                    unload = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.UnloadRouteAsync(captured));
                await unload.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.HasCount(duringConfirmation ? 1 : 0, prompts);
                    Assert.AreSame(current, scope.Game.HostedMenu.Current);
                    Assert.AreSame(route, scope.Session.SelectedRoute);
                    Assert.AreSame(replacement, scope.Session.ExistingPathEditor);
                    Assert.IsTrue(replacement.HasUnsavedChanges);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenExplicitUnloadConfirmationIsSupersededThenOldCompletionCannotClearNewFolderOrRouteIntent(bool selectFolder, bool confirmUnload)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Accepted");
                FolderModel target = MenuFolder("Pending");
                RouteModelHeader route = MenuRoute(folder, "accepted", "Accepted route");
                RouteModelHeader other = MenuRoute(folder, "pending", "Pending route");
                List<UnsavedPathConfirmationEventArgs> prompts = new List<UnsavedPathConfirmationEventArgs>();
                Task unload = null;
                Task selection = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder, target], [route, other], route);
                    PathEditor editor = CreateEditor();
                    editor.InitializeNewPath();
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => prompts.Add(args);
                    ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                    unload = scope.Session.UnloadRouteAsync(initial.Command(initial.CommittedRoute));
                    selection = selectFolder
                        ? scope.Session.SelectFolderAsync(initial.Command(new ToolboxMenuKey(target.Id, null, null)))
                        : scope.Session.ToggleRouteAsync(initial.Command(new ToolboxMenuKey(folder.Id, other.Id, null)));
                    Assert.HasCount(2, prompts);
                }).ConfigureAwait(false);
                try
                {
                    ToolboxMenuSnapshot current = scope.Game.HostedMenu.Current;
                    Assert.AreEqual(ToolboxMenuStatus.Confirming, current.Status);
                    Assert.AreEqual(selectFolder ? target.Id : other.Id, selectFolder ? current.PendingFolder.FolderId : current.PendingRoute.RouteId);
                    prompts[0].Completion.SetResult(confirmUnload);
                    await unload.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.AreSame(current, scope.Game.HostedMenu.Current, "Neither acceptance nor Cancel may clear newer selection intent.");
                }
                finally
                {
                    foreach (UnsavedPathConfirmationEventArgs prompt in prompts)
                        prompt.Completion.TrySetResult(false);
                    await Task.WhenAll(unload, selection).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                }
                Assert.AreSame(route, scope.Session.SelectedRoute);
                Assert.IsTrue(scope.Session.ExistingPathEditor.HasUnsavedChanges);
                Assert.AreEqual(ToolboxMenuStatus.Ready, scope.Game.HostedMenu.Current.Status);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenExplicitUnloadIsRepeatedThenStaleAndFreshCommandsCannotLoadTheUnloadedRoute()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                await scope.Host.OnOwnerAsync(() => SetMenuState(scope, folder, [folder], [route], route)).ConfigureAwait(false);
                ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                ToolboxMenuCommand captured = initial.Command(initial.CommittedRoute);
                TaskCompletionSource unloaded = Completion();
                await scope.Host.OnOwnerAsync(() => scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                {
                    if (state.Status == ToolboxMenuStatus.Ready && state.CommittedRoute == null)
                        unloaded.TrySetResult();
                }).ConfigureAwait(false);
                scope.Game.HostedMenu.UnloadRoute(captured);
                await unloaded.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                ToolboxMenuSnapshot current = scope.Game.HostedMenu.Current;
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.UnloadRouteAsync(captured)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.UnloadRouteAsync(current.Command(captured.Key))).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Assert.AreSame(current, scope.Game.HostedMenu.Current);
                Assert.AreEqual((folder.Id, 1, ToolboxMenuStatus.Ready), (current.AcceptedFolder.FolderId, current.Routes.Length, current.Status));
                Assert.IsNull(scope.Session.SelectedRoute);
                Assert.IsNull(current.PendingRoute);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionStopsDuringExplicitUnloadConfirmationThenDrainClosesIntentAndLateAcceptanceCannotUnload()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                UnsavedPathConfirmationEventArgs confirmation = null;
                Task unload = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetMenuState(scope, folder, [folder], [route], route);
                    PathEditor editor = CreateEditor();
                    editor.InitializeNewPath();
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Session.UnsavedPathConfirmationRequested += (_, args) => confirmation = args;
                    ToolboxMenuSnapshot initial = scope.Game.HostedMenu.Current;
                    unload = scope.Session.UnloadRouteAsync(initial.Command(initial.CommittedRoute));
                    Assert.IsNotNull(confirmation);
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await unload.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                ToolboxMenuSnapshot stopped = scope.Game.HostedMenu.Current;
                Assert.AreEqual(ToolboxMenuStatus.Stopped, stopped.Status);
                Assert.IsNull(stopped.PendingRoute);
                Assert.IsFalse(confirmation.Completion.TrySetResult(true));
                Assert.AreSame(route, scope.Session.SelectedRoute);
                Assert.AreSame(stopped, scope.Game.HostedMenu.Current);
                _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Session.UnloadRouteAsync(stopped.Command(stopped.CommittedRoute)))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStartupRouteDiscoveryFaultsThenOnlyItsAcceptedFolderGetsTerminalFailure(bool replaceFolder)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Startup folder");
                FolderModel target = MenuFolder("Current folder");
                TaskCompletionSource<ImmutableArray<RouteModelHeader>> discovery = new TaskCompletionSource<ImmutableArray<RouteModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource discovering = Completion();
                MenuRouteCache.SetRoutes(folder, discovery.Task);
                MenuRouteCache.SetRoutes(target, Task.FromResult(ImmutableArray<RouteModelHeader>.Empty));
                Task<ContentModel> previous = MenuContentCache.Replace(new ContentModel([folder, target]) { Version = ContentModel.MinimumVersion });
                await scope.Host.OnOwnerAsync(() =>
                {
                    scope.Game.ToolboxSettings.RestoreLastView = true;
                    scope.Game.ToolboxSettings.Folder = folder.Id;
                    scope.Game.ToolboxSettings.RouteId = "missing";
                    scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                    {
                        if (state.Status == ToolboxMenuStatus.DiscoveringRoutes && state.AcceptedFolder?.FolderId == folder.Id)
                            discovering.TrySetResult();
                    };
                }).ConfigureAwait(false);
                Task startup = scope.Game.InvokeOnGameThreadAsync(scope.Session.StartAsync);
                try
                {
                    await discovering.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    // Ensure the native collection waiter has been admitted before faulting its shared task.
                    await scope.Host.OnOwnerAsync(() => { }).ConfigureAwait(false);
                    ToolboxMenuSnapshot current = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((folder.Id, ToolboxMenuStatus.DiscoveringRoutes), (current.AcceptedFolder.FolderId, current.Status));
                    if (replaceFolder)
                    {
                        await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(current.Command(new ToolboxMenuKey(target.Id, null, null)))).ConfigureAwait(false);
                        current = scope.Game.HostedMenu.Current;
                        Assert.AreEqual((target.Id, ToolboxMenuStatus.Ready), (current.AcceptedFolder.FolderId, current.Status));
                    }
                    const string error = "Startup route discovery failed.";
                    discovery.SetException(new IOException(error));
                    IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => startup.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                    Assert.AreEqual(error, failure.Message);
                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    if (replaceFolder)
                    {
                        Assert.AreSame(current, state, "An old startup discovery fault cannot publish into the newly accepted context.");
                        Assert.IsNull(state.Error);
                        Assert.IsTrue(scope.Game.ToolboxSettings.RestoreLastView);
                    }
                    else
                    {
                        Assert.AreEqual((folder.Id, ToolboxMenuStatus.Failed, error, 0, 0),
                            (state.AcceptedFolder.FolderId, state.Status, state.Error, state.Routes.Length, state.Paths.Length));
                        Assert.IsNull(state.PendingFolder);
                        Assert.IsNull(state.PendingRoute);
                        Assert.IsNull(state.CommittedRoute);
                        Assert.IsNull(state.SelectedPath);
                        Assert.IsFalse(state.CanEditPath);
                        Assert.IsFalse(scope.Game.ToolboxSettings.RestoreLastView);
                    }
                }
                finally
                {
                    discovery.TrySetResult([]);
                    try
                    { await startup.ConfigureAwait(false); }
                    catch (IOException) { }
                    MenuRouteCache.RemoveRoutes(folder);
                    MenuRouteCache.RemoveRoutes(target);
                    MenuContentCache.Restore(previous);
                }
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStartupRestoresFolderThenOnlyPersistedIdNotDisplayNameMatches(bool useId)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Renamed folder");
                MenuRouteCache.SetRoutes(folder, Task.FromResult(ImmutableArray<RouteModelHeader>.Empty));
                try
                {
                    await scope.Host.OnOwnerAsync(() => SetField(scope.Session, "contentModel", new ContentModel([folder]))).ConfigureAwait(false);
                    await scope.Game.InvokeOnGameThreadAsync(() =>
                    {
                        RouteLoadCoordinator loads = (RouteLoadCoordinator)typeof(ToolboxSession).GetField("routeLoads", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session);
                        long selection = loads.BeginSelection();
                        return InvokeMenuTask(scope.Session, "PreSelectRouteAsync", [useId ? folder.Id : folder.Name, "missing", null, selection]);
                    }).ConfigureAwait(false);
                    Assert.AreEqual(useId ? folder : null, scope.Session.SelectedFolder);
                }
                finally { MenuRouteCache.RemoveRoutes(folder); }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSavedPathsRefreshThenQualifiedMetadataAndUnpersistedEditorSelectionArePreservedAndStaleResultsIgnored()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                PathModelHeader saved = new PathModelHeader { Id = "saved", Name = "Updated saved name" };
                saved.Initialize(route);
                MenuPathCache.SetPaths(route, Task.FromResult(ImmutableArray.Create(saved)));
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        SetMenuState(scope, folder, [folder], [route], route);
                        PathEditor editor = CreateEditor();
                        editor.InitializeNewPath();
                        SetField(scope.Session, "pathEditor", editor);
                    }).ConfigureAwait(false);
                    await scope.Game.InvokeOnGameThreadAsync(() => InvokeMenuTask(scope.Session, "RefreshPathListAfterSaveAsync", [route, 0L])).ConfigureAwait(false);
                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((folder.Id, route.Id, "saved", "Updated saved name"),
                        (state.Paths[0].Key.FolderId, state.Paths[0].Key.RouteId, state.Paths[0].Key.PathId, state.Paths[0].Name));
                    Assert.IsNotNull(state.SelectedPath);
                    Assert.AreNotEqual(saved.Id, state.SelectedPath.Key.PathId);
                    TaskCompletionSource<ImmutableArray<PathModelHeader>> refresh = new TaskCompletionSource<ImmutableArray<PathModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
                    MenuPathCache.SetPaths(route, refresh.Task);
                    Task stale = null;
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        stale = InvokeMenuTask(scope.Session, "RefreshPathListAfterSaveAsync", [route, 0L]);
                        SetField(scope.Session, "routeContentVersion", 1L);
                        SetField(scope.Session, "selectedRoute", MenuRoute(folder, "route", "Replaced same ID"));
                        PublishMenu(scope);
                    }).ConfigureAwait(false);
                    long revision = scope.Game.HostedMenu.Current.Revision;
                    refresh.SetResult([]);
                    await stale.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.AreEqual(revision, scope.Game.HostedMenu.Current.Revision);
                }
                finally { MenuPathCache.RemovePaths(route); }
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStaleSaveRefreshArrivesAfterCurrentRouteRefreshStartsThenItDoesNotInvalidateOrSuppressCurrentPaths(bool sameRoute, bool sameVersion)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader routeA = MenuRoute(folder, "route-a", "Old route");
                RouteModelHeader routeB = sameRoute ? routeA : MenuRoute(folder, "route-b", "Current route");
                PathModelHeader saved = new PathModelHeader { Id = "current-saved", Name = "Current saved path" };
                saved.Initialize(routeB);
                TaskCompletionSource<ImmutableArray<PathModelHeader>> pending = new TaskCompletionSource<ImmutableArray<PathModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
                MenuPathCache.SetPaths(routeB, pending.Task);
                FieldInfo refreshVersion = typeof(ToolboxSession).GetField("pathListRefreshVersion", BindingFlags.Instance | BindingFlags.NonPublic);
                Task current = null;
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        SetMenuState(scope, folder, [folder], [routeA, routeB], routeB);
                        SetField(scope.Session, "routeContentVersion", 1L);
                        current = InvokeMenuTask(scope.Session, "RefreshPathListAfterSaveAsync", [routeB, 1L]);
                        Assert.IsFalse(current.IsCompleted);
                        Assert.AreEqual(1L, refreshVersion.GetValue(scope.Session));
                    }).ConfigureAwait(false);

                    // Simulate an old save's worker continuation only after B's real cached lookup is pending.
                    Task stale = InvokeMenuTask(scope.Session, "RefreshPathListAfterSaveAsync", [routeA, sameVersion ? 1L : 0L]);
                    await scope.Host.OnOwnerAsync(() => Assert.AreEqual(1L, refreshVersion.GetValue(scope.Session))).ConfigureAwait(false);
                    await stale.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    pending.SetResult([saved]);
                    await current.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((routeB.Id, saved.Id, saved.Name),
                        (state.Paths.Single().Key.RouteId, state.Paths.Single().Key.PathId, state.Paths.Single().Name));
                }
                finally
                {
                    pending.TrySetResult([]);
                    MenuPathCache.RemovePaths(routeB);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedSessionReceivesSaveRefreshThenItDoesNotAdvanceRefreshVersion()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "route", "Route");
                await scope.Host.OnOwnerAsync(() => SetMenuState(scope, folder, [folder], [route], route)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);

                await InvokeMenuTask(scope.Session, "RefreshPathListAfterSaveAsync", [route, 0L]).ConfigureAwait(false);

                await scope.Host.OnOwnerAsync(() => Assert.AreEqual(0L,
                    typeof(ToolboxSession).GetField("pathListRefreshVersion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionStopsDuringNativeRouteReadThenFinalSnapshotClearsLoadingIntentAndSharedReadSurvives()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Folder");
                RouteModelHeader route = MenuRoute(folder, "paused", "Paused route");
                TaskCompletionSource<RouteModelHeader> read = new TaskCompletionSource<RouteModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource loading = Completion();
                MenuRouteCache.SetRead(route, read.Task);
                Task selection = null;
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        SetMenuState(scope, folder, [folder], [route], null);
                        scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                        {
                            if (state.Status == ToolboxMenuStatus.LoadingRoute)
                                loading.TrySetResult();
                        };
                    }).ConfigureAwait(false);
                    selection = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ToggleRouteAsync(scope.Game.HostedMenu.Current.Command(new ToolboxMenuKey(folder.Id, route.Id, null))));
                    await loading.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    // Publication precedes native cache admission; this owner barrier lets the waiter start.
                    await scope.Host.OnOwnerAsync(() => { }).ConfigureAwait(false);
                    Assert.AreEqual(route.Id, scope.Game.HostedMenu.Current.PendingRoute.RouteId);
                    Assert.IsFalse(selection.IsCompleted);
                    await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await selection.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    ToolboxMenuSnapshot stopped = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((ToolboxMenuStatus.Stopped, false, false), (stopped.Status, stopped.Enabled, stopped.CanEditPath));
                    Assert.IsNull(stopped.PendingFolder);
                    Assert.IsNull(stopped.PendingRoute);
                    Assert.IsFalse(read.Task.IsCompleted, "Stopping cancels the waiter, not its borrowed shared native read.");
                    RouteModel loaded = new RouteModel(WorldLocation.None) { Id = route.Id, Name = route.Name };
                    loaded.Initialize(folder);
                    read.SetResult(loaded);
                    Assert.AreSame(loaded, await route.GetExtended(TestContext.CancellationToken).ConfigureAwait(false));
                    await scope.Host.OnOwnerAsync(() => PublishMenu(scope)).ConfigureAwait(false);
                    Assert.AreSame(stopped, scope.Game.HostedMenu.Current);
                }
                finally
                {
                    await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    if (selection != null)
                        await selection.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    read.TrySetResult(null);
                    MenuRouteCache.RemoveRead(route);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionStopsThenFinalSnapshotClosesCommandsAndLatePublicationCannotReviveMenu()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);
                ToolboxMenuSnapshot stopped = scope.Game.HostedMenu.Current;
                Assert.AreEqual((ToolboxMenuStatus.Stopped, false, false), (stopped.Status, stopped.Enabled, stopped.CanEditPath));
                await scope.Host.OnOwnerAsync(() => PublishMenu(scope)).ConfigureAwait(false);
                Assert.AreSame(stopped, scope.Game.HostedMenu.Current);
            }
        }

        private static FolderModel MenuFolder(string name)
            => new FolderModel(name, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), new ContentModel()) { Id = Guid.NewGuid().ToString("N") };

        private static RouteModelHeader MenuRoute(FolderModel folder, string id, string name)
        {
            RouteModelHeader route = new MenuRouteHeader { Id = id, Name = name, Description = name + " description" };
            route.Initialize(folder);
            return route;
        }

        private static void SetMenuState(SessionScope scope, FolderModel folder, ImmutableArray<FolderModel> folders,
            ImmutableArray<RouteModelHeader> routes, RouteModelHeader route)
        {
            SetField(scope.Session, "contentModel", new ContentModel(folders));
            SetField(scope.Session, "selectedFolder", folder);
            SetField(scope.Session, "menuRoutes", routes);
            SetField(scope.Session, "selectedRoute", route);
            PublishMenu(scope);
        }

        private static void PublishMenu(SessionScope scope)
            => typeof(ToolboxSession).GetMethod("PublishMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scope.Session, null);

        private static Task InvokeMenuTask(ToolboxSession session, string name, object[] arguments)
            => (Task)typeof(ToolboxSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, arguments);

        private static ComboBox MenuRouteSelector(RouteToolView view) => (ComboBox)((StackPanel)((DockPanel)view.Content).Children[0]).Children[3];
        private static ComboBox MenuFolderSelector(RouteToolView view) => (ComboBox)((StackPanel)((DockPanel)view.Content).Children[0]).Children[1];

        private static void OpenFileMenu(MenuItem closeRoute)
        {
            PumpMenuBindings();
            // WPF defers submenu command admission until its parent opens.
            ((MenuItem)closeRoute.Parent).IsSubmenuOpen = true;
            PumpMenuBindings();
        }

        private static void InvokeCloseRouteMenuItem(MenuItem closeRoute)
        {
            OpenFileMenu(closeRoute);
            IInvokeProvider provider = (IInvokeProvider)new MenuItemAutomationPeer(closeRoute).GetPattern(PatternInterface.Invoke);
            provider.Invoke();
            PumpMenuBindings();
        }

        private static void PumpMenuBindings()
        {
            DispatcherFrame frame = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }

        private sealed record MenuRouteHeader : RouteModelHeader
        {
            internal MenuRouteHeader() : base(WorldLocation.None)
            {
            }
        }

        private abstract class MenuRouteCache : ContentHandlerBase<RouteModelHeader>
        {
            internal static void SetRoutes(FolderModel folder, Task<ImmutableArray<RouteModelHeader>> routes) => modelSetTaskCache[folder.Hierarchy()] = routes;
            internal static void RemoveRoutes(FolderModel folder) => modelSetTaskCache.TryRemove(folder.Hierarchy(), out _);
            internal static void SetRead(RouteModelHeader route, Task<RouteModelHeader> read) => modelTaskCache[route.Parent.Hierarchy(route.Id)] = read;
            internal static void RemoveRead(RouteModelHeader route) => modelTaskCache.TryRemove(route.Parent.Hierarchy(route.Id), out _);
        }

        private abstract class MenuContentCache : ContentHandlerBase<ContentModel>
        {
            internal static Task<ContentModel> Replace(ContentModel content)
            {
                modelTaskCache.TryGetValue("content", out Task<ContentModel> previous);
                modelTaskCache["content"] = Task.FromResult(content);
                return previous;
            }

            internal static void Restore(Task<ContentModel> previous)
            {
                if (previous == null)
                    modelTaskCache.TryRemove("content", out _);
                else
                    modelTaskCache["content"] = previous;
            }
        }

        private abstract class MenuPathCache : ContentHandlerBase<PathModelHeader>
        {
            internal static void SetPaths(RouteModelHeader route, Task<ImmutableArray<PathModelHeader>> paths)
            {
                collectionUpdateRequired.TryRemove(route.Hierarchy(), out _);
                modelSetTaskCache[route.Hierarchy()] = paths;
            }
            internal static void RemovePaths(RouteModelHeader route) => modelSetTaskCache.TryRemove(route.Hierarchy(), out _);
        }
    }
}
