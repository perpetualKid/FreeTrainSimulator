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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D
{
    /// <summary>
    /// Provides a MRU cache of tile data for a given resolution.
    /// </summary>
    [DebuggerDisplay("Count = {Tiles.List.Count}, Zoom = {Zoom}")]
    public class TileManager
    {
        private const int MaximumCachedTiles = 8 * 8;
        private readonly string filePath;
        private readonly TileHelper.Zoom zoom;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        private TileList Tiles = new TileList(new List<TileSample>());

        /// <summary>
        /// Constructs a new TileManager for loading tiles from a specific path, either at high-resolution or low-resolution.
        /// </summary>
        /// <param name="filePath">Path of the directory containing the MSTS tiles</param>
        /// <param name="loTiles">Flag indicating whether the tiles loaded should be high-resolution (2KM and 4KM square) or low-resolution (16KM and 32KM square, for distant mountains)</param>
        public TileManager(string filePath, bool loTiles)
        {
            this.filePath = filePath;
            zoom = loTiles ? TileHelper.Zoom.DMSmall : TileHelper.Zoom.Small;
        }

        /// <summary>
        /// Loads a specific tile, if it exists and is not already loaded.
        /// </summary>
        /// <param name="tileX">MSTS TileX coordinate of the tile, or of a logical tile inside a larger physical tile</param>
        /// <param name="tileZ">MSTS TileZ coordinate of the tile, or of a logical tile inside a larger physical tile</param>
        /// <param name="visible">Flag indicating whether the tile being loaded should be considered "key" to the user experience, and thus whether issues loading it should be shown.</param>
        public void Load(int tileX, int tileZ, bool visible)
        {
            // Take the current list of tiles, evict any necessary so the new tile fits, load and add the new
            // tile to the list, and store it all atomically in Tiles.
            List<TileSample> tileList = new List<TileSample>(Tiles.List);
            while (tileList.Count >= MaximumCachedTiles)
                tileList.RemoveAt(0);

            // Check for 1x1 (or 8x8) tiles.
            TileHelper.Snap(ref tileX, ref tileZ, zoom);
            if (Tiles.ByXZ.ContainsKey(((uint)tileX << 16) + (uint)tileZ))
                return;

            TileSample newTile = new TileSample(filePath, tileX, tileZ, zoom, visible);
            if (newTile.Valid)
            {
                tileList.Add(newTile);
                Tiles = new TileList(tileList);
                return;
            }

            // Check for 2x2 (or 16x16) tiles.
            TileHelper.Snap(ref tileX, ref tileZ, zoom - 1);
            if (Tiles.ByXZ.ContainsKey(((uint)tileX << 16) + (uint)tileZ))
                return;

            newTile = new TileSample(filePath, tileX, tileZ, zoom - 1, visible);
            if (newTile.Valid)
            {
                tileList.Add(newTile);
                Tiles = new TileList(tileList);
                return;
            }
        }

        /// <summary>
        /// Loads, if it is not already loaded, and gets the tile for the specified coordinates.
        /// </summary>
        /// <param name="tileX">MSTS TileX coordinate of the tile, or of a logical tile inside a larger physical tile</param>
        /// <param name="tileZ">MSTS TileZ coordinate of the tile, or of a logical tile inside a larger physical tile</param>
        /// <param name="visible">Flag indicating whether the tile being loaded should be considered "key" to the user experience, and thus whether issues loading it should be shown.</param>
        /// <returns>The <c>Tile</c> covering the specified coordinates, if one exists and is loaded. It may be a single tile or quad tile.</returns>
        public TileSample LoadAndGetTile(int tileX, int tileZ, bool visible)
        {
            Load(tileX, tileZ, visible);
            return GetTile(tileX, tileZ);
        }

        /// <summary>
        /// Loads a specific tile, if it is not already loaded, and gets the elevation of the terrain at a specific location, interpolating between sample points.
        /// </summary>
        /// <param name="tileX">MSTS TileX coordinate</param>
        /// <param name="tileZ">MSTS TileZ coordinate</param>
        /// <param name="x">MSTS X coordinate within tile</param>
        /// <param name="z">MSTS Z coordinate within tile</param>
        /// <param name="visible">Flag indicating whether the tile being loaded should be considered "key" to the user experience, and thus whether issues loading it should be shown.</param>
        /// <returns>Elevation at the given coordinates</returns>
        public float LoadAndGetElevation(int tileX, int tileZ, float x, float z, bool visible)
        {
            // Normalize the coordinates to the right tile.
            while (x >= 1024) { x -= 2048; tileX++; }
            while (x < -1024) { x += 2048; tileX--; }
            while (z >= 1024) { z -= 2048; tileZ++; }
            while (z < -1024) { z += 2048; tileZ--; }

            Load(tileX, tileZ, visible);
            return GetElevation(new WorldLocation(tileX, tileZ, x, 0, z));
        }

        /// <summary>
        /// Gets, if it is loaded, the tile for the specified coordinates.
        /// </summary>
        /// <param name="tileX">MSTS TileX coordinate</param>
        /// <param name="tileZ">MSTS TileZ coordinate</param>
        /// <returns>The <c>Tile</c> covering the specified coordinates, if one exists and is loaded. It may be a single tile or quad tile.</returns>
        public TileSample GetTile(int tileX, int tileZ)
        {
            // Check for 1x1 (or 8x8) tiles.
            TileHelper.Snap(ref tileX, ref tileZ, zoom);
            if (Tiles.ByXZ.TryGetValue(((uint)tileX << 16) + (uint)tileZ, out TileSample tile) && tile.Valid && tile.Size == (1 << (15 - (int)zoom)))
                return tile;

            // Check for 2x2 (or 16x16) tiles.
            TileHelper.Snap(ref tileX, ref tileZ, zoom - 1);
            if (Tiles.ByXZ.TryGetValue(((uint)tileX << 16) + (uint)tileZ, out tile) && tile.Valid && tile.Size == (1 << (15 - (int)zoom + 1)))
                return tile;

            return null;
        }

        /// <summary>
        /// Gets the elevation of the terrain at a specific location, interpolating between sample points.
        /// </summary>
        /// <param name="location">MSTS coordinates</param>
        /// <returns>Elevation at the given coordinates</returns>
        public float GetElevation(in WorldLocation location)
        {
            WorldLocation currentLocation = location.Normalize();
            // Fetch the tile we're looking up elevation for; if it isn't loaded, no elevation.
            TileSample tile = GetTile(currentLocation.TileX, currentLocation.TileZ);
            if (tile == null)
                return 0;

            // Adjust x/z based on the tile we found - this may not be in the same TileX/Z as we requested due to large (e.g. 2x2) tiles.
            float x = currentLocation.Location.X + 1024 + 2048 * (currentLocation.TileX - tile.Tile.X);
            float z = currentLocation.Location.Z + 1024 + 2048 * (currentLocation.TileZ - tile.Tile.Z - tile.Size);
            z *= -1;

            // Convert x/z in meters to terrain tile samples and get the coordinates of the NW corner.
            x /= tile.SampleSize;
            z /= tile.SampleSize;
            int ux = (int)Math.Floor(x);
            int uz = (int)Math.Floor(z);

            // Start with the north west corner.
            float nw = GetElevation(tile, ux, uz);
            float ne = GetElevation(tile, ux + 1, uz);
            float sw = GetElevation(tile, ux, uz + 1);
            float se = GetElevation(tile, ux + 1, uz + 1);

            // Condition must match TerrainPatch.SetupPatchIndexBuffer's condition.
            if (((ux & 1) == (uz & 1)))
            {
                // Split NW-SE
                if ((x - ux) > (z - uz))
                    // NE side
                    return nw + (ne - nw) * (x - ux) + (se - ne) * (z - uz);
                // SW side
                return nw + (se - sw) * (x - ux) + (sw - nw) * (z - uz);
            }
            // Split NE-SW
            if ((x - ux) + (z - uz) < 1)
                // NW side
                return nw + (ne - nw) * (x - ux) + (sw - nw) * (z - uz);
            // SE side
            return se + (sw - se) * (1 - x + ux) + (ne - se) * (1 - z + uz);
        }

        /// <summary>
        /// Gets the elevation of the terrain at a specific sample point within a specific tile. Wraps to the edges of the next tile in each direction.
        /// </summary>
        /// <param name="tile">Tile for the sample coordinates</param>
        /// <param name="ux">X sample coordinate</param>
        /// <param name="uz">Z sample coordinate</param>
        /// <returns>Elevation at the given sample coordinates</returns>
        public float GetElevation(TileSample tile, int ux, int uz)
        {
            if (tile?.InsideTile(ux, uz) ?? throw new ArgumentNullException(nameof(tile)))
                return tile.GetElevation(ux, uz);

            // We're outside the sample range for the given tile, so we need to convert the ux/uz in to physical
            // position (in meters) so that we can correctly look up the tile and not have to worry if it is the same
            // sample resolution or not.
            float x = ux * tile.SampleSize;
            float z = 2048 * tile.Size - uz * tile.SampleSize;
            TileSample otherTile = GetTile(tile.Tile.X + (int)Math.Floor(x / 2048), tile.Tile.Z + (int)Math.Floor((z - 1) / 2048));
            if (otherTile != null)
            {
                int ux2 = (int)((x + 2048 * (tile.Tile.X - otherTile.Tile.X)) / otherTile.SampleSize);
                int uz2 = -(int)((z + 2048 * (tile.Tile.Z - otherTile.Tile.Z - otherTile.Size)) / otherTile.SampleSize);
                ux2 = Math.Min(ux2, otherTile.SampleCount - 1);
                uz2 = Math.Min(uz2, otherTile.SampleCount - 1);
                return otherTile.GetElevation(ux2, uz2);
            }

            // No suitable tile was found, so just use the nearest sample from the tile we started with. This means
            // that when we run out of terrain, we just repeat the last value instead of getting a vertical cliff.
            return tile.GetElevation(MathHelper.Clamp(ux, 0, tile.SampleCount - 1), (int)MathHelper.Clamp(uz, 0, tile.SampleCount - 1));
        }

        /// <summary>
        /// Gets the vertex-hidden flag of the terrain at a specific sample point within a specific tile. Wraps to the edges of the next tile in each direction.
        /// </summary>
        /// <param name="tile">Tile for the sample coordinates</param>
        /// <param name="ux">X sample coordinate</param>
        /// <param name="uz">Z sample coordinate</param>
        /// <returns>Vertex-hidden flag at the given sample coordinates</returns>
        public bool IsVertexHidden(TileSample tile, int ux, int uz)
        {
            if (tile?.InsideTile(ux, uz) ?? throw new ArgumentNullException(nameof(tile)))
                return tile.IsVertexHidden(ux, uz);

            // We're outside the sample range for the given tile, so we need to convert the ux/uz in to physical
            // position (in meters) so that we can correctly look up the tile and not have to worry if it is the same
            // sample resolution or not.
            float x = ux * tile.SampleSize;
            float z = 2048 * tile.Size - uz * tile.SampleSize;
            var otherTile = GetTile(tile.Tile.X + (int)Math.Floor(x / 2048), tile.Tile.Z + (int)Math.Floor((z - 1) / 2048));
            if (otherTile != null)
            {
                int ux2 = (int)((x + 2048 * (tile.Tile.X - otherTile.Tile.X)) / otherTile.SampleSize);
                int uz2 = -(int)((z + 2048 * (tile.Tile.Z - otherTile.Tile.Z - otherTile.Size)) / otherTile.SampleSize);
                return otherTile.IsVertexHidden(ux2, uz2);
            }

            // No suitable tile was found, so just return that the vertex is normal - i.e. visible.
            return false;
        }

        [DebuggerDisplay("Count = {List.Count}")]
        private class TileList
        {
            /// <summary>
            /// Stores tiles in load order, so eviction is predictable and reasonable.
            /// </summary>
            public readonly List<TileSample> List;

            /// <summary>
            /// Stores tiles by their TileX, TileZ location, so lookup is fast.
            /// </summary>
            public readonly Dictionary<uint, TileSample> ByXZ;

            public TileList(List<TileSample> list)
            {
                List = list;
                ByXZ = list.ToDictionary(t => ((uint)t.Tile.X << 16) + (uint)t.Tile.Z);
            }
        }
    }
}
