// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Input;
using Orts.Common.Xna;
using Orts.Settings;
using Game = Orts.ActivityRunner.Viewer3D.Processes.Game;

namespace Orts.ActivityRunner.Viewer3D
{
    public enum RenderPrimitiveSequence
    {
        CabOpaque,
        Sky,
        WorldOpaque,
        WorldBlended,
        Lights, // TODO: May not be needed once alpha sorting works.
        Precipitation, // TODO: May not be needed once alpha sorting works.
        Particles,
        InteriorOpaque,
        InteriorBlended,
        Labels,
        CabBlended,
        OverlayOpaque,
        OverlayBlended,
        // This value must be last.
        Sentinel
    }

    public enum RenderPrimitiveGroup
    {
        Cab,
        Sky,
        World,
        Lights, // TODO: May not be needed once alpha sorting works.
        Precipitation, // TODO: May not be needed once alpha sorting works.
        Particles,
        Interior,
        Labels,
        Overlay
    }

    public abstract class RenderPrimitive
    {
        protected static GraphicsDevice graphicsDevice;
        public static void SetGraphicsDevice(GraphicsDevice device) => graphicsDevice = device;

        /// <summary>
        /// Mapping from <see cref="RenderPrimitiveGroup"/> to <see cref="RenderPrimitiveSequence"/> for blended
        /// materials. The number of items in the array must equal the number of values in
        /// <see cref="RenderPrimitiveGroup"/>.
        /// </summary>
        public static readonly RenderPrimitiveSequence[] SequenceForBlended = new[] {
			RenderPrimitiveSequence.CabBlended,
            RenderPrimitiveSequence.Sky,
			RenderPrimitiveSequence.WorldBlended,
			RenderPrimitiveSequence.Lights,
			RenderPrimitiveSequence.Precipitation,
            RenderPrimitiveSequence.Particles,
            RenderPrimitiveSequence.InteriorBlended,
            RenderPrimitiveSequence.Labels,
			RenderPrimitiveSequence.OverlayBlended,
		};

        /// <summary>
        /// Mapping from <see cref="RenderPrimitiveGroup"/> to <see cref="RenderPrimitiveSequence"/> for opaque
        /// materials. The number of items in the array must equal the number of values in
        /// <see cref="RenderPrimitiveGroup"/>.
        /// </summary>
        public static readonly RenderPrimitiveSequence[] SequenceForOpaque = new[] {
			RenderPrimitiveSequence.CabOpaque,
            RenderPrimitiveSequence.Sky,
			RenderPrimitiveSequence.WorldOpaque,
			RenderPrimitiveSequence.Lights,
			RenderPrimitiveSequence.Precipitation,
            RenderPrimitiveSequence.Particles,
            RenderPrimitiveSequence.InteriorOpaque,
            RenderPrimitiveSequence.Labels,
			RenderPrimitiveSequence.OverlayOpaque,
		};

        protected static VertexBuffer DummyVertexBuffer;
        protected static readonly VertexDeclaration DummyVertexDeclaration = new VertexDeclaration(ShapeInstanceData.SizeInBytes, ShapeInstanceData.VertexElements);
        protected static readonly Matrix[] DummyVertexData = new Matrix[] { Matrix.Identity };

        /// <summary>
        /// This is an adjustment for the depth buffer calculation which may be used to reduce the chance of co-planar primitives from fighting each other.
        /// </summary>
        // TODO: Does this actually make any real difference?
        public float ZBias;

        /// <summary>
        /// This is a sorting adjustment for primitives with similar/the same world location. Primitives with higher SortIndex values are rendered after others. Has no effect on non-blended primitives.
        /// </summary>
        public float SortIndex;

        /// <summary>
        /// This is when the object actually renders itself onto the screen.
        /// Do not reference any volatile data.
        /// Executes in the RenderProcess thread
        /// </summary>
        public abstract void Draw();
    }

