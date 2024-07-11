using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using FreeTrainSimulator.Common.Calc;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Diagnostics
{
    public sealed class MetricCollector
    {
        private readonly Process process = Process.GetCurrentProcess();
        private TimeSpan lastCpuTime;

        public EnumArray<SmoothedData, SlidingMetric> Metrics { get; } = new EnumArray<SmoothedData, SlidingMetric>();

        private MetricCollector()
        {
            Metrics[SlidingMetric.ProcessorTime] = new SmoothedData();
            Metrics[SlidingMetric.FrameRate] = new SmoothedDataWithPercentiles();
            Metrics[SlidingMetric.FrameTime] = new SmoothedDataWithPercentiles();
        }

        public static MetricCollector Instance { get; } = new MetricCollector();

        public void Update([NotNull] GameTime gameTime)
        {
            double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
            double timeCpu = (process.TotalProcessorTime - lastCpuTime).TotalSeconds;
            lastCpuTime = process.TotalProcessorTime;

            Metrics[SlidingMetric.ProcessorTime].Update(elapsed, 100 * timeCpu / elapsed);
            Metrics[SlidingMetric.FrameRate].Update(elapsed, 1 / elapsed);
            Metrics[SlidingMetric.FrameTime].Update(elapsed, elapsed);
        }
    }
}
