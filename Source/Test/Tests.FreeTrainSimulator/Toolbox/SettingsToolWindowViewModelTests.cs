using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class SettingsToolWindowViewModelTests
    {
        // Test double for the live-map side effects. It mirrors the model writes the production GameWindow
        // performs (so model-state assertions stay meaningful) and counts the calls the side-effect tests care
        // about. Item visibility is intentionally absent: the bridge writes that straight to the model.
        private sealed class FakeSettingsApplier : ISettingsApplier
        {
            private readonly ProfileToolboxSettingsModel toolboxSettings;
            private readonly ProfileUserSettingsModel userSettings;

            public FakeSettingsApplier(ProfileToolboxSettingsModel toolboxSettings, ProfileUserSettingsModel userSettings)
            {
                this.toolboxSettings = toolboxSettings;
                this.userSettings = userSettings;
            }

            public int FontOutlineWrites { get; private set; }

            public int ReapplyCount { get; private set; }

            public void ApplyEnableLogging(bool value)
                => userSettings.LogLevel = value ? TraceEventType.Verbose : TraceEventType.Critical;

            public void ApplyFontOutline(bool value)
            {
                FontOutlineWrites++;
                toolboxSettings.FontOutline = value;
            }

            public void ApplyRealTrackWidth(bool value)
                => toolboxSettings.LimitTrackWidth = !value;

            public void ApplyColorPreference(ColorSetting setting, string colorName)
                => toolboxSettings.ColorSettings[setting] = colorName;

            public void ReapplyAppearance()
                => ReapplyCount++;
        }

        private static SettingsToolWindow CreateBridge(ProfileToolboxSettingsModel toolboxSettings, ProfileUserSettingsModel userSettings)
            => new SettingsToolWindow(toolboxSettings, userSettings, new FakeSettingsApplier(toolboxSettings, userSettings));

        [TestMethod]
        public void WhenEnableLoggingSetThenUserSettingsLogLevelChanges()
        {
            ProfileUserSettingsModel userSettings = new ProfileUserSettingsModel()
            {
                LogLevel = TraceEventType.Critical
            };
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
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel() { FontOutline = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.FontOutline = true;

                Assert.IsTrue(sut.FontOutline);
            }
        }

        [TestMethod]
        public void WhenSetToSameValueThenSideEffectCallbackIsNotInvoked()
        {
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel() { FontOutline = true };
            FakeSettingsApplier applier = new FakeSettingsApplier(toolboxSettings, new ProfileUserSettingsModel());
            SettingsToolWindow bridge = new SettingsToolWindow(toolboxSettings, new ProfileUserSettingsModel(), applier);
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(bridge))
            {
                sut.FontOutline = true;

                Assert.AreEqual(0, applier.FontOutlineWrites);
            }
        }

        [TestMethod]
        public void WhenStartThenGettersAreReSyncedFromModels()
        {
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel() { FontOutline = false };
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
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel() { FontOutline = false };
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
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
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
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
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
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
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
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                toolboxSettings.ColorSettings[ColorSetting.RailTrack] = nameof(Microsoft.Xna.Framework.Color.Red);

                sut.Start();

                Assert.AreEqual(nameof(Microsoft.Xna.Framework.Color.Red), sut.ColorItems.Single(i => i.Setting == ColorSetting.RailTrack).SelectedColorName);
            }
        }

        [TestMethod]
        public void WhenResetToDefaultsThenColorRevertsToDefault()
        {
            string defaultColor = new ProfileToolboxSettingsModel().ColorSettings[ColorSetting.RailTrack];
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
            toolboxSettings.ColorSettings[ColorSetting.RailTrack] = nameof(Microsoft.Xna.Framework.Color.Red);
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.ResetToDefaults();

                Assert.AreEqual(defaultColor, sut.ColorItems.Single(i => i.Setting == ColorSetting.RailTrack).SelectedColorName);
            }
        }

        [TestMethod]
        public void WhenResetToDefaultsThenVisibilityRevertsToDefault()
        {
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
            toolboxSettings.ViewSettings[MapContentType.Tracks] = false;
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.ResetToDefaults();

                Assert.IsTrue(sut.TrackVisibilityItems.Single(i => i.Setting == MapContentType.Tracks).IsVisible);
            }
        }

        [TestMethod]
        public void WhenResetToDefaultsThenFontOutlineRevertsToDefault()
        {
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel() { FontOutline = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.ResetToDefaults();

                Assert.IsTrue(sut.FontOutline);
            }
        }

        [TestMethod]
        public void WhenResetToDefaultsThenRealTrackWidthRevertsToDefault()
        {
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel() { LimitTrackWidth = false };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(toolboxSettings, new ProfileUserSettingsModel())))
            {
                sut.ResetToDefaults();

                Assert.IsFalse(sut.RealTrackWidth);
            }
        }

        [TestMethod]
        public void WhenResetToDefaultsThenLoggingIsUnchanged()
        {
            ProfileUserSettingsModel userSettings = new ProfileUserSettingsModel() { LogLevel = TraceEventType.Verbose };
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(CreateBridge(new ProfileToolboxSettingsModel(), userSettings)))
            {
                sut.ResetToDefaults();

                Assert.AreEqual(TraceEventType.Verbose, userSettings.LogLevel);
                Assert.IsTrue(sut.EnableLogging);
            }
        }

        [TestMethod]
        public void WhenResetToDefaultsThenReapplyCallbackIsInvoked()
        {
            ProfileToolboxSettingsModel toolboxSettings = new ProfileToolboxSettingsModel();
            FakeSettingsApplier applier = new FakeSettingsApplier(toolboxSettings, new ProfileUserSettingsModel());
            SettingsToolWindow bridge = new SettingsToolWindow(toolboxSettings, new ProfileUserSettingsModel(), applier);
            using (SettingsToolWindowViewModel sut = new SettingsToolWindowViewModel(bridge))
            {
                sut.ResetToDefaults();

                Assert.AreEqual(1, applier.ReapplyCount);
            }
        }
    }
}