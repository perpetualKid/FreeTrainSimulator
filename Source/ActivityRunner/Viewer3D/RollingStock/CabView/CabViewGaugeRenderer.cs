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

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// Gauge type renderer
    /// Supports pointer, liquid, solid
    /// Supports Orientation and Direction
    /// </summary>
    public class CabViewGaugeRenderer : CabViewControlRenderer
    {
        private readonly CabViewGaugeControl Gauge;
        private readonly Rectangle SourceRectangle;
        private Rectangle DestinationRectangle;

        //      bool LoadMeterPositive = true;
        private Color DrawColor;
        private float DrawRotation;
        private double num;
        private bool fire;

        public CabViewGaugeRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewGaugeControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            Gauge = control;
            if (base.control.ControlType.CabViewControlType == CabViewControlType.Reverser_Plate || Gauge.ControlStyle == CabViewControlStyle.Pointer)
            {
                DrawColor = Color.White;
                texture = CABTextureManager.GetTexture(base.control.AceFile, false, base.locomotive.CabLightOn, out nightTexture, cabLightDirectory);
                SourceRectangle.Width = texture.Width;
                SourceRectangle.Height = texture.Height;
            }
            else
            {
                DrawColor = Gauge.PositiveColors[0];
                SourceRectangle = Gauge.Area;
            }
        }

        public CabViewGaugeRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewFireboxControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            Gauge = control;
            cabLightDirectory = CABTextureManager.LoadTextures(base.viewer, control.FireBoxAceFile);
            texture = CABTextureManager.GetTexture(control.FireBoxAceFile, false, base.locomotive.CabLightOn, out nightTexture, cabLightDirectory);
            DrawColor = Color.White;
            SourceRectangle.Width = texture.Width;
            SourceRectangle.Height = texture.Height;
            fire = true;
        }

        public Color GetColor(out bool positive)
        {
            if (locomotive.GetDataOf(control) < 0)
            {
                positive = false;
                return Gauge.NegativeColors[0];
            }
            else
            {
                positive = true;
                return Gauge.PositiveColors[0];
            }
        }

        public CabViewGaugeControl GetGauge() { return Gauge; }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (Gauge is not CabViewFireboxControl)
            {
                var dark = viewer.MaterialManager.sunDirection.Y <= -0.085f || viewer.Camera.IsUnderground;
                texture = CABTextureManager.GetTexture(control.AceFile, dark, locomotive.CabLightOn, out nightTexture, cabLightDirectory);
            }
            if (texture == SharedMaterialManager.MissingTexture)
                return;

            base.PrepareFrame(frame, elapsedTime);

            // Cab view height adjusted to allow for clip or stretch.
            var xratio = (float)viewer.CabWidthPixels / 640;
            var yratio = (float)viewer.CabHeightPixels / 480;

            float percent, xpos, ypos, zeropos;

            percent = fire ? 1f : GetRangeFraction();
            if (!IsPowered && control.ValueIfDisabled.HasValue)
                num = (float)control.ValueIfDisabled;
            else
                num = locomotive.GetDataOf(control);

            if (Gauge.Orientation == 0)  // gauge horizontal
            {
                ypos = Gauge.Bounds.Height;
                zeropos = (float)(Gauge.Bounds.Width * -control.ScaleRangeMin / (control.ScaleRangeMax - control.ScaleRangeMin));
                xpos = Gauge.Bounds.Width * percent;
            }
            else  // gauge vertical
            {
                xpos = Gauge.Bounds.Width;
                zeropos = (float)(Gauge.Bounds.Height * -control.ScaleRangeMin / (control.ScaleRangeMax - control.ScaleRangeMin));
                ypos = Gauge.Bounds.Height * percent;
            }

            int destX, destY, destW, destH;
            if (Gauge.ControlStyle == CabViewControlStyle.Solid || Gauge.ControlStyle == CabViewControlStyle.Liquid)
            {
                if (control.ScaleRangeMin < 0)
                {
                    if (Gauge.Orientation == 0)
                    {
                        destX = (int)(xratio * control.Bounds.X) + (int)(xratio * (zeropos < xpos ? zeropos : xpos));
                        destY = (int)(yratio * control.Bounds.Y);
                        destY = (int)(yratio * control.Bounds.Y - (int)(yratio * (Gauge.Direction == 0 && zeropos > xpos ? (zeropos - xpos) * Math.Sin(DrawRotation) : 0)));
                        destW = ((int)(xratio * xpos) - (int)(xratio * zeropos)) * (xpos >= zeropos ? 1 : -1);
                        destH = (int)(yratio * ypos);
                    }
                    else
                    {
                        destX = (int)(xratio * control.Bounds.X) + (int)(xratio * (Gauge.Direction == 0 && ypos > zeropos ? (ypos - zeropos) * Math.Sin(DrawRotation) : 0));
                        if (Gauge.Direction != 1 && !fire)
                            destY = (int)(yratio * (control.Bounds.Y + zeropos)) + (ypos > zeropos ? (int)(yratio * (zeropos - ypos)) : 0);
                        else
                            destY = (int)(yratio * (control.Bounds.Y + (zeropos < ypos ? zeropos : ypos)));
                        destW = (int)(xratio * xpos);
                        destH = (int)(yratio * (ypos - zeropos)) * (ypos > zeropos ? 1 : -1);
                    }
                }
                else
                {
                    var topY = control.Bounds.Y;  // top of visible column. +ve Y is downwards
                    if (Gauge.Direction != 0)  // column grows from bottom or from right
                    {
                        if (Gauge.Orientation != 0)
                        {
                            topY += (int)(Gauge.Bounds.Height * (1 - percent));
                            destX = (int)(xratio * (control.Bounds.X + Gauge.Bounds.Width - xpos + ypos * Math.Sin(DrawRotation)));
                        }
                        else
                        {
                            topY -= (int)(xpos * Math.Sin(DrawRotation));
                            destX = (int)(xratio * (control.Bounds.X + Gauge.Bounds.Width - xpos));
                        }
                    }
                    else
                    {
                        destX = (int)(xratio * control.Bounds.X);
                    }
                    destY = (int)(yratio * topY);
                    destW = (int)(xratio * xpos);
                    destH = (int)(yratio * ypos);
                }
            }
            else // pointer gauge using texture
            {
                var topY = control.Bounds.Y;  // top of visible column. +ve Y is downwards
                // even if there is a rotation, we leave the X position unaltered (for small angles Cos(alpha) = 1)
                if (Gauge.Orientation == 0) // gauge horizontal
                {

                    if (Gauge.Direction != 0)  // column grows from right
                    {
                        destX = (int)(xratio * (control.Bounds.X + Gauge.Area.Width - 0.5 * Gauge.Area.Width - xpos));
                        topY -= (int)(xpos * Math.Sin(DrawRotation));
                    }
                    else
                    {
                        destX = (int)(xratio * (control.Bounds.X - 0.5 * Gauge.Area.Width + xpos));
                        topY += (int)(xpos * Math.Sin(DrawRotation));
                    }
                }
                else // gauge vertical
                {
                    // even if there is a rotation, we leave the Y position unaltered (for small angles Cos(alpha) = 1)
                    topY += (int)(ypos - 0.5 * Gauge.Area.Height);
                    if (Gauge.Direction == 0)
                        destX = (int)(xratio * (control.Bounds.X - ypos * Math.Sin(DrawRotation)));
                    else  // column grows from bottom
                    {
                        topY += (int)(Gauge.Area.Height - 2 * ypos);
                        destX = (int)(xratio * (control.Bounds.X + ypos * Math.Sin(DrawRotation)));
                    }
                }
                destY = (int)(yratio * topY);
                destW = (int)(xratio * Gauge.Area.Width);
                destH = (int)(yratio * Gauge.Area.Height);

                // Adjust coal texture height, because it mustn't show up at the bottom of door (see Scotsman)
                // TODO: cut the texture at the bottom instead of stretching
                if (Gauge is CabViewFireboxControl)
                    destH = Math.Min(destH, (int)(yratio * (control.Bounds.Y + 0.5 * Gauge.Area.Height)) - destY);
            }
            if (control.ControlType.CabViewControlType != CabViewControlType.Reverser_Plate && Gauge.ControlStyle != CabViewControlStyle.Pointer)
            {
                if (num < 0 && Gauge.NegativeColors[0].A != 0)
                {
                    if (Gauge.NegativeColors.Length >= 2 && num < Gauge.NegativeTrigger)
                        DrawColor = Gauge.NegativeColors[1];
                    else
                        DrawColor = Gauge.NegativeColors[0];
                }
                else
                {
                    if (Gauge.PositiveColors.Length >= 2 && num > Gauge.PositiveTrigger)
                        DrawColor = Gauge.PositiveColors[1];
                    else
                        DrawColor = Gauge.PositiveColors[0];
                }
            }

            // Cab view vertical position adjusted to allow for clip or stretch.
            destX -= viewer.CabXOffsetPixels;
            destY += viewer.CabYOffsetPixels;

            // Cab view position adjusted to allow for letterboxing.
            destX += viewer.CabXLetterboxPixels;
            destY += viewer.CabYLetterboxPixels;

            DestinationRectangle.X = destX;
            DestinationRectangle.Y = destY;
            DestinationRectangle.Width = destW;
            DestinationRectangle.Height = destH;
            DrawRotation = Gauge.Rotation;
        }

        public override void Draw()
        {
            if (shader != null)
            {
                shader.SetTextureData(DestinationRectangle.Left, DestinationRectangle.Top, DestinationRectangle.Width, DestinationRectangle.Height);
            }
            controlView.SpriteBatch.Draw(texture, DestinationRectangle, SourceRectangle, DrawColor, DrawRotation, Vector2.Zero, SpriteEffects.None, 0);
        }
    }
}
