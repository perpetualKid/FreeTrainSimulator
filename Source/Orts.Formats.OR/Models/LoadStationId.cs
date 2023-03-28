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

using System.Collections.Generic;
using System;

using Orts.Formats.OR.Parsers;

namespace Orts.Formats.OR.Models
{
    public class LoadStationId
    {
        public string WorldFile { get; private set; }
        public int UiD { get; private set; }

        public LoadStationId(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json);
            json.ReadBlock(TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            ArgumentNullException.ThrowIfNull(item);
            switch (item.Path)
            {
                case "wfile":
                    WorldFile = item.AsString("");
                    break;
                case "UiD":
                    UiD = item.AsInteger(0);
                    break;
                default:
                    return false;
            }
            return true;
        }
    }

    public class ContainerStationPopulation
    {
        public LoadStationId LoadStationId { get; private set; }

        public IReadOnlyCollection<LoadDataEntry> LoadData { get; } = new List<LoadDataEntry>();

        public ContainerStationPopulation(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json);
            json.ReadBlock(TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            switch (item.Path)
            {
                case "LoadData[]":
                    // Ignore these items.
                    break;
                case "LoadStationID.":
                    LoadStationId = new LoadStationId(item);
                    break;
                case "LoadData[].":
                    (LoadData as List<LoadDataEntry>).Add(new LoadDataEntry(item));
                    break;
                default:
                    return false;
            }
            return true;
        }
    }

    public class LoadDataEntry
    {
        public string FileName { get; private set; }
        public string FolderName { get; private set; }
        public int StackLocation { get; private set; }

        public LoadDataEntry(JsonReader json)
        {
            ArgumentNullException.ThrowIfNull(json);
            json.ReadBlock(TryParse);
        }

        private bool TryParse(JsonReader item)
        {
            switch (item.Path)
            {
                case "File":
                    FileName = item.AsString("");
                    break;
                case "Folder":
                    FolderName = item.AsString("");
                    break;
                case "StackLocation":
                    StackLocation = item.AsInteger(0);
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
