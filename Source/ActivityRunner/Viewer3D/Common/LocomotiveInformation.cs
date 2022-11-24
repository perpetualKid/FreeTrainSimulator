using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Models.Simplified;
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
                    DetailInfoBase current = playerLocomotive.DetailInfo as DetailInfoBase;
                    Source = current;
                    current.Next = null;

                    int count = 1;
                    foreach (TrainCar car in train.Cars.OfType<MSTSLocomotive>())
                    {
                        if (car == playerLocomotive)
                            continue;
                        current.Next = car.DetailInfo as DetailInfoBase;
                        count++;
                        current = car.DetailInfo as DetailInfoBase;
                        current.Next = null;
                    }
                    MultiColumnCount = count;
                }
                base.Update(gameTime);
            }
        }
    }
}
