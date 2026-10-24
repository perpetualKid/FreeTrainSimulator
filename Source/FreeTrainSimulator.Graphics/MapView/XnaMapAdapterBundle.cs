using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapAdapterBundle : IMapHostAdapterBundle
    {
        public SpriteBatch SpriteBatch { get; }

        public IMapRenderingLifetime RenderingLifetime { get; }

        public IMapRenderingResources RenderingResources { get; }

        public MapTextTextureCache OwnedTextCache { get; }

        public IMapTextCache TextCache { get; }

        public IMapTextRenderer TextRenderer { get; }

        public IMapRenderBackend RenderBackend { get; }

        public IMapHostEnvironment HostEnvironment { get; }

        public BasicShapes BasicShapes { get; }

        public IMapOverlayShapeAdapter OverlayShapeAdapter { get; }

        public IMapViewController Controller { get; }

        public IMapViewStateAdapter ViewStateAdapter { get; }

        public IMapRenderAdapter RenderAdapter { get; }

        public IMapInteractionAdapter InteractionAdapter { get; }

        public IMapHostResources HostResources { get; }

        public XnaMapAdapterBundle(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(mouseInputGameComponent);

            XnaMapRenderingLifetime renderingLifetime = null;
            XnaMapHostEnvironment hostEnvironment = null;
            try
            {
                SpriteBatch = new SpriteBatch(game.GraphicsDevice);
                RenderingLifetime = renderingLifetime = new XnaMapRenderingLifetime(game);
                RenderingResources = new XnaMapRenderingResources(RenderingLifetime, SpriteBatch);
                OwnedTextCache = new MapTextTextureCache(RenderingLifetime.GetTextTextureRenderer());
                TextCache = OwnedTextCache;
                TextRenderer = new XnaMapTextRenderer(TextCache, SpriteBatch);
                RenderBackend = new XnaMapRenderBackend(SpriteBatch, RenderingResources.BasicShapes, TextRenderer);
                hostEnvironment = new XnaMapHostEnvironment(game, mouseInputGameComponent);
                HostEnvironment = hostEnvironment;
                BasicShapes = RenderingResources.BasicShapes;
                OverlayShapeAdapter = new XnaMapOverlayShapeAdapter(BasicShapes);
                Controller = new MapViewController(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom));
                Controller.SyncViewport(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom), HostEnvironment.ClientSize);

                FontManagerInstance fontManager = FontManager.Scaled("Arial", System.Drawing.FontStyle.Regular);
                System.Drawing.Font constantSizeFont = fontManager[25];
                ViewStateAdapter = new MapViewStateAdapter(Controller, constantSizeFont);
                RenderAdapter = new MapRenderAdapter(Controller, RenderBackend);
                InteractionAdapter = new MapInteractionAdapter(fontManager, Controller, HostEnvironment, (IMapViewFontState)ViewStateAdapter);
                HostResources = new XnaMapHostResources(SpriteBatch, renderingLifetime, OwnedTextCache, hostEnvironment);
            }
            catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
            {
                // A failed constructor never reaches the session/candidate owner.
                try
                {
                    new XnaMapHostResources(SpriteBatch, renderingLifetime, OwnedTextCache, hostEnvironment).Dispose();
                }
                catch (Exception cleanupError)
                {
                    throw new MapContentRollbackException(error, cleanupError);
                }
                throw;
            }
        }
    }
}
