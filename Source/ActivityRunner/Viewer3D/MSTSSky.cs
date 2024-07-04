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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.Formats.Msts.Files;
using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.World;
using Orts.Viewer3D;

namespace Orts.ActivityRunner.Viewer3D
{
    #region MSTSSkyVariables
    public static class MSTSSkyConstants
    {
        // Sky dome constants
        public static int skyRadius = 6000;
        public const int skySides = 24;
        public static int skyHeight;
        public const short skyLevels = 4;
        public static bool IsNight;
        public static float mstsskyTileu;
        public static float mstsskyTilev;
        public static float mstscloudTileu;
        public static float mstscloudTilev;
    }

    #endregion

    #region MSTSSkyDrawer
    public class MSTSSkyDrawer
    {
        private Viewer MSTSSkyViewer;
        private Material MSTSSkyMaterial;
        private bool initialized;
        // Classes reqiring instantiation
        public MSTSSkyMesh MSTSSkyMesh;
        private SeasonType mstsskyseasonType; //still need to remember it as MP now can change it.
        #region Class variables
        // Latitude of current route in radians. -pi/2 = south pole, 0 = equator, pi/2 = north pole.
        // Longitude of current route in radians. -pi = west of prime, 0 = prime, pi = east of prime.
        private double mstsskylatitude, mstsskylongitude;
        public bool ResetTexture => mstsskylatitude != 0;
        // Date of activity

        private SkyDate date;

        private SkyInterpolation skySteps = new SkyInterpolation();

        // Phase of the moon
        public int mstsskymoonPhase;
        // Wind speed and direction
        public float mstsskywindSpeed;
        public float mstsskywindDirection;
        // Overcast level
        public float mstsskyovercastFactor;
        // Fog distance
        public float mstsskyfogDistance;
        public bool isNight;

        public List<string> SkyLayers = new List<string>();

        // These arrays and vectors define the position of the sun and moon in the world
        private Vector3[] mstsskysolarPosArray = new Vector3[72];
        private Vector3[] mstsskylunarPosArray = new Vector3[72];
        public Vector3 mstsskysolarDirection;
        public Vector3 mstsskylunarDirection;
        #endregion

