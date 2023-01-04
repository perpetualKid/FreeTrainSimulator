// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D.Common;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Simulation;
using Orts.Viewer3D;

namespace Orts.ActivityRunner.Viewer3D
{
    internal static class SkyConstants
    {
        // Sky dome constants
        public const int Radius = 6000;
        public const int Sides = 24;
        // <CScomment> added a belt of triangles just below 0 level to avoid empty sky below horizon
        public const short Levels = 6;
    }

    public enum SkyElement
    {
        Sky,
        Moon,
        Clouds,
    }

    public class SkyDate
    {
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        public int OrdinalDate { get; } // Ordinal date. Range: 0 to 366.

        public SkyDate(int ordinalDate)
        {
            OrdinalDate = ordinalDate;
            Month = 1 + (ordinalDate / 30);
            Day = 21;
            Year = 2017;
        }
    }

    public class SkyViewer
    {
        private readonly Viewer viewer;
        private readonly Material material;
        private readonly Vector3[] SolarPositionCache = new Vector3[72];
        private readonly Vector3[] LunarPositionCache = new Vector3[72];
        private readonly SkyInterpolation SkyInterpolation = new SkyInterpolation();

        public SkyPrimitive Primitive { get; }
        public float WindSpeed { get; }
        public float WindDirection { get; }
        public int MoonPhase { get; private set; }
        public Vector3 SolarDirection { get; private set; }
        public Vector3 LunarDirection { get; private set; }
        public double Latitude { get; private set; } // Latitude of current route in radians. -pi/2 = south pole, 0 = equator, pi/2 = north pole.
        public double Longitude { get; private set; } // Longitude of current route in radians. -pi = west of prime, 0 = prime, pi = east of prime.

        public SkyViewer(Viewer viewer)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            this.viewer = viewer;
            material = viewer.MaterialManager.Load("Sky");

            // Instantiate classes
            Primitive = new SkyPrimitive(this.viewer);

            // Default wind speed and direction
            // TODO: We should be using Viewer.Simulator.Weather instead of our own local weather fields
            WindSpeed = 5.0f; // m/s (approx 11 mph)
            WindDirection = 4.7f; // radians (approx 270 deg, i.e. westerly)
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            (SolarDirection, LunarDirection) = SkyInterpolation.SetSunAndMoonDirection(SolarPositionCache, LunarPositionCache, Simulator.Instance.ClockTime);

            var xnaSkyWorldLocation = Matrix.CreateTranslation(viewer.Camera.Location * new Vector3(1, 1, -1));
            frame.AddPrimitive(material, Primitive, RenderPrimitiveGroup.Sky, ref xnaSkyWorldLocation);
        }

        public void LoadPrep()
        {
            (Latitude, Longitude) = EarthCoordinates.ConvertWTC(viewer.Camera.CameraWorldLocation);

            // First time around, initialize the following items:
            SkyInterpolation.OldClockTime = Simulator.Instance.ClockTime % 86400;
            while (SkyInterpolation.OldClockTime < 0)
            {
                SkyInterpolation.OldClockTime += 86400;
            }

            SkyInterpolation.Step1 = SkyInterpolation.Step2 = (int)(SkyInterpolation.OldClockTime / 1200);
            SkyInterpolation.Step2 = SkyInterpolation.Step2 < SkyInterpolation.MaxSteps - 1 ? SkyInterpolation.Step2 + 1 : 0; // limit to max. steps in case activity starts near midnight

            // And the rest depends on the weather (which is changeable)
            Simulator.Instance.WeatherChanged += (sender, e) => WeatherChanged();
            WeatherChanged();
        }

        internal void Mark()
        {
            material.Mark();
        }

