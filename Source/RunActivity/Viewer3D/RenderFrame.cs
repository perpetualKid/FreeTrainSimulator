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

// Define this to check every material is resetting the RenderState correctly.
//#define DEBUG_RENDER_STATE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Viewer3D.Processes;
using ORTS.Common;
using ORTS.Settings;
using Game = Orts.Viewer3D.Processes.Game;

namespace Orts.Viewer3D
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

    public enum ViewMatrixSequence
    {
        View = 0,
        Projection = 1,
        ViewProjection = 2,
    }

    public abstract class RenderPrimitive
    {
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
        /// <param name="graphicsDevice"></param>
        public abstract void Draw(GraphicsDevice graphicsDevice);
    }

    [DebuggerDisplay("{Material} {RenderPrimitive} {Flags}")]
    public struct RenderItem
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

                float temp = y.XNAMatrix.M41 - viewerPos.X;
                float distanceSquared = temp * temp;
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
        private readonly Matrix[] viewMatrices = new Matrix[3];

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

            viewMatrices[(int)ViewMatrixSequence.View] = Matrix.Identity; 
            viewMatrices[(int)ViewMatrixSequence.Projection] = Matrix.CreateOrthographic(game.RenderProcess.DisplaySize.X, game.RenderProcess.DisplaySize.Y, 1, 100);
            Matrix.Multiply(ref viewMatrices[0], ref viewMatrices[1], out viewMatrices[2]);

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

            viewMatrices[(int)ViewMatrixSequence.View] = camera.XnaView;
            viewMatrices[(int)ViewMatrixSequence.Projection] = camera.XnaProjection;
            Matrix.Multiply(ref viewMatrices[0], ref viewMatrices[1], out viewMatrices[2]);
            renderItemComparer.Update(cameraLocation);
        }

        static bool LockShadows;
        [CallOnThread("Updater")]
        public void PrepareFrame(ElapsedTime elapsedTime)
        {
            if (UserInput.IsPressed(UserCommands.DebugLockShadows))
                LockShadows = !LockShadows;

            if (dynamicShadows && (shadowMapCount > 0) && !LockShadows)
            {
                Vector3 normalizedSolarDirection = solarDirection;
                normalizedSolarDirection.Normalize();
                if (Vector3.Dot(steppedSolarDirection, normalizedSolarDirection) < 0.99999)
                    steppedSolarDirection = normalizedSolarDirection;

//                var cameraDirection = new Vector3(-cameraView.M13, -cameraView.M23, -cameraView.M33);
                var cameraDirection = new Vector3(-viewMatrices[(int)ViewMatrixSequence.View].M13, -viewMatrices[(int)ViewMatrixSequence.View].M23, -viewMatrices[(int)ViewMatrixSequence.View].M33);
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
                    Matrix.Multiply(ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex], out shadowMapLightViewProjection[shadowMapIndex]);
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
        [CallOnThread("Updater")]
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

        [CallOnThread("Updater")]
        public void AddPrimitive(Material material, RenderPrimitive primitive, RenderPrimitiveGroup group, ref Matrix xnaMatrix)
        {
            AddPrimitive(material, primitive, group, ref xnaMatrix, ShapeFlags.None);
        }

        static readonly bool[] PrimitiveBlendedScenery = new bool[] { true, false }; // Search for opaque pixels in alpha blended primitives, thus maintaining correct DepthBuffer
        static readonly bool[] PrimitiveBlended = new bool[] { true };
        static readonly bool[] PrimitiveNotBlended = new bool[] { false };

        [CallOnThread("Updater")]
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

        [CallOnThread("Updater")]
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

        [CallOnThread("Updater")]
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
//                    Debug.WriteLine($"Sorting {sequenceMaterial.ToString()} {sequenceMaterial.Value.Count} items");
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

        [CallOnThread("Render")]
        public void Draw(GraphicsDevice graphicsDevice)
        {
#if DEBUG_RENDER_STATE
			DebugRenderState(graphicsDevice, "RenderFrame.Draw");
#endif
            var logging = UserInput.IsPressed(UserCommands.DebugLogRenderFrame);
            if (logging)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Draw {");
            }
            if (dynamicShadows && (shadowMapCount > 0) && shadowMapMaterial != null)
                DrawShadows(graphicsDevice, logging);
            DrawSimple(graphicsDevice, logging);
            for (var i = 0; i < (int)RenderPrimitiveSequence.Sentinel; i++)
                game.RenderProcess.PrimitiveCount[i] = renderItems[i].Values.Sum(l => l.Count);

            if (logging)
            {
                Console.WriteLine("}");
                Console.WriteLine();
            }
        }

        void DrawShadows( GraphicsDevice graphicsDevice, bool logging )
        {
            if (logging) Console.WriteLine("  DrawShadows {");
            for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                DrawShadows(graphicsDevice, logging, shadowMapIndex);
            for (var shadowMapIndex = 0; shadowMapIndex < shadowMapCount; shadowMapIndex++)
                game.RenderProcess.ShadowPrimitiveCount[shadowMapIndex] = renderShadowSceneryItems[shadowMapIndex].Count + renderShadowForestItems[shadowMapIndex].Count + renderShadowTerrainItems[shadowMapIndex].Count;
            if (logging) Console.WriteLine("  }");
        }

        void DrawShadows(GraphicsDevice graphicsDevice, bool logging, int shadowMapIndex)
        {
            if (logging) Console.WriteLine("    {0} {{", shadowMapIndex);
            Matrix[] matrices = new Matrix[3];
            matrices[0] = shadowMapLightView[shadowMapIndex];
            matrices[1] = shadowMapLightProjection[shadowMapIndex];
            matrices[2] = shadowMapLightViewProjection[shadowMapIndex];

            // Prepare renderer for drawing the shadow map.
            graphicsDevice.SetRenderTarget(shadowMapRenderTarget[shadowMapIndex]);
            graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Target, Color.White, 1, 0);

            // Prepare for normal (non-blocking) rendering of scenery.
            shadowMapMaterial.SetState(graphicsDevice, ShadowMapMaterial.Mode.Normal);

            // Render non-terrain, non-forest shadow items first.
            if (logging) Console.WriteLine("      {0,-5} * SceneryMaterial (normal)", renderShadowSceneryItems[shadowMapIndex].Count);
//            shadowMapMaterial.Render(graphicsDevice, renderShadowSceneryItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(graphicsDevice, renderShadowSceneryItems[shadowMapIndex], matrices);

            // Prepare for normal (non-blocking) rendering of forests.
            shadowMapMaterial.SetState(graphicsDevice, ShadowMapMaterial.Mode.Forest);

            // Render forest shadow items next.
            if (logging) Console.WriteLine("      {0,-5} * ForestMaterial (forest)", renderShadowForestItems[shadowMapIndex].Count);
//            shadowMapMaterial.Render(graphicsDevice, renderShadowForestItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(graphicsDevice, renderShadowForestItems[shadowMapIndex], matrices);

            // Prepare for normal (non-blocking) rendering of terrain.
            shadowMapMaterial.SetState(graphicsDevice, ShadowMapMaterial.Mode.Normal);

            // Render terrain shadow items now, with their magic.
            if (logging) Console.WriteLine("      {0,-5} * TerrainMaterial (normal)", renderShadowTerrainItems[shadowMapIndex].Count);
            graphicsDevice.Indices = TerrainPrimitive.SharedPatchIndexBuffer;
//            shadowMapMaterial.Render(graphicsDevice, renderShadowTerrainItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(graphicsDevice, renderShadowTerrainItems[shadowMapIndex], matrices);

            // Prepare for blocking rendering of terrain.
            shadowMapMaterial.SetState(graphicsDevice, ShadowMapMaterial.Mode.Blocker);

            // Render terrain shadow items in blocking mode.
            if (logging) Console.WriteLine("      {0,-5} * TerrainMaterial (blocker)", renderShadowTerrainItems[shadowMapIndex].Count);
//            shadowMapMaterial.Render(graphicsDevice, renderShadowTerrainItems[shadowMapIndex], ref shadowMapLightView[shadowMapIndex], ref shadowMapLightProjection[shadowMapIndex]);
            shadowMapMaterial.Render(graphicsDevice, renderShadowTerrainItems[shadowMapIndex], matrices);

            // All done.
            shadowMapMaterial.ResetState(graphicsDevice);
#if DEBUG_RENDER_STATE
            DebugRenderState(graphicsDevice, ShadowMapMaterial.ToString());
#endif
            graphicsDevice.SetRenderTarget(null);

            // Blur the shadow map.
            if (game.Settings.ShadowMapBlur)
            {
                //shadowMap[shadowMapIndex] = 
                shadowMapMaterial.ApplyBlur(graphicsDevice, shadowMap[shadowMapIndex], shadowMapRenderTarget[shadowMapIndex]);
#if DEBUG_RENDER_STATE
                DebugRenderState(graphicsDevice, ShadowMapMaterial.ToString() + " ApplyBlur()");
#endif
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
        void DrawSimple(GraphicsDevice graphicsDevice, bool logging)
        {
            if (game.Settings.DistantMountains)
            {
                if (logging) Console.WriteLine("  DrawSimple (Distant Mountains) {");
                graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Transparent, 1, 0);
                DrawSequencesDistantMountains(graphicsDevice, logging);
                if (logging) Console.WriteLine("  }");
                if (logging) Console.WriteLine("  DrawSimple {");
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                DrawSequences(graphicsDevice, logging);
                if (logging) Console.WriteLine("  }");
            }
            else
            {
                if (logging) Console.WriteLine("  DrawSimple {");
                graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Transparent, 1, 0);
                DrawSequences(graphicsDevice, logging);
                if (logging) Console.WriteLine("  }");
            }
        }

        void DrawSequences(GraphicsDevice graphicsDevice, bool logging)
        {
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
                                    lastMaterial.Render(graphicsDevice, renderItemsSequence, viewMatrices);
                                    renderItemsSequence.Clear();
                                }

                                if (lastMaterial != null)
                                    lastMaterial.ResetState(graphicsDevice);
