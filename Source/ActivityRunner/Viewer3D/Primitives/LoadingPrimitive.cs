
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D.Materials;

namespace Orts.ActivityRunner.Viewer3D.Primitives
{
    internal class LoadingPrimitive : RenderPrimitive, IDisposable
    {
        public readonly LoadingMaterial Material;
        private readonly VertexBuffer VertexBuffer;
        private bool disposedValue;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public LoadingPrimitive(GameHost game)
        {
            Material = GetMaterial(game);
            var verticies = GetVertices(game);
            VertexBuffer = new VertexBuffer(game.GraphicsDevice, typeof(VertexPositionTexture), verticies.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(verticies);
        }

        protected virtual LoadingMaterial GetMaterial(GameHost game)
        {
            return new LoadingMaterial(game);
        }

        protected virtual VertexPositionTexture[] GetVertices(GameHost game)
        {
            var dd = (float)Material.TextureWidth / 2;
            return new[] {
                    new VertexPositionTexture(new Vector3(-dd - 0.5f, +dd + 0.5f, -3), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(+dd - 0.5f, +dd + 0.5f, -3), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(-dd - 0.5f, -dd + 0.5f, -3), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(+dd - 0.5f, -dd + 0.5f, -3), new Vector2(1, 1)),
                };
        }

        public override void Draw()
        {
            if (disposedValue) 
                return;
            graphicsDevice.SetVertexBuffer(VertexBuffer);
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Material?.Dispose();
                    VertexBuffer?.Dispose();
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
