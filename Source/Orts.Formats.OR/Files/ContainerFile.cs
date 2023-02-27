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

using Orts.Formats.OR.Models;
using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Files
{
    public class ContainerFile
    {
        public ContainerParameters ContainerParameters { get; private set; }

        public ContainerFile(string fileName)
        {
            JsonReader.ReadFile(fileName, TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            switch (item.Path)
            {
                case "":
                case "Container":
                case "Container.":
                    ContainerParameters = new ContainerParameters(item);
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
