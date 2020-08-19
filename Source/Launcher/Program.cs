// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

/* ORTS Launcher
 * 
 * This is the program that users execute start ORTS.  
 * Its purpose is to check for required dependencies 
 * before launching the rest of the ORTS executables.
 * 
 * This program must be compiled with a minimum of dependencies
 * so that it is guaranteed to run.
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Orts.Launcher
{
    internal struct DependencyHint
    {
        public string Name;
        public string Url;
        public string Text;
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool preferCoreFx = File.Exists(Path.Combine(Application.ExecutablePath, "prefercorefx"));

            Application.EnableVisualStyles();

            List<DependencyHint> missingDependencies = new List<DependencyHint>();

            bool netFx = CheckNetFx(missingDependencies);
            bool coreFx = CheckCoreFx(missingDependencies, preferCoreFx);
            CheckDXRuntime(missingDependencies);

            if (missingDependencies.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var item in missingDependencies)
                    builder.AppendLine(item.Name);

                if (MessageBox.Show($"{Application.ProductName} requires the following:\n\n{builder}" +
                    "\nWhen you click OK, we will guide you to download the required software.\n" +
                    (missingDependencies.Count > 1 ? "If there are multiple items missing, you need to repeat this process until all dependencies are resolved.\n" : string.Empty) +
                    "Click Cancel to quit.",
                    Application.ProductName, MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    DownloadDependency(missingDependencies[0]);
                }
                return;
            }

            // Check for any missing components.
            var path = Path.GetDirectoryName(Application.ExecutablePath);
            List<string> missingORFiles = new List<string>();
            if (preferCoreFx)
            {
                if (coreFx && CheckORFolder(missingORFiles, Path.Combine(path, "netcoreapp3.1")))
                {
                    Process.Start(Path.Combine(Path.Combine(path, "netcoreapp3.1"), "Menu.exe")).WaitForExit();
                }
                else if (netFx && CheckORFolder(missingORFiles, Path.Combine(path, "net48")))
                {
                    Process.Start(Path.Combine(Path.Combine(path, "net48"), "Menu.exe")).WaitForExit();
                }
                else
                {
                    MessageBox.Show($"{Application.ProductName} is missing the following:\n\n{string.Join("\n", missingORFiles.ToArray())}\n\nPlease re-install the software.", Application.ProductName);
                }
            }
            else
            {
                if (netFx && CheckORFolder(missingORFiles, Path.Combine(path, "net48")))
                {
                    Process.Start(Path.Combine(Path.Combine(path, "net48"), "Menu.exe")).WaitForExit();
                }
                else if (coreFx && CheckORFolder(missingORFiles, Path.Combine(path, "netcoreapp3.1")))
                {
                    Process.Start(Path.Combine(Path.Combine(path, "netcoreapp3.1"), "Menu.exe")).WaitForExit();
                }
                else
                {
                    MessageBox.Show($"{Application.ProductName} is missing the following:\n\n{string.Join("\n", missingORFiles.ToArray())}\n\nPlease re-install the software.", Application.ProductName);
                }
            }
        }

        private static void DownloadDependency(DependencyHint dependency)
        {
            Clipboard.SetText(dependency.Url);
            MessageBox.Show($"{dependency.Text} \n\nWhen you click OK, we will try to open a browser window pointing to the URL. " +
                "You can also open a browser window yourself now and paste the URL from clipboard (Ctrl + V).", dependency.Name);
            Process.Start(dependency.Url);
        }

        static bool CheckNetFx(List<DependencyHint> missingDependencies)
        {
            using (var RK = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                if ((SafeReadKey(RK, "Install", 0) == 1) && (SafeReadKey(RK, "Release", 0) >= 528040))  //https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed#find-net-framework-versions-45-and-later-with-code
                    return true;

            missingDependencies.Add(new DependencyHint()
            {
                Name = ("Microsoft .NET Framework 4.8 or later"),
                Text = "Please go to\n https://dotnet.microsoft.com/download/dotnet-framework/net48 \nto download the installation package " +
                "for Microsoft .NET Framework 4.8 and install the software.",
                Url = "https://dotnet.microsoft.com/download/dotnet-framework/net48"
            });
            return false;
        }

        static bool CheckCoreFx(List<DependencyHint> missingDependencies, bool preferred)
        {
            string coreFxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"dotnet\shared\Microsoft.NETCore.App");
            if (Directory.Exists(coreFxPath))
            {
                string[] versionFolders = Directory.GetDirectories(coreFxPath);
                foreach(string fxVersion in versionFolders)
                {
                    var fragments = Path.GetFileName(fxVersion).Split('.');
                    if (fragments.Length > 1)
                        if (double.TryParse($"{fragments[0]}{System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator}{fragments[1]}", out double version))
                            if (version >= 3.1)
                                return true;
                }
            }
            if (preferred)
            {
                missingDependencies.Add(new DependencyHint()
                {
                    Name = ("Microsoft .NET Core 3.1"),
                    Text = "Please go to\n https://dotnet.microsoft.com/download/dotnet-core/3.1 \nto download the installation package " +
                    "for Microsoft .NET Core 3.1 Desktop Runtime and install the software.",
                    Url = "https://dotnet.microsoft.com/download/dotnet-framework/net48"
                });
            }
            return false;
        }

        static void CheckDXRuntime(List<DependencyHint> missingDependencies)
        {
            if (File.Exists(Path.Combine(Environment.SystemDirectory, "D3Dcompiler_43.dll")))       //there is a dependency in Monogame requiring the specific version of D3D compiler
                return;

            missingDependencies.Add(new DependencyHint()
            {
                Name = "DirectX 9 Runtime",
                Text = $"Please go to\n https://www.microsoft.com/en-us/download/details.aspx?id=35&nowin10 \nto download the web installer for " +
                "DirectX Runtime and install the software. While downloading and installing, you may uncheck the installation of MSN and Bing software.",
                Url = "https://www.microsoft.com/en-us/download/details.aspx?id=35&nowin10"
            });
        }

        static bool CheckORFolder(List<string> missingFiles, string path)
        {
            missingFiles.Clear();
            foreach (var file in new[] {
                // Required libraries:
                @"Native/X86/OpenAL32.dll",
                @"Native/X64/OpenAL32.dll",
                // Programs:
                "Menu.exe",
                "ActivityRunner.exe",
            })
            {
                if (!File.Exists(Path.Combine(path, file)))
                    missingFiles.Add($"File '{file}'");
            }
            return missingFiles.Count == 0;
        }

        static int SafeReadKey(RegistryKey key, string name, int defaultValue)
        {
            try
            {
                return (int)key.GetValue(name, defaultValue);
            }
            catch
            {
                return defaultValue;
                throw;
            }
        }
    }
}