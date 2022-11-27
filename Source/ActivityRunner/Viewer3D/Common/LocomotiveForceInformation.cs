using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Calc;
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
            Next();
            Source = locomotive.LocomotiveForceInfo;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                base.Update(gameTime);
            }
        }

        public override void Next()
        {
            locomotive = Simulator.Instance.PlayerLocomotive.Train.Cars.OfType<MSTSLocomotive>().SkipWhile(x => x != locomotive).Skip(1).FirstOrDefault();
            if (locomotive == null)
                locomotive = Simulator.Instance.PlayerLocomotive.Train.Cars.OfType<MSTSLocomotive>().FirstOrDefault();
            Source = locomotive.LocomotiveForceInfo;
        }

        public override void Previous()
        {
            locomotive = Simulator.Instance.PlayerLocomotive.Train.Cars.OfType<MSTSLocomotive>().TakeWhile(x => x != locomotive).LastOrDefault();
            if (locomotive == null)
                locomotive = Simulator.Instance.PlayerLocomotive.Train.Cars.OfType<MSTSLocomotive>().LastOrDefault();
            Source = locomotive.LocomotiveForceInfo;
        }
    }
}
