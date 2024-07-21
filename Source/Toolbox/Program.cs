using System;
using System.Diagnostics;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;

[assembly: CLSCompliant(false)]

namespace FreeTrainSimulator.Toolbox
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            try
            {
                using (GameWindow game = new GameWindow())
                {
                    game.Run();
                }
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                // Log the error first in case we're burning.
                Trace.WriteLine(new FatalException(ex));
                string errorSummary = ex.GetType().FullName + ": " + ex.Message;
                DialogResult openTracker = MessageBox.Show(
@$"A fatal error has occured and {RuntimeInfo.ApplicationName} cannot continue.

    {errorSummary}

This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ApplicationName} by reporting this error in our bug tracker at {LoggingUtil.BugTrackerUrl}. 
If Logging is enabled, please also attach or post the log file.

>>> Click OK to report this error on the GitHub bug tracker <<<",
                        $"{RuntimeInfo.ApplicationName} {VersionInfo.Version}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (openTracker == DialogResult.OK)
                    SystemInfo.OpenBrowser(LoggingUtil.BugTrackerUrl);
            }
        }
    }
}

