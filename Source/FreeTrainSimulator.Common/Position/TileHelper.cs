using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FreeTrainSimulator.Common.Position
{
    public static class TileHelper
    {
        public enum TileZoom
        {
            Invalid = 0,
            /// <summary>
            /// 32KM^2
            /// </summary>
            DistantMountainLarge = 11,
            /// <summary>
            /// 16KM^2
            /// </summary>
            DistantMountainSmall = 12,
            /// <summary>
            /// 8KM^2 
            /// </summary>
            Normal = 13,    // not used
            /// <summary>
            /// 4KM^2
            /// </summary>
            Large = 14,
            /// <summary>
            /// 2KM^2
            /// </summary>
            Small = 15,
        }

        public static string TileFileName(in Tile tile, TileZoom zoom)
        {
            int rectX = -16384;
            int rectZ = -16384;
            int rectW = 16384;
            int rectH = 16384;
            StringBuilder name = new StringBuilder((int)zoom % 2 == 1 ? "-" : "_");
            int partial = 0;

            for (int z = 0; z < (int)zoom; z++)
            {
                bool east = tile.X >= rectX + rectW;
                bool north = tile.Z >= rectZ + rectH;
                partial <<= 2;
                partial += (north ? 0 : 2) + (east ^ north ? 0 : 1);
                if (z % 2 == 1)
                {
                    name.Append(partial.ToString("X", CultureInfo.InvariantCulture));
                    partial = 0;
                }
                if (east)
                    rectX += rectW;
                if (north)
                    rectZ += rectH;
                rectW /= 2;
                rectH /= 2;
            }
            if ((int)zoom % 2 == 1)
                name.Append((partial << 2).ToString("X", CultureInfo.InvariantCulture));
            return name.ToString();
        }

        /// <summary>
        /// Snap tile to the lower-left corner according to zoom setting
        /// </summary>
        public static Tile Snap(in Tile tile, TileZoom zoom)
        {
            int step = 15 - (int)zoom;
            int tileX = tile.X >> step;
            tileX <<= step;
            int tileZ = tile.Z >> step;
            tileZ <<= step;
            return new Tile(tileX, tileZ);
        }

        public static Tile FromWorldFileName(string fileName)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName);
            fileName = Path.GetFileNameWithoutExtension(fileName);

            return fileName.Length != 15 || 
                fileName[0] != 'w' || 
                (fileName[1] != '+' && fileName[1] != '-') || 
                (fileName[8] != '+' && fileName[8] != '-') ||
                !int.TryParse(fileName.AsSpan(1, 7), out int tileX) || !int.TryParse(fileName.AsSpan(8, 7), out int tileZ)
                ? throw new InvalidDataException($"WorldFile name {fileName} is not valid!")
                : new Tile(tileX, tileZ);
        }
    }
}
