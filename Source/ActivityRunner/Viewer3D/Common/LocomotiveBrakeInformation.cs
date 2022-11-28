using Orts.Common.DebugInfo;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class LocomotiveBrakeInformation: DetailInfoProxy
    {
        private MSTSLocomotive locomotive;

        public LocomotiveBrakeInformation()
        {
            Source = (locomotive = Simulator.Instance.PlayerLocomotive.Train.NextOf(locomotive))?.LocomotiveBrakeInfo;
        }

        public override void Next()
        {
            Source = (locomotive = Simulator.Instance.PlayerLocomotive.Train.NextOf(locomotive))?.LocomotiveBrakeInfo;
        }

        public override void Previous()
        {
            Source = (locomotive = Simulator.Instance.PlayerLocomotive.Train.PreviousOf(locomotive))?.LocomotiveBrakeInfo;
        }
    }
}
