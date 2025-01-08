// COPYRIGHT 2017 by the Open Rails project.
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
using System.Diagnostics;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.DataValidator
{
    internal sealed class TerrainValidator : Validator
    {
        public TerrainValidator(string file)
            : base(file)
        {
            try
            {
                Terrain parsedTerrain = TerrainFile.LoadTerrainFile(File);
                if (File.Contains("\\lo_tiles\\", StringComparison.OrdinalIgnoreCase))
                {
                    Equal(TraceEventType.Warning, 64, parsedTerrain.Samples.SampleCount, "terrain_nsamples");
                    Equal(TraceEventType.Warning, 256, parsedTerrain.Samples.SampleSize, "terrain_sample_size");
                }
                else
                {
                    Equal(TraceEventType.Warning, 256, parsedTerrain.Samples.SampleCount, "terrain_nsamples");
                    Equal(TraceEventType.Warning, 8, parsedTerrain.Samples.SampleSize, "terrain_sample_size");
                }
                Equal(TraceEventType.Warning, 0, parsedTerrain.Samples.SampleRotation, "terrain_sample_rotation");
                ValidFileRef(TraceEventType.Error, parsedTerrain.Samples.SampleBufferE, "terrain_sample_ebuffer");
                ValidFileRef(TraceEventType.Error, parsedTerrain.Samples.SampleBufferN, "terrain_sample_nbuffer");
                ValidFileRef(TraceEventType.Error, parsedTerrain.Samples.SampleBufferY, "terrain_sample_ybuffer");
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception error)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Trace.WriteLine(error);
            }
        }
    }
}
