// COPYRIGHT 2011, 2012, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D
{
    public class RoadCarViewer
    {
        private readonly Viewer Viewer;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        private Dictionary<RoadCar, RoadCarPrimitive> Cars = new Dictionary<RoadCar, RoadCarPrimitive>();
        public List<RoadCar> VisibleCars = new List<RoadCar>();

        public RoadCarViewer(Viewer viewer)
        {
            Viewer = viewer;
        }

        public void Load()
        {
            var cancellation = Viewer.LoaderProcess.CancellationToken;
            var visibleCars = VisibleCars;
            var cars = Cars;
            if (visibleCars.Any(c => !cars.ContainsKey(c)) || cars.Keys.Any(c => !visibleCars.Contains(c)))
            {
                var newCars = new Dictionary<RoadCar, RoadCarPrimitive>();
                foreach (var car in visibleCars)
                {
                    if (cancellation.IsCancellationRequested)
                        break;
                    if (cars.TryGetValue(car, out RoadCarPrimitive value))
                        newCars.Add(car, value);
                    else
                        newCars.Add(car, LoadCar(car));
                }
                Cars = newCars;
            }
        }

        public void LoadPrep()
        {
            // TODO: Maybe optimise this with some serial numbers?
            var visibleCars = VisibleCars;
            var newVisibleCars = new List<RoadCar>(visibleCars.Count);
            foreach (var tile in Viewer.World.Scenery.WorldFiles)
                foreach (var spawner in tile.CarSpawners)
                    newVisibleCars.AddRange(spawner.Cars);
            VisibleCars = newVisibleCars;
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            foreach (var car in Cars.Values)
                car.PrepareFrame(frame, elapsedTime);
        }

        private RoadCarPrimitive LoadCar(RoadCar car)
        {
            return new RoadCarPrimitive(Viewer, car);
        }

        internal void Mark()
        {
            var cars = Cars;
            foreach (var car in cars.Values)
                car.Mark();
        }
    }

    public class RoadCarPrimitive
    {
        private readonly RoadCar Car;
        private readonly RoadCarShape CarShape;

        public RoadCarPrimitive(Viewer viewer, RoadCar car)
        {
            Car = car;
            CarShape = new RoadCarShape(viewer.Simulator.CarSpawnerLists[Car.CarSpawnerListIdx][car.Type].Name, car);
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            //// TODO: Add 0.1f to Y to put wheels above road. Matching MSTS?
            //var front = Car.FrontLocation;
            //var rear = Car.RearLocation;
            //var frontY = front.Y;
            //var rearY = rear.Y;
            //if (Car.IgnoreXRotation)
            //{
            //    frontY = frontY - RoadCar.VisualHeightAdjustment;
            //    rearY = rearY - RoadCar.VisualHeightAdjustment;
            //    if (Math.Abs(frontY - rearY) > 0.01f)
            //    {
            //        if (frontY > rearY) rearY = frontY;
            //        else frontY = rearY;
            //    }
            //}
            //CarShape.Location = new WorldPosition(Car.TileX, Car.TileZ, Simulator.XNAMatrixFromMSTSCoordinates(front.X, frontY, front.Z, rear.X, rearY, rear.Z));
            CarShape.PrepareFrame(frame, elapsedTime);
        }

        internal void Mark()
        {
            CarShape.Mark();
        }
    }
}
