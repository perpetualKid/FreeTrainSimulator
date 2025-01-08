using System;
using System.Diagnostics;
using System.IO;

[assembly: CLSCompliant(false)]

namespace FreeTrainSimulator.Launcher
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            _ = Process.Start(Path.Combine(AppContext.BaseDirectory, "net8.0-windows", "Menu.exe"));
        }
    }
}