using System;
using System.Diagnostics;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;

namespace Orts.ActivityRunner.Processes.Diagnostics
{
    internal class MetricCollector
    {
        private readonly Process process = Process.GetCurrentProcess();
        private TimeSpan lastCpuTime;

        public EnumArray<SmoothedData, SlidingMetric> Metrics { get; } = new EnumArray<SmoothedData, SlidingMetric>();

        private MetricCollector()
        {
            Metrics[SlidingMetric.ProcessorTime] = new SmoothedData();
            Metrics[SlidingMetric.FrameRate] = new SmoothedData();
            Metrics[SlidingMetric.FrameTime] = new SmoothedDataWithPercentiles();
        }

        public static MetricCollector Instance { get; } = new MetricCollector();

        internal void Update(GameTime gameTime)
        {
            double elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
            double timeCpu = (process.TotalProcessorTime - lastCpuTime).TotalMilliseconds;
            lastCpuTime = process.TotalProcessorTime;

            Metrics[SlidingMetric.ProcessorTime].Update(elapsed / 1000, 100 * timeCpu / elapsed);
            Metrics[SlidingMetric.FrameRate].Update(elapsed, 1 / elapsed);
            Metrics[SlidingMetric.FrameTime].Update(elapsed, elapsed);

        }
    }
}