    [DebuggerDisplay("{Material} {RenderPrimitive} {Flags}")]
    public readonly struct RenderItem
    {
        public readonly Material Material;
        public readonly RenderPrimitive RenderPrimitive;
        public readonly Matrix XNAMatrix;
        public readonly ShapeFlags Flags;

        public RenderItem(Material material, RenderPrimitive renderPrimitive, Matrix xnaMatrix, ShapeFlags flags)
        {
            Material = material;
            RenderPrimitive = renderPrimitive;
            XNAMatrix = xnaMatrix;
            Flags = flags;
        }

        public class Comparer : IComparer<RenderItem>
        {
            private Vector3 viewerPos;

            public Comparer(Vector3 viewerPos)
            {
                this.viewerPos = viewerPos;
            }

            public void Update(Vector3 viewerPos)
            {
                this.viewerPos = viewerPos;
            }

            #region IComparer<RenderItem> Members

            public int Compare(RenderItem x, RenderItem y)
            {

                double temp = y.XNAMatrix.M41 - viewerPos.X;
                double distanceSquared = temp * temp;
                temp = y.XNAMatrix.M42 - viewerPos.Y;
                distanceSquared += temp * temp;
                temp = y.XNAMatrix.M43 - viewerPos.Z;
                distanceSquared += temp * temp;

                temp = x.XNAMatrix.M41 - viewerPos.X;
                distanceSquared -= temp * temp;
                temp = x.XNAMatrix.M42 - viewerPos.Y;
                distanceSquared -= temp * temp;
                temp = x.XNAMatrix.M43 - viewerPos.Z;
                distanceSquared -= temp * temp;

                if (Math.Abs(distanceSquared) >= 0.001)
                    return Math.Sign(distanceSquared);
                return Math.Sign(x.RenderPrimitive.SortIndex - y.RenderPrimitive.SortIndex);

                //// For unknown reasons, this would crash with an ArgumentException (saying Compare(x, x) != 0)
                //// sometimes when calculated as two values and subtracted. Presumed cause is floating point.
                //var xd = (x.XNAMatrix.Translation - viewerPos).Length();
                //var yd = (y.XNAMatrix.Translation - viewerPos).Length();
                //// If the absolute difference is >= 1mm use that; otherwise, they're effectively in the same
                //// place so fall back to the SortIndex.
                //if (Math.Abs(yd - xd) >= 0.001)
                //    return Math.Sign(yd - xd);
                //return Math.Sign(x.RenderPrimitive.SortIndex - y.RenderPrimitive.SortIndex);
            }

            #endregion
        }
    }

    public class RenderFrame
    {
        readonly Game game;

        // Shared shadow map data.
        private static RenderTarget2D[] shadowMap;
        private static RenderTarget2D[] shadowMapRenderTarget;
        private static Vector3 steppedSolarDirection = Vector3.UnitX;

        // Local shadow map data.
        private readonly Matrix[] shadowMapLightView;
        private readonly Matrix[] shadowMapLightProjection;
        private readonly Matrix[] shadowMapLightViewProjection;
        private readonly Matrix[] shadowMapLightViewProjectionShadowProjection;
        private Vector3 shadowMapX;
        private Vector3 shadowMapY;
        private readonly Vector3[] shadowMapCenter;

        readonly Material DummyBlendedMaterial;

        private readonly Dictionary<Material, List<RenderItem>>[] renderItems = new Dictionary<Material, List<RenderItem>>[(int)RenderPrimitiveSequence.Sentinel];
        private readonly List<RenderItem> renderItemsSequence = new List<RenderItem>();
        private readonly List<RenderItem>[] renderShadowTerrainItems;
        private readonly List<RenderItem>[] renderShadowForestItems;
        private readonly List<RenderItem>[] renderShadowSceneryItems;

        public bool IsScreenChanged { get; internal set; }

        private ShadowMapMaterial shadowMapMaterial;
        private SceneryShader sceneryShader;
        private Vector3 solarDirection;
        private Camera camera;
        private Vector3 cameraLocation;
        private RenderItem.Comparer renderItemComparer;

        private Matrix cameraViewProjection;
        private Matrix identity;
        private Matrix projection;

        private readonly int shadowMapCount;
        private readonly bool dynamicShadows;