        #region Constructor
        /// <summary>
        /// SkyDrawer constructor
        /// </summary>
        public MSTSSkyDrawer(Viewer viewer)
        {
            MSTSSkyViewer = viewer;
            MSTSSkyMaterial = viewer.MaterialManager.Load("MSTSSky");
            // Instantiate classes
            MSTSSkyMesh = new MSTSSkyMesh(viewer);

            //viewer.World.MSTSSky.MSTSSkyMaterial.Viewer.MaterialManager.sunDirection.Y < 0
            // Set default values
            mstsskyseasonType = MSTSSkyViewer.Simulator.Season;
            date = new SkyDate(82 + (int)mstsskyseasonType * 91);
            // Default wind speed and direction
            mstsskywindSpeed = 5.0f; // m/s (approx 11 mph)
            mstsskywindDirection = 4.7f; // radians (approx 270 deg, i.e. westerly)

            // The following keyboard commands are used for viewing sky and weather effects in "demo" mode.
            // Control- and Control+ for overcast, Shift- and Shift+ for fog and - and + for time.

            // Don't let multiplayer clients adjust the weather.
            if (MultiPlayerManager.MultiplayerState != MultiplayerState.Client)
            {
                // Overcast ranges from 0 (completely clear) to 1 (completely overcast).
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastIncrease, KeyEventType.KeyDown, (GameTime gameTIme) =>
                {
                    mstsskyovercastFactor = (float)MathHelperD.Clamp(mstsskyovercastFactor + gameTIme.ElapsedGameTime.TotalSeconds / 10, 0, 1);
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastDecrease, KeyEventType.KeyDown, (GameTime gameTIme) =>
                {
                    mstsskyovercastFactor = (float)MathHelperD.Clamp(mstsskyovercastFactor - gameTIme.ElapsedGameTime.TotalSeconds / 10, 0, 1);
                });
                // Fog ranges from 10m (can't see anything) to 100km (clear arctic conditions).
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogIncrease, KeyEventType.KeyDown, (GameTime gameTIme) =>
                {
                    mstsskyfogDistance = (float)MathHelperD.Clamp(mstsskyfogDistance - gameTIme.ElapsedGameTime.TotalSeconds * mstsskyfogDistance, 10, 100000);
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogDecrease, KeyEventType.KeyDown, (GameTime gameTIme) =>
                {
                    mstsskyfogDistance = (float)MathHelperD.Clamp(mstsskyfogDistance + gameTIme.ElapsedGameTime.TotalSeconds * mstsskyfogDistance, 10, 100000);
                });
            }
            // Don't let clock shift if multiplayer.
            if (!MultiPlayerManager.IsMultiPlayer())
            {
                // Shift the clock forwards or backwards at 1h-per-second.
                viewer.UserCommandController.AddEvent(UserCommand.DebugClockForwards, KeyEventType.KeyDown, (GameTime gameTIme) =>
                {
                    MSTSSkyViewer.Simulator.ClockTime += gameTIme.ElapsedGameTime.TotalSeconds * 3600;
                });
                viewer.UserCommandController.AddEvent(UserCommand.DebugClockBackwards, KeyEventType.KeyDown, (GameTime gameTIme) =>
                {
                    MSTSSkyViewer.Simulator.ClockTime -= gameTIme.ElapsedGameTime.TotalSeconds * 3600;
                });
            }
            // Server needs to notify clients of weather changes.
            if (MultiPlayerManager.IsServer())
            {
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastIncrease, KeyEventType.KeyReleased, SendMultiPlayerSkyChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugOvercastDecrease, KeyEventType.KeyReleased, SendMultiPlayerSkyChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogIncrease, KeyEventType.KeyReleased, SendMultiPlayerSkyChangeNotification);
                viewer.UserCommandController.AddEvent(UserCommand.DebugFogDecrease, KeyEventType.KeyReleased, SendMultiPlayerSkyChangeNotification);
            }
        }
        #endregion

        private void SendMultiPlayerSkyChangeNotification()
        {
            MultiPlayerManager.Broadcast(new WeatherMessage() { Weather = Simulator.Instance.WeatherType, Overcast = mstsskyovercastFactor, Fog = mstsskyfogDistance });
        }

        /// <summary>
        /// Used to update information affecting the SkyMesh
        /// </summary>
        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (mstsskyseasonType != MSTSSkyViewer.Simulator.Season)
            {
                mstsskyseasonType = MSTSSkyViewer.Simulator.Season;
                date = new SkyDate(82 + (int)mstsskyseasonType * 91);
            }
            // Adjust dome position so the bottom edge is not visible
            Vector3 ViewerXNAPosition = new Vector3(MSTSSkyViewer.Camera.Location.X, MSTSSkyViewer.Camera.Location.Y - 100, -MSTSSkyViewer.Camera.Location.Z);
            Matrix XNASkyWorldLocation = Matrix.CreateTranslation(ViewerXNAPosition);

