using System;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.Graphics.Shaders
{
    public class MaterialManager
    {
        public EnumArray<EffectShader, ShaderEffect> EffectShaders { get; } = new EnumArray<EffectShader, ShaderEffect>();

        private MaterialManager(GraphicsDevice graphicsDevice)
        {
            EffectShaders[ShaderEffect.PopupWindow] = new PopupWindowShader(graphicsDevice);
            EffectShaders[ShaderEffect.Diagram] = new GraphShader(graphicsDevice);
        }

        public static MaterialManager Instance(Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            MaterialManager result;
            if ((result = game.Services.GetService<MaterialManager>()) == null)
            {
                game.Services.AddService(result = new MaterialManager(game.GraphicsDevice));
            }
            return result;
        }
    }
}
