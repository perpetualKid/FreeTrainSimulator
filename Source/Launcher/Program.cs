using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

[assembly: CLSCompliant(true)]
namespace Orts.Launcher
{
    internal struct DependencyHint
    {
        public string Name;
        public string Url;
        public string Text;
    }

    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            string preferencesFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "prefernetfx");
            bool preferNetFx = File.Exists(preferencesFile);

            Application.EnableVisualStyles();

            List<DependencyHint> missingDependencies = new List<DependencyHint>();

            bool coreFx = CheckCoreFx(missingDependencies);

            if (!coreFx || preferNetFx)
            {
                DialogResult result;

                if (preferNetFx)
                {
                    if (MessageBox.Show($"You are running {Application.ProductName} on .NET Framework 4.8.\n\n" +
                        $"Please keep in mind there will be no further updates available on this version of {Application.ProductName} for .NET Framework 4.8. " +
                        "To switch to the fully supported mainstream version, click OK. \n\n" +
                        (coreFx ? string.Empty : "If nececessary we will guide you to download the required software.\n") +
                        "Click Cancel to ignore and continue.",
                        Application.ProductName, MessageBoxButtons.OKCancel) == DialogResult.OK)
                    {
                        try
                        {
                            File.Delete(preferencesFile);
                            if (coreFx)
                                preferNetFx = false;
                            else
                                DownloadDependency(missingDependencies[0]);
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        { }
                    }
                }
                else if (!coreFx)
                {
                    if ((result = MessageBox.Show($"Please read: \n\n{Application.ProductName} is now available as {missingDependencies[0].Name} version only. \n\n" +
                        $"While you can continue using {Application.ProductName} on .NET Framework 4.8, there will be no further development (including updates or patches also) for the .NET Framework 4.8 version.\n\n" +
                        $"Please follow instructions to download {missingDependencies[0].Name}. Once installed, no further action is needed.\n\n\n" +
                        "When you click OK, we will guide you to download the required software. \n\n" +
                        $"When clicking Cancel, you will not be asked to install {missingDependencies[0].Name}, but we will keep reminding you how to update to the {missingDependencies[0].Name} version.",
                        $"***Please Read***   {Application.ProductName}   ***Please Read***", MessageBoxButtons.OKCancel)) == DialogResult.OK)
                    {
                        DownloadDependency(missingDependencies[0]);
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        try
                        {
                            using (File.Create(preferencesFile)) { }
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        { }
                    }
                }
            }

            // Check for any missing components.
            string path = Path.GetDirectoryName(Application.ExecutablePath);
            if (preferNetFx)
            {
                Process.Start(Path.Combine(Path.Combine(path, "net48"), "Menu.exe")).WaitForExit();
            }
            else
            {
                Process.Start(Path.Combine(Path.Combine(path, "netcoreapp3.1"), "Menu.exe")).WaitForExit();
            }
        }

        private static void DownloadDependency(DependencyHint dependency)
        {
            Clipboard.SetText(dependency.Url);
            MessageBox.Show($"{dependency.Text} \n\nWhen you click OK, we will try to open a browser window pointing to the URL. " +
                "You can also open a browser window yourself now and paste the URL from clipboard (Ctrl + V).", dependency.Name);
            Process.Start(dependency.Url);
        }

        private static bool CheckCoreFx(List<DependencyHint> missingDependencies)
        {
            string coreFxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"dotnet\shared\Microsoft.NETCore.App");
            if (Directory.Exists(coreFxPath))
            {
                string[] versionFolders = Directory.GetDirectories(coreFxPath);
                foreach (string fxVersion in versionFolders)
                {
                    string[] fragments = Path.GetFileName(fxVersion).Split('.');
                    if (fragments.Length > 1)
                        if (double.TryParse($"{fragments[0]}{System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator}{fragments[1]}", out double version))
                            if (version >= 3.1)
                                return true;
                }
            }
            missingDependencies.Add(new DependencyHint()
            {
                Name = ("Microsoft .NET Core 3.1"),
                Text = "Please go to\n https://dotnet.microsoft.com/download/dotnet-core/3.1 \nto download the installation package " +
                "for Microsoft .NET Core 3.1 Desktop Runtime and install the software.",
                Url = "https://dotnet.microsoft.com/download/dotnet-core/3.1"
            });
            return false;
        }
    }
}