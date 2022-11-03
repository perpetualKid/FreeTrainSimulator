using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.Design;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes.State;
using Orts.Common;
using Orts.Common.DebugInfo;

namespace Orts.ActivityRunner.Processes
{
    internal class SystemProcess : ProcessBase
    {
        double nextUpdate;

        public EnumArray<DebugInfoBase, StateType> SystemInfo { get; } = new EnumArray<DebugInfoBase, StateType>();

        public SystemProcess(GameHost gameHost) : base(gameHost, "System")
        {
            SystemInfo[StateType.Common] = new CommonInfo(gameHost);
            Profiler.ProfilingData[ProcessType.System] = profiler;
        }

        protected override void Update(GameTime gameTime)
        {
            if (gameTime.TotalGameTime.TotalSeconds > nextUpdate)
            {
                foreach (Profiler profiler in Profiler.ProfilingData)
                {
                    profiler?.Mark();
                }
                nextUpdate = gameTime.ElapsedGameTime.TotalSeconds + 0.25;
            }
            foreach(DebugInfoBase stateInfo in SystemInfo)
            {
                stateInfo.Update(gameTime);
            }
        }
    }
}
