// COPYRIGHT 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using NuGet.Versioning;

namespace Orts.Common.Info
{

    /// <summary>
    /// Static class which provides version and build information about the whole game.
    /// </summary>
    public static class VersionInfo
    {
        private const string releaseChannelName = "release";

        public static readonly NuGetVersion CurrentVersion = GetVersion();
        //VersionInfo.FullVersion: "1.3.2-alpha.4+LocalBuild"
        //VersionInfo.Version: "1.3.2-alpha.4"
        //VersionInfo.FileVersion: "1.3.2.0"
        //VersionInfo.Channel: "alpha"
        //VersionInfo.Build: "4"
        //VersionInfo.Revision: "LocalBuild"

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns FullVersion: "1.3.2-alpha.4+LocalBuild"
        /// </summary>
        public static string FullVersion => CurrentVersion.ToFullString();

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns Version: "1.3.2-alpha.4"
        /// </summary>
        public static string Version => CurrentVersion.ToNormalizedString();

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns FileVersion: "1.3.2.0"
        /// </summary>
        public static string FileVersion => CurrentVersion.Version.ToString();

        /// <summary>
        /// <para/>"1.3.2-alpha.4+LocalBuild" returns Channel: "alpha"
        /// <para/>"1.3.2+LocalBuild" returns Channel: "release"
        /// </summary>
        public static string Channel => CurrentVersion.IsPrerelease ? CurrentVersion.ReleaseLabels?.ToArray()[0] : "release";

        /// <summary>
        /// <para/>"1.3.2-alpha.4+LocalBuild" returns Build: "4"
        /// <para/>"1.3.2+LocalBuild" returns Build: "0"
        /// <para/>"1.3.2.4+LocalBuild" returns Build: "4"
        /// </summary>
        public static string Build => CurrentVersion.IsPrerelease ? CurrentVersion.ReleaseLabels?.ToArray()[1] : $"{CurrentVersion.Revision}";

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns Revision: "LocalBuild"
        /// </summary>
        public static string CodeVersion => CurrentVersion.Metadata;

        private static NuGetVersion GetVersion()
        {
            if (!NuGetVersion.TryParse(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductVersion, out NuGetVersion result))
            {
                if (!NuGetVersion.TryParse(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion, out result))
                    result = NuGetVersion.Parse(new Version().ToString());
            }
            return result;
        }

        public static int Compare(string version)
        {
            if (!NuGetVersion.TryParse(version, out NuGetVersion result))
                return 1;
            return CurrentVersion.CompareTo(result);
        }

        /// <summary>
        /// Searchs the list of availableVersions for all upgrade options against the current version, 
        /// filtered to allow only targetChannel or higher prereleases and releases
        /// The result is sorted in descending order to get the most appropriate version first
        /// </summary>
        internal static List<NuGetVersion> SelectSuitableVersions(List<string> availableVersions, string targetVersion, string targetChannel)
        {
            if (availableVersions == null)
                throw new ArgumentNullException(nameof(availableVersions));

            List<NuGetVersion> result = new List<NuGetVersion>();
            IEnumerable<NuGetVersion> selection;
            foreach (string versionString in availableVersions)
            {
                if (NuGetVersion.TryParse(versionString, out NuGetVersion parsedVersion))
                    result.Add(parsedVersion);
            }
            if (!string.IsNullOrEmpty(targetVersion))
            {
                if (!NuGetVersion.TryParse(targetVersion, out NuGetVersion target))
                    throw new ArgumentException($"{targetVersion} is not a valid version for parameter {nameof(targetVersion)}");
                //compare against the current version and the target version
                selection = result.Where(
                    (version) => VersionComparer.VersionRelease.Compare(version, CurrentVersion) > 0 &&
                    VersionComparer.VersionRelease.Compare(version, target) <= 0);
            }
            else
            {
                //compare against the current version
                selection = result.Where((version) => VersionComparer.VersionRelease.Compare(version, CurrentVersion) > 0);
            }

            //filter the versions against the target channel
            selection = selection.Where((version) =>
            {
                List<string> releaseLabels = null;
                if (targetChannel == releaseChannelName)
                    return (!version.IsPrerelease);
                else
                {
                    if (version.IsPrerelease)
                    {
                        releaseLabels = version.ReleaseLabels.ToList();
                        releaseLabels[0] = targetChannel;
                    }
                }
                SemanticVersion other = new SemanticVersion(version.Major, version.Minor, version.Patch, releaseLabels, version.Metadata);
                return VersionComparer.VersionRelease.Compare(version, other) >= 0;
            });
            return selection.OrderByDescending((v) => v).ToList();
        }

        public static string SelectSuitableVersion(List<string> availableVersions, string targetChannel, string targetVersion = "")
        {
            List<NuGetVersion> versions = SelectSuitableVersions(availableVersions, targetVersion, targetChannel);
            return versions?.FirstOrDefault()?.ToNormalizedString();
        }

        internal static string ProductName()
        {
            string productName = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductName;
            if (string.IsNullOrEmpty(productName))
                productName = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductName;
            return productName;
        }

        /// <summary>
        /// Find whether a requested version is valid for this current version
        /// </summary>
        /// <param name="version">version to test again</param>
        /// <param name="youngestFailedToResume">highest version that failed to resume</param>
        /// <returns>true or false when able to determine validity, null otherwise</returns>
        public static bool? GetValidity(string version)
        {
            //TODO 20200910 bare minimum reimplementation, but versioning does not have reliable information about savepoint compatiblity
            if (NuGetVersion.TryParse(version, out NuGetVersion nugetVersion))
            {
                if (nugetVersion.Equals(CurrentVersion, VersionComparison.VersionRelease))
                    return true;
                return null;
            }
            return false; // default validity
        }
    }
}
