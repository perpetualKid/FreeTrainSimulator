// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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
using System.Collections;
using System.Diagnostics;
using System.IO;

namespace Orts.Formats.Msts.Files
{
    public static class TerrainFlagsFile
    {
        /// <summary>
        /// Returns an bit-array of vertex-hidden flags
        /// </summary>
        public static BitArray LoadTerrainFlagsFile(string fileName, int sampleCount)
        {
            BitArray result = new BitArray(sampleCount * sampleCount);
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(fileName))))
                    for (int z = 0; z < sampleCount; z++)
                        for (int x = 0; x < sampleCount; x++)
                            result[x * sampleCount + z] = (reader.ReadByte() & 0x04) == 0x04;
            }
            catch (IOException error)
            {
                Trace.WriteLine(new FileLoadException(fileName, error));
            }
            return result;
        }
    }
}
