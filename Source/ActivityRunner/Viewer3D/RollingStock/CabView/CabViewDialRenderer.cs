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

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// Dial Cab Control Renderer
    /// Problems with aspect ratio
    /// </summary>
    public class CabViewDialRenderer : CabViewControlRenderer
    {
        private readonly CabViewDialControl ControlDial;

        /// <summary>
        /// Rotation center point, in unscaled texture coordinates
        /// </summary>
        private readonly Vector2 Origin;

        /// <summary>
        /// Scale factor. Only downscaling is allowed by MSTS, so the value is in 0-1 range
        /// </summary>
        private readonly float Scale = 1;

        /// <summary>
        /// 0° is 12 o'clock, 90° is 3 o'clock
        /// </summary>
        private float Rotation;
        private float ScaleToScreen = 1;

        public CabViewDialRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewDialControl control, CabShader shader)
            : base(viewer, locomotive, control, shader)
        {
            ControlDial = control;

            texture = CABTextureManager.GetTexture(base.control.AceFile, false, false, out nightTexture, cabLightDirectory);
            if (ControlDial.Bounds.Height < texture.Height)
                Scale = (float)ControlDial.Bounds.Height / texture.Height;
            Origin = new Vector2((float)texture.Width / 2, ControlDial.Center / Scale);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var dark = viewer.MaterialManager.sunDirection.Y <= -0.085f || viewer.Camera.IsUnderground;

            texture = CABTextureManager.GetTexture(control.AceFile, dark, locomotive.CabLightOn, out nightTexture, cabLightDirectory);
            if (texture == SharedMaterialManager.MissingTexture)
                return;

            base.PrepareFrame(frame, elapsedTime);

            // Cab view height and vertical position adjusted to allow for clip or stretch.
            // Cab view position adjusted to allow for letterboxing.
            position.X = (float)viewer.CabWidthPixels / 640 * (control.Bounds.X + Origin.X * Scale) - viewer.CabXOffsetPixels + viewer.CabXLetterboxPixels;
            position.Y = (float)viewer.CabHeightPixels / 480 * (control.Bounds.Y + Origin.Y * Scale) + viewer.CabYOffsetPixels + viewer.CabYLetterboxPixels;
            ScaleToScreen = (float)viewer.CabWidthPixels / 640 * Scale;

            var rangeFraction = GetRangeFraction();
            var direction = ControlDial.Direction == 0 ? 1 : -1;
            var rangeDegrees = (int)ControlDial.Direction * (ControlDial.EndAngle - ControlDial.StartAngle);
            while (rangeDegrees <= 0)
                rangeDegrees += 360;
            Rotation = MathHelper.WrapAngle(MathHelper.ToRadians(ControlDial.StartAngle + (int)ControlDial.Direction * rangeDegrees * rangeFraction));
        }

        public override void Draw()
        {
            if (shader != null)
            {
                shader.SetTextureData(position.X, position.Y, texture.Width * ScaleToScreen, texture.Height * ScaleToScreen);
            }
            controlView.SpriteBatch.Draw(texture, position, null, Color.White, Rotation, Origin, ScaleToScreen, SpriteEffects.None, 0);
        }
    }
}
