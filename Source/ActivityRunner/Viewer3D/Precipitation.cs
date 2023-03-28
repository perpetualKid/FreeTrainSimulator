﻿// COPYRIGHT 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Shaders;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Simulation;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D
{
    public class PrecipitationViewer
    {
        public const float MinIntensityPPSPM2 = 0;
        // Default 32 bit version.
        public const float MaxIntensityPPSPM2 = 0.035f;
        private readonly Viewer viewer;
        private readonly WeatherControl weatherControl;
        private readonly Weather weather;
        private readonly Material material;
        private readonly PrecipitationPrimitive precipitation;
        private Vector3 Wind;

        public PrecipitationViewer(Viewer viewer, WeatherControl weatherControl)
        {
            ArgumentNullException.ThrowIfNull(viewer);

            this.viewer = viewer;
            this.weatherControl = weatherControl;
            weather = viewer.Simulator.Weather;

            material = viewer.MaterialManager.Load("Precipitation");
            precipitation = new PrecipitationPrimitive(this.viewer.Game.GraphicsDevice);

            Wind = new Vector3(0, 0, 0);
            Reset();
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var gameTime = (float)viewer.Simulator.GameTime;
            precipitation.DynamicUpdate(weatherControl, weather, viewer, ref Wind);
            precipitation.Update(gameTime, elapsedTime, weather.PrecipitationIntensity, viewer);

            // Note: This is quite a hack. We ideally should be able to pass this through RenderItem somehow.
            var XNAWorldLocation = Matrix.Identity;
            XNAWorldLocation.M11 = gameTime;
            XNAWorldLocation.M21 = viewer.Camera.TileX;
            XNAWorldLocation.M22 = viewer.Camera.TileZ;

            frame.AddPrimitive(material, precipitation, RenderPrimitiveGroup.Precipitation, ref XNAWorldLocation);
        }

        public void Reset()
        {
            // This procedure is only called once at the start of an activity.
            // Added random Wind.X value for rain and snow.
            // Max value used by randWind.Next is max value - 1.
            Wind.X = viewer.Simulator.WeatherType == WeatherType.Snow ? StaticRandom.Next(2, 6) : StaticRandom.Next(15, 21);

            var gameTime = (float)viewer.Simulator.GameTime;
            precipitation.Initialize(viewer.Simulator.WeatherType, Wind);
            // Camera is null during first initialisation.
            if (viewer.Camera != null)
                precipitation.Update(gameTime, ElapsedTime.Zero, weather.PrecipitationIntensity, viewer);
        }

        internal void Mark()
        {
            material.Mark();
        }
    }

    public class PrecipitationPrimitive : RenderPrimitive
    {
        // http://www-das.uwyo.edu/~geerts/cwx/notes/chap09/hydrometeor.html
        // "Rain  1.8 - 2.2mm  6.1 - 6.9m/s"
        private const float RainVelocityMpS = 6.9f;

        // "Snow flakes of any size falls at about 1 m/s"
        private const float SnowVelocityMpS = 1.0f;

        // This is a fiddle factor because the above values feel too slow. Alternative suggestions welcome.
        private const float ParticleVelocityFactor = 10.0f;
        private readonly float ParticleBoxLengthM;
        private readonly float ParticleBoxWidthM;
        private readonly float ParticleBoxHeightM;

        // 16bit Box Parameters
        private const int IndiciesPerParticle = 6;
        private const int VerticiesPerParticle = 4;
        private const int PrimitivesPerParticle = 2;
        private readonly int MaxParticles;
        private readonly ParticleVertex[] Vertices;
        private readonly VertexDeclaration VertexDeclaration;
        private readonly int VertexStride;
        private readonly DynamicVertexBuffer VertexBuffer;
        private readonly IndexBuffer IndexBuffer;

        private struct ParticleVertex
        {
            public Vector4 StartPosition_StartTime;
            public Vector4 EndPosition_EndTime;
            public Vector4 TileXZ_Vertex;

            public static readonly VertexElement[] VertexElements =
            {
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.Position, 1),
                new VertexElement(16 + 16, VertexElementFormat.Vector4, VertexElementUsage.Position, 2),
            };

            public static int SizeInBytes = sizeof(float) * (4 + 4) + sizeof(float) * 4;
        }

        private float particleDuration;
        private Vector3 particleDirection;
        private HeightCache heights;

        // Particle buffer goes like this:
        //   +--active>-----new>--+
        //   |                    |
        //   +--<retired---<free--+

        private int firstActiveParticle;
        private int firstNewParticle;
        private int firstFreeParticle;
        private int firstRetiredParticle;
        private float particlesToEmit;
        private float timeParticlesLastEmitted;
        private int drawCounter;

        public PrecipitationPrimitive(GraphicsDevice graphicsDevice)
        {
            // Snow is the slower particle, hence longer duration, hence more particles in total.
            // Setting the precipitaton box size based on GraphicsDeviceCapabilities.
            ParticleBoxLengthM = (float)Simulator.Instance.Settings.PrecipitationBoxLength;
            ParticleBoxWidthM = (float)Simulator.Instance.Settings.PrecipitationBoxWidth;
            ParticleBoxHeightM = (float)Simulator.Instance.Settings.PrecipitationBoxHeight;
            MaxParticles = (int)(PrecipitationViewer.MaxIntensityPPSPM2 * ParticleBoxLengthM * ParticleBoxWidthM * ParticleBoxHeightM / SnowVelocityMpS / ParticleVelocityFactor);
            // Checking if graphics device is 16bit.
            Vertices = new ParticleVertex[MaxParticles * VerticiesPerParticle];
            VertexDeclaration = new VertexDeclaration(ParticleVertex.SizeInBytes, ParticleVertex.VertexElements);
            VertexStride = Marshal.SizeOf(typeof(ParticleVertex));
            VertexBuffer = new DynamicVertexBuffer(graphicsDevice, VertexDeclaration, MaxParticles * VerticiesPerParticle, BufferUsage.WriteOnly);
            // Processing either 32bit or 16bit InitIndexBuffer depending on GraphicsDeviceCapabilities.
            IndexBuffer = InitIndexBuffer(graphicsDevice, MaxParticles * IndiciesPerParticle);
            heights = new HeightCache(8);
            // This Trace command is used to show how much memory is used.
            Trace.TraceInformation(string.Format(System.Globalization.CultureInfo.CurrentCulture, "Allocation for {0:N0} particles:\n\n  {1,13:N0} B RAM vertex data\n  {2,13:N0} B RAM index data (temporary)\n  {1,13:N0} B VRAM DynamicVertexBuffer\n  {2,13:N0} B VRAM IndexBuffer", MaxParticles, Marshal.SizeOf(typeof(ParticleVertex)) * MaxParticles * VerticiesPerParticle, sizeof(uint) * MaxParticles * IndiciesPerParticle));
        }

        private void VertexBuffer_ContentLost()
        {
            VertexBuffer.SetData(0, Vertices, 0, Vertices.Length, VertexStride, SetDataOptions.NoOverwrite);
        }

        // IndexBuffer for 32bit process.
        private static IndexBuffer InitIndexBuffer(GraphicsDevice graphicsDevice, int numIndicies)
        {
            var indices = new uint[numIndicies];
            var index = 0;
            for (var i = 0; i < numIndicies; i += IndiciesPerParticle)
            {
                indices[i] = (uint)index;
                indices[i + 1] = (uint)(index + 1);
                indices[i + 2] = (uint)(index + 2);

                indices[i + 3] = (uint)(index + 2);
                indices[i + 4] = (uint)(index + 3);
                indices[i + 5] = (uint)(index);

                index += VerticiesPerParticle;
            }
            var indexBuffer = new IndexBuffer(graphicsDevice, typeof(uint), numIndicies, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
            return indexBuffer;
        }

        private void RetireActiveParticles(float currentTime)
        {
            while (firstActiveParticle != firstNewParticle)
            {
                var vertex = firstActiveParticle * VerticiesPerParticle;
                var expiry = Vertices[vertex].EndPosition_EndTime.W;

                // Stop as soon as we find the first particle which hasn't expired.
                if (expiry > currentTime)
                    break;

                // Expire particle.
                Vertices[vertex].StartPosition_StartTime.W = (float)drawCounter;
                firstActiveParticle = (firstActiveParticle + 1) % MaxParticles;
            }
        }

        private void FreeRetiredParticles()
        {
            while (firstRetiredParticle != firstActiveParticle)
            {
                var vertex = firstRetiredParticle * VerticiesPerParticle;
                var age = drawCounter - (int)Vertices[vertex].StartPosition_StartTime.W;

                // Stop as soon as we find the first expired particle which hasn't been expired for at least 2 'ticks'.
                if (age < 2)
                    break;

                firstRetiredParticle = (firstRetiredParticle + 1) % MaxParticles;
            }
        }

        private int GetCountFreeParticles()
        {
            var nextFree = (firstFreeParticle + 1) % MaxParticles;

            if (nextFree <= firstRetiredParticle)
                return firstRetiredParticle - nextFree;

            return (MaxParticles - nextFree) + firstRetiredParticle;
        }

        public void Initialize(WeatherType weather, Vector3 wind)
        {
            particleDuration = ParticleBoxHeightM / (weather == WeatherType.Snow ? SnowVelocityMpS : RainVelocityMpS) / ParticleVelocityFactor;
            particleDirection = wind;
            firstActiveParticle = firstNewParticle = firstFreeParticle = firstRetiredParticle = 0;
            particlesToEmit = timeParticlesLastEmitted = 0;
            drawCounter = 0;
        }

        public void DynamicUpdate(WeatherControl weatherControl, Weather weather, Viewer viewer, ref Vector3 wind)
        {
            if (!weatherControl.weatherChangeOn || weatherControl.dynamicWeather.precipitationLiquidityTimer <= 0)
                return;
            particleDuration = ParticleBoxHeightM / ((RainVelocityMpS - SnowVelocityMpS) * weather.PrecipitationLiquidity + SnowVelocityMpS) / ParticleVelocityFactor;
            wind.X = 18 * weather.PrecipitationLiquidity + 2;
            particleDirection = wind;
        }

        public void Update(float currentTime, in ElapsedTime elapsedTime, float particlesPerSecondPerM2, Viewer viewer)
        {
            var tiles = viewer.Tiles;
            var scenery = viewer.World.Scenery;
            var worldLocation = viewer.Camera.CameraWorldLocation;
            //var worldLocation = Program.Viewer.PlayerLocomotive.WorldPosition.WorldLocation;  // This is used to test overall precipitation position.

            if (timeParticlesLastEmitted == 0)
            {
                timeParticlesLastEmitted = currentTime - particleDuration;
                particlesToEmit += particleDuration * particlesPerSecondPerM2 * ParticleBoxLengthM * ParticleBoxWidthM;
            }
            else
            {
                RetireActiveParticles(currentTime);
                FreeRetiredParticles();

                particlesToEmit += (float)elapsedTime.ClockSeconds * particlesPerSecondPerM2 * ParticleBoxLengthM * ParticleBoxWidthM;
            }

            var numParticlesAdded = 0;
            var numToBeEmitted = (int)particlesToEmit;
            var numCanBeEmitted = GetCountFreeParticles();
            var numToEmit = Math.Min(numToBeEmitted, numCanBeEmitted);

            for (var i = 0; i < numToEmit; i++)
            {
                WorldLocation temp = new WorldLocation(worldLocation.TileX, worldLocation.TileZ,
                    worldLocation.Location.X + (float)((StaticRandom.NextDouble() - 0.5) * ParticleBoxWidthM),
                    0,
                    worldLocation.Location.Z + (float)((StaticRandom.NextDouble() - 0.5) * ParticleBoxLengthM));
                temp = new WorldLocation(temp.TileX, temp.TileZ, temp.Location.X, heights.GetHeight(temp, tiles, scenery), temp.Location.Z);
                var position = new WorldPosition(temp);

                var time = MathHelper.Lerp(timeParticlesLastEmitted, currentTime, (float)i / numToEmit);
                var particle = (firstFreeParticle + 1) % MaxParticles;
                var vertex = particle * VerticiesPerParticle;

                for (var j = 0; j < VerticiesPerParticle; j++)
                {
                    Vertices[vertex + j].StartPosition_StartTime = new Vector4(position.XNAMatrix.Translation - particleDirection * particleDuration, time);
                    Vertices[vertex + j].StartPosition_StartTime.Y += ParticleBoxHeightM;
                    Vertices[vertex + j].EndPosition_EndTime = new Vector4(position.XNAMatrix.Translation, time + particleDuration);
                    Vertices[vertex + j].TileXZ_Vertex = new Vector4(position.TileX, position.TileZ, j, 0);
                }

                firstFreeParticle = particle;
                particlesToEmit--;
                numParticlesAdded++;
            }

            if (numParticlesAdded > 0)
                timeParticlesLastEmitted = currentTime;

            particlesToEmit = particlesToEmit - (int)particlesToEmit;
        }

        private void AddNewParticlesToVertexBuffer()
        {
            if (firstNewParticle < firstFreeParticle)
            {
                var numParticlesToAdd = firstFreeParticle - firstNewParticle;
                VertexBuffer.SetData(firstNewParticle * VertexStride * VerticiesPerParticle, Vertices, firstNewParticle * VerticiesPerParticle, numParticlesToAdd * VerticiesPerParticle, VertexStride, SetDataOptions.NoOverwrite);
            }
            else
            {
                var numParticlesToAddAtEnd = MaxParticles - firstNewParticle;
                VertexBuffer.SetData(firstNewParticle * VertexStride * VerticiesPerParticle, Vertices, firstNewParticle * VerticiesPerParticle, numParticlesToAddAtEnd * VerticiesPerParticle, VertexStride, SetDataOptions.NoOverwrite);
                if (firstFreeParticle > 0)
                    VertexBuffer.SetData(0, Vertices, 0, firstFreeParticle * VerticiesPerParticle, VertexStride, SetDataOptions.NoOverwrite);
            }

            firstNewParticle = firstFreeParticle;
        }

        public bool HasParticlesToRender()
        {
            return firstActiveParticle != firstFreeParticle;
        }

        public override void Draw()
        {
            if (VertexBuffer.IsContentLost)
                VertexBuffer_ContentLost();

            if (firstNewParticle != firstFreeParticle)
                AddNewParticlesToVertexBuffer();

            if (HasParticlesToRender())
            {
                graphicsDevice.Indices = IndexBuffer;
                graphicsDevice.SetVertexBuffer(VertexBuffer);

                if (firstActiveParticle < firstFreeParticle)
                {
                    var numParticles = firstFreeParticle - firstActiveParticle;
                    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, firstActiveParticle * IndiciesPerParticle, numParticles * PrimitivesPerParticle);
                }
                else
                {
                    var numParticlesAtEnd = MaxParticles - firstActiveParticle;
                    if (numParticlesAtEnd > 0)
                        graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, firstActiveParticle * IndiciesPerParticle, numParticlesAtEnd * PrimitivesPerParticle);
                    if (firstFreeParticle > 0)
                        graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, firstFreeParticle * PrimitivesPerParticle);
                }
            }

            drawCounter++;
        }

        private class HeightCache
        {
            private const int TileCount = 10;
            private readonly int BlockSize;
            private readonly int Divisions;
            private readonly List<Tile> Tiles = new List<Tile>();

            public HeightCache(int blockSize)
            {
                BlockSize = blockSize;
                Divisions = (int)Math.Round(2048f / blockSize);
            }

            public float GetHeight(in WorldLocation location, TileManager tiles, SceneryDrawer scenery)
            {
                WorldLocation temp = location.Normalize();

                // First, ensure we have the tile in question cached.
                var tile = Tiles.FirstOrDefault(t => t.TileX == temp.TileX && t.TileZ == temp.TileZ);
                if (tile == null)
                    Tiles.Add(tile = new Tile(temp.TileX, temp.TileZ, Divisions));

                // Remove excess entries.
                if (Tiles.Count > TileCount)
                    Tiles.RemoveAt(0);

                // Now calculate division to query.
                var x = (int)((temp.Location.X + 1024) / BlockSize);
                var z = (int)((temp.Location.Z + 1024) / BlockSize);

                // Trace the case where x or z are out of bounds
                var xSize = tile.Height.GetLength(0);
                var zSize = tile.Height.GetLength(1);
                if (x < 0 || x >= xSize || z < 0 || z >= zSize)
                {
                    Trace.TraceWarning("Precipitation indexes are out of bounds:  x = {0}, z = {1}, Location.X = {2}, Location.Z = {3}, BlockSize = {4}, HeightDimensionX = {5}, HeightDimensionZ = {6}",
                        x, z, location.Location.X, location.Location.Z, BlockSize, tile.Height.GetLength(0), tile.Height.GetLength(1));
                    if (x >= xSize)
                        x = xSize - 1;
                    else if (z >= zSize)
                        z = zSize - 1;
                    else if (x < 0)
                        x = 0;
                    else
                        z = 0;
                }
                // If we don't have it cached, load it.
                if (tile.Height[x, z] == float.MinValue)
                {
                    var position = new WorldLocation(temp.TileX, temp.TileZ, (x + 0.5f) * BlockSize - 1024, 0, (z + 0.5f) * BlockSize - 1024);
                    tile.Height[x, z] = Math.Max(tiles.GetElevation(position), scenery.GetBoundingBoxTop(position, BlockSize));
                    tile.Used++;
                }

                return tile.Height[x, z];
            }

            [DebuggerDisplay("Tile = {TileX},{TileZ} Used = {Used}")]
            private class Tile
            {
                public readonly int TileX;
                public readonly int TileZ;
                public readonly float[,] Height;
                public int Used;

                public Tile(int tileX, int tileZ, int divisions)
                {
                    TileX = tileX;
                    TileZ = tileZ;
                    Height = new float[divisions, divisions];
                    for (var x = 0; x < divisions; x++)
                        for (var z = 0; z < divisions; z++)
                            Height[x, z] = float.MinValue;
                }
            }
        }
    }

    public class PrecipitationMaterial : Material
    {
        private readonly Texture2D rainTexture;
        private readonly Texture2D snowTexture;
        private readonly Texture2D[] dynamicPrecipitationTexture = new Texture2D[12];
        private readonly PrecipitationShader shader;

        public PrecipitationMaterial(Viewer viewer)
            : base(viewer, null)
        {
            // TODO: This should happen on the loader thread.
            rainTexture = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "Raindrop.png"));
            snowTexture = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "Snowflake.png"));
            dynamicPrecipitationTexture[0] = snowTexture;
            dynamicPrecipitationTexture[11] = rainTexture;
            for (int i = 1; i <= 10; i++)
            {
                var path = $"Raindrop{i}.png";
                dynamicPrecipitationTexture[11 - i] = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, path));
            }
            shader = base.viewer.MaterialManager.PrecipitationShader;
        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[0]; //["Precipitation"];

            shader.LightVector.SetValue(viewer.Settings.UseMSTSEnv ? viewer.World.MSTSSky.mstsskysolarDirection : viewer.World.Sky.solarDirection);
            shader.particleSize.SetValue(1f);
            if (viewer.Simulator.Weather.PrecipitationLiquidity == 0 || viewer.Simulator.Weather.PrecipitationLiquidity == 1)
            {
                shader.precipitation_Tex.SetValue(viewer.Simulator.WeatherType == WeatherType.Snow ? snowTexture :
                    viewer.Simulator.WeatherType == WeatherType.Rain ? rainTexture :
                    viewer.Simulator.Weather.PrecipitationLiquidity == 0 ? snowTexture : rainTexture);
            }
            else
            {
                var precipitation_TexIndex = (int)(viewer.Simulator.Weather.PrecipitationLiquidity * 11);
                shader.precipitation_Tex.SetValue(dynamicPrecipitationTexture[precipitation_TexIndex]);
            }

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    // Note: This is quite a hack. We ideally should be able to pass this through RenderItem somehow.
                    shader.cameraTileXZ.SetValue(new Vector2(item.XNAMatrix.M21, item.XNAMatrix.M22));
                    shader.currentTime.SetValue(item.XNAMatrix.M11);

                    shader.SetMatrix(ref view, ref projection);
                    pass.Apply();
                    item.RenderPrimitive.Draw();
                }
            }
        }

        public override void ResetState()
        {
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        public override bool GetBlending()
        {
            return true;
        }

        public override void Mark()
        {
            viewer.TextureManager.Mark(rainTexture);
            viewer.TextureManager.Mark(snowTexture);
            for (int i = 1; i <= 10; i++)
                viewer.TextureManager.Mark(dynamicPrecipitationTexture[i]);
            base.Mark();
        }
    }

    public class PrecipitationShader : BaseShader
    {
        internal readonly EffectParameter worldViewProjection;
        internal readonly EffectParameter invView;
        internal readonly EffectParameter LightVector;
        internal readonly EffectParameter particleSize;
        internal readonly EffectParameter cameraTileXZ;
        internal readonly EffectParameter currentTime;
        internal readonly EffectParameter precipitation_Tex;

        public PrecipitationShader(GraphicsDevice graphicsDevice)
            : base(graphicsDevice, "PrecipitationShader")
        {
            worldViewProjection = Parameters["worldViewProjection"];
            invView = Parameters["invView"];
            LightVector = Parameters["LightVector"];
            particleSize = Parameters["particleSize"];
            cameraTileXZ = Parameters["cameraTileXZ"];
            currentTime = Parameters["currentTime"];
            precipitation_Tex = Parameters["precipitation_Tex"];
        }

        public void SetMatrix(ref Matrix view, ref Matrix projection)
        {
            worldViewProjection.SetValue(Matrix.Identity * view * projection);
            invView.SetValue(Matrix.Invert(view));
        }
    }
}
