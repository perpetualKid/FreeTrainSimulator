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

        private const double piRad = 180 / Math.PI;
        private const int earthRadius = 6370997; // Average radius of the earth, meters
        private const double epsilon = 0.0000000001; // Error factor (arbitrary)
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
            earthRadius * -1.74532925199,
            earthRadius * -1.74532925199,
            earthRadius * 0.523598775598,
            earthRadius * 0.523598775598,
            earthRadius * -2.79252680319,
            earthRadius * -1.0471975512,
            earthRadius * -2.79252680319,
            earthRadius * -1.0471975512,
            earthRadius * 0.349065850399,
            earthRadius * 2.44346095279,
            earthRadius * 0.349065850399,
            earthRadius * 2.44346095279,
        };

        // The upper left corner of the Goode projection is ul_x,ul_y
        // The bottom right corner of the Goode projection is -ul_x,-ul_y
        private const int ul_x = -20013965; // -180 deg in Goode projection
        private const int ul_y = 8674008; // +90 deg lat in Goode projection

        // Offsets to convert Goode raster coordinates to MSTS world tile coordinates
        private const int wt_ew_offset = -16385;
        private const int wt_ns_offset = 16385;

        /// <summary>
        /// Entry point to this series of methods
        /// Gets Longitude, Latitude from Goode X, Y
        /// </summary>        
        public static (double latitude, double longitude) ConvertWTC(in WorldLocation location)
        {
            // Decimal degrees is assumed
            int gsamp = location.Tile.X - wt_ew_offset;  // Gsamp is Goode world tile x
            int gline = wt_ns_offset - location.Tile.Z;  // Gline is Goode world tile Y
            int y = ul_y - (gline - 1) * (int)WorldPosition.TileSize + (int)location.Location.Z;   // Actual Goode X
            int x = ul_x + (gsamp - 1) * (int)WorldPosition.TileSize + (int)location.Location.X;   // Actual Goode Y

            // Return error code: 1 = success; -1 = math error; -2 = XY is in interrupted area of projection
            // Return latitude and longitude by reference
            return Goode_Inverse(x, y);
        }

        /// <summary>
        /// Entry point to this series of methods
        /// Gets Longitude, Latitude from Goode X, Y
        /// </summary>        
        public static int ConvertWTC(in Tile tile, in Vector3 tileLocation, out double latitude, out double longitude)
        {
            // Decimal degrees is assumed
            int gsamp = tile.X - wt_ew_offset;  // Gsamp is Goode world tile x
            int gline = wt_ns_offset - tile.Z;  // Gline is Goode world tile Y
            int y = ul_y - (gline - 1) * (int)WorldPosition.TileSize + (int)tileLocation.Z;   // Actual Goode X
            int x = ul_x + (gsamp - 1) * (int)WorldPosition.TileSize + (int)tileLocation.X;   // Actual Goode Y

            // Return error code: 1 = success; -1 = math error; -2 = XY is in interrupted area of projection
            // Return latitude and longitude by reference
            return Goode_Inverse(x, y, out latitude, out longitude);
        }

        /// <summary>
        /// Convert Goode XY coordinates to latitude and longitude
        /// </summary>        
        private static int Goode_Inverse(double gx, double gy, out double latitude, out double longitude)
        {
            // Goode Homolosine inverse equations
            // Mapping GX, GY to Lat, Lon
            // GX and GY must be offset in order to be in raw Goode coordinates.
            // This may alter lon and lat values.

            latitude = longitude = 0f;

            int region;

            // Inverse equations
            if (gy >= earthRadius * 0.710987989993)             // On or above 40 44' 11.8"
                if (gx <= earthRadius * -0.698131700798)        // To the left of -40
                    region = 0;
                else
                    region = 2;
            else if (gy >= 0)                                   // Between 0.0 and 40 44' 11.8"
                if (gx <= earthRadius * -0.698131700798)        // To the left of -40
                    region = 1;
                else
                    region = 3;
            else if (gy >= earthRadius * -0.710987989993)       // Between 0.0 and -40 44' 11.8"
                if (gx <= earthRadius * -1.74532925199)         // Between -180 and -100
                    region = 4;
                else if (gx <= earthRadius * -0.349065850399)   // Between -100 and -20
                    region = 5;
                else if (gx <= earthRadius * 1.3962634016)      // Between -20 and 80
                    region = 8;
                else                                            // Between 80 and 180
                    region = 9;
            else
                if (gx <= earthRadius * -1.74532925199)
                region = 6;                                  // Between -180 and -100
            else if (gx <= earthRadius * -0.349065850399)
                region = 5;                                  // Between -100 and -20
            else if (gx <= earthRadius * 1.3962634016)
                region = 10;                                 // Between -20 and 80
            else
                region = 11;                                 // Between 80 and 180

            gx -= falseEast[region];

            switch (region)
            {
                case 1:
                case 3:
                case 4:
                case 5:
                case 8:
                case 9:
                    latitude = gy / earthRadius;
                    if (Math.Abs(latitude) > MathHelper.PiOver2)
                        // Return error: math error
                        return -1;
                    double temp = Math.Abs(latitude) - MathHelper.PiOver2;
                    if (Math.Abs(temp) > epsilon)
                    {
                        temp = centralMeridians[region] + gx / (earthRadius * Math.Cos(latitude));
                        longitude = Adjust_Lon(temp);
                    }
                    else
                        longitude = centralMeridians[region];
                    break;
                default:
                    double arg = (gy + 0.0528035274542 * earthRadius * Math.Sign(gy)) / (1.4142135623731 * earthRadius);
                    if (Math.Abs(arg) > 1)
                        // Return error: in interrupred area
                        return -2;

                    double theta = Math.Asin(arg);
                    longitude = centralMeridians[region] + gx / (0.900316316158 * earthRadius * Math.Cos(theta));
                    if (longitude < -MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    arg = (2 * theta + Math.Sin(2 * theta)) / MathHelper.Pi;
                    if (Math.Abs(arg) > 1)
                        // Return error: in interrupred area
                        return -2;
                    latitude = Math.Asin(arg);
                    break;
            } // switch

            // Are we in a interrupted area? if so, return status code on in_break
            switch (region)
            {
                case 0:
                    if (longitude < -MathHelper.Pi || longitude > -0.698131700798)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 1:
                    if (longitude < -MathHelper.Pi || longitude > -0.698131700798)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 2:
                    if (longitude < -0.698131700798 || longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 3:
                    if (longitude < -0.698131700798 || longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 4:
                    if (longitude < -MathHelper.Pi || longitude > -1.74532925199)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 5:
                    if (longitude < -1.74532925199 || longitude > -0.349065850399)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 6:
                    if (longitude < -MathHelper.Pi || longitude > -1.74532925199)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 7:
                    if (longitude < -1.74532925199 || longitude > -0.349065850399)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 8:
                    if (longitude < -0.349065850399 || longitude > 1.3962634016)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 9:
                    if (longitude < 1.3962634016 || longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 10:
                    if (longitude < -0.349065850399 || longitude > 1.3962634016)
                        // Return error: in interrupred area
                        return -2;
                    break;
                case 11:
                    if (longitude < 1.3962634016 || longitude > MathHelper.Pi)
                        // Return error: in interrupred area
                        return -2;
                    break;
            } // switch

            return 1; // Success
        }

        /// <summary>
        /// Convert Goode XY coordinates to latitude and longitude
        /// </summary>        
        private static (double latitude, double longitude) Goode_Inverse(double gx, double gy)
        {
            // Goode Homolosine inverse equations
            // Mapping GX, GY to Lat, Lon
            // GX and GY must be offset in order to be in raw Goode coordinates.
            // This may alter lon and lat values.

            (double latitude, double longitude) result = default;

            int region;

            // Inverse equations
            if (gy >= earthRadius * 0.710987989993)             // On or above 40 44' 11.8"
                if (gx <= earthRadius * -0.698131700798)        // To the left of -40
                    region = 0;
                else
                    region = 2;
            else if (gy >= 0)                                   // Between 0.0 and 40 44' 11.8"
                if (gx <= earthRadius * -0.698131700798)        // To the left of -40
                    region = 1;
                else
                    region = 3;
            else if (gy >= earthRadius * -0.710987989993)       // Between 0.0 and -40 44' 11.8"
                if (gx <= earthRadius * -1.74532925199)         // Between -180 and -100
                    region = 4;
                else if (gx <= earthRadius * -0.349065850399)   // Between -100 and -20
                    region = 5;
                else if (gx <= earthRadius * 1.3962634016)      // Between -20 and 80
                    region = 8;
                else                                            // Between 80 and 180
                    region = 9;
            else
                if (gx <= earthRadius * -1.74532925199)
                region = 6;                                  // Between -180 and -100
            else if (gx <= earthRadius * -0.349065850399)
                region = 5;                                  // Between -100 and -20
            else if (gx <= earthRadius * 1.3962634016)
                region = 10;                                 // Between -20 and 80
            else
                region = 11;                                 // Between 80 and 180

            gx -= falseEast[region];

            switch (region)
            {
                case 1:
                case 3:
                case 4:
                case 5:
                case 8:
                case 9:
                    result.latitude = gy / earthRadius;
                    if (Math.Abs(result.latitude) > MathHelper.PiOver2)
                        // Return error: math error
                        return default;
                    double temp = Math.Abs(result.latitude) - MathHelper.PiOver2;
                    if (Math.Abs(temp) > epsilon)
                    {
                        temp = centralMeridians[region] + gx / (earthRadius * Math.Cos(result.latitude));
                        result.longitude = Adjust_Lon(temp);
                    }
                    else
                        result.longitude = centralMeridians[region];
                    break;
                default:
                    double arg = (gy + 0.0528035274542 * earthRadius * Math.Sign(gy)) / (1.4142135623731 * earthRadius);
                    if (Math.Abs(arg) > 1)
                        // Return error: in interrupred area
                        return default;
                    double theta = Math.Asin(arg);
                    result.longitude = centralMeridians[region] + gx / (0.900316316158 * earthRadius * Math.Cos(theta));
                    if (result.longitude < -MathHelper.Pi)
                        // Return error: in interrupred area
                        return default;
                    arg = (2 * theta + Math.Sin(2 * theta)) / MathHelper.Pi;
                    if (Math.Abs(arg) > 1)
                        // Return error: in interrupred area
                        return default;
                    result.latitude = Math.Asin(arg);
                    break;
            } // switch

            return result;
        }

        /// <summary>
        /// Checks for Pi overshoot
        /// </summary>        
        private static double Adjust_Lon(double value)
        {
            if (Math.Abs(value) > MathHelper.Pi)
                return value - Math.Sign(value) * MathHelper.TwoPi;
            else
                return value;
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
            longitude *= piRad; // E/W
            latitude *= piRad;  // N/S
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
