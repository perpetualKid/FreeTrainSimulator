﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

//#define SUPERSMOOTHNORMALS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D
{
    [DebuggerDisplay("Count = {TerrainTiles.Count}")]
    public class TerrainViewer
    {
        private readonly Viewer Viewer;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        private List<TerrainTile> TerrainTiles = new List<TerrainTile>();
        private Tile tile;
        private Tile visibleTile;

        public TerrainViewer(Viewer viewer)
        {
            Viewer = viewer;
        }

        public void Load()
        {
            var cancellation = Viewer.LoaderProcess.CancellationToken;

            if (tile != visibleTile)
            {
                tile = visibleTile;
                var terrainTiles = TerrainTiles;
                var newTerrainTiles = new List<TerrainTile>();

                var tiles = new List<TileSample>();
                var loTiles = new List<TileSample>();
                var needed = (int)Math.Ceiling(Viewer.UserSettings.ViewingDistance / 2048f);

                // First we establish the regular tiles we need to cover the current viewable area.
                for (var x = tile.X - needed; x <= tile.X + needed; x++)
                    for (var z = tile.Z - needed; z <= tile.Z + needed; z++)
                        if (!cancellation.IsCancellationRequested)
                            tiles.Add(Viewer.Tiles.LoadAndGetTile(x, z, x == tile.X && z == tile.Z));

                if (Viewer.UserSettings.FarMountainsViewingDistance > 0)
                {
                    // Second we establish the distant mountain/lo-resolution tiles we need.
                    needed = (int)Math.Ceiling((float)Viewer.UserSettings.FarMountainsViewingDistance / 2048);
                    for (var x = 8 * (int)((tile.X - needed) / 8); x <= 8 * (int)((tile.X + needed + 7) / 8); x += 8)
                        for (var z = 8 * (int)((tile.Z - needed) / 8); z <= 8 * (int)((tile.Z + needed + 7) / 8); z += 8)
                            if (!cancellation.IsCancellationRequested)
                                loTiles.Add(Viewer.LoTiles.LoadAndGetTile(x, z, false));
                }

                if (cancellation.IsCancellationRequested)
                    return;

                // Now we turn each unique (distinct) loaded tile in to a terrain tile.
                newTerrainTiles = tiles
                    .Where(t => t != null).Distinct()
                    .Select(tile => terrainTiles.FirstOrDefault(tt => tt.Tile == tile.Tile && tt.Size == tile.Size) ?? new TerrainTile(Viewer, Viewer.Tiles, tile))
                    .Union(loTiles
                        .Where(t => t != null).Distinct()
                        .Select(tile => terrainTiles.FirstOrDefault(tt => tt.Tile == tile.Tile && tt.Size == tile.Size) ?? new TerrainTile(Viewer, Viewer.LoTiles, tile))
                    ).ToList();

                TerrainTiles = newTerrainTiles;
            }
        }

        public void LoadPrep()
        {
            visibleTile = Viewer.Camera.Tile;
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var tiles = TerrainTiles;
            foreach (var tile in tiles)
                tile.PrepareFrame(frame, elapsedTime);
        }

        internal void Mark()
        {
            var tiles = TerrainTiles;
            foreach (var tile in tiles)
                tile.Mark();
        }

        public string GetStatus()
        {
            return Viewer.Catalog.GetPluralString("{0:F0} tile", "{0:F0} tiles", TerrainTiles.Count);
        }
    }

    [DebuggerDisplay("TileX = {Tile}, Size = {Size}")]
    public class TerrainTile
    {
        private readonly Tile tile;
        public ref readonly Tile Tile => ref tile;
        public int Size { get; }
        public int PatchCount { get; }

        private readonly TerrainPrimitive[,] TerrainPatches;
        private readonly WaterPrimitive WaterTile;

        public TerrainTile(Viewer viewer, TileManager tileManager, TileSample tile)
        {
            Trace.Write(tile.Size > 2 ? "L" : "T");
            this.tile = tile.Tile;
            Size = tile.Size;
            PatchCount = tile.PatchCount;

            // Terrain needs the next tiles over from its east (X+) and south (Z-) edges.
            viewer.Tiles.Load(Tile.X + tile.Size, Tile.Z, false);
            viewer.Tiles.Load(Tile.X + tile.Size, Tile.Z - 1, false);
            viewer.Tiles.Load(Tile.X, Tile.Z - 1, false);

            TerrainPatches = new TerrainPrimitive[PatchCount, PatchCount];
            for (var x = 0; x < PatchCount; ++x)
                for (var z = 0; z < PatchCount; ++z)
                    if (tile.GetPatch(x, z).DrawingEnabled)
                        TerrainPatches[x, z] = new TerrainPrimitive(viewer, tileManager, tile, x, z);

            if (tile.ContainsWater)
                WaterTile = new WaterPrimitive(viewer, tile);
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            WaterTile?.PrepareFrame(frame);

            for (var x = 0; x < PatchCount; ++x)
            {
                for (var z = 0; z < PatchCount; ++z)
                {
                    TerrainPatches[x, z]?.PrepareFrame(frame);
                }
            }
        }

        internal void Mark()
        {
            if (WaterTile != null)
                WaterPrimitive.Mark();

            for (var x = 0; x < PatchCount; ++x)
            {
                for (var z = 0; z < PatchCount; ++z)
                {
                    TerrainPatches[x, z]?.Mark();
                }
            }
        }
    }

    [DebuggerDisplay("TileX = {TileX}, TileZ = {TileZ}, Size = {Size}, PatchX = {PatchX}, PatchZ = {PatchZ}")]
    public class TerrainPrimitive : RenderPrimitive
    {
        private readonly Viewer Viewer;
        private readonly Tile tile;
        private readonly int Size, PatchX, PatchZ, PatchSize;
        private readonly float AverageElevation;
        private readonly Vector3 PatchLocation;        // In MSTS world coordinates relative to the center of the tile
        private readonly VertexBuffer PatchVertexBuffer;  // Separate vertex buffer for each patch
        private readonly IndexBuffer PatchIndexBuffer;    // Separate index buffer for each patch (if there are tunnels)
        private readonly int PatchPrimitiveCount;
        private readonly Material PatchMaterial;
        private readonly VertexBufferBinding[] VertexBufferBindings;

        // These can be shared since they are the same for all patches
        public static IndexBuffer SharedPatchIndexBuffer { get; private set; }

        // These are only used while the contructor runs and are discarded after.
        private readonly TileManager TileManager;
        private readonly TileSample Tile;
        private readonly Patch Patch;

        public TerrainPrimitive(Viewer viewer, TileManager tileManager, TileSample tileSample, int x, int z)
        {
            Viewer = viewer;
            this.tile = tileSample.Tile;
            Size = tileSample.Size;

            PatchX = x;
            PatchZ = z;
            PatchSize = tileSample.Size * 2048 / tileSample.PatchCount;

            TileManager = tileManager;
            Tile = tileSample;
            Patch = Tile.GetPatch(x, z);

            var cx = Patch.CenterX - 1024;
            var cz = Patch.CenterZ - 1024 + 2048 * tileSample.Size;
            PatchLocation = new Vector3(cx, Tile.Floor, cz);
            PatchVertexBuffer = GetVertexBuffer(out AverageElevation);
            PatchIndexBuffer = GetIndexBuffer(out PatchPrimitiveCount);

            var terrainMaterial = tileSample.Size > 2 ? "TerrainSharedDistantMountain" : PatchIndexBuffer == null ? "TerrainShared" : "Terrain";
            var ts = Tile.Shaders[Patch.ShaderIndex].Textureslots;
            var uv = Tile.Shaders[Patch.ShaderIndex].UVCalcs;
            if (ts.Count > 1)
                PatchMaterial = viewer.MaterialManager.Load(terrainMaterial, Helpers.GetTerrainTextureFile(ts[0].FileName) +
                    $"\0{Helpers.GetTerrainTextureFile(ts[1].FileName)}{(uv[1].D != 0 && uv[1].D != 32 ? $"\0{uv[1].D}" : "")}");
            else
                PatchMaterial = viewer.MaterialManager.Load(terrainMaterial, Helpers.GetTerrainTextureFile(ts[0].FileName) + "\0" + Helpers.GetTerrainTextureFile("microtex.ace"));

            if (SharedPatchIndexBuffer == null)
                SetupSharedData(Viewer.Game.GraphicsDevice);

            Tile = null;
            Patch = null;

            VertexBufferBindings = new[] { new VertexBufferBinding(PatchVertexBuffer), new VertexBufferBinding(GetDummyVertexBuffer(viewer.Game.GraphicsDevice)) };
        }

        public void PrepareFrame(RenderFrame frame)
        {
            var mstsLocation = PatchLocation + (tile - Viewer.Camera.Tile).TileVector();
            var xnaPatchMatrix = Matrix.CreateTranslation(mstsLocation.X, mstsLocation.Y, -mstsLocation.Z);
            mstsLocation.Y += AverageElevation; // Try to keep testing point somewhere useful within the patch's altitude.
            // Low-resolution terrain (Size > 2) should always be drawn (PositiveInfinity), while high-resolution terrain should only be drawn within the viewing distance (MaxValue).
            frame.AddAutoPrimitive(mstsLocation, PatchSize * 0.7071F, Size > 2 ? float.PositiveInfinity : float.MaxValue, PatchMaterial, this, RenderPrimitiveGroup.World, ref xnaPatchMatrix, Size <= 2 ? ShapeFlags.ShadowCaster : ShapeFlags.None);
        }

        public override void Draw()
        {
            graphicsDevice.SetVertexBuffers(VertexBufferBindings);
            if (PatchIndexBuffer != null)
                graphicsDevice.Indices = PatchIndexBuffer;
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PatchPrimitiveCount);
        }

        private float Elevation(int x, int z)
        {
            return TileManager.GetElevation(Tile, PatchX * 16 + x, PatchZ * 16 + z);
        }

        private bool IsVertexHidden(int x, int z)
        {
            return TileManager.IsVertexHidden(Tile, PatchX * 16 + x, PatchZ * 16 + z);
        }

        private Vector3 TerrainNormal(int x, int z)
        {
#if !SUPERSMOOTHNORMALS
            return SpecificTerrainNormal(x, z);
#else           
            var ourNormal = SpecificTerrainNormal(x, z);
            var centerWeight = 0.4f;

            var n = SpecificTerrainNormal(x, z - 1);
            var e = SpecificTerrainNormal(x + 1, z);
            var s = SpecificTerrainNormal(x, z + 1);
            var w = SpecificTerrainNormal(x - 1, z);
            
            if (x % 2 == z % 2)
            {                
                var ne = SpecificTerrainNormal(x + 1, z - 1);                
                var se = SpecificTerrainNormal(x + 1, z + 1);                
                var sw = SpecificTerrainNormal(x - 1, z + 1);                
                var nw = SpecificTerrainNormal(x - 1, z - 1);

                var restWeight = 1 - centerWeight;
                var neswWeight = restWeight * 0.66f;

                var neswAverage = Vector3.Normalize(n + e + s + w) * neswWeight;
                var othersAverage = Vector3.Normalize(ne + se + sw + nw) * (restWeight - neswWeight);
                return Vector3.Normalize((ourNormal * centerWeight) + neswAverage + othersAverage);
            }
            else
            {
                var restWeight = 1 - centerWeight;
                var neswAverage = Vector3.Normalize(n + e + s + w) * restWeight;
                return Vector3.Normalize((ourNormal * centerWeight) + neswAverage);
            }
#endif
        }

        private Vector3 SpecificTerrainNormal(int x, int z)
        {
            // TODO, decode this from the _N.RAW TILE
            // until I figure out this file, I'll compute normals from the terrain

            var d = Size * 8;
            var center = new Vector3(x, Elevation(x, z), z);

            var n = new Vector3(x + 0, Elevation(x + 0, z - 1), z - d);
            var toN = Vector3.Normalize(n - center);
            var e = new Vector3(x + d, Elevation(x + 1, z - 0), z - 0);
            var toE = Vector3.Normalize(e - center);
            var s = new Vector3(x - 0, Elevation(x - 0, z + 1), z + d);
            var toS = Vector3.Normalize(s - center);
            var w = new Vector3(x - d, Elevation(x - 1, z + 0), z + 0);
            var toW = Vector3.Normalize(w - center);

            if ((z & 1) == (x & 1))  // Triangles alternate
            {
                var ne = new Vector3(x + d, Elevation(x + 1, z - 1), z - d);
                var toNE = Vector3.Normalize(ne - center);
                var se = new Vector3(x + d, Elevation(x + 1, z + 1), z + d);
                var toSE = Vector3.Normalize(se - center);
                var sw = new Vector3(x - d, Elevation(x - 1, z + 1), z + d);
                var toSW = Vector3.Normalize(sw - center);
                var nw = new Vector3(x - d, Elevation(x - 1, z - 1), z - d);
                var toNW = Vector3.Normalize(nw - center);

                var nneFaceNormal = Vector3.Normalize(Vector3.Cross(toNE, toN));
                var eneFaceNormal = Vector3.Normalize(Vector3.Cross(toE, toNE));
                var eseFaceNormal = Vector3.Normalize(Vector3.Cross(toSE, toE));
                var sseFaceNormal = Vector3.Normalize(Vector3.Cross(toS, toSE));
                var sswFaceNormal = Vector3.Normalize(Vector3.Cross(toSW, toS));
                var wswFaceNormal = Vector3.Normalize(Vector3.Cross(toW, toSW));
                var wnwFaceNormal = Vector3.Normalize(Vector3.Cross(toNW, toW));
                var nnwFaceNormal = Vector3.Normalize(Vector3.Cross(toN, toNW));

                return Vector3.Normalize(nneFaceNormal + eneFaceNormal + eseFaceNormal + sseFaceNormal + sswFaceNormal + wswFaceNormal + wnwFaceNormal + nnwFaceNormal);
            }
            else
            {
                var neFaceNormal = Vector3.Normalize(Vector3.Cross(toE, toN));
                var seFaceNormal = Vector3.Normalize(Vector3.Cross(toS, toE));
                var swFaceNormal = Vector3.Normalize(Vector3.Cross(toW, toS));
                var nwFaceNormal = Vector3.Normalize(Vector3.Cross(toN, toW));

                return Vector3.Normalize(neFaceNormal + seFaceNormal + swFaceNormal + nwFaceNormal);
            }
        }

        private IndexBuffer GetIndexBuffer(out int primitiveCount)
        {
            const int bufferSize = 1536;    //16 * 16 * 2 * 3;
            int i = 0;
            short[] indexBuffer = new short[bufferSize];
            for (var z = 0; z < 16; ++z)
            {
                for (var x = 0; x < 16; ++x)
                {
                    var nw = (short)(z * 17 + x);  // Vertex index in the north west corner
                    var ne = (short)(nw + 1);
                    var sw = (short)(nw + 17);
                    var se = (short)(sw + 1);

                    bool vertexHiddenXZ = IsVertexHidden(x, z);
                    bool vertexHiddenX1Z1 = IsVertexHidden(x + 1, z + 1);
                    bool vertexHiddenX1Z = IsVertexHidden(x + 1, z);
                    bool vertexHiddenXZ1 = IsVertexHidden(x, z + 1);
                    if ((z & 1) == (x & 1))  // Triangles alternate
                    {
                        if (!vertexHiddenXZ && !vertexHiddenX1Z1 && !vertexHiddenXZ1)
                        {
                            indexBuffer[i++] = nw;
                            indexBuffer[i++] = se;
                            indexBuffer[i++] = sw;
                        }
                        if (!vertexHiddenXZ && !vertexHiddenX1Z && !vertexHiddenX1Z1)
                        {
                            indexBuffer[i++] = nw;
                            indexBuffer[i++] = ne;
                            indexBuffer[i++] = se;
                        }
                    }
                    else
                    {
                        if (!vertexHiddenX1Z && !vertexHiddenX1Z1 && !vertexHiddenXZ1)
                        {
                            indexBuffer[i++] = ne;
                            indexBuffer[i++] = se;
                            indexBuffer[i++] = sw;
                        }
                        if (!vertexHiddenXZ && !vertexHiddenX1Z && !vertexHiddenXZ1)
                        {
                            indexBuffer[i++] = nw;
                            indexBuffer[i++] = ne;
                            indexBuffer[i++] = sw;
                        }
                    }
                }
            }
            primitiveCount = i / 3;

            // If this patch has no holes, use the shared IndexBuffer for better performance.
            if (i == bufferSize) //16 * 16 * 6
                return null;

            IndexBuffer result = new IndexBuffer(Viewer.Game.GraphicsDevice, IndexElementSize.SixteenBits, i, BufferUsage.WriteOnly);
            result.SetData(indexBuffer, 0, i);
            return result;
        }

        private VertexBuffer GetVertexBuffer(out float averageElevation)
        {
            const int bufferSize = 289; //17*17
            int i = 0;
            var totalElevation = 0f;
            var vertexbuffer = new VertexPositionNormalTexture[bufferSize];
            var step = Tile.SampleSize;
            for (var z = 0; z < 17; ++z)
            {
                for (var x = 0; x < 17; ++x)
                {
                    var e = -Patch.RadiusM + x * step;
                    var n = -Patch.RadiusM + z * step;

                    var u = (float)x;
                    var v = (float)z;

                    // Rotate, Flip, and stretch the texture using the matrix coordinates stored in terrain_patchset_patch 
                    // transform uv by the 2x3 matrix made up of X,Y  W,B  C,H
                    var U = u * Patch.W + v * Patch.B + Patch.X;
                    var V = u * Patch.C + v * Patch.H + Patch.Y;

                    // V represents the north/south shift

                    var y = Elevation(x, z) - Tile.Floor;
                    totalElevation += y;
                    vertexbuffer[i++] = new VertexPositionNormalTexture(new Vector3(e, y, n), TerrainNormal(x, z), new Vector2(U, V));
                }
            }

            averageElevation = totalElevation / bufferSize;
            VertexBuffer result = new VertexBuffer(Viewer.Game.GraphicsDevice, typeof(VertexPositionNormalTexture), bufferSize, BufferUsage.WriteOnly);
            result.SetData(vertexbuffer);
            return result;
        }

        internal void Mark()
        {
            PatchMaterial.Mark();
        }

        private static void SetupSharedData(GraphicsDevice graphicsDevice)
        {
            // 16 x 16 squares * 2 triangles per square * 3 indices per triangle
            const int bufferSize = 1536;    //16 * 16 * 2 * 3;
            int i = 0;
            short[] indexBuffer = new short[bufferSize];

            // For each 8 meter rectangle
            for (var z = 0; z < 16; ++z)
            {
                for (var x = 0; x < 16; ++x)
                {
                    var nw = (short)(z * 17 + x);  // Vertex index in the north west corner
                    var ne = (short)(nw + 1);
                    var sw = (short)(nw + 17);
                    var se = (short)(sw + 1);

                    if ((z & 1) == (x & 1))  // Triangles alternate
                    {
                        indexBuffer[i++] = nw;
                        indexBuffer[i++] = se;
                        indexBuffer[i++] = sw;
                        indexBuffer[i++] = nw;
                        indexBuffer[i++] = ne;
                        indexBuffer[i++] = se;
                    }
                    else
                    {
                        indexBuffer[i++] = ne;
                        indexBuffer[i++] = se;
                        indexBuffer[i++] = sw;
                        indexBuffer[i++] = nw;
                        indexBuffer[i++] = ne;
                        indexBuffer[i++] = sw;
                    }
                }
            }

            SharedPatchIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, i, BufferUsage.WriteOnly);
            SharedPatchIndexBuffer.SetData(indexBuffer, 0, i);

        }
    }

    public class TerrainMaterial : Material
    {
        private readonly Texture2D patchTexture;
        private readonly Texture2D patchTextureOverlay;
        private readonly int OverlayScale;
        private readonly SceneryShader shader;
        private readonly int techniqueIndex;

        public TerrainMaterial(Viewer viewer, string terrainTexture, Texture2D defaultTexture)
            : base(viewer, terrainTexture)
        {
            var textures = terrainTexture.Split('\0');
            patchTexture = base.viewer.TextureManager.Get(textures[0], defaultTexture);
            patchTextureOverlay = textures.Length > 1 ? base.viewer.TextureManager.Get(textures[1]) : null;
            var converted = textures.Length > 2 && Int32.TryParse(textures[2], out OverlayScale);
            OverlayScale = OverlayScale != 0 && converted ? OverlayScale : 32;
            shader = base.viewer.MaterialManager.SceneryShader;
            for (int i = 0; i < shader.Techniques.Count; i++)
            {
                if (shader.Techniques[i].Name == "TerrainPS")
                {
                    techniqueIndex = i;
                    break;
                }
            }

        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[techniqueIndex];

            shader.ImageTexture = patchTexture;
            shader.OverlayTexture = patchTextureOverlay;
            shader.OverlayScale = OverlayScale;
            graphicsDevice.BlendState = BlendState.NonPremultiplied;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    shader.SetMatrix(in item.XNAMatrix, in viewProjection);
                    shader.ZBias = item.RenderPrimitive.ZBias;
                    pass.Apply();
                    graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
                    item.RenderPrimitive.Draw();

                }
            }
        }
        public override void ResetState()
        {
            graphicsDevice.BlendState = BlendState.Opaque;
        }

        public override void Mark()
        {
            viewer.TextureManager.Mark(patchTexture);
            viewer.TextureManager.Mark(patchTextureOverlay);
            base.Mark();
        }
    }

    public class TerrainSharedMaterial : TerrainMaterial
    {
        public TerrainSharedMaterial(Viewer viewer, string terrainTexture)
            : base(viewer, terrainTexture, Helpers.IsSnow() ? SharedMaterialManager.DefaultSnowTexture : SharedMaterialManager.MissingTexture)
        {
        }

        public override void SetState(Material previousMaterial)
        {
            base.SetState(previousMaterial);
            graphicsDevice.Indices = TerrainPrimitive.SharedPatchIndexBuffer;
        }
    }

    public class TerrainSharedDistantMountain : TerrainMaterial
    {
        public TerrainSharedDistantMountain(Viewer viewer, string terrainTexture)
            : base(viewer, terrainTexture, Helpers.IsSnow() ? SharedMaterialManager.DefaultDMSnowTexture : SharedMaterialManager.MissingTexture)
        {
        }

        public override void SetState(Material previousMaterial)
        {
            base.SetState(previousMaterial);
            graphicsDevice.Indices = TerrainPrimitive.SharedPatchIndexBuffer;

            graphicsDevice.BlendState = BlendState.Opaque; // Override the normal terrain blending!
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
        }

        public override void ResetState()
        {
            base.ResetState();

            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }
    }
}
