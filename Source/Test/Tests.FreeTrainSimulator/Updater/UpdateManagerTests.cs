using System;
using System.IO;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Updater;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Updater
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

            using (File.Create(UpdateManager.VersionFile))
            { }
            File.SetLastWriteTime(UpdateManager.VersionFile, DateTime.Now.AddDays(-20));

            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Daily));
            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Weekly));
            Assert.IsTrue(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Biweekly));
            Assert.IsFalse(UpdateManager.CheckUpdateNeeded(UpdateCheckFrequency.Monthly));
        }
    }
}
