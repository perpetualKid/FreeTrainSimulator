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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Viewer3D.Common;
using Orts.Viewer3D.Popups;
using ORTS.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Orts.Viewer3D
{
    [CallOnThread("Loader")]
    public class SharedTextureManager
    {
        readonly Viewer Viewer;
        readonly GraphicsDevice GraphicsDevice;
        Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        Dictionary<string, bool> TextureMarks;

        [CallOnThread("Render")]
        internal SharedTextureManager(Viewer viewer, GraphicsDevice graphicsDevice)
        {
            Viewer = viewer;
            GraphicsDevice = graphicsDevice;
        }

        public Texture2D Get(string path, bool required = false)
        {
            return (Get(path, SharedMaterialManager.MissingTexture, required));
        }

        public Texture2D Get(string path, Texture2D defaultTexture, bool required = false)
        {
            if (Thread.CurrentThread.Name != "Loader Process")
                Trace.TraceError("SharedTextureManager.Get incorrectly called by {0}; must be Loader Process or crashes will occur.", Thread.CurrentThread.Name);

            if (path == null || path == "")
                return defaultTexture;

            path = path.ToLowerInvariant();
            if (!Textures.ContainsKey(path))
            {
                try
                {
                    Texture2D texture;
                    if (Path.GetExtension(path) == ".dds" && File.Exists(path))
                    {
                        DDSLib.DDSFromFile(path, GraphicsDevice, true, out texture);
                    }
                    else if (Path.GetExtension(path) == ".ace")
                    {
                        var alternativeTexture = Path.ChangeExtension(path, ".dds");
                        
                        if (Viewer.Settings.PreferDDSTexture && File.Exists(alternativeTexture))
                        {
                            DDSLib.DDSFromFile(alternativeTexture, GraphicsDevice, true, out texture);
                        }
                        else if (File.Exists(path))
                        {
                            texture = Orts.Formats.Msts.AceFile.Texture2DFromFile(GraphicsDevice, path);
                        }
                        else
                        {
                            try //in case of no texture in wintersnow etc, go up one level
                            {
                                var p = System.IO.Directory.GetParent(path);//returns the current level of dir

                                p = System.IO.Directory.GetParent(p.FullName);//go up one level
                                var s = p.FullName + "\\" + Path.GetFileName(path);
                                if (File.Exists(s) &&  s.ToLower().Contains("texture")) //in texure and exists
                                {
                                    texture = Orts.Formats.Msts.AceFile.Texture2DFromFile(GraphicsDevice, s);
                                }
                                else {
                                    if (required)
                                        Trace.TraceWarning("Missing texture {0} replaced with default texture", path);
                                    return defaultTexture;
                                }
                            }
                            catch { texture = defaultTexture; return defaultTexture; }
                        }
                    }
                    else
                        return defaultTexture;

                    Textures.Add(path, texture);
                    return texture;
                }
                catch (InvalidDataException error)
                {
                    Trace.TraceWarning("Skipped texture with error: {1} in {0}", path, error.Message);
                    return defaultTexture;
                }
                catch (Exception error)
                {
                    if (File.Exists(path))
                        Trace.WriteLine(new FileLoadException(path, error));
                    else
                        Trace.TraceWarning("Ignored missing texture file {0}", path);
                    return defaultTexture;
                }
            }
            else
            {
                return Textures[path];
            }
        }

        public static Texture2D Get(GraphicsDevice graphicsDevice, string path)
        {
            if (path == null || path == "")
                return SharedMaterialManager.MissingTexture;

            path = path.ToLowerInvariant();
            var ext = Path.GetExtension(path);

            if (ext == ".ace")
                return Orts.Formats.Msts.AceFile.Texture2DFromFile(graphicsDevice, path);

            using (var stream = File.OpenRead(path))
            {
                if (ext == ".gif" || ext == ".jpg" || ext == ".png")
                    return Texture2D.FromStream(graphicsDevice, stream);
                else if (ext == ".bmp")
                    using (var image = System.Drawing.Image.FromStream(stream))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            return Texture2D.FromStream(graphicsDevice, memoryStream);
                        }
                    }
                else
                    Trace.TraceWarning("Unsupported texture format: {0}", path);
                return SharedMaterialManager.MissingTexture;
            }
        }
        
        public void Mark()
        {
            TextureMarks = new Dictionary<string, bool>(Textures.Count);
            foreach (var path in Textures.Keys)
                TextureMarks.Add(path, false);
        }

        public void Mark(Texture2D texture)
        {
            if (Textures.ContainsValue(texture))
                TextureMarks[Textures.First(kvp => kvp.Value == texture).Key] = true;
        }

        public void Sweep()
        {
            foreach (var path in TextureMarks.Where(kvp => !kvp.Value).Select(kvp => kvp.Key))
                Textures.Remove(path);
        }

        [CallOnThread("Updater")]
        public string GetStatus()
        {
            return Viewer.Catalog.GetPluralStringFmt("{0:F0} texture", "{0:F0} textures", Textures.Keys.Count);
        }
    }

    [CallOnThread("Loader")]
    public class SharedMaterialManager
    {
        readonly Viewer Viewer;
        Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        Dictionary<string, bool> MaterialMarks = new Dictionary<string, bool>();

        public readonly LightConeShader LightConeShader;
        public readonly LightGlowShader LightGlowShader;
        public readonly ParticleEmitterShader ParticleEmitterShader;
        public readonly PopupWindowShader PopupWindowShader;
        public readonly PrecipitationShader PrecipitationShader;
        public readonly SceneryShader SceneryShader;
        public readonly ShadowMapShader ShadowMapShader;
        public readonly ShadowMapShader[] ShadowMapShaders;
        public readonly SkyShader SkyShader;
        public readonly DebugShader DebugShader;

        public static Texture2D MissingTexture;
        public static Texture2D DefaultSnowTexture;
        public static Texture2D DefaultDMSnowTexture;

        [CallOnThread("Render")]
        public SharedMaterialManager(Viewer viewer)
        {
            Viewer = viewer;
            // TODO: Move to Loader process.
            LightConeShader = new LightConeShader(viewer.RenderProcess.GraphicsDevice);
            LightGlowShader = new LightGlowShader(viewer.RenderProcess.GraphicsDevice);
            ParticleEmitterShader = new ParticleEmitterShader(viewer.RenderProcess.GraphicsDevice);
            PopupWindowShader = new PopupWindowShader(viewer, viewer.RenderProcess.GraphicsDevice);
            PrecipitationShader = new PrecipitationShader(viewer.RenderProcess.GraphicsDevice);
            SceneryShader = new SceneryShader(viewer.RenderProcess.GraphicsDevice);
            var microtexPath = viewer.Simulator.RoutePath + @"\TERRTEX\microtex.ace";
            if (File.Exists(microtexPath))
            {
                try
                {
                    SceneryShader.OverlayTexture = Orts.Formats.Msts.AceFile.Texture2DFromFile(viewer.GraphicsDevice, microtexPath);
                }
                catch (InvalidDataException error)
                {
                    Trace.TraceWarning("Skipped texture with error: {1} in {0}", microtexPath, error.Message);
                }
                catch (Exception error)
                {
                    Trace.WriteLine(new FileLoadException(microtexPath, error));
                }
            }
            ShadowMapShader = new ShadowMapShader(viewer.RenderProcess.GraphicsDevice);
            ShadowMapShaders = new ShadowMapShader[4];
            for (int i = 0; i < ShadowMapShaders.Length; i++)
            {
                ShadowMapShaders[i] = new ShadowMapShader(viewer.RenderProcess.GraphicsDevice);
            }
            SkyShader = new SkyShader(viewer.RenderProcess.GraphicsDevice);
            DebugShader = new DebugShader(viewer.RenderProcess.GraphicsDevice);

            // TODO: This should happen on the loader thread.
            MissingTexture = SharedTextureManager.Get(viewer.RenderProcess.GraphicsDevice, Path.Combine(viewer.ContentPath, "blank.bmp"));

            // Managing default snow textures
            var defaultSnowTexturePath = viewer.Simulator.RoutePath + @"\TERRTEX\SNOW\ORTSDefaultSnow.ace";
            DefaultSnowTexture = Viewer.TextureManager.Get(defaultSnowTexturePath);
            var defaultDMSnowTexturePath = viewer.Simulator.RoutePath + @"\TERRTEX\SNOW\ORTSDefaultDMSnow.ace";
            DefaultDMSnowTexture = Viewer.TextureManager.Get(defaultDMSnowTexturePath);

        }

        public Material Load(string materialName)
        {
            return Load(materialName, null, 0, 0, 0, null);
        }

        public Material Load(string materialName, string textureName)
        {
            return Load(materialName, textureName, 0, 0, 0, null);
        }

        public Material Load(string materialName, string textureName, int options)
        {
            return Load(materialName, textureName, options, 0, 0, null);
        }

        public Material Load(string materialName, string textureName, int options, float mipMapBias)
        {
            return Load(materialName, textureName, options, 0, 0, null);
        }

        public Material Load(string materialName, string textureName, int options, float mipMapBias, int cabShaderKey, CabShader cabShader)
        {

            if (textureName != null)
                textureName = textureName.ToLower();

            var materialKey = String.Format("{0}:{1}:{2}:{3}:{4}", materialName, textureName, options, mipMapBias, cabShaderKey);

            if (!Materials.ContainsKey(materialKey))
            {
                switch (materialName)
                {
                    case "Debug":
                        Materials[materialKey] = new HUDGraphMaterial(Viewer);
                        break;
                    case "DebugNormals":
                        Materials[materialKey] = new DebugNormalMaterial(Viewer);
                        break;
                    case "Forest":
                        Materials[materialKey] = new ForestMaterial(Viewer, textureName);
                        break;
                    case "Label3D":
                        Materials[materialKey] = new Label3DMaterial(Viewer);
                        break;
                    case "LightCone":
                        Materials[materialKey] = new LightConeMaterial(Viewer);
                        break;
                    case "LightGlow":
                        Materials[materialKey] = new LightGlowMaterial(Viewer);
                        break;
                    case "PopupWindow":
                        Materials[materialKey] = new PopupWindowMaterial(Viewer);
                        break;
                    case "ParticleEmitter":
                        Materials[materialKey] = new ParticleEmitterMaterial(Viewer, textureName);
                        break;
                    case "Precipitation":
                        Materials[materialKey] = new PrecipitationMaterial(Viewer);
                        break;
                    case "Scenery":
                        Materials[materialKey] = new SceneryMaterial(Viewer, textureName, (SceneryMaterialOptions)options, mipMapBias);
                        break;
                    case "ShadowMap":
                        Materials[materialKey] = new ShadowMapMaterial(Viewer);
                        break;
                    case "SignalLight":
                        Materials[materialKey] = new SignalLightMaterial(Viewer, textureName);
                        break;
                    case "SignalLightGlow":
                        Materials[materialKey] = new SignalLightGlowMaterial(Viewer);
                        break;
                    case "Sky":
                        Materials[materialKey] = new SkyMaterial(Viewer);
                        break;
                    case "MSTSSky":
                        Materials[materialKey] = new MSTSSkyMaterial(Viewer);
                        break;
                    case "SpriteBatch":
                        Materials[materialKey] = new SpriteBatchMaterial(Viewer);
                        break;
                    case "CabSpriteBatch":
                        Materials[materialKey] = new CabSpriteBatchMaterial(Viewer, cabShader);
                        break;
                    case "Terrain":
                        Materials[materialKey] = new TerrainMaterial(Viewer, textureName, SharedMaterialManager.MissingTexture);
                        break;
                    case "TerrainShared":
                        Materials[materialKey] = new TerrainSharedMaterial(Viewer, textureName);
                        break;
                    case "TerrainSharedDistantMountain":
                        Materials[materialKey] = new TerrainSharedDistantMountain(Viewer, textureName);
                        break;
                    case "Transfer":
                        Materials[materialKey] = new TransferMaterial(Viewer, textureName);
                        break;
                    case "Water":
                        Materials[materialKey] = new WaterMaterial(Viewer, textureName);
                        break;
                    default:
                        Trace.TraceInformation("Skipped unknown material type {0}", materialName);
                        Materials[materialKey] = new YellowMaterial(Viewer);
                        break;
                }
            }
            return Materials[materialKey];
        }

       public bool LoadNightTextures()
        {
            int count = 0;
            foreach (KeyValuePair<string, Material> materialPair in Materials)
            {
                 if (materialPair.Value is SceneryMaterial)
                {
                    var material = materialPair.Value as SceneryMaterial;
                    if (material.LoadNightTexture()) count++;
                     if (count >= 20)
                     {
                         count = 0;
                         // retest if there is enough free memory left;
                         var remainingMemorySpace = Viewer.LoadMemoryThreshold - Viewer.HUDWindow.GetWorkingSetSize();
                         if (remainingMemorySpace < 0)
                         { 
                             return false; // too bad, no more space, other night textures won't be loaded
                         }
                     }
                }
            }
            return true;
         }

       public bool LoadDayTextures()
       {
           int count = 0;
           foreach (KeyValuePair<string, Material> materialPair in Materials)
           {
               if (materialPair.Value is SceneryMaterial)
               {
                   var material = materialPair.Value as SceneryMaterial;
                   if (material.LoadDayTexture()) count++;
                   if (count >= 20)
                   {
                       count = 0;
                       // retest if there is enough free memory left;
                       var remainingMemorySpace = Viewer.LoadMemoryThreshold - Viewer.HUDWindow.GetWorkingSetSize();
                       if (remainingMemorySpace < 0)
                       {
                           return false; // too bad, no more space, other night textures won't be loaded
                       }
                   }
               }
           }
           return true;
       }

        public void Mark()
        {
            MaterialMarks = new Dictionary<string, bool>(Materials.Count);
            foreach (var path in Materials.Keys)
                MaterialMarks.Add(path, false);
        }

        public void Mark(Material material)
        {
            if (Materials.ContainsValue(material))
                MaterialMarks[Materials.First(kvp => kvp.Value == material).Key] = true;
        }

        public void Sweep()
        {
            foreach (var path in MaterialMarks.Where(kvp => !kvp.Value).Select(kvp => kvp.Key))
                Materials.Remove(path);
		}

        public void LoadPrep()
        {
            if (Viewer.Settings.UseMSTSEnv == false)
            {
                Viewer.World.Sky.LoadPrep();
                sunDirection = Viewer.World.Sky.solarDirection;
            }
            else
            {
                Viewer.World.MSTSSky.LoadPrep();
                sunDirection = Viewer.World.MSTSSky.mstsskysolarDirection;
            }
        }


        [CallOnThread("Updater")]
        public string GetStatus()
        {
            return Viewer.Catalog.GetPluralStringFmt("{0:F0} material", "{0:F0} materials", Materials.Keys.Count);
        }

        public static Color FogColor = new Color(110, 110, 110, 255);

        internal Vector3 sunDirection;
        bool lastLightState;
        double fadeStartTimer;
        float fadeDuration = -1;
        float clampValue = 1;
        float distance = 1000;
        internal void UpdateShaders()
        {
            if(Viewer.Settings.UseMSTSEnv == false)
                sunDirection = Viewer.World.Sky.solarDirection;
            else
                sunDirection = Viewer.World.MSTSSky.mstsskysolarDirection;

            SceneryShader.SetLightVector_ZFar(sunDirection, Viewer.Settings.ViewingDistance);
            
            // Headlight illumination
            if (Viewer.PlayerLocomotiveViewer != null
                && Viewer.PlayerLocomotiveViewer.lightDrawer != null
                && Viewer.PlayerLocomotiveViewer.lightDrawer.HasLightCone)
            {
                var lightDrawer = Viewer.PlayerLocomotiveViewer.lightDrawer;
                var lightState = lightDrawer.IsLightConeActive;
                if (lightState != lastLightState)
                {
                    if (lightDrawer.LightConeFadeIn > 0)
                    {
                        fadeStartTimer = Viewer.Simulator.GameTime;
                        fadeDuration = lightDrawer.LightConeFadeIn;
                    }
                    else if (lightDrawer.LightConeFadeOut > 0)
                    {
                        fadeStartTimer = Viewer.Simulator.GameTime;
                        fadeDuration = -lightDrawer.LightConeFadeOut;
                    }
                    lastLightState = lightState;
                }
                else if (!lastLightState && fadeDuration < 0 && Viewer.Simulator.GameTime > fadeStartTimer - fadeDuration)
                {
                    fadeDuration = 0;
                }
                if (!lightState && fadeDuration == 0)
                    // This occurs when switching locos and needs to be handled or we get lingering light.
                    SceneryShader.SetHeadlightOff();
                else
                {
                    if (sunDirection.Y <= -0.05)
                    {
                        clampValue = 1; // at nighttime max headlight
                        distance = lightDrawer.LightConeDistance; // and max distance
                    }
                    else if (sunDirection.Y >= 0.15)
                    {
                        clampValue = 0.5f; // at daytime min headlight
                        distance = lightDrawer.LightConeDistance*0.1f; // and min distance

                    }
                    else
                    {
                        clampValue = 1 - 2.5f * (sunDirection.Y + 0.05f); // in the meantime interpolate
                        distance = lightDrawer.LightConeDistance*(1-4.5f*(sunDirection.Y + 0.05f)); //ditto
                    }
                    SceneryShader.SetHeadlight(ref lightDrawer.LightConePosition, ref lightDrawer.LightConeDirection, distance, lightDrawer.LightConeMinDotProduct, (float)(Viewer.Simulator.GameTime - fadeStartTimer), fadeDuration, clampValue, ref lightDrawer.LightConeColor);
                }
            }
            else
            {
                SceneryShader.SetHeadlightOff();
            }
            // End headlight illumination
            if (Viewer.Settings.UseMSTSEnv == false)
            {
                SceneryShader.Overcast = Viewer.Simulator.Weather.OvercastFactor;
                SceneryShader.SetFog(Viewer.Simulator.Weather.FogDistance, ref SharedMaterialManager.FogColor);
                ParticleEmitterShader.SetFog(Viewer.Simulator.Weather.FogDistance, ref SharedMaterialManager.FogColor);
                SceneryShader.ViewerPos = Viewer.Camera.XnaLocation(Viewer.Camera.CameraWorldLocation);
            }
            else
            {
                SceneryShader.Overcast = Viewer.World.MSTSSky.mstsskyovercastFactor;
                SceneryShader.SetFog(Viewer.World.MSTSSky.mstsskyfogDistance, ref SharedMaterialManager.FogColor);
                ParticleEmitterShader.SetFog(Viewer.Simulator.Weather.FogDistance, ref SharedMaterialManager.FogColor);
                SceneryShader.ViewerPos = Viewer.Camera.XnaLocation(Viewer.Camera.CameraWorldLocation);
            }
        }
    }

    public abstract class Material
    {
        protected readonly Viewer Viewer;
        private readonly string key;

        protected Material(Viewer viewer, string key)
        {
            Viewer = viewer;
            this.key = key;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(key))
                return GetType().Name;
            return string.Format("{0}({1})", GetType().Name, key);
        }

        public virtual void SetState(GraphicsDevice graphicsDevice, Material previousMaterial) { }

        public virtual void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices) { }

        public virtual void ResetState(GraphicsDevice graphicsDevice) { }

        public virtual bool GetBlending() { return false; }

        public virtual Texture2D GetShadowTexture() { return null; }
        
        public SamplerState SamplerState = SamplerState.LinearWrap;

        public int KeyLengthRemainder() //used as a "pseudorandom" number
        {
            return key?.Length % 10 ?? 0;
        }

        public Camera CurrentCamera { get { return Viewer.Camera; } }

        [CallOnThread("Loader")]
        public virtual void Mark()
        {
            Viewer.MaterialManager.Mark(this);
        }
    }

    public class EmptyMaterial : Material
    {
        public EmptyMaterial(Viewer viewer)
            : base(viewer, null)
        {
        }
    }

    public class BasicMaterial : Material
    {
        public BasicMaterial(Viewer viewer, string key)
            : base(viewer, key)
        {
        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            for (int i = 0; i < renderItems.Count; i++)
                renderItems[i].RenderPrimitive.Draw(graphicsDevice);
        }
    }

    public class BasicBlendedMaterial : BasicMaterial
    {
        public BasicBlendedMaterial(Viewer viewer, string key)
            : base(viewer, key)
        {
        }

        public override bool GetBlending()
        {
            return true;
        }
    }

    public class SpriteBatchMaterial : BasicBlendedMaterial
    {
        public readonly SpriteBatch SpriteBatch;

        public SpriteBatchMaterial(Viewer viewer)
            : base(viewer, null)
        {
            SpriteBatch = new SpriteBatch(Viewer.RenderProcess.GraphicsDevice);
        }

        public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
        {
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
        }

        public override void ResetState(GraphicsDevice graphicsDevice)
        {
            SpriteBatch.End();

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }

    public class CabSpriteBatchMaterial : BasicBlendedMaterial
    {
        public readonly SpriteBatch SpriteBatch;
        private CabShader CabShader;

        public CabSpriteBatchMaterial(Viewer viewer, CabShader cabShader)
            : base(viewer, null)
        {
            SpriteBatch = new SpriteBatch(Viewer.RenderProcess.GraphicsDevice);
            CabShader = cabShader;
        }

        public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
        {
            if (CabShader != null)
                SpriteBatch.Begin(0, BlendState.NonPremultiplied, null, DepthStencilState.Default, null, CabShader);
            else
                SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
        }

        public override void ResetState(GraphicsDevice graphicsDevice)
        {
            SpriteBatch.End();

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }

    [Flags]
    public enum SceneryMaterialOptions
    {
        None = 0,
        // Diffuse
        Diffuse = 0x1,
        // Alpha test
        AlphaTest = 0x2,
        // Blending
        AlphaBlendingNone = 0x0,
        AlphaBlendingBlend = 0x4,
        AlphaBlendingAdd = 0x8,
        AlphaBlendingMask = 0xC,
        // Shader
        ShaderImage = 0x00,
        ShaderDarkShade = 0x10,
        ShaderHalfBright = 0x20,
        ShaderFullBright = 0x30,
        ShaderVegetation = 0x40,
        ShaderMask = 0x70,
        // Lighting
        Specular0 = 0x000,
        Specular25 = 0x080,
        Specular750 = 0x100,
        SpecularMask = 0x180,
        // Texture address mode
        TextureAddressModeWrap = 0x000,
        TextureAddressModeMirror = 0x200,
        TextureAddressModeClamp = 0x400,
        TextureAddressModeMask = 0x600,
        // Night texture
        NightTexture = 0x800,
        // Texture to be shown in tunnels and underground (used for 3D cab night textures)
        UndergroundTexture = 0x40000000,
    }

    public class SceneryMaterial : Material
    {
        private readonly float timeOffset;
        readonly bool nightTextureEnabled;
        readonly bool undergroundTextureEnabled;
        private readonly SceneryMaterialOptions options;
        protected Texture2D dayTexture;
        protected Texture2D nightTexture;
        private readonly string texturePath;
        private readonly byte aceAlphaBits;   // the number of bits in the ace file's alpha channel 

        private EffectPassCollection shaderPasses;
        private readonly SceneryShader shader;
        private static int[] shaderTechniqueLookup;

        public static readonly DepthStencilState DepthReadCompareLess = new DepthStencilState
        {
            DepthBufferWriteEnable = false,
            DepthBufferFunction = CompareFunction.Less,
        };

        private static readonly Dictionary<float, SamplerState>[] samplerStates = new Dictionary<float, SamplerState>[4]; //Length of TextureAddressMode Values

        public SceneryMaterial(Viewer viewer, string texturePath, SceneryMaterialOptions options, float mipMapBias)
            : base(viewer, string.Format("{0}:{1:X}:{2}", texturePath, options, mipMapBias))
        {
            this.options = options;
            this.SamplerState = GetShadowTextureAddressMode(mipMapBias, options);
            this.texturePath = texturePath;
            dayTexture = SharedMaterialManager.MissingTexture;
            nightTexture = SharedMaterialManager.MissingTexture;
            // <CSComment> if "trainset" is in the path (true for night textures for 3DCabs) deferred load of night textures is disabled 
            if (!String.IsNullOrEmpty(texturePath) && (options & SceneryMaterialOptions.NightTexture) != 0 && ((!viewer.DontLoadNightTextures && !viewer.DontLoadDayTextures)
                || texturePath.Contains(@"\trainset\")))
            {
                var nightTexturePath = Helpers.GetNightTextureFile(Viewer.Simulator, texturePath);
                if (!String.IsNullOrEmpty(nightTexturePath))
                    nightTexture = Viewer.TextureManager.Get(nightTexturePath.ToLower());
                dayTexture = Viewer.TextureManager.Get(texturePath, true);
            }
            else if ((options & SceneryMaterialOptions.NightTexture) != 0 && viewer.DontLoadNightTextures)
            {
                viewer.NightTexturesNotLoaded = true;
                dayTexture = Viewer.TextureManager.Get(texturePath, true);
            }

            else if ((options & SceneryMaterialOptions.NightTexture) != 0 && viewer.DontLoadDayTextures)
            {
                var nightTexturePath = Helpers.GetNightTextureFile(Viewer.Simulator, texturePath);
                if (!String.IsNullOrEmpty(nightTexturePath))
                    nightTexture = Viewer.TextureManager.Get(nightTexturePath.ToLower());
                if (nightTexture != SharedMaterialManager.MissingTexture)
                {
                    viewer.DayTexturesNotLoaded = true;
                }
            }
            else dayTexture = Viewer.TextureManager.Get(texturePath, true);

            // Record the number of bits in the alpha channel of the original ace file
            var missingTexture = SharedMaterialManager.MissingTexture;
            if (dayTexture != null && dayTexture != SharedMaterialManager.MissingTexture)
                missingTexture = dayTexture;
            else if (nightTexture != null && nightTexture != SharedMaterialManager.MissingTexture)
                missingTexture = nightTexture;
            if (missingTexture.Tag?.GetType() == typeof(Orts.Formats.Msts.AceInfo))
                aceAlphaBits = ((Orts.Formats.Msts.AceInfo)missingTexture.Tag).AlphaBits;
            else
                aceAlphaBits = 0;

            // map shader techniques from Name to their index to avoid costly name-based lookups at runtime
            //this can be static as the techniques are constant for all scenery
            //possible mask values are 0x00, 0x10, 0x20, 0x30 and 0x40 as well 0x30|0x40, so we use a int[8] to map the values/0x10 by single-digit index (leaves two blanks in the array at 0x50 and 0x60)
            shader = Viewer.MaterialManager.SceneryShader;
            if (null == shaderTechniqueLookup)
            {
                shaderTechniqueLookup = new int[8];
                for (int i = 0; i < shader.Techniques.Count; i++)
                {
                    if (Viewer.Settings.ShaderModel > 2)
                    {
                        switch (shader.Techniques[i].Name)
                        {
                            case "ImagePS3":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderImage >> 4] = i; break;         //[0]
                            case "DarkShadePS3":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderDarkShade >> 4] = i; break;     //[1]   
                            case "HalfBrightPS3":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderHalfBright >> 4] = i; break;    //[2]
                            case "FullBrightPS3":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderFullBright >> 4] = i; break;    //[3]
                            case "VegetationPS3":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderVegetation >> 4] = i;           //[4]
                                shaderTechniqueLookup[(int)(SceneryMaterialOptions.ShaderVegetation | SceneryMaterialOptions.ShaderFullBright) >> 4] = i; break;    //[7]
                        }
                    }
                    else
                    {
                        switch (shader.Techniques[i].Name)
                        {
                            case "ImagePS2":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderImage >> 4] = i; break;         //[0]
                            case "DarkShadePS2":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderDarkShade >> 4] = i; break;     //[1]   
                            case "HalfBrightPS2":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderHalfBright >> 4] = i; break;    //[2]
                            case "FullBrightPS2":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderFullBright >> 4] = i; break;    //[3]
                            case "VegetationPS2":
                                shaderTechniqueLookup[(int)SceneryMaterialOptions.ShaderVegetation >> 4] = i;           //[4]
                                shaderTechniqueLookup[(int)(SceneryMaterialOptions.ShaderVegetation | SceneryMaterialOptions.ShaderFullBright) >> 4] = i; break;    //[7]
                        }

                    }
                }
            }

            timeOffset = (KeyLengthRemainder()) / 5000f; // TODO for later use for pseudorandom texture switch time
            nightTextureEnabled = nightTexture != null && nightTexture != SharedMaterialManager.MissingTexture;
            undergroundTextureEnabled = (options & SceneryMaterialOptions.UndergroundTexture) != 0;
        }

        public bool LoadNightTexture()
        {
            bool result = false;
            if (((options & SceneryMaterialOptions.NightTexture) != 0) && (nightTexture == SharedMaterialManager.MissingTexture))
            {
                var nightTexturePath = Helpers.GetNightTextureFile(Viewer.Simulator, texturePath);
                if (!string.IsNullOrEmpty(nightTexturePath))
                {
                    nightTexture = Viewer.TextureManager.Get(nightTexturePath);
                    result = true;
                }
            }
            return result;
        }

        public bool LoadDayTexture()
        {
            bool result = false;
            if (dayTexture == SharedMaterialManager.MissingTexture && !String.IsNullOrEmpty(texturePath))
            {
                dayTexture = Viewer.TextureManager.Get(texturePath);
                result = true;
            }
            return result;
        }

        public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
        {
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            shader.LightingDiffuse = (options & SceneryMaterialOptions.Diffuse) != 0 ? 1 : 0;

            // Set up for alpha blending and alpha test 

            if (GetBlending())
            {
                // Skip blend for near transparent alpha's (eliminates sorting issues for many simple alpha'd textures )
                if (previousMaterial == null  // Search for opaque pixels in alpha blended polygons
                    && (options & SceneryMaterialOptions.AlphaBlendingMask) != SceneryMaterialOptions.AlphaBlendingAdd)
                {
                    // Enable alpha blending for everything: this allows distance scenery to appear smoothly.
                    graphicsDevice.BlendState = BlendState.NonPremultiplied;
                    graphicsDevice.DepthStencilState = DepthStencilState.Default;

                    shader.ReferenceAlpha = 250;
                }
                else // Alpha blended pixels only
                {
                    shader.ReferenceAlpha = 10;  // ie default lightcone's are 9 in full transparent areas

                    // Set up for blending
                    if ((options & SceneryMaterialOptions.AlphaBlendingMask) == SceneryMaterialOptions.AlphaBlendingBlend)
                    {
                        graphicsDevice.BlendState = BlendState.NonPremultiplied;
                        graphicsDevice.DepthStencilState = DepthReadCompareLess; // To avoid processing already drawn opaque pixels
                    }
                    else
                    {
                        graphicsDevice.BlendState = BlendState.Additive;
                        graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    }
                }
            }
            else
            {
                // Enable alpha blending for everything: this allows distance scenery to appear smoothly.
                graphicsDevice.BlendState = BlendState.Opaque;

                if ((options & SceneryMaterialOptions.AlphaTest) != 0)
                {
                    // Transparency testing is enabled
                    shader.ReferenceAlpha = 200;  // setting this to 128, chain link fences become solid at distance, at 200, they become
                }
                else
                {
                    // Solid rendering.
                    shader.ReferenceAlpha = -1;
                }
            }

            shader.CurrentTechnique = shader.Techniques[shaderTechniqueLookup[(int)(options & SceneryMaterialOptions.ShaderMask) >> 4]];
            shaderPasses = shader.CurrentTechnique.Passes;

            switch (options & SceneryMaterialOptions.SpecularMask)
            {
                case SceneryMaterialOptions.Specular0:
                    shader.LightingSpecular = 0;
                    break;
                case SceneryMaterialOptions.Specular25:
                    shader.LightingSpecular = 25;
                    break;
                case SceneryMaterialOptions.Specular750:
                    shader.LightingSpecular = 750;
                    break;
                default:
                    throw new InvalidDataException("Options has unexpected SceneryMaterialOptions.SpecularMask value.");
            }

            graphicsDevice.SamplerStates[0] = SamplerState;

            if (nightTextureEnabled && ((undergroundTextureEnabled && Viewer.MaterialManager.sunDirection.Y < -0.085f || Viewer.Camera.IsUnderground) ||
            Viewer.MaterialManager.sunDirection.Y < 0.0f - timeOffset))
            //if (nightTexture != null && nightTexture != SharedMaterialManager.MissingTexture && (((options & SceneryMaterialOptions.UndergroundTexture) != 0 &&
            //    (Viewer.MaterialManager.sunDirection.Y < -0.085f || Viewer.Camera.IsUnderground)) || Viewer.MaterialManager.sunDirection.Y < 0.0f - ((float)KeyLengthRemainder()) / 5000f))
            {
                shader.ImageTexture = nightTexture;
                shader.ImageTextureIsNight = true;
            }
            else
            {
                shader.ImageTexture = dayTexture;
                shader.ImageTextureIsNight = false;
            }

        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            for (int j = 0; j < shaderPasses.Count; j++)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    shader.SetMatrix(item.XNAMatrix, ref matrices[(int)ViewMatrixSequence.ViewProjection]);
                    shader.ZBias = item.RenderPrimitive.ZBias;
                    shaderPasses[j].Apply();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
            }
        }

        public override void ResetState(GraphicsDevice graphicsDevice)
        {
            shader.ImageTextureIsNight = false;
            shader.LightingDiffuse = 1;
            shader.LightingSpecular = 0;
            shader.ReferenceAlpha = 0;

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        /// <summary>
        /// Return true if this material requires alpha blending
        /// </summary>
        /// <returns></returns>
        public override bool GetBlending()
        {
            bool alphaTestRequested = (options & SceneryMaterialOptions.AlphaTest) != 0;            // the artist requested alpha testing for this material
            bool alphaBlendRequested = (options & SceneryMaterialOptions.AlphaBlendingMask) != 0;   // the artist specified a blend capable shader

            return alphaBlendRequested                                   // the material is using a blend capable shader   
                    && (aceAlphaBits > 1                                    // and the original ace has more than 1 bit of alpha
                          || (aceAlphaBits == 1 && !alphaTestRequested));    //  or its just 1 bit, but with no alphatesting, we must blend it anyway

            // To summarize, assuming we are using a blend capable shader ..
            //     0 bits of alpha - never blend
            //     1 bit of alpha - only blend if the alpha test wasn't requested
            //     >1 bit of alpha - always blend
        }

        public override Texture2D GetShadowTexture()
        {
            //var timeOffset = (KeyLengthRemainder()) / 5000f; // TODO for later use for pseudorandom texture switch time
            //if (nightTexture != null && nightTexture != SharedMaterialManager.MissingTexture && (((options & SceneryMaterialOptions.UndergroundTexture) != 0 &&
            //    (Viewer.MaterialManager.sunDirection.Y < -0.085f || Viewer.Camera.IsUnderground)) || Viewer.MaterialManager.sunDirection.Y < 0.0f - ((float)KeyLengthRemainder()) / 5000f))
            //    return nightTexture;

            //return dayTexture;
            if (nightTextureEnabled && ((undergroundTextureEnabled && Viewer.MaterialManager.sunDirection.Y < -0.085f || Viewer.Camera.IsUnderground) 
                || Viewer.MaterialManager.sunDirection.Y < 0.0f - timeOffset))
                    return nightTexture;
            return dayTexture;
        }

        private static SamplerState GetShadowTextureAddressMode(float mipMapBias, SceneryMaterialOptions options)
        {
            mipMapBias = Math.Max(mipMapBias, -1);// MipMapBias < -1 ? -1 : MipMapBias;
            int textureAddressMode = (int)(options & SceneryMaterialOptions.TextureAddressModeMask);

            if (samplerStates[textureAddressMode] == null)
            {
                lock (samplerStates)
                {
                    if (samplerStates[textureAddressMode] == null)
                        samplerStates[textureAddressMode] = new Dictionary<float, SamplerState>();
                }
            }

            if (!samplerStates[textureAddressMode].ContainsKey(mipMapBias))
            {
                lock (samplerStates[textureAddressMode])
                {
                    if (!samplerStates[textureAddressMode].ContainsKey(mipMapBias))
                        samplerStates[textureAddressMode].Add(mipMapBias, new SamplerState
                        {
                            AddressU = (TextureAddressMode)textureAddressMode,
                            AddressV = (TextureAddressMode)textureAddressMode,
                            Filter = TextureFilter.Anisotropic,
                            MaxAnisotropy = 16,
                            MipMapLevelOfDetailBias = mipMapBias
                        });
                }
            }
            return samplerStates[textureAddressMode][mipMapBias];
        }

        public override void Mark()
        {
            Viewer.TextureManager.Mark(dayTexture);
            Viewer.TextureManager.Mark(nightTexture);
            base.Mark();
        }
    }

    public class ShadowMapMaterial : Material
    {
        private EffectPassCollection shaderPasses;
        private readonly VertexBuffer blurVertexBuffer;
        private readonly ShadowMapShader shader;
        //SemaphoreSlim test;
        //int renderIndex;

        //Order needs to match order of techniques in ShadowMap.fx to simplify lookup
        //Blur map coming last (not used in enum)
        public enum Mode
        {
            Normal,
            Forest,
            Blocker,
            Blur,
        }

        public ShadowMapMaterial(Viewer viewer)
            : base(viewer, null)
        {
            int shadowMapResolution = Viewer.Settings.ShadowMapResolution;
            blurVertexBuffer = new VertexBuffer(Viewer.RenderProcess.GraphicsDevice, typeof(VertexPositionTexture), 4, BufferUsage.WriteOnly);
            blurVertexBuffer.SetData(new[] {
               new VertexPositionTexture(new Vector3(-1, +1, 0), new Vector2(0, 0)),
               new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, shadowMapResolution)),
               new VertexPositionTexture(new Vector3(+1, +1, 0), new Vector2(shadowMapResolution, 0)),
               new VertexPositionTexture(new Vector3(+1, -1, 0), new Vector2(shadowMapResolution, shadowMapResolution)),
            });
            shader = Viewer.MaterialManager.ShadowMapShader;
