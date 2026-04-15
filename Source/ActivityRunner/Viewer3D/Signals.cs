// COPYRIGHT 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;
using FreeTrainSimulator.Models.Signalling;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.ActivityRunner.Viewer3D.Sound;
using Orts.Simulation;
using Orts.Simulation.Signalling;

namespace Orts.ActivityRunner.Viewer3D
{
    public class SignalShape : PoseableShape
    {
        private readonly bool[] SubObjVisible;
        private readonly List<SignalShapeHead> Heads = new List<SignalShapeHead>();

        public SignalShape(Orts.Formats.Msts.Models.SignalObject mstsSignal, string path, IWorldPosition positionSource, ShapeOptions flags)
            : base(path, positionSource, flags)
        {
            string signalShape = Path.GetFileName(path);
            if (!viewer.Simulator.SignalEnvironment.SignalConfig.SignalShapes.TryGetValue(signalShape, out FreeTrainSimulator.Models.Signalling.SignalShape signalShapeData))
            {
                Trace.TraceWarning("{0} signal {1} has invalid shape {2}.", WorldPosition.ToString(), mstsSignal.UiD, signalShape);
                return;
            }

            // The matrix names are used as the sub-object names. The sub-object visibility comes from
            // mstsSignal.SignalSubObj, which is mapped to names through signalShapeData.SubObjects.
            bool[] visibleMatrixNames = new bool[SharedShape.MatrixNames.Count];
            for (int i = 0; i < signalShapeData.SubObjects.Length; i++)
                if ((((mstsSignal.SignalSubObject >> i) & 0x1) == 1) && (SharedShape.MatrixNames.Contains(signalShapeData.SubObjects[i].MatrixName)))
                    visibleMatrixNames[SharedShape.MatrixNames.IndexOf(signalShapeData.SubObjects[i].MatrixName)] = true;

            // All sub-objects except the one pointing to the first matrix (99.00% times it is the first one, but not always, see Protrain) are hidden by default.
            //For each other sub-object, look up its name in the hierarchy and use the visibility of that matrix. 
            visibleMatrixNames[0] = true;
            SubObjVisible = new bool[SharedShape.LodControls[0].DistanceLevels[0].SubObjects.Length];
            SubObjVisible[0] = true;
            for (int i = 1; i < SharedShape.LodControls[0].DistanceLevels[0].SubObjects.Length; i++)
            {
                if (i == SharedShape.RootSubObjectIndex)
                    SubObjVisible[i] = true;
                else
                {
                    SharedShape.SubObject subObj = SharedShape.LodControls[0].DistanceLevels[0].SubObjects[i];
                    int minHiLevIndex = 0;
                    if (subObj.ShapePrimitives[0].Hierarchy[subObj.ShapePrimitives[0].HierarchyIndex] > 0)
                    // Search for ShapePrimitive with lowest Hierarchy Value and check visibility with it
                    {
                        int minHiLev = 999;
                        for (int j = 0; j < subObj.ShapePrimitives.Length; j++)
                        {
                            if (subObj.ShapePrimitives[0].Hierarchy[subObj.ShapePrimitives[j].HierarchyIndex] < minHiLev)
                            {
                                minHiLevIndex = j;
                                minHiLev = subObj.ShapePrimitives[0].Hierarchy[subObj.ShapePrimitives[j].HierarchyIndex];
                            }
                        }
                    }
                    SubObjVisible[i] = visibleMatrixNames[SharedShape.LodControls[0].DistanceLevels[0].SubObjects[i].ShapePrimitives[minHiLevIndex].HierarchyIndex];
                }
            }

            if (mstsSignal.SignalUnits == null)
            {
                Trace.TraceWarning("{0} signal {1} has no SignalUnits.", WorldPosition.ToString(), mstsSignal.UiD);
                return;
            }

            for (int i = 0; i < mstsSignal.SignalUnits.Count; i++)
            {
                // Find the simulation SignalObject for this shape.
                KeyValuePair<Signal, SignalHead>? signalAndHead = viewer.Simulator.SignalEnvironment.FindByTrackItem(mstsSignal.SignalUnits[i].TrackItem);
                if (!signalAndHead.HasValue)
                {
                    Trace.TraceWarning("Skipped {0} signal {1} unit {2} with invalid TrItem {3}", WorldPosition.ToString(), mstsSignal.UiD, i, mstsSignal.SignalUnits[i].TrackItem);
                    continue;
                }
                // Get the signal sub-object for this unit (head).
                SignalSubObject signalSubObj = signalShapeData.SubObjects[mstsSignal.SignalUnits[i].SubObject];
                if (signalSubObj.SignalSubType != SignalSubObjectType.SignalHead) // SIGNAL_HEAD
                {
                    Trace.TraceWarning("Skipped {0} signal {1} unit {2} with invalid SubObj {3}", WorldPosition.ToString(), mstsSignal.UiD, i, mstsSignal.SignalUnits[i].SubObject);
                    continue;
                }
                try
                {
                    // Go create the shape head.
                    Heads.Add(new SignalShapeHead(viewer, this, i, signalAndHead.Value.Value, signalSubObj));
                }
                catch (InvalidDataException error)
                {
                    Trace.TraceWarning(error.Message);
                }
            }
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            // Locate relative to the camera
            Matrix xnaTileTranslation = Matrix.CreateTranslation((WorldPosition.Tile - viewer.Camera.Tile).TileVector().XnaVector());  // object is offset from camera this many tiles
            MatrixExtension.Multiply(in WorldPosition.XNAMatrix, in xnaTileTranslation, out Matrix xnaTileTranslationResult);

            foreach (var head in Heads)
                head.PrepareFrame(frame, elapsedTime, xnaTileTranslationResult);

            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, SubObjVisible, Flags);
        }