        public RenderFrame(Game game)
        {
            shadowMapCount = RenderProcess.ShadowMapCount;
            dynamicShadows = game.Settings.DynamicShadows;
            this.game = game;
            DummyBlendedMaterial = new EmptyMaterial(null);

            for (int i = 0; i < renderItems.Length; i++)
				renderItems[i] = new Dictionary<Material, List<RenderItem>>();

            if (dynamicShadows)
            {
                if (shadowMap == null)
                {
                    int shadowMapSize = game.Settings.ShadowMapResolution;
                    shadowMap = new RenderTarget2D[shadowMapCount];
                    shadowMapRenderTarget = new RenderTarget2D[shadowMapCount];
                    for (int shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                    {
                        shadowMapRenderTarget[shadowMapIndex] = new RenderTarget2D(game.RenderProcess.GraphicsDevice, shadowMapSize, shadowMapSize, false, SurfaceFormat.Rg32, DepthFormat.Depth16, 0, RenderTargetUsage.PreserveContents);
                        shadowMap[shadowMapIndex] = new RenderTarget2D(game.RenderProcess.GraphicsDevice, shadowMapSize, shadowMapSize, false, SurfaceFormat.Rg32, DepthFormat.Depth16, 0, RenderTargetUsage.PreserveContents);
                    }
                }

                shadowMapLightView = new Matrix[shadowMapCount];
                shadowMapLightProjection = new Matrix[shadowMapCount];
                shadowMapLightViewProjection = new Matrix[shadowMapCount];
                shadowMapLightViewProjectionShadowProjection = new Matrix[shadowMapCount];
                shadowMapCenter = new Vector3[shadowMapCount];

                renderShadowSceneryItems = new List<RenderItem>[shadowMapCount];
                renderShadowForestItems = new List<RenderItem>[shadowMapCount];
                renderShadowTerrainItems = new List<RenderItem>[shadowMapCount];
                for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                {
                    renderShadowSceneryItems[shadowMapIndex] = new List<RenderItem>();
                    renderShadowForestItems[shadowMapIndex] = new List<RenderItem>();
                    renderShadowTerrainItems[shadowMapIndex] = new List<RenderItem>();
                }
            }

            identity = Matrix.Identity;
            projection = Matrix.CreateOrthographic(game.RenderProcess.DisplaySize.X, game.RenderProcess.DisplaySize.Y, 1, 100);
            MatrixExtension.Multiply(in identity, in projection, out cameraViewProjection);

            renderItemComparer = new RenderItem.Comparer(Vector3.Zero);
        }

        public void Clear()
        {
            // Attempt to clean up unused materials over time (max 1 per RenderPrimitiveSequence).
            for (var i = 0; i < renderItems.Length; i++)
            {
                foreach (var mat in renderItems[i].Keys)
                {
                    if (renderItems[i][mat].Count == 0)
                    {
                        renderItems[i].Remove(mat);
                        break;
                    }
                }
            }
            
            // Clear out (reset) all of the RenderItem lists.
            for (var i = 0; i < renderItems.Length; i++)
                foreach (Material material in renderItems[i].Keys)
                    renderItems[i][material].Clear();

            // Clear out (reset) all of the shadow mapping RenderItem lists.
            if (dynamicShadows)
            {
                for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                {
                    renderShadowSceneryItems[shadowMapIndex].Clear();
                    renderShadowForestItems[shadowMapIndex].Clear();
                    renderShadowTerrainItems[shadowMapIndex].Clear();
                }
            }
        }

        public void PrepareFrame(Viewer viewer)
        {
            if (viewer.Settings.UseMSTSEnv == false)
                solarDirection = viewer.World.Sky.solarDirection;
            else
                solarDirection = viewer.World.MSTSSky.mstsskysolarDirection;

            if (shadowMapMaterial == null)
                shadowMapMaterial = (ShadowMapMaterial)viewer.MaterialManager.Load("ShadowMap");
            if (sceneryShader == null)
                sceneryShader = viewer.MaterialManager.SceneryShader;
        }

        public void SetCamera(Camera camera)
        {
            this.camera = camera;
            cameraLocation = camera.Location;
            cameraLocation.Z *= -1;

            //viewMatrices[(int)ViewMatrixSequence.View] = camera.XnaView;
            //viewMatrices[(int)ViewMatrixSequence.Projection] = camera.XnaProjection;
            //MatrixExtension.Multiply(in viewMatrices[0], in viewMatrices[1], out viewMatrices[2]);
            MatrixExtension.Multiply(in camera.XnaView, in camera.XnaProjection, out cameraViewProjection);

            renderItemComparer.Update(cameraLocation);
        }

        static bool LockShadows;
        //[CallOnThread("Updater")]
        public void PrepareFrame(in ElapsedTime elapsedTime)
        {
            if (UserInput.IsPressed(UserCommand.DebugLockShadows))
                LockShadows = !LockShadows;

            if (dynamicShadows && (shadowMapCount > 0) && !LockShadows)
            {
                Vector3 normalizedSolarDirection = solarDirection;
                normalizedSolarDirection.Normalize();
                if (Vector3.Dot(steppedSolarDirection, normalizedSolarDirection) < 0.99999)
                    steppedSolarDirection = normalizedSolarDirection;

                //                var cameraDirection = new Vector3(-cameraView.M13, -cameraView.M23, -cameraView.M33);
                //                var cameraDirection = new Vector3(-viewMatrices[(int)ViewMatrixSequence.View].M13, -viewMatrices[(int)ViewMatrixSequence.View].M23, -viewMatrices[(int)ViewMatrixSequence.View].M33);
                Vector3 cameraDirection = new Vector3(-(camera?.XnaView.M13 ?? 0), -(camera?.XnaView.M23 ?? 0), -(camera?.XnaView.M33 ?? 1)); 
                // viewMatrices[(int)ViewMatrixSequence.View].M13, -viewMatrices[(int)ViewMatrixSequence.View].M23, -viewMatrices[(int)ViewMatrixSequence.View].M33);
                cameraDirection.Normalize();

                var shadowMapAlignAxisX = Vector3.Cross(steppedSolarDirection, Vector3.UnitY);
                var shadowMapAlignAxisY = Vector3.Cross(shadowMapAlignAxisX, steppedSolarDirection);
                shadowMapAlignAxisX.Normalize();
                shadowMapAlignAxisY.Normalize();
                shadowMapX = shadowMapAlignAxisX;
                shadowMapY = shadowMapAlignAxisY;

                for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                {
                    var viewingDistance = game.Settings.ViewingDistance;
                    var shadowMapDiameter = RenderProcess.ShadowMapDiameter[shadowMapIndex];
                    var shadowMapLocation = cameraLocation + RenderProcess.ShadowMapDistance[shadowMapIndex] * cameraDirection;

                    // Align shadow map location to grid so it doesn't "flutter" so much. This basically means aligning it along a
                    // grid based on the size of a shadow texel (shadowMapSize / shadowMapSize) along the axes of the sun direction
                    // and up/left.
                    var shadowMapAlignmentGrid = (float)shadowMapDiameter / game.Settings.ShadowMapResolution;
                    var shadowMapSize = game.Settings.ShadowMapResolution;
                    var adjustX = (float)Math.IEEERemainder(Vector3.Dot(shadowMapAlignAxisX, shadowMapLocation), shadowMapAlignmentGrid);
                    var adjustY = (float)Math.IEEERemainder(Vector3.Dot(shadowMapAlignAxisY, shadowMapLocation), shadowMapAlignmentGrid);
                    shadowMapLocation.X -= shadowMapAlignAxisX.X * adjustX;
                    shadowMapLocation.Y -= shadowMapAlignAxisX.Y * adjustX;
                    shadowMapLocation.Z -= shadowMapAlignAxisX.Z * adjustX;
                    shadowMapLocation.X -= shadowMapAlignAxisY.X * adjustY;
                    shadowMapLocation.Y -= shadowMapAlignAxisY.Y * adjustY;
                    shadowMapLocation.Z -= shadowMapAlignAxisY.Z * adjustY;

                    shadowMapLightView[shadowMapIndex] = Matrix.CreateLookAt(shadowMapLocation + viewingDistance * steppedSolarDirection, shadowMapLocation, Vector3.Up);
                    shadowMapLightProjection[shadowMapIndex] = Matrix.CreateOrthographic(shadowMapDiameter, shadowMapDiameter, 0, viewingDistance + shadowMapDiameter / 2);
                    MatrixExtension.Multiply(in shadowMapLightView[shadowMapIndex], in shadowMapLightProjection[shadowMapIndex], out shadowMapLightViewProjection[shadowMapIndex]);
//                    shadowMapLightViewProjection[shadowMapIndex] = shadowMapLightView[shadowMapIndex] * shadowMapLightProjection[shadowMapIndex];

                    shadowMapLightViewProjectionShadowProjection[shadowMapIndex] = shadowMapLightView[shadowMapIndex] * shadowMapLightProjection[shadowMapIndex] * new Matrix(0.5f, 0, 0, 0, 0, -0.5f, 0, 0, 0, 0, 1, 0, 0.5f + 0.5f / shadowMapSize, 0.5f + 0.5f / shadowMapSize, 0, 1);
                    shadowMapCenter[shadowMapIndex] = shadowMapLocation;
                }
            }
        }

        /// <summary>
        /// Automatically adds or culls a <see cref="RenderPrimitive"/> based on a location, radius and max viewing distance.
        /// </summary>
        /// <param name="mstsLocation">Center location of the <see cref="RenderPrimitive"/> in MSTS coordinates.</param>
        /// <param name="objectRadius">Radius of a sphere containing the whole <see cref="RenderPrimitive"/>, centered on <paramref name="mstsLocation"/>.</param>
        /// <param name="objectViewingDistance">Maximum distance from which the <see cref="RenderPrimitive"/> should be viewable.</param>
        /// <param name="material"></param>
        /// <param name="primitive"></param>
        /// <param name="group"></param>
        /// <param name="xnaMatrix"></param>
        /// <param name="flags"></param>
        //[CallOnThread("Updater")]
        public void AddAutoPrimitive(Vector3 mstsLocation, float objectRadius, float objectViewingDistance, Material material, RenderPrimitive primitive, RenderPrimitiveGroup group, ref Matrix xnaMatrix, ShapeFlags flags)
        {
            if (float.IsPositiveInfinity(objectViewingDistance) || (camera != null && camera.InRange(mstsLocation, objectRadius, objectViewingDistance)))
            {
                if (camera != null && camera.InFov(mstsLocation, objectRadius))
                    AddPrimitive(material, primitive, group, ref xnaMatrix, flags);
            }

            if (dynamicShadows && (shadowMapCount > 0) && ((flags & ShapeFlags.ShadowCaster) != 0))
                for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                    if (IsInShadowMap(shadowMapIndex, mstsLocation, objectRadius, objectViewingDistance))
                        AddShadowPrimitive(shadowMapIndex, material, primitive, ref xnaMatrix, flags);
        }

        //[CallOnThread("Updater")]
        public void AddPrimitive(Material material, RenderPrimitive primitive, RenderPrimitiveGroup group, ref Matrix xnaMatrix)
        {
            AddPrimitive(material, primitive, group, ref xnaMatrix, ShapeFlags.None);
        }

        static readonly bool[] PrimitiveBlendedScenery = new bool[] { true, false }; // Search for opaque pixels in alpha blended primitives, thus maintaining correct DepthBuffer
        static readonly bool[] PrimitiveBlended = new bool[] { true };
        static readonly bool[] PrimitiveNotBlended = new bool[] { false };

        //[CallOnThread("Updater")]
        public void AddPrimitive(Material material, RenderPrimitive primitive, RenderPrimitiveGroup group, ref Matrix xnaMatrix, ShapeFlags flags)
        {
            var getBlending = material.GetBlending();
            var blending = getBlending && material is SceneryMaterial ? PrimitiveBlendedScenery : getBlending ? PrimitiveBlended : PrimitiveNotBlended;

            List<RenderItem> items;
            foreach (var blended in blending)
            {
                var sortingMaterial = blended ? DummyBlendedMaterial : material;
                var sequence = renderItems[(int)GetRenderSequence(group, blended)];

                if (!sequence.TryGetValue(sortingMaterial, out items))
                {
                    items = new List<RenderItem>();
                    sequence.Add(sortingMaterial, items);
                }
                items.Add(new RenderItem(material, primitive, xnaMatrix, flags));
            }
            if (((flags & ShapeFlags.AutoZBias) != 0) && (primitive.ZBias == 0))
                primitive.ZBias = 1;
        }

        //[CallOnThread("Updater")]
        void AddShadowPrimitive(int shadowMapIndex, Material material, RenderPrimitive primitive, ref Matrix xnaMatrix, ShapeFlags flags)
        {
            if (material is SceneryMaterial)
                renderShadowSceneryItems[shadowMapIndex].Add(new RenderItem(material, primitive, xnaMatrix, flags));
            else if (material is ForestMaterial)
                renderShadowForestItems[shadowMapIndex].Add(new RenderItem(material, primitive, xnaMatrix, flags));
            else if (material is TerrainMaterial)
                renderShadowTerrainItems[shadowMapIndex].Add(new RenderItem(material, primitive, xnaMatrix, flags));
            else
                Debug.Fail("Only scenery, forest and terrain materials allowed in shadow map.");
        }

        //[CallOnThread("Updater")]
        public void Sort()
        {
            //System.Threading.Tasks.Parallel.For(0, renderItems.Length, (i) =>
            //{
            //    foreach (var sequenceMaterial in renderItems[i].Where(kvp => kvp.Value.Count > 1))
            //    {
            //        if (sequenceMaterial.Key == DummyBlendedMaterial)
            //            sequenceMaterial.Value.Sort(renderItemComparer);
            //    }
            //});

            foreach (var sequence in renderItems)
            {
                foreach (var sequenceMaterial in sequence.Where(kvp => kvp.Value.Count > 1))
                {
                    if (sequenceMaterial.Key != DummyBlendedMaterial)
                        continue;
                    //Debug.WriteLine($"Sorting {sequenceMaterial.ToString()} {sequenceMaterial.Value.Count} items");
                    sequenceMaterial.Value.Sort(renderItemComparer);
                }
            }
        }

        bool IsInShadowMap(int shadowMapIndex, Vector3 mstsLocation, float objectRadius, float objectViewingDistance)
        {
            if (shadowMapRenderTarget == null)
                return false;

            mstsLocation.Z *= -1;
            Vector3.Subtract(ref mstsLocation, ref shadowMapCenter[shadowMapIndex], out mstsLocation);
            objectRadius += RenderProcess.ShadowMapDiameter[shadowMapIndex] / 2;

            // Check if object is inside the sphere.
            var length = mstsLocation.LengthSquared();
            if (length <= objectRadius * objectRadius)
                return true;

            // Check if object is inside cylinder.
            Vector3.Dot(ref mstsLocation, ref shadowMapX, out float dotX);
            if (Math.Abs(dotX) > objectRadius)
                return false;

            Vector3.Dot(ref mstsLocation, ref shadowMapY, out float dotY);
            if (Math.Abs(dotY) > objectRadius)
                return false;

            // Check if object is on correct side of center.
            Vector3.Dot(ref mstsLocation, ref steppedSolarDirection, out float dotZ);
            if (dotZ < 0)
                return false;

            return true;
        }

        static RenderPrimitiveSequence GetRenderSequence(RenderPrimitiveGroup group, bool blended)
        {
            if (blended)
                return RenderPrimitive.SequenceForBlended[(int)group];
            return RenderPrimitive.SequenceForOpaque[(int)group];
        }

        //[CallOnThread("Render")]
        public void Draw()
        {
            var logging = UserInput.IsPressed(UserCommand.DebugLogRenderFrame);
            if (logging)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Draw {");
            }
            if (dynamicShadows && (shadowMapCount > 0) && shadowMapMaterial != null)
                DrawShadows(logging);
            DrawSimple(logging);
            for (var i = 0; i < (int)RenderPrimitiveSequence.Sentinel; i++)
                game.RenderProcess.PrimitiveCount[i] = renderItems[i].Values.Sum(l => l.Count);

            if (logging)
            {
                Console.WriteLine("}");
                Console.WriteLine();
            }
        }

        void DrawShadows(bool logging )
        {
            if (logging) Console.WriteLine("  DrawShadows {");
            for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                DrawShadows(logging, shadowMapIndex);
            for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                game.RenderProcess.ShadowPrimitiveCount[shadowMapIndex] = renderShadowSceneryItems[shadowMapIndex].Count + renderShadowForestItems[shadowMapIndex].Count + renderShadowTerrainItems[shadowMapIndex].Count;
            if (logging) Console.WriteLine("  }");
        }

        void DrawShadows(bool logging, int shadowMapIndex)
        {
            if (logging) Console.WriteLine("    {0} {{", shadowMapIndex);

            // Prepare renderer for drawing the shadow map.
            game.GraphicsDevice.SetRenderTarget(shadowMapRenderTarget[shadowMapIndex]);
            game.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Target, Color.White, 1, 0);

            // Prepare for normal (non-blocking) rendering of scenery.
            shadowMapMaterial.SetState(ShadowMapMaterial.Mode.Normal);

            // Render non-terrain, non-forest shadow items first.
            if (logging) Console.WriteLine("      {0,-5} * SceneryMaterial (normal)", renderShadowSceneryItems[shadowMapIndex].Count);
            //            shadowMapMaterial.Render(graphicsDevice, renderShadowSceneryItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(renderShadowSceneryItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex], ref shadowMapLightViewProjection[shadowMapIndex]);

            // Prepare for normal (non-blocking) rendering of forests.
            shadowMapMaterial.SetState(ShadowMapMaterial.Mode.Forest);

            // Render forest shadow items next.
            if (logging) Console.WriteLine("      {0,-5} * ForestMaterial (forest)", renderShadowForestItems[shadowMapIndex].Count);
//            shadowMapMaterial.Render(graphicsDevice, renderShadowForestItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(renderShadowForestItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex], ref shadowMapLightViewProjection[shadowMapIndex]);

            // Prepare for normal (non-blocking) rendering of terrain.
            shadowMapMaterial.SetState(ShadowMapMaterial.Mode.Normal);

            // Render terrain shadow items now, with their magic.
            if (logging) Console.WriteLine("      {0,-5} * TerrainMaterial (normal)", renderShadowTerrainItems[shadowMapIndex].Count);
            game.GraphicsDevice.Indices = TerrainPrimitive.SharedPatchIndexBuffer;
//            shadowMapMaterial.Render(graphicsDevice, renderShadowTerrainItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(renderShadowTerrainItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex], ref shadowMapLightViewProjection[shadowMapIndex]);

            // Prepare for blocking rendering of terrain.
            shadowMapMaterial.SetState(ShadowMapMaterial.Mode.Blocker);

            // Render terrain shadow items in blocking mode.
            if (logging) Console.WriteLine("      {0,-5} * TerrainMaterial (blocker)", renderShadowTerrainItems[shadowMapIndex].Count);
            //            shadowMapMaterial.Render(graphicsDevice, renderShadowTerrainItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(renderShadowTerrainItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex], ref shadowMapLightViewProjection[shadowMapIndex]);

            // All done.
            shadowMapMaterial.ResetState();
            game.GraphicsDevice.SetRenderTarget(null);

            // Blur the shadow map.
            if (game.Settings.ShadowMapBlur)
            {
                //shadowMap[shadowMapIndex] = 
                shadowMapMaterial.ApplyBlur(shadowMap[shadowMapIndex], shadowMapRenderTarget[shadowMapIndex]);
            }
            else
                shadowMap[shadowMapIndex] = shadowMapRenderTarget[shadowMapIndex];

            if (logging) Console.WriteLine("    }");
        }

