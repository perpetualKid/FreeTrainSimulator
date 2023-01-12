using Orts.Common.DebugInfo;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class LocomotiveForceInformation: DetailInfoProxy
    {
        private MSTSLocomotive locomotive;

        public LocomotiveForceInformation() 
        {
            Source = (locomotive = Simulator.Instance.PlayerLocomotive.Train.NextOf(locomotive))?.LocomotiveForceInfo;
        }

        public override void Next()
        {
            Source = (locomotive = Simulator.Instance.PlayerLocomotive.Train.NextOf(locomotive))?.LocomotiveForceInfo;
        }

        public override void Previous()
        {
            Source = (locomotive = Simulator.Instance.PlayerLocomotive.Train.PreviousOf(locomotive))?.LocomotiveForceInfo;
        }
    }
}
