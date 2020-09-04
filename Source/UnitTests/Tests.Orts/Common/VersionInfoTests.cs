using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuGet.Versioning;

using Orts.Common;

namespace Tests.Orts.Common
{
    [TestClass]
    public class VersionInfoTests
    {
        [TestMethod]
        public void VersionTest()
        {
            //VersionInfo.FullVersion: "1.3.2-alpha.4+LocalBuild"
            //VersionInfo.Version: "1.3.2-alpha.4"
            //VersionInfo.FileVersion: "1.3.2.0"
            //VersionInfo.Channel: "alpha"
            //VersionInfo.Build: "4"
            //VersionInfo.CodeVersion: "LocalBuild"

            Assert.AreEqual(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductVersion, VersionInfo.FullVersion);
            Assert.AreEqual(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).FileVersion, VersionInfo.FileVersion);

            Assert.IsTrue(VersionInfo.FullVersion.IndexOf('+') >= 5);    // there should be a + sign for product metadata
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.Channel));
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.CodeVersion));
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.Build));
        }

        [TestMethod()]
        public void CompareTest()
        {
            Assert.AreEqual(1, VersionInfo.Compare("0"));   //Passing invalid version, so the current version should in each case be ahead
            Assert.AreEqual(1, VersionInfo.Compare(MinVersion().ToNormalizedString()));
            Assert.AreEqual(0, VersionInfo.Compare(VersionInfo.CurrentVersion.ToNormalizedString()));
            Assert.AreEqual(-1, VersionInfo.Compare(NextVersion().ToNormalizedString()));
        }

        private static NuGetVersion MinVersion()
        {
            return new NuGetVersion(0, 0, 0);

        }

        private static NuGetVersion NextVersion()
        {
            NuGetVersion current = VersionInfo.CurrentVersion;
            return new NuGetVersion(current.Major, current.Minor, current.Patch + 1, current.Release);

        }
    }
}