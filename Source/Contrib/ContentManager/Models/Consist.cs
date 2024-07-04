// COPYRIGHT 2015 by the Open Rails project.
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
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.ContentManager.Models
{
    public class Consist
    {
        public string Name { get; }

        public IEnumerable<ConsistCar> Cars { get; }

        public Consist(ContentBase content)
        {
            Debug.Assert(content?.Type == ContentType.Consist);
            if (System.IO.Path.GetExtension(content.PathName).Equals(".con", StringComparison.OrdinalIgnoreCase))
            {
                ConsistFile file = new ConsistFile(content.PathName);
                Name = file.Name;
                Cars = from car in file.Train.Wagons
                           select new ConsistCar(car);
            }
        }
    }

    public class ConsistCar
    {
        public string ID { get; }
        public string Name { get; }
        public Direction Direction { get; }

        internal ConsistCar(Wagon car)
        {
            ID = $"{car.UiD}";
            Name = car.Folder + "/" + car.Name;
            Direction = car.Flip ? Direction.Backward : Direction.Forward;
        }
    }

}
