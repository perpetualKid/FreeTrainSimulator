// COPYRIGHT 2012, 2013 by the Open Rails project.
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
using System.Globalization;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    public sealed class MissingTrackNodeException : Exception
    {
        public MissingTrackNodeException()
            : base("")
        {
        }

        public MissingTrackNodeException(string message) : base(message)
        {
        }

        public MissingTrackNodeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public abstract class TravellerInitializationException : Exception
    {
        public int TileX { get; }
        public int TileZ { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public TrackVectorSection TrackVectorSection { get; }
        public float ErrorLimit { get; }

        protected TravellerInitializationException(Exception innerException, int tileX, int tileZ, float x, float y, float z, TrackVectorSection tvs, float errorLimit, string format, params object[] args)
            : base(string.Format(CultureInfo.CurrentCulture, format, args), innerException)
        {
            TileX = tileX;
            TileZ = tileZ;
            X = x;
            Y = y;
            Z = z;
            TrackVectorSection = tvs;
            ErrorLimit = errorLimit;
        }

        protected TravellerInitializationException()
        {
        }

        protected TravellerInitializationException(string message) : base(message)
        {
        }

        protected TravellerInitializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class TravellerOutsideBoundingAreaException : TravellerInitializationException
    {
        public float DistanceX { get; }
        public float DistanceZ { get; }

        public TravellerOutsideBoundingAreaException(int tileX, int tileZ, float x, float y, float z, TrackVectorSection tvs, float errorLimit, float dx, float dz)
            : base(null, tileX, tileZ, x, y, z, tvs, errorLimit, "{0} is ({3} > {2} or {4} > {2}) outside the bounding area of track vector section {1}", new WorldLocation(tileX, tileZ, x, y, z), tvs, errorLimit, dx, dz)
        {
            DistanceX = dx;
            DistanceZ = dz;
        }

        public TravellerOutsideBoundingAreaException()
        {
        }

        public TravellerOutsideBoundingAreaException(string message) : base(message)
        {
        }

        public TravellerOutsideBoundingAreaException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class TravellerOutsideCenterlineException : TravellerInitializationException
    {
        public float Distance { get; }

        public TravellerOutsideCenterlineException(int tileX, int tileZ, float x, float y, float z, TrackVectorSection tvs, float errorLimit, float distance)
            : base(null, tileX, tileZ, x, y, z, tvs, errorLimit, "{0} is ({2} > {3}) from the centerline of track vector section {1}", new WorldLocation(tileX, tileZ, x, y, z), tvs, distance, errorLimit)
        {
            Distance = distance;
        }

        public TravellerOutsideCenterlineException()
        {
        }

        public TravellerOutsideCenterlineException(string message) : base(message)
        {
        }

        public TravellerOutsideCenterlineException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class TravellerBeyondTrackLengthException : TravellerInitializationException
    {
        public float Length { get; }
        public float Distance { get; }

        public TravellerBeyondTrackLengthException(int tileX, int tileZ, float x, float y, float z, TrackVectorSection tvs, float errorLimit, float length, float distance)
            : base(null, tileX, tileZ, x, y, z, tvs, errorLimit, "{0} is ({2} < {3} or {2} > {4}) beyond the extents of track vector section {1}", new WorldLocation(tileX, tileZ, x, y, z), tvs, distance, -errorLimit, length + errorLimit)
        {
            Length = length;
            Distance = distance;
        }

        public TravellerBeyondTrackLengthException()
        {
        }

        public TravellerBeyondTrackLengthException(string message) : base(message)
        {
        }

        public TravellerBeyondTrackLengthException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
    public class TravellerInvalidDataException : Exception
    {
        public TravellerInvalidDataException(string format, params object[] args)
            : base(string.Format(CultureInfo.CurrentCulture, format, args))
        {
        }

        public TravellerInvalidDataException()
        {
        }

        public TravellerInvalidDataException(string message) : base(message)
        {
        }

        public TravellerInvalidDataException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

}
