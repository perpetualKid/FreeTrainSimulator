using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Updater;
using Orts.Settings;
using System.IO;
using Orts.Common.Info;
using Orts.Common;

namespace Tests.Orts.Updater
{
    [TestClass]
    public class UpdateManagerTests
    {

        [TestMethod]
        public void CheckUpdateNeededTest()
        {
            try
            {
                File.Delete(UpdateManager.VersionFile);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            { }

            Assert.IsFalse(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Never));
            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Always));

            Assert.IsFalse(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Monthly));

            string versionFilePath = Path.Combine(RuntimeInfo.ConfigFolder, "version.json");

            using (File.Create(versionFilePath)) { }
            File.SetLastWriteTime(versionFilePath, DateTime.Now.AddDays(-20));

            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Daily));
            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Weekly));
            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Biweekly));
            Assert.IsFalse(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Monthly));
        }
    }
}
