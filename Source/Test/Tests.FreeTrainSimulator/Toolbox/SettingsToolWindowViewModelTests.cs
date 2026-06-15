using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Wpf.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class SettingsToolWindowViewModelTests
    {
        private sealed class SettingsState
        {
            public bool EnableLogging { get; set; }
            public bool RestoreLastView { get; set; }
            public bool FontOutline { get; set; }
            public bool RealTrackWidth { get; set; }
        }

        private static SettingsToolWindow CreateBridge(SettingsState state)
        {
            return new SettingsToolWindow(
                () => state.EnableLogging,
                () => state.RestoreLastView,
                () => state.FontOutline,
                () => state.RealTrackWidth,
                value => state.EnableLogging = value,
                value => state.RestoreLastView = value,
                value => state.FontOutline = value,
                value => state.RealTrackWidth = value);
        }

        [TestMethod]
        public void WhenEnableLoggingSetThenBridgeReceivesValue()
        {
            SettingsState state = new() { EnableLogging = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));

            sut.EnableLogging = true;

            Assert.IsTrue(state.EnableLogging);
        }

        [TestMethod]
        public void WhenRestoreLastViewSetThenBridgeReceivesValue()
        {
            SettingsState state = new() { RestoreLastView = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));

            sut.RestoreLastView = true;

            Assert.IsTrue(state.RestoreLastView);
        }

        [TestMethod]
        public void WhenFontOutlineSetThenBridgeReceivesValue()
        {
            SettingsState state = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));

            sut.FontOutline = true;

            Assert.IsTrue(state.FontOutline);
        }

        [TestMethod]
        public void WhenRealTrackWidthSetThenBridgeReceivesValue()
        {
            SettingsState state = new() { RealTrackWidth = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));

            sut.RealTrackWidth = true;

            Assert.IsTrue(state.RealTrackWidth);
        }

        [TestMethod]
        public void WhenSetThenOptimisticGetterReflectsValueImmediately()
        {
            SettingsState state = new() { EnableLogging = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));

            sut.EnableLogging = true;

            Assert.IsTrue(sut.EnableLogging);
        }

        [TestMethod]
        public void WhenSetToSameValueThenBridgeSetterIsNotCalled()
        {
            int writes = 0;
            SettingsToolWindow bridge = new(
                () => true,
                () => false,
                () => false,
                () => false,
                _ => writes++,
                _ => { },
                _ => { },
                _ => { });
            SettingsToolWindowViewModel sut = new(bridge);

            sut.EnableLogging = true;

            Assert.AreEqual(0, writes);
        }

        [TestMethod]
        public void WhenStartThenGettersAreReSyncedFromBridge()
        {
            SettingsState state = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));
            state.FontOutline = true;

            sut.Start();

            Assert.IsTrue(sut.FontOutline);
        }

        [TestMethod]
        public void WhenStartReSyncsThenChangeNotificationIsRaised()
        {
            SettingsState state = new() { FontOutline = false };
            SettingsToolWindowViewModel sut = new(CreateBridge(state));
            state.FontOutline = true;
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
