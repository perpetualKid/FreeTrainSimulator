using System.Diagnostics;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuGet.Versioning;

namespace Tests.FreeTrainSimulator.Common.Info
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

            Assert.IsTrue(VersionInfo.FullVersion.IndexOf('+', System.StringComparison.OrdinalIgnoreCase) >= 5);    // there should be a + sign for product metadata
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
#pragma warning disable CS0436 // Type conflicts with imported type
            Assert.AreEqual(ThisAssembly.AssemblyInformationalVersion, VersionInfo.FullVersion);
#pragma warning restore CS0436 // Type conflicts with imported type
        }

        [TestMethod()]
        public void AvailableVersionCompareCurrentTest()
        {
            NuGetVersion currentVersion = new NuGetVersion(
                VersionInfo.CurrentVersion.Major,
                VersionInfo.CurrentVersion.Minor,
                VersionInfo.CurrentVersion.Patch,
                VersionInfo.CurrentVersion.Revision,
#pragma warning disable CS0436 // Type conflicts with imported type
                ThisAssembly.IsPublicRelease ? VersionInfo.CurrentVersion.ReleaseLabels :
#pragma warning restore CS0436 // Type conflicts with imported type
                VersionInfo.CurrentVersion.ReleaseLabels.Concat(new string[] { "g" + VersionInfo.CurrentVersion.Metadata }), string.Empty);
            Assert.IsNull(VersionInfo.GetBestAvailableVersion(new NuGetVersion[] { currentVersion }, UpdateMode.PreRelease));
        }

        [TestMethod()]
        public void AvailableVersionCompareNextTest()
        {
            NuGetVersion currentVersion = new NuGetVersion(
                VersionInfo.CurrentVersion.Major,
                VersionInfo.CurrentVersion.Minor,
                VersionInfo.CurrentVersion.Patch,
                VersionInfo.CurrentVersion.Revision + 1,
#pragma warning disable CS0436 // Type conflicts with imported type
                ThisAssembly.IsPublicRelease ? VersionInfo.CurrentVersion.ReleaseLabels :
#pragma warning restore CS0436 // Type conflicts with imported type
                VersionInfo.CurrentVersion.ReleaseLabels.Concat(new string[] { "g" + VersionInfo.CurrentVersion.Metadata }), string.Empty);
            NuGetVersion available = VersionInfo.GetBestAvailableVersion(new NuGetVersion[] { currentVersion }, UpdateMode.PreRelease);
            Assert.IsNotNull(available);
            Assert.AreEqual(-1, VersionInfo.Compare(available.ToFullString()));
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