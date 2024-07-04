using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;

using Orts.Common;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{

    /// <summary>
    /// Static shapes have a fixed position in the world, i.e. the position once given does not change
    /// </summary>
    public class StaticShape: BaseShape
    {
        protected readonly WorldPosition worldPosition;

        /// <summary>
        /// Construct and initialize the class
        /// This constructor is for objects described by a MSTS shape file
        /// </summary>
        public StaticShape(string path, in WorldPosition position, ShapeFlags flags):
            base(path, flags)
        {
            worldPosition = position;
        }

        public override ref readonly WorldPosition WorldPosition => ref worldPosition;

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            SharedShape.PrepareFrame(frame, worldPosition, Flags);
        }
    }

    public class StaticTrackShape : StaticShape
    {
        public StaticTrackShape(string path, in WorldPosition position)
            : base(path, in position, ShapeFlags.AutoZBias)
        {
        }
    }

    public class SharedStaticShapeInstance : StaticShape
    {
        private readonly bool nightObjectEnabled;
        private readonly float objectRadius;
        private readonly float objectViewingDistance;
        private readonly ShapePrimitiveInstances[] primitives;

        public SharedStaticShapeInstance(string path, List<BaseShape> shapes)
            : base(path, GetCenterLocation(shapes), GetShapeFlags(shapes[0]))
        {
            nightObjectEnabled = shapes[0].SharedShape.HasNightSubObj;

            if (shapes[0].SharedShape.LodControls.Length > 0)
            {
                // We need both ends of the distance levels. We render the first but view as far as the last.
                SharedShape.DistanceLevel dlHighest = shapes[0].SharedShape.LodControls[0].DistanceLevels.First();
                SharedShape.DistanceLevel dlLowest = shapes[0].SharedShape.LodControls[0].DistanceLevels.Last();

                // Object radius should extend from central location to the furthest instance location PLUS the actual object radius.
                objectRadius = shapes.Max(s => (WorldPosition.Location - s.WorldPosition.Location).Length()) + dlHighest.ViewSphereRadius;

                // Object viewing distance is easy because it's based on the outside of the object radius.
                if (viewer.Settings.LODViewingExtension)
                    // Set to MaxValue so that an object never disappears.
                    // Many MSTS objects had a LOD of 2km which is the maximum distance that MSTS can handle.
                    // Open Rails can handle greater distances, so we override the lowest-detail LOD to make sure OR shows shapes further away than 2km.
                    // See http://www.elvastower.com/forums/index.php?/topic/35301-menu-options/page__view__findpost__p__275531
                    objectViewingDistance = float.MaxValue;
                else
                    objectViewingDistance = dlLowest.ViewingDistance;
            }

            // Create all the primitives for the shared shape.
            List<ShapePrimitiveInstances> primitivesList = new List<ShapePrimitiveInstances>();
            foreach (var lod in shapes[0].SharedShape.LodControls)
                for (var subObjectIndex = 0; subObjectIndex < lod.DistanceLevels[0].SubObjects.Length; subObjectIndex++)
                    foreach (var primitive in lod.DistanceLevels[0].SubObjects[subObjectIndex].ShapePrimitives)
                        primitivesList.Add(new ShapePrimitiveInstances(viewer.Game.GraphicsDevice, primitive, GetMatricies(shapes, primitive), subObjectIndex));
            primitives = primitivesList.ToArray();
        }

        private static WorldPosition GetCenterLocation(List<BaseShape> shapes)
        {
            int tileX = shapes.Min(s => s.WorldPosition.Tile.X);
            int tileZ = shapes.Min(s => s.WorldPosition.Tile.Z);
            Debug.Assert(tileX == shapes.Max(s => s.WorldPosition.Tile.X));
            Debug.Assert(tileZ == shapes.Max(s => s.WorldPosition.Tile.Z));

            float minX = shapes.Min(s => s.WorldPosition.Location.X);
            float maxX = shapes.Max(s => s.WorldPosition.Location.X);
            float minY = shapes.Min(s => s.WorldPosition.Location.Y);
            float maxY = shapes.Max(s => s.WorldPosition.Location.Y);
            float minZ = shapes.Min(s => s.WorldPosition.Location.Z);
            float maxZ = shapes.Max(s => s.WorldPosition.Location.Z);
            return new WorldPosition(new Tile(tileX, tileZ), Matrix.Identity).SetTranslation((minX + maxX) / 2, (minY + maxY) / 2, - (minZ + maxZ) / 2);
        }

        private Matrix[] GetMatricies(List<BaseShape> shapes, ShapePrimitive shapePrimitive)
        {
            Matrix matrix = Matrix.Identity;
            int hi = shapePrimitive.HierarchyIndex;
            while (hi >= 0 && hi < shapePrimitive.Hierarchy.Length && shapePrimitive.Hierarchy[hi] != -1)
            {
                matrix = MatrixExtension.Multiply(matrix, SharedShape.Matrices[hi]);
                hi = shapePrimitive.Hierarchy[hi];
            }

            var matricies = new Matrix[shapes.Count];
            for (var i = 0; i < shapes.Count; i++)
                matricies[i] = MatrixExtension.Multiply(
                    MatrixExtension.Multiply(matrix, shapes[i].WorldPosition.XNAMatrix), 
                    Matrix.CreateTranslation(-WorldPosition.Location.X, -WorldPosition.Location.Y, WorldPosition.Location.Z));

            return matricies;
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            Vector3 mstsLocation = worldPosition.Location + (worldPosition.Tile - viewer.Camera.Tile).TileVector();
            Matrix xnaMatrix = Matrix.CreateTranslation(mstsLocation.XnaVector());

            foreach (var primitive in primitives)
                if (primitive.SubObjectIndex != 1 || !nightObjectEnabled || viewer.MaterialManager.sunDirection.Y < 0)
                    frame.AddAutoPrimitive(mstsLocation, objectRadius, objectViewingDistance, primitive.Material, primitive, RenderPrimitiveGroup.World, ref xnaMatrix, Flags);
        }
    }



}