        public override void Unload()
        {
            foreach (SignalShapeHead head in Heads)
                head.Unload();
            base.Unload();
        }

        internal override void Mark()
        {
            foreach (SignalShapeHead head in Heads)
                head.Mark();
            base.Mark();
        }

        private sealed class SignalShapeHead
        {
            private readonly Viewer Viewer;
            private readonly SignalShape SignalShape;
            private readonly SignalHead SignalHead;
            private readonly List<int> MatrixIndices = new List<int>();
            private readonly SignalTypeData SignalTypeData;
            private readonly SoundSource Sound;
            private float CumulativeTime;
            private float SemaphorePos;
            private float SemaphoreTarget;
            private float SemaphoreSpeed;
            private List<AnimatedPart> SemaphoreParts = new List<AnimatedPart>();
            private int DisplayState = -1;

            private readonly SignalLightState[] lightStates;

            public SignalShapeHead(Viewer viewer, SignalShape signalShape, int index, SignalHead signalHead,
                        SignalSubObject signalSubObj)
            {
                Viewer = viewer;
                SignalShape = signalShape;
                SignalHead = signalHead;
                for (int mindex = 0; mindex <= signalShape.SharedShape.MatrixNames.Count - 1; mindex++)
                {
                    string MatrixName = signalShape.SharedShape.MatrixNames[mindex];
                    if (string.Equals(MatrixName, signalSubObj.MatrixName, StringComparison.OrdinalIgnoreCase))
                        MatrixIndices.Add(mindex);
                }


                if (!Simulator.Instance.SignalEnvironment.SignalConfig.SignalTypes.TryGetValue(signalSubObj.SignalSubSignalType, out SignalType signalType))
                    return;

                SignalTypeData = viewer.SignalTypeDataManager.Get(signalType);

                if (SignalTypeData.Semaphore)
                {
                    // Check whether we have to correct the Semaphore position indexes following the strange rule of MSTS
                    // Such strange rule is that, if there are only two animation steps in the related .s file, MSTS behaves as follows:
                    // a SemaphorePos (2) in sigcfg.dat is executed as SemaphorePos (1)
                    // a SemaphorePos (1) in sigcfg.dat is executed as SemaphorePos (0)
                    // a SemaphorePos (0) in sigcfg.dat is executed as SemaphorePos (0)
                    // First we check if there are only two animation steps
                    if (signalShape.SharedShape.Animations != null && signalShape.SharedShape.Animations.Count != 0 && MatrixIndices.Count > 0 &&
                            signalShape.SharedShape.Animations[0].AnimationNodes[MatrixIndices[0]].Controllers.Count != 0 &&
                            signalShape.SharedShape.Animations[0].AnimationNodes[MatrixIndices[0]].Controllers[0].Count == 2)
                    {

                        // OK, now we check if maximum SemaphorePos is 2 (we won't correct if there are only SemaphorePos 1 and 0,
                        // because they would both be executed as SemaphorePos (0) accordingly to above law, therefore leading to a static semaphore)
                        float maxIndex = float.MinValue;
                        foreach (SignalAspectData drAsp in SignalTypeData.DrawAspects.Values)
                        {
                            if (drAsp.SemaphorePos > maxIndex)
                                maxIndex = drAsp.SemaphorePos;
                        }
                        if (maxIndex == 2)
                        {
                            // in this case we modify the SemaphorePositions for compatibility with MSTS.
                            foreach (SignalAspectData drAsp in SignalTypeData.DrawAspects.Values)
                            {
                                switch ((int)drAsp.SemaphorePos)
                                {
                                    case 2:
                                        drAsp.SemaphorePos = 1;
                                        break;
                                    case 1:
                                        drAsp.SemaphorePos = 0;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            if (!SignalTypeData.AreSemaphoresReindexed)
                            {
                                Trace.TraceInformation("Reindexing semaphore entries of signal type {0} for compatibility with MSTS", signalType.Name);
                                SignalTypeData.AreSemaphoresReindexed = true;
                            }
                        }
                    }

                    foreach (int mindex in MatrixIndices)
                    {
                        if (mindex == 0 && (signalShape.SharedShape.Animations == null || signalShape.SharedShape.Animations.Count == 0 ||
                            signalShape.SharedShape.Animations[0].AnimationNodes[mindex].Controllers.Count == 0))
                            continue;
                        AnimatedPart SemaphorePart = new AnimatedPart(signalShape);
                        SemaphorePart.AddMatrix(mindex);
                        SemaphoreParts.Add(SemaphorePart);
                    }

                    if (Simulator.Instance.RouteModel.RouteSounds[DefaultSoundType.Signal] != null)
                    {
                        string soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.RouteModel.RouteSounds[DefaultSoundType.Signal]);
                        try
                        {
                            Sound = new SoundSource(SignalShape.WorldPosition.WorldLocation, SoundEventSource.Signal, soundPath);
                            Viewer.SoundProcess.AddSoundSource(this, Sound);
                        }
                        catch (Exception error)
                        {
                            Trace.WriteLine(new FileLoadException(soundPath, error));
                        }
                    }
                }

                lightStates = new SignalLightState[SignalTypeData.Lights.Count];
                for (var i = 0; i < SignalTypeData.Lights.Count; i++)
                    lightStates[i] = new SignalLightState(SignalTypeData.TransitionTime);
            }

            public void Unload()
            {
                if (Sound != null)
                {
                    Viewer.SoundProcess.RemoveSoundSources(this);
                    Sound.Dispose();
                }
            }

            public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime, Matrix xnaTileTranslation)
            {
                var initialise = DisplayState == -1;
                if (DisplayState != SignalHead.DrawState)
                {
                    DisplayState = SignalHead.DrawState;
                    if (SignalTypeData.DrawAspects.TryGetValue(DisplayState, out SignalAspectData value))
                    {
                        SemaphoreTarget = value.SemaphorePos;
                        SemaphoreSpeed = SignalTypeData.SemaphoreAnimationTime <= 0 ? 0 : (SemaphoreTarget > SemaphorePos ? +1 : -1) / SignalTypeData.SemaphoreAnimationTime;
                        if (Sound != null)
                            Sound.HandleEvent(TrainEvent.SemaphoreArm);
                    }
                }

                CumulativeTime += (float)elapsedTime.ClockSeconds;
                while (CumulativeTime > SignalTypeData.FlashTimeTotal)
                    CumulativeTime -= SignalTypeData.FlashTimeTotal;

                if (DisplayState < 0 || !SignalTypeData.DrawAspects.TryGetValue(DisplayState, out SignalAspectData signalAspectData))
                    return;

                if (SignalTypeData.Semaphore)
                {
                    // We reset the animation matrix before preparing the lights, because they need to be positioned
                    // based on the original matrix only.
                    foreach (AnimatedPart SemaphorePart in SemaphoreParts)
                    {
                        SemaphorePart.SetFrameWrap(0);
                    }
                }

                for (var i = 0; i < SignalTypeData.Lights.Count; i++)
                {
                    SignalLightState state = lightStates[i];
                    bool semaphoreDark = SemaphorePos != SemaphoreTarget && SignalTypeData.LightsSemaphoreChange[i];
                    bool constantDark = !signalAspectData.DrawLights[i];
                    bool flashingDark = signalAspectData.FlashLights[i] && (CumulativeTime > SignalTypeData.FlashTimeOn);
                    state.UpdateIntensity(semaphoreDark || constantDark || flashingDark ? 0 : 1, elapsedTime);
                    if (!state.Illuminated)
                        continue;

                    bool isDay;
                    if (Viewer.UserSettings.MstsEnvironment == false)
                        isDay = Viewer.World.Sky.SolarDirection.Y > 0;
                    else
                        isDay = Viewer.World.MSTSSky.mstsskysolarDirection.Y > 0;
                    bool isPoorVisibility = Viewer.Simulator.Weather.FogVisibilityDistance < 200;
                    if (!SignalTypeData.DayLight && isDay && !isPoorVisibility)
                        continue;

                    var translationMatrix = Matrix.CreateTranslation(SignalTypeData.Lights[i].Position);
                    Matrix temp = default;
                    foreach (int MatrixIndex in MatrixIndices)
                    {
                        MatrixExtension.Multiply(in translationMatrix, in SignalShape.XNAMatrices[MatrixIndex], out temp);
                    }
                    MatrixExtension.Multiply(in temp, in xnaTileTranslation, out Matrix xnaMatrix);

                    frame.AddPrimitive(SignalTypeData.Material, SignalTypeData.Lights[i], RenderPrimitiveGroup.Lights, ref xnaMatrix, ShapeOptions.None, state);
                    if (Viewer.UserSettings.SignalLightGlow)
                        frame.AddPrimitive(SignalTypeData.GlowMaterial, SignalTypeData.Lights[i], RenderPrimitiveGroup.Lights, ref xnaMatrix, ShapeOptions.None, state);
                }

                if (SignalTypeData.Semaphore)
                {
                    // Now we update and re-animate the semaphore arm.
                    if (SignalTypeData.SemaphoreAnimationTime <= 0 || initialise)
                    {
                        // No timing (so instant switch) or we're initialising.
                        SemaphorePos = SemaphoreTarget;
                        SemaphoreSpeed = 0;
                    }
                    else
                    {
                        // Animate slowly to target position.
                        SemaphorePos += SemaphoreSpeed * (float)elapsedTime.ClockSeconds;
                        if (SemaphorePos * Math.Sign(SemaphoreSpeed) > SemaphoreTarget * Math.Sign(SemaphoreSpeed))
                        {
                            SemaphorePos = SemaphoreTarget;
                            SemaphoreSpeed = 0;
                        }
                    }
                    foreach (AnimatedPart SemaphorePart in SemaphoreParts)
                    {
                        SemaphorePart.SetFrameCycle(SemaphorePos);
                    }
                }
            }

