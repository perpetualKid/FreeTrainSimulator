using FreeTrainSimulator.Common.DebugInfo;

using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal sealed class LocomotiveBrakeInformation : DetailInfoProxyBase
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
