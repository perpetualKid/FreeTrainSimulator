using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common.Logging;
using Orts.Settings;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Processes
{
    internal class GameStateViewer3DTest : GameState
    {
        private readonly Simulator simulator;
        private readonly Viewer viewer;

        public bool Passed { get; set; }
        public double LoadTime { get; set; }

        public GameStateViewer3DTest(Simulator simulator, Viewer viewer)
        {
            this.simulator = simulator;
            this.viewer = viewer;
        }

        internal override void Load()
        {
            Game.PopState();
        }

        protected override void Dispose(bool disposing)
        {
            ExportTestSummary(Passed, LoadTime);
            Environment.ExitCode = Passed ? 0 : 1;
            base.Dispose(disposing);
        }

        private void ExportTestSummary(bool passed, double loadTime)
        {
            // Append to CSV file in format suitable for Excel
            string summaryFileName = Path.Combine(UserSettings.UserDataFolder, "TestingSummary.csv");
            ORTraceListener traceListener = Trace.Listeners.OfType<ORTraceListener>().FirstOrDefault();
            // Could fail if already opened by Excel
            try
            {
                using (StreamWriter writer = File.AppendText(summaryFileName))
                {
                    // Route, Activity, Passed, Errors, Warnings, Infos, Load Time, Frame Rate
                    writer.WriteLine($"{simulator?.TRK?.Route?.Name?.Replace(",", ";") },{simulator?.Activity?.Activity?.Header?.Name?.Replace(",", ";")},{(passed ? "Yes" : "No")}," +
                        $"{traceListener?.EventCount(TraceEventType.Critical) ?? 0 + traceListener?.EventCount(TraceEventType.Error) ?? 0}," +
                        $"{traceListener?.EventCount(TraceEventType.Warning) ?? 0}," +
                        $"{traceListener?.EventCount(TraceEventType.Information) ?? 0},{loadTime:F1},{viewer?.RenderProcess?.FrameRate?.SmoothedValue:F1}");
                }
            }
            catch (IOException) { }// Ignore any errors
            catch (ArgumentNullException) { }// Ignore any errors
        }
    }
}
