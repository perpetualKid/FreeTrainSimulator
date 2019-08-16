using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Settings;
using Tests.Orts.Shared;

namespace Tests.Orts.Settings
{
    [TestClass]
    public class UpdateSettingsTests
    {
        [TestMethod]
        public void DefaultsTest()
        {
            UpdateSettings settings = new UpdateSettings();
            Assert.AreEqual(string.Empty, settings.URL);
            Assert.AreEqual(string.Empty, settings.ChangeLogLink);
            Assert.AreEqual(string.Empty, settings.Channel);
            Assert.AreEqual(TimeSpan.FromDays(1), settings.TTL);
        }

        [TestMethod]
        public void GetChannelsTest()
        {
            using (TestFile testFile = new TestFile(string.Empty))
            {

            }
                UpdateSettings settings = new UpdateSettings();
            Assert.AreEqual(string.Empty, settings.URL);
            Assert.AreEqual(string.Empty, settings.ChangeLogLink);
            Assert.AreEqual(string.Empty, settings.Channel);
            Assert.AreEqual(TimeSpan.FromDays(1), settings.TTL);
        }
    }
}
