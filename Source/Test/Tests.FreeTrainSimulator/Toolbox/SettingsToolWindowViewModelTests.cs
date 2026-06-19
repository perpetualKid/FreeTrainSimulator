using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class SettingsToolWindowViewModelTests
    {
        private static SettingsToolWindow CreateBridge(ProfileToolboxSettingsModel toolboxSettings, ProfileUserSettingsModel userSettings)
        {
            return new SettingsToolWindow(
                toolboxSettings,
                userSettings,
                value => userSettings.LogLevel = value ? TraceEventType.Verbose : TraceEventType.Critical,
                value => toolboxSettings.FontOutline = value,
                value => toolboxSettings.LimitTrackWidth = !value,
                (setting, visible) => toolboxSettings.ViewSettings[setting] = visible,
                (setting, colorName) => toolboxSettings.ColorSettings[setting] = colorName);
        }

        [TestMethod]
        public void WhenEnableLoggingSetThenUserSettingsLogLevelChanges()
        {
            ProfileUserSettingsModel userSettings = new() { LogLevel = TraceEventType.Critical };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(new ProfileToolboxSettingsModel(), userSettings)))
            {
                sut.EnableLogging = true;

                Assert.AreNotEqual(TraceEventType.Critical, userSettings.LogLevel);
            }
        }

        [TestMethod]
        public void WhenRestoreLastViewSetThenToolboxSettingsChange()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { RestoreLastView = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.RestoreLastView = true;

                Assert.IsTrue(toolboxSettings.RestoreLastView);
            }
        }

        [TestMethod]
        public void WhenFontOutlineSetThenToolboxSettingsChange()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.FontOutline = true;

                Assert.IsTrue(toolboxSettings.FontOutline);
            }
        }

        [TestMethod]
        public void WhenRealTrackWidthSetThenLimitTrackWidthIsInverted()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { LimitTrackWidth = true };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.RealTrackWidth = true;

                Assert.IsFalse(toolboxSettings.LimitTrackWidth);
            }
        }

        [TestMethod]
        public void WhenSetThenOptimisticGetterReflectsValueImmediately()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.FontOutline = true;

                Assert.IsTrue(sut.FontOutline);
            }
        }

        [TestMethod]
        public void WhenSetToSameValueThenSideEffectCallbackIsNotInvoked()
        {
            int writes = 0;
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = true };
            SettingsToolWindow bridge = new(
                toolboxSettings,
                new ProfileUserSettingsModel(),
                _ => { },
                _ => writes++,
                _ => { },
                (_, _) => { },
                (_, _) => { });
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(bridge))
            {
                sut.FontOutline = true;

                Assert.AreEqual(0, writes);
            }
        }

        [TestMethod]
        public void WhenStartThenGettersAreReSyncedFromModels()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                toolboxSettings.FontOutline = true;

                sut.Start();

                Assert.IsTrue(sut.FontOutline);
            }
        }

        [TestMethod]
        public void WhenStartReSyncsThenChangeNotificationIsRaised()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                toolboxSettings.FontOutline = true;
                bool raised = false;
                sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsToolWindowViewModel.FontOutline))
                    raised = true;
            };

                sut.Start();

                Assert.IsTrue(raised);
            }
        }

        [TestMethod]
        public void WhenVisibilityItemUncheckedThenViewSettingChanges()
        {
            ProfileToolboxSettingsModel toolboxSettings = new();
            toolboxSettings.ViewSettings[MapContentType.Tracks] = true;
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                VisibilityItemViewModel item = sut.TrackVisibilityItems.Single(i => i.Setting == MapContentType.Tracks);

                item.IsVisible = false;

                Assert.IsFalse(toolboxSettings.ViewSettings[MapContentType.Tracks]);
            }
        }

        [TestMethod]
        public void WhenColorItemChangedThenColorSettingChanges()
        {
            ProfileToolboxSettingsModel toolboxSettings = new();
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                ColorItemViewModel item = sut.ColorItems.Single(i => i.Setting == ColorSetting.RailTrack);

                item.SelectedColorName = nameof(Microsoft.Xna.Framework.Color.Red);

                Assert.AreEqual(nameof(Microsoft.Xna.Framework.Color.Red), toolboxSettings.ColorSettings[ColorSetting.RailTrack]);
            }
        }

        [TestMethod]
        public void WhenStartThenVisibilityItemsAreReSyncedFromModel()
        {
            ProfileToolboxSettingsModel toolboxSettings = new();
            toolboxSettings.ViewSettings[MapContentType.Tracks] = true;
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                toolboxSettings.ViewSettings[MapContentType.Tracks] = false;

                sut.Start();

                Assert.IsFalse(sut.TrackVisibilityItems.Single(i => i.Setting == MapContentType.Tracks).IsVisible);
            }
        }

        [TestMethod]
        public void WhenStartThenColorItemsAreReSyncedFromModel()
        {
            ProfileToolboxSettingsModel toolboxSettings = new();
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                toolboxSettings.ColorSettings[ColorSetting.RailTrack] = nameof(Microsoft.Xna.Framework.Color.Red);

                sut.Start();

                Assert.AreEqual(nameof(Microsoft.Xna.Framework.Color.Red), sut.ColorItems.Single(i => i.Setting == ColorSetting.RailTrack).SelectedColorName);
            }
        }
    }
}