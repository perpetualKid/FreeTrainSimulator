using System.Diagnostics;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Common;

namespace Tests.Orts.Common
{
    [TestClass]
    public class VersionInfoTests
    {
        [TestMethod]
        public void VersionTest()
        {
            //VersionInfo.FullVersion: "1.3.0-alpha.0+LocalBuild"
            //VersionInfo.Version: "1.3.0-alpha.0"
            //VersionInfo.Revision: "alpha.0"
            //VersionInfo.FileVersion: "1.3.0.0"
            //VersionInfo.Channel: "LocalBuild"
            //VersionInfo.BuildType: "alpha"
            //VersionInfo.Build: "0"

            Assert.AreEqual(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductVersion, VersionInfo.FullVersion);
            Assert.AreEqual(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).FileVersion, VersionInfo.FileVersion);

            Assert.IsTrue(VersionInfo.FullVersion.IndexOf('+') > 5);    // there should be a + sign for product metadata
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.Revision));
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.Channel));
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.BuildType));
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.Build));
        }
    }
}
