using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Xna;

namespace Orts.Graphics.Shaders
{
    internal class GraphShader : EffectShader
    {
        public Vector2 ScreenSize 
        {
            set => Parameters["ScreenSize"].SetValue(value);
        }

        public Rectangle Bounds
        {
            set => Parameters["Bounds"].SetValue(value.ToVector4());
        }

        public Vector2 GraphSample
        {
            set => Parameters["GraphSample"].SetValue(value);
        }

        public Color BorderColor
        {
            set => Parameters["BorderColor"].SetValue(value.ToVector4());
        }

        public Color GraphColor
        {
            set => Parameters["GraphColor"].SetValue(value.ToVector4());
        }

        public EffectTechnique GraphTechnique { get; }

        public EffectTechnique BorderTechnique { get; }

        public GraphShader(GraphicsDevice graphicsDevice) : base(graphicsDevice, "Graph")
        {
            GraphTechnique = Techniques["Graph"];
            BorderTechnique = Techniques["Border"];
        }

        public override void SetState()
        {
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
        }

        public override void ResetState()
        {
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }
}
