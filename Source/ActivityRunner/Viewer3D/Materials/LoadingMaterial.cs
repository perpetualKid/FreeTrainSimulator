using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.ActivityRunner.Viewer3D.Shaders;
using Orts.Common.Info;
using Orts.Common.Xna;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal class LoadingMaterial : Material, IDisposable
    {
        internal readonly LoadingShader shader;
        public readonly Texture2D texture;
        private bool disposedValue;

        public LoadingMaterial(GameHost game)
            : base(game.GraphicsDevice)
        {
            shader = new LoadingShader(game.RenderProcess.GraphicsDevice);
            texture = GetTexture(game);
        }

        public int TextureWidth { get { return texture?.Width ?? 0; } }
        public int TextureHeight { get { return texture?.Height ?? 0; } }

        protected virtual Texture2D GetTexture(GameHost game)
        {
            return SharedTextureManager.Get(game.RenderProcess.GraphicsDevice, Path.Combine(RuntimeInfo.ContentFolder, "Loading.png"));
        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[0]; //["Loading"];
            shader.LoadingTexture = texture;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            for (int i = 0; i < renderItems.Count; i++)
            {
                RenderItem item = renderItems[i];
                MatrixExtension.Multiply(in item.XNAMatrix, in viewProjection, out Matrix wvp);
                shader.WorldViewProjection = wvp;
                //                    shader.WorldViewProjection = item.XNAMatrix * matrices[0] * matrices[1];
                shader.CurrentTechnique.Passes[0].Apply();
                item.RenderPrimitive.Draw();
            }
        }

        public override void ResetState()
        {
            graphicsDevice.BlendState = BlendState.Opaque;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    texture?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