#if DEBUG_RENDER_STATE
								if (lastMaterial != null)
									DebugRenderState(graphicsDevice, lastMaterial.ToString());
#endif
                                renderItem.Material.SetState(graphicsDevice, lastMaterial);
                                lastMaterial = renderItem.Material;
                            }
                            renderItemsSequence.Add(renderItem);
                        }
                        if (renderItemsSequence.Count > 0)
                        {
                            if (logging) Console.WriteLine("      {0,-5} * {1}", renderItemsSequence.Count, lastMaterial);
                            lastMaterial.Render(graphicsDevice, renderItemsSequence, viewMatrices);
                            renderItemsSequence.Clear();
                        }

                        if (lastMaterial != null)
                            lastMaterial.ResetState(graphicsDevice);
#if DEBUG_RENDER_STATE
						if (lastMaterial != null)
							DebugRenderState(graphicsDevice, lastMaterial.ToString());
#endif
                    }
                    else
                    {
                        if (game.Settings.DistantMountains && (sequenceMaterial.Key is TerrainSharedDistantMountain || sequenceMaterial.Key is SkyMaterial
                            || sequenceMaterial.Key is MSTSSkyMaterial))
                            continue;
                        // Opaque: single material, render in one go.
                        sequenceMaterial.Key.SetState(graphicsDevice, null);
                        if (logging) Console.WriteLine("      {0,-5} * {1}", sequenceMaterial.Value.Count, sequenceMaterial.Key);
                        sequenceMaterial.Key.Render(graphicsDevice, sequenceMaterial.Value, viewMatrices);
                        sequenceMaterial.Key.ResetState(graphicsDevice);
#if DEBUG_RENDER_STATE
						DebugRenderState(graphicsDevice, sequenceMaterial.Key.ToString());
#endif
                    }
                }
                if (logging) Console.WriteLine("    }");
            }

            if (dynamicShadows && (shadowMapCount > 0) && sceneryShader != null)
                sceneryShader.ClearShadowMap();
        }

        void DrawSequencesDistantMountains(GraphicsDevice graphicsDevice, bool logging)
        {
            Matrix[] mountainViewMatrices = new Matrix[3];
            mountainViewMatrices[0] = viewMatrices[0];
            mountainViewMatrices[1] = Camera.XnaDistantMountainProjection;
            Matrix.Multiply(ref mountainViewMatrices[0], ref mountainViewMatrices[1], out mountainViewMatrices[2]);

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
                        sequenceMaterial.Key.SetState(graphicsDevice, null);
                        if (logging) Console.WriteLine("      {0,-5} * {1}", sequenceMaterial.Value.Count, sequenceMaterial.Key);
                        sequenceMaterial.Key.Render(graphicsDevice, sequenceMaterial.Value, mountainViewMatrices);
                        sequenceMaterial.Key.ResetState(graphicsDevice);
#if DEBUG_RENDER_STATE
						DebugRenderState(graphicsDevice.RenderState, sequenceMaterial.Key.ToString());
#endif
                    }
                }
                if (logging) Console.WriteLine("    }");
            }
        }