//            test = new SemaphoreSlim(1);

        }

        public void SetState(GraphicsDevice graphicsDevice, Mode mode)
        {
            shader.CurrentTechnique = shader.Techniques[(int)mode]; //order of techniques equals order in ShadowMap.fx, avoiding costly name-based lookups at runtime

            for (int i = 0; i < Viewer.MaterialManager.ShadowMapShaders.Length; i++)
            {
                Viewer.MaterialManager.ShadowMapShaders[i].CurrentTechnique = Viewer.MaterialManager.ShadowMapShaders[i].Techniques[(int)mode];
            }

            shaderPasses = shader.CurrentTechnique.Passes;
            graphicsDevice.RasterizerState = mode == Mode.Blocker ? RasterizerState.CullClockwise : RasterizerState.CullCounterClockwise;
        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            //renderIndex = -1;
            //System.Threading.Tasks.Parallel.ForEach(Viewer.MaterialManager.ShadowMapShaders, (shadowMapShader) =>
            //    RenderParallel(graphicsDevice, renderItems, shadowMapShader, matrices, renderItemDirty));

            shader.SetData(ref matrices[(int)ViewMatrixSequence.View]);
            for (int j = 0; j < shaderPasses.Count; j++)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    Matrix wvp = item.XNAMatrix;
                    Matrix.Multiply(ref wvp, ref matrices[(int)ViewMatrixSequence.ViewProjection], out wvp);
                    shader.SetData(ref wvp, item.Material.GetShadowTexture());
                    shaderPasses[j].Apply();
                    graphicsDevice.SamplerStates[0] = item.Material.SamplerState;
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
            }
        }

