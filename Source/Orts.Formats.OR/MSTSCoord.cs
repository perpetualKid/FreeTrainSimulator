// COPYRIGHT 2014, 2015 by the Open Rails project.
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

using Microsoft.Xna.Framework;
using Orts.Formats.Msts;
using Orts.Common;
using System;
using System.Drawing;

namespace Orts.Formats.OR
{
    public readonly struct MSTSCoord

    {
        public readonly float TileX; 
        public readonly float TileY;
        public readonly float X;
        public readonly float Y;

        public override string ToString()
        {
            return $"({(int)(TileX * 2048f):d + X)},{(int)((TileY * 2048f) + Y):d})";
        }
    }

}