            if (!initialized)
            {
                // First time around, initialize the following items:
                skySteps.OldClockTime = MSTSSkyViewer.Simulator.ClockTime % 86400;
                while (skySteps.OldClockTime < 0)
                    skySteps.OldClockTime += 86400;
                skySteps.Step1 = skySteps.Step2 = (int)(skySteps.OldClockTime / 1200);
                skySteps.Step2 = skySteps.Step2 < skySteps.MaxSteps - 1 ? skySteps.Step2 + 1 : 0; // limit to max. steps in case activity starts near midnight
                                                                                                  // Get the current latitude and longitude coordinates
                (mstsskylatitude, mstsskylongitude) = EarthCoordinates.ConvertWTC(MSTSSkyViewer.Camera.CameraWorldLocation);
                if (mstsskyseasonType != MSTSSkyViewer.Simulator.Season)
                {
                    mstsskyseasonType = MSTSSkyViewer.Simulator.Season;
                    date = new SkyDate(mstsskylatitude >= 0 ? 82 + (int)mstsskyseasonType * 91 : (82 + ((int)mstsskyseasonType + 2) * 91) % 365);
                }
                // Fill in the sun- and moon-position lookup tables
                for (int i = 0; i < skySteps.MaxSteps; i++)
                {
                    mstsskysolarPosArray[i] = SunMoonPos.SolarAngle(mstsskylatitude, mstsskylongitude, ((float)i / skySteps.MaxSteps), date);
                    mstsskylunarPosArray[i] = SunMoonPos.LunarAngle(mstsskylatitude, mstsskylongitude, ((float)i / skySteps.MaxSteps), date);
                }
                // Phase of the moon is generated at random
                mstsskymoonPhase = StaticRandom.Next(8);
                if (mstsskymoonPhase == 6 && date.OrdinalDate > 45 && date.OrdinalDate < 330)
                    mstsskymoonPhase = 3; // Moon dog only occurs in winter
                // Overcast factor: 0.0=almost no clouds; 0.1=wispy clouds; 1.0=total overcast
                //mstsskyovercastFactor = MSTSSkyViewer.World.WeatherControl.overcastFactor;
                mstsskyfogDistance = MSTSSkyViewer.Simulator.Weather.FogVisibilityDistance;
                initialized = true;
            }

            EnvironmentalCondition updatedWeatherCondition;
            if ((updatedWeatherCondition = Simulator.Instance.UpdatedWeatherCondition) != null)
            {
                //received message about weather change
                mstsskyovercastFactor = updatedWeatherCondition.OvercastFactor;
                mstsskyfogDistance = updatedWeatherCondition.FogViewingDistance;
            }

            (mstsskysolarDirection, mstsskylunarDirection) = skySteps.SetSunAndMoonDirection(mstsskysolarPosArray, mstsskylunarPosArray, MSTSSkyViewer.Simulator.ClockTime);

