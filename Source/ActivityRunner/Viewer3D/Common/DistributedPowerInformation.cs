using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.Common
{
    internal class DistributedPowerInformation : DetailInfoProxyBase
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
                    DetailInfoBase current = (Simulator.Instance.PlayerLocomotive as MSTSDieselLocomotive)?.DistributedPowerInformation;
                    if (current == null)
                        return;
                    Source = current;
                    current.NextColumn = null;

                    int count = 1;
                    foreach (TrainCar car in train.Cars)
                    {
                        if (car is MSTSDieselLocomotive dieselLocomotive && dieselLocomotive.DistributedPowerInformation != current)
                        {
                            current.NextColumn = dieselLocomotive.DistributedPowerInformation;
                            count++;
                            current = dieselLocomotive.DistributedPowerInformation;
                            current.NextColumn = null;
                        }
                    }
                    MultiColumnCount = count;
                }
                base.Update(gameTime);
            }
        }
    }
}
