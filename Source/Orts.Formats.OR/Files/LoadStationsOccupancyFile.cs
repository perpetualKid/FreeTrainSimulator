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

using Orts.Formats.OR.Models;
using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Files
{
    /// <summary>
    ///
    /// class ORWeatherFile
    /// </summary>

    public class LoadStationsOccupancyFile
    {
        public List<LoadStationOccupancy> LoadStationsOccupancy { get; } = new List<LoadStationOccupancy>();

        public LoadStationsOccupancyFile(string fileName)
        {
            JsonReader.ReadFile(fileName, TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            switch (item.Path)
            {
                case "":
                    break;
                case "ContainerStationsOccupancy[].":
                    break;
                case "ContainerStationsOccupancy[].LoadStationID.":
                    LoadStationsOccupancy.Add(new ContainerStationOccupancy(item));
                    break;
                case "ContainerStationsOccupancy[].LoadData[].":
                    break;
                case "ContainerStationsOccupancy[].LoadData[].File":
                    ContainerStationOccupancy contStationOccupancy = LoadStationsOccupancy[^1] as ContainerStationOccupancy;
                    contStationOccupancy.LoadData.Add(new LoadDataEntry(item));
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
