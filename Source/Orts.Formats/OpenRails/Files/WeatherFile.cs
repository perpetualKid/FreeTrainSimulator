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
using System.Collections.ObjectModel;

using Orts.Formats.OpenRails.Models;
using Orts.Formats.OpenRails.Parsers;

namespace Orts.Formats.OpenRails.Files
{
    /// <summary>
    ///
    /// class ORWeatherFile
    /// </summary>

    public class WeatherFile
    {
        public Collection<WeatherConditionBase> Changes { get; } = new Collection<WeatherConditionBase>();
        public float TimeVariance { get; private set; }     // allowed max variation using random time setting
        public bool RandomSequence { get; private set; }    // set random sequence

        public WeatherFile(string fileName)
        {
            JsonReader.ReadFile(fileName, TryParse);
        }

        private bool TryParse(JsonReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader, nameof(reader));
            switch (reader.Path)
            {
                case "":
                case "[]":
                case "Changes[].":
                    // Ignore these items.
                    break;
                case "Changes[].Type":
                    switch (reader.AsString(""))
                    {
                        case "Clear":
                            Changes.Add(new OvercastCondition(reader));
                            break;
                        case "Precipitation":
                            Changes.Add(new PrecipitationCondition(reader));
                            break;
                        case "Fog":
                            Changes.Add(new FogCondition(reader));
                            break;
                        default:
                            return false;
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }
    }

}
