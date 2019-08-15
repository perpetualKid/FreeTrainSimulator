using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Settings;

namespace Tests.Orts.Settings
{
    [TestClass]
    public class UserSettingsTests
    {
        [TestMethod]
        public void LogTest()
        {
            UserSettings settings = new UserSettings(null);
            settings.Log();
        }
    }
}
