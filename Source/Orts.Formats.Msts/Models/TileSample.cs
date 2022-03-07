// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

using System;
using System.Diagnostics;
using System.IO;

using Orts.Common.Position;
using Orts.Formats.Msts.Files;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// Represents a single MSTS tile stored on disk, of whatever size (2KM, 4KM, 16KM or 32KM sqaure).
    /// </summary>
    [DebuggerDisplay("TileX = {TileX}, TileZ = {TileZ}, Size = {Size}")]
    public class TileSample
    {
        private readonly Tile tile;
        public ref readonly Tile Tile => ref tile;

        public int Size { get; }

        public bool Loaded { get { return TFile != null && YFile != null; } }
        public float Floor { get { return TFile.Terrain.Samples.SampleFloor; } }  // in meters
        public float Resolution { get { return TFile.Terrain.Samples.SampleScale; } }  // in meters per( number in Y-file )
        public int SampleCount { get { return TFile.Terrain.Samples.SampleCount; } }
        public float SampleSize { get { return TFile.Terrain.Samples.SampleSize; } }
        public int PatchCount { get { return TFile.Terrain.Patchsets[0].PatchSize; } }
#pragma warning disable CA1819 // Properties should not return arrays
        public Shader[] Shaders { get { return TFile.Terrain.Shaders; } }
#pragma warning restore CA1819 // Properties should not return arrays
        public float WaterNE { get { return TFile.Terrain.WaterLevelOffset.NE != 0 ? TFile.Terrain.WaterLevelOffset.NE : TFile.Terrain.WaterLevelOffset.SW; } } // in meters
        public float WaterNW { get { return TFile.Terrain.WaterLevelOffset.NW != 0 ? TFile.Terrain.WaterLevelOffset.NW : TFile.Terrain.WaterLevelOffset.SW; } }
        public float WaterSE { get { return TFile.Terrain.WaterLevelOffset.SE != 0 ? TFile.Terrain.WaterLevelOffset.SE : TFile.Terrain.WaterLevelOffset.SW; } }
        public float WaterSW { get { return TFile.Terrain.WaterLevelOffset.SW != 0 ? TFile.Terrain.WaterLevelOffset.SW : TFile.Terrain.WaterLevelOffset.SW; } }

        public bool ContainsWater
        {
            get
            {
                if (TFile.Terrain.WaterLevelOffset != null)
                    foreach (var patchset in TFile.Terrain.Patchsets)
                        foreach (var patch in patchset.Patches)
                            if (patch.WaterEnabled)
                                return true;
                return false;
            }
        }

        public Patch GetPatch(int x, int z)
        {
            return TFile.Terrain.Patchsets[0].Patches[z * PatchCount + x];
        }

        private readonly TerrainFile TFile;
        private readonly TerrainAltitudeFile YFile;
        private readonly TerrainFlagsFile FFile;

        public TileSample(string filePath, int tileX, int tileZ, TileHelper.Zoom zoom, bool visible)
        {
            if (!Directory.Exists(filePath))
                return;

            tile = new Tile(tileX, tileZ);
            Size = 1 << (15 - (int)zoom);

            string filePattern = TileHelper.FromTileXZ(tileX, tileZ, zoom);

            foreach (string fileName in Directory.EnumerateFiles(filePath, filePattern + "??.*"))
            {
                try
                {
                    switch (fileName)
                    {
                        case string t when t.EndsWith(".t", StringComparison.OrdinalIgnoreCase):
                            TFile = new TerrainFile(fileName);
                            break;
                        case string y when y.EndsWith("_y.raw", StringComparison.OrdinalIgnoreCase):
                            YFile = new TerrainAltitudeFile(fileName, SampleCount);
                            break;
                        case string f when f.EndsWith("_f.raw", StringComparison.OrdinalIgnoreCase):
                            FFile = new TerrainFlagsFile(fileName, SampleCount);
                            break;
                    }
                }
                catch (IOException exception)
                {
                    Trace.WriteLine(new FileLoadException(fileName, exception));
                }
            }
            // T and Y files are expected to exist; F files are optional.
            if (null == TFile && visible)
                Trace.TraceWarning("Ignoring missing tile {0}.t", filePattern);
        }

        public float GetElevation(int ux, int uz)
        {
            return (float)YFile.ElevationAt(ux, uz) * Resolution + Floor;
        }

        public bool IsVertexHidden(int ux, int uz)
        {
            return FFile != null && FFile.IsVertexHiddenAt(ux, uz);
        }
    }
}
