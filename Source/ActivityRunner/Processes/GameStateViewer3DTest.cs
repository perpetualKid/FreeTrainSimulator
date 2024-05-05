using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Orts.ActivityRunner.Processes.Diagnostics;
using Orts.Common.Info;
using Orts.Common.Logging;
using Orts.Settings;
using Orts.Simulation;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class GameStateViewer3DTest : GameState
    {
        public bool Passed { get; set; }
        public double LoadTime { get; set; }

        public GameStateViewer3DTest()
        {
        }

        internal override ValueTask Load()
        {
            Game.PopState();
            return base.Load();
        }

        protected override void Dispose(bool disposing)
        {
            ExportTestSummary(Passed, LoadTime);
            Environment.ExitCode = Passed ? 0 : 1;
            base.Dispose(disposing);
        }

        private static void ExportTestSummary(bool passed, double loadTime)
        {
            // Append to CSV file in format suitable for Excel
            string summaryFileName = Path.Combine(RuntimeInfo.UserDataFolder, "TestingSummary.csv");
            ORTraceListener traceListener = Trace.Listeners.OfType<ORTraceListener>().FirstOrDefault();
            // Could fail if already opened by Excel
            try
            {
                using (StreamWriter writer = File.AppendText(summaryFileName))
                {
                    // Route, Activity, Passed, Errors, Warnings, Infos, Load Time, Frame Rate
                    writer.WriteLine($"{Simulator.Instance.Route?.Name?.Replace(",", ";", StringComparison.OrdinalIgnoreCase)},{Simulator.Instance.ActivityFile?.Activity?.Header?.Name?.Replace(",", ";", StringComparison.OrdinalIgnoreCase)},{(passed ? "Yes" : "No")}," +
                        $"{traceListener?.EventCount(TraceEventType.Critical) ?? 0 + traceListener?.EventCount(TraceEventType.Error) ?? 0}," +
                        $"{traceListener?.EventCount(TraceEventType.Warning) ?? 0}," +
                        $"{traceListener?.EventCount(TraceEventType.Information) ?? 0},{loadTime:F1},{MetricCollector.Instance.Metrics[SlidingMetric.FrameRate].SmoothedValue:F1}");
                }
            }
            catch (IOException) { }// Ignore any errors
            catch (ArgumentNullException) { }// Ignore any errors
        }
    }
}
