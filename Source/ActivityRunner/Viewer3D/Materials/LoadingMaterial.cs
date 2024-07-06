using System;
using System.Collections.Generic;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Xna;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D.Shaders;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal class LoadingMaterial : Material, IDisposable
    {
        internal LoadingShader Shader { get; }
        private readonly Texture2D texture;
        private bool disposedValue;

        public LoadingMaterial(GameHost game)
            : this(game, Path.Combine(RuntimeInfo.ContentFolder, "Loading.png"))
        {
        }

        public LoadingMaterial(GameHost game, string texturePath)
            : base(game.GraphicsDevice)
        {
            Shader = new LoadingShader(game.GraphicsDevice);
            texture = TextureManager.GetTextureStatic(texturePath, game);
        }

        public int TextureWidth { get { return texture?.Width ?? 0; } }
        public int TextureHeight { get { return texture?.Height ?? 0; } }

        public override void SetState(Material previousMaterial)
        {
            Shader.CurrentTechnique = Shader.Techniques[0]; //["Loading"];
            Shader.LoadingTexture = texture;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            for (int i = 0; i < renderItems.Count; i++)
            {
                RenderItem item = renderItems[i];
                MatrixExtension.Multiply(in item.XNAMatrix, in viewProjection, out Matrix wvp);
                Shader.WorldViewProjection = wvp;
                //                    shader.WorldViewProjection = item.XNAMatrix * matrices[0] * matrices[1];
                Shader.CurrentTechnique.Passes[0].Apply();
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
