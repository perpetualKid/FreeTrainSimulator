// COPYRIGHT 2012, 2013 by the Open Rails project.
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
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Files;

namespace Orts.Simulation.World
{
    public class ContainerManager
    {
        private readonly Simulator simulator;

        internal LoadStationsPopulationFile LoadStationsPopulationFile { get; private set; }
        public Dictionary<int, ContainerHandlingStation> ContainerStations { get; } = new Dictionary<int, ContainerHandlingStation>();
        public Collection<Container> Containers { get; } = new Collection<Container>();
        public Dictionary<string, Container> LoadedContainers { get; } = new Dictionary<string, Container>();

        public ContainerManager(Simulator simulator)
        {
            this.simulator = simulator;
        }

        public void LoadPopulationFromFile(string fileName)
        {
            LoadStationsPopulationFile = new LoadStationsPopulationFile(fileName);
        }

        public ContainerHandlingStation CreateContainerStation(WorldPosition shapePosition, int trackItemId, PickupObject pickupObject)
        {
            FuelPickupItem trackItem = simulator.FuelManager.FuelPickupItems[trackItemId];
            return new ContainerHandlingStation(shapePosition, trackItem, pickupObject);
        }

        public void Update()
        {
            foreach (ContainerHandlingStation containerStation in ContainerStations.Values)
                containerStation.Update();
        }
    }
}

