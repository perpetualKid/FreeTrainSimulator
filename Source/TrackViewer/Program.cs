using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using Orts.Common.Info;
using Orts.Common.Native;

namespace Orts.TrackViewer
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
#if NETCOREAPP
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#else
            NativeMethods.SetProcessDpiAwareness(NativeMethods.PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);

#endif
            Application.SetCompatibleTextRenderingDefault(false);
            using (GameWindow game = new GameWindow())
            {
                if (Debugger.IsAttached)
                {
                    game.Run();
                }
                else
                {
                    try
                    {
                        game.Run();
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        string errorSummary = ex.GetType().FullName + ": " + ex.Message;
                        string logFile = game.LogFileName;
                        if (string.IsNullOrEmpty(logFile))
                        {
                            MessageBox.Show($"A fatal error has occured and {RuntimeInfo.ApplicationFolder} cannot continue.\n\n" +
                                    $"    {errorSummary}\n\n" +
                                    $"This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ApplicationFolder} by reporting this error in our bug tracker at https://github.com/perpetualKid/ORTS-MG/issues. Since Logging is currently disable, please enable Logging in the Option Menu, run {RuntimeInfo.ApplicationFolder} again and watch for the log file produced.\n\n",
                                    $"{RuntimeInfo.ApplicationName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            DialogResult openTracker = MessageBox.Show($"A fatal error has occured and {RuntimeInfo.ApplicationFolder} cannot continue.\n\n" +
                                    $"    {errorSummary}\n\n" +
                                    $"This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ApplicationFolder} by reporting this error in our bug tracker at https://github.com/perpetualKid/ORTS-MG/issues and attaching the log file {logFile}.\n\n" +
                                    ">>> Click OK to report this error on the GitHub bug tracker <<<",
                                    $"{RuntimeInfo.ApplicationName} {VersionInfo.Version}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                            if (openTracker == DialogResult.OK)
                                SystemInfo.OpenBrowser("https://github.com/perpetualKid/ORTS-MG/issues");
                        }

                    }
                }
            }
        }
    }
}
