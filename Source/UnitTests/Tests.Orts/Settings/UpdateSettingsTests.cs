using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Settings;
using Orts.Settings.Store;
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
            using (TestFile testFile = new TestFile("[Test1Settings]\r\n[Test2Settings]\r\n[Test3Settings]\r\n[Test4Settings]\r\n[Settings]\r\n[UpdateState]"))
            {
                UpdateSettings settings = new UpdateSettings(StoreType.Ini, testFile.FileName);
                var channels = settings.GetChannels();
                Assert.AreEqual(5, channels.Length);
                Assert.AreEqual("Test1", channels[0]);
                Assert.AreEqual(string.Empty, channels[4]);
            }
        }
    }
}
