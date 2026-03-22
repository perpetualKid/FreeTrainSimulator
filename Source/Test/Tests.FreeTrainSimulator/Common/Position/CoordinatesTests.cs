using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Position
{
    [TestClass]
    public class CoordinatesTests
    {
        // Normalize: uses Round(AwayFromZero) on half-tile counts to redistribute local coordinates across tile boundaries.
        // ±1024 boundary is asymmetric by design; both map the same absolute point to the neighboring tile.
        [TestMethod]
        public void WorldLocationNormalizeTest()
        {
            WorldLocation location = new WorldLocation(0, 0, 3834, 0, -4118, true);

            Assert.AreEqual(2, location.Tile.X);
            Assert.AreEqual(-262, location.Location.X);
            Assert.AreEqual(-2, location.Tile.Z);
            Assert.AreEqual(-22, location.Location.Z);

            location = new WorldLocation(0, 0, 3834, 0, -1088, true);
            Assert.AreEqual(-1, location.Tile.Z);
            Assert.AreEqual(960, location.Location.Z);
        }

        // NormalizeTo: shifts local coordinates by Δtile × TileSize so the absolute world point is preserved on the target tile.
        [TestMethod]
        public void WorldLocationNormalizeToTest()
        {
            WorldLocation location = new WorldLocation(new Tile(-1, 1), 3834, 0, -4118).NormalizeTo(new Tile(4, 4));

            Assert.AreEqual(4, location.Tile.X);
            Assert.AreEqual(-6406, location.Location.X);
            Assert.AreEqual(4, location.Tile.Z);
            Assert.AreEqual(-10262, location.Location.Z);
        }

        // SetElevation replaces Y with an absolute value; ChangeElevation adds a signed delta to Y.
        [TestMethod]
        public void WorldLocationElevationTest()
        {
            WorldLocation location = new WorldLocation(0, 0, 0, 0, 0).SetElevation(123.4f);
            Assert.AreEqual(123.4f, location.Location.Y);
            location = location.ChangeElevation(-10.2f);
            Assert.AreEqual(113.2f, location.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // GetDistanceSquared/GetDistanceVector/GetDistance2D/Within all produce zero or trivially-true results for identical locations.
        [TestMethod]
        public void WorldLocationDistanceZeroTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation();

            Assert.IsTrue(WorldLocation.Within(location1, location2, 0));
            Assert.AreEqual(0, WorldLocation.GetDistanceSquared(location1, location2));
            Assert.AreEqual(Microsoft.Xna.Framework.Vector3.Zero, WorldLocation.GetDistanceVector(location1, location2));
            Assert.AreEqual(Microsoft.Xna.Framework.Vector2.Zero, WorldLocation.GetDistance2D(location1, location2));
        }

        // GetDistanceSquared: tile-offset accumulation in double precision; Within: distSquared ≤ distance² (inclusive).
        // GetDistanceVector/GetDistance2D: correct from→to vectors with tile offsets; tiles (0,0)↔(1,-1) each differ by 2048 m.
        [TestMethod]
        public void WorldLocationDistanceTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation(1, -1, Microsoft.Xna.Framework.Vector3.Zero);

            Assert.AreEqual((2048 * 2048) + (2048 * 2048), WorldLocation.GetDistanceSquared(location1, location2));
            Assert.IsTrue(WorldLocation.Within(location1, location2, (float)Math.Sqrt(2048 * 2048 * 2) + 1));

            Assert.AreEqual(new Microsoft.Xna.Framework.Vector3(2048, 0, -2048), WorldLocation.GetDistanceVector(location1, location2));
            Assert.AreEqual(new Microsoft.Xna.Framework.Vector2(2048, -2048), WorldLocation.GetDistance2D(location1, location2));
        }

        // == and != compare both Tile and Location; None equals a default-constructed WorldLocation.
        [TestMethod]
        public void WorldLocationOperatorTest()
        {
            WorldLocation location1 = new WorldLocation();
            WorldLocation location2 = new WorldLocation(1, 1, Microsoft.Xna.Framework.Vector3.One);

            Assert.IsTrue(location1.Equals(WorldLocation.None));
            Assert.IsTrue(location1 != location2);

            Assert.IsFalse(Equals(WorldLocation.None.Equals(new object())));
        }

        // InterpolateAlong: linear Lerp with scale = distance / pointDist; normalize=true handles cross-tile results.
        // Traveling 1328 m from tile(-2,-3) toward tile(0,-3) crosses the tile boundary into tile(-1,-3).
        [TestMethod]
        public void WorldLocationInterpolateAlongTileTest()
        {
            WorldLocation start = new WorldLocation(-2, -3, 1001, 5, 1);
            WorldLocation end = new WorldLocation(0, -3, -999, 5, 1);

            WorldLocation result = WorldLocation.InterpolateAlong(start, end, 1328f);

            Assert.AreEqual(new Tile(-1, -3), result.Tile);
        }

        // At distance=0 scale=0; Lerp returns from unchanged regardless of to.
        [TestMethod]
        public void WorldLocationInterpolateAlongTileAtStartTest()
        {
            WorldLocation start = new WorldLocation(-2, -3, 1001, 5, 1);
            WorldLocation end = new WorldLocation(0, -3, -999, 5, 1);

            WorldLocation result = WorldLocation.InterpolateAlong(start, end, 0f);

            Assert.AreEqual(start, result);
        }

        // PointAlongDirection: scales direction to exact distance; early-return when distance==0. Division by zero on a zero vector is a caller precondition.
        // direction (2,0,0) unnormalized, distance=10 → scale=5 → result (10,0,0).
        [TestMethod]
        public void WorldLocationPointAlongDirectionVectorTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);

            WorldLocation result = WorldLocation.PointAlongDirection(start, new Microsoft.Xna.Framework.Vector3(2, 0, 0), 10f);

            Assert.AreEqual(new WorldLocation(0, 0, 10, 0, 0), result);
        }

        // Two-location overload derives direction from GetDistanceVector(start, end).
        // start tile(0,0) x=2040, end tile(1,0) x=0, distance=16 → crosses tile boundary → tile(1,0) x=8.
        [TestMethod]
        public void WorldLocationPointAlongDirectionLocationTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 2040, 0, 0);
            WorldLocation end = new WorldLocation(1, 0, 0, 0, 0);

            WorldLocation result = WorldLocation.PointAlongDirection(start, end, 16f);

            Assert.AreEqual(new WorldLocation(1, 0, 8, 0, 0), result);
        }

        // Negative distance is a caller contract violation → ArgumentOutOfRangeException.
        [TestMethod]
        public void WorldLocationPointAlongDirectionNegativeDistanceThrowsTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WorldLocation.PointAlongDirection(start, Microsoft.Xna.Framework.Vector3.UnitX, -1f));
        }

        // distance=0 early-returns start before the direction is used, so a zero-vector is safe at zero distance.
        [TestMethod]
        public void WorldLocationPointAlongDirectionZeroDistanceWithZeroVectorReturnsStartTest()
        {
            WorldLocation start = new WorldLocation(1, -2, 12, 3, -44);

            WorldLocation result = WorldLocation.PointAlongDirection(start, Microsoft.Xna.Framework.Vector3.Zero, 0f);

            Assert.AreEqual(start, result);
        }

        // ArcCenterPoint: center = midpoint(start,end) + sign(arcAngle)·r·cos(arcAngle/2)·perp_norm; equidistant from start and end at radius.
        // CCW (arcAngle=-π/2): midpoint=(5,0,5), perp=(1/√2,0,1/√2), offset=(-5,0,-5) → center=(0,0,0).
        [TestMethod]
        public void WorldLocationFindArcCenterQuarterCircleCounterClockwiseTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, -MathF.PI / 2, 10f);

            Assert.AreEqual(0, center.Tile.X);
            Assert.AreEqual(0, center.Tile.Z);
            Assert.AreEqual(0f, center.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // CW (arcAngle=+π/2): midpoint=(5,0,5), perp=(1/√2,0,1/√2), offset=(+5,0,+5) → center=(10,0,10).
        [TestMethod]
        public void WorldLocationFindArcCenterQuarterCircleClockwiseTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, MathF.PI / 2, 10f);

            Assert.AreEqual(0, center.Tile.X);
            Assert.AreEqual(0, center.Tile.Z);
            Assert.AreEqual(10f, center.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(10f, center.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // Semicircle (arcAngle=π): cos(π/2)=0 → zero perpendicular offset → center = midpoint of start and end = (0,0,0).
        [TestMethod]
        public void WorldLocationFindArcCenterSemicircleTest()
        {
            WorldLocation start = new WorldLocation(0, 0, -10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 10, 0, 0);

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, MathF.PI, 10f);

            Assert.AreEqual(0, center.Tile.X);
            Assert.AreEqual(0, center.Tile.Z);
            Assert.AreEqual(0f, center.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, center.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // The computed center must be exactly radius away from both endpoints (validates the equidistance property).
        [TestMethod]
        public void WorldLocationFindArcCenterEquidistantFromBothEndpointsTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);
            float radius = 10f;

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, -MathF.PI / 2, radius);

            Assert.AreEqual((double)radius * radius, WorldLocation.GetDistanceSquared(center, start), EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual((double)radius * radius, WorldLocation.GetDistanceSquared(center, end), EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // GetDistanceSquared2D: tile-offset accumulation in double precision; Y is excluded entirely.
        // Elevation ignored: two locations differing only in Y have zero 2D squared distance.
        [TestMethod]
        public void WorldLocationGetDistanceSquared2DIgnoresElevationTest()
        {
            WorldLocation loc1 = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation loc2 = new WorldLocation(0, 0, 0, 100, 0);

            Assert.AreEqual(0.0, WorldLocation.GetDistanceSquared2D(loc1, loc2));
        }

        // Cross-tile offset: same local XZ, tile differs by 1 in X → dx = 0 + 1·2048 = 2048, distSq = 2048².
        [TestMethod]
        public void WorldLocationGetDistanceSquared2DCrossTileTest()
        {
            // Same local XZ, tile differs by 1 in X → dx = 0 + 1*2048 = 2048, dz = 0
            WorldLocation loc1 = new WorldLocation(0, 0, 100, 50, 0);
            WorldLocation loc2 = new WorldLocation(1, 0, 100, 200, 0);

            Assert.AreEqual(2048.0 * 2048.0, WorldLocation.GetDistanceSquared2D(loc1, loc2));
        }

        // ApproximateDistance: Manhattan (L1) distance in XZ only, ignoring Y; bounds actual Euclidean distance from above.
        // Same-tile: |Δx|+|Δz| = |3|+|4| = 7.
        [TestMethod]
        public void WorldLocationApproximateDistanceSameTileTest()
        {
            WorldLocation a = new WorldLocation(0, 0, 3, 0, 4);
            WorldLocation b = new WorldLocation(0, 0, 0, 0, 0);

            Assert.AreEqual(7.0, WorldLocation.ApproximateDistance(a, b));
        }

        // Cross-tile offset: dx = 0 + 1·2048 = 2048, dz = 0 → ApproximateDistance = 2048.
        [TestMethod]
        public void WorldLocationApproximateDistanceCrossTileTest()
        {
            // dx = 0 + (1-0)*2048 = 2048, dz = 0
            WorldLocation a = new WorldLocation(1, 0, 0, 0, 0);
            WorldLocation b = new WorldLocation(0, 0, 0, 0, 0);

            Assert.AreEqual(2048.0, WorldLocation.ApproximateDistance(a, b));
        }

        // ApproximateDistance is symmetric: a→b == b→a, since |Δx| and |Δz| are sign-independent.
        [TestMethod]
        public void WorldLocationApproximateDistanceIsSymmetricTest()
        {
            WorldLocation a = new WorldLocation(0, 0, 3, 10, 4);
            WorldLocation b = new WorldLocation(1, 0, 5, 0, 2);

            Assert.AreEqual(WorldLocation.ApproximateDistance(a, b), WorldLocation.ApproximateDistance(b, a));
        }

        // GetDistanceSquared: 3D squared distance includes Y in the tile-offset accumulation.
        [TestMethod]
        public void WorldLocationGetDistanceSquaredIncludesElevationTest()
        {
            // 3D: dx=3, dy=4, dz=0 → 9+16+0=25
            WorldLocation loc1 = new WorldLocation(0, 0, 3, 4, 0);
            WorldLocation loc2 = new WorldLocation(0, 0, 0, 0, 0);

            Assert.AreEqual(25.0, WorldLocation.GetDistanceSquared(loc1, loc2));
        }

        // Within: distSquared ≤ distance² is inclusive. distSquared=25, distance=5 → 25 ≤ 25 → true.
        [TestMethod]
        public void WorldLocationWithinAtExactDistanceTest()
        {
            // distSquared=25, distance²=25 → 25<=25 is true (inclusive boundary)
            WorldLocation loc1 = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation loc2 = new WorldLocation(0, 0, 5, 0, 0);

            Assert.IsTrue(WorldLocation.Within(loc1, loc2, 5f));
        }

        // Just outside the threshold: distSquared=25, distance=4.999 → 25 ≤ 24.99 → false.
        [TestMethod]
        public void WorldLocationWithinJustOutsideTest()
        {
            WorldLocation loc1 = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation loc2 = new WorldLocation(0, 0, 5, 0, 0);

            Assert.IsFalse(WorldLocation.Within(loc1, loc2, 4.999f));
        }

        // GetDistance2D: correct from→to 2D vector with tile offsets; Y is ignored.
        [TestMethod]
        public void WorldLocationGetDistance2DCrossTileTest()
        {
            // from tile(0,0), to tile(1,1) with same local coords
            // x: 0-0 + (1-0)*2048 = 2048, z (Vector2.Y): 0-0 + (1-0)*2048 = 2048
            WorldLocation from = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation to = new WorldLocation(1, 1, 0, 0, 0);

            Microsoft.Xna.Framework.Vector2 result = WorldLocation.GetDistance2D(from, to);

            Assert.AreEqual(2048f, result.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(2048f, result.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // PointAlongDirection: unnormalized 3D direction with Y component; length=5, distance=10 → scale=2 → result=(6,8,0).
        [TestMethod]
        public void WorldLocationPointAlongDirectionWith3DDirectionTest()
        {
            // Unnormalized direction (3,4,0): length=5, scale=10/5=2 → result=(6,8,0)
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);

            WorldLocation result = WorldLocation.PointAlongDirection(start, new Microsoft.Xna.Framework.Vector3(3, 4, 0), 10f);

            Assert.AreEqual(6f, result.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(8f, result.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(0f, result.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // InterpolateAlong location value: Lerp at scale=0.5 returns the geometric midpoint.
        [TestMethod]
        public void WorldLocationInterpolateAlongMidpointTest()
        {
            // distance = half of total → Lerp at scale=0.5 → (5,0,0)
            WorldLocation from = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation to = new WorldLocation(0, 0, 10, 0, 0);

            WorldLocation result = WorldLocation.InterpolateAlong(from, to, 5f);

            Assert.AreEqual(new WorldLocation(0, 0, 5, 0, 0), result);
        }

        // scale=1: Lerp returns to.Location exactly; normalize=true handles the same-tile case cleanly.
        [TestMethod]
        public void WorldLocationInterpolateAlongAtFullDistanceTest()
        {
            // scale=1 → Lerp returns to.Location exactly
            WorldLocation from = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation to = new WorldLocation(0, 0, 10, 0, 0);

            WorldLocation result = WorldLocation.InterpolateAlong(from, to, 10f);

            Assert.AreEqual(to, result);
        }

        // InterpolateElevationAlong: Y-component of the same Lerp as InterpolateAlong; early-return when pointDist==0.
        // Identical locations: pointDist=0 → early-return path → from.Location.Y.
        [TestMethod]
        public void WorldLocationInterpolateElevationAlongWhenIdenticalLocationsTest()
        {
            // pointDistance=0 → early return with from.Location.Y
            WorldLocation from = new WorldLocation(0, 0, 0, 5, 0);
            WorldLocation to = new WorldLocation(0, 0, 0, 5, 0);

            float elevation = WorldLocation.InterpolateElevationAlong(from, to, 0f);

            Assert.AreEqual(5f, elevation);
        }

        // distance=0 → scale=0 → Lerp returns from.Location.Y regardless of to.
        [TestMethod]
        public void WorldLocationInterpolateElevationAlongAtStartTest()
        {
            // distance=0 → scale=0 → Lerp at 0 returns from.Location.Y
            WorldLocation from = new WorldLocation(0, 0, 0, 3, 0);
            WorldLocation to = new WorldLocation(0, 0, 0, 9, 0);

            float elevation = WorldLocation.InterpolateElevationAlong(from, to, 0f);

            Assert.AreEqual(3f, elevation, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // Linear interpolation: pointDist=10, distance=5 → scale=0.5 → elevation=5.
        [TestMethod]
        public void WorldLocationInterpolateElevationAlongMidpointTest()
        {
            // Only Y differs: pointDistance=10, at distance=5 → scale=0.5 → elevation=5
            WorldLocation from = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation to = new WorldLocation(0, 0, 0, 10, 0);

            float elevation = WorldLocation.InterpolateElevationAlong(from, to, 5f);

            Assert.AreEqual(5f, elevation, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // scale=1 → Lerp returns to.Location.Y exactly.
        [TestMethod]
        public void WorldLocationInterpolateElevationAlongAtFullDistanceTest()
        {
            // scale=1 → returns to.Location.Y
            WorldLocation from = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation to = new WorldLocation(0, 0, 0, 10, 0);

            float elevation = WorldLocation.InterpolateElevationAlong(from, to, 10f);

            Assert.AreEqual(10f, elevation, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // PointAlongArc: both CW and CCW are correct; direction is encoded via the center, not the parameterization.
        // t=0: center + radius·u = start; t=|arcAngle|: center + radius·(cos(|arcAngle|)·u + sin(|arcAngle|)·v) = end.
        [TestMethod]
        public void WorldLocationPointAlongArcAtZeroDistanceReturnsStartTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation result = WorldLocation.PointAlongArc(start, end, -MathF.PI / 2, 10f, 0f);

            Assert.AreEqual(start.Location.X, result.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(start.Location.Y, result.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(start.Location.Z, result.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // CCW (arcAngle=-π/2): center=(0,0,0), u=(1,0,0), v=(0,0,1).
        // At t=π/2: cos(π/2)·u+sin(π/2)·v = (0,0,1) → point = center+10·(0,0,1) = (0,0,10) = end.
        [TestMethod]
        public void WorldLocationPointAlongArcCounterClockwiseAtFullArcAngleReturnsEndTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation result = WorldLocation.PointAlongArc(start, end, -MathF.PI / 2, 10f, MathF.PI / 2);

            Assert.AreEqual(end.Location.X, result.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Y, result.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Z, result.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // CW (arcAngle=+π/2): center=(10,0,10), u=(0,0,-1), v=(-1,0,0).
        // At t=π/2: 0·u+1·v = (-1,0,0) → point = center+10·(-1,0,0) = (0,0,10) = end.
        [TestMethod]
        public void WorldLocationPointAlongArcClockwiseAtFullArcAngleReturnsEndTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);

            WorldLocation result = WorldLocation.PointAlongArc(start, end, MathF.PI / 2, 10f, MathF.PI / 2);

            Assert.AreEqual(end.Location.X, result.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Y, result.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Z, result.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // Any point on the arc is exactly radius away from the center (verified at the midpoint t=|arcAngle|/2).
        [TestMethod]
        public void WorldLocationPointAlongArcMidpointIsOnArcTest()
        {
            // Any point on the arc must be exactly radius away from the center
            WorldLocation start = new WorldLocation(0, 0, 10, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 10);
            float arcAngle = -MathF.PI / 2;
            float radius = 10f;

            WorldLocation center = WorldLocation.ArcCenterPoint(start, end, arcAngle, radius);
            WorldLocation midpoint = WorldLocation.PointAlongArc(start, end, arcAngle, radius, MathF.Abs(arcAngle) / 2f);

            Assert.AreEqual((double)radius * radius, WorldLocation.GetDistanceSquared(center, midpoint), EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        [TestMethod]
        public void WorldPositionCtorTest()
        {
            Assert.AreEqual(WorldPosition.None, new WorldPosition(Tile.Zero, Microsoft.Xna.Framework.Matrix.Identity));
            WorldLocation location = new WorldLocation(3, 4, 5, 6, 7);
            WorldPosition position = new WorldPosition(location);
            Assert.AreEqual(location.Location, position.Location);
            Assert.AreEqual(location, position.WorldLocation);

            Assert.AreEqual("{TileX:3 TileZ:4 X:5 Y:6 Z:7}", position.ToString());
        }

        [TestMethod]
        public void WorldPositionTranslationTest()
        {
            WorldLocation location = new WorldLocation(3, 4, 5, 6, 7);
            WorldPosition position = new WorldPosition(location);
            Assert.AreEqual(position.SetTranslation(Microsoft.Xna.Framework.Vector3.One), position.SetTranslation(1, 1, 1));
        }

        [TestMethod]
        public void WorldPositionNormalizeTest()
        {
            WorldPosition position = new WorldPosition(new WorldLocation(0, 0, 3834, 0, -4118)).Normalize();

            Assert.AreEqual(2, position.Tile.X);
            Assert.AreEqual(-262, position.Location.X);
            Assert.AreEqual(2, position.Tile.Z);
            Assert.AreEqual(-22, position.Location.Z);

            position = new WorldPosition(Tile.Zero, MatrixExtension.SetTranslation(Microsoft.Xna.Framework.Matrix.Identity, 3834, 0, -4118)).Normalize();

            Assert.AreEqual(2, position.Tile.X);
            Assert.AreEqual(-262, position.Location.X);
            Assert.AreEqual(-2, position.Tile.Z);
            Assert.AreEqual(22, position.Location.Z);
            Assert.AreEqual(-22, position.XNAMatrix.M43);
        }

        [TestMethod]
        public void WorldPositionNormalizeToTest()
        {
            WorldPosition position = new WorldPosition(new Tile(-1, 1), MatrixExtension.SetTranslation(Microsoft.Xna.Framework.Matrix.Identity, 3834, 0, -4118)).NormalizeTo(new Tile(4, 4));

            Assert.AreEqual(4, position.Tile.X);
            Assert.AreEqual(-6406, position.Location.X);
            Assert.AreEqual(4, position.Tile.Z);
            Assert.AreEqual(10262, position.Location.Z);
            Assert.AreEqual(-10262, position.XNAMatrix.M43);
        }

        // ComputeEndLocation straight: PointAlongDirection(start, end, trackSection.Length) where
        // trackSection.Length equals the 3D distance start→end must return exactly end.
        // start=(0,0,0,0,0), end=(0,0,0,0,100), distance=100 → result matches end.
        [TestMethod]
        public void WorldLocationPointAlongDirectionAtFullLengthReachesEndOnLevelSectionTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 0, 0, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 0, 100);
            float length = (float)Math.Sqrt(WorldLocation.GetDistanceSquared(start, end));

            WorldLocation result = WorldLocation.PointAlongDirection(start, end, length);

            Assert.AreEqual(end.Location.X, result.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Y, result.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Z, result.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }

        // ComputeEndLocation curved: PointAlongArc(start, end, arcAngle, radius, |arcAngle|) at distance==|arcAngle|
        // must return end including its elevation. Elevated CCW quarter-circle (r=10, Y=20):
        // center.Y=(20+20)/2=20; perp=Cross(UnitY, chord) is horizontal → u,v have no Y → all arc points keep Y=20.
        [TestMethod]
        public void WorldLocationPointAlongArcAtFullArcAngleReturnsEndAtElevationTest()
        {
            WorldLocation start = new WorldLocation(0, 0, 10, 20, 0);
            WorldLocation end = new WorldLocation(0, 0, 0, 20, 10);

            WorldLocation result = WorldLocation.PointAlongArc(start, end, -MathF.PI / 2, 10f, MathF.PI / 2);

            Assert.AreEqual(end.Location.X, result.Location.X, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Y, result.Location.Y, EqualityPrecisionDelta.FloatPrecisionDelta);
            Assert.AreEqual(end.Location.Z, result.Location.Z, EqualityPrecisionDelta.FloatPrecisionDelta);
        }
    }
}
