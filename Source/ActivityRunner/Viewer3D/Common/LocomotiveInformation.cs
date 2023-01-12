using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class LocomotiveInformation : DetailInfoProxy
    {
        private Train train;
        private int numberCars;

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                if (train != (train = Simulator.Instance.PlayerLocomotive.Train) | numberCars != (numberCars = train.Cars.Count))
                {
                    MultiColumnCount = 0;
                    MSTSLocomotive playerLocomotive = Simulator.Instance.PlayerLocomotive;
                    DetailInfoBase current = playerLocomotive.CarInfo.DetailInfo as DetailInfoBase;
                    Source = current;
                    current.NextColumn = null;

                    int count = 1;
                    foreach (TrainCar car in train.Cars.OfType<MSTSLocomotive>())
                    {
                        if (car == playerLocomotive)
                            continue;
                        current.NextColumn = car.CarInfo.DetailInfo as DetailInfoBase;
                        count++;
                        current = car.CarInfo.DetailInfo as DetailInfoBase;
                        current.NextColumn = null;
                    }
                    MultiColumnCount = count;
                }
                base.Update(gameTime);
            }
        }
    }
}