        private void WeatherChanged()
        {
            // TODO: Allow setting the date from route files?
            int seasonType = (int)Simulator.Instance.Season;
            SkyDate date = new SkyDate(Latitude >= 0 ? 82 + (seasonType * 91) : (82 + ((seasonType + 2) * 91)) % 365);

            // Fill in the sun- and moon-position lookup tables
            for (int i = 0; i < SkyInterpolation.MaxSteps; i++)
            {
                SolarPositionCache[i] = SunMoonPos.SolarAngle(Latitude, Longitude, (float)i / SkyInterpolation.MaxSteps, date);
                LunarPositionCache[i] = SunMoonPos.LunarAngle(Latitude, Longitude, (float)i / SkyInterpolation.MaxSteps, date);
            }

            // Phase of the moon is generated at random, but moon dog only occurs in winter
            MoonPhase = StaticRandom.Next(8);
            if (MoonPhase == 6 && date.OrdinalDate > 45 && date.OrdinalDate < 330)
            {
                MoonPhase = 3;
            }
        }
    }

    public class SkyPrimitive : RenderPrimitive
    {
        public const float SkyRadius = 6020;
        public const float MoonRadius = 6010;
        public const float CloudsRadius = 6000;
        public const float CloudsFlatness = 0.1f;


        public SkyElement Element { get; set; }

        /*
         * The sky is formed of 3 layers (back to front):
         * - Cloud-less sky and night sky textures, blended according to time of day, and with sun effect added in (in the shader)
         * - Moon textures (phase is random)
         * - Clouds blended by overcast factor and animated by wind speed and direction
         *
         * Both the cloud-less sky and clouds use sky domes; the sky is
         * perfectly spherical, while the cloud dome is squashed (see
         * `CloudsFlatness`) to make it closer to a flat plane overhead,
         * without losing the horizon connection.
         *
         * The sky dome is the top hemisphere of a globe, plus an extension
         * below the horizon to ensure we never get to see the edge. Both the
         * rotational (sides) and horizontal/vertical (steps) segments are
         * split so that the center angles are `DomeComponentDegrees`.
         *
         * It is important that there are enough sides for the texture mapping
         * to look good; otherwise, smooth curves will render as wavy lines.
         * Currently, testing shows 6° is the maximum reasonable angle.
         */
        private const int TuneDomeComponentDegrees = 6;

        private const int DomeSides = 360 / TuneDomeComponentDegrees;
        private const int DomeStepsMain = 90 / TuneDomeComponentDegrees;
        private const int DomeStepsExtra = 1;
        private const int DomeSteps = DomeStepsMain + DomeStepsExtra;
        private const int DomePrimitives = (2 * DomeSides * DomeSteps) - DomeSides;
        private const int DomeVertices = 1 + (DomeSides * DomeSteps);
        private const int DomeIndexes = 3 * DomePrimitives;

        private const int MoonPrimitives = 2;
        private const int MoonVertices = 4;
        private const int MoonIndexes = 3 * MoonPrimitives;

        private const int VertexCount = (2 * DomeVertices) + MoonVertices;
        private const int IndexCount = DomeIndexes + MoonIndexes;

        private readonly VertexPositionNormalTexture[] VertexList;
        private readonly short[] IndexList;

        private VertexBuffer VertexBuffer;
        private IndexBuffer IndexBuffer;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SkyPrimitive(Viewer viewer)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            // Initialize the vertex and index lists
            VertexList = new VertexPositionNormalTexture[VertexCount];
            IndexList = new short[IndexCount];
            var vertexIndex = 0;
            var indexIndex = 0;
            vertexIndex = InitializeDomeVertexList(vertexIndex, SkyRadius);
            vertexIndex = InitializeDomeVertexList(vertexIndex, CloudsRadius, CloudsFlatness);
            indexIndex = InitializeDomeIndexList(indexIndex);
            (vertexIndex, indexIndex) = InitializeMoonLists(vertexIndex, indexIndex);
            Debug.Assert(vertexIndex == VertexCount, $"Did not initialize all verticies; expected {VertexCount}, got {vertexIndex}");
            Debug.Assert(indexIndex == IndexCount, $"Did not initialize all indexes; expected {IndexCount}, got {indexIndex}");

            // Meshes have now been assembled, so put everything into vertex and index buffers
            InitializeVertexBuffers(viewer.Game.GraphicsDevice);
        }

        public override void Draw()
        {
            graphicsDevice.SetVertexBuffer(VertexBuffer);
            graphicsDevice.Indices = IndexBuffer;

            switch (Element)
            {
                case SkyElement.Sky:
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        baseVertex: 0,
                        startIndex: 0,
                        primitiveCount: DomePrimitives);
                    break;
                case SkyElement.Moon:
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        baseVertex: DomeVertices * 2,
                        startIndex: DomeIndexes,
                        primitiveCount: MoonPrimitives);
                    break;
                case SkyElement.Clouds:
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        baseVertex: DomeVertices,
                        startIndex: 0,
                        primitiveCount: DomePrimitives);
                    break;
            }
        }

        private int InitializeDomeVertexList(int index, float radius, float flatness = 1)
        {
            // Single vertex at zenith
            VertexList[index].Position = new Vector3(0, radius * flatness, 0);
            VertexList[index].Normal = Vector3.Normalize(VertexList[index].Position);
            VertexList[index].TextureCoordinate = new Vector2(0.5f, 0.5f);
            index++;

            for (var step = 1; step <= DomeSteps; step++)
            {
                var stepCos = (float)Math.Cos(MathHelper.ToRadians(90f * step / DomeStepsMain));
                var stepSin = (float)Math.Sin(MathHelper.ToRadians(90f * step / DomeStepsMain));

                var y = radius * stepCos * flatness;
                var d = radius * stepSin;

                for (var side = 0; side < DomeSides; side++)
                {
                    var sideCos = (float)Math.Cos(MathHelper.ToRadians(360f * side / DomeSides));
                    var sideSin = (float)Math.Sin(MathHelper.ToRadians(360f * side / DomeSides));

                    var x = d * sideCos;
                    var z = d * sideSin;

                    var u = 0.5f + ((float)step / DomeStepsMain * sideCos / 2);
                    var v = 0.5f + ((float)step / DomeStepsMain * sideSin / 2);

                    // Store the position, texture coordinates and normal (normalized position vector) for the current vertex
                    VertexList[index].Position = new Vector3(x, y, z);
                    VertexList[index].Normal = Vector3.Normalize(VertexList[index].Position);
                    VertexList[index].TextureCoordinate = new Vector2(u, v);
                    index++;
                }
            }
            return index;
        }

        private int InitializeDomeIndexList(int index)
        {
            // Zenith triangles
            for (var side = 0; side < DomeSides; side++)
            {
                IndexList[index++] = 0;
                IndexList[index++] = (short)(1 + ((side + 1) % DomeSides));
                IndexList[index++] = (short)(1 + ((side + 0) % DomeSides));
            }

            for (var step = 1; step < DomeSteps; step++)
            {
                for (var side = 0; side < DomeSides; side++)
                {
                    IndexList[index++] = (short)(1 + ((step - 1) * DomeSides) + ((side + 0) % DomeSides));
                    IndexList[index++] = (short)(1 + ((step - 0) * DomeSides) + ((side + 1) % DomeSides));
                    IndexList[index++] = (short)(1 + ((step - 0) * DomeSides) + ((side + 0) % DomeSides));
                    IndexList[index++] = (short)(1 + ((step - 1) * DomeSides) + ((side + 0) % DomeSides));
                    IndexList[index++] = (short)(1 + ((step - 1) * DomeSides) + ((side + 1) % DomeSides));
                    IndexList[index++] = (short)(1 + ((step - 0) * DomeSides) + ((side + 1) % DomeSides));
                }
            }
            return index;
        }

        private (int, int) InitializeMoonLists(int vertexIndex, int indexIndex)
        {
            // Moon vertices
            for (var i = 0; i < 2; i++)
            {
                for (var j = 0; j < 2; j++)
                {
                    VertexList[vertexIndex].Position = new Vector3(i, j, 0);
                    VertexList[vertexIndex].Normal = new Vector3(0, 0, 1);
                    VertexList[vertexIndex].TextureCoordinate = new Vector2(i, j);
                    vertexIndex++;
                }
            }

            // Moon indices - clockwise winding
            IndexList[indexIndex++] = 0;
            IndexList[indexIndex++] = 1;
            IndexList[indexIndex++] = 2;
            IndexList[indexIndex++] = 1;
            IndexList[indexIndex++] = 3;
            IndexList[indexIndex++] = 2;

            return (vertexIndex, indexIndex);
        }

        private void InitializeVertexBuffers(GraphicsDevice graphicsDevice)
        {
            VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), VertexList.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(VertexList);
            IndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), IndexCount, BufferUsage.WriteOnly);
            IndexBuffer.SetData(IndexList);
        }
    }

    public class SkyMaterial : Material
    {
        private const float NightStart = 0.15f; // The sun's Y value where it begins to get dark
        private const float NightFinish = -0.05f; // The Y value where darkest fog color is reached and held steady

        // These should be user defined in the Environment files (future)
        private static readonly Vector3 StartColor = new Vector3(0.647f, 0.651f, 0.655f); // Original daytime fog color - must be preserved!
        private static readonly Vector3 FinishColor = new Vector3(0.05f, 0.05f, 0.05f); // Darkest night-time fog color

        private readonly SkyShader skyShader;
        private readonly Texture2D skyTexture;
        private readonly Texture2D starTextureN;
        private readonly Texture2D starTextureS;
        private readonly Texture2D moonTexture;
        private readonly Texture2D moonMask;
        private readonly Texture2D cloudTexture;

        public SkyMaterial(Viewer viewer)
            : base(viewer, null)
        {
            skyShader = base.viewer.MaterialManager.SkyShader;
            // TODO: This should happen on the loader thread.
            skyTexture = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "SkyDome1.png"));
            starTextureN = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "Starmap_N.png"));
            starTextureS = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "Starmap_S.png"));
            moonTexture = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "MoonMap.png"));
            moonMask = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "MoonMask.png"));
            cloudTexture = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "Clouds01.png"));

            skyShader.SkyMapTexture = skyTexture;
            skyShader.StarMapTexture = starTextureN;
            skyShader.MoonMapTexture = moonTexture;
            skyShader.MoonMaskTexture = moonMask;
            skyShader.CloudMapTexture = cloudTexture;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            // Adjust Fog color for day-night conditions and overcast
            FogDay2Night(viewer.World.Sky.SolarDirection.Y, viewer.Simulator.Weather.OvercastFactor);

            // TODO: Use a dirty flag to determine if it is necessary to set the texture again
            skyShader.StarMapTexture = viewer.World.Sky.Latitude > 0 ? starTextureN : starTextureS;
            skyShader.Random = viewer.World.Sky.MoonPhase; // Keep setting this before LightVector for the preshader to work correctly
            skyShader.LightVector = viewer.World.Sky.SolarDirection;
            skyShader.Time = (float)viewer.Simulator.ClockTime / 100000;
            skyShader.MoonScale = SkyConstants.Radius / 20;
            skyShader.Overcast = viewer.Simulator.Weather.OvercastFactor;
            skyShader.SetFog(viewer.Simulator.Weather.FogVisibilityDistance, ref SharedMaterialManager.FogColor);
            skyShader.WindSpeed = viewer.World.Sky.WindSpeed;
            skyShader.WindDirection = viewer.World.Sky.WindDirection; // Keep setting this after Time and Windspeed. Calculating displacement here.
            for (int i = 0; i < 5; i++)
                graphicsDevice.SamplerStates[i] = SamplerState.LinearWrap;

            Matrix xnaSkyView = MatrixExtension.Multiply(view, Camera.XNASkyProjection);
            Matrix xnaMoonMatrix = Matrix.CreateTranslation(viewer.World.Sky.LunarDirection * SkyPrimitive.MoonRadius);
            Matrix xnaMoonView = MatrixExtension.Multiply(xnaMoonMatrix, xnaSkyView);
            skyShader.SetViewMatrix(ref view);

            // Sky dome
            skyShader.CurrentTechnique = skyShader.Techniques[0]; //["Sky"];
            viewer.World.Sky.Primitive.Element = SkyElement.Sky;

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            foreach (var pass in skyShader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    //                    Matrix wvp = item.XNAMatrix * viewXNASkyProj;
                    MatrixExtension.Multiply(in item.XNAMatrix, in xnaSkyView, out Matrix wvp);
                    skyShader.SetMatrix(ref wvp);
                    pass.Apply();
                    item.RenderPrimitive.Draw();
                }
            }

            // Moon
            skyShader.CurrentTechnique = skyShader.Techniques[1]; //["Moon"];
            viewer.World.Sky.Primitive.Element = SkyElement.Moon;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.RasterizerState = RasterizerState.CullClockwise;

            foreach (var pass in skyShader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    //                    Matrix wvp = item.XNAMatrix * XNAMoonMatrixView * Camera.XNASkyProjection;
                    MatrixExtension.Multiply(in item.XNAMatrix, in xnaMoonView, out Matrix wvp);
                    skyShader.SetMatrix(ref wvp);
                    pass.Apply();
                    item.RenderPrimitive.Draw();
                }
            }

            // Clouds
            skyShader.CurrentTechnique = skyShader.Techniques[2]; //["Clouds"];
            viewer.World.Sky.Primitive.Element = SkyElement.Clouds;

            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            foreach (var pass in skyShader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    //                    Matrix wvp = item.XNAMatrix * viewXNASkyProj;
                    MatrixExtension.Multiply(in item.XNAMatrix, in xnaSkyView, out Matrix wvp);
                    skyShader.SetMatrix(ref wvp);
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
            return false;
        }

        public override void Mark()
        {
            viewer.TextureManager.Mark(skyTexture);
            viewer.TextureManager.Mark(starTextureN);
            viewer.TextureManager.Mark(starTextureS);
            viewer.TextureManager.Mark(moonTexture);
            viewer.TextureManager.Mark(moonMask);
            viewer.TextureManager.Mark(cloudTexture);
            base.Mark();
        }

        /// <summary>
        /// This function darkens the fog color as night begins to fall
        /// as well as with increasing overcast.
        /// </summary>
        /// <param name="sunHeight">The Y value of the sunlight vector</param>
        /// <param name="overcast">The amount of overcast</param>
        private static void FogDay2Night(float sunHeight, float overcast)
        {
            Vector3 floatColor;

            if (sunHeight > NightStart)
                floatColor = StartColor;
            else if (sunHeight < NightFinish)
                floatColor = FinishColor;
            else
            {
                var amount = (sunHeight - NightFinish) / (NightStart - NightFinish);
                floatColor = Vector3.Lerp(FinishColor, StartColor, amount);
            }

            // Adjust fog color for overcast
            floatColor *= (1 - 0.5f * overcast);
            SharedMaterialManager.FogColor.R = (byte)(floatColor.X * 255);
            SharedMaterialManager.FogColor.G = (byte)(floatColor.Y * 255);
            SharedMaterialManager.FogColor.B = (byte)(floatColor.Z * 255);
        }
    }
}
