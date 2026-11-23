using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [DataRow(true, "folder", "route", true)]
        [DataRow(false, "folder", "route", false)]
        [DataRow(true, null, "route", false)]
        [DataRow(true, "folder", null, false)]
        [DataRow(true, "", "route", false)]
        [DataRow(true, "folder", " ", false)]
        public void WhenStartupPreferencesAreEvaluatedThenOnlyConfiguredRestorationStartsTheMap(bool restore, string folder, string route, bool expected)
        {
            MainWindow shell = CreateStartupShell();
            SetField(shell, "toolboxSettings", new ProfileToolboxSettingsModel { RestoreLastView = restore, Folder = folder, RouteId = route });

            bool actual = (bool)typeof(MainWindow).GetMethod("ShouldRestoreRoute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);

            Assert.AreEqual(expected, actual);
            Assert.IsNull(typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
        }

        [TestMethod]
        public void WhenNewProfileHasSavedRouteThenRestorationIsEnabledByDefault()
        {
            MainWindow shell = CreateStartupShell();
            SetField(shell, "toolboxSettings", new ProfileToolboxSettingsModel { Folder = "folder", RouteId = "route" });

            Assert.IsTrue((bool)typeof(MainWindow).GetMethod("ShouldRestoreRoute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null));
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, true)]
        public void WhenShellIsNotReadyOrClosingThenMapStartupIsRejected(bool initialized, bool closing)
        {
            MainWindow shell = CreateStartupShell();
            SetField(shell, "shellInitialized", initialized);
            SetField(shell, "isShuttingDown", closing);

            TargetInvocationException error = Assert.ThrowsExactly<TargetInvocationException>(() =>
                typeof(MainWindow).GetMethod("StartMapAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null));

            Assert.IsInstanceOfType<InvalidOperationException>(error.InnerException);
            Assert.IsNull(typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
        }

        [TestMethod]
        public void WhenAutomaticAndManualEntryOverlapThenTheyShareStartupCompletion()
        {
            MainWindow shell = CreateStartupShell();
            TaskCompletionSource pending = Completion();
            SetField(shell, "shellInitialized", true);
            SetField(shell, "mapStartupTask", pending.Task);
            MethodInfo start = typeof(MainWindow).GetMethod("StartMapAsync", BindingFlags.Instance | BindingFlags.NonPublic);

            Task automatic = (Task)start.Invoke(shell, null);
            Task manual = (Task)start.Invoke(shell, null);

            Assert.AreSame(pending.Task, automatic);
            Assert.AreSame(automatic, manual);
            Assert.IsFalse(manual.IsCompleted);
            pending.SetResult();
            Assert.IsTrue(manual.IsCompletedSuccessfully);
        }

        private static MainWindow CreateStartupShell()
        {
            MainWindow shell = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
#pragma warning disable CA1816 // No constructor-owned WPF or native resources exist in this policy-only object.
            GC.SuppressFinalize(shell);
#pragma warning restore CA1816
            return shell;
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenHostNeverStartsThenStopAndDisposeDoNotWaitForGameReadiness()
        {
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                await owner.OnOwnerAsync(() =>
                {
                    ToolboxGameHostControl host = new ToolboxGameHostControl();
                    System.Windows.Forms.Control panel = host.Child;
                    int releases = 0;
                    panel.Disposed += (_, _) => ++releases;
                    try
                    {
                        Assert.IsNull(typeof(ToolboxGameHostControl).GetField("gameThread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host));
                        Assert.IsFalse(host.IsStoppedSafely);
                        Task first = host.StopAsync();
                        Assert.IsTrue(first.IsCompletedSuccessfully);
                        Assert.AreSame(first, host.StopAsync());
                        Assert.IsTrue(host.IsStoppedSafely);
                        Assert.ThrowsExactly<InvalidOperationException>(() => host.StartAsync(new ProfileModel("unused"),
                            new ProfileUserSettingsModel(), new ProfileToolboxSettingsModel(), null));
                    }
                    finally
                    {
                        host.Dispose();
                    }
                    Assert.IsTrue(panel.IsDisposed);
                    int firstReleaseCount = releases;
                    host.Dispose();
                    Assert.AreEqual(firstReleaseCount, releases);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenShellNeverCreatesHostThenShutdownCompletesAndReleasesShell()
        {
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                await owner.OnOwnerAsync(() =>
                {
                    MainWindow shell = CreateShutdownShell(null);
                    Task stop = (Task)typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);

                    Assert.IsTrue(stop.IsCompletedSuccessfully);
                    Assert.IsTrue((bool)typeof(MainWindow).GetField("shutdownCompleted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
                    Assert.IsNull(typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
                    shell.Dispose();
                    shell.Dispose();
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenManagementOnlySettingsAreSavedThenMapStateAndFreshPreferencesArePreserved(bool changeRestoration)
        {
            ProfileModel profile = new ProfileModel("lazy-startup-" + Guid.NewGuid().ToString("N"));
            ProfileToolboxSettingsModel original = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            original.Folder = "saved-folder";
            original.RouteId = "saved-route";
            original.PathId = "saved-path";
            original.ContentPosition = new PointD(100, 200);
            original.ContentScale = 3;
            original.DockLayoutJson = "saved-layout";
            original.PathEditorSelectedTabIndex = 1;
            await profile.UpdateSettingsModel(original, CancellationToken.None).ConfigureAwait(false);
            ProfileToolboxSettingsModel stale = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            original.FontOutline = false;
            await profile.UpdateSettingsModel(original, CancellationToken.None).ConfigureAwait(false);

            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                Task save = null;
                await owner.OnOwnerAsync(() =>
                {
                    MainWindow shell = CreateShutdownShell(null);
                    SetField(shell, "currentProfile", profile);
                    SetField(shell, "toolboxSettings", stale);
                    stale.RestoreLastView = false;
                    SetField(shell, "restoreLastViewChanged", changeRestoration);
                    save = (Task)typeof(MainWindow).GetMethod("SaveDockLayoutAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                }).ConfigureAwait(false);
                await save.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }

            ProfileToolboxSettingsModel restored = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            Assert.AreEqual(("saved-folder", "saved-route", "saved-path"), (restored.Folder, restored.RouteId, restored.PathId));
            Assert.AreEqual(new PointD(100, 200), restored.ContentPosition);
            Assert.AreEqual(3d, restored.ContentScale);
            Assert.AreEqual("saved-layout", restored.DockLayoutJson);
            Assert.AreEqual(1, restored.PathEditorSelectedTabIndex);
            Assert.IsFalse(restored.FontOutline);
            Assert.AreEqual(!changeRestoration, restored.RestoreLastView);
        }

        [TestMethod]
        public void WhenShellXamlLoadsThenItContainsNoEagerHostAndExitDoesNotDependOnHostedMenu()
        {
            using (Stream stream = typeof(ToolboxSessionTests).Assembly.GetManifestResourceStream("Tests.FreeTrainSimulator.Toolbox.MainWindow.xaml"))
            {
                XDocument document = XDocument.Load(stream);
                XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
                Assert.AreEqual(0, document.Descendants().Count(element => element.Name.LocalName == nameof(ToolboxGameHostControl)));
                XElement surface = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "MapSurface");
                Assert.AreEqual("ContentControl", surface.Name.LocalName);
                Assert.IsEmpty(surface.Elements());
                XElement docking = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "DockingManager");
                Assert.IsNull(docking.Attribute("Visibility"), "The regular docking layout must remain visible without a map host.");
                XElement overview = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "WorkspaceOverview");
                Assert.AreSame(surface.Parent, overview.Parent, "The startup hint belongs inside the map, not over the tool windows.");
                Assert.AreEqual("MapViewDocument", (string)overview.Ancestors().Single(element => element.Name.LocalName == "LayoutDocument").Attribute("ContentId"));
                XElement menu = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "MainMenu");
                Assert.IsNull(menu.Attribute("IsEnabled"));
                XElement exit = menu.Descendants().Single(element => (string)element.Attribute("Header") == "E_xit");
                Assert.AreEqual("{Binding DataContext.ExitCommand, ElementName=RootWindow}", (string)exit.Attribute("Command"));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenDockingLayoutIsEditedWithoutMapThenOnlyChangedLayoutIsPersisted(bool changeLayout)
        {
            ProfileModel profile = new ProfileModel("empty-map-layout-" + Guid.NewGuid().ToString("N"));
            ProfileToolboxSettingsModel settings = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            settings.DockLayoutJson = "previous-layout";
            settings.RouteId = "saved-route";
            await profile.UpdateSettingsModel(settings, CancellationToken.None).ConfigureAwait(false);
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                MainWindow shell = null;
                Task shutdown = null;
                string expected = "previous-layout";
                await owner.OnOwnerAsync(() =>
                {
                    (shell, MainWindowViewModel viewModel, _, _) = CreateChartShell();
                    ToolboxGameHostControl host = (ToolboxGameHostControl)typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
                    host.Dispose();
                    SetField(shell, "MapHost", null);
                    SetField(shell, "currentProfile", profile);
                    SetField(shell, "toolboxSettings", settings);
                    MethodInfo serialize = typeof(MainWindow).GetMethod("SerializeDockLayout", BindingFlags.Instance | BindingFlags.NonPublic);
                    SetField(shell, "initialDockLayoutJson", (string)serialize.Invoke(shell, null));
                    if (changeLayout)
                    {
                        viewModel.ToggleHelpToolCommand.Execute(null);
                        expected = (string)serialize.Invoke(shell, null);
                    }
                    shutdown = (Task)typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                }).ConfigureAwait(false);
                await shutdown.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await owner.OnOwnerAsync(shell.Dispose).ConfigureAwait(false);
                ProfileToolboxSettingsModel restored = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(expected, restored.DockLayoutJson);
                Assert.AreEqual("saved-route", restored.RouteId);
            }
        }

        [STATestMethod]
        public void WhenMapHasNotStartedThenRegularToolWindowsCanStillBeShownAndHidden()
        {
            (MainWindow shell, MainWindowViewModel viewModel, _, _) = CreateChartShell();
            try
            {
                (string ContentId, RelayCommand Command)[] windows =
                [
                    ("RouteToolWindow", viewModel.ToggleRouteToolCommand),
                    ("LocationToolWindow", viewModel.ToggleLocationToolCommand),
                    ("DebugToolWindow", viewModel.ToggleDebugToolCommand),
                    ("LogToolWindow", viewModel.ToggleLogToolCommand),
                    ("RouteNavigationToolWindow", viewModel.ToggleRouteNavigationToolCommand),
                    ("HelpToolWindow", viewModel.ToggleHelpToolCommand),
                    ("SettingsToolWindow", viewModel.ToggleSettingsToolCommand),
                    ("TrainPathToolWindow", viewModel.ToggleTrainPathToolCommand),
                    ("PathChartToolWindow", viewModel.TogglePathChartToolCommand),
                ];
                MethodInfo find = typeof(MainWindow).GetMethod("FindToolWindowAnchorable", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach ((string contentId, RelayCommand command) in windows)
                {
                    AvalonDock.Layout.LayoutAnchorable window = (AvalonDock.Layout.LayoutAnchorable)find.Invoke(shell, [contentId]);
                    bool visible = window.IsVisible;
                    Assert.IsTrue(command.CanExecute(null), contentId);
                    command.Execute(null);
                    Assert.AreEqual(!visible, window.IsVisible, contentId);
                    command.Execute(null);
                    Assert.AreEqual(visible, window.IsVisible, contentId);
                }
                Assert.IsNull(typeof(MainWindow).GetField("mapStartupTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
                SetField(shell, "isShuttingDown", true);
                foreach ((string contentId, RelayCommand command) in windows)
                    Assert.IsFalse(command.CanExecute(null), contentId);
            }
            finally
            {
                ShutdownChartShell(shell);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenMapIsOpenedThenSavedRouteLoadingDoesNotDependOnAutomaticStartupPreference(bool automaticRestoration)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel folder = MenuFolder("Saved folder");
                RouteModelHeader route = MenuRoute(folder, "saved-route", "Saved route");
                TaskCompletionSource<RouteModelHeader> read = new TaskCompletionSource<RouteModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource loading = Completion();
                MenuRouteCache.SetRoutes(folder, Task.FromResult(ImmutableArray.Create(route)));
                MenuRouteCache.SetRead(route, read.Task);
                Task<ContentModel> previous = MenuContentCache.Replace(new ContentModel([folder]) { Version = ContentModel.MinimumVersion });
                Task startup = null;
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        scope.Game.ToolboxSettings.RestoreLastView = automaticRestoration;
                        scope.Game.ToolboxSettings.Folder = folder.Id;
                        scope.Game.ToolboxSettings.RouteId = route.Id;
                        scope.Game.HostedMenu.SnapshotChanged += (_, state) =>
                        {
                            if (state.Status == ToolboxMenuStatus.LoadingRoute)
                                loading.TrySetResult();
                        };
                    }).ConfigureAwait(false);
                    startup = scope.Game.InvokeOnGameThreadAsync(scope.Session.StartAsync);
                    await loading.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    ToolboxMenuSnapshot state = scope.Game.HostedMenu.Current;
                    Assert.AreEqual((folder.Id, route.Id, ToolboxMenuStatus.LoadingRoute),
                        (state.AcceptedFolder.FolderId, state.PendingRoute.RouteId, state.Status));
                    Assert.AreEqual(automaticRestoration, scope.Game.ToolboxSettings.RestoreLastView, "Explicit loading must not enable automatic startup.");
                    await scope.Host.OnOwnerAsync(() => { }).ConfigureAwait(false);
                    IOException failure = new IOException("Stop before creating GPU resources.");
                    read.SetException(failure);
                    IOException reported = await Assert.ThrowsExactlyAsync<IOException>(() => startup.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                    Assert.AreSame(failure, reported);
                }
                finally
                {
                    read.TrySetException(new IOException("Route-read cleanup."));
                    if (startup != null)
                        await startup.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    MenuRouteCache.RemoveRoutes(folder);
                    MenuRouteCache.RemoveRead(route);
                    MenuContentCache.Restore(previous);
                }
            }
        }
    }
}