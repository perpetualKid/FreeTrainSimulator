using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.Graphics.Shaders
{
    public class MaterialManager
    {
        [ThreadStatic]
        private static MaterialManager instance;

        public EnumArray<EffectShader, ShaderEffect> EffectShaders { get; } = new EnumArray<EffectShader, ShaderEffect>();

        private MaterialManager(GraphicsDevice graphicsDevice)
        {
            EffectShaders[ShaderEffect.PopupWindow] = new PopupWindowShader(graphicsDevice);
        }

        public static MaterialManager Instance => instance ?? throw new InvalidOperationException("Need to initialize MaterialManager first!");

        public static MaterialManager Initialize(GraphicsDevice graphicsDevice)
        {
            if (null == instance)
            {
                instance = new MaterialManager(graphicsDevice);
                return instance;
            }
            else
                throw new InvalidOperationException("MaterialManager has already been initialized.");
        }
    }
}
