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

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.Orts")]

namespace Orts.Common
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
                return -1;
            return CurrentVersion.CompareTo(result);
        }

        /// <summary>
        /// Searchs the list of availableVersions for all upgrade options against the current version, 
        /// filtered to allow only targetChannel or higher prereleases and releases
        /// The result is sorted in descending order to the most appropriate version first
        /// </summary>
        internal static List<NuGetVersion>SelectSuitableVersions(List<string> availableVersions, string targetChannel)
        {
            if (availableVersions == null)
                throw new ArgumentNullException(nameof(availableVersions));

            List<NuGetVersion> result = new List<NuGetVersion>();
            foreach(string versionString in availableVersions)
            {
                if (NuGetVersion.TryParse(versionString, out NuGetVersion parsedVersion))
                    result.Add(parsedVersion);
            }
            //compare against the current version
            IEnumerable<NuGetVersion> selection = result.Where((version) => VersionComparer.VersionRelease.Compare(version, CurrentVersion) >= 0);
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
            result = selection.ToList();
            result.Sort();
            result.Reverse();
            return result;
        }

        internal static List<NuGetVersion> SelectSuitableVersions(List<string> availableVersions, string targetVersion, string targetChannel)
        {
            if (availableVersions == null)
                throw new ArgumentNullException(nameof(availableVersions));

            if (!NuGetVersion.TryParse(targetVersion, out NuGetVersion target))
                throw new ArgumentException($"{targetVersion} is not a valid version for parameter {nameof(targetVersion)}");

            List<NuGetVersion> result = new List<NuGetVersion>();
            foreach (string versionString in availableVersions)
            {
                if (NuGetVersion.TryParse(versionString, out NuGetVersion parsedVersion))
                    result.Add(parsedVersion);
            }
            //compare against the current version
            IEnumerable<NuGetVersion> selection = result.Where(
                (version) => VersionComparer.VersionRelease.Compare(version, CurrentVersion) >= 0 &&
                VersionComparer.VersionRelease.Compare(version, target) <= 0);

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
            result = selection.ToList();
            result.Sort();
            result.Reverse();
            return result;
        }

        public static string SelectSuitableVersion(List<string> availableVersions, string targetChannel)
        {
            List<NuGetVersion> versions = SelectSuitableVersions(availableVersions, targetChannel);
            return versions?.FirstOrDefault()?.ToNormalizedString();
        }

        public static string SelectSuitableVersion(List<string> availableVersions, string targetVersion, string targetChannel)
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
        /// Find whether a requested version and build are valid for this build 
        /// </summary>
        /// <param name="version">version to test again</param>
        /// <param name="build">build to test again</param>
        /// <param name="youngestFailedToResume">youngest build that failed to resume</param>
        /// <returns>true or false when able to determine validity, null otherwise</returns>
        public static bool? GetValidity(string version, string build, int youngestFailedToResume)
        {
            int revision = GetRevisionFromVersion(version);
            int.TryParse(CodeVersion, out int programRevision);
            //MessageBox.Show(String.Format("VersionInfo.Build = {0}, build = {1}, version = {2}, youngestFailedToResume = {3}", VersionInfo.Build, build, Version, youngestFailedToResume));
            if (revision != 0)  // compiled remotely by Open Rails
            {
                if (revision == programRevision)
                {
                    return true;
                }
                else
                {
                    if (revision > youngestFailedToResume        // 1. Normal situation
                    || programRevision < youngestFailedToResume) // 2. If an old version of OR is used, then attempt to load Saves
                                                                 //    which would be blocked by the current version of OR
                    {
                        return null;
                    }
                }
            }
            else  // compiled locally
            {
                if (build.EndsWith(Build))
                {
                    return true;
                }
                else
                {
                    return null;
                }
            }
            return false; // default validity
        }

        /// <summary>
        /// Find the revision number (e.g. 1648) from the full version (e.g. 0.9.0.1648 or X.1648 or X1648)
        /// </summary>
        /// <param name="version">full version</param>
        public static int GetRevisionFromVersion(string fullVersion)
        {
            var versionParts = fullVersion.Split('.');
            var revision = 0;
            try
            {
                var version = versionParts[versionParts.Length - 1];
                if (version.StartsWith("X"))
                    version = version.Substring(1);
                int.TryParse(version, out revision);
            }
            catch { }
            return revision;
        }
    }
}
