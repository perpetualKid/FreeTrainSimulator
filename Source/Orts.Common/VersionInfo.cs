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
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using NuGet.Versioning;

namespace Orts.Common
{
    /// <summary>
    /// Static class which provides version and build information about the whole game.
    /// </summary>
    public static class VersionInfo
    {
        public static readonly NuGetVersion SemanticVersion = GetVersion();
        //VersionInfo.FullVersion: "1.3.2-alpha.4+LocalBuild"
        //VersionInfo.Version: "1.3.2-alpha.4"
        //VersionInfo.FileVersion: "1.3.2.0"
        //VersionInfo.Channel: "alpha"
        //VersionInfo.Build: "4"
        //VersionInfo.Revision: "LocalBuild"

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns FullVersion: "1.3.2-alpha.4+LocalBuild"
        /// </summary>
        public static string FullVersion => SemanticVersion.ToFullString();

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns Version: "1.3.2-alpha.4"
        /// </summary>
        public static string Version => SemanticVersion.ToNormalizedString();

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns FileVersion: "1.3.2.0"
        /// </summary>
        public static string FileVersion => SemanticVersion.Version.ToString();

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns Channel: "alpha"
        /// "1.3.2+LocalBuild" returns Channel: "release"
        /// </summary>
        public static string Channel => SemanticVersion.IsPrerelease ? SemanticVersion.ReleaseLabels?.ToArray()[0] : "release";

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns Build: "4"
        /// "1.3.2+LocalBuild" returns Build: "0"
        /// "1.3.2.4+LocalBuild" returns Build: "4"
        /// </summary>
        public static string Build => SemanticVersion.IsPrerelease ? SemanticVersion.ReleaseLabels?.ToArray()[1] : $"{SemanticVersion.Revision}";

        /// <summary>
        /// "1.3.2-alpha.4+LocalBuild" returns Revision: "LocalBuild"
        /// </summary>
        public static string CodeVersion => SemanticVersion.Metadata;

        /// <summary>Revision number, e.g. Release: "1648",       experimental: "1649",   local: ""</summary>
//        public static readonly string Revision = GetRevision("Revision.txt");
        /// <summary>Full version number, e.g. Release: "0.9.0.1648", experimental: "X.1649", local: ""</summary>
//        public static readonly string Version = GetVersion("Version.txt");
        /// <summary>Full build number, e.g. "0.0.5223.24629 (2014-04-20 13:40:58Z)"</summary>
//        public static readonly string Build = GetBuild("Orts.Common.dll", "OpenRails.exe", "Menu.exe", "ActivityRunner.exe");
        /// <summary>Version, but if "", returns Build</summary>

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
            return SemanticVersion.CompareTo(result);
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
