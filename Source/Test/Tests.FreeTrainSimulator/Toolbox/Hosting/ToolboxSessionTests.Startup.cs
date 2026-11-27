using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;

using AvalonDock.Layout;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using GetText;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimulatorCommand = global::FreeTrainSimulator.Common.Input.UserCommand;
using ProfileRow = FreeTrainSimulator.Toolbox.ViewModels.ManageProfilesViewModel.ProfileRow;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        private static void AssertProfileLabelsRefresh(SettingsToolView view, SettingsToolWindowViewModel model)
        {
            ListBox profiles = (ListBox)view.FindName("ProfilesList");
            CheckBox reopenRoute = (CheckBox)view.FindName("ReopenLastRouteOption");
            ProfileRow activeProfile = profiles.Items.OfType<ProfileRow>().Single(row => row.IsActive);
            ((Button)view.FindName("CreateProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            (TextBox name, Button save, Button cancel) = ProfileDraftControls(profiles);
            (string Source, Func<string> Read)[] labels =
            [
                ("Reopen last route on startup", () => (string)reopenRoute.Content),
                ("Reopens the last route and restores its saved path and map view.", () => (string)reopenRoute.ToolTip),
                ("Profiles", () => (string)((TabItem)view.FindName("ProfilesTab")).Header),
                ("Profiles", () => AutomationProperties.GetName((ListBox)view.FindName("ProfilesList"))),
                ("Active", () => AutomationProperties.GetItemStatus(profiles.ItemContainerGenerator.ContainerFromItem(activeProfile))),
                ("New profile name", () => AutomationProperties.GetName(name)),
                ("Save", () => ReadProfileActionLabel(save)),
                ("Cancel", () => ReadProfileActionLabel(cancel)),
                ("Create Profile", () => ReadProfileActionLabel((Button)view.FindName("CreateProfileButton"))),
                ("Duplicate Profile", () => ReadProfileActionLabel((Button)view.FindName("DuplicateProfileButton"))),
                ("Delete Profile", () => ReadProfileActionLabel((Button)view.FindName("DeleteProfileButton"))),
            ];
            foreach ((string source, Func<string> read) in labels)
                Assert.AreEqual(CatalogManager.Catalog.GetString(source), read(), source);

            using (LocalizationCatalogScope catalogScope = new LocalizationCatalogScope())
            {
                foreach ((string source, _) in labels)
                    catalogScope.Catalog.Translations[source] = ["Translated " + source];
                model.RefreshLocalization();
                PumpMenuBindings();
                foreach ((string source, Func<string> read) in labels)
                    Assert.AreEqual("Translated " + source, read(), source);

                catalogScope.Catalog.Translations.Clear();
                model.RefreshLocalization();
                PumpMenuBindings();
                foreach ((string source, Func<string> read) in labels)
                    Assert.AreEqual(source, read(), source);
            }
            model.RefreshLocalization();
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsEmpty(profiles.Items.OfType<ListBoxItem>());
        }

        private static string ReadProfileActionLabel(Button button)
        {
            string label = AutomationProperties.GetName(button);
            Assert.AreEqual(label, button.ToolTip);
            return label;
        }

        private static (TextBox Name, Button Save, Button Cancel) ProfileDraftControls(ListBox profiles)
        {
            PumpMenuBindings();
            Grid draft = (Grid)profiles.Items.OfType<ListBoxItem>().Single().Content;
            TextBox name = (TextBox)draft.Children[0];
            Button save = (Button)draft.Children[1];
            Button cancel = (Button)draft.Children[2];
            Point nameOrigin = name.TranslatePoint(new Point(), draft);
            Point saveOrigin = save.TranslatePoint(new Point(), draft);
            Point cancelOrigin = cancel.TranslatePoint(new Point(), draft);
            Assert.IsTrue(name.ActualWidth > 0 && nameOrigin.X < saveOrigin.X && saveOrigin.X < cancelOrigin.X);
            Assert.AreEqual(nameOrigin.Y, saveOrigin.Y, 0.5);
            Assert.AreEqual(saveOrigin.Y, cancelOrigin.Y, 0.5);
            return (name, save, cancel);
        }

        private static void PressProfileDraftKey(TextBox name, Key key)
        {
            name.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(name), 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
        }

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

        [STATestMethod]
        [DoNotParallelize]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenReplacementWindowCannotBeShownThenItClosesWithoutStartingItsWorkspace()
        {
            using (MainWindow replacement = new MainWindow())
            {
                InvalidOperationException showFailure = new InvalidOperationException("Replacement window could not be shown.");
                replacement.SourceInitialized += (_, _) => throw showFailure;
                bool closed = false;
                replacement.Closed += (_, _) => closed = true;

                Task show = (Task)typeof(MainWindow).GetMethod("ShowForProfileHandoffAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(replacement, [replacement]);
                PumpMenuUntil(() => show.IsCompleted);

                Assert.AreSame(showFailure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => show).ConfigureAwait(true));
                Assert.IsTrue(closed);
                Assert.IsTrue((bool)typeof(MainWindow).GetField("disposed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(replacement));
                Assert.IsNull(typeof(MainWindow).GetField("shellInitializationTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(replacement));
            }
        }

        [STATestMethod]
        [DoNotParallelize]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenRouteDiscoveryIsPendingThenSettingsShowsProfilesWithoutStartingMap()
        {
            ProfileModel original = default(ProfileModel).Current(CancellationToken.None).GetAwaiter().GetResult();
            ProfileModel profile = new ProfileModel("shell-profile-" + Guid.NewGuid().ToString("N"));
            ProfileModel copy = new ProfileModel("shell-copy-" + Guid.NewGuid().ToString("N"));
            _ = profile.Create(CancellationToken.None).GetAwaiter().GetResult();
            profile.UpdateCurrent(CancellationToken.None).GetAwaiter().GetResult();
            ProfileUserSettingsModel user = profile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
            user.LogLevel = System.Diagnostics.TraceEventType.Critical;
            user.SoundVolumePercent = 37;
            _ = profile.UpdateSettingsModel(user, CancellationToken.None).GetAwaiter().GetResult();
            ProfileKeyboardSettingsModel keyboard = profile.LoadSettingsModel<ProfileKeyboardSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
            keyboard.UserCommands[SimulatorCommand.CameraCab] = new UserCommandKeyInput(0x10);
            _ = profile.UpdateSettingsModel(keyboard, CancellationToken.None).GetAwaiter().GetResult();
            ProfileRailDriverSettingsModel railDriver = profile.LoadSettingsModel<ProfileRailDriverSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
            railDriver.UserCommands[SimulatorCommand.GameSave] = 42;
            _ = profile.UpdateSettingsModel(railDriver, CancellationToken.None).GetAwaiter().GetResult();
            ProfileDispatcherSettingsModel dispatcher = profile.LoadSettingsModel<ProfileDispatcherSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
            dispatcher.ContentTypeVisibility[MapContentType.Tracks] = false;
            _ = profile.UpdateSettingsModel(dispatcher, CancellationToken.None).GetAwaiter().GetResult();
            ProfileSelectionsModel selections = profile.LoadSettingsModel<ProfileSelectionsModel>(CancellationToken.None).GetAwaiter().GetResult();
            selections.RouteId = "menu-route";
            _ = profile.UpdateSettingsModel(selections, CancellationToken.None).GetAwaiter().GetResult();
            ProfileToolboxSettingsModel startupSettings = profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
            startupSettings.SettingsSelectedTabIndex = 3;
            _ = profile.UpdateSettingsModel(startupSettings, CancellationToken.None).GetAwaiter().GetResult();
            FolderModel folder = MenuFolder("Pending startup folder");
            TaskCompletionSource<ImmutableArray<RouteModelHeader>> routes = new TaskCompletionSource<ImmutableArray<RouteModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
            MenuRouteCache.SetRoutes(folder, routes.Task);
            Task<ContentModel> previous = MenuContentCache.Replace(new ContentModel([folder]) { Version = ContentModel.MinimumVersion });
            MainWindow shell = null;
            MainWindow reopened = null;
            MainWindow fallbackWindow = null;
            SynchronizationContext previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                using (shell = new MainWindow())
                {
                    shell.Show();
                    MainWindowViewModel model = (MainWindowViewModel)shell.DataContext;
                    PumpMenuUntil(() => model.RouteBrowser != null || model.WorkspaceStatus?.Contains("startup failed", StringComparison.Ordinal) == true);
                    Assert.IsNotNull(model.RouteBrowser, model.WorkspaceStatus);
                    Assert.AreEqual(profile.Name, model.SettingsTool.ProfileName);
                    SettingsToolView settings = (SettingsToolView)shell.FindName("SettingsPane");
                    TabItem tab = (TabItem)settings.FindName("ProfilesTab");
                    Assert.IsTrue(tab.IsSelected, "Startup should restore the saved Profiles tab.");
                    ListBox profiles = (ListBox)settings.FindName("ProfilesList");
                    PumpMenuUntil(() => profiles.IsVisible && profiles.Items.OfType<ProfileRow>().Any(row => row.Name == profile.Name));
                    Assert.AreEqual(profile.Name, profiles.Items.OfType<ProfileRow>().Single(row => row.IsActive).Name);
                    Button create = (Button)settings.FindName("CreateProfileButton");
                    Button duplicateAction = (Button)settings.FindName("DuplicateProfileButton");
                    Button delete = (Button)settings.FindName("DeleteProfileButton");
                    ToolBar toolbar = (ToolBar)settings.FindName("ProfilesToolbar");
                    Assert.IsTrue(create.IsEnabled);
                    Assert.AreEqual(4, toolbar.Items.Count);
                    Assert.AreSame(create, toolbar.Items[0]);
                    Assert.AreSame(duplicateAction, toolbar.Items[1]);
                    Assert.IsInstanceOfType<Separator>(toolbar.Items[2]);
                    Assert.AreSame(delete, toolbar.Items[3]);
                    Assert.IsLessThanOrEqualTo(duplicateAction.TranslatePoint(new Point(), toolbar).X, create.TranslatePoint(new Point(), toolbar).X + create.ActualWidth);
                    Assert.IsLessThan(delete.TranslatePoint(new Point(), toolbar).X, duplicateAction.TranslatePoint(new Point(), toolbar).X + duplicateAction.ActualWidth);
                    Assert.IsLessThanOrEqualTo(profiles.TranslatePoint(new Point(), settings).Y, create.TranslatePoint(new Point(0, create.ActualHeight), settings).Y);
                    Assert.Contains(profile.Name, ((TextBlock)settings.FindName("ActiveProfileLabel")).Text, StringComparison.Ordinal);
                    AvalonDock.DockingManager manager = (AvalonDock.DockingManager)shell.FindName("DockingManager");
                    LayoutAnchorable pane = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(item => ReferenceEquals(settings, item.Content));
                    manager.DetachAnchorableToWindow(pane);
                    try
                    {
                        PumpMenuBindings();
                        Assert.IsTrue(pane.IsDetached);
                        Assert.AreNotSame(PresentationSource.FromVisual(shell), PresentationSource.FromVisual(settings));
                        Assert.AreSame(model.SettingsTool, settings.DataContext);
                        Assert.IsTrue(profiles.IsVisible);
                        Assert.IsTrue(((Button)settings.FindName("CreateProfileButton")).IsEnabled);
                        AssertProfileLabelsRefresh(settings, model.SettingsTool);
                    }
                    finally
                    {
                        manager.ReattachAllDetachedAnchorables();
                    }
                    Assert.IsFalse(routes.Task.IsCompleted);
                    Assert.IsNull(typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));

                    model.SettingsTool.ColorItems.Single(item => item.Setting == ColorSetting.Background).UserSelectColorName("Black");
                    model.SettingsTool.TrackVisibilityItems.Single(item => item.Setting == MapContentType.Tracks).IsVisible = false;
                    ProfileToolboxSettingsModel activeWorkspace = (ProfileToolboxSettingsModel)typeof(MainWindow).GetField("toolboxSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
                    activeWorkspace.RouteId = "toolbox-route";
                    activeWorkspace.PathId = "toolbox-path";
                    profiles.SelectedValue = profile.Name;
                    Button duplicate = (Button)settings.FindName("DuplicateProfileButton");
                    Assert.IsTrue(duplicate.IsEnabled);
                    duplicate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    (TextBox copyName, Button saveCopy, _) = ProfileDraftControls(profiles);
                    copyName.Text = copy.Name;
                    saveCopy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PumpMenuUntil(() => profiles.Items.OfType<ProfileRow>().Any(row => row.Name == copy.Name) && duplicate.IsEnabled);
                    Assert.AreEqual(copy.Name, profiles.SelectedValue);
                    PumpMenuBindings();
                    ProfileRow activeRow = profiles.Items.OfType<ProfileRow>().Single(row => row.IsActive);
                    ListBoxItem activeContainer = (ListBoxItem)profiles.ItemContainerGenerator.ContainerFromItem(activeRow);
                    ListBoxItem selectedContainer = (ListBoxItem)profiles.ItemContainerGenerator.ContainerFromItem(profiles.SelectedItem);
                    Assert.AreEqual(profile.Name, activeRow.Name);
                    Assert.IsFalse(activeContainer.IsSelected);
                    Assert.AreEqual(FontWeights.SemiBold, activeContainer.FontWeight);
                    Assert.AreEqual(CatalogManager.Catalog.GetString("Active"), AutomationProperties.GetItemStatus(activeContainer));
                    Assert.IsTrue(selectedContainer.IsSelected);
                    Assert.AreEqual(FontWeights.Normal, selectedContainer.FontWeight);
                    Assert.IsTrue(string.IsNullOrEmpty(AutomationProperties.GetItemStatus(selectedContainer)));
                    Assert.AreEqual(profile.Name, profiles.Items.OfType<ProfileRow>().Single(row => row.IsActive).Name);
                    ProfileToolboxSettingsModel preferences = copy.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
                    Assert.AreEqual("Black", preferences.ColorSettings[ColorSetting.Background]);
                    Assert.IsFalse(preferences.ViewSettings[MapContentType.Tracks]);
                    Assert.IsNull(preferences.RouteId);
                    Assert.IsNull(preferences.PathId);
                    Assert.IsNull(copy.LoadSettingsModel<ProfileSelectionsModel>(CancellationToken.None).GetAwaiter().GetResult().RouteId);
                    Assert.AreEqual(37, copy.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).GetAwaiter().GetResult().SoundVolumePercent);
                    Assert.AreEqual(keyboard.UserCommands[SimulatorCommand.CameraCab].UniqueDescriptor, copy.LoadSettingsModel<ProfileKeyboardSettingsModel>(CancellationToken.None).GetAwaiter().GetResult().UserCommands[SimulatorCommand.CameraCab].UniqueDescriptor);
                    Assert.AreEqual((byte)42, copy.LoadSettingsModel<ProfileRailDriverSettingsModel>(CancellationToken.None).GetAwaiter().GetResult().UserCommands[SimulatorCommand.GameSave]);
                    Assert.IsFalse(copy.LoadSettingsModel<ProfileDispatcherSettingsModel>(CancellationToken.None).GetAwaiter().GetResult().ContentTypeVisibility[MapContentType.Tracks]);
                    Assert.AreEqual(profile.Name, model.SettingsTool.ProfileName);
                    Assert.AreEqual(profile.Name, copy.Current(CancellationToken.None).GetAwaiter().GetResult().Name);

                    // Simulate a destination with its own saved route. Switching from a mapless shell must not restore it.
                    preferences.Folder = folder.Id;
                    preferences.RouteId = "saved-for-copy";
                    Assert.IsTrue(preferences.RestoreLastView);
                    _ = copy.UpdateSettingsModel(preferences, CancellationToken.None).GetAwaiter().GetResult();
                    routes.TrySetResult([]);
                    profiles.ScrollIntoView(profiles.SelectedItem);
                    PumpMenuBindings();
                    ListBoxItem copyRow = (ListBoxItem)profiles.ItemContainerGenerator.ContainerFromItem(profiles.SelectedItem);
                    Assert.IsNotNull(copyRow);
                    profiles.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = Control.MouseDoubleClickEvent,
                        Source = copyRow,
                    });
                    PumpMenuUntil(() => (reopened = PresentationSource.CurrentSources.Cast<PresentationSource>()
                        .Select(source => source.RootVisual).OfType<MainWindow>()
                        .FirstOrDefault(window => window.Dispatcher == shell.Dispatcher && !ReferenceEquals(window, shell))) != null
                        && ((MainWindowViewModel)reopened.DataContext).SettingsTool?.ProfileName == copy.Name && !shell.IsVisible);
                    Assert.IsNull(typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reopened));
                    Assert.AreEqual(copy.Name, copy.Current(CancellationToken.None).GetAwaiter().GetResult().Name);
                    SettingsToolWindowViewModel copySettings = ((MainWindowViewModel)reopened.DataContext).SettingsTool;
                    copySettings.ColorItems.Single(item => item.Setting == ColorSetting.Background).UserSelectColorName("White");
                    Assert.AreEqual("Black", profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).GetAwaiter().GetResult().ColorSettings[ColorSetting.Background]);

                    ProfileModel fallback = copy.RefreshProfiles(CancellationToken.None).GetAwaiter().GetResult()
                        .Where(item => item.Name != copy.Name).OrderBy(item => item.Name).First();
                    Task deletion = copySettings.ProfileManagement.DeleteActiveProfileAsync();
                    PumpMenuUntil(() => (fallbackWindow = PresentationSource.CurrentSources.Cast<PresentationSource>()
                        .Select(source => source.RootVisual).OfType<MainWindow>()
                        .FirstOrDefault(window => window.Dispatcher == shell.Dispatcher && !ReferenceEquals(window, reopened))) != null
                        && ((MainWindowViewModel)fallbackWindow.DataContext).SettingsTool?.ProfileName == fallback.Name && deletion.IsCompleted);
                    deletion.GetAwaiter().GetResult();
                    Assert.IsFalse(reopened.IsVisible);
                    Assert.IsNull(copy.RefreshProfiles(CancellationToken.None).GetAwaiter().GetResult().GetByName(copy.Name));
                    Assert.AreEqual(fallback.Name, copy.Current(CancellationToken.None).GetAwaiter().GetResult().Name);
                }
            }
            finally
            {
                try
                {
                    routes.TrySetResult([]);
                    MainWindow remaining = fallbackWindow ?? reopened ?? shell;
                    if (remaining?.IsVisible == true)
                    {
                        Task shutdown = (Task)typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(remaining, null);
                        PumpMenuUntil(() => shutdown.IsCompleted);
                        shutdown.GetAwaiter().GetResult();
                        remaining.Close();
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                    MenuRouteCache.RemoveRoutes(folder);
                    MenuContentCache.Restore(previous);
                    original.UpdateCurrent(CancellationToken.None).GetAwaiter().GetResult();
                    _ = copy.Delete(CancellationToken.None).GetAwaiter().GetResult();
                    _ = profile.Delete(CancellationToken.None).GetAwaiter().GetResult();
                }
            }
        }

        private static void PumpMenuUntil(Func<bool> condition)
        {
            DispatcherFrame frame = new DispatcherFrame();
            long start = Environment.TickCount64;
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) => frame.Continue = !condition() && Environment.TickCount64 - start < 2500;
            timer.Start();
            try
            {
                Dispatcher.PushFrame(frame);
                Assert.IsTrue(condition(), "The expected Toolbox window state was not reached.");
            }
            finally
            {
                timer.Stop();
            }
        }

        [STATestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void WhenFolderIsChangedInRoutesViewBeforeMapStartupThenItsRoutesAreShown()
        {
            FolderModel first = MenuFolder("First");
            FolderModel second = MenuFolder("Second");
            RouteModelHeader firstRoute = MenuRoute(first, "first", "First route");
            RouteModelHeader secondRoute = MenuRoute(second, "second", "Second route");
            MenuRouteCache.SetRoutes(first, Task.FromResult(ImmutableArray.Create(firstRoute)));
            MenuRouteCache.SetRoutes(second, Task.FromResult(ImmutableArray.Create(secondRoute)));
            // Cached test routes must not eagerly initialize the folder identity, unlike a cold startup.
            typeof(ModelBase).GetField("_hierarchy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(second, null);
            Task<ContentModel> previous = MenuContentCache.Replace(new ContentModel([first, second]) { Version = ContentModel.MinimumVersion });
            RouteBrowserViewModel browser = new RouteBrowserViewModel((_, _) => Assert.Fail("Folder selection must not start the map."));
            RouteToolView view = new RouteToolView { DataContext = browser };
            Window window = new Window { Content = view, ShowInTaskbar = false };
            try
            {
                browser.InitializeAsync(first.Id).GetAwaiter().GetResult();
                window.Show();
                PumpMenuBindings();
                StackPanel selectors = ((DockPanel)view.Content).Children.OfType<StackPanel>().Single();
                ComboBox folders = selectors.Children.OfType<ComboBox>().First();
                ComboBox routes = selectors.Children.OfType<ComboBox>().Last();
                Assert.IsTrue(folders.IsEnabled);
                Assert.AreEqual(first.Id, ((ToolboxMenuEntry)folders.SelectedItem).Key.FolderId);
                Assert.AreEqual(2, folders.Items.Count);
                folders.IsDropDownOpen = true;
                PumpMenuBindings();
                ((ComboBoxItem)folders.ItemContainerGenerator.ContainerFromIndex(1)).IsSelected = true;
                folders.IsDropDownOpen = false;
                PumpMenuBindings();
                Assert.AreEqual(second.Id, browser.SelectedFolder.Key.FolderId);
                Assert.AreSame(browser.SelectedFolder, folders.SelectedItem);
                Assert.AreEqual(1, routes.Items.Count);
                Assert.AreEqual(secondRoute.Id, ((ToolboxMenuEntry)routes.Items[0]).Key.RouteId);
                Assert.IsNull(browser.SelectedRoute);
                folders.IsDropDownOpen = true;
                PumpMenuBindings();
                ((ComboBoxItem)folders.ItemContainerGenerator.ContainerFromIndex(0)).IsSelected = true;
                folders.IsDropDownOpen = false;
                PumpMenuBindings();
                Assert.AreEqual(first.Id, browser.SelectedFolder.Key.FolderId);
                Assert.AreEqual(firstRoute.Id, ((ToolboxMenuEntry)routes.Items[0]).Key.RouteId);
            }
            finally
            {
                window.Close();
                browser.Stop();
                MenuRouteCache.RemoveRoutes(first);
                MenuRouteCache.RemoveRoutes(second);
                MenuContentCache.Restore(previous);
            }
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
        public async Task WhenContentBrowserStartsThenFoldersAndRoutesAreAvailableWithoutOpeningMap()
        {
            FolderModel folder = MenuFolder("Saved folder");
            RouteModelHeader route = MenuRoute(folder, "route", "Route");
            MenuRouteCache.SetRoutes(folder, Task.FromResult(ImmutableArray.Create(route)));
            Task<ContentModel> previous = MenuContentCache.Replace(new ContentModel([folder]) { Version = ContentModel.MinimumVersion });
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                RouteBrowserViewModel browser = null;
                Task discovery = null;
                int openings = 0;
                try
                {
                    await owner.OnOwnerAsync(() =>
                    {
                        browser = new RouteBrowserViewModel((selectedFolder, selectedRoute) =>
                        {
                            Assert.AreSame(folder, selectedFolder);
                            Assert.AreSame(route, selectedRoute);
                            ++openings;
                        });
                        discovery = browser.InitializeAsync(folder.Id);
                    }).ConfigureAwait(false);
                    await discovery.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await owner.OnOwnerAsync(() =>
                    {
                        Assert.AreSequenceEqual(new[] { folder.Id }, browser.ContentFolders.Select(entry => entry.Key.FolderId));
                        Assert.AreSequenceEqual(new[] { route.Id }, browser.Routes.Select(entry => entry.Key.RouteId));
                        Assert.AreEqual(folder.Id, browser.SelectedFolder.Key.FolderId);
                        Assert.AreEqual(folder.Name, browser.SelectedFolder.Name);
                        Assert.AreEqual(route.Description, browser.Routes[0].Description);
                        Assert.IsNull(browser.SelectedRoute);
                        Assert.IsNull(browser.Error);
                        Assert.AreEqual(0, openings);
                        browser.SelectedRoute = browser.Routes[0];
                        Assert.AreEqual(1, openings);
                        browser.Stop();
                        Assert.IsFalse(browser.Enabled);
                    }).ConfigureAwait(false);
                }
                finally
                {
                    await owner.OnOwnerAsync(() => browser?.Stop()).ConfigureAwait(false);
                    MenuRouteCache.RemoveRoutes(folder);
                    MenuContentCache.Restore(previous);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenFolderDiscoveryCompletesOutOfOrderThenOnlyCurrentFolderPublishesRoutes()
        {
            FolderModel first = MenuFolder("First");
            FolderModel second = MenuFolder("Second");
            RouteModelHeader oldRoute = MenuRoute(first, "old", "Old route");
            RouteModelHeader currentRoute = MenuRoute(second, "current", "Current route");
            TaskCompletionSource<ImmutableArray<RouteModelHeader>> oldDiscovery = new TaskCompletionSource<ImmutableArray<RouteModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
            MenuRouteCache.SetRoutes(first, oldDiscovery.Task);
            MenuRouteCache.SetRoutes(second, Task.FromResult(ImmutableArray.Create(currentRoute)));
            Task<ContentModel> previous = MenuContentCache.Replace(new ContentModel([first, second]) { Version = ContentModel.MinimumVersion });
            StaControlHost owner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (owner.ConfigureAwait(false))
            {
                RouteBrowserViewModel browser = null;
                Task initialize = null;
                try
                {
                    await owner.OnOwnerAsync(() =>
                    {
                        browser = new RouteBrowserViewModel((_, _) => Assert.Fail("Browsing must not open a map."));
                        initialize = browser.InitializeAsync(first.Id);
                    }).ConfigureAwait(false);
                    await owner.OnOwnerAsync(() => browser.SelectedFolder = browser.ContentFolders.Single(entry => entry.Key.FolderId == second.Id)).ConfigureAwait(false);
                    oldDiscovery.SetResult(ImmutableArray.Create(oldRoute));
                    await initialize.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await owner.OnOwnerAsync(() =>
                    {
                        Assert.AreEqual(second.Id, browser.SelectedFolder.Key.FolderId);
                        Assert.AreSequenceEqual(new[] { currentRoute.Id }, browser.Routes.Select(entry => entry.Key.RouteId));
                        browser.Stop();
                        browser.SelectedRoute = browser.Routes[0];
                        Assert.IsNull(browser.SelectedRoute);
                    }).ConfigureAwait(false);
                }
                finally
                {
                    oldDiscovery.TrySetResult([]);
                    await owner.OnOwnerAsync(() => browser?.Stop()).ConfigureAwait(false);
                    MenuRouteCache.RemoveRoutes(first);
                    MenuRouteCache.RemoveRoutes(second);
                    MenuContentCache.Restore(previous);
                }
            }
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
        [Timeout(10000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenManagementOnlySettingsAreSavedThenMapStateAndFreshPreferencesArePreserved(bool editPreferences)
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
            original.ColorSettings[ColorSetting.Background] = "Black";
            original.ViewSettings[MapContentType.Tracks] = false;
            await profile.UpdateSettingsModel(original, CancellationToken.None).ConfigureAwait(false);
            ProfileToolboxSettingsModel stale = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            original.FontOutline = false;
            original.ColorSettings[ColorSetting.RailTrack] = "White";
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
                    SetField(shell, "userSettings", new ProfileUserSettingsModel { LogLevel = System.Diagnostics.TraceEventType.Critical });
                    SettingsToolView view = new SettingsToolView();
                    SetField(shell, "SettingsPane", view);
                    typeof(MainWindow).GetMethod("InitializeToolWindowCommands", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                    typeof(MainWindow).GetMethod("InitializeShellSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                    MainWindowViewModel model = (MainWindowViewModel)typeof(MainWindow).GetField("viewModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
                    SettingsToolWindowViewModel settings = model.SettingsTool;
                    view.DataContext = settings;
                    PumpMenuBindings();
                    Assert.Contains(profile.Name, ((TextBlock)view.FindName("ActiveProfileLabel")).Text, StringComparison.Ordinal);
                    ComboBox language = (ComboBox)view.FindName("LanguagePicker");
                    Assert.IsGreaterThanOrEqualTo(1, language.Items.Count);
                    Assert.AreSame(settings.AvailableLanguages[0], language.SelectedItem);
                    Assert.IsFalse(settings.EnableLogging);
                    Assert.IsTrue(settings.RestoreLastView);
                    Assert.AreEqual("Black", settings.ColorItems.Single(item => item.Setting == ColorSetting.Background).SelectedColorName);
                    Assert.IsFalse(settings.TrackVisibilityItems.Single(item => item.Setting == MapContentType.Tracks).IsVisible);
                    if (editPreferences)
                    {
                        settings.RestoreLastView = false;
                        settings.RealTrackWidth = true;
                        settings.TrackVisibilityItems.Single(item => item.Setting == MapContentType.Tracks).IsVisible = true;
                    }

                    Assert.AreEqual(!editPreferences, stale.RestoreLastView, "The map will receive the same populated settings model.");
                    Assert.IsNull(typeof(MainWindow).GetField("MapHost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
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
            Assert.AreEqual("White", restored.ColorSettings[ColorSetting.RailTrack]);
            Assert.AreEqual(!editPreferences, restored.RestoreLastView);
            Assert.AreEqual(!editPreferences, restored.LimitTrackWidth);
            Assert.AreEqual(editPreferences, restored.ViewSettings[MapContentType.Tracks]);
        }

        [TestMethod]
        public void WhenShellXamlLoadsThenItContainsNoEagerHostAndExitDoesNotDependOnHostedMenu()
        {
            using (Stream stream = typeof(ToolboxSessionTests).Assembly.GetManifestResourceStream("Tests.FreeTrainSimulator.Toolbox.MainWindow.xaml"))
            {
                XDocument document = XDocument.Load(stream);
                XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
                Assert.AreEqual(0, document.Descendants().Count(element => element.Name.LocalName == nameof(ToolboxGameHostControl)));
                Assert.IsEmpty(document.Descendants().Where(element => element.Name.LocalName == "LayoutDocument"));
                XElement pane = document.Descendants().Single(element => element.Name.LocalName == "LayoutDocumentPane");
                Assert.IsEmpty(pane.Elements());
                XElement docking = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "DockingManager");
                Assert.IsNull(docking.Attribute("Visibility"), "The regular docking layout must remain visible without a map host.");
                Assert.IsEmpty(document.Descendants().Where(element => (string)element.Attribute(xaml + "Name") == "WorkspaceOverview"));
                XElement status = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "MainStatusBar");
                Assert.AreEqual("{Binding DataContext.WorkspaceStatus, ElementName=RootWindow}",
                    (string)status.Elements().Single(element => element.Name.LocalName == "TextBlock").Attribute("Text"));
                XElement menu = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == "MainMenu");
                Assert.IsNull(menu.Attribute("IsEnabled"));
                XElement exit = menu.Descendants().Single(element => (string)element.Attribute("Header") == "E_xit");
                Assert.AreEqual("{Binding DataContext.ExitCommand, ElementName=RootWindow}", (string)exit.Attribute("Command"));
            }
        }

        [STATestMethod]
        [DoNotParallelize]
        [Timeout(10000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenProfileIsCreatedFromToolboxThenLauncherSelectionAndSavedRouteArePreserved(bool useFallback)
        {
            CancellationToken cancellationToken = TestContext.CancellationToken;
            ProfileModel original = default(ProfileModel).Current(cancellationToken).GetAwaiter().GetResult();
            ProfileModel active = new ProfileModel("000-toolbox-active-" + Guid.NewGuid().ToString("N"));
            ProfileModel created = new ProfileModel("toolbox-new-" + Guid.NewGuid().ToString("N"));
            _ = active.Create(cancellationToken).GetAwaiter().GetResult();
            // A missing saved profile name would take over the selection as soon as that folder exists.
            (useFallback ? created : active).UpdateCurrent(cancellationToken).GetAwaiter().GetResult();
            Assert.AreEqual(active.Name, active.Current(cancellationToken).GetAwaiter().GetResult().Name);
            ProfileToolboxSettingsModel saved = active.LoadSettingsModel<ProfileToolboxSettingsModel>(cancellationToken).GetAwaiter().GetResult();
            saved.RouteId = "saved-route";
            _ = active.UpdateSettingsModel(saved, cancellationToken).GetAwaiter().GetResult();
            SynchronizationContext previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            SettingsToolWindowViewModel model = null;
            Window settingsWindow = null;
            try
            {
                MainWindow shell = CreateShutdownShell(null);
                SetField(shell, "currentProfile", active);
                SetField(shell, "toolboxSettings", saved);
                SetField(shell, "userSettings", new ProfileUserSettingsModel { LogLevel = System.Diagnostics.TraceEventType.Critical });
                SettingsToolView settings = new SettingsToolView();
                SetField(shell, "SettingsPane", settings);
                typeof(MainWindow).GetMethod("InitializeToolWindowCommands", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                typeof(MainWindow).GetMethod("InitializeShellSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                model = ((MainWindowViewModel)typeof(MainWindow).GetField("viewModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell)).SettingsTool;
                settings.DataContext = model;
                settingsWindow = new Window { Content = settings, Width = 480, Height = 480, ShowInTaskbar = false };
                settingsWindow.Show();
                ((TabItem)settings.FindName("ProfilesTab")).IsSelected = true;
                ListBox profiles = (ListBox)settings.FindName("ProfilesList");
                Button create = (Button)settings.FindName("CreateProfileButton");
                TextBlock status = (TextBlock)settings.FindName("ProfileStatus");
                PumpMenuUntil(() => create.IsEnabled);
                Assert.IsTrue(profiles.Items.OfType<ProfileRow>().Any(row => row.Name == active.Name));
                Assert.IsFalse(profiles.Items.OfType<ListBoxItem>().Any());
                create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                (TextBox name, _, _) = ProfileDraftControls(profiles);
                Assert.IsTrue(name.IsVisible);
                name.Text = created.Name;
                PressProfileDraftKey(name, Key.Escape);
                Assert.IsFalse(profiles.Items.OfType<ListBoxItem>().Any());
                Assert.AreEqual(active.Name, profiles.SelectedValue);
                Assert.IsNull(active.RefreshProfiles(cancellationToken).GetAwaiter().GetResult().GetByName(created.Name));

                create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                (name, Button save, _) = ProfileDraftControls(profiles);
                Assert.AreEqual(string.Empty, name.Text);
                name.Text = "CON";
                PressProfileDraftKey(name, Key.Enter);
                PumpMenuUntil(() => save.IsEnabled);
                Assert.Contains("valid profile name", status.Text, StringComparison.Ordinal);
                Assert.IsFalse(profiles.Items.OfType<ProfileRow>().Any(row => row.Name == "CON"));
                Assert.AreEqual("CON", name.Text);
                name.Text = active.Name.ToUpperInvariant();
                PressProfileDraftKey(name, Key.Enter);
                PumpMenuUntil(() => save.IsEnabled);
                Assert.Contains("already exists", status.Text, StringComparison.Ordinal);
                name.Text = created.Name;
                PressProfileDraftKey(name, Key.Enter);
                PumpMenuUntil(() => create.IsEnabled);
                Assert.IsFalse(profiles.Items.OfType<ListBoxItem>().Any());
                Assert.IsTrue(profiles.Items.OfType<ProfileRow>().Any(row => row.Name == created.Name));
                Assert.IsTrue(active.RefreshProfiles(cancellationToken).GetAwaiter().GetResult().Any(profile => profile.Name == created.Name));
                Assert.AreEqual(active.Name, active.Current(cancellationToken).GetAwaiter().GetResult().Name);
                Assert.AreEqual("saved-route", active.LoadSettingsModel<ProfileToolboxSettingsModel>(cancellationToken).GetAwaiter().GetResult().RouteId);
                Assert.IsNull(created.LoadSettingsModel<ProfileToolboxSettingsModel>(cancellationToken).GetAwaiter().GetResult().RouteId);
                Task deletion = model.ProfileManagement.DeleteOtherProfileAsync(created.Name, cancellationToken);
                PumpMenuUntil(() => deletion.IsCompleted);
                deletion.GetAwaiter().GetResult();
                Assert.IsNull(active.RefreshProfiles(cancellationToken).GetAwaiter().GetResult().GetByName(created.Name));
                Assert.AreEqual(active.Name, active.Current(cancellationToken).GetAwaiter().GetResult().Name);
                Assert.AreEqual("saved-route", active.LoadSettingsModel<ProfileToolboxSettingsModel>(cancellationToken).GetAwaiter().GetResult().RouteId);
            }
            finally
            {
                settingsWindow?.Close();
                SynchronizationContext.SetSynchronizationContext(previousContext);
                model?.Dispose();
                original.UpdateCurrent(CancellationToken.None).GetAwaiter().GetResult();
                _ = created.Delete(CancellationToken.None).GetAwaiter().GetResult();
                _ = active.Delete(CancellationToken.None).GetAwaiter().GetResult();
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
            settings.SettingsSelectedTabIndex = 1;
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
                    SetField(shell, "userSettings", new ProfileUserSettingsModel());
                    SettingsToolView settingsPane = new SettingsToolView();
                    SetField(shell, "SettingsPane", settingsPane);
                    typeof(MainWindow).GetMethod("InitializeShellSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                    Assert.AreEqual(1, settingsPane.SelectedTabIndex);
                    settingsPane.SelectedTabIndex = 2;
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
                Assert.AreEqual(2, restored.SettingsSelectedTabIndex);
            }
        }

        [STATestMethod]
        public void WhenSavedLayoutContainsMapThenShellRemovesOnlyMapDocumentUntilRequested()
        {
            MainWindow shell = CreateShutdownShell(null);
            AvalonDock.DockingManager manager = (AvalonDock.DockingManager)typeof(MainWindow)
                .GetField("DockingManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(shell);
            LayoutDocumentPane pane = manager.Layout.Descendents().OfType<LayoutDocumentPane>().First();
            pane.Children.Add(new AvalonDock.Layout.LayoutDocument { ContentId = "MapViewDocument" });
            MethodInfo floating = typeof(MainWindow).GetMethod("IsMapDocumentFloating", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo synchronize = typeof(MainWindow).GetMethod("SynchronizeMapDocument", BindingFlags.Instance | BindingFlags.NonPublic);
            synchronize.Invoke(shell, null);
            Assert.IsEmpty(pane.Children);
            Assert.AreSame(manager.Layout, pane.Root);
            Assert.IsFalse((bool)floating.Invoke(shell, null), "An absent map is a valid saved layout.");
            string saved = (string)typeof(MainWindow).GetMethod("SerializeDockLayout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
            typeof(MainWindow).GetMethod("DeserializeDockLayout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, [saved]);
            Assert.IsNotEmpty(manager.Layout.Descendents().OfType<AvalonDock.Layout.LayoutDocumentPane>());
            Assert.IsEmpty(manager.Layout.Descendents().OfType<AvalonDock.Layout.LayoutDocument>());
            ShutdownChartShell(shell);
        }

        [STATestMethod]
        public void WhenMapIsRequestedThenOneDocumentIsCreatedAndReattachedAfterLayoutReset()
        {
            ToolboxGameHostControl host = new ToolboxGameHostControl();
            MainWindow shell = CreateShutdownShell(host);
            AvalonDock.DockingManager manager = (AvalonDock.DockingManager)typeof(MainWindow)
                .GetField("DockingManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(shell);
            AvalonDock.Layout.LayoutDocumentPane pane = new AvalonDock.Layout.LayoutDocumentPane();
            manager.Layout = new LayoutRoot { RootPanel = new LayoutPanel() };
            manager.Layout.RootPanel.Children.Add(pane);
            MethodInfo synchronize = typeof(MainWindow).GetMethod("SynchronizeMapDocument", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                synchronize.Invoke(shell, null);
                LayoutContent map = pane.Children.Single();
                Assert.IsInstanceOfType<LayoutDocument>(map);
                Assert.AreEqual("MapViewDocument", map.ContentId);
                Assert.AreSame(host, map.Content);
                Assert.IsFalse(map.CanClose);
                Assert.IsFalse(map.CanFloat);
                synchronize.Invoke(shell, null);
                Assert.AreSame(map, pane.Children.Single());
                map.Content = null;
                pane.Children.Remove(map);
                synchronize.Invoke(shell, null);
                Assert.AreSame(host, pane.Children.Single().Content);
                Assert.IsNull(typeof(MainWindow).GetField("mapStartupTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
            }
            finally
            {
                ShutdownChartShell(shell);
                host.Dispose();
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