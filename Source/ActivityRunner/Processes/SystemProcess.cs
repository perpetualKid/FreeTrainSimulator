using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes.Diagnostics;
using Orts.Common.Calc;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class SystemProcess : ProcessBase
    {
        internal const double UpdateInterval = 0.25;

        private double nextUpdate;
        private readonly MetricCollector metric = MetricCollector.Instance;

        public List<DetailInfoBase> Updateables { get; } = new List<DetailInfoBase>();

        public SystemProcess(GameHost gameHost) : base(gameHost, "System")
        {
            gameHost.SystemInfo[DiagnosticInfo.System] = new SystemInfo(gameHost);
            gameHost.SystemInfo[DiagnosticInfo.Clr] = new ClrEventListener();
            gameHost.SystemInfo[DiagnosticInfo.ProcessMetric] = new PerformanceDetails();
            gameHost.SystemInfo[DiagnosticInfo.GpuMetric] = new GraphicMetrics();

            Profiler.ProfilingData[ProcessType.System] = profiler;

            Updateables.Add(gameHost.SystemInfo[DiagnosticInfo.System] as DetailInfoBase);
            Updateables.Add(gameHost.SystemInfo[DiagnosticInfo.ProcessMetric] as DetailInfoBase);
            Updateables.Add(gameHost.SystemInfo[DiagnosticInfo.GpuMetric] as DetailInfoBase);
        }

        protected override void Update(GameTime gameTime)
        {
            metric.Update(gameTime);
            if (gameTime.TotalGameTime.TotalSeconds > nextUpdate)
            {

                foreach (Profiler profiler in Profiler.ProfilingData)
                {
                    profiler?.Mark();
                }

                (gameHost.SystemInfo[DiagnosticInfo.Clr] as ClrEventListener).Update(gameTime);

                for (int i=0; i < Updateables.Count; i++)
                    Updateables[i].Update(gameTime);
                nextUpdate = gameTime.TotalGameTime.TotalSeconds + UpdateInterval;
            }
        }

        private class PerformanceDetails : DetailInfoBase
        {
            private readonly int processorCount = Environment.ProcessorCount;

            public PerformanceDetails()
            {
                this["Process Metrics"] = null;
                this[".0"] = null;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    this["CPU"] = $"{MetricCollector.Instance.Metrics[SlidingMetric.ProcessorTime].SmoothedValue / processorCount:N0} % total / {MetricCollector.Instance.Metrics[SlidingMetric.ProcessorTime].SmoothedValue:0} % single core";
                    this["Render process"] = $"{Profiler.ProfilingData[ProcessType.Render].CPU.SmoothedValue:N0} %";
                    this["Update process"] = $"{Profiler.ProfilingData[ProcessType.Updater].CPU.SmoothedValue:N0} %";
                    this["Loader process"] = $"{Profiler.ProfilingData[ProcessType.Loader].CPU.SmoothedValue:N0} %";
                    this["Sound process"] = $"{Profiler.ProfilingData[ProcessType.Sound].CPU.SmoothedValue:N0} %";
                    this["Background process"] = $"{Profiler.ProfilingData[ProcessType.System].CPU.SmoothedValue:N0} %";
                    this["Memory use"] = $"{Environment.WorkingSet / 1024 / 1024} Mb";
                    this["Frame rate (actual/P50/P95/P99)"] = $"{(int)MetricCollector.Instance.Metrics[SlidingMetric.FrameRate].Value} fps / {((int)(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate] as SmoothedDataWithPercentiles).SmoothedP50)} fps / {(int)(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate] as SmoothedDataWithPercentiles).SmoothedP95} fps / {(int)(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate] as SmoothedDataWithPercentiles).SmoothedP99} fps";
                    this["Frame time (actual/P50/P95/P99)"] = $"{MetricCollector.Instance.Metrics[SlidingMetric.FrameTime].Value * 1000:F1} ms / {(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime] as SmoothedDataWithPercentiles).SmoothedP50 * 1000:F1} ms / {(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime] as SmoothedDataWithPercentiles).SmoothedP95 * 1000:F1} ms / {(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime] as SmoothedDataWithPercentiles).SmoothedP99 * 1000:F1} ms";
                    base.Update(gameTime);
                }
            }
        }

    }
}
