using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostResources : IMapHostResources
    {
        private readonly MapTextTextureCache ownedTextCache;
        private readonly XnaMapRenderingLifetime renderingLifetime;
        private readonly SpriteBatch spriteBatch;
        private readonly IDisposable hostEnvironment;
        private bool disposed;
        private ExceptionDispatchInfo releaseFailure;

        /// <summary>Takes ownership of acquired map resources; null entries represent incomplete acquisition.</summary>
        public XnaMapHostResources(SpriteBatch spriteBatch, XnaMapRenderingLifetime renderingLifetime, MapTextTextureCache ownedTextCache, IDisposable hostEnvironment)
        {
            this.spriteBatch = spriteBatch;
            this.renderingLifetime = renderingLifetime;
            this.ownedTextCache = ownedTextCache;
            this.hostEnvironment = hostEnvironment;
        }

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
                List<Exception> errors = null;
                // Stop notifications first. Generated textures precede their renderer-owned empty sentinel.
                foreach (IDisposable resource in new IDisposable[] { hostEnvironment, ownedTextCache, renderingLifetime, spriteBatch })
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        (errors ??= new List<Exception>()).Add(error);
                    }
                }
                if (errors != null)
                {
                    releaseFailure = ExceptionDispatchInfo.Capture(new AggregateException(errors));
                    releaseFailure.Throw();
                }
            }
            catch (Exception error)
            {
                releaseFailure = ExceptionDispatchInfo.Capture(error);
                throw;
            }
        }
    }
}
