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

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    /// <summary>
    /// Base class for rendering Cab Controls
    /// </summary>
    public abstract class CabViewControlRenderer : RenderPrimitive
    {
        private protected readonly Viewer viewer;
        private protected readonly MSTSLocomotive locomotive;
        internal protected readonly CabViewControl control;
        private protected readonly CabShader shader;
        private protected readonly int shaderKey = 1;
        private protected readonly CabSpriteBatchMaterial controlView;

        private protected Vector2 position;
        private protected Texture2D texture;
        private protected bool nightTexture;
        private protected bool cabLightDirectory;
        private Matrix Matrix = Matrix.Identity;

        /// <summary>
        /// Determines whether or not the control has power given the state of the cab power supply.
        /// </summary>
        /// <remarks>
        /// For controls that do not depend on the power supply, this will always return true.
        /// </remarks>
        public bool IsPowered
        {
            get
            {
                if (control.DisabledIfLowVoltagePowerSupplyOff)
                    return locomotive.LocomotivePowerSupply.LowVoltagePowerSupplyOn;
                else if (control.DisabledIfCabPowerSupplyOff)
                    return locomotive.LocomotivePowerSupply.CabPowerSupplyOn;
                else
                    return true;
            }
        }

        protected CabViewControlRenderer(Viewer viewer, MSTSLocomotive locomotive, CabViewControl control, CabShader shader)
        {
            this.viewer = viewer;
            this.locomotive = locomotive;
            this.control = control;
            this.shader = shader;

            controlView = (CabSpriteBatchMaterial)viewer.MaterialManager.Load("CabSpriteBatch", null, 0, 0, this.shader);

            cabLightDirectory = CABTextureManager.LoadTextures(this.viewer, this.control.AceFile);
        }

        public CabViewControlType GetControlType()
        {
            return control.ControlType.CabViewControlType;
        }

        /// <summary>
        /// Gets the requested Locomotive data and returns it as a fraction (from 0 to 1) of the range between Min and Max values.
        /// </summary>
        /// <returns>Data value as fraction (from 0 to 1) of the range between Min and Max values</returns>
        public float GetRangeFraction(bool offsetFromZero = false)
        {
            float data = !IsPowered && control.ValueIfDisabled != null ? (float)control.ValueIfDisabled : locomotive.GetDataOf(control);

            if (data < control.ScaleRangeMin)
                return 0;
            if (data > control.ScaleRangeMax)
                return 1;

            if (control.ScaleRangeMax == control.ScaleRangeMin)
                return 0;

            return (float)((data - (offsetFromZero && control.ScaleRangeMin < 0 ? 0 : control.ScaleRangeMin)) / (control.ScaleRangeMax - control.ScaleRangeMin));
        }

        public CabViewControlStyle Style => control.ControlStyle;

        public virtual void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (!IsPowered && control.HideIfDisabled)
                return;

            frame.AddPrimitive(controlView, this, RenderPrimitiveGroup.Cab, ref Matrix);
        }

        internal void Mark()
        {
            viewer.TextureManager.Mark(texture);
        }
    }
}
