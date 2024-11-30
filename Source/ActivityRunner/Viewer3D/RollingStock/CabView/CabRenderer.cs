// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Imported.State;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems;
using Orts.ActivityRunner.Viewer3D.RollingStock.SubSystems.Etcs;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    public class CabRenderer : RenderPrimitive, ISaveStateApi<CabRendererSaveState>
    {
        private readonly CabSpriteBatchMaterial spriteShader2DCabView;
        private Matrix scale = Matrix.Identity;
        private Texture2D cabTexture;
        private readonly Texture2D letterboxTexture;
        private readonly CabShader shader;  // Shaders must have unique Keys - below

        private Point previousScreenSize;

        private readonly EnumArray<List<CabViewControlRenderer>, CabViewType> cabViewControlRenderer = new EnumArray<List<CabViewControlRenderer>, CabViewType>();

        private readonly Viewer viewer;
        private readonly MSTSLocomotive locomotive;
        private int location;
        private bool nightTexture;
        private readonly bool cabLightDirectory;

        public Dictionary<(ControlType, int), CabViewControlRenderer> ControlMap { get; }
        public string[] ActiveScreen { get; private set; } = { "default", "default", "default", "default", "default", "default", "default", "default" };

        public CabRenderer(Viewer viewer, MSTSLocomotive car)
        {
            //Sequence = RenderPrimitiveSequence.CabView;
            this.viewer = viewer;
            locomotive = car;
            // _Viewer.DisplaySize intercepted to adjust cab view height
            Point DisplaySize = this.viewer.DisplaySize;
            DisplaySize.Y = this.viewer.CabHeightPixels;

            previousScreenSize = DisplaySize;

            letterboxTexture = new Texture2D(viewer.Game.GraphicsDevice, 1, 1);
            letterboxTexture.SetData(new Color[] { Color.Black });

            // Use same shader for both front-facing and rear-facing cabs.
            if (locomotive.CabViews[CabViewType.Front].ExtendedCVF != null)
            {
                shader = new CabShader(viewer.Game.GraphicsDevice,
                    ExtendedCVF.TranslatedPosition(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light1Position, DisplaySize),
                    ExtendedCVF.TranslatedPosition(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light2Position, DisplaySize),
                    ExtendedCVF.TranslatedColor(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light1Color),
                    ExtendedCVF.TranslatedColor(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light2Color));
            }

            spriteShader2DCabView = (CabSpriteBatchMaterial)viewer.MaterialManager.Load("CabSpriteBatch", null, 0, 0, shader);

            #region Create Control renderers
            ControlMap = new Dictionary<(ControlType, int), CabViewControlRenderer>();
            Dictionary<ControlType, int> count = new Dictionary<ControlType, int>();
            bool firstOne = true;
            foreach (Simulation.RollingStocks.CabView cabView in car.CabViews)
            {
                if (cabView?.CVFFile != null)
                {
                    // Loading ACE files, skip displaying ERROR messages
                    foreach (var cabfile in cabView.CVFFile.Views2D)
                    {
                        cabLightDirectory = CabTextureManager.LoadTextures(viewer, cabfile);
                    }

                    if (firstOne)
                    {
                        this.viewer.AdjustCabHeight(this.viewer.DisplaySize.X, this.viewer.DisplaySize.Y);

                        this.viewer.CabCamera.ScreenChanged();
                        DisplaySize.Y = this.viewer.CabHeightPixels;
                        // Use same shader for both front-facing and rear-facing cabs.
                        if (locomotive.CabViews[CabViewType.Front].ExtendedCVF != null)
                        {
                            shader = new CabShader(viewer.Game.GraphicsDevice,
                            ExtendedCVF.TranslatedPosition(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light1Position, DisplaySize),
                            ExtendedCVF.TranslatedPosition(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light2Position, DisplaySize),
                            ExtendedCVF.TranslatedColor(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light1Color),
                            ExtendedCVF.TranslatedColor(locomotive.CabViews[CabViewType.Front].ExtendedCVF.Light2Color));
                        }
                        spriteShader2DCabView = (CabSpriteBatchMaterial)viewer.MaterialManager.Load("CabSpriteBatch", null, 0, 0, shader);
                        firstOne = false;
                    }

                    if (cabView.CVFFile.CabViewControls == null)
                        continue;

                    var controlSortIndex = 1;  // Controls are drawn atop the cabview and in order they appear in the CVF file.
                                               // This allows the segments of moving-scale meters to be hidden by covers (e.g. TGV-A)
                    CabViewType cabViewType = cabView.CabViewType;
                    cabViewControlRenderer[cabViewType] = new List<CabViewControlRenderer>();
                    foreach (CabViewControl cvc in cabView.CVFFile.CabViewControls)
                    {
                        controlSortIndex++;
                        if (!count.ContainsKey(cvc.ControlType))
                            count[cvc.ControlType] = 0;
                        (ControlType ControlType, int) key = (cvc.ControlType, count[cvc.ControlType]);
                        CabViewDialControl dial = cvc as CabViewDialControl;
                        if (dial != null)
                        {
                            CabViewDialRenderer cvcr = new CabViewDialRenderer(viewer, car, dial, shader);
                            cvcr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(cvcr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, cvcr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewFireboxControl firebox = cvc as CabViewFireboxControl;
                        if (firebox != null)
                        {
                            CabViewGaugeRenderer cvgrFire = new CabViewGaugeRenderer(viewer, car, firebox, shader);
                            cvgrFire.SortIndex = controlSortIndex++;
                            cabViewControlRenderer[cabViewType].Add(cvgrFire);
                            // don't "continue", because this cvc has to be also recognized as CVCGauge
                        }
                        CabViewGaugeControl gauge = cvc as CabViewGaugeControl;
                        if (gauge != null)
                        {
                            CabViewGaugeRenderer cvgr = new CabViewGaugeRenderer(viewer, car, gauge, shader);
                            cvgr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(cvgr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, cvgr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewSignalControl asp = cvc as CabViewSignalControl;
                        if (asp != null)
                        {
                            CabViewDiscreteRenderer aspr = new CabViewDiscreteRenderer(viewer, car, asp, shader);
                            aspr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(aspr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, aspr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewAnimatedDisplayControl anim = cvc as CabViewAnimatedDisplayControl;
                        if (anim != null)
                        {
                            CabViewAnimationsRenderer animr = new CabViewAnimationsRenderer(viewer, car, anim, shader);
                            animr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(animr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, animr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewMultiStateDisplayControl multi = cvc as CabViewMultiStateDisplayControl;
                        if (multi != null)
                        {
                            CabViewDiscreteRenderer mspr = new CabViewDiscreteRenderer(viewer, car, multi, shader);
                            mspr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(mspr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, mspr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewDiscreteControl disc = cvc as CabViewDiscreteControl;
                        if (disc != null)
                        {
                            CabViewDiscreteRenderer cvdr = new CabViewDiscreteRenderer(viewer, car, disc, shader);
                            cvdr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(cvdr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, cvdr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewDigitalControl digital = cvc as CabViewDigitalControl;
                        if (digital != null)
                        {
                            CabViewDigitalRenderer cvdr;
                            if (digital.ControlStyle == CabViewControlStyle.Needle)
                                cvdr = new CircularSpeedGaugeRenderer(viewer, car, digital, shader);
                            else
                                cvdr = new CabViewDigitalRenderer(viewer, car, digital, shader);
                            cvdr.SortIndex = controlSortIndex;
                            cabViewControlRenderer[cabViewType].Add(cvdr);
                            if (!ControlMap.ContainsKey(key))
                                ControlMap.Add(key, cvdr);
                            count[cvc.ControlType]++;
                            continue;
                        }
                        CabViewScreenControl screen = cvc as CabViewScreenControl;
                        if (screen != null)
                        {
                            if (screen.ControlType.CabViewControlType == CabViewControlType.Orts_Etcs)
                            {
                                var cvr = new DriverMachineInterfaceRenderer(viewer, car, screen, shader);
                                cvr.SortIndex = controlSortIndex;
                                cabViewControlRenderer[cabViewType].Add(cvr);
                                if (!ControlMap.ContainsKey(key))
                                    ControlMap.Add(key, cvr);
                                count[cvc.ControlType]++;
                                continue;
                            }
                            else if (screen.ControlType.CabViewControlType == CabViewControlType.Orts_DistributedPower)
                            {
                                var cvr = new DistributedPowerInterfaceRenderer(viewer, car, screen, shader);
                                cvr.SortIndex = controlSortIndex;
                                cabViewControlRenderer[cabViewType].Add(cvr);
                                if (!ControlMap.ContainsKey(key))
                                    ControlMap.Add(key, cvr);
                                count[cvc.ControlType]++;
                                continue;
                            }
                        }
                    }
                }
            }
            #endregion

        }

        public CabRenderer(Viewer viewer, MSTSLocomotive car, CabViewFile CVFFile) //used by 3D cab as a reference, thus many can be eliminated
        {
            this.viewer = viewer;
            locomotive = car;


            #region Create Control renderers
            ControlMap = new Dictionary<(ControlType, int), CabViewControlRenderer>();
            Dictionary<ControlType, int> count = new Dictionary<ControlType, int>();

            var controlSortIndex = 1;  // Controls are drawn atop the cabview and in order they appear in the CVF file.
                                       // This allows the segments of moving-scale meters to be hidden by covers (e.g. TGV-A)
            CabViewType cabViewType = CabViewType.Rear;
            cabViewControlRenderer[cabViewType] = new List<CabViewControlRenderer>();
            foreach (CabViewControl cvc in CVFFile.CabViewControls)
            {
                controlSortIndex++;
                if (!count.ContainsKey(cvc.ControlType))
                    count[cvc.ControlType] = 0;
                (ControlType ControlType, int) key = (cvc.ControlType, count[cvc.ControlType]);
                CabViewDialControl dial = cvc as CabViewDialControl;
                if (dial != null)
                {
                    CabViewDialRenderer cvcr = new CabViewDialRenderer(viewer, car, dial, shader);
                    cvcr.SortIndex = controlSortIndex;
                    cabViewControlRenderer[cabViewType].Add(cvcr);
                    if (!ControlMap.ContainsKey(key))
                        ControlMap.Add(key, cvcr);
                    count[cvc.ControlType]++;
                    continue;
                }
                CabViewFireboxControl firebox = cvc as CabViewFireboxControl;
                if (firebox != null)
                {
                    CabViewGaugeRenderer cvgrFire = new CabViewGaugeRenderer(viewer, car, firebox, shader);
                    cvgrFire.SortIndex = controlSortIndex++;
                    cabViewControlRenderer[cabViewType].Add(cvgrFire);
                    // don't "continue", because this cvc has to be also recognized as CVCGauge
                }
                CabViewGaugeControl gauge = cvc as CabViewGaugeControl;
                if (gauge != null)
                {
                    CabViewGaugeRenderer cvgr = new CabViewGaugeRenderer(viewer, car, gauge, shader);
                    cvgr.SortIndex = controlSortIndex;
                    cabViewControlRenderer[cabViewType].Add(cvgr);
                    if (!ControlMap.ContainsKey(key))
                        ControlMap.Add(key, cvgr);
                    count[cvc.ControlType]++;
                    continue;
                }
                CabViewSignalControl asp = cvc as CabViewSignalControl;
                if (asp != null)
                {
                    CabViewDiscreteRenderer aspr = new CabViewDiscreteRenderer(viewer, car, asp, shader);
                    aspr.SortIndex = controlSortIndex;
                    cabViewControlRenderer[cabViewType].Add(aspr);
                    if (!ControlMap.ContainsKey(key))
                        ControlMap.Add(key, aspr);
                    count[cvc.ControlType]++;
                    continue;
                }
                CabViewMultiStateDisplayControl multi = cvc as CabViewMultiStateDisplayControl;
                if (multi != null)
                {
                    CabViewDiscreteRenderer mspr = new CabViewDiscreteRenderer(viewer, car, multi, shader);
                    mspr.SortIndex = controlSortIndex;
                    cabViewControlRenderer[cabViewType].Add(mspr);
                    if (!ControlMap.ContainsKey(key))
                        ControlMap.Add(key, mspr);
                    count[cvc.ControlType]++;
                    continue;
                }
                CabViewDiscreteControl disc = cvc as CabViewDiscreteControl;
                if (disc != null)
                {
                    CabViewDiscreteRenderer cvdr = new CabViewDiscreteRenderer(viewer, car, disc, shader);
                    cvdr.SortIndex = controlSortIndex;
                    cabViewControlRenderer[cabViewType].Add(cvdr);
                    if (!ControlMap.ContainsKey(key))
                        ControlMap.Add(key, cvdr);
                    count[cvc.ControlType]++;
                    continue;
                }
                CabViewDigitalControl digital = cvc as CabViewDigitalControl;
                if (digital != null)
                {
                    CabViewDigitalRenderer cvdr;
                    if (digital.ControlStyle == CabViewControlStyle.Needle)
                        cvdr = new CircularSpeedGaugeRenderer(viewer, car, digital, shader);
                    else
                        cvdr = new CabViewDigitalRenderer(viewer, car, digital, shader);
                    cvdr.SortIndex = controlSortIndex;
                    cabViewControlRenderer[cabViewType].Add(cvdr);
                    if (!ControlMap.ContainsKey(key))
                        ControlMap.Add(key, cvdr);
                    count[cvc.ControlType]++;
                    continue;
                }
                CabViewScreenControl screen = cvc as CabViewScreenControl;
                if (screen != null)
                {
                    if (screen.ControlType.CabViewControlType == CabViewControlType.Orts_Etcs)
                    {
                        var cvr = new DriverMachineInterfaceRenderer(viewer, car, screen, shader);
                        cvr.SortIndex = controlSortIndex;
                        cabViewControlRenderer[cabViewType].Add(cvr);
                        if (!ControlMap.ContainsKey(key))
                            ControlMap.Add(key, cvr);
                        count[cvc.ControlType]++;
                        continue;
                    }
                    else if (screen.ControlType.CabViewControlType == CabViewControlType.Orts_DistributedPower)
                    {
                        var cvr = new DistributedPowerInterfaceRenderer(viewer, car, screen, shader);
                        cvr.SortIndex = controlSortIndex;
                        cabViewControlRenderer[cabViewType].Add(cvr);
                        if (!ControlMap.ContainsKey(key))
                            ControlMap.Add(key, cvr);
                        count[cvc.ControlType]++;
                        continue;
                    }
                }
            }
            #endregion
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (!locomotive.ShowCab)
                return;

            bool Dark = viewer.MaterialManager.sunDirection.Y <= -0.085f || viewer.Camera.IsUnderground;
            bool CabLight = locomotive.CabLightOn;

            location = viewer.Camera is CabCamera cbc ? cbc.SideLocation : 0;

            CabViewType cabViewType = locomotive.UsingRearCab ? CabViewType.Rear : CabViewType.Front;
            cabTexture = CabTextureManager.GetTexture(locomotive.CabViews[cabViewType].CVFFile.Views2D[location], Dark, CabLight, out nightTexture, cabLightDirectory);
            if (cabTexture == SharedMaterialManager.MissingTexture)
                return;

            if (previousScreenSize != viewer.DisplaySize && shader != null)
            {
                previousScreenSize = viewer.DisplaySize;
                shader.SetLightPositions(
                    ExtendedCVF.TranslatedPosition(locomotive.CabViews[cabViewType].ExtendedCVF.Light1Position, viewer.DisplaySize),
                    ExtendedCVF.TranslatedPosition(locomotive.CabViews[cabViewType].ExtendedCVF.Light2Position, viewer.DisplaySize));
            }

            frame.AddPrimitive(spriteShader2DCabView, this, RenderPrimitiveGroup.Cab, ref scale);
            //frame.AddPrimitive(Materials.SpriteBatchMaterial, this, RenderPrimitiveGroup.Cab, ref _Scale);

            foreach (var cvcr in cabViewControlRenderer[cabViewType])
            {
                if (cvcr.control.CabViewpoint == location)
                {
                    if (cvcr.control.Screens != null && cvcr.control.Screens[0] != "all")
                    {
                        foreach (var screen in cvcr.control.Screens)
                        {
                            if (ActiveScreen[cvcr.control.Display] == screen)
                            {
                                cvcr.PrepareFrame(frame, elapsedTime);
                                break;
                            }
                        }
                        continue;
                    }
                    cvcr.PrepareFrame(frame, elapsedTime);
                }
            }
        }

        public override void Draw()
        {
            var cabScale = new Vector2((float)viewer.CabWidthPixels / cabTexture.Width, (float)viewer.CabHeightPixels / cabTexture.Height);
            // Cab view vertical position adjusted to allow for clip or stretch.
            var cabPos = new Vector2(viewer.CabXOffsetPixels / cabScale.X, -viewer.CabYOffsetPixels / cabScale.Y);
            var cabSize = new Vector2((viewer.CabWidthPixels - viewer.CabExceedsDisplayHorizontally) / cabScale.X, (viewer.CabHeightPixels - viewer.CabExceedsDisplay) / cabScale.Y);
            int round(float x)
            {
                return (int)Math.Round(x);
            }
            var cabRect = new Rectangle(round(cabPos.X), round(cabPos.Y), round(cabSize.X), round(cabSize.Y));

            if (shader != null)
            {
                // TODO: Readd ability to control night time lighting.
                float overcast = viewer.Settings.UseMSTSEnv ? viewer.World.MSTSSky.mstsskyovercastFactor : viewer.Simulator.Weather.OvercastFactor;
                shader.SetData(viewer.MaterialManager.sunDirection, nightTexture, false, overcast);
                shader.SetTextureData(cabRect.Left, cabRect.Top, cabRect.Width, cabRect.Height);
            }

            if (cabTexture == null)
                return;

            var drawOrigin = new Vector2(cabTexture.Width / 2, cabTexture.Height / 2);
            var drawPos = new Vector2(viewer.CabWidthPixels / 2, viewer.CabHeightPixels / 2);
            // Cab view position adjusted to allow for letterboxing.
            drawPos.X += viewer.CabXLetterboxPixels;
            drawPos.Y += viewer.CabYLetterboxPixels;

            spriteShader2DCabView.SpriteBatch.Draw(cabTexture, drawPos, cabRect, Color.White, 0f, drawOrigin, cabScale, SpriteEffects.None, 0f);

            // Draw letterboxing.
            void drawLetterbox(int x, int y, int w, int h)
            {
                spriteShader2DCabView.SpriteBatch.Draw(letterboxTexture, new Rectangle(x, y, w, h), Color.White);
            }
            if (viewer.CabXLetterboxPixels > 0)
            {
                drawLetterbox(0, 0, viewer.CabXLetterboxPixels, viewer.DisplaySize.Y);
                drawLetterbox(viewer.CabXLetterboxPixels + viewer.CabWidthPixels, 0, viewer.DisplaySize.X - viewer.CabWidthPixels - viewer.CabXLetterboxPixels, viewer.DisplaySize.Y);
            }
            if (viewer.CabYLetterboxPixels > 0)
            {
                drawLetterbox(0, 0, viewer.DisplaySize.X, viewer.CabYLetterboxPixels);
                drawLetterbox(0, viewer.CabYLetterboxPixels + viewer.CabHeightPixels, viewer.DisplaySize.X, viewer.DisplaySize.Y - viewer.CabHeightPixels - viewer.CabYLetterboxPixels);
            }
        }

        internal void Mark()
        {
            viewer.TextureManager.Mark(cabTexture);

            foreach (var cvcr in cabViewControlRenderer[locomotive.UsingRearCab ? CabViewType.Rear : CabViewType.Front])
                cvcr.Mark();
        }

        public ValueTask<CabRendererSaveState> Snapshot()
        {
            CabRendererSaveState saveState = new CabRendererSaveState();
            foreach (string activeScreen in ActiveScreen)
            {
                saveState.ActiveScreens.Add(activeScreen == null ? "---" : activeScreen);
            }
            return ValueTask.FromResult(saveState);
        }

        public ValueTask Restore(CabRendererSaveState saveState)
        {
            ActiveScreen = saveState.ActiveScreens.ToArray();
            return ValueTask.CompletedTask;
        }
    }
}
