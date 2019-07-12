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

/// This module ...
/// 
/// Author: Stéfan Paitoni
/// Updates : 
/// 

using System.Collections.Generic;
using System.Linq;

namespace Orts.ActivityEditor.Base
{
    public class AreaRoute
    {
        private float minX;
        private float minY;

        public float MaxX { get; set; }
        public float MaxY { get; set; }
        public int TileMinX { get; set; }
        public int TileMinZ { get; set; }
        public int TileMaxX { get; set; }
        public int TileMaxZ { get; set; }
        public List<TilesInfo> TilesList { get; set; }


        public AreaRoute()
        {
            TilesList = new List<TilesInfo>();

            minX = float.MaxValue;
            minY = float.MaxValue;
            MaxX = float.MinValue;
            MaxY = float.MinValue;
            TileMinX = int.MaxValue;
            TileMinZ = int.MaxValue;
            TileMaxX = int.MinValue;
            TileMaxZ = int.MinValue;
        }

        public float MinY { get => minY;
            set => minY = value; }

        public float MinX { get => minX;
            set => minX = value; }

        public void ManageTiles(int TileX, int TileZ)
        {
            var selectedTile = from f in TilesList where f.TileX == TileX && f.TileZ == TileZ select f;
            if (selectedTile.Count() == 0)
                TilesList.Add(new TilesInfo(this, TileX, TileZ));
            if (TileX < TileMinX) TileMinX = TileX;
            if (TileZ < TileMinZ) TileMinZ = TileZ;
            if (TileX >= TileMaxX) TileMaxX = TileX;
            if (TileZ >= TileMaxZ) TileMaxZ = TileZ;
        }

    }

    public class TilesInfo
    {
        public float TileX;
        public float TileZ;

        public TilesInfo(AreaRoute areaRoute, float x, float z)
        {
            TileX = x;
            TileZ = z;
            areaRoute.MaxX = Utility.CalcBounds(areaRoute.MaxX, ((x + 1f) * 2048f) + 1024f, true);
            areaRoute.MaxY = Utility.CalcBounds(areaRoute.MaxY, ((z + 1f) * 2048f) + 1024f, true);

            areaRoute.MinX = Utility.CalcBounds(areaRoute.MinX, (x * 2048f) -1024f, false);
            areaRoute.MinY = Utility.CalcBounds(areaRoute.MinY, (z * 2048f) -1024f, false);
        }
    }

    public static class Utility
    {
        
        /// <summary>
        /// Given a value representing a limit, evaluate if the given value exceeds the current limit.
        /// If so, expand the limit.
        /// </summary>
        /// <param name="limit">The current limit.</param>
        /// <param name="value">The value to compare the limit to.</param>
        /// <param name="gt">True when comparison is greater-than. False if less-than.</param>
        public static float CalcBounds(float limit, double v, bool gt)
        {
#if DEBUG_REPORTS
            if (limit == 30730174)
            {
                File.AppendAllText(@"F:\temp\AE.txt",
                    "CalcBounds: limit " + limit + "\n");

            }
#endif
            float value = (float)v;
            if (gt)
            {
                if (value > limit)
                {
                    limit = value;
                }
            }
            else
            {
                if (value < limit)
                {
                    limit = value;
                }
            }
            return limit;
        }
    }
}
