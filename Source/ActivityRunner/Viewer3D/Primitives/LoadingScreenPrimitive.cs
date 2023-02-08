﻿
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D.Materials;

namespace Orts.ActivityRunner.Viewer3D.Primitives
{
    internal class LoadingScreenPrimitive : LoadingPrimitive
    {
        public LoadingScreenPrimitive(GameHost game)
            : base(game)
        {
        }

        protected override LoadingMaterial GetMaterial(GameHost game)
        {
            return new LoadingScreenMaterial(game);
        }

        protected override VertexPositionTexture[] GetVertices(GameHost game)
        {
            float w, h;

            w = Material.TextureWidth;
            h = Material.TextureHeight;
            if (w != 0 && h != 0)
            {
                var scaleX = (float)game.RenderProcess.DisplaySize.X / w;
                var scaleY = (float)game.RenderProcess.DisplaySize.Y / h;
                var scale = scaleX < scaleY ? scaleX : scaleY;
                w = w * scale / 2;
                h = h * scale / 2;
            }
            return new[] {
                    new VertexPositionTexture(new Vector3(-w - 0.5f, +h + 0.5f, -2), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(+w - 0.5f, +h + 0.5f, -2), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(-w - 0.5f, -h + 0.5f, -2), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(+w - 0.5f, -h + 0.5f, -2), new Vector2(1, 1)),
                };
        }
    }

}
