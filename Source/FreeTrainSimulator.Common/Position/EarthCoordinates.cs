using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Position
{
    public static class EarthCoordinates
    {
        /*
            * Contains equations that convert the camera (viewer) position on the current tile
            * to coordinates of world (as in planet earth) latitude and longitude.
            * MSTS uses the so-called "interrupted Goode homolosine projection" format 
            * to define world (i.e. planet earth) latitude and longitude coordinates.
            * This class is used to convert the current location of the viewer
            * to world coordinates of latitude and longitude.
            * Adapted from code written by Jim "deanville" Jendro, which in turn was
            * adapted from code written by Dan Steinwand.
        */

        private const double RadiansToDegrees = 180 / Math.PI;
        private const double PiOverTwo = Math.PI / 2;
        private const double EarthRadius = 6370997; // Average radius of the earth, meters
        private const double Epsilon = 0.0000000001; // Error factor (arbitrary)

        // Goode projection region boundary angles (radians)
        private const double UpperLatBoundary = 0.710987989993;    // 40°44'11.8" — boundary between sinusoidal and Mollweide zones
        private const double LonBoundaryMinus40 = -0.698131700798; // -40 degrees
        private const double LonBoundaryMinus100 = -1.74532925199; // -100 degrees
        private const double LonBoundaryMinus20 = -0.349065850399; // -20 degrees
        private const double LonBoundary80 = 1.3962634016;         //  80 degrees

        // Mollweide projection constants
        private const double MollweideOffset = 0.0528035274542; // Mollweide latitude offset constant
        private const double Sqrt2 = 1.4142135623731;           // √2
        private const double MollweideScale = 0.900316316158;   // Mollweide x-scale factor

        private static readonly double[] centralMeridians = new double[12]
        {
            // Initialize central meridians for each of the 12 regions
            -1.74532925199, //-100.0 degrees
            -1.74532925199, //-100.0 degrees
            0.523598775598, //  30.0 degrees
            0.523598775598, //  30.0 degrees
            -2.79252680319, //-160.0 degrees
            -1.0471975512,  // -60.0 degrees
            -2.79252680319, //-160.0 degrees
            -1.0471975512,  // -60.0 degrees
            0.349065850399, //  20.0 degrees
            2.44346095279,  // 140.0 degrees
            0.349065850399, //  20.0 degrees
            2.44346095279,  // 140.0 degrees
        };

        private static readonly double[] falseEast = new double[12]
        {
            // Initialize false easting for each of the 12 regions
            EarthRadius * -1.74532925199,
            EarthRadius * -1.74532925199,
            EarthRadius * 0.523598775598,
            EarthRadius * 0.523598775598,
            EarthRadius * -2.79252680319,
            EarthRadius * -1.0471975512,
            EarthRadius * -2.79252680319,
            EarthRadius * -1.0471975512,
            EarthRadius * 0.349065850399,
            EarthRadius * 2.44346095279,
            EarthRadius * 0.349065850399,
            EarthRadius * 2.44346095279,
        };

        // The upper left corner of the Goode projection is UpperLeftX, UpperLeftY
        // The bottom right corner of the Goode projection is -UpperLeftX, -UpperLeftY
        private const int UpperLeftX = -20013965; // -180 deg in Goode projection
        private const int UpperLeftY = 8674008;   // +90 deg lat in Goode projection

        // Offsets to convert Goode raster coordinates to MSTS world tile coordinates
        private const int WorldTileEastWestOffset = -16385;
        private const int WorldTileNorthSouthOffset = 16385;

        /// <summary>
        /// Gets latitude and longitude from a world location using the Goode homolosine projection.
        /// Returns default if the location falls in an interrupted area or is mathematically invalid.
        /// </summary>
        public static (double latitude, double longitude) ConvertWTC(in WorldLocation location)
        {
            // Decimal degrees is assumed
            int gsamp = location.Tile.X - WorldTileEastWestOffset;    // Gsamp is Goode world tile x
            int gline = WorldTileNorthSouthOffset - location.Tile.Z;  // Gline is Goode world tile Y
            int y = UpperLeftY - (gline - 1) * (int)WorldPosition.TileSize + (int)location.Location.Z;  // Actual Goode Y
            int x = UpperLeftX + (gsamp - 1) * (int)WorldPosition.TileSize + (int)location.Location.X;  // Actual Goode X

            return GoodeInverse(x, y);
        }

        /// <summary>
        /// Gets latitude and longitude from a tile location using the Goode homolosine projection.
        /// Returns 1 on success, -1 on math error, -2 if the location falls in an interrupted area.
        /// </summary>
        public static int ConvertWTC(in Tile tile, in Vector3 tileLocation, out double latitude, out double longitude)
        {
            // Decimal degrees is assumed
            int gsamp = tile.X - WorldTileEastWestOffset;              // Gsamp is Goode world tile x
            int gline = WorldTileNorthSouthOffset - tile.Z;            // Gline is Goode world tile Y
            int y = UpperLeftY - (gline - 1) * (int)WorldPosition.TileSize + (int)tileLocation.Z;  // Actual Goode Y
            int x = UpperLeftX + (gsamp - 1) * (int)WorldPosition.TileSize + (int)tileLocation.X;  // Actual Goode X

            return GoodeInverse(x, y, out latitude, out longitude);
        }

        /// <summary>
        /// Converts Goode XY coordinates to latitude and longitude.
        /// Returns 1 on success, -1 on math error, -2 if in interrupted area.
        /// </summary>
        private static int GoodeInverse(double gx, double gy, out double latitude, out double longitude)
        {
            // Goode Homolosine inverse equations: mapping GX, GY to Lat, Lon.
            // GX and GY must be offset in order to be in raw Goode coordinates.

            latitude = longitude = 0;

            int region;

            // Determine which of the 12 projection regions the point falls in
            if (gy >= EarthRadius * UpperLatBoundary)               // On or above 40°44'11.8"
                region = gx <= EarthRadius * LonBoundaryMinus40 ? 0 : 2;
            else if (gy >= 0)                                        // Between 0° and 40°44'11.8"
                region = gx <= EarthRadius * LonBoundaryMinus40 ? 1 : 3;
            else if (gy >= EarthRadius * -UpperLatBoundary)         // Between 0° and -40°44'11.8"
                if (gx <= EarthRadius * LonBoundaryMinus100)        // Between -180° and -100°
                    region = 4;
                else if (gx <= EarthRadius * LonBoundaryMinus20)    // Between -100° and -20°
                    region = 5;
                else if (gx <= EarthRadius * LonBoundary80)         // Between -20° and 80°
                    region = 8;
                else                                                 // Between 80° and 180°
                    region = 9;
            else                                                     // Below -40°44'11.8"
                if (gx <= EarthRadius * LonBoundaryMinus100)
                    region = 6;                                          // Between -180° and -100°
                else if (gx <= EarthRadius * LonBoundaryMinus20)
                    region = 5;                                          // Between -100° and -20°
                else if (gx <= EarthRadius * LonBoundary80)
                    region = 10;                                         // Between -20° and 80°
                else
                    region = 11;                                         // Between 80° and 180°

            gx -= falseEast[region];

            switch (region)
            {
                case 1:
                case 3:
                case 4:
                case 5:
                case 8:
                case 9:
                    // Sinusoidal zone
                    latitude = gy / EarthRadius;
                    if (Math.Abs(latitude) > PiOverTwo)
                        return -1; // math error
                    double temp = Math.Abs(latitude) - PiOverTwo;
                    if (Math.Abs(temp) > Epsilon)
                    {
                        temp = centralMeridians[region] + gx / (EarthRadius * Math.Cos(latitude));
                        longitude = AdjustLon(temp);
                    }
                    else
                        longitude = centralMeridians[region];
                    break;
                default:
                    // Mollweide zone
                    double arg = (gy + MollweideOffset * EarthRadius * Math.Sign(gy)) / (Sqrt2 * EarthRadius);
                    if (Math.Abs(arg) > 1)
                        return -2; // in interrupted area
                    double theta = Math.Asin(arg);
                    longitude = centralMeridians[region] + gx / (MollweideScale * EarthRadius * Math.Cos(theta));
                    if (longitude < -Math.PI)
                        return -2; // in interrupted area
                    arg = (2 * theta + Math.Sin(2 * theta)) / Math.PI;
                    if (Math.Abs(arg) > 1)
                        return -2; // in interrupted area
                    latitude = Math.Asin(arg);
                    break;
            }

            // Verify the result falls within the valid longitude range for this region
            switch (region)
            {
                case 0:
                case 1:
                    if (longitude < -Math.PI || longitude > LonBoundaryMinus40)
                        return -2;
                    break;
                case 2:
                case 3:
                    if (longitude < LonBoundaryMinus40 || longitude > Math.PI)
                        return -2;
                    break;
                case 4:
                case 6:
                    if (longitude < -Math.PI || longitude > LonBoundaryMinus100)
                        return -2;
                    break;
                case 5:
                case 7:
                    if (longitude < LonBoundaryMinus100 || longitude > LonBoundaryMinus20)
                        return -2;
                    break;
                case 8:
                case 10:
                    if (longitude < LonBoundaryMinus20 || longitude > LonBoundary80)
                        return -2;
                    break;
                case 9:
                case 11:
                    if (longitude < LonBoundary80 || longitude > Math.PI)
                        return -2;
                    break;
            }

            return 1; // Success
        }

        /// <summary>
        /// Converts Goode XY coordinates to latitude and longitude.
        /// Returns default if the location is invalid or in an interrupted area.
        /// </summary>
        private static (double latitude, double longitude) GoodeInverse(double gx, double gy)
        {
            return GoodeInverse(gx, gy, out double latitude, out double longitude) == 1
                ? (latitude, longitude)
                : default;
        }

        /// <summary>
        /// Adjusts a longitude value to stay within [-π, π].
        /// </summary>
        private static double AdjustLon(double value)
        {
            return Math.Abs(value) > Math.PI ? value - Math.Sign(value) * 2 * Math.PI : value;
        }

        /// <summary>
        /// Consider a line starting a pX,pZ and heading away at deg from North
        /// returns lat =  distance of x,z off of the line
        /// returns lon =  distance of x,z along the line
        /// </summary>
        public static (float lat, float lon) Survey(float pX, float pZ, float rad, float x, float z)
        {
            // translate the coordinates relative to a track section that starts at 0,0 
            x -= pX;
            z -= pZ;

            // rotate the coordinates relative to a track section that is pointing due north ( +z in MSTS coordinate system )
            (double x, double z) result = Rotate2D(rad, x, z);
            return ((float)result.x, (float)result.z);
        }

        //  2D Rotation
        //    A point<x, y> can be rotated around the origin<0,0> by running it through the following equations to get the new point<x',y'> :        
        //    x' = cos(theta)*x - sin(theta)*y 
        //    y' = sin(theta)*x + cos(theta)*y        
        //where theta is the angle by which to rotate the point.
        public static (double x, double z) Rotate2D(float radians, float x, float z)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            double xp = cos * x - sin * z;
            double zp = sin * x + cos * z;

            return (xp, zp);
        }

        public static (string latitude, string longitude) ToString(double latitude, double longitude)
        {
            longitude *= RadiansToDegrees; // E/W
            latitude *= RadiansToDegrees;  // N/S
            char hemisphere = latitude >= 0 ? 'N' : 'S';
            char direction = longitude >= 0 ? 'E' : 'W';
            longitude = Math.Abs(longitude);
            latitude = Math.Abs(latitude);
            int longitudeDegree = (int)Math.Truncate(longitude);
            int latitudeDegree = (int)Math.Truncate(latitude);

            longitude -= longitudeDegree;
            latitude -= latitudeDegree;
            longitude *= 60;
            latitude *= 60;
            int longitudeMinute = (int)Math.Truncate(longitude);
            int latitudeMinute = (int)Math.Truncate(latitude);
            longitude -= longitudeMinute;
            latitude -= latitudeMinute;
            longitude *= 60;
            latitude *= 60;
            //int longitudeSecond = (int)Math.Truncate(longitude);
            //int latitudeSecond = (int)Math.Truncate(latitude);

            return ($"{latitudeDegree}°{latitudeMinute,2:00}'{latitude,4:00.00}\"{hemisphere}", $"{longitudeDegree}°{longitudeMinute,2:00}'{longitude,4:00.00}\"{direction}");
        }

    }
}
