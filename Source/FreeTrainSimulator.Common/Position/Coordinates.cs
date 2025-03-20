/*
 * 
 * COORDINATE SYSTEMS - XNA uses a different coordinate system than MSTS.  In XNA, +ve Z is toward the camera, 
 * whereas in MSTS it is the opposite.  As a result you will see the sign of all Z coordinates gets negated
 * and matrices are adjusted as they are loaded into XNA.  In addition the winding order of triangles is reversed in XNA.
 * Generally - X,Y,Z coordinates, vectors, quaternions, and angles will be expressed using MSTS coordinates 
 * unless otherwise noted with the prefix XNA.  Matrix's are usually constructed using XNA coordinates so they can be 
 * used directly in XNA draw routines.  So most matrix's will have XNA prepended to their name.
 * 
 * WorldCoordinates
 * X increases to the east
 * Y increases up
 * Z increases to the north
 * AX increases tilting down
 * AY increases turning to the right
 * 
 * LEXICON
 * Location - the x,y,z point where the center of the object is located - usually a Vector3
 * Pose - the orientation of an object in 3D, ie tilt, rotation - usually an XNAMatrix
 * Position - combines pose and location
 * WorldLocation - adds tile coordinates to a Location
 * WorldPosition - adds tile coordinates to a Position
 */

using System;

