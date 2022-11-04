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
            metric.Update(gameTime);
            if (gameTime.TotalGameTime.TotalSeconds > nextUpdate)
            {

                foreach (Profiler profiler in Profiler.ProfilingData)
                {
                    profiler?.Mark();
                }

                (gameHost.SystemInfo[DiagnosticInfo.System] as DebugInfoBase).Update(gameTime);

                nextUpdate = gameTime.TotalGameTime.TotalSeconds + UpdateInterval;
            }

        }
    }
}
