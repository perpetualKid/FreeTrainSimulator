// COPYRIGHT 2017, 2018 by the Open Rails project.
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

using System;
using System.Collections.Generic;

using Orts.Formats.OpenRails.Models;
using Orts.Formats.OpenRails.Parsers;

namespace Orts.Formats.OpenRails.Files
{
    /// <summary>
    ///
    /// class ORWeatherFile
    /// </summary>

    public class LoadStationsPopulationFile
    {
        public IReadOnlyCollection<ContainerStationPopulation> LoadStationsPopulation { get; } = new List<ContainerStationPopulation>();

        public LoadStationsPopulationFile(string fileName)
        {
            JsonReader.ReadFile(fileName, TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            switch (item.Path)
            {
                case "":
                    break;
                case "ContainerStationsPopulation[]":
                    // Ignore these items.
                    break;
                case "ContainerStationsPopulation[].":
                    (LoadStationsPopulation as List<ContainerStationPopulation>).Add(new ContainerStationPopulation(item));
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
