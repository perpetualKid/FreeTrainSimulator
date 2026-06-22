using System.Collections.Generic;
using System.Drawing;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class DebugToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenRowsRemainEmpty()
        {
            TestInformationProvider provider = new TestInformationProvider();
            provider.DetailInfo["FPS"] = "60";

            DebugToolWindow sut = new DebugToolWindow(provider)
            {
                Active = false,
            };

            sut.RefreshSnapshot();

            Assert.IsEmpty(sut.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenActiveRefreshSnapshotThenRowsArePublished()
        {
            TestInformationProvider provider = new TestInformationProvider();
            provider.DetailInfo["FPS"] = "60";

            DebugToolWindow sut = new DebugToolWindow(provider)
            {
                Active = true,
            };

            sut.RefreshSnapshot();

            Assert.HasCount(1, sut.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenFormatColorIsSetThenSnapshotColorMatches()
        {
            TestInformationProvider provider = new TestInformationProvider();
            provider.DetailInfo["FPS"] = "60";
            provider.FormattingOptions["FPS"] = FormatOption.BoldRed;

            DebugToolWindow sut = new DebugToolWindow(provider)
            {
                Active = true,
            };

            sut.RefreshSnapshot();

            Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), sut.CaptureSnapshot().Rows[0].Color);
        }

        [TestMethod]
        public void WhenFormatBoldIsSetThenSnapshotBoldIsTrue()
        {
            TestInformationProvider provider = new TestInformationProvider();
            provider.DetailInfo["FPS"] = "60";
            provider.FormattingOptions["FPS"] = FormatOption.BoldRed;

            DebugToolWindow sut = new DebugToolWindow(provider)
            {
                Active = true,
            };

            sut.RefreshSnapshot();

            Assert.IsTrue(sut.CaptureSnapshot().Rows[0].Bold);
        }

        [TestMethod]
        public void WhenProviderIsNullThenSnapshotRowsRemainEmpty()
        {
            DebugToolWindow sut = new DebugToolWindow((INameValueInformationProvider)null)
            {
                Active = true,
            };

            sut.RefreshSnapshot();

            Assert.IsEmpty(sut.CaptureSnapshot().Rows);
        }

        private sealed class TestInformationProvider : INameValueInformationProvider
        {
            public InformationDictionary DetailInfo { get; } = new InformationDictionary();

            public Dictionary<string, FormatOption> FormattingOptions { get; } = new Dictionary<string, FormatOption>();
        }
    }
}
