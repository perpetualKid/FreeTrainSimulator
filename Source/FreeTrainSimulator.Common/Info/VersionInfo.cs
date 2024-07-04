using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using NuGet.Versioning;

namespace FreeTrainSimulator.Common.Info
{

    /// <summary>
    /// Static class which provides version and build information about the whole game.
    /// </summary>
    public static class VersionInfo
    {
        public const string PackageId = "FreeTrainSimulator";

        public static readonly NuGetVersion CurrentVersion = GetVersion();

        private static readonly NuGetVersion packageVersion = NuGetVersion.Parse(ThisAssembly.NuGetPackageVersion);

        //VersionInfo.FullVersion: "1.3.21-dev.6+631091b0"
        //VersionInfo.Version: "1.3.21-dev.6"
        //VersionInfo.FileVersion: "1.3.21.0"
        //VersionInfo.Channel: "dev"
        //VersionInfo.Build: "6"
        //VersionInfo.CodeVersion: "631091b0"

        /// <summary>
        /// "1.3.21-dev.6+631091b0" returns FullVersion: "1.3.21-dev.6+631091b0"
        /// </summary>
        public static string FullVersion => CurrentVersion.ToFullString();

        /// <summary>
        /// "1.3.21-dev.6+631091b0" returns Version: "1.3.21-dev.6"
        /// </summary>
        public static string Version => CurrentVersion.ToNormalizedString();

        /// <summary>
        /// "1.3.21-dev.6+631091b0" returns Channel: "dev"
        /// "1.3.21-rc.2" returns Channel: "rc"
        /// "1.3.21" returns Channel: "release"
        /// </summary>
        public static string Channel => CurrentVersion.IsPrerelease ? CurrentVersion.ReleaseLabels?.ToArray()[0] : "release";

        /// <summary>
        /// "1.3.21-dev.6+631091b0" returns Build: 6
        /// "1.3.21-rc.2" returns Build: 2
        /// "1.3.21" returns Build: 0
        /// </summary>
        public static string Build => CurrentVersion.IsPrerelease ? CurrentVersion.ReleaseLabels?.ToArray()[1] : $"{CurrentVersion.Revision}";

        /// <summary>
        /// "1.3.21-dev.6+631091b0" returns CodeVersion: 631091b0
        /// </summary>
        public static string CodeVersion => CurrentVersion.Metadata;

        private static NuGetVersion GetVersion()
        {
            if (!NuGetVersion.TryParse(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductVersion, out NuGetVersion result))
                if (!NuGetVersion.TryParse(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion, out result))
                    result = NuGetVersion.Parse(new Version().ToString());
            return result;
        }

        public static int Compare(string version)
        {
            if (!NuGetVersion.TryParse(version, out NuGetVersion result))
                return 1;
            return CurrentVersion.CompareTo(result);
        }

        public static NuGetVersion GetBestAvailableVersion(IEnumerable<NuGetVersion> availableVersions, bool includePrerelease)
        {
            return availableVersions.Where(v => includePrerelease || !v.IsPrerelease).
                OrderByDescending(v => v, VersionComparer.VersionReleaseMetadata).
                Take(1).
                Where(v => VersionComparer.VersionRelease.Compare(v, packageVersion) != 0).
                FirstOrDefault();
        }

        internal static string ProductName()
        {
            string productName = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductName;
            if (string.IsNullOrEmpty(productName))
                productName = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductName;
            return productName;
        }
    }
}
