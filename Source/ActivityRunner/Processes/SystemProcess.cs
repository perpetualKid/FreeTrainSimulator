using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes.Diagnostics;
using Orts.Common;
using Orts.Common.DebugInfo;

namespace Orts.ActivityRunner.Processes
{
    internal class SystemProcess : ProcessBase
    {
        internal const double UpdateInterval = 0.25;

        private double nextUpdate;
        private readonly MetricCollector metric = MetricCollector.Instance;

        public SystemProcess(GameHost gameHost) : base(gameHost, "System")
        {
            gameHost.SystemInfo[DiagnosticInfo.System] = new SystemInfo(gameHost);
            gameHost.SystemInfo[DiagnosticInfo.Clr] = new ClrEventListener();
            Profiler.ProfilingData[ProcessType.System] = profiler;
        }

        protected override void Update(GameTime gameTime)
        {
            if (gameTime.TotalGameTime.TotalSeconds > nextUpdate)
            {
                metric.Update(gameTime);

                foreach (Profiler profiler in Profiler.ProfilingData)
                {
                    profiler?.Mark();
                }

                (gameHost.SystemInfo[DiagnosticInfo.System] as DebugInfoBase).Update(gameTime);

                nextUpdate = gameTime.TotalGameTime.TotalSeconds + UpdateInterval;
            }
            double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
            //need to capture them here so we got every single frame
            metric.Metrics[SlidingMetric.FrameRate].Update(elapsed, 1/elapsed);
            metric.Metrics[SlidingMetric.FrameTime].Update(elapsed, elapsed);

        }
    }
}