            frame.AddPrimitive(MSTSSkyMaterial, MSTSSkyMesh, RenderPrimitiveGroup.Sky, ref XNASkyWorldLocation);
        }

        public void LoadPrep()
        {
            if (mstsskyseasonType != MSTSSkyViewer.Simulator.Season)
                if (mstsskyseasonType != MSTSSkyViewer.Simulator.Season)
                {
                    mstsskyseasonType = MSTSSkyViewer.Simulator.Season;
                    date = new SkyDate(82 + (int)mstsskyseasonType * 91);
                }

            // Get the current latitude and longitude coordinates
            EarthCoordinates.ConvertWTC(MSTSSkyViewer.Camera.Tile, MSTSSkyViewer.Camera.Location, out mstsskylatitude, out mstsskylongitude);
            float fractClockTime = (float)MSTSSkyViewer.Simulator.ClockTime / 86400;
            mstsskysolarDirection = SunMoonPos.SolarAngle(mstsskylatitude, mstsskylongitude, fractClockTime, date);
            mstsskylatitude = 0;
            mstsskylongitude = 0;
        }

        internal void Mark()
        {
            MSTSSkyMaterial.Mark();
        }
    }
    #endregion

    #region MSTSSkyMesh
    public class MSTSSkyMesh : RenderPrimitive
    {
        private VertexBuffer MSTSSkyVertexBuffer;
        private static IndexBuffer MSTSSkyIndexBuffer;
        public int drawIndex;
        private VertexPositionNormalTexture[] vertexList;
        private static short[] triangleListIndices; // Trilist buffer.

        // Sky dome geometry is based on two global variables: the radius and the number of sides
        public int mstsskyRadius = MSTSSkyConstants.skyRadius;
        private static int mstsskySides = MSTSSkyConstants.skySides;
        public int mstscloudDomeRadiusDiff = 600;
        // skyLevels: Used for iterating vertically through the "levels" of the hemisphere polygon
        private static int mstsskyLevels = MSTSSkyConstants.skyLevels;
        private static float mstsskytextureu = MSTSSkyConstants.mstsskyTileu;
        private static float mstsskytexturev = MSTSSkyConstants.mstsskyTilev;
        private static float mstscloudtextureu = MSTSSkyConstants.mstscloudTileu;
        private static float mstscloudtexturev = MSTSSkyConstants.mstscloudTilev;
        // Number of vertices in the sky hemisphere. (each dome = 145 for 24-sided sky dome: 24 x 6 + 1)
        // plus four more for the moon quad
        private static int numVertices = 4 + 2 * (int)((mstsskyLevels + 1) * mstsskySides + 1);
        // Number of point indices (each dome = 792 for 24 sides: 5 levels of 24 triangle pairs each
        // plus 24 triangles at the zenith)
        // plus six more for the moon quad
        private static short indexCount = 6 + 2 * ((MSTSSkyConstants.skySides * 6 * ((MSTSSkyConstants.skyLevels + 3)) + 3 * MSTSSkyConstants.skySides));
        /// <summary>
        /// Constructor
        /// </summary>
        public MSTSSkyMesh(Viewer viewer)
        {
            // Initialize the vertex and point-index buffers
            vertexList = new VertexPositionNormalTexture[numVertices];
            triangleListIndices = new short[indexCount];

            // Sky dome
            MSTSSkyDomeVertexList(0, mstsskyRadius, mstsskytextureu, mstsskytexturev);
            MSTSSkyDomeTriangleList(0, 0);
            // Cloud dome
            MSTSSkyDomeVertexList((numVertices - 4) / 2, mstsskyRadius - mstscloudDomeRadiusDiff, mstscloudtextureu, mstscloudtexturev);
            MSTSSkyDomeTriangleList((short)((indexCount - 6) / 2), 1);
            // Moon quad
            MoonLists(numVertices - 5, indexCount - 6);//(144, 792);
            // Meshes have now been assembled, so put everything into vertex and index buffers
            InitializeVertexBuffers(viewer.Game.GraphicsDevice);
        }
        public override void Draw()
        {
            graphicsDevice.SetVertexBuffer(MSTSSkyVertexBuffer);
            graphicsDevice.Indices = MSTSSkyIndexBuffer;

            switch (drawIndex)
            {
                case 1: // Sky dome
                    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, (indexCount - 6) / 6);
                    break;
                case 2: // Moon
                    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, indexCount - 6, 2);
                    break;
                case 3: // Clouds Dome
                    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, (indexCount - 6) / 2, (indexCount - 6) / 6);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Creates the vertex list for each sky dome.
        /// </summary>
        /// <param name="index">The starting vertex number</param>
        /// <param name="radius">The radius of the dome</param>
        /// <param name="oblate">The amount the dome is flattened</param>
        private void MSTSSkyDomeVertexList(int index, int radius, float tile_u, float tile_v)
        {
            int vertexIndex = index;

            // for each vertex
            for (int i = 0; i <= (mstsskyLevels); i++) // (=6 for 24 sides)
            {
                // The "oblate" factor is used to flatten the dome to an ellipsoid. Used for the inner (cloud)
                // dome only. Gives the clouds a flatter appearance.
                float y = (float)Math.Sin(MathHelper.ToRadians((360 / mstsskySides) * (i - 1))) * radius; //  oblate;
                float yRadius = radius * (float)Math.Cos(MathHelper.ToRadians((360 / mstsskySides) * (i - 1)));
                for (int j = 0; j < mstsskySides; j++) // (=24 for top overlay)
                {

                    float x = (float)Math.Cos(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * yRadius;
                    float z = (float)Math.Sin(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * yRadius;

                    // UV coordinates - top overlay
                    float uvRadius;
                    uvRadius = (0.5f - (float)(0.5f * (i - 1)) / mstsskyLevels);
                    float uv_u = tile_u * (0.5f - ((float)Math.Cos(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * uvRadius));
                    float uv_v = tile_v * (0.5f - ((float)Math.Sin(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * uvRadius));

                    // Store the position, texture coordinates and normal (normalized position vector) for the current vertex
                    vertexList[vertexIndex].Position = new Vector3(x, y, z);
                    vertexList[vertexIndex].TextureCoordinate = new Vector2(uv_u, uv_v);
                    vertexList[vertexIndex].Normal = Vector3.Normalize(new Vector3(x, y, z));
                    vertexIndex++;
                }
            }
            // Single vertex at zenith
            vertexList[vertexIndex].Position = new Vector3(0, radius, 0);
            vertexList[vertexIndex].Normal = new Vector3(0, 1, 0);
            vertexList[vertexIndex].TextureCoordinate = new Vector2(0.5f * tile_u, 0.5f * tile_v); // (top overlay)
        }

        /// <summary>
        /// Creates the triangle index list for each dome.
        /// </summary>
        /// <param name="index">The starting triangle index number</param>
        /// <param name="pass">A multiplier used to arrive at the starting vertex number</param>
        private static void MSTSSkyDomeTriangleList(short index, short pass)
        {
            // ----------------------------------------------------------------------
            // 24-sided sky dome mesh is built like this:        48 49 50
            // Triangles are wound couterclockwise          71 o--o--o--o
            // because we're looking at the inner              | /|\ | /|
            // side of the hemisphere. Each time               |/ | \|/ |
            // we circle around to the start point          47 o--o--o--o 26
            // on the mesh we have to reset the                |\ | /|\ |
            // vertex number back to the beginning.            | \|/ | \|
            // Using WAC's sw,se,nw,ne coordinate    nw ne  23 o--o--o--o 
            // convention.-->                        sw se        0  1  2
            // ----------------------------------------------------------------------
            short iIndex = index;
            short baseVert = (short)(pass * (short)((numVertices - 4) / 2));
            for (int i = 0; i < mstsskyLevels; i++) // (=5 for 24 sides)
                for (int j = 0; j < mstsskySides; j++) // (=24 for 24 sides)
                {
                    // Vertex indices, beginning in the southwest corner
                    short sw = (short)(baseVert + (j + i * (mstsskySides)));
                    short nw = (short)(sw + mstsskySides); // top overlay mapping
                    short ne = (short)(nw + 1);

                    short se = (short)(sw + 1);

                    if (((i & 1) == (j & 1)))  // triangles alternate
                    {
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % mstsskySides == 0) ? (short)(ne - mstsskySides) : ne;
                        triangleListIndices[iIndex++] = nw;
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % mstsskySides == 0) ? (short)(ne - mstsskySides) : ne;
                    }
                    else
                    {
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                        triangleListIndices[iIndex++] = nw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % mstsskySides == 0) ? (short)(ne - mstsskySides) : ne;
                        triangleListIndices[iIndex++] = nw;
                    }
                }
            //Zenith triangles (=24 for 24 sides)
            for (int i = 0; i < mstsskySides; i++)
            {
                short sw = (short)(baseVert + (((mstsskySides) * mstsskyLevels) + i));
                short se = (short)(sw + 1);

                triangleListIndices[iIndex++] = sw;
                triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                triangleListIndices[iIndex++] = (short)(baseVert + (short)((numVertices - 5) / 2)); // The zenith
            }
        }

        /// <summary>
        /// Creates the moon vertex and triangle index lists.
        /// <param name="vertexIndex">The starting vertex number</param>
        /// <param name="iIndex">The starting triangle index number</param>
        /// </summary>
        private void MoonLists(int vertexIndex, int iIndex)
        {
            // Moon vertices
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    vertexIndex++;
                    vertexList[vertexIndex].Position = new Vector3(i, j, 0);
                    vertexList[vertexIndex].Normal = new Vector3(0, 0, 1);
                    vertexList[vertexIndex].TextureCoordinate = new Vector2(i, j);
                }

            // Moon indices - clockwise winding
            short msw = (short)(numVertices - 4);
            short mnw = (short)(msw + 1);
            short mse = (short)(mnw + 1);
            short mne = (short)(mse + 1);
            triangleListIndices[iIndex++] = msw;
            triangleListIndices[iIndex++] = mnw;
            triangleListIndices[iIndex++] = mse;
            triangleListIndices[iIndex++] = mse;
            triangleListIndices[iIndex++] = mnw;
            triangleListIndices[iIndex++] = mne;
        }

        /// <summary>
        /// Initializes the sky dome, cloud dome and moon vertex and triangle index list buffers.
        /// </summary>
        private void InitializeVertexBuffers(GraphicsDevice graphicsDevice)
        {
            // Initialize the vertex and index buffers, allocating memory for each vertex and index
            MSTSSkyVertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), vertexList.Length, BufferUsage.WriteOnly);
            MSTSSkyVertexBuffer.SetData(vertexList);
            if (MSTSSkyIndexBuffer == null)
            {
                MSTSSkyIndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexCount, BufferUsage.WriteOnly);
                MSTSSkyIndexBuffer.SetData(triangleListIndices);
            }
        }

    } // SkyMesh
    #endregion

    #region MSTSSkyMaterial
    public class MSTSSkyMaterial : Material
    {
        private readonly SkyShader shader;
        private readonly Texture2D mstsDayTexture;
        private readonly List<Texture2D> mstsSkyTextures = new List<Texture2D>();
        private readonly Texture2D mstsSkyStarTexture;
        private readonly Texture2D mstsSkyMoonTexture;
        private readonly Texture2D mstsSkyMoonMask;
        private readonly List<Texture2D> mstsSkyCloudTextures = new List<Texture2D>();
        private readonly Texture2D mstsSkySunTexture;
        private Matrix moonMatrix;
        private float mstsskytexturex;
        private float mstsskytexturey;
        private float mstscloudtexturex;
        private float mstscloudtexturey;

        public MSTSSkyMaterial(Viewer viewer)
            : base(viewer, null)
        {
            shader = base.viewer.MaterialManager.SkyShader;
            // TODO: This should happen on the loader thread. 
            if (viewer.ENVFile.SkyLayers != null)
            {
                var mstsskytexture = base.viewer.ENVFile.SkyLayers;
                int count = base.viewer.ENVFile.SkyLayers.Count;

                string[] mstsSkyTextureNames = new string[base.viewer.ENVFile.SkyLayers.Count];

                for (int i = 0; i < base.viewer.ENVFile.SkyLayers.Count; i++)
                {
                    mstsSkyTextureNames[i] = base.viewer.Simulator.RouteFolder.EnvironmentTextureFile(mstsskytexture[i].TextureName);
                    mstsSkyTextures.Add(AceFile.Texture2DFromFile(graphicsDevice, mstsSkyTextureNames[i]));
                    if (i == 0)
                    {
                        mstsDayTexture = mstsSkyTextures[i];
                        mstsskytexturex = mstsskytexture[i].TileX;
                        mstsskytexturey = mstsskytexture[i].TileY;
                    }
                    else if (mstsskytexture[i].FadeinStartTime != null)
                    {
                        mstsSkyStarTexture = mstsSkyTextures[i];
                        mstsskytexturex = mstsskytexture[i].TileX;
                        mstsskytexturey = mstsskytexture[i].TileY;
                    }
                    else
                    {
                        mstsSkyCloudTextures.Add(AceFile.Texture2DFromFile(graphicsDevice, mstsSkyTextureNames[i]));
                        mstscloudtexturex = mstsskytexture[i].TileX;
                        mstscloudtexturey = mstsskytexture[i].TileY;
                    }
                }

                MSTSSkyConstants.mstsskyTileu = mstsskytexturex;
                MSTSSkyConstants.mstsskyTilev = mstsskytexturey;
                MSTSSkyConstants.mstscloudTileu = mstscloudtexturex;
                MSTSSkyConstants.mstscloudTilev = mstscloudtexturey;
            }
            else
            {
                mstsSkyTextures.Add(SharedTextureManager.Get(graphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "SkyDome1.png")));
                mstsSkyStarTexture = SharedTextureManager.Get(graphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "Starmap_N.png"));
            }
            if (viewer.ENVFile.SkySatellites != null)
            {
                string mstsSkySunTextureName = base.viewer.Simulator.RouteFolder.EnvironmentTextureFile(base.viewer.ENVFile.SkySatellites[0].TextureName);
                string mstsSkyMoonTextureName = base.viewer.Simulator.RouteFolder.EnvironmentTextureFile(base.viewer.ENVFile.SkySatellites[1].TextureName);

                mstsSkySunTexture = SharedTextureManager.Get(graphicsDevice, mstsSkySunTextureName);
                mstsSkyMoonTexture = SharedTextureManager.Get(graphicsDevice, mstsSkyMoonTextureName);
            }
            else
                mstsSkyMoonTexture = SharedTextureManager.Get(graphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "MoonMap.png"));

            mstsSkyMoonMask = SharedTextureManager.Get(graphicsDevice, System.IO.Path.Combine(base.viewer.ContentPath, "MoonMask.png")); //ToDo:  No MSTS equivalent - will need to be fixed in MSTSSky.cs
            //MSTSSkyCloudTexture[0] = SharedTextureManager.Get(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, "Clouds01.png"));

            shader.SkyMapTexture = mstsDayTexture;
            shader.StarMapTexture = mstsSkyStarTexture;
            shader.MoonMapTexture = mstsSkyMoonTexture;
            shader.MoonMaskTexture = mstsSkyMoonMask;
            shader.CloudMapTexture = mstsSkyCloudTextures[0];
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            // Adjust Fog color for day-night conditions and overcast
            FogDay2Night(
                viewer.World.MSTSSky.mstsskysolarDirection.Y,
                viewer.World.MSTSSky.mstsskyovercastFactor);


            //if (Viewer.Settings.DistantMountains) SharedMaterialManager.FogCoeff *= (3 * (5 - Viewer.Settings.DistantMountainsFogValue) + 0.5f);

            if (viewer.World.MSTSSky.ResetTexture) // TODO: Use a dirty flag to determine if it is necessary to set the texture again
                shader.StarMapTexture = mstsSkyStarTexture;
            shader.Random = viewer.World.MSTSSky.mstsskymoonPhase; // Keep setting this before LightVector for the preshader to work correctly
            shader.LightVector = viewer.World.MSTSSky.mstsskysolarDirection;
            shader.Time = (float)viewer.Simulator.ClockTime / 100000;
            shader.MoonScale = MSTSSkyConstants.skyRadius / 20;
            shader.Overcast = viewer.World.MSTSSky.mstsskyovercastFactor;
            shader.SetFog(viewer.World.MSTSSky.mstsskyfogDistance, ref SharedMaterialManager.FogColor);
            shader.WindSpeed = viewer.World.MSTSSky.mstsskywindSpeed;
            shader.WindDirection = viewer.World.MSTSSky.mstsskywindDirection; // Keep setting this after Time and Windspeed. Calculating displacement here.

            for (var i = 0; i < 5; i++)
                graphicsDevice.SamplerStates[i] = SamplerState.LinearWrap;

            // Sky dome
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            MatrixExtension.Multiply(in view, in Camera.XNASkyProjection, out Matrix viewXNASkyProj);

            shader.CurrentTechnique = shader.Techniques[0]; //["Sky"];
            viewer.World.MSTSSky.MSTSSkyMesh.drawIndex = 1;
            shader.SetViewMatrix(ref view);
            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    MatrixExtension.Multiply(in item.XNAMatrix, in viewXNASkyProj, out Matrix wvp);
                    shader.SetMatrix(ref wvp);
                    pass.Apply();
                    item.RenderPrimitive.Draw();
                }
            }
            shader.CurrentTechnique = shader.Techniques[1]; //["Moon"];
            viewer.World.MSTSSky.MSTSSkyMesh.drawIndex = 2;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.RasterizerState = RasterizerState.CullClockwise;

            // Send the transform matrices to the shader
            int mstsskyRadius = viewer.World.MSTSSky.MSTSSkyMesh.mstsskyRadius;
            int mstscloudRadiusDiff = viewer.World.MSTSSky.MSTSSkyMesh.mstscloudDomeRadiusDiff;
            moonMatrix = Matrix.CreateTranslation(viewer.World.MSTSSky.mstsskylunarDirection * (mstsskyRadius));
            //            Matrix XNAMoonMatrixView = moonMatrix * viewMatrix;

            MatrixExtension.Multiply(in moonMatrix, in view, out Matrix result);
            MatrixExtension.Multiply(in result, in Camera.XNASkyProjection, out Matrix cameraProjection);

            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    //                    Matrix wvp = item.XNAMatrix * XNAMoonMatrixView * Camera.XNASkyProjection;
                    MatrixExtension.Multiply(in item.XNAMatrix, in cameraProjection, out Matrix wvp);
                    shader.SetMatrix(ref wvp);
                    pass.Apply();
                    item.RenderPrimitive.Draw();
                }
            }


            shader.CurrentTechnique = shader.Techniques[2]; //["Clouds"];
            viewer.World.MSTSSky.MSTSSkyMesh.drawIndex = 3;

            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    //                    Matrix wvp = item.XNAMatrix * viewXNASkyProj;
                    MatrixExtension.Multiply(in item.XNAMatrix, in viewXNASkyProj, out Matrix wvp);
                    shader.SetMatrix(ref wvp);
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

        private const float nightStart = 0.15f; // The sun's Y value where it begins to get dark
        private const float nightFinish = -0.05f; // The Y value where darkest fog color is reached and held steady

        // These should be user defined in the Environment files (future)
        private static Vector3 startColor = new Vector3(0.647f, 0.651f, 0.655f); // Original daytime fog color - must be preserved!
        private static Vector3 finishColor = new Vector3(0.05f, 0.05f, 0.05f); //Darkest nighttime fog color

        /// <summary>
        /// This function darkens the fog color as night begins to fall
        /// as well as with increasing overcast.
        /// </summary>
        /// <param name="sunHeight">The Y value of the sunlight vector</param>
        /// <param name="overcast">The amount of overcast</param>
        private static void FogDay2Night(float sunHeight, float overcast)
        {
            Vector3 floatColor;

            if (sunHeight > nightStart)
                floatColor = startColor;
            else if (sunHeight < nightFinish)
                floatColor = finishColor;
            else
            {
                var amount = (sunHeight - nightFinish) / (nightStart - nightFinish);
                floatColor = Vector3.Lerp(finishColor, startColor, amount);
            }

            // Adjust fog color for overcast
            floatColor *= (1 - 0.5f * overcast);
            SharedMaterialManager.FogColor.R = (byte)(floatColor.X * 255);
            SharedMaterialManager.FogColor.G = (byte)(floatColor.Y * 255);
            SharedMaterialManager.FogColor.B = (byte)(floatColor.Z * 255);
        }
    }
    #endregion
}