#if DEBUG_RENDER_STATE
        static void DebugRenderState(RenderState renderState, string location)
        {
            //if (renderState.AlphaBlendEnable != false) throw new InvalidOperationException(String.Format("RenderState.AlphaBlendEnable is {0}; expected {1} in {2}.", renderState.AlphaBlendEnable, false, location));
            if (graphicsDevice.BlendState.AlphaBlendFunction != BlendFunction.Add) throw new InvalidOperationException(String.Format("RenderState.AlphaBlendOperation is {0}; expected {1} in {2}.", graphicsDevice.BlendState.AlphaBlendFunction, BlendFunction.Add, location));
            // DOCUMENTATION IS WRONG, it says Blend.One:
            if (graphicsDevice.BlendState.AlphaDestinationBlend != Blend.Zero) throw new InvalidOperationException(String.Format("RenderState.AlphaDestinationBlend is {0}; expected {1} in {2}.", graphicsDevice.BlendState.AlphaDestinationBlend, Blend.Zero, location));
            //if (renderState.AlphaFunction != CompareFunction.Always) throw new InvalidOperationException(String.Format("RenderState.AlphaFunction is {0}; expected {1} in {2}.", renderState.AlphaFunction, CompareFunction.Always, location));
            if (graphicsDevice.BlendState.AlphaSourceBlend != Blend.One) throw new InvalidOperationException(String.Format("RenderState.AlphaSourceBlend is {0}; expected {1} in {2}.", graphicsDevice.BlendState.AlphaSourceBlend, Blend.One, location));
            //if (renderState.AlphaTestEnable != false) throw new InvalidOperationException(String.Format("RenderState.AlphaTestEnable is {0}; expected {1} in {2}.", renderState.AlphaTestEnable, false, location));
            if (graphicsDevice.BlendFactor != Color.White) throw new InvalidOperationException(String.Format("RenderState.BlendFactor is {0}; expected {1} in {2}.", graphicsDevice.BlendFactor, Color.White, location));
            if (graphicsDevice.BlendState.ColorBlendFunction != BlendFunction.Add) throw new InvalidOperationException(String.Format("RenderState.BlendFunction is {0}; expected {1} in {2}.",graphicsDevice.BlendState.ColorBlendFunction, BlendFunction.Add, location));
            // DOCUMENTATION IS WRONG, it says ColorWriteChannels.None:
            if (graphicsDevice.BlendState.ColorWriteChannels != ColorWriteChannels.All) throw new InvalidOperationException(String.Format("RenderState.ColorWriteChannels is {0}; expected {1} in {2}.", graphicsDevice.BlendState.ColorWriteChannels, ColorWriteChannels.All, location));
            // DOCUMENTATION IS WRONG, it says ColorWriteChannels.None:
            if (graphicsDevice.BlendState.ColorWriteChannels1 != ColorWriteChannels.All) throw new InvalidOperationException(String.Format("RenderState.ColorWriteChannels1 is {0}; expected {1} in {2}.", graphicsDevice.BlendState.ColorWriteChannels1, ColorWriteChannels.All, location));
            // DOCUMENTATION IS WRONG, it says ColorWriteChannels.None:
            if (graphicsDevice.BlendState.ColorWriteChannels2 != ColorWriteChannels.All) throw new InvalidOperationException(String.Format("RenderState.ColorWriteChannels2 is {0}; expected {1} in {2}.", graphicsDevice.BlendState.ColorWriteChannels2, ColorWriteChannels.All, location));
            // DOCUMENTATION IS WRONG, it says ColorWriteChannels.None:
            if (graphicsDevice.BlendState.ColorWriteChannels3 != ColorWriteChannels.All) throw new InvalidOperationException(String.Format("RenderState.ColorWriteChannels3 is {0}; expected {1} in {2}.", graphicsDevice.BlendState.ColorWriteChannels3, ColorWriteChannels.All, location));
            if (graphicsDevice.DepthStencilState.CounterClockwiseStencilDepthBufferFail != StencilOperation.Keep) throw new InvalidOperationException(String.Format("RenderState.CounterClockwiseStencilDepthBufferFail is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.CounterClockwiseStencilDepthBufferFail, StencilOperation.Keep, location));
            if (graphicsDevice.DepthStencilState.CounterClockwiseStencilFail != StencilOperation.Keep) throw new InvalidOperationException(String.Format("RenderState.CounterClockwiseStencilFail is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.CounterClockwiseStencilFail, StencilOperation.Keep, location));
            if (graphicsDevice.DepthStencilState.CounterClockwiseStencilFunction != CompareFunction.Always) throw new InvalidOperationException(String.Format("RenderState.CounterClockwiseStencilFunction is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.CounterClockwiseStencilFunction, CompareFunction.Always, location));
            if (graphicsDevice.DepthStencilState.CounterClockwiseStencilPass != StencilOperation.Keep) throw new InvalidOperationException(String.Format("RenderState.CounterClockwiseStencilPass is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.CounterClockwiseStencilPass, StencilOperation.Keep, location));
            if (graphicsDevice.RasterizerState.CullMode != CullMode.CullCounterClockwiseFace) throw new InvalidOperationException(String.Format("RenderState.CullMode is {0}; expected {1} in {2}.", graphicsDevice.RasterizerState.CullMode, CullMode.CullCounterClockwiseFace, location));
            if (graphicsDevice.RasterizerState.DepthBias != 0.0f) throw new InvalidOperationException(String.Format("RenderState.DepthBias is {0}; expected {1} in {2}.", graphicsDevice.RasterizerState.DepthBias, 0.0f, location));
            if (graphicsDevice.DepthStencilState.DepthBufferEnable != true) throw new InvalidOperationException(String.Format("RenderState.DepthBufferEnable is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.DepthBufferEnable, true, location));
            if (graphicsDevice.DepthStencilState.DepthBufferFunction != CompareFunction.LessEqual) throw new InvalidOperationException(String.Format("RenderState.DepthBufferFunction is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.DepthBufferFunction, CompareFunction.LessEqual, location));
            if (graphicsDevice.DepthStencilState.DepthBufferWriteEnable != true) throw new InvalidOperationException(String.Format("RenderState.DepthBufferWriteEnable is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.DepthBufferWriteEnable, true, location));
            if (graphicsDevice.BlendState.ColorDestinationBlend != Blend.Zero) throw new InvalidOperationException(String.Format("RenderState.DestinationBlend is {0}; expected {1} in {2}.", graphicsDevice.BlendState.ColorDestinationBlend, Blend.Zero, location));
            if (graphicsDevice.RasterizerState.FillMode != FillMode.Solid) throw new InvalidOperationException(String.Format("RenderState.FillMode is {0}; expected {1} in {2}.", graphicsDevice.RasterizerState.FillMode, FillMode.Solid, location));
            //if (renderState.FogColor != Color.TransparentBlack) throw new InvalidOperationException(String.Format("RenderState.FogColor is {0}; expected {1} in {2}.", renderState.FogColor, Color.TransparentBlack, location));
            //if (renderState.FogDensity != 1.0f) throw new InvalidOperationException(String.Format("RenderState.FogDensity is {0}; expected {1} in {2}.", renderState.FogDensity, 1.0f, location));
            //if (renderState.FogEnable != false) throw new InvalidOperationException(String.Format("RenderState.FogEnable is {0}; expected {1} in {2}.", renderState.FogEnable, false, location));
            //if (renderState.FogEnd != 1.0f) throw new InvalidOperationException(String.Format("RenderState.FogEnd is {0}; expected {1} in {2}.", renderState.FogEnd, 1.0f, location));
            //if (renderState.FogStart != 0.0f) throw new InvalidOperationException(String.Format("RenderState.FogStart is {0}; expected {1} in {2}.", renderState.FogStart, 0.0f, location));
            //if (renderState.FogTableMode != FogMode.None) throw new InvalidOperationException(String.Format("RenderState.FogTableMode is {0}; expected {1} in {2}.", renderState.FogTableMode, FogMode.None, location));
            //if (renderState.FogVertexMode != FogMode.None) throw new InvalidOperationException(String.Format("RenderState.FogVertexMode is {0}; expected {1} in {2}.", renderState.FogVertexMode, FogMode.None, location));
            if (graphicsDevice.RasterizerState.MultiSampleAntiAlias != true) throw new InvalidOperationException(String.Format("RenderState.MultiSampleAntiAlias is {0}; expected {1} in {2}.", graphicsDevice.RasterizerState.MultiSampleAntiAlias, true, location));
            if (graphicsDevice.BlendState.MultiSampleMask != -1) throw new InvalidOperationException(String.Format("RenderState.MultiSampleMask is {0}; expected {1} in {2}.", graphicsDevice.BlendState.MultiSampleMask, -1, location));
            ////if (renderState.PointSize != 64) throw new InvalidOperationException(String.Format("RenderState.e.PointSize is {0}; expected {1} in {2}.", renderState.e.PointSize, 64, location));
            ////if (renderState.PointSizeMax != 64.0f) throw new InvalidOperationException(String.Format("RenderState.PointSizeMax is {0}; expected {1} in {2}.", renderState.PointSizeMax, 64.0f, location));
            ////if (renderState.PointSizeMin != 1.0f) throw new InvalidOperationException(String.Format("RenderState.PointSizeMin is {0}; expected {1} in {2}.", renderState.PointSizeMin, 1.0f, location));
            //if (renderState.PointSpriteEnable != false) throw new InvalidOperationException(String.Format("RenderState.PointSpriteEnable is {0}; expected {1} in {2}.", renderState.PointSpriteEnable, false, location));
            //if (renderState.RangeFogEnable != false) throw new InvalidOperationException(String.Format("RenderState.RangeFogEnable is {0}; expected {1} in {2}.", renderState.RangeFogEnable, false, location));
            //if (renderState.ReferenceAlpha != 0) throw new InvalidOperationException(String.Format("RenderState.ReferenceAlpha is {0}; expected {1} in {2}.", renderState.ReferenceAlpha, 0, location));
            if (graphicsDevice.DepthStencilState.ReferenceStencil != 0) throw new InvalidOperationException(String.Format("RenderState.ReferenceStencil is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.ReferenceStencil, 0, location));
            if (graphicsDevice.RasterizerState.ScissorTestEnable != false) throw new InvalidOperationException(String.Format("RenderState.ScissorTestEnable is {0}; expected {1} in {2}.", graphicsDevice.RasterizerState.ScissorTestEnable, false, location));
            //if (renderState.SeparateAlphaBlendEnabled != false) throw new InvalidOperationException(String.Format("RenderState.SeparateAlphaBlendEnabled is {0}; expected {1} in {2}.", renderState.SeparateAlphaBlendEnabled, false, location));
            if (graphicsDevice.RasterizerState.SlopeScaleDepthBias != 0) throw new InvalidOperationException(String.Format("RenderState.SlopeScaleDepthBias is {0}; expected {1} in {2}.", graphicsDevice.RasterizerState.SlopeScaleDepthBias, 0, location));
            if (graphicsDevice.BlendState.ColorSourceBlend != Blend.One) throw new InvalidOperationException(String.Format("RenderState.SourceBlend is {0}; expected {1} in {2}.", graphicsDevice.BlendState.ColorSourceBlend, Blend.One, location));
            if (graphicsDevice.DepthStencilState.StencilDepthBufferFail != StencilOperation.Keep) throw new InvalidOperationException(String.Format("RenderState.StencilDepthBufferFail is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilDepthBufferFail, StencilOperation.Keep, location));
            if (graphicsDevice.DepthStencilState.StencilEnable != false) throw new InvalidOperationException(String.Format("RenderState.StencilEnable is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilEnable, false, location));
            if (graphicsDevice.DepthStencilState.StencilFail != StencilOperation.Keep) throw new InvalidOperationException(String.Format("RenderState.StencilFail is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilFail, StencilOperation.Keep, location));
            if (graphicsDevice.DepthStencilState.StencilFunction != CompareFunction.Always) throw new InvalidOperationException(String.Format("RenderState.StencilFunction is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilFunction, CompareFunction.Always, location));
            // DOCUMENTATION IS WRONG, it says Int32.MaxValue:
            if (graphicsDevice.DepthStencilState.StencilMask != -1) throw new InvalidOperationException(String.Format("RenderState.StencilMask is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilMask, -1, location));
            if (graphicsDevice.DepthStencilState.StencilPass != StencilOperation.Keep) throw new InvalidOperationException(String.Format("RenderState.StencilPass is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilPass, StencilOperation.Keep, location));
            // DOCUMENTATION IS WRONG, it says Int32.MaxValue:
            if (graphicsDevice.DepthStencilState.StencilWriteMask != -1) throw new InvalidOperationException(String.Format("RenderState.StencilWriteMask is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.StencilWriteMask, -1, location));
            if (graphicsDevice.DepthStencilState.TwoSidedStencilMode != false) throw new InvalidOperationException(String.Format("RenderState.TwoSidedStencilMode is {0}; expected {1} in {2}.", graphicsDevice.DepthStencilState.TwoSidedStencilMode, false, location));
        }
#endif
    }
}
