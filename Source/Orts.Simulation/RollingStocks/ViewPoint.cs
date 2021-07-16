// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

using Microsoft.Xna.Framework;

namespace Orts.Simulation.RollingStocks
{
    public class ViewPoint
    {
        public Vector3 Location { get; internal set; }
        public Vector3 StartDirection { get; internal set; }
        public Vector3 RotationLimit { get; internal set; }

        public ViewPoint()
        {
        }

        public ViewPoint(Vector3 location)
        {
            Location = location;
        }

        public ViewPoint(ViewPoint source, bool rotate)
        {
            Location = source?.Location ?? throw new ArgumentNullException(nameof(source));
            StartDirection = source.StartDirection;
            RotationLimit = source.RotationLimit;
            if (rotate)
            {
                Location = new Vector3(-Location.X, Location.Y, -Location.Z);
            }
        }
    }

    public class PassengerViewPoint : ViewPoint
    {
        // Remember direction of passenger camera and apply when user returns to it.
        public float RotationXRadians { get; internal set; }
        public float RotationYRadians { get; internal set; }
    }


}
