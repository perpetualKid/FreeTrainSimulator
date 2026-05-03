using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapAdapterBundle
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

        public MapViewAdapterSet AdapterSet { get; }

        public XnaMapAdapterBundle(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
        {
            SpriteBatch = new SpriteBatch(game.GraphicsDevice);
            RenderingLifetime = new XnaMapRenderingLifetime(game);
            RenderingResources = new XnaMapRenderingResources(RenderingLifetime, SpriteBatch);
            OwnedTextCache = new MapTextTextureCache(RenderingLifetime.GetTextTextureRenderer());
            TextCache = OwnedTextCache;
            TextRenderer = new XnaMapTextRenderer(TextCache, SpriteBatch);
            RenderBackend = new XnaMapRenderBackend(SpriteBatch, RenderingResources.BasicShapes, TextRenderer);
            HostEnvironment = new XnaMapHostEnvironment(game, mouseInputGameComponent);
            BasicShapes = RenderingResources.BasicShapes;
            OverlayShapeAdapter = new XnaMapOverlayShapeAdapter(BasicShapes);
            Controller = new MapViewController(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom));
            Controller.SyncViewport(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom), HostEnvironment.ClientSize);

            FontManagerInstance fontManager = FontManager.Scaled("Arial", System.Drawing.FontStyle.Regular);
            System.Drawing.Font constantSizeFont = fontManager[25];
            MapViewStateAdapter viewStateAdapter = new MapViewStateAdapter(Controller, constantSizeFont);
            MapRenderAdapter renderAdapter = new MapRenderAdapter(Controller, RenderBackend);
            MapInteractionAdapter interactionAdapter = new MapInteractionAdapter(fontManager, Controller, HostEnvironment, viewStateAdapter);
            AdapterSet = new MapViewAdapterSet(viewStateAdapter, renderAdapter, interactionAdapter);
        }
    }
}
