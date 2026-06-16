using FreeTrainSimulator.Common;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class HelpToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenRowsRemainEmpty()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = false,
            };

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(0, helpToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenActiveRefreshSnapshotThenAllCommandsArePublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(EnumExtension.GetValues<UserCommand>().Length, helpToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenSearchByCommandTextThenOnlyMatchingRowIsPublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("Help", HelpToolWindow.HelpSearchColumn.Command);

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(1, helpToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenSearchByCommandTextThenRowKeyMatchesConfiguredBinding()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("Help", HelpToolWindow.HelpSearchColumn.Command);

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(InputSettings.UserCommands[UserCommand.DisplayHelpWindow].ToString(), helpToolWindow.CaptureSnapshot().Rows[0].Value);
        }

        [TestMethod]
        public void WhenSearchByKeyTextThenOnlyMatchingRowIsPublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("Left", HelpToolWindow.HelpSearchColumn.Key);

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(1, helpToolWindow.CaptureSnapshot().Rows.Length);
        }

        [TestMethod]
        public void WhenSearchTextIsWhitespaceThenAllRowsArePublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("   ", HelpToolWindow.HelpSearchColumn.Command);

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(EnumExtension.GetValues<UserCommand>().Length, helpToolWindow.CaptureSnapshot().Rows.Length);
        }
    }
}
