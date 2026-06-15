using System.Diagnostics;

using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Settings;
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
                value => toolboxSettings.FontOutline = value,
                value => toolboxSettings.LimitTrackWidth = !value);
        }

        [TestMethod]
        public void WhenEnableLoggingSetThenUserSettingsLogLevelChanges()
        {
            ProfileUserSettingsModel userSettings = new() { LogLevel = TraceEventType.Critical };
            SettingsToolWindowViewModel sut = new(CreateBridge(new ProfileToolboxSettingsModel(), userSettings));

            sut.EnableLogging = true;

            Assert.AreNotEqual(TraceEventType.Critical, userSettings.LogLevel);
        }

        [TestMethod]
        public void WhenRestoreLastViewSetThenToolboxSettingsChange()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { RestoreLastView = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(toolboxSettings, new ProfileUserSettingsModel()));

            sut.RestoreLastView = true;

            Assert.IsTrue(toolboxSettings.RestoreLastView);
        }

        [TestMethod]
        public void WhenFontOutlineSetThenToolboxSettingsChange()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(toolboxSettings, new ProfileUserSettingsModel()));

            sut.FontOutline = true;

            Assert.IsTrue(toolboxSettings.FontOutline);
        }

        [TestMethod]
        public void WhenRealTrackWidthSetThenLimitTrackWidthIsInverted()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { LimitTrackWidth = true };
            SettingsToolWindowViewModel sut = new(CreateBridge(toolboxSettings, new ProfileUserSettingsModel()));

            sut.RealTrackWidth = true;

            Assert.IsFalse(toolboxSettings.LimitTrackWidth);
        }

        [TestMethod]
        public void WhenSetThenOptimisticGetterReflectsValueImmediately()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(toolboxSettings, new ProfileUserSettingsModel()));

            sut.FontOutline = true;

            Assert.IsTrue(sut.FontOutline);
        }

        [TestMethod]
        public void WhenSetToSameValueThenSideEffectCallbackIsNotInvoked()
        {
            int writes = 0;
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = true };
            SettingsToolWindow bridge = new(
                toolboxSettings,
                new ProfileUserSettingsModel(),
                _ => writes++,
                _ => { });
            SettingsToolWindowViewModel sut = new(bridge);

            sut.FontOutline = true;

            Assert.AreEqual(0, writes);
        }

        [TestMethod]
        public void WhenStartThenGettersAreReSyncedFromModels()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(toolboxSettings, new ProfileUserSettingsModel()));
            toolboxSettings.FontOutline = true;

            sut.Start();

            Assert.IsTrue(sut.FontOutline);
        }

        [TestMethod]
        public void WhenStartReSyncsThenChangeNotificationIsRaised()
        {
            ProfileToolboxSettingsModel toolboxSettings = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(toolboxSettings, new ProfileUserSettingsModel()));
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
}