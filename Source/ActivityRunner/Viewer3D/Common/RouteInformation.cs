using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Formats.Msts;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class RouteInformation: DetailInfoBase
    {
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
        }
    }
}