            internal void Mark()
            {
                SignalTypeData.Material.Mark();
                SignalTypeData.GlowMaterial?.Mark();
            }
        }

    }
    public class SignalTypeDataManager
    {
        private readonly Viewer Viewer;
        private readonly Dictionary<string, SignalTypeData> SignalTypes = new Dictionary<string, SignalTypeData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> SignalTypesMarks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public SignalTypeDataManager(Viewer viewer)
        {
            Viewer = viewer;
        }

        public SignalTypeData Get(SignalType signalType)
        {
            if (!SignalTypes.TryGetValue(signalType.Name, out SignalTypeData value))
            {
                value = new SignalTypeData(Viewer, signalType);
                SignalTypes[signalType.Name] = value;
            }

            return value;
        }

        public void Mark()
        {
            SignalTypesMarks.Clear();
            foreach (string signalTypeName in SignalTypes.Keys)
            {
                SignalTypesMarks.Add(signalTypeName, false);
            }
        }

        public void Mark(SignalTypeData signalType)
        {
            if (SignalTypes.ContainsValue(signalType))
            {
                SignalTypesMarks[SignalTypes.First(x => x.Value == signalType).Key] = true;
            }
        }

        public void Sweep()
        {
            foreach (string signalTypeName in SignalTypesMarks.Where(x => !x.Value).Select(x => x.Key))
            {
                SignalTypes.Remove(signalTypeName);
            }
        }
    }

    public class SignalTypeData
    {
        public readonly Material Material;
        public readonly Material GlowMaterial;
        public readonly List<SignalLightPrimitive> Lights = new List<SignalLightPrimitive>();
        public readonly List<bool> LightsSemaphoreChange = new List<bool>();
        public readonly Dictionary<int, SignalAspectData> DrawAspects = new Dictionary<int, SignalAspectData>();
        public readonly float FlashTimeOn;
        public readonly float FlashTimeTotal;
        public readonly float TransitionTime;
        public readonly bool Semaphore;
        public readonly bool DayLight = true;
        public readonly float SemaphoreAnimationTime;
        public bool AreSemaphoresReindexed;

        private readonly Viewer viewer;

        public SignalTypeData(Viewer viewer, SignalType signalType)
        {
            viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            if (!viewer.Simulator.SignalEnvironment.SignalConfig.LightTextures.TryGetValue(signalType.LightTexture, out SignalLightTexture lightTexture))
            {
                Trace.TraceWarning("Skipped invalid light texture {1} for signal type {0}", signalType.Name, signalType.LightTexture);
                Material = viewer.MaterialManager.Load("missing-signal-light");
                FlashTimeOn = 1;
                FlashTimeTotal = 2;
            }
            else
            {
                Material = viewer.MaterialManager.Load("SignalLight", Helpers.GetRouteTextureFile(Helpers.TextureFlags.None, lightTexture.TextureFile));
                GlowMaterial = viewer.MaterialManager.Load("SignalLightGlow");
                if (signalType.Lights.Length > 0)
                {
                    // Set up some heuristic glow values from the available data:
                    //   Typical electric light is 3.0/5.0
                    //   Semaphore is 0.0/5.0
                    //   Theatre box is 0.0/0.0
                    float glowDay = 3.0f;
                    float glowNight = 5.0f;

                    if (signalType.SignalFlags.HasFlag(SignalOptions.Semaphore))
                        glowDay = 0.0f;
                    if (signalType.FunctionType == SignalFunctionType.Info || signalType.FunctionType == SignalFunctionType.Shunting) // These are good at identifying theatre boxes.
                        glowDay = glowNight = 0.0f;

                    // use values from signal if defined
                    if (signalType.DayGlow.HasValue)
                    {
                        glowDay = signalType.DayGlow.Value;
                    }
                    if (signalType.NightGlow.HasValue)
                    {
                        glowNight = signalType.NightGlow.Value;
                    }

                    Matrix2x2 textureCoordinates = new Matrix2x2(lightTexture.U0, lightTexture.V0, lightTexture.U1, lightTexture.V1);
                    foreach (SignalLight signalLight in signalType.Lights)
                    {
                        Lights.Add(new SignalLightPrimitive(viewer, signalLight.Position, signalLight.Radius, signalLight.Color, glowDay, glowNight, textureCoordinates));
                        LightsSemaphoreChange.Add(signalLight.SemaphoreChange);
                    }
                }

                foreach (KeyValuePair<string, SignalDrawState> drawState in signalType.DrawStates)
                    DrawAspects.Add(drawState.Value.Index, new SignalAspectData(signalType, drawState.Value));
                FlashTimeOn = signalType.FlashTimeOn;
                FlashTimeTotal = signalType.FlashTimeOn + signalType.FlashTimeOff;
                Semaphore = signalType.SignalFlags.HasFlag(SignalOptions.Semaphore);
                SemaphoreAnimationTime = signalType.SemaphoreAnimationnDuration;
                DayLight = signalType.DayLight;
            }

            TransitionTime = signalType.TransitionTime;
        }

        public void Mark()
        {
            viewer.SignalTypeDataManager.Mark(this);
            Material.Mark();
            GlowMaterial?.Mark();
        }
    }

    public enum SignalTypeDataType
    {
        Normal,
        Distance,
        Repeater,
        Shunting,
        Info,
    }

    public class SignalAspectData
    {
        public readonly bool[] DrawLights;
        public readonly bool[] FlashLights;
        public float SemaphorePos;

        public SignalAspectData(SignalType signalType, SignalDrawState drawStateData)
        {
            if (signalType.Lights.Length > 0)
            {
                DrawLights = new bool[signalType.Lights.Length];
                FlashLights = new bool[signalType.Lights.Length];
            }
            else
            {
                DrawLights = null;
                FlashLights = null;
            }

            for (int i = 0; i < drawStateData.DrawStateLights.Length; i++)
            {
                if (drawStateData.DrawStateLights[i] == SignalDrawStateLightMode.Unlit)
                    continue;
                if (DrawLights == null || i >= DrawLights.Length)
                    Trace.TraceWarning("Skipped extra draw light {0}", i);
                else
                {
                    DrawLights[i] = true;
                    FlashLights[i] = drawStateData.DrawStateLights[i] == SignalDrawStateLightMode.Flashing;
                }
            }
            SemaphorePos = drawStateData.SemaphorePosition;
        }
    }

    /// <summary>
    /// Tracks state for individual signal head lamps, with smooth lit/unlit transitions.
    /// </summary>
    internal sealed class SignalLightState
    {
        private readonly float transitionTime; // Transition time in seconds.
        private double intensity;
        private bool firstUpdate = true;

        public SignalLightState(float transitionTime)
        {
            this.transitionTime = transitionTime;
        }

        public double Intensity => intensity;
        public bool Illuminated => intensity > 0;

        public void UpdateIntensity(float target, in ElapsedTime elapsedTime)
        {
            if (firstUpdate || transitionTime == 0)
                intensity = target;
            else if (target > intensity)
                intensity = Math.Min(intensity + elapsedTime.ClockSeconds / transitionTime, target);
            else if (target < intensity)
                intensity = Math.Max(intensity - elapsedTime.ClockSeconds / transitionTime, target);
            firstUpdate = false;
        }
    }

    public class SignalLightPrimitive : RenderPrimitive
    {
        internal readonly Vector3 Position;
        internal readonly float GlowIntensityDay;
        internal readonly float GlowIntensityNight;
        private readonly VertexBuffer VertexBuffer;

        public SignalLightPrimitive(Viewer viewer, in Vector3 position, float radius, Color color, float glowDay, float glowNight, in Matrix2x2 textureCoordinates)
        {
            Position = position;
            Position.X *= -1;
            GlowIntensityDay = glowDay;
            GlowIntensityNight = glowNight;

            VertexPositionColorTexture[] verticies = new[] {
                new VertexPositionColorTexture(new Vector3(-radius, +radius, 0), color, new Vector2(textureCoordinates.M10, textureCoordinates.M01)),
                new VertexPositionColorTexture(new Vector3(+radius, +radius, 0), color, new Vector2(textureCoordinates.M00, textureCoordinates.M01)),
                new VertexPositionColorTexture(new Vector3(-radius, -radius, 0), color, new Vector2(textureCoordinates.M10, textureCoordinates.M11)),
                new VertexPositionColorTexture(new Vector3(+radius, -radius, 0), color, new Vector2(textureCoordinates.M00, textureCoordinates.M11)),
            };

            VertexBuffer = new VertexBuffer(viewer.Game.GraphicsDevice, typeof(VertexPositionColorTexture), verticies.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(verticies);
        }

        public override void Draw()
        {
            graphicsDevice.SetVertexBuffer(VertexBuffer);
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
        }
    }

    public class SignalLightMaterial : Material
    {
        private readonly SceneryShader shader;
        private readonly Texture2D texture;
        private readonly int techniqueIndex;

        public SignalLightMaterial(Viewer viewer, string textureName)
            : base(viewer, textureName)
        {
            shader = base.viewer.MaterialManager.SceneryShader;
            texture = base.viewer.TextureManager.Get(textureName, true);

            for (int i = 0; i < shader.Techniques.Count; i++)
            {
                if (shader.Techniques[i].Name == "SignalLight")
                {
                    techniqueIndex = i;
                    break;
                }
            }
        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = viewer.MaterialManager.SceneryShader.Techniques[techniqueIndex]; //["SignalLight"];
            shader.ImageTexture = texture;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            foreach (EffectPass pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    shader.SignalLightIntensity = (float)(item.ItemData as SignalLightState).Intensity;
                    shader.SetMatrix(in item.XNAMatrix, in viewProjection);
                    pass.Apply();
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
            viewer.TextureManager.Mark(texture);
            base.Mark();
        }
    }

    public class SignalLightGlowMaterial : Material
    {
        private readonly SceneryShader shader;
        private readonly Texture2D texture;
        private readonly int techniqueIndex;
        private float nightEffect;

        public SignalLightGlowMaterial(Viewer viewer)
            : base(viewer, null)
        {
            shader = base.viewer.MaterialManager.SceneryShader;
            texture = SharedTextureManager.Get(base.viewer.Game.GraphicsDevice, Path.Combine(base.viewer.ContentPath, "SignalLightGlow.png"));
            for (int i = 0; i < shader.Techniques.Count; i++)
            {
                if (shader.Techniques[i].Name == "SignalLightGlow")
                {
                    techniqueIndex = i;
                    break;
                }
            }

        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = viewer.MaterialManager.SceneryShader.Techniques[techniqueIndex];
            shader.ImageTexture = texture;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;

            // The following constants define the beginning and the end conditions of
            // the day-night transition. Values refer to the Y postion of LightVector.
            const float startNightTrans = 0.1f;
            const float finishNightTrans = -0.1f;

            var sunDirection = viewer.UserSettings.MstsEnvironment ? viewer.World.MSTSSky.mstsskysolarDirection : viewer.World.Sky.SolarDirection;
            nightEffect = 1 - MathHelper.Clamp((sunDirection.Y - finishNightTrans) / (startNightTrans - finishNightTrans), 0, 1);
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            foreach (EffectPass pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    var slp = item.RenderPrimitive as SignalLightPrimitive;
                    shader.ZBias = MathHelper.Lerp(slp.GlowIntensityDay, slp.GlowIntensityNight, nightEffect);
                    shader.SignalLightIntensity = (float)(item.ItemData as SignalLightState).Intensity;
                    shader.SetMatrix(in item.XNAMatrix, in viewProjection);
                    pass.Apply();
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
            viewer.TextureManager.Mark(texture);
            base.Mark();
        }
    }
}