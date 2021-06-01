using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

[assembly: CLSCompliant(true)]
namespace Orts.Launcher
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            string path = Path.GetDirectoryName(Application.ExecutablePath);
            Process.Start(Path.Combine(path, "netcoreapp3.1", "Menu.exe"));
        }
    }
}