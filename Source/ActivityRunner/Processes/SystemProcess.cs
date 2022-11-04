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
            gameHost.SystemInfo[StateType.Common] = new CommonInfo(gameHost);
            gameHost.SystemInfo[StateType.Clr] = new ClrEventListener();
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

                (gameHost.SystemInfo[StateType.Common] as DebugInfoBase).Update(gameTime);

                nextUpdate = gameTime.ElapsedGameTime.TotalSeconds + UpdateInterval;
            }
        }
    }
}
