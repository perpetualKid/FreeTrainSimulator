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

// Define this to include extra data on loading performance and progress indications.
//#define DEBUG_LOADING

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.ActivityRunner.Viewer3D.Shaders
{
    internal sealed class LoadingShader : BaseShader
    {
        private readonly EffectParameter worldViewProjection;
        private readonly EffectParameter loadingPercent;
        private readonly EffectParameter loadingTexture;

        public Matrix WorldViewProjection { set => worldViewProjection.SetValue(value); }

        public float LoadingPercent { set => loadingPercent.SetValue(value); }

        public Texture2D LoadingTexture { set => loadingTexture.SetValue(value); }

        public LoadingShader(GraphicsDevice graphicsDevice)
            : base(graphicsDevice, "Loading")
        {
            worldViewProjection = Parameters["WorldViewProjection"];
            loadingPercent = Parameters["LoadingPercent"];
            loadingTexture = Parameters["LoadingTexture"];
        }
    }
}
