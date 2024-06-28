using System;
using System.Globalization;

using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Info;
using Orts.Simulation;

namespace Orts.ActivityRunner.Processes.Diagnostics
{
    internal sealed class SystemInfo : DetailInfoBase
    {
        private readonly GameHost gameHost;
        private readonly int processorCount = Environment.ProcessorCount;
        private readonly MetricCollector metricCollector = MetricCollector.Instance;

        public SystemInfo(GameHost game) : base(true)
        {
            gameHost = game;
            this["System Details"] = null;
            this[".0"] = null;
            this["Version"] = VersionInfo.Version;
            this["System Time"] = null;
            this["Game Time"] = null;
            this["OS"] = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
            this["Framework"] = $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";
            this["Adapter"] = $"{gameHost.GraphicsDevice.Adapter.Description} ({Common.Info.SystemInfo.GraphicAdapterMemoryInformation})";
            this["Resolution"] = gameHost.Window.ClientBounds.ToString();
            this["CPU"] = null;
            this["Memory"] = null;
            this[".0"] = null;
            this["Frame rate"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                this["System Time"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                this["Game Time"] = Simulator.Instance != null ? $"{FormatStrings.FormatTime(Simulator.Instance.ClockTime)}" : null;
                this["Frame rate"] = $"{metricCollector.Metrics[SlidingMetric.FrameRate].SmoothedValue:0}";
                this["CPU"] = $"{metricCollector.Metrics[SlidingMetric.ProcessorTime].SmoothedValue / processorCount:N0}% total / {metricCollector.Metrics[SlidingMetric.ProcessorTime].SmoothedValue:0}% of single core ({processorCount} logical cores)";
                this["Memory"] = $"{Environment.WorkingSet >> 20} MB";
                base.Update(gameTime);
            }
        }

    }
}
