using System;
using System.Diagnostics;

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
            Trace.Listeners.Clear();
            UserSettings settings = new UserSettings();
            settings.Log();
        }
    }
}
