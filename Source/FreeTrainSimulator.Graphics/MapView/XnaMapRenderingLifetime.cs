using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapRenderingLifetime : IMapRenderingLifetime, IDisposable
    {
        private readonly Game game;
        private readonly TextTextureRenderer textTextureRenderer;
        private readonly BasicShapes basicShapes;
        private bool disposed;
        private ExceptionDispatchInfo releaseFailure;

        public XnaMapRenderingLifetime(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            textTextureRenderer = TextTextureRenderer.Create(game);
            try
            {
                basicShapes = BasicShapes.Create(game.GraphicsDevice);
            }
            catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupError) when (CleanupExceptionPolicy.CanContinue(cleanupError))
                {
                    throw new AggregateException(error, cleanupError);
                }
                throw;
            }
        }

        /// <summary>Releases only the private renderer and textures, never the borrowed TextShape or host services.</summary>
        public void Dispose()
        {
            if (disposed)
            {
                releaseFailure?.Throw();
                return;
            }

            disposed = true;
            try
            {
                List<Exception> errors = new List<Exception>();
                try
                {
                    basicShapes?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                try
                {
                    textTextureRenderer?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                if (errors.Count > 0)
                {
                    releaseFailure = ExceptionDispatchInfo.Capture(errors.Count == 1 ? errors[0] : new AggregateException(errors));
                    releaseFailure.Throw();
                }
            }
            catch (Exception error)
            {
                releaseFailure = ExceptionDispatchInfo.Capture(error);
                throw;
            }
        }

        public BasicShapes GetBasicShapes()
        {
            return basicShapes;
        }

        public TextShape GetTextShape(SpriteBatch spriteBatch)
        {
            return TextShape.Instance(game, spriteBatch);
        }

        public TextTextureRenderer GetTextTextureRenderer()
        {
            return textTextureRenderer;
        }
    }
}