//        private void RenderParallel(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, ShadowMapShader shadowMapShader, Matrix[] matrices, bool renderItemDirty)
//        {
//            shadowMapShader.SetData(ref matrices[0]);
//            //            foreach (var pass  in shadowMapShader.CurrentTechnique.Passes)
//            for (int j = 0; j < shadowMapShader.CurrentTechnique.Passes.Count; j++)
//            {
//                int i = Interlocked.Increment(ref renderIndex);
//                while (i < renderItems.Count)
//                {
//                    RenderItem item = renderItems[i];
//                    if (renderItemDirty)
//                    {
//                        Matrix wvp = item.XNAMatrix;
//                        Matrix.Multiply(ref wvp, ref matrices[2], out item.WVP);
//                    }
//                    //Matrix wvp = item.XNAMatrix;
//                    //Matrix.Multiply(ref wvp, ref matrices[2], out item.WVP);
//                    shadowMapShader.SetData(ref item.WVP, item.Material.GetShadowTexture());
//                    SamplerState current = item.Material.SamplerState;
//                    lock (graphicsDevice)
//                    {
//                        graphicsDevice.SamplerStates[0] = current;
//                        shadowMapShader.CurrentTechnique.Passes[j].Apply();
//                        item.RenderPrimitive.Draw(graphicsDevice);
//                    }
////                    test.Release();
//                    i = Interlocked.Increment(ref renderIndex);
//                }
//            }
//        }

        public RenderTarget2D ApplyBlur(GraphicsDevice graphicsDevice, RenderTarget2D shadowMap, RenderTarget2D renderTarget)
        {
            var wvp = Matrix.Identity;

            shader.CurrentTechnique = shader.Techniques[(int)Mode.Blur];
            shader.SetBlurData(ref wvp);

            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.SetVertexBuffer(blurVertexBuffer);

            for (int j = 0; j < shader.CurrentTechnique.Passes.Count; j++)
            {
                shader.SetBlurData(renderTarget);
                shader.CurrentTechnique.Passes[j].Apply();
                graphicsDevice.SetRenderTarget(shadowMap);
                graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
                graphicsDevice.SetRenderTarget(null);
            }
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;

            return shadowMap;
        }

        public override void ResetState(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }
    }

    public class PopupWindowMaterial : Material
    {
        private EffectPassCollection shaderPasses;
        private readonly PopupWindowShader shader;

        public PopupWindowMaterial(Viewer viewer)
            : base(viewer, null)
        {
            shader = Viewer.MaterialManager.PopupWindowShader;
        }

        public void SetState(GraphicsDevice graphicsDevice, Texture2D screen)
        {
            shader.CurrentTechnique = shader.Techniques[screen == null ? 0 : 1]; //screen == null ? shader.Techniques["PopupWindow"] : shader.Techniques["PopupWindowGlass"];
            shaderPasses = shader.CurrentTechnique.Passes;
            
            // FIXME: MonoGame cannot read backbuffer contents
            //shader.Screen = screen;
            shader.GlassColor = Color.Black;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
        }

        public void Render(GraphicsDevice graphicsDevice, RenderPrimitive renderPrimitive, ref Matrix worldMatrix, ref Matrix viewMatrix, ref Matrix projectionMatrix)
        {
            Matrix.Multiply(ref worldMatrix, ref viewMatrix, out Matrix wvp);
            Matrix.Multiply(ref wvp, ref projectionMatrix, out wvp);
//            Matrix wvp = worldMatrix * viewMatrix * projectionMatrix;
            shader.SetMatrix(ref worldMatrix, ref wvp);

            for (int j = 0; j < shaderPasses.Count; j++)
            {
                shaderPasses[j].Apply();
                renderPrimitive.Draw(graphicsDevice);
            }
        }

        public override void ResetState(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        public override bool GetBlending()
        {
            return true;
        }
    }

    public class YellowMaterial : Material
    {
        static BasicEffect basicEffect;

        public YellowMaterial(Viewer viewer)
            : base(viewer, null)
        {
            if (basicEffect == null)
            {
                basicEffect = new BasicEffect(Viewer.RenderProcess.GraphicsDevice);
                basicEffect.Alpha = 1.0f;
                basicEffect.DiffuseColor = new Vector3(197.0f / 255.0f, 203.0f / 255.0f, 37.0f / 255.0f);
                basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
                basicEffect.SpecularPower = 5.0f;
                basicEffect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);

                basicEffect.DirectionalLight0.Enabled = true;
                basicEffect.DirectionalLight0.DiffuseColor = Vector3.One * 0.8f;
                basicEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(1.0f, -1.0f, -1.0f));
                basicEffect.DirectionalLight0.SpecularColor = Vector3.One;

                basicEffect.DirectionalLight1.Enabled = true;
                basicEffect.DirectionalLight1.DiffuseColor = new Vector3(0.5f, 0.5f, 0.5f);
                basicEffect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(-1.0f, -1.0f, 1.0f));
                basicEffect.DirectionalLight1.SpecularColor = new Vector3(0.5f, 0.5f, 0.5f);

                basicEffect.LightingEnabled = true;
            }
        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            basicEffect.View = matrices[(int)ViewMatrixSequence.View];
            basicEffect.Projection = matrices[(int)ViewMatrixSequence.Projection];

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    basicEffect.World = item.XNAMatrix;
                    pass.Apply();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
            }
        }
    }

    public class SolidColorMaterial : Material
    {
        static BasicEffect basicEffect;

        public SolidColorMaterial(Viewer viewer, float a, float r, float g, float b)
            : base(viewer, null)
        {
            if (basicEffect == null)
            {
                basicEffect = new BasicEffect(Viewer.RenderProcess.GraphicsDevice);
                basicEffect.Alpha = a;
                basicEffect.DiffuseColor = new Vector3(r , g , b );
                basicEffect.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
                basicEffect.SpecularPower = 5.0f;
                basicEffect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);

                basicEffect.DirectionalLight0.Enabled = true;
                basicEffect.DirectionalLight0.DiffuseColor = Vector3.One * 0.8f;
                basicEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(1.0f, -1.0f, -1.0f));
                basicEffect.DirectionalLight0.SpecularColor = Vector3.One;

                basicEffect.DirectionalLight1.Enabled = true;
                basicEffect.DirectionalLight1.DiffuseColor = new Vector3(0.5f, 0.5f, 0.5f);
                basicEffect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(-1.0f, -1.0f, 1.0f));
                basicEffect.DirectionalLight1.SpecularColor = new Vector3(0.5f, 0.5f, 0.5f);

                basicEffect.LightingEnabled = true;
            }
        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            basicEffect.View = matrices[(int)ViewMatrixSequence.View];
            basicEffect.Projection = matrices[(int)ViewMatrixSequence.Projection];

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    basicEffect.World = item.XNAMatrix;
                    pass.Apply();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
            }
        }
    }

    public class Label3DMaterial : SpriteBatchMaterial
    {
        public Texture2D Texture { get; private set; }
        public WindowTextFont Font { get; private set; }

        private readonly List<Rectangle> textBoxes = new List<Rectangle>();

        public Label3DMaterial(Viewer viewer)
            : base(viewer)
        {
            Texture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            Texture.SetData(new[] { Color.White });
            Font = Viewer.WindowManager.TextManager.GetScaled("Arial", 12, System.Drawing.FontStyle.Bold, 1);
        }

        public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
        {
            var scaling = (float)graphicsDevice.PresentationParameters.BackBufferHeight / Viewer.RenderProcess.GraphicsDeviceManager.PreferredBackBufferHeight;
            Vector3 screenScaling = new Vector3(scaling);
            SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, null, null, Matrix.CreateScale(scaling));
            SpriteBatch.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            textBoxes.Clear();
            base.Render(graphicsDevice, renderItems, matrices);
        }

        public override bool GetBlending()
        {
            return true;
        }

        public Point GetTextLocation(int x, int y, string text)
        {
            // Start with a box in the location specified.
            var textBox = new Rectangle(x, y, Font.MeasureString(text), Font.Height);
            textBox.X -= textBox.Width / 2;
            textBox.Inflate(5, 2);
            // Find all the existing boxes which overlap with the new box, as if its top was extended upwards to infinity.
            var boxes = textBoxes.Where(box => box.Top <= textBox.Bottom && box.Right >= textBox.Left && box.Left <= textBox.Right).OrderBy(box => -box.Top);
            // For each possible colliding box, if it does collide, shift the new box above it.
            foreach (var box in boxes)
                if (box.Top <= textBox.Bottom && box.Bottom >= textBox.Top)
                    textBox.Y = box.Top - textBox.Height;
            // And we're done.
            textBoxes.Add(textBox);
            return new Point(textBox.X + 5, textBox.Y + 2);
        }
    }

    public class DebugNormalMaterial : Material
    {
        private readonly EffectPassCollection shaderPasses;
        private readonly DebugShader shader;

        public DebugNormalMaterial(Viewer viewer)
            : base(viewer, null)
        {
            shader = viewer.MaterialManager.DebugShader;
            shaderPasses = shader.Techniques[0].Passes;
        }

        public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[0]; //["Normal"];
        }

        public override void Render(GraphicsDevice graphicsDevice, List<RenderItem> renderItems, Matrix[] matrices)
        {
            for (int j = 0; j < shaderPasses.Count; j++)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    shader.SetMatrix(item.XNAMatrix, ref matrices[(int)ViewMatrixSequence.ViewProjection]);
                    shaderPasses[j].Apply();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
            }
        }
    }
}
