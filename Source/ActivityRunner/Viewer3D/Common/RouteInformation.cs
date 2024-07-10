using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal sealed class RouteInformation : DetailInfoBase
    {
        private bool replaySet;

        public RouteInformation()
        {
            this["Route Name"] = RuntimeData.Instance.RouteName;
            this["Metric Scale"] = RuntimeData.Instance.UseMetricUnits.ToString();
            this["Activity File"] = Simulator.Instance.ActivityFileName;
            this["Consist File"] = Simulator.Instance.ConsistFileName;
            this["Path File"] = Simulator.Instance.PathFileName;
            this["Path Name"] = Simulator.Instance.PathName;
            this["Season"] = Simulator.Instance.Season.GetLocalizedDescription();
            this["Timetable"] = Simulator.Instance.TimetableFileName;
            this["Weather type"] = Simulator.Instance.WeatherType.GetLocalizedDescription();

            this[".dynamic"] = null;
            this["Time"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                this["Time"] = MultiPlayerManager.MultiplayerState == MultiplayerState.Client ?
                    FormatStrings.FormatTime(Simulator.Instance.ClockTime + MultiPlayerManager.Instance().ServerTimeDifference) : FormatStrings.FormatTime(Simulator.Instance.ClockTime);
                if (Simulator.Instance.IsReplaying)
                {
                    this["Replay"] = FormatStrings.FormatTime(Simulator.Instance.Log.ReplayEndsAt - Simulator.Instance.ClockTime);
                    replaySet = true;
                }
                else if (replaySet)
                {
                    Remove("Replay");
                }
                base.Update(gameTime);
            }
        }
    }
}
