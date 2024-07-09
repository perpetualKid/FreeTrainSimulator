using System;
using System.Windows.Forms;

[assembly: CLSCompliant(false)]

namespace FreeTrainSimulator.Updater
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (UpdaterProgress updater = (new UpdaterProgress()))
                Application.Run(updater);
        }
    }
}