        /// <summary>
        /// Executed in the RenderProcess thread - simple draw
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="logging"></param>
        void DrawSimple(bool logging)
        {
            if (game.Settings.DistantMountains)
            {
                if (logging) Console.WriteLine("  DrawSimple (Distant Mountains) {");
                game.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Transparent, 1, 0);
                DrawSequencesDistantMountains(logging);
                if (logging) Console.WriteLine("  }");
                if (logging) Console.WriteLine("  DrawSimple {");
                game.GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                DrawSequences(logging);
                if (logging) Console.WriteLine("  }");
            }
            else
            {
                if (logging) Console.WriteLine("  DrawSimple {");
                game.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Transparent, 1, 0);
                DrawSequences(logging);
                if (logging) Console.WriteLine("  }");
            }
        }

        void DrawSequences(bool logging)
        {
            ref Matrix viewRef = ref (camera != null ? ref camera.XnaView : ref identity);
            ref Matrix projectionRef = ref (camera != null ? ref camera.XnaProjection : ref projection);

            if (dynamicShadows && (shadowMapCount > 0) && sceneryShader != null)
                sceneryShader.SetShadowMap(shadowMapLightViewProjectionShadowProjection, shadowMap, RenderProcess.ShadowMapLimit);

            renderItemsSequence.Clear();
            for (var i = 0; i < (int)RenderPrimitiveSequence.Sentinel; i++)
            {
                if (logging) Console.WriteLine("    {0} {{", (RenderPrimitiveSequence)i);
                var sequence = renderItems[i];
                foreach (var sequenceMaterial in sequence)
                {
                    if (sequenceMaterial.Value.Count == 0)
                        continue;
                    if (sequenceMaterial.Key == DummyBlendedMaterial)
                    {
                        // Blended: multiple materials, group by material as much as possible without destroying ordering.
                        Material lastMaterial = null;
                        foreach (var renderItem in sequenceMaterial.Value)
                        {
                            if (lastMaterial != renderItem.Material)
                            {
                                if (renderItemsSequence.Count > 0)
                                {
                                    if (logging) Console.WriteLine("      {0,-5} * {1}", renderItemsSequence.Count, lastMaterial);
                                    lastMaterial.Render(renderItemsSequence, ref viewRef, ref projectionRef, ref cameraViewProjection);
                                    renderItemsSequence.Clear();
                                }

                                if (lastMaterial != null)
                                    lastMaterial.ResetState();
                                renderItem.Material.SetState(lastMaterial);
                                lastMaterial = renderItem.Material;
                            }
                            renderItemsSequence.Add(renderItem);
                        }
                        if (renderItemsSequence.Count > 0)
                        {
                            if (logging) Console.WriteLine("      {0,-5} * {1}", renderItemsSequence.Count, lastMaterial);
                            lastMaterial.Render(renderItemsSequence, ref viewRef, ref projectionRef, ref cameraViewProjection);
                            renderItemsSequence.Clear();
                        }

                        if (lastMaterial != null)
                            lastMaterial.ResetState();
                    }
                    else
                    {
                        if (game.Settings.DistantMountains && (sequenceMaterial.Key is TerrainSharedDistantMountain || sequenceMaterial.Key is SkyMaterial
                            || sequenceMaterial.Key is MSTSSkyMaterial))
                            continue;
                        // Opaque: single material, render in one go.
                        sequenceMaterial.Key.SetState(null);
                        if (logging) Console.WriteLine("      {0,-5} * {1}", sequenceMaterial.Value.Count, sequenceMaterial.Key);
                        sequenceMaterial.Key.Render(sequenceMaterial.Value, ref viewRef, ref projectionRef, ref cameraViewProjection);
                        sequenceMaterial.Key.ResetState();
                    }
                }
                if (logging) Console.WriteLine("    }");
            }

            if (dynamicShadows && (shadowMapCount > 0) && sceneryShader != null)
                sceneryShader.ClearShadowMap();
        }

        void DrawSequencesDistantMountains(bool logging)
        {
            Matrix mountainViewProjection;
            if (camera == null)
                MatrixExtension.Multiply(in identity, in Camera.XnaDistantMountainProjection, out mountainViewProjection);
            else
                MatrixExtension.Multiply(in camera.XnaView, in Camera.XnaDistantMountainProjection, out mountainViewProjection);

            for (var i = 0; i < (int)RenderPrimitiveSequence.Sentinel; i++)
            {
                if (logging) Console.WriteLine("    {0} {{", (RenderPrimitiveSequence)i);
                var sequence = renderItems[i];
                foreach (var sequenceMaterial in sequence)
                {
                    if (sequenceMaterial.Value.Count == 0)
                        continue;
                    if (sequenceMaterial.Key is TerrainSharedDistantMountain || sequenceMaterial.Key is SkyMaterial || sequenceMaterial.Key is MSTSSkyMaterial)
                    {
                        // Opaque: single material, render in one go.
                        sequenceMaterial.Key.SetState(null);
                        if (logging) Console.WriteLine("      {0,-5} * {1}", sequenceMaterial.Value.Count, sequenceMaterial.Key);
                        sequenceMaterial.Key.Render(sequenceMaterial.Value, ref camera.XnaView, ref Camera.XnaDistantMountainProjection, ref mountainViewProjection);
                        sequenceMaterial.Key.ResetState();
                    }
                }
                if (logging) Console.WriteLine("    }");
            }
        }
    }
}