using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Position
{
    /// <summary>
    /// Represents the position and orientation of an object within a tile in XNA coordinates.
    /// </summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public const double TileSize = Tile.TileSize;

        /// <summary>The position within a tile (relative to the center of tile)</summary>
        public readonly Matrix XNAMatrix;

        public readonly Tile Tile;

        public WorldPosition(in Tile tile, Matrix xnaMatrix)
        {
            XNAMatrix = xnaMatrix;
            Tile = tile;
        }

        /// <summary>
        /// MSTS WFiles represent some location with a position, quaternion and tile coordinates
        /// This converts it to the ORTS WorldPosition representation
        /// </summary>
        public WorldPosition(int tileX, int tileZ, Vector3 xnaPosition, Quaternion xnaQuaternion)
        {
            XNAMatrix = MatrixExtension.Multiply(Matrix.CreateFromQuaternion(xnaQuaternion), Matrix.CreateTranslation(xnaPosition));
            Tile = new Tile(tileX, tileZ);
        }

        private static readonly WorldPosition none = new WorldPosition(Tile.Zero, Matrix.Identity);

        /// <summary>
        /// Returns a WorldPosition representing no Position at all.
        /// </summary>
        public static ref readonly WorldPosition None => ref none;

        /// <summary>
        /// Copy constructor using a MSTS-coordinates world-location 
        /// </summary>
        public WorldPosition(in WorldLocation source)
        {
            Tile = source.Tile;
            source.Location.Deconstruct(out float x, out float y, out float z);
            XNAMatrix = MatrixExtension.SetTranslation(Matrix.Identity, x, y, -z);
        }

        public WorldPosition ChangeTranslation(float x, float y, float z)
        {
            return new WorldPosition(Tile, MatrixExtension.ChangeTranslation(XNAMatrix, x, y, z));
        }

        public WorldPosition SetTranslation(Vector3 translation)
        {
            return new WorldPosition(Tile, MatrixExtension.SetTranslation(XNAMatrix, translation));
        }

        public WorldPosition SetTranslation(float x, float y, float z)
        {
            return new WorldPosition(Tile, MatrixExtension.SetTranslation(XNAMatrix, x, y, z));
        }

        /// <summary>
        /// The world-location in MSTS coordinates of the current position
        /// </summary>
        public WorldLocation WorldLocation => new WorldLocation(Tile, XNAMatrix.M41, XNAMatrix.M42, -XNAMatrix.M43);

        /// <summary>
        /// Describes the location as 3D vector in MSTS coordinates within the tile
        /// </summary>
        public Vector3 Location => new Vector3(XNAMatrix.M41, XNAMatrix.M42, -XNAMatrix.M43); // "inlined" XnaMatrix.Translation() Decomposition

        /// <summary>
        /// Ensure tile coordinates are within tile boundaries
        /// </summary>
        public WorldPosition Normalize()
        {
            Tile delta = new Tile((int)Math.Round((int)(XNAMatrix.M41 / 1024) / 2.0, MidpointRounding.AwayFromZero),
                (int)Math.Round((int)(XNAMatrix.M43 / 1024) / 2.0, MidpointRounding.AwayFromZero));

            return delta == Tile.Zero ? this : new WorldPosition(delta, MatrixExtension.SetTranslation(XNAMatrix, (float)(XNAMatrix.M41 - delta.X * TileSize),
                XNAMatrix.M42, (float)(XNAMatrix.M43 - delta.Z * TileSize)));
        }

        /// <summary>
        /// Change tile and location values to make it as if the location where on the requested tile.
        /// </summary>
        public WorldPosition NormalizeTo(Tile tile)
        {
            Tile delta = Tile - tile;

            return delta == Tile.Zero ? this : new WorldPosition(tile, MatrixExtension.SetTranslation(XNAMatrix, (float)(XNAMatrix.M41 + delta.X * TileSize),
                XNAMatrix.M42, (float)(XNAMatrix.M43 + delta.Z * TileSize)));
        }

        /// <summary>
        /// Create a nice string-representation of the world position
        /// </summary>
        public override string ToString()
        {
            return WorldLocation.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Tile.GetHashCode() ^ Location.GetHashCode();
        }

        public static bool operator ==(WorldPosition left, WorldPosition right)
        {
            return left.Tile.X == right.Tile.X && left.Tile.Z == right.Tile.Z && left.XNAMatrix == right.XNAMatrix;
        }

        public static bool operator !=(WorldPosition left, WorldPosition right)
        {
            return !(left == right);
        }

        public bool Equals(WorldPosition other)
        {
            return this == other;
        }
    }

    /// <summary>
    /// Represents the position of an object within a tile in MSTS coordinates.
    /// </summary>
    public readonly struct WorldLocation : IEquatable<WorldLocation>
    {
        public const double TileSize = Tile.TileSize;
        private static readonly WorldLocation none;

        /// <summary>
        /// Returns a WorldLocation representing no location at all.
        /// </summary>
        public static ref readonly WorldLocation None => ref none;

        /// <summary>The x-value of the tile</summary>
        public readonly int TileX => Tile.X;
        /// <summary>The z-value of the tile</summary>
        public readonly int TileZ => Tile.Z;
        /// <summary>The vector to the location within a tile, relative to center of tile in MSTS coordinates</summary>
        public readonly Vector3 Location;

        public readonly Tile Tile;

        /// <summary>
        /// Constructor using values for tileX and tileZ, and a vector for x, y, z
        /// </summary>
        public WorldLocation(in Tile tile, Vector3 location, bool normalize = false)
        {
            Tile = tile;
            Location = location;
            if (normalize)
                this = Normalize();
        }

        public WorldLocation(Tile tile, float x, float y, float z, bool normalize = false) :
            this(tile, new Vector3(x, y, z), normalize)
        {
        }

        /// <summary>
        /// Constructor using values for tileX, tileZ, x, y, and z.
        /// </summary>
        public WorldLocation(int tileX, int tileZ, float x, float y, float z, bool normalize = false) :
            this(new Tile(tileX, tileZ), new Vector3(x, y, z), normalize)
        {
        }

        /// <summary>
        /// Constructor using values for tileX and tileZ, and a vector for x, y, z
        /// </summary>
        public WorldLocation(int tileX, int tileZ, Vector3 location, bool normalize = false) :
            this(new Tile(tileX, tileZ), location, normalize)
        {
        }

        /// <summary>
        /// Ensure tile coordinates are within tile boundaries
        /// </summary>
        public WorldLocation Normalize()
        {
            int xTileDistance = (int)Math.Round((int)(Location.X / 1024) / 2.0, MidpointRounding.AwayFromZero);
            int zTileDistance = (int)Math.Round((int)(Location.Z / 1024) / 2.0, MidpointRounding.AwayFromZero);

            return xTileDistance == 0 && zTileDistance == 0 ? this : new WorldLocation(Tile.X + xTileDistance, Tile.Z + zTileDistance,
                new Vector3((float)(Location.X - xTileDistance * TileSize), Location.Y, (float)(Location.Z - zTileDistance * TileSize)));
        }

        /// <summary>
        /// Change tile and location values to make it as if the location where on the requested tile.
        /// </summary>
        /// <param name="tileX">The x-value of the tile to normalize to</param>
        /// <param name="tileZ">The x-value of the tile to normalize to</param>
        public WorldLocation NormalizeTo(int tileX, int tileZ) => NormalizeTo(new Tile(tileX, tileZ));

        public WorldLocation NormalizeTo(in Tile tile)
        {
            Tile delta = Tile - tile;

            return delta == Tile.Zero ? this : new WorldLocation(tile.X, tile.Z,
                new Vector3((float)(Location.X + delta.X * TileSize), Location.Y, (float)(Location.Z + delta.Z * TileSize)));
        }

        /// <summary>
        /// Helper method to set the elevation only to specified value
        /// </summary>
        /// <param name="elevation"></param>
        /// <returns></returns>
        public WorldLocation SetElevation(float elevation)
        {
            return new WorldLocation(Tile, Location.X, elevation, Location.Z);
        }

        /// <summary>
        /// Helper method to change (update) the elevation only by specifed delta
        /// </summary>
        /// <param name="delta"></param>
        /// <returns></returns>
        public WorldLocation ChangeElevation(float delta)
        {
            return new WorldLocation(Tile, Location.X, Location.Y + delta, Location.Z);
        }

        /// <summary>
        /// Check whether location1 and location2 are within given distance (in meters) from each other in 3D
        /// </summary>
        public static bool Within(in WorldLocation location1, in WorldLocation location2, float distance)
        {
            return GetDistanceSquared(location1, location2) <= distance * distance;
        }

        /// <summary>
        /// Get squared distance between two world locations (in meters)
        /// </summary>
        public static double GetDistanceSquared(in WorldLocation location1, in WorldLocation location2)
        {
            double dx = location1.Location.X - location2.Location.X;
            double dy = location1.Location.Y - location2.Location.Y;
            double dz = location1.Location.Z - location2.Location.Z;
            dx += TileSize * (location1.Tile.X - location2.Tile.X);
            dz += TileSize * (location1.Tile.Z - location2.Tile.Z);
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Get squared distance between two world locations (in meters), neglecting elevation (y) information
        /// </summary>
        public static double GetDistanceSquared2D(in WorldLocation location1, in WorldLocation location2)
        {
            double dx = location1.Location.X - location2.Location.X;
            double dz = location1.Location.Z - location2.Location.Z;
            dx += TileSize * (location1.Tile.X - location2.Tile.X);
            dz += TileSize * (location1.Tile.Z - location2.Tile.Z);
            return dx * dx + dz * dz;
        }
        /// <summary>
        /// Get a (3D) vector pointing locationFrom to locationTo
        /// </summary>
        public static Vector3 GetDistance(in WorldLocation locationFrom, in WorldLocation locationTo)
        {
            return new Vector3(
                (float)(locationTo.Location.X - locationFrom.Location.X + (locationTo.Tile.X - locationFrom.Tile.X) * TileSize),
                locationTo.Location.Y - locationFrom.Location.Y,
                (float)(locationTo.Location.Z - locationFrom.Location.Z + (locationTo.Tile.Z - locationFrom.Tile.Z) * TileSize));
        }

        /// <summary>
        /// Get a (2D) vector pointing from locationFrom to locationTo, neglecting elevation (y) information
        /// </summary>
        public static Vector2 GetDistance2D(in WorldLocation locationFrom, in WorldLocation locationTo)
        {
            return new Vector2(
                (float)(locationTo.Location.X - locationFrom.Location.X + (locationTo.Tile.X - locationFrom.Tile.X) * TileSize),
                (float)(locationTo.Location.Z - locationFrom.Location.Z + (locationTo.TileZ - locationFrom.Tile.Z) * TileSize));
        }

        public static double ApproximateDistance(in WorldLocation a, in WorldLocation b)
        {
            double dx = a.Location.X - b.Location.X;
            double dz = a.Location.Z - b.Location.Z;
            dx += (a.Tile.X - b.Tile.X) * TileSize;
            dz += (a.TileZ - b.Tile.Z) * TileSize;
            return Math.Abs(dx) + Math.Abs(dz);
        }

        public static WorldLocation InterpolateAlong(in WorldLocation locationFrom, in WorldLocation locationTo, float distance)
        {
            WorldLocation normalizedLocationTo = locationTo.NormalizeTo(locationFrom.Tile);
            float scale = (float)(distance / Math.Sqrt(GetDistanceSquared(locationFrom, locationTo)));

            return new WorldLocation(locationFrom.Tile, Vector3.Lerp(locationFrom.Location, normalizedLocationTo.Location, scale), true);
        }

        /// <summary>
        /// Create a nice string-representation of the world location
        /// </summary>
        public override string ToString()
        {
            return $"{{TileX:{Tile.X} TileZ:{Tile.Z} X:{Location.X} Y:{Location.Y} Z:{Location.Z}}}";
        }

        public static bool operator ==(in WorldLocation a, in WorldLocation b)
        {
            return a.Tile == b.Tile && a.Location == b.Location;
        }

        public static bool operator !=(in WorldLocation a, in WorldLocation b)
        {
            return a.Tile != b.Tile || a.Location != b.Location;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Tile.GetHashCode() ^ Location.GetHashCode();
        }

        public bool Equals(WorldLocation other)
        {
            return this == other;
        }
    }
}
